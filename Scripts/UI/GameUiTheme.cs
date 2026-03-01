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
	private const string ConfigPath = "res://Data/Config/ui_theme.json";

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

		public int BaseFontSize { get; set; } = 14;
		public int TitleFontSize { get; set; } = 18;
	}

	private static UiThemeConfig? _cfg;
	private static Theme? _theme;

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

	private static UiThemeConfig LoadCfg()
	{
		if (_cfg != null) return _cfg;

		try
		{
			if (!ResourceLoader.Exists(ConfigPath))
				return _cfg = new UiThemeConfig();

			var json = FileAccess.GetFileAsString(ConfigPath);
			var parsed = JsonUtil.Deserialize<UiThemeConfig>(json);
			_cfg = parsed ?? new UiThemeConfig();
			return _cfg;
		}
		catch
		{
			_cfg = new UiThemeConfig();
			return _cfg;
		}
	}

	private static Theme BuildTheme()
	{
		var cfg = LoadCfg();
		var t = new Theme();

		// --- Base colors ---
		t.SetColor("font_color", "Label", TextColor);
		t.SetColor("font_color", "Button", TextColor);
		t.SetColor("font_color_disabled", "Button", TextMutedColor);
		t.SetColor("font_color_hover", "Button", TextColor);
		t.SetColor("font_color_pressed", "Button", TextColor);
		t.SetColor("font_color_focus", "Button", TextColor);

		// ItemList
		t.SetColor("font_color", "ItemList", TextColor);
		t.SetColor("font_color_selected", "ItemList", Colors.Black);

		// LineEdit
		t.SetColor("font_color", "LineEdit", TextColor);
		t.SetColor("font_color_uneditable", "LineEdit", TextMutedColor);
		t.SetColor("caret_color", "LineEdit", AccentCyanColor);

		// --- Font sizes ---
		t.SetFontSize("font_size", "Label", BaseFontSize);
		t.SetFontSize("font_size", "Button", BaseFontSize);
		t.SetFontSize("font_size", "ItemList", BaseFontSize);
		t.SetFontSize("font_size", "LineEdit", BaseFontSize);

		// --- Panel containers ---
		var panel = new StyleBoxFlat
		{
			BgColor = WithAlpha(PanelColor, cfg.PanelAlpha),
			BorderColor = WithAlpha(AccentGoldColor, 0.55f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerRadiusBottomRight = 10,
			ShadowColor = new Color(0, 0, 0, 0.55f),
			ShadowSize = 8,
			ContentMarginLeft = 10,
			ContentMarginRight = 10,
			ContentMarginTop = 8,
			ContentMarginBottom = 8
		};
		t.SetStylebox("panel", "PanelContainer", panel);
		t.SetStylebox("panel", "Panel", panel);

		// --- Buttons ---
		var btn = new StyleBoxFlat
		{
			BgColor = WithAlpha(PanelAltColor, 0.88f),
			BorderColor = WithAlpha(AccentCyanColor, 0.55f),
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
			ContentMarginTop = 6,
			ContentMarginBottom = 6
		};
		var btnHover = (StyleBoxFlat)btn.Duplicate();
		btnHover.BgColor = WithAlpha(PanelAltColor.Lightened(0.08f), 0.92f);
		btnHover.BorderColor = WithAlpha(AccentCyanColor, 0.85f);

		var btnPressed = (StyleBoxFlat)btn.Duplicate();
		btnPressed.BgColor = WithAlpha(AccentGoldColor, 0.35f);
		btnPressed.BorderColor = WithAlpha(AccentGoldColor, 0.95f);

		t.SetStylebox("normal", "Button", btn);
		t.SetStylebox("hover", "Button", btnHover);
		t.SetStylebox("pressed", "Button", btnPressed);
		t.SetStylebox("disabled", "Button", btn);

		// OptionButton / MenuButton should look like normal buttons.
		t.SetStylebox("normal", "OptionButton", btn);
		t.SetStylebox("hover", "OptionButton", btnHover);
		t.SetStylebox("pressed", "OptionButton", btnPressed);
		t.SetStylebox("disabled", "OptionButton", btn);
		t.SetStylebox("normal", "MenuButton", btn);
		t.SetStylebox("hover", "MenuButton", btnHover);
		t.SetStylebox("pressed", "MenuButton", btnPressed);
		t.SetStylebox("disabled", "MenuButton", btn);

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

		return t;
	}

	private static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, Mathf.Clamp(a, 0f, 1f));

	
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