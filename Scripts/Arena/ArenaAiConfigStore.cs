// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/ArenaAiConfigStore.cs
// Purpose: Runtime-tunable arena AI driving / firing configuration.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using WastelandSurvivor.Core.IO;

namespace WastelandSurvivor.Game.Arena;

public sealed class ArenaAiConfigStore : JsonConfigStore<ArenaAiConfig>
{
	public static ArenaAiConfigStore Instance { get; } = new();

	private const string ArenaAiConfigPath = "res://Data/Config/arena_ai.json";

	// Merged per-tier configs are cached against the base instance so Reload()/ClearCache()
	// (which swap the base) invalidates them automatically.
	private ArenaAiConfig? _tierCacheBase;
	private readonly Dictionary<int, ArenaAiConfig> _tierCache = new();

	private ArenaAiConfigStore() : base(ArenaAiConfigPath)
	{
	}

	protected override ArenaAiConfig CreateDefault() => new();

	/// <summary>
	/// Base config with the tier's personality overrides applied. Tiers without an entry in the
	/// config's "tiers" map (or configs with no map at all) get the base config unchanged.
	/// </summary>
	public ArenaAiConfig GetForTier(int tier)
	{
		var baseConfig = Get();
		if (!ReferenceEquals(_tierCacheBase, baseConfig))
		{
			_tierCache.Clear();
			_tierCacheBase = baseConfig;
		}

		if (_tierCache.TryGetValue(tier, out var cached))
			return cached;

		var merged = BuildTierConfig(baseConfig, tier);
		_tierCache[tier] = merged;
		return merged;
	}

	private ArenaAiConfig BuildTierConfig(ArenaAiConfig baseConfig, int tier)
	{
		var overrides = FindTierOverrides(baseConfig.Tiers, tier);
		if (overrides == null)
			return baseConfig;

		var merged = baseConfig.CloneWithoutTiers();
		overrides.ApplyTo(merged);
		return Normalize(merged);
	}

	private static ArenaAiTierOverrides? FindTierOverrides(Dictionary<string, ArenaAiTierOverrides>? tiers, int tier)
	{
		if (tiers == null || tiers.Count == 0)
			return null;

		return tiers.TryGetValue(tier.ToString(CultureInfo.InvariantCulture), out var overrides) ? overrides : null;
	}

