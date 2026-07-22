// -------------------------------------------------------------------------------------------------
// GamePawnKit
// File: PawnKit/Pawns/IVehiclePawn.cs
// Purpose: Optional specialization for vehicle-type pawns.
// -------------------------------------------------------------------------------------------------
namespace GamePawnKit.Pawns;

public interface IVehiclePawn : IPossessablePawn
{
	float ThrottleInput { get; set; } // -1..1
	float SteerInput { get; set; }    // -1..1
}
