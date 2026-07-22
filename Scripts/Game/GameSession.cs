// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/GameSession.cs
// Purpose: Facade over the current SaveGameState plus focused session services (World/Garage/Encounters). UI should call into this, not mutate save state directly.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game.Session;
using WastelandSurvivor.Game.Systems;

namespace WastelandSurvivor.Game;

/// <summary>
/// In-memory runtime session: current save game + services to mutate it safely.
/// UI talks to this facade, not directly to raw SaveGameState.
/// </summary>
public sealed class GameSession
{
	// Bump this when we introduce backward-compatible save migrations.
	private const int CurrentSaveVersion = 12;

	private readonly SessionContext _ctx;

	public SaveGameState Save => _ctx.Save;

	// Subsystems (foundation for growing the game without a "God object" GameSession).
	internal SessionWorld World { get; }
	internal SessionGarage Garage { get; }
	internal SessionEncounters Encounters { get; }
	internal SessionStore Store { get; }
	internal SessionFreight Freight { get; }
	internal SessionBounties Bounties { get; }

	public event Action<SaveGameState>? SaveChanged
	{
		add => _ctx.SaveChanged += value;
		remove => _ctx.SaveChanged -= value;
	}

	public GameSession(SaveGameStore store, SaveGameState save)
	{
		_ctx = new SessionContext(store, save, Console);
		World = new SessionWorld(_ctx);
		Garage = new SessionGarage(_ctx);
		Encounters = new SessionEncounters(_ctx);
		Store = new SessionStore(_ctx);
		Freight = new SessionFreight(_ctx);
		Bounties = new SessionBounties(_ctx);

		MigrateSaveIfNeeded();
	}

	public void Persist() => _ctx.Persist();

	/// <summary>
	/// NEW GAME (title screen): replace the whole save with the canonical fresh-start state and walk
	/// it through the normal migration chain so it lands on <see cref="CurrentSaveVersion"/> exactly
	/// like a loaded save would. Persisted immediately — the previous save is gone after this call.
	/// </summary>
	public void ResetToNewGame()
	{
		_ctx.Replace(SaveGameStore.CreateDefaultState());
		MigrateSaveIfNeeded();
		// Issue the starter chassis immediately (judge round, loop 6): a fresh run should open on
		// a drivable game, not a hub-locked "find the one services button" scavenger hunt.
		try
		{
			Garage.CreateStarterVehicleIfMissing("veh_compact");
		}
		catch (Exception ex)
		{
			Console()?.Error($"New game: starter vehicle issue failed ({ex.Message}) — use Issue Starter Vehicle in the city hub.");
		}
		Console()?.Status("New game started — previous save wiped.");
	}

	// --- World ---
	public void SetCurrentCity(string cityId) => World.SetCurrentCity(cityId);
	public CityDefinition? GetCurrentCityDef(DefDatabase defs) => World.GetCurrentCityDef(defs);
	public IReadOnlyList<TravelOption> GetTravelOptions(DefDatabase defs) => World.GetTravelOptions(defs);
	public bool TryTravelTo(DefDatabase defs, string cityId, out int ambushTier, out string error) => World.TryTravelTo(defs, cityId, out ambushTier, out error);
	public bool TryTravelTo(DefDatabase defs, string cityId, out int ambushTier, out bool roadsideMerchant, out string error) => World.TryTravelTo(defs, cityId, out ambushTier, out roadsideMerchant, out error);
	public (float current, float capacity) GetActiveVehicleFuel(DefDatabase defs) => World.GetActiveVehicleFuel(defs);
	public bool TryUploadMemory(DefDatabase defs, out string error) => World.TryUploadMemory(defs, out error);
	public IReadOnlyList<CapturedVehicleState> GetCapturedVehicles() => Encounters.GetCapturedVehicles();
	public bool TryPayVehicleRansom(string vehicleInstanceId, out string error) => Encounters.TryPayVehicleRansom(vehicleInstanceId, out error);
	public bool TryStartInterceptionEncounter(string vehicleInstanceId, out string error) => Encounters.TryStartInterceptionEncounter(vehicleInstanceId, out error);
	public (float missingUnits, int costUsd) ComputeRefuelToFullCost(DefDatabase defs) => World.ComputeRefuelToFullCost(defs);
	public bool TryRefuelActiveVehicleToFull(DefDatabase defs, out int costUsd, out string error) => World.TryRefuelActiveVehicleToFull(defs, out costUsd, out error);
	// --- On-foot personal weapons (Docs/ONFOOT_COMBAT_PLAN.md) ---
	public bool TryBuyPersonalWeapon(DefDatabase defs, string weaponId, out string error) => Store.TryBuyPersonalWeapon(defs, weaponId, out error);
	public bool TryEquipPersonalWeapon(DefDatabase defs, string weaponId, out string error) => Store.TryEquipPersonalWeapon(defs, weaponId, out error);
	public bool TryBuyPersonalAmmo(DefDatabase defs, string weaponId, out string error) => Store.TryBuyPersonalAmmo(defs, weaponId, out error);
	public void ReplacePersonalAmmoPools(IReadOnlyDictionary<string, int> pools) => Store.ReplacePersonalAmmoPools(pools);

