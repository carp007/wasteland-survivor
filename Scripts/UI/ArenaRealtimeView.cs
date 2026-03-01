// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/ArenaRealtimeView.cs
// Purpose: Arena UI controller. Spawns the 3D ArenaWorld + pawns, runs realtime combat, binds HUD, and resolves/persists encounter outcomes.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WastelandSurvivor.Framework.SceneBinding;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game;
using WastelandSurvivor.Game.Arena;
using WastelandSurvivor.Game.Navigation;
using WastelandSurvivor.Game.Systems;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// 3D real-time arena prototype (2.5D camera). Uses the existing encounter/reward loop,
/// but runs combat in the shared 3D WorldRoot.
/// </summary>
public partial class ArenaRealtimeView : Control
{

	// -------------------------------------------------------------------------------------------------
	// File navigation (high level)
	// - _Ready(): scene wiring, HUD binding, resource loading
	// - Match lifecycle: StartMatch() -> Combat loop -> Post-match salvage -> ResolveAndCommit()
	// - Input: Vehicle mode vs Driver mode, Tab target cycling, Space fire, E enter/exit, G hold-to-exit salvage
	// - Persistence: commit runtime state back into GameSession via ResolveArenaEncounterRealtime()
	// -------------------------------------------------------------------------------------------------

	private PanelContainer _hudPanel = null!;
	private Label _lblStatus = null!;
	private Label _lblStats = null!;
	private TargetStatusHud _targetStatusHud = null!;
	private PlayerStatusHud _playerStatusHud = null!;
	// Vehicle HUD: the scene node is always a Control, but in rare cases Godot can fail to bind the managed
	// script type (VehicleStatusHud). We support both the scripted path and a robust fallback binder that
	// updates the UI controls directly (no cast required).
	private Control? _vehicleStatusHudRoot;
	private VehicleStatusHud? _vehicleStatusHud;
	private VehicleStatusHudFallback? _vehicleStatusHudFallback;
	private Label _lblMatchEnd = null!;
	private ActionPromptOverlay _actionPrompt = null!;

	private Button _btnTier1 = null!;
	private Button _btnTier2 = null!;
	private Button _btnTier3 = null!;
	private Button _btnStart = null!;
	private Button _btnFlee = null!;
	private Button _btnBack = null!;

	private PanelContainer _postPanel = null!;
	private Label _lblRewards = null!;
	private Button _btnRepair = null!;
	private Button _btnRepairDriverArmor = null!;
	private Button _btnPatchArmor = null!;
	private Button _btnPatchTire = null!;
	private Button _btnReturn = null!;
	private Button _btnCloseResults = null!;

	private int _selectedTier = 1;

	private Node3D? _worldRoot;
	private ArenaWorld? _arenaWorld;
	private PackedScene _worldScene = null!;
	private PackedScene _vehScene = null!;
	private PackedScene _driverScene = null!;

	private VehiclePawn? _playerPawn;
	private DriverPawn? _driverPawn;
	private Node3D? _playerControlledEntity;
	private PlayerControlMode _playerControlMode = PlayerControlMode.Vehicle;
	private DriverPawnConfig _driverCfg = DriverPawnConfig.Default();
	private VehiclePawn? _enemyPawn;

	private enum PlayerControlMode
	{
		Vehicle,
		Driver
	}
	private VehiclePawn? _selectedTarget;
	private TargetIndicator3D? _targetIndicator;
	private int _targetCycleIndex = 0;
	private float _enemyUnstuckRemaining = 0f;
	private float _hudDynamicRemaining = 0f;

	// On-foot: take damage when hit by vehicles (ram/run-over). Cooldown prevents per-frame draining.
	private float _driverCollisionCooldown = 0f;

	// Tuning (kept conservative; getting clipped at speed should still be very dangerous).
	private const float DriverCollisionProbeRadius = 0.55f;
	private const float DriverCollisionMinSpeed = 3.0f;
	private const float DriverCollisionCooldownSeconds = 0.45f;
	private const float DriverCollisionDamageBase = 2.0f;
	private const float DriverCollisionDamageSpeedSqFactor = 0.15f;


	// UI feedback (hit marker + SFX)
	private HitMarkerOverlay? _hitMarker;
	private AudioStreamPlayer? _sfxPlayer;
	private AudioStream? _sfxHit;
	private AudioStream? _sfxTirePop;
	private readonly Dictionary<string, WeaponSfx> _weaponSfx = new(StringComparer.OrdinalIgnoreCase);

	private sealed class WeaponSfx
	{
		public AudioStream? Fire;
		public float FireVolumeDb = -4.0f;
		public AudioStream? HitVehicle;
		public float HitVehicleVolumeDb = -3.0f;
		public AudioStream? HitWorld;
		public float HitWorldVolumeDb = -4.0f;
	}

	// Runtime combat state; commit only on resolve.
	private VehicleInstanceState? _playerVehicleRuntime;
	private VehicleInstanceState? _enemyVehicleRuntime;
	private int _playerHpRuntime = 0;
	private int _playerHpMaxRuntime = 0;
	private int _playerArmorRuntime = 0;
	private int _playerArmorMaxRuntime = 0;
	private int _enemyHpRuntime = 0; // enemy driver HP
	private int _enemyHpMaxRuntime = 50;
	private int _enemyArmorRuntime = 0;
	private int _enemyArmorMaxRuntime = 50;
	private int _enemyAmmoRuntime = 0;
	private bool _combatLive = false;
	private bool _postMatchAwaitingExit = false;
	private string _postMatchOutcome = "";
	// When true, player controls stay disabled (e.g., player died this match but driver HP may
	// be restored by save/session logic).
	private bool _playerKilledThisMatch = false;
	private bool _enemyKilledThisMatch = false;
	private float _exitHoldSeconds = 0f;
	private readonly List<string> _runtimeLog = new();

	// Mines (runtime hazards)
	private readonly List<MineRuntime> _mines = new();
	private int _mineSeq = 0;


	// Tuning
	private const float PlayerSpreadRad = 0.045f;
	private const float EnemySpreadRad = 0.09f;
	private const float EnemyPreferredRange = 22f;
	private const float ExitHoldSecondsRequired = 3.0f;
	private void EnsureBound()
	{
		var b = new SceneBinder(this, nameof(ArenaRealtimeView));

		_hudPanel = b.Req<PanelContainer>("HudPanel");
		_lblStatus = b.Req<Label>("HudPanel/VBox/LblStatus");
		_lblStats = b.Req<Label>("HudPanel/VBox/LblStats");
		_targetStatusHud = b.Req<TargetStatusHud>("TargetStatusHud");
		_playerStatusHud = b.Req<PlayerStatusHud>("PlayerStatusHud");

		var vehicleHudNode = b.Opt<Node>("VehicleStatusHud");
		_vehicleStatusHudRoot = vehicleHudNode as Control;
		_vehicleStatusHud = _vehicleStatusHudRoot as VehicleStatusHud;
		if (_vehicleStatusHudRoot != null && GodotObject.IsInstanceValid(_vehicleStatusHudRoot))
		{
			// Never show the vehicle HUD in the pre-fight dialog state.
			_vehicleStatusHudRoot.Visible = false;
			// If the managed script isn't bound, bind a fallback updater so the HUD still works.
			if (_vehicleStatusHud == null)
				_vehicleStatusHudFallback = new VehicleStatusHudFallback(_vehicleStatusHudRoot);
		}

		_lblMatchEnd = b.Req<Label>("LblMatchEnd");
		_actionPrompt = b.Req<ActionPromptOverlay>("ActionPromptOverlay");

		_btnTier1 = b.Req<Button>("HudPanel/VBox/HBoxTiers/BtnTier1");
		_btnTier2 = b.Req<Button>("HudPanel/VBox/HBoxTiers/BtnTier2");
		_btnTier3 = b.Req<Button>("HudPanel/VBox/HBoxTiers/BtnTier3");
		_btnStart = b.Req<Button>("HudPanel/VBox/HBoxActions/BtnStart");
		_btnFlee = b.Req<Button>("HudPanel/VBox/HBoxActions/BtnFlee");
		_btnBack = b.Req<Button>("HudPanel/VBox/HBoxActions/BtnBack");

		_postPanel = b.Req<PanelContainer>("PostPanel");
		_lblRewards = b.Req<Label>("PostPanel/PostVBox/LblRewards");
		_btnRepair = b.Req<Button>("PostPanel/PostVBox/HBoxPost/BtnRepair");
		_btnRepairDriverArmor = b.Req<Button>("PostPanel/PostVBox/HBoxPost/BtnRepairDriverArmor");
		_btnReturn = b.Req<Button>("PostPanel/PostVBox/HBoxPost/BtnReturn");
		_btnCloseResults = b.Req<Button>("PostPanel/PostVBox/HBoxPost/BtnCloseResults");
		_btnPatchArmor = b.Req<Button>("PostPanel/PostVBox/HBoxScrap/BtnPatchArmor");
		_btnPatchTire = b.Req<Button>("PostPanel/PostVBox/HBoxScrap/BtnPatchTire");
	}


	public override void _Ready()
	{

		GameUiTheme.ApplyToTree(this);
		EnsureBound();

		_btnTier1.Pressed += () => SelectTier(1);
		_btnTier2.Pressed += () => SelectTier(2);
		_btnTier3.Pressed += () => SelectTier(3);
		_btnStart.Pressed += StartEncounter;
		_btnFlee.Pressed += Flee;
		_btnBack.Pressed += Back;
		_btnRepair.Pressed += QuickRepair;
		_btnRepairDriverArmor.Pressed += RepairDriverArmor;
		_btnPatchArmor.Pressed += PatchArmor;
		_btnPatchTire.Pressed += PatchTire;
		_btnReturn.Pressed += ReturnToCity;
		_btnCloseResults.Pressed += CloseResults;

		// Note: Resource loads can return null if paths are wrong or imports are stale.
		// We defensively (re)load again later in EnsureWorld/SpawnActors as well.
		_worldScene = GD.Load<PackedScene>("res://Scenes/Arena/ArenaWorld.tscn");
		_vehScene = GD.Load<PackedScene>("res://Scenes/Arena/VehiclePawn.tscn");
		_driverScene = GD.Load<PackedScene>("res://Scenes/Arena/DriverPawn.tscn");

		EnsureInputActions();
		_driverCfg = DriverPawnConfigStore.Instance.Get();
		EnsureSfx();
		EnsureHitMarker();
		_lblMatchEnd.Visible = false;
		_actionPrompt.Hide();
			// Don't spawn the world until the user presses Start; avoids any timing issues
			// with WorldRoot availability when opening this view.
		SelectTier(1);
		ResetUi();
		RefreshArenaDialogVisibility();

		// If the player quit/crashed mid-encounter, the save can still have an active encounter.
		// Don't block them with "already active"; let them resume.
		var session = Session();
		if (session?.HasActiveEncounter() == true)
		{
			var enc = session.GetCurrentEncounter();
			if (enc != null)
				_lblStatus.Text = $"Status: Active encounter found (tier {enc.Tier}). Press Start to resume.";
		}
	

	}

	private VehicleStatusHud? ResolveVehicleStatusHud()
	{
		// Normal case: the scene instantiates VehicleStatusHud.tscn and this is already the correct scripted type.
		Node? raw = GetNodeOrNull<Node>("VehicleStatusHud");
		if (raw is VehicleStatusHud direct)
			return direct;

		// If the node exists but isn't the scripted type, scan descendants (covers wrapper containers).
		VehicleStatusHud? FindIn(Node root)
		{
			foreach (var c in root.GetChildren())
			{
				if (c is VehicleStatusHud v) return v;
				if (c is Node n)
				{
					var d = FindIn(n);
					if (d != null) return d;
				}
			}
			return null;
		}

		var found = raw != null ? FindIn(raw) : FindIn(this);
		if (found != null)
			return found;

		// Last resort: replace the placeholder control with a freshly instanced, typed VehicleStatusHud.
		return ReplaceVehicleHudPlaceholder(raw as Control);
	}

