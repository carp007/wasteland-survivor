// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Audio/VehicleEngineAudio.cs
// Purpose: Layered positional engine audio component. Crossfades RPM layers, maps speed/throttle to RPM, and plays tire/brake SFX hooks.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.Audio;

/// <summary>
/// Layered, crossfaded vehicle engine audio:
/// - 5 simultaneous looping layers (idle/low/mid/high/very_high)
/// - Crossfade volumes based on a smoothed normalized RPM value (0..1)
/// - Optional subtle pitch scaling
/// - Optional hard-brake skid loop (fades in/out with brake state)
///
/// Asset convention (per Docs/Audio/Engine_Archetypes.md):
/// res://Assets/Audio/Vehicles/Engines/veh_engine_{archetype}_loop_{layer}_a.ogg
/// where archetype is typically: i4_compact | v8_muscle | diesel_truck
/// ("engine_" prefix is tolerated and stripped).
/// </summary>
public partial class VehicleEngineAudio : Node3D
{

	// -------------------------------------------------------------------------------------------------
	// File navigation (high level)
	// - SetTelemetrySource(): bind to a pawn that implements IVehicleAudioTelemetry
	// - SetArchetype(): selects which audio layer set to use (gas/diesel/etc.)
	// - _Process(): maps telemetry (speed, throttle, gear intent) to RPM + layer crossfades
	// - Brake skid: looping squeal faded by UpdateSkid (optional; depends on Assets)
	// -------------------------------------------------------------------------------------------------

	public enum Layer
	{
		Idle,
		Low,
		Mid,
		High,
		VeryHigh
	}

	[Export] public string ArchetypeId = "i4_compact";
	[Export] public bool AutoStart = true;
	// The loops are mastered fairly low to avoid clipping when multiple vehicles are present.
	// We still want engines to be clearly audible from the top-down camera.
	[Export] public float EngineVolumeDb = -2f;
	[Export] public float MinLayerDb = -80f;

	[ExportGroup("RPM")]
	[Export] public float SpeedWeight = 0.7f;
	[Export] public float ThrottleWeight = 0.3f;
	[Export] public float RpmSmoothing = 10f;
	[Export] public float CrossfadeWidth = 0.25f;

	[ExportGroup("Mix / Layer logic")]
	// --- NOTE -------------------------------------------------------------
	// The default system supports a 5-layer crossfaded loop set.
	// For early builds (and per user feedback), we also support a simplified
	// mode: idle loop + a single "drive" loop with pitch progression.
	// ----------------------------------------------------------------------

	/// <summary>
	/// When throttle is effectively zero, prefer the idle loop.
	/// </summary>
	[Export(PropertyHint.Range, "0,0.25,0.005")] public float IdleThrottleThreshold01 = 0.03f;
	/// <summary>
	/// Smoothing rate for transitioning between idle-loop and drive-loop blends.
	/// Higher = snappier transitions. Kept moderate: snappy blends turn AI throttle pulses into
	/// audible on/off tone stabs.
	/// </summary>
	[Export] public float IdleDriveBlendSmoothing = 6f;
	/// <summary>
	/// RPM response smoothing for throttle increases (higher = snappier rev-up).
	/// Lower values make RPM rise more gradually when the player first hits the accelerator.
	/// </summary>
	[Export] public float ThrottleRpmSmoothingUp = 5.0f;
	/// <summary>
	/// RPM response smoothing for throttle decreases (higher = snappier drop).
	/// Usually higher than <see cref="ThrottleRpmSmoothingUp"/> so RPM settles quickly when letting off.
	/// </summary>
	[Export] public float ThrottleRpmSmoothingDown = 7.0f;

	[ExportGroup("Simple drive loop (override)")]
	/// <summary>
	/// If enabled, use ONLY the archetype idle loop while coasting,
	/// and a single drive loop (typically a "low" loop) with pitch progression while throttling.
	/// Default off: each staged loop set is a matched 5-layer export of one source vehicle, so the
	/// crossfade mode is safe. The old "fly buzzing" came from layering mismatched archetypes
	/// (every vehicle used its own idle but the shared diesel drive loop).
	/// </summary>
	[Export] public bool UseSingleDriveLoop = false;
	/// <summary>
	/// Archetype id for the drive loop when <see cref="UseSingleDriveLoop"/> is enabled.
	/// Empty (default) follows <see cref="ArchetypeId"/> so every vehicle keeps its own engine voice.
	/// </summary>
	[Export] public string DriveLoopArchetypeId = "";
	/// <summary>Layer id to load for the drive loop when <see cref="UseSingleDriveLoop"/> is enabled (usually "low").</summary>
	[Export] public string DriveLoopLayerId = "low";
	/// <summary>Enable pitch progression for the drive loop (recommended).</summary>
	[Export] public bool DriveUsesPitchProgression = true;
	/// <summary>Minimum pitch for the drive loop when RPM is low.</summary>
	[Export] public float DrivePitchMin = 0.92f;
	/// <summary>Maximum pitch for the drive loop near redline.</summary>
	[Export] public float DrivePitchMax = 1.55f;
	/// <summary>Exponent for pitch curve (1 = linear, &gt;1 biases change toward higher RPM).</summary>
	[Export] public float DrivePitchExponent = 1.15f;
	/// <summary>
	/// Most loop sets already include RPM layers; pitch-scaling can read as a "buzz".
	/// Default off.
	/// </summary>
	[Export] public bool EnablePitchScaling = false;
	[Export] public float PitchMin = 0.96f;
	[Export] public float PitchMax = 1.12f;

