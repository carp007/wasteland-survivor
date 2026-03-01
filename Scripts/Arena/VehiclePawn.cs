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
public partial class VehiclePawn : CharacterBody3D, IVehicleAudioTelemetry
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
	[Export] public int PassengerCarPackEnemyVariantIndex = 0;  // Body 0 for enemy
	[Export] public float PassengerCarPackTargetLength = 3.25f; // roughly matches proxy length
	[Export] public float PassengerCarPackMinVisibleLength = 0.5f; // if model imports tiny, we autoscale
	[Export] public bool PassengerCarPackAutoAlignYaw = true; // attempts to align model long axis to vehicle forward (-Z)
	// When true, logs model path + computed alignment vectors/angles to the Godot Output.
	[Export] public bool PassengerCarPackDebugAlignment = true;
	[Export] public Vector3 PassengerCarPackRotationDegrees = Vector3.Zero; // tweak if imported forward axis is off
	[Export] public Vector3 PassengerCarPackExtraScale = Vector3.One; // extra user scale multiplier

	[Export] public float MaxForwardSpeed = 18.0f;
	[Export] public float MaxReverseSpeed = 8.0f;
	[Export] public float Accel = 16.0f;
	[Export] public float AccelNearMaxFactor = 0.35f;
	[Export] public float AccelSpeedFalloffExponent = 1.6f;

	// Realism tuning (bicycle model + friction). Keep defaults gentle for early gameplay.
	[Export] public float WheelbaseMeters = 2.6f;
	[Export] public float CoastDecel = 3.5f;
	[Export] public float BrakeDecel = 58.0f;
	[Export] public float LateralFriction = 26.0f;
	[Export] public float LinearDrag = 0.06f;
	[Export] public float QuadraticDrag = 0.0012f;

	[Export] public float FrontWheelMaxSteerDeg = 28f;
	[Export] public float FrontWheelSteerLerp = 14f;

	[Export] public Color BodyColor = new(0.85f, 0.85f, 0.85f);

	public float ThrottleInput { get; set; } = 0f; // -1..1
	public float SteerInput { get; set; } = 0f;    // -1..1

	public Vector3 AimWorldPosition { get; set; }

	public float FireCooldownSeconds { get; set; } = 0.22f;
	public float FireCooldownRemaining { get; set; } = 0f;

	// Runtime-derived performance (weight + damage). These are recomputed automatically.
	public float TotalMassKg { get; private set; } = 0f;
	public float EffectiveMaxForwardSpeed { get; private set; } = 0f;
	public float EffectiveMaxReverseSpeed { get; private set; } = 0f;
	public float TractionGrip { get; private set; } = 1f;
	public float SteerGrip { get; private set; } = 1f;
	public float DriveGrip { get; private set; } = 1f;
	private Node3D? _visualRoot;
	private bool _usingPassengerCarPackVisual;
	private readonly List<MeshInstance3D> _bodyMeshes = new();
	private Node3D? _hitboxes;

	// Tire VFX (blown tires)
	private readonly bool[] _blownTires = new bool[4];
	private readonly float[] _smokeCooldown = new float[4];
	private readonly float[] _skidCooldown = new float[4];
	private ArenaWorld? _arenaWorldCached;

	// Audio
	private VehicleEngineAudio? _engineAudio;

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
	}

	public override void _Ready()
	{
		// Ensure vehicle-vs-vehicle collisions are enabled even if editor defaults change.
		CollisionLayer = 1u;
		CollisionMask = 1u;
		SafeMargin = 0.05f;
		AddToGroup("vehicle_pawn");
		// Be explicit: ensure physics processing is enabled.
		SetPhysicsProcess(true);
		EnsureVisualAndCollision();
		ApplyBodyColor();
		EnsureHitboxes();
		// Default mounts so a pawn is usable even before ConfigureLoadout() is called.
		EnsureMountsFallback();
		_arenaWorldCached = FindArenaWorld();

		// Sensible initial values before defs are configured.
		EffectiveMaxForwardSpeed = MaxForwardSpeed;
		EffectiveMaxReverseSpeed = MaxReverseSpeed;

		EnsureEngineAudio();
	}

	/// <summary>
	/// Arena code frequently replaces the VehicleInstanceState record (immutable "with" updates).
	/// This lets the pawn stay in sync without rebuilding visuals.
	/// </summary>
	public void SetRuntimeState(VehicleInstanceState runtime)
	{
		_vehicleRuntime = runtime;
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

		if (_vehicleDef != null)
			EnsureMountPoints(_vehicleDef);
		AttachWeaponVisuals();
		ApplyBodyColor();
		ApplyEngineAudioArchetype();
	}

	

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

