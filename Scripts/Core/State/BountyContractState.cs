// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/State/BountyContractState.cs
// Purpose: Serializable runtime/persisted state records (SaveGameState and related sub-records).
// -------------------------------------------------------------------------------------------------

namespace WastelandSurvivor.Core.State;

/// <summary>
/// Active WANTED-board bounty (spec: expandable quest systems + "AI should feel like players" —
/// named raiders haunt specific road legs). Accepted at an arena city's Ops board; traveling the
/// named leg forces the bounty fight. Winning pays the reward and clears the contract; losing or
/// fleeing leaves the target at large (the contract stays active). One active bounty at a time (v1).
/// </summary>
public sealed record BountyContractState
{
    public string BountyId { get; init; } = "";
    public string OriginCityId { get; init; } = "";

    /// <summary>The hunted raider's handle, e.g. "MAULER KANE".</summary>
    public string TargetName { get; init; } = "";

    /// <summary>Road leg the target haunts — matched in either travel direction.</summary>
    public string RoadFromCityId { get; init; } = "";
    public string RoadToCityId { get; init; } = "";

    /// <summary>Arena-encounter tier the bounty fight spawns at.</summary>
    public int Tier { get; init; } = 1;

    public int RewardUsd { get; init; }
}