	private VehicleStatusHud? ReplaceVehicleHudPlaceholder(Control? placeholder)
	{
		try
		{
			var scene = GD.Load<PackedScene>(GameScenes.VehicleStatusHud);
			if (scene == null)
			{
				GD.PrintErr("[ArenaRealtimeView] VehicleStatusHud scene could not be loaded.");
				return null;
			}

			VehicleStatusHud? instHud = null;
			try
			{
				// Using the generic Instantiate<T> is the most reliable way to ensure the managed script is attached.
				instHud = scene.Instantiate<VehicleStatusHud>();
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ArenaRealtimeView] VehicleStatusHud Instantiate<VehicleStatusHud>() failed: {ex.Message}");
			}

			if (instHud == null)
			{
				// Fallback: instantiate untyped and try to locate the scripted node inside (shouldn't happen, but fail soft).
				var inst = scene.Instantiate();
				instHud = inst as VehicleStatusHud;
				if (instHud == null)
					instHud = FindVehicleHudIn(inst);
			}

			if (instHud == null)
			{
				GD.PrintErr("[ArenaRealtimeView] VehicleStatusHud replacement failed: instantiated scene did not contain VehicleStatusHud script.");
				// Hide the placeholder so the user doesn't see 0/0 bars.
				if (placeholder != null && GodotObject.IsInstanceValid(placeholder))
					placeholder.Visible = false;
				return null;
			}

			instHud.Name = "VehicleStatusHud";
			instHud.Visible = false; // will be enabled by RefreshStats when we have a live vehicle

			if (placeholder != null && GodotObject.IsInstanceValid(placeholder) && placeholder.GetParent() is Node parent)
			{
				if (instHud is Control instCtrl)
				{
					CopyControlLayout(placeholder, instCtrl);
					instCtrl.Visible = placeholder.Visible;
				}

				var idx = parent.GetChildren().IndexOf(placeholder);
				parent.AddChild(instHud);
				if (idx >= 0)
					parent.MoveChild(instHud, idx);

				placeholder.QueueFree();
			}
			else
			{
				AddChild(instHud);
			}

			GD.Print("[ArenaRealtimeView] VehicleStatusHud replaced with a fresh typed instance.");
			return instHud;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[ArenaRealtimeView] VehicleStatusHud replacement failed: {ex}");
			if (placeholder != null && GodotObject.IsInstanceValid(placeholder))
				placeholder.Visible = false;
			return null;
		}
	}

	private static VehicleStatusHud? FindVehicleHudIn(Node root)
	{
		foreach (var c in root.GetChildren())
		{
			if (c is VehicleStatusHud v) return v;
			if (c is Node n)
			{
				var d = FindVehicleHudIn(n);
				if (d != null) return d;
			}
		}
		return null;
	}

	private static void CopyControlLayout(Control src, Control dst)
	{
		// Copy the essential layout properties so the repaired instance stays in the same spot.
		dst.LayoutMode = src.LayoutMode;
		dst.AnchorLeft = src.AnchorLeft;
		dst.AnchorTop = src.AnchorTop;
		dst.AnchorRight = src.AnchorRight;
		dst.AnchorBottom = src.AnchorBottom;
		dst.OffsetLeft = src.OffsetLeft;
		dst.OffsetTop = src.OffsetTop;
		dst.OffsetRight = src.OffsetRight;
		dst.OffsetBottom = src.OffsetBottom;
		dst.GrowHorizontal = src.GrowHorizontal;
		dst.GrowVertical = src.GrowVertical;
	}

	private void EnsureScenesLoaded()
	{
		if (_worldScene == null || !GodotObject.IsInstanceValid(_worldScene))
			_worldScene = GD.Load<PackedScene>("res://Scenes/Arena/ArenaWorld.tscn");
		if (_vehScene == null || !GodotObject.IsInstanceValid(_vehScene))
			_vehScene = GD.Load<PackedScene>("res://Scenes/Arena/VehiclePawn.tscn");
		if (_driverScene == null || !GodotObject.IsInstanceValid(_driverScene))
			_driverScene = GD.Load<PackedScene>("res://Scenes/Arena/DriverPawn.tscn");
	}

	private void EnsureSfx()
	{
		if (_sfxPlayer != null && GodotObject.IsInstanceValid(_sfxPlayer)) return;
		_sfxPlayer = new AudioStreamPlayer { Name = "Sfx", Bus = "SFX" };
		// A little quieter than default so it doesn't get annoying.
		_sfxPlayer.VolumeDb = -6.0f;
		AddChild(_sfxPlayer);

		// These assets are expected to exist in res://Assets/Audio. (You told me you won't delete Assets when updating.)
		_sfxHit = GD.Load<AudioStream>("res://Assets/Audio/ui_hit.wav");
		_sfxTirePop = GD.Load<AudioStream>("res://Assets/Audio/tire_pop.wav");
	}

	private void EnsureHitMarker()
	{
		if (_hitMarker != null && GodotObject.IsInstanceValid(_hitMarker)) return;
		_hitMarker = new HitMarkerOverlay { Name = "HitMarkerOverlay" };
		AddChild(_hitMarker);
		_hitMarker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_hitMarker.MouseFilter = MouseFilterEnum.Ignore;
	}

	private void PlaySfx(AudioStream? stream)
	{
		if (stream == null) return;
		if (_sfxPlayer == null || !GodotObject.IsInstanceValid(_sfxPlayer)) return;
		_sfxPlayer.Stream = stream;
		_sfxPlayer.Play();
	}

	private void PlaySfx3D(AudioStream? stream, Vector3 atWorld, float volumeDb = -4.0f)
	{
		if (stream == null) return;
		if (_arenaWorld == null || !GodotObject.IsInstanceValid(_arenaWorld)) return;
		var root = _arenaWorld.GetVfxRoot();
		var p = new AudioStreamPlayer3D
		{
			Name = "Sfx3D",
			Stream = stream,
			Bus = "SFX",
			VolumeDb = volumeDb,
			GlobalPosition = atWorld,
			UnitSize = 8.0f,
			MaxDistance = 60.0f,
		};
		root.AddChild(p);
		p.Play();
		// Auto-free after a short time. (AudioStream length isn't reliable for all formats.)
		try
		{
			var t = GetTree()?.CreateTimer(2.5f);
			if (t != null)
				t.Timeout += () => { if (GodotObject.IsInstanceValid(p)) p.QueueFree(); };
			else
				p.QueueFree();
		}
		catch
		{
			if (GodotObject.IsInstanceValid(p)) p.QueueFree();
		}
	}

	private WeaponSfx GetWeaponSfx(string weaponId)
	{
		if (_weaponSfx.TryGetValue(weaponId, out var cached))
			return cached;

		var sfx = new WeaponSfx();
		try
		{
			var cfg = WastelandSurvivor.Game.Arena.WeaponVisualConfigStore.Instance.Get(weaponId);
			if (cfg != null)
			{
				if (!string.IsNullOrWhiteSpace(cfg.FireSoundPath))
					sfx.Fire = GD.Load<AudioStream>(cfg.FireSoundPath!);
				sfx.FireVolumeDb = cfg.FireVolumeDb;
				if (!string.IsNullOrWhiteSpace(cfg.HitVehicleSoundPath))
					sfx.HitVehicle = GD.Load<AudioStream>(cfg.HitVehicleSoundPath!);
				sfx.HitVehicleVolumeDb = cfg.HitVehicleVolumeDb;
				if (!string.IsNullOrWhiteSpace(cfg.HitWorldSoundPath))
					sfx.HitWorld = GD.Load<AudioStream>(cfg.HitWorldSoundPath!);
				sfx.HitWorldVolumeDb = cfg.HitWorldVolumeDb;
			}
		}
		catch
		{
			// Ignore missing assets/config.
		}

		_weaponSfx[weaponId] = sfx;
		return sfx;
	}

	private static Color HitMarkerColor(in RayHit hit)
	{
		if (hit.DriverHit) return new Color(1f, 0.25f, 0.25f); // driver
		if (hit.TireIndex >= 0) return new Color(1f, 0.85f, 0.2f); // tire
		return new Color(0.25f, 0.85f, 1f); // section/body
	}


	public override void _ExitTree()
	{
		// If user backs out abruptly, ensure the world is cleared.
		ForceFleeIfLive("exit");
		ClearWorld();
	}

	public override void _PhysicsProcess(double delta)
	{
		var dt = (float)delta;

		// Combat phase.
		if (_combatLive)
		{
			if (_playerPawn == null || _enemyPawn == null || _playerVehicleRuntime == null) return;

			UpdateTargetSelection();
			UpdatePlayerInput();
			UpdateEnemyAi(dt);
			ResolveVehicleOverlap();
			UpdateMines(dt);
			UpdateHudDynamic(dt);
			UpdateDriverCollisionDamage(dt);

			var fire1 = Input.IsActionPressed("ws_fire_1") || Input.IsActionPressed("ws_fire");
			var fire2 = Input.IsActionPressed("ws_fire_2");
			var fire3 = Input.IsActionPressed("ws_fire_3");
			// Don't allow firing if the driver is dead, or when the player is on-foot.
			if (_playerHpRuntime > 0 && _playerControlMode == PlayerControlMode.Vehicle)
			{
				if (fire1) TryFire(isPlayer: true, slot: 1);
				if (fire2) TryFire(isPlayer: true, slot: 2);
				if (fire3) TryFire(isPlayer: true, slot: 3);
			}

			// Resolve checks.
			if (_playerHpRuntime <= 0) ResolveOutcome("lose");
			else if (_enemyHpRuntime <= 0) ResolveOutcome("win");
			return;
		}

		// Post-match salvage phase (temporary): allow player to drive around, then hold G to exit.
		if (_postMatchAwaitingExit)
		{
			if (_playerPawn == null || _playerVehicleRuntime == null) return;
			UpdatePlayerInput();
			ResolveVehicleOverlap();
			UpdateMines(dt);
			UpdateHudDynamic(dt);
			UpdateDriverCollisionDamage(dt);
			UpdateExitHold(dt);
		}
	}

	private void UpdateHudDynamic(float dt)
	{
		if (_playerPawn == null || _playerVehicleRuntime == null) return;
		_hudDynamicRemaining -= dt;
		if (_hudDynamicRemaining > 0f) return;
		_hudDynamicRemaining = 0.10f;

		var defs = Defs();
		if (defs != null && _vehicleStatusHudRoot != null && GodotObject.IsInstanceValid(_vehicleStatusHudRoot) && _vehicleStatusHudRoot.Visible)
		{
			var speed = _playerPawn.Velocity.Length();
			var maxSpeed = _playerPawn.EffectiveMaxForwardSpeed;
			if (_vehicleStatusHud != null && GodotObject.IsInstanceValid(_vehicleStatusHud))
			{
				_vehicleStatusHud.UpdateDynamic(_playerVehicleRuntime, defs, speed, maxSpeed,
					_playerPawn.GetEngineRpm01ForHud(),
					_playerPawn.GetEngineDisplayRpmForHud(),
					_playerPawn.GetEngineGearDisplayForHud());
			}
			else
			{
				_vehicleStatusHudFallback?.UpdateDynamic(_playerVehicleRuntime, defs, speed, maxSpeed,
					_playerPawn.GetEngineRpm01ForHud(),
					_playerPawn.GetEngineDisplayRpmForHud(),
					_playerPawn.GetEngineGearDisplayForHud());
			}
		}
	}

	private void UpdateExitHold(float dt)
	{
		// Holding G for a few seconds exits the salvage phase and shows the post-encounter panel.
		var holding = Input.IsActionPressed("ws_exit_match");
		if (!holding)
		{
			_exitHoldSeconds = 0f;
			return;
		}

		_exitHoldSeconds += dt;
		if (_exitHoldSeconds < ExitHoldSecondsRequired) return;

		_exitHoldSeconds = 0f;
		_postMatchAwaitingExit = false;
		_lblMatchEnd.Visible = false;
		ShowPostPanel();
		RefreshStats();
	}

	private void SelectTier(int tier)
	{
		_selectedTier = tier;
		_lblStatus.Text = $"Status: Selected Tier {tier}";
	}

	private static GameSession? Session()
	{
		var app = App.Instance;
		return (app?.Services.TryGet<GameSession>(out var s) == true) ? s : null;
	}

	private static DefDatabase? Defs()
	{
		var app = App.Instance;
		return (app?.Services.TryGet<DefDatabase>(out var d) == true) ? d : null;
	}

	private static GameConsole? Console()
	{
		var app = App.Instance;
		return (app?.Services.TryGet<GameConsole>(out var c) == true) ? c : null;
	}

	
	private void ResolveVehicleOverlap()
	{
		// Early top-down prototype: ensure vehicles don't visually/phsyically clip through each other
		// even if collision layers/masks are misconfigured in a given editor environment.
		if (_playerPawn == null || _enemyPawn == null) return;

		var a = _playerPawn.GlobalPosition;
		var b = _enemyPawn.GlobalPosition;

		var d = new Vector3(b.X - a.X, 0f, b.Z - a.Z);
		var dist = d.Length();
		const float minDist = 2.35f; // slightly larger than half-widths of the proxy collision boxes
		if (dist >= minDist) return;

		if (dist < 0.001f) d = Vector3.Right;
		var dir = d.Normalized();
		var push = (minDist - dist) * 0.5f;

		_playerPawn.GlobalPosition = new Vector3(a.X - dir.X * push, a.Y, a.Z - dir.Z * push);
		_enemyPawn.GlobalPosition = new Vector3(b.X + dir.X * push, b.Y, b.Z + dir.Z * push);
	}

	private void ClearMines()
	{
		if (_mines.Count == 0) return;
		foreach (var m in _mines)
		{
			if (m.Node != null && GodotObject.IsInstanceValid(m.Node))
				m.Node.QueueFree();
		}
		_mines.Clear();
	}

	private void UpdateMines(float dt)
	{
		if (_arenaWorld == null || _mines.Count == 0) return;
		var defs = Defs();
		if (defs == null) return;

		for (var i = _mines.Count - 1; i >= 0; i--)
		{
			var mine = _mines[i];
			mine.LifetimeRemaining -= dt;
			mine.ArmRemaining -= dt;
			mine.OwnerGraceRemaining -= dt;
			if (!mine.Armed && mine.ArmRemaining <= 0f)
			{
				mine.Armed = true;
				SetMineVisualArmed(mine.Node, armed: true);
			}

			if (mine.LifetimeRemaining <= 0f)
			{
				if (mine.Node != null && GodotObject.IsInstanceValid(mine.Node))
					mine.Node.QueueFree();
				_mines.RemoveAt(i);
				continue;
			}

			if (!mine.Armed) continue;

			// Trigger: proximity to either vehicle.
			var triggerRadius = 1.55f;
			bool trigger = false;
			if (_playerPawn != null && GodotObject.IsInstanceValid(_playerPawn))
			{
				var d = Distance2D(mine.Position, _playerPawn.GlobalPosition);
				if (d <= triggerRadius && !(mine.FromPlayer && mine.OwnerGraceRemaining > 0f)) trigger = true;
			}
			if (!trigger && _enemyPawn != null && GodotObject.IsInstanceValid(_enemyPawn))
			{
				var d = Distance2D(mine.Position, _enemyPawn.GlobalPosition);
				if (d <= triggerRadius && !(!mine.FromPlayer && mine.OwnerGraceRemaining > 0f)) trigger = true;
			}

			if (!trigger) continue;

			ExplodeMine(mine, defs);
			if (mine.Node != null && GodotObject.IsInstanceValid(mine.Node))
				mine.Node.QueueFree();
			_mines.RemoveAt(i);
		}
	}

	private static float Distance2D(Vector3 a, Vector3 b)
	{
		var dx = a.X - b.X;
		var dz = a.Z - b.Z;
		return MathF.Sqrt(dx * dx + dz * dz);
	}


	
	private Node3D? GetPlayerEntityForEnemyTarget()
	{
		if (_playerControlMode == PlayerControlMode.Driver && _driverPawn != null && GodotObject.IsInstanceValid(_driverPawn))
			return _driverPawn;
		return _playerPawn;
	}

	private static Vector3 GetEntityVelocity(Node3D node)
	{
		return node is CharacterBody3D cb ? cb.Velocity : Vector3.Zero;
	}

	private bool IsPlayerOnFoot()
	{
		return _playerControlMode == PlayerControlMode.Driver && _driverPawn != null && GodotObject.IsInstanceValid(_driverPawn);
	}


	private const float VehicleExitMaxSpeed = 1.25f;

	private void SetPlayerControlledEntity(Node3D? node)
	{
		// Stable group for UI systems that need the currently-controlled entity (radar + camera).
		try
		{
			if (_playerControlledEntity != null && GodotObject.IsInstanceValid(_playerControlledEntity))
				_playerControlledEntity.RemoveFromGroup("player_controlled");
		}
		catch
		{
			// ignore
		}

		_playerControlledEntity = node;

		try
		{
			if (_playerControlledEntity != null && GodotObject.IsInstanceValid(_playerControlledEntity))
				_playerControlledEntity.AddToGroup("player_controlled");
		}
		catch
		{
			// ignore
		}

		if (_arenaWorld != null && GodotObject.IsInstanceValid(_arenaWorld))
			_arenaWorld.SetCameraTarget(_playerControlledEntity);

		UpdateHudVisibilityForControlMode();
	}

	private void UpdateHudVisibilityForControlMode()
	{
		if (_vehicleStatusHudRoot == null || !GodotObject.IsInstanceValid(_vehicleStatusHudRoot)) return;

		// Vehicle HUD should disappear when the player is on-foot.
		// (We don't force it visible here; RefreshStats controls when it should appear.)
		if (_playerControlMode != PlayerControlMode.Vehicle)
			_vehicleStatusHudRoot.Visible = false;
	}


	private void UpdateActionPrompt()
	{
		if (_actionPrompt == null || !GodotObject.IsInstanceValid(_actionPrompt)) return;

		// Ensure the overlay knows which camera to use for projection.
		if (_arenaWorld != null && GodotObject.IsInstanceValid(_arenaWorld))
		{
			var cam = _arenaWorld.GetNodeOrNull<Camera3D>("CameraRig/Camera3D");
			if (cam != null && GodotObject.IsInstanceValid(cam))
				_actionPrompt.SetCamera(cam);
		}

		// Only show hints when the arena world is active.
		if (!_combatLive && !_postMatchAwaitingExit)
		{
			_actionPrompt.Hide();
			return;
		}

		if (_playerHpRuntime <= 0 || _playerKilledThisMatch)
		{
			_actionPrompt.Hide();
			return;
		}

		if (_playerControlMode == PlayerControlMode.Vehicle)
		{
			// Action prompts only show while the player is on-foot.
			_actionPrompt.Hide();
			return;
		}
		else
		{
			if (_driverPawn == null || !GodotObject.IsInstanceValid(_driverPawn) || _playerPawn == null || !GodotObject.IsInstanceValid(_playerPawn))
			{
				_actionPrompt.Hide();
				return;
			}

			var d = Distance2D(_driverPawn.GlobalPosition, _playerPawn.GlobalPosition);
			if (d <= _driverCfg.EnterRadius)
			{
				// Show the prompt floating above the vehicle.
				_actionPrompt.ShowFor(_playerPawn, actionText: "Enter", keyText: "E");
			}
			else
			{
				_actionPrompt.Hide();
			}
		}
	}

	private void TryExitVehicle()
	{
		if (_arenaWorld == null || !GodotObject.IsInstanceValid(_arenaWorld)) return;
		if (_playerPawn == null || !GodotObject.IsInstanceValid(_playerPawn)) return;
		if (_driverPawn != null && GodotObject.IsInstanceValid(_driverPawn)) return;
		if (_playerControlMode != PlayerControlMode.Vehicle) return;
		if (_playerHpRuntime <= 0 || _playerKilledThisMatch) return;

		var speed = _playerPawn.Velocity.Length();
		if (speed > VehicleExitMaxSpeed) return;

		EnsureScenesLoaded();

		DriverPawn? driver = null;
		try
		{
			if (_driverScene != null && GodotObject.IsInstanceValid(_driverScene))
				driver = _driverScene.Instantiate() as DriverPawn;
		}
		catch
		{
			// ignore, fall back below
		}
		driver ??= new DriverPawn();

		driver.Name = "Driver";
		driver.MoveSpeed = _driverCfg.MoveSpeed;
		driver.SprintMultiplier = _driverCfg.SprintMultiplier;
		driver.Acceleration = _driverCfg.Acceleration;
		driver.Deceleration = _driverCfg.Deceleration;
		driver.AvatarConfig = _driverCfg.Avatar;
		driver.AddToGroup("player_driver");

		// Spawn beside the vehicle using a local-space offset (left side is -X).
		var local = _driverCfg.ExitOffsetVec3();
		var worldOffset = _playerPawn.GlobalTransform.Basis * local;
		var spawn = _playerPawn.GlobalPosition + worldOffset;
		// Stand on the floor plane (top of the arena floor collision is Y=0).
		spawn.Y = 0.8f + local.Y;
		driver.GlobalPosition = spawn;
		driver.Rotation = new Vector3(0f, _playerPawn.Rotation.Y, 0f);

		_arenaWorld.ActorsRoot.AddChild(driver);
		_driverPawn = driver;

		// Freeze the vehicle immediately so it doesn't drift.
		_playerPawn.ThrottleInput = 0f;
		_playerPawn.SteerInput = 0f;
		_playerPawn.Velocity = Vector3.Zero;

		_playerControlMode = PlayerControlMode.Driver;
		SetPlayerControlledEntity(_driverPawn);
		RefreshStats();
	}

	private void TryEnterVehicle()
	{
		if (_playerPawn == null || !GodotObject.IsInstanceValid(_playerPawn)) return;
		if (_driverPawn == null || !GodotObject.IsInstanceValid(_driverPawn)) return;
		if (_playerControlMode != PlayerControlMode.Driver) return;
		if (_playerHpRuntime <= 0 || _playerKilledThisMatch) return;

		var d = Distance2D(_driverPawn.GlobalPosition, _playerPawn.GlobalPosition);
		if (d > _driverCfg.EnterRadius) return;

		_driverPawn.Stop();
		_driverPawn.QueueFree();
		_driverPawn = null;

		_playerControlMode = PlayerControlMode.Vehicle;
		SetPlayerControlledEntity(_playerPawn);
		RefreshStats();
	}

	private void ExplodeMine(MineRuntime mine, DefDatabase defs)
	{
		if (_arenaWorld == null) return;
		// VFX
		ArenaVfx.SpawnSparks(_arenaWorld, mine.Position + Vector3.Up * 0.15f, count: 6);
		// big flash
		// reuse SpawnShot flash pattern by a quick shot to self
		ArenaVfx.SpawnShot(_arenaWorld, mine.Position + Vector3.Up * 0.10f, mine.Position + Vector3.Up * 0.10f, fromPlayer: mine.FromPlayer, hit: true);

		if (!defs.Weapons.TryGetValue("wpn_mine_dropper", out var wdef))
			return;
		var baseDamage = Math.Max(1, (int)MathF.Round(wdef.BaseDamage));
			// SplashRadius is optional in defs (nullable). Default to 0 when missing, then enforce a sane minimum.
			var radius = MathF.Max(1.0f, wdef.SplashRadius ?? 0f);

		ApplyMineDamageToVictim(mine, defs, victimIsPlayer: true, radius: radius, baseDamage: baseDamage);
		ApplyMineDamageToVictim(mine, defs, victimIsPlayer: false, radius: radius, baseDamage: baseDamage);

		RefreshStats();
	}

	private void ApplyMineDamageToVictim(MineRuntime mine, DefDatabase defs, bool victimIsPlayer, float radius, int baseDamage)
	{
		var pawn = victimIsPlayer ? _playerPawn : _enemyPawn;
		if (pawn == null || !GodotObject.IsInstanceValid(pawn)) return;
		// Owner grace
		if (victimIsPlayer && mine.FromPlayer && mine.OwnerGraceRemaining > 0f) return;
		if (!victimIsPlayer && !mine.FromPlayer && mine.OwnerGraceRemaining > 0f) return;

		var dist = Distance2D(mine.Position, pawn.GlobalPosition);
		if (dist > radius) return;
		var t = 1f - MathF.Min(1f, dist / radius);
		var dmg = Math.Max(1, (int)MathF.Round(baseDamage * (0.70f + 0.30f * t)));

		if (victimIsPlayer)
		{
			if (_playerVehicleRuntime == null) return;
			if (!defs.Vehicles.TryGetValue(_playerVehicleRuntime.DefinitionId, out var vdef)) return;
			ApplyExplosionToVehicle(ref _playerVehicleRuntime, vdef, pawn, dmg, mine.Position);
			ApplyDriverDamage(Math.Max(1, (int)MathF.Round(dmg * 0.20f)));
			AddLog($"Mine explodes! You take {dmg} (blast). HP: {_playerHpRuntime}/{_playerHpMaxRuntime}.");
		}
		else
		{
			if (_enemyVehicleRuntime == null) return;
			if (!defs.Vehicles.TryGetValue(_enemyVehicleRuntime.DefinitionId, out var vdef)) return;
			ApplyExplosionToVehicle(ref _enemyVehicleRuntime, vdef, pawn, dmg, mine.Position);
			ApplyEnemyDriverDamage(Math.Max(1, (int)MathF.Round(dmg * 0.20f)));
			AddLog($"Mine explodes! Enemy takes {dmg} (blast). Enemy HP: {_enemyHpRuntime}/{_enemyHpMaxRuntime}.");
		}
	}

	private void ApplyExplosionToVehicle(ref VehicleInstanceState veh, VehicleDefinition vdef, VehiclePawn pawn, int damage, Vector3 explosionPos)
	{
		var tireIdx = FindNearestTireIndex(pawn, explosionPos);
		var tireDamage = Math.Max(1, (int)MathF.Round(damage * 0.85f));
		var underDamage = Math.Max(1, (int)MathF.Round(damage * 0.55f));
		var remaining = 0;
		// Tire
		if (tireIdx >= 0 && veh.CurrentTireHp is { Length: > 0 } && tireIdx < veh.CurrentTireHp.Length)
		{
			var oldHp = veh.CurrentTireHp[tireIdx];
			veh = VehicleCombatMath.ApplyDamageToTire(veh, vdef, tireIdx, tireDamage, out remaining);
			if (oldHp > 0 && veh.CurrentTireHp.Length > tireIdx && veh.CurrentTireHp[tireIdx] <= 0)
				PlaySfx(_sfxTirePop);
		}
		// Undercarriage
		veh = VehicleCombatMath.ApplyDamageToSection(veh, vdef, ArmorSection.Undercarriage, underDamage, out remaining);
	}

	private static int FindNearestTireIndex(VehiclePawn pawn, Vector3 explosionPos)
	{
		var bestIdx = -1;
		var best = float.MaxValue;
		for (var i = 0; i < 4; i++)
		{
			var p = pawn.GetTireWorldPosition(i);
			var dx = p.X - explosionPos.X;
			var dz = p.Z - explosionPos.Z;
			var d = dx * dx + dz * dz;
			if (d < best) { best = d; bestIdx = i; }
		}
		return bestIdx;
	}

	private static void SetMineVisualArmed(Node3D? node, bool armed)
	{
		if (node == null || !GodotObject.IsInstanceValid(node)) return;
		var mesh = node.GetNodeOrNull<MeshInstance3D>("MineMesh");
		if (mesh == null) return;
		var color = armed ? new Color(0.95f, 0.25f, 0.15f) : new Color(0.25f, 0.25f, 0.25f);
		var mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = color,
			Roughness = 1.0f
		};
		mesh.SetSurfaceOverrideMaterial(0, mat);
	}

	private Node3D? SpawnMineMarker(Vector3 worldPos)
	{
		if (_arenaWorld == null) return null;
		var vfxRoot = _arenaWorld.GetVfxRoot();
		// Keep mines grouped under a stable node ("mine layer") so they're easy to manage.
		var minesRoot = vfxRoot.GetNodeOrNull<Node3D>("Mines");
		if (minesRoot == null)
		{
			minesRoot = new Node3D { Name = "Mines" };
			vfxRoot.AddChild(minesRoot);
		}

		// IMPORTANT: set only the parent node's world position.
		// If we set both the child mesh GlobalPosition and the parent GlobalPosition, the mesh ends up offset twice.
		var node = new Node3D { Name = $"Mine_{_mineSeq}" };
		node.GlobalPosition = worldPos;
		var mesh = new MeshInstance3D
		{
			Name = "MineMesh",
			Mesh = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.18f, Height = 0.10f },
			Position = Vector3.Zero
		};
		node.AddChild(mesh);
		minesRoot.AddChild(node);
		SetMineVisualArmed(node, armed: false);
		return node;
	}


