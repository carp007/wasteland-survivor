using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game.Systems;

/// <summary>
/// Centralizes player-facing vehicle presentation concerns (display names, rename validation,
/// and stable garage preview colors) so garage/workshop/UIs don't re-embed that logic.
/// </summary>
public static class VehiclePresentation
{
    public const int MaxCustomNameLength = 24;

    /// <summary>
    /// The canonical player paint (yellow). This is the exact body color the arena player pawn and
    /// the in-arena HUD use, and every menu preview (garage fleet roster, selected-vehicle card,
    /// workshop showcase, city shell) must route through it so the player's vehicle keeps ONE
    /// visual identity across the whole game. Enemy tints are assigned elsewhere and never use this.
    /// </summary>
    // Deepened from the original (0.93, 0.76, 0.12): the brighter gold read as toy plastic under
    // the arena sun (play-test: "cartoony"). Still unambiguously the player's warm gold.
    public static readonly Color PlayerBodyColor = new(0.84f, 0.66f, 0.14f);

    public static string GetDisplayName(VehicleInstanceState vehicle, VehicleDefinition? definition)
    {
        var customName = NormalizeCustomName(vehicle.CustomName);
        if (!string.IsNullOrWhiteSpace(customName))
            return customName;

        return GetChassisName(vehicle, definition);
    }

    public static string GetChassisName(VehicleInstanceState vehicle, VehicleDefinition? definition)
    {
        if (!string.IsNullOrWhiteSpace(definition?.DisplayName))
            return definition!.DisplayName.Trim();

        return string.IsNullOrWhiteSpace(vehicle.DefinitionId) ? "Vehicle" : vehicle.DefinitionId.Trim();
    }

    public static string NormalizeCustomName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        var parts = rawName.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }

    public static bool TryNormalizeCustomName(string? rawName, out string normalizedName, out string error)
    {
        normalizedName = NormalizeCustomName(rawName);
        error = string.Empty;

        if (normalizedName.Length == 0)
            return true;

        if (normalizedName.Length > MaxCustomNameLength)
        {
            error = $"Vehicle names must be {MaxCustomNameLength} characters or fewer.";
            return false;
        }

        foreach (var ch in normalizedName)
        {
            if (!char.IsControl(ch))
                continue;

            error = "Vehicle names cannot contain control characters.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Body color for menu previews of player-owned vehicles. Historically this hashed the instance
    /// id into a random palette, which made the SAME vehicle render purple in the garage and green
    /// in the workshop while the arena drove it yellow. Owned vehicles always use the player paint.
    /// </summary>
    public static Color GetGaragePreviewColor(VehicleInstanceState vehicle)
    {
        return PlayerBodyColor;
    }

    public static string BuildGarageSubtitle(VehicleInstanceState vehicle, VehicleDefinition? definition)
    {
        var chassis = GetChassisName(vehicle, definition);
        var display = GetDisplayName(vehicle, definition);
        return string.Equals(display, chassis, StringComparison.Ordinal)
            ? chassis
            : $"{display} · {chassis}";
    }

    public static string BuildListSummary(VehicleInstanceState vehicle, VehicleDefinition? definition)
    {
        var display = GetDisplayName(vehicle, definition);
        var chassis = GetChassisName(vehicle, definition);
        var weaponCount = CountInstalledWeapons(vehicle);
        var cargoStacks = CountCargoStacks(vehicle);
        var towingCount = vehicle.Towing?.AttachedTowTargetInstanceIds?.Count ?? 0;
        var secondary = $"{chassis} · Weapons {weaponCount} · Cargo {cargoStacks}";
        if (towingCount > 0)
            secondary += $" · Towing {towingCount}";
        return $"{display}\n{secondary}";
    }

    public static int CountInstalledWeapons(VehicleInstanceState vehicle)
        => vehicle.InstalledWeaponsByMountId?.Count(kv => !string.IsNullOrWhiteSpace(kv.Value?.WeaponId)) ?? 0;

    public static int CountCargoStacks(VehicleInstanceState vehicle)
        => vehicle.CargoInventory?.Count(kv => kv.Value > 0) ?? 0;

    public static string BuildIconSignature(VehicleInstanceState vehicle)
    {
        var armor = string.Join(",", vehicle.CurrentArmorBySection.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}"));
        var hp = string.Join(",", vehicle.CurrentHpBySection.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}"));
        var tiresArmor = string.Join(",", vehicle.CurrentTireArmor ?? Array.Empty<int>());
        var tiresHp = string.Join(",", vehicle.CurrentTireHp ?? Array.Empty<int>());
        var weapons = string.Join(",", vehicle.InstalledWeaponsByMountId.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value?.WeaponId}"));
        var tow = string.Join(",", vehicle.Towing?.AttachedTowTargetInstanceIds ?? new List<string>());
        return string.Join("|", new[]
        {
            vehicle.DefinitionId,
            NormalizeCustomName(vehicle.CustomName),
            armor,
            hp,
            tiresArmor,
            tiresHp,
            weapons,
            tow,
            vehicle.ArmorPlatingLevel.ToString(),
            vehicle.TirePlatingLevel.ToString(),
        });
    }
}
