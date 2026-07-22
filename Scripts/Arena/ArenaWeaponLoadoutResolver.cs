// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/ArenaWeaponLoadoutResolver.cs
// Purpose: Shared weapon-slot and targeting-computer resolution used by arena combat controllers.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game.Arena;

public sealed class ArenaResolvedWeapon
{
	public string MountId { get; init; } = "";
	public MountLocation MountLocation { get; init; } = MountLocation.Front;
	public WeaponMountDefinition MountDefinition { get; init; } = null!;
	public InstalledWeaponState InstalledWeapon { get; init; } = null!;
	public WeaponDefinition WeaponDefinition { get; init; } = null!;
	public string AmmoId { get; init; } = "";
}

public static class ArenaWeaponLoadoutResolver
{
	/// <summary>
	/// Total addressable weapon slots. Slots 1-3 are the classic bound fire groups (Front/Top/Rear
	/// preference); slots 4+ address the remaining mounts (broadside guns on 4+-mount rigs) so the
	/// fixed-angle fallback can reach every installed weapon for player AND AI.
	/// </summary>
	public const int MaxWeaponSlots = 5;

	/// <summary>Half-angle of the opportunistic-fire cone for computer-degraded fixed mounts.</summary>
	public const float DegradedBearingConeDegrees = 10f;

	/// <summary>cos(DegradedBearingConeDegrees): dot threshold for "target crosses the fixed axis".</summary>
	public static readonly float DegradedBearingConeDot = MathF.Cos(Mathf.DegToRad(DegradedBearingConeDegrees));

	private static readonly MountLocation[] SlotPreferenceOrder =
	{
		MountLocation.Front,
		MountLocation.Top,
		MountLocation.Left,
		MountLocation.Right,
		MountLocation.Rear,
	};

	public static ArenaResolvedWeapon? ResolveForSlot(DefDatabase defs, VehicleInstanceState veh, int slot)
	{
		if (slot < 1)
			slot = 1;

		if (!defs.Vehicles.TryGetValue(veh.DefinitionId, out var vdef))
			return null;

		var ordered = ResolveInstalledWeapons(defs, veh, vdef);
		if (ordered.Count == 0)
			return null;

		if (slot <= 3)
			return ResolveBoundSlot(ordered, slot);

		// Slots 4+: weapons not reachable through the three bound groups, in preference order.
		// Returns null when nothing remains (never aliases an already-bound weapon, which would
		// hand it a second, independent cooldown track).
		var assigned = new List<ArenaResolvedWeapon>(3);
		for (var s = 1; s <= 3; s++)
		{
			var bound = ResolveBoundSlot(ordered, s);
			if (bound != null && !assigned.Contains(bound))
				assigned.Add(bound);
		}

		var index = slot - 4;
		foreach (var w in ordered)
		{
			if (assigned.Contains(w))
				continue;
			if (index == 0)
				return w;
			index--;
		}

		return null;
	}

	private static ArenaResolvedWeapon? ResolveBoundSlot(List<ArenaResolvedWeapon> ordered, int slot)
	{
		ArenaResolvedWeapon? pick = null;
		if (slot == 1)
			pick = ordered.FirstOrDefault(x => x.MountLocation == MountLocation.Front);
		else if (slot == 2)
			pick = ordered.FirstOrDefault(x => x.MountLocation == MountLocation.Top);
		else if (slot == 3)
			pick = ordered.FirstOrDefault(x => x.MountLocation == MountLocation.Rear);

		return pick ?? (slot <= ordered.Count ? ordered[slot - 1] : ordered[0]);
	}

	public static TargetingComputerDefinition? ResolveInstalledComputer(DefDatabase defs, VehicleInstanceState? veh)
	{
		if (veh == null || string.IsNullOrWhiteSpace(veh.InstalledComputerId))
			return null;

		return defs.Computers.TryGetValue(veh.InstalledComputerId!, out var comp) ? comp : null;
	}

	/// <summary>
	/// Utility weapons (rear-deploy droppers) are dumb release systems: they do not consume a
	/// targeting-computer control group and never auto-track.
	/// </summary>
	public static bool IsUtilityWeapon(WeaponDefinition w)
		=> w.WeaponType is WeaponType.MineDropper or WeaponType.OilSlickDropper or WeaponType.SmokeScreenDropper;

	/// <summary>Weapon groups the installed computer can control at once. No computer = 1 (manual control of the primary).</summary>
	public static int GetMaxControlledGroups(DefDatabase defs, VehicleInstanceState? veh)
	{
		var comp = ResolveInstalledComputer(defs, veh);
		return Math.Max(1, comp?.MaxActiveWeaponGroups ?? 1);
	}

	/// <summary>True if the installed computer can drive auto-tracking turret mounts at all.</summary>
	public static bool HasAutoTrackCapability(DefDatabase defs, VehicleInstanceState? veh)
	{
		var comp = ResolveInstalledComputer(defs, veh);
		return comp != null && comp.AutoAimSlots > 0;
	}

