// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Session/SessionEncounters.cs
// Purpose: Focused session service that mutates SaveGameState via SessionContext.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game.Systems;

namespace WastelandSurvivor.Game.Session;

/// <summary>
/// Encounter lifecycle + persistence.
/// Arena scenes run combat; this service owns the save state and reward application.
/// </summary>
internal sealed class SessionEncounters
{
    private readonly SessionContext _ctx;

    public SessionEncounters(SessionContext ctx)
    {
        _ctx = ctx;
    }

    /// <summary>Best-effort def lookup for validation (services normally take defs as params; the
    /// tier-cap check guards call sites that predate the city catalog and pass none).</summary>
    private static Core.IO.DefDatabase? ResolveDefs()
    {
        try
        {
            return App.Instance != null && App.Instance.Services.TryGet<Core.IO.DefDatabase>(out var defs)
                ? defs
                : null;
        }
        catch
        {
            return null;
        }
    }

    public bool HasActiveEncounter() => _ctx.Save.CurrentEncounter is { Outcome: null };
    public EncounterState? GetCurrentEncounter() => _ctx.Save.CurrentEncounter;

    public int GetActiveVehicleHp()
    {
        var id = _ctx.Save.Player.ActiveVehicleId;
        if (string.IsNullOrWhiteSpace(id)) return 0;
        var v = _ctx.Save.Vehicles.FirstOrDefault(x => x.InstanceId == id);
        return v == null ? 0 : VehicleCombatMath.ComputeVehicleHp(v);
    }

    public bool TryStartArenaEncounter(int tier, out string error)
        => TryStartArenaEncounter(tier, bypassCityCap: false, out error);

    /// <summary>
    /// Starts an arena bracket fight. City arenas only run brackets up to their def's ArenaMaxTier
    /// (spec: city variety); interception fights bypass the cap — they happen at the captor crew's
    /// staging ground, not in the sanctioned arena.
    /// </summary>
    public bool TryStartArenaEncounter(int tier, bool bypassCityCap, out string error)
    {
        error = "";
        if (tier < 1 || tier > 5)
        {
            error = "Tier must be 1..5";
            return false;
        }

        if (!bypassCityCap && ResolveDefs() is { } defs
            && defs.Cities.TryGetValue(_ctx.Save.Player.CurrentCityId ?? "", out var city)
            && tier > city.ArenaMaxTier)
        {
            error = city.ArenaMaxTier <= 0
                ? "This city has no arena."
                : $"This arena only runs brackets up to tier {city.ArenaMaxTier}.";
            return false;
        }

        if (_ctx.Save.CurrentEncounter is { Outcome: null })
        {
            error = "An encounter is already active.";
            return false;
        }

        var activeVehicleId = _ctx.Save.Player.ActiveVehicleId;
        if (string.IsNullOrWhiteSpace(activeVehicleId))
        {
            error = "No active vehicle selected. Go to Garage and set an active vehicle.";
            return false;
        }

        var veh = _ctx.Save.Vehicles.FirstOrDefault(v => v.InstanceId == activeVehicleId);
        if (veh is null)
        {
            error = "Active vehicle not found in save data.";
            return false;
        }

        var enemyHp = tier switch
        {
			1 => Random.Shared.Next(35, 56),
			2 => Random.Shared.Next(55, 76),
			3 => Random.Shared.Next(70, 91),
			4 => Random.Shared.Next(80, 101),
			_ => Random.Shared.Next(90, 111)
        };

        // Kept for save compatibility; some UIs may still show this as a rough starting band.
        var startDistanceBand = tier switch
        {
            1 => Random.Shared.Next(2, 4),
            2 => Random.Shared.Next(3, 5),
            _ => Random.Shared.Next(4, 6),
        };

        var enc = new EncounterState
        {
            EncounterId = Guid.NewGuid().ToString("N"),
            CityId = _ctx.Save.Player.CurrentCityId ?? "detroit",
            Tier = tier,
            VehicleInstanceId = activeVehicleId,
			PlayerHp = Math.Clamp(_ctx.Save.Player.DriverHp, 0, Math.Max(1, _ctx.Save.Player.DriverHpMax)),
            EnemyHp = enemyHp,
            Distance = startDistanceBand,
            Turn = 0,
            StartedUtc = DateTime.UtcNow,
            Outcome = null,
            MoneyRewardUsd = 0,
            ScrapReward = 0,
            AmmoRewards = Array.Empty<RewardAmmoState>(),
            CombatLog = new[] { $"Encounter started (tier {tier})." },
        };

        _ctx.Replace(_ctx.Save with { CurrentEncounter = enc });
        return true;
    }

    public void EndActiveEncounter(string outcome = "fled")
    {
        var enc = _ctx.Save.CurrentEncounter;
        if (enc is null) return;
        if (enc.Outcome is not null) return;

        var resolved = enc with
        {
            EndedUtc = DateTime.UtcNow,
            Outcome = outcome,
        };

        _ctx.Replace(_ctx.Save with { CurrentEncounter = resolved });
        _ctx.Status($"Encounter ended: {resolved.Outcome} (tier {resolved.Tier})");
    }

    public void ClearEncounter()
    {
        if (_ctx.Save.CurrentEncounter is null) return;
        _ctx.Replace(_ctx.Save with { CurrentEncounter = null });
        _ctx.Status("Encounter cleared.");
    }

    /// <summary>
    /// Sets the outcome on the active encounter (only if unresolved).
    /// Optional extraLogLine lets callers avoid a separate AppendEncounterLog() call (single Persist).
    /// </summary>
    public void SetEncounterOutcome(string outcome, string? extraLogLine = null)
    {
        var enc = _ctx.Save.CurrentEncounter;
        if (enc == null) return;
        if (enc.Outcome is not null) return;

        var logArr = enc.CombatLog;
        if (!string.IsNullOrWhiteSpace(extraLogLine))
        {
            var log = logArr?.ToList() ?? new List<string>();
            EncounterLogUtil.AppendAndClamp(log, extraLogLine.Trim());
            logArr = log.ToArray();
        }

        _ctx.Replace(_ctx.Save with
        {
            CurrentEncounter = enc with
            {
                Outcome = outcome,
                EndedUtc = DateTime.UtcNow,
                CombatLog = logArr,
            }
        });
    }

