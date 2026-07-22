// -------------------------------------------------------------------------------------------------
// GamePawnKit
// File: PawnKit/Pawns/IEnterExit.cs
// Purpose: Small generic interfaces to model enter/exit interactions across games.
// -------------------------------------------------------------------------------------------------
using System;

namespace GamePawnKit.Pawns;

/// <summary>
/// Represents something that can be entered by a pawn (vehicles, turrets, stations, mounts, etc.).
///
/// This interface is intentionally tiny; games can layer richer seat/occupancy models on top.
/// </summary>
public interface IEnterable
{
	bool IsOccupied { get; }
	IPawn? Occupant { get; }

	/// <summary>
	/// Returns true if the given pawn can enter now.
	/// </summary>
	bool CanEnter(IPawn entrant);

	/// <summary>
	/// Attempts to enter. Returns true if the transition occurred.
	/// </summary>
	bool TryEnter(IPawn entrant);

	event Action<IPawn>? Entered;
}

/// <summary>
/// Represents something that can be exited.
/// Typically paired with <see cref="IEnterable"/>.
/// </summary>
public interface IExitable
{
	bool CanExit();
	bool TryExit();
	event Action<IPawn>? Exited;
}
