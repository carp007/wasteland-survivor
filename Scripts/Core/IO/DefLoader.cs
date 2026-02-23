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
        IReadOnlyList<LoadMessage> Messages
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

        ValidateRefs(vehicles, weapons, ammo, engines, computers, armors, messages);

        var db = new DefDatabase
        {
            Vehicles = vehicles,
            Weapons = weapons,
            Ammo = ammo,
            Engines = engines,
            Computers = computers,
            Armors = armors
        };

        return new LoadResult(
            db,
            vehicles.Count,
            weapons.Count,
            ammo.Count,
            engines.Count,
            computers.Count,
            armors.Count,
            messages
        );
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
