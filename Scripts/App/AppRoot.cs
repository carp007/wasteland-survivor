using Godot;

namespace WastelandSurvivor.Game;

public partial class AppRoot : Node
{
	private CanvasLayer? _uiRoot;
	private Node? _currentUi;

	public override void _Ready()
	{
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

		GD.Print($"AppRoot: BootOk={app.BootOk} Status='{app.BootStatusText}'");
		app.BootCompleted += OnBootCompleted;
		// In case Boot already completed before AppRoot was ready.
		OnBootCompleted();
	}

	private void OnBootCompleted()
	{
		var app = App.Instance;
		if (app == null) return;

		GD.Print($"AppRoot: BootCompleted => BootOk={app.BootOk} Status='{app.BootStatusText}'");
		if (!app.BootOk) return;

		ShowCityShell();
	}

	private void ShowCityShell()
	{
		if (_uiRoot == null) return;
		if (_currentUi != null) return; // already shown

		var scene = GD.Load<PackedScene>("res://Scenes/UI/CityShell.tscn");
		_currentUi = scene.Instantiate();
		_uiRoot.AddChild(_currentUi);
	}


	public override void _UnhandledInput(InputEvent @event)
	{
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
	}
}