    /// <summary>
    /// Resolves the active encounter as a win, applies rewards to player/vehicle, and persists.
    /// Optional extraLogLine lets callers avoid a separate AppendEncounterLog() call (single Persist).
    /// </summary>
    public void ResolveActiveEncounterWin(int moneyRewardUsd, int scrapReward, string ammoId, int ammoCount, string? extraLogLine = null)
    {
        var enc = _ctx.Save.CurrentEncounter;
        if (enc == null) return;
        if (enc.Outcome is not null) return;

        VehicleInstanceState? activeVeh = null;
        var vehicles = new List<VehicleInstanceState>(_ctx.Save.Vehicles);

        var activeId = _ctx.Save.Player.ActiveVehicleId;
        var activeIdx = -1;
        if (!string.IsNullOrWhiteSpace(activeId))
        {
            activeIdx = vehicles.FindIndex(v => string.Equals(v.InstanceId, activeId, StringComparison.Ordinal));
            if (activeIdx >= 0)
                activeVeh = vehicles[activeIdx];
        }

        if (activeVeh != null && ammoCount > 0)
        {
            activeVeh = AmmoMath.AddAmmo(activeVeh, ammoId, ammoCount);
            vehicles[activeIdx] = activeVeh;
        }

		var playerHp = Math.Clamp(_ctx.Save.Player.DriverHp, 0, Math.Max(1, _ctx.Save.Player.DriverHpMax));

        var logArr = enc.CombatLog;
        if (!string.IsNullOrWhiteSpace(extraLogLine))
        {
            var log = logArr?.ToList() ?? new List<string>();
            EncounterLogUtil.AppendAndClamp(log, extraLogLine.Trim());
            logArr = log.ToArray();
        }

        _ctx.Replace(_ctx.Save with
        {
            Vehicles = vehicles,
            Player = _ctx.Save.Player with
            {
                MoneyUsd = _ctx.Save.Player.MoneyUsd + Math.Max(0, moneyRewardUsd),
                Scrap = _ctx.Save.Player.Scrap + Math.Max(0, scrapReward)
            },
            CurrentEncounter = enc with
            {
                Outcome = "win",
                EndedUtc = DateTime.UtcNow,
                EnemyHp = 0,
                PlayerHp = playerHp,
                MoneyRewardUsd = Math.Max(0, moneyRewardUsd),
                ScrapReward = Math.Max(0, scrapReward),
                AmmoRewards = ammoCount > 0
                    ? new[] { new RewardAmmoState { AmmoId = ammoId, Count = Math.Max(0, ammoCount) } }
                    : Array.Empty<RewardAmmoState>(),
                CombatLog = logArr,
            }
        });
    }

    public bool TryUpdateActiveVehicleTowState(TowingState towingState, out VehicleInstanceState updatedVehicle, out string error)
    {
        updatedVehicle = default!;
        error = "";

        var enc = _ctx.Save.CurrentEncounter;
        if (enc is null || !string.Equals(enc.Outcome, "win", StringComparison.OrdinalIgnoreCase))
        {
            error = "Towing can only be updated during the post-win salvage phase.";
            return false;
        }

        return _ctx.TryMutateActiveVehicleAndPlayer(
            vehicleMutator: veh => veh with { Towing = towingState ?? new TowingState() },
            playerMutator: null,
            out updatedVehicle,
            out error);
    }

    public bool TryAwardBattlefieldScrap(int scrapAmount, string? logLine, out string error)
    {
        error = "";
        var enc = _ctx.Save.CurrentEncounter;
        if (enc is null || !string.Equals(enc.Outcome, "win", StringComparison.OrdinalIgnoreCase))
        {
            error = "Battlefield salvage is only available after winning the encounter.";
            return false;
        }

        var delta = Math.Max(0, scrapAmount);
        if (delta <= 0)
        {
            error = "No salvage value to recover.";
            return false;
        }

        var log = enc.CombatLog?.ToList() ?? new List<string>();
        var line = string.IsNullOrWhiteSpace(logLine)
            ? $"Battlefield salvage recovered: +{delta} scrap."
            : logLine.Trim();
        EncounterLogUtil.AppendAndClamp(log, line);

        _ctx.Replace(_ctx.Save with
        {
            Player = _ctx.Save.Player with { Scrap = _ctx.Save.Player.Scrap + delta },
            CurrentEncounter = enc with
            {
                BattlefieldScrapRecovered = enc.BattlefieldScrapRecovered + delta,
                CombatLog = log.ToArray(),
            }
        });
        return true;
    }


    public bool TryClaimHijackedVehicle(
        VehicleInstanceState salvagedVehicle,
        string displayName,
        out VehicleInstanceState claimedVehicle,
        out string error)
    {
        claimedVehicle = default!;
        error = "";

        var enc = _ctx.Save.CurrentEncounter;
        if (enc is null || !string.Equals(enc.Outcome, "win", StringComparison.OrdinalIgnoreCase))
        {
            error = "Claimed vehicles can only be recovered after winning the encounter.";
            return false;
        }

        if (salvagedVehicle == null || string.IsNullOrWhiteSpace(salvagedVehicle.DefinitionId))
        {
            error = "Claimed vehicle data is invalid.";
            return false;
        }

        if (enc.HijackedVehicleRecovered)
        {
            error = "A claimed vehicle was already recovered from this encounter.";
            return false;
        }

        var vehicles = _ctx.Save.Vehicles.ToList();
        claimedVehicle = salvagedVehicle with
        {
            InstanceId = Guid.NewGuid().ToString("N"),
            Towing = new TowingState(),
        };
        vehicles.Add(claimedVehicle);

        var owned = _ctx.Save.Player.OwnedVehicleIds.ToList();
        owned.Add(claimedVehicle.InstanceId);

        var safeName = string.IsNullOrWhiteSpace(displayName) ? salvagedVehicle.DefinitionId : displayName.Trim();
        var log = enc.CombatLog?.ToList() ?? new List<string>();
        EncounterLogUtil.AppendAndClamp(log, $"Claimed vehicle recovered: {safeName}.");

        _ctx.Replace(_ctx.Save with
        {
            Vehicles = vehicles,
            Player = _ctx.Save.Player with
            {
                OwnedVehicleIds = owned,
                ActiveVehicleId = claimedVehicle.InstanceId,
            },
            CurrentEncounter = enc with
            {
                HijackedVehicleRecovered = true,
                HijackedVehicleDisplayName = safeName,
                CombatLog = log.ToArray(),
            }
        });
        return true;
    }

