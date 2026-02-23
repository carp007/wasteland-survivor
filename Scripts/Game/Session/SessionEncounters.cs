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
    {
        error = "";
        if (tier < 1 || tier > 3)
        {
            error = "Tier must be 1..3";
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
			2 => Random.Shared.Next(50, 71),
			_ => Random.Shared.Next(65, 86)
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
            CityId = _ctx.Save.Player.CurrentCityId,
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
		playerState = playerState with { DriverHpMax = maxHp, DriverHp = clampedHp };

		var maxArmor = playerState.DriverArmorMax;
		if (maxArmor <= 0) maxArmor = GameBalance.DefaultDriverArmorMax;
		var clampedArmor = Math.Clamp(driverArmorAfter, 0, maxArmor);
		if (norm == "lose") clampedArmor = 0;
		playerState = playerState with { DriverArmorMax = maxArmor, DriverArmor = clampedArmor };

        if (norm == "win")
        {
            var rewards = EncounterRewardGenerator.RollArenaWinRewards(enc.Tier);
            var rewardedVehicle = AmmoMath.AddAmmo(finalPlayerVehicle, GameBalance.PrimaryAmmoId, rewards.Ammo);
            vehicles[vidx] = rewardedVehicle;
            updatedPlayerVehicle = rewardedVehicle;

			playerState = playerState with
			{
				MoneyUsd = playerState.MoneyUsd + rewards.MoneyUsd,
				Scrap = playerState.Scrap + rewards.Scrap,
			};

            log.Add($"Encounter resolved: win. +${rewards.MoneyUsd}, +{rewards.Scrap} scrap, +{rewards.Ammo} ammo.");
            EncounterLogUtil.Clamp(log, maxLines);

            resolvedEnc = resolvedEnc with
            {
                EnemyHp = 0,
                MoneyRewardUsd = rewards.MoneyUsd,
                ScrapReward = rewards.Scrap,
                AmmoRewards = new[] { new RewardAmmoState { AmmoId = GameBalance.PrimaryAmmoId, Count = rewards.Ammo } },
                CombatLog = log.ToArray(),
            };
        }

        _ctx.Replace(_ctx.Save with
        {
            Vehicles = vehicles,
            Player = playerState,
            CurrentEncounter = resolvedEnc,
        });
        return true;
    }
}
