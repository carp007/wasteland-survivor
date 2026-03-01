// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/CityShell.cs
// Purpose: City “main menu” view. Lets the player pick a city, open Garage/Workshop/Arena, save, create a starter vehicle, or exit.
// -------------------------------------------------------------------------------------------------
using Godot;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Framework.SceneBinding;
using WastelandSurvivor.Game.Navigation;

namespace WastelandSurvivor.Game.UI;

public partial class CityShell : Control
{
	private ColorRect? _bg;
	private Label _lblCity = null!;
	private Button _btnDetroit = null!;
	private Button _btnCleveland = null!;
	private Button _btnGarage = null!;
	private Button _btnWorkshop = null!;
	private Button _btnArena = null!;
	private Button _btnCreateStarter = null!;
	private Button _btnSave = null!;
	private Button _btnExit = null!;

	private void EnsureBound()
	{
		var b = new SceneBinder(this, nameof(CityShell));
		_bg = b.Opt<ColorRect>("Bg");
		_lblCity = b.Req<Label>("Panel/VBox/LblCity");
		_btnDetroit = b.Req<Button>("Panel/VBox/HBoxCities/BtnDetroit");
		_btnCleveland = b.Req<Button>("Panel/VBox/HBoxCities/BtnCleveland");
		_btnGarage = b.Req<Button>("Panel/VBox/BtnGarage");
		_btnWorkshop = b.Req<Button>("Panel/VBox/BtnWorkshop");
		_btnArena = b.Req<Button>("Panel/VBox/BtnArena");
		_btnCreateStarter = b.Req<Button>("Panel/VBox/BtnCreateStarter");
		_btnSave = b.Req<Button>("Panel/VBox/BtnSave");
		_btnExit = b.Req<Button>("Panel/VBox/BtnExit");
	}

	public override void _Ready()
	{
		GameUiTheme.ApplyToTree(this);
		EnsureBound();

		if (_bg != null)
			_bg.Color = GameUiTheme.BackgroundColor;

		_btnDetroit.Pressed += () => SetCity("detroit");
		_btnCleveland.Pressed += () => SetCity("cleveland");
		_btnSave.Pressed += SaveNow;
		_btnCreateStarter.Pressed += CreateStarter;
		_btnGarage.Pressed += OpenGarage;
		_btnWorkshop.Pressed += OpenWorkshop;
		_btnArena.Pressed += OpenArena;
		_btnExit.Pressed += ExitGame;

		Refresh();
	}

	private void Refresh()
	{
		var app = App.Instance;
		if (app == null)
		{
			_lblCity.Text = "Current: (App missing)";
			return;
		}

		var session = app.Services.Get<GameSession>();
		var city = session.Save.Player.CurrentCityId;
		var active = session.GetActiveVehicle();
		var activeText = active == null ? "none" : active.DefinitionId;

		_lblCity.Text = $"Current: {city}   |   Active vehicle: {activeText}";
	}

	private void SetCity(string cityId)
	{
		var app = App.Instance;
		if (app == null) return;

		var session = app.Services.Get<GameSession>();
		session.SetCurrentCity(cityId);
		Refresh();
	}

	private void SaveNow()
	{
		var app = App.Instance;
		if (app == null) return;

		var session = app.Services.Get<GameSession>();
		session.Persist();
		GD.Print("[CityShell] Saved.");
	}

	private void CreateStarter()
	{
		var app = App.Instance;
		if (app == null) return;

		var session = app.Services.Get<GameSession>();
		var defs = app.Services.Get<DefDatabase>();

		// Only create if the player has no owned vehicles yet.
		if (session.Save.Player.OwnedVehicleIds.Count == 0)
			session.CreateStarterVehicle(defs);
		else
			GD.Print("[CityShell] Starter already exists; skipping.");

		Refresh();
	}



	private void OpenWorkshop()
	{
		var app = App.Instance;
		if (app == null) return;
		if (!app.Services.TryGet<IGameNavigator>(out var nav) || nav == null)
		{
			GD.PrintErr("[CityShell] IGameNavigator not registered (cannot navigate to Workshop).");
			return;
		}

		nav.ToWorkshop(this);
	}


	private void OpenArena()
	{
		var app = App.Instance;
		if (app == null) return;
		if (!app.Services.TryGet<IGameNavigator>(out var nav) || nav == null)
		{
			GD.PrintErr("[CityShell] IGameNavigator not registered (cannot navigate to Arena).");
			return;
		}

		nav.ToArena(this);
	}


	private void OpenGarage()
	{
		var app = App.Instance;
		if (app == null) return;
		if (!app.Services.TryGet<IGameNavigator>(out var nav) || nav == null)
		{
			GD.PrintErr("[CityShell] IGameNavigator not registered (cannot navigate to Garage).");
			return;
		}

		nav.ToGarage(this);
	}

	private void ExitGame()
	{
		// Allow quitting directly from the City shell (main menu).
		GetTree().Quit();
	}
}
