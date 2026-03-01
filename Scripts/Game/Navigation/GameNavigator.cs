// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Navigation/GameNavigator.cs
// Purpose: Default implementation of IGameNavigator using UiNav + GameScenes (router-first, legacy fallback).
// -------------------------------------------------------------------------------------------------
using Godot;
using WastelandSurvivor.Framework.UI;
using WastelandSurvivor.Game.UI;

namespace WastelandSurvivor.Game.Navigation;

public sealed class GameNavigator : IGameNavigator
{
	private static ScreenRouter? TryGetRouter()
	{
		var app = WastelandSurvivor.Game.App.Instance;
		if (app != null && app.Services.TryGet<ScreenRouter>(out var router))
			return router;
		return null;
	}

	public void ToCityShell(Node from)
		=> UiNav.Replace(from, GameScenes.CityShell, TryGetRouter());

	public void ToGarage(Node from)
		=> UiNav.Replace(from, GameScenes.GarageView, TryGetRouter());

	public void ToWorkshop(Node from)
		=> UiNav.Replace(from, GameScenes.WorkshopView, TryGetRouter());

	public void ToArena(Node from)
	{
		if (!GameScenes.TryGetArenaEntry(out var path))
		{
			GD.PrintErr("[Nav] No arena scene found. Expected one of: " + GameScenes.DescribeArenaCandidates());
			return;
		}

		UiNav.Replace(from, path, TryGetRouter());
	}
}
