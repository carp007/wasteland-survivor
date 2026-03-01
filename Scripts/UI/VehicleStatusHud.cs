// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/VehicleStatusHud.cs
// Purpose: Vehicle HUD panel. Renders per-section/tire HP/AP, weapons list, speed/gear, and a mini top-down vehicle preview.
// -------------------------------------------------------------------------------------------------
using System;
using System.Linq;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Framework.SceneBinding;
using WastelandSurvivor.Game.Systems;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Top-right HUD showing the active vehicle's section + tire HP/AP around a small top-down vehicle preview.
/// </summary>
public partial class VehicleStatusHud : PanelContainer
{
    private bool _bound = false;
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
    private ValueBar _rpmBar = null!;

    // Vehicle preview (SubViewport)
    private SubViewport? _previewViewport;
    private Node3D? _previewRoot;
    private Camera3D? _previewCamera;
    private DirectionalLight3D? _previewLight;
    private MeshInstance3D? _previewMesh;

    // Live arena preview (render the real 3D world into the SubViewport using a camera above the player's vehicle).
    private bool _useLiveArenaPreview = false;
    private Node3D? _liveArenaVehicle;
    private const float LivePreviewHeight = 10.0f;
    // Keep the live preview zoomed in enough to be useful inside the small HUD window.
    private const float LivePreviewOrthoSize = 5.5f;

    private string? _previewVehicleDefId;
    private Color _previewBodyColor;
    private bool _previewInitialized = false;

	private Node3D? FindPlayerVehicleNode()
	{
		// Prefer the node named "Player" if present (that's what ArenaRealtimeView uses).
		try
		{
			foreach (var n in GetTree().GetNodesInGroup("player_vehicle"))
			{
				if (n is Node3D v && GodotObject.IsInstanceValid(v) && v.IsInsideTree() && v.Name == "Player")
					return v;
			}

			// Fallback: any valid Node3D in the group.
			foreach (var n in GetTree().GetNodesInGroup("player_vehicle"))
			{
				if (n is Node3D v && GodotObject.IsInstanceValid(v) && v.IsInsideTree())
					return v;
			}
		}
		catch
		{
			// ignore
		}

		return null;
	}

    public override void _Ready()
    {

		GameUiTheme.ApplyToTree(this);
		EnsureBound();
    }

    public override void _Process(double delta)
{
    if (!_previewInitialized) return;

    // If the HUD bound before the arena vehicle existed, switch the mini-view into "live" mode
    // once the player vehicle is spawned (group: player_vehicle).
    if (!_useLiveArenaPreview)
    {
        TryEnableLiveArenaPreview();
        if (!_useLiveArenaPreview) return;
    }

    if (_previewCamera == null) return;

    // Keep the vehicle preview camera locked above the real vehicle in arena.
    if (_liveArenaVehicle == null || !GodotObject.IsInstanceValid(_liveArenaVehicle))
    {
		_liveArenaVehicle = FindPlayerVehicleNode();
        if (_liveArenaVehicle == null)
        {
            // If we're no longer in arena, fall back to the static preview.
            _useLiveArenaPreview = false;
            RestoreStaticPreviewVisuals();
            return;
        }
    }

    // Center the preview camera directly above the active vehicle.
    // IMPORTANT: In Godot, "forward" is -Z, so we treat vehicle forward as (-Basis.Z).
    var tgt = _liveArenaVehicle.GlobalPosition;
    var camPos = tgt + new Vector3(0f, LivePreviewHeight, 0f);
    _previewCamera.GlobalPosition = camPos;

    // Rotate the mini-view so the vehicle always faces "up" on the HUD preview.
    // We do this by making the camera look straight down at the vehicle, and setting the camera's
    // UP vector to the vehicle's forward direction projected onto the ground plane.
    var vehicleForward = -_liveArenaVehicle.GlobalTransform.Basis.Z;
    vehicleForward.Y = 0f;
    if (vehicleForward.LengthSquared() < 0.0001f)
        vehicleForward = Vector3.Forward; // (0,0,-1)
    vehicleForward = vehicleForward.Normalized();

	// LookAt() points the camera's -Z toward the target; the "up" argument defines screen-up.
	// We lock the camera roll to the vehicle's forward so the vehicle always faces the top of the preview.
	_previewCamera.LookAt(tgt, vehicleForward);
}


