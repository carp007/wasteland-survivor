// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/GameLog.cs
// Purpose: Game-layer services and facades (session, balance, logging).
// -------------------------------------------------------------------------------------------------
using Godot;

namespace WastelandSurvivor.Game;

/// <summary>
/// Convenience logging API so any game object can write to the global Console
/// without threading service references everywhere.
/// </summary>
public static class GameLog
{
	private static GameConsole? TryGetConsole()
	{
		var app = App.Instance;
		if (app is null) return null;
		return app.Services.TryGet<GameConsole>(out var c) ? c : null;
	}

	public static void Debug(string text)
	{
		(TryGetConsole())?.Debug(text);
		GD.Print(text);
	}

	public static void Status(string text)
	{
		(TryGetConsole())?.Status(text);
		GD.Print(text);
	}

	public static void Input(string text)
	{
		(TryGetConsole())?.Input(text);
		GD.Print(text);
	}

	public static void Error(string text)
	{
		(TryGetConsole())?.Error(text);
		GD.PrintErr(text);
	}
}
