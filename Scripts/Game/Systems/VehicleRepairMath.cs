// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Systems/VehicleRepairMath.cs
// Purpose: Pure or near-pure gameplay math/logic (no Godot nodes), called by session/UI.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game.Systems;

internal static class VehicleRepairMath
{
	public static int ComputeMissingRepairPoints(VehicleInstanceState veh, VehicleDefinition vdef)
	{
		var missing = 0;

		var armorLevel = Math.Max(0, veh.ArmorPlatingLevel);
		var armor = veh.CurrentArmorBySection ?? new Dictionary<ArmorSection, int>();
		foreach (var kv in vdef.BaseArmorBySection)
		{
			var maxVal = Math.Max(0, kv.Value + armorLevel);
			armor.TryGetValue(kv.Key, out var cur);
			var curVal = Math.Max(0, cur);
			missing += Math.Max(0, maxVal - curVal);
		}

		// Structural HP per section
		var hp = veh.CurrentHpBySection ?? new Dictionary<ArmorSection, int>();
		foreach (var kv in vdef.BaseHpBySection)
		{
			var maxVal = Math.Max(0, kv.Value);
			hp.TryGetValue(kv.Key, out var cur);
			var curVal = Math.Max(0, cur);
			missing += Math.Max(0, maxVal - curVal);
		}

		var tireLevel = Math.Max(0, veh.TirePlatingLevel);
		var maxTire = Math.Max(0, vdef.BaseTireArmor + tireLevel);
		var tireCount = Math.Max(0, vdef.TireCount);
		var tires = veh.CurrentTireArmor ?? Array.Empty<int>();
		for (var i = 0; i < tireCount; i++)
		{
			var cur = (i < tires.Length) ? Math.Max(0, tires[i]) : 0;
			missing += Math.Max(0, maxTire - cur);
		}

		// Tire HP
		var maxTireHp = Math.Max(0, vdef.BaseTireHp);
		var tiresHp = veh.CurrentTireHp ?? Array.Empty<int>();
		for (var i = 0; i < tireCount; i++)
		{
			var cur = (i < tiresHp.Length) ? Math.Max(0, tiresHp[i]) : 0;
			missing += Math.Max(0, maxTireHp - cur);
		}

		return missing;
	}

	public static (int armorMissing, int tireMissing) ComputeMissingRepairPointsSplit(VehicleInstanceState veh, VehicleDefinition vdef)
	{
		var armorMissing = 0;
		var tireMissing = 0;

		var armorLevel = Math.Max(0, veh.ArmorPlatingLevel);
		var armor = veh.CurrentArmorBySection ?? new Dictionary<ArmorSection, int>();
		foreach (var kv in vdef.BaseArmorBySection)
		{
			var maxVal = Math.Max(0, kv.Value + armorLevel);
			armor.TryGetValue(kv.Key, out var cur);
			var curVal = Math.Max(0, cur);
			armorMissing += Math.Max(0, maxVal - curVal);
		}

		// Structural HP per section counts toward "armorMissing" bucket (keeps UI stable for now).
		var hp = veh.CurrentHpBySection ?? new Dictionary<ArmorSection, int>();
		foreach (var kv in vdef.BaseHpBySection)
		{
			var maxVal = Math.Max(0, kv.Value);
			hp.TryGetValue(kv.Key, out var cur);
			var curVal = Math.Max(0, cur);
			armorMissing += Math.Max(0, maxVal - curVal);
		}

		var tireLevel = Math.Max(0, veh.TirePlatingLevel);
		var maxTire = Math.Max(0, vdef.BaseTireArmor + tireLevel);
		var tireCount = Math.Max(0, vdef.TireCount);
		var tires = veh.CurrentTireArmor ?? Array.Empty<int>();
		for (var i = 0; i < tireCount; i++)
		{
			var cur = (i < tires.Length) ? Math.Max(0, tires[i]) : 0;
			tireMissing += Math.Max(0, maxTire - cur);
		}

		// Tire HP counts toward "tireMissing" bucket.
		var maxTireHp = Math.Max(0, vdef.BaseTireHp);
		var tiresHp = veh.CurrentTireHp ?? Array.Empty<int>();
		for (var i = 0; i < tireCount; i++)
		{
			var cur = (i < tiresHp.Length) ? Math.Max(0, tiresHp[i]) : 0;
			tireMissing += Math.Max(0, maxTireHp - cur);
		}

		return (armorMissing, tireMissing);
	}

