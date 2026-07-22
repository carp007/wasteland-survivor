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

	// A typical post-fight ammo restock runs ~$120-150; purses never pay below this so a win
	// never feels like a net loss. (Kept under tier 1's variance band so payouts still roll —
	// a hard 180 floor above the whole band made every tier-1 purse an identical $180.)
	private const int MinPurseUsd = 150;

	public static WinRewards RollArenaWinRewards(int tier, Random? rng = null)
	{
		var t = Math.Max(1, tier);
		var r = rng ?? Random.Shared;

		// Hand-tuned purse curve: sub-linear early (tier 1 leans on the MinPurseUsd floor),
		// steep enough late to fund Parts Store upgrades without becoming a money faucet.
		var basePurse = t switch
		{
			1 => 160,
			2 => 280,
			3 => 520,
			4 => 800,
			5 => 1150,
			_ => 1150 + (t - 5) * 350,
		};
		var baseScrap = t switch
		{
			1 => 8,
			2 => 14,
			3 => 22,
			4 => 32,
			5 => 45,
			_ => 45 + (t - 5) * 13,
		};
		var baseAmmo = t switch
		{
			1 => 20,
			2 => 35,
			3 => 55,
			4 => 80,
			5 => 110,
			_ => 110 + (t - 5) * 30,
		};

		var rewardMoney = Math.Max(MinPurseUsd, Vary(r, basePurse));
		var rewardScrap = Math.Max(1, Vary(r, baseScrap));
		var rewardAmmo = Math.Max(1, Vary(r, baseAmmo));

		return new WinRewards(rewardMoney, rewardScrap, rewardAmmo);
	}

	/// <summary>Uniform ±30% variance around <paramref name="baseValue"/>.</summary>
	private static int Vary(Random r, int baseValue)
		=> (int)Math.Round(baseValue * (0.7 + r.NextDouble() * 0.6));
}