	[ExportGroup("RPM pitch tracking (5-layer mode)")]
	/// <summary>
	/// Rate-matched crossfading: bend each RPM layer's pitch toward the CURRENT engine RPM so two
	/// crossfading layers always sound like the same engine speed. Without this every loop plays at
	/// its recorded pitch and an RPM sweep steps through five discrete tones — play-testing described
	/// it as "randomly hitting keys on an organ". This is how layered engines work in racing games.
	/// </summary>
	[Export] public bool UseRpmPitchTracking = true;
	/// <summary>
	/// Perceived idle-to-redline frequency ratio of the loop ladder. Adjacent layers sit 0.25 rpm01
	/// apart, so at a crossfade midpoint each layer bends ~ratio^0.125 (~11% at 2.3) toward the other.
	/// </summary>
	[Export] public float RevFrequencyRatio = 2.3f;
	/// <summary>Safety clamp so far-from-anchor layers never chipmunk (they are near-silent anyway).</summary>
	[Export] public float LayerPitchMin = 0.80f;
	[Export] public float LayerPitchMax = 1.32f;
	[Export] public float RandomizeStartMaxSeconds = 0.12f;
	[Export] public float MaxSpeedMpsOverride = 0f;

	[ExportGroup("Transmission (auto)")]
	[Export] public bool UseAutomaticTransmission = true;
	[Export(PropertyHint.Range, "2,6,1")] public int GearCount = 4;
	/// <summary>Speed hysteresis (0..1) used for downshifts to prevent gear hunting.</summary>
	[Export(PropertyHint.Range, "0,0.20,0.005")] public float GearDownshiftHysteresis01 = 0.08f;
	
	// Speed band thresholds (normalized to max speed) for 4-gear tuning.
	[Export(PropertyHint.Range, "0.05,0.45,0.01")] public float Gear1To2Speed01 = 0.18f;
	[Export(PropertyHint.Range, "0.10,0.85,0.01")] public float Gear2To3Speed01 = 0.40f;
	[Export(PropertyHint.Range, "0.20,0.98,0.01")] public float Gear3To4Speed01 = 0.68f;

	/// <summary>Idle RPM floor (0..1) used by the pedal model.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float IdleRpm01 = 0.12f;
	/// <summary>
	/// Maximum RPM (0..1) allowed at (near) zero speed when the player floors the accelerator.
	/// Helps avoid instantly jumping to redline at launch.
	/// </summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float StallRpm01 = 0.62f;
	/// <summary>Upshift when engine RPM within current gear exceeds this threshold (0..1).</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float ShiftUpRpm01 = 0.93f;
	/// <summary>Downshift when engine RPM within current gear drops below this threshold (0..1).</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float ShiftDownRpm01 = 0.32f;
	/// <summary>Minimum pedal before upshifts are allowed.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float ShiftMinThrottle01 = 0.45f;
	[Export] public float ShiftCooldownSeconds = 0.50f;
	/// <summary>How strongly the pedal can pull RPM above wheel-speed RPM (0..1).</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float PedalRevStrength = 0.75f;
	/// <summary>RPM target after an upshift (simulates RPM drop on shift).</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float ShiftRpmDrop01 = 0.55f;

	[ExportGroup("HUD Display")]
	[Export] public int DisplayIdleRpm = 900;
	[Export] public int DisplayRedlineRpm = 6500;

	[ExportGroup("3D Settings")]
	// Defaults tuned for our current camera distance/angle.
	[Export] public float EngineMaxDistance = 450f;
	[Export] public float EngineUnitSize = 18.0f;
	[Export] public float SkidMaxDistance = 500f;
	[Export] public float SkidUnitSize = 18.0f;

	[ExportGroup("Layer trims (dB)")]
	[Export] public float TrimIdleDb = 0f;
	[Export] public float TrimLowDb = 0f;
	[Export] public float TrimMidDb = 0f;
	[Export] public float TrimHighDb = 0f;
	[Export] public float TrimVeryHighDb = 0f;