    public bool TryRecoverTowedVehicle(
        VehicleInstanceState salvagedVehicle,
        string displayName,
        out VehicleInstanceState recoveredVehicle,
        out VehicleInstanceState updatedActiveVehicle,
        out string error)
    {
        recoveredVehicle = default!;
        updatedActiveVehicle = default!;
        error = "";

        var enc = _ctx.Save.CurrentEncounter;
        if (enc is null || !string.Equals(enc.Outcome, "win", StringComparison.OrdinalIgnoreCase))
        {
            error = "Recovered vehicles can only be claimed after winning the encounter.";
            return false;
        }

        if (salvagedVehicle == null || string.IsNullOrWhiteSpace(salvagedVehicle.DefinitionId))
        {
            error = "Recovered vehicle data is invalid.";
            return false;
        }

        var activeId = _ctx.Save.Player.ActiveVehicleId;
        if (string.IsNullOrWhiteSpace(activeId))
        {
            error = "No active vehicle selected.";
            return false;
        }

        var vehicles = _ctx.Save.Vehicles.ToList();
        var activeIdx = vehicles.FindIndex(v => string.Equals(v.InstanceId, activeId, StringComparison.Ordinal));
        if (activeIdx < 0)
        {
            error = "Active vehicle missing.";
            return false;
        }

        updatedActiveVehicle = vehicles[activeIdx] with { Towing = new TowingState() };
        vehicles[activeIdx] = updatedActiveVehicle;

        recoveredVehicle = salvagedVehicle with
        {
            InstanceId = Guid.NewGuid().ToString("N"),
            Towing = new TowingState(),
        };
        vehicles.Add(recoveredVehicle);

        var owned = _ctx.Save.Player.OwnedVehicleIds.ToList();
        owned.Add(recoveredVehicle.InstanceId);

        var log = enc.CombatLog?.ToList() ?? new List<string>();
        var safeName = string.IsNullOrWhiteSpace(displayName) ? salvagedVehicle.DefinitionId : displayName.Trim();
        EncounterLogUtil.AppendAndClamp(log, $"Recovered towed vehicle: {safeName}.");

        _ctx.Replace(_ctx.Save with
        {
            Vehicles = vehicles,
            Player = _ctx.Save.Player with { OwnedVehicleIds = owned },
            CurrentEncounter = enc with
            {
                TowedVehicleRecovered = true,
                TowedVehicleDisplayName = safeName,
                CombatLog = log.ToArray(),
            }
        });
        return true;
    }

