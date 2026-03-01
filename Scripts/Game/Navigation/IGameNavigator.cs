// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Navigation/IGameNavigator.cs
// Purpose: Game-level navigation facade so UI scripts do not hard-code scene paths or routing details.
// -------------------------------------------------------------------------------------------------
using Godot;

namespace WastelandSurvivor.Game.Navigation;

/// <summary>
/// Game navigation API used by UI nodes. This keeps scene-path knowledge and routing/fallback logic
/// out of individual UI scripts (and makes it easy to evolve navigation behavior later).
/// </summary>
public interface IGameNavigator
{
	void ToCityShell(Node from);
	void ToGarage(Node from);
	void ToWorkshop(Node from);
	void ToArena(Node from);
}
