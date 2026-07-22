// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/ValueBar.cs
// Purpose: Small HUD bar used throughout the UI (sections/tires/speed/RPM). Supports vertical or horizontal fill.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Lightweight bar for HUD: supports horizontal or vertical fill with centered value text.
/// </summary>
public partial class ValueBar : Control
{
	private bool _vertical = false;
	private bool _bound = false;

	// Godot can send notifications or parent scripts can call SetValues before _Ready().
	// Cache the most recent state so the bar can render correctly once ready/layout is complete.
	private bool _hasPending = false;
	private float _pendingPct = 0f;
	private string _pendingText = string.Empty;
	private Color _pendingFillColor;
	private bool _pendingHasTextColor = false;
	private Color _pendingTextColor;

	private bool _hasLast = false;
	private float _lastPct = 0f;

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

	// Custom-drawn rounded bar (the legacy ColorRects are hidden and kept only for scene compat).
	private readonly StyleBoxFlat _sbBg = new()
	{
		BgColor = new Color(0.015f, 0.025f, 0.045f, 0.85f),
		BorderColor = new Color(1f, 1f, 1f, 0.10f),
		BorderWidthLeft = 1,
		BorderWidthTop = 1,
		BorderWidthRight = 1,
		BorderWidthBottom = 1,
		CornerRadiusTopLeft = 3,
		CornerRadiusTopRight = 3,
		CornerRadiusBottomLeft = 3,
		CornerRadiusBottomRight = 3,
	};
	private readonly StyleBoxFlat _sbFill = new()
	{
		CornerRadiusTopLeft = 2,
		CornerRadiusTopRight = 2,
		CornerRadiusBottomLeft = 2,
		CornerRadiusBottomRight = 2,
	};
	private Color _fillColor = new(0.5f, 0.5f, 0.5f);

	public override void _Draw()
	{
		var size = Size;
		if (size.X < 2f || size.Y < 2f) return;

		_sbBg.Draw(GetCanvasItem(), new Rect2(Vector2.Zero, size));

		var pct = _hasLast ? _lastPct : 0f;
		if (pct <= 0.001f) return;

		Rect2 fillRect;
		if (Vertical)
		{
			var h = MathF.Max(2f, (size.Y - 2f) * pct);
			fillRect = new Rect2(new Vector2(1f, size.Y - 1f - h), new Vector2(size.X - 2f, h));
		}
		else
		{
			var w = MathF.Max(2f, (size.X - 2f) * pct);
			fillRect = new Rect2(new Vector2(1f, 1f), new Vector2(w, size.Y - 2f));
		}

		_sbFill.BgColor = _fillColor;
		_sbFill.Draw(GetCanvasItem(), fillRect);

		// Bright cap line at the fill edge gives the bar a readable "level" marker.
		var cap = new Color(
			Mathf.Clamp(_fillColor.R * 1.35f + 0.10f, 0f, 1f),
			Mathf.Clamp(_fillColor.G * 1.35f + 0.10f, 0f, 1f),
			Mathf.Clamp(_fillColor.B * 1.35f + 0.10f, 0f, 1f),
			0.95f);
		if (Vertical)
			DrawRect(new Rect2(new Vector2(1f, fillRect.Position.Y), new Vector2(size.X - 2f, 1.5f)), cap);
		else
			DrawRect(new Rect2(new Vector2(fillRect.End.X - 1.5f, 1f), new Vector2(1.5f, size.Y - 2f)), cap);

		// Optional gauge segmentation (horizontal bars only): faint ticks over track + fill make
		// speed/RPM read as instruments instead of plain progress rectangles.
		if (GaugeTicks > 1 && !Vertical)
		{
			var tickColor = new Color(0f, 0f, 0f, 0.38f);
			for (var i = 1; i < GaugeTicks; i++)
			{
				var x = 1f + (size.X - 2f) * i / GaugeTicks;
				DrawRect(new Rect2(new Vector2(x, 1f), new Vector2(1f, size.Y - 2f)), tickColor);
			}
		}
	}

	/// <summary>Horizontal-gauge tick count (0 = plain bar). Ticks divide the track into equal segments.</summary>
	public int GaugeTicks { get; set; }

	private void SetPending(float pct, string text, Color fillColor, Color? textColor)
	{
		_pendingPct = Mathf.Clamp(pct, 0f, 1f);
		_pendingText = text ?? string.Empty;
		_pendingFillColor = fillColor;
		if (textColor.HasValue)
		{
			_pendingHasTextColor = true;
			_pendingTextColor = textColor.Value;
		}
		else
		{
			_pendingHasTextColor = false;
		}
		_hasPending = true;
	}

