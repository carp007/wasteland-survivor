// -------------------------------------------------------------------------------------------------
// UiKit
// File: Scripts/Controls/HoldToActivateButton.cs
// Purpose: Reusable “hold to activate” button-like control with progress fill (mouse + keyboard).
//          Designed to be portable to future Godot/C# projects.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace GameUiKit.Controls;

public enum HoldToActivateVisualState
{
	Neutral,
	Success,
	Warning,
	Danger,
}

/// <summary>
/// A reusable control that requires the user to press-and-hold (mouse button down over the control
/// OR holding a configured shortcut key) until a duration elapses.
///
/// Visual:
/// - Button-like panel
/// - Progress fill drawn behind the text
/// - Base background can be semantically tinted (neutral/success/warning/danger)
///
/// Behavior:
/// - Releasing early resets immediately.
/// - When the hold completes, emits <see cref="Activated"/> once, and does not re-fire until fully released.
/// - Supports mixed input sources (mouse + key) without double-advancing or double-triggering.
/// </summary>
public partial class HoldToActivateButton : PanelContainer
{
	[Signal]
	public delegate void ActivatedEventHandler();

	private string _buttonText = "Hold";
	private float _holdDurationSeconds = 1.0f;
	private Key _shortcutKey = Key.None;
	private HoldToActivateVisualState _visualState = HoldToActivateVisualState.Neutral;
	private bool _disabled;

	private bool _built;
	private StyleBoxFlat? _panelStyle;
	private Control? _layer;
	private ColorRect? _bg;
	private ColorRect? _fill;
	private Label? _label;

	private bool _mouseHeld;
	private bool _keyHeld;
	private float _holdSeconds;
	private bool _firedThisHold;

	/// <summary>Button label text.</summary>
	[Export]
	public string ButtonText
	{
		get => _buttonText;
		set
		{
			_buttonText = value ?? string.Empty;
			if (_label != null) _label.Text = _buttonText;
		}
	}

	/// <summary>Hold duration in seconds required to activate.</summary>
	[Export]
	public float HoldDurationSeconds
	{
		get => _holdDurationSeconds;
		set => _holdDurationSeconds = Math.Max(0.01f, value);
	}

	/// <summary>
	/// Optional keyboard shortcut that can also be held to activate while this control is visible.
	/// Use <see cref="Key.None"/> to disable.
	/// </summary>
	[Export]
	public Key ShortcutKey
	{
		get => _shortcutKey;
		set => _shortcutKey = value;
	}

	/// <summary>Semantic visual style for the button.</summary>
	[Export]
	public HoldToActivateVisualState VisualState
	{
		get => _visualState;
		set
		{
			_visualState = value;
			ApplyVisualStyle();
		}
	}

	/// <summary>Disables interaction and dims the control.</summary>
	[Export]
	public bool Disabled
	{
		get => _disabled;
		set
		{
			if (_disabled == value)
			{
				ApplyVisualStyle();
				return;
			}

			_disabled = value;
			_mouseHeld = false;
			_keyHeld = false;
			ResetHold();
			ApplyVisualStyle();
		}
	}

	/// <summary>Progress from 0..1.</summary>
	public float Progress01 { get; private set; }

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.All;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		CustomMinimumSize = new Vector2(240, 44);

