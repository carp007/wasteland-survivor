using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game.Systems;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Minimal 3D vehicle pawn for the early 2.5D transition.
/// Kinematic-ish car movement on the XZ plane (Y up), using CharacterBody3D.
/// </summary>
public partial class VehiclePawn : CharacterBody3D
{
	[Export] public float MaxForwardSpeed = 18.0f;
	[Export] public float MaxReverseSpeed = 8.0f;
	[Export] public float Accel = 48.0f;

	// Realism tuning (bicycle model + friction). Keep defaults gentle for early gameplay.
	[Export] public float WheelbaseMeters = 2.6f;
	[Export] public float CoastDecel = 18.0f;
	[Export] public float BrakeDecel = 58.0f;
	[Export] public float LateralFriction = 26.0f;
	[Export] public float LinearDrag = 0.8f;
	[Export] public float QuadraticDrag = 0.018f;

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
	private readonly List<MeshInstance3D> _bodyMeshes = new();
	private Node3D? _hitboxes;

	// Tire VFX (blown tires)
	private readonly bool[] _blownTires = new bool[4];
	private readonly float[] _smokeCooldown = new float[4];
	private readonly float[] _skidCooldown = new float[4];
	private ArenaWorld? _arenaWorldCached;

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
	}

	/// <summary>
	/// Arena code frequently replaces the VehicleInstanceState record (immutable "with" updates).
	/// This lets the pawn stay in sync without rebuilding visuals.
	/// </summary>
	public void SetRuntimeState(VehicleInstanceState runtime)
	{
		_vehicleRuntime = runtime;
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


		// Replace the old single-box mesh with a slightly more "car-like" proxy model.
		// This remains box-based for robustness and because we'll swap to real imported models later.
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
				vFwd += throttle * accel * dt;
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

	
	public Vector3 GetAimPointWorld()
	{
		// Aim toward the approximate center-mass of the vehicle so raycasts hit reliably.
		// Vehicle GlobalPosition is kept at ~half-height (y=0.4).
		return GlobalPosition + Vector3.Up * 0.10f;
	}
private void ApplyBodyColor()
	{
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

		if (_vehicleRuntime == null) return;
		if (_vehicleRuntime.InstalledWeaponsByMountId.Count == 0) return;

		// Pick a "primary" mount.
		// 1) Prefer the machine gun (our default test weapon) if present.
		// 2) Else prefer a Front mount if present.
		// 3) Else first key.
		var primary = _vehicleRuntime.InstalledWeaponsByMountId.Keys.FirstOrDefault();
		foreach (var kv in _vehicleRuntime.InstalledWeaponsByMountId)
		{
			if (kv.Value.WeaponId == "wpn_mg")
			{
				primary = kv.Key;
				break;
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

			var weapon = CreateBoxWeaponVisual(mountId, kv.Value.WeaponId);
			mountMarker.AddChild(weapon);
			// Align weapon so its internal MountPoint coincides with the mount marker origin.
			var wMount = weapon.GetNodeOrNull<Marker3D>("MountPoint");
			if (wMount != null)
				weapon.Transform = wMount.Transform.AffineInverse();

			DisableWeaponCollision(weapon);

			if (_primaryMuzzle == null && mountId == _primaryMountId)
				_primaryMuzzle = weapon.GetNodeOrNull<Marker3D>("Muzzle");
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
