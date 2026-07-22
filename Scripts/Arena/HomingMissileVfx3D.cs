// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/HomingMissileVfx3D.cs
// Purpose: Lightweight homing missile visual for guided weapon lock-on shots.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Simple code-only homing missile visual. Tracks a target node (or its last known position),
/// emits a lightweight trail, then invokes a callback when it detonates.
/// </summary>
public partial class HomingMissileVfx3D : Node3D
{
	// In-flight thruster bed. No dedicated missile-thruster loop is staged yet, so we reuse the
	// near-redline i4 engine loop pitched up — at -18 dB it reads as a faint rocket-motor whine.
	// Launch/detonate one-shots stay view-side (ArenaRealtimeView plays fire + explosion SFX).
	private const string ThrusterLoopPath = "res://Assets/Audio/Vehicles/Engines/veh_engine_i4_compact_loop_very_high_a.ogg";
	private const float ThrusterVolumeDb = -18f;
	private const float ThrusterPitchScale = 1.35f;

	private Node3D? _target;
	private Vector3 _lastKnownTarget;
	private Vector3 _direction = Vector3.Forward;
	private float _speed = 40f;
	private float _turnRateRad = 4.5f;
	private float _detonationRadius = 1.25f;
	// Locked-target hull-contact fuse (gameplay judge round 10 #3): detonating at _detonationRadius
	// from the target's CENTER parked every blast ~2.4 m out — outside the hull — so splash falloff
	// always bottomed out at half damage. When the shooter knows the target's hull bounding radius,
	// the fuse fires where the flight path crosses that radius instead: on the hull surface.
	private float _targetHullRadius;
	private float _lifetimeRemaining = 3.0f;
	private Action<Vector3>? _onDetonate;
	private bool _detonated;
	private Color _bodyColor = new(1f, 0.92f, 0.35f);
	private float _trailEmitCooldown;
	private float _pulseTimer;
	private MeshInstance3D? _exhaustCore;
	private StandardMaterial3D? _exhaustCoreMat;
	private AudioStreamPlayer3D? _thrusterLoop;

	public static HomingMissileVfx3D? Spawn(
		Node3D parent,
		Vector3 fromWorld,
		Node3D? target,
		Vector3 initialDirection,
		float speed,
		Color color,
		float turnRateDeg,
		float detonationRadius,
		float maxLifetimeSeconds,
		Action<Vector3>? onDetonate = null,
		float targetHullRadius = 0f)
	{
		if (parent == null || !GodotObject.IsInstanceValid(parent))
			return null;

		var node = new HomingMissileVfx3D
		{
			Name = "HomingMissileVfx",
			_target = target,
			_lastKnownTarget = target != null && GodotObject.IsInstanceValid(target)
				? target.GlobalPosition
				: fromWorld + initialDirection.Normalized() * 24f,
			_direction = initialDirection.Length() > 0.001f ? initialDirection.Normalized() : Vector3.Forward,
			_speed = MathF.Max(10f, speed),
			_turnRateRad = Mathf.DegToRad(Mathf.Clamp(turnRateDeg, 20f, 720f)),
			_detonationRadius = Mathf.Clamp(detonationRadius, 0.4f, 6.0f),
			_targetHullRadius = Mathf.Clamp(targetHullRadius, 0f, 8f),
			_lifetimeRemaining = Mathf.Clamp(maxLifetimeSeconds, 0.5f, 8.0f),
			_onDetonate = onDetonate,
			_bodyColor = color,
			_trailEmitCooldown = 0.01f
		};

		parent.AddChild(node);
		node.GlobalPosition = fromWorld;
		node.BuildVisual(color);
		node.EnsureThrusterAudio();
		node.UpdateOrientation();
		return node;
	}

