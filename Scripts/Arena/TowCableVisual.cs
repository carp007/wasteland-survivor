// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/TowCableVisual.cs
// Purpose: Tow cable render node with slack sag and tension color feedback (gray -> amber -> red).
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Persistent tow-cable visual. The owner (ArenaRealtimeView) calls <see cref="UpdateCable"/> each
/// frame with the two hitch points; this node renders a catenary-ish heavy cable that sags when
/// slack and pulls straight + shifts gray -> amber -> red as it approaches snap length, so cable
/// tension reads from the RTS camera without looking at the HUD.
/// Round 10 P2-8: the old render was three 1px line strips — a white thread at the sacred RTS
/// altitude. The cable is now a volumetric cross-ribbon (~0.23m wide dark sheath with a bright
/// tension-colored core strip) plus a visible hook/coupler node at the wreck end.
/// One ImmediateMesh + the materials are allocated once and reused across rebuilds (no per-frame
/// RID churn).
/// </summary>
public partial class TowCableVisual : MeshInstance3D
{
	private const int CurveSegments = 14;
	private const float CableHalfWidth = 0.115f; // dark sheath ribbon
	private const float CoreHalfWidth = 0.045f;  // bright tension strip
	private const float MaxSagMeters = 0.85f;

	private static readonly Color SheathDark = new(0.13f, 0.12f, 0.11f);
	private static readonly Color SlackGray = new(0.72f, 0.72f, 0.72f);
	private static readonly Color WarnAmber = new(1.0f, 0.68f, 0.16f);
	private static readonly Color SnapRed = new(1.0f, 0.22f, 0.12f);

	private readonly ImmediateMesh _im = new();

	// Dark sheath: opaque-ish alpha so it renders in the transparent pass (always on top, matching
	// the old NoDepthTest thread behavior) and silhouettes against floor and vehicles alike.
	private readonly StandardMaterial3D _matSheath = new()
	{
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		AlbedoColor = new Color(SheathDark.R, SheathDark.G, SheathDark.B, 0.96f),
		CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		NoDepthTest = true,
	};

	// Bright core strip: the tension color read (gray -> amber -> red + near-snap pulse).
	private readonly StandardMaterial3D _matCore = new()
	{
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		AlbedoColor = new Color(0.72f, 0.72f, 0.72f, 0.95f),
		EmissionEnabled = true,
		Emission = new Color(0.72f, 0.72f, 0.72f),
		EmissionEnergyMultiplier = 0.9f,
		CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		NoDepthTest = true,
	};

	private MeshInstance3D? _hook;
	private StandardMaterial3D? _hookMarkerMat;

	/// <summary>0 = fully slack, 1 = at snap length. Updated by <see cref="UpdateCable"/>.</summary>
	public float CurrentTension01 { get; private set; }

