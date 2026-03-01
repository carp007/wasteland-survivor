// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/GameBalance.cs
// Purpose: Game-layer services and facades (session, balance, logging).
// -------------------------------------------------------------------------------------------------
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

	// Prototype ammo refill policy used by the Workshop UI.
	public const int BallisticAmmoRefillTarget = 200;
	public const int GuidedAmmoRefillTarget = 8;
	public const int ExplosiveAmmoRefillTarget = 12;
	public const int MineAmmoRefillTarget = 6;

	public const int GuidedAmmoUnitCostUsd = 35;
	public const int ExplosiveAmmoUnitCostUsd = 25;
	public const int MineAmmoUnitCostUsd = 45;

	public static (int target, int unitCostUsd) GetAmmoRefillPolicy(Core.Defs.AmmoKind kind)
	{
		return kind switch
		{
			Core.Defs.AmmoKind.Guided => (GuidedAmmoRefillTarget, GuidedAmmoUnitCostUsd),
			Core.Defs.AmmoKind.Explosive => (ExplosiveAmmoRefillTarget, ExplosiveAmmoUnitCostUsd),
			Core.Defs.AmmoKind.Mine => (MineAmmoRefillTarget, MineAmmoUnitCostUsd),
			_ => (BallisticAmmoRefillTarget, PrimaryAmmoUnitCostUsd),
		};
	}

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