	protected override ArenaAiConfig Normalize(ArenaAiConfig config)
	{
		config.PreferredRange = Mathf.Max(2f, config.PreferredRange);
		config.RangeTolerance = Mathf.Max(0.5f, config.RangeTolerance);
		config.VehicleLeadSeconds = Mathf.Clamp(config.VehicleLeadSeconds, 0f, 2f);
		config.DriverLeadSeconds = Mathf.Clamp(config.DriverLeadSeconds, 0f, 2f);
		config.SteerGain = Mathf.Clamp(config.SteerGain, 0.1f, 5f);
		config.SteerDamping = Mathf.Clamp(config.SteerDamping, 0f, 2f);
		config.SteerSlewPerSecond = Mathf.Clamp(config.SteerSlewPerSecond, 0f, 40f);
		config.TacticalHoldSeconds = Mathf.Clamp(config.TacticalHoldSeconds, 0f, 5f);
		config.DetourClearanceMeters = Mathf.Clamp(config.DetourClearanceMeters, 0f, 10f);
		config.DetourScanMeters = Mathf.Clamp(config.DetourScanMeters, 5f, 80f);
		config.DetourCommitSeconds = Mathf.Clamp(config.DetourCommitSeconds, 0f, 4f);
		config.CoverSeekMaxMeters = Mathf.Clamp(config.CoverSeekMaxMeters, 0f, 80f);
		config.CoverStandoffMeters = Mathf.Clamp(config.CoverStandoffMeters, 1f, 10f);
		config.LosRepositionRangeFactor = Mathf.Clamp(config.LosRepositionRangeFactor, 0f, 3f);
		config.FacingAwayHeadingRad = Mathf.Clamp(config.FacingAwayHeadingRad, 0.2f, Mathf.Pi);
		config.WideTurnHeadingRad = Mathf.Clamp(config.WideTurnHeadingRad, 0.1f, config.FacingAwayHeadingRad);
		config.MinAlignmentThrottleFactor = Mathf.Clamp(config.MinAlignmentThrottleFactor, 0f, 1f);
		config.UnstuckDuration = Mathf.Clamp(config.UnstuckDuration, 0f, 3f);
		config.UnstuckThrottle = Mathf.Clamp(config.UnstuckThrottle, -1f, 1f);
		config.UnstuckSteer = Mathf.Clamp(config.UnstuckSteer, -1f, 1f);
		config.StuckSpeedThreshold = Mathf.Clamp(config.StuckSpeedThreshold, 0f, 12f);
		config.StuckMinProgressSpeed = Mathf.Clamp(config.StuckMinProgressSpeed, 0f, 12f);
		config.StuckTriggerSeconds = Mathf.Clamp(config.StuckTriggerSeconds, 0.05f, 5f);
		config.StuckEffortThrottle = Mathf.Clamp(config.StuckEffortThrottle, 0f, 1f);
		config.StuckEffortSteer = Mathf.Clamp(config.StuckEffortSteer, 0f, 1f);
		config.RecoveryChainWindowSeconds = Mathf.Clamp(config.RecoveryChainWindowSeconds, 0f, 6f);
		config.RecoveryAttemptExtraSeconds = Mathf.Clamp(config.RecoveryAttemptExtraSeconds, 0f, 2f);
		config.MaxRecoveryAttempts = Math.Max(1, config.MaxRecoveryAttempts);
		config.RecoveryReversePhaseRatio = Mathf.Clamp(config.RecoveryReversePhaseRatio, 0.1f, 0.95f);
		config.RecoveryLaneCommitSeconds = Mathf.Clamp(config.RecoveryLaneCommitSeconds, 0f, 6f);
		config.RecoveryReverseDistance = Mathf.Clamp(config.RecoveryReverseDistance, 0.5f, 20f);
		config.RecoveryReverseLateralDistance = Mathf.Clamp(config.RecoveryReverseLateralDistance, 0f, 20f);
		config.RecoveryForwardDistance = Mathf.Clamp(config.RecoveryForwardDistance, 0.5f, 24f);
		config.RecoveryForwardLateralDistance = Mathf.Clamp(config.RecoveryForwardLateralDistance, 0f, 20f);
		config.RecoveryForwardThrottle = Mathf.Clamp(config.RecoveryForwardThrottle, -1f, 1f);
		config.LaneOffsetDistanceFactor = Mathf.Clamp(config.LaneOffsetDistanceFactor, 0f, 1f);
		config.LaneOffsetMinMeters = Mathf.Clamp(config.LaneOffsetMinMeters, 0f, 24f);
		config.LaneOffsetMaxMeters = Mathf.Max(config.LaneOffsetMinMeters, config.LaneOffsetMaxMeters);
		config.LaneCommitSeconds = Mathf.Clamp(config.LaneCommitSeconds, 0f, 6f);
		config.ArenaDriveHalfWidth = Mathf.Clamp(config.ArenaDriveHalfWidth, 12f, 96f);
		config.ArenaDriveHalfHeight = Mathf.Clamp(config.ArenaDriveHalfHeight, 12f, 128f);
		config.ArenaWallHardMargin = Mathf.Clamp(config.ArenaWallHardMargin, 0f, 24f);
		config.ArenaWallSoftMargin = Mathf.Clamp(config.ArenaWallSoftMargin, config.ArenaWallHardMargin + 0.1f, 48f);
		config.ArenaWallBiasMeters = Mathf.Clamp(config.ArenaWallBiasMeters, 0f, 48f);
		config.ArenaWallTargetBiasFactor = Mathf.Clamp(config.ArenaWallTargetBiasFactor, 0f, 1f);
		config.ArenaLaneClampMargin = Mathf.Clamp(config.ArenaLaneClampMargin, 0f, 24f);
		config.ArenaWallEscapePressure = Mathf.Clamp(config.ArenaWallEscapePressure, 0f, 1f);
		config.ArenaWallSteerBlend = Mathf.Clamp(config.ArenaWallSteerBlend, 0f, 1f);
		config.ArenaWallBrakeDotThreshold = Mathf.Clamp(config.ArenaWallBrakeDotThreshold, -1f, 1f);
		config.ArenaWallReverseDotThreshold = Mathf.Clamp(config.ArenaWallReverseDotThreshold, -1f, config.ArenaWallBrakeDotThreshold);
		config.ArenaWallThrottleSoftCap = Mathf.Clamp(config.ArenaWallThrottleSoftCap, -1f, 1f);
		config.ArenaWallThrottleHardCap = Mathf.Clamp(config.ArenaWallThrottleHardCap, -1f, config.ArenaWallThrottleSoftCap);
		config.FeelerMinDistance = Mathf.Clamp(config.FeelerMinDistance, 1f, 24f);
		config.FeelerMaxDistance = Mathf.Max(config.FeelerMinDistance, config.FeelerMaxDistance);
		config.FeelerDistancePerSpeed = Mathf.Clamp(config.FeelerDistancePerSpeed, 0f, 3f);
		config.ImmediateBlockClearance01 = Mathf.Clamp(config.ImmediateBlockClearance01, 0f, 1f);
		config.ReverseBlockClearance01 = Mathf.Clamp(config.ReverseBlockClearance01, 0f, config.ImmediateBlockClearance01);
		config.ReverseSideClearance01 = Mathf.Clamp(config.ReverseSideClearance01, 0f, 1f);
		config.BlockedThrottleCap = Mathf.Clamp(config.BlockedThrottleCap, -1f, 1f);
		config.OpenThrottleCap = Mathf.Clamp(config.OpenThrottleCap, -1f, 1f);
		config.OrbitActivationDistance = Mathf.Max(config.PreferredRange + 1f, config.OrbitActivationDistance);
		config.OrbitOffsetDistance = Mathf.Clamp(config.OrbitOffsetDistance, 0f, 24f);
		config.BrakeTurnHeadingRad = Mathf.Clamp(config.BrakeTurnHeadingRad, 0.1f, Mathf.Pi);
		config.BrakeTurnThrottle = Mathf.Clamp(config.BrakeTurnThrottle, -1f, 1f);
		config.WeaponFireRange = Mathf.Max(1f, config.WeaponFireRange);
		config.CloseFireDistance = Mathf.Clamp(config.CloseFireDistance, 0f, config.WeaponFireRange);
		config.CloseFireChance = Mathf.Clamp(config.CloseFireChance, 0f, 1f);
		config.FarFireChance = Mathf.Clamp(config.FarFireChance, 0f, 1f);
		config.MinFireDot = Mathf.Clamp(config.MinFireDot, -1f, 1f);
		config.AlignmentFireBonus = Mathf.Clamp(config.AlignmentFireBonus, 0f, 1f);
		config.MineDropDistanceMin = Mathf.Clamp(config.MineDropDistanceMin, 0f, 24f);
		config.MineDropDistanceMax = Mathf.Clamp(config.MineDropDistanceMax, config.MineDropDistanceMin, 48f);
		config.MineDropRearDotMax = Mathf.Clamp(config.MineDropRearDotMax, -1f, 1f);
		config.MineDropMinSpeed = Mathf.Clamp(config.MineDropMinSpeed, 0f, 24f);
		config.MineDropCooldownSeconds = Mathf.Clamp(config.MineDropCooldownSeconds, 0f, 12f);
		config.MineDropBaseChance = Mathf.Clamp(config.MineDropBaseChance, 0f, 1f);
		config.MineDropRecoveryChanceBonus = Mathf.Clamp(config.MineDropRecoveryChanceBonus, 0f, 1f);
		config.MineDropWallPressureChanceBonus = Mathf.Clamp(config.MineDropWallPressureChanceBonus, 0f, 1f);
		config.LowHpRetreatThreshold01 = Mathf.Clamp(config.LowHpRetreatThreshold01, 0f, 1f);
		config.LowHpRetreatRangeScale = Mathf.Clamp(config.LowHpRetreatRangeScale, 1f, 4f);
		config.DisengageTriggerSeconds = Mathf.Clamp(config.DisengageTriggerSeconds, 0f, 30f);
		config.DisengageDistance = Mathf.Clamp(config.DisengageDistance, 0f, 200f);
		config.FireWindowSeconds = Mathf.Clamp(config.FireWindowSeconds, 0f, 10f);
		config.FirePauseSeconds = Mathf.Clamp(config.FirePauseSeconds, 0f, 10f);
		config.OpportunismTurnRateRadPerSec = Mathf.Clamp(config.OpportunismTurnRateRadPerSec, 0f, 12f);
		config.OpportunismWindowSeconds = Mathf.Clamp(config.OpportunismWindowSeconds, 0f, 6f);
		config.OpportunismFireChanceBonus = Mathf.Clamp(config.OpportunismFireChanceBonus, 0f, 1f);
		config.OpportunismMissileBias = Mathf.Clamp(config.OpportunismMissileBias, 0f, 200f);
		config.RamCommitDistance = Mathf.Clamp(config.RamCommitDistance, 0f, 60f);
		config.AttackRunIntervalSeconds = Mathf.Clamp(config.AttackRunIntervalSeconds, 0f, 30f);
		config.AttackRunDurationSeconds = Mathf.Clamp(config.AttackRunDurationSeconds, 0.5f, 8f);
		config.ContactBreakawayTriggerSeconds = Mathf.Clamp(config.ContactBreakawayTriggerSeconds, 0f, 10f);
		config.ContactBreakawayDurationSeconds = Mathf.Clamp(config.ContactBreakawayDurationSeconds, 0.2f, 6f);
		config.ContactBreakawayDistance = Mathf.Clamp(config.ContactBreakawayDistance, 0f, 24f);
		config.ContactBreakawayMinSpeedKmh = Mathf.Clamp(config.ContactBreakawayMinSpeedKmh, 0f, 60f);
		config.RamThrottle = Mathf.Clamp(config.RamThrottle, 0f, 1f);
		config.MineDropLowHpThreshold01 = Mathf.Clamp(config.MineDropLowHpThreshold01, 0f, 1f);
		config.MineDropLowHpChanceBonus = Mathf.Clamp(config.MineDropLowHpChanceBonus, 0f, 1f);
		return config;
	}
}

