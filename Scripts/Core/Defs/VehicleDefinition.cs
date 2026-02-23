using System.Collections.Generic;

namespace WastelandSurvivor.Core.Defs;

public sealed record WeaponMountDefinition
{
    public string MountId { get; init; } = "";
    public MountLocation MountLocation { get; init; } = MountLocation.Front;

    /// <summary>
    /// High-level behavior intent for the mount.
    /// Fixed: weapon aims with vehicle heading.
    /// Turret: weapon may yaw independently (e.g., 360° top mount).
    /// </summary>
    public WeaponMountKind Kind { get; init; } = WeaponMountKind.Fixed;

    public float ArcDegrees { get; init; } = 0f;
    public bool CanAutoAim { get; init; } = false;

    // Optional fine-grain aiming constraints (degrees). Used for adjustable mounts/turrets.
    // Defaults mean "no constraint beyond ArcDegrees".
    public float? YawMinDegrees { get; init; } = null;
    public float? YawMaxDegrees { get; init; } = null;

    public int MaxWeaponSize { get; init; } = 1; // future
    public string[] AllowedWeaponTags { get; init; } = System.Array.Empty<string>(); // future
}

public sealed record VehicleDefinition : IHasId
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public VehicleClass Class { get; init; } = VehicleClass.Compact;

    public float BaseMassKg { get; init; } = 1200f;
    public int StorageCapacityUnits { get; init; } = 10;

    public int TireCount { get; init; } = 4;
    public bool SpareTireIncluded { get; init; } = true;

    public Dictionary<ArmorSection, int> BaseArmorBySection { get; init; } = new();
    public int BaseTireArmor { get; init; } = 5;

	/// <summary>
	/// Structural HP per section. This is separate from armor points.
	/// </summary>
	public Dictionary<ArmorSection, int> BaseHpBySection { get; init; } = new();

	/// <summary>
	/// Structural HP per tire.
	/// </summary>
	public int BaseTireHp { get; init; } = 10;

    public List<WeaponMountDefinition> MountPoints { get; init; } = new();

    // Simple V1 constraints: list of allowed classes for engines
    public VehicleClass[] AllowedEngineClasses { get; init; } = System.Array.Empty<VehicleClass>();
}