	[ExportGroup("Skid (hard braking)")]
	[Export] public bool EnableSkid = true;
	[Export] public float BrakeHardThreshold01 = 0.75f;
	[Export] public float BrakeMinSpeedMps = 7.0f;
	[Export] public float BrakeHoldSeconds = 0.25f;
	[Export] public float SkidVolumeDb = -6f;
	// Fade rates for the looping brake squeal (sustained while braking hard, fades on release).
	[Export] public float SkidFadeInRate = 9f;
	[Export] public float SkidFadeOutRate = 5f;
	[Export] public AudioStream? BrakeSkidStream = null;
	// Brake skid rides the same looping squeal VehicleContactAudio uses for lateral slip;
	// tire_pop.wav is reserved exclusively for the tire-destroyed cue (ArenaRealtimeView).
	[Export] public string BrakeSkidPlaceholderPath = "res://Assets/Audio/Vehicles/Tires/veh_tire_skid_asphalt_loop_a.ogg";

	private AudioStreamPlayer3D? _pIdle;
	private AudioStreamPlayer3D? _pLow;
	private AudioStreamPlayer3D? _pMid;
	private AudioStreamPlayer3D? _pHigh;
	private AudioStreamPlayer3D? _pVeryHigh;
	private AudioStreamPlayer3D? _pSkid;

	private IVehicleAudioTelemetry? _telemetry;
	private string _loadedArchetype = "";
	private float _rpm01;
	private float _throttleRpm01;
	private float _driveBlend01;
	private int _gear = 1;
	private float _shiftCooldown;
	private bool _started;

	private float _brakeHeld;
	private float _skidWeight01;

	// Volume floor for the skid loop right as it engages (mirrors VehicleContactAudio.SkidMinDb).
	private const float SkidMinFadeDb = -26f;

	private static readonly float[] Breakpoints = { 0.0f, 0.25f, 0.5f, 0.75f, 1.0f };

	public float CurrentRpm01 => _rpm01;
	public int CurrentGear => _gear;
	public int GetDisplayRpm() => Mathf.RoundToInt(Mathf.Lerp(DisplayIdleRpm, DisplayRedlineRpm, Mathf.Clamp(_rpm01, 0f, 1f)));

	public override void _Ready()
	{
		AudioBusUtil.EnsureBuses();
		EnsurePlayers();

		// Default telemetry: parent node (VehiclePawn attaches us under itself).
		_telemetry ??= GetParent() as IVehicleAudioTelemetry;

		// IMPORTANT: start deferred so the parent can set archetype/telemetry right after AddChild().
		// (Setting streams while an AudioStreamPlayer is already playing can stop playback silently.)
		if (AutoStart)
			CallDeferred(nameof(Start));
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_started) return;
		if (_telemetry == null) return;

		var dt = (float)delta;
		var speed = _telemetry.GetSpeedMps();
		var signedForwardSpeed = _telemetry.GetForwardSpeedSignedMps();
		// Use signed forward speed (abs) for RPM mapping so reverse behaves like 1st gear going forward.
		// Also blend in a bit of total speed so lateral slip/drift doesn't collapse RPM to ~0.
		var speedAbs = MathF.Max(MathF.Abs(signedForwardSpeed), speed * 0.85f);
		// Slightly lower threshold so reverse engages quickly and doesn't flicker near 0.
		var isReverse = signedForwardSpeed < -0.05f;
		var maxSpeed = MaxSpeedMpsOverride > 0.01f
			? MaxSpeedMpsOverride
			: MathF.Max(0.01f, _telemetry.GetMaxSpeedMps());

		var speed01 = Mathf.Clamp(speedAbs / maxSpeed, 0f, 1f);
		var throttleRaw01 = Mathf.Clamp(_telemetry.GetThrottle01(), 0f, 1f);
		var brakeRaw01 = Mathf.Clamp(_telemetry.GetBrake01(), 0f, 1f);
		// In our vehicle input model, reverse is commanded via a negative throttle input.
		// The telemetry abstraction exposes only 0..1 throttle/brake values, which can still
		// leave edge-cases where reverse intent doesn't contribute to "drive" mixing.
		//
		// We defensively sniff the parent VehiclePawn to determine reverse command intent.
		// This keeps reverse engine audio consistent (should sound like 1st gear forward).
		var rawThrottleSigned = 0f;
		var reverseCommanded = false;
		if (GetParent() is WastelandSurvivor.Game.Arena.VehiclePawn vp)
		{
			rawThrottleSigned = Mathf.Clamp(vp.ThrottleInput, -1f, 1f);
			reverseCommanded = rawThrottleSigned < -0.10f;
		}
		// If input is opposing motion (e.g. holding reverse while still rolling forward),
		// GetThrottle01() may be ~0. Count some brake intent toward "drive" so the engine doesn't go silent.
		var driveIntent01 = MathF.Max(throttleRaw01, brakeRaw01 * 0.65f);
		// Ensure reverse intent contributes even if telemetry ends up reporting 0.
		driveIntent01 = MathF.Max(driveIntent01, MathF.Abs(rawThrottleSigned));
		// Treat reverse command (or actual reverse movement) as a "drive" state so we don't fade back to idle.
		if (isReverse || reverseCommanded)
			driveIntent01 = MathF.Max(driveIntent01, MathF.Min(1f, speed01 * 1.10f));

