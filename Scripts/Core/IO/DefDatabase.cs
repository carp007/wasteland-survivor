// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/IO/DefDatabase.cs
// Purpose: Serialization and definition loading helpers (Godot file access + System.Text.Json).
// -------------------------------------------------------------------------------------------------
using System.Collections.Generic;
using WastelandSurvivor.Core.Defs;

namespace WastelandSurvivor.Core.IO;

public sealed class DefDatabase
{
    public IReadOnlyDictionary<string, VehicleDefinition> Vehicles { get; init; } =
        new Dictionary<string, VehicleDefinition>();

    public IReadOnlyDictionary<string, WeaponDefinition> Weapons { get; init; } =
        new Dictionary<string, WeaponDefinition>();

    public IReadOnlyDictionary<string, AmmoDefinition> Ammo { get; init; } =
        new Dictionary<string, AmmoDefinition>();

    public IReadOnlyDictionary<string, EngineDefinition> Engines { get; init; } =
        new Dictionary<string, EngineDefinition>();

    public IReadOnlyDictionary<string, TargetingComputerDefinition> Computers { get; init; } =
        new Dictionary<string, TargetingComputerDefinition>();

    public IReadOnlyDictionary<string, ArmorDefinition> Armors { get; init; } =
        new Dictionary<string, ArmorDefinition>();

    public IReadOnlyDictionary<string, DriverUpgradeDefinition> DriverUpgrades { get; init; } =
        new Dictionary<string, DriverUpgradeDefinition>();

    public IReadOnlyDictionary<string, PersonalWeaponDefinition> PersonalWeapons { get; init; } =
        new Dictionary<string, PersonalWeaponDefinition>();

    public IReadOnlyDictionary<string, CityDefinition> Cities { get; init; } =
        new Dictionary<string, CityDefinition>();
}
