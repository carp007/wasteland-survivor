// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Systems/SaveMigration.cs
// Purpose: Save migration steps for backward-compatible changes to SaveGameState (record-based, immutable "with" updates).
// -------------------------------------------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game;

namespace WastelandSurvivor.Game.Systems;

internal static class SaveMigration
{
	/// <summary>
	/// Apply backward-compatible migrations. Returns true if the save was changed.
	/// </summary>
	public static bool TryMigrateToVersion(
		ref SaveGameState save,
		int targetVersion,
		string primaryAmmoId,
		int seedAmmoIfMissing,
		DefDatabase? defs = null)
	{
		if (save.Version >= targetVersion)
			return false;

		// v3: ensure vehicles have an ammo inventory initialized and primary ammo key exists.
		var vehicles = save.Vehicles.ToList();
		for (var i = 0; i < vehicles.Count; i++)
		{
			var v = vehicles[i];
			var inv = v.AmmoInventory ?? new Dictionary<string, int>();
			// Only seed if the key is missing entirely (older saves), not when the player has legitimately spent ammo.
			if (!inv.ContainsKey(primaryAmmoId))
			{
				var updated = new Dictionary<string, int>(inv)
				{
					[primaryAmmoId] = seedAmmoIfMissing
				};
				vehicles[i] = v with { AmmoInventory = updated };
			}
		}

		// v4+: ensure player has driver stats initialized + clamped.
		var p = save.Player;
		var hpMax = p.DriverHpMax;
		if (hpMax <= 0)
			hpMax = GameBalance.DefaultDriverHpMax;
		var hpCur = p.DriverHp;
		if (hpCur < 0) hpCur = 0;
		if (hpCur > hpMax) hpCur = hpMax;
			// Prototype safeguard: avoid "dead on load" soft-locks.
			// Until we have a proper healing/medbay flow, treat 0 HP as "recover to full".
			if (hpCur == 0) hpCur = hpMax;

		var armorId = string.IsNullOrWhiteSpace(p.EquippedArmorId)
			? GameBalance.DefaultDriverArmorId
			: p.EquippedArmorId;

		var maxArmor = p.DriverArmorMax;
		if (maxArmor <= 0)
			maxArmor = GameBalance.DefaultDriverArmorMax;
		// If we have defs, trust the equipped armor max.
		if (defs != null && defs.Armors.TryGetValue(armorId, out var adef) && adef.MaxArmorPoints > 0)
			maxArmor = adef.MaxArmorPoints;
		var curArmor = p.DriverArmor;
		if (curArmor < 0) curArmor = 0;
		if (curArmor > maxArmor) curArmor = maxArmor;

		p = p with
		{
			DriverHpMax = hpMax,
			DriverHp = hpCur,
			EquippedArmorId = armorId,
			DriverArmorMax = maxArmor,
			DriverArmor = curArmor
		};

		// v6: ensure vehicles have structural HP initialized.
		if (defs != null && save.Vehicles.Count > 0)
		{
			for (var i = 0; i < vehicles.Count; i++)
			{
				var v = vehicles[i];
				if (!defs.Vehicles.TryGetValue(v.DefinitionId, out var vdef))
					continue;

				var hpBySection = v.CurrentHpBySection is { Count: > 0 }
					? new Dictionary<ArmorSection, int>(v.CurrentHpBySection)
					: new Dictionary<ArmorSection, int>();
				foreach (var kv in vdef.BaseHpBySection)
				{
					var max = System.Math.Max(0, kv.Value);
					hpBySection.TryGetValue(kv.Key, out var curRaw);
					var cur = System.Math.Clamp(curRaw, 0, max);
					if (!hpBySection.ContainsKey(kv.Key))
						cur = max;
					hpBySection[kv.Key] = cur;
				}

				var tireCount = System.Math.Max(0, vdef.TireCount);
				var tireHp = v.CurrentTireHp is { Length: > 0 } ? (int[])v.CurrentTireHp.Clone() : new int[tireCount];
				if (tireHp.Length != tireCount)
				{
					var resized = new int[tireCount];
					var copyLen = System.Math.Min(tireCount, tireHp.Length);
					for (var t = 0; t < copyLen; t++) resized[t] = tireHp[t];
					tireHp = resized;
				}
				for (var t = 0; t < tireCount; t++)
				{
					var max = System.Math.Max(0, vdef.BaseTireHp);
					var cur = System.Math.Clamp(tireHp[t], 0, max);
					if (cur == 0) cur = max;
					tireHp[t] = cur;
				}

				vehicles[i] = v with
				{
					CurrentHpBySection = hpBySection,
					CurrentTireHp = tireHp
				};
			}
		}

		

		// v7: seed secondary ammo types when corresponding weapons are installed, and add a rear mine dropper
		// to starter-style vehicles that have a B1 mount but no weapon installed there.
		if (defs != null && vehicles.Count > 0)
		{
			for (var i = 0; i < vehicles.Count; i++)
			{
				var v = vehicles[i];
				if (!defs.Vehicles.TryGetValue(v.DefinitionId, out var vdef))
					continue;

				var changed = false;
				var installs = v.InstalledWeaponsByMountId is { Count: > 0 }
					? new Dictionary<string, InstalledWeaponState>(v.InstalledWeaponsByMountId)
					: new Dictionary<string, InstalledWeaponState>();

				// Add a mine dropper to B1 only if the chassis supports B1 and it's currently empty.
				if (vdef.MountPoints.Any(m => m.MountId == "B1") && !installs.ContainsKey("B1"))
				{
					installs["B1"] = new InstalledWeaponState { WeaponId = "wpn_mine_dropper", SelectedAmmoId = "ammo_mine_std" };
					changed = true;
				}

				// Seed ammo for installed weapons if missing.
				var inv = v.AmmoInventory ?? new Dictionary<string, int>();
				var invUpdated = new Dictionary<string, int>(inv);
				foreach (var kv in installs)
				{
					var ammoId = kv.Value.SelectedAmmoId;
					if (string.IsNullOrWhiteSpace(ammoId)) continue;
					if (invUpdated.ContainsKey(ammoId!)) continue;

					// Conservative seed amounts so this doesn't feel like "free loot".
					var seed = ammoId switch
					{
						"ammo_missile_std" => 6,
						"ammo_mine_std" => 4,
						_ => 0
					};
					if (seed > 0)
					{
						invUpdated[ammoId!] = seed;
						changed = true;
					}
				}

				if (changed)
					vehicles[i] = v with { InstalledWeaponsByMountId = installs, AmmoInventory = invUpdated };
			}
		}
save = save with { Vehicles = vehicles, Player = p, Version = targetVersion };
		return true;
	}
}
