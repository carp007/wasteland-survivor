// -------------------------------------------------------------------------------------------------
// GamePawnKit
// File: PawnKit/Pawns/IVehicleTelemetry.cs
// Purpose: Minimal runtime telemetry for vehicles (speed/throttle/brake) that other systems can consume.
// -------------------------------------------------------------------------------------------------
namespace GamePawnKit.Pawns;

/// <summary>
/// Minimal telemetry surface for vehicles.
///
/// This intentionally stays small and game-agnostic so it can be used by:
/// - audio systems (engine loops, tire squeal),
/// - HUD/UI (speedometer, throttle/brake indicators),
/// - AI debugging / tuning tools.
///
/// Units: meters/second for speed.
/// </summary>
public interface IVehicleTelemetry
{
	float GetSpeedMps();
	/// <summary>
	/// Signed forward speed in meters/second (positive forward, negative reverse).
	/// </summary>
	float GetForwardSpeedSignedMps();
	float GetMaxSpeedMps();

	/// <summary>
	/// "Driving" throttle intensity (0..1). Should be 0 when the driver is braking.
	/// </summary>
	float GetThrottle01();

	/// <summary>
	/// Brake intensity (0..1). Should be 0 when the driver is simply accelerating.
	/// </summary>
	float GetBrake01();
}
