// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Session/SessionStore.cs
// Purpose: City parts store (master spec: each city has a Store). Buy/sell weapons, engines, and
//          targeting computers into the player's PartsInventory; the Workshop installs owned parts.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game.Systems;

namespace WastelandSurvivor.Game.Session;

internal sealed class SessionStore
{
	// Buy-back rate for selling parts to the store.
	private const float SellRate = 0.55f;

	// Trade-in rate for the currently worn driver vest when buying a new one at the outfitter.
	private const float DriverArmorTradeInRate = 0.40f;

	private readonly SessionContext _ctx;

	public SessionStore(SessionContext ctx)
	{
		_ctx = ctx;
	}

	public int GetOwnedPartCount(string partId)
	{
		var inv = _ctx.Save.Player.PartsInventory;
		return inv != null && inv.TryGetValue(partId, out var n) ? n : 0;
	}

	/// <summary>Resolve a part's store price from the defs (0 = not sold).</summary>
	public static int GetPartPrice(DefDatabase defs, string partId)
	{
		if (defs.Weapons.TryGetValue(partId, out var w)) return Math.Max(0, w.PriceUsd);
		if (defs.Engines.TryGetValue(partId, out var e)) return Math.Max(0, e.PriceUsd);
		if (defs.Computers.TryGetValue(partId, out var c)) return Math.Max(0, c.PriceUsd);
		return 0;
	}

	public static int GetPartSellValue(DefDatabase defs, string partId)
		=> (int)MathF.Round(GetPartPrice(defs, partId) * SellRate);

	public bool TryBuyPart(DefDatabase defs, string partId, out string error)
	{
		error = string.Empty;
		var price = GetPartPrice(defs, partId);
		if (price <= 0)
		{
			error = "That part is not sold here.";
			return false;
		}

		var player = _ctx.Save.Player;
		if (player.MoneyUsd < price)
		{
			error = $"Not enough cash (need ${price}).";
			return false;
		}

		var parts = new Dictionary<string, int>(player.PartsInventory ?? new Dictionary<string, int>());
		parts.TryGetValue(partId, out var owned);
		parts[partId] = owned + 1;

		_ctx.Replace(_ctx.Save with
		{
			Player = player with
			{
				MoneyUsd = player.MoneyUsd - price,
				PartsInventory = parts,
			}
		});
		return true;
	}

	public bool TrySellPart(DefDatabase defs, string partId, out string error)
	{
		error = string.Empty;
		var player = _ctx.Save.Player;
		var parts = new Dictionary<string, int>(player.PartsInventory ?? new Dictionary<string, int>());
		if (!parts.TryGetValue(partId, out var owned) || owned <= 0)
		{
			error = "You don't have a spare one of those to sell.";
			return false;
		}

		var value = GetPartSellValue(defs, partId);
		if (owned <= 1)
			parts.Remove(partId);
		else
			parts[partId] = owned - 1;

		_ctx.Replace(_ctx.Save with
		{
			Player = player with
			{
				MoneyUsd = player.MoneyUsd + value,
				PartsInventory = parts,
			}
		});
		return true;
	}

	/// <summary>
	/// Buy a bare vehicle off the dealership floor (spec: each city has a Store; this is the only
	/// way to buy a whole vehicle). The build is chassis-only — stock engine for the class, no
	/// weapons, no ammo — so the Workshop outfits it from owned parts afterwards. Appends the new
	/// instance to the save, marks it owned, and makes it active only if the player has no active
	/// vehicle yet.
	/// </summary>
	public bool TryBuyVehicle(DefDatabase defs, string vehicleDefId, out string error)
	{
		error = string.Empty;
		if (string.IsNullOrWhiteSpace(vehicleDefId)
			|| !defs.Vehicles.TryGetValue(vehicleDefId, out var vdef)
			|| vdef.PriceUsd <= 0)
		{
			error = "That vehicle is not sold here.";
			return false;
		}

		var player = _ctx.Save.Player;
		var price = vdef.PriceUsd;
		if (player.MoneyUsd < price)
		{
			error = $"Not enough cash (need ${price}).";
			return false;
		}

		// Bare showroom build: no preset lookup — an empty preset gives chassis + stock engine
		// (VehicleBuildFactory resolves the first engine that fits the class) and no weapons.
		var preset = new VehicleBuildPreset
		{
			PresetId = $"dealer_{vehicleDefId}",
			VehicleDefinitionId = vehicleDefId,
		};
		var inst = VehicleBuildFactory.CreateVehicleInstance(defs, preset);

		var vehicles = _ctx.Save.Vehicles.ToList();
		vehicles.Add(inst);

		var owned = player.OwnedVehicleIds.ToList();
		owned.Add(inst.InstanceId);

		_ctx.Replace(_ctx.Save with
		{
			Vehicles = vehicles,
			Player = player with
			{
				MoneyUsd = player.MoneyUsd - price,
				OwnedVehicleIds = owned,
				// A trailer can never be the ACTIVE vehicle (it has no engine to drive out with).
				ActiveVehicleId = string.IsNullOrWhiteSpace(player.ActiveVehicleId) && vdef.Class != VehicleClass.Trailer
					? inst.InstanceId
					: player.ActiveVehicleId,
			}
		});
		return true;
	}

