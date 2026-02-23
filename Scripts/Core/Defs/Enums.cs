namespace WastelandSurvivor.Core.Defs;

public enum VehicleClass
{
    Compact,
    Sedan,
    Sports,
    LightTruck
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

public enum WeaponType
{
    MG,
    Rocket,
    Missile,
    MineDropper
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
    Mine
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
