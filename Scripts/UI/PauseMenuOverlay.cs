// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/PauseMenuOverlay.cs
// Purpose: Global pause menu overlay toggled with Escape (Settings stub + Exit confirmation). Pauses the scene tree while visible.
// -------------------------------------------------------------------------------------------------
using Godot;
using WastelandSurvivor.Framework.UI;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Simple pause/menu overlay toggled with Escape.
/// - Shows Settings + Exit
/// - Settings opens an empty dialog with a Close button
/// - Exit opens a confirmation dialog
///
/// The overlay pauses the scene tree while open.
/// </summary>
public partial class PauseMenuOverlay : Control
{
	private ColorRect? _dim;
	private PanelContainer? _menuPanel;
	private Button? _btnSettings;
	private Button? _btnExit;
	private ModalHost? _modalHost;
	private IModalService? _modals;

	public bool IsOpen => Visible;

	public override void _Ready()
	{
		// Allow the menu to keep processing/input while paused.
		ProcessMode = Node.ProcessModeEnum.WhenPaused;
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.All;
		SetProcessUnhandledInput(true);

		// Theme + full-screen anchors.
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
		// AppRoot registers services early; bind now. If startup ordering changes,
		// try again deferred as a safety net.
		EnsureModalRefs();
		if (_modals == null)
			CallDeferred(nameof(EnsureModalRefs));
		Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible) return;
		EnsureModalRefs();
		// If a modal is open, let the ModalHost consume Escape instead of closing the pause menu.
		if (_modalHost != null && _modalHost.Visible) return;
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
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
		_btnSettings?.GrabFocus();
	}

	public void Close()
	{
		Visible = false;
		GetTree().Paused = false;
		if (_menuPanel != null) _menuPanel.Visible = false;
	}

	private void BuildUi()
	{
		// Dim background.
		_dim = new ColorRect
		{
			Name = "Dim",
			Color = new Color(0, 0, 0, 0.55f),
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
	}

	private PanelContainer BuildMainPanel()
	{
		var panel = CreateCenteredPanel("MenuPanel", new Vector2(320, 180));
		var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		panel.AddChild(vbox);

		var title = new Label
		{
			Text = "Paused",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		title.AddThemeFontSizeOverride("font_size", GameUiTheme.TitleFontSize);
		title.AddThemeColorOverride("font_color", GameUiTheme.AccentGoldColor);
		vbox.AddChild(title);

		vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

		_btnSettings = new Button { Name = "BtnSettings", Text = "Settings" };
		_btnSettings.Pressed += () => ShowSettingsModal();
		vbox.AddChild(_btnSettings);

		_btnExit = new Button { Name = "BtnExit", Text = "Exit" };
		_btnExit.Pressed += () => ShowExitConfirmModal();
		vbox.AddChild(_btnExit);

		var hint = new Label
		{
			Text = "Press Esc to resume",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		hint.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 1);
		hint.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
		vbox.AddChild(hint);

		return panel;
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
		if (_menuPanel != null) _menuPanel.Visible = true;
		_btnSettings?.GrabFocus();
	}

	private void ShowSettingsModal()
	{
		EnsureModalRefs();
		if (_modals == null)
		{
			GD.PrintErr("PauseMenuOverlay: IModalService not registered.");
			return;
		}

		_modals.ShowMessage(
			title: "Settings",
			body: "(Coming soon)",
			closeText: "Close",
			onClosed: () => _btnSettings?.GrabFocus(),
			options: new ModalOptions(DimBackground: false, CloseOnEscape: true, AutoFocus: true));
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
			body: "Are you sure you want to exit?",
			confirmText: "Exit",
			cancelText: "Cancel",
			onConfirm: () => GetTree().Quit(),
			onCancel: () => _btnSettings?.GrabFocus(),
			options: new ModalOptions(DimBackground: false, CloseOnEscape: true, AutoFocus: true));
	}

	private void EnsureModalRefs()
	{
		// ModalHost is a sibling under OverlayRoot added by AppRoot at runtime.
		_modalHost ??= GetParent()?.GetNodeOrNull<ModalHost>("ModalHost");
		if (_modals != null) return;

		var app = App.Instance;
		if (app != null && app.Services.TryGet<IModalService>(out var svc) && svc != null)
			_modals = svc;
	}
}