	/// <summary>
	/// Buy a spare tire consumable and stow it directly in the active vehicle's cargo
	/// (cargo key <see cref="GameBalance.SpareTireCargoId"/>). Vehicles start with exactly one and
	/// nothing else grants more, so the store is the only resupply for the roadside tire swap.
	/// <paramref name="defs"/> is accepted for symmetry with the other buy paths (future per-city pricing).
	/// </summary>
	public bool TryBuySpareTireForActiveVehicle(DefDatabase defs, out string error)
	{
		error = string.Empty;
		var price = GameBalance.SpareTireCostUsd;

		var player = _ctx.Save.Player;
		if (player.MoneyUsd < price)
		{
			error = $"Not enough cash (need ${price}).";
			return false;
		}

		var activeId = player.ActiveVehicleId;
		var vehicle = string.IsNullOrWhiteSpace(activeId)
			? null
			: _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == activeId);
		if (vehicle == null)
		{
			error = "No active vehicle to stow a spare tire in.";
			return false;
		}

		// Cargo capacity gate (spec: storage units constrain what a chassis can haul).
		if (defs.Vehicles.TryGetValue(vehicle.DefinitionId, out var vdef))
		{
			var cargoUnits = vehicle.CargoInventory?.Sum(kv => Math.Max(0, kv.Value)) ?? 0;
			if (cargoUnits >= vdef.StorageCapacityUnits)
			{
				error = "Cargo bay's packed to the roof — sell or use something before wedging in another tire.";
				return false;
			}
		}

		var cargo = new Dictionary<string, int>(vehicle.CargoInventory ?? new Dictionary<string, int>());
		cargo.TryGetValue(GameBalance.SpareTireCargoId, out var carried);
		cargo[GameBalance.SpareTireCargoId] = carried + 1;

		var vehicles = _ctx.Save.Vehicles.ToList();
		var idx = vehicles.FindIndex(v => v.InstanceId == vehicle.InstanceId);
		if (idx < 0)
		{
			error = "Active vehicle missing.";
			return false;
		}
		vehicles[idx] = vehicle with { CargoInventory = cargo };

