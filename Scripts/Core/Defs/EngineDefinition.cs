// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/Defs/EngineDefinition.cs
// Purpose: Data definition model loaded from JSON under Data/Defs (stable, Godot-independent).
// -------------------------------------------------------------------------------------------------
namespace WastelandSurvivor.Core.Defs;

/// <summary>
/// Definition for a vehicle engine (loaded from Data/Defs/Engines). Used to compute archetype/audio and (later) performance constraints.
/// </summary>
public sealed record EngineDefinition : IHasId
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";

    public FuelType FuelType { get; init; } = FuelType.Gas;

    public float PowerKw { get; init; } = 120f;
    public float? TorqueNm { get; init; } = null;
    public float Efficiency { get; init; } = 1.0f;

    public VehicleClass[] AllowedVehicleClasses { get; init; } = System.Array.Empty<VehicleClass>();
}
