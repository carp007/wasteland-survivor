// -------------------------------------------------------------------------------------------------
// GamePawnKit
// File: PawnKit/Pawns/VehiclePawnBase.cs
// Purpose: Reusable 3D vehicle pawn core (CharacterBody3D) with kinematic-style movement/handling.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace GamePawnKit.Pawns;

/// <summary>
/// Reusable vehicle pawn core suitable for top-down / 2.5D games.
/// 
/// - Movement/handling is tuned for arcade-ish "bicycle model" yaw + lateral friction.
/// - Game-specific systems (weapons, loadouts, damage models, audio) should live in subclasses.
/// </summary>
public partial class VehiclePawnBase : CharacterBody3D, IVehiclePawn, IVehicleTelemetry, IEnterable, IExitable
{
	// --- Movement tuning ---
	[Export] public float MaxForwardSpeed { get; set; } = 18.0f;
	[Export] public float MaxReverseSpeed { get; set; } = 8.0f;
	[Export] public float Accel { get; set; } = 16.0f;
	[Export] public float AccelNearMaxFactor { get; set; } = 0.35f;
	[Export] public float AccelSpeedFalloffExponent { get; set; } = 1.6f;

	// Bicycle model + friction/drag.
	[Export] public float WheelbaseMeters { get; set; } = 2.6f;
	[Export] public float CoastDecel { get; set; } = 3.5f;
	[Export] public float BrakeDecel { get; set; } = 58.0f;
	[Export] public float LateralFriction { get; set; } = 26.0f;
	[Export] public float LinearDrag { get; set; } = 0.06f;
	[Export] public float QuadraticDrag { get; set; } = 0.0012f;

	[Export] public float FrontWheelMaxSteerDeg { get; set; } = 28f;
	[Export] public float FrontWheelSteerLerp { get; set; } = 14f;

	// --- Inputs (typically driven by player/AI controllers) ---
	public float ThrottleInput { get; set; } = 0f; // -1..1
	public float SteerInput { get; set; } = 0f;    // -1..1

	/// <summary>
	/// Optional aim point used by subclasses (turrets/targeting).
	/// </summary>
	public Vector3 AimWorldPosition { get; set; } = Vector3.Zero;

	/// <summary>
	/// Apply a reusable control payload. This gives player, AI, replay, and future network/controller
	/// code a single shared surface for driving a vehicle pawn.
	/// </summary>
	public void ApplyControlIntent(VehicleControlIntent intent)
	{
		ThrottleInput = Mathf.Clamp(intent.Throttle, -1f, 1f);
		SteerInput = Mathf.Clamp(intent.Steer, -1f, 1f);
		AimWorldPosition = intent.AimWorldPosition;
	}

	/// <summary>
	/// Clears throttle / steering without touching the current aim target.
	/// </summary>
	public void ClearControlIntent()
	{
		ThrottleInput = 0f;
		SteerInput = 0f;
	}

	// --- Runtime-derived performance (weight + damage). Subclasses can compute these.
	public float TotalMassKg { get; protected set; } = 0f;
	public float EffectiveMaxForwardSpeed { get; protected set; } = 0f;
	public float EffectiveMaxReverseSpeed { get; protected set; } = 0f;
	public float TractionGrip { get; protected set; } = 1f;
	public float SteerGrip { get; protected set; } = 1f;
	public float DriveGrip { get; protected set; } = 1f;

	// --- IPawn (minimal lifecycle/events) ---
	public bool IsDead => _isDead;
	public bool IsPlayerControlled => !_playerDisabled;
	public int? ControllerId { get; private set; }

	public event Action<bool>? PlayerControlledChanged;
	public event Action? Died;
	public event Action<int?>? Possessed;
	public event Action<int?>? Unpossessed;
	public event Action<int?, int?>? ControllerIdChanged;

