// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Systems/VehicleMassMath.cs
// Purpose: Pure or near-pure gameplay math/logic (no Godot nodes), called by session/UI.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game.Systems;

internal static class VehicleMassMath
{
	public readonly struct MassBreakdown
	{
		public readonly float VehicleKg;
		public readonly float WeaponsKg;
		public readonly float AmmoKg;
		public readonly float TowedKg;

		public float TotalKg => VehicleKg + WeaponsKg + AmmoKg + TowedKg;

		public MassBreakdown(float vehicleKg, float weaponsKg, float ammoKg, float towedKg)
		{
			VehicleKg = vehicleKg;
			WeaponsKg = weaponsKg;
			AmmoKg = ammoKg;
			TowedKg = towedKg;
		}
	}

	public static MassBreakdown ComputeBreakdown(VehicleDefinition vdef, VehicleInstanceState inst, DefDatabase defs)
	{
		var vehicleKg = Math.Max(0f, vdef.BaseMassKg);
		var weaponsKg = 0f;
		var ammoKg = 0f;
		var towedKg = 0f;

		// Plating weight (spec: more armor = more weight = less speed). Folded into the
		// vehicle/chassis term so TotalKg -> ComputeSpeedFactor and the Workshop preview
		// pick it up without any signature change for existing consumers.
		vehicleKg += GetArmorPlatingMassKg(inst.ArmorPlatingLevel);
		vehicleKg += GetTirePlatingMassKg(inst.TirePlatingLevel);

		// Weapons
		if (inst.InstalledWeaponsByMountId != null)
		{
			foreach (var kv in inst.InstalledWeaponsByMountId)
			{
				var wId = kv.Value.WeaponId;
				if (string.IsNullOrWhiteSpace(wId)) continue;
				if (defs.Weapons.TryGetValue(wId, out var wdef))
					weaponsKg += Math.Max(0f, wdef.MassKg);
			}
		}

		// Ammo
		if (inst.AmmoInventory != null)
		{
			foreach (var kv in inst.AmmoInventory)
			{
				var ammoId = kv.Key;
				var count = Math.Max(0, kv.Value);
				if (count <= 0) continue;
				if (!defs.Ammo.TryGetValue(ammoId, out var adef)) continue;
				ammoKg += Math.Max(0f, adef.UnitMassKg) * count;
			}
		}

		// Cargo (spec: cargo weight impacts accel/top speed). Folded into the vehicle/chassis
		// component so the MassBreakdown shape stays stable for existing HUD/Workshop consumers;
		// TotalKg (and therefore ComputeSpeedFactor) picks it up automatically.
		if (inst.CargoInventory != null)
		{
			foreach (var kv in inst.CargoInventory)
			{
				var count = Math.Max(0, kv.Value);
				if (count <= 0) continue;
				vehicleKg += GetCargoItemMassKg(kv.Key) * count;
			}
		}

		// Towing (cached)
		if (inst.Towing != null)
			towedKg += Math.Max(0f, inst.Towing.TotalTowedMassKgCached);

		return new MassBreakdown(vehicleKg, weaponsKg, ammoKg, towedKg);
	}

	/// <summary>Per-unit mass of a stowed cargo item (spare tire is the only item today).</summary>
	private static float GetCargoItemMassKg(string cargoId) => cargoId switch
	{
		GameBalance.SpareTireCargoId => GameBalance.SpareTireMassKg,
		_ => GameBalance.DefaultCargoItemMassKg,
	};

	/// <summary>
	/// Extra chassis mass from armor plating, cumulative by level (levels are capped at
	/// GameBalance.MaxArmorPlatingLevel = 3; anything above clamps to the L3 weight).
	/// </summary>
	public static float GetArmorPlatingMassKg(int level) => level switch
	{
		<= 0 => 0f,
		1 => 60f,
		2 => 140f,
		_ => 260f,
	};

	/// <summary>Extra mass from reinforced tires (total across all tires, by plating level).</summary>
	public static float GetTirePlatingMassKg(int level) => level switch
	{
		<= 0 => 0f,
		1 => 30f,
		2 => 70f,
		_ => 120f,
	};

	/// <summary>
	/// Max-armor bonus per section for an armor plating level. Shared home for the
	/// "max armor = base + bonus(level)" formula (replaces the old "base + level"),
	/// so plating meaningfully outpaces its scrap cost.
	/// </summary>
	public static int GetPlatingArmorBonus(int level) => level switch
	{
		<= 0 => 0,
		1 => 3,
		2 => 6,
		_ => 10,
	};

	public static float ComputeTotalMassKg(VehicleDefinition vdef, VehicleInstanceState inst, DefDatabase defs)
	{
		return ComputeBreakdown(vdef, inst, defs).TotalKg;
	}

	// Hard cap on hitch links walked when summing a tow chain (cycle/corrupt-save guard).
	private const int MaxHitchChainLinks = 8;

