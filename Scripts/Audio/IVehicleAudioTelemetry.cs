// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Audio/IVehicleAudioTelemetry.cs
// Purpose: Audio utilities and runtime components (buses, telemetry, layered engine audio).
// -------------------------------------------------------------------------------------------------
using GamePawnKit.Pawns;

namespace WastelandSurvivor.Game.Audio;

/// <summary>
/// Minimal telemetry interface for driving vehicle audio.
/// Keep this intentionally tiny so it can be implemented by VehiclePawn or future vehicle controllers.
/// Units: meters/second for speed.
/// </summary>
public interface IVehicleAudioTelemetry : IVehicleTelemetry
{
}