using System.Collections.Generic;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game.Systems;

namespace WastelandSurvivor.Game.UI;

public partial class VehicleShowcaseCard : PanelContainer
{
    private readonly Label _kicker;
    private readonly VehiclePreviewCanvas _preview;
    private readonly VehiclePortraitViewport _portrait;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _detail;
    private readonly Dictionary<string, Label> _statValues = new();

    public VehicleShowcaseCard()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(0f, 154f);
        AddThemeStyleboxOverride("panel", GeneratedUiArt.CreateCardStyle());

        var margin = new MarginContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
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

        // Landscape footprint matched to the portrait viewport's aspect (460x300) and centered
        // vertically instead of stretched: the old tall 156-wide shell letterboxed the turntable
        // render down to a thin band, so the car read at ~20% of the box (round 10 P2-10).
        var previewShell = new PanelContainer
        {
            CustomMinimumSize = new Vector2(218f, 146f),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        previewShell.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreateCardStyle());
        row.AddChild(previewShell);

        _preview = new VehiclePreviewCanvas
        {
            CustomMinimumSize = new Vector2(148f, 118f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        previewShell.AddChild(_preview);

        // Live 3D turntable portrait over the vector canvas (canvas stays as fallback) — the
        // same showroom treatment as the garage hero card.
        _portrait = new VehiclePortraitViewport
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        previewShell.AddChild(_portrait);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        content.AddThemeConstantOverride("separation", 6);
        row.AddChild(content);

        _kicker = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Text = "Vehicle",
        };
        _kicker.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 1);
        _kicker.AddThemeColorOverride("font_color", GameUiTheme.AccentGoldColor);
        content.AddChild(_kicker);

        _title = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _title.AddThemeFontSizeOverride("font_size", GameUiTheme.TitleFontSize - 6);
        content.AddChild(_title);

