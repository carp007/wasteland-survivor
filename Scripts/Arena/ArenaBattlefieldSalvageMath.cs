using System;
using System.Linq;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game.Systems;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Lightweight salvage/towing math for the arena post-match prototype.
/// Keeps reward numbers and intactness heuristics out of ArenaRealtimeView.
/// </summary>
internal static class ArenaBattlefieldSalvageMath
{
	public static int ComputeStripScrapReward(VehicleDefinition vdef, VehicleInstanceState vehicle, DefDatabase defs)
	{
		if (vdef == null || vehicle == null || defs == null)
			return 0;

		var weaponCount = vehicle.InstalledWeaponsByMountId?.Count(kv => !string.IsNullOrWhiteSpace(kv.Value?.WeaponId)) ?? 0;
		var intactness = ComputeIntactness01(vdef, vehicle);
		var baseValue = (vdef.BaseMassKg / 250f) + (vdef.StorageCapacityUnits / 3f) + (weaponCount * 1.75f) + (intactness * 4f);
		return Math.Clamp((int)MathF.Round(baseValue), 4, 18);
	}

	public static float ComputeTowMassKg(VehicleDefinition vdef, VehicleInstanceState vehicle, DefDatabase defs)
	{
		if (vdef == null || vehicle == null || defs == null)
			return 0f;
		return MathF.Max(0f, VehicleMassMath.ComputeTotalMassKg(vdef, vehicle, defs));
	}


	public static bool CanHijackAndDrive(VehicleDefinition vdef, VehicleInstanceState vehicle, out string reason)
	{
		reason = string.Empty;
		if (vdef == null || vehicle == null)
		{
			reason = "Vehicle data is missing.";
			return false;
		}

		var tireCount = Math.Max(0, vdef.TireCount);
		var rollingTires = 0;
		for (var i = 0; i < tireCount; i++)
		{
			var hp = vehicle.CurrentTireHp != null && i < vehicle.CurrentTireHp.Length ? vehicle.CurrentTireHp[i] : vdef.BaseTireHp;
			if (hp > 0)
				rollingTires++;
		}

		var minRollingTires = tireCount <= 2 ? 1 : Math.Max(2, (int)MathF.Ceiling(tireCount * 0.5f));
		if (rollingTires < minRollingTires)
		{
			reason = tireCount > 0
				? $"Too many tires are destroyed ({rollingTires}/{tireCount} intact)."
				: "The vehicle cannot roll safely.";
			return false;
		}

		var intactness = ComputeIntactness01(vdef, vehicle);
		if (intactness < 0.32f)
		{
			reason = "The chassis is too badly damaged to drive home.";
			return false;
		}

		var frontHp = GetSectionHpPct(vdef, vehicle, ArmorSection.Front);
		var rearHp = GetSectionHpPct(vdef, vehicle, ArmorSection.Rear);
		if (MathF.Max(frontHp, rearHp) < 0.20f)
		{
			reason = "The vehicle is too structurally compromised to drive.";
			return false;
		}

		return true;
	}

	private static float GetSectionHpPct(VehicleDefinition vdef, VehicleInstanceState vehicle, ArmorSection section)
	{
		if (!vdef.BaseHpBySection.TryGetValue(section, out var maxHp) || maxHp <= 0)
			return 1f;
		var curHp = vehicle.CurrentHpBySection.TryGetValue(section, out var cur) ? cur : maxHp;
		return Math.Clamp((float)curHp / maxHp, 0f, 1f);
	}

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
		return Math.Clamp((hpPct * 0.8f) + (tirePct * 0.2f), 0f, 1f);
	}
}