private void ResetUi()
	{
		_postPanel.Visible = false;
		_runtimeLog.Clear();
		RefreshStats();
	}

	private void RefreshArenaDialogVisibility()
	{
		// The pre-fight arena dialog should be hidden during combat and during post-match UI.
		// It should only be visible when we are in the pre-fight selection state.
		if (_hudPanel == null) return;
		var show = !_combatLive && !_postMatchAwaitingExit && (_postPanel == null || !_postPanel.Visible);
		_hudPanel.Visible = show;
	}


	private Node3D? FindWorldRoot()
	{
		// Canonical path (Main scene)
		var root = GetTree()?.Root;
		var wr = root?.GetNodeOrNull<Node3D>("Main/AppRoot/WorldRoot");
		if (wr != null) return wr;

		// Walk up parents and look for a sibling WorldRoot under an AppRoot-like node.
		Node? p = this;
		while (p != null)
		{
			var candidate = p.GetNodeOrNull<Node3D>("WorldRoot");
			if (candidate != null) return candidate;
			p = p.GetParent();
		}

		// Fallback: deep search by name.
		var found = root?.FindChild("WorldRoot", true, false);
		return found as Node3D;
	}

	private void EnsureWorld()
	{
			EnsureScenesLoaded();
			if (_worldScene == null)
			{
				_lblStatus.Text = "Status: Arena world scene missing.";
				Console()?.Error("Arena: Failed to load res://Scenes/Arena/ArenaWorld.tscn");
				return;
			}

		_worldRoot = FindWorldRoot();
		if (_worldRoot == null)
		{
			_lblStatus.Text = "Status: WorldRoot missing.";
			Console()?.Error("Arena: WorldRoot not found; cannot spawn arena world.");
			return;
		}

		ClearWorld();

			var worldNode = _worldScene.Instantiate();
			_arenaWorld = worldNode as ArenaWorld;
			if (_arenaWorld == null)
			{
				worldNode.QueueFree();
				_lblStatus.Text = "Status: Arena world type mismatch.";
				Console()?.Error("Arena: ArenaWorld.tscn root is not ArenaWorld (script mismatch?)");
				return;
			}
			_worldRoot.AddChild(_arenaWorld);

			EnsureTargetIndicator();

		// Ensure the world is initialized even if its _Ready has not run yet.
		_arenaWorld.EnsureObstacles();
		_arenaWorld.EnsureCamera();
			Console()?.Debug($"Arena: world spawned. children={_worldRoot.GetChildCount()} camCurrent={_arenaWorld.GetNodeOrNull<Camera3D>("CameraRig/Camera3D")?.Current == true}");
	}

	private void EnsureTargetIndicator()
	{
		if (_arenaWorld == null) return;
		if (_targetIndicator != null && GodotObject.IsInstanceValid(_targetIndicator)) return;

		var vfx = _arenaWorld.GetVfxRoot();
		var existing = vfx.GetNodeOrNull<TargetIndicator3D>("TargetIndicator");
		if (existing != null)
		{
			_targetIndicator = existing;
			return;
		}

		_targetIndicator = new TargetIndicator3D { Name = "TargetIndicator" };
		vfx.AddChild(_targetIndicator);
	}

	private void ClearWorld()
	{
		if (_worldRoot == null) return;
		foreach (var child in _worldRoot.GetChildren())
			(child as Node)?.QueueFree();
		ClearMines();
		_arenaWorld = null;
		_playerPawn = null;
		_driverPawn = null;
		_playerControlledEntity = null;
		_playerControlMode = PlayerControlMode.Vehicle;
		_enemyPawn = null;
		_selectedTarget = null;
		_targetIndicator = null;
		_actionPrompt?.Hide();
	}

	private void StartEncounter()
	{
		_lblStatus.Text = "Status: Starting encounter...";
		Console()?.Debug("Arena: Start pressed.");
		try
		{
				// Reset any previous post-match state.
				_postMatchAwaitingExit = false;
				_postMatchOutcome = "";
				_playerKilledThisMatch = false;
				_enemyKilledThisMatch = false;
				_exitHoldSeconds = 0f;
				_postPanel.Visible = false;
				_lblMatchEnd.Visible = false;


				// If an old ArenaWorld was freed but our reference survived, fix it up.
				if (_arenaWorld != null && !GodotObject.IsInstanceValid(_arenaWorld))
					_arenaWorld = null;

		var session = Session();
		if (session == null)
		{
			_lblStatus.Text = "Status: GameSession missing.";
			Console()?.Error("Arena: GameSession service missing.");
			return;
		}

		if (Defs() == null)
		{
			_lblStatus.Text = "Status: Defs missing.";
			Console()?.Error("Arena: DefDatabase service missing.");
			return;
		}
		if (_arenaWorld == null)
		{
			EnsureWorld();
			if (_arenaWorld == null) return;
		}
		// Defensive: ensure camera is current before combat starts.
		_arenaWorld.EnsureCamera();
		ClearMines();

		// If an encounter is already active in the save (e.g. app restarted mid-fight), resume it.
		if (session.HasActiveEncounter())
		{
			var existing = session.GetCurrentEncounter();
			if (existing != null)
			{
				Console()?.Status($"Arena: Resuming active encounter (tier {existing.Tier}).");
				ResumeEncounter(session, existing);
				return;
			}
		}

			Console()?.Debug($"Arena: calling TryStartArenaEncounter tier={_selectedTier}");
			if (!session.TryStartArenaEncounter(_selectedTier, out var err))
		{
			_lblStatus.Text = $"Status: {err}";
			Console()?.Error(err);
			return;
		}

		Console()?.Debug("Arena: TryStartArenaEncounter ok");

			var enc = session.GetCurrentEncounter();
		if (enc == null)
		{
			_lblStatus.Text = "Status: Failed to start encounter.";
			Console()?.Error("Arena: Encounter missing after start.");
			return;
		}
		Console()?.Debug($"Arena: encounter seeded id={enc.EncounterId} tier={enc.Tier} enemyHp={enc.EnemyHp}");

		_playerVehicleRuntime = session.Save.Vehicles.FirstOrDefault(v => v.InstanceId == enc.VehicleInstanceId);
		if (_playerVehicleRuntime == null)
		{
			_lblStatus.Text = "Status: Active vehicle missing.";
			Console()?.Error("Arena: Active vehicle missing.");
			return;
		}

		// Load persistent driver stats.
		_playerHpMaxRuntime = Math.Max(1, session.GetDriverHpMax());
		_playerHpRuntime = Math.Clamp(session.GetDriverHp(), 0, _playerHpMaxRuntime);
		_playerArmorMaxRuntime = Math.Max(1, session.GetDriverArmorMax());
		_playerArmorRuntime = Math.Clamp(session.GetDriverArmor(), 0, _playerArmorMaxRuntime);

		// Ensure vehicle has section/tire HP initialized.
		var defs = Defs();
		if (defs != null && defs.Vehicles.TryGetValue(_playerVehicleRuntime.DefinitionId, out var vdef))
			_playerVehicleRuntime = VehicleCombatMath.EnsureDamageState(_playerVehicleRuntime, vdef);

		_enemyHpMaxRuntime = 50;
		_enemyHpRuntime = Math.Clamp(enc.EnemyHp, 0, _enemyHpMaxRuntime);
		_enemyArmorMaxRuntime = 50;
		_enemyArmorRuntime = _enemyArmorMaxRuntime;
		_enemyVehicleRuntime = CreateEnemyVehicleRuntime(defs, enc.Tier, _playerVehicleRuntime);
		_enemyAmmoRuntime = Random.Shared.Next(22, 42);
		_combatLive = true;
		RefreshArenaDialogVisibility();
		_runtimeLog.Clear();
		AddLog($"Encounter started (tier {_selectedTier}).");

		SpawnActors();
		Console()?.Debug($"Arena: actors spawned. player={_playerPawn != null} enemy={_enemyPawn != null} actors={_arenaWorld.ActorsRoot.GetChildCount()}");
		RefreshStats();
		_lblStatus.Text = "Status: Fight!";
		}
		catch (Exception ex)
		{
			_lblStatus.Text = "Status: Arena start failed (exception).";
			var txt = ex.ToString();
				foreach (var line in txt.Split('\n'))
					Console()?.Error($"Arena start exception: {line.TrimEnd()}" );
			GD.PrintErr(ex);
		}
	}

	private void SpawnActors()
	{
		if (_arenaWorld == null) return;
		EnsureTargetIndicator();
			EnsureScenesLoaded();
			if (_vehScene == null)
			{
				Console()?.Error("Arena: Failed to load res://Scenes/Arena/VehiclePawn.tscn");
				return;
			}
		foreach (var child in _arenaWorld.ActorsRoot.GetChildren())
			(child as Node)?.QueueFree();

		// Ensure we don't leave stale nodes in the "player_controlled" group.
		// (Queued-for-free nodes can stay in groups until they are fully freed.)
		try
		{
			foreach (var n in GetTree().GetNodesInGroup("player_controlled"))
			{
				if (n is Node node && GodotObject.IsInstanceValid(node))
					node.RemoveFromGroup("player_controlled");
			}

			// Same issue applies to vehicle groups used by HUDs (radar, vehicle preview, etc.).
			// If we don't remove these explicitly, GetFirstNodeInGroup("player_vehicle") can
			// return an old queued-for-free pawn, causing the HUD preview camera to aim at the wrong thing.
			foreach (var n in GetTree().GetNodesInGroup("player_vehicle"))
			{
				if (n is Node node && GodotObject.IsInstanceValid(node))
					node.RemoveFromGroup("player_vehicle");
			}
			foreach (var n in GetTree().GetNodesInGroup("enemy_vehicle"))
			{
				if (n is Node node && GodotObject.IsInstanceValid(node))
					node.RemoveFromGroup("enemy_vehicle");
			}
		}
		catch
		{
			// ignore
		}

		_driverPawn = null;
		_playerControlledEntity = null;
		_playerControlMode = PlayerControlMode.Vehicle;
		_actionPrompt.Hide();

			var playerNode = _vehScene.Instantiate();
			_playerPawn = playerNode as VehiclePawn;
			if (_playerPawn == null)
			{
				playerNode.QueueFree();
				Console()?.Error("Arena: VehiclePawn.tscn root is not VehiclePawn (script mismatch?)");
				return;
			}
		_playerPawn.Name = "Player";
		_playerPawn.BodyColor = new Color(0.20f, 0.85f, 0.25f);
		_playerPawn.Position = new Vector3(-16, 0.4f, 10);
		_playerPawn.Rotation = new Vector3(0, 0, 0);
		_playerPawn.AddToGroup("player_vehicle");
		_arenaWorld.ActorsRoot.AddChild(_playerPawn);
		var defs = Defs();
		if (defs != null && _playerVehicleRuntime != null)
			_playerPawn.ConfigureLoadout(defs, _playerVehicleRuntime);

		// Use the canonical setter so radar/camera always follow the correct entity.
		SetPlayerControlledEntity(_playerPawn);

			var enemyNode = _vehScene.Instantiate();
			_enemyPawn = enemyNode as VehiclePawn;
			if (_enemyPawn == null)
			{
				enemyNode.QueueFree();
				Console()?.Error("Arena: VehiclePawn.tscn root is not VehiclePawn (script mismatch?)");
				return;
			}
		_enemyPawn.Name = "Enemy";
		_enemyPawn.BodyColor = new Color(0.85f, 0.22f, 0.22f);
		_enemyPawn.Position = new Vector3(16, 0.4f, -10);
		_enemyPawn.Rotation = new Vector3(0, Mathf.Pi, 0);
		_enemyPawn.AddToGroup("enemy_vehicle");
		_arenaWorld.ActorsRoot.AddChild(_enemyPawn);
		if (defs != null && _enemyVehicleRuntime != null)
			_enemyPawn.ConfigureLoadout(defs, _enemyVehicleRuntime);

		_selectedTarget = _enemyPawn;
		_targetCycleIndex = 0;
		_playerControlMode = PlayerControlMode.Vehicle;
		SetPlayerControlledEntity(_playerPawn);
		_targetIndicator?.SetTarget(_selectedTarget);
	}

	private static VehicleInstanceState? CreateEnemyVehicleRuntime(DefDatabase? defs, int tier, VehicleInstanceState? loadoutTemplate)
	{
		if (defs == null || defs.Vehicles.Count == 0) return null;
		var defId = tier switch
		{
			1 => "veh_compact",
			2 => "veh_sedan",
			_ => "veh_light_truck"
		};
		if (!defs.Vehicles.TryGetValue(defId, out var vdef))
			vdef = defs.Vehicles.Values.First();

		var armor = new Dictionary<ArmorSection, int>(vdef.BaseArmorBySection);
		var hp = new Dictionary<ArmorSection, int>(vdef.BaseHpBySection);
		var tiresArmor = new int[Math.Max(0, vdef.TireCount)];
		var tiresHp = new int[Math.Max(0, vdef.TireCount)];
		for (var i = 0; i < tiresArmor.Length; i++)
		{
			tiresArmor[i] = vdef.BaseTireArmor;
			tiresHp[i] = vdef.BaseTireHp;
		}

		// Enemy loadout: by default match the player's installed weapons where mount ids overlap.
		var installed = new Dictionary<string, InstalledWeaponState>();
		if (loadoutTemplate != null && loadoutTemplate.InstalledWeaponsByMountId.Count > 0)
		{
			var validMountIds = vdef.MountPoints.Select(m => m.MountId).ToHashSet();
			foreach (var kv in loadoutTemplate.InstalledWeaponsByMountId)
			{
				if (validMountIds.Contains(kv.Key))
					installed[kv.Key] = kv.Value;
			}
		}

		// Fallback starter loadout: Machine Gun (front if possible) + Guided Missile (top/right if possible).
		if (installed.Count == 0)
		{
			var mountIds = vdef.MountPoints.Select(m => m.MountId).ToList();
			var primaryMount = mountIds.Contains("F1") ? "F1" : mountIds.FirstOrDefault();
			if (!string.IsNullOrWhiteSpace(primaryMount))
				installed[primaryMount] = new InstalledWeaponState { WeaponId = "wpn_mg_50cal" };

			string? secondaryMount = null;
			var top = vdef.MountPoints.FirstOrDefault(m => m.MountLocation == MountLocation.Top)?.MountId;
			if (!string.IsNullOrWhiteSpace(top) && top != primaryMount) secondaryMount = top;
			else if (mountIds.Contains("R1") && "R1" != primaryMount) secondaryMount = "R1";
			else if (mountIds.Contains("L1") && "L1" != primaryMount) secondaryMount = "L1";
			else secondaryMount = mountIds.FirstOrDefault(x => x != primaryMount);

			if (!string.IsNullOrWhiteSpace(secondaryMount))
				installed[secondaryMount] = new InstalledWeaponState { WeaponId = "wpn_missile" };
		}

		return new VehicleInstanceState
		{
			DefinitionId = vdef.Id,
			CurrentArmorBySection = armor,
			CurrentHpBySection = hp,
			CurrentTireArmor = tiresArmor,
			CurrentTireHp = tiresHp,
			InstalledWeaponsByMountId = installed,
			AmmoInventory = new Dictionary<string, int>(),
			CargoInventory = new Dictionary<string, int>()
		};
	}

	private void ResumeEncounter(GameSession session, EncounterState enc)
	{
		_postMatchAwaitingExit = false;
		_postMatchOutcome = "";
		_playerKilledThisMatch = false;
		_enemyKilledThisMatch = false;
		_exitHoldSeconds = 0f;
		_postPanel.Visible = false;
		_lblMatchEnd.Visible = false;

		// Sync tier selection so UI matches the resumed encounter.
		_selectedTier = enc.Tier;

		_playerVehicleRuntime = session.Save.Vehicles.FirstOrDefault(v => v.InstanceId == enc.VehicleInstanceId);
		if (_playerVehicleRuntime == null)
		{
			_lblStatus.Text = "Status: Active encounter vehicle missing.";
			Console()?.Error("Arena: Active encounter vehicle missing; cannot resume.");
			return;
		}

		_playerHpMaxRuntime = Math.Max(1, session.GetDriverHpMax());
		_playerHpRuntime = Math.Clamp(session.GetDriverHp(), 0, _playerHpMaxRuntime);
		_playerArmorMaxRuntime = Math.Max(1, session.GetDriverArmorMax());
		_playerArmorRuntime = Math.Clamp(session.GetDriverArmor(), 0, _playerArmorMaxRuntime);

		var defs = Defs();
		if (defs != null && defs.Vehicles.TryGetValue(_playerVehicleRuntime.DefinitionId, out var vdef))
			_playerVehicleRuntime = VehicleCombatMath.EnsureDamageState(_playerVehicleRuntime, vdef);

		_enemyHpMaxRuntime = 50;
		_enemyHpRuntime = Math.Clamp(enc.EnemyHp, 0, _enemyHpMaxRuntime);
		_enemyArmorMaxRuntime = 50;
		_enemyArmorRuntime = _enemyArmorMaxRuntime;
		_enemyVehicleRuntime = CreateEnemyVehicleRuntime(defs, enc.Tier, _playerVehicleRuntime);
		_enemyAmmoRuntime = Random.Shared.Next(22, 42);
		_combatLive = true;
		RefreshArenaDialogVisibility();
		_runtimeLog.Clear();
		_runtimeLog.Add($"Encounter resumed (tier {enc.Tier}).");
		Console()?.Status($"Encounter resumed (tier {enc.Tier}).");

		SpawnActors();
		RefreshStats();
		_lblStatus.Text = "Status: Fight!";
	}

	private void ForceFleeIfLive(string reason)
	{
		if (!_combatLive) return;
		var session = Session();
		if (session == null || _playerVehicleRuntime == null) return;

		// Persist the player's runtime vehicle (ammo/damage) and end the encounter.
		if (!session.ResolveArenaEncounterRealtime("fled", _playerVehicleRuntime, _enemyHpRuntime, _playerArmorRuntime, _playerHpRuntime, _runtimeLog.ToArray(),
			out var updatedVeh, out var err))
		{
			Console()?.Error($"Arena: forced flee failed ({reason}): {err}");
		}
		else
		{
			_playerVehicleRuntime = updatedVeh;
			_playerHpMaxRuntime = Math.Max(1, session.GetDriverHpMax());
			_playerHpRuntime = Math.Clamp(session.GetDriverHp(), 0, _playerHpMaxRuntime);
			_playerArmorMaxRuntime = Math.Max(1, session.GetDriverArmorMax());
			_playerArmorRuntime = Math.Clamp(session.GetDriverArmor(), 0, _playerArmorMaxRuntime);
			Console()?.Status($"Arena: forced flee ({reason}).");
		}
		_combatLive = false;
	}


	private void UpdatePlayerInput()
	{
		UpdateActionPrompt();

		// If the driver is dead (or was killed this match), disable controls immediately.
		if (_playerHpRuntime <= 0 || _playerKilledThisMatch)
		{
			if (_playerPawn != null && GodotObject.IsInstanceValid(_playerPawn))
			{
				_playerPawn.ThrottleInput = 0f;
				_playerPawn.SteerInput = 0f;
			}
			if (_driverPawn != null && GodotObject.IsInstanceValid(_driverPawn))
			{
				// If the player died while on-foot, play the death animation instead of snapping back to idle.
				if (IsPlayerOnFoot())
					_driverPawn.TriggerDeath();
				else
					_driverPawn.Stop();
			}
			return;
		}

		if (_playerControlMode == PlayerControlMode.Driver)
		{
			if (_driverPawn == null || !GodotObject.IsInstanceValid(_driverPawn))
			{
				// Safety fallback
				_playerControlMode = PlayerControlMode.Vehicle;
				if (_playerPawn != null && GodotObject.IsInstanceValid(_playerPawn))
					SetPlayerControlledEntity(_playerPawn);
				return;
			}

			// Contextual interact: enter vehicle.
			if (Input.IsActionJustPressed("ws_interact"))
				TryEnterVehicle();

			var dir = Vector3.Zero;
			if (Input.IsActionPressed("ws_move_forward")) dir += Vector3.Forward;
			if (Input.IsActionPressed("ws_move_backward")) dir += Vector3.Back;
			if (Input.IsActionPressed("ws_steer_left")) dir += Vector3.Left;
			if (Input.IsActionPressed("ws_steer_right")) dir += Vector3.Right;

			_driverPawn.MoveInput = dir;
			_driverPawn.Sprint = Input.IsActionPressed("ws_sprint");
			return;
		}

		// Vehicle control mode.
		if (_playerPawn == null || !GodotObject.IsInstanceValid(_playerPawn)) return;

		// Contextual interact: exit vehicle.
		if (Input.IsActionJustPressed("ws_interact"))
			TryExitVehicle();

		var throttle = 0f;
		if (Input.IsActionPressed("ws_move_forward")) throttle += 1f;
		if (Input.IsActionPressed("ws_move_backward")) throttle -= 0.55f;
		var steer = 0f;
		if (Input.IsActionPressed("ws_steer_left")) steer -= 1f;
		if (Input.IsActionPressed("ws_steer_right")) steer += 1f;

		_playerPawn.ThrottleInput = Mathf.Clamp(throttle, -1f, 1f);
		_playerPawn.SteerInput = Mathf.Clamp(steer, -1f, 1f);

		var aimTarget = _selectedTarget != null
			? _selectedTarget.GlobalPosition
			: (_playerPawn.GlobalPosition + (-_playerPawn.GlobalTransform.Basis.Z) * 12f);
		_playerPawn.AimWorldPosition = aimTarget;

		if (Input.IsActionJustPressed("ws_target_next"))
			CycleTarget();
	}

	private void UpdateTargetSelection()
	{
		if (_selectedTarget != null && GodotObject.IsInstanceValid(_selectedTarget))
		{
			_targetIndicator?.SetTarget(_selectedTarget);
			return;
		}
		_selectedTarget = _enemyPawn;
		_targetIndicator?.SetTarget(_selectedTarget);
	}

	private void CycleTarget()
	{
		// Only one enemy currently, but keep the API stable.
		_selectedTarget = _enemyPawn;
		_targetIndicator?.SetTarget(_selectedTarget);
	}

	private void UpdateEnemyAi(float dt)
	{
		if (_enemyPawn == null || !GodotObject.IsInstanceValid(_enemyPawn)) return;

		var target = GetPlayerEntityForEnemyTarget();
		if (target == null || !GodotObject.IsInstanceValid(target)) return;

		// If either driver is dead, stop the enemy immediately.
		if (_enemyHpRuntime <= 0 || _playerHpRuntime <= 0)
		{
			_enemyPawn.ThrottleInput = 0f;
			_enemyPawn.SteerInput = 0f;
			return;
		}

		if (_enemyUnstuckRemaining > 0f)
		{
			_enemyUnstuckRemaining -= dt;
			_enemyPawn.ThrottleInput = -0.45f;
			_enemyPawn.SteerInput = 0.85f;
			_enemyPawn.AimWorldPosition = target.GlobalPosition;
			return;
		}

		// Steering controller: lead the target a bit and slow down when heavily misaligned
		// so we don't orbit/spin around the player.
		var targetVel = GetEntityVelocity(target);
		var lead = target is VehiclePawn ? 0.35f : 0.15f;
		var targetPos = target.GlobalPosition + targetVel * lead;
		var toTarget = targetPos - _enemyPawn.GlobalPosition;
		toTarget.Y = 0f;
		var dist = toTarget.Length();
		if (dist < 0.001f) dist = 0.001f;
		var toTargetDir = toTarget / dist;
		var desiredYaw = Mathf.Atan2(toTarget.X, toTarget.Z) + Mathf.Pi; // facing -Z forward
		var headingDiff = Mathf.Wrap(desiredYaw - _enemyPawn.Rotation.Y, -Mathf.Pi, Mathf.Pi);

		// Slightly stronger steering so the bot can actually line up, but we'll also
		// manage throttle based on alignment to prevent perpetual circles.
		_enemyPawn.SteerInput = Mathf.Clamp(headingDiff * 1.05f, -1f, 1f);

		// Range keeping with alignment-aware throttle.
		float throttle;
		var absDiff = MathF.Abs(headingDiff);
		if (absDiff > 2.05f)
		{
			// If we're basically facing away, reverse + turn to recover.
			throttle = -0.70f;
		}
		else if (absDiff > 1.35f)
		{
			// Big misalignment: go slow so steering can catch up (prevents spiraling).
			throttle = 0.18f;
		}
		else if (dist > EnemyPreferredRange + 7f) throttle = 1.0f;
		else if (dist < EnemyPreferredRange - 7f) throttle = -0.45f;
		else throttle = 0.70f;

		// Slow down when we're not pointed at the target; reduces orbiting.
		var align01 = Mathf.Clamp(1f - (absDiff / Mathf.Pi), 0f, 1f);
		throttle *= Mathf.Lerp(0.30f, 1f, align01);
		_enemyPawn.ThrottleInput = throttle;

		_enemyPawn.AimWorldPosition = target.GlobalPosition;

		if (_enemyPawn.GetSlideCollisionCount() > 0)
			_enemyUnstuckRemaining = 0.30f;

		// Fire in range. Pick the best-aligned weapon mount (front/top/rear) rather than always slot 1.
		if (_enemyPawn.FireCooldownRemaining <= 0f && dist < 55f)
		{
			var slot = PickEnemyFireSlot(toTargetDir, dist);
			if (slot != 0)
			{
				// Don't shoot every frame; bursty but readable.
				var p = dist < 26f ? 0.65 : 0.45;
				if (Random.Shared.NextDouble() < p)
					TryFire(isPlayer: false, slot: slot);
			}
		}
	}


	private int PickEnemyFireSlot(Vector3 toTargetDir, float dist)
	{
		var defs = Defs();
		if (defs == null || _enemyVehicleRuntime == null || _enemyPawn == null) return 0;
		if (!defs.Vehicles.TryGetValue(_enemyVehicleRuntime.DefinitionId, out var vdef)) return 0;

		int bestSlot = 0;
		float bestScore = -999f;

		for (var slot = 1; slot <= 3; slot++)
		{
			if (!TryResolveWeaponForSlot(defs, _enemyVehicleRuntime, slot, out var mountId, out var installed, out var wdef, out var ammoId))
				continue;
			if (installed == null || wdef == null) continue;

			// Aiming: compare mount forward to target direction.
			var fwd = _enemyPawn.GetMuzzleWorldForward(mountId);
			var dot = Mathf.Clamp(fwd.Dot(toTargetDir), -1f, 1f);
			var loc = vdef.MountPoints.FirstOrDefault(m => m.MountId == mountId)?.MountLocation ?? MountLocation.Front;

			// Alignment thresholds by mount type.
			// - Turret/top can usually rotate, so accept wider error.
			// - Rear/side mounts are fixed; require tighter alignment.
			var minDot = loc switch
			{
				MountLocation.Top => 0.20f,
				MountLocation.Rear => 0.72f,
				MountLocation.Left => 0.65f,
				MountLocation.Right => 0.65f,
				_ => 0.72f
			};
			if (dot < minDot) continue;

			// Range preference: prefer front/top at normal range; rear only if close.
			var rangePenalty = loc == MountLocation.Rear && dist > 24f ? 0.25f : 0f;

			// Score: alignment is king, then prefer higher damage when tied.
			var score = dot * 10f + wdef.BaseDamage - rangePenalty;
			if (score > bestScore)
			{
				bestScore = score;
				bestSlot = slot;
			}
		}

		return bestSlot;
	}

	private void TryFire(bool isPlayer, int slot = 1)
	{
		if (_playerPawn == null || _enemyPawn == null) return;
		var shooter = isPlayer ? _playerPawn : _enemyPawn;
		if (shooter.FireCooldownRemaining > 0f) return;

		var defs = Defs();
		var shooterVeh = isPlayer ? _playerVehicleRuntime : _enemyVehicleRuntime;

		// Resolve mount + weapon from the vehicle's installed weapons.
		string? mountId = null;
		InstalledWeaponState? installed = null;
		WeaponDefinition? wdef = null;
		string? ammoId = null;

		if (defs != null && shooterVeh != null)
		{
			if (TryResolveWeaponForSlot(defs, shooterVeh, slot, out var resolvedMount, out var resolvedInstalled, out var resolvedWeaponDef, out var resolvedAmmoId))
			{
				mountId = resolvedMount;
				installed = resolvedInstalled;
				wdef = resolvedWeaponDef;
				ammoId = resolvedAmmoId;
				// Drive cooldown from weapon def.
				shooter.FireCooldownSeconds = Math.Max(0.05f, resolvedWeaponDef.CooldownMs / 1000f);
			}
		}

		// If we couldn't resolve a weapon, fall back to the previous prototype behavior.
		if (wdef == null || string.IsNullOrWhiteSpace(ammoId) || installed == null)
		{
			TryFireLegacy(isPlayer, shooter);
			return;
		}

		// Consume ammo
		if (isPlayer)
		{
			if (_playerVehicleRuntime == null)
				return;

			if (!TryConsumeAmmo(ref _playerVehicleRuntime, ammoId!, 1))
			{
				shooter.FireCooldownRemaining = 0.25f;
				AddLog($"Click! Out of ammo ({wdef.DisplayName}).");
				return;
			}
		}
		else
		{
			if (_enemyAmmoRuntime <= 0)
			{
				shooter.FireCooldownRemaining = 0.50f;
				if (Random.Shared.NextDouble() < 0.06)
					AddLog("Enemy: out of ammo.");
				return;
			}
			_enemyAmmoRuntime = Math.Max(0, _enemyAmmoRuntime - 1);
		}



		// Special case: mine dropper places a hazard behind the vehicle instead of firing a hitscan shot.
		if (wdef.WeaponType == WeaponType.MineDropper)
		{
			TryDropMine(isPlayer, shooter, mountId ?? "B1");
			shooter.FireCooldownRemaining = shooter.FireCooldownSeconds;
			RefreshStats();
			return;
		}

		// Fire from the selected mount muzzle.
		var from = mountId != null ? shooter.GetMuzzleWorldPosition(mountId) : shooter.GetMuzzleWorldPosition();
		var dir = mountId != null ? shooter.GetMuzzleWorldForward(mountId) : shooter.GetMuzzleWorldForward();
		// When the enemy is firing at the on-foot driver, aim toward their body (include a slight downward component)
		// so shots don't skim above the driver collider.
		if (!isPlayer)
		{
			var t = GetPlayerEntityForEnemyTarget();
			if (t is DriverPawn dp && GodotObject.IsInstanceValid(dp))
			{
				var aim = dp.GlobalPosition + Vector3.Up * 0.10f;
				dir = (aim - from).Normalized();
			}
		}
		var wSfx = GetWeaponSfx(wdef.Id);

		// Spread based on weapon type (missiles are tighter).
		var spread = isPlayer ? PlayerSpreadRad : EnemySpreadRad;
		if (wdef.WeaponType == WeaponType.Missile) spread *= 0.35f;
		if (wdef.WeaponType == WeaponType.MineDropper) spread *= 0.15f;

		var jitter = (float)(Random.Shared.NextDouble() * 2.0 - 1.0) * spread;
		dir = dir.Rotated(Vector3.Up, jitter).Normalized();

		// Range heuristic.
		var maxRange = wdef.WeaponType switch
		{
			WeaponType.MineDropper => 18f,
			WeaponType.Missile => 120f,
			_ => 90f
		};
		var to = from + dir * maxRange;
		var hit = Raycast(from, to, shooter);
		var impactPos = hit.Hit ? hit.Position : to;

		// Always play fire SFX per shot (weapon-configurable).
		PlaySfx3D(wSfx.Fire, from, volumeDb: wSfx.FireVolumeDb);

		// Damage: use weapon base damage with a small random band.
		var dmgBase = Math.Max(1f, wdef.BaseDamage);
		var dmgScale = (float)(0.85 + Random.Shared.NextDouble() * 0.30);
		var damage = Math.Max(1, (int)MathF.Round(dmgBase * dmgScale));

		// Only apply damage if we actually hit the intended pawn.
		var intended = isPlayer ? (Node3D?)_enemyPawn : GetPlayerEntityForEnemyTarget();
		var hitTarget = hit.Hit && intended != null && IsHitOnNode(hit, intended);

		// Visuals: hitscan vs projectile.
		var isProjectile = wdef.ProjectileSpeed > 0.01f;
		float travelSeconds = 0f;
		if (_arenaWorld != null)
		{
			if (isProjectile)
				travelSeconds = ArenaVfx.SpawnProjectileShot(_arenaWorld, from, impactPos, fromPlayer: isPlayer, hit: hit.Hit, projectileSpeed: wdef.ProjectileSpeed);
			else
				ArenaVfx.SpawnShot(_arenaWorld, from, impactPos, fromPlayer: isPlayer, hit: hit.Hit);
		}

		void ApplyImpact()
		{
			if (!hitTarget) return;
			if (!isPlayer && IsPlayerOnFoot() && intended == _driverPawn)
				OnHitDriverOnFoot(damage, hit);
			else
				OnHit(isPlayer, damage, hit);

			// Weapon-specific impact SFX for vehicle hits.
			if (wSfx.HitVehicle != null)
				PlaySfx3D(wSfx.HitVehicle, impactPos, volumeDb: wSfx.HitVehicleVolumeDb);
			else if (isPlayer)
				PlaySfx(_sfxHit);

			// Hit marker overlay should appear at the hit location (screen position), not fixed center.
			if (isPlayer && _hitMarker != null && GodotObject.IsInstanceValid(_hitMarker))
			{
				var cam = _arenaWorld?.GetNodeOrNull<Camera3D>("CameraRig/Camera3D");
				if (cam != null)
				{
					var sp = cam.UnprojectPosition(impactPos);
					_hitMarker.FlashAt(sp, HitMarkerColor(hit));
				}
				else
				{
					_hitMarker.Flash(HitMarkerColor(hit));
				}
			}
		}

		if (isProjectile && travelSeconds > 0.001f)
		{
			// Apply damage at impact time for projectile weapons.
			try
			{
				var t = GetTree()?.CreateTimer(travelSeconds);
				if (t != null) t.Timeout += ApplyImpact;
				else ApplyImpact();
			}
			catch
			{
				ApplyImpact();
			}
		}
		else
		{
			// Hitscan: apply immediately.
			ApplyImpact();
		}

		// No "miss" sound. We only play explicit fire + hit SFX.

		// Cooldown is weapon-defined.
		shooter.FireCooldownRemaining = MathF.Max(0.02f, wdef.CooldownMs / 1000f);
		var who = isPlayer ? "You" : "Enemy";
		AddLog($"{who} fire {wdef.DisplayName} ({(hitTarget ? "hit" : "miss")}).");

		RefreshStats();
	}

	private void TryFireLegacy(bool isPlayer, VehiclePawn shooter)
	{
		if (_playerPawn == null || _enemyPawn == null || _playerVehicleRuntime == null) return;

		if (isPlayer)
		{
			if (!TryConsumeAmmo(ref _playerVehicleRuntime, GameBalance.PrimaryAmmoId, 1))
			{
				shooter.FireCooldownRemaining = 0.25f;
				AddLog("Click! Out of ammo.");
				return;
			}
		}
		else
		{
			if (_enemyAmmoRuntime <= 0)
			{
				shooter.FireCooldownRemaining = 0.50f;
				if (Random.Shared.NextDouble() < 0.06)
					AddLog("Enemy: out of ammo.");
				return;
			}
			_enemyAmmoRuntime = Math.Max(0, _enemyAmmoRuntime - 1);
		}

		var from = shooter.GetMuzzleWorldPosition();
		var dir = shooter.GetMuzzleWorldForward();
		if (!isPlayer)
		{
			var t = GetPlayerEntityForEnemyTarget();
			if (t is DriverPawn dp && GodotObject.IsInstanceValid(dp))
			{
				var aim = dp.GlobalPosition + Vector3.Up * 0.10f;
				dir = (aim - from).Normalized();
			}
		}
		var spread = isPlayer ? PlayerSpreadRad : EnemySpreadRad;
		var jitter = (float)(Random.Shared.NextDouble() * 2.0 - 1.0) * spread;
		dir = dir.Rotated(Vector3.Up, jitter).Normalized();

		var maxRange = 90f;
		var to = from + dir * maxRange;
		var hit = Raycast(from, to, shooter);
		var impactPos = hit.Hit ? hit.Position : to;
		if (_arenaWorld != null)
			ArenaVfx.SpawnShot(_arenaWorld, from, impactPos, fromPlayer: isPlayer, hit: hit.Hit);
		var damage = isPlayer ? Random.Shared.Next(12, 26) : Random.Shared.Next(6, 16);

		var intended = isPlayer ? (Node3D?)_enemyPawn : GetPlayerEntityForEnemyTarget();
		var hitTarget = hit.Hit && intended != null && IsHitOnNode(hit, intended);
		if (hitTarget)
		{
			if (!isPlayer && IsPlayerOnFoot() && intended == _driverPawn)
				OnHitDriverOnFoot(damage, hit);
			else
				OnHit(isPlayer, damage, hit);
		}

		if (isPlayer)
		{
			if (hitTarget)
			{
				PlaySfx(_sfxHit);
				_hitMarker?.Flash(HitMarkerColor(hit));
			}
			// No miss sound.
		}

		shooter.FireCooldownRemaining = shooter.FireCooldownSeconds;
		AddLog(isPlayer
			? (hitTarget ? "You fire (hit)." : "You fire (miss).")
			: (hitTarget ? "Enemy fires (hit)." : "Enemy fires (miss)."));

		RefreshStats();
	}

	private void TryDropMine(bool isPlayer, VehiclePawn shooter, string mountId)
	{
		if (_arenaWorld == null) return;
		_mineSeq++;
		// Drop behind the vehicle.
		var back = shooter.GlobalTransform.Basis.Z;
		back.Y = 0f;
		if (back.Length() < 0.001f) back = Vector3.Back;
		back = back.Normalized();
		var drop = shooter.GlobalPosition + back * 2.05f;
		drop.Y = 0.05f;

		var node = SpawnMineMarker(drop);
		var mine = new MineRuntime
		{
			Id = _mineSeq,
			FromPlayer = isPlayer,
			Position = drop,
			ArmRemaining = 0.40f,
			OwnerGraceRemaining = 1.10f,
			LifetimeRemaining = 22.0f,
			Armed = false,
			Node = node
		};
		_mines.Add(mine);
		AddLog(isPlayer ? "Mine dropped." : "Enemy drops a mine." );
	}


	private static bool TryResolveWeaponForSlot(
		DefDatabase defs,
		VehicleInstanceState veh,
		int slot,
		out string mountId,
		out InstalledWeaponState installed,
		out WeaponDefinition weaponDef,
		out string ammoId)
	{
		mountId = "";
		installed = null!;
		weaponDef = null!;
		ammoId = "";

		if (slot < 1) slot = 1;
		if (defs.Vehicles.TryGetValue(veh.DefinitionId, out var vdef) == false)
			return false;

		var installedMountIds = veh.InstalledWeaponsByMountId.Keys.ToHashSet();
		if (installedMountIds.Count == 0) return false;

		// Build a stable per-slot mapping based on mount locations.
		var ordered = vdef.MountPoints
			.Where(m => installedMountIds.Contains(m.MountId))
			.Select(m => (m.MountId, m.MountLocation))
			.ToList();

		string? pick = null;
		if (slot == 1) pick = ordered.FirstOrDefault(x => x.MountLocation == MountLocation.Front).MountId;
		else if (slot == 2) pick = ordered.FirstOrDefault(x => x.MountLocation == MountLocation.Top).MountId;
		else if (slot == 3) pick = ordered.FirstOrDefault(x => x.MountLocation == MountLocation.Rear).MountId;

		// Fallback: pick Nth installed mount by a location preference order.
		if (string.IsNullOrWhiteSpace(pick))
		{
			var pref = new[] { MountLocation.Front, MountLocation.Top, MountLocation.Left, MountLocation.Right, MountLocation.Rear };
			ordered.Sort((a, b) => Array.IndexOf(pref, a.MountLocation).CompareTo(Array.IndexOf(pref, b.MountLocation)));
			pick = slot <= ordered.Count ? ordered[slot - 1].MountId : ordered[0].MountId;
		}

		if (string.IsNullOrWhiteSpace(pick)) return false;
		if (!veh.InstalledWeaponsByMountId.TryGetValue(pick, out var inst)) return false;
		if (!defs.Weapons.TryGetValue(inst.WeaponId, out var wdef)) return false;

		var ammo = inst.SelectedAmmoId;
		if (string.IsNullOrWhiteSpace(ammo))
			ammo = (wdef.AmmoTypeIds != null && wdef.AmmoTypeIds.Length > 0) ? wdef.AmmoTypeIds[0] : "";
		if (string.IsNullOrWhiteSpace(ammo)) return false;

		mountId = pick;
		installed = inst;
		weaponDef = wdef;
		ammoId = ammo!;
		return true;
	}


	private static bool IsHitOnNode(RayHit hit, Node3D node)
	{
		if (hit.Collider is not Node n) return false;
		// Collider may be the node itself, or a child (CollisionShape3D / Area3D / etc).
		Node? cur = n;
		while (cur != null)
		{
			if (cur == node) return true;
			cur = cur.GetParent();
		}
		return false;
	}

	private static bool IsNodeOrParentInGroup(Node n, string group)
	{
		Node? cur = n;
		while (cur != null)
		{
			if (cur.IsInGroup(group)) return true;
			cur = cur.GetParent();
		}
		return false;
	}

	
	private sealed class MineRuntime
	{
		public int Id;
		public bool FromPlayer;
		public Vector3 Position;
		public float ArmRemaining;
		public float OwnerGraceRemaining;
		public float LifetimeRemaining;
		public bool Armed;
		public Node3D? Node;
	}

