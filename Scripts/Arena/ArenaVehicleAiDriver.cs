// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/ArenaVehicleAiDriver.cs
// Purpose: Shared arena enemy driving heuristics that emit reusable vehicle control intents.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Godot;
using GamePawnKit.Pawns;
using WastelandSurvivor.Core.Defs;

namespace WastelandSurvivor.Game.Arena;

public readonly struct ArenaVehicleAiDriveDecision
{
	public ArenaVehicleAiDriveDecision(VehicleControlIntent controlIntent, float distanceToTarget, bool allowFire)
	{
		ControlIntent = controlIntent;
		DistanceToTarget = distanceToTarget;
		AllowFire = allowFire;
	}

	public VehicleControlIntent ControlIntent { get; }
	public float DistanceToTarget { get; }
	public bool AllowFire { get; }
}

public sealed class ArenaVehicleAiRuntimeState
{
	public Vector3 LastPosition { get; set; } = Vector3.Zero;
	public bool HasLastPosition { get; set; }
	public float LowProgressSeconds { get; set; }
	public float RecoveryRemainingSeconds { get; set; }
	public float LaneCommitRemainingSeconds { get; set; }
	public int LaneSign { get; set; }
	public int RecoverySteerSign { get; set; } = 1;
	public int RecoveryAttemptCount { get; set; }
	public float TimeSinceLastRecoverySeconds { get; set; } = 999f;
	public float MineDeployCooldownRemainingSeconds { get; set; }
	public float ContainmentRemainingSeconds { get; set; }
	public Vector3 ContainmentTarget { get; set; } = Vector3.Zero;
	public float UnderPressureSeconds { get; set; }
	public bool DisengageActive { get; set; }
	public float FireCyclePhaseSeconds { get; set; }
	public bool FireGateOpen { get; set; } = true;
	public float AttackRunRemainingSeconds { get; set; }
	public float SecondsSinceAttackRun { get; set; }
	public float OpportunismWindowRemainingSeconds { get; set; }
	public Vector3 LastTargetVelocity { get; set; } = Vector3.Zero;
	public bool HasLastTargetVelocity { get; set; }
	public float ContactCrawlSeconds { get; set; }
	public float BreakawayRemainingSeconds { get; set; }
	public int BreakawaySteerSign { get; set; } = 1;
	public int TurnaroundSteerSign { get; set; }
	public float TurnaroundCommitRemainingSeconds { get; set; }
	public float AsternStallSeconds { get; set; }
	// --- Smooth-driving layer (PD steering + slew + committed detours), loop #6 ---
	public float LastHeadingError { get; set; }
	public bool HasLastHeadingError { get; set; }
	public float LastSteerOutput { get; set; }
	public bool TacticalActive { get; set; }
	public float TacticalHoldRemainingSeconds { get; set; }
	public int DetourSideSign { get; set; }
	public float DetourCommitRemainingSeconds { get; set; }
	/// <summary>Telemetry: number of committed obstacle detours planned this match.</summary>
	public int DetourCount { get; set; }

	public void Reset()
	{
		LastPosition = Vector3.Zero;
		HasLastPosition = false;
		LowProgressSeconds = 0f;
		RecoveryRemainingSeconds = 0f;
		LaneCommitRemainingSeconds = 0f;
		LaneSign = 0;
		RecoverySteerSign = 1;
		RecoveryAttemptCount = 0;
		TimeSinceLastRecoverySeconds = 999f;
		MineDeployCooldownRemainingSeconds = 0f;
		ContainmentRemainingSeconds = 0f;
		ContainmentTarget = Vector3.Zero;
		UnderPressureSeconds = 0f;
		DisengageActive = false;
		FireCyclePhaseSeconds = 0f;
		FireGateOpen = true;
		OpportunismWindowRemainingSeconds = 0f;
		LastTargetVelocity = Vector3.Zero;
		HasLastTargetVelocity = false;
		AttackRunRemainingSeconds = 0f;
		SecondsSinceAttackRun = 0f;
		ContactCrawlSeconds = 0f;
		BreakawayRemainingSeconds = 0f;
		BreakawaySteerSign = 1;
		TurnaroundSteerSign = 0;
		TurnaroundCommitRemainingSeconds = 0f;
		AsternStallSeconds = 0f;
		LastHeadingError = 0f;
		HasLastHeadingError = false;
		LastSteerOutput = 0f;
		TacticalActive = false;
		TacticalHoldRemainingSeconds = 0f;
		DetourSideSign = 0;
		DetourCommitRemainingSeconds = 0f;
		DetourCount = 0;
	}
}

public static class ArenaVehicleAiDriver
{
	private const float StartLaneInnerZ = 55.0f;
	private const float StartLaneOuterZ = 81.0f;
	private const float StartLaneHalfWidth = 14.6f;
	private const float StartLaneCenteringWeight = 0.82f;
	private const float StartLaneExitInset = 2.75f;
	private const float StartLaneTransitZ = 37.5f;
	private const float AvoidanceEyeHeight = 0.8f;
	private const float AvoidanceSideOffset = 1.1f;
	private const float AvoidanceInnerSideWeight = 0.58f;
	private const float AvoidanceOuterSideWeight = 1.02f;
	private const float CandidatePathWeight = 3.2f;
	private const float CandidateProgressWeight = 4.4f;
	private const float CandidateDirectBonus = 0.45f;
	private const float CandidateLaneCommitBonus = 1.15f;
	private const float CandidateLaneSwitchPenalty = 0.75f;
	private const float CandidateWideLaneBias = 0.35f;
	private const float CandidateBoundarySafetyWeight = 2.05f;
	private const float CandidateBoundaryDirectionWeight = 1.35f;
	private const float CandidateClearPathThreshold = 0.78f;
	private const float ContainmentCommitSeconds = 1.45f;
	private const float ContainmentBoundaryReleasePressure = 0.18f;
	private const float ContainmentStartLaneReleasePressure = 0.08f;
	// Target must be moving at least this fast for its velocity heading to count as a "turn".
	private const float OpportunismMinTargetSpeed = 3.0f;

