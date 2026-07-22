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

	/// <summary>
	/// Missile agility, 0..1: turn rate = lerp(140, 480, strength) deg/s in FireTrackingMissile
	/// (0.55 ≈ the legacy 320 deg/s feel). 0 / missing = legacy 320 deg/s exactly.
	/// </summary>
	public float TrackingStrength { get; init; } = 0.0f; // missiles
	public string ArmorPenetrationTag { get; init; } = ""; // future tag

	/// <summary>
	/// Price per round in USD (resolved by GameBalance.GetAmmoUnitPriceUsd — shared by the Workshop
	/// shelf and the tournament pit-crew restock). 0 / missing = the flat per-kind baseline from
	/// GameBalance.GetAmmoRefillPolicy applies; specialty rounds (AP/HE variants) set this in JSON
	/// to price above their kind's baseline.
	/// </summary>
	public int UnitPriceUsd { get; init; } = 0;
}