	/// <summary>
	/// Total mass hanging off a vehicle's overworld hitch: every chained unit's full loadout mass
	/// (chassis + plating + weapons + ammo + cargo + its own cached arena tow), walked link by link
	/// (vehicle → trailer → towed vehicle → ...). Missing/duplicate links end the walk safely.
	/// </summary>
	public static float ComputeHitchedChainMassKg(VehicleInstanceState head, DefDatabase defs, IEnumerable<VehicleInstanceState> allVehicles)
	{
		if (head is null || string.IsNullOrWhiteSpace(head.HitchedTrailerInstanceId) || allVehicles is null)
			return 0f;

		var byId = new Dictionary<string, VehicleInstanceState>(StringComparer.Ordinal);
		foreach (var v in allVehicles)
			byId.TryAdd(v.InstanceId, v);

		var totalKg = 0f;
		var visited = new HashSet<string>(StringComparer.Ordinal) { head.InstanceId };
		var nextId = head.HitchedTrailerInstanceId;
		for (var i = 0; i < MaxHitchChainLinks && !string.IsNullOrWhiteSpace(nextId); i++)
		{
			if (!visited.Add(nextId!) || !byId.TryGetValue(nextId!, out var link))
				break;

			if (defs.Vehicles.TryGetValue(link.DefinitionId, out var linkDef))
				totalKg += ComputeBreakdown(linkDef, link, defs).TotalKg;

			nextId = link.HitchedTrailerInstanceId;
		}

		return totalKg;
	}

	/// <summary>
	/// Chain-aware breakdown: same as the 3-arg overload plus the whole hitched chain folded into
	/// TowedKg. Pass the save's vehicle list so trailers (and anything they tow) weigh the tug down.
	/// </summary>
	public static MassBreakdown ComputeBreakdown(VehicleDefinition vdef, VehicleInstanceState inst, DefDatabase defs, IEnumerable<VehicleInstanceState>? allVehicles)
	{
		var bd = ComputeBreakdown(vdef, inst, defs);
		var chainKg = allVehicles is null ? 0f : ComputeHitchedChainMassKg(inst, defs, allVehicles);
		return chainKg <= 0f
			? bd
			: new MassBreakdown(bd.VehicleKg, bd.WeaponsKg, bd.AmmoKg, bd.TowedKg + chainKg);
	}

	public static float ComputeTotalMassKg(VehicleDefinition vdef, VehicleInstanceState inst, DefDatabase defs, IEnumerable<VehicleInstanceState>? allVehicles)
	{
		return ComputeBreakdown(vdef, inst, defs, allVehicles).TotalKg;
	}

	/// <summary>
	/// How much total mass the installed engine can drag on a tow hitch (spec: towing is gated by
	/// engine power, and chains must stay within it). No engine = no towing.
	/// </summary>
	public static float ComputeTowCapacityKg(EngineDefinition? engine)
	{
		if (engine is null)
			return 0f;
		return MathF.Max(0f, engine.PowerKw) * 20f;
	}

	/// <summary>
	/// Canonical loadout-weight speed penalty (shared by VehiclePawn runtime handling and the Workshop
	/// performance preview so what the menu predicts is exactly what the arena applies).
	/// Returns the multiplier applied to the vehicle's stock top speed.
	/// </summary>
	public static float ComputeSpeedFactor(float baseMassKg, float totalMassKg)
	{
		var baseMass = MathF.Max(1f, baseMassKg);
		var totalMass = MathF.Max(baseMass, totalMassKg);
		var massFactor = Math.Clamp(baseMass / totalMass, 0.45f, 1.15f);
		return Math.Clamp(MathF.Pow(massFactor, 0.25f), 0.65f, 1.15f);
	}

	/// <summary>
	/// Neutral power-to-weight in kW per tonne: a stock compact on its stock engine (~90 kW /
	/// ~1.2 t loadout) sits right at 1.0, so only meaningfully over/under-powered builds move.
	/// </summary>
	public const float ReferencePowerKwPerTonne = 70f;

	/// <summary>
	/// Power-to-weight aware speed penalty (spec: engine power gates what a chassis can haul).
	/// Multiplies the canonical weight penalty by how the installed engine's kW stacks up against
	/// the full chain mass — a diesel truck shrugs off a loaded trailer that makes a compact crawl.
	/// enginePowerKw &lt;= 0 (no engine data) falls back to the weight-only formula, so existing
	/// 2-arg callers keep their exact behavior.
	/// </summary>
	public static float ComputeSpeedFactor(float baseMassKg, float totalMassKg, float enginePowerKw)
	{
		var massOnly = ComputeSpeedFactor(baseMassKg, totalMassKg);
		if (enginePowerKw <= 0f)
			return massOnly;

		var tonnes = MathF.Max(0.25f, MathF.Max(1f, totalMassKg) / 1000f);
		var kwPerTonne = enginePowerKw / tonnes;
		var powerFactor = Math.Clamp(MathF.Pow(kwPerTonne / ReferencePowerKwPerTonne, 0.4f), 0.35f, 1.15f);
		return Math.Clamp(massOnly * powerFactor, 0.25f, 1.25f);
	}
}
