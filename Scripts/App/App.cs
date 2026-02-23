using System;
using Godot;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game;

public partial class App : Node
{
	public static App? Instance
	{
		get
		{
			if (Engine.GetMainLoop() is not SceneTree tree) return null;
			// Autoload nodes are under /root
			return tree.Root.GetNodeOrNull<App>("App");
		}
	}

	public GameServices Services { get; private set; } = new();
	public bool BootOk { get; private set; }
	public string BootStatusText { get; private set; } = "Boot: pending";

	public DefLoader.LoadResult? LastLoadResult { get; private set; }

	private bool _windowDefaultsCaptured;
	private Window.ContentScaleModeEnum _defaultContentScaleMode;
	private Window.ContentScaleAspectEnum _defaultContentScaleAspect;
	private Vector2I _defaultContentScaleSize;
	private Vector2I _defaultWindowSize;
	private DisplayServer.WindowMode _defaultWindowMode;

	public event Action? BootCompleted;

	public override void _Ready()
	{
		ApplyStartupWindowMode();
		Boot();
	}

	private void CaptureWindowDefaults()
	{
		if (_windowDefaultsCaptured) return;
		var root = GetTree().Root;
		_defaultContentScaleMode = root.ContentScaleMode;
		_defaultContentScaleAspect = root.ContentScaleAspect;
		_defaultContentScaleSize = root.ContentScaleSize;
		_defaultWindowSize = DisplayServer.WindowGetSize();
		_defaultWindowMode = DisplayServer.WindowGetMode();
		_windowDefaultsCaptured = true;
	}

	private void RestoreDefaultContentScale()
	{
		if (!_windowDefaultsCaptured) return;
		var root = GetTree().Root;
		root.ContentScaleMode = _defaultContentScaleMode;
		root.ContentScaleAspect = _defaultContentScaleAspect;
		root.ContentScaleSize = _defaultContentScaleSize;
	}

	private void ApplyNativeFullscreenContentScaleIfAppropriate()
	{
		var root = GetTree().Root;
		var mode = DisplayServer.WindowGetMode();
		var screen = DisplayServer.WindowGetCurrentScreen();
		var screenSize = DisplayServer.ScreenGetSize(screen);
		var windowSize = DisplayServer.WindowGetSize();

		var isFullscreenMode = mode == DisplayServer.WindowMode.Fullscreen || mode == DisplayServer.WindowMode.ExclusiveFullscreen;
		var isWindowAtScreenSize = Math.Abs(windowSize.X - screenSize.X) <= 4 && Math.Abs(windowSize.Y - screenSize.Y) <= 4;
		var shouldApplyNativeScale = isFullscreenMode || isWindowAtScreenSize;

		if (shouldApplyNativeScale)
		{
			// Force the game viewport to render at actual fullscreen resolution.
			root.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
			root.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
			root.ContentScaleSize = screenSize;
		}
		else
		{
			RestoreDefaultContentScale();
		}
	}

	private void ApplyStartupWindowMode()
	{
		// Prefer fullscreen at startup for a more predictable HUD layout.
		// NOTE: switching the window mode can occur before the root viewport completes resizing.
		// Defer a frame and then force the root content scale to match the screen.
		CallDeferred(nameof(ApplyStartupWindowModeDeferred));
	}

	private async void ApplyStartupWindowModeDeferred()
	{
		try
		{
			CaptureWindowDefaults();

			// Give Godot one frame to finish initializing the window.
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);

			// Wait a frame so the display server applies the mode and reports the correct size.
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

			ApplyNativeFullscreenContentScaleIfAppropriate();
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[WastelandSurvivor] Failed to apply fullscreen+scale: {ex.Message}");
		}
	}

	public void ToggleFullscreen()
	{
		try
		{
			CaptureWindowDefaults();
			var mode = DisplayServer.WindowGetMode();
			var goingFullscreen = mode != DisplayServer.WindowMode.Fullscreen && mode != DisplayServer.WindowMode.ExclusiveFullscreen;

			if (goingFullscreen)
			{
				DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
			}
			else
			{
				DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
				if (_defaultWindowSize.X > 0 && _defaultWindowSize.Y > 0)
					DisplayServer.WindowSetSize(_defaultWindowSize);
			}

			// Wait for the display server to apply the mode before adjusting content scale.
			CallDeferred(nameof(ApplyContentScaleAfterModeToggleDeferred));

			if (Services.TryGet<GameConsole>(out var console) && console != null)
			{
				var txt = goingFullscreen ? "Fullscreen" : "Windowed";
				console.Status($"Display: {txt}");
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[WastelandSurvivor] Failed to toggle fullscreen: {ex.Message}");
		}
	}

	private async void ApplyContentScaleAfterModeToggleDeferred()
	{
		try
		{
			// Give the window mode change time to apply.
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			ApplyNativeFullscreenContentScaleIfAppropriate();
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[WastelandSurvivor] Failed to apply content scale after toggle: {ex.Message}");
		}
	}

	private void Boot()
	{
		try
		{
			Services = new GameServices();

			// Global in-memory console (UI overlay consumes this).
			var console = new GameConsole();
			Services.AddSingleton(console);
			console.Debug($"Boot: starting (v{GetVersionString()})");

			var loader = new DefLoader();
			var result = loader.LoadAll("res://Data/Defs");
			LastLoadResult = result;

			Services.AddSingleton(result.Database);
			console.Debug(
				$"Defs loaded: Vehicles={result.VehicleCount} Weapons={result.WeaponCount} Ammo={result.AmmoCount} " +
				$"Engines={result.EngineCount} Computers={result.ComputerCount} Armors={result.ArmorCount} " +
				$"({result.ErrorCount} errors, {result.WarningCount} warnings)");

			// Load (or create) save game
			var saveStore = new SaveGameStore();
			var save = saveStore.LoadOrCreateDefault();
			var session = new GameSession(saveStore, save);
			Services.AddSingleton(session);
			Services.AddSingleton(saveStore);

			BootOk = result.ErrorCount == 0;
			BootStatusText =
				$"Loaded Vehicles={result.VehicleCount} Weapons={result.WeaponCount} Ammo={result.AmmoCount} " +
				$"Engines={result.EngineCount} Computers={result.ComputerCount} Armors={result.ArmorCount} " +
				$"({result.ErrorCount} errors, {result.WarningCount} warnings)";

			GD.Print($"[WastelandSurvivor] {BootStatusText}");

			foreach (var msg in result.Messages)
			{
				if (msg.Severity == "ERROR")
				{
					GD.PrintErr($"[Defs] {msg.Severity}: {msg.Message}");
					console.Error($"[Defs] {msg.Severity}: {msg.Message}");
				}
				else
				{
					GD.Print($"[Defs] {msg.Severity}: {msg.Message}");
					console.Debug($"[Defs] {msg.Severity}: {msg.Message}");
				}
			}
		}
		catch (Exception ex)
		{
			BootOk = false;
			BootStatusText = $"Boot FAILED: {ex.Message}";
			GD.PrintErr($"[WastelandSurvivor] {BootStatusText}");
				if (Services.TryGet<GameConsole>(out var console) && console != null)
					console.Error(BootStatusText);
		}
		finally
		{
			BootCompleted?.Invoke();
		}
	}

	public string GetVersionString()
	{
		var v = GetType().Assembly.GetName().Version;
		return v?.ToString() ?? "0.0.0";
	}
}
