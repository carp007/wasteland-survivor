// -------------------------------------------------------------------------------------------------
// GamePawnKit
// File: PawnKit/Pawns/IHumanoidPawn.cs
// Purpose: Optional specialization for on-foot / humanoid pawns.
// -------------------------------------------------------------------------------------------------
using Godot;

namespace GamePawnKit.Pawns;

public interface IHumanoidPawn : IPossessablePawn
{
	Vector3 MoveInput { get; set; }
	bool Sprint { get; set; }
	void Stop();
}
