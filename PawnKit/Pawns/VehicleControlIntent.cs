// -------------------------------------------------------------------------------------------------
// GamePawnKit
// File: PawnKit/Pawns/VehicleControlIntent.cs
// Purpose: Small reusable control-intent surface shared by player and AI vehicle controllers.
// -------------------------------------------------------------------------------------------------
using Godot;

namespace GamePawnKit.Pawns;

/// <summary>
/// Lightweight control payload that can be produced by player input readers, AI drivers, replays,
/// or network code and then applied consistently to a <see cref="VehiclePawnBase"/>.
/// </summary>
public readonly struct VehicleControlIntent
{
	public VehicleControlIntent(float throttle, float steer, Vector3 aimWorldPosition)
	{
		Throttle = throttle;
		Steer = steer;
		AimWorldPosition = aimWorldPosition;
	}

	public float Throttle { get; }
	public float Steer { get; }
	public Vector3 AimWorldPosition { get; }

	public static VehicleControlIntent Neutral(Vector3 aimWorldPosition)
		=> new(0f, 0f, aimWorldPosition);
}