public sealed class ArenaAiConfig
{
	public float PreferredRange { get; set; } = 22f;
	public float RangeTolerance { get; set; } = 7f;

	public float VehicleLeadSeconds { get; set; } = 0.35f;
	public float DriverLeadSeconds { get; set; } = 0.15f;
	public float SteerGain { get; set; } = 1.05f;

	// --- Smooth-driving layer (loop #6): PD steering + wheel slew + planned detours + hysteresis.
	// Zeros disable each piece individually (legacy bang-bang behavior).
	/// <summary>Derivative gain on heading error; damps overshoot so pursuit stops tail-wagging.</summary>
	public float SteerDamping { get; set; } = 0.24f;
	/// <summary>Max steer change per second (0 = uncapped). Converts sign-flip jitter into arcs.</summary>
	public float SteerSlewPerSecond { get; set; } = 7f;
	/// <summary>Minimum seconds between tactical-stance range flips (orbit vs pursuit hysteresis).</summary>
	public float TacticalHoldSeconds { get; set; } = 0.7f;
	/// <summary>Lateral clearance kept when planning a detour around a known obstacle footprint.</summary>
	public float DetourClearanceMeters { get; set; } = 2.6f;
	/// <summary>How far along the pursuit line obstacle footprints are considered for detours.</summary>
	public float DetourScanMeters { get; set; } = 34f;
	/// <summary>Seconds a chosen detour side stays latched (prevents S-wiggle side flip-flop).</summary>
	public float DetourCommitSeconds { get; set; } = 0.9f;
	/// <summary>Max distance to a cover obstacle considered when disengaging (0 = no cover-seek).</summary>
	public float CoverSeekMaxMeters { get; set; } = 30f;
	/// <summary>How far beyond the cover obstacle's edge the hide point sits.</summary>
	public float CoverStandoffMeters { get; set; } = 3.4f;
	/// <summary>Inside WeaponFireRange x this factor, a cover-blocked sight line drops the stance
	/// and pursues around the blocker to re-open the guns (0 = disabled).</summary>
	public float LosRepositionRangeFactor { get; set; } = 1.15f;

