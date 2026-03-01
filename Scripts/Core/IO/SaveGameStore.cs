// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/IO/SaveGameStore.cs
// Purpose: Loads/saves SaveGameState to user://savegame.json using System.Text.Json (Godot-independent save format).
// -------------------------------------------------------------------------------------------------
using System;
using System.Text.Json;
using Godot;
using WastelandSurvivor.Game;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Core.IO;

/// <summary>
/// Loads/saves the SaveGameState to user://savegame.json.
/// Keeps the save format independent from Godot nodes.
/// </summary>
public sealed class SaveGameStore
{
	public const string DefaultSavePath = "user://savegame.json";

	public string SavePath { get; }

	public SaveGameStore(string? savePath = null)
	{
		SavePath = string.IsNullOrWhiteSpace(savePath) ? DefaultSavePath : savePath!;
	}

	public SaveGameState LoadOrCreateDefault()
	{
		try
		{
			if (!FileAccess.FileExists(SavePath))
				return CreateDefault();

			var json = FileAccess.GetFileAsString(SavePath);
			var save = JsonSerializer.Deserialize<SaveGameState>(json, JsonUtil.Options);
			return save ?? CreateDefault();
		}
		catch (Exception ex)
		{
			GameLog.Error($"[Save] Failed to load save '{SavePath}': {ex.Message}. Creating default.");
			return CreateDefault();
		}
	}

	public void Save(SaveGameState state)
	{
		try
		{
			var json = JsonSerializer.Serialize(state with { TimestampUtc = DateTime.UtcNow }, JsonUtil.Options);
			using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
			f.StoreString(json);
			GameLog.Debug($"[Save] Wrote {SavePath}");
		}
		catch (Exception ex)
		{
			GameLog.Error($"[Save] Failed to write save '{SavePath}': {ex.Message}");
		}
	}

	private static SaveGameState CreateDefault()
	{
		return new SaveGameState
		{
			Version = 3,
			Player = new PlayerProfileState
			{
				MoneyUsd = 1000,
				Scrap = 0,
				CurrentCityId = "detroit",
				LastRespawnCityId = "detroit",
				OwnedVehicleIds = new(),
				ActiveVehicleId = null
			}
		};
	}
}
