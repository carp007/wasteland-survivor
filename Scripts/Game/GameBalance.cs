// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/GameBalance.cs
// Purpose: Game-layer services and facades (session, balance, logging).
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;

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
	public const int OilAmmoRefillTarget = 6;
	public const int SmokeAmmoRefillTarget = 8;

	public const int GuidedAmmoUnitCostUsd = 55;
	public const int ExplosiveAmmoUnitCostUsd = 25;
	public const int MineAmmoUnitCostUsd = 22;
	public const int OilAmmoUnitCostUsd = 18;
	public const int SmokeAmmoUnitCostUsd = 15;

	public static (int target, int unitCostUsd) GetAmmoRefillPolicy(Core.Defs.AmmoKind kind)
	{
		return kind switch
		{
			Core.Defs.AmmoKind.Guided => (GuidedAmmoRefillTarget, GuidedAmmoUnitCostUsd),
			Core.Defs.AmmoKind.Explosive => (ExplosiveAmmoRefillTarget, ExplosiveAmmoUnitCostUsd),
			Core.Defs.AmmoKind.Mine => (MineAmmoRefillTarget, MineAmmoUnitCostUsd),
			// Dropper-style kinds must not fall through to the 200-round ballistic default.
			Core.Defs.AmmoKind.Oil => (OilAmmoRefillTarget, OilAmmoUnitCostUsd),
			Core.Defs.AmmoKind.Smoke => (SmokeAmmoRefillTarget, SmokeAmmoUnitCostUsd),
			_ => (BallisticAmmoRefillTarget, PrimaryAmmoUnitCostUsd),
		};
	}

	/// <summary>
	/// Legacy per-ammo price fallback (moved here from WorkshopView so every pricing call site
	/// shares one resolution). Pricing is def-driven now (<c>AmmoDefinition.UnitPriceUsd</c> in
	/// Data/Defs/Ammo — all shipped AP/HE defs carry the field, so this map is normally shadowed);
	/// it survives only as a safety net for def databases whose JSON predates the property. Values
	/// must stay in sync with the JSON if this ever grows.
	/// </summary>
	private static readonly Dictionary<string, int> AmmoUnitPriceOverridesUsd = new(StringComparer.OrdinalIgnoreCase)
	{
		["ammo_mg_9mm_ap"] = 4,
		["ammo_mg_50cal_ap"] = 6,
		["ammo_ac_20mm_he"] = 8,
	};

	/// <summary>
	/// Canonical per-round workshop price for an ammo def. Resolution order: the def's own JSON
	/// price (<c>AmmoDefinition.UnitPriceUsd</c>), then the legacy override map, then the flat
	/// per-kind baseline from <see cref="GetAmmoRefillPolicy"/>. Shared by the Workshop buy flow
	/// AND the tournament pit-crew restock (which applies <see cref="TournamentPitAmmoMarkup"/> on
	/// top) so the two call sites cannot drift — the pit crew used to charge the kind baseline,
	/// letting specialty AP rounds restock UNDER workshop price.
	/// </summary>
	public static int GetAmmoUnitPriceUsd(Core.Defs.AmmoDefinition? adef, string ammoId, Core.Defs.AmmoKind kind)
	{
		if (adef != null && adef.UnitPriceUsd > 0)
			return adef.UnitPriceUsd;

		if (!string.IsNullOrWhiteSpace(ammoId)
			&& AmmoUnitPriceOverridesUsd.TryGetValue(ammoId, out var overrideUsd) && overrideUsd > 0)
			return overrideUsd;

		var (_, unitCost) = GetAmmoRefillPolicy(kind);
		return unitCost;
	}

    // Spare tire consumable (master spec: the roadside tire swap is the only field repair).
    // Stored in the active vehicle's CargoInventory under this key; vehicles start with exactly 1.
    public const string SpareTireCargoId = "spare_tire";
    public const int SpareTireCostUsd = 60;

    // Cargo item masses (master spec: cargo weight impacts accel/top speed via total vehicle mass).
    public const float SpareTireMassKg = 20f;
    /// <summary>Fallback per-unit mass for cargo items without a dedicated entry.</summary>
    public const float DefaultCargoItemMassKg = 5f;

    // Prototype: 1 scrap repairs 1 point of armor/tire.
    public const int ScrapRepairCostPerPoint = 1;

    // Prototype upgrades: plating increases max armor/tire armor by +1 per level.
    public const int MaxArmorPlatingLevel = 3;
    public const int MaxTirePlatingLevel = 3;

    public const int RepairCostPerPointUsd = 3;

    // Clone respawn (master spec): decanting a fresh clone after driver death isn't free.
    public const int CloneRespawnFeeUsd = 150;

    // Memory upload at a clone facility re-anchors where a killed driver's clone wakes up.
    public const int MemoryUploadFeeUsd = 50;

    /// <summary>
    /// Ransom the captors demand for a vehicle they salvaged off the arena floor.
    /// Fallback when the vehicle's market value is unknown; prefer the value-based overload.
    /// </summary>
    public static int GetVehicleRansomUsd(int captorTier)
        => 120 + Math.Max(0, captorTier) * 60;

    /// <summary>
    /// Value-based ransom: a cut of what the vehicle is actually worth, plus a captor-tier markup.
    /// </summary>
    public static int GetVehicleRansomUsd(int captorTier, int vehicleValueUsd)
        => Math.Clamp((int)Math.Round(vehicleValueUsd * 0.22), 150, 2500)
            + Math.Max(0, captorTier) * 40;

    // Arena tournaments (master spec: arenas host AutoDuel-style tournaments).
    // A 3-round gauntlet at escalating tiers: entry paid once, per-round purses as normal,
    // champion bonus on top. The real cost is attrition — only a pit-crew patch between rounds.
    public const int TournamentRounds = 3;
    /// <summary>Fraction of missing armor/tire points the pit crew restores between rounds.</summary>
    public const float TournamentPitCrewRepairFraction = 0.35f;

    /// <summary>
    /// Entry fee for a tournament whose final round is <paramref name="maxTier"/>. Low brackets
    /// discount to $100/tier — at 150/tier a flawless Detroit run netted less than freelancing the
    /// same three fights (gameplay judge round 6 P1: "low-tier tournaments are dead content").
    /// </summary>
    public static int GetTournamentEntryFeeUsd(int maxTier)
        => (Math.Clamp(maxTier, 1, 5) <= 2 ? 100 : 150) * Math.Clamp(maxTier, 1, 5);

    /// <summary>
    /// Champion salvage rights: the bracket's stripped wrecks pay out in scrap on top of the cash
    /// bonus — tournament fights don't have a salvage phase, so the champion collects it here.
    /// </summary>
    public static int GetTournamentChampionScrapBonus(int maxTier)
        => 20 + 22 * Math.Clamp(maxTier, 1, 5);

    /// <summary>
    /// Champion bonus paid on winning the final round. Superlinear so the marquee gauntlets pay a
    /// bigger relative premium than farming low-city brackets (linear 400/tier meant Detroit paid
    /// an 83% uplift over its round purses while Pittsburgh paid 51% for radically more risk).
    /// </summary>
    public static int GetTournamentChampionBonusUsd(int maxTier)
        => Math.Clamp(maxTier, 1, 5) switch
        {
            1 => 250,
            2 => 600,
            3 => 1100,
            4 => 1700,
            _ => 2500,
        };

    /// <summary>Pit-crew ammo markup between tournament rounds (track prices — convenience costs).</summary>
    public const float TournamentPitAmmoMarkup = 1.6f;

    // Overworld-lite travel (master spec: roads between cities with encounters).
    // Legacy flat cost kept for fallback paths; road-graph travel charges toll + real fuel instead.
    public const int TravelCostUsd = 40;
    public const double RoadAmbushChance = 0.45;

    // Road-graph travel (Data/Defs/Cities): flat toll per leg + fuel burned by distance/mass/engine.
    public const int RoadTollUsd = 10;

    // Chance a quiet road leg turns up a roaming merchant convoy (spec: overworld merchants).
    public const double RoadsideMerchantChance = 0.18;

    // Chance a quiet, merchant-free leg passes a scavenge site (spec: abandoned structures with salvage).
    public const double ScavengeSiteChance = 0.16;

    // Chance an otherwise-quiet leg passes a rival salvage crew hauling a wreck (spec: AI crews
    // roam and salvage like players). Fighting them is optional; their haul pays in scrap.
    public const double SalvageCrewChance = 0.12;

    // Extra ambush chance while a freight contract's cargo is aboard (spec: cargo runs carry risk —
    // loaded haulers are worth robbing). Added to the road's own AmbushChance, capped at 0.85.
    public const double FreightAmbushChanceBonus = 0.15;

    /// <summary>Scrap paid out for a raided crew's haul (converted on the spot, tier-scaled).</summary>
    public static int GetSalvageCrewHaulScrap(int tier)
        => 20 + Math.Clamp(tier, 1, 5) * 14;

    /// <summary>USD per fuel unit before the city's local price multiplier.</summary>
    public static float GetFuelUnitPriceUsd(Core.Defs.FuelType fuel) => fuel switch
    {
        Core.Defs.FuelType.Diesel => 2.6f,
        Core.Defs.FuelType.Electric => 1.4f,
        _ => 3.0f,
    };

    // Prototype: driver personal armor (extra HP buffer).
	public const int DefaultDriverHpMax = 50;
	public const string DefaultDriverArmorId = "armor_kevlar_basic";
	public const int DefaultDriverArmorMax = 50;
	public const int DriverArmorRepairCostPerPointUsd = 3;

    // Plating costs are paid in scrap (see SessionGarage): a real sink, not pocket change.
    public static int GetArmorPlatingUpgradeCost(int nextLevel)
        => nextLevel switch
        {
            1 => 400,
            2 => 900,
            3 => 1600,
            _ => Math.Max(0, nextLevel) * 600,
        };

    public static int GetTirePlatingUpgradeCost(int nextLevel)
        => nextLevel switch
        {
            1 => 250,
            2 => 550,
            3 => 950,
            _ => Math.Max(0, nextLevel) * 400,
        };
}