	public int GetDriverHp() => _ctx.Save.Player.DriverHp;
	public int GetDriverHpMax() => _ctx.Save.Player.DriverHpMax;
	public int GetDriverArmor() => _ctx.Save.Player.DriverArmor;
	public int GetDriverArmorMax() => _ctx.Save.Player.DriverArmorMax;
	public string GetEquippedDriverArmorId() => _ctx.Save.Player.EquippedArmorId;
	public int GetMoneyUsd() => _ctx.Save.Player.MoneyUsd;
	public (int missingPoints, int costUsd) ComputeDriverArmorRepairCost() => World.ComputeDriverArmorRepairCost();
	public bool TryRepairDriverArmorToFull(out string error) => World.TryRepairDriverArmorToFull(out error);
	public bool TryAddMoney(int amountUsd, out int newBalance, out string error) => World.TryAddMoney(amountUsd, out newBalance, out error);

	// --- Vehicles / Garage ---
	public IEnumerable<VehicleInstanceState> GetOwnedVehicles() => Garage.GetOwnedVehicles();
	public VehicleInstanceState? GetActiveVehicle() => Garage.GetActiveVehicle();
	public void SetActiveVehicle(string instanceId) => Garage.SetActiveVehicle(instanceId);
	public void UpdateVehicle(VehicleInstanceState updated) => Garage.UpdateVehicle(updated);
	public bool TryRenameOwnedVehicle(string vehicleInstanceId, string? requestedName, out string normalizedName, out string error)
		=> Garage.TryRenameOwnedVehicle(vehicleInstanceId, requestedName, out normalizedName, out error);
	public VehicleInstanceState CreateStarterVehicle(DefDatabase defs, string vehicleDefId = "veh_compact")
		=> Garage.CreateStarterVehicle(defs, vehicleDefId);
	public VehicleInstanceState CreateStarterVehicleIfMissing(DefDatabase defs, string vehicleDefId = "veh_compact")
		=> Garage.CreateStarterVehicleIfMissing(defs, vehicleDefId);
	public VehicleInstanceState CreateStarterVehicleIfMissing(string vehicleDefId = "veh_compact")
		=> Garage.CreateStarterVehicleIfMissing(vehicleDefId);

	// --- Trailers (spec pillar 4: towing incl. chained towing; V1 overworld/garage layer) ---
	public bool TryHitchTrailer(DefDatabase defs, string trailerInstanceId, out string error)
		=> Garage.TryHitchTrailer(defs, trailerInstanceId, out error);
	public bool TryUnhitchTrailer(string trailerInstanceId, out string error)
		=> Garage.TryUnhitchTrailer(trailerInstanceId, out error);
	public VehicleInstanceState? FindTowingVehicle(string trailerInstanceId)
		=> Garage.FindTowingVehicle(trailerInstanceId);

