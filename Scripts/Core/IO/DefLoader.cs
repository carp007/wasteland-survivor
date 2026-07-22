// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/IO/DefLoader.cs
// Purpose: Loads JSON definitions under Data/Defs into a DefDatabase with basic validation and cross-reference checks.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WastelandSurvivor.Core.Defs;

namespace WastelandSurvivor.Core.IO;

public sealed class DefLoader
{
    public sealed record LoadMessage(string Severity, string Message);

    public sealed record LoadResult(
        DefDatabase Database,
        int VehicleCount,
        int WeaponCount,
        int AmmoCount,
        int EngineCount,
        int ComputerCount,
        int ArmorCount,
        IReadOnlyList<LoadMessage> Messages,
        int CityCount = 0,
        int DriverUpgradeCount = 0,
        int PersonalWeaponCount = 0
    )
    {
        public int ErrorCount => Messages.Count(m => m.Severity == "ERROR");
        public int WarningCount => Messages.Count(m => m.Severity == "WARN");
    }

    public LoadResult LoadAll(string rootPath = "res://Data/Defs")
    {
        var messages = new List<LoadMessage>();

        var vehicles = LoadCategory<VehicleDefinition>($"{rootPath}/Vehicles", messages, "Vehicles");
        var weapons  = LoadCategory<WeaponDefinition>($"{rootPath}/Weapons", messages, "Weapons");
        var ammo     = LoadCategory<AmmoDefinition>($"{rootPath}/Ammo", messages, "Ammo");
        var engines  = LoadCategory<EngineDefinition>($"{rootPath}/Engines", messages, "Engines");
        var computers= LoadCategory<TargetingComputerDefinition>($"{rootPath}/Computers", messages, "Computers");
        var armors   = LoadCategory<ArmorDefinition>($"{rootPath}/Armor", messages, "Armor");
        var cities   = LoadCategory<CityDefinition>($"{rootPath}/Cities", messages, "Cities");
        var driverUpgrades = LoadCategory<DriverUpgradeDefinition>($"{rootPath}/DriverUpgrades", messages, "DriverUpgrades");
        var personalWeapons = LoadCategory<PersonalWeaponDefinition>($"{rootPath}/PersonalWeapons", messages, "PersonalWeapons");

        ValidateRefs(vehicles, weapons, ammo, engines, computers, armors, messages);
        ValidateCities(cities, messages);
        ValidateDriverUpgrades(driverUpgrades, messages);
        ValidatePersonalWeapons(personalWeapons, messages);

        var db = new DefDatabase
        {
            Vehicles = vehicles,
            Weapons = weapons,
            Ammo = ammo,
            Engines = engines,
            Computers = computers,
            Armors = armors,
            Cities = cities,
            DriverUpgrades = driverUpgrades,
            PersonalWeapons = personalWeapons
        };

        return new LoadResult(
            db,
            vehicles.Count,
            weapons.Count,
            ammo.Count,
            engines.Count,
            computers.Count,
            armors.Count,
            messages,
            cities.Count,
            driverUpgrades.Count,
            personalWeapons.Count
        );
    }

    private static void ValidatePersonalWeapons(Dictionary<string, PersonalWeaponDefinition> personalWeapons, List<LoadMessage> messages)
    {
        foreach (var p in personalWeapons.Values)
        {
            if (p.BaseDamage <= 0)
                messages.Add(new("ERROR", $"PersonalWeapon '{p.Id}' has invalid BaseDamage={p.BaseDamage}."));
            if (p.CooldownMs < 60)
                messages.Add(new("WARN", $"PersonalWeapon '{p.Id}' CooldownMs={p.CooldownMs} is below the 60ms floor."));
            if (p.RangeMeters <= 0)
                messages.Add(new("ERROR", $"PersonalWeapon '{p.Id}' has invalid RangeMeters={p.RangeMeters}."));
            if (p.PelletsPerShot < 1)
                messages.Add(new("ERROR", $"PersonalWeapon '{p.Id}' has invalid PelletsPerShot={p.PelletsPerShot}."));
            if (p.VehicleDamageMultiplier <= 0f || p.VehicleDamageMultiplier > 1f)
                messages.Add(new("WARN", $"PersonalWeapon '{p.Id}' VehicleDamageMultiplier={p.VehicleDamageMultiplier} outside (0,1] — on-foot fire is chip damage by design."));
            if (string.IsNullOrWhiteSpace(p.AmmoId))
                messages.Add(new("ERROR", $"PersonalWeapon '{p.Id}' has no AmmoId."));
            if (p.AmmoCapacity <= 0)
                messages.Add(new("WARN", $"PersonalWeapon '{p.Id}' AmmoCapacity={p.AmmoCapacity} looks odd."));
        }
    }