	public static ArenaVehicleAiDriveDecision BuildDecision(
		VehiclePawn pawn,
		Vector3 targetPosition,
		Vector3 targetVelocity,
		bool targetIsVehicle,
		float dt,
		ArenaVehicleAiRuntimeState runtimeState,
		ArenaVehicleAiTacticalProfile tacticalProfile,
		ArenaAiConfig config,
		float selfHealth01 = 1f,
		IReadOnlyList<ArenaObstacleFootprint>? obstacles = null)
	{
		runtimeState ??= new ArenaVehicleAiRuntimeState();
		runtimeState.TimeSinceLastRecoverySeconds += dt;
		runtimeState.LaneCommitRemainingSeconds = Mathf.Max(0f, runtimeState.LaneCommitRemainingSeconds - dt);
		runtimeState.ContainmentRemainingSeconds = Mathf.Max(0f, runtimeState.ContainmentRemainingSeconds - dt);
		UpdateFireGate(runtimeState, dt, config);
		UpdateOpportunismWindow(runtimeState, targetVelocity, dt, config);

		if (runtimeState.RecoveryRemainingSeconds > 0f)
		{
			runtimeState.RecoveryRemainingSeconds = Mathf.Max(0f, runtimeState.RecoveryRemainingSeconds - dt);
			var recoveryIntent = BuildRecoveryIntent(pawn, targetPosition, runtimeState, config);
			RememberPosition(runtimeState, pawn.GlobalPosition);
			CommitSteerState(runtimeState, recoveryIntent.Steer);
			return new ArenaVehicleAiDriveDecision(recoveryIntent, 0f, allowFire: false);
		}

		// Bumper-contact breakaway: a sustained nose-to-nose crawl (both vehicles pushing at
		// walking pace) is a stalemate no stance logic escapes — stuck-recovery never fires
		// because the pair still creeps above the stuck thresholds. After the trigger time in
		// near-contact at crawl speed, commit to a reverse-arc pull-out, then resume the stance.
		UpdateContactBreakaway(pawn, runtimeState, targetPosition, targetIsVehicle, dt, config);
		if (runtimeState.BreakawayRemainingSeconds > 0f)
		{
			runtimeState.BreakawayRemainingSeconds = Mathf.Max(0f, runtimeState.BreakawayRemainingSeconds - dt);
			var breakawayIntent = BuildContactBreakawayIntent(pawn, targetPosition, runtimeState, config);
			RememberPosition(runtimeState, pawn.GlobalPosition);
			CommitSteerState(runtimeState, breakawayIntent.Steer);
			return new ArenaVehicleAiDriveDecision(breakawayIntent, 0f, allowFire: false);
		}

		var leadSeconds = targetIsVehicle ? config.VehicleLeadSeconds : config.DriverLeadSeconds;
		var ledTargetPosition = targetPosition + targetVelocity * leadSeconds;
		var currentBoundaryPressure = GetArenaBoundaryPressure(pawn.GlobalPosition, config, out var currentInwardDir);
		var startLanePressure = GetStartLanePressure(pawn.GlobalPosition, out var startLaneInwardDir);
		if (runtimeState.ContainmentRemainingSeconds > 0f)
		{
			var releaseContainment = currentBoundaryPressure <= ContainmentBoundaryReleasePressure
				&& startLanePressure <= ContainmentStartLaneReleasePressure;
			if (!releaseContainment)
			{
				var committedEscapePoint = runtimeState.ContainmentTarget;
				if (committedEscapePoint.LengthSquared() < 0.0001f)
					committedEscapePoint = BuildContainmentEscapePoint(pawn.GlobalPosition, currentBoundaryPressure, currentInwardDir, startLanePressure, startLaneInwardDir, config);

				var committedIntent = BuildContainmentEscapeIntent(pawn, targetPosition, currentBoundaryPressure, currentInwardDir, startLanePressure, startLaneInwardDir, config, committedEscapePoint);
				RememberPosition(runtimeState, pawn.GlobalPosition);
				CommitSteerState(runtimeState, committedIntent.Steer);
				return new ArenaVehicleAiDriveDecision(committedIntent, 0f, allowFire: false);
			}

			runtimeState.ContainmentRemainingSeconds = 0f;
			runtimeState.ContainmentTarget = Vector3.Zero;
		}

		if (ShouldForceContainmentEscape(pawn, currentBoundaryPressure, currentInwardDir, startLanePressure, startLaneInwardDir, config))
		{
			runtimeState.ContainmentTarget = BuildContainmentEscapePoint(pawn.GlobalPosition, currentBoundaryPressure, currentInwardDir, startLanePressure, startLaneInwardDir, config);
			runtimeState.ContainmentRemainingSeconds = ContainmentCommitSeconds + (startLanePressure > 0.24f ? 0.35f : 0f);
			var escapeIntent = BuildContainmentEscapeIntent(pawn, targetPosition, currentBoundaryPressure, currentInwardDir, startLanePressure, startLaneInwardDir, config, runtimeState.ContainmentTarget);
			RememberPosition(runtimeState, pawn.GlobalPosition);
			CommitSteerState(runtimeState, escapeIntent.Steer);
			return new ArenaVehicleAiDriveDecision(escapeIntent, 0f, allowFire: false);
		}

		var boundaryBias = BuildArenaBoundaryBias(pawn.GlobalPosition, config);
		if (boundaryBias.LengthSquared() > 0.0001f)
			ledTargetPosition += boundaryBias * config.ArenaWallTargetBiasFactor;
		if (startLanePressure > 0f)
		{
			var exitPoint = BuildStartLaneEscapePoint(pawn.GlobalPosition);
			ledTargetPosition = startLanePressure >= 0.04f
				? exitPoint
				: ledTargetPosition.Lerp(exitPoint, Mathf.Clamp(startLanePressure * 0.82f, 0f, 0.82f));
		}
		ledTargetPosition = ClampToArenaDriveBounds(ledTargetPosition, config, config.ArenaLaneClampMargin);

		var toTarget = ledTargetPosition - pawn.GlobalPosition;
		toTarget.Y = 0f;

		var distance = toTarget.Length();
		if (distance < 0.001f)
			distance = 0.001f;

		var preferredRange = config.PreferredRange * tacticalProfile.PreferredRangeScale;
		// Low-HP retreat hook: below the threshold the driver extends its standoff instead of trading.
		if (config.LowHpRetreatThreshold01 > 0f && selfHealth01 <= config.LowHpRetreatThreshold01)
			preferredRange *= config.LowHpRetreatRangeScale;
		var disengaging = UpdateDisengageState(runtimeState, distance, preferredRange, config, dt);
		var ramCommit = targetIsVehicle && config.RamCommitDistance > 0f && distance <= config.RamCommitDistance;
		var forwardSpeed = MathF.Abs(pawn.GetForwardSpeedSignedMps());
		// Hysteretic tactical-stance transitions: the raw range/speed test flip-flopped at the
		// activation boundary (orbit point one frame, pursuit point the next = visible jitter).
		// Hard eligibility (target type / disengage / ram) still switches instantly; only the
		// range/speed component holds its last verdict for TacticalHoldSeconds after a flip.
		var tacticalEligible = targetIsVehicle && !disengaging && !ramCommit;
		var tacticalRangeOk = distance <= preferredRange + (config.OrbitActivationDistance * 0.55f)
			&& forwardSpeed >= 2.25f;
		if (!tacticalEligible)
		{
			runtimeState.TacticalActive = false;
			runtimeState.TacticalHoldRemainingSeconds = 0f;
		}
		else
		{
			runtimeState.TacticalHoldRemainingSeconds = Mathf.Max(0f, runtimeState.TacticalHoldRemainingSeconds - dt);
			if (tacticalRangeOk != runtimeState.TacticalActive && runtimeState.TacticalHoldRemainingSeconds <= 0f)
			{
				runtimeState.TacticalActive = tacticalRangeOk;
				runtimeState.TacticalHoldRemainingSeconds = config.TacticalHoldSeconds;
			}
		}

		var useTacticalSteering = tacticalEligible && runtimeState.TacticalActive;

		// LOS-repossession: stance-holding with a pillar between the guns and the target is dead
		// time (the combat view's line-of-sight gate rightly holds fire the whole while). When the
		// direct line to the target crosses a known obstacle footprint inside weapon range, drop
		// the orbit/standoff point and pursue the target instead — the detour planner below then
		// swings the pawn around the blocker's edge, visibly cutting through gates and around
		// pillar corners to re-open the firing lane.
		if (useTacticalSteering
			&& config.LosRepositionRangeFactor > 0f
			&& distance <= config.WeaponFireRange * config.LosRepositionRangeFactor
			&& IsSegmentBlockedByFootprint(pawn.GlobalPosition, ledTargetPosition, obstacles))
		{
			useTacticalSteering = false;
		}

		// Attack-run cadence: kiting and orbiting park fixed front mounts out of bearing forever
		// (the fire planner can never pick a gun whose muzzle can't reach the target), so tiers
		// with those personalities delivered 4-5 shots per fight — all during the opening joust.
		// Periodically commit to a straight nose-on pass so the guns sweep across the target,
		// then resume the personality stance.
		if (config.AttackRunIntervalSeconds > 0f && targetIsVehicle && !ramCommit)
		{
			runtimeState.AttackRunRemainingSeconds = Mathf.Max(0f, runtimeState.AttackRunRemainingSeconds - dt);
			if (runtimeState.AttackRunRemainingSeconds > 0f)
			{
				disengaging = false;
				useTacticalSteering = false; // straight pursuit of the led target = nose on it
			}
			else
			{
				runtimeState.SecondsSinceAttackRun += dt;
				if (runtimeState.SecondsSinceAttackRun >= config.AttackRunIntervalSeconds
					&& distance <= config.WeaponFireRange * 1.15f)
				{
					runtimeState.SecondsSinceAttackRun = 0f;
					// A fleeing kiter with the target on its bumper needs a full 180 — scale the
					// run by how far the nose has to swing (up to 2x when the target is astern).
					var fwd = GetFlatForward(pawn);
					var toTargetDir = toTarget / distance;
					var facingDot = fwd.Dot(toTargetDir);
					runtimeState.AttackRunRemainingSeconds =
						config.AttackRunDurationSeconds * (1f + Mathf.Clamp((1f - facingDot) * 0.5f, 0f, 1f));
				}
			}
		}
		var baseSteeringPoint = useTacticalSteering
			? BuildSteeringPoint(pawn.GlobalPosition, ledTargetPosition, distance, preferredRange, tacticalProfile, config)
			: ledTargetPosition;
		if (disengaging)
			baseSteeringPoint = BuildDisengagePoint(pawn.GlobalPosition, targetPosition, config, obstacles);
		if (startLanePressure > 0f)
		{
			var exitPoint = BuildStartLaneEscapePoint(pawn.GlobalPosition);
			baseSteeringPoint = startLanePressure >= 0.04f
				? exitPoint
				: baseSteeringPoint.Lerp(exitPoint, Mathf.Clamp(startLanePressure, 0f, 0.92f));
		}
		if (boundaryBias.LengthSquared() > 0.0001f)
			baseSteeringPoint += boundaryBias;
		baseSteeringPoint = ClampToArenaDriveBounds(baseSteeringPoint, config, config.ArenaLaneClampMargin);

		// PLANNED obstacle avoidance: when the straight line to the steering point crosses a known
		// obstacle footprint (registry published by ArenaWorld), swing the aim point around the
		// better clearance side NOW instead of feeler-panicking at the last moment. The feelers
		// below stay on as the safety net. Start-lane transit keeps its funnel override and ram
		// commits keep raw plow pursuit.
		if (startLanePressure <= 0f && !ramCommit)
			baseSteeringPoint = ApplyObstacleDetour(pawn, baseSteeringPoint, obstacles, runtimeState, config, dt);
		else
			runtimeState.DetourCommitRemainingSeconds = 0f;

		var wallEscapeBlend = ComputeArenaWallEscapeBlend(currentBoundaryPressure, config);
		if (wallEscapeBlend > 0f && currentInwardDir.LengthSquared() > 0.0001f)
		{
			var interiorPoint = BuildArenaInteriorEscapePoint(pawn.GlobalPosition, currentInwardDir, config);
			baseSteeringPoint = baseSteeringPoint.Lerp(interiorPoint, wallEscapeBlend);
		}

		// While disengaging, lane candidates score progress along the flee line, not toward the enemy.
		var lanePlan = BuildLanePlan(pawn, baseSteeringPoint, disengaging ? baseSteeringPoint : ledTargetPosition, runtimeState, config);
		var steeringPoint = lanePlan.SteeringPoint;
		var probeSet = lanePlan.ProbeSet;
		if (wallEscapeBlend > 0f && currentInwardDir.LengthSquared() > 0.0001f)
		{
			var interiorPoint = BuildArenaInteriorEscapePoint(pawn.GlobalPosition, currentInwardDir, config);
			steeringPoint = steeringPoint.Lerp(interiorPoint, wallEscapeBlend);
		}

		var toSteeringPoint = steeringPoint - pawn.GlobalPosition;
		toSteeringPoint.Y = 0f;
		if (toSteeringPoint.LengthSquared() < 0.0001f)
			toSteeringPoint = toTarget;

		var desiredYaw = Mathf.Atan2(toSteeringPoint.X, toSteeringPoint.Z) + Mathf.Pi;
		var headingDiff = Mathf.Wrap(desiredYaw - pawn.Rotation.Y, -Mathf.Pi, Mathf.Pi);
		// PD steering: proportional on heading error plus a clamped derivative term that damps the
		// error as it closes — kills the overshoot/oscillation the pure P controller produced
		// (visible as constant tail-wagging at cruise). The derivative is skipped on the frame
		// after a committed state (recovery/breakaway/containment) since the error baseline is stale.
		var steerCmd = headingDiff * config.SteerGain;
		if (config.SteerDamping > 0f && runtimeState.HasLastHeadingError && dt > 0.0001f)
		{
			var errRate = Mathf.Wrap(headingDiff - runtimeState.LastHeadingError, -Mathf.Pi, Mathf.Pi) / dt;
			steerCmd += Mathf.Clamp(errRate * config.SteerDamping, -0.55f, 0.55f);
		}
		runtimeState.LastHeadingError = headingDiff;
		runtimeState.HasLastHeadingError = true;
		var steer = Mathf.Clamp(steerCmd, -1f, 1f);
		var absDiff = MathF.Abs(headingDiff);

		// Astern recovery (judge round 11: tier-4 weapons silence, root cause #2 below).
		// A target far astern needs the nose swung around before any fixed mount can fire again.
		// Two separate defects kept that from ever happening:
		//   1. STEER FLIP-FLOP: with the target dead astern, headingDiff jitters between +PI and
		//      -PI frame to frame, so steer alternated between +1 and -1 — net yaw zero. Latch the
		//      turn direction on entering the astern state and hold it (hysteresis: release below
		//      the wide-turn heading) so yaw actually accumulates.
		//   2. THROTTLE-FLOOR OVERRIDE (the killer): the two anti-creep floors below
		//      (far-distance -> FarThrottle, slow+non-tactical -> CruiseThrottle) ran AFTER the
		//      facing-away ReverseRecoverThrottle decision and Max()'d it back to nearly full
		//      FORWARD throttle. Probe telemetry showed the t4 STILETTO plodding straight AWAY
		//      from the player at thr=+0.68..0.72 with saturated alternating steer for 15+ s,
		//      weapons silent at bearing dot -1.00 (too fast for the stuck system's 1.35 m/s
		//      progress gate, no state to exit). Both floors are now gated on NOT facing away.
		// The stall K-turn handles the third case: reverse swing blocked by cover/walls (the
		// bicycle model yaws proportional to speed, so a blocked reverse means zero yaw forever) —
		// after 0.6 s astern at near-zero speed, commit to a short forward arc instead.
		// Ram commits are exempt from all astern special-casing: a committed rammer at point-blank
		// wants the old "floor to forward and plow" behavior (t5 regression probe: gating the
		// floors below on facing-away cut JUGGERNAUT's brawl output ~40% because its bulldozer
		// surges were exactly those floor overrides).
		var facingAway = absDiff > config.FacingAwayHeadingRad;
		var turnaroundActive = runtimeState.TurnaroundCommitRemainingSeconds > 0f && !ramCommit;
		if (facingAway && !probeSet.ForceReverse && !ramCommit)
		{
			if (runtimeState.TurnaroundSteerSign == 0)
				runtimeState.TurnaroundSteerSign = headingDiff >= 0f ? 1 : -1;
			steer = runtimeState.TurnaroundSteerSign;

			if (!turnaroundActive)
			{
				if (MathF.Abs(pawn.GetForwardSpeedSignedMps()) < 1.1f)
					runtimeState.AsternStallSeconds += dt;
				else
					runtimeState.AsternStallSeconds = Mathf.Max(0f, runtimeState.AsternStallSeconds - dt);

				if (runtimeState.AsternStallSeconds >= 0.6f)
				{
					runtimeState.AsternStallSeconds = 0f;
					runtimeState.TurnaroundCommitRemainingSeconds = 2.0f;
					turnaroundActive = true;
				}
			}
		}
		else
		{
			runtimeState.AsternStallSeconds = 0f;
			if (absDiff < config.WideTurnHeadingRad)
			{
				runtimeState.TurnaroundSteerSign = 0;
				runtimeState.TurnaroundCommitRemainingSeconds = 0f;
				turnaroundActive = false;
			}
		}

		if (turnaroundActive)
		{
			runtimeState.TurnaroundCommitRemainingSeconds = Mathf.Max(0f, runtimeState.TurnaroundCommitRemainingSeconds - dt);
			steer = runtimeState.TurnaroundSteerSign == 0 ? 1f : runtimeState.TurnaroundSteerSign;
		}

		if (wallEscapeBlend > 0f && currentInwardDir.LengthSquared() > 0.0001f)
		{
			var interiorPoint = BuildArenaInteriorEscapePoint(pawn.GlobalPosition, currentInwardDir, config);
			var steerToInterior = BuildSteerTowardPoint(pawn, interiorPoint, config.SteerGain * 1.35f);
			steer = Mathf.Lerp(steer, steerToInterior, wallEscapeBlend);
		}

		float throttle;
		if (probeSet.ForceReverse)
		{
			throttle = config.ReverseRecoverThrottle;
		}
		else if (turnaroundActive)
		{
			// Forward K-turn arc: enough throttle to actually generate yaw (see stall note above);
			// the blocked-path probe cap and wall throttle caps below still bound it near obstacles.
			throttle = MathF.Max(config.WideTurnThrottle, config.CruiseThrottle * 0.85f);
		}
		else if (absDiff > config.FacingAwayHeadingRad)
		{
			throttle = config.ReverseRecoverThrottle;
		}
		else if (absDiff > config.WideTurnHeadingRad)
		{
			throttle = config.WideTurnThrottle;
		}
		else if (disengaging)
		{
			// Kiter flee leg: the steering point faces away from the target, so range-hold
			// throttle rules (which key off distance-to-target) would wrongly reverse here.
			throttle = config.FarThrottle;
		}
		else if (ramCommit)
		{
			throttle = config.RamThrottle;
		}
		else if (distance > preferredRange + config.RangeTolerance)
		{
			throttle = config.FarThrottle;
		}
		else if (distance < preferredRange - config.RangeTolerance)
		{
			throttle = config.NearThrottle;
		}
		else
		{
			throttle = config.CruiseThrottle;
		}

		if (useTacticalSteering && distance <= config.OrbitActivationDistance && absDiff > config.BrakeTurnHeadingRad)
			throttle = MathF.Min(throttle, config.BrakeTurnThrottle);

		// Anti-creep floors — ONLY while roughly facing the pursuit point (or ram-committed, see
		// the ram exemption above). Applying them facing-away was root cause #2 of the tier-4
		// weapons silence: they Max()'d the turn-around/reverse decision back to full forward
		// throttle, driving the pawn straight AWAY from an astern target indefinitely (see the
		// astern-recovery note above).
		if (!useTacticalSteering && (!facingAway || ramCommit) && distance > preferredRange + config.RangeTolerance)
			throttle = MathF.Max(throttle, config.FarThrottle);
		if (!useTacticalSteering && (!facingAway || ramCommit) && forwardSpeed < 2.0f)
			throttle = MathF.Max(throttle, config.CruiseThrottle);

		if (probeSet.IsBlocked)
			throttle = MathF.Min(throttle, probeSet.ThrottleCap);

		if (wallEscapeBlend > 0f && currentInwardDir.LengthSquared() > 0.0001f)
		{
			var forward = GetFlatForward(pawn);
			var inwardDot = forward.Dot(currentInwardDir);
			var wallThrottleCap = Mathf.Lerp(config.ArenaWallThrottleSoftCap, config.ArenaWallThrottleHardCap, currentBoundaryPressure);
			if (inwardDot <= config.ArenaWallReverseDotThreshold)
				throttle = MathF.Min(throttle, config.ReverseRecoverThrottle);
			else if (inwardDot <= config.ArenaWallBrakeDotThreshold)
				throttle = MathF.Min(throttle, config.BrakeTurnThrottle);
			else
				throttle = MathF.Min(throttle, wallThrottleCap);
		}

		var align01 = Mathf.Clamp(1f - (absDiff / Mathf.Pi), 0f, 1f);
		var minAlignmentThrottleFactor = useTacticalSteering ? config.MinAlignmentThrottleFactor : 0.72f;
		throttle *= Mathf.Lerp(minAlignmentThrottleFactor, 1f, align01);

		// Steer slew limit: the wheel can only move so fast. This is what turns per-frame sign
		// flips (bang-bang jitter) into a smooth arc; at the default rate a full -1 -> +1 swing
		// still completes in ~0.3 s, so committed turns are unaffected.
		if (config.SteerSlewPerSecond > 0f && dt > 0f)
		{
			var maxDelta = config.SteerSlewPerSecond * dt;
			steer = Mathf.Clamp(steer, runtimeState.LastSteerOutput - maxDelta, runtimeState.LastSteerOutput + maxDelta);
		}
		runtimeState.LastSteerOutput = steer;

		var intent = new VehicleControlIntent(throttle, steer, targetPosition);
		if (ShouldEnterRecovery(pawn, runtimeState, probeSet, intent, dt, config))
		{
			StartRecovery(runtimeState, probeSet, config);
			var recoveryIntent = BuildRecoveryIntent(pawn, targetPosition, runtimeState, config);
			RememberPosition(runtimeState, pawn.GlobalPosition);
			CommitSteerState(runtimeState, recoveryIntent.Steer);
			return new ArenaVehicleAiDriveDecision(recoveryIntent, distance, allowFire: false);
		}

		RememberPosition(runtimeState, pawn.GlobalPosition);
		return new ArenaVehicleAiDriveDecision(intent, distance, allowFire: true);
	}