    /// <summary>
    /// Resolve the currently active arena encounter using real-time combat results.
    /// This is used by the real-time arena prototype.
    /// </summary>
    public bool ResolveArenaEncounterRealtime(
        string outcome,
        VehicleInstanceState finalPlayerVehicle,
        int enemyHpAfter,
		int driverArmorAfter,
		int driverHpAfter,
        string[] runtimeLog,
        out VehicleInstanceState updatedPlayerVehicle,
        out string error)
    {
        error = "";
        updatedPlayerVehicle = finalPlayerVehicle;

        var enc = _ctx.Save.CurrentEncounter;
        if (enc is null)
        {
            error = "No active encounter.";
            return false;
        }
        if (enc.Outcome is not null)
        {
            error = "Encounter is already resolved.";
            return false;
        }

        var norm = (outcome ?? "").Trim().ToLowerInvariant();
        if (norm is not ("win" or "lose" or "fled"))
        {
            error = "Invalid outcome. Expected 'win', 'lose', or 'fled'.";
            return false;
        }

        if (!string.Equals(finalPlayerVehicle.InstanceId, enc.VehicleInstanceId, StringComparison.Ordinal))
        {
            error = "Vehicle mismatch for active encounter.";
            return false;
        }

        // Clamp the runtime log so saves stay small.
        var log = new List<string>();
        if (runtimeLog is { Length: > 0 })
        {
            foreach (var l in runtimeLog)
            {
                if (string.IsNullOrWhiteSpace(l)) continue;
                log.Add(l.Trim());
            }
        }
        if (log.Count == 0)
            log.Add("Encounter resolved.");
        const int maxLines = 30;
        EncounterLogUtil.Clamp(log, maxLines);

        var vehicles = _ctx.Save.Vehicles.ToList();
        var vidx = vehicles.FindIndex(v => v.InstanceId == finalPlayerVehicle.InstanceId);
        if (vidx < 0)
        {
            error = "Vehicle not found.";
            return false;
        }

			var playerState = _ctx.Save.Player;
			var maxHp = playerState.DriverHpMax;
			if (maxHp <= 0) maxHp = GameBalance.DefaultDriverHpMax;
			var clampedHp = Math.Clamp(driverHpAfter, 0, maxHp);
			// Prototype safeguard: don't soft-lock the player after a loss.
			// A loss represents being recovered/towed; return to the next fight with full driver HP.
			if (norm == "lose") clampedHp = maxHp;
			var survivedEncounter = norm != "lose" && clampedHp > 0;

        var resolvedEnc = enc with
        {
            Outcome = norm,
            EndedUtc = DateTime.UtcNow,
			PlayerHp = clampedHp,
            EnemyHp = Math.Max(0, enemyHpAfter),
            CombatLog = log.ToArray(),
        };

		// Always persist the player's runtime vehicle state.
		vehicles[vidx] = finalPlayerVehicle;

		// Persist driver HP + armor (extra HP buffer). This is separate from vehicle armor/tires.
		var maxArmor = playerState.DriverArmorMax;
		if (maxArmor <= 0) maxArmor = GameBalance.DefaultDriverArmorMax;
		var clampedArmor = Math.Clamp(driverArmorAfter, 0, maxArmor);
		if (norm == "lose")
			clampedArmor = 0;
		else if (survivedEncounter)
		{
			clampedHp = maxHp;
			clampedArmor = maxArmor;
		}

		playerState = playerState with
		{
			DriverHpMax = maxHp,
			DriverHp = clampedHp,
			DriverArmorMax = maxArmor,
			DriverArmor = clampedArmor,
		};

		// Clone respawn (master spec): a killed driver wakes as a fresh clone at the last upload
		// facility. Money and garaged vehicles persist, but decanting a clone isn't free.
		var capturedVehicles = _ctx.Save.CapturedVehicles.ToList();
		if (norm == "lose")
		{
			var fee = Math.Min(GameBalance.CloneRespawnFeeUsd, Math.Max(0, playerState.MoneyUsd));
			var respawnCity = string.IsNullOrWhiteSpace(playerState.LastRespawnCityId)
				? playerState.CurrentCityId
				: playerState.LastRespawnCityId;
			playerState = playerState with
			{
				MoneyUsd = playerState.MoneyUsd - fee,
				LastRespawnCityId = respawnCity,
				// The clone wakes where its memory was uploaded, not where the body dropped.
				CurrentCityId = respawnCity,
			};
			log.Add(fee > 0
				? $"Driver killed. Clone decanted at the {respawnCity} facility (-${fee} clone fee). Memory intact."
				: $"Driver killed. Clone decanted at the {respawnCity} facility on credit. Memory intact.");

			// The victors salvage the loser's machine (spec: AI crews tow wrecks like players do).
			// Ownership is suspended until the player pays the ransom or wins an interception.
			if (playerState.OwnedVehicleIds.Contains(finalPlayerVehicle.InstanceId))
			{
				var owned = playerState.OwnedVehicleIds
					.Where(id => !string.Equals(id, finalPlayerVehicle.InstanceId, StringComparison.Ordinal))
					.ToList();
				var nextActive = string.Equals(playerState.ActiveVehicleId, finalPlayerVehicle.InstanceId, StringComparison.Ordinal)
					? owned.FirstOrDefault()
					: playerState.ActiveVehicleId;
				playerState = playerState with
				{
					OwnedVehicleIds = owned,
					ActiveVehicleId = nextActive,
				};

				// Ransom scales with what the machine is actually worth (a captured war rig costs
				// real money to buy back; a scrap compact doesn't), with a captor-tier markup.
				var ransom = GameBalance.GetVehicleRansomUsd(enc.Tier);
				if (ResolveDefs() is { } ransomDefs
					&& ransomDefs.Vehicles.TryGetValue(finalPlayerVehicle.DefinitionId, out var capturedDef))
				{
					var value = VehicleRecoveryValueMath.ComputeSellValueUsd(capturedDef, finalPlayerVehicle, ransomDefs);
					ransom = GameBalance.GetVehicleRansomUsd(enc.Tier, value);
				}

				capturedVehicles.Add(new CapturedVehicleState
				{
					VehicleInstanceId = finalPlayerVehicle.InstanceId,
					CityId = enc.CityId,
					CaptorTier = Math.Clamp(enc.Tier, 1, 5),
					RansomUsd = ransom,
					CapturedUtc = DateTime.UtcNow,
				});
				log.Add($"Your vehicle was towed off the arena floor by the victors. Word is it's held in {enc.CityId} — pay the ransom or take it back by force.");
			}

			EncounterLogUtil.Clamp(log, maxLines);
			resolvedEnc = resolvedEnc with { CombatLog = log.ToArray() };
		}

		// Interception win: the reclaimed vehicle rejoins the fleet (spec: reclaim after respawn).
		if (norm == "win" && !string.IsNullOrWhiteSpace(enc.RecoverVehicleInstanceId))
		{
			var capIdx = capturedVehicles.FindIndex(c => c.VehicleInstanceId == enc.RecoverVehicleInstanceId);
			if (capIdx >= 0)
			{
				capturedVehicles.RemoveAt(capIdx);
				if (!playerState.OwnedVehicleIds.Contains(enc.RecoverVehicleInstanceId!))
				{
					var owned = playerState.OwnedVehicleIds.ToList();
					owned.Add(enc.RecoverVehicleInstanceId!);
					playerState = playerState with { OwnedVehicleIds = owned };
				}
				log.Add("Interception successful — your vehicle is back in the fleet (bring it in for repairs).");
				EncounterLogUtil.Clamp(log, maxLines);
				resolvedEnc = resolvedEnc with { CombatLog = log.ToArray() };
			}
		}

		resolvedEnc = resolvedEnc with { PlayerHp = clampedHp };


        var tournament = _ctx.Save.ActiveTournament;
        var activeBounty = _ctx.Save.ActiveBountyContract;

        if (norm == "win")
        {
            var rewards = EncounterRewardGenerator.RollArenaWinRewards(enc.Tier);
            // Road ambushes are unsanctioned highway fights: nobody hands the winner an arena
            // purse. 45% money, no ammo payout — wreck scrap stays the real upside, which keeps
            // the freight-hauler ambush bonus a genuine risk instead of an income multiplier
            // (gameplay judge round 6 P1: loaded haulers were FARMING their own ambushes).
            // Bounty hunts are highway fights too: same unsanctioned-purse rules as ambushes —
            // the contract reward (added below) is the sanctioned payday.
            var highwayFight = enc.IsRoadAmbush || enc.IsBountyHunt;
            var moneyReward = highwayFight ? (int)Math.Round(rewards.MoneyUsd * 0.45) : rewards.MoneyUsd;
            // Reward ammo the player can actually fire (the old fixed 9mm payout was dead weight
            // for every default build, which mounts .50cal). Non-ballistic payouts are capped —
            // 60 free missiles is a different game.
            var (rewardAmmoId, rewardBallistic) = ResolveWinRewardAmmo(finalPlayerVehicle);
            var rewardAmmoCount = highwayFight
                ? 0
                : rewardBallistic ? rewards.Ammo : Math.Min(4, Math.Max(1, rewards.Ammo / 10));
            var rewardedVehicle = rewardAmmoCount > 0
                ? AmmoMath.AddAmmo(finalPlayerVehicle, rewardAmmoId, rewardAmmoCount)
                : finalPlayerVehicle;
            vehicles[vidx] = rewardedVehicle;
            updatedPlayerVehicle = rewardedVehicle;

			playerState = playerState with
			{
				MoneyUsd = playerState.MoneyUsd + moneyReward,
				Scrap = playerState.Scrap + rewards.Scrap,
			};

            log.Add(enc.IsRoadAmbush
                ? $"Raiders driven off. Their stash pays ${moneyReward} and {rewards.Scrap} scrap — strip the wreck for the real haul."
                : $"Encounter resolved: win. +${moneyReward}, +{rewards.Scrap} scrap, +{rewardAmmoCount} ammo.");

            // Bounty claimed: the contract reward lands on top of the highway spoils, the head
            // count rerolls every WANTED board, and the contract clears atomically with the win.
            if (enc.IsBountyHunt && activeBounty != null)
            {
                playerState = playerState with
                {
                    MoneyUsd = playerState.MoneyUsd + activeBounty.RewardUsd,
                    BountiesClaimed = playerState.BountiesClaimed + 1,
                };
                log.Add($"BOUNTY CLAIMED — {activeBounty.TargetName} is finished. The contract pays ${activeBounty.RewardUsd}.");
                activeBounty = null;
            }

			// Tournament round win: advance the bracket, or crown a champion on the final round.
			if (enc.IsTournamentRound && tournament != null)
			{
				var wonRound = Math.Clamp(enc.TournamentRoundIndex, 0, tournament.RoundTiers.Length - 1);
				var isFinal = wonRound >= tournament.RoundTiers.Length - 1;
				tournament = tournament with
				{
					WinningsUsd = tournament.WinningsUsd + rewards.MoneyUsd,
					CurrentRound = wonRound + 1,
				};
				if (isFinal)
				{
					var maxTier = tournament.RoundTiers[^1];
					var bonus = GameBalance.GetTournamentChampionBonusUsd(maxTier);
					var salvageRights = GameBalance.GetTournamentChampionScrapBonus(maxTier);
					playerState = playerState with
					{
						MoneyUsd = playerState.MoneyUsd + bonus,
						Scrap = playerState.Scrap + salvageRights,
					};
					log.Add($"TOURNAMENT CHAMPION! The crowd roars — champion bonus +${bonus} on top of ${tournament.WinningsUsd} in round purses, plus salvage rights to the bracket's wrecks (+{salvageRights} scrap).");
					tournament = null;
				}
				else
				{
					log.Add($"Round {wonRound + 1} won — advance to round {wonRound + 2} of {tournament.RoundTiers.Length}.");
				}
			}

            EncounterLogUtil.Clamp(log, maxLines);

            resolvedEnc = resolvedEnc with
            {
                EnemyHp = 0,
                MoneyRewardUsd = moneyReward,
                ScrapReward = rewards.Scrap,
                AmmoRewards = rewardAmmoCount > 0
                    ? new[] { new RewardAmmoState { AmmoId = rewardAmmoId, Count = rewardAmmoCount } }
                    : Array.Empty<RewardAmmoState>(),
                CombatLog = log.ToArray(),
            };
        }
		else if (tournament != null && (enc.IsTournamentRound || norm == "lose"))
		{
			// A lost/fled round eliminates the player (purses earned are kept). Dying anywhere
			// mid-bracket — e.g. a road ambush between rounds — also forfeits the run.
			log.Add(enc.IsTournamentRound
				? $"Eliminated from the tournament in round {Math.Clamp(enc.TournamentRoundIndex, 0, tournament.RoundTiers.Length - 1) + 1}. Winnings kept: ${tournament.WinningsUsd}."
				: $"Your tournament run in {tournament.CityId} is forfeit — the clone wakes far from the bracket.");
			EncounterLogUtil.Clamp(log, maxLines);
			resolvedEnc = resolvedEnc with { CombatLog = log.ToArray() };
			tournament = null;
		}

        _ctx.Replace(_ctx.Save with
        {
            Vehicles = vehicles,
            Player = playerState,
            CurrentEncounter = resolvedEnc,
            CapturedVehicles = capturedVehicles,
            ActiveTournament = tournament,
            ActiveBountyContract = activeBounty,
        });
        return true;
    }

