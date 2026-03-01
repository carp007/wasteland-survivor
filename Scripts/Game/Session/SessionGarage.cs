// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Session/SessionGarage.cs
// Purpose: Focused session service that mutates SaveGameState via SessionContext.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game.Systems;

namespace WastelandSurvivor.Game.Session;

/// <summary>
/// Player garage: owned vehicles, active vehicle, ammo, repairs, and prototype upgrades.
/// </summary>
internal sealed class SessionGarage
{
    private readonly SessionContext _ctx;

    public SessionGarage(SessionContext ctx)
    {
        _ctx = ctx;
    }

    public IEnumerable<VehicleInstanceState> GetOwnedVehicles()
    {
        var ownedSet = new HashSet<string>(_ctx.Save.Player.OwnedVehicleIds);
        return _ctx.Save.Vehicles.Where(v => ownedSet.Contains(v.InstanceId));
    }

    public VehicleInstanceState? GetActiveVehicle()
    {
        var id = _ctx.Save.Player.ActiveVehicleId;
        if (string.IsNullOrWhiteSpace(id)) return null;
        return _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == id);
    }

    public void SetActiveVehicle(string instanceId)
    {
        if (!_ctx.Save.Player.OwnedVehicleIds.Contains(instanceId))
            return;

        _ctx.Replace(_ctx.Save with { Player = _ctx.Save.Player with { ActiveVehicleId = instanceId } });
    }

    public void UpdateVehicle(VehicleInstanceState updated)
    {
        var vehicles = _ctx.Save.Vehicles.ToList();
        var idx = vehicles.FindIndex(v => v.InstanceId == updated.InstanceId);
        if (idx < 0) return;

        vehicles[idx] = updated;
        _ctx.Replace(_ctx.Save with { Vehicles = vehicles });
    }

    /// <summary>
    /// Creates a starter vehicle instance (default: veh_compact) from definitions.
    /// Adds it to OwnedVehicles and makes it Active.
    /// </summary>
    public VehicleInstanceState CreateStarterVehicle(DefDatabase defs, string vehicleDefId = "veh_compact")
    {
        if (!defs.Vehicles.TryGetValue(vehicleDefId, out var vdef))
            throw new InvalidOperationException($"Unknown vehicle def '{vehicleDefId}'.");

        var instanceId = Guid.NewGuid().ToString("N");

        // Copy base armor
        var armor = new Dictionary<ArmorSection, int>(vdef.BaseArmorBySection);
		var hp = new Dictionary<ArmorSection, int>(vdef.BaseHpBySection);
        var tires = new int[vdef.TireCount];
        for (var i = 0; i < tires.Length; i++)
            tires[i] = vdef.BaseTireArmor;
		var tireHp = new int[vdef.TireCount];
		for (var i = 0; i < tireHp.Length; i++)
			tireHp[i] = vdef.BaseTireHp;

        // Pick default engine/computer if available
        var engineId = PickFirstAllowedEngine(defs, vdef.Class);
        var computerId = defs.Computers.Keys.FirstOrDefault();

        var ammoInv = new Dictionary<string, int>
        {
            [GameBalance.PrimaryAmmoId] = 120, // 9mm (kept for workshop/testing)
            ["ammo_mg_50cal"] = 80,
            ["ammo_missile_std"] = 12,
            ["ammo_mine_std"] = 8
        };

		// Starter loadout (minimal but playable):
		// - Slot 1 (Space): front MG (F1)
		// - Slot 2 (Shift): top turret missile (R1)
		// - Slot 3 (Ctrl): rear mine dropper (B1) when supported by the chassis.
		var installs = new Dictionary<string, InstalledWeaponState>
		{
			["F1"] = new InstalledWeaponState { WeaponId = "wpn_mg_50cal", SelectedAmmoId = "ammo_mg_50cal" },
			["R1"] = new InstalledWeaponState { WeaponId = "wpn_missile", SelectedAmmoId = "ammo_missile_std" }
		};
		if (vdef.MountPoints.Any(m => m.MountId == "B1"))
			installs["B1"] = new InstalledWeaponState { WeaponId = "wpn_mine_dropper", SelectedAmmoId = "ammo_mine_std" };

        var inst = new VehicleInstanceState
        {
            InstanceId = instanceId,
            DefinitionId = vdef.Id,
            CurrentArmorBySection = armor,
            CurrentTireArmor = tires,
			CurrentHpBySection = hp,
			CurrentTireHp = tireHp,
            FuelAmount = 1.0f,
            InstalledEngineId = engineId,
            InstalledComputerId = computerId,
            AmmoInventory = ammoInv,
			InstalledWeaponsByMountId = installs,
            CargoInventory = new Dictionary<string, int>()
        };

        var vehicles = _ctx.Save.Vehicles.ToList();
        vehicles.Add(inst);

        var owned = _ctx.Save.Player.OwnedVehicleIds.ToList();
        owned.Add(inst.InstanceId);

        _ctx.Replace(_ctx.Save with
        {
            Vehicles = vehicles,
            Player = _ctx.Save.Player with
            {
                OwnedVehicleIds = owned,
                ActiveVehicleId = inst.InstanceId
            }
        });

        return inst;
    }

    public VehicleInstanceState CreateStarterVehicleIfMissing(DefDatabase defs, string vehicleDefId = "veh_compact")
    {
        // If we already have vehicles, ensure one is active.
        if (_ctx.Save.Player.OwnedVehicleIds.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(_ctx.Save.Player.ActiveVehicleId))
                return GetActiveVehicle() ?? CreateStarterVehicle(defs, vehicleDefId);

            var firstOwned = _ctx.Save.Player.OwnedVehicleIds.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstOwned))
                return CreateStarterVehicle(defs, vehicleDefId);

            _ctx.Replace(_ctx.Save with { Player = _ctx.Save.Player with { ActiveVehicleId = firstOwned } });
            return GetActiveVehicle() ?? CreateStarterVehicle(defs, vehicleDefId);
        }

        // Otherwise, create a starter.
        return CreateStarterVehicle(defs, vehicleDefId);
    }

    // Convenience overload used by some UI scripts.
    public VehicleInstanceState CreateStarterVehicleIfMissing(string vehicleDefId = "veh_compact")
    {
        var app = App.Instance ?? throw new InvalidOperationException(
            "App.Instance is null. Ensure App is initialized before calling CreateStarterVehicleIfMissing().");
        return CreateStarterVehicleIfMissing(app.Services.Defs, vehicleDefId);
    }

    public int GetActiveVehicleAmmo(string ammoId)
    {
        var v = GetActiveVehicle();
        return v == null ? 0 : AmmoMath.GetAmmo(v, ammoId);
    }

    public bool TryConsumeActiveVehicleAmmo(string ammoId, int count, out string error)
    {
        error = "";
        var v = GetActiveVehicle();
        if (v == null)
        {
            error = "No active vehicle.";
            return false;
        }

        var tmp = v;
        if (!AmmoMath.TryConsumeAmmo(ref tmp, ammoId, count))
        {
            error = "Out of ammo.";
            return false;
        }

        UpdateVehicle(tmp);
        return true;
    }

    public bool TryBuyAmmoForActiveVehicle(string ammoId, int count, int unitCostUsd, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(ammoId))
        {
            error = "Invalid ammo.";
            return false;
        }
        if (count <= 0)
        {
            error = "Invalid quantity.";
            return false;
        }
        if (unitCostUsd < 0)
        {
            error = "Invalid price.";
            return false;
        }

        var v = GetActiveVehicle();
        if (v == null)
        {
            error = "No active vehicle.";
            return false;
        }

        var totalCost = checked(count * unitCostUsd);
        if (_ctx.Save.Player.MoneyUsd < totalCost)
        {
            error = $"Not enough money. Need ${totalCost}.";
            return false;
        }

        var updatedVehicle = AmmoMath.AddAmmo(v, ammoId, count);
        var vehicles = _ctx.Save.Vehicles.ToList();
        var idx = vehicles.FindIndex(x => x.InstanceId == updatedVehicle.InstanceId);
        if (idx < 0)
        {
            error = "Active vehicle missing.";
            return false;
        }
        vehicles[idx] = updatedVehicle;

        _ctx.Replace(_ctx.Save with
        {
            Vehicles = vehicles,
            Player = _ctx.Save.Player with { MoneyUsd = _ctx.Save.Player.MoneyUsd - totalCost }
        });
        _ctx.Status($"Bought ammo: {count}x {ammoId} for ${totalCost}.");
        return true;
    }

    public (int armorMissing, int tireMissing, int totalMissing) ComputeMissingRepairPointsByType(string vehicleInstanceId, DefDatabase defs)
    {
        var veh = _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == vehicleInstanceId);
        if (veh is null) return (0, 0, 0);

        if (!defs.Vehicles.TryGetValue(veh.DefinitionId, out var vdef))
            return (0, 0, 0);

        var (armorMissing, tireMissing) = VehicleRepairMath.ComputeMissingRepairPointsSplit(veh, vdef);
        return (armorMissing, tireMissing, armorMissing + tireMissing);
    }

    public (int missingPoints, int costUsd) ComputeRepairToFullCost(string vehicleInstanceId, DefDatabase defs)
    {
        var veh = _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == vehicleInstanceId);
        if (veh is null) return (0, 0);

        if (!defs.Vehicles.TryGetValue(veh.DefinitionId, out var vdef))
            return (0, 0);

        var missing = VehicleRepairMath.ComputeMissingRepairPoints(veh, vdef);
        return (missing, missing * GameBalance.RepairCostPerPointUsd);
    }

    public bool TryRepairVehicleToFull(string vehicleInstanceId, DefDatabase defs, out string error)
    {
        error = "";
        var veh = _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == vehicleInstanceId);
        if (veh is null)
        {
            error = "Vehicle not found.";
            return false;
        }

        if (!defs.Vehicles.TryGetValue(veh.DefinitionId, out var vdef))
        {
            error = $"Missing vehicle def '{veh.DefinitionId}'.";
            return false;
        }

        var missing = VehicleRepairMath.ComputeMissingRepairPoints(veh, vdef);
        if (missing <= 0)
        {
            error = "No repairs needed.";
            return false;
        }

        var cost = missing * GameBalance.RepairCostPerPointUsd;
        if (_ctx.Save.Player.MoneyUsd < cost)
        {
            error = $"Not enough money. Need ${cost}, have ${_ctx.Save.Player.MoneyUsd}.";
            return false;
        }

        var repaired = VehicleRepairMath.RepairToFull(veh, vdef);
        if (!_ctx.TryMutateVehicleAndPlayer(
                vehicleInstanceId,
                missingVehicleError: "Vehicle not found.",
                vehicleMutator: _ => repaired,
                playerMutator: p => p with { MoneyUsd = p.MoneyUsd - cost },
                out _,
                out error))
        {
            return false;
        }

        var shortId = vehicleInstanceId.Length <= 8 ? vehicleInstanceId : vehicleInstanceId[..8];
        _ctx.Status($"Repair: vehicle {shortId} to full (-${cost}).");
        return true;
    }

    public bool TryPatchArmorWithScrap(string vehicleInstanceId, DefDatabase defs, out string error)
        => TryPatchWithScrap(vehicleInstanceId, defs, PatchTarget.Armor, out error);

    public bool TryPatchTireWithScrap(string vehicleInstanceId, DefDatabase defs, out string error)
        => TryPatchWithScrap(vehicleInstanceId, defs, PatchTarget.Tire, out error);

    private enum PatchTarget { Armor, Tire }

    private bool TryPatchWithScrap(string vehicleInstanceId, DefDatabase defs, PatchTarget target, out string error)
    {
        error = "";
        if (_ctx.Save.CurrentEncounter is { Outcome: null })
        {
            error = "Cannot repair during an active encounter.";
            return false;
        }

        var veh = _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == vehicleInstanceId);
        if (veh is null)
        {
            error = "Vehicle not found.";
            return false;
        }

        if (!defs.Vehicles.TryGetValue(veh.DefinitionId, out var vdef))
        {
            error = $"Missing vehicle def '{veh.DefinitionId}'.";
            return false;
        }

        if (_ctx.Save.Player.Scrap < GameBalance.ScrapRepairCostPerPoint)
        {
            error = $"Not enough scrap. Need {GameBalance.ScrapRepairCostPerPoint}, have {_ctx.Save.Player.Scrap}.";
            return false;
        }

        var updatedVeh = veh;
		var patched = target switch
		{
			// Scrap repairs structural damage (HP) in this prototype step.
			PatchTarget.Armor => VehicleRepairMath.TryPatchSectionHpOnePoint(ref updatedVeh, vdef),
			PatchTarget.Tire => VehicleRepairMath.TryPatchTireHpOnePoint(ref updatedVeh, vdef),
			_ => false
		};

        if (!patched)
        {
			error = target == PatchTarget.Armor ? "No hull repairs needed." : "No tire repairs needed.";
            return false;
        }

        if (!_ctx.TryMutateVehicleAndPlayer(
                vehicleInstanceId,
                missingVehicleError: "Vehicle not found.",
                vehicleMutator: _ => updatedVeh,
                playerMutator: p => p with { Scrap = p.Scrap - GameBalance.ScrapRepairCostPerPoint },
                out _,
                out error))
        {
            return false;
        }

        var shortId = vehicleInstanceId.Length <= 8 ? vehicleInstanceId : vehicleInstanceId[..8];
		var what = target == PatchTarget.Armor ? "hull" : "tire";
		_ctx.Status($"Patch: {what} +1 HP (vehicle {shortId}, -{GameBalance.ScrapRepairCostPerPoint} scrap).");
        return true;
    }

    public bool TryUpgradeArmorPlating(string vehicleInstanceId, DefDatabase defs, out string error)
    {
        error = "";
        if (_ctx.Save.CurrentEncounter is { Outcome: null })
        {
            error = "Cannot upgrade during an active encounter.";
            return false;
        }

        var veh = _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == vehicleInstanceId);
        if (veh is null)
        {
            error = "Vehicle not found.";
            return false;
        }

        if (!defs.Vehicles.TryGetValue(veh.DefinitionId, out var vdef))
        {
            error = $"Missing vehicle def '{veh.DefinitionId}'.";
            return false;
        }

        var curLevel = Math.Max(0, veh.ArmorPlatingLevel);
        if (curLevel >= GameBalance.MaxArmorPlatingLevel)
        {
            error = "Armor plating already at max level.";
            return false;
        }

        var nextLevel = curLevel + 1;
        var cost = GameBalance.GetArmorPlatingUpgradeCost(nextLevel);
        if (_ctx.Save.Player.Scrap < cost)
        {
            error = $"Not enough scrap. Need {cost}, have {_ctx.Save.Player.Scrap}.";
            return false;
        }

        var upgraded = VehicleRepairMath.ApplyArmorPlatingUpgrade(veh, vdef, nextLevel);
        if (!_ctx.TryMutateVehicleAndPlayer(
                vehicleInstanceId,
                missingVehicleError: "Vehicle not found.",
                vehicleMutator: _ => upgraded,
                playerMutator: p => p with { Scrap = p.Scrap - cost },
                out _,
                out error))
        {
            return false;
        }

        var shortId = vehicleInstanceId.Length <= 8 ? vehicleInstanceId : vehicleInstanceId[..8];
        _ctx.Status($"Upgrade: armor plating -> L{nextLevel} (vehicle {shortId}, -{cost} scrap).");
        return true;
    }

    public bool TryUpgradeTirePlating(string vehicleInstanceId, DefDatabase defs, out string error)
    {
        error = "";
        if (_ctx.Save.CurrentEncounter is { Outcome: null })
        {
            error = "Cannot upgrade during an active encounter.";
            return false;
        }

        var veh = _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == vehicleInstanceId);
        if (veh is null)
        {
            error = "Vehicle not found.";
            return false;
        }

        if (!defs.Vehicles.TryGetValue(veh.DefinitionId, out var vdef))
        {
            error = $"Missing vehicle def '{veh.DefinitionId}'.";
            return false;
        }

        var curLevel = Math.Max(0, veh.TirePlatingLevel);
        if (curLevel >= GameBalance.MaxTirePlatingLevel)
        {
            error = "Tire plating already at max level.";
            return false;
        }

        var nextLevel = curLevel + 1;
        var cost = GameBalance.GetTirePlatingUpgradeCost(nextLevel);
        if (_ctx.Save.Player.Scrap < cost)
        {
            error = $"Not enough scrap. Need {cost}, have {_ctx.Save.Player.Scrap}.";
            return false;
        }

        var upgraded = VehicleRepairMath.ApplyTirePlatingUpgrade(veh, vdef, nextLevel);
        if (!_ctx.TryMutateVehicleAndPlayer(
                vehicleInstanceId,
                missingVehicleError: "Vehicle not found.",
                vehicleMutator: _ => upgraded,
                playerMutator: p => p with { Scrap = p.Scrap - cost },
                out _,
                out error))
        {
            return false;
        }

        var shortId = vehicleInstanceId.Length <= 8 ? vehicleInstanceId : vehicleInstanceId[..8];
        _ctx.Status($"Upgrade: tire plating -> L{nextLevel} (vehicle {shortId}, -{cost} scrap).");
        return true;
    }

    private static string? PickFirstAllowedEngine(DefDatabase defs, VehicleClass vehicleClass)
    {
        foreach (var e in defs.Engines.Values)
        {
            if (e.AllowedVehicleClasses != null && e.AllowedVehicleClasses.Contains(vehicleClass))
                return e.Id;
        }
        return null;
    }
}
