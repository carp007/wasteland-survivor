using System;

namespace WastelandSurvivor.Game;

/// <summary>
/// Centralized prototype balancing knobs.
/// Keep gameplay rules out of GameSession so systems can depend on a single source.
/// </summary>
public static class GameBalance
{
    // Arena prototype assumes a single "primary" ammo type for firing.
    public const string PrimaryAmmoId = "ammo_mg_9mm";

    // Prototype economy: dollars per round.
    public const int PrimaryAmmoUnitCostUsd = 2;

    // Prototype: 1 scrap repairs 1 point of armor/tire.
    public const int ScrapRepairCostPerPoint = 1;

    // Prototype upgrades: plating increases max armor/tire armor by +1 per level.
    public const int MaxArmorPlatingLevel = 3;
    public const int MaxTirePlatingLevel = 3;

    public const int RepairCostPerPointUsd = 3;

    // Prototype: driver personal armor (extra HP buffer).
	public const int DefaultDriverHpMax = 50;
	public const string DefaultDriverArmorId = "armor_kevlar_basic";
	public const int DefaultDriverArmorMax = 50;
	public const int DriverArmorRepairCostPerPointUsd = 3;

    public static int GetArmorPlatingUpgradeCost(int nextLevel)
        => Math.Max(0, nextLevel) * 10;

    public static int GetTirePlatingUpgradeCost(int nextLevel)
        => Math.Max(0, nextLevel) * 8;
}