	public override void _Ready()
	{
		Mesh = _im;
		CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		Visible = false; // first UpdateCable shows it — avoids a one-frame hook at the origin.

		// Hook/coupler at the wreck end: dark steel knuckle with a small emissive amber marker so
		// the attachment point reads at 1x.
		_hook = new MeshInstance3D
		{
			Name = "TowHook",
			Mesh = new SphereMesh { Radius = 0.17f, Height = 0.34f, RadialSegments = 10, Rings = 6 },
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		_hook.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0.20f, 0.20f, 0.22f),
			NoDepthTest = true,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		});
		AddChild(_hook);

		_hookMarkerMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = WarnAmber,
			EmissionEnabled = true,
			Emission = WarnAmber,
			EmissionEnergyMultiplier = 1.8f,
			NoDepthTest = true,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		};
		var marker = new MeshInstance3D
		{
			Name = "TowHookMarker",
			Mesh = new SphereMesh { Radius = 0.08f, Height = 0.16f, RadialSegments = 8, Rings = 4 },
			Position = new Vector3(0f, 0.14f, 0f),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		marker.SetSurfaceOverrideMaterial(0, _hookMarkerMat);
		_hook.AddChild(marker);
	}

	/// <summary>
	/// Rebuild the cable between two world-space hitch points. <paramref name="maxLengthMeters"/>
	/// must match the gameplay snap length so the color ramp lines up with the snap warning.
	/// </summary>
	public void UpdateCable(Vector3 fromWorld, Vector3 toWorld, float maxLengthMeters = 10.5f)
	{
		if (!IsInsideTree())
			return;

		var span = toWorld - fromWorld;
		var len = span.Length();
		if (len < 0.05f)
		{
			Visible = false;
			return;
		}
		Visible = true;

		// Resting tow-follow distance sits around a quarter of snap length; treat that as fully slack.
		var slackRef = MathF.Max(0.5f, maxLengthMeters * 0.25f);
		var tension = Mathf.Clamp((len - slackRef) / MathF.Max(0.1f, maxLengthMeters - slackRef), 0f, 1f);
		CurrentTension01 = tension;

		var color = tension < 0.5f
			? SlackGray.Lerp(WarnAmber, tension * 2f)
			: WarnAmber.Lerp(SnapRed, (tension - 0.5f) * 2f);
		if (tension > 0.82f)
		{
			// Near-snap pulse: flash toward white so the "about to lose the wreck" moment pops.
			var pulse = 0.5f + 0.5f * MathF.Sin(Time.GetTicksMsec() * 0.02f);
			color = color.Lerp(new Color(1f, 0.9f, 0.85f), pulse * 0.45f);
		}
		_matCore.AlbedoColor = new Color(color.R, color.G, color.B, 0.95f);
		_matCore.Emission = color;
		_matCore.EmissionEnergyMultiplier = Mathf.Lerp(0.9f, 2.8f, tension);
		// The sheath warms slightly with tension so the whole cable participates in the warning.
		var sheath = SheathDark.Lerp(color, 0.12f + 0.30f * tension);
		_matSheath.AlbedoColor = new Color(sheath.R, sheath.G, sheath.B, 0.96f);

		// Quadratic bezier: control point drops 2x the desired mid sag so the curve's midpoint sags
		// by exactly `sag`. Taut cable keeps a barely-visible droop.
		var sag = 0.08f + MathF.Pow(1f - tension, 1.6f) * MaxSagMeters;
		var control = (fromWorld + toWorld) * 0.5f - Vector3.Up * (sag * 2f);

		// Sample the curve once, then extrude ribbons from the shared points.
		Span<Vector3> pts = stackalloc Vector3[CurveSegments + 1];
		for (var i = 0; i <= CurveSegments; i++)
		{
			var t = i / (float)CurveSegments;
			var omt = 1f - t;
			pts[i] = fromWorld * (omt * omt) + control * (2f * omt * t) + toWorld * (t * t);
		}

		var side = span.Cross(Vector3.Up);
		side = side.LengthSquared() < 0.0001f ? Vector3.Right : side.Normalized();

		_im.ClearSurfaces();
		// Dark sheath: horizontal ribbon (the top-down read) + vertical ribbon (glancing angles).
		BuildRibbon(pts, side, CableHalfWidth, _matSheath, yLift: 0f);
		BuildRibbon(pts, Vector3.Up, CableHalfWidth * 0.60f, _matSheath, yLift: 0f);
		// Bright tension strip floats a hair above the sheath.
		BuildRibbon(pts, side, CoreHalfWidth, _matCore, yLift: 0.015f);

		// Hook rides the wreck-end hitch point, live.
		if (_hook != null && GodotObject.IsInstanceValid(_hook))
			_hook.GlobalPosition = toWorld + Vector3.Up * 0.05f;
	}

	private void BuildRibbon(ReadOnlySpan<Vector3> pts, Vector3 axis, float halfWidth, Material mat, float yLift)
	{
		_im.SurfaceBegin(Godot.Mesh.PrimitiveType.TriangleStrip, mat);
		for (var i = 0; i < pts.Length; i++)
		{
			var p = pts[i] + Vector3.Up * yLift;
			_im.SurfaceAddVertex(ToLocal(p - axis * halfWidth));
			_im.SurfaceAddVertex(ToLocal(p + axis * halfWidth));
		}
		_im.SurfaceEnd();
	}
}
