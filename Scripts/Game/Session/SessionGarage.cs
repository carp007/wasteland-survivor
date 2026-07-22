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


    // --- Trailer hitching (spec pillar 4: towing incl. chained towing; V1 = overworld/garage layer) ---

    /// <summary>The vehicle whose hitch link points directly at this unit, if any.</summary>
    public VehicleInstanceState? FindTowingVehicle(string trailerInstanceId)
    {
        if (string.IsNullOrWhiteSpace(trailerInstanceId)) return null;
        return _ctx.Save.Vehicles.FirstOrDefault(v =>
            string.Equals(v.HitchedTrailerInstanceId, trailerInstanceId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Hitch an owned unit onto the ACTIVE vehicle's tow chain (spec non-negotiable #4:
    /// truck → trailer → salvaged vehicle). New links attach at the chain TAIL. Trailers can sit
    /// anywhere in the chain; a non-trailer vehicle can only ride behind a TRAILER and, having no
    /// hitch of its own, always terminates the chain. City-garage only; the active engine must be
    /// able to pull the WHOLE chain (plus any wreck already on the arena tow line).
    /// </summary>
    public bool TryHitchTrailer(DefDatabase defs, string trailerInstanceId, out string error)
    {
        error = "";
        if (_ctx.Save.CurrentEncounter is { Outcome: null })
        {
            error = "Cannot hitch a trailer during an active encounter.";
            return false;
        }

        var cityId = _ctx.Save.Player.CurrentCityId ?? "";
        if (!defs.Cities.TryGetValue(cityId, out var city) || !city.HasGarage)
        {
            error = "Hitching needs a garage bay — find a city with one.";
            return false;
        }

        var active = GetActiveVehicle();
        if (active is null)
        {
            error = "No active vehicle to hitch to — set one in the Garage.";
            return false;
        }

        if (string.Equals(active.InstanceId, trailerInstanceId, StringComparison.Ordinal))
        {
            error = "A vehicle cannot tow itself.";
            return false;
        }

        if (!_ctx.Save.Player.OwnedVehicleIds.Contains(trailerInstanceId))
        {
            error = "That unit is not owned.";
            return false;
        }

        var unit = _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == trailerInstanceId);
        if (unit is null)
        {
            error = "Unit not found.";
            return false;
        }

        if (!defs.Vehicles.TryGetValue(unit.DefinitionId, out var unitDef))
        {
            error = $"Missing vehicle def '{unit.DefinitionId}'.";
            return false;
        }

        if (FindTowingVehicle(trailerInstanceId) != null)
        {
            error = "That unit is already hitched to another vehicle.";
            return false;
        }

        // Walk to the chain tail (active → trailer → ...), guarding against cycles.
        var tail = active;
        VehicleDefinition? tailDef = null;
        for (var guard = 0; guard < 8 && !string.IsNullOrWhiteSpace(tail.HitchedTrailerInstanceId); guard++)
        {
            if (string.Equals(tail.HitchedTrailerInstanceId, trailerInstanceId, StringComparison.Ordinal))
            {
                error = "That unit is already part of this chain.";
                return false;
            }
            var next = _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == tail.HitchedTrailerInstanceId);
            if (next is null) break;
            tail = next;
        }
        if (!ReferenceEquals(tail, active))
            defs.Vehicles.TryGetValue(tail.DefinitionId, out tailDef);

        // Chain-legality rules.
        var tailIsTrailer = tailDef?.Class == VehicleClass.Trailer;
        if (!ReferenceEquals(tail, active) && !tailIsTrailer)
        {
            error = "The chain already ends in a towed vehicle — nothing hitches behind that.";
            return false;
        }
        if (unitDef.Class != VehicleClass.Trailer && ReferenceEquals(tail, active))
        {
            error = "Vehicles ride behind a trailer, not on the hitch — attach a trailer first.";
            return false;
        }

        var engine = active.InstalledEngineId != null && defs.Engines.TryGetValue(active.InstalledEngineId, out var e) ? e : null;
        if (engine is null)
        {
            error = "The active vehicle has no engine — nothing can pull a chain.";
            return false;
        }

        // Whole-chain capacity check: existing chain + the new unit and everything it carries,
        // on top of any wreck already cached on the arena tow line.
        var unitSelfKg = VehicleMassMath.ComputeBreakdown(unitDef, unit, defs).TotalKg;
        var existingChainKg = VehicleMassMath.ComputeHitchedChainMassKg(active, defs, _ctx.Save.Vehicles);
        var newChainKg = existingChainKg + unitSelfKg + VehicleMassMath.ComputeHitchedChainMassKg(unit, defs, _ctx.Save.Vehicles);
        var cachedTowKg = Math.Max(0f, active.Towing?.TotalTowedMassKgCached ?? 0f);
        var capacityKg = VehicleMassMath.ComputeTowCapacityKg(engine);
        if (newChainKg + cachedTowKg > capacityKg)
        {
            error = $"Too heavy: that chain is {newChainKg + cachedTowKg:0} kg but the {engine.DisplayName} pulls at most {capacityKg:0} kg.";
            return false;
        }

        UpdateVehicle(tail with { HitchedTrailerInstanceId = unit.InstanceId });

        var label = string.IsNullOrWhiteSpace(unitDef.DisplayName) ? unit.DefinitionId : unitDef.DisplayName.Trim();
        _ctx.Status(ReferenceEquals(tail, active)
            ? $"Garage: hitched {label} ({newChainKg:0} kg chain) to the active vehicle."
            : $"Garage: chained {label} behind the trailer ({newChainKg:0} kg total chain).");
        return true;
    }

    /// <summary>Drop a trailer off whatever vehicle is towing it (any city, any time outside combat).</summary>
    public bool TryUnhitchTrailer(string trailerInstanceId, out string error)
    {
        error = "";
        if (_ctx.Save.CurrentEncounter is { Outcome: null })
        {
            error = "Cannot unhitch during an active encounter.";
            return false;
        }

        var towingVehicle = FindTowingVehicle(trailerInstanceId);
        if (towingVehicle is null)
        {
            error = "That trailer is not hitched to anything.";
            return false;
        }

        UpdateVehicle(towingVehicle with { HitchedTrailerInstanceId = null });
        _ctx.Status("Garage: trailer unhitched.");
        return true;
    }

    /// <summary>Remove a vehicle from the list and clear any hitch link pointing at it (no dangling chain ids).</summary>
    private static List<VehicleInstanceState> RemoveVehicleAndDetachHitches(IEnumerable<VehicleInstanceState> vehicles, string removedInstanceId)
        => vehicles
            .Where(v => !string.Equals(v.InstanceId, removedInstanceId, StringComparison.Ordinal))
            .Select(v => string.Equals(v.HitchedTrailerInstanceId, removedInstanceId, StringComparison.Ordinal)
                ? v with { HitchedTrailerInstanceId = null }
                : v)
            .ToList();

    public bool TryRenameOwnedVehicle(string vehicleInstanceId, string? requestedName, out string normalizedName, out string error)
    {
        normalizedName = string.Empty;
        error = string.Empty;

        if (_ctx.Save.CurrentEncounter is { Outcome: null })
        {
            error = "Cannot rename a vehicle during an active encounter.";
            return false;
        }

        if (!_ctx.Save.Player.OwnedVehicleIds.Contains(vehicleInstanceId))
        {
            error = "Vehicle is not owned.";
            return false;
        }

        var vehicle = _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == vehicleInstanceId);
        if (vehicle is null)
        {
            error = "Vehicle not found.";
            return false;
        }

        if (!VehiclePresentation.TryNormalizeCustomName(requestedName, out normalizedName, out error))
            return false;

        if (string.Equals(vehicle.CustomName ?? string.Empty, normalizedName, StringComparison.Ordinal))
            return true;

        var updated = vehicle with { CustomName = normalizedName };
        UpdateVehicle(updated);

        var label = string.IsNullOrWhiteSpace(normalizedName)
            ? updated.DefinitionId
            : normalizedName;
        _ctx.Status(string.IsNullOrWhiteSpace(normalizedName)
            ? $"Garage: reset vehicle name to default ({label})."
            : $"Garage: renamed vehicle to {label}.");
        return true;
    }

    /// <summary>
    /// Creates a starter vehicle instance (default: veh_compact) from definitions.
    /// Adds it to OwnedVehicles and makes it Active.
    /// </summary>
    public VehicleInstanceState CreateStarterVehicle(DefDatabase defs, string vehicleDefId = "veh_compact")
    {
        if (!defs.Vehicles.TryGetValue(vehicleDefId, out _))
            throw new InvalidOperationException($"Unknown vehicle def '{vehicleDefId}'.");

        var instanceId = Guid.NewGuid().ToString("N");
        var inst = VehicleBuildFactory.CreateStarterVehicle(defs, vehicleDefId) with
        {
            InstanceId = instanceId
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
        => TryPatchWithScrap(vehicleInstanceId, defs, PatchTarget.Armor, out _, out error);

    public bool TryPatchTireWithScrap(string vehicleInstanceId, DefDatabase defs, out string error)
        => TryPatchWithScrap(vehicleInstanceId, defs, PatchTarget.Tire, out _, out error);

    public bool TryPatchArmorToAffordableFullWithScrap(string vehicleInstanceId, DefDatabase defs, out int repairedPoints, out string error)
        => TryPatchWithScrap(vehicleInstanceId, defs, PatchTarget.Armor, out repairedPoints, out error, patchAllAffordable: true);

    public bool TryPatchTiresToAffordableFullWithScrap(string vehicleInstanceId, DefDatabase defs, out int repairedPoints, out string error)
        => TryPatchWithScrap(vehicleInstanceId, defs, PatchTarget.Tire, out repairedPoints, out error, patchAllAffordable: true);

    private enum PatchTarget { Armor, Tire }

    private bool TryPatchWithScrap(string vehicleInstanceId, DefDatabase defs, PatchTarget target, out int repairedPoints, out string error, bool patchAllAffordable = false)
    {
        repairedPoints = 0;
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
        var affordablePoints = patchAllAffordable
            ? Math.Max(1, _ctx.Save.Player.Scrap / Math.Max(1, GameBalance.ScrapRepairCostPerPoint))
            : 1;

        for (var i = 0; i < affordablePoints; i++)
        {
			var patched = target switch
			{
				// Scrap repairs structural damage (HP) in this prototype step.
				PatchTarget.Armor => VehicleRepairMath.TryPatchSectionHpOnePoint(ref updatedVeh, vdef),
				PatchTarget.Tire => VehicleRepairMath.TryPatchTireHpOnePoint(ref updatedVeh, vdef),
				_ => false
			};

            if (!patched)
                break;

            repairedPoints++;
            if (!patchAllAffordable)
                break;
        }

        if (repairedPoints <= 0)
        {
			error = target == PatchTarget.Armor ? "No hull repairs needed." : "No tire repairs needed.";
            return false;
        }

        var scrapCost = repairedPoints * GameBalance.ScrapRepairCostPerPoint;
        if (!_ctx.TryMutateVehicleAndPlayer(
                vehicleInstanceId,
                missingVehicleError: "Vehicle not found.",
                vehicleMutator: _ => updatedVeh,
                playerMutator: p => p with { Scrap = p.Scrap - scrapCost },
                out _,
                out error))
        {
            repairedPoints = 0;
            return false;
        }

        var shortId = vehicleInstanceId.Length <= 8 ? vehicleInstanceId : vehicleInstanceId[..8];
		var what = target == PatchTarget.Armor ? "hull" : "tire";
		_ctx.Status($"Patch: {what} +{repairedPoints} HP (vehicle {shortId}, -{scrapCost} scrap).");
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
        // Plating is a fabricated shop part, priced in dollars (scrap stays the field-patch
        // currency). +3/+6/+10 max armor per section with a real weight penalty.
        var cost = GameBalance.GetArmorPlatingUpgradeCost(nextLevel);
        if (_ctx.Save.Player.MoneyUsd < cost)
        {
            error = $"Not enough cash. Plating costs ${cost}, have ${_ctx.Save.Player.MoneyUsd}.";
            return false;
        }

        var upgraded = VehicleRepairMath.ApplyArmorPlatingUpgrade(veh, vdef, nextLevel);
        if (!_ctx.TryMutateVehicleAndPlayer(
                vehicleInstanceId,
                missingVehicleError: "Vehicle not found.",
                vehicleMutator: _ => upgraded,
                playerMutator: p => p with { MoneyUsd = p.MoneyUsd - cost },
                out _,
                out error))
        {
            return false;
        }

        var shortId = vehicleInstanceId.Length <= 8 ? vehicleInstanceId : vehicleInstanceId[..8];
        _ctx.Status($"Upgrade: armor plating -> L{nextLevel} (vehicle {shortId}, -${cost}).");
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
        if (_ctx.Save.Player.MoneyUsd < cost)
        {
            error = $"Not enough cash. Plating costs ${cost}, have ${_ctx.Save.Player.MoneyUsd}.";
            return false;
        }

        var upgraded = VehicleRepairMath.ApplyTirePlatingUpgrade(veh, vdef, nextLevel);
        if (!_ctx.TryMutateVehicleAndPlayer(
                vehicleInstanceId,
                missingVehicleError: "Vehicle not found.",
                vehicleMutator: _ => upgraded,
                playerMutator: p => p with { MoneyUsd = p.MoneyUsd - cost },
                out _,
                out error))
        {
            return false;
        }

        var shortId = vehicleInstanceId.Length <= 8 ? vehicleInstanceId : vehicleInstanceId[..8];
        _ctx.Status($"Upgrade: tire plating -> L{nextLevel} (vehicle {shortId}, -${cost}).");
        return true;
    }

    public int ComputeStripVehicleScrapValue(string vehicleInstanceId, DefDatabase defs)
    {
        var veh = _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == vehicleInstanceId);
        if (veh is null) return 0;
        if (!defs.Vehicles.TryGetValue(veh.DefinitionId, out var vdef)) return 0;
        return VehicleRecoveryValueMath.ComputeStripScrapValue(vdef, veh, defs);
    }

    public int ComputeSellVehicleUsdValue(string vehicleInstanceId, DefDatabase defs)
    {
        var veh = _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == vehicleInstanceId);
        if (veh is null) return 0;
        if (!defs.Vehicles.TryGetValue(veh.DefinitionId, out var vdef)) return 0;
        return VehicleRecoveryValueMath.ComputeSellValueUsd(vdef, veh, defs);
    }

    public bool TryStripOwnedVehicle(string vehicleInstanceId, DefDatabase defs, out int scrapAwarded, out string error)
    {
        scrapAwarded = 0;
        error = "";
        if (_ctx.Save.CurrentEncounter is { Outcome: null })
        {
            error = "Cannot strip a vehicle during an active encounter.";
            return false;
        }

        var player = _ctx.Save.Player;
        if (string.Equals(player.ActiveVehicleId, vehicleInstanceId, StringComparison.Ordinal))
        {
            error = "Set a different active vehicle before stripping this one.";
            return false;
        }

        if (!player.OwnedVehicleIds.Contains(vehicleInstanceId))
        {
            error = "Vehicle is not owned.";
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

        scrapAwarded = VehicleRecoveryValueMath.ComputeStripScrapValue(vdef, veh, defs);
        if (scrapAwarded <= 0)
        {
            error = "No salvage value remains in this vehicle.";
            return false;
        }

        var vehicles = RemoveVehicleAndDetachHitches(_ctx.Save.Vehicles, vehicleInstanceId);
        var owned = _ctx.Save.Player.OwnedVehicleIds.Where(id => !string.Equals(id, vehicleInstanceId, StringComparison.Ordinal)).ToList();
        var label = string.IsNullOrWhiteSpace(vdef.DisplayName) ? veh.DefinitionId : vdef.DisplayName.Trim();

        _ctx.Replace(_ctx.Save with
        {
            Vehicles = vehicles,
            Player = _ctx.Save.Player with
            {
                OwnedVehicleIds = owned,
                Scrap = _ctx.Save.Player.Scrap + scrapAwarded,
            }
        });

        _ctx.Status($"Garage salvage: stripped {label} for +{scrapAwarded} scrap.");
        return true;
    }

    public bool TrySellOwnedVehicle(string vehicleInstanceId, DefDatabase defs, out int salePriceUsd, out string error)
    {
        salePriceUsd = 0;
        error = "";
        if (_ctx.Save.CurrentEncounter is { Outcome: null })
        {
            error = "Cannot sell a vehicle during an active encounter.";
            return false;
        }

        var player = _ctx.Save.Player;
        if (string.Equals(player.ActiveVehicleId, vehicleInstanceId, StringComparison.Ordinal))
        {
            error = "Set a different active vehicle before selling this one.";
            return false;
        }

        if (!player.OwnedVehicleIds.Contains(vehicleInstanceId))
        {
            error = "Vehicle is not owned.";
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

        salePriceUsd = VehicleRecoveryValueMath.ComputeSellValueUsd(vdef, veh, defs);
        if (salePriceUsd <= 0)
        {
            error = "This vehicle has no sale value.";
            return false;
        }

        var vehicles = RemoveVehicleAndDetachHitches(_ctx.Save.Vehicles, vehicleInstanceId);
        var owned = _ctx.Save.Player.OwnedVehicleIds.Where(id => !string.Equals(id, vehicleInstanceId, StringComparison.Ordinal)).ToList();
        var label = string.IsNullOrWhiteSpace(vdef.DisplayName) ? veh.DefinitionId : vdef.DisplayName.Trim();

        _ctx.Replace(_ctx.Save with
        {
            Vehicles = vehicles,
            Player = _ctx.Save.Player with
            {
                OwnedVehicleIds = owned,
                MoneyUsd = _ctx.Save.Player.MoneyUsd + salePriceUsd,
            }
        });

        _ctx.Status($"Garage sale: sold {label} for ${salePriceUsd}.");
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