private string ComputeEngineArchetypeId()
{
	// Prefer the installed engine's fuel type when available; otherwise fall back to vehicle class.
	if (_defs != null && _vehicleRuntime != null && !string.IsNullOrWhiteSpace(_vehicleRuntime.InstalledEngineId))
	{
		if (_defs.Engines.TryGetValue(_vehicleRuntime.InstalledEngineId!, out var eng))
		{
			if (eng.FuelType == FuelType.Diesel) return "diesel_truck";
			if (eng.FuelType == FuelType.Electric) return "i4_compact"; // placeholder (no EV loops yet)

		}

	}

	if (_vehicleDef != null)
	{
		return _vehicleDef.Class switch
		{
			VehicleClass.Compact => "i4_compact",
			VehicleClass.LightTruck => "diesel_truck",
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
		if (UsePassengerCarPackVisual)
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

	public override void _PhysicsProcess(double delta)
	{
		var dt = (float)delta;
		FireCooldownRemaining = Mathf.Max(0f, FireCooldownRemaining - dt);

		// Keep movement strictly on the XZ plane.
		Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);

		UpdateRuntimeDerivedStats();

		var forward = -GlobalTransform.Basis.Z;
		forward.Y = 0f;
		forward = forward.Normalized();
		var right = GlobalTransform.Basis.X;
		right.Y = 0f;
		right = right.Normalized();

		// Decompose current velocity.
		var v = Velocity;
		var vFwd = v.Dot(forward);
		var vLat = v.Dot(right);

		var throttle = Mathf.Clamp(ThrottleInput, -1f, 1f);
		var steerInput = Mathf.Clamp(SteerInput, -1f, 1f);

		// --- Steering (bicycle model) ---
		// At very low speeds, yaw rate naturally approaches 0 (no "spin in place").
		var maxSteerRad = Mathf.DegToRad(FrontWheelMaxSteerDeg) * Mathf.Clamp(SteerGrip, 0.25f, 1f);
		// Match the rest of the project input conventions (A=left, D=right).
		// Godot forward is -Z, so we negate steer here to keep steering intuitive.
		var steerRad = -steerInput * maxSteerRad;
		var wheelbase = MathF.Max(0.5f, WheelbaseMeters);
		var yawRate = 0f;
		if (MathF.Abs(steerRad) > 0.0001f)
			yawRate = (vFwd / wheelbase) * MathF.Tan(steerRad);
		Rotation = new Vector3(0f, Rotation.Y + yawRate * dt, 0f);

		// Refresh basis after rotation.
		forward = -GlobalTransform.Basis.Z;
		forward.Y = 0f;
		forward = forward.Normalized();
		right = GlobalTransform.Basis.X;
		right.Y = 0f;
		right = right.Normalized();

		// Re-decompose velocity using the updated basis.
		vFwd = v.Dot(forward);
		vLat = v.Dot(right);

		// --- Longitudinal acceleration / braking ---
		// Weight + rear-tire condition reduces effective acceleration.
		var accel = Accel * Mathf.Clamp(DriveGrip, 0.25f, 1f) * ComputeMassAccelFactor();
		var brake = BrakeDecel * Mathf.Clamp(DriveGrip, 0.25f, 1f);
		var coast = CoastDecel;

		if (MathF.Abs(throttle) > 0.01f)
		{
			// If trying to reverse direction, brake hard first.
			if (MathF.Sign(throttle) != MathF.Sign(vFwd) && MathF.Abs(vFwd) > 0.6f)
			{
				vFwd = Mathf.MoveToward(vFwd, 0f, brake * dt);
			}
			else
			{
				// Ease acceleration as we approach top speed so we don't hit max too quickly.
				var maxFwdLocal = EffectiveMaxForwardSpeed > 0.01f ? EffectiveMaxForwardSpeed : MaxForwardSpeed;
				var maxRevLocal = EffectiveMaxReverseSpeed > 0.01f ? EffectiveMaxReverseSpeed : MaxReverseSpeed;
				var desiredMax = throttle >= 0f ? maxFwdLocal : maxRevLocal;
				var spd01 = desiredMax > 0.01f ? Mathf.Clamp(MathF.Abs(vFwd) / desiredMax, 0f, 1f) : 0f;
				var accelFactor = Mathf.Lerp(1f, Mathf.Clamp(AccelNearMaxFactor, 0.05f, 1f), MathF.Pow(spd01, MathF.Max(0.5f, AccelSpeedFalloffExponent)));
				vFwd += throttle * accel * accelFactor * dt;
			}
		}
		else
		{
			// Coasting (rolling resistance).
			vFwd = Mathf.MoveToward(vFwd, 0f, coast * dt);
		}

		// --- Lateral grip (side-slip damping) ---
		var latFric = LateralFriction * Mathf.Clamp(TractionGrip, 0.2f, 1f);
		vLat = Mathf.MoveToward(vLat, 0f, latFric * dt);

		// Recompose velocity.
		v = forward * vFwd + right * vLat;

		// --- Drag (keeps top speed sane + adds weighty feel) ---
		var speed = v.Length();
		if (speed > 0.001f)
		{
			var dragLin = LinearDrag;
			var dragQuad = QuadraticDrag;
			var drag = (dragLin + dragQuad * speed * speed) * dt;
			v *= MathF.Max(0f, 1f - drag);
		}

		// --- Speed clamp (weight + tire condition affect top speed) ---
		var maxFwd = EffectiveMaxForwardSpeed;
		var maxRev = EffectiveMaxReverseSpeed;
		// If traction is poor (blown tires), cap speed further.
		var tractionSpeedFactor = Mathf.Lerp(0.55f, 1f, Mathf.Clamp(TractionGrip, 0f, 1f));
		maxFwd *= tractionSpeedFactor;
		maxRev *= tractionSpeedFactor;

		// Clamp forward and reverse along forward direction.
		vFwd = v.Dot(forward);
		vLat = v.Dot(right);
		vFwd = Mathf.Clamp(vFwd, -maxRev, maxFwd);
		v = forward * vFwd + right * vLat;

		Velocity = v;
		MoveAndSlide();

		ResolveVehicleOverlap();

		// Keep grounded (collision box height is 0.8, so half-height is 0.4).
		var p = GlobalPosition;
		GlobalPosition = new Vector3(p.X, 0.4f, p.Z);

		// Blown tire visuals (sparks + smoke + simple skid marks).
		var vNow = Velocity;
		var vF = vNow.Dot(forward);
		var vL = vNow.Dot(right);
		UpdateTireVfx(dt, vF, vL);

		UpdateFrontWheelSteer(dt, steerRad);
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



// --- IVehicleAudioTelemetry (engine audio driver) ---
public float GetSpeedMps() => Velocity.Length();

	// --- HUD helpers (RPM + gear display) ---
	public float GetForwardSpeedSignedMps() => GetForwardSpeedMps();

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
		var vFwd = GetForwardSpeedMps();
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

public float GetMaxSpeedMps() => MathF.Max(0.01f, EffectiveMaxForwardSpeed > 0.01f ? EffectiveMaxForwardSpeed : MaxForwardSpeed);

public float GetThrottle01()
{
	var throttle = Mathf.Clamp(ThrottleInput, -1f, 1f);
	var vFwd = GetForwardSpeedMps();
	// If nearly stopped, treat either direction as a rev.
	if (MathF.Abs(vFwd) < 0.15f) return Mathf.Clamp(MathF.Abs(throttle), 0f, 1f);
	// Forward acceleration.
	if (vFwd > 0.15f && throttle > 0f) return throttle;
	// Reverse acceleration.
	if (vFwd < -0.15f && throttle < 0f) return -throttle;
	return 0f;
}

public float GetBrake01()
{
	var throttle = Mathf.Clamp(ThrottleInput, -1f, 1f);
	var vFwd = GetForwardSpeedMps();
	// Braking when input opposes motion.
	if (vFwd > 0.15f && throttle < 0f) return Mathf.Clamp(-throttle, 0f, 1f);
	if (vFwd < -0.15f && throttle > 0f) return Mathf.Clamp(throttle, 0f, 1f);
	return 0f;
}

private float GetForwardSpeedMps()
{
	var forward = -GlobalTransform.Basis.Z;
	forward.Y = 0f;
	forward = forward.Normalized();
	return Velocity.Dot(forward);
}

private void ApplyBodyColor()
	{
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

		// 3) Last resort: scan for a scene.gltf named like the pack.
		try
		{
			var found = FindFirstFileRecursive("res://Assets", "scene.gltf");
			if (!string.IsNullOrWhiteSpace(found))
				return found;
		}
		catch
		{
			// ignore
		}

		return null;
	}

	private static string? FindFirstFileRecursive(string rootResPath, string fileName)
	{
		var dir = DirAccess.Open(rootResPath);
		if (dir == null) return null;
		dir.ListDirBegin();
		while (true)
		{
			var name = dir.GetNext();
			if (string.IsNullOrEmpty(name)) break;
			if (name == "." || name == "..") continue;
			var full = rootResPath.TrimEnd('/') + "/" + name;
			if (dir.CurrentIsDir())
			{
				var sub = FindFirstFileRecursive(full, fileName);
				if (!string.IsNullOrWhiteSpace(sub))
				{
					dir.ListDirEnd();
					return sub;
				}
			}
			else
			{
				if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
				{
					dir.ListDirEnd();
					return full;
				}
			}
		}
		dir.ListDirEnd();
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

	private void AutoScaleAndCenterPassengerCar(Node3D model)
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

		// If the model is tiny, scale it up to match our proxy length.
		if (len < PassengerCarPackMinVisibleLength)
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
		if (mats.Count == 0) return;

		// Swap materials on all visible nodes (body + wheels).
		var pattern = new Regex(@"^(Body|Glass|Optics|Wheel|Wheek)_(\d+)$", RegexOptions.IgnoreCase);
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
			if (!mi.IsVisibleInTree()) continue;

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
			}
			if (!blown)
			{
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
				CreateTurretMount(m.MountId, pos, m.YawMinDegrees, m.YawMaxDegrees);
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

	private void CreateTurretMount(string mountId, Vector3 pos, float? yawMinDeg, float? yawMaxDeg)
	{
		if (_mountsRoot == null) return;
		var pivot = new Node3D { Name = $"TurretPivot_{mountId}", Position = pos };
		_mountsRoot.AddChild(pivot);
		var marker = new Marker3D { Name = $"Mount_{mountId}" };
		pivot.AddChild(marker);
		_mountById[mountId] = marker;
		_turretsByMountId[mountId] = new TurretInfo { Pivot = pivot, MinYawDeg = yawMinDeg, MaxYawDeg = yawMaxDeg };
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
		if (aim == Vector3.Zero)
			return;

		foreach (var kv in _turretsByMountId)
		{
			var info = kv.Value;
			if (info.Pivot == null || !GodotObject.IsInstanceValid(info.Pivot))
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

			if (info.MinYawDeg.HasValue) localYawDeg = MathF.Max(info.MinYawDeg.Value, localYawDeg);
			if (info.MaxYawDeg.HasValue) localYawDeg = MathF.Min(info.MaxYawDeg.Value, localYawDeg);

			info.Pivot.RotationDegrees = new Vector3(0f, localYawDeg, 0f);
		}
	}

	private void UpdateRuntimeDerivedStats()
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

		TotalMassKg = VehicleMassMath.ComputeTotalMassKg(_vehicleDef, _vehicleRuntime, _defs);
		var baseMass = MathF.Max(1f, _vehicleDef.BaseMassKg);
		var totalMass = MathF.Max(baseMass, TotalMassKg);
		var massFactor = Mathf.Clamp(baseMass / totalMass, 0.45f, 1.15f);
		var speedFactor = Mathf.Clamp(MathF.Pow(massFactor, 0.25f), 0.65f, 1.15f);
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
	}

	private float ComputeMassAccelFactor()
	{
		if (_vehicleDef == null) return 1f;
		var baseMass = MathF.Max(1f, _vehicleDef.BaseMassKg);
		var total = TotalMassKg <= 0 ? baseMass : TotalMassKg;
		var massFactor = Mathf.Clamp(baseMass / MathF.Max(baseMass, total), 0.45f, 1.15f);
		return Mathf.Clamp(MathF.Pow(massFactor, 1.0f), 0.45f, 1.15f);
	}

	private void ResolveVehicleOverlap()
	{
		// CharacterBody vs CharacterBody collisions can sometimes feel "soft".
		// This is a lightweight fallback to keep cars from clipping through each other.
		var tree = GetTree();
		if (tree == null) return;
		var nodes = tree.GetNodesInGroup("vehicle_pawn");
		if (nodes == null || nodes.Count == 0) return;

		const float minDist = 2.15f;
		var selfPos = GlobalPosition;
		foreach (var n in nodes)
		{
			if (n is not VehiclePawn other) continue;
			if (other == this) continue;
			if (!GodotObject.IsInstanceValid(other)) continue;
			var op = other.GlobalPosition;
			var d = new Vector3(selfPos.X - op.X, 0f, selfPos.Z - op.Z);
			var dist = d.Length();
			if (dist <= 0.001f || dist >= minDist) continue;
			var dir = d / dist;
			var push = (minDist - dist) * 0.55f;
			selfPos += dir * push;
		}
		GlobalPosition = new Vector3(selfPos.X, GlobalPosition.Y, selfPos.Z);
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
