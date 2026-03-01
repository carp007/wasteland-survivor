// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/State/VehicleInstanceState.cs
// Purpose: Serializable runtime/persisted state records (SaveGameState and related sub-records).
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using WastelandSurvivor.Core.Defs;

namespace WastelandSurvivor.Core.State;

public sealed record InstalledWeaponState
{
    public string WeaponId { get; init; } = "";
    public string? SelectedAmmoId { get; init; } = null;
}

public sealed record TowingState
{
    public float TowHitchCapacityKg { get; init; } = 0f;
    public List<string> AttachedTowTargetInstanceIds { get; init; } = new();
    public float TotalTowedMassKgCached { get; init; } = 0f;
}

/// <summary>
/// Persisted per-vehicle instance state (section/tire HP/AP, ammo inventory, upgrades, and installed parts).
/// </summary>
public sealed record VehicleInstanceState
{
    public string InstanceId { get; init; } = Guid.NewGuid().ToString("N");
    public string DefinitionId { get; init; } = "";

    public Dictionary<ArmorSection, int> CurrentArmorBySection { get; init; } = new();
    public int[] CurrentTireArmor { get; init; } = Array.Empty<int>();

    // Structural HP (distinct from armor points).
    public Dictionary<ArmorSection, int> CurrentHpBySection { get; init; } = new();
    public int[] CurrentTireHp { get; init; } = Array.Empty<int>();

    // Prototype upgrades: each level increases max armor / tire armor by +1.
    public int ArmorPlatingLevel { get; init; } = 0;
    public int TirePlatingLevel { get; init; } = 0;

    public float FuelAmount { get; init; } = 0f;

    // ammoId -> count
    public Dictionary<string, int> AmmoInventory { get; init; } = new();

    // itemId -> count (future)
    public Dictionary<string, int> CargoInventory { get; init; } = new();

    public string? InstalledEngineId { get; init; } = null;
    public string? InstalledComputerId { get; init; } = null;

    // mountId -> installed weapon
    public Dictionary<string, InstalledWeaponState> InstalledWeaponsByMountId { get; init; } = new();

    public TowingState Towing { get; init; } = new();
}
