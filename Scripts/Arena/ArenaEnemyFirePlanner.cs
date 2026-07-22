// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/ArenaEnemyFirePlanner.cs
// Purpose: Shared enemy weapon-slot selection so AI uses the same loadout/mount rules as the player.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game.Arena;

public readonly struct ArenaEnemyFireSelection
{
	public ArenaEnemyFireSelection(int slot, float alignmentDot, MountLocation mountLocation)
	{
		Slot = slot;
		AlignmentDot = alignmentDot;
		MountLocation = mountLocation;
	}

	public int Slot { get; }
	public float AlignmentDot { get; }
	public MountLocation MountLocation { get; }
	public bool HasSelection => Slot != 0;
}

public readonly struct ArenaVehicleAiTacticalProfile
{
	public ArenaVehicleAiTacticalProfile(MountLocation preferredMountLocation, float preferredRangeScale, float orbitScale, int orbitSign)
	{
		PreferredMountLocation = preferredMountLocation;
		PreferredRangeScale = preferredRangeScale;
		OrbitScale = orbitScale;
		OrbitSign = orbitSign >= 0 ? 1 : -1;
	}

	public MountLocation PreferredMountLocation { get; }
	public float PreferredRangeScale { get; }
	public float OrbitScale { get; }
	public int OrbitSign { get; }

	public static ArenaVehicleAiTacticalProfile Default { get; } = new(MountLocation.Front, 1.0f, 0.60f, 1);
}

public static class ArenaEnemyFirePlanner
{
	public static int FindSlotForWeaponType(DefDatabase defs, VehicleInstanceState veh, WeaponType weaponType)
	{
		for (var slot = 1; slot <= ArenaWeaponLoadoutResolver.MaxWeaponSlots; slot++)
		{
			var resolved = ArenaWeaponLoadoutResolver.ResolveForSlot(defs, veh, slot);
			if (resolved?.WeaponDefinition?.WeaponType == weaponType)
				return slot;
		}

		return 0;
	}

	/// <summary>Rear-deploy "dropper" weapons (mines, oil slicks, smoke) are not used as primary aimed fire.</summary>
	public static bool IsDropperWeapon(WeaponType weaponType)
		=> weaponType is WeaponType.MineDropper or WeaponType.OilSlickDropper or WeaponType.SmokeScreenDropper;

	private static bool IsUtilityWeapon(WeaponDefinition weaponDefinition)
		=> IsDropperWeapon(weaponDefinition.WeaponType);

	/// <summary>
	/// Find the first installed rear-dropper slot (mine or oil slick), or 0 if none.
	/// Lets the shared "drop a hazard behind me" AI work for whatever dropper is equipped.
	/// </summary>
	public static int FindRearDropperSlot(DefDatabase defs, VehicleInstanceState veh)
	{
		var slot = FindSlotForWeaponType(defs, veh, WeaponType.MineDropper);
		if (slot <= 0)
			slot = FindSlotForWeaponType(defs, veh, WeaponType.OilSlickDropper);
		if (slot <= 0)
			slot = FindSlotForWeaponType(defs, veh, WeaponType.SmokeScreenDropper);
		return slot;
	}