    private void EnsureBound()
    {
        if (_bound) return;

        try
        {
			var b = new SceneBinder(this, nameof(VehicleStatusHud));

			_lblVehicleName = b.Req<Label>("VBox/LblVehicleName");
			_lblVehicleMass = b.Req<Label>("VBox/LblVehicleMass");
			_lblVehicleMassDetail = b.Req<Label>("VBox/LblVehicleMassDetail");

			_frontHp = b.Req<ValueBar>("VBox/FrontBox/FrontCenter/VBoxBars/FrontHp");
			_frontAp = b.Req<ValueBar>("VBox/FrontBox/FrontCenter/VBoxBars/FrontAp");
			_rearHp = b.Req<ValueBar>("VBox/RearBox/RearCenter/VBoxBars/RearHp");
			_rearAp = b.Req<ValueBar>("VBox/RearBox/RearCenter/VBoxBars/RearAp");

			_leftHp = b.ReqFallback<ValueBar>(
				"VBox/MidRow/LeftBox/LeftMargin/BarsCenter/HBoxBars/LeftHp",
				"VBox/MidRow/LeftBox/BarsCenter/HBoxBars/LeftHp");
			_leftAp = b.ReqFallback<ValueBar>(
				"VBox/MidRow/LeftBox/LeftMargin/BarsCenter/HBoxBars/LeftAp",
				"VBox/MidRow/LeftBox/BarsCenter/HBoxBars/LeftAp");
			_rightHp = b.ReqFallback<ValueBar>(
				"VBox/MidRow/RightBox/RightMargin/BarsCenter/HBoxBars/RightHp",
				"VBox/MidRow/RightBox/BarsCenter/HBoxBars/RightHp");
			_rightAp = b.ReqFallback<ValueBar>(
				"VBox/MidRow/RightBox/RightMargin/BarsCenter/HBoxBars/RightAp",
				"VBox/MidRow/RightBox/BarsCenter/HBoxBars/RightAp");

			_topHp = b.Req<ValueBar>("VBox/BottomVBox/TopUnderCenter/TopUnderBox/TopRow/TopHp");
			_topAp = b.Req<ValueBar>("VBox/BottomVBox/TopUnderCenter/TopUnderBox/TopRow/TopAp");
			_underHp = b.Req<ValueBar>("VBox/BottomVBox/TopUnderCenter/TopUnderBox/UnderRow/UnderHp");
			_underAp = b.Req<ValueBar>("VBox/BottomVBox/TopUnderCenter/TopUnderBox/UnderRow/UnderAp");

			_tireFlHp = b.Req<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireFL/VBoxBars/Hp");
			_tireFlAp = b.Req<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireFL/VBoxBars/Ap");
			_tireFrHp = b.Req<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireFR/VBoxBars/Hp");
			_tireFrAp = b.Req<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireFR/VBoxBars/Ap");
			_tireRlHp = b.Req<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireRL/VBoxBars/Hp");
			_tireRlAp = b.Req<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireRL/VBoxBars/Ap");
			_tireRrHp = b.Req<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireRR/VBoxBars/Hp");
			_tireRrAp = b.Req<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireRR/VBoxBars/Ap");

			_lblWeaponsList = b.Req<Label>("VBox/BottomVBox/WeaponsBox/WeaponsListMargin/LblWeaponsList");
			_speedBar = b.ReqFallback<ValueBar>(
				"VBox/BottomVBox/SpeedBox/SpeedRow/SpeedBar",
				"VBox/BottomVBox/SpeedBox/SpeedCenter/SpeedBar");
			_rpmBar = b.Req<ValueBar>("VBox/BottomVBox/SpeedBox/RpmRow/RpmBar");

            // Mark vertical bars.
            _leftHp.Vertical = true;
            _leftAp.Vertical = true;
            _rightHp.Vertical = true;
            _rightAp.Vertical = true;

			HookPreviewNodes(b);
            EnsurePreviewInitialized();
            _bound = true;
        }
        catch (Exception ex)
        {
            _bound = false;
            GD.PrintErr($"[VehicleStatusHud] Failed to bind UI nodes: {ex}");
        }
    }

