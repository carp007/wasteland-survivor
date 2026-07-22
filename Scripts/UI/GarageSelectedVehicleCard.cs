using System.Collections.Generic;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game.Systems;

namespace WastelandSurvivor.Game.UI;

public partial class GarageSelectedVehicleCard : PanelContainer
{
    private readonly Label _kicker;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _detail;
    private readonly TextureRect _heroArt;
    private readonly VehiclePreviewCanvas _heroPreview;
    private readonly Dictionary<string, Label> _statValues = new();

    public GarageSelectedVehicleCard()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeStyleboxOverride("panel", GeneratedUiArt.CreateGarageSectionStyle(emphasized: true));

        var margin = new MarginContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        AddChild(margin);

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", 12);
        margin.AddChild(row);

        var infoColumn = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(210f, 0f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        infoColumn.AddThemeConstantOverride("separation", 5);
        row.AddChild(infoColumn);

        _kicker = new Label
        {
            Text = "Selected Vehicle",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _kicker.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize + 1);
        _kicker.AddThemeColorOverride("font_color", GameUiTheme.AccentGoldColor);
        infoColumn.AddChild(_kicker);

        _title = new Label
        {
            Text = "No Vehicle Selected",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _title.AddThemeFontSizeOverride("font_size", GameUiTheme.TitleFontSize + 2);
        infoColumn.AddChild(_title);

        _subtitle = new Label
        {
            Text = "Choose a vehicle from the fleet roster to inspect it.",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _subtitle.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
        infoColumn.AddChild(_subtitle);

        var statGrid = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        statGrid.AddThemeConstantOverride("h_separation", 10);
        statGrid.AddThemeConstantOverride("v_separation", 10);
        infoColumn.AddChild(statGrid);

        AddStatTile(statGrid, "condition", "Condition", "icon_armor");
        AddStatTile(statGrid, "mass", "Mass", "icon_engine");
        AddStatTile(statGrid, "storage", "Storage", "icon_vehicle_card");
        AddStatTile(statGrid, "weapons", "Weapons", "icon_weapon_front");
        AddStatTile(statGrid, "repairs", "Repairs", "icon_repair");
        AddStatTile(statGrid, "fuel", "Fuel", "icon_engine");

        _detail = new Label
        {
            Text = string.Empty,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _detail.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
        _detail.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 1);
        infoColumn.AddChild(_detail);

        var artColumn = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(224f, 0f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        artColumn.AddThemeConstantOverride("separation", 10);
        row.AddChild(artColumn);

        // Single preview shell. PanelContainer lays out every child to fill the panel, so the art
        // TextureRect and the fallback silhouette canvas overlay each other correctly. (The old
        // version nested them in a plain Control, which does no container layout — the children
        // stayed at 0x0 and the gold-bordered panel rendered completely blank.)
        var heroShell = new PanelContainer
        {
            ClipContents = true,
            CustomMinimumSize = new Vector2(0f, 138f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        heroShell.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreateGarageSectionStyle(gold: true));
        artColumn.AddChild(heroShell);

        _heroArt = new TextureRect
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        heroShell.AddChild(_heroArt);

        _heroPreview = new VehiclePreviewCanvas
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 138f),
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        heroShell.AddChild(_heroPreview);

        // Live 3D turntable portrait — the flat vector car clashed with the photographic garage
        // backdrop (eval round 6). Layers over the canvas; the canvas stays as the fallback.
        _heroPortrait = new VehiclePortraitViewport
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        heroShell.AddChild(_heroPortrait);

        Clear();
    }

    private readonly VehiclePortraitViewport _heroPortrait;

    public void Clear()
    {
        _title.Text = "No Vehicle Selected";
        _subtitle.Text = "Choose a vehicle from the fleet roster to inspect it.";
        _detail.Text = string.Empty;
        foreach (var value in _statValues.Values)
            value.Text = "—";

        _heroArt.Texture = null;
        _heroArt.Visible = false;
        _heroPreview.Visible = true;
        if (_heroPortrait != null && GodotObject.IsInstanceValid(_heroPortrait))
        {
            _heroPortrait.ClearPortrait();
            _heroPortrait.Visible = false;
        }

        var fallbackVehicle = CreateFallbackVehicle();
        var fallbackDef = CreateFallbackDefinition();
        _heroPreview.SetVehicle(fallbackDef, fallbackVehicle, VehiclePresentation.PlayerBodyColor);
        _heroPreview.SetLiveYaw(0f);
    }

    public void Configure(VehicleInstanceState vehicle, VehicleDefinition definition, DefDatabase defs, string detailText)
    {
        var displayName = VehiclePresentation.GetDisplayName(vehicle, definition);
        var chassisName = VehiclePresentation.GetChassisName(vehicle, definition);
        var condition = VehicleRecoveryValueMath.ComputeConditionPercent(definition, vehicle);
        var massKg = Mathf.RoundToInt(VehicleMassMath.ComputeTotalMassKg(definition, vehicle, defs));
        var cargoStacks = VehiclePresentation.CountCargoStacks(vehicle);
        var weaponCount = VehiclePresentation.CountInstalledWeapons(vehicle);
        var repairPoints = VehicleRepairMath.ComputeMissingRepairPoints(vehicle, definition);
        var garageColor = VehiclePresentation.GetGaragePreviewColor(vehicle);

        _title.Text = displayName;
        _subtitle.Text = string.Equals(displayName, chassisName, System.StringComparison.Ordinal)
            ? $"{definition.Class} chassis · {definition.StorageCapacityUnits} cargo slots"
            : $"{chassisName} · {definition.Class} chassis";
        _detail.Text = $"Armor plating L{vehicle.ArmorPlatingLevel} · Tire plating L{vehicle.TirePlatingLevel} · {detailText}";

        _statValues["condition"].Text = $"{condition}%";
        _statValues["mass"].Text = $"{massKg} kg";
        _statValues["storage"].Text = $"{cargoStacks} / {definition.StorageCapacityUnits}";
        _statValues["weapons"].Text = weaponCount.ToString();
        _statValues["repairs"].Text = repairPoints > 0 ? repairPoints.ToString() : "Ready";
        _statValues["fuel"].Text = definition.FuelCapacityUnits > 0f
            ? $"{Mathf.Clamp(vehicle.FuelAmount, 0f, definition.FuelCapacityUnits):0} / {definition.FuelCapacityUnits:0}"
            : "—";

        // Live 3D turntable portrait of the ACTUAL chassis (real model, hull detail, team color).
        // The vector canvas stays underneath as the fallback if the portrait fails to build.
        _heroArt.Texture = null;
        _heroArt.Visible = false;
        _heroPreview.Visible = true;
        _heroPreview.SetVehicle(definition, vehicle, garageColor);
        _heroPreview.SetLiveYaw(0f);
        if (_heroPortrait != null && GodotObject.IsInstanceValid(_heroPortrait))
        {
            _heroPortrait.ShowVehicle(defs, definition, vehicle, garageColor);
            var portraitLive = _heroPortrait.HasPortrait;
            _heroPortrait.Visible = portraitLive;
            _heroPreview.Visible = !portraitLive;
        }
    }

    private void AddStatTile(Container parent, string key, string caption, string iconAsset)
    {
        var shell = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        shell.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreateGarageMetricTileStyle());
        parent.AddChild(shell);

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", 10);
        shell.AddChild(row);

        var icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(28f, 28f),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = GeneratedUiArt.LoadSized(iconAsset, 28),
        };
        row.AddChild(icon);

        var textColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddChild(textColumn);

        var value = new Label
        {
            Text = "—",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        value.AddThemeFontSizeOverride("font_size", GameUiTheme.TitleFontSize - 3);
        textColumn.AddChild(value);

        var label = new Label
        {
            Text = caption,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 3);
        label.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
        textColumn.AddChild(label);

        _statValues[key] = value;
    }

    private static VehicleInstanceState CreateFallbackVehicle()
    {
        return new VehicleInstanceState
        {
            DefinitionId = "veh_compact",
            CurrentArmorBySection = new(),
            CurrentHpBySection = new(),
            // Healthy placeholder — zero-HP tires drew destroyed-tire X marks on the empty state.
            CurrentTireArmor = new[] { 5, 5, 5, 5 },
            CurrentTireHp = new[] { 10, 10, 10, 10 },
            InstalledWeaponsByMountId = new(),
            AmmoInventory = new(),
            CargoInventory = new(),
            Towing = new TowingState(),
        };
    }

    private static VehicleDefinition CreateFallbackDefinition()
    {
        return new VehicleDefinition
        {
            Id = "veh_compact",
            DisplayName = "Compact",
            Class = VehicleClass.Compact,
            BaseMassKg = 1200f,
            StorageCapacityUnits = 8,
            TireCount = 4,
            BaseArmorBySection = new(),
            BaseHpBySection = new(),
            BaseTireArmor = 5,
            BaseTireHp = 10,
            MountPoints = new(),
        };
    }
}