	public static ArenaEnemyFireSelection PickBestSelection(
		DefDatabase defs,
		VehicleInstanceState veh,
		Vector3 toTargetDir,
		float distance,
		Func<string, Vector3> getMountForward,
		ArenaAiConfig? config = null,
		ArenaVehicleAiRuntimeState? runtimeState = null,
		Func<int, float>? getSlotCooldownSeconds = null)
	{
		var bestSlot = 0;
		var bestScore = float.NegativeInfinity;
		var bestDot = -1f;
		var bestLocation = MountLocation.Front;
		// Missile opportunism (T4 SIDEWINDER): while the target is committed to a turn, ordnance
		// weapons jump the selection queue. Callers that omit config/runtime keep the old scoring.
		var opportunismBias = config != null
			&& runtimeState is { OpportunismWindowRemainingSeconds: > 0f }
				? config.OpportunismMissileBias
				: 0f;

		var seenMounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (var slot = 1; slot <= ArenaWeaponLoadoutResolver.MaxWeaponSlots; slot++)
		{
			var resolved = ArenaWeaponLoadoutResolver.ResolveForSlot(defs, veh, slot);
			if (resolved == null)
				continue;

			// Bound slots 1-3 can alias the same mount on sparse loadouts; never score a weapon
			// twice (a duplicate pick would hand it a second independent cooldown track).
			if (!seenMounts.Add(resolved.MountId))
				continue;

			if (IsUtilityWeapon(resolved.WeaponDefinition))
				continue;

			// Targeting-computer rule: slots beyond the installed computer's control-group capacity
			// degrade to fixed-angle mounts. They are not part of the computer-assisted selection —
			// the shared opportunistic-fire pass (ArenaRealtimeView.TryFireDegradedWeapons) covers
			// them for player and AI alike whenever the target crosses their fixed bearing.
			if (!ArenaWeaponLoadoutResolver.IsWeaponComputerControlled(defs, veh, resolved.MountId, out _))
				continue;

			// A slot that CANNOT fire right now must never win the pick: the missile's raw
			// BaseDamage out-scored the guns so hard the AI spent whole fights pointing a cooling
			// launcher at the player — tier-5's 20mm autocannon never fired a single logged round.
			if (getSlotCooldownSeconds != null && getSlotCooldownSeconds(slot) > 0.05f)
				continue;

			// Dry weapons don't shoot either (TryFire would just eat the window with a click).
			if (!string.IsNullOrWhiteSpace(resolved.AmmoId)
				&& veh.AmmoInventory != null
				&& veh.AmmoInventory.TryGetValue(resolved.AmmoId!, out var ammoLeft)
				&& ammoLeft <= 0)
				continue;

			var mountForward = getMountForward(resolved.MountId);
			var dot = Mathf.Clamp(mountForward.Dot(toTargetDir), -1f, 1f);
			var minDot = resolved.MountLocation switch
			{
				MountLocation.Top => 0.20f,
				MountLocation.Rear => 0.72f,
				MountLocation.Left => 0.65f,
				MountLocation.Right => 0.65f,
				_ => 0.72f,
			};
			if (dot < minDot)
				continue;

			var rangePenalty = resolved.MountLocation == MountLocation.Rear && distance > 24f ? 0.25f : 0f;
			var score = dot * 10f + resolved.WeaponDefinition.BaseDamage - rangePenalty;
			if (opportunismBias > 0f && resolved.WeaponDefinition.WeaponType is WeaponType.Missile or WeaponType.Rocket)
				score += opportunismBias;
			if (score > bestScore)
			{
				bestScore = score;
				bestSlot = slot;
				bestDot = dot;
				bestLocation = resolved.MountLocation;
			}
		}

		return new ArenaEnemyFireSelection(bestSlot, bestDot, bestLocation);
	}

	public static int PickBestSlot(
		DefDatabase defs,
		VehicleInstanceState veh,
		Vector3 toTargetDir,
		float distance,
		Func<string, Vector3> getMountForward)
		=> PickBestSelection(defs, veh, toTargetDir, distance, getMountForward).Slot;

	public static ArenaVehicleAiTacticalProfile BuildTacticalProfile(DefDatabase defs, VehicleInstanceState? veh)
	{
		if (veh == null)
			return ArenaVehicleAiTacticalProfile.Default;

		var damageByLocation = new Dictionary<MountLocation, float>();
		var seenMounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var primaryWeaponFound = false;
		for (var slot = 1; slot <= ArenaWeaponLoadoutResolver.MaxWeaponSlots; slot++)
		{
			var resolved = ArenaWeaponLoadoutResolver.ResolveForSlot(defs, veh, slot);
			if (resolved == null || !seenMounts.Add(resolved.MountId))
				continue;

			if (IsUtilityWeapon(resolved.WeaponDefinition))
				continue;

			primaryWeaponFound = true;
			damageByLocation.TryGetValue(resolved.MountLocation, out var existing);
			damageByLocation[resolved.MountLocation] = existing + Mathf.Max(1f, resolved.WeaponDefinition.BaseDamage);
		}

		if (!primaryWeaponFound)
		{
			seenMounts.Clear();
			for (var slot = 1; slot <= ArenaWeaponLoadoutResolver.MaxWeaponSlots; slot++)
			{
				var resolved = ArenaWeaponLoadoutResolver.ResolveForSlot(defs, veh, slot);
				if (resolved == null || !seenMounts.Add(resolved.MountId))
					continue;

				var weight = IsUtilityWeapon(resolved.WeaponDefinition) ? 0.35f : 1f;
				damageByLocation.TryGetValue(resolved.MountLocation, out var existing);
				damageByLocation[resolved.MountLocation] = existing + Mathf.Max(1f, resolved.WeaponDefinition.BaseDamage) * weight;
			}
		}

		if (damageByLocation.Count == 0)
			return ArenaVehicleAiTacticalProfile.Default;

		var best = MountLocation.Front;
		var bestDamage = float.NegativeInfinity;
		foreach (var pair in damageByLocation)
		{
			if (pair.Value <= bestDamage)
				continue;
			bestDamage = pair.Value;
			best = pair.Key;
		}

		return best switch
		{
			MountLocation.Left => new ArenaVehicleAiTacticalProfile(best, 1.00f, 1.05f, -1),
			MountLocation.Right => new ArenaVehicleAiTacticalProfile(best, 1.00f, 1.05f, 1),
			MountLocation.Top => new ArenaVehicleAiTacticalProfile(best, 1.00f, 0.95f, 1),
			MountLocation.Rear => new ArenaVehicleAiTacticalProfile(best, 1.08f, 0.50f, 1),
			_ => new ArenaVehicleAiTacticalProfile(best, 0.95f, 0.70f, 1),
		};
	}
}
