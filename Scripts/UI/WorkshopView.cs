using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game.UI;

public partial class WorkshopView : Control
{
    private Label? _lblActive;
    private OptionButton? _optEngine;
    private OptionButton? _optComputer;
    private VBoxContainer? _vboxMounts;
    private Button? _btnApply;
    private Button? _btnBack;

    private Label? _lblAmmo;
    private Button? _btnBuy10;
    private Button? _btnBuy50;
    private Button? _btnBuy200;

    private VehicleInstanceState? _vehicle;
    private VehicleDefinition? _vdef;

    // mountId -> (weaponOpt, ammoOpt)
    private readonly Dictionary<string, (OptionButton weapon, OptionButton ammo)> _mountControls = new();

    public override void _Ready()
    {
        _lblActive = GetNodeOrNull<Label>("Panel/VBox/LblActive");
        _optEngine = GetNodeOrNull<OptionButton>("Panel/VBox/HBoxEngine/OptEngine");
        _optComputer = GetNodeOrNull<OptionButton>("Panel/VBox/HBoxComputer/OptComputer");
        _vboxMounts = GetNodeOrNull<VBoxContainer>("Panel/VBox/Scroll/VBoxMounts");
        _btnApply = GetNodeOrNull<Button>("Panel/VBox/HBoxButtons/BtnApply");
        _btnBack = GetNodeOrNull<Button>("Panel/VBox/HBoxButtons/BtnBack");

        _lblAmmo = GetNodeOrNull<Label>("Panel/VBox/HBoxAmmo/LblAmmo");
        _btnBuy10 = GetNodeOrNull<Button>("Panel/VBox/HBoxAmmo/BtnBuy10");
        _btnBuy50 = GetNodeOrNull<Button>("Panel/VBox/HBoxAmmo/BtnBuy50");
        _btnBuy200 = GetNodeOrNull<Button>("Panel/VBox/HBoxAmmo/BtnBuy200");

        _btnApply!.Pressed += ApplyAndSave;
        _btnBack!.Pressed += Back;

        if (_btnBuy10 != null) _btnBuy10.Pressed += () => BuyAmmo(10);
        if (_btnBuy50 != null) _btnBuy50.Pressed += () => BuyAmmo(50);
        if (_btnBuy200 != null) _btnBuy200.Pressed += () => BuyAmmo(200);

        LoadActiveVehicleAndBuildUi();
    }

    private void LoadActiveVehicleAndBuildUi()
    {
        var app = App.Instance;
        if (app == null)
        {
            _lblActive!.Text = "Active: (App missing)";
            DisableAll();
            return;
        }

        var session = app.Services.Get<GameSession>();
        var defs = app.Services.Get<DefDatabase>();

        _vehicle = session.GetActiveVehicle();
        if (_vehicle == null)
        {
            _lblActive!.Text = "Active: (none). Go to Garage and set an active vehicle.";
            DisableAll();
            return;
        }

        if (!defs.Vehicles.TryGetValue(_vehicle.DefinitionId, out _vdef))
        {
            _lblActive!.Text = $"Active: (missing def '{_vehicle.DefinitionId}')";
            DisableAll();
            return;
        }

        _lblActive!.Text = $"Active: {_vdef.DisplayName}  ({_vdef.Id})  Class={_vdef.Class}";

        BuildEngineOptions(defs);
        BuildComputerOptions(defs);
        BuildMountOptions(defs);
        RefreshAmmoUi(session, defs);
    }

    private void DisableAll()
    {
        _optEngine!.Disabled = true;
        _optComputer!.Disabled = true;
        _btnApply!.Disabled = true;

        if (_btnBuy10 != null) _btnBuy10.Disabled = true;
        if (_btnBuy50 != null) _btnBuy50.Disabled = true;
        if (_btnBuy200 != null) _btnBuy200.Disabled = true;
    }

    private void BuildEngineOptions(DefDatabase defs)
    {
        _optEngine!.Clear();
        _optEngine.AddItem("(none)");
        _optEngine.SetItemMetadata(0, "");

        var idx = 1;
        foreach (var eng in defs.Engines.Values.OrderBy(e => e.DisplayName))
        {
            if (_vdef == null) continue;

            // Only show engines that support this vehicle class.
            if (eng.AllowedVehicleClasses != null && eng.AllowedVehicleClasses.Length > 0)
            {
                if (!eng.AllowedVehicleClasses.Contains(_vdef.Class))
                    continue;
            }

            _optEngine.AddItem($"{eng.DisplayName} [{eng.FuelType}]");
            _optEngine.SetItemMetadata(idx, eng.Id);
            idx++;
        }

        // Select current
        SelectByMetadata(_optEngine, _vehicle?.InstalledEngineId ?? "");
    }