	public float FacingAwayHeadingRad { get; set; } = 2.05f;
	public float WideTurnHeadingRad { get; set; } = 1.35f;

	public float ReverseRecoverThrottle { get; set; } = -0.70f;
	public float WideTurnThrottle { get; set; } = 0.18f;
	public float FarThrottle { get; set; } = 1.00f;
	public float NearThrottle { get; set; } = -0.45f;
	public float CruiseThrottle { get; set; } = 0.70f;
	public float MinAlignmentThrottleFactor { get; set; } = 0.30f;

	public float UnstuckDuration { get; set; } = 0.55f;
	public float UnstuckThrottle { get; set; } = -0.55f;
	public float UnstuckSteer { get; set; } = 0.85f;
	public float StuckSpeedThreshold { get; set; } = 1.55f;
	public float StuckMinProgressSpeed { get; set; } = 1.35f;
	public float StuckTriggerSeconds { get; set; } = 0.65f;
	public float StuckEffortThrottle { get; set; } = 0.48f;
	public float StuckEffortSteer { get; set; } = 0.42f;
	public float RecoveryChainWindowSeconds { get; set; } = 1.8f;
	public float RecoveryAttemptExtraSeconds { get; set; } = 0.18f;
	public int MaxRecoveryAttempts { get; set; } = 4;
	public float RecoveryReversePhaseRatio { get; set; } = 0.58f;
	public float RecoveryLaneCommitSeconds { get; set; } = 1.25f;
	public float RecoveryReverseDistance { get; set; } = 4.2f;
	public float RecoveryReverseLateralDistance { get; set; } = 3.8f;
	public float RecoveryForwardDistance { get; set; } = 6.2f;
	public float RecoveryForwardLateralDistance { get; set; } = 4.5f;
	public float RecoveryForwardThrottle { get; set; } = 0.72f;
	public float LaneOffsetDistanceFactor { get; set; } = 0.18f;
	public float LaneOffsetMinMeters { get; set; } = 5.5f;
	public float LaneOffsetMaxMeters { get; set; } = 11.5f;
	public float LaneCommitSeconds { get; set; } = 0.95f;
	public float ArenaDriveHalfWidth { get; set; } = 52f;
	public float ArenaDriveHalfHeight { get; set; } = 78f;
	public float ArenaWallHardMargin { get; set; } = 2.8f;
	public float ArenaWallSoftMargin { get; set; } = 9.5f;
	public float ArenaWallBiasMeters { get; set; } = 15f;
	public float ArenaWallTargetBiasFactor { get; set; } = 0.35f;
	public float ArenaLaneClampMargin { get; set; } = 6f;
	public float ArenaWallEscapePressure { get; set; } = 0.56f;
	public float ArenaWallSteerBlend { get; set; } = 0.88f;
	public float ArenaWallBrakeDotThreshold { get; set; } = -0.10f;
	public float ArenaWallReverseDotThreshold { get; set; } = -0.42f;
	public float ArenaWallThrottleSoftCap { get; set; } = 0.42f;
	public float ArenaWallThrottleHardCap { get; set; } = 0.18f;
	public float FeelerMinDistance { get; set; } = 5.5f;
	public float FeelerMaxDistance { get; set; } = 11.5f;
	public float FeelerDistancePerSpeed { get; set; } = 0.52f;
	public float ImmediateBlockClearance01 { get; set; } = 0.46f;
	public float ReverseBlockClearance01 { get; set; } = 0.24f;
	public float ReverseSideClearance01 { get; set; } = 0.34f;
	public float BlockedThrottleCap { get; set; } = 0.24f;
	public float OpenThrottleCap { get; set; } = 0.65f;

