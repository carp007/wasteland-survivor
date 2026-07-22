// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/PauseMenuOverlay.cs
// Purpose: Global pause menu overlay toggled with Escape. Pauses the scene tree while visible and
//          presents a more game-like menu layout with Resume / Save / Settings / Exit actions.
// -------------------------------------------------------------------------------------------------
using Godot;
using GameUiKit.UI;
using WastelandSurvivor.Game;

namespace WastelandSurvivor.Game.UI;

public partial class PauseMenuOverlay : Control
{
	private ColorRect? _dim;
	private PanelContainer? _menuPanel;
	private Button? _btnResume;
	private Button? _btnSave;
	private Button? _btnSettings;
	private Button? _btnExit;
	private Label? _lblSaveStatus;
	private ModalHost? _modalHost;
	private IModalService? _modals;

	// Settings page (swapped with the main panel while open).
	private PanelContainer? _settingsPanel;
	private HSlider? _sldMaster;
	private HSlider? _sldSfx;
	private HSlider? _sldEngines;
	private HSlider? _sldMusic;
	private Label? _lblMasterPct;
	private Label? _lblSfxPct;
	private Label? _lblEnginesPct;
	private Label? _lblMusicPct;
	private CheckButton? _chkFullscreen;
	private bool _syncingSettingsUi;

	public bool IsOpen => Visible;

