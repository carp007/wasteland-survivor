// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/App/AppRoot.cs
// Purpose: Main scene controller. Shows boot splash, loads the CityShell UI, and handles global input (Escape pause menu, F11).
// -------------------------------------------------------------------------------------------------
using Godot;
using GameUiKit.UI;
using WastelandSurvivor.Game.Navigation;
using WastelandSurvivor.Game.UI;

namespace WastelandSurvivor.Game;

public partial class AppRoot : Node
{
	private CanvasLayer? _uiRoot;
	private CanvasLayer? _overlayRoot;
	private ScreenRouter? _router;
	private Node? _bootSplash;
	private PauseMenuOverlay? _pauseMenu;
	private ModalHost? _modalHost;
	private bool _servicesRegistered;
	private bool _bootSubscribed;

	public override void _EnterTree()
	{
		// IMPORTANT GODOT LIFECYCLE NOTE:
		// Parent _Ready() runs AFTER children _Ready(). If AppRoot registers services in _Ready(),
		// child UI nodes can try to resolve services (router/modal/etc.) before registration.
		//
		// We register services in _EnterTree() so they exist before any child _Ready() executes.
		// (Godot calls _EnterTree() top-down: parent first, then children.)
		RegisterServicesEarly();
	}

	public override void _Ready()
	{
		// Services should already be registered via _EnterTree().
		// If something caused _EnterTree to not run (should be rare), try again as a safety net.
		RegisterServicesEarly();

		var app = App.Instance;
		if (app == null)
		{
			GD.PrintErr("AppRoot: App autoload not found. Did you add Scripts/App/App.cs as Autoload named 'App'?");
			return;
		}

		if (!_bootSubscribed)
		{
			_bootSubscribed = true;
			GD.Print($"AppRoot: BootOk={app.BootOk} Status='{app.BootStatusText}'");
			app.BootCompleted += OnBootCompleted;
			// In case Boot already completed before AppRoot was ready.
			OnBootCompleted();
		}
	}

	private void RegisterServicesEarly()
	{
		if (_servicesRegistered)
			return;

		var app = App.Instance;
		if (app == null)
		{
			GD.PrintErr("AppRoot: App autoload not found. Did you add Scripts/App/App.cs as Autoload named 'App'?");
			return;
		}

		_uiRoot = GetNodeOrNull<CanvasLayer>("UIRoot");
		if (_uiRoot == null)
		{
			GD.PrintErr("AppRoot: Missing UIRoot CanvasLayer under AppRoot.");
			return;
		}

		_overlayRoot = GetNodeOrNull<CanvasLayer>("OverlayRoot");
		if (_overlayRoot == null)
		{
			GD.PrintErr("AppRoot: Missing OverlayRoot CanvasLayer under AppRoot.");
			return;
		}

		// Global pause menu overlay (Escape).
		_pauseMenu = GetNodeOrNull<PauseMenuOverlay>("OverlayRoot/PauseMenuOverlay");

		// Modal host overlay (dialogs/confirmations, etc.).
		_modalHost = _overlayRoot.GetNodeOrNull<ModalHost>("ModalHost");
		if (_modalHost == null)
		{
			_modalHost = new ModalHost { Name = "ModalHost" };
			_overlayRoot.AddChild(_modalHost);
		}

		// Centralized UI navigation.
		_router = new ScreenRouter(_uiRoot);
		app.Services.AddSingleton(_router);

		// Centralized modal service (framework plumbing).
		app.Services.AddSingleton<IModalService>(new ModalService(
			_modalHost!,
			new ModalDialogStyle(
				ThemeApplier: GameUiTheme.ApplyToTree,
				DialogStyler: d =>
				{
					if (d.TitleLabel != null)
					{
						GameUiTheme.StyleHeading(d.TitleLabel, GameUiTheme.TitleFontSize + 4, GameUiTheme.AccentGoldColor);
						d.TitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
					}
					if (d.BodyLabel != null)
					{
						d.BodyLabel.HorizontalAlignment = HorizontalAlignment.Center;
						d.BodyLabel.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
						d.BodyLabel.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize);
					}
				},
				DefaultMinSize: new Vector2(480, 240)
			)));

		// Game-level navigation facade (UI scripts should depend on this, not scene paths).
		app.Services.AddSingleton<IGameNavigator>(new GameNavigator());

		// Background music (city/combat tracks; no-ops when local music assets are absent).
		if (GetNodeOrNull<WastelandSurvivor.Game.Audio.MusicDirector>("MusicDirector") == null)
			AddChild(new WastelandSurvivor.Game.Audio.MusicDirector { Name = "MusicDirector" });

		// Background ambience (city hum / desert wind); follows MusicDirector.CombatActive so the
		// city/combat switch happens in the same place the music already switches.
		if (GetNodeOrNull<WastelandSurvivor.Game.Audio.AmbienceDirector>("AmbienceDirector") == null)
			AddChild(new WastelandSurvivor.Game.Audio.AmbienceDirector { Name = "AmbienceDirector" });

		// Global UI sounds (click/hover/confirm/error/purchase; no-ops when assets are absent).
		if (GetNodeOrNull<WastelandSurvivor.Game.Audio.UiSfx>("UiSfx") == null)
			AddChild(new WastelandSurvivor.Game.Audio.UiSfx { Name = "UiSfx" });

		HookUiSfxAutoWiring();

