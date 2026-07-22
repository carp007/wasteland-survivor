// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/Defs/WeaponDefinition.cs
// Purpose: Data definition model loaded from JSON under Data/Defs (stable, Godot-independent).
// -------------------------------------------------------------------------------------------------
namespace WastelandSurvivor.Core.Defs;

/// <summary>
/// Definition for a weapon (loaded from Data/Defs/Weapons). Includes damage/rate, mount constraints, ammo types, and (later) projectile behavior.
/// </summary>
public sealed record WeaponDefinition : IHasId
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";

    public WeaponType WeaponType { get; init; } = WeaponType.MG;
    public FireMode FireMode { get; init; } = FireMode.DumbFire;

    public float ProjectileSpeed { get; init; } = 0f; // 0 for hitscan (MG V1)
    public float BaseDamage { get; init; } = 5f;
    public float? SplashRadius { get; init; } = null;

    public int CooldownMs { get; init; } = 200;

    /// <summary>
    /// Approximate mass of the weapon (used for vehicle performance/handling).
    /// </summary>
    public float MassKg { get; init; } = 0f;

    public string[] AmmoTypeIds { get; init; } = System.Array.Empty<string>();

    /// <summary>Store price (USD). 0 = not sold in stores.</summary>
    public int PriceUsd { get; init; } = 0;

    /// <summary>
    /// Mount locations this weapon may be installed on. Empty = any mount.
    /// (Master spec: certain weapons are only allowed on certain mount locations —
    /// e.g. droppers are rear-deploy systems and only fit Rear mounts.)
    /// </summary>
    public MountLocation[] AllowedMountLocations { get; init; } = System.Array.Empty<MountLocation>();

    /// <summary>True if this weapon may be installed on a mount at <paramref name="location"/>.</summary>
    public bool IsMountLocationAllowed(MountLocation location)
    {
        if (AllowedMountLocations.Length == 0) return true;
        for (var i = 0; i < AllowedMountLocations.Length; i++)
        {
            if (AllowedMountLocations[i] == location) return true;
        }
        return false;
    }
}
