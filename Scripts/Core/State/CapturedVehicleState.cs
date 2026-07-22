// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/State/CapturedVehicleState.cs
// Purpose: Serializable runtime/persisted state records (SaveGameState and related sub-records).
// -------------------------------------------------------------------------------------------------
using System;

namespace WastelandSurvivor.Core.State;

/// <summary>
/// A player vehicle salvaged/towed by the AI after a defeat (master spec: winners salvage like
/// players do). The vehicle instance stays in SaveGameState.Vehicles with its battle damage; this
/// record marks who holds it, where, and what it costs to get back. The player can pay the ransom
/// or run an interception fight to reclaim it.
/// </summary>
public sealed record CapturedVehicleState
{
    public string VehicleInstanceId { get; init; } = "";

    /// <summary>City where the captors hold the vehicle (interception happens here).</summary>
    public string CityId { get; init; } = "detroit";

    /// <summary>Arena tier of the crew that took it — sets the interception fight difficulty.</summary>
    public int CaptorTier { get; init; } = 1;

    public int RansomUsd { get; init; } = 200;

    public DateTime CapturedUtc { get; init; } = DateTime.UtcNow;
}
