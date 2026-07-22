// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/RadarHud.cs
// Purpose: UI view/controller code for scenes under Scenes/UI.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;
using GameUiKit.SceneBinding;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Simple 2D radar/minimap.
///
/// - Anchored bottom-right in ArenaRealtimeView.
/// - Shows controlled player entity at center; enemies as dots.
/// - **North-up**: world -Z is always "up" on the radar (does not rotate with player).
///
/// This intentionally avoids rendering the 3D world (SubViewport) to keep it cheap and robust.
/// </summary>
public partial class RadarHud : Control
{
	[Export] public float RangeMeters { get; set; } = 60f;
	[Export] public float UpdateHz { get; set; } = 30f;
	[Export] public float DotRadius { get; set; } = 3.5f;
	[Export] public Color PlayerColor { get; set; } = new Color(0.20f, 0.95f, 0.35f, 1f);
	[Export] public Color EnemyColor { get; set; } = new Color(1.0f, 0.25f, 0.25f, 1f);

	private Node3D? _player;
	private float _t;

	public override void _Ready()
	{
		// No required bindings today, but keep the HUD on the standard binding path.
		SceneAutoBinder.Apply(this, nameof(RadarHud));

		GameUiTheme.ApplyToTree(this);
		MouseFilter = MouseFilterEnum.Ignore;
		ClipContents = true;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		_t += (float)delta;
		var interval = 1f / MathF.Max(1f, UpdateHz);
		if (_t < interval) return;
		_t = 0f;

		var newPlayer = GetTree().GetFirstNodeInGroup("player_controlled") as Node3D;
		if (newPlayer == null)
			newPlayer = GetTree().GetFirstNodeInGroup("player_vehicle") as Node3D;

		var changed = !ReferenceEquals(_player, newPlayer);
		_player = newPlayer;
		Visible = _player != null && GodotObject.IsInstanceValid(_player);
		if (Visible)
			QueueRedraw();
		else if (changed)
			QueueRedraw();
	}

