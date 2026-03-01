// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/App/AppRoot.cs
// Purpose: Main scene controller. Shows boot splash, loads the CityShell UI, and handles global input (Escape pause menu, F11).
// -------------------------------------------------------------------------------------------------
using Godot;
using WastelandSurvivor.Framework.UI;
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
					// Keep the prior dialog look: centered title with gold accent.
					if (d.TitleLabel != null)
					{
						d.TitleLabel.AddThemeFontSizeOverride("font_size", GameUiTheme.TitleFontSize);
						d.TitleLabel.AddThemeColorOverride("font_color", GameUiTheme.AccentGoldColor);
					}
				},
				DefaultMinSize: new Vector2(440, 220)
			)));

		// Game-level navigation facade (UI scripts should depend on this, not scene paths).
		app.Services.AddSingleton<IGameNavigator>(new GameNavigator());

		_servicesRegistered = true;
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
	}
}
