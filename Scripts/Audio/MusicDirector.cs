// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Audio/MusicDirector.cs
// Purpose: Lightweight background-music controller. Crossfades (~1.2s) between a city/menu track
//          and a combat track while an arena match is live, remembering per-mode playback positions
//          so re-entering the city resumes where it left off. Tracks are optional local assets
//          (CC-BY, see Docs/Audio/ATTRIBUTION_AND_LICENSES.md) and everything no-ops if absent.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.Audio;

public partial class MusicDirector : Node
{
	private const string CityTrackPath = "res://Assets/Audio/Music/industrial_cinematic_kmacleod.mp3";
	private const string CombatTrackPath = "res://Assets/Audio/Music/mechanolith_kmacleod.mp3";
	private const string StingerWinPath = "res://Assets/Audio/Music/stinger_win_a.ogg";
	private const string StingerLossPath = "res://Assets/Audio/Music/stinger_loss_a.ogg";
	private const float PollSeconds = 0.6f;
	private const float StingerDuckDb = -10f;
	private const float CrossfadeSeconds = 1.2f;
	private const float SilenceDb = -60f;

	// Two players so mode switches crossfade instead of hard-cutting/restarting.
	private readonly AudioStreamPlayer?[] _players = new AudioStreamPlayer?[2];
	private readonly float[] _weights = new float[2];
	private int _active;

	private AudioStreamPlayer? _stingerPlayer;
	private AudioStream? _cityTrack;
	private AudioStream? _combatTrack;
	private AudioStream? _stingerWin;
	private AudioStream? _stingerLoss;
	private bool _combatMode;
	private float _pollRemaining;
	// Additional gain applied to both music players (stinger duck).
	private float _duckDb;
	// Per-mode resume positions so re-entering city/combat picks the track up where it left off.
	private float _cityResumeSeconds;
	private float _combatResumeSeconds;

	public static bool CombatActive { get; set; }

	/// <summary>
	/// 0..1 danger intensity (1 = driver critical). Set by the arena view each frame; sweeps a
	/// low-pass over the music bed and ducks it slightly so near-death reads in the mix. Decays
	/// automatically outside combat, so stale values can't muffle the city.
	/// </summary>
	public static float DangerLevel { get; set; }

	// Smoothed danger follower + the extra duck it contributes to the bed players.
	private float _dangerSmoothed;
	private const float DangerDuckMaxDb = -4.5f;
	private const float DangerLowPassOpenHz = 20500f;
	private const float DangerLowPassClosedHz = 1050f;

	/// <summary>Live instance (set while the director is in the tree); used by the static stinger helper.</summary>
	public static MusicDirector? Instance { get; private set; }

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
		ProcessMode = ProcessModeEnum.Always; // keep music alive through pause

		AudioBusUtil.EnsureBuses();
		_cityTrack = TryLoad(CityTrackPath);
		_combatTrack = TryLoad(CombatTrackPath);
		_stingerWin = TryLoad(StingerWinPath);
		_stingerLoss = TryLoad(StingerLossPath);
		if (_cityTrack == null && _combatTrack == null)
		{
			GD.Print("[Music] No music tracks found under Assets/Audio/Music; director idle.");
			SetProcess(false);
			return;
		}

		for (var i = 0; i < _players.Length; i++)
		{
			var player = new AudioStreamPlayer
			{
				Name = $"MusicPlayer{(char)('A' + i)}",
				Bus = "Music",
				Autoplay = false,
				VolumeDb = SilenceDb,
			};
			AddChild(player);
			var idx = i;
			player.Finished += () => OnTrackFinished(idx);
			_players[i] = player;
		}

