using System;
using System.Linq;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Game;

namespace WastelandSurvivor.Game.Arena;

internal sealed record ArenaPostEncounterViewModel(
	bool Visible,
	string RewardsText,
	string RepairText,
	bool RepairDisabled,
	string RepairDriverArmorText,
	bool RepairDriverArmorDisabled,
	string PatchArmorText,
	bool PatchArmorDisabled,
	string PatchTireText,
	bool PatchTireDisabled);

/// <summary>
/// Builds the post-encounter panel view-model from session state so ArenaRealtimeView stays focused on wiring.
/// </summary>
internal static class ArenaPostEncounterPresenter
{
	public static ArenaPostEncounterViewModel Build(GameSession session, DefDatabase? defs)
	{
		var encounter = session.GetCurrentEncounter();
		if (encounter is null || encounter.Outcome is null)
		{
			return new ArenaPostEncounterViewModel(
				Visible: false,
				RewardsText: string.Empty,
				RepairText: "Repair Vehicle",
				RepairDisabled: true,
				RepairDriverArmorText: "Repair Armor",
				RepairDriverArmorDisabled: true,
				PatchArmorText: "Patch Armor (-1 Scrap)",
				PatchArmorDisabled: true,
				PatchTireText: "Patch Tire (-1 Scrap)",
				PatchTireDisabled: true);
		}

		var ammoRewardText = encounter.AmmoRewards is { Length: > 0 }
			? string.Join(", ", encounter.AmmoRewards.Select(a => $"{a.Count} {a.AmmoId}"))
			: "none";

		var rewardsText = $"Outcome: {CapitalizeOutcome(encounter.Outcome)}\nCredits Earned: +${encounter.MoneyRewardUsd}\nScrap Recovered: +{encounter.ScrapReward}\nAmmo Recovered: {ammoRewardText}";
		if (encounter.BattlefieldScrapRecovered > 0)
			rewardsText += $"\nBattlefield Salvage: +{encounter.BattlefieldScrapRecovered} scrap";
		if (encounter.TowedVehicleRecovered && !string.IsNullOrWhiteSpace(encounter.TowedVehicleDisplayName))
			rewardsText += $"\nRecovered Vehicle: {encounter.TowedVehicleDisplayName}";
		if (encounter.HijackedVehicleRecovered && !string.IsNullOrWhiteSpace(encounter.HijackedVehicleDisplayName))
			rewardsText += $"\nClaimed Vehicle: {encounter.HijackedVehicleDisplayName}";

		var repairText = "Repair Vehicle";
		var repairDisabled = true;
		var patchArmorText = "Patch Armor (-1 Scrap)";
		var patchArmorDisabled = true;
		var patchTireText = "Patch Tire (-1 Scrap)";
		var patchTireDisabled = true;

		var activeId = session.Save.Player.ActiveVehicleId;
		if (defs != null && !string.IsNullOrWhiteSpace(activeId))
		{
			var (repairMissing, repairCost) = session.ComputeRepairToFullCost(activeId!, defs);
			if (repairMissing <= 0)
			{
				repairText = "Repair Vehicle (Full)";
				repairDisabled = true;
			}
			else
			{
				repairText = $"Repair Vehicle (-${repairCost})";
				repairDisabled = session.Save.Player.MoneyUsd < repairCost;
			}

			var (armorMissing, tireMissing, _) = session.ComputeMissingRepairPointsByType(activeId!, defs);
			var scrap = session.Save.Player.Scrap;
			patchArmorText = armorMissing <= 0 ? "Patch Armor (Full)" : "Patch Armor (-1 Scrap)";
			patchArmorDisabled = armorMissing <= 0 || scrap < GameSession.ScrapRepairCostPerPoint;
			patchTireText = tireMissing <= 0 ? "Patch Tire (Full)" : "Patch Tire (-1 Scrap)";
			patchTireDisabled = tireMissing <= 0 || scrap < GameSession.ScrapRepairCostPerPoint;
		}

		var (missingArmorAp, repairArmorCost) = session.ComputeDriverArmorRepairCost();
		var repairDriverArmorText = missingArmorAp <= 0
			? "Repair Armor (Full)"
			: $"Repair Armor (-${repairArmorCost})";
		var repairDriverArmorDisabled = missingArmorAp <= 0 || session.Save.Player.MoneyUsd < repairArmorCost;

		return new ArenaPostEncounterViewModel(
			Visible: true,
			RewardsText: rewardsText,
			RepairText: repairText,
			RepairDisabled: repairDisabled,
			RepairDriverArmorText: repairDriverArmorText,
			RepairDriverArmorDisabled: repairDriverArmorDisabled,
			PatchArmorText: patchArmorText,
			PatchArmorDisabled: patchArmorDisabled,
			PatchTireText: patchTireText,
			PatchTireDisabled: patchTireDisabled);
	}

	private static string CapitalizeOutcome(string? outcome)
	{
		if (string.IsNullOrWhiteSpace(outcome))
			return "Unknown";

		return char.ToUpperInvariant(outcome![0]) + outcome[1..].ToLowerInvariant();
	}
}
