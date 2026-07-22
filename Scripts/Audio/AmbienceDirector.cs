// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Audio/AmbienceDirector.cs
// Purpose: Quiet background ambience loops layered under the music. Plays an industrial city hum
//          while in city/menu mode and desert wind during arena combat, following the same
//          MusicDirector.CombatActive flag the music switching already uses. No-ops if the local
//          ambience assets are absent.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.Audio;

public partial class AmbienceDirector : Node
{
	private const string CityLoopPath = "res://Assets/Audio/Ambience/amb_city_industrial_loop_a.ogg";
	private const string CombatLoopPath = "res://Assets/Audio/Ambience/amb_wind_desert_loop_a.ogg";
	// Arena crowd bed + swell one-shots: not staged yet (sourcing gap — Kenney "Crowd" pack or a
	// freesound CC0 stadium loop). Everything below no-ops until the files land at these paths.
	private const string CrowdBedLoopPath = "res://Assets/Audio/Ambience/amb_crowd_arena_loop_a.ogg";
	private static readonly string[] CrowdSwellPaths =
	{
		"res://Assets/Audio/Ambience/amb_crowd_swell_a.ogg",
		"res://Assets/Audio/Ambience/amb_crowd_swell_b.ogg",
		"res://Assets/Audio/Ambience/amb_crowd_swell_c.ogg",
	};
	private const float PollSeconds = 0.6f;

	// Ambience sits well under the music bed.
	private const float AmbienceVolumeDb = -16f;
	private const float CrowdBedVolumeDb = -20f;
	private const float CrowdSwellVolumeDb = -12f;

	/// <summary>Live instance (set while the director is in the tree); used by the static swell helper.</summary>
	public static AmbienceDirector? Instance { get; private set; }

	/// <summary>
	/// Whether the current combat venue actually has a crowd (city stadium = yes; roadside salvage
	/// yards / interceptions = no). Gates the crowd bed AND the reaction swells so a wreck yard in
	/// the middle of nowhere doesn't cheer. Set by the arena view when the world is configured.
	/// </summary>
	public static bool CrowdPresent { get; set; } = true;

	// Reaction swells: minimum spacing so a multi-toast moment (tire + section + mobility in one
	// volley) reads as one roar, not a stutter.
	private const float SwellCooldownSeconds = 1.4f;
	private double _lastSwellAtMs = -10000;

	private AudioStreamPlayer? _player;
	private AudioStream? _cityLoop;
	private AudioStream? _combatLoop;
	private AudioStreamPlayer? _crowdBedPlayer;
	private AudioStreamPlayer? _crowdSwellPlayer;
	private readonly System.Collections.Generic.List<AudioStream> _crowdSwells = new();
	private bool _combatMode;
	private float _pollRemaining;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always; // keep ambience alive through pause

		AudioBusUtil.EnsureBuses();
		_cityLoop = TryLoadLooping(CityLoopPath);
		_combatLoop = TryLoadLooping(CombatLoopPath);

		var crowdBedLoop = TryLoadLooping(CrowdBedLoopPath);
		foreach (var path in CrowdSwellPaths)
		{
			var swell = TryLoadLooping(path);
			if (swell is AudioStreamOggVorbis swellOgg)
				swellOgg.Loop = false; // swells are one-shots
			if (swell != null)
				_crowdSwells.Add(swell);
		}

		if (_cityLoop == null && _combatLoop == null && crowdBedLoop == null)
		{
			GD.Print("[Ambience] No ambience loops found under Assets/Audio/Ambience; director idle.");
			SetProcess(false);
			return;
		}

		// AudioBusUtil currently defines Master/SFX/Engines/Tires/Music; ambience rides the Music
		// bus (music-like bed, ducked by the same mixer slider) with a quiet per-player gain.
		_player = new AudioStreamPlayer
		{
			Name = "AmbiencePlayer",
			Bus = "Music",
			VolumeDb = AmbienceVolumeDb,
			Autoplay = false,
		};
		AddChild(_player);
		_player.Finished += OnLoopFinished;