	public float OrbitActivationDistance { get; set; } = 34f;
	public float OrbitOffsetDistance { get; set; } = 9f;
	public float BrakeTurnHeadingRad { get; set; } = 0.85f;
	public float BrakeTurnThrottle { get; set; } = 0.10f;

	public float WeaponFireRange { get; set; } = 55f;
	public float CloseFireDistance { get; set; } = 26f;
	public float CloseFireChance { get; set; } = 0.65f;
	public float FarFireChance { get; set; } = 0.45f;
	public float MinFireDot { get; set; } = 0.62f;
	public float AlignmentFireBonus { get; set; } = 0.22f;

	public float MineDropDistanceMin { get; set; } = 4.5f;
	public float MineDropDistanceMax { get; set; } = 13.5f;
	public float MineDropRearDotMax { get; set; } = -0.22f;
	public float MineDropMinSpeed { get; set; } = 4.0f;
	public float MineDropCooldownSeconds { get; set; } = 2.4f;
	public float MineDropBaseChance { get; set; } = 0.72f;
	public float MineDropRecoveryChanceBonus { get; set; } = 0.18f;
	public float MineDropWallPressureChanceBonus { get; set; } = 0.14f;

	// --- Personality hooks. Defaults disable each hook so configs without tier overrides
	// --- (and tiers with no entry) drive exactly like the pre-personality AI.
	public float LowHpRetreatThreshold01 { get; set; } = 0f;
	public float LowHpRetreatRangeScale { get; set; } = 1.6f;
	public float DisengageTriggerSeconds { get; set; } = 0f;
	public float DisengageDistance { get; set; } = 0f;
	public float FireWindowSeconds { get; set; } = 0f;
	public float FirePauseSeconds { get; set; } = 0f;
	public float OpportunismTurnRateRadPerSec { get; set; } = 0f;
	public float OpportunismWindowSeconds { get; set; } = 0f;
	public float OpportunismFireChanceBonus { get; set; } = 0f;
	public float OpportunismMissileBias { get; set; } = 0f;
	public float RamCommitDistance { get; set; } = 0f;
	public float RamThrottle { get; set; } = 1f;
	public float MineDropLowHpThreshold01 { get; set; } = 0f;
	public float MineDropLowHpChanceBonus { get; set; } = 0f;