	// --- Freight contracts (spec: cargo/logistics pillar) ---
	public List<FreightOffer> GetFreightOffers(DefDatabase defs) => Freight.GetOffers(defs);
	public FreightContractState? GetActiveFreightContract() => _ctx.Save.ActiveFreightContract;
	public bool TryAcceptFreightOffer(DefDatabase defs, string offerId, out string error) => Freight.TryAcceptOffer(defs, offerId, out error);
	public bool TryAbandonFreightContract(out string error) => Freight.TryAbandonContract(out error);
	public int GetFreightFreeStorageUnits(DefDatabase defs) => Freight.GetChainFreeStorageUnits(defs);
	/// <summary>Payout completed by the most recent travel leg (0 = none); cleared by the caller.</summary>
	public int LastLegDeliveredFreightPayout { get => World.LastLegDeliveredFreightPayout; set => World.LastLegDeliveredFreightPayout = value; }

	// --- WANTED bounties (spec: expandable quest systems; named raiders haunt road legs) ---
	public List<BountyOffer> GetBountyOffers(DefDatabase defs) => Bounties.GetOffers(defs);
	public BountyContractState? GetActiveBountyContract() => _ctx.Save.ActiveBountyContract;
	public bool TryAcceptBounty(DefDatabase defs, string bountyId, out string error) => Bounties.TryAcceptBounty(defs, bountyId, out error);
	public bool TryAbandonBounty(out string error) => Bounties.TryAbandonBounty(out error);
	public bool TryStartBountyHunt(out string error) => Encounters.TryStartBountyHunt(out error);
	/// <summary>Tier (>0) when the most recent leg was the active bounty's haunted road.</summary>
	public int LastLegBountyTier => World.LastLegBountyTier;

	// --- Store / parts economy ---
	public int GetOwnedPartCount(string partId) => Store.GetOwnedPartCount(partId);
	public bool TryBuyPart(DefDatabase defs, string partId, out string error) => Store.TryBuyPart(defs, partId, out error);
	public bool TrySellPart(DefDatabase defs, string partId, out string error) => Store.TrySellPart(defs, partId, out error);
	public bool TryConsumeOwnedPart(string partId) => Store.TryConsumeOwnedPart(partId);
	public void ReturnPartToInventory(string partId) => Store.ReturnPartToInventory(partId);
	public bool TryBuySpareTireForActiveVehicle(DefDatabase defs, out string error) => Store.TryBuySpareTireForActiveVehicle(defs, out error);
	public bool TryBuyVehicle(DefDatabase defs, string vehicleDefId, out string error) => Store.TryBuyVehicle(defs, vehicleDefId, out error);

	// --- Ammo ---
	public int GetActiveVehicleAmmo(string ammoId) => Garage.GetActiveVehicleAmmo(ammoId);
	public bool TryConsumeActiveVehicleAmmo(string ammoId, int count, out string error)
		=> Garage.TryConsumeActiveVehicleAmmo(ammoId, count, out error);
	public bool TryBuyAmmoForActiveVehicle(string ammoId, int count, int unitCostUsd, out string error)
		=> Garage.TryBuyAmmoForActiveVehicle(ammoId, count, unitCostUsd, out error);

