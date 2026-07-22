// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/Defs/CityDefinition.cs
// Purpose: Data definition model loaded from JSON under Data/Defs (stable, Godot-independent).
// -------------------------------------------------------------------------------------------------
using System.Collections.Generic;

namespace WastelandSurvivor.Core.Defs;

/// <summary>
/// A road leg connecting this city to an adjacent city (overworld travel happens per-leg).
/// Roads are authored on both endpoints; the loader warns when a road is missing its mirror.
/// </summary>
public sealed record CityRoadDefinition
{
    public string ToCityId { get; init; } = "";
    public float DistanceKm { get; init; } = 100f;

    /// <summary>Chance (0..1) that traveling this leg rolls a raider ambush encounter.</summary>
    public double AmbushChance { get; init; } = 0.4;

    public int AmbushTierMin { get; init; } = 1;
    public int AmbushTierMax { get; init; } = 2;

    /// <summary>
    /// Mid-route fuel stop (master spec: side roads to gas stations). When true, travel on this leg
    /// automatically tops the tank up at the station if the vehicle would otherwise arrive low.
    /// Author it on the long legs; both mirrored road entries should agree.
    /// </summary>
    public bool HasFuelStation { get; init; } = false;

    /// <summary>Station price vs the base fuel unit price — highway stations undercut cities.</summary>
    public float FuelPriceMultiplier { get; init; } = 0.9f;

    /// <summary>Optional display name for the leg's station, used in travel log flavor.</summary>
    public string FuelStationName { get; init; } = "";

    /// <summary>
    /// "Road Closed" barrier (master spec: gates world expansion). Travel refuses closed legs; the
    /// loader warns if a closure disconnects the road graph. Both mirrored entries should agree.
    /// </summary>
    public bool Closed { get; init; } = false;

    /// <summary>Why the barrier is up — surfaced on the route card and travel errors.</summary>
    public string ClosedReason { get; init; } = "";
}

/// <summary>
/// Definition for an overworld city (loaded from Data/Defs/Cities). Cities are menu-based service
/// hubs (master spec): every city has a store + garage, some have arenas / clone facilities, and
/// roads connect cities into the travel graph.
/// </summary>
public sealed record CityDefinition : IHasId
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";

    /// <summary>Normalized overworld map position (0..1, x east / y south) for map UI.</summary>
    public float MapX { get; init; } = 0.5f;
    public float MapY { get; init; } = 0.5f;

    public bool HasStore { get; init; } = true;
    public bool HasGarage { get; init; } = true;

    /// <summary>Highest arena tier hosted here. 0 = this city has no arena.</summary>
    public int ArenaMaxTier { get; init; } = 0;

    /// <summary>Clone facilities allow memory upload / respawn anchoring (spec pillar).</summary>
    public bool HasCloneFacility { get; init; } = false;

    /// <summary>
    /// Select cities run a gambling den (master spec income sources: "later: casino in some
    /// cities"). Gates the Casino service button and the SessionWorld gamble transactions.
    /// </summary>
    public bool HasCasino { get; init; } = false;

    /// <summary>Fuel price multiplier vs the base per-unit price (local scarcity flavor).</summary>
    public float FuelPriceMultiplier { get; init; } = 1.0f;

    public string Flavor { get; init; } = "";

    /// <summary>
    /// Optional res:// path to full-screen backdrop art for this city's hub screens. When empty or
    /// the file is absent (Assets/ is optional on some machines), the UI falls back to a
    /// procedurally generated per-city skyline.
    /// </summary>
    public string BackdropPath { get; init; } = "";

    public List<CityRoadDefinition> Roads { get; init; } = new();
}
