using System;
using System.Collections.Generic;

namespace WastelandSurvivor.Core.State;

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