		_combatMode = CombatActive;
		PlayCurrent();
	}

	public override void _Process(double delta)
	{
		// Crossfade weights advance every frame; the mode flag polls on a slower cadence.
		UpdateFadeVolumes((float)delta);
		UpdateDangerState((float)delta);

		_pollRemaining -= (float)delta;
		if (_pollRemaining > 0f) return;
		_pollRemaining = PollSeconds;

		if (CombatActive != _combatMode)
		{
			_combatMode = CombatActive;
			PlayCurrent();
		}
	}

	/// <summary>
	/// Smoothly follows <see cref="DangerLevel"/>: sweeps the Music-bus low-pass toward closed and
	/// leans on the bed volume as the driver gets critical. Outside combat the level decays on its
	/// own so a match that ends at 1.0 never leaves the city muffled.
	/// </summary>
	private void UpdateDangerState(float dt)
	{
		if (!CombatActive)
			DangerLevel = MathF.Max(0f, DangerLevel - dt * 1.5f);

		var target = Mathf.Clamp(DangerLevel, 0f, 1f);
		_dangerSmoothed = Mathf.MoveToward(_dangerSmoothed, target, dt * 1.6f);

		var (busIdx, fxIdx) = AudioBusUtil.FindMusicLowPass();
		if (busIdx >= 0 && fxIdx >= 0)
		{
			var wantEnabled = _dangerSmoothed > 0.02f;
			if (AudioServer.IsBusEffectEnabled(busIdx, fxIdx) != wantEnabled)
				AudioServer.SetBusEffectEnabled(busIdx, fxIdx, wantEnabled);

			if (wantEnabled && AudioServer.GetBusEffect(busIdx, fxIdx) is AudioEffectLowPassFilter lp)
			{
				// Exponential sweep reads more natural than linear on a cutoff.
				var t = MathF.Pow(_dangerSmoothed, 1.4f);
				lp.CutoffHz = Mathf.Lerp(DangerLowPassOpenHz, DangerLowPassClosedHz, t);
			}
		}

		ApplyAllPlayerVolumes();
	}

	private void OnTrackFinished(int idx)
	{
		// Outgoing fade player ended on its own; the fade logic stops/reuses it.
		if (idx != _active) return;

		// Loop the active selection from the start (and forget the stale resume position).
		SetResume(_combatMode, 0f);
		var p = _players[idx];
		if (p != null && GodotObject.IsInstanceValid(p) && p.Stream != null)
			p.Play();
	}

	/// <summary>
	/// Static convenience for match-end call sites: plays the win/loss stinger if a director is live.
	/// </summary>
	public static void PlayMatchStinger(bool won) => Instance?.PlayStinger(won);

	/// <summary>
	/// City-side danger cue (road ambush modal). Reuses the loss stinger's ominous hit until a
	/// dedicated ambush stinger is staged. Safe to call with no director live.
	/// </summary>
	public static void PlayDangerStinger() => Instance?.PlayStinger(won: false);

	/// <summary>
	/// Ducks the current music by ~10 dB, plays the win/loss stinger once on the Music bus, then
	/// restores the music volume when the stinger finishes. No-ops if the stinger asset is absent.
	/// Works even when no background tracks were found (the stinger still plays).
	/// </summary>
	public void PlayStinger(bool won)
	{
		var stream = won ? _stingerWin : _stingerLoss;
		if (stream == null) return;

		if (_stingerPlayer == null || !GodotObject.IsInstanceValid(_stingerPlayer))
		{
			_stingerPlayer = new AudioStreamPlayer { Name = "StingerPlayer", Bus = "Music", Autoplay = false };
			AddChild(_stingerPlayer);
			_stingerPlayer.Finished += OnStingerFinished;
		}

		_duckDb = StingerDuckDb;
		ApplyAllPlayerVolumes(); // duck immediately even if processing is idle

		_stingerPlayer.Stream = stream;
		_stingerPlayer.Play();
	}

	private void OnStingerFinished()
	{
		// Restore the music bed to its normal per-player gain.
		_duckDb = 0f;
		ApplyAllPlayerVolumes();
	}

	/// <summary>
	/// Starts (or crossfades to) the track for the current mode. When both modes resolve to the same
	/// stream (only one track installed) the bed keeps playing untouched — no cut, no restart.
	/// </summary>
	private void PlayCurrent()
	{
		var target = ResolveStream(_combatMode);
		if (target == null) return;

		var active = _players[_active];
		if (active != null && active.Stream == target)
		{
			if (!active.Playing)
				active.Play(ClampResume(target, GetResume(_combatMode)));
			return;
		}

		// Remember where the outgoing mode's track left off before fading it out.
		if (active != null && GodotObject.IsInstanceValid(active) && active.Playing)
			SetResume(!_combatMode, active.GetPlaybackPosition());

		var next = 1 - _active;
		var incoming = _players[next];
		if (incoming == null || !GodotObject.IsInstanceValid(incoming)) return;
		incoming.Stream = target;
		incoming.Play(ClampResume(target, GetResume(_combatMode)));
		_active = next;
		// _weights move toward the new active player in UpdateFadeVolumes (equal-power crossfade).
	}

	private void UpdateFadeVolumes(float dt)
	{
		var step = dt / MathF.Max(0.05f, CrossfadeSeconds);
		for (var i = 0; i < _players.Length; i++)
		{
			var p = _players[i];
			if (p == null || !GodotObject.IsInstanceValid(p)) continue;

			var targetWeight = i == _active ? 1f : 0f;
			_weights[i] = Mathf.MoveToward(_weights[i], targetWeight, step);
			ApplyPlayerVolume(i);

			// Fully faded out: release the voice (its resume position was saved at switch time).
			if (i != _active && _weights[i] <= 0.0001f && p.Playing)
				p.Stop();
		}
	}

	private void ApplyAllPlayerVolumes()
	{
		for (var i = 0; i < _players.Length; i++)
			ApplyPlayerVolume(i);
	}

	private void ApplyPlayerVolume(int i)
	{
		var p = _players[i];
		if (p == null || !GodotObject.IsInstanceValid(p)) return;

		// Equal-power curve keeps perceived loudness steady through the middle of the crossfade.
		var gain = MathF.Sin(Mathf.Clamp(_weights[i], 0f, 1f) * MathF.PI * 0.5f);
		var db = gain <= 0.001f ? SilenceDb : MathF.Max(SilenceDb, Mathf.LinearToDb(gain));
		p.VolumeDb = db + _duckDb + DangerDuckMaxDb * _dangerSmoothed;
	}

	private AudioStream? ResolveStream(bool combat)
		=> combat ? (_combatTrack ?? _cityTrack) : (_cityTrack ?? _combatTrack);

	private float GetResume(bool combat) => combat ? _combatResumeSeconds : _cityResumeSeconds;

	private void SetResume(bool combat, float seconds)
	{
		if (combat) _combatResumeSeconds = MathF.Max(0f, seconds);
		else _cityResumeSeconds = MathF.Max(0f, seconds);
	}

	private static float ClampResume(AudioStream stream, float pos)
	{
		if (pos <= 0f) return 0f;
		var len = (float)stream.GetLength();
		// If the saved position is at/past the end (or length is unknown), restart from the top.
		if (len <= 0.05f || pos >= len - 0.5f) return 0f;
		return pos;
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