	public static VehicleInstanceState RepairToFull(VehicleInstanceState veh, VehicleDefinition vdef)
	{
		var armor = veh.CurrentArmorBySection is not null
			? new Dictionary<ArmorSection, int>(veh.CurrentArmorBySection)
			: new Dictionary<ArmorSection, int>();

		var hp = veh.CurrentHpBySection is not null
			? new Dictionary<ArmorSection, int>(veh.CurrentHpBySection)
			: new Dictionary<ArmorSection, int>();

		var armorLevel = Math.Max(0, veh.ArmorPlatingLevel);
		foreach (var kv in vdef.BaseArmorBySection)
		{
			var max = Math.Max(0, kv.Value + armorLevel);
			armor[kv.Key] = max;
		}

		foreach (var kv in vdef.BaseHpBySection)
		{
			var max = Math.Max(0, kv.Value);
			hp[kv.Key] = max;
		}

		var tireLevel = Math.Max(0, veh.TirePlatingLevel);
		var tireCount = Math.Max(0, vdef.TireCount);
		var tires = new int[tireCount];
		for (var i = 0; i < tires.Length; i++)
			tires[i] = Math.Max(0, vdef.BaseTireArmor + tireLevel);

		var tiresHp = new int[tireCount];
		for (var i = 0; i < tiresHp.Length; i++)
			tiresHp[i] = Math.Max(0, vdef.BaseTireHp);

		return veh with
		{
			CurrentArmorBySection = armor,
			CurrentHpBySection = hp,
			CurrentTireArmor = tires,
			CurrentTireHp = tiresHp
		};
	}

	public static VehicleInstanceState ApplyArmorPlatingUpgrade(VehicleInstanceState veh, VehicleDefinition vdef, int nextLevel)
	{
		var armor = veh.CurrentArmorBySection is not null
			? new Dictionary<ArmorSection, int>(veh.CurrentArmorBySection)
			: new Dictionary<ArmorSection, int>();

		foreach (var kv in vdef.BaseArmorBySection)
		{
			var max = Math.Max(0, kv.Value + nextLevel);
			armor.TryGetValue(kv.Key, out var curRaw);
			var cur = Math.Max(0, curRaw);
			armor[kv.Key] = Math.Min(max, cur + 1);
		}

		return veh with
		{
			ArmorPlatingLevel = nextLevel,
			CurrentArmorBySection = armor
		};
	}

	public static VehicleInstanceState ApplyTirePlatingUpgrade(VehicleInstanceState veh, VehicleDefinition vdef, int nextLevel)
	{
		var tireCount = Math.Max(0, vdef.TireCount);
		var maxTire = Math.Max(0, vdef.BaseTireArmor + nextLevel);
		var tires = veh.CurrentTireArmor is { Length: > 0 } ? (int[])veh.CurrentTireArmor.Clone() : new int[tireCount];

		if (tires.Length != tireCount)
		{
			var resized = new int[tireCount];
			var copyLen = Math.Min(tireCount, tires.Length);
			for (var i = 0; i < copyLen; i++)
				resized[i] = tires[i];
			tires = resized;
		}

		for (var i = 0; i < tireCount; i++)
		{
			var cur = Math.Max(0, tires[i]);
			tires[i] = Math.Min(maxTire, cur + 1);
		}

		return veh with
		{
			TirePlatingLevel = nextLevel,
			CurrentTireArmor = tires
		};
	}

