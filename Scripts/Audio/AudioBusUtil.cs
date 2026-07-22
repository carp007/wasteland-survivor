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
	private const float MusicDb = -8f;

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
			EnsureBus("Music", sendTo: "Master");

			// Apply default bus gains even if the buses already existed.
			TrySetBusVolume("SFX", SfxDb);
			TrySetBusVolume("Engines", EnginesDb);
			TrySetBusVolume("Tires", TiresDb);
			TrySetBusVolume("Music", MusicDb);

			// Venue acoustics: a light large-room reverb on the SFX tree, DISABLED by default.
			// AmbienceDirector enables it only for stadium fights (crowded bowl = slap-back),
			// so highway/yard fights stay dry. Created once here so toggling is allocation-free.
			EnsureReverbEffect();
			// Danger state: a low-pass on the Music bus, DISABLED by default. MusicDirector
			// sweeps it in when the driver is critical — the world closes in, the music muffles.
			EnsureMusicLowPassEffect();
		}
		catch (System.Exception ex)
		{
			GD.PrintErr($"[Audio] Failed to ensure audio buses: {ex.Message}");
		}
	}

	/// <summary>Bus index + effect index of the SFX venue reverb (created by EnsureBuses).</summary>
	public static (int busIdx, int effectIdx) FindSfxReverb() => FindEffect("SFX", typeof(AudioEffectReverb));

	/// <summary>Bus index + effect index of the Music danger low-pass (created by EnsureBuses).</summary>
	public static (int busIdx, int effectIdx) FindMusicLowPass() => FindEffect("Music", typeof(AudioEffectLowPassFilter));

	/// <summary>Enable/disable the stadium reverb (no-op if the effect is missing).</summary>
	public static void SetSfxReverbEnabled(bool enabled)
	{
		var (busIdx, fxIdx) = FindSfxReverb();
		if (busIdx < 0 || fxIdx < 0) return;
		if (AudioServer.IsBusEffectEnabled(busIdx, fxIdx) != enabled)
			AudioServer.SetBusEffectEnabled(busIdx, fxIdx, enabled);
	}

	private static (int busIdx, int effectIdx) FindEffect(string busName, System.Type effectType)
	{
		var busIdx = AudioServer.GetBusIndex(busName);
		if (busIdx < 0) return (-1, -1);
		for (var i = 0; i < AudioServer.GetBusEffectCount(busIdx); i++)
		{
			var fx = AudioServer.GetBusEffect(busIdx, i);
			if (fx != null && effectType.IsInstanceOfType(fx))
				return (busIdx, i);
		}
		return (busIdx, -1);
	}

	private static void EnsureReverbEffect()
	{
		var (busIdx, fxIdx) = FindSfxReverb();
		if (busIdx < 0 || fxIdx >= 0) return;

		// Restrained "concrete bowl" space: short-ish tail, mostly dry — presence, not cathedral.
		var reverb = new AudioEffectReverb
		{
			RoomSize = 0.72f,
			Damping = 0.62f,
			Wet = 0.16f,
			Dry = 1.0f,
			Spread = 0.6f,
			PredelayMsec = 28f,
		};
		AudioServer.AddBusEffect(busIdx, reverb);
		var (_, newIdx) = FindSfxReverb();
		if (newIdx >= 0)
			AudioServer.SetBusEffectEnabled(busIdx, newIdx, false);
	}

	private static void EnsureMusicLowPassEffect()
	{
		var (busIdx, fxIdx) = FindMusicLowPass();
		if (busIdx < 0 || fxIdx >= 0) return;

		var lp = new AudioEffectLowPassFilter
		{
			CutoffHz = 20500f, // fully open at rest; MusicDirector sweeps it down in danger
			Resonance = 0.5f,
		};
		AudioServer.AddBusEffect(busIdx, lp);
		var (_, newIdx) = FindMusicLowPass();
		if (newIdx >= 0)
			AudioServer.SetBusEffectEnabled(busIdx, newIdx, false);
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