	public override void _Process(double delta)
	{
		if (_detonated)
			return;

		var dt = (float)delta;
		_lifetimeRemaining -= dt;
		_pulseTimer += dt;

		var hasLiveTarget = _target != null && GodotObject.IsInstanceValid(_target);
		var targetPos = _lastKnownTarget;
		if (hasLiveTarget)
		{
			targetPos = _target!.GlobalPosition + Vector3.Up * 0.15f;
			_lastKnownTarget = targetPos;
		}

		var desired = targetPos - GlobalPosition;
		if (desired.Length() > 0.001f)
		{
			desired = desired.Normalized();
			var blend = Mathf.Clamp(_turnRateRad * dt, 0f, 1f);
			_direction = ((_direction * (1f - blend)) + (desired * blend)).Normalized();
		}

		// Fuse: with a live locked target and a known hull radius, trigger where the flight path
		// crosses the hull bounding sphere — the blast lands ON the surface, so splash falloff
		// reads ~1.0 exactly as the weapon sheet promises. Lost-lock / stale-point flights keep
		// the legacy proximity radius (near-miss splash territory). Segment math also stops fast
		// missiles from stepping THROUGH the fuse sphere between frames.
		var fuseRadius = hasLiveTarget && _targetHullRadius > 0.05f
			? MathF.Max(0.4f, _targetHullRadius)
			: _detonationRadius;
		var move = _direction * _speed * dt;
		if (TryGetFuseCrossing(GlobalPosition, move, targetPos, fuseRadius, out var detonatePos))
		{
			GlobalPosition = detonatePos;
			UpdateOrientation();
			Detonate();
			return;
		}

		GlobalPosition += move;
		UpdateOrientation();
		UpdateExhaustVisual();
		EmitTrail(dt);

		if (_lifetimeRemaining <= 0f)
			Detonate();
	}

	/// <summary>
	/// Earliest point along the movement segment that touches the fuse sphere around
	/// <paramref name="targetPos"/> (or the current position when already inside it).
	/// </summary>
	private static bool TryGetFuseCrossing(Vector3 from, Vector3 move, Vector3 targetPos, float radius, out Vector3 crossing)
	{
		crossing = from;
		var rel = from - targetPos;
		var c = rel.LengthSquared() - radius * radius;
		if (c <= 0f)
			return true; // already inside the fuse sphere

		var a = move.LengthSquared();
		if (a < 0.000001f)
			return false;

		var b = 2f * rel.Dot(move);
		var disc = b * b - 4f * a * c;
		if (disc < 0f)
			return false;

		var t = (-b - MathF.Sqrt(disc)) / (2f * a);
		if (t < 0f || t > 1f)
			return false;

		crossing = from + move * t;
		return true;
	}

	private void Detonate()
	{
		if (_detonated)
			return;
		_detonated = true;

		// Cut the thruster bed immediately so it doesn't overlap the explosion one-shot.
		if (_thrusterLoop != null && GodotObject.IsInstanceValid(_thrusterLoop))
			_thrusterLoop.Stop();

		try
		{
			_onDetonate?.Invoke(GlobalPosition);
		}
		catch
		{
			// Never let VFX callback failures break gameplay.
		}

		if (GodotObject.IsInstanceValid(this))
			QueueFree();
	}

	private void BuildVisual(Color color)
	{
		var body = new Node3D { Name = "Body" };
		AddChild(body);

		var mesh = new MeshInstance3D
		{
			Name = "Mesh",
			Mesh = new CapsuleMesh
			{
				Radius = 0.08f,
				Height = 0.34f,
				RadialSegments = 12,
				Rings = 2
			},
			Position = new Vector3(0f, 0f, -0.18f)
		};
		var mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = color,
			NoDepthTest = true,
			EmissionEnabled = true,
			Emission = color,
			EmissionEnergyMultiplier = 0.55f
		};
		mesh.SetSurfaceOverrideMaterial(0, mat);
		body.AddChild(mesh);