	/// <summary>
	/// Reward ammo the player can actually fire: prefer the first installed weapon whose selected
	/// ammo is Ballistic; else the first installed weapon's ammo (capped by the caller); else the
	/// legacy primary. Returns (ammoId, isBallistic).
	/// </summary>
	private (string ammoId, bool isBallistic) ResolveWinRewardAmmo(VehicleInstanceState veh)
	{
		var defs = ResolveDefs();
		string? firstAny = null;
		if (veh.InstalledWeaponsByMountId is { Count: > 0 })
		{
			foreach (var kv in veh.InstalledWeaponsByMountId.OrderBy(k => k.Key, StringComparer.Ordinal))
			{
				var ammoId = kv.Value?.SelectedAmmoId;
				if (string.IsNullOrWhiteSpace(ammoId)) continue;
				firstAny ??= ammoId;
				if (defs != null && defs.Ammo.TryGetValue(ammoId!, out var adef)
					&& adef.AmmoKind == Core.Defs.AmmoKind.Ballistic)
					return (ammoId!, true);
			}
		}
		if (firstAny == null)
			return (GameBalance.PrimaryAmmoId, true);
		var ballistic = defs != null && defs.Ammo.TryGetValue(firstAny, out var fdef)
			&& fdef.AmmoKind == Core.Defs.AmmoKind.Ballistic;
		return (firstAny, ballistic);
	}

