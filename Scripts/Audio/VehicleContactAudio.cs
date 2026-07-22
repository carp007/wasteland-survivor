// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Audio/VehicleContactAudio.cs
// Purpose: Per-vehicle contact audio: pooled ram/crash one-shots (light/med/heavy by impact speed
//          loss) and a lateral-slip tire skid loop that fades with slip magnitude. No-ops when the
//          staged CC0 assets are absent.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.Audio;

/// <summary>
/// Contact audio component owned by VehiclePawn (added as a child at the pawn origin, so the
/// positional players track the vehicle automatically):
/// - <see cref="NotifyImpact"/>: plays a light/med/heavy crash one-shot via a small pooled set of
///   AudioStreamPlayer3D children (SFX bus), rate-limited per pawn so wall scraping can't
///   machine-gun samples.
/// - <see cref="UpdateContact"/>: fades a looping tire-skid AudioStreamPlayer3D in/out with lateral
///   slip magnitude, using the same engage thresholds as the skid-mark VFX in
///   VehiclePawn.UpdateTireVfx (slip &gt; 1.2 m/s while moving &gt; 3 m/s).
/// The pawn drives both calls from its _PhysicsProcess; this node holds no game logic.
/// </summary>
public partial class VehicleContactAudio : Node3D
{
	private const string CrashLightPath = "res://Assets/Audio/Vehicles/Impacts/veh_crash_light_a.wav";
	private const string CrashMedPath = "res://Assets/Audio/Vehicles/Impacts/veh_crash_med_a.wav";
	private const string CrashHeavyPath = "res://Assets/Audio/Vehicles/Impacts/veh_crash_heavy_a.wav";
	private const string SkidLoopPath = "res://Assets/Audio/Vehicles/Tires/veh_tire_skid_asphalt_loop_a.ogg";

	// Impact tiers: velocity lost in a single physics tick (m/s). Below Light = scrape = silent.
	public const float LightImpactSpeedLoss = 3f;
	public const float MedImpactSpeedLoss = 6f;
	public const float HeavyImpactSpeedLoss = 10f;

	// Per-pawn cooldown so prolonged scraping/grinding doesn't machine-gun crash samples.
	private const float ImpactCooldownSeconds = 0.35f;
	private const int CrashPoolSize = 3;

	// Skid engage thresholds — keep in sync with VehiclePawn.UpdateTireVfx (slip > 1.2, speed > 3).
	private const float SkidSlipEngageMps = 1.2f;
	private const float SkidMinForwardSpeedMps = 3.0f;
	// Slip magnitude that maps to the loudest skid volume.
	private const float SkidSlipFullMps = 6.0f;
	private const float SkidMaxDb = -6f;   // volume cap
	private const float SkidMinDb = -26f;  // volume right at the engage threshold
	private const float SkidFadeInRate = 9f;
	private const float SkidFadeOutRate = 5f;

	// Match VehicleEngineAudio 3D settings so contact SFX are audible from the fixed RTS camera.
	private const float MaxDistance3D = 450f;
	private const float UnitSize3D = 18f;

	private readonly AudioStreamPlayer3D?[] _crashPool = new AudioStreamPlayer3D?[CrashPoolSize];
	private int _crashPoolNext;
	private float _impactCooldown;

	private AudioStream? _crashLight;
	private AudioStream? _crashMed;
	private AudioStream? _crashHeavy;

	private AudioStreamPlayer3D? _skidPlayer;
	private AudioStream? _skidLoop;
	private float _skidWeight01;

	public override void _Ready()
	{
		AudioBusUtil.EnsureBuses();

		_crashLight = TryLoad(CrashLightPath);
		_crashMed = TryLoad(CrashMedPath);
		_crashHeavy = TryLoad(CrashHeavyPath);
		_skidLoop = TryLoadLooping(SkidLoopPath);

		// Small round-robin pool so overlapping impacts (multi-car pileups) don't cut each other off.
		if (_crashLight != null || _crashMed != null || _crashHeavy != null)
		{
			for (var i = 0; i < CrashPoolSize; i++)
			{
				var p = new AudioStreamPlayer3D
				{
					Name = $"Crash{i}",
					Bus = "SFX",
					MaxDistance = MaxDistance3D,
					UnitSize = UnitSize3D,
					Autoplay = false,
				};
				AddChild(p);
				_crashPool[i] = p;
			}
		}

		if (_skidLoop != null)
		{
			_skidPlayer = new AudioStreamPlayer3D
			{
				Name = "SkidLoop",
				Bus = "Tires",
				MaxDistance = MaxDistance3D,
				UnitSize = UnitSize3D,
				VolumeDb = SkidMinDb,
				Autoplay = false,
				Stream = _skidLoop,
			};
			AddChild(_skidPlayer);
		}
	}