		if (crowdBedLoop != null)
		{
			// Quiet stadium murmur layered over the desert wind while a match is live.
			_crowdBedPlayer = new AudioStreamPlayer
			{
				Name = "CrowdBedPlayer",
				Bus = "Music",
				VolumeDb = CrowdBedVolumeDb,
				Autoplay = false,
				Stream = crowdBedLoop,
			};
			AddChild(_crowdBedPlayer);
			_crowdBedPlayer.Finished += OnCrowdBedFinished;
		}

		PlayCurrent();
	}

	/// <summary>
	/// One-shot crowd reaction (kill shots, big hits). Picks a random staged swell variant; no-ops
	/// when no crowd assets exist or no director is live, so call sites never need guards.
	/// </summary>
	public static void PlayCrowdSwell() => Instance?.PlayCrowdSwellInternal();

	private void PlayCrowdSwellInternal()
	{
		if (_crowdSwells.Count == 0 || !CrowdPresent) return;

		var nowMs = Time.GetTicksMsec();
		if (nowMs - _lastSwellAtMs < SwellCooldownSeconds * 1000.0) return;
		_lastSwellAtMs = nowMs;

		if (_crowdSwellPlayer == null || !GodotObject.IsInstanceValid(_crowdSwellPlayer))
		{
			_crowdSwellPlayer = new AudioStreamPlayer
			{
				Name = "CrowdSwellPlayer",
				Bus = "Music",
				VolumeDb = CrowdSwellVolumeDb,
				Autoplay = false,
			};
			AddChild(_crowdSwellPlayer);
		}

		_crowdSwellPlayer.Stream = _crowdSwells[(int)(GD.Randi() % (uint)_crowdSwells.Count)];
		_crowdSwellPlayer.Play();
	}

	private void OnCrowdBedFinished()
	{
		// Fallback loop for streams whose import settings don't loop.
		if (_combatMode && _crowdBedPlayer != null)
			_crowdBedPlayer.Play();
	}

	public override void _Process(double delta)
	{
		_pollRemaining -= (float)delta;
		if (_pollRemaining > 0f) return;
		_pollRemaining = PollSeconds;

		if (MusicDirector.CombatActive != _combatMode)
		{
			_combatMode = MusicDirector.CombatActive;
			PlayCurrent();
		}
		else
		{
			// CrowdPresent can flip between venue kinds without a combat-mode edge.
			UpdateCrowdBed();
		}

		// Venue acoustics: stadium fights get the light concrete-bowl reverb on the SFX tree;
		// wreck yards, highways, and the city menus stay dry. Same poll cadence as the beds.
		AudioBusUtil.SetSfxReverbEnabled(_combatMode && CrowdPresent);
	}

	private void OnLoopFinished()
	{
		// Fallback loop for streams whose import settings don't loop.
		PlayCurrent();
	}

	private void PlayCurrent()
	{
		UpdateCrowdBed();

		if (_player == null) return;
		var stream = _combatMode ? (_combatLoop ?? _cityLoop) : (_cityLoop ?? _combatLoop);
		if (stream == null) return;
		_player.Stream = stream;
		_player.Play();
	}

	private void UpdateCrowdBed()
	{
		if (_crowdBedPlayer == null || !GodotObject.IsInstanceValid(_crowdBedPlayer)) return;
		if (_combatMode && CrowdPresent)
		{
			if (!_crowdBedPlayer.Playing)
				_crowdBedPlayer.Play();
		}
		else if (_crowdBedPlayer.Playing)
		{
			_crowdBedPlayer.Stop();
		}
	}

	private static AudioStream? TryLoadLooping(string path)
	{
		try
		{
			if (!ResourceLoader.Exists(path)) return null;
			var stream = GD.Load<AudioStream>(path);

			// Ensure ogg ambience loops seamlessly even if the import left looping off.
			if (stream is AudioStreamOggVorbis ogg)
				ogg.Loop = true;

			return stream;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