	public static bool IsWithinFireRange(float distanceToTarget, ArenaAiConfig config)
		=> distanceToTarget < config.WeaponFireRange;

	public static float GetFireChance(float distanceToTarget, float alignmentDot, ArenaAiConfig config, ArenaVehicleAiRuntimeState? runtimeState = null)
	{
		// Fire-discipline duty cycle: outside the fire window the trigger stays untouched.
		if (runtimeState is { FireGateOpen: false })
			return 0f;

		var baseChance = distanceToTarget < config.CloseFireDistance ? config.CloseFireChance : config.FarFireChance;
		if (alignmentDot <= config.MinFireDot)
			return Mathf.Clamp(baseChance * 0.55f, 0f, 1f);

		var alignSpan = MathF.Max(0.01f, 1f - config.MinFireDot);
		var align01 = Mathf.Clamp((alignmentDot - config.MinFireDot) / alignSpan, 0f, 1f);
		var chance = Mathf.Clamp(baseChance + align01 * config.AlignmentFireBonus, 0f, 1f);
		if (runtimeState != null && runtimeState.OpportunismWindowRemainingSeconds > 0f)
			chance = Mathf.Clamp(chance + config.OpportunismFireChanceBonus, 0f, 1f);
		return chance;
	}

	private static void UpdateFireGate(ArenaVehicleAiRuntimeState runtimeState, float dt, ArenaAiConfig config)
	{
		if (config.FireWindowSeconds <= 0f || config.FirePauseSeconds <= 0f)
		{
			runtimeState.FireGateOpen = true;
			runtimeState.FireCyclePhaseSeconds = 0f;
			return;
		}

		var cycleSeconds = config.FireWindowSeconds + config.FirePauseSeconds;
		runtimeState.FireCyclePhaseSeconds = (runtimeState.FireCyclePhaseSeconds + dt) % cycleSeconds;
		runtimeState.FireGateOpen = runtimeState.FireCyclePhaseSeconds < config.FireWindowSeconds;
	}