		// A rolling drivetrain is never silent: keep the drive layers engaged at wheel-tracking RPM
		// while the vehicle is actually moving. Without this floor, every throttle pulse (AI drivers
		// pulse constantly) snapped the mix across the hard idle/drive gate — each pulse started and
		// stopped a tone, which is the other half of the "organ keys" read.
		if (speed01 > 0.04f)
			driveIntent01 = MathF.Max(driveIntent01, MathF.Min(0.70f, 0.25f + speed01 * 0.45f));

		// Also treat a commanded reverse (from a stop) as reverse for RPM mapping.
		isReverse = isReverse || reverseCommanded;

		// Throttle-to-RPM response: smooth increases so we don't jump to redline instantly on launch,
		// while letting decreases settle faster.
		var up = driveIntent01 > _throttleRpm01;
		var throttleRate = up ? ThrottleRpmSmoothingUp : ThrottleRpmSmoothingDown;
		_throttleRpm01 = SmoothTo(_throttleRpm01, driveIntent01, throttleRate, dt);
		var throttleForRpm01 = _throttleRpm01;

		var targetRpm01 = UseAutomaticTransmission
			? ComputeAutomaticTransmissionRpm(speed01, throttleForRpm01, dt, isReverse)
			: Mathf.Clamp(speed01 * SpeedWeight + throttleForRpm01 * ThrottleWeight, 0f, 1f);

		// Smooth to prevent jitter.
		_rpm01 = SmoothTo(_rpm01, targetRpm01, RpmSmoothing, dt);
		_driveBlend01 = SmoothTo(_driveBlend01, ComputeDriveBlend01(driveIntent01), IdleDriveBlendSmoothing, dt);

		ApplyEngineMix(_rpm01, driveIntent01, _driveBlend01);

