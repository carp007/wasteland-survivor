// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/GameSettingsStore.cs
// Purpose: User-adjustable app settings (audio volumes, display) persisted to user://settings.json.
// -------------------------------------------------------------------------------------------------
using System;
using System.Text.Json;
using Godot;
using WastelandSurvivor.Core.IO;

namespace WastelandSurvivor.Game;

public sealed record GameSettingsData
{
	public float MasterVolume01 { get; init; } = 1.0f;
	public float SfxVolume01 { get; init; } = 1.0f;
	public float EnginesVolume01 { get; init; } = 1.0f;
	public float MusicVolume01 { get; init; } = 1.0f;
	public bool StartFullscreen { get; init; } = true;
}

/// <summary>
/// Loads/saves user settings (separate from the save game — settings survive save wipes) and applies
/// them to the engine. Audio sliders are user gains layered on top of the fixed mix trims that
/// <see cref="Audio.AudioBusUtil"/> establishes, so 100% always means "the intended default mix".
/// </summary>
public static class GameSettingsStore
{
	private const string SettingsPath = "user://settings.json";

	// Must match the mix trims in AudioBusUtil.EnsureBuses().
	private const float SfxTrimDb = 0f;
	private const float EnginesTrimDb = 8f;
	private const float MusicTrimDb = -8f;

	private static bool _loaded;

	public static GameSettingsData Current { get; private set; } = new();

	public static GameSettingsData Load()
	{
		if (_loaded) return Current;
		_loaded = true;

		try
		{
			if (FileAccess.FileExists(SettingsPath))
			{
				using var f = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
				if (f != null)
				{
					var json = f.GetAsText();
					var data = JsonSerializer.Deserialize<GameSettingsData>(json, JsonUtil.Options);
					if (data != null)
						Current = Sanitize(data);
				}
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[Settings] Failed to load settings, using defaults: {ex.Message}");
			Current = new GameSettingsData();
		}

		return Current;
	}

	public static void Save(GameSettingsData data)
	{
		Current = Sanitize(data);
		try
		{
			using var f = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Write);
			if (f == null)
			{
				GD.PrintErr($"[Settings] Failed to open {SettingsPath} for write.");
				return;
			}
			f.StoreString(JsonSerializer.Serialize(Current, JsonUtil.Options));
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[Settings] Failed to save settings: {ex.Message}");
		}
	}

	/// <summary>Apply the current audio volumes to the engine buses (call after EnsureBuses).</summary>
	public static void ApplyAudio()
	{
		TrySetBusDb("Master", VolumeToDb(Current.MasterVolume01, trimDb: 0f));
		TrySetBusDb("SFX", VolumeToDb(Current.SfxVolume01, SfxTrimDb));
		TrySetBusDb("Engines", VolumeToDb(Current.EnginesVolume01, EnginesTrimDb));
		TrySetBusDb("Music", VolumeToDb(Current.MusicVolume01, MusicTrimDb));
	}

	private static float VolumeToDb(float volume01, float trimDb)
	{
		volume01 = Mathf.Clamp(volume01, 0f, 1f);
		if (volume01 <= 0.001f) return -80f; // effectively mute
		return trimDb + Mathf.LinearToDb(volume01);
	}

	private static void TrySetBusDb(string busName, float volumeDb)
	{
		var idx = AudioServer.GetBusIndex(busName);
		if (idx < 0) return;
		AudioServer.SetBusVolumeDb(idx, volumeDb);
	}

	private static GameSettingsData Sanitize(GameSettingsData data) => data with
	{
		MasterVolume01 = Mathf.Clamp(data.MasterVolume01, 0f, 1f),
		SfxVolume01 = Mathf.Clamp(data.SfxVolume01, 0f, 1f),
		EnginesVolume01 = Mathf.Clamp(data.EnginesVolume01, 0f, 1f),
		MusicVolume01 = Mathf.Clamp(data.MusicVolume01, 0f, 1f),
	};
}
