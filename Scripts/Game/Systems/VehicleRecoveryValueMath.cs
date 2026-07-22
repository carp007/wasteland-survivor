using System;
using System.Linq;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game.Systems;

/// <summary>
/// Shared valuation helpers for garage-side salvage / sale choices.
/// Keeps reward heuristics out of UI and session services.
/// </summary>
internal static class VehicleRecoveryValueMath
{
	public static int ComputeStripScrapValue(VehicleDefinition vdef, VehicleInstanceState vehicle, DefDatabase defs)
	{
		if (vdef == null || vehicle == null || defs == null)
			return 0;

		var intactness = ComputeIntactness01(vdef, vehicle);
		var weaponCount = vehicle.InstalledWeaponsByMountId?.Count(kv => !string.IsNullOrWhiteSpace(kv.Value?.WeaponId)) ?? 0;
		var ammoStacks = vehicle.AmmoInventory?.Count(kv => kv.Value > 0) ?? 0;
		var cargoStacks = vehicle.CargoInventory?.Count(kv => kv.Value > 0) ?? 0;

		var baseValue =
			(vdef.BaseMassKg / 225f) +
			(vdef.StorageCapacityUnits / 2.4f) +
			(weaponCount * 2.2f) +
			(ammoStacks * 0.8f) +
			(cargoStacks * 0.5f) +
			(intactness * 8.5f);

		return Math.Clamp((int)MathF.Round(baseValue), 6, 32);
	}

	public static int ComputeSellValueUsd(VehicleDefinition vdef, VehicleInstanceState vehicle, DefDatabase defs)
	{
		if (vdef == null || vehicle == null || defs == null)
			return 0;

		var intactness = ComputeIntactness01(vdef, vehicle);
		var weaponCount = vehicle.InstalledWeaponsByMountId?.Count(kv => !string.IsNullOrWhiteSpace(kv.Value?.WeaponId)) ?? 0;
		var ammoCount = vehicle.AmmoInventory?.Values.Where(v => v > 0).Sum() ?? 0;
		var cargoCount = vehicle.CargoInventory?.Values.Where(v => v > 0).Sum() ?? 0;
		var tireBonus = Math.Max(0, vdef.TireCount - CountDestroyedTires(vehicle, vdef));

		var conditionFactor = 0.28f + (0.72f * intactness);
		var basePrice =
			120f +
			(vdef.BaseMassKg * 0.07f) +
			(vdef.StorageCapacityUnits * 22f) +
			(weaponCount * 95f) +
			(Math.Min(80, ammoCount) * 1.5f) +
			(Math.Min(40, cargoCount) * 3.0f) +
			(tireBonus * 14f);

		var sellValue = basePrice * conditionFactor;
		return Math.Clamp((int)MathF.Round(sellValue), 90, 4500);
	}

	public static int ComputeConditionPercent(VehicleDefinition vdef, VehicleInstanceState vehicle)
		=> (int)MathF.Round(ComputeIntactness01(vdef, vehicle) * 100f);

	public static float ComputeIntactness01(VehicleDefinition vdef, VehicleInstanceState vehicle)
	{
		if (vdef == null || vehicle == null)
			return 0f;

		float hpCur = 0f;
		float hpMax = 0f;
		foreach (var kv in vdef.BaseHpBySection)
		{
			hpMax += Math.Max(0, kv.Value);
			if (vehicle.CurrentHpBySection.TryGetValue(kv.Key, out var cur))
				hpCur += Math.Clamp(cur, 0, Math.Max(0, kv.Value));
		}

		var tireCount = Math.Max(0, vdef.TireCount);
		var baseTireHp = Math.Max(1, vdef.BaseTireHp);
		float tireCur = 0f;
		float tireMax = tireCount * baseTireHp;
		for (var i = 0; i < tireCount; i++)
		{
			var cur = (vehicle.CurrentTireHp != null && i < vehicle.CurrentTireHp.Length) ? vehicle.CurrentTireHp[i] : baseTireHp;
			tireCur += Math.Clamp(cur, 0, baseTireHp);
		}

		var hpPct = hpMax <= 0.01f ? 1f : hpCur / hpMax;
		var tirePct = tireMax <= 0.01f ? 1f : tireCur / tireMax;
		return Math.Clamp((hpPct * 0.82f) + (tirePct * 0.18f), 0f, 1f);
	}

	public static int CountDestroyedTires(VehicleInstanceState vehicle, VehicleDefinition vdef)
	{
		if (vehicle == null || vdef == null)
			return 0;

		var destroyed = 0;
		var tireCount = Math.Max(0, vdef.TireCount);
		for (var i = 0; i < tireCount; i++)
		{
			var cur = (vehicle.CurrentTireHp != null && i < vehicle.CurrentTireHp.Length) ? vehicle.CurrentTireHp[i] : vdef.BaseTireHp;
			if (cur <= 0)
				destroyed++;
		}
		return destroyed;
	}
}
