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

	// When set, this encounter is an interception run: winning returns the referenced captured
	// vehicle to the player's fleet (spec: reclaim your salvaged vehicle from its captors).
	public string? RecoverVehicleInstanceId { get; init; } = null;

	// Tournament linkage: this fight is round (TournamentRoundIndex+1) of the save's ActiveTournament.
	public bool IsTournamentRound { get; init; } = false;
	public int TournamentRoundIndex { get; init; } = -1;

	// Overworld scavenge site (spec: abandoned structures containing salvage): a pre-resolved
	// salvage-only encounter — no fight, just a derelict to strip or tow out of the yard.
	public bool IsScavengeSite { get; init; } = false;

	// Highway raider ambush rolled during travel. Wins pay a cut-down purse and no ammo — raiders
	// don't hand out arena purses; the wreck salvage is the real upside. This keeps the freight
	// ambush-chance bonus a RISK instead of an income multiplier.
	public bool IsRoadAmbush { get; init; } = false;

	// Rival salvage-crew raid (spec: AI crews roam and salvage like players): a real fight
	// against a crew hauling a prize wreck on its tow line — win and the haul is yours.
	public bool IsSalvageCrewRaid { get; init; } = false;

	// WANTED-board bounty hunt: a named raider forced the fight on their haunted road leg.
	// Winning pays BountyRewardUsd on top of the (ambush-cut) purse and clears the contract;
	// losing/fleeing leaves the bounty active — the target stays at large.
	public bool IsBountyHunt { get; init; } = false;
	public string? BountyTargetName { get; init; } = null;
	public int BountyRewardUsd { get; init; } = 0;

	// Battlefield salvage / towing / hijacking summary (v1 prototype).
	public int BattlefieldScrapRecovered { get; init; } = 0;
	public bool TowedVehicleRecovered { get; init; } = false;
	public string? TowedVehicleDisplayName { get; init; } = null;
	public bool HijackedVehicleRecovered { get; init; } = false;
	public string? HijackedVehicleDisplayName { get; init; } = null;
}