    private void BuildComputerOptions(DefDatabase defs)
    {
        _optComputer!.Clear();
        _optComputer.AddItem("(none)");
        _optComputer.SetItemMetadata(0, "");

        var idx = 1;
        foreach (var c in defs.Computers.Values.OrderBy(c => c.DisplayName))
        {
            _optComputer.AddItem($"{c.DisplayName} (Groups {c.MaxActiveWeaponGroups}, AutoAim {c.AutoAimSlots})");
            _optComputer.SetItemMetadata(idx, c.Id);
            idx++;
        }

        SelectByMetadata(_optComputer, _vehicle?.InstalledComputerId ?? "");
    }

    private void BuildMountOptions(DefDatabase defs)
    {
        if (_vboxMounts == null || _vdef == null || _vehicle == null) return;

        // Clear existing
        foreach (var child in _vboxMounts.GetChildren())
            (child as Node)?.QueueFree();

        _mountControls.Clear();

        if (_vdef.MountPoints.Count == 0)
        {
            var lbl = new Label { Text = "(No mounts on this vehicle)" };
            _vboxMounts.AddChild(lbl);
            return;
        }

        foreach (var mount in _vdef.MountPoints)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var lbl = new Label
            {
                Text = $"{mount.MountId}  {mount.MountLocation}  Arc {mount.ArcDegrees:0}°  AutoAim {(mount.CanAutoAim ? "Y" : "N")}",
                CustomMinimumSize = new Vector2(320, 0)
            };

            var optWeapon = new OptionButton { CustomMinimumSize = new Vector2(220, 0) };
            var optAmmo = new OptionButton { CustomMinimumSize = new Vector2(220, 0) };

            row.AddChild(lbl);
            row.AddChild(optWeapon);
            row.AddChild(optAmmo);

            _vboxMounts.AddChild(row);

            // Populate weapon list
            optWeapon.AddItem("(none)");
            optWeapon.SetItemMetadata(0, "");

            var wi = 1;
            foreach (var w in defs.Weapons.Values.OrderBy(w => w.DisplayName))
            {
                optWeapon.AddItem($"{w.DisplayName} [{w.WeaponType}]");
                optWeapon.SetItemMetadata(wi, w.Id);
                wi++;
            }

            // Current installed
            _vehicle.InstalledWeaponsByMountId.TryGetValue(mount.MountId, out var installed);
            var currentWeaponId = installed?.WeaponId ?? "";
            var currentAmmoId = installed?.SelectedAmmoId ?? "";

            SelectByMetadata(optWeapon, currentWeaponId);
            PopulateAmmo(defs, optAmmo, currentWeaponId, currentAmmoId);

            // Wire changes: weapon change updates ammo list immediately
            optWeapon.ItemSelected += (long _) =>
            {
                var wId = GetSelectedMetadata(optWeapon);
                PopulateAmmo(defs, optAmmo, wId, "");
            };

            _mountControls[mount.MountId] = (optWeapon, optAmmo);
        }
    }

    private static void PopulateAmmo(DefDatabase defs, OptionButton optAmmo, string weaponId, string preferredAmmoId)
    {
        optAmmo.Clear();

        if (string.IsNullOrWhiteSpace(weaponId) || !defs.Weapons.TryGetValue(weaponId, out var wdef))
        {
            optAmmo.AddItem("(n/a)");
            optAmmo.SetItemMetadata(0, "");
            optAmmo.Disabled = true;
            return;
        }

        optAmmo.Disabled = false;

        var ammoIds = wdef.AmmoTypeIds ?? Array.Empty<string>();
        if (ammoIds.Length == 0)
        {
            optAmmo.AddItem("(no ammo)");
            optAmmo.SetItemMetadata(0, "");
            return;
        }

        var idx = 0;
        foreach (var aId in ammoIds)
        {
            if (!defs.Ammo.TryGetValue(aId, out var adef))
            {
                optAmmo.AddItem($"(missing ammo) {aId}");
                optAmmo.SetItemMetadata(idx, aId);
            }
            else
            {
                optAmmo.AddItem($"{adef.DisplayName} [{adef.AmmoKind}]");
                optAmmo.SetItemMetadata(idx, aId);
            }
            idx++;
        }

        SelectByMetadata(optAmmo, preferredAmmoId);
        if (optAmmo.Selected < 0 && optAmmo.ItemCount > 0)
            optAmmo.Select(0);
    }


    private void RefreshAmmoUi(GameSession session, DefDatabase defs)
    {
        if (_lblAmmo == null) return;

        if (_vehicle == null)
        {
            _lblAmmo.Text = "9mm Rounds: (n/a)";
            if (_btnBuy10 != null) _btnBuy10.Disabled = true;
            if (_btnBuy50 != null) _btnBuy50.Disabled = true;
            if (_btnBuy200 != null) _btnBuy200.Disabled = true;
            return;
        }

        var ammoId = GameSession.PrimaryAmmoId;
        var ammoCount = _vehicle.AmmoInventory != null && _vehicle.AmmoInventory.TryGetValue(ammoId, out var c) ? c : 0;
        var money = session.Save.Player.MoneyUsd;

        var unit = GameSession.PrimaryAmmoUnitCostUsd;
        var cost10 = 10 * unit;
        var cost50 = 50 * unit;
        var cost200 = 200 * unit;

        var ammoName = defs.Ammo.TryGetValue(ammoId, out var adef) ? adef.DisplayName : ammoId;
        _lblAmmo.Text = $"{ammoName}: {ammoCount}    Money: ${money}";

        if (_btnBuy10 != null) _btnBuy10.Text = $"Buy +10 (${cost10})";
        if (_btnBuy50 != null) _btnBuy50.Text = $"Buy +50 (${cost50})";
        if (_btnBuy200 != null) _btnBuy200.Text = $"Buy +200 (${cost200})";

        var can10 = money >= cost10;
        var can50 = money >= cost50;
        var can200 = money >= cost200;
        if (_btnBuy10 != null) _btnBuy10.Disabled = !can10;
        if (_btnBuy50 != null) _btnBuy50.Disabled = !can50;
        if (_btnBuy200 != null) _btnBuy200.Disabled = !can200;
    }

    private void BuyAmmo(int count)
    {
        var app = App.Instance;
        if (app == null || _vehicle == null) return;

        var session = app.Services.Get<GameSession>();
        var defs = app.Services.Get<DefDatabase>();

        if (!session.TryBuyAmmoForActiveVehicle(GameSession.PrimaryAmmoId, count, GameSession.PrimaryAmmoUnitCostUsd, out var err))
        {
            GD.Print($"[Workshop] Buy ammo failed: {err}");
            RefreshAmmoUi(session, defs);
            return;
        }

        // Refresh local vehicle snapshot
        _vehicle = session.GetActiveVehicle();
        GD.Print($"[Workshop] Bought +{count} ammo.");
        RefreshAmmoUi(session, defs);
    }

    private void ApplyAndSave()
    {
        var app = App.Instance;
        if (app == null || _vehicle == null) return;

        var session = app.Services.Get<GameSession>();

        var engineId = GetSelectedMetadata(_optEngine!);
        if (string.IsNullOrWhiteSpace(engineId)) engineId = null;

        var computerId = GetSelectedMetadata(_optComputer!);
        if (string.IsNullOrWhiteSpace(computerId)) computerId = null;

        // Build mount installs
        var installs = new Dictionary<string, InstalledWeaponState>();
        foreach (var kv in _mountControls)
        {
            var mountId = kv.Key;
            var weaponId = GetSelectedMetadata(kv.Value.weapon);
            if (string.IsNullOrWhiteSpace(weaponId))
                continue;

            var ammoId = GetSelectedMetadata(kv.Value.ammo);
            if (string.IsNullOrWhiteSpace(ammoId)) ammoId = null;

            installs[mountId] = new InstalledWeaponState
            {
                WeaponId = weaponId,
                SelectedAmmoId = ammoId
            };
        }

        var updated = _vehicle with
        {
            InstalledEngineId = engineId,
            InstalledComputerId = computerId,
            InstalledWeaponsByMountId = installs
        };

        session.UpdateVehicle(updated);

        // Refresh local cache to match persisted
        _vehicle = updated;
        GD.Print("[Workshop] Applied changes to active vehicle.");
    }

    private void Back()
    {
        var parent = GetParent();
        if (parent == null) return;

        var scene = GD.Load<PackedScene>("res://Scenes/UI/CityShell.tscn");
        var ui = scene.Instantiate();
        parent.AddChild(ui);
        QueueFree();
    }

    private static void SelectByMetadata(OptionButton opt, string desired)
    {
        if (opt.ItemCount == 0) return;

        for (var i = 0; i < opt.ItemCount; i++)
        {
            var md = opt.GetItemMetadata(i).AsString();
            if (string.Equals(md, desired ?? "", StringComparison.OrdinalIgnoreCase))
            {
                opt.Select(i);
                return;
            }
        }

        // Default to first item if desired not found.
        opt.Select(0);
    }

    private static string GetSelectedMetadata(OptionButton opt)
    {
        if (opt.Selected < 0 || opt.Selected >= opt.ItemCount) return "";
        return opt.GetItemMetadata(opt.Selected).AsString();
    }
}
