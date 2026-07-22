// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/GameUiTheme.cs
// Purpose: UI view/controller code for scenes under Scenes/UI.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;
using WastelandSurvivor.Core.IO;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Centralized UI theme for "game-like" menus/containers.
/// Palette is loaded from: res://Data/Config/ui_theme.json
///
/// This is intentionally runtime-built (no .tres dependency) to keep zip iterations resilient.
/// </summary>
public static class GameUiTheme
{
	private const string ThemeConfigPath = "res://Data/Config/ui_theme.json";

	private sealed class UiThemeConfig
	{
		public string Background { get; set; } = "#0b0d10";
		public string Panel { get; set; } = "#11161d";
		public string PanelAlt { get; set; } = "#0f131a";
		public float PanelAlpha { get; set; } = 0.92f;

		public string AccentGold { get; set; } = "#d9b740";
		public string AccentCyan { get; set; } = "#35d0ff";

		public string Text { get; set; } = "#e8edf2";
		public string TextMuted { get; set; } = "#9aa7b3";
		public string Danger { get; set; } = "#ff4d4d";
		public string Success { get; set; } = "#3ddc97";

		public int BaseFontSize { get; set; } = 16;
		public int TitleFontSize { get; set; } = 22;
	}

	private static readonly UiThemeStore Store = new();
	private static Theme? _theme;

	// Bundled OFL fonts (Resources/UI/Fonts). Body = Inter (variable), Display = Rajdhani.
	// Loaded lazily with null fallback so the UI still works if the files are missing.
	private const string BodyFontPath = "res://Resources/UI/Fonts/Inter-Variable.ttf";
	private const string DisplayFontPath = "res://Resources/UI/Fonts/Rajdhani-SemiBold.ttf";
	private const string DisplayBoldFontPath = "res://Resources/UI/Fonts/Rajdhani-Bold.ttf";

	private static bool _fontsLoaded;
	private static Font? _bodyFont;
	private static Font? _displayFont;
	private static Font? _displayBoldFont;

	public static Font? BodyFont { get { EnsureFonts(); return _bodyFont; } }
	public static Font? DisplayFont { get { EnsureFonts(); return _displayFont; } }
	public static Font? DisplayBoldFont { get { EnsureFonts(); return _displayBoldFont; } }

	private static void EnsureFonts()
	{
		if (_fontsLoaded) return;
		_fontsLoaded = true;
		_bodyFont = TryLoadFont(BodyFontPath);
		_displayFont = TryLoadFont(DisplayFontPath);
		_displayBoldFont = TryLoadFont(DisplayBoldFontPath);
		if (_bodyFont == null || _displayFont == null)
			GD.Print("[GameUiTheme] Bundled fonts missing; falling back to engine default font.");
	}

	private static Font? TryLoadFont(string path)
	{
		try
		{
			if (!ResourceLoader.Exists(path)) return null;
			return GD.Load<Font>(path);
		}
		catch
		{
			return null;
		}
	}

