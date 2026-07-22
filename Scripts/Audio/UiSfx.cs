// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Audio/UiSfx.cs
// Purpose: Global UI sound effects (click/hover/confirm/error/purchase). AppRoot adds one instance;
//          callers use the static UiSfx.Play("click") API. Everything no-ops if assets are absent.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Godot;

namespace WastelandSurvivor.Game.Audio;

public partial class UiSfx : Node
{
	private const string BusName = "SFX";

	private static UiSfx? _instance;

	private sealed record SoundSpec(string Path, float VolumeDb);

	// Hover is intentionally very quiet and click quiet; they fire constantly during menu use.
	private static readonly Dictionary<string, SoundSpec> Specs = new(StringComparer.OrdinalIgnoreCase)
	{
		["click"] = new SoundSpec("res://Assets/Audio/UI/ui_click_a.wav", -10f),
		["hover"] = new SoundSpec("res://Assets/Audio/UI/ui_hover_a.wav", -18f),
		["confirm"] = new SoundSpec("res://Assets/Audio/UI/ui_confirm_a.wav", -8f),
		["error"] = new SoundSpec("res://Assets/Audio/UI/ui_error_a.wav", -8f),
		["purchase"] = new SoundSpec("res://Assets/Audio/UI/ui_purchase_a.wav", -8f),
		// City-side travel departure cue (legacy asset — see ATTRIBUTION_AND_LICENSES.md).
		["ignition"] = new SoundSpec("res://Assets/Audio/Vehicles/ignition.wav", -6f),
	};

	private readonly Dictionary<string, AudioStreamPlayer> _players = new(StringComparer.OrdinalIgnoreCase);

	public override void _EnterTree()
	{
		_instance = this;
	}

	public override void _ExitTree()
	{
		if (_instance == this)
			_instance = null;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always; // UI sounds keep working while the game is paused

		AudioBusUtil.EnsureBuses();

		foreach (var kv in Specs)
		{
			var stream = TryLoad(kv.Value.Path);
			if (stream == null)
				continue;

			var player = new AudioStreamPlayer
			{
				Name = $"UiSfx_{kv.Key}",
				Bus = BusName,
				VolumeDb = kv.Value.VolumeDb,
				Stream = stream,
				Autoplay = false,
			};
			AddChild(player);
			_players[kv.Key] = player;
		}

		if (_players.Count == 0)
			GD.Print("[UiSfx] No UI sound assets found under Assets/Audio/UI; UI sfx idle.");
	}

	/// <summary>
	/// Plays a named UI sound: "click", "hover", "confirm", "error", "purchase", or "ignition".
	/// Safe to call from anywhere; no-ops when the instance or asset is missing.
	/// </summary>
	public static void Play(string soundName)
	{
		var self = _instance;
		if (self == null || !GodotObject.IsInstanceValid(self))
			return;

		if (!self._players.TryGetValue(soundName, out var player))
			return;
		if (player == null || !GodotObject.IsInstanceValid(player))
			return;

		player.Play();
	}

	private static AudioStream? TryLoad(string path)
	{
		try
		{
			if (!ResourceLoader.Exists(path)) return null;
			return GD.Load<AudioStream>(path);
		}
		catch (Exception)
		{
			return null;
		}
	}
}
