// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/ArenaTargetingController.cs
// Purpose: Shared arena target-selection helpers so UI controllers stop hand-rolling target validity / cycling.
// -------------------------------------------------------------------------------------------------
using System.Collections.Generic;
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Small runtime helper that tracks targetable enemy vehicles for the player and keeps selection/cycle state valid.
/// Built to scale beyond the current single-enemy arena fights without forcing <c>ArenaRealtimeView</c>
/// to own target-list bookkeeping itself.
/// </summary>
public sealed class ArenaTargetingController
{
	private readonly List<VehiclePawn> _vehicleCandidates = new();

	public VehiclePawn? SelectedVehicleTarget { get; private set; }
	public int VehicleCycleIndex { get; private set; }

	public void SetVehicleCandidates(VehiclePawn? primaryCandidate)
	{
		_vehicleCandidates.Clear();
		if (primaryCandidate != null && GodotObject.IsInstanceValid(primaryCandidate))
			_vehicleCandidates.Add(primaryCandidate);

		EnsureVehicleTarget();
	}

	public void SetVehicleCandidates(IEnumerable<VehiclePawn?> candidates)
	{
		_vehicleCandidates.Clear();
		foreach (var candidate in candidates)
		{
			if (candidate == null || !GodotObject.IsInstanceValid(candidate))
				continue;
			_vehicleCandidates.Add(candidate);
		}

		EnsureVehicleTarget();
	}

	public VehiclePawn? EnsureVehicleTarget()
	{
		if (SelectedVehicleTarget != null && GodotObject.IsInstanceValid(SelectedVehicleTarget))
		{
			var idx = _vehicleCandidates.IndexOf(SelectedVehicleTarget);
			if (idx >= 0)
			{
				VehicleCycleIndex = idx;
				return SelectedVehicleTarget;
			}
		}

		if (_vehicleCandidates.Count == 0)
		{
			Clear();
			return null;
		}

		if (VehicleCycleIndex < 0)
			VehicleCycleIndex = 0;
		else if (VehicleCycleIndex >= _vehicleCandidates.Count)
			VehicleCycleIndex = _vehicleCandidates.Count - 1;

		SelectedVehicleTarget = _vehicleCandidates[VehicleCycleIndex];
		return SelectedVehicleTarget;
	}

	public VehiclePawn? CycleVehicleTarget()
	{
		if (_vehicleCandidates.Count == 0)
		{
			Clear();
			return null;
		}

		if (_vehicleCandidates.Count == 1)
		{
			VehicleCycleIndex = 0;
			SelectedVehicleTarget = _vehicleCandidates[0];
			return SelectedVehicleTarget;
		}

		VehicleCycleIndex = (VehicleCycleIndex + 1) % _vehicleCandidates.Count;
		SelectedVehicleTarget = _vehicleCandidates[VehicleCycleIndex];
		return SelectedVehicleTarget;
	}

	public void ApplyIndicator(TargetIndicator3D? indicator)
	{
		if (indicator == null || !GodotObject.IsInstanceValid(indicator))
			return;

		indicator.SetTarget(SelectedVehicleTarget);
	}

	public void Clear()
	{
		_vehicleCandidates.Clear();
		SelectedVehicleTarget = null;
		VehicleCycleIndex = 0;
	}

	public static Node3D? ResolveEnemyAimTarget(bool playerOnFoot, DriverPawn? driverPawn, VehiclePawn? playerPawn)
	{
		if (playerOnFoot && driverPawn != null && GodotObject.IsInstanceValid(driverPawn))
			return driverPawn;
		return playerPawn;
	}
}
