// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/DriverPawn.cs
// Purpose: On-foot driver pawn (CharacterBody3D). Handles enter/exit vehicle interactions, movement, avatar animation switching, and taking damage.
// -------------------------------------------------------------------------------------------------
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Minimal on-foot pawn for the arena (Phase 1 placeholder visuals).
///
/// - Top-down movement on the XZ plane.
/// - Uses CharacterBody3D for simple collision against arena geometry/vehicles.
/// - Input is provided by the arena controller (ArenaRealtimeView) via MoveInput/Sprint.
/// </summary>
public partial class DriverPawn : CharacterBody3D
{
	[Export] public float MoveSpeed { get; set; } = 7.0f;
	[Export] public float SprintMultiplier { get; set; } = 1.6f;

	// Optional avatar/animation configuration (typically sourced from driver_pawn.json).
	public DriverAvatarConfig AvatarConfig { get; set; } = DriverAvatarConfig.Default();

	// Movement smoothing (optional; defaults are tuned for a slightly more "human" feel).
	[Export] public float Acceleration { get; set; } = 38.0f;
	[Export] public float Deceleration { get; set; } = 55.0f;

	/// <summary>
	/// World-space movement direction (XZ). Typically normalized by caller.
	/// </summary>
	public Vector3 MoveInput { get; set; } = Vector3.Zero;
	public bool Sprint { get; set; } = false;

	public override void _Ready()
	{
		CollisionLayer = 1u;
		CollisionMask = 1u;
		SafeMargin = 0.05f;
		AddToGroup("driver_pawn");
		SetPhysicsProcess(true);
		EnsureVisualAndCollision();
		TryLoadAvatarFromConfig();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead)
		{
			// Keep the body in place; animation (if any) is driven by the AnimationPlayer.
			Velocity = Vector3.Zero;
			MoveInput = Vector3.Zero;
			Sprint = false;
			return;
		}

		var dt = (float)delta;
		var dir = MoveInput;
		dir.Y = 0f;
		if (dir.Length() > 1f)
			dir = dir.Normalized();

		var spd = MoveSpeed * (Sprint ? SprintMultiplier : 1f);
		var target = new Vector3(dir.X * spd, 0f, dir.Z * spd);
		var accel = (target.Length() > Velocity.Length()) ? Acceleration : Deceleration;
		Velocity = Velocity.MoveToward(target, accel * dt);
		MoveAndSlide();

		// Face movement direction (purely cosmetic).
		if (dir.Length() > 0.15f)
		{
			var yaw = Mathf.Atan2(dir.X, dir.Z) + Mathf.Pi; // face -Z convention
			Rotation = new Vector3(0f, yaw, 0f);
		}