	// --- Optional enter/exit surface (seat/occupancy). ---
	// This is intentionally simple and does not manipulate the scene tree. Games control visuals/spawning.
	private IPawn? _occupant;
	public bool IsOccupied => GetValidOccupant() != null;
	public IPawn? Occupant => GetValidOccupant();
	public event Action<IPawn>? Entered;
	public event Action<IPawn>? Exited;

	private bool _playerDisabled;
	private bool _isDead;

	public override void _Ready()
	{
		// Conservative defaults so pawns remain robust even if editor settings drift.
		CollisionLayer = 1u;
		CollisionMask = 1u;
		SafeMargin = 0.05f;

		AddToGroup("vehicle_pawn");
		SetPhysicsProcess(true);

		// Sensible initial values before a subclass configures derived stats.
		EffectiveMaxForwardSpeed = MaxForwardSpeed;
		EffectiveMaxReverseSpeed = MaxReverseSpeed;
	}

	public virtual bool CanEnter(IPawn entrant)
	{
		if (entrant == null) return false;
		if (IsDead) return false;
		if (entrant.IsDead) return false;
		return GetValidOccupant() == null;
	}

	public virtual bool TryEnter(IPawn entrant)
	{
		if (!CanEnter(entrant)) return false;
		_occupant = entrant;
		Entered?.Invoke(entrant);
		return true;
	}

	public virtual bool CanExit()
		=> GetValidOccupant() != null;

	public virtual bool TryExit()
	{
		var occ = GetValidOccupant();
		if (occ == null) return false;
		_occupant = null;
		Exited?.Invoke(occ);
		return true;
	}

	private IPawn? GetValidOccupant()
	{
		if (_occupant == null) return null;
		// If the occupant is a Godot object, it may have been QueueFree'd.
		if (_occupant is GodotObject go && !GodotObject.IsInstanceValid(go))
		{
			_occupant = null;
			return null;
		}
		return _occupant;
	}

	public void SetPlayerControlled(bool enabled)
		=> SetPlayerControlled(enabled, enabled ? (ControllerId ?? 0) : null);

	public void SetPlayerControlled(bool enabled, int? controllerId)
	{
		var wasEnabled = !_playerDisabled;
		var prevController = ControllerId;

		_playerDisabled = !enabled;
		ControllerId = enabled ? (controllerId ?? 0) : null;

		if (!enabled)
		{
			ThrottleInput = 0f;
			SteerInput = 0f;
			Velocity = Vector3.Zero;
		}

		PlayerControlledChanged?.Invoke(enabled);

		// Possession notifications are additive; current game code does not depend on them yet.
		if (enabled && !wasEnabled)
			Possessed?.Invoke(ControllerId);
		else if (!enabled && wasEnabled)
			Unpossessed?.Invoke(prevController);
		else if (enabled && wasEnabled && prevController != ControllerId)
			ControllerIdChanged?.Invoke(prevController, ControllerId);
	}

	public virtual void TriggerDeath()
	{
		if (_isDead) return;
		_isDead = true;
		_playerDisabled = true;
		ThrottleInput = 0f;
		SteerInput = 0f;
		Velocity = Vector3.Zero;
		Died?.Invoke();
	}

	protected readonly struct VehicleStepInfo
	{
		public VehicleStepInfo(Vector3 forward, Vector3 right, float steerRad)
		{
			Forward = forward;
			Right = right;
			SteerRad = steerRad;
		}

		public Vector3 Forward { get; }
		public Vector3 Right { get; }
		public float SteerRad { get; }
	}