		if (EnableSkid)
			UpdateSkid(dt, speed, maxSpeed);
	}

	private float ComputeDriveBlend01(float throttle01)
	{
		var dead = Mathf.Clamp(IdleThrottleThreshold01, 0f, 0.25f);
		if (throttle01 <= dead) return 0f;
		return Mathf.Clamp((throttle01 - dead) / MathF.Max(0.0001f, 1f - dead), 0f, 1f);
	}

	public void SetTelemetrySource(IVehicleAudioTelemetry telemetry)
	{
		_telemetry = telemetry;
	}

	public void SetArchetype(string archetypeId)
	{
		if (string.IsNullOrWhiteSpace(archetypeId)) return;
		ArchetypeId = archetypeId;

		// If we're already running, reload and restart so the new streams actually play.
		if (_started)
		{
			ReloadStreamsIfNeeded(force: true);
			RestartAllLayers();
		}
	}

	public void Start()
	{
		_started = true;
		_gear = 1;
		_shiftCooldown = 0f;
		_driveBlend01 = 0f;
		_throttleRpm01 = 0f;
		ReloadStreamsIfNeeded(force: false);
		RestartAllLayers();
		ApplyEngineMix(0f, 0f, 0f);
	}

	private void RestartAllLayers()
	{
		var startPos = ComputeStartOffsetSeconds();
		TryRestartLayer(_pIdle, startPos);
		TryRestartLayer(_pLow, startPos);
		TryRestartLayer(_pMid, startPos);
		TryRestartLayer(_pHigh, startPos);
		TryRestartLayer(_pVeryHigh, startPos);
	}

	private void EnsurePlayers()
	{
		_pIdle = GetNodeOrNull<AudioStreamPlayer3D>("LayerIdle") ?? CreateLayer("LayerIdle");
		_pLow = GetNodeOrNull<AudioStreamPlayer3D>("LayerLow") ?? CreateLayer("LayerLow");
		_pMid = GetNodeOrNull<AudioStreamPlayer3D>("LayerMid") ?? CreateLayer("LayerMid");
		_pHigh = GetNodeOrNull<AudioStreamPlayer3D>("LayerHigh") ?? CreateLayer("LayerHigh");
		_pVeryHigh = GetNodeOrNull<AudioStreamPlayer3D>("LayerVeryHigh") ?? CreateLayer("LayerVeryHigh");
		_pSkid = GetNodeOrNull<AudioStreamPlayer3D>("Skid") ?? CreateSkid("Skid");

		// Even if the players were created in the .tscn, we still enforce sane 3D settings.
		ConfigureEnginePlayer(_pIdle);
		ConfigureEnginePlayer(_pLow);
		ConfigureEnginePlayer(_pMid);
		ConfigureEnginePlayer(_pHigh);
		ConfigureEnginePlayer(_pVeryHigh);
		ConfigureSkidPlayer(_pSkid);
	}

	private void ConfigureEnginePlayer(AudioStreamPlayer3D? p)
	{
		if (p == null) return;
		p.Bus = "Engines";
		p.VolumeDb = MinLayerDb;
		p.MaxDistance = EngineMaxDistance;
		p.UnitSize = EngineUnitSize;
	}

	private void ConfigureSkidPlayer(AudioStreamPlayer3D? p)
	{
		if (p == null) return;
		p.Bus = "Tires";
		// Starts silent; UpdateSkid fades toward SkidVolumeDb while hard braking.
		p.VolumeDb = MinLayerDb;
		p.MaxDistance = SkidMaxDistance;
		p.UnitSize = SkidUnitSize;
	}

	private AudioStreamPlayer3D CreateLayer(string name)
	{
		var p = new AudioStreamPlayer3D
		{
			Name = name,
		};
		AddChild(p);
		return p;
	}

	// --- Gear-shift clunk: short pitched-up metallic tick on each upshift/downshift. Reuses a
	// staged impact sample rather than requiring a new asset; positional so nearby AI shifts read
	// quietly too. Skipped entirely if the sample is absent.
	private const string ShiftClunkPath = "res://Assets/Audio/Vehicles/Impacts/veh_hit_metal_a.wav";
	private AudioStreamPlayer3D? _pShift;
	private AudioStream? _shiftStream;
	private bool _shiftStreamLoaded;

	private void PlayShiftClunk()
	{
		if (!_shiftStreamLoaded)
		{
			_shiftStreamLoaded = true;
			try
			{
				if (ResourceLoader.Exists(ShiftClunkPath))
					_shiftStream = GD.Load<AudioStream>(ShiftClunkPath);
			}
			catch
			{
				_shiftStream = null;
			}
		}
		if (_shiftStream == null) return;

		if (_pShift == null || !GodotObject.IsInstanceValid(_pShift))
		{
			_pShift = new AudioStreamPlayer3D
			{
				Name = "ShiftClunk",
				Bus = _pIdle?.Bus ?? "Engines",
				VolumeDb = -17f,
				MaxDistance = 26f,
				Stream = _shiftStream,
			};
			AddChild(_pShift);
		}

		// Slight pitch variance keeps repeated shifts from sounding stamped.
		_pShift.PitchScale = 1.55f + GD.Randf() * 0.20f;
		_pShift.Play();
	}

	private AudioStreamPlayer3D CreateSkid(string name)
	{
		var p = new AudioStreamPlayer3D
		{
			Name = name,
		};
		AddChild(p);
		return p;
	}

	private void ReloadStreamsIfNeeded(bool force)
	{
		var norm = NormalizeArchetypeId(ArchetypeId);
		// Empty drive-loop override means "follow the vehicle's own archetype".
		var driveNorm = string.IsNullOrWhiteSpace(DriveLoopArchetypeId) ? norm : NormalizeArchetypeId(DriveLoopArchetypeId);
		var driveLayer = (DriveLoopLayerId ?? "low").Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(driveLayer)) driveLayer = "low";

		var key = UseSingleDriveLoop
			? $"{norm}|drive:{driveNorm}:{driveLayer}"
			: norm;
		if (!force && _loadedArchetype == key) return;
		_loadedArchetype = key;

		// Always load idle from the current archetype.
		TryAssignLayer(_pIdle, LoadStream(norm, "idle"));

		if (UseSingleDriveLoop)
		{
			// Drive loop is a single override stream (commonly diesel_truck low), pitched by RPM.
			TryAssignLayer(_pLow, LoadStream(driveNorm, driveLayer));
			TryAssignLayer(_pMid, null);
			TryAssignLayer(_pHigh, null);
			TryAssignLayer(_pVeryHigh, null);
		}
		else
		{
			TryAssignLayer(_pLow, LoadStream(norm, "low"));
			TryAssignLayer(_pMid, LoadStream(norm, "mid"));
			TryAssignLayer(_pHigh, LoadStream(norm, "high"));
			TryAssignLayer(_pVeryHigh, LoadStream(norm, "very_high"));
		}

		// Skid stream: explicit override first, then placeholder. Looped — UpdateSkid fades it
		// in/out instead of re-triggering a one-shot.
		if (_pSkid != null)
		{
			var skid = BrakeSkidStream ?? LoadOptional(BrakeSkidPlaceholderPath);
			if (skid != null)
				TryEnableLoop(skid);
			_pSkid.Stream = skid;
		}
	}

	private static string NormalizeArchetypeId(string archetypeId)
	{
		var id = (archetypeId ?? "").Trim().ToLowerInvariant();
		if (id.StartsWith("engine_")) id = id.Substring("engine_".Length);
		return id;
	}

	private AudioStream? LoadStream(string archetypeId, string layer)
	{
		// Primary convention.
		var path = $"res://Assets/Audio/Vehicles/Engines/veh_engine_{archetypeId}_loop_{layer}_a.ogg";
		var s = GD.Load<AudioStream>(path);

		// A couple of tolerances to avoid silent failures if naming changes slightly.
		if (s == null)
		{
			// (Some earlier drafts dropped the trailing _a.)
			var alt = $"res://Assets/Audio/Vehicles/Engines/veh_engine_{archetypeId}_loop_{layer}.ogg";
			s = GD.Load<AudioStream>(alt);
			if (s == null)
				GD.PushWarning($"[Audio] Missing engine loop: {path}");
		}

		if (s == null) return null;

		TryEnableLoop(s);
		return s;
	}

	private AudioStream? LoadOptional(string path)
	{
		if (string.IsNullOrWhiteSpace(path)) return null;
		var s = GD.Load<AudioStream>(path);
		if (s == null)
		{
			GD.PushWarning($"[Audio] Missing optional stream: {path}");
			return null;
		}
		return s;
	}

	private static void TryEnableLoop(AudioStream stream)
	{
		try
		{
			if (stream is AudioStreamOggVorbis ogg)
				ogg.Loop = true;
			if (stream is AudioStreamWav wav)
				wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
		}
		catch
		{
			// If a stream type doesn't support toggling loop, ignore.
		}
	}

	private static void TryAssignLayer(AudioStreamPlayer3D? player, AudioStream? stream)
	{
		if (player == null) return;
		player.Stream = stream;
	}

	private float ComputeStartOffsetSeconds()
	{
		var max = MathF.Max(0f, RandomizeStartMaxSeconds);
		if (max <= 0.0001f) return 0f;
		return (float)GD.RandRange(0.0, max);
	}

	private static void TryRestartLayer(AudioStreamPlayer3D? player, float startPos)
	{
		if (player == null) return;
		if (player.Stream == null) return;
		// Force restart to keep all layers in sync.
		player.Stop();
		player.Play(startPos);
	}

	private float ComputeAutomaticTransmissionRpm(float speed01, float throttle01, float dt, bool isReverse)
	{
		// Keep inputs sane.
		speed01 = Mathf.Clamp(speed01, 0f, 1f);
		throttle01 = Mathf.Clamp(throttle01, 0f, 1f);

		_shiftCooldown = MathF.Max(0f, _shiftCooldown - dt);

		// Reverse: keep audio in 1st gear so backing up sounds like launching in 1st.
		// (We still use absolute speed for RPM within the gear.)
		if (isReverse)
		{
			var gearCountR = Math.Clamp(GearCount, 2, 6);
			_gear = 1;
			var topR = GetGearTopSpeed01(1, gearCountR);
			var wheelRpmR = Mathf.Clamp(speed01 / MathF.Max(0.01f, topR), 0f, 1f);
			var pedalRpmR = Mathf.Clamp(IdleRpm01 + throttle01 * (1f - IdleRpm01), 0f, 1f);
			var slipR = Mathf.Clamp(PedalRevStrength * (1f - wheelRpmR), 0f, 1f);
			var rpmTargetR = Mathf.Lerp(wheelRpmR, pedalRpmR, slipR);
			rpmTargetR = MathF.Max(wheelRpmR, rpmTargetR);
			return Mathf.Clamp(rpmTargetR, 0f, 1f);
		}

		// If nearly stopped, stay in 1st and cap RPM at a "stall" value so we don't hit redline instantly.
		if (speed01 < 0.02f)
		{
			_gear = 1;
			var stall = Mathf.Clamp(StallRpm01, IdleRpm01, 1f);
			return Mathf.Clamp(IdleRpm01 + throttle01 * (stall - IdleRpm01), 0f, 1f);
		}

		var gearCount = Math.Clamp(GearCount, 2, 6);
		_gear = Math.Clamp(_gear, 1, gearCount);

		var top = GetGearTopSpeed01(_gear, gearCount);
		var wheelRpm = speed01 / MathF.Max(0.01f, top); // 0..1 within gear
		wheelRpm = Mathf.Clamp(wheelRpm, 0f, 1f);

		var pedalRpm = IdleRpm01 + throttle01 * (1f - IdleRpm01);
		pedalRpm = Mathf.Clamp(pedalRpm, 0f, 1f);

		// Converter slip effect: allow throttle to pull RPM above wheel RPM more at low wheel RPM,
		// and converge back toward wheel RPM as speed builds (prevents "stuck at high RPM" feel).
		var slip = Mathf.Clamp(PedalRevStrength * (1f - wheelRpm), 0f, 1f);
		var rpmTarget = Mathf.Lerp(wheelRpm, pedalRpm, slip);
		rpmTarget = MathF.Max(wheelRpm, rpmTarget);


		// Shift by normalized speed bands (not RPM threshold) so gears map evenly across speed.
		if (_shiftCooldown <= 0.0001f)
		{
			// Upshift when we exceed this gear's normalized top speed.
			if (_gear < gearCount && speed01 >= top)
			{
				_gear++;
				_shiftCooldown = MathF.Max(0.05f, ShiftCooldownSeconds);
				PlayShiftClunk();

				// Force an audible RPM drop on shift (helps the rev-up → shift → rev-up feel).
				_rpm01 = MathF.Min(_rpm01, ShiftRpmDrop01);
				rpmTarget = MathF.Min(rpmTarget, ShiftRpmDrop01);
				return Mathf.Clamp(rpmTarget, 0f, 1f);
			}

			// Downshift with hysteresis to prevent gear hunting.
			if (_gear > 1)
			{
				var prevTop = GetGearTopSpeed01(_gear - 1, gearCount);
				var hyst = Mathf.Clamp(GearDownshiftHysteresis01, 0f, 0.20f);
				if (speed01 < MathF.Max(0.02f, prevTop - hyst))
				{
					_gear--;
					_shiftCooldown = MathF.Max(0.05f, ShiftCooldownSeconds * 0.75f);
					PlayShiftClunk();

					// RPM bump on downshift so it doesn't feel like it falls on its face.
					_rpm01 = MathF.Max(_rpm01, 0.72f);
					rpmTarget = MathF.Max(rpmTarget, 0.72f);
					return Mathf.Clamp(rpmTarget, 0f, 1f);
				}
			}
		}

		return Mathf.Clamp(rpmTarget, 0f, 1f);
	}

	private float GetGearTopSpeed01(int gear, int gearCount)
	{
		// 4-speed is the primary tuning target (game feel).
		if (gearCount == 4)
		{
			// Sanitize thresholds to ensure monotonic order even if tweaked in the inspector.
			var g12 = Mathf.Clamp(Gear1To2Speed01, 0.05f, 0.45f);
			var g23 = Mathf.Clamp(Gear2To3Speed01, g12 + 0.05f, 0.85f);
			var g34 = Mathf.Clamp(Gear3To4Speed01, g23 + 0.05f, 0.98f);

			return gear switch
			{
				1 => g12,
				2 => g23,
				3 => g34,
				_ => 1.00f
			};
		}

		if (gearCount == 3)
		{
			return gear switch
			{
				1 => 0.28f,
				2 => 0.62f,
				_ => 1.00f
			};
		}

		// Generic fallback: non-linear spacing.
		var t = gear / (float)gearCount;
		var curved = MathF.Pow(t, 0.78f);
		return MathF.Min(1.0f, MathF.Max(0.08f, curved));
	}

	private void ApplyEngineMix(float rpm01, float throttle01, float driveBlend01)
	{
		// Prefer idle-only when throttle is zero, and drive when throttling.
		var dead = Mathf.Clamp(IdleThrottleThreshold01, 0f, 0.25f);
		var wantsIdleOnly = throttle01 <= dead && driveBlend01 <= 0.001f;

		if (UseSingleDriveLoop)
		{
			// Simplified mode: idle loop + one drive loop with pitch progression.
			if (wantsIdleOnly)
			{
				ApplyLayerWeight(_pIdle, 1f, TrimIdleDb, 1.0f);
				ApplyLayerWeight(_pLow, 0f, TrimLowDb, 1.0f);
				ApplyLayerWeight(_pMid, 0f, TrimMidDb, 1.0f);
				ApplyLayerWeight(_pHigh, 0f, TrimHighDb, 1.0f);
				ApplyLayerWeight(_pVeryHigh, 0f, TrimVeryHighDb, 1.0f);
				return;
			}

			var idleW = 1f - Mathf.Clamp(driveBlend01, 0f, 1f);
			var driveW = 1f - idleW;

			var t = Mathf.Clamp(rpm01, 0f, 1f);
			if (DrivePitchExponent > 0.0001f)
				t = MathF.Pow(t, DrivePitchExponent);

			var drivePitch = (DriveUsesPitchProgression)
				? Mathf.Lerp(DrivePitchMin, DrivePitchMax, t)
				: 1.0f;

			ApplyLayerWeight(_pIdle, idleW, TrimIdleDb, 1.0f);
			ApplyLayerWeight(_pLow, driveW, TrimLowDb, drivePitch);
			ApplyLayerWeight(_pMid, 0f, TrimMidDb, 1.0f);
			ApplyLayerWeight(_pHigh, 0f, TrimHighDb, 1.0f);
			ApplyLayerWeight(_pVeryHigh, 0f, TrimVeryHighDb, 1.0f);
			return;
		}

		// 5-layer crossfade mode.
		var pitch = EnablePitchScaling
			? Mathf.Lerp(PitchMin, PitchMax, rpm01)
			: 1.0f;

		if (wantsIdleOnly)
		{
			ApplyLayerWeight(_pIdle, 1f, TrimIdleDb, 1.0f);
			ApplyLayerWeight(_pLow, 0f, TrimLowDb, pitch);
			ApplyLayerWeight(_pMid, 0f, TrimMidDb, pitch);
			ApplyLayerWeight(_pHigh, 0f, TrimHighDb, pitch);
			ApplyLayerWeight(_pVeryHigh, 0f, TrimVeryHighDb, pitch);
			return;
		}

		// Smooth blend between idle-loop and drive-loops.
		var idleW2 = 1f - Mathf.Clamp(driveBlend01, 0f, 1f);
		var driveW2 = 1f - idleW2;

		ApplyLayerWeight(_pIdle, idleW2, TrimIdleDb, LayerTrackedPitch(rpm01, Breakpoints[0], pitch));

		var w = MathF.Max(0.0001f, CrossfadeWidth);
		var low = TriWeight(rpm01, Breakpoints[1], w) * driveW2;
		var mid = TriWeight(rpm01, Breakpoints[2], w) * driveW2;
		var high = TriWeight(rpm01, Breakpoints[3], w) * driveW2;
		var vh = TriWeight(rpm01, Breakpoints[4], w) * driveW2;

		ApplyLayerWeight(_pLow, low, TrimLowDb, LayerTrackedPitch(rpm01, Breakpoints[1], pitch));
		ApplyLayerWeight(_pMid, mid, TrimMidDb, LayerTrackedPitch(rpm01, Breakpoints[2], pitch));
		ApplyLayerWeight(_pHigh, high, TrimHighDb, LayerTrackedPitch(rpm01, Breakpoints[3], pitch));
		ApplyLayerWeight(_pVeryHigh, vh, TrimVeryHighDb, LayerTrackedPitch(rpm01, Breakpoints[4], pitch));
	}

	/// <summary>
	/// Pitch multiplier that bends a layer recorded at <paramref name="anchorRpm01"/> toward the
	/// current engine RPM (see <see cref="UseRpmPitchTracking"/>). Falls back to the legacy global
	/// pitch when tracking is disabled.
	/// </summary>
	private float LayerTrackedPitch(float rpm01, float anchorRpm01, float legacyPitch)
	{
		if (!UseRpmPitchTracking)
			return legacyPitch;
		var ratio = MathF.Max(1.01f, RevFrequencyRatio);
		var bend = MathF.Pow(ratio, Mathf.Clamp(rpm01, 0f, 1f) - anchorRpm01);
		return Mathf.Clamp(bend, MathF.Min(LayerPitchMin, LayerPitchMax), MathF.Max(LayerPitchMin, LayerPitchMax));
	}

	private void ApplyLayerWeight(AudioStreamPlayer3D? player, float weight01, float trimDb, float pitch)
	{
		if (player == null) return;
		if (player.Stream == null)
		{
			player.VolumeDb = MinLayerDb;
			return;
		}

		var w = Mathf.Clamp(weight01, 0f, 1f);
		// Equal-power gain: lerping raw dB put partial weights near -40 dB, which carved a deep
		// loudness dip between crossfaded RPM layers and made the 5-layer mode sound broken.
		var gain = MathF.Sin(w * MathF.PI * 0.5f);
		var db = gain <= 0.001f
			? MinLayerDb
			: MathF.Max(MinLayerDb, Mathf.LinearToDb(gain)) + EngineVolumeDb + trimDb;
		player.VolumeDb = db;
		player.PitchScale = pitch;

		// Defensive: if something stopped our players (stream reassignment, audio server hiccup), restart.
		if (!player.Playing)
			player.Play();
	}

	private static float TriWeight(float x, float center, float halfWidth)
	{
		var d = MathF.Abs(x - center);
		var w = 1f - (d / halfWidth);
		return Mathf.Clamp(w, 0f, 1f);
	}

	private void UpdateSkid(float dt, float speedMps, float maxSpeedMps)
	{
		if (_pSkid == null) return;
		var brake01 = Mathf.Clamp(_telemetry?.GetBrake01() ?? 0f, 0f, 1f);

		var hard = brake01 >= BrakeHardThreshold01 && speedMps >= BrakeMinSpeedMps;
		_brakeHeld = hard ? _brakeHeld + dt : 0f;

		// Looping squeal faded with brake state (was: a repeated one-shot, which machine-gunned
		// the sample ~5x/sec and previously pointed at the tire-blowout cue).
		var engaged = hard && _brakeHeld >= BrakeHoldSeconds && _pSkid.Stream != null;
		var target = engaged ? 1f : 0f;
		var rate = target > _skidWeight01 ? SkidFadeInRate : SkidFadeOutRate;
		_skidWeight01 = Mathf.Lerp(_skidWeight01, target, 1f - Mathf.Exp(-MathF.Max(0.01f, rate) * dt));

		if (_skidWeight01 <= 0.02f)
		{
			if (_pSkid.Playing)
				_pSkid.Stop();
			return;
		}

		// Use speed to add a little variation.
		var t = Mathf.Clamp(speedMps / MathF.Max(0.01f, maxSpeedMps), 0f, 1f);
		_pSkid.PitchScale = Mathf.Lerp(0.92f, 1.10f, t);
		_pSkid.VolumeDb = Mathf.Lerp(SkidMinFadeDb, SkidVolumeDb, _skidWeight01);
		if (!_pSkid.Playing)
			_pSkid.Play();
	}

	private static float SmoothTo(float current, float target, float rate, float dt)
	{
		if (rate <= 0.0001f) return target;
		var t = 1f - Mathf.Exp(-rate * dt);
		return Mathf.Lerp(current, target, t);
	}
}
