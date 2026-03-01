// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/RadarHud.cs
// Purpose: UI view/controller code for scenes under Scenes/UI.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

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

		// Range ring + crosshair lines (drawn inside the themed panel).
		DrawArc(center, radius, 0f, Mathf.Pi * 2f, 48, new Color(1f, 1f, 1f, 0.10f), 2f);
		DrawLine(center + new Vector2(-radius, 0), center + new Vector2(radius, 0), new Color(1f, 1f, 1f, 0.06f), 1f);
		DrawLine(center + new Vector2(0, -radius), center + new Vector2(0, radius), new Color(1f, 1f, 1f, 0.06f), 1f);

		// Player dot
		DrawCircle(center, DotRadius + 1.5f, PlayerColor);

		// Enemies
		var enemies = GetTree().GetNodesInGroup("enemy_vehicle");
		if (enemies == null || enemies.Count == 0) return;

		var pPos = _player.GlobalPosition;
		pPos.Y = 0f;

		var metersPerPixel = RangeMeters / radius;
		if (metersPerPixel <= 0.001f) metersPerPixel = 0.001f;

		foreach (var e in enemies)
		{
			if (e is not Node3D n || !GodotObject.IsInstanceValid(n)) continue;
			var rel = n.GlobalPosition - pPos;
			rel.Y = 0f;

			// North-up: +X is right, -Z is up (screen negative Y).
			var px = rel.X / metersPerPixel;
			var py = rel.Z / metersPerPixel; // rel.Z < 0 => up
			var pt = center + new Vector2(px, py);

			// Clamp to radar bounds.
			var to = pt - center;
			var len = to.Length();
			if (len > radius)
				pt = center + to / len * radius;

			DrawCircle(pt, DotRadius, EnemyColor);
		}
	}
}