	private void ApplyPending()
	{
		if (!_hasPending) return;
		if (!IsNodeReady()) return;
		EnsureBound();
		_lbl.Text = _pendingText;
		_fill.Color = _pendingFillColor;
		if (_pendingHasTextColor)
			_lbl.AddThemeColorOverride("font_color", _pendingTextColor);
		UpdateFill(_pendingPct);
		_hasPending = false;
	}

	private void EnsureBound()
	{
		if (_bound) return;
		_bound = true;
		// Scene instances (ValueBar.tscn) carry Bg/Fill/Lbl children; bars constructed in code
		// (e.g. the target HUD hull readout) get equivalent children built here. Bg/Fill are
		// hidden color stashes for the custom draw, so a missing child must never throw — an
		// unbound bar inside RefreshStats() once unwound TryFire before the missile cooldown
		// was set, turning lock-on missiles into a machine gun.
		_bg = GetNodeOrNull<ColorRect>("Bg") ?? AddFullRect("Bg", new Color(0f, 0f, 0f, 0.45f));
		_fill = GetNodeOrNull<ColorRect>("Fill") ?? AddFullRect("Fill", new Color(0.45f, 0.9f, 0.45f));
		_lbl = GetNodeOrNull<Label>("Lbl") ?? AddCenteredLabel("Lbl");
	}

	private ColorRect AddFullRect(string name, Color color)
	{
		var rect = new ColorRect { Name = name, Color = color };
		AddChild(rect);
		rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		return rect;
	}

	private Label AddCenteredLabel(string name)
	{
		var lbl = new Label
		{
			Name = name,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};
		AddChild(lbl);
		lbl.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		return lbl;
	}

	public override void _Ready()
	{
		GameUiTheme.ApplyToTree(this);
		EnsureBound();

		// The bar is custom-drawn (rounded, bordered, capped); hide the legacy flat rects.
		_bg.Visible = false;
		_fill.Visible = false;

		// Slightly smaller text inside compact HUD bars, with a drop shadow for contrast on any fill.
		_lbl.AddThemeFontSizeOverride("font_size", 10);
		_lbl.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.70f));
		_lbl.AddThemeConstantOverride("shadow_offset_x", 0);
		_lbl.AddThemeConstantOverride("shadow_offset_y", 1);
		// The 1px shadow alone loses white text on bright fills (the speed bar lerps to pale gold
		// at high pct — loop-4 iter-24/27 finding). A thin dark outline keeps the value readable on
		// ANY fill tone, including when the label straddles the bright-fill/dark-track boundary.
		_lbl.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
		_lbl.AddThemeConstantOverride("outline_size", 3);

		ApplyLabelRotation();
		ApplyLabelStyle();

		// If anyone tried to set values before _Ready(), apply them once layout has settled.
		if (_hasPending)
			CallDeferred(nameof(ApplyPending));
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

			// Recompute fill offsets after layout changes.
			if (_hasPending)
				ApplyPending();
			else if (_hasLast)
				UpdateFill(_lastPct);
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
		max = Math.Max(1, max);
		cur = Math.Clamp(cur, 0, max);
		var pct = (double)cur / max;
		pct = double.IsFinite(pct) ? Math.Clamp(pct, 0.0, 1.0) : 0.0;
		var text = $"{cur}/{max}";

		if (!IsNodeReady())
		{
			SetPending((float)pct, text, fillColor, textColor);
			return;
		}

		EnsureBound();
		_lbl.Text = text;
		_fill.Color = fillColor;

		if (textColor.HasValue)
			_lbl.AddThemeColorOverride("font_color", textColor.Value);

		UpdateFill((float)pct);
	}

	public void SetValues(float cur, float max, Color fillColor, Color? textColor = null, string format = "0.0")
	{
		max = Math.Max(0.01f, max);
		cur = Mathf.Clamp(cur, 0f, max);
		var pct = (double)cur / max;
		pct = double.IsFinite(pct) ? Math.Clamp(pct, 0.0, 1.0) : 0.0;
		var text = $"{cur.ToString(format)}/{max.ToString(format)}";

		if (!IsNodeReady())
		{
			SetPending((float)pct, text, fillColor, textColor);
			return;
		}

		EnsureBound();
		_lbl.Text = text;
		_fill.Color = fillColor;

		if (textColor.HasValue)
			_lbl.AddThemeColorOverride("font_color", textColor.Value);

		UpdateFill((float)pct);
	}

	public void SetCustom(float pct, string text, Color fillColor, Color? textColor = null)
	{
		pct = Mathf.Clamp(pct, 0f, 1f);
		if (!IsNodeReady())
		{
			SetPending(pct, text, fillColor, textColor);
			return;
		}

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

		_hasLast = true;
		_lastPct = pct;
		_fillColor = _fill.Color; // SetValues/SetCustom stash the requested color on the hidden rect
		QueueRedraw();
	}
}
