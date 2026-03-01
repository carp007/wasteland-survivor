// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/State/SaveGameState.cs
// Purpose: Serializable runtime/persisted state records (SaveGameState and related sub-records).
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace WastelandSurvivor.Core.State;

/// <summary>
/// Root persisted state (saved as JSON). Versioned and migrated forward by SaveMigration.
/// </summary>
public sealed record SaveGameState
{
    public int Version { get; init; } = 1;
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public PlayerProfileState Player { get; init; } = new();

    public EncounterState? CurrentEncounter { get; init; }
    public List<VehicleInstanceState> Vehicles { get; init; } = new();

    // CityId -> list of vehicle instance ids stored there
    public Dictionary<string, List<string>> CityStorage { get; init; } = new();

    public Dictionary<string, bool> WorldFlags { get; init; } = new();
}