	private static void UpdateOpportunismWindow(ArenaVehicleAiRuntimeState runtimeState, Vector3 targetVelocity, float dt, ArenaAiConfig config)
	{
		runtimeState.OpportunismWindowRemainingSeconds = Mathf.Max(0f, runtimeState.OpportunismWindowRemainingSeconds - dt);
		if (config.OpportunismTurnRateRadPerSec <= 0f || config.OpportunismWindowSeconds <= 0f)
		{
			runtimeState.HasLastTargetVelocity = false;
			return;
		}

		var flatVelocity = targetVelocity;
		flatVelocity.Y = 0f;
		var lastVelocity = runtimeState.LastTargetVelocity;
		var hadValidSample = runtimeState.HasLastTargetVelocity;
		runtimeState.LastTargetVelocity = flatVelocity;
		runtimeState.HasLastTargetVelocity = flatVelocity.Length() >= OpportunismMinTargetSpeed;
		if (!hadValidSample || !runtimeState.HasLastTargetVelocity || dt <= 0f)
			return;

		var lastYaw = MathF.Atan2(lastVelocity.X, lastVelocity.Z);
		var currentYaw = MathF.Atan2(flatVelocity.X, flatVelocity.Z);
		var yawRate = MathF.Abs(Mathf.Wrap(currentYaw - lastYaw, -Mathf.Pi, Mathf.Pi)) / dt;
		if (yawRate >= config.OpportunismTurnRateRadPerSec)
			runtimeState.OpportunismWindowRemainingSeconds = config.OpportunismWindowSeconds;
	}

	private static bool UpdateDisengageState(
		ArenaVehicleAiRuntimeState runtimeState,
		float distance,
		float preferredRange,
		ArenaAiConfig config,
		float dt)
	{
		if (config.DisengageTriggerSeconds <= 0f || config.DisengageDistance <= 0f)
		{
			runtimeState.UnderPressureSeconds = 0f;
			runtimeState.DisengageActive = false;
			return false;
		}

		if (runtimeState.DisengageActive)
		{
			if (distance >= config.DisengageDistance)
			{
				runtimeState.DisengageActive = false;
				runtimeState.UnderPressureSeconds = 0f;
			}
			return runtimeState.DisengageActive;
		}

		if (distance < preferredRange - config.RangeTolerance)
			runtimeState.UnderPressureSeconds += dt;
		else
			runtimeState.UnderPressureSeconds = Mathf.Max(0f, runtimeState.UnderPressureSeconds - (dt * 1.5f));

		if (runtimeState.UnderPressureSeconds >= config.DisengageTriggerSeconds)
		{
			runtimeState.DisengageActive = true;
			runtimeState.UnderPressureSeconds = 0f;
		}

		return runtimeState.DisengageActive;
	}

	/// <summary>
	/// Committed-state steer bookkeeping: keeps the slew-limit baseline in sync with what was
	/// actually applied, and invalidates the PD error history so the derivative term does not
	/// spike on the first frame back in normal driving.
	/// </summary>
	private static void CommitSteerState(ArenaVehicleAiRuntimeState runtimeState, float steer)
	{
		runtimeState.LastSteerOutput = steer;
		runtimeState.HasLastHeadingError = false;
	}

	/// <summary>
	/// True when the straight XZ segment from <paramref name="from"/> to <paramref name="to"/>
	/// clips any registered obstacle footprint (actual radius, no clearance inflation — this asks
	/// "is the sight line physically interrupted", not "is the driving lane comfortable").
	/// </summary>
	public static bool IsSegmentBlockedByFootprint(Vector3 from, Vector3 to, IReadOnlyList<ArenaObstacleFootprint>? obstacles)
	{
		if (obstacles == null || obstacles.Count == 0)
			return false;

		var from2 = new Vector2(from.X, from.Z);
		var to2 = new Vector2(to.X, to.Z);
		var seg = to2 - from2;
		var segLen = seg.Length();
		if (segLen < 0.5f)
			return false;

		var dir = seg / segLen;
		var normal = new Vector2(-dir.Y, dir.X);
		foreach (var ob in obstacles)
		{
			var rel = ob.Center - from2;
			var t = rel.Dot(dir);
			if (t < 0.5f || t > segLen - 0.5f)
				continue;
			if (MathF.Abs(rel.Dot(normal)) < ob.Radius)
				return true;
		}

		return false;
	}

