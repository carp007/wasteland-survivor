// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/State/EncounterState.cs
// Purpose: Serializable runtime/persisted state records (SaveGameState and related sub-records).
// -------------------------------------------------------------------------------------------------
using System;

namespace WastelandSurvivor.Core.State;

public sealed record RewardAmmoState
{
	public string AmmoId { get; init; } = "";
	public int Count { get; init; } = 0;
}

/// <summary>
/// Persisted encounter state for the current/last encounter (tier, outcome, rewards, runtime log).
/// </summary>
public sealed record EncounterState
{
	public string EncounterId { get; init; } = Guid.NewGuid().ToString("N");
	public string CityId { get; init; } = "detroit";
	public int Tier { get; init; } = 1;
	public string VehicleInstanceId { get; init; } = "";

	// Lightweight combat stats for the arena prototype.
	public int PlayerHp { get; init; } = 100;
	public int EnemyHp { get; init; } = 100;

	// Simple 1D distance band for the arena prototype.
	// 0 = point-blank, larger numbers = farther away.
	public int Distance { get; init; } = 3;

	public int Turn { get; init; } = 0;

	// Small, append-only combat log for UI/debugging.
	// We keep this as a string array for save compatibility and serializer simplicity.
	public string[] CombatLog { get; init; } = Array.Empty<string>();
	public DateTime StartedUtc { get; init; } = DateTime.UtcNow;
	public DateTime? EndedUtc { get; init; }
	public string? Outcome { get; init; }

	public int MoneyRewardUsd { get; init; } = 0;

	// Prototype rewards (already applied to the player/vehicle when the encounter resolves).
	public int ScrapReward { get; init; } = 0;
	public RewardAmmoState[] AmmoRewards { get; init; } = Array.Empty<RewardAmmoState>();
}