	/// <summary>Display-font header treatment (Rajdhani Bold, uppercase, tracking via text).</summary>
	public static void StyleHeading(Label label, int fontSize, Color? color = null)
	{
		if (label == null) return;
		EnsureFonts();
		if (_displayBoldFont != null)
			label.AddThemeFontOverride("font", _displayBoldFont);
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color ?? TextColor);
	}

	public static Color BackgroundColor => LoadCfg().Background.ToColor(Colors.Black);
	public static Color PanelColor => LoadCfg().Panel.ToColor(new Color(0.07f, 0.07f, 0.09f));
	public static Color PanelAltColor => LoadCfg().PanelAlt.ToColor(new Color(0.06f, 0.06f, 0.08f));
	public static Color AccentGoldColor => LoadCfg().AccentGold.ToColor(new Color(0.85f, 0.72f, 0.25f));
	public static Color AccentCyanColor => LoadCfg().AccentCyan.ToColor(new Color(0.25f, 0.85f, 1f));
	public static Color TextColor => LoadCfg().Text.ToColor(new Color(0.92f, 0.93f, 0.95f));
	public static Color TextMutedColor => LoadCfg().TextMuted.ToColor(new Color(0.65f, 0.69f, 0.74f));
	public static Color DangerColor => LoadCfg().Danger.ToColor(new Color(1f, 0.3f, 0.3f));
	public static Color SuccessColor => LoadCfg().Success.ToColor(new Color(0.24f, 0.86f, 0.6f));

	public static int BaseFontSize => Math.Max(10, LoadCfg().BaseFontSize);
	public static int TitleFontSize => Math.Max(BaseFontSize + 2, LoadCfg().TitleFontSize);

	public static Theme GetTheme()
	{
		if (_theme != null) return _theme;
		_theme = BuildTheme();
		return _theme;
	}

	public static void ApplyTo(Control root)
	{
		if (root == null) return;
		root.Theme = GetTheme();
	}

	/// <summary>
	/// Apply theme to all Controls in a subtree. Safe to call repeatedly.
	/// </summary>
	public static void ApplyToTree(Node root)
	{
		if (root == null) return;
		if (root is Control c) c.Theme = GetTheme();

		foreach (var childObj in root.GetChildren())
		{
			if (childObj is Node child)
				ApplyToTree(child);
		}
	}

	public static void Reload()
	{
		Store.Reload();
		_theme = null;
	}

	private static UiThemeConfig LoadCfg() => Store.Get();

	private static Theme BuildTheme()
	{
		var cfg = LoadCfg();
		var t = new Theme();
		EnsureFonts();

		// --- Fonts ---
		// Body text uses Inter; interactive/display chrome uses Rajdhani (squared tech display face).
		if (_bodyFont != null)
		{
			t.DefaultFont = _bodyFont;
			t.DefaultFontSize = BaseFontSize;
		}
		if (_displayFont != null)
		{
			t.SetFont("font", "Button", _displayFont);
			t.SetFont("font", "OptionButton", _displayFont);
			t.SetFont("font", "MenuButton", _displayFont);
			t.SetFont("font", "TabBar", _displayFont);
			t.SetFont("font", "CheckButton", _displayFont);
		}

		// --- Base colors ---
		t.SetColor("font_color", "Label", TextColor);
		t.SetColor("font_color", "Button", TextColor);
		t.SetColor("font_color_disabled", "Button", TextMutedColor);
		t.SetColor("font_color_hover", "Button", Colors.White);
		t.SetColor("font_color_pressed", "Button", Colors.Black);
		t.SetColor("font_color_focus", "Button", Colors.White);

		// ItemList
		t.SetColor("font_color", "ItemList", TextColor);
		t.SetColor("font_color_selected", "ItemList", Colors.Black);

		// LineEdit
		t.SetColor("font_color", "LineEdit", TextColor);
		t.SetColor("font_color_uneditable", "LineEdit", TextMutedColor);
		t.SetColor("caret_color", "LineEdit", AccentCyanColor);

		// TabBar
		t.SetColor("font_selected_color", "TabBar", AccentGoldColor);
		t.SetColor("font_unselected_color", "TabBar", TextMutedColor);
		t.SetColor("font_hovered_color", "TabBar", TextColor);

		// --- Font sizes ---
		t.SetFontSize("font_size", "Label", BaseFontSize);
		t.SetFontSize("font_size", "Button", BaseFontSize + 2);
		t.SetFontSize("font_size", "ItemList", BaseFontSize);
		t.SetFontSize("font_size", "LineEdit", BaseFontSize);
		t.SetFontSize("font_size", "TabBar", BaseFontSize + 2);

		// --- Panel containers ---
		// Flat, dark, near-opaque command-console panels. The old frosted-glass + bright cyan border
		// on every panel read as dev tooling; color accents are now reserved for states/CTAs.
		var panel = new StyleBoxFlat
		{
			BgColor = WithAlpha(PanelColor, 0.94f),
			BorderColor = new Color(1f, 1f, 1f, 0.07f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerRadiusBottomRight = 10,
			ShadowColor = new Color(0, 0, 0, 0.50f),
			ShadowSize = 16,
			ContentMarginLeft = 16,
			ContentMarginRight = 16,
			ContentMarginTop = 14,
			ContentMarginBottom = 14
		};
		t.SetStylebox("panel", "PanelContainer", panel);
		t.SetStylebox("panel", "Panel", panel);

		// --- Buttons ---
		var btn = new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, 0.05f),
			BorderColor = new Color(1f, 1f, 1f, 0.14f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginLeft = 14,
			ContentMarginRight = 14,
			ContentMarginTop = 9,
			ContentMarginBottom = 9
		};
		var btnHover = (StyleBoxFlat)btn.Duplicate();
		btnHover.BgColor = WithAlpha(AccentCyanColor, 0.14f);
		btnHover.BorderColor = WithAlpha(AccentCyanColor, 0.85f);

		var btnPressed = (StyleBoxFlat)btn.Duplicate();
		btnPressed.BgColor = WithAlpha(AccentGoldColor, 0.92f);
		btnPressed.BorderColor = WithAlpha(AccentGoldColor, 1.0f);

		var btnDisabled = (StyleBoxFlat)btn.Duplicate();
		btnDisabled.BgColor = new Color(1f, 1f, 1f, 0.02f);
		btnDisabled.BorderColor = new Color(1f, 1f, 1f, 0.06f);

		var btnFocus = (StyleBoxFlat)btn.Duplicate();
		btnFocus.BorderColor = WithAlpha(AccentCyanColor, 0.95f);
		btnFocus.BgColor = WithAlpha(AccentCyanColor, 0.08f);

		t.SetStylebox("normal", "Button", btn);
		t.SetStylebox("hover", "Button", btnHover);
		t.SetStylebox("pressed", "Button", btnPressed);
		t.SetStylebox("disabled", "Button", btnDisabled);
		t.SetStylebox("focus", "Button", btnFocus);

		// OptionButton / MenuButton should look like normal buttons.
		t.SetStylebox("normal", "OptionButton", btn);
		t.SetStylebox("hover", "OptionButton", btnHover);
		t.SetStylebox("pressed", "OptionButton", btnPressed);
		t.SetStylebox("disabled", "OptionButton", btnDisabled);
		t.SetStylebox("focus", "OptionButton", btnFocus);
		t.SetStylebox("normal", "MenuButton", btn);
		t.SetStylebox("hover", "MenuButton", btnHover);
		t.SetStylebox("pressed", "MenuButton", btnPressed);
		t.SetStylebox("disabled", "MenuButton", btnDisabled);

		// --- ItemList ---
		var listBg = new StyleBoxFlat
		{
			BgColor = WithAlpha(PanelAltColor, 0.70f),
			BorderColor = WithAlpha(AccentGoldColor, 0.35f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ContentMarginLeft = 8,
			ContentMarginRight = 8,
			ContentMarginTop = 6,
			ContentMarginBottom = 6,
		};
		t.SetStylebox("panel", "ItemList", listBg);

		// Selected item style (drawn behind selected rows).
		var listSel = new StyleBoxFlat
		{
			BgColor = WithAlpha(AccentGoldColor, 0.75f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
		};
		t.SetStylebox("selected", "ItemList", listSel);

		// --- LineEdit ---
		var edit = new StyleBoxFlat
		{
			BgColor = WithAlpha(PanelAltColor, 0.85f),
			BorderColor = WithAlpha(AccentCyanColor, 0.55f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginLeft = 8,
			ContentMarginRight = 8,
			ContentMarginTop = 4,
			ContentMarginBottom = 4
		};
		var editFocus = (StyleBoxFlat)edit.Duplicate();
		editFocus.BorderColor = WithAlpha(AccentCyanColor, 0.90f);

		t.SetStylebox("normal", "LineEdit", edit);
		t.SetStylebox("focus", "LineEdit", editFocus);
		t.SetStylebox("read_only", "LineEdit", edit);

		// --- ProgressBar ---
		var pbBg = new StyleBoxFlat
		{
			BgColor = WithAlpha(PanelAltColor, 0.55f),
			BorderColor = WithAlpha(AccentGoldColor, 0.30f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
		};
		var pbFill = new StyleBoxFlat
		{
			BgColor = WithAlpha(AccentCyanColor, 0.80f),
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
		};
		t.SetStylebox("background", "ProgressBar", pbBg);
		t.SetStylebox("fill", "ProgressBar", pbFill);

		// --- HSlider / VSlider ---
		var sliderTrack = new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, 0.08f),
			CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
			ContentMarginTop = 3, ContentMarginBottom = 3,
		};
		var sliderFill = new StyleBoxFlat
		{
			BgColor = WithAlpha(AccentCyanColor, 0.75f),
			CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
		};
		foreach (var slider in new[] { "HSlider", "VSlider" })
		{
			t.SetStylebox("slider", slider, sliderTrack);
			t.SetStylebox("grabber_area", slider, sliderFill);
			t.SetStylebox("grabber_area_highlight", slider, sliderFill);
		}

		// --- ScrollBars (every ScrollContainer in the game) ---
		var scrollTrack = new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, 0.03f),
			CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
		};
		var scrollGrabber = new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, 0.16f),
			CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
			ContentMarginLeft = 2, ContentMarginRight = 2,
			ContentMarginTop = 2, ContentMarginBottom = 2,
		};
		var scrollGrabberHover = (StyleBoxFlat)scrollGrabber.Duplicate();
		scrollGrabberHover.BgColor = WithAlpha(AccentCyanColor, 0.45f);
		foreach (var bar in new[] { "VScrollBar", "HScrollBar" })
		{
			t.SetStylebox("scroll", bar, scrollTrack);
			t.SetStylebox("grabber", bar, scrollGrabber);
			t.SetStylebox("grabber_highlight", bar, scrollGrabberHover);
			t.SetStylebox("grabber_pressed", bar, scrollGrabberHover);
		}

		// --- Tabs (raw TabContainer/TabBar baseline) ---
		var tabUnselected = new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, 0.03f),
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
			ContentMarginLeft = 14, ContentMarginRight = 14,
			ContentMarginTop = 6, ContentMarginBottom = 6,
		};
		var tabSelected = new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, 0.07f),
			BorderColor = WithAlpha(AccentGoldColor, 0.95f),
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
			ContentMarginLeft = 14, ContentMarginRight = 14,
			ContentMarginTop = 6, ContentMarginBottom = 6,
		};
		var tabHovered = (StyleBoxFlat)tabUnselected.Duplicate();
		tabHovered.BgColor = new Color(1f, 1f, 1f, 0.06f);
		t.SetStylebox("tab_unselected", "TabBar", tabUnselected);
		t.SetStylebox("tab_selected", "TabBar", tabSelected);
		t.SetStylebox("tab_hovered", "TabBar", tabHovered);
		t.SetStylebox("tab_unselected", "TabContainer", tabUnselected);
		t.SetStylebox("tab_selected", "TabContainer", tabSelected);
		t.SetStylebox("tab_hovered", "TabContainer", tabHovered);
		var tabPanel = new StyleBoxFlat
		{
			BgColor = new Color(0f, 0f, 0f, 0f),
			ContentMarginTop = 8,
		};
		t.SetStylebox("panel", "TabContainer", tabPanel);

		return t;
	}

	private static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, Mathf.Clamp(a, 0f, 1f));

	
	private sealed class UiThemeStore : JsonConfigStore<UiThemeConfig>
	{
		public UiThemeStore() : base(ThemeConfigPath)
		{
		}

		protected override UiThemeConfig CreateDefault() => new();
	}

	private static Color ToColor(this string html, Color fallback)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(html)) return fallback;
			var s = html.Trim();
			if (s.StartsWith("#")) s = s.Substring(1);
			if (s.Length == 6)
			{
				var r = Convert.ToInt32(s.Substring(0, 2), 16) / 255f;
				var g = Convert.ToInt32(s.Substring(2, 2), 16) / 255f;
				var b = Convert.ToInt32(s.Substring(4, 2), 16) / 255f;
				return new Color(r, g, b, 1f);
			}
			if (s.Length == 8)
			{
				var r = Convert.ToInt32(s.Substring(0, 2), 16) / 255f;
				var g = Convert.ToInt32(s.Substring(2, 2), 16) / 255f;
				var b = Convert.ToInt32(s.Substring(4, 2), 16) / 255f;
				var a = Convert.ToInt32(s.Substring(6, 2), 16) / 255f;
				return new Color(r, g, b, a);
			}
			return fallback;
		}
		catch
		{
			return fallback;
		}
	}

}