	public static bool TryPatchArmorOnePoint(ref VehicleInstanceState veh, VehicleDefinition vdef)
	{
		var armor = veh.CurrentArmorBySection is not null
			? new Dictionary<ArmorSection, int>(veh.CurrentArmorBySection)
			: new Dictionary<ArmorSection, int>();

		var armorLevel = Math.Max(0, veh.ArmorPlatingLevel);

		var order = new[]
		{
			ArmorSection.Front,
			ArmorSection.Left,
			ArmorSection.Right,
			ArmorSection.Rear,
			ArmorSection.Top,
			ArmorSection.Undercarriage
		};

		foreach (var s in order)
		{
			if (!vdef.BaseArmorBySection.TryGetValue(s, out var baseValRaw))
				continue;

			var maxVal = Math.Max(0, baseValRaw + armorLevel);
			armor.TryGetValue(s, out var curRaw);
			var cur = Math.Max(0, curRaw);
			if (cur >= maxVal) continue;

			armor[s] = cur + 1;
			veh = veh with { CurrentArmorBySection = armor };
			return true;
		}

		return false;
	}

	public static bool TryPatchTireOnePoint(ref VehicleInstanceState veh, VehicleDefinition vdef)
	{
		var tireCount = Math.Max(0, vdef.TireCount);
		var tireLevel = Math.Max(0, veh.TirePlatingLevel);
		var maxTire = Math.Max(0, vdef.BaseTireArmor + tireLevel);
		if (tireCount <= 0 || maxTire <= 0) return false;

		var tires = veh.CurrentTireArmor is { Length: > 0 }
			? (int[])veh.CurrentTireArmor.Clone()
			: new int[tireCount];

		if (tires.Length != tireCount)
		{
			var resized = new int[tireCount];
			var copyLen = Math.Min(tireCount, tires.Length);
			for (var i = 0; i < copyLen; i++)
				resized[i] = tires[i];
			tires = resized;
		}

		for (var i = 0; i < tireCount; i++)
		{
			var cur = Math.Max(0, tires[i]);
			if (cur >= maxTire) continue;
			tires[i] = cur + 1;
			veh = veh with { CurrentTireArmor = tires };
			return true;
		}

		return false;
	}

	public static bool TryPatchSectionHpOnePoint(ref VehicleInstanceState veh, VehicleDefinition vdef)
	{
		var hp = veh.CurrentHpBySection is not null
			? new Dictionary<ArmorSection, int>(veh.CurrentHpBySection)
			: new Dictionary<ArmorSection, int>();

		var order = new[]
		{
			ArmorSection.Front,
			ArmorSection.Left,
			ArmorSection.Right,
			ArmorSection.Rear,
			ArmorSection.Top,
			ArmorSection.Undercarriage
		};

		foreach (var s in order)
		{
			if (!vdef.BaseHpBySection.TryGetValue(s, out var maxValRaw))
				continue;
			var maxVal = Math.Max(0, maxValRaw);
			hp.TryGetValue(s, out var curRaw);
			var cur = Math.Max(0, curRaw);
			if (cur >= maxVal) continue;

			hp[s] = cur + 1;
			veh = veh with { CurrentHpBySection = hp };
			return true;
		}

		return false;
	}

	public static bool TryPatchTireHpOnePoint(ref VehicleInstanceState veh, VehicleDefinition vdef)
	{
		var tireCount = Math.Max(0, vdef.TireCount);
		var maxTireHp = Math.Max(0, vdef.BaseTireHp);
		if (tireCount <= 0 || maxTireHp <= 0) return false;

		var tires = veh.CurrentTireHp is { Length: > 0 }
			? (int[])veh.CurrentTireHp.Clone()
			: new int[tireCount];

		if (tires.Length != tireCount)
		{
			var resized = new int[tireCount];
			var copyLen = Math.Min(tireCount, tires.Length);
			for (var i = 0; i < copyLen; i++)
				resized[i] = tires[i];
			tires = resized;
		}

		for (var i = 0; i < tireCount; i++)
		{
			var cur = Math.Max(0, tires[i]);
			if (cur >= maxTireHp) continue;
			tires[i] = cur + 1;
			veh = veh with { CurrentTireHp = tires };
			return true;
		}

		return false;
	}
}
