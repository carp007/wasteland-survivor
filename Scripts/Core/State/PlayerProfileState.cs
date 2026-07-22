// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/State/PlayerProfileState.cs
// Purpose: Serializable runtime/persisted state records (SaveGameState and related sub-records).
// -------------------------------------------------------------------------------------------------
using System.Collections.Generic;

namespace WastelandSurvivor.Core.State;

/// <summary>
/// Persisted player profile/progression (money, city, owned/active vehicles, driver HP/AP and equipped armor).
/// </summary>
public sealed record PlayerProfileState
{
    public int MoneyUsd { get; init; } = 1000;

    // Prototype crafting currency (used for future repairs/upgrades).
    public int Scrap { get; init; } = 0;

    public string CurrentCityId { get; init; } = "detroit";
    public string LastRespawnCityId { get; init; } = "detroit";

    public List<string> OwnedVehicleIds { get; init; } = new();
    public string? ActiveVehicleId { get; init; } = null;

    // Owned-but-not-installed parts (weapon/engine/computer def id -> count). Bought at the city
    // Store; the Workshop can only install parts you own (master spec: stores + economy).
    public Dictionary<string, int> PartsInventory { get; init; } = new();

    // Prototype: player/driver "personal armor" (extra HP buffer). Separate from vehicle armor/tires.
	// This is intentionally simple for now; later it becomes a full equipment system.
	public int DriverHpMax { get; init; } = 50;
	public int DriverHp { get; init; } = 50;

	public string EquippedArmorId { get; init; } = "armor_kevlar_basic";
	public int DriverArmorMax { get; init; } = 50;
	public int DriverArmor { get; init; } = 50;

	// One-time cybernetic installs (DriverUpgradeDefinition ids). Each raises DriverHpMax permanently;
	// installs happen only at clone-facility cities (they cut you open and re-upload the driver).
	// Defaulted so pre-existing saves deserialize cleanly with no migration step.
	public List<string> InstalledCyberneticIds { get; init; } = new();

	// Lifetime freight contracts delivered. Doubles as the freight-office reroll seed so each
	// completed delivery refreshes the offer board. Additive default — no migration needed.
	public int FreightContractsDelivered { get; init; } = 0;

	// Lifetime bounty heads claimed. Doubles as the WANTED-board reroll seed so each claimed
	// bounty refreshes the posters. Additive default — no migration needed.
	public int BountiesClaimed { get; init; } = 0;

	// --- On-foot personal weapons (master spec: on-foot gameplay; Docs/ONFOOT_COMBAT_PLAN.md) ---
	// A fresh clone always wakes with the basic 9mm sidearm: the equipped default is implicitly
	// owned even when OwnedPersonalWeaponIds is empty. Personal ammo pools ("pammo_*") live HERE,
	// not on any vehicle — they follow the driver through cloning and vehicle swaps.
	// All additive defaults — no save-version bump needed.
	public string EquippedPersonalWeaponId { get; init; } = "pw_pistol_9mm";
	public List<string> OwnedPersonalWeaponIds { get; init; } = new();
	public Dictionary<string, int> PersonalAmmoInventory { get; init; } = new();
}
