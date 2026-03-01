// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/Sim/CommandsEvents.cs
// Purpose: Simulation command/event structs used for decoupling input from simulation ticks.
// -------------------------------------------------------------------------------------------------
namespace WastelandSurvivor.Core.Sim;

// Marker interfaces for future multiplayer/authority patterns.
public interface IGameCommand { }
public interface IGameEvent { }
