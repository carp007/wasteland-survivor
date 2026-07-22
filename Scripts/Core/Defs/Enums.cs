// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/Defs/Enums.cs
// Purpose: Data definition model loaded from JSON under Data/Defs (stable, Godot-independent).
// -------------------------------------------------------------------------------------------------
namespace WastelandSurvivor.Core.Defs;

public enum VehicleClass
{
    Compact,
    Sedan,
    Sports,
    LightTruck,
    // Unpowered haulage chassis (spec: trailers are treated like vehicles — locational damage,
    // weight, cargo). No engine ever fits this class; it moves only on another vehicle's hitch.
    Trailer,
    // Appended after Trailer so any legacy ordinal serialization keeps its meaning.
    Suv,
    HeavyTruck,
    SemiTruck
}

public enum ArmorSection
{
    Front,
    Rear,
    Left,
    Right,
    Top,
    Undercarriage
    // Tire handled separately by wheel index
}

/// <summary>
/// Armor material family for a chassis. Interacts with <see cref="AmmoDefinition.ArmorPenetrationTag"/>
/// via <c>DamageMatrix</c> (Scripts/Game/Systems) to create rock-paper-scissors ammo-vs-armor matchups:
/// Steel = baseline plate (weak to AP), Composite = light layered armor (blunts ball rounds but is
/// vulnerable to blasts), Reactive = explosive-reactive blocks (defeats penetrators and eats blasts,
/// but plain ball ammo is unaffected by it).
/// </summary>
public enum ArmorType
{
    Steel,
    Composite,
    Reactive
}

public enum WeaponType
{
    MG,
    Rocket,
    Missile,
    MineDropper,
    // Rear-deploy "dropper" hazard that lays a non-damaging oil slick which reduces traction for any
    // vehicle that drives over it. Shares the dropper deployment path with MineDropper.
    OilSlickDropper,
    // Rear-deploy "dropper" that releases a smoke cloud. Shots/missile-locks whose line of sight passes
    // through an active cloud are spoiled. Shares the dropper deployment path with the other droppers.
    SmokeScreenDropper
}

public enum FireMode
{
    DumbFire,
    LockRequired,
    LockOptional
}

public enum AmmoKind
{
    Ballistic,
    Explosive,
    Guided,
    Mine,
    Oil,
    Smoke
}

public enum FuelType
{
    Electric,
    Gas,
    Diesel
}

public enum MountLocation
{
    Front,
    Rear,
    Left,
    Right,
    Top
}

/// <summary>
/// High-level intent for a weapon mount. Used to drive runtime mounting behavior.
/// </summary>
public enum WeaponMountKind
{
    Fixed,
    Turret
}

public sealed record DamageThresholds
{
    public float Light { get; init; } = 0.25f;
    public float Heavy { get; init; } = 0.60f;
    public float Critical { get; init; } = 0.85f;
}

// Simple interface so loader can enforce Id consistently.
public interface IHasId
{
    string Id { get; }
}
