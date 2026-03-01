// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Systems/EncounterRewardGenerator.cs
// Purpose: Pure or near-pure gameplay math/logic (no Godot nodes), called by session/UI.
// -------------------------------------------------------------------------------------------------
using System;

namespace WastelandSurvivor.Game.Systems;

internal static class EncounterRewardGenerator
{
	public readonly record struct WinRewards(int MoneyUsd, int Scrap, int Ammo);

	public static WinRewards RollArenaWinRewards(int tier, Random? rng = null)
	{
		var t = Math.Max(1, tier);
		var r = rng ?? Random.Shared;

		var rewardMoney = r.Next(120, 360);
		var rewardScrap = r.Next(6, 18) * t;
		var rewardAmmo = r.Next(10, 30) * t;

		return new WinRewards(rewardMoney, rewardScrap, rewardAmmo);
	}
}
