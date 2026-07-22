// -------------------------------------------------------------------------------------------------
// GamePawnKit
// File: PawnKit/Pawns/IPawn.cs
// Purpose: Minimal, reusable interfaces for pawn-style gameplay objects.
// -------------------------------------------------------------------------------------------------
using System;

namespace GamePawnKit.Pawns;

/// <summary>
/// Base abstraction for any player/AI controllable "pawn".
/// Keep this interface small and broadly applicable.
/// </summary>
public interface IPawn
{
	bool IsDead { get; }
	bool IsPlayerControlled { get; }

	/// <summary>
	/// Fired when <see cref="IsPlayerControlled"/> changes.
	/// </summary>
	event Action<bool>? PlayerControlledChanged;

	/// <summary>
	/// Fired when the pawn transitions to the dead state.
	/// </summary>
	event Action? Died;

	void SetPlayerControlled(bool enabled);
}