	// --- Repairs / Upgrades ---
	public (int armorMissing, int tireMissing, int totalMissing) ComputeMissingRepairPointsByType(string vehicleInstanceId, DefDatabase defs)
		=> Garage.ComputeMissingRepairPointsByType(vehicleInstanceId, defs);
	public (int missingPoints, int costUsd) ComputeRepairToFullCost(string vehicleInstanceId, DefDatabase defs)
		=> Garage.ComputeRepairToFullCost(vehicleInstanceId, defs);
	public bool TryRepairVehicleToFull(string vehicleInstanceId, DefDatabase defs, out string error)
		=> Garage.TryRepairVehicleToFull(vehicleInstanceId, defs, out error);
	public bool TryPatchArmorWithScrap(string vehicleInstanceId, DefDatabase defs, out string error)
		=> Garage.TryPatchArmorWithScrap(vehicleInstanceId, defs, out error);
	public bool TryPatchTireWithScrap(string vehicleInstanceId, DefDatabase defs, out string error)
		=> Garage.TryPatchTireWithScrap(vehicleInstanceId, defs, out error);
	public bool TryPatchArmorToAffordableFullWithScrap(string vehicleInstanceId, DefDatabase defs, out int repairedPoints, out string error)
		=> Garage.TryPatchArmorToAffordableFullWithScrap(vehicleInstanceId, defs, out repairedPoints, out error);
	public bool TryPatchTiresToAffordableFullWithScrap(string vehicleInstanceId, DefDatabase defs, out int repairedPoints, out string error)
		=> Garage.TryPatchTiresToAffordableFullWithScrap(vehicleInstanceId, defs, out repairedPoints, out error);
	public bool TryUpgradeArmorPlating(string vehicleInstanceId, DefDatabase defs, out string error)
		=> Garage.TryUpgradeArmorPlating(vehicleInstanceId, defs, out error);
	public bool TryUpgradeTirePlating(string vehicleInstanceId, DefDatabase defs, out string error)
		=> Garage.TryUpgradeTirePlating(vehicleInstanceId, defs, out error);
	public int ComputeStripVehicleScrapValue(string vehicleInstanceId, DefDatabase defs)
		=> Garage.ComputeStripVehicleScrapValue(vehicleInstanceId, defs);
	public int ComputeSellVehicleUsdValue(string vehicleInstanceId, DefDatabase defs)
		=> Garage.ComputeSellVehicleUsdValue(vehicleInstanceId, defs);
	public bool TryStripOwnedVehicle(string vehicleInstanceId, DefDatabase defs, out int scrapAwarded, out string error)
		=> Garage.TryStripOwnedVehicle(vehicleInstanceId, defs, out scrapAwarded, out error);
	public bool TrySellOwnedVehicle(string vehicleInstanceId, DefDatabase defs, out int salePriceUsd, out string error)
		=> Garage.TrySellOwnedVehicle(vehicleInstanceId, defs, out salePriceUsd, out error);

	// --- Scavenge sites (spec: abandoned roadside structures containing salvage) ---
	public bool TryStartScavengeEncounter(out string error) => Encounters.TryStartScavengeEncounter(out error);
	public bool TryConsumeWorldFlag(string flag) => Encounters.TryConsumeWorldFlag(flag);
	public bool LastLegRolledScavengeSite => World.LastLegRolledScavengeSite;

	// --- Rival salvage crews (spec: AI crews roam and salvage like players) ---
	public bool TryStartSalvageCrewRaid(out string error) => Encounters.TryStartSalvageCrewRaid(out error);
	public bool LastLegRolledSalvageCrew => World.LastLegRolledSalvageCrew;

	// --- Tournaments (spec: arenas host AutoDuel-style tournaments) ---
	public TournamentState? GetActiveTournament() => Encounters.GetActiveTournament();
	public bool TryEnterTournament(out string error) => Encounters.TryEnterTournament(out error);
	public bool TryStartNextTournamentRound(out string error) => Encounters.TryStartNextTournamentRound(out error);
	public void AbandonTournament(string? reason = null) => Encounters.AbandonTournament(reason);

	// --- Encounters ---
	public bool HasActiveEncounter() => Encounters.HasActiveEncounter();
	public EncounterState? GetCurrentEncounter() => Encounters.GetCurrentEncounter();
	public int GetActiveVehicleHp() => Encounters.GetActiveVehicleHp();
	public bool TryStartArenaEncounter(int tier, out string error) => Encounters.TryStartArenaEncounter(tier, out error);
	public bool TryStartRoadEncounter(int tier, out string error) => Encounters.TryStartRoadAmbushEncounter(tier, out error);
	public void EndActiveEncounter(string outcome = "fled") => Encounters.EndActiveEncounter(outcome);
	public void ClearEncounter() => Encounters.ClearEncounter();
	public void SetEncounterOutcome(string outcome, string? extraLogLine = null) => Encounters.SetEncounterOutcome(outcome, extraLogLine);
	public void ResolveActiveEncounterWin(int moneyRewardUsd, int scrapReward, string ammoId, int ammoCount, string? extraLogLine = null)
		=> Encounters.ResolveActiveEncounterWin(moneyRewardUsd, scrapReward, ammoId, ammoCount, extraLogLine);
	public bool ResolveArenaEncounterRealtime(
		string outcome,
		VehicleInstanceState finalPlayerVehicle,
		int enemyHpAfter,
		int driverArmorAfter,
		int driverHpAfter,
		string[] runtimeLog,
		out VehicleInstanceState updatedPlayerVehicle,
		out string error)
		=> Encounters.ResolveArenaEncounterRealtime(outcome, finalPlayerVehicle, enemyHpAfter, driverArmorAfter, driverHpAfter, runtimeLog, out updatedPlayerVehicle, out error);
	public bool TryUpdateActiveVehicleTowState(TowingState towingState, out VehicleInstanceState updatedVehicle, out string error)
		=> Encounters.TryUpdateActiveVehicleTowState(towingState, out updatedVehicle, out error);
	public bool TryAwardBattlefieldScrap(int scrapAmount, string? logLine, out string error)
		=> Encounters.TryAwardBattlefieldScrap(scrapAmount, logLine, out error);
	public bool TryRecoverTowedVehicle(
		VehicleInstanceState salvagedVehicle,
		string displayName,
		out VehicleInstanceState recoveredVehicle,
		out VehicleInstanceState updatedActiveVehicle,
		out string error)
		=> Encounters.TryRecoverTowedVehicle(salvagedVehicle, displayName, out recoveredVehicle, out updatedActiveVehicle, out error);