		UpdateAvatarAnimation(dt);
	}

	public void Stop()
	{
		MoveInput = Vector3.Zero;
		Sprint = false;
		Velocity = Vector3.Zero;
		UpdateAvatarAnimation(0f);
	}

	private enum AvatarAnimState
	{
		None,
		Idle,
		Walk,
		Run,
		Dead
	}

	private bool _avatarLoadAttempted;
	private Node3D? _avatarRoot;
	private Node3D? _avatarInstance;
	private AnimationPlayer? _avatarAnimPlayer;
	private AvatarAnimState _avatarState = AvatarAnimState.None;
	private string? _resolvedIdle;
	private string? _resolvedWalk;
	private string? _resolvedRun;
	private string? _resolvedDeath;
	private MeshInstance3D? _fallbackVisual;
	private bool _playerDisabled;
	private bool _isDead;

	private void EnsureVisualAndCollision()
	{
		// Keep the scene minimal/robust. If nodes are missing (e.g., during refactors), create them.
		var col = GetNodeOrNull<CollisionShape3D>("Collision");
		if (col == null)
		{
			col = new CollisionShape3D { Name = "Collision" };
			AddChild(col);
		}
		if (col.Shape == null)
		{
			col.Shape = new CapsuleShape3D { Height = 1.55f, Radius = 0.35f };
		}

		_avatarRoot = GetNodeOrNull<Node3D>("AvatarRoot");
		if (_avatarRoot == null)
		{
			_avatarRoot = new Node3D { Name = "AvatarRoot" };
			AddChild(_avatarRoot);
		}

		var vis = GetNodeOrNull<MeshInstance3D>("Visual");
		if (vis == null)
		{
			vis = new MeshInstance3D { Name = "Visual" };
			AddChild(vis);
		}
		if (vis.Mesh == null)
			vis.Mesh = new CapsuleMesh { Height = 1.55f, Radius = 0.35f };
		_fallbackVisual = vis;

		var mat = vis.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
		if (mat == null)
		{
			mat = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.92f, 0.92f, 0.92f),
				Roughness = 1.0f,
			};
			vis.SetSurfaceOverrideMaterial(0, mat);
		}
	}

	private void TryLoadAvatarFromConfig()
	{
		if (_avatarLoadAttempted) return;
		_avatarLoadAttempted = true;

		if (_avatarRoot == null) return;
		var cfg = AvatarConfig ?? DriverAvatarConfig.Default();
		if (string.IsNullOrWhiteSpace(cfg.ModelScenePath)) return;

		try
		{
			if (!ResourceLoader.Exists(cfg.ModelScenePath))
			{
				GD.PushWarning($"[DriverPawn] Mixamo avatar not found at {cfg.ModelScenePath}. Using capsule placeholder. " +
				               "Place Driver.glb under Assets/Characters/Driver/Mixamo/ and re-open the project so Godot imports it.");
				return;
			}

			var ps = GD.Load<PackedScene>(cfg.ModelScenePath);
			if (ps == null)
			{
				GD.PrintErr($"[DriverPawn] Failed to load avatar scene: {cfg.ModelScenePath}");
				return;
			}

			_avatarInstance = ps.Instantiate() as Node3D;
			if (_avatarInstance == null)
			{
				GD.PrintErr($"[DriverPawn] Avatar scene is not a Node3D: {cfg.ModelScenePath}");
				return;
			}

			_avatarInstance.Name = "Avatar";
			_avatarRoot.AddChild(_avatarInstance);
			_avatarRoot.Scale = Vector3.One * Mathf.Max(0.001f, cfg.ModelScale);
			_avatarRoot.Position = cfg.ModelLocalOffsetVec3();
			_avatarRoot.Rotation = new Vector3(0f, Mathf.DegToRad(cfg.ModelYawOffsetDegrees), 0f);

			_avatarAnimPlayer = FindFirstChildOfType<AnimationPlayer>(_avatarInstance);
			if (_avatarAnimPlayer == null)
			{
				GD.Print($"[DriverPawn] Avatar loaded but no AnimationPlayer found under {cfg.ModelScenePath}. Movement will be unanimated.");
			}
			else
			{
				ResolveAnimationNames(cfg);
				EnsureLocomotionLoops();
				LogAvatarAnimationNamesOnce();
			}

			// Hide fallback capsule if we have a real avatar.
			if (_fallbackVisual != null) _fallbackVisual.Visible = false;
		}
		catch (System.Exception ex)
		{
			GD.PrintErr($"[DriverPawn] Failed to load avatar: {ex.Message}");
		}
	}

	private void ResolveAnimationNames(DriverAvatarConfig cfg)
	{
		if (_avatarAnimPlayer == null) return;
		_resolvedIdle = ResolveAnimName(cfg.IdleAnim);
		_resolvedWalk = ResolveAnimName(cfg.WalkAnim);
		_resolvedRun = ResolveAnimName(cfg.RunAnim);
		_resolvedDeath = ResolveAnimName(cfg.DeathAnim);

		// If user provided bad names, try common Mixamo defaults as fallback.
		_resolvedIdle ??= ResolveAnimName("Idle");
		_resolvedWalk ??= ResolveAnimName("Walking") ?? ResolveAnimName("Walk");
		_resolvedRun ??= ResolveAnimName("Running") ?? ResolveAnimName("Run");
		_resolvedDeath ??= ResolveAnimName("Death") ?? ResolveAnimName("Dying") ?? ResolveAnimName("Die") ?? ResolveAnimName("Dead");
	}

	private bool _loggedAnimNames;
	private void LogAvatarAnimationNamesOnce()
	{
		if (_loggedAnimNames) return;
		_loggedAnimNames = true;
		if (_avatarAnimPlayer == null) return;

		try
		{
			var list = _avatarAnimPlayer.GetAnimationList();
			var sb = new System.Text.StringBuilder();
			for (var i = 0; i < list.Length; i++)
			{
				if (i > 0) sb.Append(", ");
				sb.Append(list[i].ToString());
			}

			GD.Print($"[DriverPawn] Avatar animations: {sb}");
			GD.Print($"[DriverPawn] Resolved idle={_resolvedIdle ?? "<none>"}, walk={_resolvedWalk ?? "<none>"}, run={_resolvedRun ?? "<none>"}, death={_resolvedDeath ?? "<none>"}");
		}
		catch
		{
			// ignore
		}
	}

	private string? ResolveAnimName(string desired)
	{
		if (_avatarAnimPlayer == null) return null;
		if (string.IsNullOrWhiteSpace(desired)) return null;

		var list = _avatarAnimPlayer.GetAnimationList();
		// Exact match first
		foreach (var sn in list)
		{
			var name = sn.ToString();
			if (string.Equals(name, desired, System.StringComparison.OrdinalIgnoreCase))
				return name;
		}
		// Contains match next (handles names like "mixamo_com/Walking" or similar)
		foreach (var sn in list)
		{
			var name = sn.ToString();
			if (name.IndexOf(desired, System.StringComparison.OrdinalIgnoreCase) >= 0)
				return name;
		}
		return null;
	}

	private void UpdateAvatarAnimation(float dt)
	{
		// Allow a late load attempt (e.g., user adds assets while the game is running in-editor).
		if (!_avatarLoadAttempted)
			TryLoadAvatarFromConfig();

		if (_avatarAnimPlayer == null) return;
		if (_playerDisabled || _isDead) return;

		var speed = new Vector2(Velocity.X, Velocity.Z).Length();
		var idleMax = Mathf.Max(0.05f, MoveSpeed * 0.08f);
		var runMin = Mathf.Max(0.10f, MoveSpeed * 0.70f);

		AvatarAnimState desired;
		if (speed <= idleMax)
			desired = AvatarAnimState.Idle;
		else if (speed >= runMin || Sprint)
			desired = AvatarAnimState.Run;
		else
			desired = AvatarAnimState.Walk;

		if (desired == _avatarState) return;
		_avatarState = desired;

		var anim = desired switch
		{
			AvatarAnimState.Idle => _resolvedIdle,
			AvatarAnimState.Walk => _resolvedWalk,
			AvatarAnimState.Run => _resolvedRun,
			_ => null
		};
		if (string.IsNullOrWhiteSpace(anim)) return;

		_avatarAnimPlayer.Play(anim, customBlend: 0.12);
	}

	private void EnsureLocomotionLoops()
	{
		EnsureAnimLoops(_resolvedIdle);
		EnsureAnimLoops(_resolvedWalk);
		EnsureAnimLoops(_resolvedRun);
	}

	private void EnsureAnimLoops(string? animName)
	{
		if (_avatarAnimPlayer == null) return;
		if (string.IsNullOrWhiteSpace(animName)) return;
		try
		{
			var anim = _avatarAnimPlayer.GetAnimation(animName);
			if (anim == null) return;
			if (anim.LoopMode == Animation.LoopModeEnum.None)
				anim.LoopMode = Animation.LoopModeEnum.Linear;
		}
		catch
		{
			// ignore
		}
	}

	/// <summary>
	/// Trigger the on-foot death behavior. Intended to be called by the arena controller
	/// when the driver is killed while outside the vehicle.
	/// </summary>
	public void TriggerDeath()
	{
		if (_isDead) return;
		_isDead = true;
		_playerDisabled = true;
		MoveInput = Vector3.Zero;
		Sprint = false;
		Velocity = Vector3.Zero;
		_avatarState = AvatarAnimState.Dead;

		// Prefer a real death animation if available.
		if (_avatarAnimPlayer != null)
		{
			var anim = _resolvedDeath;
			if (string.IsNullOrWhiteSpace(anim))
				anim = ResolveAnimName(AvatarConfig?.DeathAnim ?? "");
			if (!string.IsNullOrWhiteSpace(anim))
			{
				try
				{
					var a = _avatarAnimPlayer.GetAnimation(anim);
					if (a != null)
						a.LoopMode = Animation.LoopModeEnum.None;
				}
				catch { /* ignore */ }

				_avatarAnimPlayer.Play(anim, customBlend: 0.08);
				return;
			}
		}

		// Fallback: lay the capsule down so death is still readable even without an avatar anim.
		if (_fallbackVisual != null)
		{
			_fallbackVisual.Visible = true;
			_fallbackVisual.Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f);
			_fallbackVisual.Position = new Vector3(0f, 0.25f, 0f);
		}

		GD.Print("[DriverPawn] TriggerDeath: no death animation found; using fallback corpse pose.");
	}

	/// <summary>
	/// Called by arena controller when the driver is not currently player-controlled.
	/// </summary>
	public void SetPlayerControlled(bool enabled)
	{
		_playerDisabled = !enabled;
		if (!enabled) Stop();
	}

	private static T? FindFirstChildOfType<T>(Node root) where T : class
	{
		if (root is T t) return t;
		foreach (var c in root.GetChildren())
		{
			if (c is Node n)
			{
				var found = FindFirstChildOfType<T>(n);
				if (found != null) return found;
			}
		}
		return null;
	}
}
