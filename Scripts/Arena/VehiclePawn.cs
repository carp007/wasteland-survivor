// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/VehiclePawn.cs
// Purpose: 3D vehicle pawn (CharacterBody3D). Handles movement/handling, visuals, hitboxes, weapon mounts/muzzles, and exposes audio telemetry.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using GamePawnKit.Pawns;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game.Systems;
using WastelandSurvivor.Game.Audio;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Minimal 3D vehicle pawn for the early 2.5D transition.
/// Kinematic-ish car movement on the XZ plane (Y up), using CharacterBody3D.
/// </summary>
public partial class VehiclePawn : VehiclePawnBase, IVehicleAudioTelemetry
{

	// -------------------------------------------------------------------------------------------------
	// File navigation (high level)
	// - ConfigureLoadout(): connect DefDatabase + VehicleInstanceState to mounts/weapon visuals
	// - _PhysicsProcess(): movement/handling (bicycle model + friction/drag) from ThrottleInput/SteerInput
	// - Ensure*(): lazily builds visuals, hitboxes, mounts, and engine audio child nodes
	// - Damage/VFX: section+tire hitboxes, blown-tire smoke/skid hooks
	// - Audio: implements IVehicleAudioTelemetry for VehicleEngineAudio
	// -------------------------------------------------------------------------------------------------

	// --- Visuals (Passenger Car Pack) ---
	[Export] public bool UsePassengerCarPackVisual = true;
	[Export(PropertyHint.File, "*.gltf,*.glb,*.tscn")] public string PassengerCarPackScenePath = "res://Assets/Models/Vehicles/GenericPassengerCarPack/scene.gltf";
	[Export] public string PassengerCarPackBodyNodeName = "Compact Body";
	[Export] public string PassengerCarPackWheelRootPrefix = "Wheel_C"; // Wheel_C, Wheel_C001, Wheel_C002, Wheel_C003
	[Export] public int PassengerCarPackPlayerVariantIndex = 1; // Body 1 for player
	[Export] public int PassengerCarPackEnemyVariantIndex = 2;  // Body 2 (dark red) reads hostile
	[Export] public float PassengerCarPackTargetLength = 3.25f; // roughly matches proxy length
	[Export] public float PassengerCarPackMinVisibleLength = 0.5f; // if model imports tiny, we autoscale
	[Export] public bool PassengerCarPackAutoAlignYaw = true; // attempts to align model long axis to vehicle forward (-Z)
	// When true, logs model path + computed alignment vectors/angles to the Godot Output.
	[Export] public bool PassengerCarPackDebugAlignment = true;
	[Export] public Vector3 PassengerCarPackRotationDegrees = Vector3.Zero; // tweak if imported forward axis is off
	[Export] public Vector3 PassengerCarPackExtraScale = Vector3.One; // extra user scale multiplier

	[Export] public Color BodyColor = new(0.85f, 0.85f, 0.85f);

	/// <summary>
	/// Hostile faction treatment (round 11 N-4): enemy combatants get a subtle constant red
	/// edge/skirt emission on the hull, a luma floor under dark preset paints, and a hostile-red
	/// underglow — so an enemy chassis reads at 1x between the light pools instead of leaving the
	/// yellow target brackets to carry the entire hostile read. Set by the arena spawner BEFORE
	/// ApplyVisualPreset/ConfigureLoadout; false for the player, trailers, towed rigs and wrecks.
	/// </summary>
	public bool HostileIdentity { get; set; }

	// Weapon fire cooldown.
	// NOTE: This remains WastelandSurvivor-specific (firing logic lives in ArenaRealtimeView).
	// It was temporarily dropped during the VehiclePawnBase extraction; keep it here to preserve behavior.
	public float FireCooldownSeconds { get; set; } = 0.22f;
	public float FireCooldownRemaining { get; set; } = 0f;

	// Per-weapon-slot cooldowns: holding fire group 1 must not starve groups 2/3 (mines/missiles).
	private readonly System.Collections.Generic.Dictionary<int, float> _slotCooldowns = new();

	public float GetSlotCooldown(int slot)
		=> _slotCooldowns.TryGetValue(slot, out var v) ? v : 0f;

	public void SetSlotCooldown(int slot, float seconds)
		=> _slotCooldowns[slot] = Mathf.Max(0f, seconds);

	// Optional per-pawn model override (enemy tier visual identity from ArenaEnemyBuildPreset).
	// When set it wins over VehicleDefinition.VisualModelPath. See ApplyVisualPreset().
	public string? VisualModelPathOverride { get; set; }

	private Node3D? _visualRoot;
	private bool _usingPassengerCarPackVisual;
	// Per-class model driven by VehicleDefinition.VisualModelPath (Kenney Car Kit et al.).
	private bool _usingDefModelVisual;
	private string? _activeDefModelPath;
	// Target length the active def model was scaled against; a rebuild is needed when the def
	// arrives after the model was built (override models can build before ConfigureLoadout).
	private float _activeDefModelTargetLen;
	private readonly List<MeshInstance3D> _bodyMeshes = new();
	private Node3D? _hitboxes;

	// Tire VFX (blown tires)
	private readonly bool[] _blownTires = new bool[4];
	private readonly float[] _smokeCooldown = new float[4];
	private readonly float[] _skidCooldown = new float[4];
	private ArenaWorld? _arenaWorldCached;

	// Audio
	private VehicleEngineAudio? _engineAudio;
	private VehicleContactAudio? _contactAudio;

	// Wheels (visual steering)
	private Node3D? _wheelFlPivot;
	private Node3D? _wheelFrPivot;
	private Node3D? _wheelRlPivot;
	private Node3D? _wheelRrPivot;
	private float _frontWheelSteerDeg = 0f;

	// Mounts/weapons
	private Node3D? _mountsRoot;
	private Node3D? _weaponsRoot;
	private readonly Dictionary<string, Marker3D> _mountById = new();
	private readonly Dictionary<string, Marker3D> _muzzleByMountId = new();
	private readonly Dictionary<string, TurretInfo> _turretsByMountId = new();
	private Marker3D? _primaryMuzzle;
	private string? _primaryMountId;

	private VehicleDefinition? _vehicleDef;
	private VehicleInstanceState? _vehicleRuntime;
	private DefDatabase? _defs;

	private sealed class TurretInfo
	{
		public Node3D Pivot = null!;
		public float? MinYawDeg;
		public float? MaxYawDeg;
		/// <summary>Vehicle-local yaw of the mount's neutral (mount-forward) bearing.</summary>
		public float NeutralYawDeg;
	}

	/// <summary>
	/// Turret mounts whose weapon sits beyond the installed targeting computer's control-group
	/// capacity (master spec): they stop auto-tracking and lock to their neutral bearing, behaving
	/// like fixed mounts. Recomputed whenever the loadout/runtime state is (re)configured.
	/// </summary>
	private readonly HashSet<string> _computerLockedMountIds = new(StringComparer.OrdinalIgnoreCase);

	public override void _Ready()
	{
		base._Ready();

		EnsureVisualAndCollision();
		ApplyBodyColor();
		EnsureIdentityVisuals();
		EnsureHitboxes();
		// Default mounts so a pawn is usable even before ConfigureLoadout() is called.
		EnsureMountsFallback();
		_arenaWorldCached = FindArenaWorld();

		EnsureEngineAudio();
		EnsureContactAudio();
	}

	/// <summary>
	/// Arena code frequently replaces the VehicleInstanceState record (immutable "with" updates).
	/// This lets the pawn stay in sync without rebuilding visuals.
	/// </summary>
	public void SetRuntimeState(VehicleInstanceState runtime)
	{
		_vehicleRuntime = runtime;
		// If the pawn was handed a different vehicle definition (ex: the player claims the enemy
		// vehicle mid-encounter), re-run the loadout so the per-class visual/mounts follow the def.
		if (_defs != null && _vehicleDef != null
			&& !string.IsNullOrWhiteSpace(runtime.DefinitionId)
			&& !string.Equals(_vehicleDef.Id, runtime.DefinitionId, StringComparison.OrdinalIgnoreCase)
			&& _defs.Vehicles.ContainsKey(runtime.DefinitionId))
		{
			ConfigureLoadout(_defs, runtime);
			return;
		}
		RefreshComputerControlLocks();
		ApplyEngineAudioArchetype();
	}

	/// <summary>
	/// Configure this pawn with the active vehicle runtime state so it can mount weapon visuals
	/// and drive turret aiming/muzzle placement.
	/// Safe to call multiple times.
	/// </summary>
	public void ConfigureLoadout(DefDatabase defs, VehicleInstanceState runtime)
	{
		_defs = defs;
		_vehicleRuntime = runtime;
		VehicleDefinition? vdef = null;
		if (defs.Vehicles.TryGetValue(runtime.DefinitionId, out var found))
			vdef = found;
		else
			vdef = defs.Vehicles.Values.FirstOrDefault();
		_vehicleDef = vdef;

		// Swap in the per-class model from the definition (no-op when unset or when the pawn is
		// still off-tree; _Ready()/EnsureVisualAndCollision() honors the def path in that case).
		EnsureDefModelVisual();

		if (_vehicleDef != null)
			EnsureMountPoints(_vehicleDef);
		AttachWeaponVisuals();
		RefreshComputerControlLocks();
		ApplyBodyColor();
		EnsureIdentityVisuals();
		ApplyEngineAudioArchetype();
	}

	/// <summary>
	/// Master-spec fixed-angle fallback: weapons beyond the installed computer's control-group
	/// capacity keep firing but lose tracking — their turret mounts lock to the neutral bearing.
	/// Applies identically to player and AI pawns (both run through this class).
	/// </summary>
	private void RefreshComputerControlLocks()
	{
		_computerLockedMountIds.Clear();
		if (_defs == null || _vehicleRuntime == null)
			return;

		foreach (var mountId in _vehicleRuntime.InstalledWeaponsByMountId.Keys)
		{
			if (!_turretsByMountId.ContainsKey(mountId))
				continue;
			if (!ArenaWeaponLoadoutResolver.IsWeaponComputerControlled(_defs, _vehicleRuntime, mountId, out _))
				_computerLockedMountIds.Add(mountId);
		}
	}

	/// <summary>
	/// Preset-driven visual identity (per-tier enemy skins): optional model path override and
	/// body color ("#rrggbb"). Empty/invalid values leave the def model / current BodyColor
	/// untouched. Preferably called before ConfigureLoadout(); safe afterwards too (rebuilds
	/// the visual in place).
	/// </summary>
	public void ApplyVisualPreset(string? visualModelPathOverride, string? bodyColorHex)
	{
		if (!string.IsNullOrWhiteSpace(visualModelPathOverride))
			VisualModelPathOverride = visualModelPathOverride;

		if (!string.IsNullOrWhiteSpace(bodyColorHex) && Color.HtmlIsValid(bodyColorHex))
			BodyColor = Color.FromHtml(bodyColorHex);

		// Live-apply when the pawn already built its visuals; pre-_Ready the stored values are
		// honored by EnsureVisualAndCollision()/ConfigureLoadout().
		if (IsInsideTree() && _visualRoot != null)
		{
			EnsureDefModelVisual();
			ApplyBodyColor();
			EnsureIdentityVisuals();
		}
	}

	/// <summary>Model path for the def-model visual: per-pawn override wins over the definition.</summary>
	private string? ResolveVisualModelPath()
		=> !string.IsNullOrWhiteSpace(VisualModelPathOverride) ? VisualModelPathOverride : _vehicleDef?.VisualModelPath;

