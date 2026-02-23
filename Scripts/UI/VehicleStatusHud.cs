using System;
using System.Linq;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game.Systems;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Top-right HUD showing the active vehicle's section + tire HP/AP around a small top-down vehicle preview.
/// </summary>
public partial class VehicleStatusHud : PanelContainer
{
    private Label _lblVehicleName = null!;
    private Label _lblVehicleMass = null!;
    private Label _lblVehicleMassDetail = null!;

    private ValueBar _frontHp = null!;
    private ValueBar _frontAp = null!;
    private ValueBar _rearHp = null!;
    private ValueBar _rearAp = null!;

    private ValueBar _leftHp = null!;
    private ValueBar _leftAp = null!;
    private ValueBar _rightHp = null!;
    private ValueBar _rightAp = null!;

    private ValueBar _topHp = null!;
    private ValueBar _topAp = null!;
    private ValueBar _underHp = null!;
    private ValueBar _underAp = null!;

    private ValueBar _tireFlHp = null!;
    private ValueBar _tireFlAp = null!;
    private ValueBar _tireFrHp = null!;
    private ValueBar _tireFrAp = null!;
    private ValueBar _tireRlHp = null!;
    private ValueBar _tireRlAp = null!;
    private ValueBar _tireRrHp = null!;
    private ValueBar _tireRrAp = null!;

    private Label _lblWeaponsList = null!;
    private ValueBar _speedBar = null!;

    // Vehicle preview (SubViewport)
    private SubViewport? _previewViewport;
    private Node3D? _previewRoot;
    private Camera3D? _previewCamera;
    private DirectionalLight3D? _previewLight;
    private MeshInstance3D? _previewMesh;

    private string? _previewVehicleDefId;
    private Color _previewBodyColor;
    private bool _previewInitialized = false;

    public override void _Ready()
    {
        _lblVehicleName = GetNode<Label>("VBox/LblVehicleName");
        _lblVehicleMass = GetNode<Label>("VBox/LblVehicleMass");
        _lblVehicleMassDetail = GetNode<Label>("VBox/LblVehicleMassDetail");

        _frontHp = GetNode<ValueBar>("VBox/FrontBox/FrontCenter/VBoxBars/FrontHp");
        _frontAp = GetNode<ValueBar>("VBox/FrontBox/FrontCenter/VBoxBars/FrontAp");
        _rearHp = GetNode<ValueBar>("VBox/RearBox/RearCenter/VBoxBars/RearHp");
        _rearAp = GetNode<ValueBar>("VBox/RearBox/RearCenter/VBoxBars/RearAp");

        _leftHp = GetNode<ValueBar>("VBox/MidRow/LeftBox/BarsCenter/HBoxBars/LeftHp");
        _leftAp = GetNode<ValueBar>("VBox/MidRow/LeftBox/BarsCenter/HBoxBars/LeftAp");
        _rightHp = GetNode<ValueBar>("VBox/MidRow/RightBox/BarsCenter/HBoxBars/RightHp");
        _rightAp = GetNode<ValueBar>("VBox/MidRow/RightBox/BarsCenter/HBoxBars/RightAp");

        _topHp = GetNode<ValueBar>("VBox/BottomVBox/TopUnderCenter/TopUnderBox/TopRow/TopHp");
        _topAp = GetNode<ValueBar>("VBox/BottomVBox/TopUnderCenter/TopUnderBox/TopRow/TopAp");
        _underHp = GetNode<ValueBar>("VBox/BottomVBox/TopUnderCenter/TopUnderBox/UnderRow/UnderHp");
        _underAp = GetNode<ValueBar>("VBox/BottomVBox/TopUnderCenter/TopUnderBox/UnderRow/UnderAp");

        _tireFlHp = GetNode<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireFL/VBoxBars/Hp");
        _tireFlAp = GetNode<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireFL/VBoxBars/Ap");
        _tireFrHp = GetNode<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireFR/VBoxBars/Hp");
        _tireFrAp = GetNode<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireFR/VBoxBars/Ap");
        _tireRlHp = GetNode<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireRL/VBoxBars/Hp");
        _tireRlAp = GetNode<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireRL/VBoxBars/Ap");
        _tireRrHp = GetNode<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireRR/VBoxBars/Hp");
        _tireRrAp = GetNode<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireRR/VBoxBars/Ap");

        _lblWeaponsList = GetNode<Label>("VBox/BottomVBox/WeaponsBox/WeaponsListMargin/LblWeaponsList");
        _speedBar = GetNode<ValueBar>("VBox/BottomVBox/SpeedBox/SpeedCenter/SpeedBar");

        // Mark vertical bars.
        _leftHp.Vertical = true;
        _leftAp.Vertical = true;
        _rightHp.Vertical = true;
        _rightAp.Vertical = true;

        HookPreviewNodes();
        EnsurePreviewInitialized();
    }

