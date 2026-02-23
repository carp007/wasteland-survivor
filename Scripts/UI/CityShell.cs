using Godot;
using WastelandSurvivor.Core.IO;

namespace WastelandSurvivor.Game.UI;

public partial class CityShell : Control
{
    private Label? _lblCity;
    private Button? _btnDetroit;
    private Button? _btnCleveland;
    private Button? _btnGarage;
    private Button? _btnWorkshop;
    private Button? _btnArena;
    private Button? _btnCreateStarter;
    private Button? _btnSave;
    private Button? _btnExit;

    public override void _Ready()
    {
        _lblCity = GetNodeOrNull<Label>("Panel/VBox/LblCity");
        _btnDetroit = GetNodeOrNull<Button>("Panel/VBox/HBoxCities/BtnDetroit");
        _btnCleveland = GetNodeOrNull<Button>("Panel/VBox/HBoxCities/BtnCleveland");
        _btnGarage = GetNodeOrNull<Button>("Panel/VBox/BtnGarage");
        _btnWorkshop = GetNodeOrNull<Button>("Panel/VBox/BtnWorkshop");
        _btnArena = GetNodeOrNull<Button>("Panel/VBox/BtnArena");
        _btnCreateStarter = GetNodeOrNull<Button>("Panel/VBox/BtnCreateStarter");
        _btnSave = GetNodeOrNull<Button>("Panel/VBox/BtnSave");
        _btnExit = GetNodeOrNull<Button>("Panel/VBox/BtnExit");

        if (_btnDetroit != null) _btnDetroit.Pressed += () => SetCity("detroit");
        if (_btnCleveland != null) _btnCleveland.Pressed += () => SetCity("cleveland");
        if (_btnSave != null) _btnSave.Pressed += SaveNow;
        if (_btnCreateStarter != null) _btnCreateStarter.Pressed += CreateStarter;
        if (_btnGarage != null) _btnGarage.Pressed += OpenGarage;
        if (_btnWorkshop != null) _btnWorkshop.Pressed += OpenWorkshop;
        if (_btnArena != null) _btnArena.Pressed += OpenArena;
        if (_btnExit != null) _btnExit.Pressed += ExitGame;

        Refresh();
    }

    private void Refresh()
    {
        var app = App.Instance;
        if (app == null)
        {
            if (_lblCity != null) _lblCity.Text = "Current: (App missing)";
            return;
        }

        var session = app.Services.Get<GameSession>();
        var city = session.Save.Player.CurrentCityId;
        var active = session.GetActiveVehicle();
        var activeText = active == null ? "none" : active.DefinitionId;

        if (_lblCity != null)
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
        var parent = GetParent();
        if (parent == null) return;

        var scene = GD.Load<PackedScene>("res://Scenes/UI/WorkshopView.tscn");
        var ui = scene.Instantiate();
        parent.AddChild(ui);
        QueueFree();
    }

    private void OpenArena()
    {
        var parent = GetParent();
        if (parent == null) return;

        // Prefer the real-time arena UI wrapper (Control-based) because it's the most robust
        // when swapping screens under a CanvasLayer. Fall back to other scenes if needed.
		var candidates = new[]
		{
			"res://Scenes/UI/ArenaRealtimeView.tscn",
		};

        string? chosen = null;
        foreach (var p in candidates)
        {
            if (ResourceLoader.Exists(p))
            {
                chosen = p;
                break;
            }
        }

        if (chosen == null)
        {
            GD.PrintErr("[CityShell] No arena scene found. Expected one of: " + string.Join(", ", candidates));
            return;
        }

        var scene = GD.Load<PackedScene>(chosen);
        if (scene == null)
        {
            GD.PrintErr($"[CityShell] Failed to load arena scene: {chosen}");
            return;
        }

        GD.Print($"[CityShell] Opening arena: {chosen}");
        var ui = scene.Instantiate();
        parent.AddChild(ui);
        QueueFree();
    }

    private void OpenGarage()
    {
        var parent = GetParent();
        if (parent == null) return;

        var scene = GD.Load<PackedScene>("res://Scenes/UI/GarageView.tscn");
        var ui = scene.Instantiate();
        parent.AddChild(ui);
        QueueFree();
    }

    private void ExitGame()
    {
        // Allow quitting directly from the City shell (main menu).
        GetTree().Quit();
    }
}
