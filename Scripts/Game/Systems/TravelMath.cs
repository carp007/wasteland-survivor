// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Systems/TravelMath.cs
// Purpose: Pure overworld travel math (fuel consumption per road leg, refuel pricing). No Godot deps.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game.Systems;

/// <summary>
/// One selectable road leg out of the current city, pre-computed for UI display and travel checks.
/// </summary>
public sealed record TravelOption
{
    public CityDefinition Destination { get; init; } = new();
    public CityRoadDefinition Road { get; init; } = new();
    public float FuelUnitsNeeded { get; init; }
    public int TollUsd { get; init; }
    public bool HasEngine { get; init; }
    public bool HasFuel { get; init; }
    public bool CanAffordToll { get; init; }

    /// <summary>Road Closed barrier is up on this leg (master spec: gates expansion).</summary>
    public bool IsClosed { get; init; }

    /// <summary>The leg's station would top the tank up mid-route on this run.</summary>
    public bool StationStopPlanned { get; init; }
    public float StationTopUpUnits { get; init; }
    public int StationTopUpCostUsd { get; init; }

    /// <summary>
    /// The mandatory station top-up is unaffordable and skipping it would strand the vehicle —
    /// travel is blocked before departure (spec: no strandings).
    /// </summary>
    public bool BlockedAtStation { get; init; }

    public bool CanTravel => !IsClosed && HasEngine && HasFuel && CanAffordToll && !BlockedAtStation;
}

/// <summary>
/// Pre-departure plan for the automatic mid-route fuel stop on a station leg (master spec: side
/// roads to gas stations off the highways). Computed before leaving so travel never strands anyone.
/// </summary>
public sealed record MidRouteStopPlan
{
    /// <summary>The leg has a station and the tank would arrive below the trigger fraction.</summary>
    public bool StopsAtStation { get; init; }

    /// <summary>Units bought at the station — just enough to arrive at the target fraction.</summary>
    public float TopUpUnits { get; init; }

    public int TopUpCostUsd { get; init; }

    /// <summary>Arrival tank if the stop is skipped (station absent or unaffordable).</summary>
    public float ArrivalUnitsWithoutStop { get; init; }

    /// <summary>Arrival tank after the top-up (target fraction of capacity).</summary>
    public float ArrivalUnitsWithStop { get; init; }

    /// <summary>Skipping the stop would land under the stranding floor — block if unaffordable.</summary>
    public bool MustStopToAvoidStranding { get; init; }
}

internal static class TravelMath
{
    // Mid-route station tuning (kept beside the travel math they drive; see GameBalance for the
    // city-side fuel economy knobs).
    /// <summary>Projected arrival tank fraction that triggers an automatic station stop.</summary>
    public const float StationTopUpTriggerFraction = 0.35f;

    /// <summary>The station sells just enough to arrive at this fraction of capacity.</summary>
    public const float StationTopUpTargetFraction = 0.50f;

    /// <summary>Arriving under this fraction counts as stranded — an unaffordable stop blocks travel.</summary>
    public const float StrandedArrivalFraction = 0.10f;

    /// <summary>Baseline consumption (fuel units per 100 km) by fuel type, before mass/efficiency scaling.</summary>
    public static float BaseUnitsPer100Km(FuelType fuel) => fuel switch
    {
        FuelType.Diesel => 7.5f,
        FuelType.Electric => 6f,
        _ => 9f,
    };

    /// <summary>
    /// Fuel units needed to drive a road leg with this vehicle: baseline by fuel type, scaled up with
    /// total mass (cargo/weapons/towing included) and down with engine efficiency.
    /// </summary>
    public static float ComputeLegFuelUnits(
        CityRoadDefinition road,
        VehicleDefinition vdef,
        EngineDefinition engine,
        VehicleInstanceState inst,
        DefDatabase defs)
        => ComputeLegFuelUnits(road, vdef, engine, inst, defs, allVehicles: null);

    /// <summary>
    /// Chain-aware overload: pass the save's vehicle list so a hitched trailer chain (trailer +
    /// its cargo + anything it tows) burns real extra fuel on the leg.
    /// </summary>
    public static float ComputeLegFuelUnits(
        CityRoadDefinition road,
        VehicleDefinition vdef,
        EngineDefinition engine,
        VehicleInstanceState inst,
        DefDatabase defs,
        IEnumerable<VehicleInstanceState>? allVehicles)
    {
        var distanceKm = MathF.Max(0f, road.DistanceKm);
        var basePer100 = BaseUnitsPer100Km(engine.FuelType);

        var totalMassKg = VehicleMassMath.ComputeTotalMassKg(vdef, inst, defs, allVehicles);
        var massFactor = Math.Clamp(totalMassKg / 1400f, 0.6f, 2.5f);

        var efficiency = Math.Clamp(engine.Efficiency, 0.25f, 3f);

        var units = distanceKm / 100f * basePer100 * massFactor / efficiency;
        return MathF.Max(0.1f, units);
    }

    /// <summary>Cost in USD to add fuel units at a city (local price multiplier applies).</summary>
    public static int ComputeRefuelCostUsd(float units, FuelType fuel, float cityPriceMultiplier)
    {
        if (units <= 0f) return 0;
        var unitPrice = GameBalance.GetFuelUnitPriceUsd(fuel);
        var mult = cityPriceMultiplier <= 0f ? 1f : cityPriceMultiplier;
        return (int)MathF.Ceiling(units * unitPrice * mult);
    }

    /// <summary>
    /// Plan the automatic mid-route fuel stop for a leg: no stop unless the leg has a station and
    /// the projected arrival tank falls below <see cref="StationTopUpTriggerFraction"/>; the stop
    /// buys just enough to arrive at <see cref="StationTopUpTargetFraction"/>, priced by the road's
    /// station multiplier. Pure math — the caller applies money/tank changes.
    /// </summary>
    public static MidRouteStopPlan PlanMidRouteStop(
        CityRoadDefinition road,
        VehicleDefinition vdef,
        EngineDefinition engine,
        float currentFuelUnits,
        float legFuelUnits)
    {
        var capacity = MathF.Max(1f, vdef.FuelCapacityUnits);
        var arrivalWithoutStop = currentFuelUnits - legFuelUnits;

        if (!road.HasFuelStation || arrivalWithoutStop >= capacity * StationTopUpTriggerFraction)
            return new MidRouteStopPlan { ArrivalUnitsWithoutStop = arrivalWithoutStop };

        var arrivalWithStop = capacity * StationTopUpTargetFraction;
        var topUpUnits = MathF.Max(0f, arrivalWithStop - arrivalWithoutStop);
        if (topUpUnits <= 0.01f)
            return new MidRouteStopPlan { ArrivalUnitsWithoutStop = arrivalWithoutStop };

        var mult = road.FuelPriceMultiplier <= 0f ? 1f : road.FuelPriceMultiplier;
        return new MidRouteStopPlan
        {
            StopsAtStation = true,
            TopUpUnits = topUpUnits,
            TopUpCostUsd = ComputeRefuelCostUsd(topUpUnits, engine.FuelType, mult),
            ArrivalUnitsWithoutStop = arrivalWithoutStop,
            ArrivalUnitsWithStop = arrivalWithStop,
            MustStopToAvoidStranding = arrivalWithoutStop < capacity * StrandedArrivalFraction,
        };
    }
}