	/// <summary>
	/// True when a shot leaving <paramref name="muzzlePosition"/> along <paramref name="muzzleForward"/>
	/// would die on a registered obstacle within <paramref name="checkDistance"/> meters. Pure 2D
	/// footprint math on purpose: a physics ray here false-positives on floor grazes under nose
	/// pitch and on far walls (both of which are legitimate misses, not barrier grinding).
	/// Footprint radii are slightly shrunk because the bounding circle over-covers thin barriers.
	/// </summary>
	public static bool IsMuzzleLineBlockedByFootprint(
		Vector3 muzzlePosition,
		Vector3 muzzleForward,
		float checkDistance,
		IReadOnlyList<ArenaObstacleFootprint>? obstacles)
	{
		if (obstacles == null || obstacles.Count == 0 || checkDistance <= 0.5f)
			return false;

		var flatForward = new Vector2(muzzleForward.X, muzzleForward.Z);
		if (flatForward.LengthSquared() < 0.0001f)
			return false;
		var dir = flatForward.Normalized();
		var normal = new Vector2(-dir.Y, dir.X);
		var from2 = new Vector2(muzzlePosition.X, muzzlePosition.Z);
		foreach (var ob in obstacles)
		{
			var rel = ob.Center - from2;
			var t = rel.Dot(dir);
			if (t < 0.5f || t > checkDistance)
				continue;
			if (MathF.Abs(rel.Dot(normal)) < ob.Radius * 0.8f)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Plans around known cover instead of reacting to it: if the straight XZ segment from the pawn
	/// to <paramref name="steeringPoint"/> clips an obstacle footprint circle (inflated by the
	/// clearance margin), returns a detour aim point off the nearer edge of the FIRST blocker.
	/// The chosen side is latched for DetourCommitSeconds so alternating solutions cannot S-wiggle
	/// the pawn, and a side whose detour point would leave the drive bounds flips to the other.
	/// </summary>
	private static Vector3 ApplyObstacleDetour(
		VehiclePawn pawn,
		Vector3 steeringPoint,
		IReadOnlyList<ArenaObstacleFootprint>? obstacles,
		ArenaVehicleAiRuntimeState runtimeState,
		ArenaAiConfig config,
		float dt)
	{
		runtimeState.DetourCommitRemainingSeconds = Mathf.Max(0f, runtimeState.DetourCommitRemainingSeconds - dt);
		if (obstacles == null || obstacles.Count == 0 || config.DetourClearanceMeters <= 0f)
			return steeringPoint;

		var from2 = new Vector2(pawn.GlobalPosition.X, pawn.GlobalPosition.Z);
		var to2 = new Vector2(steeringPoint.X, steeringPoint.Z);
		var seg = to2 - from2;
		var segLen = seg.Length();
		if (segLen < 1.5f)
		{
			runtimeState.DetourSideSign = 0;
			return steeringPoint;
		}

		var scan = MathF.Min(segLen, config.DetourScanMeters);
		var dir = seg / segLen;
		var normal = new Vector2(-dir.Y, dir.X);

		var bestT = float.MaxValue;
		ArenaObstacleFootprint blocker = default;
		var found = false;
		foreach (var ob in obstacles)
		{
			var inflated = ob.Radius + config.DetourClearanceMeters;
			var rel = ob.Center - from2;
			var t = rel.Dot(dir);
			if (t < 0.5f || t > scan)
				continue; // behind us or beyond the scan window
			if (MathF.Abs(rel.Dot(normal)) >= inflated)
				continue; // the straight line already clears this footprint
			if (rel.Length() <= inflated - 0.6f)
				continue; // we're basically inside its ring — the feeler safety net owns this
			if (t < bestT)
			{
				bestT = t;
				blocker = ob;
				found = true;
			}
		}

		if (!found)
		{
			runtimeState.DetourSideSign = 0;
			return steeringPoint;
		}

		// Pass on the side the line is already biased toward (least deviation), unless a side was
		// committed moments ago or that side's aim point would leave the drive bounds.
		var sideOffset = (blocker.Center - from2).Dot(normal);
		var side = sideOffset >= 0f ? -1 : 1;
		if (runtimeState.DetourSideSign != 0 && runtimeState.DetourCommitRemainingSeconds > 0f)
		{
			side = runtimeState.DetourSideSign;
		}
		else
		{
			var probe2 = blocker.Center + normal * (side * (blocker.Radius + config.DetourClearanceMeters + 1.2f));
			var clamped = ClampToArenaDriveBounds(new Vector3(probe2.X, 0f, probe2.Y), config, config.ArenaLaneClampMargin);
			if (new Vector2(clamped.X, clamped.Z).DistanceTo(probe2) > 1.5f)
				side = -side;
			runtimeState.DetourSideSign = side;
			runtimeState.DetourCommitRemainingSeconds = config.DetourCommitSeconds;
			runtimeState.DetourCount++;
		}

		var detour2 = blocker.Center + normal * (side * (blocker.Radius + config.DetourClearanceMeters + 1.2f));
		return ClampToArenaDriveBounds(new Vector3(detour2.X, steeringPoint.Y, detour2.Y), config, config.ArenaLaneClampMargin);
	}

	private static Vector3 BuildDisengagePoint(
		Vector3 pawnPosition,
		Vector3 targetPosition,
		ArenaAiConfig config,
		IReadOnlyList<ArenaObstacleFootprint>? obstacles = null)
	{
		// Reposition-through-cover: a disengaging driver that can put a pillar/wreck between
		// itself and the shooter breaks line of sight instead of just running in the open. Only
		// footprints big enough to hide a car qualify; the flee falls back to the open-field loop
		// when no sensible cover exists.
		if (obstacles is { Count: > 0 } && config.CoverSeekMaxMeters > 0f)
		{
			var pawn2 = new Vector2(pawnPosition.X, pawnPosition.Z);
			var target2 = new Vector2(targetPosition.X, targetPosition.Z);
			var bestScore = float.MinValue;
			var bestPoint = Vector3.Zero;
			var foundCover = false;
			foreach (var ob in obstacles)
			{
				if (ob.Radius < 1.2f)
					continue; // tires/cans do not hide a vehicle
				var obDistance = (ob.Center - pawn2).Length();
				if (obDistance < 2f || obDistance > config.CoverSeekMaxMeters)
					continue;
				var awayFromTarget = ob.Center - target2;
				var awayLen = awayFromTarget.Length();
				if (awayLen < 0.01f)
					continue;
				var coverPoint2 = ob.Center + (awayFromTarget / awayLen) * (ob.Radius + config.CoverStandoffMeters);
				var travel = (coverPoint2 - pawn2).Length();
				var separationGain = (coverPoint2 - target2).Length() - (pawn2 - target2).Length();
				if (separationGain < 0f)
					continue; // never flee TOWARD the shooter
				var score = (separationGain * 0.6f) - travel;
				if (score > bestScore)
				{
					bestScore = score;
					bestPoint = new Vector3(coverPoint2.X, pawnPosition.Y, coverPoint2.Y);
					foundCover = true;
				}
			}

			if (foundCover)
				return ClampToArenaDriveBounds(bestPoint, config, config.ArenaLaneClampMargin);
		}

		var away3 = pawnPosition - targetPosition;
		away3.Y = 0f;
		var away = away3.LengthSquared() < 0.0001f ? Vector3.Forward : away3.Normalized();

		// Bias the flee line toward the arena center so the kiter loops around instead of
		// pinning itself against a wall (wall-escape logic then never has to fight the flee).
		var centerPull = -pawnPosition;
		centerPull.Y = 0f;
		if (centerPull.LengthSquared() > 0.0001f)
			away = (away + centerPull.Normalized() * 0.45f).Normalized();

		var fleeDistance = MathF.Max(10f, config.DisengageDistance * 0.5f);
		return ClampToArenaDriveBounds(pawnPosition + away * fleeDistance, config, config.ArenaLaneClampMargin);
	}

	private static LanePlan BuildLanePlan(
		VehiclePawn pawn,
		Vector3 desiredSteeringPoint,
		Vector3 targetPosition,
		ArenaVehicleAiRuntimeState runtimeState,
		ArenaAiConfig config)
	{
		var forward = GetFlatForward(pawn);
		var toDesired = desiredSteeringPoint - pawn.GlobalPosition;
		toDesired.Y = 0f;
		var desiredDir = toDesired.LengthSquared() < 0.0001f ? forward : toDesired.Normalized();
		var desiredDistance = MathF.Max(4f, toDesired.Length());
		var lateral = new Vector3(-desiredDir.Z, 0f, desiredDir.X);
		if (lateral.LengthSquared() < 0.0001f)
			lateral = GetFlatRight(pawn);
		else
			lateral = lateral.Normalized();

		var laneOffset = Mathf.Clamp(desiredDistance * config.LaneOffsetDistanceFactor, config.LaneOffsetMinMeters, config.LaneOffsetMaxMeters);
		LaneCandidate? bestCandidate = null;
		foreach (var sign in new[] { 0, runtimeState.LaneSign != 0 ? runtimeState.LaneSign : 1, runtimeState.LaneSign != 0 ? -runtimeState.LaneSign : -1 })
		{
			var steeringPoint = desiredSteeringPoint + lateral * (sign * laneOffset);
			steeringPoint = ClampToArenaDriveBounds(steeringPoint, config, config.ArenaLaneClampMargin);
			var scoreCandidate = ScoreLaneCandidate(pawn, steeringPoint, targetPosition, sign, runtimeState, config);
			if (bestCandidate == null || scoreCandidate.Score > bestCandidate.Value.Score)
				bestCandidate = scoreCandidate;
		}

		var selected = bestCandidate ?? ScoreLaneCandidate(pawn, desiredSteeringPoint, targetPosition, 0, runtimeState, config);
		var chosenSign = selected.Sign;
		if (runtimeState.LaneCommitRemainingSeconds <= 0f && chosenSign != 0)
		{
			runtimeState.LaneSign = chosenSign;
			runtimeState.LaneCommitRemainingSeconds = config.LaneCommitSeconds;
		}
		else if (runtimeState.LaneCommitRemainingSeconds <= 0f && chosenSign == 0 && selected.ProbeSet.CenterClear01 >= CandidateClearPathThreshold)
		{
			runtimeState.LaneSign = 0;
		}

		return new LanePlan(selected.SteeringPoint, selected.ProbeSet);
	}

	private static LaneCandidate ScoreLaneCandidate(
		VehiclePawn pawn,
		Vector3 steeringPoint,
		Vector3 targetPosition,
		int sign,
		ArenaVehicleAiRuntimeState runtimeState,
		ArenaAiConfig config)
	{
		var probeSet = SampleProbeSetTowardPoint(pawn, steeringPoint, config);
		var toTarget = targetPosition - pawn.GlobalPosition;
		toTarget.Y = 0f;
		var toSteering = steeringPoint - pawn.GlobalPosition;
		toSteering.Y = 0f;
		var pathDir = toSteering.LengthSquared() < 0.0001f ? GetFlatForward(pawn) : toSteering.Normalized();
		var targetDir = toTarget.LengthSquared() < 0.0001f ? pathDir : toTarget.Normalized();
		var pathProgress = Mathf.Clamp(pathDir.Dot(targetDir), -1f, 1f);
		var score = (probeSet.PathClear01 * CandidatePathWeight)
			+ (Mathf.Clamp(pathProgress, -1f, 1f) * CandidateProgressWeight)
			+ (probeSet.BestSideClear01 * CandidateWideLaneBias);

		var lookAheadDistance = MathF.Max(4f, MathF.Min(toSteering.Length(), config.FeelerMaxDistance + 4f));
		var predictedPoint = pawn.GlobalPosition + pathDir * lookAheadDistance;
		var boundaryPressure = GetArenaBoundaryPressure(predictedPoint, config, out var inwardDir);
		var boundarySafety = 1f - boundaryPressure;
		if (boundaryPressure > 0f)
		{
			var inwardAlignment = inwardDir.LengthSquared() > 0.0001f
				? Mathf.Clamp(pathDir.Dot(inwardDir), -1f, 1f)
				: 0f;
			score += boundarySafety * CandidateBoundarySafetyWeight;
			score += inwardAlignment * CandidateBoundaryDirectionWeight;
		}
		else
		{
			score += CandidateBoundarySafetyWeight;
		}

		if (sign == 0)
			score += CandidateDirectBonus;
		if (runtimeState.LaneCommitRemainingSeconds > 0f && sign != 0 && sign == runtimeState.LaneSign)
			score += CandidateLaneCommitBonus;
		else if (runtimeState.LaneCommitRemainingSeconds > 0f && runtimeState.LaneSign != 0 && sign != 0 && sign != runtimeState.LaneSign)
			score -= CandidateLaneSwitchPenalty;
		if (probeSet.ForceReverse)
			score -= 3.8f;
		if (sign != 0 && probeSet.PathClear01 >= CandidateClearPathThreshold)
			score += 0.35f;

		return new LaneCandidate(sign, steeringPoint, probeSet, score);
	}

	/// <summary>
	/// Accumulates near-contact crawl time against the target and arms the breakaway state when it
	/// exceeds the configured trigger. Disabled when ContactBreakawayTriggerSeconds is 0 or the
	/// target is on foot (pinning a driver pawn is legitimate pressure, not a stalemate).
	/// </summary>
	private static void UpdateContactBreakaway(
		VehiclePawn pawn,
		ArenaVehicleAiRuntimeState runtimeState,
		Vector3 targetPosition,
		bool targetIsVehicle,
		float dt,
		ArenaAiConfig config)
	{
		if (config.ContactBreakawayTriggerSeconds <= 0f || !targetIsVehicle)
		{
			runtimeState.ContactCrawlSeconds = 0f;
			return;
		}

		if (runtimeState.BreakawayRemainingSeconds > 0f)
			return;

		var toTarget = targetPosition - pawn.GlobalPosition;
		toTarget.Y = 0f;
		var rawDistance = toTarget.Length();
		var speedKmh = MathF.Abs(pawn.GetForwardSpeedSignedMps()) * 3.6f;
		var inContactCrawl = rawDistance <= config.ContactBreakawayDistance
			&& speedKmh <= config.ContactBreakawayMinSpeedKmh;
		if (inContactCrawl)
			runtimeState.ContactCrawlSeconds += dt;
		else
			runtimeState.ContactCrawlSeconds = Mathf.Max(0f, runtimeState.ContactCrawlSeconds - (dt * 2f));

		if (runtimeState.ContactCrawlSeconds < config.ContactBreakawayTriggerSeconds)
			return;

		runtimeState.ContactCrawlSeconds = 0f;
		runtimeState.BreakawayRemainingSeconds = config.ContactBreakawayDurationSeconds;
		// Arc out on the side AWAY from the target so the reverse swing points the nose clear of
		// the grind partner and the next approach comes in as a fresh pass.
		var targetSide = rawDistance > 0.001f ? GetFlatRight(pawn).Dot(toTarget / rawDistance) : 0f;
		runtimeState.BreakawaySteerSign = targetSide >= 0f ? -1 : 1;
	}

	/// <summary>Committed reverse-arc pull-out: back up hard while wheeling toward the open side.</summary>
	private static VehicleControlIntent BuildContactBreakawayIntent(
		VehiclePawn pawn,
		Vector3 targetPosition,
		ArenaVehicleAiRuntimeState runtimeState,
		ArenaAiConfig config)
	{
		var forward = GetFlatForward(pawn);
		var right = GetFlatRight(pawn);
		var sign = runtimeState.BreakawaySteerSign == 0 ? 1 : runtimeState.BreakawaySteerSign;
		var desiredPoint = ClampToArenaDriveBounds(
			pawn.GlobalPosition - forward * config.RecoveryReverseDistance + right * sign * config.RecoveryReverseLateralDistance,
			config,
			config.ArenaLaneClampMargin);
		var steer = BuildSteerTowardPoint(pawn, desiredPoint, config.SteerGain * 1.15f);
		return new VehicleControlIntent(config.ReverseRecoverThrottle, steer, targetPosition);
	}

	private static bool ShouldEnterRecovery(
		VehiclePawn pawn,
		ArenaVehicleAiRuntimeState runtimeState,
		DirectionalProbeSet probeSet,
		VehicleControlIntent intent,
		float dt,
		ArenaAiConfig config)
	{
		var effortHigh = MathF.Abs(intent.Throttle) >= config.StuckEffortThrottle || MathF.Abs(intent.Steer) >= config.StuckEffortSteer;
		var forwardSpeed = MathF.Abs(pawn.GetForwardSpeedSignedMps());
		var actualMovement = MeasureMovementSpeed(runtimeState, pawn.GlobalPosition, dt);
		var lowMovement = actualMovement <= config.StuckMinProgressSpeed && forwardSpeed <= config.StuckSpeedThreshold;
		var collided = pawn.GetSlideCollisionCount() > 0;
		var blocked = probeSet.ImmediateBlock || probeSet.ForceReverse;
		var boundaryPressure = GetArenaBoundaryPressure(pawn.GlobalPosition, config, out var inwardDir);
		if (boundaryPressure >= config.ArenaWallEscapePressure && inwardDir.LengthSquared() > 0.0001f)
		{
			var forward = GetFlatForward(pawn);
			if (forward.Dot(inwardDir) <= config.ArenaWallBrakeDotThreshold)
				blocked = true;
		}

		if (effortHigh && (lowMovement || collided || blocked))
			runtimeState.LowProgressSeconds += dt;
		else
			runtimeState.LowProgressSeconds = Mathf.Max(0f, runtimeState.LowProgressSeconds - (dt * 1.7f));

		return runtimeState.LowProgressSeconds >= config.StuckTriggerSeconds;
	}

	private static void StartRecovery(ArenaVehicleAiRuntimeState runtimeState, DirectionalProbeSet probeSet, ArenaAiConfig config)
	{
		var chainRecovery = runtimeState.TimeSinceLastRecoverySeconds <= config.RecoveryChainWindowSeconds;
		runtimeState.RecoveryAttemptCount = chainRecovery
			? Math.Min(runtimeState.RecoveryAttemptCount + 1, config.MaxRecoveryAttempts)
			: 1;
		runtimeState.TimeSinceLastRecoverySeconds = 0f;
		runtimeState.LowProgressSeconds = 0f;

		var sign = probeSet.BestEscapeSign;
		if (runtimeState.RecoveryAttemptCount > 1 && MathF.Abs(probeSet.LeftClear01 - probeSet.RightClear01) < 0.08f)
			sign = runtimeState.RecoverySteerSign * -1;
		if (sign == 0)
			sign = runtimeState.RecoverySteerSign == 0 ? 1 : runtimeState.RecoverySteerSign * -1;

		runtimeState.RecoverySteerSign = sign;
		runtimeState.LaneSign = sign;
		runtimeState.LaneCommitRemainingSeconds = config.RecoveryLaneCommitSeconds;
		runtimeState.RecoveryRemainingSeconds = config.UnstuckDuration
			+ ((runtimeState.RecoveryAttemptCount - 1) * config.RecoveryAttemptExtraSeconds);
	}

	private static VehicleControlIntent BuildRecoveryIntent(
		VehiclePawn pawn,
		Vector3 targetPosition,
		ArenaVehicleAiRuntimeState runtimeState,
		ArenaAiConfig config)
	{
		var forward = GetFlatForward(pawn);
		var right = GetFlatRight(pawn);
		var reversePhaseThreshold = runtimeState.RecoveryRemainingSeconds <= 0f
			? 0f
			: config.UnstuckDuration * config.RecoveryReversePhaseRatio;
		var reversePhase = runtimeState.RecoveryRemainingSeconds > reversePhaseThreshold;
		var sign = runtimeState.RecoverySteerSign == 0 ? 1 : runtimeState.RecoverySteerSign;
		if (GetStartLanePressure(pawn.GlobalPosition, out var startLaneInward) > 0f && startLaneInward.LengthSquared() > 0.0001f)
		{
			var escapePoint = BuildStartLaneEscapePoint(pawn.GlobalPosition);
			var laneSteerGain = reversePhase ? config.SteerGain * 1.34f : config.SteerGain * 1.18f;
			var laneSteer = BuildSteerTowardPoint(pawn, escapePoint, laneSteerGain);
			var laneAlignment = forward.Dot(startLaneInward.Normalized());
			var laneThrottle = reversePhase && laneAlignment <= 0.18f
				? config.UnstuckThrottle
				: Mathf.Max(config.BrakeTurnThrottle, config.RecoveryForwardThrottle);
			if (laneThrottle > 0f && MathF.Abs(laneSteer) > 0.72f)
				laneThrottle = MathF.Min(laneThrottle, config.BrakeTurnThrottle);
			return new VehicleControlIntent(laneThrottle, laneSteer, targetPosition);
		}

		var boundaryPressure = GetArenaBoundaryPressure(pawn.GlobalPosition, config, out var inwardDir);
		if (boundaryPressure >= config.ArenaWallEscapePressure && inwardDir.LengthSquared() > 0.0001f)
		{
			inwardDir = inwardDir.Normalized();
			var travelDistance = reversePhase ? config.RecoveryReverseDistance : config.RecoveryForwardDistance;
			var lateralDistance = reversePhase ? config.RecoveryReverseLateralDistance : config.RecoveryForwardLateralDistance;
			var wallEscapePoint = ClampToArenaDriveBounds(
				pawn.GlobalPosition + inwardDir * travelDistance + right * sign * lateralDistance,
				config,
				config.ArenaLaneClampMargin);
			var wallSteerGain = reversePhase ? config.SteerGain * 1.28f : config.SteerGain * 1.12f;
			var wallSteer = BuildSteerTowardPoint(pawn, wallEscapePoint, wallSteerGain);
			var inwardDot = forward.Dot(inwardDir);
			var wallThrottle = reversePhase && inwardDot <= config.ArenaWallBrakeDotThreshold
				? config.UnstuckThrottle
				: Mathf.Max(config.CruiseThrottle, config.RecoveryForwardThrottle);
			return new VehicleControlIntent(wallThrottle, wallSteer, targetPosition);
		}

		var desiredPoint = reversePhase
			? pawn.GlobalPosition - forward * config.RecoveryReverseDistance + right * sign * config.RecoveryReverseLateralDistance
			: pawn.GlobalPosition + forward * config.RecoveryForwardDistance + right * sign * config.RecoveryForwardLateralDistance;
		var steerGain = reversePhase ? config.SteerGain * 1.18f : config.SteerGain * 1.06f;
		var steer = BuildSteerTowardPoint(pawn, desiredPoint, steerGain);
		var throttle = reversePhase ? config.UnstuckThrottle : Mathf.Max(config.CruiseThrottle, config.RecoveryForwardThrottle);
		return new VehicleControlIntent(throttle, steer, targetPosition);
	}

	private static DirectionalProbeSet SampleProbeSetTowardPoint(VehiclePawn pawn, Vector3 point, ArenaAiConfig config)
	{
		var direction = point - pawn.GlobalPosition;
		direction.Y = 0f;
		if (direction.LengthSquared() < 0.0001f)
			direction = GetFlatForward(pawn);
		else
			direction = direction.Normalized();

		return SampleDirectionalProbeSet(pawn, direction, config);
	}


	private static bool ShouldForceContainmentEscape(
		VehiclePawn pawn,
		float boundaryPressure,
		Vector3 inwardDirection,
		float startLanePressure,
		Vector3 startLaneInwardDirection,
		ArenaAiConfig config)
	{
		if (startLanePressure >= 0.12f && startLaneInwardDirection.LengthSquared() > 0.0001f)
			return true;

		if (boundaryPressure < config.ArenaWallEscapePressure * 0.82f || inwardDirection.LengthSquared() < 0.0001f)
			return false;

		var forward = GetFlatForward(pawn);
		var inwardDot = forward.Dot(inwardDirection.Normalized());
		if (boundaryPressure >= config.ArenaWallEscapePressure + 0.08f)
			return true;

		return inwardDot <= 0.28f || MathF.Abs(pawn.GetForwardSpeedSignedMps()) < 6.5f;
	}

	private static VehicleControlIntent BuildContainmentEscapeIntent(
		VehiclePawn pawn,
		Vector3 targetPosition,
		float boundaryPressure,
		Vector3 inwardDirection,
		float startLanePressure,
		Vector3 startLaneInwardDirection,
		ArenaAiConfig config,
		Vector3? forcedEscapePoint = null)
	{
		var escapePoint = forcedEscapePoint ?? BuildContainmentEscapePoint(pawn.GlobalPosition, boundaryPressure, inwardDirection, startLanePressure, startLaneInwardDirection, config);
		var forward = GetFlatForward(pawn);
		var toEscape = escapePoint - pawn.GlobalPosition;
		toEscape.Y = 0f;
		if (toEscape.LengthSquared() < 0.0001f)
			toEscape = (startLaneInwardDirection.LengthSquared() > 0.0001f ? startLaneInwardDirection : inwardDirection).Normalized();
		else
			toEscape = toEscape.Normalized();

		var steerGain = startLanePressure > 0f ? config.SteerGain * 1.55f : config.SteerGain * 1.35f;
		var steer = BuildSteerTowardPoint(pawn, escapePoint, steerGain);
		var alignment = forward.Dot(toEscape);
		float throttle;
		if (alignment <= config.ArenaWallReverseDotThreshold)
		{
			throttle = config.ReverseRecoverThrottle;
		}
		else if (alignment <= config.ArenaWallBrakeDotThreshold || MathF.Abs(steer) >= 0.74f)
		{
			throttle = config.BrakeTurnThrottle;
		}
		else
		{
			var align01 = Mathf.Clamp((alignment + 0.15f) / 1.15f, 0f, 1f);
			var escapeCruise = Mathf.Max(config.CruiseThrottle, config.RecoveryForwardThrottle);
			throttle = Mathf.Lerp(config.BrakeTurnThrottle, escapeCruise, align01);
		}

		if (boundaryPressure > 0.82f && throttle > 0f)
			throttle = MathF.Min(throttle, MathF.Max(config.BrakeTurnThrottle, config.ArenaWallThrottleSoftCap));
		if ((startLanePressure > 0.18f || boundaryPressure > 0.62f) && throttle > 0f && MathF.Abs(steer) >= 0.58f)
			throttle = MathF.Min(throttle, config.BrakeTurnThrottle);

		return new VehicleControlIntent(throttle, steer, targetPosition);
	}

	private static Vector3 BuildContainmentEscapePoint(
		Vector3 position,
		float boundaryPressure,
		Vector3 inwardDirection,
		float startLanePressure,
		Vector3 startLaneInwardDirection,
		ArenaAiConfig config)
	{
		if (startLanePressure > 0f)
		{
			return ClampToArenaDriveBounds(BuildStartLaneEscapePoint(position), config, config.ArenaLaneClampMargin);
		}

		if (boundaryPressure > 0f && inwardDirection.LengthSquared() > 0.0001f)
		{
			var interiorPoint = BuildArenaInteriorEscapePoint(position, inwardDirection, config);
			var centerPull = new Vector3(
				Mathf.Lerp(position.X, 0f, 0.86f),
				position.Y,
				Mathf.Clamp(position.Z * 0.35f, -34f, 34f));
			return ClampToArenaDriveBounds(interiorPoint.Lerp(centerPull, 0.74f), config, config.ArenaLaneClampMargin);
		}

		return ClampToArenaDriveBounds(new Vector3(0f, position.Y, 0f), config, config.ArenaLaneClampMargin);
	}

	private static DirectionalProbeSet SampleDirectionalProbeSet(VehiclePawn pawn, Vector3 direction, ArenaAiConfig config)
	{
		var origin = pawn.GlobalPosition + Vector3.Up * AvoidanceEyeHeight;
		var right = new Vector3(-direction.Z, 0f, direction.X);
		if (right.LengthSquared() < 0.0001f)
			right = GetFlatRight(pawn);
		else
			right = right.Normalized();

		var speed = MathF.Abs(pawn.GetForwardSpeedSignedMps());
		var rayDistance = Mathf.Clamp(config.FeelerMinDistance + (speed * config.FeelerDistancePerSpeed), config.FeelerMinDistance, config.FeelerMaxDistance);
		var innerLeftDir = (direction - right * AvoidanceInnerSideWeight).Normalized();
		var innerRightDir = (direction + right * AvoidanceInnerSideWeight).Normalized();
		var outerLeftDir = (direction - right * AvoidanceOuterSideWeight).Normalized();
		var outerRightDir = (direction + right * AvoidanceOuterSideWeight).Normalized();

		var centerClear = SampleObstacleClearance(pawn, origin, direction, rayDistance);
		var leftClear = SampleObstacleClearance(pawn, origin + right * -AvoidanceSideOffset, innerLeftDir, rayDistance);
		var rightClear = SampleObstacleClearance(pawn, origin + right * AvoidanceSideOffset, innerRightDir, rayDistance);
		var farLeftClear = SampleObstacleClearance(pawn, origin + right * -(AvoidanceSideOffset * 1.35f), outerLeftDir, rayDistance);
		var farRightClear = SampleObstacleClearance(pawn, origin + right * (AvoidanceSideOffset * 1.35f), outerRightDir, rayDistance);

		var centerClear01 = Mathf.Clamp(centerClear / rayDistance, 0f, 1f);
		var leftClear01 = Mathf.Clamp(leftClear / rayDistance, 0f, 1f);
		var rightClear01 = Mathf.Clamp(rightClear / rayDistance, 0f, 1f);
		var farLeftClear01 = Mathf.Clamp(farLeftClear / rayDistance, 0f, 1f);
		var farRightClear01 = Mathf.Clamp(farRightClear / rayDistance, 0f, 1f);
		var pathClear01 = (centerClear01 * 0.42f) + (MathF.Max(leftClear01, rightClear01) * 0.28f) + (MathF.Max(farLeftClear01, farRightClear01) * 0.30f);
		var bestEscapeSign = (rightClear + farRightClear) >= (leftClear + farLeftClear) ? 1 : -1;
		var bestSideClear01 = MathF.Max((leftClear01 + farLeftClear01) * 0.5f, (rightClear01 + farRightClear01) * 0.5f);
		var immediateBlock = centerClear01 <= config.ImmediateBlockClearance01;
		var forceReverse = centerClear01 <= config.ReverseBlockClearance01
			&& MathF.Max(leftClear01, rightClear01) <= config.ReverseSideClearance01;
		var throttleCap = forceReverse
			? config.ReverseRecoverThrottle
			: Mathf.Lerp(config.BlockedThrottleCap, config.OpenThrottleCap, Mathf.Clamp(pathClear01, 0f, 1f));

		return new DirectionalProbeSet(
			centerClear01,
			leftClear01,
			rightClear01,
			farLeftClear01,
			farRightClear01,
			pathClear01,
			bestSideClear01,
			bestEscapeSign,
			immediateBlock,
			forceReverse,
			throttleCap);
	}

	private static Vector3 BuildArenaBoundaryBias(Vector3 position, ArenaAiConfig config)
	{
		var pressure = GetArenaBoundaryPressure(position, config, out var inwardDir);
		if (pressure <= 0f || inwardDir.LengthSquared() < 0.0001f)
			return Vector3.Zero;

		return inwardDir * (config.ArenaWallBiasMeters * pressure);
	}

	public static float GetArenaBoundaryPressure(Vector3 position, ArenaAiConfig config, out Vector3 inwardDirection)
	{
		var inwardAccumulator = Vector3.Zero;
		var maxPressure = 0f;

		void ConsiderWall(float distanceToWall, Vector3 inward)
		{
			var pressure = ComputeWallPressure(distanceToWall, config);
			if (pressure <= 0f)
				return;

			inwardAccumulator += inward * pressure;
			maxPressure = MathF.Max(maxPressure, pressure);
		}

		ConsiderWall(config.ArenaDriveHalfWidth - position.X, Vector3.Left);
		ConsiderWall(position.X + config.ArenaDriveHalfWidth, Vector3.Right);
		ConsiderWall(config.ArenaDriveHalfHeight - position.Z, Vector3.Forward);
		ConsiderWall(position.Z + config.ArenaDriveHalfHeight, Vector3.Back);

		var startLanePressure = GetStartLanePressure(position, out var startLaneInward);
		if (startLanePressure > 0f)
		{
			inwardAccumulator += startLaneInward * startLanePressure;
			maxPressure = MathF.Max(maxPressure, startLanePressure);
		}

		inwardAccumulator.Y = 0f;
		inwardDirection = inwardAccumulator.LengthSquared() > 0.0001f
			? inwardAccumulator.Normalized()
			: Vector3.Zero;

		return Mathf.Clamp(maxPressure, 0f, 1f);
	}

	private static float ComputeWallPressure(float distanceToWall, ArenaAiConfig config)
	{
		if (distanceToWall >= config.ArenaWallSoftMargin)
			return 0f;
		if (distanceToWall <= config.ArenaWallHardMargin)
			return 1f;

		var span = MathF.Max(0.01f, config.ArenaWallSoftMargin - config.ArenaWallHardMargin);
		return 1f - Mathf.Clamp((distanceToWall - config.ArenaWallHardMargin) / span, 0f, 1f);
	}

	private static float GetStartLanePressure(Vector3 position, out Vector3 inwardDirection)
	{
		inwardDirection = Vector3.Zero;
		if (MathF.Abs(position.Z) <= StartLaneInnerZ)
			return 0f;

		var exitPoint = BuildStartLaneEscapePoint(position);
		var toExit = exitPoint - position;
		toExit.Y = 0f;
		if (toExit.LengthSquared() < 0.0001f)
			return 0f;

		inwardDirection = toExit.Normalized();
		var laneDepthSpan = MathF.Max(0.01f, StartLaneOuterZ - StartLaneInnerZ);
		var depth01 = Mathf.Clamp((MathF.Abs(position.Z) - StartLaneInnerZ) / laneDepthSpan, 0f, 1f);
		var centerPenalty = Mathf.Clamp(MathF.Abs(position.X) / StartLaneHalfWidth, 0f, 1f);
		return Mathf.Clamp(depth01 + (centerPenalty * 0.32f), 0f, 1f);
	}

	private static Vector3 BuildStartLaneEscapePoint(Vector3 position)
	{
		var sign = position.Z >= 0f ? 1f : -1f;
		var targetX = Mathf.Lerp(position.X, 0f, 0.94f);
		var targetZ = sign * Mathf.Min(StartLaneInnerZ - StartLaneExitInset, StartLaneTransitZ);
		return new Vector3(targetX, position.Y, targetZ);
	}

	private static float ComputeArenaWallEscapeBlend(float boundaryPressure, ArenaAiConfig config)
	{
		if (boundaryPressure <= config.ArenaWallEscapePressure)
			return 0f;

		var span = MathF.Max(0.01f, 1f - config.ArenaWallEscapePressure);
		var blend01 = Mathf.Clamp((boundaryPressure - config.ArenaWallEscapePressure) / span, 0f, 1f);
		return blend01 * config.ArenaWallSteerBlend;
	}

	private static Vector3 BuildArenaInteriorEscapePoint(Vector3 position, Vector3 inwardDirection, ArenaAiConfig config)
	{
		inwardDirection.Y = 0f;
		if (inwardDirection.LengthSquared() < 0.0001f)
			return ClampToArenaDriveBounds(position, config, config.ArenaLaneClampMargin);

		var escapeDistance = MathF.Max(config.ArenaWallBiasMeters * 1.4f, config.ArenaLaneClampMargin + 6f);
		return ClampToArenaDriveBounds(position + inwardDirection.Normalized() * escapeDistance, config, config.ArenaLaneClampMargin);
	}

	private static Vector3 ClampToArenaDriveBounds(Vector3 point, ArenaAiConfig config, float margin)
	{
		var clamped = point;
		var inset = MathF.Max(0f, margin);
		var halfWidth = MathF.Max(0f, config.ArenaDriveHalfWidth - inset);
		var halfHeight = MathF.Max(0f, config.ArenaDriveHalfHeight - inset);
		clamped.X = Mathf.Clamp(clamped.X, -halfWidth, halfWidth);
		clamped.Z = Mathf.Clamp(clamped.Z, -halfHeight, halfHeight);
		return clamped;
	}

	private static float BuildSteerTowardPoint(VehiclePawn pawn, Vector3 point, float steerGain)
	{
		var toPoint = point - pawn.GlobalPosition;
		toPoint.Y = 0f;
		if (toPoint.LengthSquared() < 0.0001f)
			return 0f;

		var desiredYaw = Mathf.Atan2(toPoint.X, toPoint.Z) + Mathf.Pi;
		var headingDiff = Mathf.Wrap(desiredYaw - pawn.Rotation.Y, -Mathf.Pi, Mathf.Pi);
		return Mathf.Clamp(headingDiff * steerGain, -1f, 1f);
	}

	private static float MeasureMovementSpeed(ArenaVehicleAiRuntimeState runtimeState, Vector3 currentPosition, float dt)
	{
		if (!runtimeState.HasLastPosition || dt <= 0f)
			return 0f;

		var delta = currentPosition - runtimeState.LastPosition;
		delta.Y = 0f;
		return delta.Length() / MathF.Max(dt, 0.0001f);
	}

	private static void RememberPosition(ArenaVehicleAiRuntimeState runtimeState, Vector3 position)
	{
		runtimeState.LastPosition = position;
		runtimeState.HasLastPosition = true;
	}

	private static float SampleObstacleClearance(VehiclePawn pawn, Vector3 origin, Vector3 direction, float maxDistance)
	{
		var world = pawn.GetWorld3D();
		if (world == null)
			return maxDistance;

		direction.Y = 0f;
		if (direction.LengthSquared() < 0.0001f)
			return maxDistance;
		direction = direction.Normalized();

		var query = PhysicsRayQueryParameters3D.Create(origin, origin + direction * maxDistance);
		query.CollideWithBodies = true;
		query.CollideWithAreas = false;
		query.CollisionMask = uint.MaxValue;
		query.Exclude = new Godot.Collections.Array<Rid> { pawn.GetRid() };
		var hit = world.DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
			return maxDistance;

		if (hit.TryGetValue("collider", out var colliderVar) && !IsObstacleCollider(colliderVar.AsGodotObject()))
			return maxDistance;

		if (hit.TryGetValue("position", out var posVar))
			return origin.DistanceTo(posVar.AsVector3());

		return maxDistance * 0.5f;
	}

	private static bool IsObstacleCollider(GodotObject? collider)
	{
		if (collider is not Node node)
			return true;

		Node? cur = node;
		while (cur != null)
		{
			if (cur.IsInGroup("vehicle_pawn") || cur.IsInGroup("driver_pawn") || cur.IsInGroup("player_driver"))
				return false;
			cur = cur.GetParent();
		}

		return true;
	}

	private static Vector3 GetFlatForward(Node3D node)
	{
		var forward = -node.GlobalTransform.Basis.Z;
		forward.Y = 0f;
		return forward.LengthSquared() < 0.0001f ? Vector3.Forward : forward.Normalized();
	}

	private static Vector3 GetFlatRight(Node3D node)
	{
		var right = node.GlobalTransform.Basis.X;
		right.Y = 0f;
		return right.LengthSquared() < 0.0001f ? Vector3.Right : right.Normalized();
	}

	private static Vector3 BuildSteeringPoint(
		Vector3 pawnPosition,
		Vector3 targetPosition,
		float distance,
		float preferredRange,
		ArenaVehicleAiTacticalProfile tacticalProfile,
		ArenaAiConfig config)
	{
		var radial = targetPosition - pawnPosition;
		radial.Y = 0f;
		if (radial.LengthSquared() < 0.0001f)
			return targetPosition;

		radial = radial.Normalized();
		var lateral = new Vector3(-radial.Z, 0f, radial.X) * tacticalProfile.OrbitSign;
		var holdPoint = targetPosition - radial * preferredRange;
		if (distance <= config.OrbitActivationDistance)
			holdPoint += lateral * config.OrbitOffsetDistance * tacticalProfile.OrbitScale;

		return holdPoint;
	}

	private readonly struct LanePlan
	{
		public LanePlan(Vector3 steeringPoint, DirectionalProbeSet probeSet)
		{
			SteeringPoint = steeringPoint;
			ProbeSet = probeSet;
		}

		public Vector3 SteeringPoint { get; }
		public DirectionalProbeSet ProbeSet { get; }
	}

	private readonly struct LaneCandidate
	{
		public LaneCandidate(int sign, Vector3 steeringPoint, DirectionalProbeSet probeSet, float score)
		{
			Sign = sign;
			SteeringPoint = steeringPoint;
			ProbeSet = probeSet;
			Score = score;
		}

		public int Sign { get; }
		public Vector3 SteeringPoint { get; }
		public DirectionalProbeSet ProbeSet { get; }
		public float Score { get; }
	}

	private readonly struct DirectionalProbeSet
	{
		public DirectionalProbeSet(
			float centerClear01,
			float leftClear01,
			float rightClear01,
			float farLeftClear01,
			float farRightClear01,
			float pathClear01,
			float bestSideClear01,
			int bestEscapeSign,
			bool immediateBlock,
			bool forceReverse,
			float throttleCap)
		{
			CenterClear01 = centerClear01;
			LeftClear01 = leftClear01;
			RightClear01 = rightClear01;
			FarLeftClear01 = farLeftClear01;
			FarRightClear01 = farRightClear01;
			PathClear01 = pathClear01;
			BestSideClear01 = bestSideClear01;
			BestEscapeSign = bestEscapeSign;
			ImmediateBlock = immediateBlock;
			ForceReverse = forceReverse;
			ThrottleCap = throttleCap;
		}

		public float CenterClear01 { get; }
		public float LeftClear01 { get; }
		public float RightClear01 { get; }
		public float FarLeftClear01 { get; }
		public float FarRightClear01 { get; }
		public float PathClear01 { get; }
		public float BestSideClear01 { get; }
		public int BestEscapeSign { get; }
		public bool ImmediateBlock { get; }
		public bool ForceReverse { get; }
		public float ThrottleCap { get; }
		public bool IsBlocked => ImmediateBlock || PathClear01 < 0.74f;
	}
}