	/// <summary>
	/// Called by the pawn after MoveAndSlide when slide contact removed velocity this tick.
	/// Picks the crash tier by speed loss; below the light threshold nothing plays (wall scrapes).
	/// </summary>
	public void NotifyImpact(float speedLossMps)
	{
		if (_impactCooldown > 0f) return;
		if (speedLossMps < LightImpactSpeedLoss) return;

		var stream = speedLossMps >= HeavyImpactSpeedLoss ? _crashHeavy
			: speedLossMps >= MedImpactSpeedLoss ? _crashMed
			: _crashLight;
		// Missing tier assets fall back to whatever crash sample exists.
		stream ??= _crashMed ?? _crashLight ?? _crashHeavy;
		if (stream == null) return;

		var player = _crashPool[_crashPoolNext];
		_crashPoolNext = (_crashPoolNext + 1) % CrashPoolSize;
		if (player == null || !GodotObject.IsInstanceValid(player)) return;

		// Slight variation so repeated hits don't sound identical; a touch quieter near threshold.
		var severity01 = Mathf.Clamp(
			(speedLossMps - LightImpactSpeedLoss) / (HeavyImpactSpeedLoss - LightImpactSpeedLoss), 0f, 1f);
		player.Stream = stream;
		player.PitchScale = (float)GD.RandRange(0.94, 1.06);
		player.VolumeDb = Mathf.Lerp(-5f, 0f, severity01);
		player.Play();

		_impactCooldown = ImpactCooldownSeconds;
	}

	/// <summary>
	/// Per-physics-tick update from the pawn: decrements the impact cooldown and fades the skid loop
	/// with lateral slip. vForward/vLateral are the same decomposed values the tire VFX uses.
	/// </summary>
	public void UpdateContact(float dt, float vForwardMps, float vLateralMps)
	{
		if (dt <= 0f) return;
		_impactCooldown = MathF.Max(0f, _impactCooldown - dt);

		if (_skidPlayer == null || !GodotObject.IsInstanceValid(_skidPlayer)) return;

		var slip = MathF.Abs(vLateralMps);
		var speed = MathF.Abs(vForwardMps);
		var engaged = slip > SkidSlipEngageMps && speed > SkidMinForwardSpeedMps;

		// Target loudness follows slip magnitude (floor keeps the just-engaged squeal audible).
		var target = engaged
			? Mathf.Clamp((slip - SkidSlipEngageMps) / MathF.Max(0.01f, SkidSlipFullMps - SkidSlipEngageMps), 0.15f, 1f)
			: 0f;

		var rate = target > _skidWeight01 ? SkidFadeInRate : SkidFadeOutRate;
		_skidWeight01 = Mathf.Lerp(_skidWeight01, target, 1f - Mathf.Exp(-rate * dt));

		if (_skidWeight01 <= 0.02f)
		{
			if (_skidPlayer.Playing)
				_skidPlayer.Stop();
			return;
		}

		_skidPlayer.VolumeDb = Mathf.Lerp(SkidMinDb, SkidMaxDb, _skidWeight01);
		if (!_skidPlayer.Playing)
			_skidPlayer.Play();
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

	private static AudioStream? TryLoadLooping(string path)
	{
		var stream = TryLoad(path);
		if (stream == null) return null;
		try
		{
			// Ensure the skid ogg loops seamlessly even if the import left looping off.
			if (stream is AudioStreamOggVorbis ogg)
				ogg.Loop = true;
			if (stream is AudioStreamWav wav)
				wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
		}
		catch
		{
			// If a stream type doesn't support toggling loop, ignore.
		}
		return stream;
	}
}