	// Attack-run cadence (0 = disabled): kite/orbit stances park fixed front mounts out of bearing
	// permanently — measured tier-2/4/5 enemies fired 4-5 shots per fight, all during the opening
	// joust (gameplay judge round 6 P0). Every AttackRunIntervalSeconds of stance driving, the AI
	// commits to a nose-on pass for AttackRunDurationSeconds so its guns sweep across the target.
	public float AttackRunIntervalSeconds { get; set; } = 0f;
	public float AttackRunDurationSeconds { get; set; } = 2.0f;

	// Bumper-contact breakaway (trigger 0 = disabled): tier-3 probes collapsed into a multi-minute
	// 8 km/h nose-grind and tier-5 rammers pinned the player nose-to-nose for long stretches
	// (gameplay judge round 10 #4). When the AI sits in near-contact with its target at crawl
	// speed past the trigger time, it commits to a reverse-arc breakaway — back off, wheel the
	// nose away — for the duration, then resumes its normal stance (which re-opens gun/missile
	// geometry for a fresh pass).
	// Crawl gate note: the judge-cited grind sits at ~8 km/h. 12 km/h also swallowed tier-5's
	// legitimate close-brawl passes (A/B probe: 6-7 enemy shots vs 13-14), so the gate ships at 10.
	public float ContactBreakawayTriggerSeconds { get; set; } = 0f;
	public float ContactBreakawayDurationSeconds { get; set; } = 1.3f;
	public float ContactBreakawayDistance { get; set; } = 4.5f;
	public float ContactBreakawayMinSpeedKmh { get; set; } = 10f;

	// Optional per-tier personality overrides keyed by tier number ("1".."5"). Null on old configs.
	public Dictionary<string, ArenaAiTierOverrides>? Tiers { get; set; }