	/// <summary>
	/// Shared movement step used by vehicle subclasses.
	/// Subclasses call this inside their own <c>_PhysicsProcess</c> and then apply any extra logic (turrets, VFX, etc).
	/// </summary>
	protected VehicleStepInfo StepVehicleMovement(float dt)
	{
		if (_isDead || _playerDisabled)
		{
			Velocity = Vector3.Zero;
			ThrottleInput = 0f;
			SteerInput = 0f;
			return new VehicleStepInfo(Vector3.Forward, Vector3.Right, 0f);
		}

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

		// Match common input conventions (A=left, D=right).
		// Godot forward is -Z, so negate steer to keep steering intuitive.
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
				var accelFactor = Mathf.Lerp(1f, Mathf.Clamp(AccelNearMaxFactor, 0.05f, 1f),
					MathF.Pow(spd01, MathF.Max(0.5f, AccelSpeedFalloffExponent)));
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
			var drag = (LinearDrag + QuadraticDrag * speed * speed) * dt;
			v *= MathF.Max(0f, 1f - drag);
		}

		// --- Speed clamp (weight + tire condition affect top speed) ---
		var maxFwd = EffectiveMaxForwardSpeed;
		var maxRev = EffectiveMaxReverseSpeed;

		// If traction is poor, cap speed further (subclasses can lower TractionGrip).
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

		return new VehicleStepInfo(forward, right, steerRad);
	}

	/// <summary>
	/// Subclasses may override to compute derived stats from weight/damage.
	/// Defaults to "base export values".
	/// </summary>
	protected virtual void UpdateRuntimeDerivedStats()
	{
		EffectiveMaxForwardSpeed = MaxForwardSpeed;
		EffectiveMaxReverseSpeed = MaxReverseSpeed;
		TotalMassKg = 0f;
		TractionGrip = 1f;
		SteerGrip = 1f;
		DriveGrip = 1f;
	}

	/// <summary>
	/// Subclasses may override to compute acceleration scaling from mass.
	/// </summary>
	protected virtual float ComputeMassAccelFactor() => 1f;

	// -------------------------------------------------------------------------------------------------
	// IVehicleTelemetry (generic surface used by HUD/audio/AI)
	// -------------------------------------------------------------------------------------------------
	public float GetSpeedMps() => Velocity.Length();

	/// <summary>
	/// Signed forward speed in meters/second (positive forward, negative reverse).
	/// </summary>
	public float GetForwardSpeedSignedMps() => GetForwardSpeedSignedMpsInternal();

	public float GetMaxSpeedMps()
	{
		var max = EffectiveMaxForwardSpeed > 0.01f ? EffectiveMaxForwardSpeed : MaxForwardSpeed;
		return MathF.Max(0.01f, max);
	}

	public float GetThrottle01()
	{
		var throttle = Mathf.Clamp(ThrottleInput, -1f, 1f);
		var vFwd = GetForwardSpeedSignedMpsInternal();
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
		var vFwd = GetForwardSpeedSignedMpsInternal();
		// Braking when input opposes motion.
		if (vFwd > 0.15f && throttle < 0f) return Mathf.Clamp(-throttle, 0f, 1f);
		if (vFwd < -0.15f && throttle > 0f) return Mathf.Clamp(throttle, 0f, 1f);
		return 0f;
	}

	/// <summary>
	/// Signed speed along the pawn's forward axis (-Z forward convention).
	/// Protected so subclasses can build UI and behaviors without duplicating basis math.
	/// </summary>
	protected float GetForwardSpeedSignedMpsInternal()
	{
		var forward = -GlobalTransform.Basis.Z;
		forward.Y = 0f;
		var len = forward.Length();
		if (len < 0.0001f) return 0f;
		forward /= len;
		return Velocity.Dot(forward);
	}

	/// <summary>
	/// Optional overlap resolution between multiple vehicle pawns to reduce clipping.
	/// This is intentionally lightweight and relies on the "vehicle_pawn" group.
	/// </summary>
	protected virtual void ResolveVehicleOverlap()
	{
		var tree = GetTree();
		if (tree == null) return;
		var nodes = tree.GetNodesInGroup("vehicle_pawn");
		if (nodes == null || nodes.Count == 0) return;

		const float minDist = 2.15f;
		var selfPos = GlobalPosition;
		foreach (var n in nodes)
		{
			if (n is not VehiclePawnBase other) continue;
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
}