		_servicesRegistered = true;
	}

	// ---------------------------------------------------------------------------------------------
	// Global UI sfx auto-wiring: every BaseButton that enters the tree gets click/hover sounds.
	// A metadata flag on each button prevents double-subscription if a node re-enters the tree
	// (scene swaps, reparenting); freed buttons drop their handlers with the instance.
	// ---------------------------------------------------------------------------------------------
	private const string UiSfxWiredMeta = "ui_sfx_wired";
	private bool _uiSfxHooked;

	private void HookUiSfxAutoWiring()
	{
		if (_uiSfxHooked)
			return;

		var tree = GetTree();
		if (tree == null)
			return;

		_uiSfxHooked = true;
		tree.NodeAdded += OnNodeAddedForUiSfx;

		// Wire anything that entered the tree before the hook (safety net when registration ran late).
		WireUiSfxRecursive(tree.Root);
	}

	private static void WireUiSfxRecursive(Node node)
	{
		OnNodeAddedForUiSfx(node);
		foreach (var child in node.GetChildren())
			WireUiSfxRecursive(child);
	}

	private static void OnNodeAddedForUiSfx(Node node)
	{
		if (node is not BaseButton button)
			return;
		if (button.HasMeta(UiSfxWiredMeta))
			return;

		button.SetMeta(UiSfxWiredMeta, true);
		button.Pressed += () => WastelandSurvivor.Game.Audio.UiSfx.Play("click");
		button.MouseEntered += () => WastelandSurvivor.Game.Audio.UiSfx.Play("hover");
	}

	private void OnBootCompleted()
	{
		var app = App.Instance;
		if (app == null) return;

		GD.Print($"AppRoot: BootCompleted => BootOk={app.BootOk} Status='{app.BootStatusText}'");
		if (!app.BootOk) return;

		// Show splash sequence first, then transition to the main UI.
		ShowBootSplashThenCityShell();
	}

	private void ShowBootSplashThenCityShell()
	{
		if (_router == null) return;
		if (_router.Current != null) return; // already in main UI
		if (_bootSplash != null) return; // already showing

		// Screenshot mode: skip the splash and attach the capture driver (the "splash" target keeps
		// the real splash sequence, and the "title" target jumps straight to the title screen, so
		// both can be captured).
		if (ScreenshotHarness.Active)
		{
			if (GetNodeOrNull<ScreenshotHarness>("ScreenshotHarness") == null)
				AddChild(new ScreenshotHarness { Name = "ScreenshotHarness" });
			if (string.Equals(ScreenshotHarness.Target, "title", System.StringComparison.OrdinalIgnoreCase))
			{
				ShowTitleScreen();
				return;
			}
			if (!string.Equals(ScreenshotHarness.Target, "splash", System.StringComparison.OrdinalIgnoreCase))
			{
				ShowCityShell();
				return;
			}
		}

		var splashPath = GameScenes.BootSplashView;
		if (!ResourceLoader.Exists(splashPath))
		{
			// If scene missing, just proceed.
			ShowCityShell();
			return;
		}

		if (!_router.TryReplace(splashPath))
		{
			ShowCityShell();
			return;
		}

		_bootSplash = _router.Current;

		// BootSplashView will raise a Completed event when done or skipped.
		if (_bootSplash is BootSplashView splash)
		{
			splash.Completed += OnBootSplashCompleted;
		}
		else
		{
			// Unexpected type; proceed without splash.
			_bootSplash?.QueueFree();
			_bootSplash = null;
			ShowCityShell();
		}
	}

	private void OnBootSplashCompleted()
	{
		if (_bootSplash is BootSplashView splash)
			splash.Completed -= OnBootSplashCompleted;

		_bootSplash = null;

		// Harness splash captures end after the sequence; keep the legacy city hand-off there so
		// existing shot targets stay deterministic. Real players land on the title screen.
		if (ScreenshotHarness.Active)
			ShowCityShell();
		else
			ShowTitleScreen();
	}

	/// <summary>
	/// Title screen / main menu. Navigates itself onward (CONTINUE / NEW GAME both route to the
	/// city shell through IGameNavigator), so AppRoot only needs to put it on screen.
	/// </summary>
	private void ShowTitleScreen()
	{
		if (_router == null) return;
		if (_router.Current is TitleScreenView) return;

		if (!ResourceLoader.Exists(GameScenes.TitleScreenView) || !_router.TryReplace(GameScenes.TitleScreenView))
			ShowCityShell();
	}

	private void ShowCityShell()
	{
		if (_router == null) return;
		// Avoid re-instantiating if we are already on CityShell.
		if (_router.Current is CityShell) return;

		_router.TryReplace(GameScenes.CityShell);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Escape toggles a simple pause menu.
		if (@event is InputEventKey esc && esc.Pressed && !esc.Echo && esc.Keycode == Key.Escape)
		{
			if (_pauseMenu != null)
			{
				_pauseMenu.Toggle();
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F11)
		{
			App.Instance?.ToggleFullscreen();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _ExitTree()
	{
		var app = App.Instance;
		if (app != null)
			app.BootCompleted -= OnBootCompleted;

		if (_bootSplash is BootSplashView splash)
			splash.Completed -= OnBootSplashCompleted;

		if (_uiSfxHooked)
		{
			var tree = GetTree();
			if (tree != null)
				tree.NodeAdded -= OnNodeAddedForUiSfx;
			_uiSfxHooked = false;
		}
	}
}