	public ArenaAiConfig CloneWithoutTiers()
	{
		var clone = (ArenaAiConfig)MemberwiseClone();
		clone.Tiers = null;
		return clone;
	}
}

/// <summary>
/// Sparse per-tier knob overrides: null members inherit the base config value, so a tier only
/// states what its personality changes.
/// </summary>
public sealed class ArenaAiTierOverrides
{
	public float? PreferredRange { get; set; }
	public float? RangeTolerance { get; set; }
	public float? SteerGain { get; set; }
	public float? FarThrottle { get; set; }
	public float? NearThrottle { get; set; }
	public float? CruiseThrottle { get; set; }
	public float? OrbitActivationDistance { get; set; }
	public float? OrbitOffsetDistance { get; set; }
	public float? LaneOffsetMinMeters { get; set; }
	public float? LaneOffsetMaxMeters { get; set; }
	public float? WeaponFireRange { get; set; }
	public float? CloseFireDistance { get; set; }
	public float? CloseFireChance { get; set; }
	public float? FarFireChance { get; set; }
	public float? MinFireDot { get; set; }
	public float? AlignmentFireBonus { get; set; }
	public float? MineDropDistanceMin { get; set; }
	public float? MineDropDistanceMax { get; set; }
	public float? MineDropRearDotMax { get; set; }
	public float? MineDropMinSpeed { get; set; }
	public float? MineDropCooldownSeconds { get; set; }
	public float? MineDropBaseChance { get; set; }
	public float? MineDropRecoveryChanceBonus { get; set; }
	public float? MineDropWallPressureChanceBonus { get; set; }
	public float? LowHpRetreatThreshold01 { get; set; }
	public float? LowHpRetreatRangeScale { get; set; }
	public float? DisengageTriggerSeconds { get; set; }
	public float? DisengageDistance { get; set; }
	public float? FireWindowSeconds { get; set; }
	public float? FirePauseSeconds { get; set; }
	public float? OpportunismTurnRateRadPerSec { get; set; }
	public float? OpportunismWindowSeconds { get; set; }
	public float? OpportunismFireChanceBonus { get; set; }
	public float? OpportunismMissileBias { get; set; }
	public float? RamCommitDistance { get; set; }
	public float? RamThrottle { get; set; }
	public float? MineDropLowHpThreshold01 { get; set; }
	public float? MineDropLowHpChanceBonus { get; set; }
	public float? AttackRunIntervalSeconds { get; set; }
	public float? AttackRunDurationSeconds { get; set; }
	public float? ContactBreakawayTriggerSeconds { get; set; }
	public float? ContactBreakawayDurationSeconds { get; set; }
	public float? ContactBreakawayDistance { get; set; }
	public float? ContactBreakawayMinSpeedKmh { get; set; }