    public void SetVehicle(VehicleDefinition def, VehicleInstanceState inst, DefDatabase defs, float speedCur, float speedMax, Color? bodyColor = null)
    {
        _lblVehicleName.Text = $"Vehicle: {def.DisplayName}";

		var bd = VehicleMassMath.ComputeBreakdown(def, inst, defs);
		_lblVehicleMass.Text = $"Mass: {FormatKg(bd.TotalKg)}";
		_lblVehicleMassDetail.Text = $"V {FormatKg(bd.VehicleKg)}  W {FormatKg(bd.WeaponsKg)}  A {FormatKg(bd.AmmoKg)}" + (bd.TowedKg > 0.5f ? $"  Tow {FormatKg(bd.TowedKg)}" : "");

        // Update the top-down preview (cheap + cached).
        EnsurePreviewInitialized();
        UpdatePreview(def.Id, bodyColor ?? new Color(0.20f, 0.85f, 0.25f));

        SetSection(def, inst, ArmorSection.Front, _frontHp, _frontAp);
        SetSection(def, inst, ArmorSection.Rear, _rearHp, _rearAp);
        SetSection(def, inst, ArmorSection.Left, _leftHp, _leftAp);
        SetSection(def, inst, ArmorSection.Right, _rightHp, _rightAp);
        SetSection(def, inst, ArmorSection.Top, _topHp, _topAp);
        SetSection(def, inst, ArmorSection.Undercarriage, _underHp, _underAp);

        SetTire(def, inst, 0, _tireFlHp, _tireFlAp);
        SetTire(def, inst, 1, _tireFrHp, _tireFrAp);
        SetTire(def, inst, 2, _tireRlHp, _tireRlAp);
        SetTire(def, inst, 3, _tireRrHp, _tireRrAp);

        UpdateWeaponsList(inst, defs);
        UpdateSpeed(speedCur, speedMax);
    }

    public void UpdateDynamic(VehicleInstanceState inst, DefDatabase defs, float speedCur, float speedMax)
    {
        UpdateWeaponsList(inst, defs);
        UpdateSpeed(speedCur, speedMax);
    }

    private void HookPreviewNodes()
    {
        // Optional: if scene changes or this HUD is used elsewhere, fail soft.
        _previewViewport = GetNodeOrNull<SubViewport>("VBox/MidRow/CenterBox/Center/VehiclePreview/Viewport");
        _previewRoot = GetNodeOrNull<Node3D>("VBox/MidRow/CenterBox/Center/VehiclePreview/Viewport/PreviewRoot");
        _previewCamera = GetNodeOrNull<Camera3D>("VBox/MidRow/CenterBox/Center/VehiclePreview/Viewport/PreviewRoot/Camera");
        _previewLight = GetNodeOrNull<DirectionalLight3D>("VBox/MidRow/CenterBox/Center/VehiclePreview/Viewport/PreviewRoot/Light");
        _previewMesh = GetNodeOrNull<MeshInstance3D>("VBox/MidRow/CenterBox/Center/VehiclePreview/Viewport/PreviewRoot/VehicleMesh");
    }

    private void EnsurePreviewInitialized()
    {
        if (_previewInitialized) return;
        if (_previewViewport == null || _previewRoot == null || _previewCamera == null || _previewLight == null || _previewMesh == null)
            return;

        // Ensure a clean, predictable 3D space for the preview.
        _previewViewport.TransparentBg = true;
        _previewViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;

        // Top-down orthographic camera.
        _previewCamera.Projection = Camera3D.ProjectionType.Orthogonal;
        _previewCamera.Size = 4.2f;
        _previewCamera.Position = new Vector3(0f, 6f, 0f);
        _previewCamera.RotationDegrees = new Vector3(-90f, 0f, 0f);
        _previewCamera.Current = true;

        // Simple light to show some shape/edges.
        _previewLight.LightEnergy = 1.15f;
        _previewLight.RotationDegrees = new Vector3(-55f, 45f, 0f);

        // Simple vehicle mesh (v1 vehicle is a box). Front points toward -Z.
        _previewMesh.Mesh = new BoxMesh { Size = new Vector3(1.8f, 0.8f, 3.2f) };
        _previewMesh.Position = new Vector3(0f, 0.4f, 0f);
        _previewMesh.Rotation = Vector3.Zero;

        _previewVehicleDefId = null;
        _previewBodyColor = new Color(0, 0, 0, 0);
        _previewInitialized = true;
    }

    private void UpdatePreview(string vehicleDefId, Color bodyColor)
    {
        if (!_previewInitialized) return;
        if (_previewMesh == null) return;

        // Cache so RefreshStats() can call SetVehicle frequently without redoing work.
        if (_previewVehicleDefId == vehicleDefId && _previewBodyColor == bodyColor)
            return;

        _previewVehicleDefId = vehicleDefId;
        _previewBodyColor = bodyColor;

        var mat = new StandardMaterial3D
        {
            AlbedoColor = bodyColor,
            Roughness = 0.9f,
            Metallic = 0.05f,
        };
        _previewMesh.SetSurfaceOverrideMaterial(0, mat);
    }