private struct RayHit
	{
		public bool Hit;
		public GodotObject? Collider;
		public Vector3 Position;
		public string Part;
		public ArmorSection? Section;
		public int TireIndex;
		public bool DriverHit;
	}

	private RayHit Raycast(Vector3 from, Vector3 to, VehiclePawn shooter)
	{
		var hit = new RayHit { Hit = false, Collider = null, Position = to, Part = "", Section = null, TireIndex = -1, DriverHit = false };
		var world = shooter.GetWorld3D();
		if (world == null) return hit;
		var space = world.DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		// Be explicit about collision mask so we reliably hit vehicle bodies (layer 1) and hitbox Areas (layer 7).
		query.CollisionMask = uint.MaxValue;
		query.CollideWithBodies = true;
		query.CollideWithAreas = true;
		query.Exclude = BuildRaycastExcludes(shooter);
		var res = space.IntersectRay(query);
		if (res.Count == 0) return hit;

		hit.Hit = true;
		if (res.TryGetValue("collider", out var colVar))
			hit.Collider = colVar.AsGodotObject();
		if (res.TryGetValue("position", out var posVar))
			hit.Position = posVar.AsVector3();

		// Identify part type from hitbox group.
		if (hit.Collider is Node n)
		{
			// DriverPawn (on-foot) doesn't use vehicle hitboxes; detect it by group on the parent chain.
			if (IsNodeOrParentInGroup(n, "driver_pawn") || IsNodeOrParentInGroup(n, "player_driver"))
			{
				hit.Part = "driver";
				hit.DriverHit = true;
			}
			else
			{
				// IntersectRay may return a child CollisionShape; walk up until we find our Area3D hitbox.
				var cur = n;
				while (cur != null && !cur.IsInGroup("hitbox"))
					cur = cur.GetParent();
				if (cur != null)
					n = cur;

				if (n.IsInGroup("hit_driver"))
				{
					hit.Part = "driver";
					hit.DriverHit = true;
				}
				else if (n.IsInGroup("hit_tire"))
				{
					hit.Part = "tire";
					hit.TireIndex = TireIndexFromHitbox(n);
				}
				else if (n.IsInGroup("hit_section"))
				{
					hit.Section = SectionFromHitbox(n);
					hit.Part = hit.Section?.ToString().ToLowerInvariant() ?? "section";
				}
				else if (n.IsInGroup("hit_body")) hit.Part = "body";
			}
		}
		return hit;
	}

	private static int TireIndexFromHitbox(Node n)
	{
		// Tire naming convention on VehiclePawn hitboxes.
		return n.Name.ToString() switch
		{
			"TireFL" => 0,
			"TireFR" => 1,
			"TireRL" => 2,
			"TireRR" => 3,
			_ => -1
		};
	}

	private static ArmorSection? SectionFromHitbox(Node n)
	{
		return n.Name.ToString() switch
		{
			"SecFront" => ArmorSection.Front,
			"SecRear" => ArmorSection.Rear,
			"SecLeft" => ArmorSection.Left,
			"SecRight" => ArmorSection.Right,
			"SecTop" => ArmorSection.Top,
			"SecUnder" => ArmorSection.Undercarriage,
			_ => null
		};
	}

	private static Godot.Collections.Array<Rid> BuildRaycastExcludes(VehiclePawn shooter)
	{
		var arr = new Godot.Collections.Array<Rid> { shooter.GetRid() };
		// Exclude non-blocking hitbox Areas under the shooter so we don't instantly "hit" ourselves.
		CollectHitboxRids(shooter, arr);
		return arr;
	}

	private static void CollectHitboxRids(Node node, Godot.Collections.Array<Rid> into)
	{
		foreach (var childObj in node.GetChildren())
		{
			if (childObj is not Node child) continue;
			if (child.IsInGroup("hitbox") && child is CollisionObject3D co)
				into.Add(co.GetRid());
			CollectHitboxRids(child, into);
		}
	}

	private void OnHit(bool fromPlayer, int damage, RayHit hit)
	{
		var defs = Defs();
		var partText = string.IsNullOrWhiteSpace(hit.Part) ? "" : $" ({hit.Part})";
		var dmg = Math.Max(0, damage);
		if (dmg <= 0) return;

		// Small chip-through so driver HP remains relevant during this transition.
		var chip = Math.Max(1, (int)MathF.Round(dmg * 0.15f));

		if (fromPlayer)
		{
			var remaining = 0;
			var oldTireHp = -1;
			if (hit.TireIndex >= 0 && _enemyVehicleRuntime != null && _enemyVehicleRuntime.CurrentTireHp.Length > hit.TireIndex)
				oldTireHp = _enemyVehicleRuntime.CurrentTireHp[hit.TireIndex];
			if (_enemyVehicleRuntime != null && defs != null && defs.Vehicles.TryGetValue(_enemyVehicleRuntime.DefinitionId, out var vdef))
			{
				if (hit.TireIndex >= 0)
					_enemyVehicleRuntime = VehicleCombatMath.ApplyDamageToTire(_enemyVehicleRuntime, vdef, hit.TireIndex, dmg, out remaining);
				else if (hit.Section != null)
					_enemyVehicleRuntime = VehicleCombatMath.ApplyDamageToSection(_enemyVehicleRuntime, vdef, hit.Section.Value, dmg, out remaining);
				else
					_enemyVehicleRuntime = VehicleCombatMath.ApplyDamageToVehicle(_enemyVehicleRuntime, dmg);
			}

			if (hit.TireIndex >= 0 && oldTireHp > 0 && _enemyVehicleRuntime != null && _enemyVehicleRuntime.CurrentTireHp.Length > hit.TireIndex && _enemyVehicleRuntime.CurrentTireHp[hit.TireIndex] <= 0)
				PlaySfx(_sfxTirePop);

			var driverDamage = hit.DriverHit ? dmg : (remaining > 0 ? remaining : chip);
			ApplyEnemyDriverDamage(driverDamage);
			AddLog($"Hit enemy for {dmg}{partText}. Driver dmg {driverDamage}. Enemy HP: {_enemyHpRuntime}/{_enemyHpMaxRuntime}. AP: {_enemyArmorRuntime}/{_enemyArmorMaxRuntime}.");
		}
		else
		{
			if (_playerVehicleRuntime == null) return;
			var remaining = 0;
			var oldTireHp = -1;
			if (hit.TireIndex >= 0 && _playerVehicleRuntime.CurrentTireHp.Length > hit.TireIndex)
				oldTireHp = _playerVehicleRuntime.CurrentTireHp[hit.TireIndex];
			if (defs != null && defs.Vehicles.TryGetValue(_playerVehicleRuntime.DefinitionId, out var vdef))
			{
				if (hit.TireIndex >= 0)
					_playerVehicleRuntime = VehicleCombatMath.ApplyDamageToTire(_playerVehicleRuntime, vdef, hit.TireIndex, dmg, out remaining);
				else if (hit.Section != null)
					_playerVehicleRuntime = VehicleCombatMath.ApplyDamageToSection(_playerVehicleRuntime, vdef, hit.Section.Value, dmg, out remaining);
				else
					_playerVehicleRuntime = VehicleCombatMath.ApplyDamageToVehicle(_playerVehicleRuntime, dmg);
			}
			else
			{
				_playerVehicleRuntime = VehicleCombatMath.ApplyDamageToVehicle(_playerVehicleRuntime, dmg);
			}

			if (hit.TireIndex >= 0 && oldTireHp > 0 && _playerVehicleRuntime.CurrentTireHp.Length > hit.TireIndex && _playerVehicleRuntime.CurrentTireHp[hit.TireIndex] <= 0)
				PlaySfx(_sfxTirePop);

			var driverDamage = hit.DriverHit ? dmg : (remaining > 0 ? remaining : chip);
			ApplyDriverDamage(driverDamage);
			AddLog($"You take {dmg} dmg{partText}. Driver dmg {driverDamage}. HP: {_playerHpRuntime}/{_playerHpMaxRuntime}. AP: {_playerArmorRuntime}/{_playerArmorMaxRuntime}.");
		}

		RefreshStats();
	}

	
	private void OnHitDriverOnFoot(int damage, RayHit hit)
	{
		var dmg = Math.Max(0, damage);
		if (dmg <= 0) return;

		// Treat this like taking a full-strength hit with no vehicle armor protecting you.
		ApplyDriverDamage(dmg);

		var partText = string.IsNullOrWhiteSpace(hit.Part) ? "" : $" ({hit.Part})";
		AddLog($"You are hit on-foot for {dmg} dmg{partText}. HP: {_playerHpRuntime}/{_playerHpMaxRuntime}. AP: {_playerArmorRuntime}/{_playerArmorMaxRuntime}.");

		RefreshStats();
	}

	private void UpdateDriverCollisionDamage(float dt)
	{
		// Only relevant when the player is on-foot and alive.
		if (!IsPlayerOnFoot()) { _driverCollisionCooldown = 0f; return; }
		if (_driverPawn == null || !GodotObject.IsInstanceValid(_driverPawn)) return;
		if (_playerHpRuntime <= 0 || _playerKilledThisMatch) return;

		_driverCollisionCooldown -= dt;
		if (_driverCollisionCooldown > 0f) return;

		if (!TryGetDriverVehicleCollision(_driverPawn, out var other, out var vehicleSpeed))
			return;

		if (vehicleSpeed < DriverCollisionMinSpeed) return;

		var dmg = Math.Max(1, (int)MathF.Round(DriverCollisionDamageBase + vehicleSpeed * vehicleSpeed * DriverCollisionDamageSpeedSqFactor));
		ApplyDriverDamage(dmg);

		_driverCollisionCooldown = DriverCollisionCooldownSeconds;

		if (_arenaWorld != null && GodotObject.IsInstanceValid(_arenaWorld))
			ArenaVfx.SpawnSparks(_arenaWorld, _driverPawn.GlobalPosition + Vector3.Up * 0.15f, count: 4);

		var who = other != null && GodotObject.IsInstanceValid(other) ? other.Name.ToString() : "a vehicle";
		AddLog($"Collision with {who} ({vehicleSpeed:0.0} m/s)! You take {dmg} dmg (on-foot). HP: {_playerHpRuntime}/{_playerHpMaxRuntime}. AP: {_playerArmorRuntime}/{_playerArmorMaxRuntime}.");

		RefreshStats();
	}

	private static bool TryGetDriverVehicleCollision(DriverPawn driver, out VehiclePawn? vehicle, out float vehicleSpeed)
	{
		vehicle = null;
		vehicleSpeed = 0f;

		var world = driver.GetWorld3D();
		if (world == null) return false;

		var space = world.DirectSpaceState;
		var shape = new SphereShape3D { Radius = DriverCollisionProbeRadius };

		var q = new PhysicsShapeQueryParameters3D
		{
			Shape = shape,
			Transform = new Transform3D(Basis.Identity, driver.GlobalPosition),
			CollisionMask = 1u,
			CollideWithBodies = true,
			CollideWithAreas = false
		};

		q.Exclude = new Godot.Collections.Array<Rid> { driver.GetRid() };

		Godot.Collections.Array<Godot.Collections.Dictionary>? hits = null;
		try
		{
			hits = space.IntersectShape(q, 8);
		}
		catch
		{
			return false;
		}

		if (hits == null || hits.Count == 0) return false;

		foreach (var h in hits)
		{
			if (!h.TryGetValue("collider", out var colVar)) continue;
			var obj = colVar.AsGodotObject();
			if (obj is not Node n) continue;

			// Walk up to find the owning VehiclePawn (ray/shape queries can return child CollisionShapes).
			Node? cur = n;
			while (cur != null && cur is not VehiclePawn)
				cur = cur.GetParent();
			if (cur is not VehiclePawn vp) continue;

			// Only apply on-foot collision damage when the vehicle is actually moving.
			var v = vp.Velocity;
			v.Y = 0f;
			var spd = v.Length();
			if (spd > vehicleSpeed)
			{
				vehicleSpeed = spd;
				vehicle = vp;
			}
		}

		return vehicle != null;
	}


	private void ApplyDriverDamage(int damage)
	{
		var remaining = Math.Max(0, damage);
		if (remaining <= 0) return;

		if (_playerArmorRuntime > 0)
		{
			var absorbed = Math.Min(_playerArmorRuntime, remaining);
			_playerArmorRuntime = Math.Max(0, _playerArmorRuntime - absorbed);
			remaining -= absorbed;
		}

		if (remaining > 0)
			_playerHpRuntime = Math.Max(0, _playerHpRuntime - remaining);

		// If we were killed while on-foot, trigger the fall/death animation immediately.
		if (_playerHpRuntime <= 0 && IsPlayerOnFoot() && _driverPawn != null && GodotObject.IsInstanceValid(_driverPawn))
			_driverPawn.TriggerDeath();
	}

	private void ApplyEnemyDriverDamage(int damage)
	{
		var remaining = Math.Max(0, damage);
		if (remaining <= 0) return;

		if (_enemyArmorRuntime > 0)
		{
			var absorbed = Math.Min(_enemyArmorRuntime, remaining);
			_enemyArmorRuntime = Math.Max(0, _enemyArmorRuntime - absorbed);
			remaining -= absorbed;
		}

		if (remaining > 0)
			_enemyHpRuntime = Math.Max(0, _enemyHpRuntime - remaining);
	}

	private static bool TryConsumeAmmo(ref VehicleInstanceState veh, string ammoId, int count)
	{
		if (count <= 0) return true;
		var inv = veh.AmmoInventory ?? new Dictionary<string, int>();
		inv.TryGetValue(ammoId, out var cur);
		if (cur < count) return false;

		var next = cur - count;
		var updated = new Dictionary<string, int>(inv);
		if (next <= 0) updated.Remove(ammoId);
		else updated[ammoId] = next;
		veh = veh with { AmmoInventory = updated };
		return true;
	}

	private void AddLog(string line)
	{
		_runtimeLog.Add(line);
		const int maxLines = 40;
		while (_runtimeLog.Count > maxLines)
			_runtimeLog.RemoveAt(0);

		Console()?.Status(line);
	}

	private void RefreshStats()
	{
		// Vehicle HUD visibility is controlled here (not in the pre-fight dialog state).
		if (_vehicleStatusHudRoot != null && !GodotObject.IsInstanceValid(_vehicleStatusHudRoot))
		{
			_vehicleStatusHudRoot = null;
			_vehicleStatusHud = null;
			_vehicleStatusHudFallback = null;
		}

		var session = Session();
		var enc = session?.GetCurrentEncounter();

		// When combat is not live, mirror persisted driver stats.
		if (!_combatLive && session != null)
		{
			_playerHpMaxRuntime = Math.Max(1, session.GetDriverHpMax());
			_playerHpRuntime = Math.Clamp(session.GetDriverHp(), 0, _playerHpMaxRuntime);
			_playerArmorMaxRuntime = Math.Max(1, session.GetDriverArmorMax());
			_playerArmorRuntime = Math.Clamp(session.GetDriverArmor(), 0, _playerArmorMaxRuntime);
		}
		if (_playerPawn != null && _playerVehicleRuntime != null)
			_playerPawn.SetRuntimeState(_playerVehicleRuntime);
		if (_enemyPawn != null && _enemyVehicleRuntime != null)
			_enemyPawn.SetRuntimeState(_enemyVehicleRuntime);

		var speed = _playerPawn != null ? new Vector3(_playerPawn.Velocity.X, 0f, _playerPawn.Velocity.Z).Length() : 0f;
		var maxSpeed = _playerPawn != null ? _playerPawn.EffectiveMaxForwardSpeed : 0f;

		var targetName = _selectedTarget != null && GodotObject.IsInstanceValid(_selectedTarget)
			? _selectedTarget.Name.ToString()
			: "none";
		// For now there's only one enemy pawn; display its driver stats.
		if (targetName != "none" && _selectedTarget == _enemyPawn)
			_targetStatusHud.SetTarget(targetName, _enemyHpRuntime, _enemyHpMaxRuntime, _enemyArmorRuntime, _enemyArmorMaxRuntime);
		else
			_targetStatusHud.SetTarget("none", 0, 1, 0, 1);

		var defs = Defs();

		// Player status HUD (top-right): driver HP + driver armor AP.
		_playerStatusHud.SetValues(_playerHpRuntime, _playerHpMaxRuntime, _playerArmorRuntime, _playerArmorMaxRuntime);

		// Vehicle status HUD (top-right, under player HUD): show only during match/post-match UI.
		var inMatchUiContext = _combatLive || _postMatchAwaitingExit || (_postPanel?.Visible == true);
		var showVehicleHud = inMatchUiContext && _playerControlMode == PlayerControlMode.Vehicle;

		// Resolve the vehicle definition separately (avoid relying on short-circuit && chains for
		// definite assignment / nullability).
		VehicleDefinition? vdef = null;
		if (_playerVehicleRuntime != null && defs != null)
			defs.Vehicles.TryGetValue(_playerVehicleRuntime.DefinitionId, out vdef);

		var hasHudRoot = _vehicleStatusHudRoot != null && GodotObject.IsInstanceValid(_vehicleStatusHudRoot);
		var hasTypedHud = _vehicleStatusHud != null && GodotObject.IsInstanceValid(_vehicleStatusHud);
		var hasFallbackHud = _vehicleStatusHudFallback?.IsBound == true;
		var canUpdateVehicleHud = showVehicleHud
			&& hasHudRoot
			&& (hasTypedHud || hasFallbackHud)
			&& _playerVehicleRuntime != null
			&& defs != null
			&& vdef != null;

		if (hasHudRoot)
			_vehicleStatusHudRoot.Visible = canUpdateVehicleHud;

		if (canUpdateVehicleHud)
		{
			var rpm01 = _playerPawn != null ? _playerPawn.GetEngineRpm01ForHud() : 0f;
			var rpmVal = _playerPawn != null ? _playerPawn.GetEngineDisplayRpmForHud() : 0;
			var gear = _playerPawn != null ? _playerPawn.GetEngineGearDisplayForHud() : "1";

			if (hasTypedHud)
			{
				_vehicleStatusHud!.SetVehicle(vdef!, _playerVehicleRuntime!, defs!, speed, maxSpeed, rpm01, rpmVal, gear, _playerPawn?.BodyColor);
			}
			else if (hasFallbackHud)
			{
				_vehicleStatusHudFallback!.SetVehicle(vdef!, _playerVehicleRuntime!, defs!, speed, maxSpeed, rpm01, rpmVal, gear);
			}
		}


		_lblStats.Text = $"Tier: {_selectedTier}";
		var encounterActive = _combatLive || _postMatchAwaitingExit || (_postPanel?.Visible == true);
		_btnFlee.Disabled = !_combatLive;
		_btnStart.Disabled = encounterActive;
	}

	private void ResolveOutcome(string outcome)
	{
		if (!_combatLive) return;
		var session = Session();
		if (session is null || _playerVehicleRuntime is null) return;

		_combatLive = false;

		if (!session.ResolveArenaEncounterRealtime(outcome, _playerVehicleRuntime, _enemyHpRuntime, _playerArmorRuntime, _playerHpRuntime, _runtimeLog.ToArray(), out var finalVehicle, out var err))
		{
			_lblStatus.Text = $"Status: {err}";
			Console()?.Error(err);
			return;
		}

		_playerVehicleRuntime = finalVehicle;
		_playerHpMaxRuntime = Math.Max(1, session.GetDriverHpMax());
		_playerHpRuntime = Math.Clamp(session.GetDriverHp(), 0, _playerHpMaxRuntime);
		_playerArmorMaxRuntime = Math.Max(1, session.GetDriverArmorMax());
		_playerArmorRuntime = Math.Clamp(session.GetDriverArmor(), 0, _playerArmorMaxRuntime);
		_lblStatus.Text = $"Status: Encounter resolved: {outcome}";
		EnterPostMatch(outcome);
		RefreshStats();
	}

	private void EnterPostMatch(string outcome)
	{
		_postMatchOutcome = outcome;
		_postMatchAwaitingExit = true;
		_exitHoldSeconds = 0f;
		_postPanel.Visible = false;
		_playerKilledThisMatch = string.Equals(outcome, "lose", StringComparison.OrdinalIgnoreCase);
		_enemyKilledThisMatch = string.Equals(outcome, "win", StringComparison.OrdinalIgnoreCase);

		// Stop enemy movement immediately.
		if (_enemyPawn != null && GodotObject.IsInstanceValid(_enemyPawn))
		{
			_enemyPawn.ThrottleInput = 0f;
			_enemyPawn.SteerInput = 0f;
		}

		// If the player died, stop player movement immediately as well.
		if (_playerPawn != null && GodotObject.IsInstanceValid(_playerPawn) && (_playerHpRuntime <= 0 || _playerKilledThisMatch))
		{
			_playerPawn.ThrottleInput = 0f;
			_playerPawn.SteerInput = 0f;
		}

		var msg = outcome switch
		{
			"win" => "You have won the match, hold G to exit",
			"lose" => "You have lost the match, hold G to exit",
			_ => $"Match ended ({outcome}), hold G to exit"
		};

		_lblMatchEnd.Text = msg;
		_lblMatchEnd.Visible = true;
		RefreshArenaDialogVisibility();
	}

	private void ShowPostPanel()
	{
		var session = Session();
		if (session is null) return;
		var enc = session.GetCurrentEncounter();
		if (enc is null || enc.Outcome is null)
		{
			_postPanel.Visible = false;
			return;
		}

		_postPanel.Visible = true;
		RefreshArenaDialogVisibility();
		var ammoRewardText = enc.AmmoRewards is { Length: > 0 }
			? string.Join(", ", enc.AmmoRewards.Select(a => $"{a.Count} {a.AmmoId}"))
			: "";

		_lblRewards.Text = $"Outcome: {enc.Outcome}\nMoney: +${enc.MoneyRewardUsd}\nScrap: +{enc.ScrapReward}\nAmmo: {ammoRewardText}";

		// Post actions: driver armor repair cost.
		var (missingAp, apCost) = session.ComputeDriverArmorRepairCost();
		if (missingAp <= 0)
		{
			_btnRepairDriverArmor.Text = "Repair Armor (Full)";
			_btnRepairDriverArmor.Disabled = true;
		}
		else
		{
			_btnRepairDriverArmor.Text = $"Repair Armor (-${apCost})";
			_btnRepairDriverArmor.Disabled = session.Save.Player.MoneyUsd < apCost;
		}
	}

	private void QuickRepair()
	{
		var session = Session();
		var defs = Defs();
		if (session is null || defs is null) return;
		var activeId = session.Save.Player.ActiveVehicleId;
		if (string.IsNullOrWhiteSpace(activeId))
		{
			const string msg = "No active vehicle selected.";
			_lblStatus.Text = $"Status: {msg}";
			Console()?.Error(msg);
			return;
		}
		if (string.IsNullOrWhiteSpace(activeId))
		{
			const string msg = "No active vehicle selected.";
			_lblStatus.Text = $"Status: {msg}";
			Console()?.Error(msg);
			return;
		}
		if (string.IsNullOrWhiteSpace(activeId))
		{
			const string msg = "No active vehicle selected.";
			_lblStatus.Text = $"Status: {msg}";
			Console()?.Error(msg);
			return;
		}
		if (!session.TryRepairVehicleToFull(activeId, defs, out var err))
		{
			_lblStatus.Text = $"Status: {err}";
			Console()?.Error(err);
			return;
		}
		_playerVehicleRuntime = session.Save.Vehicles.FirstOrDefault(v => v.InstanceId == activeId) ?? _playerVehicleRuntime;
		Console()?.Status("Quick repair applied.");
		ShowPostPanel();
		RefreshStats();
	}

	private void RepairDriverArmor()
	{
		var session = Session();
		if (session is null) return;
		var (missing, cost) = session.ComputeDriverArmorRepairCost();
		if (missing <= 0)
		{
			_lblStatus.Text = "Status: Armor already full.";
			return;
		}
		if (!session.TryRepairDriverArmorToFull(out var err))
		{
			_lblStatus.Text = $"Status: {err}";
			Console()?.Error(err);
			ShowPostPanel();
			RefreshStats();
			return;
		}

		_playerArmorMaxRuntime = Math.Max(1, session.GetDriverArmorMax());
		_playerArmorRuntime = Math.Clamp(session.GetDriverArmor(), 0, _playerArmorMaxRuntime);
		_lblStatus.Text = $"Status: Armor repaired (-${cost}).";
		ShowPostPanel();
		RefreshStats();
	}

	private void PatchArmor()
	{
		var session = Session();
		var defs = Defs();
		if (session is null || defs is null) return;
		var activeId = session.Save.Player.ActiveVehicleId;
		if (string.IsNullOrWhiteSpace(activeId))
		{
			const string msg = "No active vehicle selected.";
			_lblStatus.Text = $"Status: {msg}";
			Console()?.Error(msg);
			return;
		}
		if (!session.TryPatchArmorWithScrap(activeId, defs, out var err))
		{
			_lblStatus.Text = $"Status: {err}";
			Console()?.Error(err);
			return;
		}
		_playerVehicleRuntime = session.Save.Vehicles.FirstOrDefault(v => v.InstanceId == activeId) ?? _playerVehicleRuntime;
		Console()?.Status("Patched hull with scrap.");
		ShowPostPanel();
		RefreshStats();
	}

	private void PatchTire()
	{
		var session = Session();
		var defs = Defs();
		if (session is null || defs is null) return;
		var activeId = session.Save.Player.ActiveVehicleId;
		if (string.IsNullOrWhiteSpace(activeId))
		{
			const string msg = "No active vehicle selected.";
			_lblStatus.Text = $"Status: {msg}";
			Console()?.Error(msg);
			return;
		}
		if (!session.TryPatchTireWithScrap(activeId, defs, out var err))
		{
			_lblStatus.Text = $"Status: {err}";
			Console()?.Error(err);
			return;
		}
		_playerVehicleRuntime = session.Save.Vehicles.FirstOrDefault(v => v.InstanceId == activeId) ?? _playerVehicleRuntime;
		Console()?.Status("Patched tire with scrap.");
		ShowPostPanel();
		RefreshStats();
	}

	private void Flee() => ResolveOutcome("fled");

	private void Back()
	{
		ForceFleeIfLive("back");
		ReturnToCity();
	}


	private void CloseResults()
	{
		// Close the post-encounter results panel and return to the pre-fight arena dialog.
		_postPanel.Visible = false;
		_lblMatchEnd.Visible = false;
		_postMatchAwaitingExit = false;
		_combatLive = false;

		// Clear the 3D world so we're back to a clean pre-fight state.
		ClearWorld();
		ResetUi();
		RefreshArenaDialogVisibility();
		RefreshStats();
		_lblStatus.Text = "Status: Ready.";
	}

	private void ReturnToCity()
	{
		ForceFleeIfLive("return");
		ClearWorld();
		var app = App.Instance;
		if (app == null) return;
			if (!app.Services.TryGet<IGameNavigator>(out var nav) || nav == null)
			{
				GD.PrintErr("[ArenaRealtimeView] IGameNavigator not registered (cannot navigate back to CityShell).");
				return;
			}

			nav.ToCityShell(this);
	}

	// --- Vehicle HUD fallback ------------------------------------------------
	// If Godot fails to bind VehicleStatusHud.cs (leaving the node as a plain PanelContainer), we still
	// want a functional HUD. This binder updates the VehicleStatusHud scene controls directly.
	private sealed class VehicleStatusHudFallback
	{
		private readonly Control _root;
		private bool _bound;

		public bool IsBound
		{
			get
			{
				TryBind();
				return _bound;
			}
		}

		private Label? _lblVehicleName;
		private Label? _lblVehicleMass;
		private Label? _lblVehicleMassDetail;
		private Label? _lblWeaponsList;

		private ValueBar? _frontHp;
		private ValueBar? _frontAp;
		private ValueBar? _rearHp;
		private ValueBar? _rearAp;
		private ValueBar? _leftHp;
		private ValueBar? _leftAp;
		private ValueBar? _rightHp;
		private ValueBar? _rightAp;
		private ValueBar? _topHp;
		private ValueBar? _topAp;
		private ValueBar? _underHp;
		private ValueBar? _underAp;
		private ValueBar? _tireFlHp;
		private ValueBar? _tireFlAp;
		private ValueBar? _tireFrHp;
		private ValueBar? _tireFrAp;
		private ValueBar? _tireRlHp;
		private ValueBar? _tireRlAp;
		private ValueBar? _tireRrHp;
		private ValueBar? _tireRrAp;
		private ValueBar? _speedBar;
		private ValueBar? _rpmBar;

		public VehicleStatusHudFallback(Control root)
		{
			_root = root;
			TryBind();
		}

		public void SetVehicle(VehicleDefinition def, VehicleInstanceState inst, DefDatabase defs,
			float speedCur, float speedMax, float rpm01, int rpmValue, string gearDisplay)
		{
			TryBind();
			if (!_bound) return;

			_lblVehicleName!.Text = $"Vehicle: {def.DisplayName}";

			var bd = VehicleMassMath.ComputeBreakdown(def, inst, defs);
			_lblVehicleMass!.Text = $"Mass: {FormatKg(bd.TotalKg)}";
			_lblVehicleMassDetail!.Text = $"V {FormatKg(bd.VehicleKg)}  W {FormatKg(bd.WeaponsKg)}  A {FormatKg(bd.AmmoKg)}" + (bd.TowedKg > 0.5f ? $"  Tow {FormatKg(bd.TowedKg)}" : "");

			SetSection(def, inst, ArmorSection.Front, _frontHp!, _frontAp!);
			SetSection(def, inst, ArmorSection.Rear, _rearHp!, _rearAp!);
			SetSection(def, inst, ArmorSection.Left, _leftHp!, _leftAp!);
			SetSection(def, inst, ArmorSection.Right, _rightHp!, _rightAp!);
			SetSection(def, inst, ArmorSection.Top, _topHp!, _topAp!);
			SetSection(def, inst, ArmorSection.Undercarriage, _underHp!, _underAp!);

			SetTire(def, inst, 0, _tireFlHp!, _tireFlAp!);
			SetTire(def, inst, 1, _tireFrHp!, _tireFrAp!);
			SetTire(def, inst, 2, _tireRlHp!, _tireRlAp!);
			SetTire(def, inst, 3, _tireRrHp!, _tireRrAp!);

			UpdateWeaponsList(inst, defs);
			UpdateSpeedAndRpm(speedCur, speedMax, rpm01, rpmValue, gearDisplay);
		}

		public void UpdateDynamic(VehicleInstanceState inst, DefDatabase defs,
			float speedCur, float speedMax, float rpm01, int rpmValue, string gearDisplay)
		{
			TryBind();
			if (!_bound) return;
			UpdateWeaponsList(inst, defs);
			UpdateSpeedAndRpm(speedCur, speedMax, rpm01, rpmValue, gearDisplay);
		}

		private void TryBind()
		{
			if (_bound) return;
			if (!GodotObject.IsInstanceValid(_root)) return;

			// Labels
			_lblVehicleName = NodeAt<Label>("VBox/LblVehicleName");
			_lblVehicleMass = NodeAt<Label>("VBox/LblVehicleMass");
			_lblVehicleMassDetail = NodeAt<Label>("VBox/LblVehicleMassDetail");
			_lblWeaponsList = NodeAt<Label>("VBox/BottomVBox/WeaponsBox/WeaponsListMargin/LblWeaponsList");

			// Section bars
			_frontHp = NodeAt<ValueBar>("VBox/FrontBox/FrontCenter/VBoxBars/FrontHp");
			_frontAp = NodeAt<ValueBar>("VBox/FrontBox/FrontCenter/VBoxBars/FrontAp");
			_rearHp = NodeAt<ValueBar>("VBox/RearBox/RearCenter/VBoxBars/RearHp");
			_rearAp = NodeAt<ValueBar>("VBox/RearBox/RearCenter/VBoxBars/RearAp");

			_leftHp = NodeAt<ValueBar>("VBox/MidRow/LeftBox/LeftMargin/BarsCenter/HBoxBars/LeftHp")
				?? NodeAt<ValueBar>("VBox/MidRow/LeftBox/BarsCenter/HBoxBars/LeftHp");
			_leftAp = NodeAt<ValueBar>("VBox/MidRow/LeftBox/LeftMargin/BarsCenter/HBoxBars/LeftAp")
				?? NodeAt<ValueBar>("VBox/MidRow/LeftBox/BarsCenter/HBoxBars/LeftAp");
			_rightHp = NodeAt<ValueBar>("VBox/MidRow/RightBox/RightMargin/BarsCenter/HBoxBars/RightHp")
				?? NodeAt<ValueBar>("VBox/MidRow/RightBox/BarsCenter/HBoxBars/RightHp");
			_rightAp = NodeAt<ValueBar>("VBox/MidRow/RightBox/RightMargin/BarsCenter/HBoxBars/RightAp")
				?? NodeAt<ValueBar>("VBox/MidRow/RightBox/BarsCenter/HBoxBars/RightAp");

			_topHp = NodeAt<ValueBar>("VBox/BottomVBox/TopUnderCenter/TopUnderBox/TopRow/TopHp");
			_topAp = NodeAt<ValueBar>("VBox/BottomVBox/TopUnderCenter/TopUnderBox/TopRow/TopAp");
			_underHp = NodeAt<ValueBar>("VBox/BottomVBox/TopUnderCenter/TopUnderBox/UnderRow/UnderHp");
			_underAp = NodeAt<ValueBar>("VBox/BottomVBox/TopUnderCenter/TopUnderBox/UnderRow/UnderAp");

			// Tires
			_tireFlHp = NodeAt<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireFL/VBoxBars/Hp");
			_tireFlAp = NodeAt<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireFL/VBoxBars/Ap");
			_tireFrHp = NodeAt<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireFR/VBoxBars/Hp");
			_tireFrAp = NodeAt<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireFR/VBoxBars/Ap");
			_tireRlHp = NodeAt<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireRL/VBoxBars/Hp");
			_tireRlAp = NodeAt<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireRL/VBoxBars/Ap");
			_tireRrHp = NodeAt<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireRR/VBoxBars/Hp");
			_tireRrAp = NodeAt<ValueBar>("VBox/BottomVBox/Tires/TiresCenter/Grid/TireRR/VBoxBars/Ap");

			// Speed/RPM
			_speedBar = NodeAt<ValueBar>("VBox/BottomVBox/SpeedBox/SpeedRow/SpeedBar")
				?? NodeAt<ValueBar>("VBox/BottomVBox/SpeedBox/SpeedCenter/SpeedBar");
			_rpmBar = NodeAt<ValueBar>("VBox/BottomVBox/SpeedBox/RpmRow/RpmBar");

			_bound = _lblVehicleName != null
				&& _lblVehicleMass != null
				&& _lblVehicleMassDetail != null
				&& _lblWeaponsList != null
				&& _frontHp != null && _frontAp != null
				&& _rearHp != null && _rearAp != null
				&& _leftHp != null && _leftAp != null
				&& _rightHp != null && _rightAp != null
				&& _topHp != null && _topAp != null
				&& _underHp != null && _underAp != null
				&& _tireFlHp != null && _tireFlAp != null
				&& _tireFrHp != null && _tireFrAp != null
				&& _tireRlHp != null && _tireRlAp != null
				&& _tireRrHp != null && _tireRrAp != null
				&& _speedBar != null && _rpmBar != null;

			if (_bound)
			{
				// Mark vertical bars.
				_leftHp!.Vertical = true;
				_leftAp!.Vertical = true;
				_rightHp!.Vertical = true;
				_rightAp!.Vertical = true;
			}
		}

		private T? NodeAt<T>(string path) where T : Node
			=> _root.GetNodeOrNull<Node>(path) as T;

		private void UpdateWeaponsList(VehicleInstanceState inst, DefDatabase defs)
		{
			if (inst.InstalledWeaponsByMountId.Count == 0)
			{
				_lblWeaponsList!.Text = "(none)";
				return;
			}

			var lines = new List<string>();
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

			_lblWeaponsList!.Text = string.Join("\n", lines);
		}

		private void UpdateSpeedAndRpm(float speedCur, float speedMax, float rpm01, int rpmValue, string gearDisplay)
		{
			_speedBar!.SetValues(speedCur, MathF.Max(0.01f, speedMax), new Color(0.90f, 0.75f, 0.20f), Colors.White);
			rpm01 = Mathf.Clamp(rpm01, 0f, 1f);
			_rpmBar!.SetCustom(rpm01, $"{rpmValue:0000} - [{gearDisplay}]", new Color(0.90f, 0.75f, 0.20f), Colors.White);
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
			if (pct >= 0.999) return new Color(0.35f, 0.80f, 0.35f);
			if (pct >= 0.70) return new Color(0.10f, 0.55f, 0.10f);
			if (pct >= 0.30) return new Color(1.00f, 0.90f, 0.20f);
			if (pct >= 0.10) return new Color(0.55f, 0.05f, 0.05f);
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

		private static string FormatKg(float kg)
		{
			kg = MathF.Max(0f, kg);
			if (kg >= 10000f) return $"{MathF.Round(kg):0} kg";
			if (kg >= 1000f) return $"{kg:0} kg";
			return $"{kg:0} kg";
		}
	}

	// --- Input -------------------------------------------------------------
	private void EnsureInputActions()
	{
		EnsureActionIfMissing("ws_move_forward", KeyEvent(Key.W), KeyEvent(Key.Up));
		EnsureActionIfMissing("ws_move_backward", KeyEvent(Key.S), KeyEvent(Key.Down));
		EnsureActionIfMissing("ws_steer_left", KeyEvent(Key.A), KeyEvent(Key.Left));
		EnsureActionIfMissing("ws_steer_right", KeyEvent(Key.D), KeyEvent(Key.Right));
		EnsureActionIfMissing("ws_fire_1", KeyEvent(Key.Space));
		// NOTE: Godot key mapping doesn't reliably distinguish left/right Shift/Ctrl across platforms.
		// We bind to Shift/Ctrl generally; in-game we treat this as RightShift/RightCtrl for now.
		EnsureActionIfMissing("ws_fire_2", KeyEvent(Key.Shift));
		EnsureActionIfMissing("ws_fire_3", KeyEvent(Key.Ctrl));
		// Back-compat: old action name still triggers weapon 1.
		EnsureActionIfMissing("ws_fire", KeyEvent(Key.Space));
		EnsureActionIfMissing("ws_target_next", KeyEvent(Key.Tab));
		EnsureActionIfMissing("ws_interact", KeyEvent(Key.E));
		EnsureActionIfMissing("ws_sprint", KeyEvent(Key.Shift));
		EnsureActionIfMissing("ws_exit_match", KeyEvent(Key.G));
	}

	private static void EnsureActionIfMissing(string action, params InputEvent[] eventsToAdd)
	{
		if (InputMap.HasAction(action)) return;
		InputMap.AddAction(action);
		foreach (var ev in eventsToAdd)
			InputMap.ActionAddEvent(action, ev);
	}

	private static InputEventKey KeyEvent(Key key) => new() { Keycode = key };
}