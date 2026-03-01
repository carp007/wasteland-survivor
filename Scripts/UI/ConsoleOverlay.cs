// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/ConsoleOverlay.cs
// Purpose: In-game console overlay. Displays GameConsole history, supports command input, and provides a collapsed one-line mode.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Global console overlay:
/// - Tilde/backtick toggles visibility.
/// - Expanded view: header + scrollable history + input.
/// - Collapsed view: single-line (most recent entry) with no header.
/// </summary>
public partial class ConsoleOverlay : Control
{
	private const float BottomMargin = 10f;
	private const float WidthPct = 0.60f;

	private const int LogFontSize = 12;

	private const float ExpandedHeight = 148f;
	private const float CollapsedHeight = 30f;

	private PanelContainer? _panel;
	private PanelContainer? _headerPanel;
	private RichTextLabel? _lblTitle;
	private Button? _btnCollapse;

	private PanelContainer? _collapsedBar;
	private Label? _collapsedLine;
	private Button? _btnExpand;

	private VBoxContainer? _expandedBody;
	private RichTextLabel? _rtl;
	private LineEdit? _input;

	private bool _expanded = true;
	private GameConsole? _console;

	public override void _Ready()
	{
		GameUiTheme.ApplyToTree(this);
		// Ensure this overlay is clickable/interactive and renders above other UI.
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.All;
		ZAsRelative = false;
		ZIndex = 1000;
		SetProcessInput(true);

		// Anchor to bottom-left at 60% screen width (keeps it away from right-side HUD).
		AnchorLeft = 0f;
		AnchorRight = WidthPct;
		AnchorTop = 1;
		AnchorBottom = 1;

		OffsetLeft = 0;
		OffsetRight = 0;
		OffsetBottom = -BottomMargin;
		OffsetTop = OffsetBottom - ExpandedHeight;

		Visible = false;
		_expanded = true;

		_panel = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Stop,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		AddChild(_panel);
		_panel.AnchorLeft = 0;
		_panel.AnchorRight = 1;
		_panel.AnchorTop = 0;
		_panel.AnchorBottom = 1;
		_panel.OffsetLeft = 0;
		_panel.OffsetRight = 0;
		_panel.OffsetTop = 0;
		_panel.OffsetBottom = 0;

		var bg = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.28f) };
		_panel.AddThemeStyleboxOverride("panel", bg);

		var pad = new MarginContainer
		{
			MouseFilter = MouseFilterEnum.Pass,
		};
		pad.AnchorLeft = 0;
		pad.AnchorRight = 1;
		pad.AnchorTop = 0;
		pad.AnchorBottom = 1;
		pad.AddThemeConstantOverride("margin_left", 8);
		pad.AddThemeConstantOverride("margin_right", 8);
		pad.AddThemeConstantOverride("margin_top", 4);
		pad.AddThemeConstantOverride("margin_bottom", 4);
		_panel.AddChild(pad);

		var root = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		root.MouseFilter = MouseFilterEnum.Pass;
		pad.AddChild(root);

		BuildHeader(root);
		BuildCollapsedBar(root);
		BuildExpandedBody(root);

		// Hook up to the console service.
		var app = App.Instance;
		_console = (app?.Services.TryGet<GameConsole>(out var c) == true) ? c : null;
		if (_console != null)
		{
			RebuildAll();
			_console.LineAdded += OnLineAdded;
			_console.LinesReset += OnLinesReset;
		}

		ApplyExpandedState();
		// When calling engine methods by string (deferred/Call), use snake_case.
		// ("MoveToFront" would look for a C# method and fail.)
		CallDeferred("move_to_front");
		if (Visible)
			CallDeferred(nameof(FocusInputIfVisible));
	}

	public override void _ExitTree()
	{
		if (_console != null)
		{
			_console.LineAdded -= OnLineAdded;
			_console.LinesReset -= OnLinesReset;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey k)
			return;
		if (!k.Pressed || k.Echo)
			return;

		// Tilde/backtick key toggles visibility.
		// Godot C# enum naming varies; use ASCII values.
		// ` = 96, ~ = 126
		var code = (int)k.Keycode;
		if (code == 96 || code == 126)
		{
			Visible = !Visible;
			if (Visible)
				if (Visible)
			CallDeferred(nameof(FocusInputIfVisible));
			GetViewport().SetInputAsHandled();
		}
	}

	private void BuildHeader(VBoxContainer parent)
	{
		_headerPanel = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 0),
		};
		// Ensure child controls (collapse button) receive mouse input.
		_headerPanel.MouseFilter = MouseFilterEnum.Stop;
		var headerBg = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.18f) };
		_headerPanel.AddThemeStyleboxOverride("panel", headerBg);
		parent.AddChild(_headerPanel);

		var headerPad = new MarginContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
		};
		headerPad.MouseFilter = MouseFilterEnum.Pass;
		headerPad.AddThemeConstantOverride("margin_left", 6);
		headerPad.AddThemeConstantOverride("margin_right", 4);
		headerPad.AddThemeConstantOverride("margin_top", 2);
		headerPad.AddThemeConstantOverride("margin_bottom", 2);
		_headerPanel.AddChild(headerPad);

		var header = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		header.MouseFilter = MouseFilterEnum.Pass;
		header.AddThemeConstantOverride("separation", 6);
		headerPad.AddChild(header);

		_lblTitle = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			ScrollFollowing = false,
			Text = "[b]CONSOLE[/b]",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		_lblTitle.MouseFilter = MouseFilterEnum.Ignore;
		_lblTitle.AddThemeColorOverride("default_color", new Color(1f, 0.84f, 0.2f)); // gold
		// Match log line font size (and ensure bold uses the same size).
		_lblTitle.AddThemeFontSizeOverride("normal_font_size", LogFontSize);
		_lblTitle.AddThemeFontSizeOverride("bold_font_size", LogFontSize);
		_lblTitle.AddThemeFontSizeOverride("font_size", LogFontSize);
		header.AddChild(_lblTitle);

		_btnCollapse = new Button
		{
			Text = "▾",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = new Vector2(18, 14),
		};
		_btnCollapse.MouseFilter = MouseFilterEnum.Stop;
		_btnCollapse.AddThemeFontSizeOverride("font_size", 10);
		_btnCollapse.AddThemeConstantOverride("content_margin_left", 3);
		_btnCollapse.AddThemeConstantOverride("content_margin_right", 3);
		_btnCollapse.AddThemeConstantOverride("content_margin_top", 1);
		_btnCollapse.AddThemeConstantOverride("content_margin_bottom", 1);
		_btnCollapse.Pressed += () => SetExpanded(false);
		header.AddChild(_btnCollapse);

		// Clicking the header (not the button) also collapses.
		_headerPanel.GuiInput += OnHeaderGuiInput;
	}

	private void BuildCollapsedBar(VBoxContainer parent)
	{
		_collapsedBar = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 20),
		};
		_collapsedBar.MouseFilter = MouseFilterEnum.Stop;
		var barBg = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.18f) };
		_collapsedBar.AddThemeStyleboxOverride("panel", barBg);
		parent.AddChild(_collapsedBar);

		var barPad = new MarginContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
		};
		barPad.MouseFilter = MouseFilterEnum.Pass;
		barPad.AddThemeConstantOverride("margin_left", 6);
		barPad.AddThemeConstantOverride("margin_right", 4);
		barPad.AddThemeConstantOverride("margin_top", 2);
		barPad.AddThemeConstantOverride("margin_bottom", 2);
		_collapsedBar.AddChild(barPad);

		var h = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		h.MouseFilter = MouseFilterEnum.Pass;
		h.AddThemeConstantOverride("separation", 6);
		barPad.AddChild(h);

		_collapsedLine = new Label
		{
			Text = "(no activity yet)",
			AutowrapMode = TextServer.AutowrapMode.Off,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		_collapsedLine.MouseFilter = MouseFilterEnum.Ignore;
		_collapsedLine.AddThemeFontSizeOverride("font_size", 11);
		_collapsedLine.AddThemeColorOverride("font_color", ToColor(GameConsoleLineKind.Status));
		h.AddChild(_collapsedLine);

		_btnExpand = new Button
		{
			Text = "▸",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = new Vector2(18, 14),
		};
		_btnExpand.MouseFilter = MouseFilterEnum.Stop;
		_btnExpand.AddThemeFontSizeOverride("font_size", 10);
		_btnExpand.AddThemeConstantOverride("content_margin_left", 3);
		_btnExpand.AddThemeConstantOverride("content_margin_right", 3);
		_btnExpand.AddThemeConstantOverride("content_margin_top", 1);
		_btnExpand.AddThemeConstantOverride("content_margin_bottom", 1);
		_btnExpand.Pressed += () => SetExpanded(true);
		h.AddChild(_btnExpand);

		// Clicking the collapsed bar anywhere expands.
		_collapsedBar.GuiInput += OnCollapsedGuiInput;
	}

	private void BuildExpandedBody(VBoxContainer parent)
	{
		_expandedBody = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		_expandedBody.MouseFilter = MouseFilterEnum.Pass;
		_expandedBody.AddThemeConstantOverride("separation", 4);
		parent.AddChild(_expandedBody);

		_rtl = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = false,
			ScrollActive = true,
			ScrollFollowing = true,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		_rtl.MouseFilter = MouseFilterEnum.Stop;
		_rtl.AddThemeFontSizeOverride("normal_font_size", LogFontSize);
		_rtl.AddThemeFontSizeOverride("bold_font_size", LogFontSize);
		_rtl.AddThemeFontSizeOverride("font_size", LogFontSize);
		_rtl.CustomMinimumSize = new Vector2(0, 78); // ~5 lines visible
		_expandedBody.AddChild(_rtl);

		_input = new LineEdit
		{
			PlaceholderText = "Enter command…",
			ClearButtonEnabled = true,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		_input.MouseFilter = MouseFilterEnum.Stop;
		_input.AddThemeFontSizeOverride("font_size", 11);
		_input.CustomMinimumSize = new Vector2(0, 22);
		_input.TextSubmitted += OnCommandSubmitted;
		_expandedBody.AddChild(_input);
	}

	private void SetExpanded(bool expanded)
	{
		_expanded = expanded;
		ApplyExpandedState();
		if (Visible)
			if (Visible)
			CallDeferred(nameof(FocusInputIfVisible));
	}

	private void ApplyExpandedState()
	{
		if (_headerPanel != null)
			_headerPanel.Visible = _expanded;
		if (_expandedBody != null)
			_expandedBody.Visible = _expanded;
		if (_collapsedBar != null)
			_collapsedBar.Visible = !_expanded;

		var h = _expanded ? ExpandedHeight : CollapsedHeight;
		OffsetTop = OffsetBottom - h;
	}

	private void FocusInputIfVisible()
	{
		if (!Visible) return;
		if (!_expanded) return;
		_input?.GrabFocus();
	}

	private void OnHeaderGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mb)
			return;
		if (!mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;
		if (_btnCollapse == null)
			return;

		// Avoid double-trigger when clicking the actual collapse button.
		if (_btnCollapse.GetGlobalRect().HasPoint(GetGlobalMousePosition()))
			return;

		SetExpanded(false);
		GetViewport().SetInputAsHandled();
	}

	private void OnCollapsedGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mb)
			return;
		if (!mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;
		if (_btnExpand == null)
			return;

		// Avoid double-trigger when clicking the actual expand button.
		if (_btnExpand.GetGlobalRect().HasPoint(GetGlobalMousePosition()))
			return;

		SetExpanded(true);
		GetViewport().SetInputAsHandled();
	}

	private void OnLinesReset()
	{
		RebuildAll();
	}

	private void OnLineAdded(GameConsoleLine line)
	{
		AppendLine(line);
	}

	private void RebuildAll()
	{
		if (_console == null)
			return;

		if (_rtl != null)
		{
			_rtl.Text = "";
			foreach (var line in _console.Lines)
				_rtl.AppendText(FormatLine(line) + "\n");
		}

		if (_console.Lines.Count > 0)
		{
			var last = _console.Lines[^1];
			UpdateCollapsedLine(last);
		}
	}

	private void AppendLine(GameConsoleLine line)
	{
		if (_rtl != null)
			_rtl.AppendText(FormatLine(line) + "\n");

		UpdateCollapsedLine(line);
	}

	private void UpdateCollapsedLine(GameConsoleLine line)
	{
		if (_collapsedLine == null) return;
		_collapsedLine.Text = line.Text;
		_collapsedLine.AddThemeColorOverride("font_color", ToColor(line.Kind));
	}

	private void OnCommandSubmitted(string text)
	{
		if (_console == null)
			return;

		var cmd = (text ?? "").Trim();
		_input!.Text = "";
		if (cmd.Length == 0)
			return;

		// Echo command (Input/gold), then execute.
		var handled = ExecuteBuiltInCommand(cmd);
		if (!handled)
		{
			_console.Input($"> {cmd}");
			_console.Error($"unrecognized command: {cmd}");
		}
	}

	private bool ExecuteBuiltInCommand(string cmd)
	{
		if (_console == null) return false;

		// Parse: "command arg1 arg2" (basic)
		var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length == 0) return false;
		var head = parts[0].ToLowerInvariant();

		switch (head)
		{
			case "help":
			{
				_console.Input($"> {cmd}");
				_console.Status("Commands:");
				_console.Status("  help    - list commands");
				_console.Status("  clear   - clear console output");
				_console.Status("  version - show current build id");
				return true;
			}
			case "clear":
			{
				// Preserve the echoed command line (requirement: echo input).
				_console.Input($"> {cmd}");
				_console.Clear(preserveTailLines: 1);
				_console.Status("console cleared");
				return true;
			}
			case "version":
			{
				_console.Input($"> {cmd}");
				var v = ReadVersionLine();
				_console.Status(v.Length == 0 ? "(version unknown)" : v);
				return true;
			}
			default:
				return false;
		}
	}

	private static string ReadVersionLine()
	{
		try
		{
			// VERSION.txt is in project root.
			var f = FileAccess.Open("res://VERSION.txt", FileAccess.ModeFlags.Read);
			if (f == null) return "";
			// Return the first non-empty line that starts with "Build:" if present.
			var content = f.GetAsText();
			f.Close();
			foreach (var raw in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
			{
				var line = raw.Trim();
				if (line.StartsWith("Build:", StringComparison.OrdinalIgnoreCase))
					return line;
			}
			return content.Trim();
		}
		catch
		{
			return "";
		}
	}

	private static string FormatLine(GameConsoleLine line)
	{
		var color = line.Kind switch
		{
			GameConsoleLineKind.Debug => "#66A3FF",
			GameConsoleLineKind.Status => "#FFFFFF",
			GameConsoleLineKind.Input => "#FFD24D",
			GameConsoleLineKind.Error => "#FF5555",
			_ => "#FFFFFF",
		};
		var safe = EscapeBbcode(line.Text);
		return $"[color={color}]{safe}[/color]";
	}

	private static string EscapeBbcode(string s)
	{
		var t = s ?? "";
		// Be tolerant of older-style escaping that used backslashes (\[ ... \]).
		t = t.Replace("\\[", "[").Replace("\\]", "]");
		return t.Replace("[", "[lb]").Replace("]", "[rb]");
	}

	private static Color ToColor(GameConsoleLineKind kind)
		=> kind switch
		{
			GameConsoleLineKind.Debug => new Color(0.4f, 0.64f, 1f),
			GameConsoleLineKind.Status => new Color(1f, 1f, 1f),
			GameConsoleLineKind.Input => new Color(1f, 0.84f, 0.2f),
			GameConsoleLineKind.Error => new Color(1f, 0.35f, 0.35f),
			_ => new Color(1f, 1f, 1f),
		};
}