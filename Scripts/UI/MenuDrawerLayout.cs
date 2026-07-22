// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/MenuDrawerLayout.cs
// Purpose: Applies a reliable left-edge drawer layout to premium management/menu shells.
//          This is owned by the scene scripts instead of relying on separate scene nodes so the
//          shell placement survives scene-tree refactors and is obvious on the user's machine.
// -------------------------------------------------------------------------------------------------
using Godot;

namespace WastelandSurvivor.Game.UI;

public readonly record struct MenuDrawerSpec(
	float PreferredWidth,
	float MinWidth,
	float MaxWidthPercent,
	float LeftMargin,
	float TopMargin,
	float RightReserve,
	float BottomMargin)
{
	public static MenuDrawerSpec City => new(920f, 620f, 0.48f, 24f, 84f, 24f, 24f);
	public static MenuDrawerSpec Garage => new(940f, 640f, 0.49f, 24f, 84f, 24f, 24f);
	public static MenuDrawerSpec Workshop => new(940f, 640f, 0.49f, 24f, 84f, 24f, 24f);
}

public static class MenuDrawerLayout
{
	public static void Apply(Control? panel, Node owner, MenuDrawerSpec spec)
	{
		if (panel == null || !GodotObject.IsInstanceValid(panel))
			return;

		var viewport = owner.GetViewport();
		var viewSize = viewport?.GetVisibleRect().Size ?? Vector2.Zero;
		if (viewSize.X <= 1f || viewSize.Y <= 1f)
			return;

		var availableWidth = Mathf.Max(320f, viewSize.X - spec.LeftMargin - spec.RightReserve);
		var availableHeight = Mathf.Max(320f, viewSize.Y - spec.TopMargin - spec.BottomMargin);
		var width = Mathf.Min(availableWidth, Mathf.Max(spec.MinWidth, Mathf.Min(spec.PreferredWidth, viewSize.X * spec.MaxWidthPercent)));
		var height = availableHeight;

		var x = spec.LeftMargin;
		var y = spec.TopMargin;

		panel.AnchorLeft = 0f;
		panel.AnchorTop = 0f;
		panel.AnchorRight = 0f;
		panel.AnchorBottom = 0f;
		panel.OffsetLeft = x;
		panel.OffsetTop = y;
		panel.OffsetRight = x + width;
		panel.OffsetBottom = y + height;
		panel.Position = new Vector2(x, y);
		panel.Size = new Vector2(width, height);
		panel.CustomMinimumSize = Vector2.Zero;
	}

	public static void StyleTabs(TabContainer? tabs)
	{
		if (tabs == null || !GodotObject.IsInstanceValid(tabs))
			return;

		// Gold-underline tab language (matches the global theme): quiet tabs, the selected one carries
		// a 2px gold bottom edge instead of a glowing chip.
		var unselected = new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, 0.03f),
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
			ContentMarginLeft = 14, ContentMarginRight = 14,
			ContentMarginTop = 6, ContentMarginBottom = 6,
		};
		var selected = new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, 0.07f),
			BorderColor = GameUiTheme.AccentGoldColor,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
			ContentMarginLeft = 14, ContentMarginRight = 14,
			ContentMarginTop = 6, ContentMarginBottom = 6,
		};
		var hovered = (StyleBoxFlat)unselected.Duplicate();
		hovered.BgColor = new Color(1f, 1f, 1f, 0.06f);

		tabs.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
		tabs.AddThemeStyleboxOverride("tab_selected", selected);
		tabs.AddThemeStyleboxOverride("tab_unselected", unselected);
		tabs.AddThemeStyleboxOverride("tab_hovered", hovered);
		tabs.AddThemeStyleboxOverride("tab_disabled", unselected);
		tabs.AddThemeColorOverride("font_selected_color", GameUiTheme.AccentGoldColor);
		tabs.AddThemeColorOverride("font_unselected_color", GameUiTheme.TextMutedColor);
		tabs.AddThemeColorOverride("font_hovered_color", GameUiTheme.TextColor);
		tabs.AddThemeColorOverride("font_disabled_color", GameUiTheme.TextMutedColor);
		if (GameUiTheme.DisplayFont is { } displayFont)
			tabs.AddThemeFontOverride("font", displayFont);
		tabs.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize + 1);
		tabs.AddThemeConstantOverride("h_separation", 6);
		tabs.AddThemeConstantOverride("top_margin", 8);
	}
}
