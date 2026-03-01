// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Audio/AudioBusUtil.cs
// Purpose: Audio utilities and runtime components (buses, telemetry, layered engine audio).
// -------------------------------------------------------------------------------------------------
using Godot;

namespace WastelandSurvivor.Game.Audio;

public static class AudioBusUtil
{
	private static bool _ensured;

	// Mix defaults (dB). These are intentionally conservative but should be clearly audible
	// from the top-down camera. Tweak later once we have master mixing/UI sliders.
	private const float SfxDb = 0f;
	private const float EnginesDb = 8f;
	private const float TiresDb = 3f;

	public static void EnsureBuses()
	{
		if (_ensured) return;
		_ensured = true;

		try
		{
			// Godot starts with a single "Master" bus. We create a simple hierarchy:
			// Master
			//   └─ SFX
			//       ├─ Engines
			//       └─ Tires
			EnsureBus("SFX", sendTo: "Master");
			EnsureBus("Engines", sendTo: "SFX");
			EnsureBus("Tires", sendTo: "SFX");

			// Apply default bus gains even if the buses already existed.
			TrySetBusVolume("SFX", SfxDb);
			TrySetBusVolume("Engines", EnginesDb);
			TrySetBusVolume("Tires", TiresDb);
		}
		catch (System.Exception ex)
		{
			GD.PrintErr($"[Audio] Failed to ensure audio buses: {ex.Message}");
		}
	}

	private static void TrySetBusVolume(string name, float volumeDb)
	{
		var idx = AudioServer.GetBusIndex(name);
		if (idx < 0) return;
		AudioServer.SetBusVolumeDb(idx, volumeDb);
	}

	private static void EnsureBus(string name, string sendTo)
	{
		var idx = AudioServer.GetBusIndex(name);
		if (idx < 0)
		{
			AudioServer.AddBus();
			idx = AudioServer.BusCount - 1;
			AudioServer.SetBusName(idx, name);
		}

		var sendIdx = AudioServer.GetBusIndex(sendTo);
		if (sendIdx >= 0)
			AudioServer.SetBusSend(idx, sendTo);
	}
}
