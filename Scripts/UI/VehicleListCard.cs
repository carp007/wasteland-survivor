using System.Collections.Generic;
using Godot;

namespace WastelandSurvivor.Game.UI;

public partial class VehicleListCard : Button
{
    private readonly TextureRect _preview;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Dictionary<string, Label> _metricLabels = new();
    private bool _selected;
    private bool _active;

    public string InstanceId { get; private set; } = string.Empty;

    public VehicleListCard()
    {
        Flat = false;
        ToggleMode = false;
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(0f, 78f);
        Alignment = HorizontalAlignment.Left;
        ClipText = false;
        Text = string.Empty;
        AddThemeStyleboxOverride("normal", GeneratedUiArt.CreateGarageInsetStyle());
        AddThemeStyleboxOverride("hover", GeneratedUiArt.CreateGarageInsetStyle(selected: true));
        AddThemeStyleboxOverride("pressed", GeneratedUiArt.CreateGarageInsetStyle(selected: true));
        AddThemeStyleboxOverride("disabled", GeneratedUiArt.CreateGarageInsetStyle());

        var margin = new MarginContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        AddChild(margin);

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", 10);
        margin.AddChild(row);

        var previewShell = new PanelContainer
        {
            CustomMinimumSize = new Vector2(72f, 56f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        previewShell.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreateGarageInsetStyle());
        row.AddChild(previewShell);

        _preview = new TextureRect
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        previewShell.AddChild(_preview);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        content.AddThemeConstantOverride("separation", 4);
        row.AddChild(content);

        var topRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        topRow.AddThemeConstantOverride("separation", 8);
        content.AddChild(topRow);

        _title = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        GameUiTheme.StyleHeading(_title, GameUiTheme.BaseFontSize + 3);
        topRow.AddChild(_title);

        var metrics = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        metrics.AddThemeConstantOverride("separation", 6);
        topRow.AddChild(metrics);

        AddMetric(metrics, "condition", "icon_armor");
        AddMetric(metrics, "weapons", "icon_weapon_front");
        AddMetric(metrics, "cargo", "icon_vehicle_card");
        AddMetric(metrics, "tow", "icon_repair");

        _subtitle = new Label
        {
            Text = string.Empty,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _subtitle.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize + 1);
        _subtitle.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
        content.AddChild(_subtitle);

        UpdateVisualState();
    }

    public void Configure(
        string instanceId,
        string title,
        string statusText,
        int conditionPercent,
        int weaponCount,
        int cargoStacks,
        int towCount,
        bool isActive,
        bool selected,
        Texture2D? previewTexture)
    {
        InstanceId = instanceId ?? string.Empty;
        _title.Text = title;
        _subtitle.Text = statusText;
        _metricLabels["condition"].Text = $"{conditionPercent}%";
        _metricLabels["weapons"].Text = weaponCount.ToString();
        _metricLabels["cargo"].Text = cargoStacks.ToString();
        _metricLabels["tow"].Text = towCount.ToString();
        _active = isActive;
        _selected = selected;
        SetPreviewTexture(previewTexture);
        UpdateVisualState();
    }

    public void SetSelectedState(bool selected)
    {
        if (_selected == selected)
            return;

        _selected = selected;
        UpdateVisualState();
    }

    public void SetPreviewTexture(Texture2D? texture)
    {
        _preview.Texture = texture;
    }

    private void AddMetric(HBoxContainer parent, string key, string iconAsset)
    {
        var badge = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
        };
        badge.AddThemeConstantOverride("separation", 4);
        parent.AddChild(badge);

        var icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(16f, 16f),
            Texture = GeneratedUiArt.LoadSized(iconAsset, 16),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        badge.AddChild(icon);

        var label = new Label
        {
            Text = "0",
            MouseFilter = MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize);
        badge.AddChild(label);

        _metricLabels[key] = label;
    }

    private void UpdateVisualState()
    {
        var normal = GeneratedUiArt.CreateGarageInsetStyle(_selected, _active);
        var hover = GeneratedUiArt.CreateGarageInsetStyle(true, _active);
        AddThemeStyleboxOverride("normal", normal);
        AddThemeStyleboxOverride("hover", hover);
        AddThemeStyleboxOverride("pressed", hover);
        AddThemeStyleboxOverride("disabled", normal);

        _title.AddThemeColorOverride("font_color", _selected ? new Color(1f, 0.95f, 0.84f, 1f) : GameUiTheme.TextColor);
        _subtitle.AddThemeColorOverride("font_color", _selected ? new Color(0.96f, 0.88f, 0.68f, 0.96f) : GameUiTheme.TextMutedColor);
        foreach (var metric in _metricLabels.Values)
            metric.AddThemeColorOverride("font_color", _selected ? new Color(1f, 0.95f, 0.84f, 1f) : GameUiTheme.TextColor);
    }
}