		EnsureBuilt();
		SetProgress01(Progress01);
		ApplyVisualStyle();
		SetProcess(true);
	}

	public override void _ExitTree()
	{
		// Ensure we don't leak state across reuse.
		_mouseHeld = false;
		_keyHeld = false;
		ResetHold();
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (_disabled)
		{
			AcceptEvent();
			return;
		}

		// Mouse press/hold.
		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				_mouseHeld = true;
			}
			else
			{
				_mouseHeld = false;
				// If the key is currently held, do not reset (avoid mixed-input edge cases).
				if (!IsShortcutHeldNow())
					ResetHold();
			}
			AcceptEvent();
			return;
		}
	}

	public override void _Process(double delta)
	{
		if (_disabled)
		{
			if (_mouseHeld || _keyHeld || Progress01 > 0f)
			{
				_mouseHeld = false;
				_keyHeld = false;
				ResetHold();
			}
			return;
		}

		// If we are hidden, treat as fully released.
		if (!IsVisibleInTree())
		{
			if (_mouseHeld || _keyHeld)
			{
				_mouseHeld = false;
				_keyHeld = false;
				ResetHold();
			}
			return;
		}

		// Track key-held state each frame (so it works regardless of focus).
		_keyHeld = IsShortcutHeldNow();

		// If the mouse was held but released outside our bounds, Godot may not route the button-up
		// back to this control. Use global state to detect release.
		if (_mouseHeld && !Input.IsMouseButtonPressed(MouseButton.Left))
			_mouseHeld = false;

		var holding = (_mouseHeld || _keyHeld);
		if (!holding)
		{
			ResetHold();
			return;
		}

		// Once fired, stay “latched” until fully released.
		if (_firedThisHold)
		{
			SetProgress01(1f);
			return;
		}

		_holdSeconds += (float)delta;
		var dur = Math.Max(0.01f, _holdDurationSeconds);
		var p = Mathf.Clamp(_holdSeconds / dur, 0f, 1f);
		SetProgress01(p);

		if (p < 0.999f) return;
		_firedThisHold = true;
		SetProgress01(1f);
		EmitSignal(SignalName.Activated);
	}

	private bool IsShortcutHeldNow()
	{
		if (_disabled || _shortcutKey == Key.None) return false;
		// Prefer physical key state when available; fall back to keycode.
		return Input.IsPhysicalKeyPressed(_shortcutKey) || Input.IsKeyPressed(_shortcutKey);
	}

	private void EnsureBuilt()
	{
		if (_built) return;
		_built = true;

		// Default “button-ish” style that stays readable without requiring a custom scene.
		// Projects can override via Theme or theme overrides.
		_panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.06f, 0.08f, 0.90f),
			BorderColor = new Color(1f, 1f, 1f, 0.18f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ContentMarginLeft = 10,
			ContentMarginRight = 10,
			ContentMarginTop = 8,
			ContentMarginBottom = 8,
		};
		AddThemeStyleboxOverride("panel", _panelStyle);

		_layer = new Control
		{
			Name = "Layer",
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		AddChild(_layer);

		_bg = new ColorRect
		{
			Name = "Background",
			Color = new Color(1f, 1f, 1f, 0.10f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_layer.AddChild(_bg);
		SetFullRect(_bg);

		_fill = new ColorRect
		{
			Name = "Fill",
			Color = new Color(1f, 1f, 1f, 0.20f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_layer.AddChild(_fill);
		SetFullRect(_fill);
		_fill.AnchorRight = 0f; // start empty

		_label = new Label
		{
			Name = "Label",
			Text = _buttonText,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Off,
			ClipText = true,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_layer.AddChild(_label);
		SetFullRect(_label);
	}

	private void ResetHold()
	{
		_holdSeconds = 0f;
		_firedThisHold = false;
		SetProgress01(0f);
	}

	private void SetProgress01(float p)
	{
		Progress01 = Mathf.Clamp(p, 0f, 1f);
		if (_fill != null)
		{
			// AnchorRight expresses the fill width as a fraction of parent width.
			_fill.AnchorRight = Progress01;
			_fill.OffsetLeft = 0;
			_fill.OffsetTop = 0;
			_fill.OffsetRight = 0;
			_fill.OffsetBottom = 0;
		}
	}

	private void ApplyVisualStyle()
	{
		if (!_built)
			return;

		var accent = _visualState switch
		{
			HoldToActivateVisualState.Success => new Color(0.22f, 0.82f, 0.42f, 1f),
			HoldToActivateVisualState.Warning => new Color(0.96f, 0.80f, 0.22f, 1f),
			HoldToActivateVisualState.Danger => new Color(0.92f, 0.34f, 0.34f, 1f),
			_ => new Color(0.10f, 0.72f, 1.00f, 1f),
		};

		var bgAlpha = _disabled ? 0.08f : 0.13f;
		var fillAlpha = _disabled ? 0.12f : 0.24f;
		var borderAlpha = _disabled ? 0.25f : 0.85f;

		if (_panelStyle != null)
		{
			_panelStyle.BgColor = new Color(0.04f, 0.05f, 0.07f, _disabled ? 0.72f : 0.90f);
			_panelStyle.BorderColor = WithAlpha(accent, borderAlpha);
		}

		if (_bg != null)
			_bg.Color = WithAlpha(accent, bgAlpha);

		if (_fill != null)
			_fill.Color = WithAlpha(accent, fillAlpha);

		if (_label != null)
			_label.Modulate = _disabled ? new Color(1f, 1f, 1f, 0.55f) : Colors.White;

		MouseDefaultCursorShape = _disabled ? CursorShape.Arrow : CursorShape.PointingHand;
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = _disabled ? FocusModeEnum.None : FocusModeEnum.All;
		QueueRedraw();
	}

	private static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, a);

	private static void SetFullRect(Control c)
	{
		c.AnchorLeft = 0;
		c.AnchorTop = 0;
		c.AnchorRight = 1;
		c.AnchorBottom = 1;
		c.OffsetLeft = 0;
		c.OffsetTop = 0;
		c.OffsetRight = 0;
		c.OffsetBottom = 0;
	}
}
