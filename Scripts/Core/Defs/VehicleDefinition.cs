// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/Defs/VehicleDefinition.cs
// Purpose: Data definition model loaded from JSON under Data/Defs (stable, Godot-independent).
// -------------------------------------------------------------------------------------------------
using System.Collections.Generic;

namespace WastelandSurvivor.Core.Defs;

/// <summary>
/// Definition for a single weapon mount point on a vehicle (location, turret/fixed behavior, yaw constraints).
/// </summary>
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

/// <summary>
/// Definition for a vehicle archetype (loaded from Data/Defs/Vehicles). Contains base mass/storage, armor/HP by section, tires, and weapon mount points.
/// </summary>
public sealed record VehicleDefinition : IHasId
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public VehicleClass Class { get; init; } = VehicleClass.Compact;

    /// <summary>
    /// Dealership sticker price for a bare chassis (stock engine, no weapons) at a city store.
    /// 0 = not sold at the dealership.
    /// </summary>
    public int PriceUsd { get; init; } = 0;

    /// <summary>
    /// Optional res:// path to a per-class visual model scene (GLB/TSCN). When set, VehiclePawn
    /// prefers this model over its generic car-pack visual. Null/empty = pawn default visual.
    /// </summary>
    public string? VisualModelPath { get; init; } = null;

    /// <summary>
    /// Target visual length in meters the model is auto-scaled to. 0 = use the pawn's default target length.
    /// </summary>
    public float VisualTargetLength { get; init; } = 0f;

    public float BaseMassKg { get; init; } = 1200f;
    public int StorageCapacityUnits { get; init; } = 10;

    /// <summary>Fuel tank / battery capacity in abstract fuel units (roughly liters).</summary>
    public float FuelCapacityUnits { get; init; } = 50f;

    public int TireCount { get; init; } = 4;
    public bool SpareTireIncluded { get; init; } = true;

    public Dictionary<ArmorSection, int> BaseArmorBySection { get; init; } = new();
    public int BaseTireArmor { get; init; } = 5;

    /// <summary>
    /// Armor material family for the whole chassis (def-level, v1). Cross-referenced against the
    /// attacker's <see cref="AmmoDefinition.ArmorPenetrationTag"/> by DamageMatrix to scale incoming
    /// damage (ammo-vs-armor rock-paper-scissors). Defaults to Steel for legacy defs.
    /// </summary>
    public ArmorType ArmorType { get; init; } = ArmorType.Steel;

	/// <summary>
	/// Structural HP per section. This is separate from armor points.
	/// </summary>
	public Dictionary<ArmorSection, int> BaseHpBySection { get; init; } = new();

	/// <summary>
	/// Structural HP per tire.
	/// </summary>
	public int BaseTireHp { get; init; } = 10;

    public List<WeaponMountDefinition> MountPoints { get; init; } = new();

    // NOTE: engine-fit constraints live on EngineDefinition.AllowedVehicleClasses (the single
    // vocabulary the workshop, build factory, and loader all enforce). A vehicle-side
    // AllowedEngineClasses twin existed through build 171 but was never read anywhere and had
    // drifted out of agreement with the engine defs — see gameplay judge round 10 finding #1.
}
