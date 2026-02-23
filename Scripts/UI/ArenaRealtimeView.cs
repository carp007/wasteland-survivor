using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game.Arena;
using WastelandSurvivor.Game.Systems;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// 3D real-time arena prototype (2.5D camera). Uses the existing encounter/reward loop,
/// but runs combat in the shared 3D WorldRoot.
/// </summary>
public partial class ArenaRealtimeView : Control
{
	private Label _lblStatus = null!;
	private Label _lblStats = null!;
	private TargetStatusHud _targetStatusHud = null!;
	private PlayerStatusHud _playerStatusHud = null!;
	private VehicleStatusHud _vehicleStatusHud = null!;

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

	private int _selectedTier = 1;

	private Node3D? _worldRoot;
	private ArenaWorld? _arenaWorld;
	private PackedScene _worldScene = null!;
	private PackedScene _vehScene = null!;

	private VehiclePawn? _playerPawn;
	private VehiclePawn? _enemyPawn;
	private VehiclePawn? _selectedTarget;
	private TargetIndicator3D? _targetIndicator;
	private int _targetCycleIndex = 0;
	private float _enemyUnstuckRemaining = 0f;
	private float _hudDynamicRemaining = 0f;

	// UI feedback (hit marker + SFX)
	private HitMarkerOverlay? _hitMarker;
	private AudioStreamPlayer? _sfxPlayer;
	private AudioStream? _sfxHit;
	private AudioStream? _sfxMiss;
	private AudioStream? _sfxTirePop;

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
	private readonly List<string> _runtimeLog = new();

	// Tuning
	private const float PlayerSpreadRad = 0.045f;
	private const float EnemySpreadRad = 0.09f;
	private const float EnemyPreferredRange = 22f;

