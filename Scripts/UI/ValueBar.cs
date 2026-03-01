// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/ValueBar.cs
// Purpose: Small HUD bar used throughout the UI (sections/tires/speed/RPM). Supports vertical or horizontal fill.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;
using WastelandSurvivor.Framework.SceneBinding;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Lightweight bar for HUD: supports horizontal or vertical fill with centered value text.
/// </summary>
public partial class ValueBar : Control
{
	private bool _vertical = false;
	private bool _bound = false;

	[Export]
	public bool Vertical
	{
		get => _vertical;
		set
		{
			_vertical = value;
			if (IsNodeReady())
			{
				ApplyLabelRotation();
				ApplyLabelStyle();
			}
		}
	}

	private ColorRect _bg = null!;
	private ColorRect _fill = null!;
	private Label _lbl = null!;

	private void EnsureBound()
	{
		if (_bound) return;
		_bound = true;
		var b = new SceneBinder(this, nameof(ValueBar));
		_bg = b.Req<ColorRect>("Bg");
		_fill = b.Req<ColorRect>("Fill");
		_lbl = b.Req<Label>("Lbl");
	}

	public override void _Ready()
	{
		GameUiTheme.ApplyToTree(this);
		EnsureBound();

		// Slightly smaller text inside compact HUD bars.
		_lbl.AddThemeFontSizeOverride("font_size", 10);

		ApplyLabelRotation();
		ApplyLabelStyle();
	}

	public override void _Notification(int what)
	{
		// Keep rotation/pivot correct after layout.
		if (what == NotificationResized)
		{
			// Godot can emit resize notifications before _Ready, especially when scenes are being instantiated.
			// Avoid touching bound nodes until we know we're ready.
			if (!IsNodeReady()) return;
			EnsureBound();
			ApplyLabelRotation();
			ApplyLabelStyle();
		}
	}

	private void ApplyLabelRotation()
	{
		if (!_bound) return;
		if (Vertical)
			_lbl.Rotation = -Mathf.Pi / 2f;
		else
			_lbl.Rotation = 0f;

		// Rotate around center.
		_lbl.PivotOffset = _lbl.Size / 2f;
	}

	private void ApplyLabelStyle()
	{
		if (!_bound) return;
		// Vertical bars are very narrow; shrink and clip so text stays inside.
		_lbl.AddThemeFontSizeOverride("font_size", Vertical ? 9 : 10);
		_lbl.ClipText = Vertical;
	}

	public void SetValues(int cur, int max, Color fillColor, Color? textColor = null)
	{
		if (!IsNodeReady()) return;
		EnsureBound();
		max = Math.Max(1, max);
		cur = Math.Clamp(cur, 0, max);
		_lbl.Text = $"{cur}/{max}";
		_fill.Color = fillColor;

		if (textColor.HasValue)
			_lbl.AddThemeColorOverride("font_color", textColor.Value);

		var pct = (double)cur / max;
		pct = double.IsFinite(pct) ? Math.Clamp(pct, 0.0, 1.0) : 0.0;
		UpdateFill((float)pct);
	}

	public void SetValues(float cur, float max, Color fillColor, Color? textColor = null, string format = "0.0")
	{
		if (!IsNodeReady()) return;
		EnsureBound();
		max = Math.Max(0.01f, max);
		cur = Mathf.Clamp(cur, 0f, max);
		_lbl.Text = $"{cur.ToString(format)}/{max.ToString(format)}";
		_fill.Color = fillColor;

		if (textColor.HasValue)
			_lbl.AddThemeColorOverride("font_color", textColor.Value);

		var pct = (double)cur / max;
		pct = double.IsFinite(pct) ? Math.Clamp(pct, 0.0, 1.0) : 0.0;
		UpdateFill((float)pct);
	}

	public void SetCustom(float pct, string text, Color fillColor, Color? textColor = null)
	{
		if (!IsNodeReady()) return;
		EnsureBound();
		_lbl.Text = text;
		_fill.Color = fillColor;
		if (textColor.HasValue)
			_lbl.AddThemeColorOverride("font_color", textColor.Value);
		UpdateFill(pct);
	}

	private void UpdateFill(float pct)
	{
		if (!IsNodeReady()) return;
		EnsureBound();
		pct = Mathf.Clamp(pct, 0f, 1f);

		// Reset offsets.
		_fill.OffsetLeft = 0;
		_fill.OffsetTop = 0;
		_fill.OffsetRight = 0;
		_fill.OffsetBottom = 0;

		if (Vertical)
		{
			// Fill from bottom.
			var h = Size.Y;
			var filled = h * pct;
			_fill.OffsetTop = h - filled;
		}
		else
		{
			// Fill from left.
			_fill.OffsetRight = Size.X * (pct - 1f);
		}
	}
}