	/// <summary>
	/// Master-spec targeting-computer rule: the computer governs how many (non-utility) weapons can be
	/// controlled at once. Returns false (with a player-readable reason) when the weapon on
	/// <paramref name="mountId"/> is beyond the installed computer's control-group capacity — such a
	/// weapon is NOT offline: it degrades to a fixed-angle mount (turrets lock to their neutral
	/// bearing) and only fires opportunistically when the target crosses its axis
	/// (<see cref="DegradedBearingConeDot"/>).
	/// </summary>
	public static bool IsWeaponComputerControlled(DefDatabase defs, VehicleInstanceState veh, string mountId, out string offlineReason)
	{
		offlineReason = string.Empty;
		if (!defs.Vehicles.TryGetValue(veh.DefinitionId, out var vdef))
			return true;

		var ordered = ResolveInstalledWeapons(defs, veh, vdef);
		var resolved = ordered.FirstOrDefault(x => string.Equals(x.MountId, mountId, StringComparison.OrdinalIgnoreCase));
		if (resolved == null)
			return true;
		if (IsUtilityWeapon(resolved.WeaponDefinition))
			return true;

		var maxGroups = GetMaxControlledGroups(defs, veh);
		var combatIndex = 0;
		foreach (var w in ordered)
		{
			if (IsUtilityWeapon(w.WeaponDefinition))
				continue;
			if (ReferenceEquals(w, resolved))
				break;
			combatIndex++;
		}

		if (combatIndex >= maxGroups)
		{
			var compName = ResolveInstalledComputer(defs, veh)?.DisplayName ?? "No targeting computer";
			offlineReason = $"{compName} can only control {maxGroups} weapon group{(maxGroups == 1 ? "" : "s")}.";
			return false;
		}

		return true;
	}

	/// <summary>
	/// Fixed-angle fallback fire gate: true when <paramref name="targetPos"/> sits inside the tight
	/// bearing cone around the degraded weapon's fixed axis (planar check). Shared by player and AI
	/// so opportunistic fire obeys identical rules on both sides.
	/// </summary>
	public static bool IsTargetInDegradedBearingCone(Vector3 muzzlePos, Vector3 muzzleForward, Vector3 targetPos)
	{
		muzzleForward.Y = 0f;
		var to = targetPos - muzzlePos;
		to.Y = 0f;
		if (muzzleForward.LengthSquared() < 0.000001f || to.LengthSquared() < 0.000001f)
			return false;
		return muzzleForward.Normalized().Dot(to.Normalized()) >= DegradedBearingConeDot;
	}

	public static Node3D? ResolveMissileLockTarget(DefDatabase defs, VehicleInstanceState? shooterVeh, VehiclePawn shooter, Node3D? candidateTarget)
	{
		if (candidateTarget == null || !GodotObject.IsInstanceValid(candidateTarget))
			return null;

		var comp = ResolveInstalledComputer(defs, shooterVeh);
		if (comp == null)
			return null;

		var targetPos = GetMissileAimPoint(candidateTarget);
		var lockRange = MathF.Max(6f, comp.LockRange);
		var planar = Distance2D(shooter.GlobalPosition, targetPos);
		return planar <= lockRange ? candidateTarget : null;
	}

	public static Vector3 GetMissileAimPoint(Node3D target)
	{
		if (target is VehiclePawn vp && GodotObject.IsInstanceValid(vp))
			return vp.GlobalPosition + Vector3.Up * 0.35f;
		if (target is DriverPawn dp && GodotObject.IsInstanceValid(dp))
			return dp.GlobalPosition + Vector3.Up * 0.15f;
		return target.GlobalPosition + Vector3.Up * 0.20f;
	}

	private static List<ArenaResolvedWeapon> ResolveInstalledWeapons(DefDatabase defs, VehicleInstanceState veh, VehicleDefinition vdef)
	{
		var installedByMount = veh.InstalledWeaponsByMountId ?? new Dictionary<string, InstalledWeaponState>(StringComparer.OrdinalIgnoreCase);
		if (installedByMount.Count == 0)
			return new List<ArenaResolvedWeapon>();

		var result = new List<ArenaResolvedWeapon>();
		foreach (var mount in vdef.MountPoints.Where(m => installedByMount.ContainsKey(m.MountId)))
		{
			if (!installedByMount.TryGetValue(mount.MountId, out var installed) || installed == null)
				continue;
			if (!defs.Weapons.TryGetValue(installed.WeaponId, out var weaponDef))
				continue;

			var ammoId = installed.SelectedAmmoId;
			if (string.IsNullOrWhiteSpace(ammoId))
				ammoId = weaponDef.AmmoTypeIds?.FirstOrDefault();
			if (string.IsNullOrWhiteSpace(ammoId))
				continue;

			result.Add(new ArenaResolvedWeapon
			{
				MountId = mount.MountId,
				MountLocation = mount.MountLocation,
				MountDefinition = mount,
				InstalledWeapon = installed,
				WeaponDefinition = weaponDef,
				AmmoId = ammoId,
			});
		}

		result.Sort((a, b) => Array.IndexOf(SlotPreferenceOrder, a.MountLocation).CompareTo(Array.IndexOf(SlotPreferenceOrder, b.MountLocation)));
		return result;
	}

	private static float Distance2D(Vector3 a, Vector3 b)
	{
		var dx = a.X - b.X;
		var dz = a.Z - b.Z;
		return Mathf.Sqrt(dx * dx + dz * dz);
	}
}