	public bool TryClaimHijackedVehicle(
		VehicleInstanceState salvagedVehicle,
		string displayName,
		out VehicleInstanceState claimedVehicle,
		out string error)
		=> Encounters.TryClaimHijackedVehicle(salvagedVehicle, displayName, out claimedVehicle, out error);

	// Back-compat overload (older callers): use current saved armor.
	public bool ResolveArenaEncounterRealtime(
		string outcome,
		VehicleInstanceState finalPlayerVehicle,
		int enemyHpAfter,
		string[] runtimeLog,
		out VehicleInstanceState updatedPlayerVehicle,
		out string error)
		=> Encounters.ResolveArenaEncounterRealtime(outcome, finalPlayerVehicle, enemyHpAfter, _ctx.Save.Player.DriverArmor, _ctx.Save.Player.DriverHp, runtimeLog, out updatedPlayerVehicle, out error);

	// --- Save migration ---
	private void MigrateSaveIfNeeded()
	{
		var save = _ctx.Save;
		var defs = App.Instance?.Services.Defs;
		var changed = SaveMigration.TryMigrateToVersion(
			ref save,
			CurrentSaveVersion,
			primaryAmmoId: PrimaryAmmoId,
			seedAmmoIfMissing: 50,
			defs: defs);

		if (!changed)
			return;

		_ctx.Replace(save);
	}

	// --- Console hook ---
	private static GameConsole? Console()
	{
		var app = App.Instance;
		if (app == null) return null;
		return app.Services.TryGet<GameConsole>(out var c) ? c : null;
	}

	// --- Compatibility + shared rules ---
	public const string PrimaryAmmoId = GameBalance.PrimaryAmmoId;
	public const int PrimaryAmmoUnitCostUsd = GameBalance.PrimaryAmmoUnitCostUsd;
	public const int ScrapRepairCostPerPoint = GameBalance.ScrapRepairCostPerPoint;
	public const int MaxArmorPlatingLevel = GameBalance.MaxArmorPlatingLevel;
	public const int MaxTirePlatingLevel = GameBalance.MaxTirePlatingLevel;
	public const int RepairCostPerPointUsd = GameBalance.RepairCostPerPointUsd;

	public static int GetArmorPlatingUpgradeCost(int nextLevel) => GameBalance.GetArmorPlatingUpgradeCost(nextLevel);
	public static int GetTirePlatingUpgradeCost(int nextLevel) => GameBalance.GetTirePlatingUpgradeCost(nextLevel);

	/// <summary>
	/// Public wrapper kept for existing callers. Actual math lives in VehicleCombatMath.
	/// </summary>
	public static int ComputeVehicleHp(VehicleInstanceState v) => VehicleCombatMath.ComputeVehicleHp(v);

	/// <summary>
	/// Public wrapper kept for existing callers. Actual math lives in VehicleCombatMath.
	/// </summary>
	public static VehicleInstanceState ApplyDamageToVehicle(VehicleInstanceState v, int damage) => VehicleCombatMath.ApplyDamageToVehicle(v, damage);
}