	/// <summary>
	/// Start an overworld scavenge-site encounter (spec: side roads lead to abandoned structures
	/// containing salvage). Created PRE-RESOLVED as a zero-reward "win" so the arena's existing
	/// post-win salvage phase (strip / tow / hijack, out the gate) drives the whole interaction —
	/// there is no fight and no purse; the salvage IS the reward.
	/// </summary>
	public bool TryStartScavengeEncounter(out string error)
	{
		error = "";
		if (_ctx.Save.CurrentEncounter is { Outcome: null })
		{
			error = "An encounter is already active.";
			return false;
		}

		var activeVehicleId = _ctx.Save.Player.ActiveVehicleId;
		if (string.IsNullOrWhiteSpace(activeVehicleId)
			|| _ctx.Save.Vehicles.All(v => v.InstanceId != activeVehicleId))
		{
			error = "No active vehicle to scavenge with.";
			return false;
		}

		var flags = new Dictionary<string, bool>(_ctx.Save.WorldFlags) { ["scavenge_pending"] = true };
		_ctx.Replace(_ctx.Save with
		{
			WorldFlags = flags,
			CurrentEncounter = new EncounterState
			{
				EncounterId = Guid.NewGuid().ToString("N"),
				CityId = _ctx.Save.Player.CurrentCityId ?? "detroit",
				Tier = Random.Shared.Next(1, 3),
				VehicleInstanceId = activeVehicleId!,
				PlayerHp = Math.Clamp(_ctx.Save.Player.DriverHp, 0, Math.Max(1, _ctx.Save.Player.DriverHpMax)),
				EnemyHp = 0,
				Outcome = "win",
				EndedUtc = DateTime.UtcNow,
				IsScavengeSite = true,
				CombatLog = new[] { "Scavenge site: a collapsed depot off the highway — derelict metal everywhere." },
			},
		});
		_ctx.Status("Scavenge site: strip the derelict or drag it home.");
		return true;
	}

	/// <summary>
	/// Start a fight against a rival salvage crew hauling a prize wreck (spec: AI crews roam and
	/// salvage like players). A REAL encounter — lose and the usual capture stakes apply; win and
	/// the crew's haul converts to scrap on the spot.
	/// </summary>
	public bool TryStartSalvageCrewRaid(out string error)
	{
		var tier = Random.Shared.Next(1, 4);
		if (!TryStartArenaEncounter(tier, bypassCityCap: true, out error))
			return false;

		var enc = _ctx.Save.CurrentEncounter;
		if (enc is null)
		{
			error = "Encounter failed to start.";
			return false;
		}

		var log = enc.CombatLog.ToList();
		EncounterLogUtil.AppendAndClamp(log, "Rival salvage crew engaged — their haul is on the line behind them.");
		_ctx.Replace(_ctx.Save with
		{
			CurrentEncounter = enc with { IsSalvageCrewRaid = true, CombatLog = log.ToArray() }
		});
		return true;
	}

	/// <summary>
	/// Start the active bounty's forced fight (spec: expandable quest systems — named raiders haunt
	/// road legs). A REAL encounter at the bounty's tier — lose and the usual capture stakes apply,
	/// and the target stays at large; win and the head pays out on top of the highway spoils.
	/// </summary>
	public bool TryStartBountyHunt(out string error)
	{
		var bounty = _ctx.Save.ActiveBountyContract;
		if (bounty == null)
		{
			error = "No active bounty contract.";
			return false;
		}

		var tier = Math.Clamp(bounty.Tier, 1, 5);
		if (!TryStartArenaEncounter(tier, bypassCityCap: true, out error))
			return false;

		var enc = _ctx.Save.CurrentEncounter;
		if (enc is null)
		{
			error = "Encounter failed to start.";
			return false;
		}

		var log = enc.CombatLog.ToList();
		EncounterLogUtil.AppendAndClamp(log, $"BOUNTY HUNT — {bounty.TargetName} takes the field. The head is worth ${bounty.RewardUsd}.");
		_ctx.Replace(_ctx.Save with
		{
			CurrentEncounter = enc with
			{
				IsBountyHunt = true,
				BountyTargetName = bounty.TargetName,
				BountyRewardUsd = bounty.RewardUsd,
				CombatLog = log.ToArray(),
			}
		});
		return true;
	}

	/// <summary>Consume a one-shot world flag (returns true exactly once per set).</summary>
	public bool TryConsumeWorldFlag(string flag)
	{
		if (!_ctx.Save.WorldFlags.TryGetValue(flag, out var v) || !v)
			return false;
		var flags = new Dictionary<string, bool>(_ctx.Save.WorldFlags);
		flags.Remove(flag);
		_ctx.Replace(_ctx.Save with { WorldFlags = flags });
		return true;
	}

	// --- Arena tournaments (spec: arenas host AutoDuel-style tournaments) ---

	public TournamentState? GetActiveTournament() => _ctx.Save.ActiveTournament;

	/// <summary>
	/// Pay the entry fee and start round 1 of a tournament in the current city's arena.
	/// The gauntlet runs [maxTier-2, maxTier-1, maxTier] (clamped to 1) back-to-back.
	/// </summary>
	public bool TryEnterTournament(out string error)
	{
		error = "";
		if (_ctx.Save.ActiveTournament != null)
		{
			error = "A tournament is already underway.";
			return false;
		}
		if (_ctx.Save.CurrentEncounter is { Outcome: null })
		{
			error = "An encounter is already active.";
			return false;
		}

		var cityId = _ctx.Save.Player.CurrentCityId ?? "";
		var maxTier = 0;
		if (ResolveDefs() is { } defs && defs.Cities.TryGetValue(cityId, out var city))
			maxTier = Math.Clamp(city.ArenaMaxTier, 0, 5);
		if (maxTier <= 0)
		{
			error = "This city has no arena — no tournament circuit here.";
			return false;
		}

		var fee = GameBalance.GetTournamentEntryFeeUsd(maxTier);
		if (_ctx.Save.Player.MoneyUsd < fee)
		{
			error = $"Tournament entry costs ${fee} — not enough cash.";
			return false;
		}

		var rounds = new int[GameBalance.TournamentRounds];
		for (var i = 0; i < rounds.Length; i++)
			rounds[i] = Math.Max(1, maxTier - (rounds.Length - 1 - i));

		// Charge entry before the round-1 encounter is created so a start failure refunds cleanly.
		_ctx.Replace(_ctx.Save with
		{
			Player = _ctx.Save.Player with { MoneyUsd = _ctx.Save.Player.MoneyUsd - fee },
			ActiveTournament = new TournamentState
			{
				CityId = cityId,
				RoundTiers = rounds,
				CurrentRound = 0,
				EntryFeeUsd = fee,
			},
		});

		if (!TryStartTournamentRound(0, out error))
		{
			// Refund + roll back the tournament shell.
			_ctx.Replace(_ctx.Save with
			{
				Player = _ctx.Save.Player with { MoneyUsd = _ctx.Save.Player.MoneyUsd + fee },
				ActiveTournament = null,
			});
			return false;
		}

		_ctx.Status($"Tournament entered (-${fee}). Round 1 of {rounds.Length}: tier {rounds[0]}.");
		return true;
	}

