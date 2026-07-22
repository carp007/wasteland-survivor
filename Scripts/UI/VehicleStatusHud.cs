// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/VehicleStatusHud.cs
// Purpose: Compact combat vehicle HUD. Locational damage reads off the mini-vehicle schematic
//          (tints + facing ring); weapons render as icon slots with ammo + cooldown sweep.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using GameUiKit.SceneBinding;
using WastelandSurvivor.Game.Systems;
using WastelandSurvivor.Game.Arena;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Top-right HUD: vehicle name, total mass, mini top-down vehicle preview (the primary locational
/// damage read), compact weapon slots, and speed/RPM. The legacy per-section/tire bar nodes stay in
/// the scene (hidden) so ArenaRealtimeView's untyped fallback binder keeps working unchanged.
/// </summary>
public partial class VehicleStatusHud : PanelContainer
{
    private bool _bound = false;

    [Bind("VBox/LblVehicleName")]
    private Label _lblVehicleName = null!;

    [Bind("VBox/LblVehicleMass")]
    private Label _lblVehicleMass = null!;

    [Bind("VBox/BottomVBox/SpeedBox/SpeedRow/SpeedBar", Fallback = "VBox/BottomVBox/SpeedBox/SpeedCenter/SpeedBar")]
    private ValueBar _speedBar = null!;

    [Bind("VBox/BottomVBox/SpeedBox/RpmRow/RpmBar")]
    private ValueBar _rpmBar = null!;

    [Bind("VBox/MidRow/CenterBox/Center/VehiclePreviewHost", Optional = true, Fallback = "VBox/MidRow/CenterBox/Center/VehiclePreview")]
    private VehiclePreviewCanvas? _previewCanvas;

    // Preview source = the live player vehicle in the arena. The HUD now draws directly into the
    // on-screen preview control instead of spawning runtime child controls/viewports, which could leave
    // us mutating a different path than the one actually visible in the HUD.
    private Node3D? _previewVehicleSource;
    private Node3D? _explicitPreviewVehicleSource;

    private string? _previewVehicleDefId;
    private Color _previewBodyColor;
    private VehicleInstanceState? _previewRuntimeState;
    private DefDatabase? _previewDefs;
    private bool _previewRebuildPending;
    private int _previewBuildGeneration;
    private TextureRect? _embeddedPreviewPresenter;
    private SubViewport? _embeddedPreviewViewport;
    private Node3D? _embeddedPreviewRoot;
    private VehiclePawn? _embeddedPreviewPawn;
    private Camera3D? _embeddedPreviewCamera;

	private Node3D? FindPlayerVehicleNode()
	{
		if (_explicitPreviewVehicleSource != null
			&& GodotObject.IsInstanceValid(_explicitPreviewVehicleSource)
			&& _explicitPreviewVehicleSource.IsInsideTree())
		{
			return _explicitPreviewVehicleSource;
		}

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

    public void SetPreviewSource(Node3D? vehicle)
    {
        if (_explicitPreviewVehicleSource == vehicle)
            return;

        _explicitPreviewVehicleSource = vehicle;
        InvalidatePreviewSurface();
    }

    public override void _Ready()
    {

		GameUiTheme.ApplyToTree(this);
		EnsureBound();

		// Near-opaque backing: at the old translucency, pennant flags / husks / floor decals at
		// the frame edge bled through and read as stray UI elements overlapping the ammo rows
		// (eval round 6 P1-3). Full modulate + a dedicated dark stylebox keeps the panel legible
		// over any venue dressing.
		Modulate = Colors.White;
		AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.045f, 0.055f, 0.075f, 0.94f),
			BorderColor = new Color(0.28f, 0.33f, 0.42f, 0.55f),
			BorderWidthTop = 1,
			BorderWidthBottom = 1,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			ContentMarginLeft = 8,
			ContentMarginRight = 8,
			ContentMarginTop = 6,
			ContentMarginBottom = 8,
		});

		// HUD text hierarchy: display-font header, quiet supporting mass line (workshop owns the breakdown).
		if (_bound)
		{
			GameUiTheme.StyleHeading(_lblVehicleName, GameUiTheme.BaseFontSize + 3);
			_lblVehicleMass.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
			_lblVehicleMass.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 4);
		}
    }

    public override void _Process(double delta)
    {
        UpdateHazardPulse(delta);

        if (_previewCanvas == null || !GodotObject.IsInstanceValid(_previewCanvas))
            return;

        SyncLiveVehiclePreview();
        EnsureEmbeddedPreviewViewport();
    }

    // ---------------------------------------------------------------------------------------------
    // Hazard warning strip (e.g. LOW GRIP while sliding on an oil slick). Created lazily in code so
    // the scene file stays untouched; ArenaRealtimeView drives it from VehiclePawn.IsOnOilSlick.
    // ---------------------------------------------------------------------------------------------
    private PanelContainer? _hazardStrip;
    private Label? _hazardLabel;
    private bool _lowGripActive;
    private double _hazardPulseT;

    /// <summary>Show/hide the pulsing "LOW GRIP" hazard strip under the HUD (oil-slick feedback).</summary>
    public void SetLowGripWarning(bool active)
    {
        if (_lowGripActive == active && (_hazardStrip != null || !active))
        {
            return;
        }

        _lowGripActive = active;
        if (active)
            EnsureHazardStrip();

        if (_hazardStrip != null && GodotObject.IsInstanceValid(_hazardStrip))
            _hazardStrip.Visible = active;
    }

    private void EnsureHazardStrip()
    {
        if (_hazardStrip != null && GodotObject.IsInstanceValid(_hazardStrip))
            return;

        var vbox = GetNodeOrNull<BoxContainer>("VBox");
        if (vbox == null)
            return;

        _hazardStrip = new PanelContainer { Name = "HazardStrip", Visible = false };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.55f, 0.28f, 0.02f, 0.85f),
            BorderColor = new Color(1.0f, 0.62f, 0.10f, 0.95f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 6, ContentMarginRight = 6,
            ContentMarginTop = 2, ContentMarginBottom = 2,
        };
        style.SetBorderWidthAll(1);
        _hazardStrip.AddThemeStyleboxOverride("panel", style);

        _hazardLabel = new Label
        {
            Text = "LOW GRIP - OIL",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _hazardLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.45f));
        _hazardLabel.AddThemeFontSizeOverride("font_size", 12);
        _hazardStrip.AddChild(_hazardLabel);
        vbox.AddChild(_hazardStrip);
    }

    private void UpdateHazardPulse(double delta)
    {
        if (!_lowGripActive || _hazardLabel == null || !GodotObject.IsInstanceValid(_hazardLabel))
            return;

        _hazardPulseT += delta;
        var pulse = 0.70f + 0.30f * MathF.Sin((float)_hazardPulseT * 7.0f);
        _hazardLabel.Modulate = new Color(1f, 1f, 1f, pulse);
    }

    private void EnsureBound()
    {
        if (_bound) return;

        try
        {
            SceneAutoBinder.Apply(this, nameof(VehicleStatusHud));

            _previewCanvas ??= GetNodeOrNull<VehiclePreviewCanvas>("VBox/MidRow/CenterBox/Center/VehiclePreviewHost")
                ?? GetNodeOrNull<VehiclePreviewCanvas>("VBox/MidRow/CenterBox/Center/VehiclePreview");

            _bound = _previewCanvas != null;
            if (!_bound)
                GD.PrintErr("[VehicleStatusHud] VehiclePreviewCanvas node/script is missing or failed to bind.");

            if (_bound)
                RestructureForCombat();
        }
        catch (Exception ex)
        {
            _bound = false;
            GD.PrintErr($"[VehicleStatusHud] Failed to bind UI nodes: {ex}");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Combat layout diet: the section/tire bar grids fold into the schematic (canvas tints + facing
    // ring + tire pads). The legacy nodes are only HIDDEN — the untyped fallback binder in
    // ArenaRealtimeView still resolves them by path when this script fails to attach.
    // ---------------------------------------------------------------------------------------------
    private static readonly string[] LegacyCombatNodePaths =
    {
        "VBox/LblVehicleMassDetail",
        "VBox/FrontBox",
        "VBox/RearBox",
        "VBox/MidRow/LeftBox",
        "VBox/MidRow/RightBox",
        "VBox/BottomVBox/Spacer",
        "VBox/BottomVBox/TopUnderCenter",
        "VBox/BottomVBox/TiresSpacer",
        "VBox/BottomVBox/Tires",
        "VBox/BottomVBox/WeaponsSpacer",
        "VBox/BottomVBox/WeaponsBox/LblWeapons",
        "VBox/BottomVBox/WeaponsBox/WeaponsListMargin",
    };

    private void RestructureForCombat()
    {
        foreach (var path in LegacyCombatNodePaths)
        {
            if (GetNodeOrNull<Control>(path) is { } legacy)
                legacy.Visible = false;
        }

        // Give the schematic the space freed by the side bar columns; it is now the primary read.
        if (GetNodeOrNull<Control>("VBox/MidRow/CenterBox") is { } centerBox)
            centerBox.CustomMinimumSize = new Vector2(184f, 184f);
        if (_previewCanvas != null && GodotObject.IsInstanceValid(_previewCanvas))
        {
            _previewCanvas.CustomMinimumSize = new Vector2(168f, 168f);
            _previewCanvas.SetCombatReadoutEnabled(true);
        }

        // Weapon slots replace the plain-text weapons list.
        if (_weaponSlotsBox == null && GetNodeOrNull<VBoxContainer>("VBox/BottomVBox/WeaponsBox") is { } weaponsBox)
        {
            _weaponSlotsBox = new VBoxContainer { Name = "WeaponSlots" };
            _weaponSlotsBox.AddThemeConstantOverride("separation", 4);
            weaponsBox.AddChild(_weaponSlotsBox);
        }

        // Narrower + shrink-to-content height (Godot clamps to combined minimum size, so the panel
        // hugs the slimmed layout instead of the old 558px footprint).
        OffsetLeft = -330f;
        OffsetBottom = OffsetTop + 40f;
    }

    public void SetVehicle(VehicleDefinition def, VehicleInstanceState inst, DefDatabase defs, float speedCur, float speedMax, float rpm01, int rpmValue, string gearDisplay, Color? bodyColor = null)
    {
		EnsureBound();
		if (!_bound) return;
        _lblVehicleName.Text = def.DisplayName;

		var bd = VehicleMassMath.ComputeBreakdown(def, inst, defs);
		_lblVehicleMass.Text = $"Mass: {FormatKg(bd.TotalKg)}" + (bd.TowedKg > 0.5f ? $"  (+{FormatKg(bd.TowedKg)} tow)" : "");

        // Default matches the car pack's player paint (Body_1, yellow) so the HUD mini-vehicle reads
        // as YOUR car rather than a generic green proxy.
        UpdatePreview(def, inst, defs, bodyColor ?? new Color(0.93f, 0.76f, 0.12f));

        UpdateWeaponSlots(inst, defs);
        UpdateSpeedAndRpm(speedCur, speedMax, rpm01, rpmValue, gearDisplay);
    }

    public void UpdateDynamic(VehicleInstanceState inst, DefDatabase defs, float speedCur, float speedMax, float rpm01, int rpmValue, string gearDisplay)
    {
		EnsureBound();
		if (!_bound) return;
        UpdateWeaponSlots(inst, defs);
        UpdateSpeedAndRpm(speedCur, speedMax, rpm01, rpmValue, gearDisplay);

        if (_previewCanvas != null && GodotObject.IsInstanceValid(_previewCanvas) && !string.IsNullOrWhiteSpace(_previewVehicleDefId)
            && defs.Vehicles.TryGetValue(_previewVehicleDefId!, out var previewDef))
        {
            // Pushes fresh section/tire damage into the canvas AND its embedded-preview damage
            // overlay every dynamic tick; the canvas only repaints when values actually change.
            _previewCanvas.SetVehicle(previewDef, inst, _previewBodyColor);
            SyncLiveVehiclePreview();
        }
    }



    private void SyncLiveVehiclePreview()
    {
        if (_previewCanvas == null || !GodotObject.IsInstanceValid(_previewCanvas))
            return;

        var arenaVehicle = FindPlayerVehicleNode();
        if (arenaVehicle == null || !GodotObject.IsInstanceValid(arenaVehicle))
        {
            _previewVehicleSource = null;
            _previewCanvas.SetLiveYaw(0f);
            ClearEmbeddedPreview();
            return;
        }

        if (_previewVehicleSource != null && _previewVehicleSource != arenaVehicle)
            InvalidatePreviewSurface();

        _previewVehicleSource = arenaVehicle;
        _previewCanvas.SetLiveYaw(arenaVehicle.GlobalRotation.Y);
        UpdateEmbeddedPreviewOrientation();

        if (arenaVehicle is VehiclePawn vp)
            _previewCanvas.SetLiveBodyColor(vp.BodyColor);
    }

    private void UpdatePreview(VehicleDefinition def, VehicleInstanceState inst, DefDatabase defs, Color bodyColor)
    {
        if (_previewCanvas == null || !GodotObject.IsInstanceValid(_previewCanvas))
            return;

        var source = FindPlayerVehicleNode();
        var snapshotInputsChanged = _previewVehicleDefId != def.Id
            || !ColorsClose(_previewBodyColor, bodyColor)
            || source != _previewVehicleSource;

        _previewVehicleDefId = def.Id;
        _previewBodyColor = bodyColor;
        _previewRuntimeState = inst;
        _previewDefs = defs;

        _previewCanvas.SetVehicle(def, inst, bodyColor);

        if (snapshotInputsChanged)
            InvalidatePreviewSurface();

        SyncLiveVehiclePreview();
        EnsureEmbeddedPreviewViewport();
    }

    private void InvalidatePreviewSurface()
    {
        _previewBuildGeneration++;
        _previewRebuildPending = false;
        ClearEmbeddedPreview();

        if (_previewCanvas != null && GodotObject.IsInstanceValid(_previewCanvas))
        {
            _previewCanvas.SetSnapshotTexture(null);
            _previewCanvas.SetEmbeddedPreviewActive(false);
        }
    }

    private void EnsureEmbeddedPreviewViewport()
    {
        if (_previewCanvas == null || !GodotObject.IsInstanceValid(_previewCanvas))
            return;

        if (_embeddedPreviewViewport != null && GodotObject.IsInstanceValid(_embeddedPreviewViewport)
            && _embeddedPreviewPawn != null && GodotObject.IsInstanceValid(_embeddedPreviewPawn))
        {
            _previewCanvas.SetEmbeddedPreviewActive(true);
            UpdateEmbeddedPreviewOrientation();
            return;
        }

        if (_previewRebuildPending)
            return;

        var arenaVehicle = FindPlayerVehicleNode();
        if (arenaVehicle is not VehiclePawn liveVehicle || !GodotObject.IsInstanceValid(liveVehicle) || !liveVehicle.IsInsideTree())
            return;
        if (_previewDefs == null || _previewRuntimeState == null || string.IsNullOrWhiteSpace(_previewVehicleDefId))
            return;
        if (!_previewDefs.Vehicles.TryGetValue(_previewVehicleDefId!, out var previewDef))
            return;

        _previewVehicleSource = liveVehicle;
        _previewRebuildPending = true;
        var generation = ++_previewBuildGeneration;
        _ = BuildEmbeddedPreviewAsync(liveVehicle, previewDef, _previewRuntimeState, _previewDefs, _previewBodyColor, generation);
    }

    private async Task BuildEmbeddedPreviewAsync(VehiclePawn liveVehicle, VehicleDefinition _def, VehicleInstanceState runtime, DefDatabase defs, Color bodyColor, int generation)
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            if (generation != _previewBuildGeneration)
                return;
            if (_previewCanvas == null || !GodotObject.IsInstanceValid(_previewCanvas))
                return;
            if (!GodotObject.IsInstanceValid(liveVehicle) || !liveVehicle.IsInsideTree())
                return;

            var viewport = BuildEmbeddedPreviewViewport(liveVehicle, _def, runtime, defs, bodyColor);
            if (viewport == null)
            {
                GD.PrintErr("[VehicleStatusHud] Preview build skipped: failed to create embedded preview viewport.");
                return;
            }

            if (generation != _previewBuildGeneration)
            {
                viewport.QueueFree();
                return;
            }

            InstallEmbeddedPreviewViewport(viewport);

            // Configure the loadout only now that the viewport subtree is INSIDE the scene tree
            // (install adds it under _previewCanvas). Off-tree configuration made every weapon
            // alignment ForceUpdateTransform call spam native "!is_inside_tree()" errors.
            // Kept synchronous with the install so the generation guard still covers it.
            if (_embeddedPreviewPawn != null && GodotObject.IsInstanceValid(_embeddedPreviewPawn))
            {
                _embeddedPreviewPawn.ConfigureLoadout(defs, runtime);
                _embeddedPreviewPawn.SetRuntimeState(runtime);
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            if (generation != _previewBuildGeneration)
                return;

            RemovePreviewPawnAudio();
            if (_embeddedPreviewViewport != null && GodotObject.IsInstanceValid(_embeddedPreviewViewport)
                && (_embeddedPreviewViewport.GetCamera3D() == null || _embeddedPreviewViewport.FindWorld3D() == null))
            {
                GD.PrintErr("[VehicleStatusHud] Preview viewport missing active camera or World3D after install.");
            }
            if (_embeddedPreviewPawn != null && GodotObject.IsInstanceValid(_embeddedPreviewPawn))
            {
                _embeddedPreviewPawn.Velocity = Vector3.Zero;
                _embeddedPreviewPawn.ClearControlIntent();
                _embeddedPreviewPawn.SetPhysicsProcess(false);
                _embeddedPreviewPawn.SetProcess(false);
            }

            FitEmbeddedPreviewCamera();
            UpdateEmbeddedPreviewOrientation();

            if (_previewCanvas != null && GodotObject.IsInstanceValid(_previewCanvas))
                _previewCanvas.SetEmbeddedPreviewActive(true);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[VehicleStatusHud] Embedded preview build failed: {ex}");
        }
        finally
        {
            if (generation == _previewBuildGeneration)
                _previewRebuildPending = false;
        }
    }

    private static Node3D? GetPreviewVisualNode(Node3D arenaVehicle)
    {
        if (arenaVehicle.GetNodeOrNull<Node3D>("Visual") is { } visual)
            return visual;
        return arenaVehicle;
    }

    private void InstallEmbeddedPreviewViewport(SubViewport viewport)
    {
        ClearEmbeddedPreview();

        if (_previewCanvas == null || !GodotObject.IsInstanceValid(_previewCanvas))
        {
            viewport.QueueFree();
            return;
        }

        // ZIndex contract: the presenter covers the canvas at ZIndex 1; VehiclePreviewCanvas parks its
        // damage readout overlay (facing ring + tire dots + pips) at ZIndex 2 so locational damage still
        // reads over the live 3D render. Damage data flows to that overlay via _previewCanvas.SetVehicle(...).
        var presenter = new TextureRect
        {
            Name = "LiveVehiclePreviewPresenter",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = viewport.GetTexture(),
            ZIndex = 1,
        };
        presenter.AnchorLeft = 0f;
        presenter.AnchorTop = 0f;
        presenter.AnchorRight = 1f;
        presenter.AnchorBottom = 1f;
        presenter.OffsetLeft = 8f;
        presenter.OffsetTop = 8f;
        presenter.OffsetRight = -8f;
        presenter.OffsetBottom = -8f;
        presenter.Set("expand_mode", 1);
        presenter.Set("stretch_mode", 5);

        _previewCanvas.AddChild(viewport);
        _previewCanvas.AddChild(presenter);

        _embeddedPreviewPresenter = presenter;
        _embeddedPreviewViewport = viewport;
        _embeddedPreviewRoot = viewport.GetNodeOrNull<Node3D>("PreviewRoot");
        _embeddedPreviewPawn = viewport.GetNodeOrNull<VehiclePawn>("PreviewRoot/PreviewVehicle");
        _embeddedPreviewCamera = viewport.GetNodeOrNull<Camera3D>("PreviewRoot/PreviewCamera");

        if (_previewCanvas != null && GodotObject.IsInstanceValid(_previewCanvas))
            _previewCanvas.SetEmbeddedPreviewActive(true);

        GD.Print($"[VehicleStatusHud] Preview installed. ownWorld={viewport.OwnWorld3D} camera={_embeddedPreviewCamera != null} pawn={_embeddedPreviewPawn != null} texture={presenter.Texture != null}");
        UpdateEmbeddedPreviewOrientation();
    }

    private void ClearEmbeddedPreview()
    {
        if (_embeddedPreviewPresenter != null && GodotObject.IsInstanceValid(_embeddedPreviewPresenter))
            _embeddedPreviewPresenter.QueueFree();
        if (_embeddedPreviewViewport != null && GodotObject.IsInstanceValid(_embeddedPreviewViewport))
            _embeddedPreviewViewport.QueueFree();

        _embeddedPreviewPresenter = null;
        _embeddedPreviewViewport = null;
        _embeddedPreviewRoot = null;
        _embeddedPreviewPawn = null;
        _embeddedPreviewCamera = null;

        if (_previewCanvas != null && GodotObject.IsInstanceValid(_previewCanvas))
            _previewCanvas.SetEmbeddedPreviewActive(false);
    }

    private void UpdateEmbeddedPreviewOrientation()
    {
        if (_embeddedPreviewRoot == null || !GodotObject.IsInstanceValid(_embeddedPreviewRoot))
            return;
        if (_previewVehicleSource == null || !GodotObject.IsInstanceValid(_previewVehicleSource))
            return;

        _embeddedPreviewRoot.Rotation = new Vector3(0f, -_previewVehicleSource.GlobalRotation.Y, 0f);
    }

    private static bool ColorsClose(Color a, Color b)
    {
        return Mathf.Abs(a.R - b.R) < 0.001f
            && Mathf.Abs(a.G - b.G) < 0.001f
            && Mathf.Abs(a.B - b.B) < 0.001f
            && Mathf.Abs(a.A - b.A) < 0.001f;
    }

    private static SubViewport? BuildEmbeddedPreviewViewport(VehiclePawn liveVehicle, VehicleDefinition _def, VehicleInstanceState runtime, DefDatabase defs, Color bodyColor)
    {
        var viewport = new SubViewport
        {
            Name = "VehiclePreviewViewport",
            Size = new Vector2I(320, 320),
            TransparentBg = true,
            OwnWorld3D = true,
            Disable3D = false,
            GuiDisableInput = true,
            HandleInputLocally = false,
            PhysicsObjectPicking = false,
        };
        viewport.World3D = new World3D();
        viewport.Set("render_target_update_mode", 4);

        var root = new Node3D { Name = "PreviewRoot" };
        viewport.AddChild(root);

        var previewPawn = InstantiatePreviewPawn(liveVehicle);
        if (previewPawn == null)
            return null;

        previewPawn.Name = "PreviewVehicle";
        previewPawn.AddToGroup("player_vehicle");
        previewPawn.BodyColor = bodyColor;
        previewPawn.Position = Vector3.Zero;
        previewPawn.Rotation = Vector3.Zero;
        root.AddChild(previewPawn);
        // NOTE: ConfigureLoadout is deliberately NOT called here. Weapon alignment calls
        // ForceUpdateTransform, which logs native "!is_inside_tree()" errors while this viewport
        // is still off-tree. BuildEmbeddedPreviewAsync configures the loadout right after
        // InstallEmbeddedPreviewViewport puts the subtree in the scene tree (mirrors the
        // "Enter the tree BEFORE the pawn configures its loadout" fix in VehiclePortraitViewport).

        var cam = new Camera3D
        {
            Name = "PreviewCamera",
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 4.5f,
            Near = 0.05f,
            Far = 100f,
            Position = new Vector3(0f, 7f, 0f),
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            Current = true,
        };
        root.AddChild(cam);

        var sun = new DirectionalLight3D
        {
            Name = "PreviewLight",
            LightEnergy = 2.4f,
            ShadowEnabled = false,
            RotationDegrees = new Vector3(-65f, 25f, 0f),
        };
        root.AddChild(sun);

        var fill = new OmniLight3D
        {
            Name = "PreviewFill",
            LightEnergy = 1.25f,
            ShadowEnabled = false,
            Position = new Vector3(0f, 4f, 0f),
        };
        root.AddChild(fill);

        return viewport;
    }

    private static VehiclePawn? InstantiatePreviewPawn(VehiclePawn sourceVehicle)
    {
        VehiclePawn? previewPawn = null;
        try
        {
            var scene = GD.Load<PackedScene>("res://Scenes/Arena/VehiclePawn.tscn");
            if (scene != null)
                previewPawn = scene.Instantiate<VehiclePawn>();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[VehicleStatusHud] Preview pawn instantiate failed: {ex.Message}");
        }

        previewPawn ??= new VehiclePawn();
        CopyPreviewVisualConfig(sourceVehicle, previewPawn);
        return previewPawn;
    }

    private static void CopyPreviewVisualConfig(VehiclePawn source, VehiclePawn target)
    {
        target.UsePassengerCarPackVisual = source.UsePassengerCarPackVisual;
        target.PassengerCarPackScenePath = source.PassengerCarPackScenePath;
        target.PassengerCarPackBodyNodeName = source.PassengerCarPackBodyNodeName;
        target.PassengerCarPackWheelRootPrefix = source.PassengerCarPackWheelRootPrefix;
        target.PassengerCarPackPlayerVariantIndex = source.PassengerCarPackPlayerVariantIndex;
        target.PassengerCarPackEnemyVariantIndex = source.PassengerCarPackEnemyVariantIndex;
        target.PassengerCarPackTargetLength = source.PassengerCarPackTargetLength;
        target.PassengerCarPackMinVisibleLength = source.PassengerCarPackMinVisibleLength;
        target.PassengerCarPackAutoAlignYaw = source.PassengerCarPackAutoAlignYaw;
        target.PassengerCarPackDebugAlignment = source.PassengerCarPackDebugAlignment;
        target.PassengerCarPackRotationDegrees = source.PassengerCarPackRotationDegrees;
        target.PassengerCarPackExtraScale = source.PassengerCarPackExtraScale;
    }

    private void RemovePreviewPawnAudio()
    {
        if (_embeddedPreviewPawn == null || !GodotObject.IsInstanceValid(_embeddedPreviewPawn))
            return;

        if (_embeddedPreviewPawn.GetNodeOrNull<Node>("EngineAudio") is Node audio && GodotObject.IsInstanceValid(audio))
            audio.QueueFree();
    }

    private void FitEmbeddedPreviewCamera()
    {
        if (_embeddedPreviewCamera == null || !GodotObject.IsInstanceValid(_embeddedPreviewCamera))
            return;
        if (_embeddedPreviewRoot == null || !GodotObject.IsInstanceValid(_embeddedPreviewRoot))
            return;
        if (_embeddedPreviewPawn == null || !GodotObject.IsInstanceValid(_embeddedPreviewPawn))
            return;

        var previewVisual = GetPreviewVisualNode(_embeddedPreviewPawn) ?? (Node3D)_embeddedPreviewPawn;
        if (!TryComputeAabbInSpace(previewVisual, _embeddedPreviewRoot, out var bounds))
            return;

        var center = bounds.Position + bounds.Size * 0.5f;
        var radius = MathF.Max(bounds.Size.X, bounds.Size.Z) * 0.62f + 0.35f;
        _embeddedPreviewCamera.Size = MathF.Max(1.6f, radius * 2.1f);
        _embeddedPreviewCamera.Position = center + new Vector3(0f, MathF.Max(5.5f, bounds.Size.Y + 4.0f), 0f);
        _embeddedPreviewCamera.RotationDegrees = new Vector3(-90f, 0f, 0f);
    }

    private static bool TryComputeAabbInSpace(Node root, Node3D space, out Aabb aabb)
    {
        aabb = default;
        var first = true;
        var invSpace = space.GlobalTransform.AffineInverse();
        var stack = new System.Collections.Generic.Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            foreach (var childObj in n.GetChildren())
            {
                if (childObj is Node child)
                    stack.Push(child);
            }

            // This helper is also used for off-tree duplicated preview models.
            // `IsVisibleInTree()` stays false until the duplicated viewport subtree is attached to the scene tree,
            // which made the one-shot HUD portrait capture bail out before a snapshot was ever taken.
            // Respect the mesh's own Visible flag, but do not require tree visibility here.
            if (n is not MeshInstance3D mi || mi.Mesh == null || !mi.Visible)
                continue;

            var rel = invSpace * mi.GlobalTransform;
            var meshAabb = TransformAabb(mi.GetAabb(), rel);
            if (first)
            {
                aabb = meshAabb;
                first = false;
            }
            else
            {
                aabb = aabb.Merge(meshAabb);
            }
        }

        return !first;
    }

    private static Aabb TransformAabb(Aabb localAabb, Transform3D xform)
    {
        var p = localAabb.Position;
        var s = localAabb.Size;
        var corners = new Vector3[8]
        {
            p,
            p + new Vector3(s.X, 0f, 0f),
            p + new Vector3(0f, s.Y, 0f),
            p + new Vector3(0f, 0f, s.Z),
            p + new Vector3(s.X, s.Y, 0f),
            p + new Vector3(s.X, 0f, s.Z),
            p + new Vector3(0f, s.Y, s.Z),
            p + new Vector3(s.X, s.Y, s.Z),
        };

        var firstPoint = TransformPoint(xform, corners[0]);
        var min = firstPoint;
        var max = firstPoint;
        for (var i = 1; i < corners.Length; i++)
        {
            var w = TransformPoint(xform, corners[i]);
            min = new Vector3(Mathf.Min(min.X, w.X), Mathf.Min(min.Y, w.Y), Mathf.Min(min.Z, w.Z));
            max = new Vector3(Mathf.Max(max.X, w.X), Mathf.Max(max.Y, w.Y), Mathf.Max(max.Z, w.Z));
        }

        return new Aabb(min, max - min);
    }

    private static Vector3 TransformPoint(Transform3D t, Vector3 v)
        => t.Origin + t.Basis * v;

    // ---------------------------------------------------------------------------------------------
    // Weapon slots: one compact row per installed weapon (glyph, name, ammo counter, cooldown sweep).
    // Rows rebuild only when the loadout signature changes; per-tick updates touch labels/colors only.
    // Slot order = mounts sorted by mount id (same OrderBy the old text list used).
    // ---------------------------------------------------------------------------------------------
    private sealed class WeaponSlotRow
    {
        public string MountId = "";
        public string BaseName = "";
        public Control Root = null!;
        public Label Name = null!;
        public Label Ammo = null!;
        public CooldownSweepBar Cooldown = null!;
        public bool Offline;
    }

    private VBoxContainer? _weaponSlotsBox;
    private Label? _lblNoWeapons;
    private readonly List<WeaponSlotRow> _weaponSlots = new();
    private string _weaponSlotsSignature = string.Empty;

    private static readonly Color AmmoLowColor = new(0.98f, 0.72f, 0.18f);
    private static readonly Color AmmoOutColor = new(1.00f, 0.28f, 0.22f);

    /// <summary>Number of weapon slot rows currently shown (mounts ordered by mount id).</summary>
    public int WeaponSlotCount => _weaponSlots.Count;

    /// <summary>Mount id backing a slot row, or null when out of range. Use to wire cooldown feeds robustly.</summary>
    public string? GetWeaponSlotMountId(int slotIndex)
        => slotIndex >= 0 && slotIndex < _weaponSlots.Count ? _weaponSlots[slotIndex].MountId : null;

    /// <summary>
    /// Drive the thin cooldown sweep under a weapon slot. slotIndex follows the HUD's slot order
    /// (mounts sorted by mount id — see <see cref="GetWeaponSlotMountId"/>). fraction01: 0 = just
    /// fired, 1 = ready to fire. Cheap to call every frame; only repaints on visible change.
    /// </summary>
    public void SetWeaponCooldown01(int slotIndex, float fraction01)
    {
        if (slotIndex < 0 || slotIndex >= _weaponSlots.Count)
            return;

        var bar = _weaponSlots[slotIndex].Cooldown;
        if (bar != null && GodotObject.IsInstanceValid(bar))
            bar.SetFraction(fraction01);
    }

    private void UpdateWeaponSlots(VehicleInstanceState inst, DefDatabase defs)
    {
        if (_weaponSlotsBox == null || !GodotObject.IsInstanceValid(_weaponSlotsBox))
            return;

        if (inst.InstalledWeaponsByMountId.Count == 0)
        {
            if (_weaponSlots.Count > 0)
                ClearWeaponSlotRows();

            if (_lblNoWeapons == null || !GodotObject.IsInstanceValid(_lblNoWeapons))
            {
                _lblNoWeapons = new Label { Text = "(no weapons)" };
                _lblNoWeapons.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 4);
                _lblNoWeapons.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
                _weaponSlotsBox.AddChild(_lblNoWeapons);
            }
            _lblNoWeapons.Visible = true;
            return;
        }

        if (_lblNoWeapons != null && GodotObject.IsInstanceValid(_lblNoWeapons))
            _lblNoWeapons.Visible = false;

        var signature = BuildLoadoutSignature(inst);
        if (signature != _weaponSlotsSignature)
        {
            RebuildWeaponSlotRows(inst, defs);
            _weaponSlotsSignature = signature;
        }

        var i = 0;
        foreach (var kvp in inst.InstalledWeaponsByMountId.OrderBy(k => k.Key))
        {
            if (i >= _weaponSlots.Count)
                break;
            UpdateWeaponSlotRow(_weaponSlots[i++], kvp.Value, inst, defs);
        }
    }

    private static string BuildLoadoutSignature(VehicleInstanceState inst)
        => string.Join("|", inst.InstalledWeaponsByMountId.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value.WeaponId}"));

    private void ClearWeaponSlotRows()
    {
        foreach (var row in _weaponSlots)
        {
            if (row.Root != null && GodotObject.IsInstanceValid(row.Root))
                row.Root.QueueFree();
        }
        _weaponSlots.Clear();
        _weaponSlotsSignature = string.Empty;
    }

    private void RebuildWeaponSlotRows(VehicleInstanceState inst, DefDatabase defs)
    {
        ClearWeaponSlotRows();
        if (_weaponSlotsBox == null || !GodotObject.IsInstanceValid(_weaponSlotsBox))
            return;

        VehicleDefinition? vdef = null;
        if (!string.IsNullOrWhiteSpace(_previewVehicleDefId))
            defs.Vehicles.TryGetValue(_previewVehicleDefId!, out vdef);

        foreach (var kvp in inst.InstalledWeaponsByMountId.OrderBy(k => k.Key))
        {
            var mountId = kvp.Key;
            defs.Weapons.TryGetValue(kvp.Value.WeaponId, out var wdef);
            var baseName = wdef?.DisplayName ?? kvp.Value.WeaponId;

            var root = new HBoxContainer { Name = $"Slot_{mountId}" };
            root.AddThemeConstantOverride("separation", 6);

            var location = vdef?.MountPoints
                .FirstOrDefault(m => string.Equals(m.MountId, mountId, StringComparison.OrdinalIgnoreCase))
                ?.MountLocation ?? MountLocation.Front;
            var icon = new TextureRect
            {
                CustomMinimumSize = new Vector2(18f, 18f),
                Texture = GeneratedUiArt.LoadMountIcon(location),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            icon.Set("expand_mode", 1);
            icon.Set("stretch_mode", 5);
            root.AddChild(icon);

            var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            col.AddThemeConstantOverride("separation", 2);

            var nameRow = new HBoxContainer();
            nameRow.AddThemeConstantOverride("separation", 6);
            var lblName = new Label
            {
                Text = baseName,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ClipText = true,
            };
            lblName.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 4);
            lblName.AddThemeColorOverride("font_color", GameUiTheme.TextColor);
            var lblAmmo = new Label { Text = "--", HorizontalAlignment = HorizontalAlignment.Right };
            lblAmmo.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 4);
            nameRow.AddChild(lblName);
            nameRow.AddChild(lblAmmo);

            var cooldown = new CooldownSweepBar { CustomMinimumSize = new Vector2(0f, 3f) };

            col.AddChild(nameRow);
            col.AddChild(cooldown);
            root.AddChild(col);
            _weaponSlotsBox.AddChild(root);

            _weaponSlots.Add(new WeaponSlotRow
            {
                MountId = mountId,
                BaseName = baseName,
                Root = root,
                Name = lblName,
                Ammo = lblAmmo,
                Cooldown = cooldown,
            });
        }
    }

    private static void UpdateWeaponSlotRow(WeaponSlotRow row, InstalledWeaponState w, VehicleInstanceState inst, DefDatabase defs)
    {
        defs.Weapons.TryGetValue(w.WeaponId, out var wdef);
        var ammoId = w.SelectedAmmoId;
        if (string.IsNullOrWhiteSpace(ammoId) && wdef != null && wdef.AmmoTypeIds.Length > 0)
            ammoId = wdef.AmmoTypeIds[0];

        var ammoCount = 0;
        if (!string.IsNullOrWhiteSpace(ammoId))
            inst.AmmoInventory.TryGetValue(ammoId!, out ammoCount);

        var (ammoText, ammoColor) = CompactAmmoStatus(defs, ammoId, ammoCount);
        if (row.Ammo.Text != ammoText)
            row.Ammo.Text = ammoText;
        row.Ammo.AddThemeColorOverride("font_color", ammoColor);

        // Fixed-angle fallback (master spec): weapons beyond the computer's control-group cap are
        // not offline — they lock to a fixed bearing and fire opportunistically, so the row stays
        // readable and is tagged [FIXED] instead of dark.
        var degraded = !ArenaWeaponLoadoutResolver.IsWeaponComputerControlled(defs, inst, row.MountId, out _);
        if (degraded != row.Offline)
        {
            row.Offline = degraded;
            row.Root.Modulate = degraded ? new Color(1f, 1f, 1f, 0.70f) : Colors.White;
            row.Name.Text = degraded ? $"{row.BaseName}  [FIXED]" : row.BaseName;
        }
    }

    /// <summary>Compact counter with the same low/out policy as <see cref="FormatAmmoStatus"/>.</summary>
    private static (string Text, Color Color) CompactAmmoStatus(DefDatabase defs, string? ammoId, int ammoCount)
    {
        if (string.IsNullOrWhiteSpace(ammoId))
            return ("--", GameUiTheme.TextMutedColor);
        if (ammoCount <= 0)
            return ("OUT", AmmoOutColor);

        var kind = defs.Ammo.TryGetValue(ammoId!, out var adef) ? adef.AmmoKind : default;
        var (target, _) = GameBalance.GetAmmoRefillPolicy(kind);
        return ammoCount * 4 <= Math.Max(1, target)
            ? ($"{ammoCount} LOW", AmmoLowColor)
            : (ammoCount.ToString(), GameUiTheme.TextColor);
    }

    /// <summary>
    /// Ammo readout with low/empty emphasis. "Low" is kind-aware: a quarter of that ammo kind's
    /// workshop refill target (so 3 missiles can be fine while 3 MG rounds are nearly dry).
    /// Kept verbatim: ArenaRealtimeView's fallback HUD binder still renders the text list with it.
    /// </summary>
    internal static string FormatAmmoStatus(DefDatabase defs, string? ammoId, int ammoCount)
    {
        if (string.IsNullOrWhiteSpace(ammoId)) return "Ammo: n/a";
        if (ammoCount <= 0) return "Ammo: OUT";

        var kind = defs.Ammo.TryGetValue(ammoId!, out var adef) ? adef.AmmoKind : default;
        var (target, _) = GameBalance.GetAmmoRefillPolicy(kind);
        return ammoCount * 4 <= Math.Max(1, target) ? $"Ammo: {ammoCount} LOW" : $"Ammo: {ammoCount}";
    }

    /// <summary>
    /// 3px cooldown sweep under each weapon slot: amber while cycling, quiet cyan once ready.
    /// (Internal, not private: the Godot script registry references nested node types via typeof.)
    /// </summary>
    internal partial class CooldownSweepBar : Control
    {
        private float _fraction = 1f;

        private static readonly Color TrackColor = new(1f, 1f, 1f, 0.07f);
        private static readonly Color CyclingColor = new(0.95f, 0.72f, 0.18f, 0.95f);
        private static readonly Color ReadyColor = new(0.25f, 0.85f, 1f, 0.45f);

        public CooldownSweepBar()
        {
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public void SetFraction(float fraction01)
        {
            fraction01 = Mathf.Clamp(fraction01, 0f, 1f);
            if (Mathf.Abs(_fraction - fraction01) < 0.004f)
                return;

            _fraction = fraction01;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var size = Size;
            if (size.X < 4f || size.Y < 1f)
                return;

            DrawRect(new Rect2(Vector2.Zero, size), TrackColor, true);
            if (_fraction <= 0.001f)
                return;

            var ready = _fraction >= 0.999f;
            DrawRect(new Rect2(Vector2.Zero, new Vector2(size.X * _fraction, size.Y)), ready ? ReadyColor : CyclingColor, true);
        }
    }

    private void UpdateSpeedAndRpm(float speedCur, float speedMax, float rpm01, int rpmValue, string gearDisplay)
    {
        // Instrument styling (eval round 5 P1): segmented tick gauges with live color sweeps
        // instead of two identical flat gold rectangles.
        _speedBar.GaugeTicks = 10;
        _rpmBar.GaugeTicks = 8;

        // Speed in km/h reads like a real gauge instead of raw m/s fractions; the fill brightens
        // toward top speed so "flat out" is visible peripherally.
        var pct = Mathf.Clamp(speedCur / MathF.Max(0.01f, speedMax), 0f, 1f);
        var speedFill = new Color(0.72f, 0.58f, 0.16f).Lerp(new Color(1.00f, 0.93f, 0.50f), pct);
        _speedBar.SetCustom(pct, $"{speedCur * 3.6f:0} km/h", speedFill, Colors.White);

        // RPM sweeps like a tach: green through gold into a red top band near redline.
        rpm01 = Mathf.Clamp(rpm01, 0f, 1f);
        var rpmFill = rpm01 < 0.62f
            ? new Color(0.38f, 0.58f, 0.34f).Lerp(new Color(0.90f, 0.75f, 0.20f), rpm01 / 0.62f)
            : new Color(0.90f, 0.75f, 0.20f).Lerp(new Color(0.94f, 0.30f, 0.16f), (rpm01 - 0.62f) / 0.38f);
        // Compact "6,212 · G4" — the long "RPM · Gear 4" form clipped past the bar at redline.
        var txt = $"{rpmValue:#,0} · {(gearDisplay == "R" ? "REV" : "G" + gearDisplay)}";
        _rpmBar.SetCustom(rpm01, txt, rpmFill, rpm01 > 0.92f ? new Color(1f, 0.82f, 0.72f) : Colors.White);
    }

	private static string FormatKg(float kg)
	{
		kg = MathF.Max(0f, kg);
		// Simple readable formatting.
		if (kg >= 10000f) return $"{MathF.Round(kg):0} kg";
		if (kg >= 1000f) return $"{kg:0} kg";
		return $"{kg:0} kg";
	}
}
