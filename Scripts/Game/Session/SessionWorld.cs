// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Session/SessionWorld.cs
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
/// World/session-level state (city, world flags, etc.).
/// </summary>
internal sealed class SessionWorld
{
    private readonly SessionContext _ctx;

    public SessionWorld(SessionContext ctx)
    {
        _ctx = ctx;
    }

    public void SetCurrentCity(string cityId)
    {
        cityId = (cityId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cityId))
            return;

        _ctx.Replace(_ctx.Save with { Player = _ctx.Save.Player with { CurrentCityId = cityId } });
        _ctx.Status($"City: {cityId}");
    }

    public CityDefinition? GetCurrentCityDef(DefDatabase defs)
    {
        var id = _ctx.Save.Player.CurrentCityId ?? "";
        return defs.Cities.TryGetValue(id, out var city) ? city : null;
    }

    /// <summary>
    /// Road legs available from the current city, with fuel/toll requirements pre-computed against
    /// the active vehicle so the travel UI can show real costs and disable unaffordable routes.
    /// </summary>
    public IReadOnlyList<TravelOption> GetTravelOptions(DefDatabase defs)
    {
        var options = new List<TravelOption>();
        var city = GetCurrentCityDef(defs);
        if (city is null)
            return options;

        var player = _ctx.Save.Player;
        var vehicle = string.IsNullOrWhiteSpace(player.ActiveVehicleId)
            ? null
            : _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == player.ActiveVehicleId);
        var vdef = vehicle != null && defs.Vehicles.TryGetValue(vehicle.DefinitionId, out var vd) ? vd : null;
        var engine = vehicle?.InstalledEngineId != null && defs.Engines.TryGetValue(vehicle.InstalledEngineId, out var e) ? e : null;

        foreach (var road in city.Roads)
        {
            if (!defs.Cities.TryGetValue(road.ToCityId, out var dest))
                continue;

            var fuelNeeded = vehicle != null && vdef != null && engine != null
                ? TravelMath.ComputeLegFuelUnits(road, vdef, engine, vehicle, defs, _ctx.Save.Vehicles)
                : 0f;

            // Mid-route station plan (spec: side roads to gas stations) so the route card can show
            // the projected top-up cost and disable departures that would strand the vehicle.
            var stopPlan = vehicle != null && vdef != null && engine != null
                ? TravelMath.PlanMidRouteStop(road, vdef, engine, vehicle.FuelAmount, fuelNeeded)
                : null;
            var blockedAtStation = stopPlan is { StopsAtStation: true, MustStopToAvoidStranding: true }
                && player.MoneyUsd < GameBalance.RoadTollUsd + stopPlan.TopUpCostUsd;

            options.Add(new TravelOption
            {
                Destination = dest,
                Road = road,
                FuelUnitsNeeded = fuelNeeded,
                TollUsd = GameBalance.RoadTollUsd,
                HasEngine = engine != null,
                HasFuel = vehicle != null && engine != null && vehicle.FuelAmount >= fuelNeeded,
                CanAffordToll = player.MoneyUsd >= GameBalance.RoadTollUsd,
                IsClosed = road.Closed,
                StationStopPlanned = stopPlan?.StopsAtStation == true,
                StationTopUpUnits = stopPlan?.TopUpUnits ?? 0f,
                StationTopUpCostUsd = stopPlan?.TopUpCostUsd ?? 0,
                BlockedAtStation = blockedAtStation,
            });
        }

        return options;
    }

    /// <summary>
    /// Road-graph travel (master spec: roads between cities with encounters). Travel is per-leg to an
    /// adjacent city: burns real fuel from the active vehicle, charges a small toll, and rolls the
    /// road's ambush profile — when one fires, the caller should start the returned encounter tier
    /// before city services resume.
    /// </summary>
    public bool TryTravelTo(DefDatabase defs, string cityId, out int ambushTier, out string error)
        => TryTravelTo(defs, cityId, out ambushTier, out _, out error);

    /// <summary>Set when the most recent quiet leg rolled a scavenge site (spec: roadside salvage).
    /// The caller (CityShell) reads + clears it after a successful travel; kept as a property so
    /// the long-standing two-out-param travel API stays stable for older call sites.</summary>
    public bool LastLegRolledScavengeSite { get; private set; }

    /// <summary>Set when the most recent quiet leg passed a rival salvage crew hauling a wreck.</summary>
    public bool LastLegRolledSalvageCrew { get; private set; }

    /// <summary>Set (>0, the fight tier) when the most recent leg was the active bounty's haunted
    /// road — the named target ambushes ON SIGHT, preempting the ordinary event rolls.</summary>
    public int LastLegBountyTier { get; private set; }

    public bool TryTravelTo(DefDatabase defs, string cityId, out int ambushTier, out bool roadsideMerchant, out string error)
    {
        ambushTier = 0;
        roadsideMerchant = false;
        LastLegRolledScavengeSite = false;
        LastLegRolledSalvageCrew = false;
        LastLegBountyTier = 0;
        error = string.Empty;
        cityId = (cityId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cityId))
        {
            error = "No destination.";
            return false;
        }

        var player = _ctx.Save.Player;
        if (string.Equals(player.CurrentCityId, cityId, StringComparison.OrdinalIgnoreCase))
        {
            error = "You are already in that city.";
            return false;
        }

        if (_ctx.Save.CurrentEncounter is { Outcome: null })
        {
            error = "Finish the active encounter before leaving town.";
            return false;
        }

        var city = GetCurrentCityDef(defs);
        var road = city?.Roads.FirstOrDefault(r => string.Equals(r.ToCityId, cityId, StringComparison.OrdinalIgnoreCase));
        if (city is null || road is null || !defs.Cities.TryGetValue(cityId, out var dest))
        {
            error = "No road runs there from here — travel city to city along the highways.";
            return false;
        }

        // Road Closed barrier (master spec: closures gate world expansion) — refused outright.
        if (road.Closed)
        {
            var reason = string.IsNullOrWhiteSpace(road.ClosedReason)
                ? "The route is barricaded."
                : road.ClosedReason;
            error = $"ROAD CLOSED — {reason}";
            return false;
        }

        var vehicle = string.IsNullOrWhiteSpace(player.ActiveVehicleId)
            ? null
            : _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == player.ActiveVehicleId);
        if (vehicle is null)
        {
            error = "You need an active vehicle to travel — set one in the Garage.";
            return false;
        }

        if (!defs.Vehicles.TryGetValue(vehicle.DefinitionId, out var vdef))
        {
            error = "Active vehicle has an unknown chassis definition.";
            return false;
        }

        var engine = vehicle.InstalledEngineId != null && defs.Engines.TryGetValue(vehicle.InstalledEngineId, out var e) ? e : null;
        if (engine is null)
        {
            error = "Your vehicle has no engine installed — fit one in the Workshop.";
            return false;
        }

        // Chain-aware mass: a hitched trailer (plus its cargo and anything chained behind it) burns
        // real fuel and must stay within the engine's tow capacity to leave town at all.
        var massBreakdown = VehicleMassMath.ComputeBreakdown(vdef, vehicle, defs, _ctx.Save.Vehicles);
        var towCapacityKg = VehicleMassMath.ComputeTowCapacityKg(engine);
        if (massBreakdown.TowedKg > towCapacityKg)
        {
            error = $"Your hitched load is {massBreakdown.TowedKg:0} kg but the {engine.DisplayName} can only pull {towCapacityKg:0} kg — unload cargo, unhitch, or fit a stronger engine.";
            return false;
        }

        var fuelNeeded = TravelMath.ComputeLegFuelUnits(road, vdef, engine, vehicle, defs, _ctx.Save.Vehicles);
        if (vehicle.FuelAmount < fuelNeeded)
        {
            error = $"Not enough fuel: the run to {dest.DisplayName} needs {fuelNeeded:0.0} units, tank has {vehicle.FuelAmount:0.0}. Refuel first.";
            return false;
        }

        var toll = GameBalance.RoadTollUsd;
        if (player.MoneyUsd < toll)
        {
            error = $"Road toll is ${toll} — not enough cash.";
            return false;
        }

        // Mid-route fuel station (spec: side roads to gas stations off the long highways). When the
        // tank would arrive low, an automatic top-up happens at station prices; if the player can't
        // cover a stop they genuinely need, travel is blocked BEFORE departure — no strandings.
        var stopPlan = TravelMath.PlanMidRouteStop(road, vdef, engine, vehicle.FuelAmount, fuelNeeded);
        var stationCostUsd = 0;
        var stationUnits = 0f;
        var arrivalFuel = MathF.Max(0f, vehicle.FuelAmount - fuelNeeded);
        if (stopPlan.StopsAtStation)
        {
            if (player.MoneyUsd >= toll + stopPlan.TopUpCostUsd)
            {
                stationCostUsd = stopPlan.TopUpCostUsd;
                stationUnits = stopPlan.TopUpUnits;
                arrivalFuel = stopPlan.ArrivalUnitsWithStop;
            }
            else if (stopPlan.MustStopToAvoidStranding)
            {
                error = $"The run to {dest.DisplayName} needs a ${stopPlan.TopUpCostUsd} top-up at {StationLabel(road)} and you can't cover it after the ${toll} toll — you'd roll in on fumes. Earn cash or refuel here first.";
                return false;
            }
            // Otherwise the stop is skippable: arrive low but above the stranding floor, no purchase.
        }

        var updatedVehicle = vehicle with { FuelAmount = arrivalFuel };
        var vehicles = _ctx.Save.Vehicles.Select(v => v.InstanceId == vehicle.InstanceId ? updatedVehicle : v).ToList();

        // Freight delivery (spec: cargo/logistics): arriving at the contract's destination pays out
        // and unloads the crates from every chain link. Committed atomically with the arrival.
        var contract = _ctx.Save.ActiveFreightContract;
        var deliveringFreight = contract != null && string.Equals(contract.DestCityId, dest.Id, StringComparison.OrdinalIgnoreCase);
        LastLegDeliveredFreightPayout = deliveringFreight ? contract!.PayoutUsd : 0;
        if (deliveringFreight)
            vehicles = vehicles.Select(v => SessionFreight.StripFreightCargo(v, contract!.CargoId)).ToList();

        _ctx.Replace(_ctx.Save with
        {
            Vehicles = vehicles,
            ActiveFreightContract = deliveringFreight ? null : _ctx.Save.ActiveFreightContract,
            Player = player with
            {
                MoneyUsd = player.MoneyUsd - toll - stationCostUsd + (deliveringFreight ? contract!.PayoutUsd : 0),
                CurrentCityId = dest.Id,
                FreightContractsDelivered = player.FreightContractsDelivered + (deliveringFreight ? 1 : 0),
            }
        });

        // WANTED bounty: the active contract's named target haunts one specific leg — traveling it
        // forces the fight (either direction), preempting the ordinary event rolls below. The
        // caller reads LastLegBountyTier and starts the bounty encounter.
        var bounty = _ctx.Save.ActiveBountyContract;
        if (SessionBounties.BountyMatchesLeg(bounty, city.Id, dest.Id))
        {
            LastLegBountyTier = Math.Clamp(bounty!.Tier, 1, 5);
            var bountyStationNote = stationUnits > 0.01f
                ? $" Topped up {stationUnits:0.0} units at {StationLabel(road)} (-${stationCostUsd})."
                : string.Empty;
            _ctx.Status($"On the road to {dest.DisplayName}: {bounty.TargetName} runs you down — the bounty found YOU.{bountyStationNote}");
            return true;
        }

        // Loaded haulers attract raiders (spec: cargo runs are supposed to be dangerous — this is
        // what "defend the haul" exists for).
        var ambushChance = _ctx.Save.ActiveFreightContract != null || deliveringFreight
            ? Math.Min(0.85, road.AmbushChance + GameBalance.FreightAmbushChanceBonus)
            : road.AmbushChance;
        if (Random.Shared.NextDouble() < ambushChance)
        {
            var min = Math.Max(1, road.AmbushTierMin);
            var max = Math.Max(min, road.AmbushTierMax);
            ambushTier = Random.Shared.Next(min, max + 1);
        }
        else if (Random.Shared.NextDouble() < GameBalance.RoadsideMerchantChance)
        {
            // Spec: roaming merchants roam the overworld and trade through the same store UI.
            roadsideMerchant = true;
        }
        else if (Random.Shared.NextDouble() < GameBalance.ScavengeSiteChance)
        {
            // Spec: side roads lead to abandoned structures containing salvage.
            LastLegRolledScavengeSite = true;
        }
        else if (Random.Shared.NextDouble() < GameBalance.SalvageCrewChance)
        {
            // Spec: AI crews roam the overworld salvaging like players — sometimes you pass one
            // dragging a prize home, and its cargo is negotiable at gunpoint.
            LastLegRolledSalvageCrew = true;
        }

        var stationSuffix = stationUnits > 0.01f
            ? $" Topped up {stationUnits:0.0} units at {StationLabel(road)} (-${stationCostUsd})."
            : string.Empty;
        var haulingSuffix = massBreakdown.TowedKg > 0f
            ? $" Hauling {massBreakdown.TowedKg:0} kg in tow."
            : string.Empty;
        var freightSuffix = deliveringFreight
            ? $" FREIGHT DELIVERED — {contract!.CargoUnits}u {contract.CargoDisplayName}, +${contract.PayoutUsd}."
            : _ctx.Save.ActiveFreightContract != null
                ? " Contract freight aboard — raiders can smell it."
                : string.Empty;
        _ctx.Status(ambushTier > 0
            ? $"On the road to {dest.DisplayName} ({road.DistanceKm:0} km, {fuelNeeded:0.0} fuel)... raiders ahead!{stationSuffix}{haulingSuffix}{freightSuffix}"
            : roadsideMerchant
                ? $"On the road to {dest.DisplayName}: a roaming merchant convoy waves you down.{stationSuffix}{haulingSuffix}{freightSuffix}"
                : $"Arrived in {dest.DisplayName} ({road.DistanceKm:0} km, {fuelNeeded:0.0} fuel burned). The road was quiet.{stationSuffix}{haulingSuffix}{freightSuffix}");
        return true;
    }

    /// <summary>Freight payout completed by the most recent leg (0 = none). Read + cleared by the UI.</summary>
    public int LastLegDeliveredFreightPayout { get; set; }

    /// <summary>Log/error label for a road leg's fuel station ("the Lakeshore 75 Station").</summary>
    private static string StationLabel(CityRoadDefinition road)
        => string.IsNullOrWhiteSpace(road.FuelStationName)
            ? "the roadside station"
            : $"the {road.FuelStationName}";

    /// <summary>Current fuel status of the active vehicle: (current units, capacity units).</summary>
    public (float current, float capacity) GetActiveVehicleFuel(DefDatabase defs)
    {
        var player = _ctx.Save.Player;
        var vehicle = string.IsNullOrWhiteSpace(player.ActiveVehicleId)
            ? null
            : _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == player.ActiveVehicleId);
        if (vehicle is null || !defs.Vehicles.TryGetValue(vehicle.DefinitionId, out var vdef))
            return (0f, 0f);
        return (Math.Clamp(vehicle.FuelAmount, 0f, vdef.FuelCapacityUnits), vdef.FuelCapacityUnits);
    }

    /// <summary>Cost to fill the active vehicle's tank at the current city's station prices.</summary>
    public (float missingUnits, int costUsd) ComputeRefuelToFullCost(DefDatabase defs)
    {
        var player = _ctx.Save.Player;
        var vehicle = string.IsNullOrWhiteSpace(player.ActiveVehicleId)
            ? null
            : _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == player.ActiveVehicleId);
        if (vehicle is null || !defs.Vehicles.TryGetValue(vehicle.DefinitionId, out var vdef))
            return (0f, 0);

        var engine = vehicle.InstalledEngineId != null && defs.Engines.TryGetValue(vehicle.InstalledEngineId, out var e) ? e : null;
        if (engine is null)
            return (0f, 0);

        var missing = MathF.Max(0f, vdef.FuelCapacityUnits - vehicle.FuelAmount);
        var mult = GetCurrentCityDef(defs)?.FuelPriceMultiplier ?? 1f;
        return (missing, TravelMath.ComputeRefuelCostUsd(missing, engine.FuelType, mult));
    }

    /// <summary>Fill the active vehicle's tank at the current city (spec: cities refuel/recharge).</summary>
    public bool TryRefuelActiveVehicleToFull(DefDatabase defs, out int costUsd, out string error)
    {
        costUsd = 0;
        error = "";
        var player = _ctx.Save.Player;
        var vehicle = string.IsNullOrWhiteSpace(player.ActiveVehicleId)
            ? null
            : _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == player.ActiveVehicleId);
        if (vehicle is null)
        {
            error = "No active vehicle to refuel.";
            return false;
        }

        if (!defs.Vehicles.TryGetValue(vehicle.DefinitionId, out var vdef))
        {
            error = "Active vehicle has an unknown chassis definition.";
            return false;
        }

        var engine = vehicle.InstalledEngineId != null && defs.Engines.TryGetValue(vehicle.InstalledEngineId, out var e) ? e : null;
        if (engine is null)
        {
            error = "No engine installed — nothing to fuel.";
            return false;
        }

        var (missing, cost) = ComputeRefuelToFullCost(defs);
        if (missing <= 0.01f)
        {
            error = "Tank is already full.";
            return false;
        }

        if (player.MoneyUsd < cost)
        {
            error = $"Refueling costs ${cost} — not enough cash.";
            return false;
        }

        var updatedVehicle = vehicle with { FuelAmount = vdef.FuelCapacityUnits };
        var vehicles = _ctx.Save.Vehicles.Select(v => v.InstanceId == vehicle.InstanceId ? updatedVehicle : v).ToList();

        _ctx.Replace(_ctx.Save with
        {
            Vehicles = vehicles,
            Player = player with { MoneyUsd = player.MoneyUsd - cost }
        });

        costUsd = cost;
        _ctx.Status($"Refueled {missing:0.0} units (-${cost}).");
        return true;
    }

	/// <summary>
	/// Upload the driver's memory at this city's clone facility (spec pillar: clone respawn anchors
	/// to the last upload). A killed driver's clone wakes in this city from now on.
	/// </summary>
	public bool TryUploadMemory(DefDatabase defs, out string error)
	{
		error = "";
		var city = GetCurrentCityDef(defs);
		if (city is null || !city.HasCloneFacility)
		{
			error = "No clone facility in this city — Detroit, Cleveland, Chicago, and Pittsburgh run them.";
			return false;
		}

		var player = _ctx.Save.Player;
		if (string.Equals(player.LastRespawnCityId, city.Id, StringComparison.OrdinalIgnoreCase))
		{
			error = "Your memory is already anchored at this facility.";
			return false;
		}

		var fee = GameBalance.MemoryUploadFeeUsd;
		if (player.MoneyUsd < fee)
		{
			error = $"Memory upload costs ${fee} — not enough cash.";
			return false;
		}

		_ctx.Replace(_ctx.Save with
		{
			Player = player with
			{
				MoneyUsd = player.MoneyUsd - fee,
				LastRespawnCityId = city.Id,
			}
		});
		_ctx.Status($"Memory uploaded at {city.DisplayName} (-${fee}). New clones decant here.");
		return true;
	}

	public (int missingPoints, int costUsd) ComputeDriverArmorRepairCost()
	{
		var p = _ctx.Save.Player;
		var max = p.DriverArmorMax;
		if (max <= 0) max = GameBalance.DefaultDriverArmorMax;
		var cur = Math.Clamp(p.DriverArmor, 0, max);
		var missing = Math.Max(0, max - cur);
		var cost = missing * GameBalance.DriverArmorRepairCostPerPointUsd;
		return (missing, cost);
	}

	public bool TryRepairDriverArmorToFull(out string error)
	{
		error = "";
		var p = _ctx.Save.Player;
		var max = p.DriverArmorMax;
		if (max <= 0) max = GameBalance.DefaultDriverArmorMax;
		var cur = Math.Clamp(p.DriverArmor, 0, max);
		var missing = Math.Max(0, max - cur);
		if (missing <= 0)
		{
			error = "Armor already full.";
			return false;
		}

		var cost = missing * GameBalance.DriverArmorRepairCostPerPointUsd;
		if (p.MoneyUsd < cost)
		{
			error = $"Not enough money. Need ${cost}.";
			return false;
		}

		_ctx.Replace(_ctx.Save with
		{
			Player = p with
			{
				MoneyUsd = p.MoneyUsd - cost,
				DriverArmorMax = max,
				DriverArmor = max,
			}
		});
		_ctx.Status($"Driver armor repaired to full (-${cost}).");
		return true;
	}

	public bool TryAddMoney(int amountUsd, out int newBalance, out string error)
	{
		error = "";
		newBalance = _ctx.Save.Player.MoneyUsd;
		if (amountUsd <= 0)
		{
			error = "Amount must be greater than 0.";
			return false;
		}

		var current = _ctx.Save.Player.MoneyUsd;
		if (current > int.MaxValue - amountUsd)
		{
			error = "Amount is too large.";
			return false;
		}

		newBalance = current + amountUsd;
		_ctx.Replace(_ctx.Save with
		{
			Player = _ctx.Save.Player with
			{
				MoneyUsd = newBalance,
			}
		});
		_ctx.Status($"Money added: +${amountUsd}. Balance: ${newBalance}.");
		return true;
	}

	// --- Casino (master spec income sources: salvage, arena winnings, "later: casino in some cities") ---

	/// <summary>
	/// Wasteland Wheel spin: validates the city has a casino and the stake is covered, rolls the
	/// outcome (Random.Shared at spin time), and applies the whole result in one atomic save
	/// replace. Returns the rolled wedge index so the UI can land the drawn wheel deterministically
	/// on the exact wedge it already paid out. Comped spins (pit-boss mercy) skip the stake
	/// check/charge but pay out normally.
	/// </summary>
	public bool TryPlaceCasinoWheelBet(DefDatabase defs, int stakeUsd, bool compedSpin, out int wedgeIndex, out int multiplier, out int payoutUsd, out string error)
	{
		wedgeIndex = -1;
		multiplier = 0;
		payoutUsd = 0;
		error = "";

		var city = GetCurrentCityDef(defs);
		if (city is not { HasCasino: true })
		{
			error = "No gambling den in this city — Chicago's lakefront and the Erie road stop run the tables.";
			return false;
		}

		if (stakeUsd <= 0)
		{
			error = "Stake must be greater than 0.";
			return false;
		}

		var player = _ctx.Save.Player;
		if (!compedSpin && player.MoneyUsd < stakeUsd)
		{
			error = $"The wheel takes ${stakeUsd} up front — not enough cash.";
			return false;
		}

		wedgeIndex = CasinoRules.RollWheelWedgeIndex(Random.Shared);
		multiplier = CasinoRules.WheelWedges[wedgeIndex].Multiplier;
		payoutUsd = stakeUsd * multiplier;

		var delta = payoutUsd - (compedSpin ? 0 : stakeUsd);
		if (delta != 0)
		{
			_ctx.Replace(_ctx.Save with
			{
				Player = player with { MoneyUsd = player.MoneyUsd + delta }
			});
		}

		var stakeText = compedSpin ? $"a comped ${stakeUsd}" : $"${stakeUsd}";
		_ctx.Status(multiplier > 0
			? $"Wasteland Wheel: {stakeText} on the spin — hit x{multiplier}, paid ${payoutUsd}."
			: $"Wasteland Wheel: {stakeText} on the spin — bust. The house nods.");
		return true;
	}

	/// <summary>
	/// Highway Dice: flat stake against the house, 2d6 each. Higher total pays double the stake
	/// back, ties push (stake refunded), lower loses the stake. Rolled (Random.Shared) and settled
	/// atomically; dice values are returned so the UI can draw the pips.
	/// </summary>
	public bool TryPlayHighwayDice(DefDatabase defs, out int playerDie1, out int playerDie2, out int houseDie1, out int houseDie2, out int payoutUsd, out string error)
	{
		playerDie1 = playerDie2 = houseDie1 = houseDie2 = 0;
		payoutUsd = 0;
		error = "";

		var city = GetCurrentCityDef(defs);
		if (city is not { HasCasino: true })
		{
			error = "No gambling den in this city — Chicago's lakefront and the Erie road stop run the tables.";
			return false;
		}

		var stake = CasinoRules.DiceStakeUsd;
		var player = _ctx.Save.Player;
		if (player.MoneyUsd < stake)
		{
			error = $"Highway Dice runs a flat ${stake} a roll — not enough cash.";
			return false;
		}

		playerDie1 = Random.Shared.Next(1, 7);
		playerDie2 = Random.Shared.Next(1, 7);
		houseDie1 = Random.Shared.Next(1, 7);
		houseDie2 = Random.Shared.Next(1, 7);
		var playerTotal = playerDie1 + playerDie2;
		var houseTotal = houseDie1 + houseDie2;

		payoutUsd = playerTotal > houseTotal
			? CasinoRules.DiceWinPayoutUsd
			: playerTotal == houseTotal ? stake : 0;

		var delta = payoutUsd - stake;
		if (delta != 0)
		{
			_ctx.Replace(_ctx.Save with
			{
				Player = player with { MoneyUsd = player.MoneyUsd + delta }
			});
		}

		_ctx.Status(playerTotal > houseTotal
			? $"Highway Dice: {playerTotal} beats the house's {houseTotal} — ${CasinoRules.DiceWinPayoutUsd} across the felt."
			: playerTotal == houseTotal
				? $"Highway Dice: both show {playerTotal} — push, stake back."
				: $"Highway Dice: {playerTotal} under the house's {houseTotal} — the felt eats ${stake}.");
		return true;
	}
}

