// -------------------------------------------------------------------------------------------------
// GamePawnKit
// File: PawnKit/Pawns/IPossessablePawn.cs
// Purpose: Optional possession/control surface for pawns (player/AI controllers) across games.
// -------------------------------------------------------------------------------------------------
using System;

namespace GamePawnKit.Pawns;

/// <summary>
/// Optional extension over <see cref="IPawn"/> for games that model "possession" by a controller
/// (player input device, AI brain, network peer, etc.).
///
/// Keep this small and engine-agnostic.
/// </summary>
public interface IPossessablePawn : IPawn
{
	/// <summary>
	/// Identifier for the active controller (when player-controlled), otherwise null.
	///
	/// Games can interpret this however they like:
	/// - 0 = local player
	/// - &gt;0 = additional local players / peers
	/// - null = unpossessed / AI-only / disabled
	/// </summary>
	int? ControllerId { get; }

	/// <summary>
	/// Fired when the pawn becomes possessed (i.e., transitions to player-controlled).
	/// Provides the new controller id.
	/// </summary>
	event Action<int?>? Possessed;

	/// <summary>
	/// Fired when the pawn becomes unpossessed (i.e., transitions away from player-controlled).
	/// Provides the previous controller id.
	/// </summary>
	event Action<int?>? Unpossessed;

	/// <summary>
	/// Fired when <see cref="ControllerId"/> changes while still possessed.
	/// (e.g., controller handoff, split-screen reassignment, network authority transfer).
	/// </summary>
	event Action<int?, int?>? ControllerIdChanged;

	/// <summary>
	/// Convenience overload allowing callers to specify a controller id.
	/// </summary>
	void SetPlayerControlled(bool enabled, int? controllerId);
}