	/// <summary>
	/// After a round win is resolved, patch the player's vehicle (pit crew) and start the next round.
	/// </summary>
	public bool TryStartNextTournamentRound(out string error)
	{
		error = "";
		var t = _ctx.Save.ActiveTournament;
		if (t == null)
		{
			error = "No tournament is underway.";
			return false;
		}
		if (_ctx.Save.CurrentEncounter is { Outcome: null })
		{
			error = "Finish the current fight first.";
			return false;
		}
		if (t.CurrentRound >= t.RoundTiers.Length)
		{
			error = "The tournament is already decided.";
			return false;
		}
		if (!string.Equals(_ctx.Save.Player.CurrentCityId, t.CityId, StringComparison.OrdinalIgnoreCase))
		{
			error = $"The bracket runs in {t.CityId} — travel back to continue.";
			return false;
		}

		ApplyPitCrewPatch();
		var restockNote = ApplyPitCrewAmmoRestock();

		if (!TryStartTournamentRound(t.CurrentRound, out error))
			return false;

		_ctx.Status($"Tournament round {t.CurrentRound + 1} of {t.RoundTiers.Length}: tier {t.RoundTiers[t.CurrentRound]}. Pit crew patched what they could.{restockNote}");
		return true;
	}

	/// <summary>
	/// Between rounds the pit crew also sells ammo at track prices (a markup over the workshop) —
	/// as much of the refill target as the wallet affords. Without this, a single fight drained a
	/// full ammo hold and rounds 2-3 were fought dry, making every tournament a strictly losing
	/// proposition regardless of the purse math.
	/// </summary>
	private string ApplyPitCrewAmmoRestock()
	{
		var activeId = _ctx.Save.Player.ActiveVehicleId;
		if (string.IsNullOrWhiteSpace(activeId)) return "";
		var vehicles = _ctx.Save.Vehicles.ToList();
		var idx = vehicles.FindIndex(v => v.InstanceId == activeId);
		if (idx < 0) return "";

		var defs = ResolveDefs();
		if (defs == null) return "";

		var veh = vehicles[idx];
		var money = _ctx.Save.Player.MoneyUsd;
		var spent = 0;
		var bought = 0;
		var inv = new Dictionary<string, int>(veh.AmmoInventory ?? new Dictionary<string, int>());

		foreach (var kv in (veh.InstalledWeaponsByMountId ?? new Dictionary<string, InstalledWeaponState>()).OrderBy(k => k.Key, StringComparer.Ordinal))
		{
			var ammoId = kv.Value?.SelectedAmmoId;
			if (string.IsNullOrWhiteSpace(ammoId) || !defs.Ammo.TryGetValue(ammoId!, out var adef))
				continue;

			var (target, _) = GameBalance.GetAmmoRefillPolicy(adef.AmmoKind);
			// Same per-def price resolution the Workshop sells at (def UnitPriceUsd → legacy override
			// map → kind baseline) — the restock used to charge the KIND baseline, so specialty AP
			// rounds restocked at $3 while the workshop sold them at $4 ("track markup" backwards).
			var unitCost = GameBalance.GetAmmoUnitPriceUsd(adef, ammoId!, adef.AmmoKind);
			var markedUp = Math.Max(1, (int)MathF.Round(unitCost * GameBalance.TournamentPitAmmoMarkup));
			inv.TryGetValue(ammoId!, out var current);
			var need = Math.Max(0, target - current);
			if (need <= 0) continue;

			var affordable = Math.Min(need, (money - spent) / markedUp);
			if (affordable <= 0) continue;

			inv[ammoId!] = current + affordable;
			spent += affordable * markedUp;
			bought += affordable;
		}

		if (bought <= 0)
			return "";

		vehicles[idx] = veh with { AmmoInventory = inv };
		_ctx.Replace(_ctx.Save with
		{
			Vehicles = vehicles,
			Player = _ctx.Save.Player with { MoneyUsd = _ctx.Save.Player.MoneyUsd - spent },
		});
		return $" Restocked {bought} rounds at track prices (-${spent}).";
	}

	/// <summary>Withdraw between rounds (keep winnings, forfeit the run) or clear a stale tournament.</summary>
	public void AbandonTournament(string? reason = null)
	{
		if (_ctx.Save.ActiveTournament == null) return;
		_ctx.Replace(_ctx.Save with { ActiveTournament = null });
		_ctx.Status(string.IsNullOrWhiteSpace(reason)
			? "Withdrew from the tournament."
			: reason);
	}

	private bool TryStartTournamentRound(int roundIndex, out string error)
	{
		var t = _ctx.Save.ActiveTournament;
		if (t == null || roundIndex < 0 || roundIndex >= t.RoundTiers.Length)
		{
			error = "Tournament round out of range.";
			return false;
		}

		// Tournament rounds never exceed the city's own cap by construction; bypass anyway so a
		// mid-tournament city-def edit can't strand the save.
		if (!TryStartArenaEncounter(t.RoundTiers[roundIndex], bypassCityCap: true, out error))
			return false;

		var enc = _ctx.Save.CurrentEncounter;
		if (enc == null)
		{
			error = "Encounter failed to start.";
			return false;
		}

		var log = enc.CombatLog.ToList();
		EncounterLogUtil.AppendAndClamp(log, $"Tournament round {roundIndex + 1} of {t.RoundTiers.Length} — win to advance.");
		_ctx.Replace(_ctx.Save with
		{
			CurrentEncounter = enc with
			{
				IsTournamentRound = true,
				TournamentRoundIndex = roundIndex,
				CombatLog = log.ToArray(),
			}
		});
		return true;
	}