    public void SetVehicle(VehicleDefinition def, VehicleInstanceState inst, DefDatabase defs, float speedCur, float speedMax, float rpm01, int rpmValue, string gearDisplay, Color? bodyColor = null)
    {
		EnsureBound();
		if (!_bound) return;
        _lblVehicleName.Text = $"Vehicle: {def.DisplayName}";

		var bd = VehicleMassMath.ComputeBreakdown(def, inst, defs);
		_lblVehicleMass.Text = $"Mass: {FormatKg(bd.TotalKg)}";
		_lblVehicleMassDetail.Text = $"V {FormatKg(bd.VehicleKg)}  W {FormatKg(bd.WeaponsKg)}  A {FormatKg(bd.AmmoKg)}" + (bd.TowedKg > 0.5f ? $"  Tow {FormatKg(bd.TowedKg)}" : "");

        // Update the top-down preview (cheap + cached).
        EnsurePreviewInitialized();
        TryEnableLiveArenaPreview();
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
        UpdateSpeedAndRpm(speedCur, speedMax, rpm01, rpmValue, gearDisplay);
    }

    public void UpdateDynamic(VehicleInstanceState inst, DefDatabase defs, float speedCur, float speedMax, float rpm01, int rpmValue, string gearDisplay)
    {
		EnsureBound();
		if (!_bound) return;
        UpdateWeaponsList(inst, defs);
        UpdateSpeedAndRpm(speedCur, speedMax, rpm01, rpmValue, gearDisplay);
    }

	private void HookPreviewNodes(SceneBinder b)
    {
        // Optional: if scene changes or this HUD is used elsewhere, fail soft.
		_previewViewport = b.Opt<SubViewport>("VBox/MidRow/CenterBox/Center/VehiclePreview/Viewport");
		_previewRoot = b.Opt<Node3D>("VBox/MidRow/CenterBox/Center/VehiclePreview/Viewport/PreviewRoot");
		_previewCamera = b.Opt<Camera3D>("VBox/MidRow/CenterBox/Center/VehiclePreview/Viewport/PreviewRoot/Camera");
		_previewLight = b.Opt<DirectionalLight3D>("VBox/MidRow/CenterBox/Center/VehiclePreview/Viewport/PreviewRoot/Light");
		_previewMesh = b.Opt<MeshInstance3D>("VBox/MidRow/CenterBox/Center/VehiclePreview/Viewport/PreviewRoot/VehicleMesh");
    }

