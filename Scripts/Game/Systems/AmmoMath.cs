using System;
using System.Collections.Generic;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game.Systems;

internal static class AmmoMath
{
	public static int GetAmmo(VehicleInstanceState veh, string ammoId)
	{
		if (veh.AmmoInventory == null) return 0;
		return veh.AmmoInventory.TryGetValue(ammoId, out var cur) ? Math.Max(0, cur) : 0;
	}

	public static bool TryConsumeAmmo(ref VehicleInstanceState veh, string ammoId, int count)
	{
		if (count <= 0) return true;
		var inv = veh.AmmoInventory ?? new Dictionary<string, int>();
		inv.TryGetValue(ammoId, out var cur);
		if (cur < count) return false;

		var next = cur - count;
		var updated = new Dictionary<string, int>(inv);
		if (next <= 0) updated.Remove(ammoId);
		else updated[ammoId] = next;
		veh = veh with { AmmoInventory = updated };
		return true;
	}

	public static VehicleInstanceState AddAmmo(VehicleInstanceState veh, string ammoId, int delta)
	{
		if (delta == 0) return veh;
		var inv = veh.AmmoInventory ?? new Dictionary<string, int>();
		var updated = new Dictionary<string, int>(inv);
		updated.TryGetValue(ammoId, out var cur);
		var next = Math.Max(0, cur + delta);
		if (next <= 0) updated.Remove(ammoId);
		else updated[ammoId] = next;
		return veh with { AmmoInventory = updated };
	}

	public static VehicleInstanceState EnsureAmmoKey(VehicleInstanceState veh, string ammoId, int seedAmount)
	{
		var inv = veh.AmmoInventory ?? new Dictionary<string, int>();
		if (inv.ContainsKey(ammoId)) return veh;
		var updated = new Dictionary<string, int>(inv) { [ammoId] = Math.Max(0, seedAmount) };
		return veh with { AmmoInventory = updated };
	}
}