		_ctx.Replace(_ctx.Save with
		{
			Player = player with { MoneyUsd = player.MoneyUsd - price },
			Vehicles = vehicles,
		});
		return true;
	}

	// --- Driver store (Clinic & Outfitter: cybernetics + body armor) ---

	/// <summary>True when the given cybernetic (DriverUpgradeDefinition id) is already installed.</summary>
	public bool IsCyberneticInstalled(string upgradeId)
	{
		var list = _ctx.Save.Player.InstalledCyberneticIds;
		return list != null && list.Contains(upgradeId, StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Trade-in value of the currently equipped driver vest: 40% of store price scaled by the
	/// vest's REMAINING armor fraction (judge round, loop 6 — a shredded assault rig traded at
	/// full value, so cycling swaps beat the repair service and beat taking care of your gear).
	/// Basic kevlar and anything not sold at the outfitter trade in at $0.
	/// </summary>
	public static int GetDriverArmorTradeInValue(DefDatabase defs, string equippedArmorId)
		=> GetDriverArmorTradeInValue(defs, equippedArmorId, conditionFraction: 1f);

	public static int GetDriverArmorTradeInValue(DefDatabase defs, string equippedArmorId, float conditionFraction)
	{
		if (string.IsNullOrWhiteSpace(equippedArmorId))
			return 0;
		var cond = Math.Clamp(conditionFraction, 0f, 1f);
		return defs.DriverUpgrades.TryGetValue(equippedArmorId, out var u) && u.Kind == DriverUpgradeKind.Armor
			? (int)MathF.Round(u.PriceUsd * DriverArmorTradeInRate * cond)
			: 0;
	}

	/// <summary>
	/// Install a cybernetic (one-time, stacks with others): appends to InstalledCyberneticIds and
	/// permanently raises max driver HP, healing the delta. Fiction anchor: only clone-facility
	/// cities can do this — they cut you open, lace in the hardware, and re-upload the driver.
	/// </summary>
	public bool TryBuyDriverCybernetic(DefDatabase defs, string upgradeId, out string error)
	{
		error = string.Empty;
		if (string.IsNullOrWhiteSpace(upgradeId)
			|| !defs.DriverUpgrades.TryGetValue(upgradeId, out var def)
			|| def.Kind != DriverUpgradeKind.Cybernetic
			|| def.PriceUsd <= 0)
		{
			error = "That implant is not sold here.";
			return false;
		}

		var player = _ctx.Save.Player;
		if (!defs.Cities.TryGetValue(player.CurrentCityId ?? string.Empty, out var city) || !city.HasCloneFacility)
		{
			error = "Cybernetic installs need a clone facility — Detroit, Cleveland, Chicago, and Pittsburgh run them.";
			return false;
		}

		if (IsCyberneticInstalled(def.Id))
		{
			error = "That implant is already installed.";
			return false;
		}

		if (player.MoneyUsd < def.PriceUsd)
		{
			error = $"Not enough cash (need ${def.PriceUsd}).";
			return false;
		}

		var installed = new List<string>(player.InstalledCyberneticIds ?? new List<string>()) { def.Id };
		var bonus = Math.Max(0, def.HpBonus);
		var hpMax = player.DriverHpMax + bonus;
		var hp = Math.Clamp(player.DriverHp + bonus, 0, hpMax);

		_ctx.Replace(_ctx.Save with
		{
			Player = player with
			{
				MoneyUsd = player.MoneyUsd - def.PriceUsd,
				InstalledCyberneticIds = installed,
				DriverHpMax = hpMax,
				DriverHp = hp,
			}
		});
		_ctx.Status($"Installed {def.DisplayName} (-${def.PriceUsd}). Max driver HP +{bonus}.");
		return true;
	}

	/// <summary>
	/// Buy a driver armor vest: swaps EquippedArmorId and resets DriverArmorMax/DriverArmor to the
	/// new vest's points. The old vest trades in at 40% of its store price (basic kevlar: $0).
	/// </summary>
	public bool TryBuyDriverArmor(DefDatabase defs, string upgradeId, out string error)
	{
		error = string.Empty;
		if (string.IsNullOrWhiteSpace(upgradeId)
			|| !defs.DriverUpgrades.TryGetValue(upgradeId, out var def)
			|| def.Kind != DriverUpgradeKind.Armor
			|| def.PriceUsd <= 0)
		{
			error = "That vest is not sold here.";
			return false;
		}

		var player = _ctx.Save.Player;
		if (string.Equals(player.EquippedArmorId, def.Id, StringComparison.OrdinalIgnoreCase))
		{
			error = "You're already wearing that vest.";
			return false;
		}

		var tradeCondition = player.DriverArmorMax > 0 ? (float)player.DriverArmor / player.DriverArmorMax : 1f;
		var tradeIn = GetDriverArmorTradeInValue(defs, player.EquippedArmorId, tradeCondition);
		var netCost = Math.Max(0, def.PriceUsd - tradeIn);
		if (player.MoneyUsd < netCost)
		{
			error = $"Not enough cash (need ${netCost} after trade-in).";
			return false;
		}

		var points = Math.Max(0, def.ArmorPoints);
		_ctx.Replace(_ctx.Save with
		{
			Player = player with
			{
				MoneyUsd = player.MoneyUsd - netCost,
				EquippedArmorId = def.Id,
				DriverArmorMax = points,
				DriverArmor = points,
			}
		});
		_ctx.Status(tradeIn > 0
			? $"Equipped {def.DisplayName} (-${netCost} after ${tradeIn} trade-in). Armor {points}/{points}."
			: $"Equipped {def.DisplayName} (-${netCost}). Armor {points}/{points}.");
		return true;
	}

	// --- Personal weapons (on-foot; Docs/ONFOOT_COMBAT_PLAN.md) -------------------------------------

	/// <summary>The default sidearm is implicitly owned — a fresh clone always wakes armed.</summary>
	public bool IsPersonalWeaponOwned(string weaponId)
	{
		var player = _ctx.Save.Player;
		if (string.Equals(weaponId, "pw_pistol_9mm", StringComparison.OrdinalIgnoreCase))
			return true;
		var list = player.OwnedPersonalWeaponIds;
		return list != null && list.Contains(weaponId, StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Buy a personal weapon at the outfitter: adds it to the owned list, equips it, and seeds its
	/// ammo pool with the def's StartingAmmo (topped up to at most capacity). Owned weapons are
	/// kept — swapping between owned weapons later is free via TryEquipPersonalWeapon.
	/// </summary>
	public bool TryBuyPersonalWeapon(DefDatabase defs, string weaponId, out string error)
	{
		error = string.Empty;
		if (string.IsNullOrWhiteSpace(weaponId)
			|| !defs.PersonalWeapons.TryGetValue(weaponId, out var def)
			|| def.PriceUsd <= 0)
		{
			error = "That weapon is not sold here.";
			return false;
		}

		var player = _ctx.Save.Player;
		if (IsPersonalWeaponOwned(def.Id))
		{
			error = "You already own that weapon — equip it instead.";
			return false;
		}

		if (player.MoneyUsd < def.PriceUsd)
		{
			error = $"Not enough cash (need ${def.PriceUsd}).";
			return false;
		}

		var owned = new List<string>(player.OwnedPersonalWeaponIds ?? new List<string>()) { def.Id };
		var ammo = new Dictionary<string, int>(player.PersonalAmmoInventory ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase);
		var pool = ammo.TryGetValue(def.AmmoId, out var cur) ? cur : 0;
		ammo[def.AmmoId] = Math.Clamp(pool + Math.Max(0, def.StartingAmmo), 0, Math.Max(1, def.AmmoCapacity));

		_ctx.Replace(_ctx.Save with
		{
			Player = player with
			{
				MoneyUsd = player.MoneyUsd - def.PriceUsd,
				OwnedPersonalWeaponIds = owned,
				EquippedPersonalWeaponId = def.Id,
				PersonalAmmoInventory = ammo,
			}
		});
		_ctx.Status($"Bought and holstered {def.DisplayName} (-${def.PriceUsd}).");
		return true;
	}

	/// <summary>Swap between owned personal weapons (free — it's your own holster).</summary>
	public bool TryEquipPersonalWeapon(DefDatabase defs, string weaponId, out string error)
	{
		error = string.Empty;
		if (string.IsNullOrWhiteSpace(weaponId) || !defs.PersonalWeapons.TryGetValue(weaponId, out var def))
		{
			error = "Unknown weapon.";
			return false;
		}

		var player = _ctx.Save.Player;
		if (!IsPersonalWeaponOwned(def.Id))
		{
			error = "You don't own that weapon.";
			return false;
		}

		if (string.Equals(player.EquippedPersonalWeaponId, def.Id, StringComparison.OrdinalIgnoreCase))
		{
			error = "Already carrying it.";
			return false;
		}

		_ctx.Replace(_ctx.Save with { Player = player with { EquippedPersonalWeaponId = def.Id } });
		_ctx.Status($"Now carrying the {def.DisplayName}.");
		return true;
	}

	/// <summary>
	/// Buy a 10-round box for a personal weapon's pool, capped at that weapon's carry capacity.
	/// Pools are shared per ammo id (pistol + SMG both feed on pammo_9mm), and the cap used is the
	/// EQUIPPED weapon's capacity when it shares the pool, else the purchased weapon's.
	/// </summary>
	public bool TryBuyPersonalAmmo(DefDatabase defs, string weaponId, out string error)
	{
		error = string.Empty;
		if (string.IsNullOrWhiteSpace(weaponId) || !defs.PersonalWeapons.TryGetValue(weaponId, out var def))
		{
			error = "Unknown weapon.";
			return false;
		}

		var player = _ctx.Save.Player;
		if (!IsPersonalWeaponOwned(def.Id))
		{
			error = "Buy the weapon before stocking its ammo.";
			return false;
		}

		var cap = Math.Max(1, def.AmmoCapacity);
		if (defs.PersonalWeapons.TryGetValue(player.EquippedPersonalWeaponId ?? string.Empty, out var equipped)
			&& string.Equals(equipped.AmmoId, def.AmmoId, StringComparison.OrdinalIgnoreCase))
			cap = Math.Max(cap, equipped.AmmoCapacity);

		var ammo = new Dictionary<string, int>(player.PersonalAmmoInventory ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase);
		var pool = ammo.TryGetValue(def.AmmoId, out var cur) ? cur : 0;
		if (pool >= cap)
		{
			error = "You can't carry any more of that ammo.";
			return false;
		}

		var price = Math.Max(1, def.AmmoPricePer10Usd);
		if (player.MoneyUsd < price)
		{
			error = $"Not enough cash (need ${price}).";
			return false;
		}

		ammo[def.AmmoId] = Math.Min(cap, pool + 10);
		_ctx.Replace(_ctx.Save with
		{
			Player = player with
			{
				MoneyUsd = player.MoneyUsd - price,
				PersonalAmmoInventory = ammo,
			}
		});
		_ctx.Status($"Bought a box of {def.AmmoId.Replace("pammo_", "").ToUpperInvariant()} (-${price}). {ammo[def.AmmoId]}/{cap} carried.");
		return true;
	}

	/// <summary>
	/// Seeds the equipped default sidearm's ammo pool with its free starting rounds if that pool
	/// has never been initialized. Called by the Driver Store (display) AND the arena (runtime
	/// mirror) so both see the same numbers — without this, the store showed "0/60" pre-seed and
	/// buying one box would silently forfeit the free 60-round issue.
	/// </summary>
	public void EnsureDefaultSidearmSeeded(DefDatabase defs)
	{
		var player = _ctx.Save.Player;
		var equippedId = string.IsNullOrWhiteSpace(player.EquippedPersonalWeaponId)
			? "pw_pistol_9mm"
			: player.EquippedPersonalWeaponId;
		if (!defs.PersonalWeapons.TryGetValue(equippedId, out var def))
			return;
		if (player.PersonalAmmoInventory != null && player.PersonalAmmoInventory.ContainsKey(def.AmmoId))
			return;

		var ammo = new Dictionary<string, int>(player.PersonalAmmoInventory ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase)
		{
			[def.AmmoId] = Math.Clamp(def.StartingAmmo, 0, Math.Max(1, def.AmmoCapacity)),
		};
		_ctx.Replace(_ctx.Save with { Player = player with { PersonalAmmoInventory = ammo } });
	}

	/// <summary>
	/// Arena resolve-time commit of the driver's personal ammo pools (single write when a match
	/// ends, mirroring how vehicle ammo commits — never per-shot).
	/// </summary>
	public void ReplacePersonalAmmoPools(IReadOnlyDictionary<string, int> pools)
	{
		var player = _ctx.Save.Player;
		var next = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (var kv in pools)
			next[kv.Key] = Math.Max(0, kv.Value);
		_ctx.Replace(_ctx.Save with { Player = player with { PersonalAmmoInventory = next } });
	}

	/// <summary>
	/// Move one owned part out of inventory (called when the Workshop installs it). Returns false if
	/// the player doesn't own a spare.
	/// </summary>
	public bool TryConsumeOwnedPart(string partId)
	{
		var player = _ctx.Save.Player;
		var parts = new Dictionary<string, int>(player.PartsInventory ?? new Dictionary<string, int>());
		if (!parts.TryGetValue(partId, out var owned) || owned <= 0)
			return false;

		if (owned <= 1)
			parts.Remove(partId);
		else
			parts[partId] = owned - 1;

		_ctx.Replace(_ctx.Save with { Player = player with { PartsInventory = parts } });
		return true;
	}

	/// <summary>Return a part to inventory (called when the Workshop uninstalls it).</summary>
	public void ReturnPartToInventory(string partId)
	{
		if (string.IsNullOrWhiteSpace(partId)) return;
		var player = _ctx.Save.Player;
		var parts = new Dictionary<string, int>(player.PartsInventory ?? new Dictionary<string, int>());
		parts.TryGetValue(partId, out var owned);
		parts[partId] = owned + 1;
		_ctx.Replace(_ctx.Save with { Player = player with { PartsInventory = parts } });
	}
}
