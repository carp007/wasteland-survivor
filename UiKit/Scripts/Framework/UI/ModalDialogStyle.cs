// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: UiKit/Scripts/Framework/UI/ModalDialogStyle.cs
// Purpose: Small style container for ModalService's built-in dialogs. This keeps the UiKit project
//          reusable by letting the game provide theme + per-dialog styling.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Framework.UI;

/// <summary>
/// Styling hooks used by <see cref="ModalService"/> when building message/confirm dialogs.
/// 
/// In reusable UI code we avoid depending on any game-specific theme implementation; the game can
/// provide these hooks at service registration time.
/// </summary>
public sealed record ModalDialogStyle(
	Action<Node>? ThemeApplier = null,
	Action<DialogCard>? DialogStyler = null,
	Vector2? DefaultMinSize = null);