    private void EnsurePreviewInitialized()
{
    if (_previewInitialized) return;
    if (_previewViewport == null || _previewRoot == null || _previewCamera == null || _previewLight == null || _previewMesh == null)
        return;

    _previewViewport.TransparentBg = true;
    _previewViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;

    // Always process so we can switch to live arena preview once the player vehicle exists.
    SetProcess(true);

    // Default to static preview.
    _useLiveArenaPreview = false;

    // Top-down orthographic camera.
    _previewCamera.Projection = Camera3D.ProjectionType.Orthogonal;
    _previewCamera.Size = 4.2f;
    _previewCamera.Position = new Vector3(0f, 6f, 0f);
    _previewCamera.RotationDegrees = new Vector3(-90f, 0f, 0f);
    _previewCamera.Current = true;

    // Simple light to show some shape/edges.
    _previewLight.Visible = true;
    _previewLight.LightEnergy = 1.15f;
    _previewLight.RotationDegrees = new Vector3(-55f, 45f, 0f);

    // Simple vehicle mesh (v1 vehicle is a box). Front points toward -Z.
    _previewMesh.Visible = true;
    _previewMesh.Mesh = new BoxMesh { Size = new Vector3(1.8f, 0.8f, 3.2f) };
    _previewMesh.Position = new Vector3(0f, 0.4f, 0f);
    _previewMesh.Rotation = Vector3.Zero;

    _previewVehicleDefId = null;
    _previewBodyColor = new Color(0, 0, 0, 0);
    _previewInitialized = true;

    // If the arena is already active, switch immediately.
    TryEnableLiveArenaPreview();
}



private void TryEnableLiveArenaPreview()
{
    if (_previewViewport == null || _previewCamera == null || _previewLight == null || _previewMesh == null)
        return;
    if (_useLiveArenaPreview) return;

	var arenaVehicle = FindPlayerVehicleNode();
    if (arenaVehicle == null) return;

    _useLiveArenaPreview = true;
    _liveArenaVehicle = arenaVehicle;

    // Share the main world so we render the actual arena/vehicle.
    _previewViewport.World3D = GetViewport().World3D;

    // Hide static preview visuals.
    _previewLight.Visible = false;
    _previewMesh.Visible = false;

    _previewCamera.Projection = Camera3D.ProjectionType.Orthogonal;
	_previewCamera.Size = LivePreviewOrthoSize;
	_previewCamera.Current = true;

	// Force an immediate pose update so the first frame of the live preview is correct.
	_liveArenaVehicle = arenaVehicle;
	var tgt = _liveArenaVehicle.GlobalPosition;
	var camPos = tgt + new Vector3(0f, LivePreviewHeight, 0f);
	var vehicleForward = -_liveArenaVehicle.GlobalTransform.Basis.Z;
	vehicleForward.Y = 0f;
	if (vehicleForward.LengthSquared() < 0.0001f)
		vehicleForward = Vector3.Forward;
	vehicleForward = vehicleForward.Normalized();
	_previewCamera.GlobalPosition = camPos;
	_previewCamera.LookAt(tgt, vehicleForward);
}
    private void RestoreStaticPreviewVisuals()
    {
        if (_previewViewport == null || _previewCamera == null || _previewLight == null || _previewMesh == null) return;
        _previewViewport.World3D = null;
        _previewLight.Visible = true;
        _previewMesh.Visible = true;
        _previewCamera.Projection = Camera3D.ProjectionType.Orthogonal;
        _previewCamera.Size = 4.2f;
        _previewCamera.Position = new Vector3(0f, 6f, 0f);
        _previewCamera.RotationDegrees = new Vector3(-90f, 0f, 0f);
        _previewCamera.Current = true;
    }

    private void UpdatePreview(string vehicleDefId, Color bodyColor)
    {
        if (!_previewInitialized) return;
        if (_previewMesh == null) return;

        // When using a live arena preview we render the real 3D vehicle, so don't color the proxy mesh.
        if (_useLiveArenaPreview) return;

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

            var ammoText = string.IsNullOrWhiteSpace(ammoId) ? "Ammo: n/a" : $"Ammo: {ammoCount}";
            lines.Add($"{mountId}: {weaponName}  {ammoText}");
        }

        // Use a blank line between items for readability (acts like light padding).
        _lblWeaponsList.Text = string.Join("\n", lines);
    }

    private void UpdateSpeedAndRpm(float speedCur, float speedMax, float rpm01, int rpmValue, string gearDisplay)
    {
        // Speed: keep readable and compact.
        _speedBar.SetValues(speedCur, MathF.Max(0.01f, speedMax), new Color(0.90f, 0.75f, 0.20f), Colors.White);

        // RPM: gold bar with gear indicator.
        rpm01 = Mathf.Clamp(rpm01, 0f, 1f);
        var txt = $"{rpmValue:0000} - [{gearDisplay}]";
        _rpmBar.SetCustom(rpm01, txt, new Color(0.90f, 0.75f, 0.20f), Colors.White);
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
