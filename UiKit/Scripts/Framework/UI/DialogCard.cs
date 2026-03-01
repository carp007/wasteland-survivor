// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Framework/UI/DialogCard.cs
// Purpose: Reusable modal “card” layout used by ModalService (title + body + buttons/content area).
//          Designed to be portable to future Godot/C# projects: project-specific styling is injected
//          via ThemeApplier/DialogStyler callbacks.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Framework.UI;

/// <summary>
/// A small, code-only dialog shell: centered panel with a title, body text, and a footer area for
/// buttons or any custom content.
/// </summary>
public partial class DialogCard : PanelContainer
{
	private bool _built;
	private VBoxContainer? _root;
	private Control? _buttonHost;

	private string? _pendingTitle;
	private string? _pendingBody;
	private Vector2? _pendingMinSize;

	/// <summary>
	/// Optional hook to apply a project theme / overrides to the dialog tree.
	/// Called once after the internal controls are built.
	/// </summary>
	public Action<Node>? ThemeApplier { get; set; }

	/// <summary>
	/// Optional hook to apply project-specific styling (colors, font sizes, etc.).
	/// Called once after the internal controls are built.
	/// </summary>
	public Action<DialogCard>? DialogStyler { get; set; }

	/// <summary>Title label (may be null prior to build).</summary>
	public Label? TitleLabel { get; private set; }

	/// <summary>Body label (may be null prior to build).</summary>
	public Label? BodyLabel { get; private set; }

	/// <summary>
	/// Configure the dialog content and sizing. Safe to call before the node enters the tree.
	/// </summary>
	public void Configure(string title, string body, Vector2? minSize = null)
	{
		_pendingTitle = title;
		_pendingBody = body;
		_pendingMinSize = minSize;
		ApplyPending();
	}

	/// <summary>
	/// Adds one or more controls to the dialog footer area (usually buttons).
	/// Safe to call before the node enters the tree.
	/// </summary>
	public void AddButtons(params Control[] controls)
	{
		if (controls == null || controls.Length == 0) return;
		EnsureBuilt();
		if (_buttonHost == null) return;

		foreach (var c in controls)
		{
			if (c == null) continue;
			_buttonHost.AddChild(c);
		}
	}

	public override void _Ready()
	{
		EnsureBuilt();
		ApplyPending();
	}

	private void EnsureBuilt()
	{
		if (_built) return;
		_built = true;

		// ModalHost is responsible for centering the dialog; the card should shrink-to-fit.
		SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		SizeFlagsVertical = SizeFlags.ShrinkCenter;

		_root = new VBoxContainer
		{
			Name = "Root",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_root.AddThemeConstantOverride("separation", 12);
		AddChild(_root);

		TitleLabel = new Label
		{
			Name = "Title",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_root.AddChild(TitleLabel);

		BodyLabel = new Label
		{
			Name = "Body",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_root.AddChild(BodyLabel);

		_buttonHost = new VBoxContainer
		{
			Name = "Buttons",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		((VBoxContainer)_buttonHost).AddThemeConstantOverride("separation", 8);
		_root.AddChild(_buttonHost);

		ThemeApplier?.Invoke(this);
		DialogStyler?.Invoke(this);
	}

	private void ApplyPending()
	{
		if (!_built) return;

		if (_pendingMinSize.HasValue)
			CustomMinimumSize = _pendingMinSize.Value;

		if (TitleLabel != null && _pendingTitle != null)
			TitleLabel.Text = _pendingTitle;

		if (BodyLabel != null && _pendingBody != null)
			BodyLabel.Text = _pendingBody;
	}
}