/// <summary>
/// Casino tuning knobs + the single shared odds table for the Wasteland Wheel. The wheel's visual
/// wedges and the roll probabilities come from the same array so the drawn wheel is honest.
///
/// Wheel EV = (4×107×2 + 20×3 + 8×5) / 1000 = 0.956 → ~4.4% house edge (spec target 2–6%).
/// Note the design brief's literal weights (44% ×2 / 8% ×3 / 3% ×5) would give the PLAYER a +27%
/// edge, contradicting its own "house always edges" rule — ×3/×5 weights were trimmed instead.
/// Highway Dice is EV-neutral (ties push), so the wheel carries the house's whole margin.
/// </summary>
public static class CasinoRules
{
	/// <summary>Selectable wheel stakes, in the order shown on the stake row.</summary>
	public static readonly int[] WheelStakesUsd = { 50, 100, 250, 500 };

	/// <summary>Pit-boss mercy: after this many consecutive busts, one comped spin is offered.</summary>
	public const int CompSpinBustThreshold = 5;

	/// <summary>Stake of the comped mercy spin (house money — no charge, payout is real).</summary>
	public const int CompSpinStakeUsd = 50;

	public const int DiceStakeUsd = 100;
	public const int DiceWinPayoutUsd = 200;

	public readonly record struct WheelWedge(int Multiplier, int Weight);

	/// <summary>
	/// Wheel wedges in clockwise draw order; Weight is out of <see cref="TotalWheelWeight"/> (1000)
	/// and doubles as the wedge's visual arc share. Multiplier 0 = bust.
	/// </summary>
	public static readonly WheelWedge[] WheelWedges =
	{
		new(2, 107), new(0, 136), new(3, 20), new(0, 136), new(2, 107),
		new(0, 136), new(5, 8), new(2, 107), new(0, 136), new(2, 107),
	};

	public static readonly int TotalWheelWeight = ComputeTotalWeight();

	private static int ComputeTotalWeight()
	{
		var total = 0;
		foreach (var wedge in WheelWedges)
			total += wedge.Weight;
		return total;
	}

	/// <summary>Weighted roll over the wedge table; returns the landed wedge index.</summary>
	public static int RollWheelWedgeIndex(Random rng)
	{
		var roll = rng.Next(TotalWheelWeight);
		var accumulated = 0;
		for (var i = 0; i < WheelWedges.Length; i++)
		{
			accumulated += WheelWedges[i].Weight;
			if (roll < accumulated)
				return i;
		}

		return WheelWedges.Length - 1;
	}
}
