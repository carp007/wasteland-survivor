// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/GameScenes.cs
// Purpose: Central catalog of UI scene paths. Keeps navigation call sites free of hard-coded strings.
// -------------------------------------------------------------------------------------------------
using Godot;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Central catalog of common scene paths.
/// 
/// Keeping these in one place makes it easier to:
/// - rename/move scenes without hunting string literals
/// - standardize navigation flows
/// - reuse navigation helpers in future projects
/// </summary>
public static class GameScenes
{
	public const string BootSplashView = "res://Scenes/UI/BootSplashView.tscn";
	public const string CityShell = "res://Scenes/UI/CityShell.tscn";
	public const string GarageView = "res://Scenes/UI/GarageView.tscn";
	public const string WorkshopView = "res://Scenes/UI/WorkshopView.tscn";
	public const string ArenaRealtimeView = "res://Scenes/UI/ArenaRealtimeView.tscn";
	public const string VehicleStatusHud = "res://Scenes/UI/VehicleStatusHud.tscn";

	private static readonly string[] ArenaEntryCandidates =
	{
		ArenaRealtimeView,
	};

	/// <summary>
	/// Resolve the best arena entry scene available in the project.
	/// This lets us switch to a different wrapper/scene later without changing UI scripts.
	/// </summary>
	public static bool TryGetArenaEntry(out string scenePath)
	{
		foreach (var p in ArenaEntryCandidates)
		{
			if (ResourceLoader.Exists(p))
			{
				scenePath = p;
				return true;
			}
		}

		scenePath = string.Empty;
		return false;
	}

	public static string DescribeArenaCandidates() => string.Join(", ", ArenaEntryCandidates);
}
