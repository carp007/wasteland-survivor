// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/Defs/PersonalWeaponDefinition.cs
// Purpose: Data definition model loaded from JSON under Data/Defs (stable, Godot-independent).
// -------------------------------------------------------------------------------------------------
namespace WastelandSurvivor.Core.Defs;

/// <summary>
/// Role class of a driver-carried personal weapon (master spec: on-foot gameplay).
/// Sidearm = starter/backup, Smg = close spray, Shotgun = point-blank burst, Rifle = long finisher.
/// </summary>
public enum PersonalWeaponClass
{
    Sidearm,
    Smg,
    Shotgun,
    Rifle
}

/// <summary>
/// Driver-carried personal weapon (loaded from Data/Defs/PersonalWeapons). Sold at the Driver Store
/// ("Clinic &amp; Outfitter") alongside vests and cybernetics. Single equipped slot on the driver,
/// mirroring the armor-vest slot. Distinct from vehicle-mounted <see cref="WeaponDefinition"/>:
/// personal weapons fire from the on-foot pawn, draw from per-person ammo pools carried on the
/// player profile (not any vehicle's ammo inventory), and are deliberately weaker than any vehicle
/// mount — on-foot is high-risk utility, and these are last-resort defense plus a finishing tool.
/// See Docs/ONFOOT_COMBAT_PLAN.md for the full schema/balance rationale.
/// </summary>
public sealed record PersonalWeaponDefinition : IHasId
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";

    public PersonalWeaponClass WeaponClass { get; init; } = PersonalWeaponClass.Sidearm;

    /// <summary>Damage per pellet against drivers/on-foot targets (before the arena's random band).</summary>
    public float BaseDamage { get; init; } = 5f;

    /// <summary>Pellets per trigger pull (1 for everything except shotguns). Each pellet rolls spread independently.</summary>
    public int PelletsPerShot { get; init; } = 1;

    /// <summary>Per-shot cooldown (same unit convention as <see cref="WeaponDefinition.CooldownMs"/>).</summary>
    public int CooldownMs { get; init; } = 400;

    /// <summary>Hard hitscan range; on-foot auto-aim only engages targets inside this radius.</summary>
    public float RangeMeters { get; init; } = 25f;

    /// <summary>Yaw jitter per pellet in radians (matches the arena spread convention).</summary>
    public float SpreadRadians { get; init; } = 0.030f;

    /// <summary>
    /// Scales damage applied to vehicle sections (chip damage by design; keep at or below 0.5 so
    /// personal fire finishes stripped sections instead of competing with mounted weapons).
    /// </summary>
    public float VehicleDamageMultiplier { get; init; } = 0.25f;

    /// <summary>
    /// Personal ammo pool key ("pammo_*"). Pools live on PlayerProfileState and follow the driver
    /// through cloning; they are NOT AmmoDefinition ids and never touch vehicle ammo inventories.
    /// Weapons may share a pool (pistol + SMG both feed on "pammo_9mm").
    /// </summary>
    public string AmmoId { get; init; } = "";

    /// <summary>Max rounds of <see cref="AmmoId"/> carried while this weapon is equipped.</summary>
    public int AmmoCapacity { get; init; } = 60;

    /// <summary>Driver Store price per 10-round box of this weapon's ammo.</summary>
    public int AmmoPricePer10Usd { get; init; } = 5;

    /// <summary>Rounds granted on purchase (topped up to at most <see cref="AmmoCapacity"/>).</summary>
    public int StartingAmmo { get; init; } = 0;

    /// <summary>Carried mass. Cosmetic today; interacts with the future exo-frame carry-weight rules.</summary>
    public float MassKg { get; init; } = 1.0f;

    /// <summary>Store price (USD). 0 = not sold in stores (convention shared with WeaponDefinition).</summary>
    public int PriceUsd { get; init; } = 0;

    /// <summary>Short fiction line shown under the stat line in the store.</summary>
    public string Flavor { get; init; } = "";
}