		var tail = new MeshInstance3D
		{
			Name = "Tail",
			Mesh = new BoxMesh { Size = new Vector3(0.06f, 0.06f, 0.22f) },
			Position = new Vector3(0f, 0f, 0.04f)
		};
		var tailMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = new Color(color.R, color.G * 0.85f, color.B * 0.75f, 0.55f),
			EmissionEnabled = true,
			Emission = new Color(color.R, color.G * 0.75f, color.B * 0.50f),
			EmissionEnergyMultiplier = 0.35f,
			NoDepthTest = true
		};
		tail.SetSurfaceOverrideMaterial(0, tailMat);
		body.AddChild(tail);

		_exhaustCore = new MeshInstance3D
		{
			Name = "ExhaustCore",
			Mesh = new SphereMesh
			{
				Radius = 0.07f,
				Height = 0.14f,
				RadialSegments = 10,
				Rings = 4
			},
			Position = new Vector3(0f, 0f, 0.13f)
		};
		_exhaustCoreMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = new Color(1.0f, 0.78f, 0.22f, 0.82f),
			EmissionEnabled = true,
			Emission = new Color(1.0f, 0.56f, 0.18f),
			EmissionEnergyMultiplier = 0.85f,
			NoDepthTest = true
		};
		_exhaustCore.SetSurfaceOverrideMaterial(0, _exhaustCoreMat);
		body.AddChild(_exhaustCore);
	}

	/// <summary>Quiet positional thruster loop for the flight phase. Silent-safe when the asset is absent.</summary>
	private void EnsureThrusterAudio()
	{
		if (_thrusterLoop != null && GodotObject.IsInstanceValid(_thrusterLoop))
			return;
		if (!ResourceLoader.Exists(ThrusterLoopPath))
			return;

		var stream = GD.Load<AudioStream>(ThrusterLoopPath);
		if (stream == null)
			return;
		if (stream is AudioStreamOggVorbis ogg)
			ogg.Loop = true;

		_thrusterLoop = new AudioStreamPlayer3D
		{
			Name = "ThrusterLoop",
			Stream = stream,
			Bus = "SFX",
			VolumeDb = ThrusterVolumeDb,
			PitchScale = ThrusterPitchScale,
			// Match the arena's other positional one-shots (ArenaRealtimeView.PlaySfx3D).
			UnitSize = 8.0f,
			MaxDistance = 60.0f,
			Autoplay = false,
		};
		AddChild(_thrusterLoop);
		_thrusterLoop.Play();
	}

	private void UpdateExhaustVisual()
	{
		if (_exhaustCore == null || _exhaustCoreMat == null)
			return;

		var pulse = 0.82f + 0.18f * (0.5f + 0.5f * MathF.Sin(_pulseTimer * 26f));
		_exhaustCore.Scale = new Vector3(1f, 1f, pulse);
		_exhaustCoreMat.AlbedoColor = new Color(1.0f, 0.80f, 0.24f, 0.72f + 0.10f * pulse);
		_exhaustCoreMat.EmissionEnergyMultiplier = 0.7f + 0.35f * pulse;
	}

	private bool _trailSmokeAlternator;

	private void EmitTrail(float dt)
	{
		_trailEmitCooldown -= dt;
		if (_trailEmitCooldown > 0f)
			return;
		_trailEmitCooldown = 0.028f;

		if (GetParent() is not Node3D parent || !GodotObject.IsInstanceValid(parent))
			return;

		var tailWorld = GlobalPosition - _direction * 0.22f;
		ArenaVfx.SpawnProjectileTrail(parent, tailWorld, _direction, _bodyColor, ttlSeconds: 0.34f);

		// Hanging smoke line: short-lived hot particles alone were invisible from the RTS camera.
		// Alternating gray puffs persist ~1.2s so the missile draws a readable arc across the arena.
		_trailSmokeAlternator = !_trailSmokeAlternator;
		if (_trailSmokeAlternator)
			SmokePuff3D.Spawn(parent, tailWorld, new Color(0.78f, 0.78f, 0.80f),
				baseAlpha: 0.42f, size: 0.30f, ttlSeconds: 1.2f, riseSpeed: 0.25f);
	}

	private void UpdateOrientation()
	{
		var look = GlobalPosition + _direction;
		if (look.DistanceTo(GlobalPosition) < 0.001f)
			return;
		LookAt(look, Vector3.Up);
	}
}