	/// <summary>
	/// Highway raider ambush (rolled by travel): a real arena fight, tagged so resolution pays the
	/// cut-down roadside reward instead of a sanctioned arena purse.
	/// </summary>
	public bool TryStartRoadAmbushEncounter(int tier, out string error)
	{
		if (!TryStartArenaEncounter(tier, bypassCityCap: true, out error))
			return false;

		var enc = _ctx.Save.CurrentEncounter;
		if (enc == null)
		{
			error = "Encounter failed to start.";
			return false;
		}
		_ctx.Replace(_ctx.Save with { CurrentEncounter = enc with { IsRoadAmbush = true } });
		return true;
	}

	/// <summary>Between rounds the pit crew restores a fraction of missing armor/tire points — free,
	/// but far from a full garage repair, so attrition across the gauntlet is the tournament's cost.</summary>
	private void ApplyPitCrewPatch()
	{
		var activeId = _ctx.Save.Player.ActiveVehicleId;
		if (string.IsNullOrWhiteSpace(activeId)) return;
		var vehicles = _ctx.Save.Vehicles.ToList();
		var idx = vehicles.FindIndex(v => v.InstanceId == activeId);
		if (idx < 0) return;

		var defs = ResolveDefs();
		if (defs == null || !defs.Vehicles.TryGetValue(vehicles[idx].DefinitionId, out var vdef))
			return;

		var v = VehicleCombatMath.EnsureDamageState(vehicles[idx], vdef);
		var frac = Math.Clamp(GameBalance.TournamentPitCrewRepairFraction, 0f, 1f);

		static int Patch(int cur, int max, float f)
		{
			var missing = Math.Max(0, max - cur);
			return missing <= 0 ? cur : Math.Min(max, cur + (int)Math.Floor(missing * f));
		}

		var armor = new Dictionary<Core.Defs.ArmorSection, int>(v.CurrentArmorBySection);
		foreach (var kv in v.CurrentArmorBySection)
		{
			vdef.BaseArmorBySection.TryGetValue(kv.Key, out var baseMax);
			armor[kv.Key] = Patch(kv.Value, baseMax + VehicleMassMath.GetPlatingArmorBonus(v.ArmorPlatingLevel), frac);
		}

		var hp = new Dictionary<Core.Defs.ArmorSection, int>(v.CurrentHpBySection);
		foreach (var kv in v.CurrentHpBySection)
		{
			vdef.BaseHpBySection.TryGetValue(kv.Key, out var hpMax);
			hp[kv.Key] = Patch(kv.Value, hpMax, frac);
		}

		var tireArmorMax = vdef.BaseTireArmor + VehicleMassMath.GetPlatingArmorBonus(v.TirePlatingLevel);
		var tireArmor = (int[])v.CurrentTireArmor.Clone();
		for (var i = 0; i < tireArmor.Length; i++)
			tireArmor[i] = Patch(tireArmor[i], tireArmorMax, frac);

		var tireHp = (int[])v.CurrentTireHp.Clone();
		for (var i = 0; i < tireHp.Length; i++)
			tireHp[i] = Patch(tireHp[i], vdef.BaseTireHp, frac);

		vehicles[idx] = v with
		{
			CurrentArmorBySection = armor,
			CurrentHpBySection = hp,
			CurrentTireArmor = tireArmor,
			CurrentTireHp = tireHp,
		};
		_ctx.Replace(_ctx.Save with { Vehicles = vehicles });
	}

	// --- Captured-vehicle recovery (spec: intercept and reclaim salvaged vehicles) ---

	public IReadOnlyList<CapturedVehicleState> GetCapturedVehicles() => _ctx.Save.CapturedVehicles;

	/// <summary>Buy a captured vehicle back from its captors without a fight.</summary>
	public bool TryPayVehicleRansom(string vehicleInstanceId, out string error)
	{
		error = "";
		var captured = _ctx.Save.CapturedVehicles.ToList();
		var idx = captured.FindIndex(c => c.VehicleInstanceId == vehicleInstanceId);
		if (idx < 0)
		{
			error = "That vehicle isn't held by anyone.";
			return false;
		}

		var entry = captured[idx];
		var player = _ctx.Save.Player;
		if (player.MoneyUsd < entry.RansomUsd)
		{
			error = $"The captors want ${entry.RansomUsd} — not enough cash.";
			return false;
		}

		captured.RemoveAt(idx);
		var owned = player.OwnedVehicleIds.ToList();
		if (!owned.Contains(vehicleInstanceId))
			owned.Add(vehicleInstanceId);

		_ctx.Replace(_ctx.Save with
		{
			CapturedVehicles = captured,
			Player = player with
			{
				MoneyUsd = player.MoneyUsd - entry.RansomUsd,
				OwnedVehicleIds = owned,
				ActiveVehicleId = string.IsNullOrWhiteSpace(player.ActiveVehicleId) ? vehicleInstanceId : player.ActiveVehicleId,
			}
		});
		_ctx.Status($"Ransom paid (-${entry.RansomUsd}). Vehicle returned to your garage.");
		return true;
	}

	/// <summary>
	/// Start an interception fight against the crew holding a captured vehicle. Requires being in
	/// the city where it's held and having an active vehicle to fight with. Winning returns it.
	/// </summary>
	public bool TryStartInterceptionEncounter(string vehicleInstanceId, out string error)
	{
		error = "";
		var entry = _ctx.Save.CapturedVehicles.FirstOrDefault(c => c.VehicleInstanceId == vehicleInstanceId);
		if (entry is null)
		{
			error = "That vehicle isn't held by anyone.";
			return false;
		}

		if (!string.Equals(_ctx.Save.Player.CurrentCityId, entry.CityId, StringComparison.OrdinalIgnoreCase))
		{
			error = $"The captors hole up in {entry.CityId} — travel there to intercept.";
			return false;
		}

		if (!TryStartArenaEncounter(Math.Clamp(entry.CaptorTier, 1, 5), bypassCityCap: true, out error))
			return false;

		var enc = _ctx.Save.CurrentEncounter;
		if (enc is null)
		{
			error = "Encounter failed to start.";
			return false;
		}

		var log = enc.CombatLog.ToList();
		log.Add("Interception: win this fight to reclaim your captured vehicle.");
		_ctx.Replace(_ctx.Save with
		{
			CurrentEncounter = enc with
			{
				RecoverVehicleInstanceId = vehicleInstanceId,
				CombatLog = log.ToArray(),
			}
		});
		return true;
	}
}