	public void ApplyTo(ArenaAiConfig config)
	{
		if (PreferredRange.HasValue) config.PreferredRange = PreferredRange.Value;
		if (RangeTolerance.HasValue) config.RangeTolerance = RangeTolerance.Value;
		if (SteerGain.HasValue) config.SteerGain = SteerGain.Value;
		if (FarThrottle.HasValue) config.FarThrottle = FarThrottle.Value;
		if (NearThrottle.HasValue) config.NearThrottle = NearThrottle.Value;
		if (CruiseThrottle.HasValue) config.CruiseThrottle = CruiseThrottle.Value;
		if (OrbitActivationDistance.HasValue) config.OrbitActivationDistance = OrbitActivationDistance.Value;
		if (OrbitOffsetDistance.HasValue) config.OrbitOffsetDistance = OrbitOffsetDistance.Value;
		if (LaneOffsetMinMeters.HasValue) config.LaneOffsetMinMeters = LaneOffsetMinMeters.Value;
		if (LaneOffsetMaxMeters.HasValue) config.LaneOffsetMaxMeters = LaneOffsetMaxMeters.Value;
		if (WeaponFireRange.HasValue) config.WeaponFireRange = WeaponFireRange.Value;
		if (CloseFireDistance.HasValue) config.CloseFireDistance = CloseFireDistance.Value;
		if (CloseFireChance.HasValue) config.CloseFireChance = CloseFireChance.Value;
		if (FarFireChance.HasValue) config.FarFireChance = FarFireChance.Value;
		if (MinFireDot.HasValue) config.MinFireDot = MinFireDot.Value;
		if (AlignmentFireBonus.HasValue) config.AlignmentFireBonus = AlignmentFireBonus.Value;
		if (MineDropDistanceMin.HasValue) config.MineDropDistanceMin = MineDropDistanceMin.Value;
		if (MineDropDistanceMax.HasValue) config.MineDropDistanceMax = MineDropDistanceMax.Value;
		if (MineDropRearDotMax.HasValue) config.MineDropRearDotMax = MineDropRearDotMax.Value;
		if (MineDropMinSpeed.HasValue) config.MineDropMinSpeed = MineDropMinSpeed.Value;
		if (MineDropCooldownSeconds.HasValue) config.MineDropCooldownSeconds = MineDropCooldownSeconds.Value;
		if (MineDropBaseChance.HasValue) config.MineDropBaseChance = MineDropBaseChance.Value;
		if (MineDropRecoveryChanceBonus.HasValue) config.MineDropRecoveryChanceBonus = MineDropRecoveryChanceBonus.Value;
		if (MineDropWallPressureChanceBonus.HasValue) config.MineDropWallPressureChanceBonus = MineDropWallPressureChanceBonus.Value;
		if (LowHpRetreatThreshold01.HasValue) config.LowHpRetreatThreshold01 = LowHpRetreatThreshold01.Value;
		if (LowHpRetreatRangeScale.HasValue) config.LowHpRetreatRangeScale = LowHpRetreatRangeScale.Value;
		if (DisengageTriggerSeconds.HasValue) config.DisengageTriggerSeconds = DisengageTriggerSeconds.Value;
		if (DisengageDistance.HasValue) config.DisengageDistance = DisengageDistance.Value;
		if (FireWindowSeconds.HasValue) config.FireWindowSeconds = FireWindowSeconds.Value;
		if (FirePauseSeconds.HasValue) config.FirePauseSeconds = FirePauseSeconds.Value;
		if (OpportunismTurnRateRadPerSec.HasValue) config.OpportunismTurnRateRadPerSec = OpportunismTurnRateRadPerSec.Value;
		if (OpportunismWindowSeconds.HasValue) config.OpportunismWindowSeconds = OpportunismWindowSeconds.Value;
		if (OpportunismFireChanceBonus.HasValue) config.OpportunismFireChanceBonus = OpportunismFireChanceBonus.Value;
		if (OpportunismMissileBias.HasValue) config.OpportunismMissileBias = OpportunismMissileBias.Value;
		if (RamCommitDistance.HasValue) config.RamCommitDistance = RamCommitDistance.Value;
		if (RamThrottle.HasValue) config.RamThrottle = RamThrottle.Value;
		if (MineDropLowHpThreshold01.HasValue) config.MineDropLowHpThreshold01 = MineDropLowHpThreshold01.Value;
		if (MineDropLowHpChanceBonus.HasValue) config.MineDropLowHpChanceBonus = MineDropLowHpChanceBonus.Value;
		if (AttackRunIntervalSeconds.HasValue) config.AttackRunIntervalSeconds = AttackRunIntervalSeconds.Value;
		if (AttackRunDurationSeconds.HasValue) config.AttackRunDurationSeconds = AttackRunDurationSeconds.Value;
		if (ContactBreakawayTriggerSeconds.HasValue) config.ContactBreakawayTriggerSeconds = ContactBreakawayTriggerSeconds.Value;
		if (ContactBreakawayDurationSeconds.HasValue) config.ContactBreakawayDurationSeconds = ContactBreakawayDurationSeconds.Value;
		if (ContactBreakawayDistance.HasValue) config.ContactBreakawayDistance = ContactBreakawayDistance.Value;
		if (ContactBreakawayMinSpeedKmh.HasValue) config.ContactBreakawayMinSpeedKmh = ContactBreakawayMinSpeedKmh.Value;
	}
}