	public override void _Ready()
	{
		ProcessMode = Node.ProcessModeEnum.WhenPaused;
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.All;
		SetProcessUnhandledInput(true);

		GameUiTheme.ApplyTo(this);
		AnchorLeft = 0;
		AnchorTop = 0;
		AnchorRight = 1;
		AnchorBottom = 1;
		OffsetLeft = 0;
		OffsetTop = 0;
		OffsetRight = 0;
		OffsetBottom = 0;

		BuildUi();
		EnsureModalRefs();
		if (_modals == null)
			CallDeferred(nameof(EnsureModalRefs));
		Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible) return;
		EnsureModalRefs();
		if (_modalHost != null && _modalHost.Visible) return;
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
			// Escape backs out of the settings page first, then closes the pause menu.
			if (_settingsPanel != null && _settingsPanel.Visible)
				CloseSettings();
			else
				Close();
			GetViewport().SetInputAsHandled();
		}
	}

	public void Toggle()
	{
		if (Visible) Close();
		else Open();
	}

	public void Open()
	{
		Visible = true;
		ShowMain();
		GetTree().Paused = true;
		_btnResume?.GrabFocus();
	}

	public void Close()
	{
		if (_settingsPanel != null && _settingsPanel.Visible)
			PersistSettings();
		Visible = false;
		GetTree().Paused = false;
		if (_menuPanel != null) _menuPanel.Visible = false;
		if (_settingsPanel != null) _settingsPanel.Visible = false;
		if (_lblSaveStatus != null) _lblSaveStatus.Text = "";
	}

	private void BuildUi()
	{
		_dim = new ColorRect
		{
			Name = "Dim",
			Color = new Color(0, 0, 0, 0.62f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		AddChild(_dim);
		_dim.AnchorLeft = 0;
		_dim.AnchorTop = 0;
		_dim.AnchorRight = 1;
		_dim.AnchorBottom = 1;
		_dim.OffsetLeft = 0;
		_dim.OffsetTop = 0;
		_dim.OffsetRight = 0;
		_dim.OffsetBottom = 0;

		_menuPanel = BuildMainPanel();
		AddChild(_menuPanel);

		_settingsPanel = BuildSettingsPanel();
		_settingsPanel.Visible = false;
		AddChild(_settingsPanel);
	}

	private PanelContainer BuildSettingsPanel()
	{
		var panel = CreateCenteredPanel("SettingsPanel", new Vector2(480, 460));
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 24);
		margin.AddThemeConstantOverride("margin_right", 24);
		margin.AddThemeConstantOverride("margin_top", 20);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		panel.AddChild(margin);

		var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		vbox.AddThemeConstantOverride("separation", 12);
		margin.AddChild(vbox);

		var title = new Label
		{
			Text = "SETTINGS",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		GameUiTheme.StyleHeading(title, GameUiTheme.TitleFontSize + 6);
		vbox.AddChild(title);

		var audioHeader = new Label { Text = "AUDIO" };
		GameUiTheme.StyleHeading(audioHeader, GameUiTheme.BaseFontSize - 1, GameUiTheme.AccentCyanColor);
		vbox.AddChild(audioHeader);

		(_sldMaster, _lblMasterPct) = AddVolumeRow(vbox, "Master");
		(_sldSfx, _lblSfxPct) = AddVolumeRow(vbox, "Effects");
		(_sldEngines, _lblEnginesPct) = AddVolumeRow(vbox, "Engines");
		(_sldMusic, _lblMusicPct) = AddVolumeRow(vbox, "Music");

		var displayHeader = new Label { Text = "DISPLAY" };
		GameUiTheme.StyleHeading(displayHeader, GameUiTheme.BaseFontSize - 1, GameUiTheme.AccentCyanColor);
		vbox.AddChild(displayHeader);

		_chkFullscreen = new CheckButton
		{
			Text = "Fullscreen (also F11)",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_chkFullscreen.Toggled += OnFullscreenToggled;
		vbox.AddChild(_chkFullscreen);

		var btnBack = BuildMenuButton("Back");
		btnBack.Pressed += CloseSettings;
		vbox.AddChild(btnBack);

		return panel;
	}

	private (HSlider slider, Label pct) AddVolumeRow(VBoxContainer parent, string label)
	{
		var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 10);
		parent.AddChild(row);

		var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(86, 0) };
		row.AddChild(lbl);

		var slider = new HSlider
		{
			MinValue = 0.0,
			MaxValue = 1.0,
			Step = 0.05,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(0, 24),
			FocusMode = FocusModeEnum.All,
		};
		slider.ValueChanged += _ => OnVolumeSliderChanged();
		row.AddChild(slider);

		var pct = new Label
		{
			Text = "100%",
			CustomMinimumSize = new Vector2(52, 0),
			HorizontalAlignment = HorizontalAlignment.Right,
		};
		row.AddChild(pct);

		return (slider, pct);
	}

	private void ShowSettings()
	{
		SyncSettingsUiFromState();
		if (_menuPanel != null) _menuPanel.Visible = false;
		if (_settingsPanel != null) _settingsPanel.Visible = true;
		_sldMaster?.GrabFocus();
	}

	private void CloseSettings()
	{
		PersistSettings();
		if (_settingsPanel != null) _settingsPanel.Visible = false;
		ShowMain();
		_btnSettings?.GrabFocus();
	}

	private void SyncSettingsUiFromState()
	{
		_syncingSettingsUi = true;
		try
		{
			var s = GameSettingsStore.Load();
			if (_sldMaster != null) _sldMaster.Value = s.MasterVolume01;
			if (_sldSfx != null) _sldSfx.Value = s.SfxVolume01;
			if (_sldEngines != null) _sldEngines.Value = s.EnginesVolume01;
			if (_sldMusic != null) _sldMusic.Value = s.MusicVolume01;

			var mode = DisplayServer.WindowGetMode();
			var isFullscreen = mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
			if (_chkFullscreen != null) _chkFullscreen.ButtonPressed = isFullscreen;
			UpdateVolumePctLabels();
		}
		finally
		{
			_syncingSettingsUi = false;
		}
	}

	private void OnVolumeSliderChanged()
	{
		if (_syncingSettingsUi) return;
		UpdateVolumePctLabels();
		PersistSettings();
		GameSettingsStore.ApplyAudio();
	}

	private void OnFullscreenToggled(bool pressed)
	{
		if (_syncingSettingsUi) return;

		var mode = DisplayServer.WindowGetMode();
		var isFullscreen = mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
		if (pressed != isFullscreen)
			App.Instance?.ToggleFullscreen();
		PersistSettings();
	}

	private void PersistSettings()
	{
		var s = GameSettingsStore.Current with
		{
			MasterVolume01 = (float)(_sldMaster?.Value ?? 1.0),
			SfxVolume01 = (float)(_sldSfx?.Value ?? 1.0),
			EnginesVolume01 = (float)(_sldEngines?.Value ?? 1.0),
			MusicVolume01 = (float)(_sldMusic?.Value ?? 1.0),
			StartFullscreen = _chkFullscreen?.ButtonPressed ?? true,
		};
		GameSettingsStore.Save(s);
	}

	private void UpdateVolumePctLabels()
	{
		if (_lblMasterPct != null && _sldMaster != null) _lblMasterPct.Text = $"{_sldMaster.Value * 100:0}%";
		if (_lblSfxPct != null && _sldSfx != null) _lblSfxPct.Text = $"{_sldSfx.Value * 100:0}%";
		if (_lblEnginesPct != null && _sldEngines != null) _lblEnginesPct.Text = $"{_sldEngines.Value * 100:0}%";
		if (_lblMusicPct != null && _sldMusic != null) _lblMusicPct.Text = $"{_sldMusic.Value * 100:0}%";
	}

	private PanelContainer BuildMainPanel()
	{
		var panel = CreateCenteredPanel("MenuPanel", new Vector2(420, 360));
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 22);
		margin.AddThemeConstantOverride("margin_right", 22);
		margin.AddThemeConstantOverride("margin_top", 22);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		panel.AddChild(margin);

		var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		vbox.AddThemeConstantOverride("separation", 12);
		margin.AddChild(vbox);

		var title = new Label
		{
			Text = "PAUSED",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		GameUiTheme.StyleHeading(title, GameUiTheme.TitleFontSize + 8);
		vbox.AddChild(title);

		_btnResume = BuildMenuButton("Resume Game");
		GeneratedUiArt.ApplyIcon(_btnResume, "icon_resume", 22);
		_btnResume.Pressed += Close;
		vbox.AddChild(_btnResume);

		_btnSave = BuildMenuButton("Save Game");
		GeneratedUiArt.ApplyIcon(_btnSave, "icon_save", 20);
		_btnSave.Pressed += SaveGame;
		vbox.AddChild(_btnSave);

		_btnSettings = BuildMenuButton("Settings");
		GeneratedUiArt.ApplyIcon(_btnSettings, "icon_settings", 20);
		_btnSettings.Pressed += ShowSettings;
		vbox.AddChild(_btnSettings);

		_btnExit = BuildMenuButton("Exit to Desktop");
		GeneratedUiArt.ApplyIcon(_btnExit, "icon_exit", 20);
		_btnExit.Pressed += ShowExitConfirmModal;
		vbox.AddChild(_btnExit);


		_lblSaveStatus = new Label
		{
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0, 22),
		};
		_lblSaveStatus.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 1);
		_lblSaveStatus.AddThemeColorOverride("font_color", GameUiTheme.SuccessColor);
		vbox.AddChild(_lblSaveStatus);

		var footer = new Label
		{
			Text = $"WASTELAND SURVIVOR · build {ReadBuildId()}",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		footer.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		footer.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 4);
		vbox.AddChild(footer);

		return panel;
	}

	private static string ReadBuildId()
	{
		try
		{
			if (FileAccess.FileExists("res://VERSION.txt"))
				return FileAccess.GetFileAsString("res://VERSION.txt").Trim();
		}
		catch { /* cosmetic only */ }
		return "dev";
	}

	private static Button BuildMenuButton(string text)
	{
		return new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(0, 48),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Alignment = HorizontalAlignment.Center,
			FocusMode = Control.FocusModeEnum.All,
		};
	}

	private static PanelContainer CreateCenteredPanel(string name, Vector2 size)
	{
		var panel = new PanelContainer
		{
			Name = name,
			MouseFilter = MouseFilterEnum.Stop,
		};
		panel.AnchorLeft = 0.5f;
		panel.AnchorTop = 0.5f;
		panel.AnchorRight = 0.5f;
		panel.AnchorBottom = 0.5f;
		panel.OffsetLeft = -size.X * 0.5f;
		panel.OffsetRight = size.X * 0.5f;
		panel.OffsetTop = -size.Y * 0.5f;
		panel.OffsetBottom = size.Y * 0.5f;
		return panel;
	}

	private void ShowMain()
	{
		if (_settingsPanel != null) _settingsPanel.Visible = false;
		if (_menuPanel != null) _menuPanel.Visible = true;
		_btnResume?.GrabFocus();
	}

	private void SaveGame()
	{
		var app = App.Instance;
		if (app == null)
		{
			if (_lblSaveStatus != null)
				_lblSaveStatus.Text = "Unable to save right now.";
			return;
		}

		if (!app.Services.TryGet<GameSession>(out var session) || session == null)
		{
			if (_lblSaveStatus != null)
				_lblSaveStatus.Text = "Unable to save right now.";
			return;
		}

		session.Persist();
		if (_lblSaveStatus != null)
			_lblSaveStatus.Text = "Game saved.";
		_btnSave?.GrabFocus();
	}

	private void ShowExitConfirmModal()
	{
		EnsureModalRefs();
		if (_modals == null)
		{
			GD.PrintErr("PauseMenuOverlay: IModalService not registered.");
			return;
		}

		_modals.ShowConfirm(
			title: "Exit Game",
			body: "Leave Wasteland Survivor and close the application?",
			confirmText: "Exit Game",
			cancelText: "Stay",
			onConfirm: () => GetTree().Quit(),
			onCancel: () => _btnResume?.GrabFocus(),
			options: new ModalOptions(DimBackground: false, CloseOnEscape: true, AutoFocus: true));
	}

	private void EnsureModalRefs()
	{
		_modalHost ??= GetParent()?.GetNodeOrNull<ModalHost>("ModalHost");
		if (_modals != null) return;

		var app = App.Instance;
		if (app != null && app.Services.TryGet<IModalService>(out var svc) && svc != null)
			_modals = svc;
	}
}
