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

    // Prototype: player/driver "personal armor" (extra HP buffer). Separate from vehicle armor/tires.
	// This is intentionally simple for now; later it becomes a full equipment system.
	public int DriverHpMax { get; init; } = 50;
	public int DriverHp { get; init; } = 50;

	public string EquippedArmorId { get; init; } = "armor_kevlar_basic";
	public int DriverArmorMax { get; init; } = 50;
	public int DriverArmor { get; init; } = 50;
}
