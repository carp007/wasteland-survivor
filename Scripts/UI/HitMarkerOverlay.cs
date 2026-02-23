using Godot;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Minimal center-screen hit marker that flashes and fades.
/// Purely code-driven (no textures).
/// </summary>
public partial class HitMarkerOverlay : Control
{
	private float _tRemaining = 0f;
	private float _tTotal = 0.12f;
	private Color _color = Colors.White;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Visible = false;
		SetProcess(true);
	}

	public void Flash(Color color, float durationSeconds = 0.12f)
	{
		_color = color;
		_tTotal = Mathf.Max(0.05f, durationSeconds);
		_tRemaining = _tTotal;
		Visible = true;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (_tRemaining <= 0f)
		{
			if (Visible)
				Visible = false;
			return;
		}
		_tRemaining -= (float)delta;
		if (_tRemaining <= 0f)
		{
			Visible = false;
			return;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_tRemaining <= 0f) return;

		var alpha = Mathf.Clamp(_tRemaining / _tTotal, 0f, 1f);
		var c = new Color(_color.R, _color.G, _color.B, alpha);

		// Center of the viewport.
		var center = Size * 0.5f;
		// Marker geometry (pixels)
		const float gap = 7f;
		const float len = 10f;
		const float thickness = 2f;

		// Four short lines forming an X-like hit marker (but with gaps).
		DrawLine(center + new Vector2(-gap - len, -gap - len), center + new Vector2(-gap, -gap), c, thickness);
		DrawLine(center + new Vector2(gap, -gap), center + new Vector2(gap + len, -gap - len), c, thickness);
		DrawLine(center + new Vector2(-gap - len, gap + len), center + new Vector2(-gap, gap), c, thickness);
		DrawLine(center + new Vector2(gap, gap), center + new Vector2(gap + len, gap + len), c, thickness);
	}
}