	private float CurrentDefModelTargetLength()
		=> _vehicleDef != null && _vehicleDef.VisualTargetLength > 0.01f
			? _vehicleDef.VisualTargetLength
			: PassengerCarPackTargetLength;

	

private void EnsureEngineAudio()
{
	if (_engineAudio != null && GodotObject.IsInstanceValid(_engineAudio)) return;

	VehicleEngineAudio? node = null;
	try
	{
		var ps = GD.Load<PackedScene>("res://Scenes/Audio/VehicleEngineAudio.tscn");
		if (ps != null)
			node = ps.Instantiate() as VehicleEngineAudio;
	}
	catch
	{
		// Ignore and fall back to code-only node.
	}

	node ??= new VehicleEngineAudio();
	node.Name = "EngineAudio";
	AddChild(node);
	node.SetTelemetrySource(this);
	_engineAudio = node;

	ApplyEngineAudioArchetype();
}

private void ApplyEngineAudioArchetype()
{
	if (_engineAudio == null || !GodotObject.IsInstanceValid(_engineAudio)) return;
	var archetype = ComputeEngineArchetypeId();
	_engineAudio.SetArchetype(archetype);
}

private void EnsureContactAudio()
{
	if (_contactAudio != null && GodotObject.IsInstanceValid(_contactAudio)) return;
	var node = new VehicleContactAudio { Name = "ContactAudio" };
	// Child sits at the pawn origin; its pooled 3D players inherit the vehicle position.
	AddChild(node);
	_contactAudio = node;
}

/// <summary>
/// Detects hard contacts after MoveAndSlide: when slide collisions removed enough velocity this
/// tick (ram/wall crash), plays a tiered crash one-shot. Low-delta wall scrapes stay silent, and
/// per-tick throttle/brake/drag changes (&lt;~1 m/s at 60 Hz) sit well under the light threshold.
/// </summary>
private void NotifyCrashAudioAfterMove(Vector3 preMoveVelocity)
{
	if (_contactAudio == null || !GodotObject.IsInstanceValid(_contactAudio)) return;
	// Death/possession changes zero Velocity without calling MoveAndSlide (stale slide data).
	if (IsDead) return;
	if (GetSlideCollisionCount() <= 0) return;

	var speedLoss = (preMoveVelocity - Velocity).Length();
	if (speedLoss < VehicleContactAudio.LightImpactSpeedLoss) return;
	_contactAudio.NotifyImpact(speedLoss);
}

private string ComputeEngineArchetypeId()
{
	// Prefer the installed engine's fuel type when available; otherwise fall back to vehicle class.
	if (_defs != null && _vehicleRuntime != null && !string.IsNullOrWhiteSpace(_vehicleRuntime.InstalledEngineId))
	{
		if (_defs.Engines.TryGetValue(_vehicleRuntime.InstalledEngineId!, out var eng))
		{
			if (eng.FuelType == FuelType.Diesel) return "diesel_truck";
			if (eng.FuelType == FuelType.Electric) return "ev"; // staged CC0 whine set (5 RPM layers)
			// Gas follows displacement, not chassis: a Gas V6 in a light truck must not sound diesel.
			if (eng.FuelType == FuelType.Gas)
				return eng.PowerKw >= 100f ? "v8_muscle" : "i4_compact";

		}

	}

	if (_vehicleDef != null)
	{
		return _vehicleDef.Class switch
		{
			VehicleClass.Compact => "i4_compact",
			VehicleClass.LightTruck => "diesel_truck",
			VehicleClass.HeavyTruck => "diesel_truck",
			VehicleClass.SemiTruck => "diesel_truck",
			_ => "v8_muscle",
		};
	}

	return "v8_muscle";
}

private void EnsureVisualAndCollision()
	{
		// Keep the .tscn minimal; ensure required child nodes exist.
		var col = GetNodeOrNull<CollisionShape3D>("Collision");
		if (col == null)
		{
			col = new CollisionShape3D { Name = "Collision" };
			AddChild(col);
		}
		if (col.Shape == null)
		{
			col.Shape = new BoxShape3D { Size = new Vector3(1.8f, 0.8f, 3.2f) };
		}


		// Vehicle visuals (proxy by default; optionally use imported passenger car pack).
		_visualRoot = GetNodeOrNull<Node3D>("Visual");
		if (_visualRoot == null)
		{
			_visualRoot = new Node3D { Name = "Visual" };
			AddChild(_visualRoot);
		}

		// If an older build left BodyMesh around, remove it.
		var legacy = GetNodeOrNull<MeshInstance3D>("BodyMesh");
		if (legacy != null)
			legacy.QueueFree();

		_usingPassengerCarPackVisual = false;
		_usingDefModelVisual = false;

		// Per-class model from the vehicle definition (or the per-pawn override) takes precedence
		// when configured (set via ConfigureLoadout()/ApplyVisualPreset(); may already be known
		// when _Ready runs, ex: HUD preview pawns).
		var defModelPath = ResolveVisualModelPath();
		if (!string.IsNullOrWhiteSpace(defModelPath))
		{
			var existingDefModel = _visualRoot.GetNodeOrNull<Node>("DefVehicleModel");
			if (existingDefModel != null && GodotObject.IsInstanceValid(existingDefModel)
				&& string.Equals(_activeDefModelPath, defModelPath, StringComparison.OrdinalIgnoreCase)
				&& Mathf.IsEqualApprox(_activeDefModelTargetLen, CurrentDefModelTargetLength()))
			{
				_usingDefModelVisual = true;
			}
			else if (TryBuildDefModelVisual(_visualRoot, defModelPath!))
			{
				_usingDefModelVisual = true;
				_activeDefModelPath = defModelPath;
			}
		}

		// Trailers have no kit model — and the generic passenger-car fallback rendered them as a
		// CAR riding the hitch (wrong fiction in every haul/garage beat). Dedicated flatbed proxy.
		if (!_usingDefModelVisual && _vehicleDef?.Class == VehicleClass.Trailer)
		{
			if (_visualRoot.GetNodeOrNull<Node>("TrailerProxy") == null)
			{
				ClearChildren(_visualRoot);
				BuildProxyTrailerVisual(_visualRoot, _vehicleDef.TireCount);
			}
			return;
		}

		if (!_usingDefModelVisual && UsePassengerCarPackVisual)
		{
			// If the model isn't already present, rebuild visuals.
			var existing = _visualRoot.GetNodeOrNull<Node>("PassengerCarModel");
			if (existing == null || !GodotObject.IsInstanceValid(existing))
			{
				ClearChildren(_visualRoot);
				if (!TryBuildPassengerCarPackVisual(_visualRoot))
				{
					// Fallback for robustness.
					ClearChildren(_visualRoot);
					BuildProxyVehicleVisual(_visualRoot);
				}
			}
		}

		if (_visualRoot.GetChildCount() == 0)
			BuildProxyVehicleVisual(_visualRoot);
	}

	/// <summary>
	/// Procedural flatbed trailer: plank deck + side rails + A-frame drawbar with coupler +
	/// axle-mounted wheels (single axle for 2 tires, tandem for 4). The 4-tire cargo variant
	/// carries a strapped crate; the utility variant carries a flat spare tire + toolbox so the
	/// deck isn't an empty slab. Rust-industrial palette, ignores team tint.
	/// Top-face read matters most: the sacred near-top-down camera only ever sees the deck plane,
	/// so the read comes from crosswise plank tone alternation, dark wear runners, a red/white
	/// hazard bar at the rear, and fender tabs marking the axles — not from side detail.
	/// </summary>
	private static void BuildProxyTrailerVisual(Node3D parent, int tireCount)
	{
		var root = new Node3D { Name = "TrailerProxy" };
		parent.AddChild(root);

		// Dark non-specular steel: the old metallic 0.55 frame caught the arena keys as bright
		// white specular sticks at gameplay zoom (rails read as disconnected white posts).
		var steel = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.135f, 0.14f, 0.155f),
			Roughness = 0.68f,
			Metallic = 0.30f,
		};
		var rubber = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.085f, 0.088f, 0.092f),
			Roughness = 0.96f,
		};
		// Weathered wood tones for the plank alternation (kept desaturated so the arena warm
		// keys don't push the deck to milk-chocolate).
		var woodTones = new[]
		{
			new Color(0.335f, 0.295f, 0.235f),
			new Color(0.265f, 0.235f, 0.185f),
			new Color(0.305f, 0.26f, 0.20f),
			new Color(0.225f, 0.20f, 0.16f),
		};
		var woodMats = new StandardMaterial3D[woodTones.Length];
		for (int i = 0; i < woodTones.Length; i++)
			woodMats[i] = new StandardMaterial3D { AlbedoColor = woodTones[i], Roughness = 0.94f, Metallic = 0.02f };
		var wearMat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.135f, 0.12f, 0.10f),
			Roughness = 0.90f,
		};
		var hazardRed = new StandardMaterial3D { AlbedoColor = new Color(0.58f, 0.13f, 0.10f), Roughness = 0.62f };
		var hazardWhite = new StandardMaterial3D { AlbedoColor = new Color(0.68f, 0.66f, 0.60f), Roughness = 0.62f };

		void AddBox(string name, Vector3 size, Vector3 pos, Material mat, Vector3? rotDeg = null)
		{
			var mesh = new MeshInstance3D
			{
				Name = name,
				Mesh = new BoxMesh { Size = size },
				Position = pos,
				RotationDegrees = rotDeg ?? Vector3.Zero,
			};
			mesh.SetSurfaceOverrideMaterial(0, mat);
			root.AddChild(mesh);
		}

		var tandem = tireCount >= 4;
		var deckLen = tandem ? 3.2f : 2.5f;
		var deckZ0 = -deckLen * 0.5f;

		// Structural deck base (mostly hidden under the planks; keeps gaps from showing floor).
		AddBox("Deck", new Vector3(1.85f, 0.08f, deckLen), new Vector3(0f, 0.55f, 0f), woodMats[3]);

		// Crosswise planks with deterministic tone alternation — the parallel "ladder" lines are
		// the strongest top-down "this is a flatbed deck" cue.
		const float plankPitch = 0.25f;
		const float plankGap = 0.022f;
		var plankCount = Math.Max(6, (int)MathF.Round(deckLen / plankPitch));
		for (int i = 0; i < plankCount; i++)
		{
			var z = deckZ0 + (i + 0.5f) * (deckLen / plankCount);
			// Non-repeating-looking but deterministic tone pick.
			var mat = woodMats[(i * 5 + (i / 3)) % woodTones.Length];
			AddBox($"Plank_{i}", new Vector3(1.79f, 0.045f, deckLen / plankCount - plankGap),
				new Vector3(0f, 0.605f, z), mat);
		}

		// Two dark lengthwise wear runners (cargo/boot scuff lines) breaking the plank rhythm.
		foreach (var sx in new[] { -1f, 1f })
			AddBox($"WearRunner_{sx}", new Vector3(0.15f, 0.012f, deckLen * 0.94f),
				new Vector3(sx * 0.50f, 0.632f, 0f), wearMat);

		// Perimeter rails + corner/mid stake posts (posts read as dark dots punctuating the
		// outline from above).
		AddBox("RailL", new Vector3(0.08f, 0.26f, deckLen), new Vector3(-0.92f, 0.72f, 0f), steel);
		AddBox("RailR", new Vector3(0.08f, 0.26f, deckLen), new Vector3(0.92f, 0.72f, 0f), steel);
		AddBox("RailF", new Vector3(1.85f, 0.26f, 0.08f), new Vector3(0f, 0.72f, deckZ0), steel);
		AddBox("RailB", new Vector3(1.85f, 0.26f, 0.08f), new Vector3(0f, 0.72f, -deckZ0), steel);
		var stakeZs = tandem
			? new[] { deckZ0 + 0.10f, deckZ0 + deckLen * 0.5f, -deckZ0 - 0.10f }
			: new[] { deckZ0 + 0.10f, -deckZ0 - 0.10f };
		foreach (var sz in stakeZs)
			foreach (var sx in new[] { -0.92f, 0.92f })
				AddBox($"Stake_{sx}_{sz:0.0}", new Vector3(0.11f, 0.42f, 0.11f), new Vector3(sx, 0.78f, sz), steel);

		// Rear hazard bar: alternating red/white segments along the tail rail — the classic
		// "you are following a trailer" cue, and it marks the rear at any yaw.
		const int hazardSegs = 7;
		for (int i = 0; i < hazardSegs; i++)
		{
			var segW = 1.72f / hazardSegs;
			var x = -0.86f + (i + 0.5f) * segW;
			AddBox($"Hazard_{i}", new Vector3(segW - 0.02f, 0.10f, 0.10f),
				new Vector3(x, 0.87f, -deckZ0), (i % 2 == 0) ? hazardRed : hazardWhite);
		}

		// A-frame drawbar to the coupler (forward is -Z).
		var couplerZ = deckZ0 - 0.85f;
		AddBox("DrawbarL", new Vector3(0.08f, 0.08f, 1.05f), new Vector3(-0.38f, 0.52f, deckZ0 - 0.42f), steel, new Vector3(0f, -22f, 0f));
		AddBox("DrawbarR", new Vector3(0.08f, 0.08f, 1.05f), new Vector3(0.38f, 0.52f, deckZ0 - 0.42f), steel, new Vector3(0f, 22f, 0f));
		var coupler = new MeshInstance3D
		{
			Name = "Coupler",
			Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.16f, RadialSegments = 10 },
			Position = new Vector3(0f, 0.52f, couplerZ),
		};
		coupler.SetSurfaceOverrideMaterial(0, steel);
		root.AddChild(coupler);

		// Axles + wheels + fender tabs (fenders sit above the wheel tops, so from the top-down
		// camera the axle positions read as dark side tabs breaking the deck outline).
		var axleZs = tandem ? new[] { 0.35f, 0.95f } : new[] { 0.25f };
		foreach (var az in axleZs)
		{
			var axle = new MeshInstance3D
			{
				Name = $"Axle_{az:0.0}",
				Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = 1.9f, RadialSegments = 8 },
				Position = new Vector3(0f, 0.33f, az),
				RotationDegrees = new Vector3(0f, 0f, 90f),
			};
			axle.SetSurfaceOverrideMaterial(0, steel);
			root.AddChild(axle);

			foreach (var sx in new[] { -1f, 1f })
			{
				var wheel = new MeshInstance3D
				{
					Name = $"TrailerWheel_{sx}_{az:0.0}",
					Mesh = new CylinderMesh { TopRadius = 0.33f, BottomRadius = 0.33f, Height = 0.22f, RadialSegments = 14 },
					Position = new Vector3(sx * 0.98f, 0.33f, az),
					RotationDegrees = new Vector3(0f, 0f, 90f),
				};
				wheel.SetSurfaceOverrideMaterial(0, rubber);
				root.AddChild(wheel);
			}
		}
		var fenderLen = tandem ? 1.55f : 0.90f;
		var fenderZ = tandem ? (axleZs[0] + axleZs[1]) * 0.5f : axleZs[0];
		// Dull dark galvanized — a specular fender top face outshines the whole deck under the
		// arena keys at the near-top-down camera.
		var fenderMat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.155f, 0.16f, 0.165f),
			Roughness = 0.88f,
			Metallic = 0.05f,
		};
		foreach (var sx in new[] { -1f, 1f })
			AddBox($"Fender_{sx}", new Vector3(0.30f, 0.045f, fenderLen),
				new Vector3(sx * 0.99f, 0.70f, fenderZ), fenderMat);

		if (tandem)
		{
			// Cargo crate on the tandem hauler — a loaded trailer should read as loaded. Straps in
			// near-black webbing (the old steel-toned straps vanished against the crate).
			AddBox("Crate", new Vector3(1.30f, 0.60f, 1.60f), new Vector3(0f, 0.92f, 0.30f), new StandardMaterial3D
			{
				AlbedoColor = new Color(0.33f, 0.34f, 0.26f),
				Roughness = 0.92f,
			});
			var strapMat = new StandardMaterial3D { AlbedoColor = new Color(0.09f, 0.09f, 0.10f), Roughness = 0.88f };
			AddBox("StrapA", new Vector3(1.36f, 0.64f, 0.06f), new Vector3(0f, 0.92f, -0.10f), strapMat);
			AddBox("StrapB", new Vector3(1.36f, 0.64f, 0.06f), new Vector3(0f, 0.92f, 0.72f), strapMat);
		}
		else
		{
			// Utility deck cargo: a flat-lying spare tire (instant top-down ring read) + toolbox.
			var spare = new MeshInstance3D
			{
				Name = "SpareTire",
				Mesh = new TorusMesh { InnerRadius = 0.10f, OuterRadius = 0.27f, Rings = 20, RingSegments = 8 },
				Position = new Vector3(-0.36f, 0.665f, deckZ0 + 0.52f),
			};
			spare.SetSurfaceOverrideMaterial(0, rubber);
			root.AddChild(spare);
			var hub = new MeshInstance3D
			{
				Name = "SpareHub",
				Mesh = new CylinderMesh { TopRadius = 0.10f, BottomRadius = 0.10f, Height = 0.06f, RadialSegments = 10 },
				Position = new Vector3(-0.36f, 0.665f, deckZ0 + 0.52f),
			};
			hub.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
			{
				AlbedoColor = new Color(0.38f, 0.37f, 0.34f),
				Roughness = 0.55f,
				Metallic = 0.25f,
			});
			root.AddChild(hub);
			AddBox("Toolbox", new Vector3(0.52f, 0.20f, 0.32f), new Vector3(0.42f, 0.73f, deckZ0 + 0.50f),
				new StandardMaterial3D { AlbedoColor = new Color(0.30f, 0.16f, 0.09f), Roughness = 0.78f });
			AddBox("ToolboxLid", new Vector3(0.54f, 0.03f, 0.34f), new Vector3(0.42f, 0.845f, deckZ0 + 0.50f),
				new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.20f, 0.11f), Roughness = 0.70f });
		}
	}

	// Transient "slippery surface" timer (seconds). While > 0 the per-frame grip computation in
	// UpdateRuntimeDerivedStats applies an oil-slick slip on top of the tire-condition grip. The arena
	// refreshes this each frame the vehicle is over an active oil slick (see ArenaRealtimeView.UpdateOilSlicks).
	private float _oilSlipRemaining;

	/// <summary>Apply (or refresh) an oil-slick slip for at least <paramref name="seconds"/> seconds.</summary>
	public void ApplyOilSlick(float seconds)
	{
		if (seconds <= 0f) return;
		_oilSlipRemaining = MathF.Max(_oilSlipRemaining, seconds);
	}

	/// <summary>True while the vehicle is sliding on an oil slick (drives HUD/AI/VFX hints).</summary>
	public bool IsOnOilSlick => _oilSlipRemaining > 0f;

	public override void _PhysicsProcess(double delta)
	{
		var dt = (float)delta;
		FireCooldownRemaining = Mathf.Max(0f, FireCooldownRemaining - dt);
		if (_slotCooldowns.Count > 0)
		{
			foreach (var key in System.Linq.Enumerable.ToArray(_slotCooldowns.Keys))
				_slotCooldowns[key] = Mathf.Max(0f, _slotCooldowns[key] - dt);
		}
		// Decay the oil-slip timer before movement so this frame's grip reflects the current surface.
		_oilSlipRemaining = MathF.Max(0f, _oilSlipRemaining - dt);

		// Capture the pre-step velocity so we can measure how much speed slide collisions removed.
		var preMoveVelocity = Velocity;
		var step = StepVehicleMovement(dt);
		NotifyCrashAudioAfterMove(preMoveVelocity);

		ResolveVehicleOverlap();

		// Keep grounded (collision box height is 0.8, so half-height is 0.4).
		var p = GlobalPosition;
		GlobalPosition = new Vector3(p.X, 0.4f, p.Z);

		// Blown tire visuals (sparks + smoke + simple skid marks).
		var vNow = Velocity;
		var vF = vNow.Dot(step.Forward);
		var vL = vNow.Dot(step.Right);
		UpdateTireVfx(dt, vF, vL);
		// Skid-loop audio follows the same lateral-slip decomposition the tire VFX uses.
		_contactAudio?.UpdateContact(dt, vF, vL);

		UpdateFrontWheelSteer(dt, step.SteerRad);
		UpdateTurrets();
	}

	public Vector3 GetMuzzleWorldPosition()
	{
		if (_primaryMuzzle != null && GodotObject.IsInstanceValid(_primaryMuzzle))
			return _primaryMuzzle.GlobalPosition;

		var forward = -GlobalTransform.Basis.Z;
		forward.Y = 0f;
		forward = forward.Normalized();
		return GlobalPosition + forward * 1.6f + Vector3.Up * 0.35f;
	}

	public Vector3 GetMuzzleWorldForward()
	{
		if (_primaryMuzzle != null && GodotObject.IsInstanceValid(_primaryMuzzle))
		{
			var f = -_primaryMuzzle.GlobalTransform.Basis.Z;
			f.Y = 0f;
			if (f.Length() < 0.001f)
			{
				var fallback = -GlobalTransform.Basis.Z;
				fallback.Y = 0f;
				return fallback.Length() < 0.001f ? Vector3.Forward : fallback.Normalized();
			}
			return f.Normalized();
		}

		var forward = -GlobalTransform.Basis.Z;
		forward.Y = 0f;
		return forward.Length() < 0.001f ? Vector3.Forward : forward.Normalized();
	}

	public Vector3 GetMuzzleWorldPosition(string mountId)
	{
		if (!string.IsNullOrWhiteSpace(mountId) && _muzzleByMountId.TryGetValue(mountId, out var muzzle) && GodotObject.IsInstanceValid(muzzle))
			return muzzle.GlobalPosition;
		return GetMuzzleWorldPosition();
	}

	public Vector3 GetMuzzleWorldForward(string mountId)
	{
		if (!string.IsNullOrWhiteSpace(mountId) && _muzzleByMountId.TryGetValue(mountId, out var muzzle) && GodotObject.IsInstanceValid(muzzle))
		{
			var f = -muzzle.GlobalTransform.Basis.Z;
			f.Y = 0f;
			if (f.Length() < 0.001f)
				return GetMuzzleWorldForward();
			return f.Normalized();
		}
		return GetMuzzleWorldForward();
	}


	
	public Vector3 GetAimPointWorld()
	{
		// Aim toward the approximate center-mass of the vehicle so raycasts hit reliably.
		// Vehicle GlobalPosition is kept at ~half-height (y=0.4).
		return GlobalPosition + Vector3.Up * 0.10f;
	}

	public Vector3 GetTireWorldPosition(int tireIndex)
	{
		// Matches the rough tire hitbox offsets used in EnsureHitboxes().
		var local = tireIndex switch
		{
			0 => new Vector3(-0.9f, -0.05f, -1.3f), // FL
			1 => new Vector3(0.9f, -0.05f, -1.3f),  // FR
			2 => new Vector3(-0.9f, -0.05f, 1.3f),  // RL
			3 => new Vector3(0.9f, -0.05f, 1.3f),   // RR
			_ => Vector3.Zero
		};
		// Convert pawn-local to world.
		var basis = GlobalTransform.Basis;
		var origin = GlobalTransform.Origin;
		return origin + basis * local;
	}

	// --- IVehicleAudioTelemetry ---
	// Telemetry methods (speed/throttle/brake) are now provided by VehiclePawnBase via GamePawnKit.Pawns.IVehicleTelemetry.
	// VehiclePawn continues to implement IVehicleAudioTelemetry for the current Wasteland Survivor audio stack.

	// --- HUD helpers (RPM + gear display) ---

	public float GetEngineRpm01ForHud()
	{
		if (_engineAudio != null) return _engineAudio.CurrentRpm01;
		var max = GetMaxSpeedMps();
		return Mathf.Clamp(GetSpeedMps() / MathF.Max(0.01f, max), 0f, 1f);
	}

	public int GetEngineGearForHud()
	{
		if (_engineAudio != null) return Math.Max(1, _engineAudio.CurrentGear);
		return 1;
	}

	public string GetEngineGearDisplayForHud()
	{
		// Treat reverse as "R" when backing up (or when the player is commanding reverse from a stop).
		var vFwd = GetForwardSpeedSignedMps();
		if (vFwd < -0.6f) return "R";
		if (MathF.Abs(vFwd) < 0.6f && ThrottleInput < -0.4f) return "R";
		return GetEngineGearForHud().ToString();
	}

	public int GetEngineDisplayRpmForHud()
	{
		if (_engineAudio != null) return _engineAudio.GetDisplayRpm();
		// Fallback mapping if audio not present.
		var rpm01 = GetEngineRpm01ForHud();
		return Mathf.RoundToInt(Mathf.Lerp(900f, 6500f, rpm01));
	}