    private void UpdateWeaponsList(VehicleInstanceState inst, DefDatabase defs)
    {
        if (inst.InstalledWeaponsByMountId.Count == 0)
        {
            _lblWeaponsList.Text = "(none)";
            return;
        }

        var lines = new System.Collections.Generic.List<string>();
        foreach (var kvp in inst.InstalledWeaponsByMountId.OrderBy(k => k.Key))
        {
            var mountId = kvp.Key;
            var w = kvp.Value;
            var weaponName = defs.Weapons.TryGetValue(w.WeaponId, out var wdef) ? wdef.DisplayName : w.WeaponId;
            var ammoId = w.SelectedAmmoId;
            if (string.IsNullOrWhiteSpace(ammoId) && wdef != null && wdef.AmmoTypeIds.Length > 0)
                ammoId = wdef.AmmoTypeIds[0];

            var ammoCount = 0;
            if (!string.IsNullOrWhiteSpace(ammoId))
                inst.AmmoInventory.TryGetValue(ammoId!, out ammoCount);

            var ammoText = string.IsNullOrWhiteSpace(ammoId) ? "Ammo: n/a" : $"Ammo: {ammoCount} {ammoId}";
            lines.Add($"{mountId}: {weaponName}  ({ammoText})");
        }

        // Use a blank line between items for readability (acts like light padding).
        _lblWeaponsList.Text = string.Join("\n\n", lines);
    }

    private void UpdateSpeed(float cur, float max)
    {
        // Gold bar with white text.
        _speedBar.SetValues(cur, MathF.Max(0.01f, max), new Color(0.90f, 0.75f, 0.20f), Colors.White);
    }

	private static string FormatKg(float kg)
	{
		kg = MathF.Max(0f, kg);
		// Simple readable formatting.
		if (kg >= 10000f) return $"{MathF.Round(kg):0} kg";
		if (kg >= 1000f) return $"{kg:0} kg";
		return $"{kg:0} kg";
	}

    private static void SetSection(VehicleDefinition def, VehicleInstanceState inst, ArmorSection section, ValueBar hpBar, ValueBar apBar)
    {
        inst.CurrentHpBySection.TryGetValue(section, out var curHp);
        inst.CurrentArmorBySection.TryGetValue(section, out var curAp);

        def.BaseHpBySection.TryGetValue(section, out var maxHp);
        def.BaseArmorBySection.TryGetValue(section, out var baseAp);
        var maxAp = Math.Max(0, baseAp + Math.Max(0, inst.ArmorPlatingLevel));

        hpBar.SetValues(curHp, Math.Max(1, maxHp), HpColor(curHp, Math.Max(1, maxHp)));
        apBar.SetValues(curAp, Math.Max(1, maxAp), ArmorColor(curAp, Math.Max(1, maxAp)));
    }

    private static void SetTire(VehicleDefinition def, VehicleInstanceState inst, int idx, ValueBar hpBar, ValueBar apBar)
    {
        var tireCount = Math.Max(0, def.TireCount);
        if (tireCount <= 0)
        {
            hpBar.SetValues(0, 1, HpColor(0, 1));
            apBar.SetValues(0, 1, ArmorColor(0, 1));
            return;
        }

        idx = Math.Clamp(idx, 0, tireCount - 1);
        var curHp = (inst.CurrentTireHp is { Length: > 0 } && idx < inst.CurrentTireHp.Length) ? inst.CurrentTireHp[idx] : 0;
        var curAp = (inst.CurrentTireArmor is { Length: > 0 } && idx < inst.CurrentTireArmor.Length) ? inst.CurrentTireArmor[idx] : 0;

        var maxHp = Math.Max(1, def.BaseTireHp);
        var maxAp = Math.Max(1, def.BaseTireArmor + Math.Max(0, inst.TirePlatingLevel));

        hpBar.SetValues(curHp, maxHp, HpColor(curHp, maxHp));
        apBar.SetValues(curAp, maxAp, ArmorColor(curAp, maxAp));
    }

    private static Color HpColor(int cur, int max)
    {
        max = Math.Max(1, max);
        var pct = (double)Math.Clamp(cur, 0, max) / max;

        if (pct >= 0.999)
            return new Color(0.35f, 0.80f, 0.35f);
        if (pct >= 0.70)
            return new Color(0.10f, 0.55f, 0.10f);
        if (pct >= 0.30)
            return new Color(1.00f, 0.90f, 0.20f);
        if (pct >= 0.10)
            return new Color(0.55f, 0.05f, 0.05f);
        return new Color(1.00f, 0.15f, 0.15f);
    }

    private static Color ArmorColor(int cur, int max)
    {
        max = Math.Max(1, max);
        var pct = (double)Math.Clamp(cur, 0, max) / max;
        var light = new Color(0.35f, 0.65f, 0.90f);
        var dark = new Color(0.05f, 0.20f, 0.45f);
        return dark.Lerp(light, (float)pct);
    }
}
