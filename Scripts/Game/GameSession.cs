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
	private const int CurrentSaveVersion = 6;

	private readonly SessionContext _ctx;

	public SaveGameState Save => _ctx.Save;

	// Subsystems (foundation for growing the game without a "God object" GameSession).
	internal SessionWorld World { get; }
	internal SessionGarage Garage { get; }
	internal SessionEncounters Encounters { get; }

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

		MigrateSaveIfNeeded();
	}

	public void Persist() => _ctx.Persist();

	// --- World ---
	public void SetCurrentCity(string cityId) => World.SetCurrentCity(cityId);
	public int GetDriverHp() => _ctx.Save.Player.DriverHp;
	public int GetDriverHpMax() => _ctx.Save.Player.DriverHpMax;
	public int GetDriverArmor() => _ctx.Save.Player.DriverArmor;
	public int GetDriverArmorMax() => _ctx.Save.Player.DriverArmorMax;
	public string GetEquippedDriverArmorId() => _ctx.Save.Player.EquippedArmorId;
	public (int missingPoints, int costUsd) ComputeDriverArmorRepairCost() => World.ComputeDriverArmorRepairCost();
	public bool TryRepairDriverArmorToFull(out string error) => World.TryRepairDriverArmorToFull(out error);

	// --- Vehicles / Garage ---
	public IEnumerable<VehicleInstanceState> GetOwnedVehicles() => Garage.GetOwnedVehicles();
	public VehicleInstanceState? GetActiveVehicle() => Garage.GetActiveVehicle();
	public void SetActiveVehicle(string instanceId) => Garage.SetActiveVehicle(instanceId);
	public void UpdateVehicle(VehicleInstanceState updated) => Garage.UpdateVehicle(updated);
	public VehicleInstanceState CreateStarterVehicle(DefDatabase defs, string vehicleDefId = "veh_compact")
		=> Garage.CreateStarterVehicle(defs, vehicleDefId);
	public VehicleInstanceState CreateStarterVehicleIfMissing(DefDatabase defs, string vehicleDefId = "veh_compact")
		=> Garage.CreateStarterVehicleIfMissing(defs, vehicleDefId);
	public VehicleInstanceState CreateStarterVehicleIfMissing(string vehicleDefId = "veh_compact")
		=> Garage.CreateStarterVehicleIfMissing(vehicleDefId);

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
	public bool TryUpgradeArmorPlating(string vehicleInstanceId, DefDatabase defs, out string error)
		=> Garage.TryUpgradeArmorPlating(vehicleInstanceId, defs, out error);
	public bool TryUpgradeTirePlating(string vehicleInstanceId, DefDatabase defs, out string error)
		=> Garage.TryUpgradeTirePlating(vehicleInstanceId, defs, out error);

	// --- Encounters ---
	public bool HasActiveEncounter() => Encounters.HasActiveEncounter();
	public EncounterState? GetCurrentEncounter() => Encounters.GetCurrentEncounter();
	public int GetActiveVehicleHp() => Encounters.GetActiveVehicleHp();
	public bool TryStartArenaEncounter(int tier, out string error) => Encounters.TryStartArenaEncounter(tier, out error);
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
