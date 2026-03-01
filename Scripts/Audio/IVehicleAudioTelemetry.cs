// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Audio/IVehicleAudioTelemetry.cs
// Purpose: Audio utilities and runtime components (buses, telemetry, layered engine audio).
// -------------------------------------------------------------------------------------------------
using Godot;

namespace WastelandSurvivor.Game.Audio;

/// <summary>
/// Minimal telemetry interface for driving vehicle audio.
/// Keep this intentionally tiny so it can be implemented by VehiclePawn or future vehicle controllers.
/// Units: meters/second for speed.
/// </summary>
public interface IVehicleAudioTelemetry
{
	float GetSpeedMps();
	/// <summary>
	/// Signed forward speed in meters/second (positive forward, negative reverse).
	/// Used to keep engine audio gear logic stable while reversing.
	/// </summary>
	float GetForwardSpeedSignedMps();
	float GetMaxSpeedMps();

	/// <summary>
	/// "Driving" throttle intensity (0..1). Should be 0 when the player is braking.
	/// </summary>
	float GetThrottle01();

	/// <summary>
	/// Brake intensity (0..1). Should be 0 when the player is simply accelerating.
	/// </summary>
	float GetBrake01();
}