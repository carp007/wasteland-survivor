// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/Defs/AmmoDefinition.cs
// Purpose: Data definition model loaded from JSON under Data/Defs (stable, Godot-independent).
// -------------------------------------------------------------------------------------------------
namespace WastelandSurvivor.Core.Defs;

/// <summary>
/// Definition for an ammo type (loaded from Data/Defs/Ammo). Referenced by WeaponDefinition and stored as counts on VehicleInstanceState.
/// </summary>
public sealed record AmmoDefinition : IHasId
{
	public string Id { get; init; } = "";
	public string DisplayName { get; init; } = "";

	public AmmoKind AmmoKind { get; init; } = AmmoKind.Ballistic;

	/// <summary>
	/// Mass per single unit/round (used for vehicle performance/handling).
	/// </summary>
	public float UnitMassKg { get; init; } = 0f;

	public float DamageMultiplier { get; init; } = 1.0f;
	public float TrackingStrength { get; init; } = 0.0f; // missiles
	public string ArmorPenetrationTag { get; init; } = ""; // future tag
}
