// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/IO/JsonConfigStore.cs
// Purpose: Shared cached JSON config loading for runtime-tunable project config files.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Core.IO;

/// <summary>
/// Small reusable base for JSON-backed runtime config files. Provides cached loading, reload support,
/// default fallbacks, and consistent error logging so config-backed systems stop duplicating file I/O code.
/// </summary>
public abstract class JsonConfigStore<TConfig> where TConfig : class
{
	protected JsonConfigStore(string configPath)
	{
		ConfigPath = configPath;
	}

	public string ConfigPath { get; set; }

	private TConfig? _cached;

	public TConfig Get()
	{
		_cached ??= LoadInternal();
		return _cached;
	}

	public TConfig Reload()
	{
		_cached = LoadInternal();
		return _cached;
	}

	public void ClearCache() => _cached = null;

	protected virtual string LogName => GetType().Name;

	protected abstract TConfig CreateDefault();

	protected virtual TConfig Normalize(TConfig config) => config;

	private TConfig LoadInternal()
	{
		try
		{
			if (!FileAccess.FileExists(ConfigPath))
				return Normalize(CreateDefault());

			var json = FileAccess.GetFileAsString(ConfigPath);
			var parsed = JsonUtil.Deserialize<TConfig>(json);
			return Normalize(parsed ?? CreateDefault());
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[{LogName}] Failed to load '{ConfigPath}': {ex.Message}");
			return Normalize(CreateDefault());
		}
	}
}
