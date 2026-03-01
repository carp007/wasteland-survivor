// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Systems/VehicleMassMath.cs
// Purpose: Pure or near-pure gameplay math/logic (no Godot nodes), called by session/UI.
// -------------------------------------------------------------------------------------------------
using System;
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

		// Towing (cached)
		if (inst.Towing != null)
			towedKg += Math.Max(0f, inst.Towing.TotalTowedMassKgCached);

		return new MassBreakdown(vehicleKg, weaponsKg, ammoKg, towedKg);
	}

	public static float ComputeTotalMassKg(VehicleDefinition vdef, VehicleInstanceState inst, DefDatabase defs)
	{
		return ComputeBreakdown(vdef, inst, defs).TotalKg;
	}
}
