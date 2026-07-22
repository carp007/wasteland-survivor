// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/State/TournamentState.cs
// Purpose: Persisted state for an in-progress arena tournament (spec: AutoDuel-style tournaments).
// -------------------------------------------------------------------------------------------------
using System;

namespace WastelandSurvivor.Core.State;

/// <summary>
/// An active arena tournament: a multi-round gauntlet at escalating tiers in one city's arena.
/// The player pays entry once, fights every round back-to-back (pit crew patches between rounds,
/// no full garage access), and a champion bonus lands on top of the per-round purses.
/// Losing or fleeing any round eliminates the player (winnings earned so far are kept).
/// </summary>
public sealed record TournamentState
{
    public string TournamentId { get; init; } = Guid.NewGuid().ToString("N");
    public string CityId { get; init; } = "";

    /// <summary>Opponent tier per round, in fight order (e.g. [1,2,3]).</summary>
    public int[] RoundTiers { get; init; } = Array.Empty<int>();

    /// <summary>Index into RoundTiers of the round currently being fought (or fought next).</summary>
    public int CurrentRound { get; init; } = 0;

    public int EntryFeeUsd { get; init; } = 0;

    /// <summary>Accumulated round purses so far (display; already applied to the wallet).</summary>
    public int WinningsUsd { get; init; } = 0;

    public DateTime StartedUtc { get; init; } = DateTime.UtcNow;
}