private void ApplyBodyColor()
	{
		// Per-class def models get a dedicated body tint pass (shared flat colormap texture).
		if (_usingDefModelVisual)
		{
			ApplyDefModelTint(null);
			return;
		}

		// Passenger car pack uses textured materials; do not override.
		if (_usingPassengerCarPackVisual) return;

		if (_visualRoot == null) return;
		if (_bodyMeshes.Count == 0)
		{
			// Collect body meshes on demand.
			_bodyMeshes.Clear();
			CollectMeshesByPrefix(_visualRoot, "Body", _bodyMeshes);
		}

		var mat = new StandardMaterial3D
		{
			AlbedoColor = BodyColor,
			Roughness = 0.9f,
			Metallic = 0.05f,
		};
		foreach (var m in _bodyMeshes)
		{
			if (m.Mesh == null) continue;
			m.SetSurfaceOverrideMaterial(0, mat);
		}
	}

	private static void CollectMeshesByPrefix(Node node, string prefix, List<MeshInstance3D> into)
	{
		foreach (var childObj in node.GetChildren())
		{
			if (childObj is not Node child) continue;
			if (child is MeshInstance3D mi && mi.Name.ToString().StartsWith(prefix))
				into.Add(mi);
			CollectMeshesByPrefix(child, prefix, into);
		}
	}

	private static void ClearChildren(Node parent)
	{
		foreach (var childObj in parent.GetChildren())
		{
			if (childObj is Node n)
				n.QueueFree();
		}
	}

	private bool TryBuildPassengerCarPackVisual(Node3D parent)
	{
		string? scenePath = ResolvePassengerCarPackScenePath();
		if (string.IsNullOrWhiteSpace(scenePath))
		{
			GD.PushWarning($"[VehiclePawn] Passenger car pack scene not found. Expected: {PassengerCarPackScenePath}");
			return false;
		}

		if (PassengerCarPackDebugAlignment)
			GD.Print($"[VehiclePawn] PassengerCarPack scene path: {scenePath}");

		PackedScene? ps = null;
		try
		{
			ps = GD.Load<PackedScene>(scenePath);
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[VehiclePawn] Failed to load passenger car pack scene '{scenePath}': {ex.Message}");
			return false;
		}
		if (ps == null)
		{
			GD.PushWarning($"[VehiclePawn] Passenger car pack scene load returned null: {scenePath}");
			return false;
		}

		Node? inst;
		try
		{
			inst = ps.Instantiate();
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[VehiclePawn] Failed to instantiate passenger car pack scene '{scenePath}': {ex.Message}");
			return false;
		}
		if (inst is not Node3D model)
		{
			inst.QueueFree();
			GD.PushWarning($"[VehiclePawn] Passenger car pack scene root is not Node3D: {inst.GetType().Name}");
			return false;
		}

		model.Name = "PassengerCarModel";
		model.Position = Vector3.Zero;
		// Start from a clean local transform; we'll apply alignment and then any user overrides.
		model.RotationDegrees = Vector3.Zero;
		model.Scale = PassengerCarPackExtraScale;
		parent.AddChild(model);

		// Visibility selection (choose one body + one wheel set).
		ApplyPassengerCarPackSelection(model);
		// Ensure transforms/materials are ready before measuring.
		model.ForceUpdateTransform();

		// If the import is extremely tiny (common for some Sketchfab exports), auto-scale to match our proxy.
		AutoScaleAndCenterPassengerCar(model);

		// Auto-align yaw so the car points the same direction as our proxy vehicle (forward is -Z).
		AutoYawAlignPassengerCar(model);

		// Apply any user override rotations (rarely needed once auto-align is in place).
		if (PassengerCarPackRotationDegrees != Vector3.Zero)
			model.RotationDegrees += PassengerCarPackRotationDegrees;

		// Re-center after yaw/overrides so the model stays on the pawn origin.
		AutoScaleAndCenterPassengerCar(model);

		// Apply variant materials (Body 0 enemy, Body 1 player).
		var variant = IsInGroup("player_vehicle") ? PassengerCarPackPlayerVariantIndex : PassengerCarPackEnemyVariantIndex;
		ApplyPassengerCarPackVariantMaterials(model, variant);

		// Bind wheel pivots for steering visuals.
		BindPassengerCarWheels(model);

		_usingPassengerCarPackVisual = true;
		_bodyMeshes.Clear(); // so ApplyBodyColor won't reuse cached proxy meshes
		return true;
	}

	private string? ResolvePassengerCarPackScenePath()
	{
		// 1) Use configured path if it exists.
		if (!string.IsNullOrWhiteSpace(PassengerCarPackScenePath) && ResourceLoader.Exists(PassengerCarPackScenePath))
			return PassengerCarPackScenePath;

		// 2) Common fallbacks.
		var fallbacks = new[]
		{
			"res://Assets/Models/Vehicles/GenericPassengerCarPack/scene.gltf",
			"res://Assets/Models/Vehicles/generic_passenger_car_pack/scene.gltf",
			"res://Assets/Models/GenericPassengerCarPack/scene.gltf",
			"res://Assets/generic_passenger_car_pack/scene.gltf",
		};
		foreach (var p in fallbacks)
			if (ResourceLoader.Exists(p))
				return p;

		// NOTE: A previous "last resort" here recursively scanned res://Assets for any scene.gltf,
		// which could silently bind an arbitrary mesh as the car. Removed on purpose: when the pack
		// isn't found we fail here and the caller falls back to the procedural proxy visual.
		return null;
	}

	private void ApplyPassengerCarPackSelection(Node3D model)
	{
		// Imported hierarchy typically includes a "RootNode" containing all bodies/wheels.
		var root = model.FindChild("RootNode", recursive: true, owned: false) as Node3D;
		if (root == null)
		{
			GD.PushWarning("[VehiclePawn] Passenger car pack: RootNode not found. Leaving model as-is.");
			return;
		}

		foreach (var childObj in root.GetChildren())
		{
			if (childObj is not Node3D n) continue;
			var nm = n.Name.ToString();

			var keepBody = string.Equals(nm, PassengerCarPackBodyNodeName, StringComparison.OrdinalIgnoreCase);
			var keepWheel = nm.StartsWith(PassengerCarPackWheelRootPrefix, StringComparison.OrdinalIgnoreCase);

			// Hide everything except the chosen body + wheel set.
			n.Visible = keepBody || keepWheel;
		}
	}

	private void AutoScaleAndCenterPassengerCar(Node3D model, float forcedTargetLength = 0f)
	{
		// IMPORTANT:
		// Measure bounds in the coordinate space of the model's *parent* (the VehiclePawn's "Visual" node),
		// not in world space. World-space bounds include the pawn's spawn position and will create a huge
		// constant offset between the vehicle origin (weapon mounts) and the visible model.
		var parentSpace = model.GetParent() as Node3D ?? model;
		if (!TryComputeAabbInSpace(model, parentSpace, out var aabb))
			return;

		var len = MathF.Max(aabb.Size.X, aabb.Size.Z);
		if (len < 0.0001f) return;

		if (forcedTargetLength > 0.01f)
		{
			// Def-model path: always normalize to the requested length (Kenney kit models are
			// authored around ~2m; per-class defs request their real-ish footprint).
			if (MathF.Abs(len - forcedTargetLength) > 0.01f)
			{
				var scaleFactor = Mathf.Clamp(forcedTargetLength / len, 0.05f, 50000f);
				model.Scale *= new Vector3(scaleFactor, scaleFactor, scaleFactor);
				if (!TryComputeAabbInSpace(model, parentSpace, out aabb))
					return;
			}
		}
		// If the model is tiny, scale it up to match our proxy length.
		else if (len < PassengerCarPackMinVisibleLength)
		{
			var target = MathF.Max(0.5f, PassengerCarPackTargetLength);
			var scaleFactor = target / len;
			// Clamp to avoid catastrophic transforms.
			scaleFactor = Mathf.Clamp(scaleFactor, 0.05f, 50000f);
			model.Scale *= new Vector3(scaleFactor, scaleFactor, scaleFactor);
			if (!TryComputeAabbInSpace(model, parentSpace, out aabb))
				return;
		}

		// Center XZ and rest on the ground plane.
		var center = aabb.Position + aabb.Size * 0.5f;
		var bottomY = aabb.Position.Y;
		model.Position -= new Vector3(center.X, bottomY, center.Z);
	}

	private void AutoYawAlignPassengerCar(Node3D model)
	{
		if (!PassengerCarPackAutoAlignYaw) return;

		var parentSpace = model.GetParent() as Node3D ?? model;

		// Prefer a wheel-axle based direction if we can find 4 wheels.
		// This is far more stable than sampling mesh corners, and avoids "diagonal" PCA artifacts.
		var method = "wheelAxle";
		if (!TryComputeWheelAxleDirectionXZ(model, parentSpace, out var axisDir))
		{
			method = "pca";
			var pts = CollectAlignmentPointsXZ(model, parentSpace);
			if (pts.Count < 4) return;
			axisDir = ComputePrincipalAxisXZ(pts);
		}

		if (axisDir.LengthSquared() < 0.000001f) return;
		axisDir = axisDir.Normalized();

		// Pick a consistent sign so we don't randomly flip 180° depending on wheel pair ordering.
		// We prefer the axis direction that points "mostly forward" (-Z) in our parent space.
		if (axisDir.Y > 0f)
			axisDir = -axisDir;

		// Convert to yaw (0 means pointing along -Z).
		// NOTE: axisDir may be flipped (v or -v), but rotating by the computed heading always aligns
		// the *axis* to our forward (-Z). Front/back can still be ambiguous for some models; if we
		// ever hit that case, we handle it via an optional 180° override.
		var heading = MathF.Atan2(axisDir.X, -axisDir.Y); // axisDir=(x,z) mapped to (x,y=z)
		heading = WrapAnglePi(heading);

		if (PassengerCarPackDebugAlignment)
		{
			var beforeDeg = model.RotationDegrees.Y;
			var headingDeg = Mathf.RadToDeg(heading);
			GD.Print($"[VehiclePawn] AutoYawAlign ({method}): axisDirXZ=({axisDir.X:0.###},{axisDir.Y:0.###}) headingDeg={headingDeg:0.##} beforeYawDeg={beforeDeg:0.##}");
		}

		// IMPORTANT: in Godot's coordinate system (forward = -Z), the yaw that maps a direction onto -Z
		// is +heading (not -heading).
		model.RotateY(heading);

		if (PassengerCarPackDebugAlignment)
		{
			var afterDeg = model.RotationDegrees.Y;
			GD.Print($"[VehiclePawn] AutoYawAlign: afterYawDeg={afterDeg:0.##}");
		}
	}

	private bool TryComputeWheelAxleDirectionXZ(Node3D model, Node3D space, out Vector2 axisDir)
	{
		axisDir = Vector2.Zero;
		var root = model.FindChild("RootNode", recursive: true, owned: false) as Node3D;
		if (root == null) return false;

		var invSpace = space.GlobalTransform.AffineInverse();
		var wheels = new List<Vector2>(8);
		foreach (var childObj in root.GetChildren())
		{
			if (childObj is not Node3D n) continue;
			var nm = n.Name.ToString();
			if (!nm.StartsWith(PassengerCarPackWheelRootPrefix, StringComparison.OrdinalIgnoreCase))
				continue;
			var rel = invSpace * n.GlobalTransform;
			var p = rel.Origin;
			wheels.Add(new Vector2(p.X, p.Z));
		}
		if (wheels.Count < 4) return false;

		// Find two disjoint pairs with the smallest distance (these should be the axle widths).
		var pairs = new List<(int i, int j, float d)>(16);
		for (var i = 0; i < wheels.Count; i++)
		for (var j = i + 1; j < wheels.Count; j++)
		{
			var d = wheels[i].DistanceTo(wheels[j]);
			pairs.Add((i, j, d));
		}
		pairs.Sort((a, b) => a.d.CompareTo(b.d));

		(int i, int j, float d) p1 = default;
		(int i, int j, float d) p2 = default;
		var got1 = false;
		for (var k = 0; k < pairs.Count; k++)
		{
			var p = pairs[k];
			if (!got1)
			{
				p1 = p;
				got1 = true;
				continue;
			}
			// second pair must be disjoint
			if (p.i == p1.i || p.i == p1.j || p.j == p1.i || p.j == p1.j)
				continue;
			p2 = p;
			break;
		}
		if (!got1 || p2.d <= 0f) return false;

		var m1 = (wheels[p1.i] + wheels[p1.j]) * 0.5f;
		var m2 = (wheels[p2.i] + wheels[p2.j]) * 0.5f;
		var dAxis = m2 - m1;
		if (dAxis.LengthSquared() < 0.000001f) return false;
		axisDir = dAxis;
		return true;
	}

	private List<Vector2> CollectAlignmentPointsXZ(Node3D model, Node3D space)
	{
		var pts = new List<Vector2>(64);
		var invSpace = space.GlobalTransform.AffineInverse();

		// Prefer wheel nodes if present (most stable for vehicles).
		var root = model.FindChild("RootNode", recursive: true, owned: false) as Node3D;
		if (root != null)
		{
			foreach (var childObj in root.GetChildren())
			{
				if (childObj is not Node3D n) continue;
				var nm = n.Name.ToString();
				if (!nm.StartsWith(PassengerCarPackWheelRootPrefix, StringComparison.OrdinalIgnoreCase))
					continue;
				var rel = invSpace * n.GlobalTransform;
				var p = rel.Origin;
				pts.Add(new Vector2(p.X, p.Z));
			}
		}

		if (pts.Count >= 4) return pts;
		pts.Clear();

		// Fallback: sample mesh AABB corners transformed into the requested space.
		var stack = new Stack<Node>();
		stack.Push(model);
		while (stack.Count > 0)
		{
			var n = stack.Pop();
			foreach (var childObj in n.GetChildren())
				if (childObj is Node child)
					stack.Push(child);

			if (n is not MeshInstance3D mi) continue;
			if (mi.Mesh == null) continue;
			if (!mi.IsVisibleInTree()) continue;

			var rel = invSpace * mi.GlobalTransform;
			var aabb = mi.GetAabb();
			var p0 = aabb.Position;
			var s = aabb.Size;
			var corners = new Vector3[8]
			{
				p0,
				p0 + new Vector3(s.X, 0f, 0f),
				p0 + new Vector3(0f, s.Y, 0f),
				p0 + new Vector3(0f, 0f, s.Z),
				p0 + new Vector3(s.X, s.Y, 0f),
				p0 + new Vector3(s.X, 0f, s.Z),
				p0 + new Vector3(0f, s.Y, s.Z),
				p0 + new Vector3(s.X, s.Y, s.Z),
			};
			for (var i = 0; i < corners.Length; i++)
			{
				var w = TransformPoint(rel, corners[i]);
				pts.Add(new Vector2(w.X, w.Z));
			}
		}

		return pts;
	}

	private static Vector2 ComputePrincipalAxisXZ(List<Vector2> pts)
	{
		if (pts.Count == 0) return Vector2.Zero;

		var mean = Vector2.Zero;
		foreach (var p in pts) mean += p;
		mean /= pts.Count;

		float sxx = 0f, szz = 0f, sxz = 0f;
		foreach (var p in pts)
		{
			var dx = p.X - mean.X;
			var dz = p.Y - mean.Y;
			sxx += dx * dx;
			szz += dz * dz;
			sxz += dx * dz;
		}
		// Normalize by N (not required for eigenvectors, but keeps numbers sane).
		var invN = 1f / MathF.Max(1, pts.Count);
		sxx *= invN;
		szz *= invN;
		sxz *= invN;

		// Eigenvector for the largest eigenvalue of [[sxx, sxz],[sxz, szz]].
		var tr = sxx + szz;
		var det = sxx * szz - sxz * sxz;
		var disc = MathF.Sqrt(MathF.Max(0f, tr * tr * 0.25f - det));
		var lambda1 = tr * 0.5f + disc;

		Vector2 v;
		if (MathF.Abs(sxz) > 0.000001f)
			v = new Vector2(lambda1 - szz, sxz);
		else
			v = sxx >= szz ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
		return v;
	}

	private static float WrapAnglePi(float a)
	{
		// Wrap to (-PI, PI]
		while (a <= -MathF.PI) a += MathF.Tau;
		while (a > MathF.PI) a -= MathF.Tau;
		return a;
	}

	private static bool TryComputeAabbInSpace(Node root, Node3D space, out Aabb aabb)
	{
		aabb = default;
		var first = true;
		var invSpace = space.GlobalTransform.AffineInverse();
		var stack = new Stack<Node>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			var n = stack.Pop();
			foreach (var childObj in n.GetChildren())
			{
				if (childObj is Node child)
					stack.Push(child);
			}

			if (n is not MeshInstance3D mi) continue;
			if (mi.Mesh == null) continue;
			if (!mi.IsVisibleInTree()) continue;

			// Convert each mesh AABB into the requested space (typically the VehiclePawn's "Visual" node).
			// NOTE: Godot C# bindings for Aabb do not expose the GDScript-style `transformed()` helper.
			// We transform the 8 corners manually to avoid version/API differences.
			var rel = invSpace * mi.GlobalTransform;
			var mAabb = TransformAabb(mi.GetAabb(), rel);
			if (first)
			{
				aabb = mAabb;
				first = false;
			}
			else
			{
				aabb = aabb.Merge(mAabb);
			}
		}

		return !first;
	}

	private static Aabb TransformAabb(Aabb localAabb, Transform3D xform)
	{
		var p = localAabb.Position;
		var s = localAabb.Size;
		var corners = new Vector3[8]
		{
			p,
			p + new Vector3(s.X, 0f, 0f),
			p + new Vector3(0f, s.Y, 0f),
			p + new Vector3(0f, 0f, s.Z),
			p + new Vector3(s.X, s.Y, 0f),
			p + new Vector3(s.X, 0f, s.Z),
			p + new Vector3(0f, s.Y, s.Z),
			p + new Vector3(s.X, s.Y, s.Z),
		};

		var w0 = TransformPoint(xform, corners[0]);
		var min = w0;
		var max = w0;
		for (var i = 1; i < corners.Length; i++)
		{
			var w = TransformPoint(xform, corners[i]);
			min = new Vector3(Mathf.Min(min.X, w.X), Mathf.Min(min.Y, w.Y), Mathf.Min(min.Z, w.Z));
			max = new Vector3(Mathf.Max(max.X, w.X), Mathf.Max(max.Y, w.Y), Mathf.Max(max.Z, w.Z));
		}

		return new Aabb(min, max - min);
	}

	private static Vector3 TransformPoint(Transform3D t, Vector3 v)
	{
		var b = t.Basis;
		// Godot Basis columns (X/Y/Z) represent the transformed axes.
		return t.Origin + b.X * v.X + b.Y * v.Y + b.Z * v.Z;
	}

	private void ApplyPassengerCarPackVariantMaterials(Node3D model, int variantIndex)
	{
		variantIndex = Mathf.Clamp(variantIndex, 0, 9);
		var mats = CollectMaterialsByName(model);
		if (PassengerCarPackDebugAlignment)
			GD.Print($"[VehiclePawn] VariantSwap: want={variantIndex} playerGroup={IsInGroup("player_vehicle")} mats=[{string.Join(", ", mats.Keys)}]");
		if (mats.Count == 0) return;

		// Swap materials on all visible nodes (body + wheels). The visible default mesh uses BARE
		// material names ("Body", "Glass", "Wheel") — only the hidden variant meshes carry "_N"
		// suffixes — so the pattern must match both or the swap never touches what's on screen.
		var pattern = new Regex(@"^(Body|Glass|Optics|Wheel|Wheek)(?:_(\d+))?$", RegexOptions.IgnoreCase);
		var stack = new Stack<Node>();
		stack.Push(model);
		while (stack.Count > 0)
		{
			var n = stack.Pop();
			foreach (var childObj in n.GetChildren())
			{
				if (childObj is Node child)
					stack.Push(child);
			}

			if (n is not MeshInstance3D mi) continue;
			if (mi.Mesh == null) continue;
			// Playbook trap: IsVisibleInTree() is false while the model is configured off-tree, which
			// silently skipped every variant swap (the enemy never got its paint). Use Visible instead.
			if (!mi.Visible) continue;

			var surfaces = mi.Mesh.GetSurfaceCount();
			for (var s = 0; s < surfaces; s++)
			{
				var mat = mi.GetSurfaceOverrideMaterial(s) ?? mi.Mesh.SurfaceGetMaterial(s);
				if (mat == null) continue;
				var name = GetMaterialName(mat);
				if (string.IsNullOrWhiteSpace(name)) continue;

				var m = pattern.Match(name);
				if (!m.Success) continue;
				var prefix = m.Groups[1].Value;
				var desired = $"{prefix}_{variantIndex}";
				if (mats.TryGetValue(desired, out var newMat))
					mi.SetSurfaceOverrideMaterial(s, newMat);
			}
		}
	}

	private static Dictionary<string, Material> CollectMaterialsByName(Node root)
	{
		var dict = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
		var stack = new Stack<Node>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			var n = stack.Pop();
			foreach (var childObj in n.GetChildren())
			{
				if (childObj is Node child)
					stack.Push(child);
			}

			if (n is not MeshInstance3D mi) continue;
			if (mi.Mesh == null) continue;

			var surfaces = mi.Mesh.GetSurfaceCount();
			for (var s = 0; s < surfaces; s++)
			{
				var mat = mi.GetSurfaceOverrideMaterial(s) ?? mi.Mesh.SurfaceGetMaterial(s);
				if (mat == null) continue;
				var name = GetMaterialName(mat);
				if (string.IsNullOrWhiteSpace(name)) continue;
				if (!dict.ContainsKey(name))
					dict[name] = mat;
			}
		}
		return dict;
	}

	private static string GetMaterialName(Material mat)
	{
		if (!string.IsNullOrWhiteSpace(mat.ResourceName))
			return mat.ResourceName;
		// Godot sometimes leaves ResourceName blank for imported embedded resources.
		var path = mat.ResourcePath;
		if (string.IsNullOrWhiteSpace(path)) return "";
		return System.IO.Path.GetFileNameWithoutExtension(path);
	}

	private void BindPassengerCarWheels(Node3D model)
	{
		_wheelFlPivot = null;
		_wheelFrPivot = null;
		_wheelRlPivot = null;
		_wheelRrPivot = null;

		var root = model.FindChild("RootNode", recursive: true, owned: false) as Node3D;
		if (root == null) return;

		var wheels = new List<Node3D>();
		foreach (var childObj in root.GetChildren())
		{
			if (childObj is not Node3D n) continue;
			var nm = n.Name.ToString();
			if (nm.StartsWith(PassengerCarPackWheelRootPrefix, StringComparison.OrdinalIgnoreCase))
				wheels.Add(n);
		}
		if (wheels.Count < 4) return;

		// Determine FL/FR/RL/RR by position in the VehiclePawn's visual space (forward is -Z).
		var parentSpace = model.GetParent() as Node3D ?? model;
		var invParent = parentSpace.GlobalTransform.AffineInverse();
		var wheelInfo = wheels
			.Select(w => new { node = w, pos = (invParent * w.GlobalTransform).Origin })
			.ToList();

		var orderedByFront = wheelInfo.OrderBy(w => w.pos.Z).ToList();
		var front = orderedByFront.Take(2).OrderBy(w => w.pos.X).ToList();
		var rear = orderedByFront.Skip(orderedByFront.Count - 2).OrderBy(w => w.pos.X).ToList();

		_wheelFlPivot = front[0].node;
		_wheelFrPivot = front[1].node;
		_wheelRlPivot = rear[0].node;
		_wheelRrPivot = rear[1].node;
	}

	// --- Per-class def model visuals (VehicleDefinition.VisualModelPath, ex: Kenney Car Kit GLBs) ---

	/// <summary>
	/// Builds (or rebuilds) the per-class visual model when the vehicle definition specifies one.
	/// No-op when the definition has no VisualModelPath, or when the pawn is off-tree — in that case
	/// _Ready()/EnsureVisualAndCollision() builds it once transforms are valid.
	/// On build failure the previously built visual (car pack or proxy) is left untouched.
	/// </summary>
	private void EnsureDefModelVisual()
	{
		var path = ResolveVisualModelPath();

		// Trailers have no kit model; if _Ready built the generic car before the def was known
		// (ConfigureLoadout order), swap in the flatbed proxy now.
		if (string.IsNullOrWhiteSpace(path)
			&& _vehicleDef?.Class == VehicleClass.Trailer
			&& _visualRoot != null
			&& _visualRoot.GetNodeOrNull<Node>("TrailerProxy") == null)
		{
			ClearChildren(_visualRoot);
			_usingPassengerCarPackVisual = false;
			_usingDefModelVisual = false;
			BuildProxyTrailerVisual(_visualRoot, _vehicleDef.TireCount);
			return;
		}

		if (string.IsNullOrWhiteSpace(path)) return;
		// AABB/yaw normalization needs valid global transforms; HUD preview pawns are configured
		// before their viewport enters the tree, so defer to _Ready() in that case.
		if (!IsInsideTree() || _visualRoot == null) return;

		if (_usingDefModelVisual
			&& string.Equals(_activeDefModelPath, path, StringComparison.OrdinalIgnoreCase)
			&& Mathf.IsEqualApprox(_activeDefModelTargetLen, CurrentDefModelTargetLength())
			&& _visualRoot.GetNodeOrNull<Node>("DefVehicleModel") is Node existing
			&& GodotObject.IsInstanceValid(existing))
			return;

		if (TryBuildDefModelVisual(_visualRoot, path!))
		{
			_usingDefModelVisual = true;
			_activeDefModelPath = path;
		}
	}

	/// <summary>
	/// Instantiates the definition's model scene, yaw-aligns it (front wheels forward = -Z),
	/// scales it to the def's target length, centers it on the pawn origin, binds wheel pivots
	/// and applies the body tint. Only clears the previous visual after the scene instantiated OK.
	/// </summary>
	private bool TryBuildDefModelVisual(Node3D parent, string scenePath)
	{
		if (!ResourceLoader.Exists(scenePath))
		{
			GD.PushWarning($"[VehiclePawn] Def vehicle model not found: {scenePath}");
			return false;
		}

		PackedScene? ps = null;
		try
		{
			ps = GD.Load<PackedScene>(scenePath);
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[VehiclePawn] Failed to load def vehicle model '{scenePath}': {ex.Message}");
			return false;
		}
		if (ps == null)
		{
			GD.PushWarning($"[VehiclePawn] Def vehicle model load returned null: {scenePath}");
			return false;
		}

		Node? inst;
		try
		{
			inst = ps.Instantiate();
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[VehiclePawn] Failed to instantiate def vehicle model '{scenePath}': {ex.Message}");
			return false;
		}
		if (inst is not Node3D model)
		{
			inst?.QueueFree();
			GD.PushWarning($"[VehiclePawn] Def vehicle model root is not Node3D: {scenePath}");
			return false;
		}

		// Only now discard the previous visual (pack/proxy) — keeps a working fallback on failure above.
		ClearChildren(parent);

		model.Name = "DefVehicleModel";
		model.Position = Vector3.Zero;
		model.RotationDegrees = Vector3.Zero;
		model.Scale = Vector3.One;
		parent.AddChild(model);
		model.ForceUpdateTransform();

		// Deterministic yaw alignment from the kit's named wheel nodes (front wheels sit at +Z in
		// the glTF export, our forward is -Z). Falls back to the PCA-based pack helper if names miss.
		if (!TryYawAlignDefModelByWheelNames(model))
			AutoYawAlignPassengerCar(model);

		var targetLen = CurrentDefModelTargetLength();
		AutoScaleAndCenterPassengerCar(model, targetLen);
		_activeDefModelTargetLen = targetLen;

		BindDefModelWheels(model);
		ApplyDefModelTint(model);

		_bodyMeshes.Clear();
		_usingPassengerCarPackVisual = false;
		return true;
	}

	/// <summary>
	/// Yaw-aligns the model so the side with the front wheels points along the pawn's forward (-Z).
	/// Uses the Kenney Car Kit wheel node names (wheel-front-left etc.); returns false if not found.
	/// </summary>
	private bool TryYawAlignDefModelByWheelNames(Node3D model)
	{
		var fl = FindDescendantByName(model, "wheel-front-left");
		var fr = FindDescendantByName(model, "wheel-front-right");
		var bl = FindDescendantByName(model, "wheel-back-left");
		var br = FindDescendantByName(model, "wheel-back-right");
		if (fl == null || fr == null || bl == null || br == null)
			return false;

		var space = model.GetParent() as Node3D ?? model;
		var inv = space.GlobalTransform.AffineInverse();
		Vector3 PosIn(Node3D n) => (inv * n.GlobalTransform).Origin;

		var frontMid = (PosIn(fl) + PosIn(fr)) * 0.5f;
		var backMid = (PosIn(bl) + PosIn(br)) * 0.5f;
		var fwd = frontMid - backMid;
		var fwdXz = new Vector2(fwd.X, fwd.Z);
		if (fwdXz.LengthSquared() < 0.000001f)
			return false;

		// Yaw that maps the model's front direction onto -Z (see AutoYawAlignPassengerCar notes).
		var yaw = MathF.Atan2(fwdXz.X, -fwdXz.Y);
		model.RotateY(yaw);
		if (PassengerCarPackDebugAlignment)
			GD.Print($"[VehiclePawn] DefModel yaw-align by wheel names: fwdXZ=({fwdXz.X:0.###},{fwdXz.Y:0.###}) yawDeg={Mathf.RadToDeg(yaw):0.##}");
		return true;
	}

	private static Node3D? FindDescendantByName(Node root, string name)
	{
		var stack = new Stack<Node>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			var n = stack.Pop();
			foreach (var childObj in n.GetChildren())
				if (childObj is Node child)
					stack.Push(child);
			if (n is Node3D n3 && string.Equals(n.Name.ToString(), name, StringComparison.OrdinalIgnoreCase))
				return n3;
		}
		return null;
	}

	/// <summary>
	/// Binds the def model's named wheel nodes as steer/VFX pivots. Each wheel is re-parented under
	/// a fresh pivot at its axle position because UpdateFrontWheelSteer() writes RotationDegrees
	/// absolutely (which would destroy any baked wheel orientation).
	/// </summary>
	private void BindDefModelWheels(Node3D model)
	{
		_wheelFlPivot = WrapDefWheelInPivot(FindDescendantByName(model, "wheel-front-left"));
		_wheelFrPivot = WrapDefWheelInPivot(FindDescendantByName(model, "wheel-front-right"));
		_wheelRlPivot = WrapDefWheelInPivot(FindDescendantByName(model, "wheel-back-left"));
		_wheelRrPivot = WrapDefWheelInPivot(FindDescendantByName(model, "wheel-back-right"));
	}

	private static Node3D? WrapDefWheelInPivot(Node3D? wheel)
	{
		if (wheel == null) return null;
		if (wheel.GetParent() is not Node3D parent) return wheel;

		var pivot = new Node3D { Name = $"{wheel.Name}_Pivot", Position = wheel.Position };
		parent.RemoveChild(wheel);
		parent.AddChild(pivot);
		pivot.AddChild(wheel);
		wheel.Position = Vector3.Zero; // keep any baked rotation/scale; pivot owns the axle offset
		return pivot;
	}

	/// <summary>
	/// Body tint for def models. The Kenney kit shares one flat "colormap" texture across body,
	/// glass and wheels, so a multiplicative texture tint would produce muddy/ambiguous team colors
	/// (yellow x blue = olive). Instead the body meshes get a duplicated flat material in BodyColor
	/// (player yellow vs enemy dark red stays unambiguous); wheels keep the original textured look.
	/// Re-runs cheaply when BodyColor changes (updates the already-duplicated override materials).
	/// </summary>
	private void ApplyDefModelTint(Node3D? model)
	{
		if (model == null || !GodotObject.IsInstanceValid(model))
		{
			if (_visualRoot == null) return;
			model = _visualRoot.GetNodeOrNull<Node3D>("DefVehicleModel");
			if (model == null || !GodotObject.IsInstanceValid(model)) return;
		}

		var archetype = VehicleHullDetailer.ResolveArchetype(ResolveVisualModelPath());
		foreach (var mi in CollectDefModelBodyMeshes(model))
		{
			if (mi.Mesh == null) continue;
			var surfaces = mi.Mesh.GetSurfaceCount();
			for (var s = 0; s < surfaces; s++)
			{
				if (mi.GetSurfaceOverrideMaterial(s) is ShaderMaterial priorHull && priorHull.HasMeta("ws_body_tint"))
				{
					VehicleHullDetailer.UpdateBodyColor(priorHull, BodyColor);
					continue;
				}
				if (mi.GetSurfaceOverrideMaterial(s) is StandardMaterial3D prior && prior.HasMeta("ws_body_tint"))
				{
					prior.AlbedoColor = BodyColor;
					continue;
				}

				mi.SetSurfaceOverrideMaterial(s, BuildHullDetailMaterial(mi, archetype));
			}
		}
	}

	/// <summary>
	/// Builds the top-projected hull detail material for one body mesh. The projection axes map
	/// mesh-local space onto canonical car space (u across the width, v front->rear), derived from
	/// the pawn's forward/right transformed back through the mesh's parent chain — this survives
	/// the model-level yaw alignment without assuming which way the kit exported the car.
	/// </summary>
	private ShaderMaterial BuildHullDetailMaterial(MeshInstance3D mi, string archetype)
	{
		// mesh-local <- pawn transform accumulated up the parent chain (valid off-tree too).
		var toPawn = Transform3D.Identity;
		Node? cur = mi;
		while (cur != null && cur != this)
		{
			if (cur is Node3D n3) toPawn = n3.Transform * toPawn;
			cur = cur.GetParent();
		}
		var invBasis = toPawn.Basis.Inverse();

		var axisV = SnapToDominantXzAxis(invBasis * Vector3.Back);   // v grows toward the rear
		var axisU = SnapToDominantXzAxis(invBasis * Vector3.Right);  // u grows to the right

		var aabb = mi.GetAabb();
		var (uMin, uMax) = ProjectAabb(aabb, axisU);
		var (vMin, vMax) = ProjectAabb(aabb, axisV);

		return VehicleHullDetailer.BuildHullMaterial(
			BodyColor, archetype,
			axisU, axisV,
			new Vector2(uMin, vMin),
			new Vector2(MathF.Max(uMax - uMin, 0.001f), MathF.Max(vMax - vMin, 0.001f)),
			aabb.Position.Y, aabb.End.Y,
			hostile: HostileIdentity);
	}

	private static Vector3 SnapToDominantXzAxis(Vector3 v)
	{
		return MathF.Abs(v.X) >= MathF.Abs(v.Z)
			? new Vector3(MathF.Sign(v.X) >= 0 ? 1f : -1f, 0f, 0f)
			: new Vector3(0f, 0f, MathF.Sign(v.Z) >= 0 ? 1f : -1f);
	}

	private static (float min, float max) ProjectAabb(Aabb aabb, Vector3 axis)
	{
		var min = float.MaxValue;
		var max = float.MinValue;
		for (var i = 0; i < 8; i++)
		{
			var p = aabb.GetEndpoint(i);
			var d = p.Dot(axis);
			if (d < min) min = d;
			if (d > max) max = d;
		}
		return (min, max);
	}

	/// <summary>
	/// Body meshes for the tint pass: nodes named like "body" outside any wheel subtree; if none
	/// match, falls back to the largest non-wheel/non-glass mesh by local AABB volume.
	/// </summary>
	private static List<MeshInstance3D> CollectDefModelBodyMeshes(Node3D model)
	{
		var all = new List<MeshInstance3D>();
		var stack = new Stack<Node>();
		stack.Push(model);
		while (stack.Count > 0)
		{
			var n = stack.Pop();
			foreach (var childObj in n.GetChildren())
				if (childObj is Node child)
					stack.Push(child);
			if (n is MeshInstance3D mi && mi.Mesh != null)
				all.Add(mi);
		}

		bool IsWheelish(Node n)
		{
			Node? cur = n;
			while (cur != null && cur != model)
			{
				if (cur.Name.ToString().StartsWith("wheel", StringComparison.OrdinalIgnoreCase))
					return true;
				cur = cur.GetParent();
			}
			return false;
		}

		bool IsGlassish(Node n)
		{
			var nm = n.Name.ToString();
			return nm.Contains("glass", StringComparison.OrdinalIgnoreCase)
				|| nm.Contains("window", StringComparison.OrdinalIgnoreCase);
		}

		var named = all
			.Where(m => !IsWheelish(m) && m.Name.ToString().Contains("body", StringComparison.OrdinalIgnoreCase))
			.ToList();
		if (named.Count > 0) return named;

		MeshInstance3D? best = null;
		var bestVol = -1f;
		foreach (var m in all)
		{
			if (IsWheelish(m) || IsGlassish(m)) continue;
			var size = m.GetAabb().Size;
			var vol = size.X * size.Y * size.Z;
			if (vol > bestVol)
			{
				bestVol = vol;
				best = m;
			}
		}

		var result = new List<MeshInstance3D>();
		if (best != null) result.Add(best);
		return result;
	}

	private void BuildProxyVehicleVisual(Node3D parent)
	{
		// Body
		var body = new MeshInstance3D
		{
			Name = "Body_Main",
			Mesh = new BoxMesh { Size = new Vector3(1.85f, 0.55f, 3.25f) },
			Position = new Vector3(0f, 0.30f, 0f)
		};
		parent.AddChild(body);

		// Cabin
		var cabin = new MeshInstance3D
		{
			Name = "Body_Cabin",
			Mesh = new BoxMesh { Size = new Vector3(1.25f, 0.50f, 1.35f) },
			Position = new Vector3(0f, 0.68f, -0.25f)
		};
		parent.AddChild(cabin);

		// Hood bump
		var hood = new MeshInstance3D
		{
			Name = "Body_Hood",
			Mesh = new BoxMesh { Size = new Vector3(1.35f, 0.15f, 0.95f) },
			Position = new Vector3(0f, 0.58f, -1.15f)
		};
		parent.AddChild(hood);

		// Wheels (darker)
		var wheelMat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.10f, 0.10f, 0.10f),
			Roughness = 1.0f,
			Metallic = 0.0f
		};
		AddWheelProxy(parent, "Wheel_FL", new Vector3(-0.95f, 0.15f, -1.25f), wheelMat);
		AddWheelProxy(parent, "Wheel_FR", new Vector3(0.95f, 0.15f, -1.25f), wheelMat);
		AddWheelProxy(parent, "Wheel_RL", new Vector3(-0.95f, 0.15f, 1.25f), wheelMat);
		AddWheelProxy(parent, "Wheel_RR", new Vector3(0.95f, 0.15f, 1.25f), wheelMat);
	}

	private void AddWheelProxy(Node3D parent, string name, Vector3 pos, StandardMaterial3D mat)
	{
		// Use a pivot so we can steer the front wheels without affecting the cylinder axis orientation.
		var pivot = new Node3D { Name = $"{name}_Pivot", Position = pos };
		var w = new MeshInstance3D
		{
			Name = name,
			Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.22f, Height = 0.18f, RadialSegments = 14 },
			// Cylinder axis is Y; rotate so axis becomes X (wheel left-right), so it looks like a tire.
			RotationDegrees = new Vector3(0f, 0f, 90f)
		};
		w.SetSurfaceOverrideMaterial(0, mat);
		pivot.AddChild(w);
		parent.AddChild(pivot);

		// Keep references to front wheel pivots for visual steering.
		if (name == "Wheel_FL") _wheelFlPivot = pivot;
		if (name == "Wheel_FR") _wheelFrPivot = pivot;
		if (name == "Wheel_RL") _wheelRlPivot = pivot;
		if (name == "Wheel_RR") _wheelRrPivot = pivot;
	}

	private void UpdateFrontWheelSteer(float dt, float steerRad)
	{
		if (_wheelFlPivot == null || _wheelFrPivot == null) return;
		if (dt <= 0.0001f) return;

		var targetDeg = Mathf.RadToDeg(steerRad);
		_frontWheelSteerDeg = Mathf.Lerp(_frontWheelSteerDeg, targetDeg, 1f - Mathf.Exp(-FrontWheelSteerLerp * dt));

		var rot = new Vector3(0f, _frontWheelSteerDeg, 0f);
		_wheelFlPivot.RotationDegrees = rot;
		_wheelFrPivot.RotationDegrees = rot;
	}

	private void UpdateTireVfx(float dt, float vForward, float vLateral)
	{
		if (dt <= 0.0001f) return;
		if (_vehicleDef == null || _vehicleRuntime == null) return;
		_arenaWorldCached ??= FindArenaWorld();
		if (_arenaWorldCached == null) return;

		var tireHp = _vehicleRuntime.CurrentTireHp;
		if (tireHp == null || tireHp.Length < 4) return;

		var speed = MathF.Abs(vForward);
		var slip = MathF.Abs(vLateral);
		var yaw = GlobalRotation.Y;

		for (var i = 0; i < 4; i++)
		{
			_smokeCooldown[i] = MathF.Max(0f, _smokeCooldown[i] - dt);
			_skidCooldown[i] = MathF.Max(0f, _skidCooldown[i] - dt);

			var blown = tireHp[i] <= 0;
			if (blown && !_blownTires[i])
			{
				_blownTires[i] = true;
				// One-time burst when the tire first blows.
				ArenaVfx.SpawnSparks(_arenaWorldCached, GetWheelWorld(i) + new Vector3(0f, 0.06f, 0f), count: 6);
				// Persistent flat-tire pad + rubber chips so a destroyed tire reads from the RTS camera.
				BuildBlownTireDebris(i);
			}
			if (!blown)
			{
				if (_blownTires[i])
					RemoveBlownTireDebris(i);
				_blownTires[i] = false;
				continue;
			}

			// Smoke puffs while moving.
			if (speed > 2.0f && _smokeCooldown[i] <= 0f)
			{
				_smokeCooldown[i] = 0.10f;
				var at = GetWheelWorld(i) + new Vector3(0f, 0.10f, 0f);
				SmokePuff3D.Spawn(_arenaWorldCached.GetVfxRoot(), at);
			}

			// Skid marks when slipping/braking.
			if (speed > 3.0f && (slip > 1.2f || (ThrottleInput < -0.35f && vForward > 2.0f)) && _skidCooldown[i] <= 0f)
			{
				_skidCooldown[i] = 0.07f;
				var at = GetWheelWorld(i);
				ArenaVfx.SpawnSkidMark(_arenaWorldCached, new Vector3(at.X, 0.01f, at.Z), yaw, ttlSeconds: 7.5f);
				if (speed > 6.0f && Random.Shared.NextDouble() < 0.25)
					ArenaVfx.SpawnSparks(_arenaWorldCached, at + new Vector3(0f, 0.05f, 0f), count: 2);
			}
		}
	}

	private Vector3 GetWheelWorld(int idx)
	{
		Node3D? p = idx switch
		{
			0 => _wheelFlPivot,
			1 => _wheelFrPivot,
			2 => _wheelRlPivot,
			3 => _wheelRrPivot,
			_ => null
		};
		if (p != null && GodotObject.IsInstanceValid(p))
			return p.GlobalPosition;
		// Fallback: approximate around the chassis.
		return GlobalPosition + (idx switch
		{
			0 => new Vector3(-0.95f, 0.15f, -1.25f),
			1 => new Vector3(0.95f, 0.15f, -1.25f),
			2 => new Vector3(-0.95f, 0.15f, 1.25f),
			3 => new Vector3(0.95f, 0.15f, 1.25f),
			_ => Vector3.Zero
		});
	}

	// --- Top-down identity VFX (contact shadow, facing wedge) ---
	// The fixed RTS camera sits ~37m out at ~51.6 deg; these cheap always-on accents keep vehicles
	// readable against the arena floor:
	// - a soft elliptical contact shadow grounds the chassis instantly,
	// - a small emissive windshield wedge on the hood line makes facing readable at a glance.
	// (Emissive side underglow strips used to live here too; play-testing read them as meaningless
	// colored lines on the ground beside every car, so they were removed — hull detail + shadow +
	// the target indicator carry the silhouette.)
	// All nodes live under a pawn-level "IdentityVfx" root (NOT under "Visual", which gets cleared
	// whenever the def model rebuilds).
	private Node3D? _identityRoot;
	private MeshInstance3D? _contactShadow;
	private MeshInstance3D? _facingWedge;
	private bool _identityDestroyed;
	private float _visualTopLocalY = 0.95f;

	// Blown-tire debris (flat-tire pad + rubber chips), one persistent node per wheel.
	private Node3D? _tireDebrisRoot;
	private readonly Node3D?[] _tireDebris = new Node3D?[4];

	// Shared soft radial-falloff blob (white with alpha; AlbedoColor supplies the tint).
	private static ImageTexture? _softBlobTexture;

	/// <summary>Footprint scale relative to the 3.25 m reference chassis (drives shadow/decal sizing).</summary>
	public float VisualFootprintScale => CurrentDefModelTargetLength() / 3.25f;

	/// <summary>
	/// Ground-plane bounding-circle radius of the hull in meters (pawn origin to a hull corner),
	/// derived from the def's visual length and the reference chassis aspect (1.8 m wide : 3.25 m
	/// long). Blast/proximity math uses this so explosions measure against the SURFACE of the
	/// vehicle instead of its center point (a 2.4 m blast next to a 3.2 m hull is a contact hit,
	/// not a half-damage near miss).
	/// </summary>
	public float HullBoundingRadius
	{
		get
		{
			var length = CurrentDefModelTargetLength();
			var halfLength = length * 0.5f;
			var halfWidth = length * (0.9f / 3.25f);
			return MathF.Sqrt(halfLength * halfLength + halfWidth * halfWidth);
		}
	}

	/// <summary>Top of the visual hull in pawn-local space (estimated until the model is measured).</summary>
	public float VisualTopLocalY => _visualTopLocalY;

	/// <summary>
	/// Wrecks go dark: hides the emissive identity accents (underglow + facing wedge) but keeps the
	/// grounding contact shadow. Called by <see cref="VehicleDamageVfx.MarkDestroyed"/> because the
	/// charring pass there only touches the "Visual" subtree.
	/// </summary>
	public void SetIdentityDestroyed()
	{
		_identityDestroyed = true;
		if (_facingWedge != null && GodotObject.IsInstanceValid(_facingWedge))
			_facingWedge.Visible = false;
	}

	private void EnsureIdentityVisuals()
	{
		if (_identityRoot == null || !GodotObject.IsInstanceValid(_identityRoot))
		{
			_identityRoot = new Node3D { Name = "IdentityVfx" };
			AddChild(_identityRoot);

			// 1) Contact shadow: flat dark blob just above the floor (pawn origin is at world y=0.4).
			var shadowMat = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				AlbedoColor = new Color(0f, 0f, 0f, 0.50f),
				AlbedoTexture = GetSoftBlobTexture(),
			};
			_contactShadow = new MeshInstance3D
			{
				Name = "ContactShadow",
				Mesh = new QuadMesh { Size = new Vector2(1f, 1f) },
				RotationDegrees = new Vector3(-90f, 0f, 0f),
				Position = new Vector3(0f, -0.375f, 0f),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			_contactShadow.SetSurfaceOverrideMaterial(0, shadowMat);
			_identityRoot.AddChild(_contactShadow);

			// 2) Facing wedge: low flat windshield-glass triangle pointing at the nose.
			// PrismMesh apex is +Y; rotating -90 deg about X points the apex along -Z (forward).
			var wedgeColor = new Color(0.72f, 0.93f, 1.0f);
			var wedgeMat = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = wedgeColor,
				EmissionEnabled = true,
				Emission = wedgeColor,
				EmissionEnergyMultiplier = 1.5f,
			};
			_facingWedge = new MeshInstance3D
			{
				Name = "FacingWedge",
				Mesh = new PrismMesh { Size = Vector3.One },
				RotationDegrees = new Vector3(-90f, 0f, 0f),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			_facingWedge.SetSurfaceOverrideMaterial(0, wedgeMat);
			_identityRoot.AddChild(_facingWedge);
		}

		RefreshIdentityVisuals();
	}

	/// <summary>
	/// Re-fits the identity accents to the current visual: measures the built model's AABB when the
	/// pawn is in-tree (def models vary per class), otherwise falls back to footprint-scaled
	/// estimates. Safe to call repeatedly (runs on _Ready/ConfigureLoadout/ApplyVisualPreset).
	/// </summary>
	private void RefreshIdentityVisuals()
	{
		if (_identityRoot == null || !GodotObject.IsInstanceValid(_identityRoot))
			return;

		var s = Mathf.Clamp(VisualFootprintScale, 0.6f, 1.8f);
		var halfW = 0.93f * s;
		var len = 3.25f * s;
		var topY = 0.95f;
		if (IsInsideTree() && _visualRoot != null && GodotObject.IsInstanceValid(_visualRoot)
			&& TryComputeAabbInSpace(_visualRoot, _visualRoot, out var aabb)
			&& aabb.Size.X > 0.2f && aabb.Size.Z > 0.5f)
		{
			halfW = Mathf.Clamp(aabb.Size.X * 0.5f, 0.55f, 1.6f);
			len = Mathf.Clamp(aabb.Size.Z, 2.0f, 6.0f);
			topY = Mathf.Clamp(aabb.End.Y, 0.50f, 1.6f);
		}
		_visualTopLocalY = topY;

		if (_contactShadow != null && GodotObject.IsInstanceValid(_contactShadow))
			_contactShadow.Scale = new Vector3(halfW * 2f + 0.9f, len + 1.0f, 1f);

		// Trailers are cargo, not combatants: the emissive combat-identity accents read as a
		// phantom "windshield" wedge over the coupler at gameplay zoom. Keep only the grounding
		// contact shadow for trailer pawns.
		var isTrailer = _vehicleDef?.Class == VehicleClass.Trailer;

		if (_facingWedge != null && GodotObject.IsInstanceValid(_facingWedge))
		{
			// Hood/windshield line: slightly above the measured roof so it never clips per-class
			// models; local scale (width, forward length, thickness) maps through the -90 X rotation.
			_facingWedge.Position = new Vector3(0f, topY + 0.05f, -len * 0.28f);
			_facingWedge.Scale = new Vector3(0.60f * s, 0.55f * s, 0.06f);
			_facingWedge.Visible = !_identityDestroyed && !isTrailer;
		}
	}

	private static ImageTexture GetSoftBlobTexture()
	{
		if (_softBlobTexture != null)
			return _softBlobTexture;
		const int n = 64;
		var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
		for (var y = 0; y < n; y++)
		{
			for (var x = 0; x < n; x++)
			{
				var dx = (x + 0.5f) / n * 2f - 1f;
				var dy = (y + 0.5f) / n * 2f - 1f;
				var d = MathF.Sqrt(dx * dx + dy * dy);
				var a = Mathf.Clamp(1f - d, 0f, 1f);
				a = a * a * (3f - 2f * a); // smoothstep falloff, soft edge
				img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
			}
		}
		_softBlobTexture = ImageTexture.CreateFromImage(img);
		return _softBlobTexture;
	}

	/// <summary>
	/// Deterministic per-pawn seed. string.GetHashCode is randomized per process, so use a plain
	/// char-sum: debris layouts stay identical across runs (screenshot-verification determinism).
	/// </summary>
	private int StablePawnSeed()
	{
		var name = Name.ToString();
		var h = 17;
		foreach (var c in name)
			h = unchecked(h * 31 + c);
		return h;
	}

	private void EnsureTireDebrisRoot()
	{
		if (_tireDebrisRoot != null && GodotObject.IsInstanceValid(_tireDebrisRoot))
			return;
		_tireDebrisRoot = new Node3D { Name = "TireDebris" };
		AddChild(_tireDebrisRoot);
	}

	/// <summary>
	/// Persistent blown-tire read: a dark flat-tire pad where the tire meets the ground plus a few
	/// shredded-rubber chips around the rim. All flat ground-level quads (no floating billboards),
	/// attached to the pawn so they track the wheel; placement is seeded on pawn name + wheel index.
	/// </summary>
	private void BuildBlownTireDebris(int tireIndex)
	{
		if (tireIndex < 0 || tireIndex >= _tireDebris.Length)
			return;
		var existing = _tireDebris[tireIndex];
		if (existing != null && GodotObject.IsInstanceValid(existing))
			return;
		EnsureTireDebrisRoot();

		var local = ToLocal(GetWheelWorld(tireIndex));
		var root = new Node3D { Name = $"TireDebris{tireIndex}", Position = new Vector3(local.X, 0f, local.Z) };
		_tireDebrisRoot!.AddChild(root);
		_tireDebris[tireIndex] = root;

		var rubber = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = new Color(0.05f, 0.05f, 0.055f, 0.85f),
			AlbedoTexture = GetSoftBlobTexture(),
		};

		// Flat-tire pad: squashed dark blob under the wheel (world y ~0.028; above skid marks).
		var pad = new MeshInstance3D
		{
			Name = "FlatPad",
			Mesh = new QuadMesh { Size = new Vector2(1f, 1f) },
			RotationDegrees = new Vector3(-90f, 0f, 0f),
			Position = new Vector3(0f, -0.372f, 0.03f),
			Scale = new Vector3(0.50f, 0.62f, 1f),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		pad.SetSurfaceOverrideMaterial(0, rubber);
		root.AddChild(pad);

		var rnd = new Random(unchecked(StablePawnSeed() * 31 + tireIndex * 101));
		for (var k = 0; k < 3; k++)
		{
			var ang = (float)(rnd.NextDouble() * Math.Tau);
			var dist = 0.22f + (float)rnd.NextDouble() * 0.22f;
			var sz = 0.08f + (float)rnd.NextDouble() * 0.08f;
			var chip = new MeshInstance3D
			{
				Name = $"Chip{k}",
				Mesh = new QuadMesh { Size = new Vector2(1f, 1f) },
				// Flat on the ground; yaw varies per chip. Staggered y avoids z-fighting the pad.
				RotationDegrees = new Vector3(-90f, (float)(rnd.NextDouble() * 180.0), 0f),
				Position = new Vector3(MathF.Cos(ang) * dist, -0.369f + k * 0.002f, MathF.Sin(ang) * dist),
				Scale = new Vector3(sz, sz * (0.6f + (float)rnd.NextDouble() * 0.5f), 1f),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			chip.SetSurfaceOverrideMaterial(0, rubber);
			root.AddChild(chip);
		}
	}

	private void RemoveBlownTireDebris(int tireIndex)
	{
		if (tireIndex < 0 || tireIndex >= _tireDebris.Length)
			return;
		var node = _tireDebris[tireIndex];
		if (node != null && GodotObject.IsInstanceValid(node))
			node.QueueFree();
		_tireDebris[tireIndex] = null;
	}

	private void EnsureMountsFallback()
	{
		// If we haven't been configured from defs yet, create a minimal mount set.
		if (_mountsRoot != null && _mountById.Count > 0) return;
		_mountsRoot = GetNodeOrNull<Node3D>("Mounts");
		if (_mountsRoot == null)
		{
			_mountsRoot = new Node3D { Name = "Mounts" };
			AddChild(_mountsRoot);
		}
		// Front fixed
		CreateFixedMount("F1", MountLocation.Front, new Vector3(0f, 0.62f, -1.68f), 0f);
		// Top turret
		CreateTurretMount("R1", new Vector3(0f, 0.95f, 0.0f), -180f, 180f);
	}

	private void EnsureMountPoints(VehicleDefinition vdef)
	{
		_mountsRoot = GetNodeOrNull<Node3D>("Mounts");
		if (_mountsRoot == null)
		{
			_mountsRoot = new Node3D { Name = "Mounts" };
			AddChild(_mountsRoot);
		}

		// Clear and rebuild (mount defs can change per vehicle).
		foreach (var childObj in _mountsRoot.GetChildren())
			(childObj as Node)?.QueueFree();
		_mountById.Clear();
		_turretsByMountId.Clear();

		foreach (var m in vdef.MountPoints)
		{
			var pos = MountPositionFor(m.MountLocation);
			var yawDeg = MountYawDegFor(m.MountLocation);

			if (m.Kind == WeaponMountKind.Turret || m.ArcDegrees >= 180f || m.CanAutoAim)
			{
				// Limited-arc mounts (spec): 0 < ArcDegrees < 180 restricts tracking to the mount
				// facing +/- Arc/2 (vehicle-local). 0 or >= 180 stays an unrestricted turret.
				// Explicit def Yaw{Min,Max}Degrees remain the fine-grain override when present.
				var minYaw = m.YawMinDegrees;
				var maxYaw = m.YawMaxDegrees;
				if (m.ArcDegrees > 0f && m.ArcDegrees < 180f)
				{
					var halfArc = m.ArcDegrees * 0.5f;
					minYaw ??= yawDeg - halfArc;
					maxYaw ??= yawDeg + halfArc;
				}
				CreateTurretMount(m.MountId, pos, minYaw, maxYaw, yawDeg);
			}
			else
			{
				CreateFixedMount(m.MountId, m.MountLocation, pos, yawDeg);
			}
		}
	}

	private static Vector3 MountPositionFor(MountLocation loc)
	{
		return loc switch
		{
			MountLocation.Front => new Vector3(0f, 0.62f, -1.68f),
			MountLocation.Rear => new Vector3(0f, 0.62f, 1.68f),
			MountLocation.Left => new Vector3(-1.05f, 0.62f, 0.0f),
			MountLocation.Right => new Vector3(1.05f, 0.62f, 0.0f),
			MountLocation.Top => new Vector3(0f, 0.95f, 0.0f),
			_ => new Vector3(0f, 0.62f, 0f)
		};
	}

	private static float MountYawDegFor(MountLocation loc)
	{
		// Default orientation: forward is vehicle forward (-Z).
		return loc switch
		{
			MountLocation.Rear => 180f,
			MountLocation.Left => -90f,
			MountLocation.Right => 90f,
			_ => 0f
		};
	}

	private void CreateFixedMount(string mountId, MountLocation loc, Vector3 pos, float yawDeg)
	{
		if (_mountsRoot == null) return;
		var marker = new Marker3D { Name = $"Mount_{mountId}" };
		marker.Position = pos;
		marker.RotationDegrees = new Vector3(0f, yawDeg, 0f);
		_mountsRoot.AddChild(marker);
		_mountById[mountId] = marker;
	}

	private void CreateTurretMount(string mountId, Vector3 pos, float? yawMinDeg, float? yawMaxDeg, float neutralYawDeg = 0f)
	{
		if (_mountsRoot == null) return;
		var pivot = new Node3D { Name = $"TurretPivot_{mountId}", Position = pos };
		_mountsRoot.AddChild(pivot);
		var marker = new Marker3D { Name = $"Mount_{mountId}" };
		pivot.AddChild(marker);
		_mountById[mountId] = marker;
		_turretsByMountId[mountId] = new TurretInfo { Pivot = pivot, MinYawDeg = yawMinDeg, MaxYawDeg = yawMaxDeg, NeutralYawDeg = neutralYawDeg };
	}

	private void AttachWeaponVisuals()
	{
		_weaponsRoot = GetNodeOrNull<Node3D>("Weapons");
		if (_weaponsRoot == null)
		{
			_weaponsRoot = new Node3D { Name = "Weapons" };
			AddChild(_weaponsRoot);
		}
		foreach (var childObj in _weaponsRoot.GetChildren())
			(childObj as Node)?.QueueFree();
		_primaryMuzzle = null;
		_primaryMountId = null;
		_muzzleByMountId.Clear();

		if (_vehicleRuntime == null) return;
		if (_vehicleRuntime.InstalledWeaponsByMountId.Count == 0) return;

		// Pick a "primary" mount.
		// 1) Prefer the machine gun (our default test weapon) if present.
		// 2) Else prefer a Front mount if present.
		// 3) Else first key.
		var primary = _vehicleRuntime.InstalledWeaponsByMountId.Keys.FirstOrDefault();

// Prefer an MG-type weapon as the "primary" if possible (covers 9mm MG, 50cal MG, etc.).
if (_defs != null)
{
	foreach (var kv in _vehicleRuntime.InstalledWeaponsByMountId)
	{
		if (_defs.Weapons.TryGetValue(kv.Value.WeaponId, out var wdef) && wdef.WeaponType == WeaponType.MG)
		{
			primary = kv.Key;
			break;
		}
	}
}
else
{
	foreach (var kv in _vehicleRuntime.InstalledWeaponsByMountId)
	{
		if (string.Equals(kv.Value.WeaponId, "wpn_mg", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(kv.Value.WeaponId, "wpn_mg_50cal", StringComparison.OrdinalIgnoreCase))
		{
			primary = kv.Key;
			break;
		}
	}
}
if (_vehicleDef != null)
		{
			var frontMount = _vehicleDef.MountPoints.FirstOrDefault(m => m.MountLocation == MountLocation.Front);
			if (frontMount != null && _vehicleRuntime.InstalledWeaponsByMountId.ContainsKey(frontMount.MountId))
				primary = frontMount.MountId;
		}
		_primaryMountId = primary;

		foreach (var kv in _vehicleRuntime.InstalledWeaponsByMountId)
		{
			var mountId = kv.Key;
			if (!_mountById.TryGetValue(mountId, out var mountMarker))
				continue;

			var weapon = WeaponVisualFactory.CreateWeaponVisual(mountId, kv.Value.WeaponId);
			mountMarker.AddChild(weapon);
			// If this weapon has a configured 3D model visual, auto-align/scale it now.
			WeaponVisualFactory.TryAutoAlignWeaponVisual(weapon);
			// Align weapon so its internal MountPoint coincides with the mount marker origin.
			var wMount = weapon.GetNodeOrNull<Marker3D>("MountPoint");
			if (wMount != null)
				weapon.Transform = wMount.Transform.AffineInverse();

			// The transform snap above wipes root scale — re-apply the config's slimming factors.
			WeaponVisualFactory.ApplyCrossAxisScale(weapon);

			DisableWeaponCollision(weapon);

			var muzzle = weapon.GetNodeOrNull<Marker3D>("Muzzle");
			if (muzzle != null)
				_muzzleByMountId[mountId] = muzzle;

			if (_primaryMuzzle == null && mountId == _primaryMountId)
				_primaryMuzzle = muzzle;
		}
	}

	private static void DisableWeaponCollision(Node node)
	{
		// Future-proof: if weapon scenes include collisions, ensure they don't block or collide with the parent vehicle.
		if (node is CollisionObject3D co)
		{
			co.CollisionLayer = 0;
			co.CollisionMask = 0;
		}
		foreach (var childObj in node.GetChildren())
		{
			if (childObj is Node child)
				DisableWeaponCollision(child);
		}
	}

	private static Node3D CreateBoxWeaponVisual(string mountId, string weaponId)
	{
		var root = new Node3D { Name = $"Weapon_{mountId}" };
		// Marker defining how the weapon attaches to a vehicle mount.
		var mount = new Marker3D { Name = "MountPoint" };
		root.AddChild(mount);

		// Weapon mesh (proxy).
		var mesh = new MeshInstance3D
		{
			Name = "Mesh",
			Mesh = new BoxMesh { Size = new Vector3(0.38f, 0.20f, 0.85f) },
			Position = new Vector3(0f, 0.12f, -0.35f)
		};
		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.55f, 0.55f, 0.60f),
			Roughness = 0.55f,
			Metallic = 0.25f
		};
		mesh.SetSurfaceOverrideMaterial(0, mat);
		root.AddChild(mesh);

		// Muzzle marker (used for tracer origin + aim direction).
		var muzzle = new Marker3D { Name = "Muzzle", Position = new Vector3(0f, 0.15f, -0.82f) };
		root.AddChild(muzzle);

		// Label node for debugging (in case we want to show weapon id in editor).
		root.SetMeta("weapon_id", weaponId);
		return root;
	}

	private void UpdateTurrets()
	{
		if (_turretsByMountId.Count == 0) return;
		var aim = AimWorldPosition;

		foreach (var kv in _turretsByMountId)
		{
			var info = kv.Value;
			if (info.Pivot == null || !GodotObject.IsInstanceValid(info.Pivot))
				continue;

			// Fixed-angle fallback (master spec): a weapon beyond the computer's control groups
			// loses auto-tracking — its mount locks to the neutral bearing like a fixed gun.
			if (_computerLockedMountIds.Contains(kv.Key))
			{
				info.Pivot.RotationDegrees = new Vector3(0f, info.NeutralYawDeg, 0f);
				continue;
			}

			if (aim == Vector3.Zero)
				continue;

			var pivotPos = info.Pivot.GlobalPosition;
			var toAim = aim - pivotPos;
			toAim.Y = 0f;
			if (toAim.Length() < 0.01f)
				continue;
			toAim = toAim.Normalized();
			var desiredYawGlobal = Mathf.Atan2(toAim.X, toAim.Z) + Mathf.Pi;
			// Convert to local yaw relative to the vehicle so the turret stays stable under vehicle rotation.
			var localYaw = Mathf.Wrap(desiredYawGlobal - GlobalRotation.Y, -Mathf.Pi, Mathf.Pi);
			var localYawDeg = Mathf.RadToDeg(localYaw);

			if (info.MinYawDeg.HasValue && info.MaxYawDeg.HasValue)
			{
				// Wrap-aware clamp: rear/side arcs (e.g. 180 +/- 5) must snap to the nearest arc
				// edge, not across the +/-180 wrap seam. No-op for full turrets (-180..180).
				var center = (info.MinYawDeg.Value + info.MaxYawDeg.Value) * 0.5f;
				var halfRange = (info.MaxYawDeg.Value - info.MinYawDeg.Value) * 0.5f;
				var rel = Mathf.Wrap(localYawDeg - center, -180f, 180f);
				localYawDeg = center + Mathf.Clamp(rel, -halfRange, halfRange);
			}
			else
			{
				if (info.MinYawDeg.HasValue) localYawDeg = MathF.Max(info.MinYawDeg.Value, localYawDeg);
				if (info.MaxYawDeg.HasValue) localYawDeg = MathF.Min(info.MaxYawDeg.Value, localYawDeg);
			}

			info.Pivot.RotationDegrees = new Vector3(0f, localYawDeg, 0f);
		}
	}

	/// <summary>
	/// Mass hitched to this pawn that the instance's own breakdown can't see (the overworld tow
	/// chain in-arena — spec: you defend what you haul, and it weighs you down in the fight).
	/// Set by the arena view at spawn; 0 for fresh pawns.
	/// </summary>
	public float ExternalTowedMassKg { get; set; }

	protected override void UpdateRuntimeDerivedStats()
	{
		// Defaults
		EffectiveMaxForwardSpeed = MaxForwardSpeed;
		EffectiveMaxReverseSpeed = MaxReverseSpeed;
		TotalMassKg = _vehicleDef?.BaseMassKg ?? 0f;
		TractionGrip = 1f;
		SteerGrip = 1f;
		DriveGrip = 1f;

		if (_vehicleDef == null || _vehicleRuntime == null || _defs == null)
			return;

		TotalMassKg = VehicleMassMath.ComputeTotalMassKg(_vehicleDef, _vehicleRuntime, _defs) + MathF.Max(0f, ExternalTowedMassKg);
		// Power-to-weight: the installed engine's output caps how much mass it can actually move.
		var enginePowerKw = _vehicleRuntime.InstalledEngineId != null
			&& _defs.Engines.TryGetValue(_vehicleRuntime.InstalledEngineId, out var engineDef)
				? engineDef.PowerKw
				: 0f;
		var speedFactor = VehicleMassMath.ComputeSpeedFactor(_vehicleDef.BaseMassKg, TotalMassKg, enginePowerKw);
		EffectiveMaxForwardSpeed = MaxForwardSpeed * speedFactor;
		EffectiveMaxReverseSpeed = MaxReverseSpeed * speedFactor;

		// Tire condition -> handling. Use HP (not armor) as the determinant.
		var maxHp = Math.Max(1, _vehicleDef.BaseTireHp);
		float Pct(int idx)
		{
			if (_vehicleRuntime.CurrentTireHp is not { Length: > 0 }) return 1f;
			if (idx < 0 || idx >= _vehicleRuntime.CurrentTireHp.Length) return 1f;
			var cur = Math.Clamp(_vehicleRuntime.CurrentTireHp[idx], 0, maxHp);
			return (float)cur / maxHp;
		}

		var fl = Pct(0);
		var fr = Pct(1);
		var rl = Pct(2);
		var rr = Pct(3);
		var frontAvg = (fl + fr) * 0.5f;
		var rearAvg = (rl + rr) * 0.5f;
		var allAvg = (fl + fr + rl + rr) * 0.25f;

		SteerGrip = Mathf.Clamp(0.25f + 0.75f * frontAvg, 0.25f, 1f);
		DriveGrip = Mathf.Clamp(0.25f + 0.75f * rearAvg, 0.25f, 1f);
		TractionGrip = Mathf.Clamp(0.20f + 0.80f * allAvg, 0.20f, 1f);

		// Extra penalty: if both front tires are essentially gone, steering becomes *much* harder.
		if (fl <= 0.05f && fr <= 0.05f)
			SteerGrip = Mathf.Clamp(SteerGrip * 0.35f, 0.08f, 1f);

		// Oil slick: drape a slippery surface over whatever tire-based grip we computed. Use Min so oil
		// only ever *reduces* grip. Low traction => the car keeps sliding (less lateral friction); poor
		// drive grip => it can't simply power/brake straight out; vague steering => loose direction.
		if (_oilSlipRemaining > 0f)
		{
			TractionGrip = MathF.Min(TractionGrip, 0.22f);
			DriveGrip = MathF.Min(DriveGrip, 0.30f);
			SteerGrip = MathF.Min(SteerGrip, 0.38f);
		}
	}

	protected override float ComputeMassAccelFactor()
	{
		if (_vehicleDef == null) return 1f;
		var baseMass = MathF.Max(1f, _vehicleDef.BaseMassKg);
		var total = TotalMassKg <= 0 ? baseMass : TotalMassKg;
		var massFactor = Mathf.Clamp(baseMass / MathF.Max(baseMass, total), 0.45f, 1.15f);
		return Mathf.Clamp(MathF.Pow(massFactor, 1.0f), 0.45f, 1.15f);
	}


	private ArenaWorld? FindArenaWorld()
	{
		Node? cur = this;
		while (cur != null)
		{
			if (cur is ArenaWorld aw) return aw;
			cur = cur.GetParent();
		}
		return null;
	}

	private void EnsureHitboxes()
	{
		// Non-blocking Area3D hitboxes so ray hits can identify parts.
		if (_hitboxes != null) return;

		_hitboxes = new Node3D { Name = "Hitboxes" };
		AddChild(_hitboxes);

		// Section hitboxes (non-overlapping slices) for positional damage.
		// IMPORTANT: Our weapon mount/muzzle points sit higher than the pawn's movement collider.
		// Since bullets currently travel in a flat top-down plane, the hitboxes must extend upward
		// enough for shots fired from raised mounts (ex: y~0.8) to still intersect the vehicle.
		CreateHitbox("SecFront", new Vector3(0f, 0.45f, -1.10f), new Vector3(1.8f, 1.4f, 1.0f), "hit_section");
		CreateHitbox("SecRear", new Vector3(0f, 0.45f, 1.10f), new Vector3(1.8f, 1.4f, 1.0f), "hit_section");
		CreateHitbox("SecLeft", new Vector3(-0.60f, 0.45f, 0.0f), new Vector3(0.6f, 1.4f, 1.2f), "hit_section");
		CreateHitbox("SecRight", new Vector3(0.60f, 0.45f, 0.0f), new Vector3(0.6f, 1.4f, 1.2f), "hit_section");
		CreateHitbox("SecTop", new Vector3(0.0f, 0.92f, 0.0f), new Vector3(0.9f, 0.45f, 1.2f), "hit_section");
		CreateHitbox("SecUnder", new Vector3(0.0f, -0.30f, 0.0f), new Vector3(0.9f, 0.40f, 1.2f), "hit_section");

		// Driver/cabin hit area (counts as direct driver hit).
		CreateHitbox("Driver", new Vector3(0f, 0.62f, -0.10f), new Vector3(0.8f, 1.1f, 1.2f), "hit_driver");

		// Tires (rough positions)
		CreateHitbox("TireFL", new Vector3(-0.9f, -0.05f, -1.3f), new Vector3(0.5f, 0.5f, 0.5f), "hit_tire");
		CreateHitbox("TireFR", new Vector3(0.9f, -0.05f, -1.3f), new Vector3(0.5f, 0.5f, 0.5f), "hit_tire");
		CreateHitbox("TireRL", new Vector3(-0.9f, -0.05f, 1.3f), new Vector3(0.5f, 0.5f, 0.5f), "hit_tire");
		CreateHitbox("TireRR", new Vector3(0.9f, -0.05f, 1.3f), new Vector3(0.5f, 0.5f, 0.5f), "hit_tire");
	}

	private void CreateHitbox(string name, Vector3 localPos, Vector3 size, string group)
	{
		var area = new Area3D { Name = name };
		area.AddToGroup(group);
		area.AddToGroup("hitbox");
		area.Position = localPos;

		// Give hitboxes their own collision layer so we can raycast them easily.
		area.CollisionLayer = 1u << 6; // layer 7
		area.CollisionMask = 0;

		var col = new CollisionShape3D();
		col.Shape = new BoxShape3D { Size = size };
		area.AddChild(col);
		_hitboxes!.AddChild(area);
	}
}