	public override void _Draw()
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player))
			return;

		var outer = new Rect2(Vector2.Zero, Size);
		var sb = GetThemeStylebox("panel", "PanelContainer") ?? GetThemeStylebox("panel", "Panel");

		// Godot's C# StyleBox API doesn't expose GetContentRect(). Compute it from content margins.
		float ml = 0, mt = 0, mr = 0, mb = 0;
		if (sb != null)
		{
			ml = sb.GetContentMargin(Side.Left);
			mt = sb.GetContentMargin(Side.Top);
			mr = sb.GetContentMargin(Side.Right);
			mb = sb.GetContentMargin(Side.Bottom);
		}
		var inner = new Rect2(
			outer.Position + new Vector2(ml, mt),
			outer.Size - new Vector2(ml + mr, mt + mb)
		);

		var center = inner.Position + inner.Size * 0.5f;
		var radius = MathF.Min(inner.Size.X, inner.Size.Y) * 0.5f - 2f;
		if (radius <= 4f) return;

		// Scope face: dark disc, range rings, bearing ticks, slow sweep.
		DrawCircle(center, radius, new Color(0.012f, 0.045f, 0.035f, 0.88f));
		DrawArc(center, radius, 0f, Mathf.Pi * 2f, 56, new Color(0.25f, 0.85f, 0.65f, 0.35f), 1.6f);
		DrawArc(center, radius * 0.62f, 0f, Mathf.Pi * 2f, 40, new Color(0.25f, 0.85f, 0.65f, 0.12f), 1f);
		DrawArc(center, radius * 0.30f, 0f, Mathf.Pi * 2f, 28, new Color(0.25f, 0.85f, 0.65f, 0.08f), 1f);
		for (var i = 0; i < 8; i++)
		{
			var a = i * Mathf.Pi / 4f;
			var dirTick = new Vector2(MathF.Sin(a), -MathF.Cos(a));
			var major = i % 2 == 0;
			DrawLine(center + dirTick * (radius - (major ? 6f : 3.5f)), center + dirTick * radius,
				new Color(0.25f, 0.85f, 0.65f, major ? 0.45f : 0.20f), major ? 1.6f : 1f);
		}

		// Sweep: faint rotating wedge gives the scope life without distracting.
		var sweep = (float)(Time.GetTicksMsec() % 4000) / 4000f * Mathf.Pi * 2f;
		for (var i = 0; i < 5; i++)
		{
			var trail = sweep - i * 0.05f;
			DrawLine(center, center + new Vector2(MathF.Sin(trail), -MathF.Cos(trail)) * radius,
				new Color(0.30f, 0.95f, 0.70f, 0.10f - i * 0.018f), 2f);
		}

		// Player: heading-oriented wedge (north-up map, so the wedge shows facing).
		var fwd = -_player.GlobalTransform.Basis.Z;
		var heading = MathF.Atan2(fwd.X, -fwd.Z);
		Vector2 Rot(Vector2 v, float ang) => new(
			v.X * MathF.Cos(ang) - v.Y * MathF.Sin(ang),
			v.X * MathF.Sin(ang) + v.Y * MathF.Cos(ang));
		var tip = center + Rot(new Vector2(0, -(DotRadius + 3.5f)), heading);
		var bl = center + Rot(new Vector2(-(DotRadius + 0.5f), DotRadius + 1.5f), heading);
		var br = center + Rot(new Vector2(DotRadius + 0.5f, DotRadius + 1.5f), heading);
		DrawColoredPolygon(new[] { tip, bl, br }, PlayerColor);

		var pPos = _player.GlobalPosition;
		pPos.Y = 0f;

		var metersPerPixel = RangeMeters / radius;
		if (metersPerPixel <= 0.001f) metersPerPixel = 0.001f;

		Vector2 ToRadarPoint(Vector3 world, out bool clamped)
		{
			var rel = world - pPos;
			rel.Y = 0f;

			// North-up: +X is right, -Z is up (screen negative Y).
			var pt = center + new Vector2(rel.X / metersPerPixel, rel.Z / metersPerPixel);
			var to = pt - center;
			var len = to.Length();
			clamped = len > radius;
			return clamped ? center + to / len * radius : pt;
		}

		// Hazard blips (player-known mines, oil slicks, smoke clouds) under the vehicle dots.
		foreach (var blip in WastelandSurvivor.Game.Arena.ArenaHazardTelemetry.Blips)
		{
			var pt = ToRadarPoint(blip.Position, out var clamped);
			if (clamped) continue; // off-range hazards aren't actionable; don't pin them to the rim
			switch (blip.Kind)
			{
				case WastelandSurvivor.Game.Arena.ArenaHazardKind.Mine:
					DrawMineDiamond(pt, DotRadius + 0.5f, new Color(1.0f, 0.85f, 0.25f, 0.95f));
					break;
				case WastelandSurvivor.Game.Arena.ArenaHazardKind.Oil:
					DrawCircle(pt, DotRadius - 0.5f, new Color(0.45f, 0.35f, 0.75f, 0.80f));
					break;
				case WastelandSurvivor.Game.Arena.ArenaHazardKind.Smoke:
					DrawArc(pt, DotRadius + 1.0f, 0f, Mathf.Pi * 2f, 16, new Color(0.80f, 0.80f, 0.82f, 0.70f), 1.5f);
					break;
			}
		}

		// Enemies
		var enemies = GetTree().GetNodesInGroup("enemy_vehicle");
		if (enemies == null || enemies.Count == 0) return;

		foreach (var e in enemies)
		{
			if (e is not Node3D n || !GodotObject.IsInstanceValid(n)) continue;
			var pt = ToRadarPoint(n.GlobalPosition, out _);
			DrawCircle(pt, DotRadius, EnemyColor);
		}
	}

	private void DrawMineDiamond(Vector2 at, float r, Color color)
	{
		var pts = new Vector2[]
		{
			at + new Vector2(0, -r),
			at + new Vector2(r, 0),
			at + new Vector2(0, r),
			at + new Vector2(-r, 0),
		};
		DrawColoredPolygon(pts, color);
	}
}
