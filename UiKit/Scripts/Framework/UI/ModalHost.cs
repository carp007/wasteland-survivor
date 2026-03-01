// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Framework/UI/ModalHost.cs
// Purpose: Fullscreen modal host used to display dialogs/overlays above the current UI.
//          Implemented as a small, reusable Control that supports stacking and Escape-to-close.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Godot;

namespace WastelandSurvivor.Framework.UI;

/// <summary>
/// Fullscreen modal host.
/// 
/// Intended usage:
/// - Add a <see cref="ModalHost"/> node under an overlay CanvasLayer (e.g., Main.tscn OverlayRoot).
/// - Register <see cref="ModalService"/> in AppRoot so UI scripts can open dialogs via <see cref="IModalService"/>.
/// 
/// Notes:
/// - ModalHost runs <see cref="Node.ProcessModeEnum.WhenPaused"/> so it remains interactive while the game is paused.
/// - Supports stacking (top modal is visible; previous is hidden).
/// </summary>
public partial class ModalHost : Control
{
	private sealed class Entry
	{
		public required long Id;
		public required Control Root;
		public required bool CloseOnEscape;
	}

	private ColorRect? _dim;
	private Control? _layer;
	private readonly List<Entry> _stack = new();
	private long _nextId = 1;

	public override void _Ready()
	{
		// Ensure modals can be used while the game is paused (e.g., PauseMenuOverlay).
		ProcessMode = Node.ProcessModeEnum.WhenPaused;
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.All;
		SetProcessUnhandledInput(true);

		// Fullscreen anchors.
		AnchorLeft = 0;
		AnchorTop = 0;
		AnchorRight = 1;
		AnchorBottom = 1;
		OffsetLeft = 0;
		OffsetTop = 0;
		OffsetRight = 0;
		OffsetBottom = 0;

		// Render above most UI, but below the console overlay (ConsoleOverlay sets ZIndex=1000).
		ZAsRelative = false;
		ZIndex = 500;

		BuildIfNeeded();
		// If something opened a modal before we reached _Ready (rare, but can happen during boot),
		// do not hide it.
		Visible = _stack.Count > 0;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible) return;
		if (_stack.Count == 0) return;

		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
			var top = _stack[^1];
			if (top.CloseOnEscape)
			{
				Close(top.Id);
				GetViewport().SetInputAsHandled();
			}
		}
	}

	/// <summary>
	/// Show <paramref name="content"/> as a modal dialog.
	/// </summary>
	public IModalHandle Show(Control content, ModalOptions options)
	{
		if (content == null) throw new ArgumentNullException(nameof(content));
		BuildIfNeeded();

		// Hide previous modal (if any).
		if (_stack.Count > 0)
			_stack[^1].Root.Visible = false;

		var id = _nextId++;
		var root = new CenterContainer
		{
			Name = $"Modal_{id}",
			MouseFilter = MouseFilterEnum.Stop,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		root.AnchorLeft = 0;
		root.AnchorTop = 0;
		root.AnchorRight = 1;
		root.AnchorBottom = 1;
		root.OffsetLeft = 0;
		root.OffsetTop = 0;
		root.OffsetRight = 0;
		root.OffsetBottom = 0;

		// Ensure the modal content remains interactive while paused.
		content.ProcessMode = Node.ProcessModeEnum.WhenPaused;
		content.MouseFilter = MouseFilterEnum.Stop;
		content.FocusMode = FocusModeEnum.All;
		root.AddChild(content);

		_layer!.AddChild(root);
		_stack.Add(new Entry { Id = id, Root = root, CloseOnEscape = options.CloseOnEscape });

		UpdateDim(options.DimBackground);
		Visible = true;

		if (options.AutoFocus)
			CallDeferred(nameof(FocusFirstControl), content);

		return new Handle(this, id);
	}

	private void FocusFirstControl(Node root)
	{
		// Best-effort: focus the first focusable Control.
		if (root is Control c && c.FocusMode != FocusModeEnum.None)
		{
			c.GrabFocus();
			return;
		}

		foreach (var child in root.GetChildren())
			FocusFirstControl((Node)child);
	}

	public bool Close(long id)
	{
		for (var i = _stack.Count - 1; i >= 0; i--)
		{
			if (_stack[i].Id != id) continue;

			var entry = _stack[i];
			_stack.RemoveAt(i);

			if (GodotObject.IsInstanceValid(entry.Root))
				entry.Root.QueueFree();

			// Show previous modal (if any)
			if (_stack.Count > 0)
				_stack[^1].Root.Visible = true;

			if (_stack.Count == 0)
				Visible = false;
			return true;
		}

		return false;
	}

	public void CloseAll()
	{
		for (var i = _stack.Count - 1; i >= 0; i--)
		{
			var entry = _stack[i];
			if (GodotObject.IsInstanceValid(entry.Root))
				entry.Root.QueueFree();
		}
		_stack.Clear();
		Visible = false;
	}

	private void BuildIfNeeded()
	{
		if (_dim != null && _layer != null)
			return;

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

		_layer = new Control
		{
			Name = "ModalLayer",
			MouseFilter = MouseFilterEnum.Stop,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		AddChild(_layer);
		_layer.AnchorLeft = 0;
		_layer.AnchorTop = 0;
		_layer.AnchorRight = 1;
		_layer.AnchorBottom = 1;
		_layer.OffsetLeft = 0;
		_layer.OffsetTop = 0;
		_layer.OffsetRight = 0;
		_layer.OffsetBottom = 0;
	}

	private void UpdateDim(bool dim)
	{
		if (_dim == null) return;
		_dim.Visible = dim;
	}

	private sealed class Handle : IModalHandle
	{
		private readonly ModalHost _host;
		private readonly long _id;
		private bool _closed;

		public Handle(ModalHost host, long id)
		{
			_host = host;
			_id = id;
		}

		public bool IsOpen => !_closed;

		public void Close()
		{
			if (_closed) return;
			_closed = true;
			_host.Close(_id);
		}
	}
}
