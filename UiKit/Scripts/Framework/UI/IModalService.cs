// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Framework/UI/IModalService.cs
// Purpose: Project-agnostic modal dialog service interface. Keeps UI screens from hand-rolling overlays
//          and enables consistent, reusable modal behaviors across this project (and future Godot games).
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Framework.UI;

/// <summary>
/// A small service for showing modal UI (dialogs, confirmations, etc.) above the current screen.
/// 
/// Design goals:
/// - Reusable: copy/paste friendly into other Godot/C# projects.
/// - Minimal: just enough for common game UI flows (pause/settings/confirmations).
/// - Safe with pause: modal UIs should still work when <c>GetTree().Paused = true</c>.
/// </summary>
public interface IModalService
{
	/// <summary>Show an arbitrary Control as a modal. Returns a handle that can close the modal.</summary>
	IModalHandle Show(Control content, ModalOptions? options = null);

	/// <summary>Show a simple message dialog (title + body + one Close button).</summary>
	IModalHandle ShowMessage(
		string title,
		string body,
		string closeText = "Close",
		Action? onClosed = null,
		ModalOptions? options = null);

	/// <summary>Show a simple confirmation dialog (title + body + confirm/cancel buttons).</summary>
	IModalHandle ShowConfirm(
		string title,
		string body,
		string confirmText,
		string cancelText,
		Action onConfirm,
		Action? onCancel = null,
		ModalOptions? options = null);
}

/// <summary>
/// Handle returned by <see cref="IModalService"/>. Allows callers to close an opened modal.
/// </summary>
public interface IModalHandle
{
	bool IsOpen { get; }
	void Close();
}

/// <summary>
/// Options to control basic modal behavior.
/// </summary>
public sealed record ModalOptions(
	bool DimBackground = true,
	bool CloseOnEscape = true,
	bool AutoFocus = true
);