	public override void _Ready()
	{
		_lblStatus = GetNode<Label>("HudPanel/VBox/LblStatus");
		_lblStats = GetNode<Label>("HudPanel/VBox/LblStats");
		_targetStatusHud = GetNode<TargetStatusHud>("TargetStatusHud");
		_playerStatusHud = GetNode<PlayerStatusHud>("PlayerStatusHud");
		_vehicleStatusHud = GetNode<VehicleStatusHud>("VehicleStatusHud");
		_btnTier1 = GetNode<Button>("HudPanel/VBox/HBoxTiers/BtnTier1");
		_btnTier2 = GetNode<Button>("HudPanel/VBox/HBoxTiers/BtnTier2");
		_btnTier3 = GetNode<Button>("HudPanel/VBox/HBoxTiers/BtnTier3");
		_btnStart = GetNode<Button>("HudPanel/VBox/HBoxActions/BtnStart");
		_btnFlee = GetNode<Button>("HudPanel/VBox/HBoxActions/BtnFlee");
		_btnBack = GetNode<Button>("HudPanel/VBox/HBoxActions/BtnBack");

		_postPanel = GetNode<PanelContainer>("PostPanel");
		_lblRewards = GetNode<Label>("PostPanel/PostVBox/LblRewards");
		_btnRepair = GetNode<Button>("PostPanel/PostVBox/HBoxPost/BtnRepair");
		_btnRepairDriverArmor = GetNode<Button>("PostPanel/PostVBox/HBoxPost/BtnRepairDriverArmor");
		_btnReturn = GetNode<Button>("PostPanel/PostVBox/HBoxPost/BtnReturn");
		_btnPatchArmor = GetNode<Button>("PostPanel/PostVBox/HBoxScrap/BtnPatchArmor");
		_btnPatchTire = GetNode<Button>("PostPanel/PostVBox/HBoxScrap/BtnPatchTire");

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

			// Note: Resource loads can return null if paths are wrong or imports are stale.
			// We defensively (re)load again later in EnsureWorld/SpawnActors as well.
			_worldScene = GD.Load<PackedScene>("res://Scenes/Arena/ArenaWorld.tscn");
			_vehScene = GD.Load<PackedScene>("res://Scenes/Arena/VehiclePawn.tscn");

		EnsureInputActions();
		EnsureSfx();
		EnsureHitMarker();
			// Don't spawn the world until the user presses Start; avoids any timing issues
			// with WorldRoot availability when opening this view.
		SelectTier(1);
		ResetUi();

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

	private void EnsureScenesLoaded()
	{
		if (_worldScene == null || !GodotObject.IsInstanceValid(_worldScene))
			_worldScene = GD.Load<PackedScene>("res://Scenes/Arena/ArenaWorld.tscn");
		if (_vehScene == null || !GodotObject.IsInstanceValid(_vehScene))
			_vehScene = GD.Load<PackedScene>("res://Scenes/Arena/VehiclePawn.tscn");
	}

	private void EnsureSfx()
	{
		if (_sfxPlayer != null && GodotObject.IsInstanceValid(_sfxPlayer)) return;
		_sfxPlayer = new AudioStreamPlayer { Name = "Sfx" };
		// A little quieter than default so it doesn't get annoying.
		_sfxPlayer.VolumeDb = -6.0f;
		AddChild(_sfxPlayer);

		// These assets are expected to exist in res://Assets/Audio. (You told me you won't delete Assets when updating.)
		_sfxHit = GD.Load<AudioStream>("res://Assets/Audio/ui_hit.wav");
		_sfxMiss = GD.Load<AudioStream>("res://Assets/Audio/ui_miss.wav");
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
		if (!_combatLive) return;
		if (_playerPawn == null || _enemyPawn == null || _playerVehicleRuntime == null) return;

		UpdateTargetSelection();
		UpdatePlayerInput();
		UpdateEnemyAi(dt);
		ResolveVehicleOverlap();

		// Update fast-changing HUD elements (speed/ammo) without repainting everything every frame.
		_hudDynamicRemaining -= dt;
		if (_hudDynamicRemaining <= 0f)
		{
			_hudDynamicRemaining = 0.10f;
			var defs = Defs();
			if (defs != null && _vehicleStatusHud.Visible)
			{
				var speed = _playerPawn.Velocity.Length();
				var maxSpeed = _playerPawn.MaxForwardSpeed;
				_vehicleStatusHud.UpdateDynamic(_playerVehicleRuntime, defs, speed, maxSpeed);
			}
		}

		if (Input.IsActionJustPressed("ws_fire"))
			TryFire(isPlayer: true);

		// Resolve checks.
		if (_playerHpRuntime <= 0) ResolveOutcome("lose");
		else if (_enemyHpRuntime <= 0) ResolveOutcome("win");
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

private void ResetUi()
	{
		_postPanel.Visible = false;
		_runtimeLog.Clear();
		RefreshStats();
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
		_arenaWorld = null;
		_playerPawn = null;
		_enemyPawn = null;
		_selectedTarget = null;
		_targetIndicator = null;
	}

	private void StartEncounter()
	{
		_lblStatus.Text = "Status: Starting encounter...";
		Console()?.Debug("Arena: Start pressed.");
		try
		{


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
		_arenaWorld.SetCameraTarget(_playerPawn);
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
				installed[primaryMount] = new InstalledWeaponState { WeaponId = "wpn_mg" };

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
		if (_playerPawn == null) return;
		var throttle = 0f;
		if (Input.IsActionPressed("ws_move_forward")) throttle += 1f;
		if (Input.IsActionPressed("ws_move_backward")) throttle -= 0.55f;
		var steer = 0f;
		if (Input.IsActionPressed("ws_steer_left")) steer -= 1f;
		if (Input.IsActionPressed("ws_steer_right")) steer += 1f;

		_playerPawn.ThrottleInput = Mathf.Clamp(throttle, -1f, 1f);
		_playerPawn.SteerInput = Mathf.Clamp(steer, -1f, 1f);

		var aimTarget = _selectedTarget != null ? _selectedTarget.GlobalPosition : (_playerPawn.GlobalPosition + (-_playerPawn.GlobalTransform.Basis.Z) * 12f);
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
		if (_playerPawn == null || _enemyPawn == null) return;

		if (_enemyUnstuckRemaining > 0f)
		{
			_enemyUnstuckRemaining -= dt;
			_enemyPawn.ThrottleInput = -0.45f;
			_enemyPawn.SteerInput = 0.85f;
			_enemyPawn.AimWorldPosition = _playerPawn.GlobalPosition;
			return;
		}

		var toPlayer = _playerPawn.GlobalPosition - _enemyPawn.GlobalPosition;
		toPlayer.Y = 0f;
		var dist = toPlayer.Length();
		var desiredYaw = Mathf.Atan2(toPlayer.X, toPlayer.Z) + Mathf.Pi; // facing -Z forward
		var headingDiff = Mathf.Wrap(desiredYaw - _enemyPawn.Rotation.Y, -Mathf.Pi, Mathf.Pi);
		_enemyPawn.SteerInput = Mathf.Clamp(headingDiff * 1.1f, -1f, 1f);

		if (dist > EnemyPreferredRange + 4f) _enemyPawn.ThrottleInput = 1f;
		else if (dist < EnemyPreferredRange - 5f) _enemyPawn.ThrottleInput = -0.35f;
		else _enemyPawn.ThrottleInput = 0.15f;

		_enemyPawn.AimWorldPosition = _playerPawn.GlobalPosition;

		if (_enemyPawn.GetSlideCollisionCount() > 0)
			_enemyUnstuckRemaining = 0.30f;

		// Fire in range.
		if (dist < 34f)
		{
			if (_enemyPawn.FireCooldownRemaining <= 0f && Random.Shared.NextDouble() < 0.28)
				TryFire(isPlayer: false);
		}
	}

	private void TryFire(bool isPlayer)
	{
		if (_playerPawn == null || _enemyPawn == null || _playerVehicleRuntime == null) return;
		var shooter = isPlayer ? _playerPawn : _enemyPawn;
		if (shooter.FireCooldownRemaining > 0f) return;

		if (isPlayer)
		{
			if (!TryConsumeAmmo(ref _playerVehicleRuntime, GameSession.PrimaryAmmoId, 1))
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
		// Fire from the weapon muzzle direction (turrets can yaw independently).
		var dir = shooter.GetMuzzleWorldForward();

		var spread = isPlayer ? PlayerSpreadRad : EnemySpreadRad;
		var jitter = (float)(Random.Shared.NextDouble() * 2.0 - 1.0) * spread;
		// Rotate around Y axis.
		dir = dir.Rotated(Vector3.Up, jitter).Normalized();

		var maxRange = 90f;
		var to = from + dir * maxRange;
		var hit = Raycast(from, to, shooter);
		var impactPos = hit.Hit ? hit.Position : to;
		if (_arenaWorld != null)
			ArenaVfx.SpawnShot(_arenaWorld, from, impactPos, fromPlayer: isPlayer, hit: hit.Hit);
		var damage = isPlayer ? Random.Shared.Next(12, 26) : Random.Shared.Next(6, 16);

		// Only apply damage if we actually hit the intended pawn (not a wall, floor, or our own hitboxes).
		var intended = isPlayer ? _enemyPawn : _playerPawn;
		var hitTarget = hit.Hit && intended != null && IsHitOnPawn(hit, intended);
		if (hitTarget)
			OnHit(isPlayer, damage, hit);

		// Player feedback (SFX + hit marker)
		if (isPlayer)
		{
			if (hitTarget)
			{
				PlaySfx(_sfxHit);
				_hitMarker?.Flash(HitMarkerColor(hit));
			}
			else
			{
				// Optional: a subtle miss click.
				PlaySfx(_sfxMiss);
			}
		}

		shooter.FireCooldownRemaining = shooter.FireCooldownSeconds;
		AddLog(isPlayer
			? (hitTarget ? "You fire (hit)." : "You fire (miss).")
			: (hitTarget ? "Enemy fires (hit)." : "Enemy fires (miss)."));

		// Keep ammo/speed/UI responsive even on misses.
		RefreshStats();
	}

	private static bool IsHitOnPawn(RayHit hit, VehiclePawn pawn)
	{
		if (hit.Collider is not Node n) return false;
		// Collider may be the pawn itself, or an Area3D hitbox under it.
		Node? cur = n;
		while (cur != null)
		{
			if (cur == pawn) return true;
			cur = cur.GetParent();
		}
		return false;
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

		// Vehicle status HUD (top-right, under player HUD).
		if (_playerVehicleRuntime != null && defs != null && defs.Vehicles.TryGetValue(_playerVehicleRuntime.DefinitionId, out var vdef))
		{
			_vehicleStatusHud.Visible = true;
			_vehicleStatusHud.SetVehicle(vdef, _playerVehicleRuntime, defs, speed, maxSpeed, _playerPawn?.BodyColor);
		}
		else
		{
			_vehicleStatusHud.Visible = false;
		}

		_lblStats.Text = $"Tier: {_selectedTier}";
		_btnFlee.Disabled = !_combatLive;
_btnStart.Disabled = _combatLive;
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
		ShowPostPanel();
		RefreshStats();
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

	private void ReturnToCity()
	{
		var parent = GetParent();
		if (parent == null) return;
		ForceFleeIfLive("return");
		ClearWorld();
		var scene = GD.Load<PackedScene>("res://Scenes/UI/CityShell.tscn");
		var ui = scene.Instantiate();
		parent.AddChild(ui);
		QueueFree();
	}

	// --- Input -------------------------------------------------------------
	private void EnsureInputActions()
	{
		EnsureActionIfMissing("ws_move_forward", KeyEvent(Key.W), KeyEvent(Key.Up));
		EnsureActionIfMissing("ws_move_backward", KeyEvent(Key.S), KeyEvent(Key.Down));
		EnsureActionIfMissing("ws_steer_left", KeyEvent(Key.A), KeyEvent(Key.Left));
		EnsureActionIfMissing("ws_steer_right", KeyEvent(Key.D), KeyEvent(Key.Right));
		EnsureActionIfMissing("ws_fire", KeyEvent(Key.Space));
		EnsureActionIfMissing("ws_target_next", KeyEvent(Key.Tab));
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