    private static Dictionary<string, T> LoadCategory<T>(string categoryPath, List<LoadMessage> messages, string label)
        where T : class, IHasId
    {
        var dict = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

        if (!DirExists(categoryPath))
        {
            messages.Add(new("WARN", $"Missing category folder: {categoryPath}"));
            return dict;
        }

        foreach (var file in EnumerateJsonFilesRecursive(categoryPath))
        {
            try
            {
                var json = FileAccess.GetFileAsString(file);
                var obj = JsonUtil.Deserialize<T>(json);
                if (obj is null)
                {
                    messages.Add(new("ERROR", $"{label}: Failed to deserialize (null): {file}"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(obj.Id))
                {
                    messages.Add(new("ERROR", $"{label}: Missing Id: {file}"));
                    continue;
                }

                if (!dict.TryAdd(obj.Id, obj))
                {
                    messages.Add(new("ERROR", $"{label}: Duplicate Id '{obj.Id}' found in: {file}"));
                    continue;
                }
            }
            catch (Exception ex)
            {
                messages.Add(new("ERROR", $"{label}: Exception reading {file}: {ex.Message}"));
            }
        }

        if (dict.Count == 0)
            messages.Add(new("WARN", $"{label}: No definitions loaded from {categoryPath}"));

        return dict;
    }

    private static void ValidateRefs(
        Dictionary<string, VehicleDefinition> vehicles,
        Dictionary<string, WeaponDefinition> weapons,
        Dictionary<string, AmmoDefinition> ammo,
        Dictionary<string, EngineDefinition> engines,
        Dictionary<string, TargetingComputerDefinition> computers,
        Dictionary<string, ArmorDefinition> armors,
        List<LoadMessage> messages)
    {
        // Weapons -> Ammo references
        foreach (var w in weapons.Values)
        {
            if (w.AmmoTypeIds is null || w.AmmoTypeIds.Length == 0)
            {
                messages.Add(new("WARN", $"Weapon '{w.Id}' has no AmmoTypeIds (ok for future energy weapons, but check intent)."));
                continue;
            }

            foreach (var ammoId in w.AmmoTypeIds)
            {
                if (!ammo.ContainsKey(ammoId))
                    messages.Add(new("ERROR", $"Weapon '{w.Id}' references missing ammo '{ammoId}'."));
            }
        }

        // Vehicle armor sanity
        foreach (var v in vehicles.Values)
        {
            if (v.TireCount <= 0)
                messages.Add(new("ERROR", $"Vehicle '{v.Id}' has invalid TireCount={v.TireCount}"));

            if (v.BaseTireArmor < 0)
                messages.Add(new("ERROR", $"Vehicle '{v.Id}' has invalid BaseTireArmor={v.BaseTireArmor}"));

            if (v.BaseArmorBySection is null || v.BaseArmorBySection.Count == 0)
                messages.Add(new("WARN", $"Vehicle '{v.Id}' has empty BaseArmorBySection."));

            if (v.BaseHpBySection is null || v.BaseHpBySection.Count == 0)
                messages.Add(new("WARN", $"Vehicle '{v.Id}' has empty BaseHpBySection."));

            if (v.BaseTireHp <= 0)
                messages.Add(new("WARN", $"Vehicle '{v.Id}' has BaseTireHp={v.BaseTireHp} (expected > 0)."));
        }

        // Engines allowed classes sanity
        foreach (var e in engines.Values)
        {
            if (e.AllowedVehicleClasses is null || e.AllowedVehicleClasses.Length == 0)
                messages.Add(new("WARN", $"Engine '{e.Id}' has no AllowedVehicleClasses."));
        }

        // Targeting computer sanity
        foreach (var c in computers.Values)
        {
            if (c.MaxActiveWeaponGroups <= 0)
                messages.Add(new("WARN", $"Computer '{c.Id}' MaxActiveWeaponGroups={c.MaxActiveWeaponGroups} looks odd."));
        }

        // Armor sanity
        foreach (var a in armors.Values)
        {
            if (a.MaxArmorPoints <= 0)
                messages.Add(new("WARN", $"Armor '{a.Id}' MaxArmorPoints={a.MaxArmorPoints} looks odd."));
            if (a.RepairCostPerPointUsd < 0)
                messages.Add(new("WARN", $"Armor '{a.Id}' RepairCostPerPointUsd={a.RepairCostPerPointUsd} looks odd."));
        }
    }

    private static void ValidateDriverUpgrades(Dictionary<string, DriverUpgradeDefinition> upgrades, List<LoadMessage> messages)
    {
        foreach (var u in upgrades.Values)
        {
            if (u.PriceUsd <= 0)
                messages.Add(new("WARN", $"DriverUpgrade '{u.Id}' PriceUsd={u.PriceUsd} looks odd (0 = not sold)."));

            if (u.Kind == DriverUpgradeKind.Cybernetic && u.HpBonus <= 0)
                messages.Add(new("WARN", $"DriverUpgrade '{u.Id}' is Cybernetic but HpBonus={u.HpBonus}."));

            if (u.Kind == DriverUpgradeKind.Armor && u.ArmorPoints <= 0)
                messages.Add(new("WARN", $"DriverUpgrade '{u.Id}' is Armor but ArmorPoints={u.ArmorPoints}."));
        }
    }

    private static void ValidateCities(Dictionary<string, CityDefinition> cities, List<LoadMessage> messages)
    {
        foreach (var c in cities.Values)
        {
            if (c.ArenaMaxTier is < 0 or > 5)
                messages.Add(new("WARN", $"City '{c.Id}' ArenaMaxTier={c.ArenaMaxTier} outside expected 0..5."));

            // Backdrop art is optional (Assets/ may be absent); warn so a typo'd path is visible.
            if (!string.IsNullOrWhiteSpace(c.BackdropPath)
                && !ResourceLoader.Exists(c.BackdropPath)
                && !FileAccess.FileExists(c.BackdropPath))
            {
                messages.Add(new("WARN", $"City '{c.Id}' BackdropPath '{c.BackdropPath}' not found — the procedural backdrop will be used."));
            }

            if (c.Roads is null || c.Roads.Count == 0)
            {
                messages.Add(new("WARN", $"City '{c.Id}' has no roads — it is unreachable."));
                continue;
            }

            foreach (var road in c.Roads)
            {
                if (!cities.TryGetValue(road.ToCityId, out var other))
                {
                    messages.Add(new("ERROR", $"City '{c.Id}' road references missing city '{road.ToCityId}'."));
                    continue;
                }

                if (road.DistanceKm <= 0)
                    messages.Add(new("ERROR", $"City '{c.Id}' road to '{road.ToCityId}' has invalid DistanceKm={road.DistanceKm}."));

                if (road.AmbushTierMin < 1 || road.AmbushTierMax < road.AmbushTierMin)
                    messages.Add(new("WARN", $"City '{c.Id}' road to '{road.ToCityId}' has odd ambush tier range {road.AmbushTierMin}..{road.AmbushTierMax}."));

                // Mid-route fuel stations (spec: side roads to gas stations on the long legs).
                if (road.FuelPriceMultiplier <= 0f)
                    messages.Add(new("WARN", $"City '{c.Id}' road to '{road.ToCityId}' has FuelPriceMultiplier={road.FuelPriceMultiplier} (expected > 0)."));

                // Road Closed barriers gate expansion; a barrier without a reason reads as a bug in-game.
                if (road.Closed && string.IsNullOrWhiteSpace(road.ClosedReason))
                    messages.Add(new("WARN", $"City '{c.Id}' road to '{road.ToCityId}' is Closed without a ClosedReason."));

                var mirror = other.Roads?.Find(r => string.Equals(r.ToCityId, c.Id, StringComparison.OrdinalIgnoreCase));
                if (mirror is null)
                    messages.Add(new("WARN", $"City '{c.Id}' has a road to '{road.ToCityId}' but no mirror road exists back."));
                else
                {
                    if (System.Math.Abs(mirror.DistanceKm - road.DistanceKm) > 0.01f)
                        messages.Add(new("WARN", $"Road '{c.Id}'<->'{road.ToCityId}' has asymmetric distances ({road.DistanceKm} vs {mirror.DistanceKm})."));
                    if (mirror.Closed != road.Closed)
                        messages.Add(new("WARN", $"Road '{c.Id}'<->'{road.ToCityId}' is Closed in one direction only — mirror the barrier."));
                    if (mirror.HasFuelStation != road.HasFuelStation)
                        messages.Add(new("WARN", $"Road '{c.Id}'<->'{road.ToCityId}' has HasFuelStation on one side only — mirror the station."));
                }
            }
        }

        ValidateRoadGraphConnectivity(cities, messages);
    }

    /// <summary>
    /// Road Closed barriers must never disconnect the travel graph (spec: closures gate expansion,
    /// they don't strand the player). BFS over the OPEN road entries; warn per unreachable city.
    /// </summary>
    private static void ValidateRoadGraphConnectivity(Dictionary<string, CityDefinition> cities, List<LoadMessage> messages)
    {
        if (cities.Count <= 1)
            return;

        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        var start = cities.Keys.First();
        reachable.Add(start);
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!cities.TryGetValue(id, out var current) || current.Roads is null)
                continue;

            foreach (var road in current.Roads)
            {
                if (road is null || road.Closed || string.IsNullOrWhiteSpace(road.ToCityId))
                    continue;
                if (cities.ContainsKey(road.ToCityId) && reachable.Add(road.ToCityId))
                    queue.Enqueue(road.ToCityId);
            }
        }

        foreach (var id in cities.Keys)
        {
            if (!reachable.Contains(id))
                messages.Add(new("WARN", $"City '{id}' is unreachable from '{start}' on open roads — a Closed road (or missing leg) disconnects the travel graph."));
        }
    }

    private static bool DirExists(string path)
    {
        var d = DirAccess.Open(path);
        return d != null;
    }

    private static IEnumerable<string> EnumerateJsonFilesRecursive(string dirPath)
    {
        var dir = DirAccess.Open(dirPath);
        if (dir == null)
            yield break;

        dir.ListDirBegin();
        while (true)
        {
            var name = dir.GetNext();
            if (string.IsNullOrEmpty(name))
                break;

            if (name == "." || name == "..")
                continue;

            var full = $"{dirPath}/{name}";
            if (dir.CurrentIsDir())
            {
                foreach (var sub in EnumerateJsonFilesRecursive(full))
                    yield return sub;
            }
            else if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                yield return full;
            }
        }
        dir.ListDirEnd();
    }
}