        _subtitle = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _subtitle.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
        _subtitle.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 1);
        content.AddChild(_subtitle);

        var stats = new GridContainer
        {
            Columns = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        stats.AddThemeConstantOverride("h_separation", 8);
        stats.AddThemeConstantOverride("v_separation", 8);
        content.AddChild(stats);

        AddStat(stats, "condition", "Condition");
        AddStat(stats, "mass", "Mass");
        AddStat(stats, "weapons", "Weapons");
        AddStat(stats, "storage", "Storage");
        AddStat(stats, "tow", "Towing");
        AddStat(stats, "repairs", "Repairs");

        _detail = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _detail.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
        _detail.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 2);
        content.AddChild(_detail);

        Clear("Vehicle", "Select a vehicle to inspect its build and readiness.");
    }

    /// <summary>
    /// Replace the procedural top-down silhouette with a baked snapshot. Pass null to fall back to
    /// the live <see cref="VehiclePreviewCanvas"/> drawing so the card stays useful when no snapshot
    /// is available yet (or while one is loading).
    /// </summary>
    public void SetSnapshotTexture(Texture2D? snapshot)
    {
        _preview.SetSnapshotTexture(snapshot);
    }

    public void Clear(string kicker, string body)
    {
        Visible = true;
        _kicker.Text = kicker;
        _title.Text = "No Vehicle Selected";
        _subtitle.Text = body;
        _detail.Text = "";
        foreach (var label in _statValues.Values)
            label.Text = "—";

        _preview.SetSnapshotTexture(null);
        _preview.SetEmbeddedPreviewActive(false);
        _preview.SetLiveYaw(0f);
        _preview.Visible = true;
        if (_portrait != null && GodotObject.IsInstanceValid(_portrait))
        {
            _portrait.ClearPortrait();
            _portrait.Visible = false;
        }
        // Healthy placeholder: zero-HP tires made the empty-state silhouette draw destroyed-tire
        // X marks, which read as a broken icon on the City Hub card (eval round 7 P1-5).
        var fallbackVehicle = new VehicleInstanceState
        {
            DefinitionId = "veh_compact",
            CurrentArmorBySection = new(),
            CurrentHpBySection = new(),
            CurrentTireArmor = new[] { 5, 5, 5, 5 },
            CurrentTireHp = new[] { 10, 10, 10, 10 },
            InstalledWeaponsByMountId = new(),
            AmmoInventory = new(),
            CargoInventory = new(),
            Towing = new TowingState(),
        };
        _preview.SetVehicle(new VehicleDefinition
        {
            Class = VehicleClass.Compact,
            DisplayName = "Compact",
            BaseMassKg = 1200f,
            StorageCapacityUnits = 10,
            TireCount = 4,
            BaseArmorBySection = new(),
            BaseHpBySection = new(),
            BaseTireArmor = 5,
            BaseTireHp = 10,
            MountPoints = new(),
        }, fallbackVehicle, VehiclePresentation.PlayerBodyColor);
    }

    public void Configure(string kicker, VehicleInstanceState vehicle, VehicleDefinition definition, DefDatabase defs, string detailText)
    {
        Visible = true;
        _kicker.Text = kicker;

        var displayName = VehiclePresentation.GetDisplayName(vehicle, definition);
        var chassisName = VehiclePresentation.GetChassisName(vehicle, definition);
        _title.Text = displayName;
        _subtitle.Text = string.Equals(displayName, chassisName, System.StringComparison.Ordinal)
            ? $"{definition.Class} chassis · {definition.StorageCapacityUnits} cargo slots"
            : $"{chassisName} · {definition.Class} chassis";

        var condition = VehicleRecoveryValueMath.ComputeConditionPercent(definition, vehicle);
        var massKg = Mathf.RoundToInt(VehicleMassMath.ComputeTotalMassKg(definition, vehicle, defs));
        var weapons = VehiclePresentation.CountInstalledWeapons(vehicle);
        var cargoStacks = VehiclePresentation.CountCargoStacks(vehicle);
        var storageText = $"{cargoStacks}/{definition.StorageCapacityUnits}";
        var towCount = vehicle.Towing?.AttachedTowTargetInstanceIds?.Count ?? 0;
        var towMass = Mathf.RoundToInt(vehicle.Towing?.TotalTowedMassKgCached ?? 0f);
        var repairPoints = VehicleRepairMath.ComputeMissingRepairPoints(vehicle, definition);

        _statValues["condition"].Text = $"{condition}%";
        _statValues["mass"].Text = $"{massKg} kg";
        _statValues["weapons"].Text = weapons.ToString();
        _statValues["storage"].Text = storageText;
        _statValues["tow"].Text = towCount > 0 ? $"{towCount} · {towMass} kg" : "None";
        _statValues["repairs"].Text = repairPoints > 0 ? repairPoints.ToString() : "Ready";

        _detail.Text = $"Armor plating L{vehicle.ArmorPlatingLevel} · Tire plating L{vehicle.TirePlatingLevel} · {detailText}";

        _preview.SetSnapshotTexture(null);
        _preview.SetEmbeddedPreviewActive(false);
        _preview.SetLiveYaw(0f);
        var previewColor = VehiclePresentation.GetGaragePreviewColor(vehicle);
        _preview.SetVehicle(definition, vehicle, previewColor);
        if (_portrait != null && GodotObject.IsInstanceValid(_portrait))
        {
            _portrait.ShowVehicle(defs, definition, vehicle, previewColor);
            var portraitLive = _portrait.HasPortrait;
            _portrait.Visible = portraitLive;
            _preview.Visible = !portraitLive;
        }
    }

    private void AddStat(GridContainer parent, string key, string caption)
    {
        var shell = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        shell.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreateCardStyle());
        parent.AddChild(shell);

        var box = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        box.AddThemeConstantOverride("separation", 1);
        shell.AddChild(box);

        var value = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Text = "—",
        };
        value.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize);
        value.AddThemeColorOverride("font_color", GameUiTheme.TextColor);
        box.AddChild(value);

        var label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Text = caption,
        };
        label.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 3);
        label.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
        box.AddChild(label);

        _statValues[key] = value;
    }
}
