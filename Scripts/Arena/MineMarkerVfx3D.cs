// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/MineMarkerVfx3D.cs
// Purpose: Live-mine arena marker: dark body puck, pulsing emissive core dome, and a slow-breathing
//          ground hazard ring so mines read as a lethal AREA threat from the fixed RTS camera.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Mine marker visual (round 10 P2-7 — the old marker was a static 0.18m cylinder, a 3-4px dot at
/// the sacred RTS altitude). Owned by ArenaRealtimeView's mine runtime list; the view only toggles
/// <see cref="SetArmed"/> — all animation is local to this node.
/// Unarmed: dim static amber (deploying, not yet dangerous). Armed: hot red-orange with a blinking
/// core and a breathing ~1.5m ground ring in additive hazard color.
/// </summary>
public partial class MineMarkerVfx3D : Node3D
{
	private const float RingRadius = 0.75f; // ~1.5m diameter hazard footprint

	private static readonly Color ArmedRing = new(1.00f, 0.36f, 0.10f);
	private static readonly Color ArmedCore = new(1.00f, 0.18f, 0.08f);
	private static readonly Color UnarmedRing = new(0.85f, 0.62f, 0.20f);
	private static readonly Color UnarmedCore = new(0.60f, 0.45f, 0.16f);

	private MeshInstance3D? _ring;
	private StandardMaterial3D? _ringMat;
	private MeshInstance3D? _core;
	private StandardMaterial3D? _coreMat;
	private bool _armed;
	private float _clock;

	public override void _Ready()
	{
		// Physical mine: squat dark puck so there is a solid object under the glow.
		var body = new MeshInstance3D
		{
			Name = "MineBody",
			Mesh = new CylinderMesh { TopRadius = 0.19f, BottomRadius = 0.24f, Height = 0.12f, RadialSegments = 12 },
			Position = new Vector3(0f, 0.06f, 0f),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		body.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
		{
			AlbedoColor = new Color(0.15f, 0.14f, 0.13f),
			Roughness = 0.9f,
			Metallic = 0.2f,
		});
		AddChild(body);

		// Emissive core dome — the blink source. Transparent pass + high render priority so a
		// later-sorted oil slick can't paint over it (depth test still lets vehicles hide it).
		_coreMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = UnarmedCore,
			EmissionEnabled = true,
			Emission = UnarmedCore,
			EmissionEnergyMultiplier = 0.5f,
			RenderPriority = 7,
		};
		_core = new MeshInstance3D
		{
			Name = "MineCore",
			Mesh = new SphereMesh { Radius = 0.15f, Height = 0.30f, RadialSegments = 10, Rings = 6 },
			Scale = new Vector3(1f, 0.55f, 1f),
			Position = new Vector3(0f, 0.13f, 0f),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		_core.SetSurfaceOverrideMaterial(0, _coreMat);
		AddChild(_core);

		// Ground hazard ring: flattened torus = true annulus from top-down; additive so it reads
		// against the dark arena floor without painting an opaque disc. RenderPriority keeps it
		// above other ground transparents (oil slicks) while depth test lets vehicles occlude it.
		_ringMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			AlbedoColor = new Color(UnarmedRing.R, UnarmedRing.G, UnarmedRing.B, 0.30f),
			EmissionEnabled = true,
			Emission = UnarmedRing,
			EmissionEnergyMultiplier = 0.5f,
			RenderPriority = 6,
		};
		_ring = new MeshInstance3D
		{
			Name = "MineRing",
			// NOTE: TorusMesh.Rings subdivides AROUND the ring (too few = polygonal "diamond");
			// RingSegments subdivides the tube cross-section.
			Mesh = new TorusMesh { InnerRadius = 0.70f, OuterRadius = 1.0f, Rings = 48, RingSegments = 6 },
			Scale = new Vector3(RingRadius, 1f, RingRadius),
			Position = new Vector3(0f, 0.05f, 0f),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		_ring.SetSurfaceOverrideMaterial(0, _ringMat);
		AddChild(_ring);

		ApplyArmedLook();
	}

	public void SetArmed(bool armed)
	{
		if (_armed == armed) return;
		_armed = armed;
		ApplyArmedLook();
	}

	private void ApplyArmedLook()
	{
		if (_ringMat == null || _coreMat == null) return;
		var ring = _armed ? ArmedRing : UnarmedRing;
		var core = _armed ? ArmedCore : UnarmedCore;
		_ringMat.AlbedoColor = new Color(ring.R, ring.G, ring.B, _armed ? 0.55f : 0.30f);
		_ringMat.Emission = ring;
		_ringMat.EmissionEnergyMultiplier = _armed ? 2.0f : 0.5f;
		_coreMat.AlbedoColor = core;
		_coreMat.Emission = core;
		_coreMat.EmissionEnergyMultiplier = _armed ? 2.2f : 0.5f;
		if (_ring != null && GodotObject.IsInstanceValid(_ring))
			_ring.Scale = new Vector3(RingRadius, 1f, RingRadius);
	}

	public override void _Process(double delta)
	{
		if (!_armed) return;
		if (_ring == null || _ringMat == null || _coreMat == null) return;

		_clock += (float)delta;

		// Slow-breathing hazard ring (~0.8 Hz) — the area-threat read.
		var breath = 0.5f + 0.5f * MathF.Sin(_clock * 5.0f);
		var s = RingRadius * (0.90f + 0.16f * breath);
		_ring.Scale = new Vector3(s, 1f, s);
		_ringMat.AlbedoColor = new Color(ArmedRing.R, ArmedRing.G, ArmedRing.B, 0.50f + 0.30f * breath);
		_ringMat.EmissionEnergyMultiplier = 1.8f + 2.6f * breath;

		// Sharper, faster core blink (~1.4 Hz) — the "this thing is live" beacon.
		var blink = MathF.Pow(0.5f + 0.5f * MathF.Sin(_clock * 9.0f), 3f);
		_coreMat.EmissionEnergyMultiplier = 0.9f + 3.6f * blink;
	}
}
