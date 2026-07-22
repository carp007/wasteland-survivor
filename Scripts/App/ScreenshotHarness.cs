// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/App/ScreenshotHarness.cs
// Purpose: Developer screenshot mode. Launch the game with `-- --shot=<target>` to auto-navigate to a
//          screen (or into live arena combat), save viewport PNGs to --shot-dir, and quit. This gives
//          AI/visual iteration a real rendered-frame feedback loop without manual play.
// Usage:
//   godot --path . --windowed -- --shot=arena --shot-dir="C:/tmp/shots" [--shot-tier=2]
//   Targets: city | garage | workshop | arena | pause
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WastelandSurvivor.Game.Navigation;
using WastelandSurvivor.Game.UI;

namespace WastelandSurvivor.Game;

public partial class ScreenshotHarness : Node
{
	public static bool Active { get; private set; }
	public static string Target { get; private set; } = "city";
	public static string OutDir { get; private set; } = "user://shots";
	public static int Tier { get; private set; } = 1;
	/// <summary>Optional player chassis def id for the sandbox (--shot-vehicle=veh_heavy_truck).</summary>
	public static string VehicleDefId { get; private set; } = "";

	/// <summary>
	/// Optional staged city for arena captures (--shot-city=pittsburgh) so tier A/B frames can share
	/// one venue palette. Empty keeps the legacy behavior (tier>=3 auto-stages pittsburgh).
	/// </summary>
	public static string CityId { get; private set; } = "";

	/// <summary>
	/// Optional forced enemy variant preset id (--shot-variant=arena_tier_5_convoy) so combat probes
	/// against a specific variant don't depend on the encounter RNG roll. Harness-only.
	/// </summary>
	public static string VariantPresetId { get; private set; } = "";

	private const float GlobalTimeoutSeconds = 60f;

	private readonly List<(float Delay, Action Act, string Label)> _steps = new();
	private int _stepIndex;
	private float _stepWait;
	private float _elapsedTotal;
	private bool _sawCityShell;
	private float _waitCityTimeout = 12f;
	private int _shotCounter;
	private bool _autoDriveActive;

	/// <summary>Parse user args (everything after `--`). Call once, very early in App boot.</summary>
	public static void ParseArgs()
	{
		try
		{
			foreach (var arg in OS.GetCmdlineUserArgs())
			{
				if (arg.StartsWith("--shot=", StringComparison.OrdinalIgnoreCase))
				{
					Target = arg["--shot=".Length..].Trim().ToLowerInvariant();
					Active = true;
				}
				else if (arg.StartsWith("--shot-dir=", StringComparison.OrdinalIgnoreCase))
				{
					OutDir = arg["--shot-dir=".Length..].Trim().Trim('"');
				}
				else if (arg.StartsWith("--shot-tier=", StringComparison.OrdinalIgnoreCase))
				{
					if (int.TryParse(arg["--shot-tier=".Length..], out var t))
						Tier = Math.Clamp(t, 1, 5);
				}
				else if (arg.StartsWith("--shot-vehicle=", StringComparison.OrdinalIgnoreCase))
				{
					// Player chassis for the sandbox (e.g. veh_heavy_truck) — uses the def's
					// starter preset from vehicle_builds.json, so new classes can be filmed in
					// a real fight without hand-staging a save.
					VehicleDefId = arg["--shot-vehicle=".Length..].Trim();
				}
				else if (arg.StartsWith("--shot-variant=", StringComparison.OrdinalIgnoreCase))
				{
					VariantPresetId = arg["--shot-variant=".Length..].Trim();
				}
				else if (arg.StartsWith("--shot-city=", StringComparison.OrdinalIgnoreCase))
				{
					CityId = arg["--shot-city=".Length..].Trim().ToLowerInvariant();
				}
			}

			if (Active)
				GD.Print($"[ShotHarness] Active. target={Target} dir={OutDir} tier={Tier}");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[ShotHarness] Arg parse failed: {ex.Message}");
		}
	}

	public override void _Ready()
	{
		// Must keep running while the pause-menu target pauses the tree.
		ProcessMode = ProcessModeEnum.Always;

		// Deterministic capture size, decoupled from the user's fullscreen preference.
		DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		DisplayServer.WindowSetSize(new Vector2I(1600, 900));
		DisplayServer.WindowSetPosition(new Vector2I(60, 60));

		EnsureOutDir();
		BuildSteps();
		GD.Print($"[ShotHarness] Ready with {_steps.Count} steps.");
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		_elapsedTotal += dt;
		if (_elapsedTotal > GlobalTimeoutSeconds)
		{
			GD.PrintErr("[ShotHarness] Global timeout; quitting.");
			Quit();
			return;
		}

		// Phase 0: wait for the city shell (boot splash is skipped in shot mode, except the splash
		// target which times its steps from boot instead, and the title target which lands on the
		// title screen rather than the city).
		if (!_sawCityShell && (Target == "splash" || Target == "title"))
		{
			_sawCityShell = true;
			_stepWait = _steps.Count > 0 ? _steps[0].Delay : 0f;
		}
		if (!_sawCityShell)
		{
			_waitCityTimeout -= dt;
			var app = App.Instance;
			if (app != null && app.Services.TryGet<GameUiKit.UI.ScreenRouter>(out var router) && router?.Current is CityShell)
			{
				_sawCityShell = true;
				_stepWait = _steps.Count > 0 ? _steps[0].Delay : 0f;

				// The sandbox save starts empty: grant the starter build so garage/arena targets work.
				try
				{
					if (app.Services.TryGet<GameSession>(out var session) && session != null)
					{
						session.CreateStarterVehicleIfMissing(
							string.IsNullOrWhiteSpace(VehicleDefId) ? "veh_compact" : VehicleDefId);
						// Arena captures must be able to run any tier 1..5; city tier caps are part of
						// the campaign arc (Detroit caps at 2), so stage the sandbox in the tier-5 city.
						// --shot-city overrides so tier A/B comparisons can share one venue palette.
						if ((Target == "arena" || Target == "bailout") && !string.IsNullOrWhiteSpace(CityId))
							session.SetCurrentCity(CityId);
						else if ((Target == "arena" || Target == "bailout") && Tier >= 3)
							session.SetCurrentCity("pittsburgh");
						// Tournament entry charges a real fee — bankroll the sandbox clone.
						if (Target == "tournament" || Target == "tournamentbracket")
							session.TryAddMoney(1500, out _, out _);
						// Combat probes fight with a REAL ammo hold (starter 58 rounds runs dry long
						// before tier-3+ pools empty, which skews every TTK measurement).
						if (Target == "arena" || Target == "tournament" || Target == "bailout")
							session.TryBuyAmmoForActiveVehicle("ammo_mg_50cal", 150, 0, out _);
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[ShotHarness] Starter vehicle grant failed: {ex.Message}");
				}
			}
			else if (_waitCityTimeout <= 0f)
			{
				GD.PrintErr("[ShotHarness] CityShell never appeared; capturing whatever is on screen and quitting.");
				Capture("timeout_fallback");
				Quit();
			}
			return;
		}

		if (_autoDriveActive)
			UpdateAutoDrive(dt);

		// Kill-moment burst (round 11 N-5): the fixed-timer captures always straddled the
		// destroy VFX (~0.5s white flash + torus shockwave), so it had never been sampled.
		if (Target is "arena" or "tournament" or "haul" or "salvageraid")
			UpdateKillBurstWatch(dt);

		// Phase 1: run timed steps.
		if (_stepIndex >= _steps.Count)
			return;

		_stepWait -= dt;
		if (_stepWait > 0f)
			return;

		var step = _steps[_stepIndex];
		try
		{
			GD.Print($"[ShotHarness] Step {_stepIndex + 1}/{_steps.Count}: {step.Label}");
			step.Act();
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[ShotHarness] Step '{step.Label}' failed: {ex.Message}");
		}

		_stepIndex++;
		if (_stepIndex < _steps.Count)
			_stepWait = _steps[_stepIndex].Delay;
	}

	private void BuildSteps()
	{
		switch (Target)
		{
			case "garage":
				_steps.Add((0.7f, () => NavigateTo(GameScenes.GarageView), "nav garage"));
				_steps.Add((1.0f, () => Capture("garage_tab0"), "shot garage tab0"));
				AddTabSteps("garage", maxTabs: 4);
				_steps.Add((0.2f, Quit, "quit"));
				break;

			case "workshop":
				_steps.Add((0.7f, () => NavigateTo(GameScenes.WorkshopView), "nav workshop"));
				_steps.Add((1.0f, () => Capture("workshop_tab0"), "shot workshop tab0"));
				AddTabSteps("workshop", maxTabs: 3);
				_steps.Add((0.2f, Quit, "quit"));
				break;

			case "arena":
				_steps.Add((0.7f, () => NavigateTo(GameScenes.ArenaRealtimeView), "nav arena"));
				// Select the requested tier BEFORE the briefing capture so the frame shows the real
				// tier card state (captures used to always read "Tier: 1").
				_steps.Add((0.6f, SelectBriefingTier, "select tier"));
				_steps.Add((0.6f, () => Capture("arena_briefing"), "shot briefing"));
				_steps.Add((0.2f, StartArenaMatch, "start match"));
				// Auto-drive toward the enemy (steering recomputed every frame, with blocked-recovery),
				// firing throughout so captures land during a live exchange.
				_steps.Add((0.5f, () => _autoDriveActive = true, "auto-drive on"));
				_steps.Add((2.0f, () => Input.ActionPress("ws_fire"), "hold fire"));
				_steps.Add((1.5f, () => Capture("arena_combat_early"), "shot combat early"));
				// Fire group 2 taps: exercises the guided missile (lock-on + HE armor matrix).
				_steps.Add((0.6f, () => Input.ActionPress("ws_fire_2"), "missile tap down"));
				_steps.Add((0.4f, () => Input.ActionRelease("ws_fire_2"), "missile tap up"));
				_steps.Add((1.0f, () => Capture("arena_combat_mid"), "shot combat mid"));
				_steps.Add((2.0f, () => Capture("arena_combat_mid2"), "shot combat mid2"));
				// Drop a couple of rear mines mid-fight so their markers get exercised on camera.
				_steps.Add((0.4f, () => Input.ActionPress("ws_fire_3"), "mine tap down"));
				_steps.Add((0.3f, () => Input.ActionRelease("ws_fire_3"), "mine tap up"));
				_steps.Add((0.6f, () => Input.ActionPress("ws_fire_2"), "missile tap 2 down"));
				_steps.Add((0.4f, () => Input.ActionRelease("ws_fire_2"), "missile tap 2 up"));
				_steps.Add((1.3f, () => Capture("arena_combat_late"), "shot combat late"));
				_steps.Add((3.0f, () => Capture("arena_combat_x1"), "shot combat x1"));
				_steps.Add((3.0f, () => Capture("arena_combat_x2"), "shot combat x2"));
				_steps.Add((0.3f, Quit, "quit"));
				break;

			case "store":
				_steps.Add((0.7f, () => NavigateTo(GameScenes.StoreView), "nav store"));
				_steps.Add((1.0f, () => Capture("store"), "shot store"));
				// Select a vehicle row so the detail pane's live 3D dealership portrait gets
				// filmed too (round 10 P2-10a verification).
				_steps.Add((0.3f, SelectFirstStoreVehicle, "select vehicle row"));
				_steps.Add((1.0f, () => Capture("store_vehicle_detail"), "shot vehicle detail"));
				_steps.Add((0.2f, Quit, "quit"));
				break;

			case "cityfreight":
				// Ops-tab freight ledger with a LIVE contract: accept the first fitting offer via
				// the session API, re-open the city shell so it refreshes from the staged state,
				// then capture the Ops tab's in-transit card.
				_steps.Add((0.5f, AcceptFirstFreightOffer, "accept freight offer"));
				// Also accept the first WANTED poster so the Ops active-bounty card, the travel-tab
				// route-card warning, AND the map's haunted-leg reticle all render in one run.
				_steps.Add((0.3f, AcceptFirstBounty, "accept bounty"));
				_steps.Add((0.4f, () => NavigateTo(GameScenes.CityShell), "reload city shell"));
				_steps.Add((0.6f, () => SwitchBodyTab(2), "tab ops"));
				_steps.Add((0.6f, () => Capture("city_ops_freight_active"), "shot ops freight"));
				_steps.Add((0.4f, () => SwitchBodyTab(1), "tab travel"));
				_steps.Add((0.8f, () => Capture("city_travel_bounty_marked"), "shot travel bounty"));
				_steps.Add((0.2f, Quit, "quit"));
				break;

			case "tournamentbracket":
				// Mid-bracket briefing capture: enter + win round 1 through the session API (same
				// path as tournamentlogic — no combat sim), then open the briefing so the bracket
				// strip shows its WON / CURRENT / UPCOMING node states for visual verification.
				_steps.Add((0.5f, WinOneTournamentRound, "enter + win round 1"));
				_steps.Add((0.7f, () => NavigateTo(GameScenes.ArenaRealtimeView), "nav arena"));
				_steps.Add((1.2f, () => Capture("tournament_bracket_midrun"), "shot bracket midrun"));
				_steps.Add((0.2f, Quit, "quit"));
				break;

			case "tournamentlogic":
				// Headless end-to-end probe of the tournament bracket through the real session API:
				// enter -> win -> pit patch -> next round -> ... -> champion. No combat sim; prints
				// [TournLogic] lines so the full advancement/champion math is verifiable from stdout.
				_steps.Add((1.0f, RunTournamentLogicProbe, "tournament logic probe"));
				_steps.Add((0.5f, Quit, "quit"));
				break;

			case "freightlogic":
				// Headless end-to-end probe of the freight loop through the real session API:
				// board -> accept -> cargo loaded/mass up -> travel -> delivery payout -> board
				// rerolled. Prints [FreightLogic] lines so the math is verifiable from stdout.
				_steps.Add((1.0f, RunFreightLogicProbe, "freight logic probe"));
				_steps.Add((0.5f, Quit, "quit"));
				break;

			case "bountylogic":
				// Headless end-to-end probe of the WANTED-bounty loop through the real session API:
				// board -> accept -> travel the haunted leg (forced trigger) -> bounty fight staged
				// -> probe win -> payout + contract cleared + board rerolled. Prints [BountyLogic]
				// lines so the whole quest loop is verifiable from stdout.
				_steps.Add((1.0f, RunBountyLogicProbe, "bounty logic probe"));
				_steps.Add((0.5f, Quit, "quit"));
				break;

			case "haul":
				// Defend the haul: hitch a trailer, then fight with it riding behind the player.
				_steps.Add((0.6f, StageHitchedTrailer, "buy + hitch trailer"));
				_steps.Add((0.7f, () => NavigateTo(GameScenes.ArenaRealtimeView), "nav arena"));
				_steps.Add((0.6f, SelectBriefingTier, "select tier"));
				_steps.Add((0.4f, StartArenaMatch, "start match"));
				_steps.Add((1.2f, () => Capture("haul_line"), "shot trailer on line"));
				_steps.Add((0.4f, () => _autoDriveActive = true, "auto-drive on"));
				_steps.Add((3.0f, () => Capture("haul_fight"), "shot fight with haul"));
				_steps.Add((0.3f, Quit, "quit"));
				break;

			case "bailout":
				// Mobility-kill → bail-out duel probe (tier 3+): force the enemy's tires dead,
				// then film the on-foot driver shooting back. Use --shot-tier=3.
				_steps.Add((0.7f, () => NavigateTo(GameScenes.ArenaRealtimeView), "nav arena"));
				_steps.Add((0.6f, SelectBriefingTier, "select tier"));
				_steps.Add((0.4f, StartArenaMatch, "start match"));
				_steps.Add((0.5f, () => _autoDriveActive = true, "auto-drive on"));
				_steps.Add((3.0f, ForceEnemyMobilityKill, "force mobility kill"));
				_steps.Add((1.2f, () => Capture("bailout_start"), "shot bail-out start"));
				_steps.Add((1.2f, () => Input.ActionPress("ws_fire"), "hold fire"));
				_steps.Add((1.6f, () => Capture("bailout_duel"), "shot duel"));
				_steps.Add((3.0f, () => Capture("bailout_duel2"), "shot duel 2"));
				_steps.Add((3.0f, () => Capture("bailout_end"), "shot duel end"));
				_steps.Add((0.3f, Quit, "quit"));
				break;

			case "highway":
				// Road-ambush fight on the HIGHWAY venue (asphalt ribbon, guardrails, shoulder
				// derelicts): stage the encounter via the session, resume in the arena, film it.
				_steps.Add((0.6f, StartRoadAmbush, "start road ambush"));
				_steps.Add((0.7f, () => NavigateTo(GameScenes.ArenaRealtimeView), "nav arena"));
				_steps.Add((0.8f, StartArenaMatch, "resume road fight"));
				_steps.Add((1.0f, () => Capture("highway_start"), "shot highway start"));
				_steps.Add((0.4f, () => _autoDriveActive = true, "auto-drive on"));
				_steps.Add((1.6f, () => Input.ActionPress("ws_fire"), "hold fire"));
				_steps.Add((2.4f, () => Capture("highway_fight"), "shot highway fight"));
				_steps.Add((3.0f, () => Capture("highway_fight2"), "shot highway fight 2"));
				_steps.Add((0.3f, Quit, "quit"));
				break;

			case "salvageraid":
				// Rival crew hauling a prize wreck: start the raid via the session, resume in the
				// arena, film the haul riding the crew's tow line during a real fight.
				_steps.Add((0.6f, StartSalvageRaid, "start raid"));
				_steps.Add((0.7f, () => NavigateTo(GameScenes.ArenaRealtimeView), "nav arena"));
				_steps.Add((0.8f, StartArenaMatch, "resume raid"));
				_steps.Add((1.2f, () => Capture("raid_line"), "shot haul on line"));
				_steps.Add((0.4f, () => _autoDriveActive = true, "auto-drive on"));
				_steps.Add((1.6f, () => Input.ActionPress("ws_fire"), "hold fire"));
				_steps.Add((2.4f, () => Capture("raid_fight"), "shot fight"));
				_steps.Add((3.0f, () => Capture("raid_fight2"), "shot fight 2"));
				_steps.Add((0.3f, Quit, "quit"));
				break;

			case "interception":
				// Full pipeline: lose a fight (vehicle captured) -> buy a replacement -> start the
				// interception in the same city -> film the captured rig riding the captor's tow line.
				_steps.Add((0.7f, () => NavigateTo(GameScenes.ArenaRealtimeView), "nav arena"));
				_steps.Add((0.6f, SelectBriefingTier, "select tier"));
				_steps.Add((0.4f, StartArenaMatch, "start match"));
				_steps.Add((2.0f, ForceLose, "force lose (capture)"));
				_steps.Add((1.0f, () => Input.ActionPress("ws_fire"), "skip aftermath down"));
				_steps.Add((0.3f, () => Input.ActionRelease("ws_fire"), "skip aftermath up"));
				_steps.Add((4.0f, StageInterception, "buy replacement + start interception"));
				_steps.Add((1.0f, () => NavigateTo(GameScenes.ArenaRealtimeView), "re-enter arena"));
				_steps.Add((0.8f, StartArenaMatch, "resume interception"));
				_steps.Add((1.5f, () => Capture("intercept_line"), "shot rig on line"));
				_steps.Add((0.4f, () => _autoDriveActive = true, "auto-drive on"));
				_steps.Add((3.0f, () => Capture("intercept_chase"), "shot chase"));
				_steps.Add((0.3f, Quit, "quit"));
				break;

			case "scavenge":
				// Roll a scavenge-site encounter directly, then open the arena view — the pending
				// flag auto-enters the yard (no briefing hold). Film the derelict + salvage flow.
				_steps.Add((0.6f, StartScavengeSite, "start scavenge"));
				_steps.Add((0.7f, () => NavigateTo(GameScenes.ArenaRealtimeView), "nav arena"));
				_steps.Add((1.6f, () => Capture("scavenge_yard"), "shot yard"));
				_steps.Add((0.4f, () => _autoDriveActive = true, "auto-drive on"));
				_steps.Add((3.0f, () => Capture("scavenge_approach"), "shot approach"));
				_steps.Add((3.0f, () => Capture("scavenge_close"), "shot close"));
				_steps.Add((0.3f, Quit, "quit"));
				break;

			case "lossaftermath":
				// Force a mid-fight loss and film the witnessed-capture beat: the victor drives to
				// the wreck, hitches the tow cable, and hauls it out the south gate.
				_steps.Add((0.7f, () => NavigateTo(GameScenes.ArenaRealtimeView), "nav arena"));
				_steps.Add((0.6f, SelectBriefingTier, "select tier"));
				_steps.Add((0.4f, StartArenaMatch, "start match"));
				_steps.Add((0.5f, () => _autoDriveActive = true, "auto-drive on"));
				_steps.Add((2.5f, () => { _autoDriveActive = false; ForceLose(); }, "force lose"));
				_steps.Add((1.6f, () => Capture("aftermath_drive"), "shot drive-to-wreck"));
				_steps.Add((2.4f, () => Capture("aftermath_hitch"), "shot hitch"));
				_steps.Add((2.6f, () => Capture("aftermath_haul"), "shot haul out"));
				_steps.Add((2.6f, () => Capture("aftermath_haul2"), "shot haul out 2"));
				// Late beat: by now the tow is at/near the gate — the frame that parks the camera
				// next to the gate dressing, crowd slabs and skyline (round 10 P1-5 verification).
				_steps.Add((4.5f, () => Capture("aftermath_gate"), "shot gate"));
				_steps.Add((0.3f, Quit, "quit"));
				break;

			case "tournament":
				// Enter the city tournament through the REAL entry path (fee, round-1 encounter),
				// then fight round 1 on auto-drive. Combat truth streams via [Combat] stdout lines.
				_steps.Add((0.7f, () => NavigateTo(GameScenes.ArenaRealtimeView), "nav arena"));
				_steps.Add((0.8f, () => Capture("tournament_briefing"), "shot entry card"));
				_steps.Add((0.3f, EnterTournament, "enter tournament"));
				_steps.Add((0.5f, () => _autoDriveActive = true, "auto-drive on"));
				_steps.Add((2.0f, () => Input.ActionPress("ws_fire"), "hold fire"));
				_steps.Add((2.0f, () => Capture("tournament_round1"), "shot round1"));
				_steps.Add((3.0f, () => Capture("tournament_round1_late"), "shot round1 late"));
				_steps.Add((0.3f, Quit, "quit"));
				break;

			case "pause":
				_steps.Add((0.8f, OpenPauseMenu, "open pause"));
				_steps.Add((0.6f, () => Capture("pause_menu"), "shot pause"));
				_steps.Add((0.2f, Quit, "quit"));
				break;

			case "splash":
				// Boot splash plays normally in this mode; capture a few beats of it.
				_steps.Add((1.0f, () => Capture("splash_a"), "shot splash a"));
				_steps.Add((1.6f, () => Capture("splash_b"), "shot splash b"));
				_steps.Add((1.6f, () => Capture("splash_c"), "shot splash c"));
				_steps.Add((0.2f, Quit, "quit"));
				break;

			case "title":
				// AppRoot routes shot mode straight onto the title screen for this target.
				_steps.Add((1.0f, () => Capture("title_menu"), "shot title menu"));
				_steps.Add((0.2f, Quit, "quit"));
				break;

			case "driverstore":
				_steps.Add((0.7f, () => NavigateTo(GameScenes.DriverStoreView), "nav driver store"));
				_steps.Add((1.0f, () => Capture("driverstore"), "shot driver store"));
				_steps.Add((0.2f, Quit, "quit"));
				break;

			case "journey":
				// Travel journey interstitial: stage it directly over the city shell with a
				// representative leg (ambush interrupt) so the presentation can be filmed without
				// depending on travel RNG.
				_steps.Add((0.8f, StageJourneyOverlay, "stage journey overlay"));
				_steps.Add((1.2f, () => Capture("journey_early"), "shot journey early"));
				_steps.Add((1.6f, () => Capture("journey_mid"), "shot journey mid"));
				_steps.Add((1.4f, () => Capture("journey_interrupt"), "shot journey interrupt"));
				_steps.Add((0.3f, Quit, "quit"));
				break;

			case "onfoot":
				// On-foot personal weapon probe: start a match, drive in, stop, bail out, and hold
				// fire — captures the sidearm tracers + HUD ammo row, logs [ShotDebug] pw lines.
				_steps.Add((0.7f, () => NavigateTo(GameScenes.ArenaRealtimeView), "nav arena"));
				_steps.Add((0.8f, StartArenaMatch, "start match"));
				_steps.Add((0.5f, () => _autoDriveActive = true, "auto-drive on"));
				_steps.Add((5.0f, () =>
				{
					_autoDriveActive = false;
					Input.ActionRelease("ws_move_forward");
					Input.ActionRelease("ws_steer_left");
					Input.ActionRelease("ws_steer_right");
					// Brake hard — exit requires the vehicle stopped/slow, and a coasting compact
					// stays above the threshold for seconds.
					Input.ActionPress("ws_move_backward");
				}, "stop driving + brake"));
				_steps.Add((1.2f, () => Input.ActionRelease("ws_move_backward"), "release brake"));
				_steps.Add((0.8f, () => Input.ActionPress("ws_interact"), "exit vehicle down"));
				_steps.Add((0.2f, () => Input.ActionRelease("ws_interact"), "exit vehicle up"));
				// Second attempt in case the first press still caught residual rolling speed.
				_steps.Add((0.8f, () => Input.ActionPress("ws_interact"), "exit retry down"));
				_steps.Add((0.2f, () => Input.ActionRelease("ws_interact"), "exit retry up"));
				// Sprint toward the enemy so the facing-cone/range gate can pass (spec aim model) AND
				// the driver clears intervening archetype cover — the probe should land at least one
				// real hull hit, not only whiffs/barrier impacts. Re-steer mid-approach (the enemy
				// drives while we walk).
				_steps.Add((0.5f, () =>
				{
					Input.ActionPress("ws_sprint");
					ApproachEnemyOnFoot(true);
				}, "sprint at enemy"));
				_steps.Add((2.2f, () => ApproachEnemyOnFoot(true), "re-steer"));
				_steps.Add((2.2f, () =>
				{
					ApproachEnemyOnFoot(false);
					Input.ActionRelease("ws_sprint");
				}, "stop walking"));
				// Deterministic hit: park the enemy in front of the driver for the fire window.
				_steps.Add((0.4f, StageEnemyBesideDriver, "stage enemy close"));
				_steps.Add((0.3f, () => Input.ActionPress("ws_fire"), "hold fire on foot"));
				_steps.Add((1.0f, () => Capture("onfoot_firing"), "shot onfoot firing"));
				_steps.Add((1.5f, () => Capture("onfoot_firing2"), "shot onfoot firing 2"));
				_steps.Add((1.2f, () => Input.ActionRelease("ws_fire"), "release fire"));
				_steps.Add((0.4f, () => Capture("onfoot_hud"), "shot onfoot hud"));
				_steps.Add((0.3f, Quit, "quit"));
				break;

			case "enginesweep":
				// Audio probe: record the Engines bus through idle → full-throttle gear sweep →
				// coast → throttle pulses (the exact pattern that produced the "organ keys" read).
				_steps.Add((0.7f, () => NavigateTo(GameScenes.ArenaRealtimeView), "nav arena"));
				_steps.Add((0.8f, StartArenaMatch, "start match"));
				_steps.Add((0.8f, StartEngineRecording, "record on"));
				_steps.Add((1.5f, () => Input.ActionPress("ws_move_forward"), "full throttle"));
				_steps.Add((6.0f, () => Input.ActionRelease("ws_move_forward"), "coast"));
				_steps.Add((2.5f, () => Input.ActionPress("ws_move_forward"), "pulse1 on"));
				_steps.Add((0.5f, () => Input.ActionRelease("ws_move_forward"), "pulse1 off"));
				_steps.Add((0.5f, () => Input.ActionPress("ws_move_forward"), "pulse2 on"));
				_steps.Add((0.5f, () => Input.ActionRelease("ws_move_forward"), "pulse2 off"));
				_steps.Add((0.5f, () => Input.ActionPress("ws_move_forward"), "pulse3 on"));
				_steps.Add((0.5f, () => Input.ActionRelease("ws_move_forward"), "pulse3 off"));
				_steps.Add((0.5f, () => Input.ActionPress("ws_move_forward"), "pulse4 on"));
				_steps.Add((0.5f, () => Input.ActionRelease("ws_move_forward"), "pulse4 off"));
				_steps.Add((2.0f, () => Input.ActionRelease("ws_move_forward"), "settle"));
				_steps.Add((1.0f, StopEngineRecordingAndSave, "record off + save"));
				_steps.Add((0.3f, Quit, "quit"));
				break;

			default: // "city"
				_steps.Add((0.9f, () => Capture("city_tab0"), "shot city tab0"));
				AddTabSteps("city", maxTabs: 3);
				_steps.Add((0.2f, Quit, "quit"));
				break;
		}
	}

	/// <summary>Page through the current screen's BodyTabs and capture each page.</summary>
	private void AddTabSteps(string label, int maxTabs)
	{
		for (var i = 1; i < maxTabs; i++)
		{
			var idx = i;
			_steps.Add((0.35f, () => SwitchBodyTab(idx), $"tab {idx}"));
			_steps.Add((0.55f, () => Capture($"{label}_tab{idx}"), $"shot {label} tab{idx}"));
		}
	}

	private void SwitchBodyTab(int index)
	{
		var app = App.Instance;
		if (app == null || !app.Services.TryGet<GameUiKit.UI.ScreenRouter>(out var router) || router?.Current is not Node screen)
			return;
		if (FindTabContainer(screen) is not { } tabs)
			return;
		if (index >= 0 && index < tabs.GetTabCount())
			tabs.CurrentTab = index;
	}

	private static TabContainer? FindTabContainer(Node root)
	{
		if (root is TabContainer tc) return tc;
		foreach (var childObj in root.GetChildren())
		{
			if (childObj is Node child && FindTabContainer(child) is { } found)
				return found;
		}
		return null;
	}

	/// <summary>
	/// Navigate via the ScreenRouter directly. NEVER pass this harness node into UiNav.Replace —
	/// its legacy fallback path swaps/frees the `from` node, which would kill the harness mid-run.
	/// </summary>
	private static void NavigateTo(string scenePath)
	{
		var app = App.Instance;
		if (app == null) return;
		if (app.Services.TryGet<GameUiKit.UI.ScreenRouter>(out var router) && router != null)
		{
			if (!router.TryReplace(scenePath))
				GD.PrintErr($"[ShotHarness] Router failed to open {scenePath}");
			return;
		}
		GD.PrintErr("[ShotHarness] No ScreenRouter available.");
	}

	// --- Kill-moment burst capture (round 11 N-5) ---
	// Poll the enemy wreck state each frame during combat runs; on the alive->destroyed
	// transition fire a short burst of captures so the destroy VFX (white flash ~0.14s, torus
	// shockwave ~0.5s, fireball ~0.95s) finally lands on film. First kill only; harness-gated.
	private bool _killBurstDone;
	private int _killWatchDestroyedCount = -1;
	private readonly List<(float Delay, string Label)> _killBurstQueue = new();

	private void UpdateKillBurstWatch(float dt)
	{
		for (var i = _killBurstQueue.Count - 1; i >= 0; i--)
		{
			var (delay, label) = _killBurstQueue[i];
			delay -= dt;
			if (delay <= 0f)
			{
				_killBurstQueue.RemoveAt(i);
				Capture(label);
			}
			else
			{
				_killBurstQueue[i] = (delay, label);
			}
		}

		if (_killBurstDone)
			return;

		var destroyed = 0;
		foreach (var n in GetTree().GetNodesInGroup("enemy_vehicle"))
		{
			if (n is Node node && GodotObject.IsInstanceValid(node)
				&& node.GetNodeOrNull<Arena.VehicleDamageVfx>("DamageVfx") is { IsDestroyed: true })
				destroyed++;
		}

		// First sample is the staging state (a scavenge derelict starts destroyed) — only a
		// TRANSITION during the run counts as a kill.
		if (_killWatchDestroyedCount < 0)
		{
			_killWatchDestroyedCount = destroyed;
			return;
		}

		if (destroyed > _killWatchDestroyedCount)
		{
			_killBurstDone = true;
			GD.Print("[ShotHarness] Enemy destroyed - firing kill-moment capture burst.");
			Capture("kill_t0");
			_killBurstQueue.Add((0.12f, "kill_flash"));
			_killBurstQueue.Add((0.30f, "kill_ring"));
		}
		_killWatchDestroyedCount = destroyed;
	}

	private float _blockedTime;
	private float _recoverTime;
	private float _autoDrivePhaseSeconds;

	/// <summary>
	/// Minimal chase driver for capture runs: hold forward, bang-bang steer toward the enemy, and
	/// reverse-turn briefly when wedged against cover.
	/// </summary>
	private void UpdateAutoDrive(float dt)
	{
		var player = GetTree().GetFirstNodeInGroup("player_vehicle") as Node3D;
		var enemy = GetTree().GetFirstNodeInGroup("enemy_vehicle") as Node3D;
		if (player == null || !GodotObject.IsInstanceValid(player))
			return;

		// Blocked recovery: back out while turning, then resume the chase.
		if (_recoverTime > 0f)
		{
			_recoverTime -= dt;
			Input.ActionRelease("ws_move_forward");
			Input.ActionPress("ws_move_backward");
			Input.ActionPress("ws_steer_right");
			Input.ActionRelease("ws_steer_left");
			if (_recoverTime <= 0f)
			{
				Input.ActionRelease("ws_move_backward");
				Input.ActionRelease("ws_steer_right");
			}
			return;
		}

		Input.ActionPress("ws_move_forward");

		var speed = player is CharacterBody3D body ? body.Velocity.Length() : 99f;
		_blockedTime = speed < 0.6f ? _blockedTime + dt : 0f;
		if (_blockedTime > 0.8f)
		{
			_blockedTime = 0f;
			_recoverTime = 0.9f;
			return;
		}

		if (enemy == null || !GodotObject.IsInstanceValid(enemy))
		{
			Input.ActionRelease("ws_steer_left");
			Input.ActionRelease("ws_steer_right");
			return;
		}

		var to = enemy.GlobalPosition - player.GlobalPosition;
		to.Y = 0f;
		if (to.LengthSquared() < 1f)
			return;

		// Mix ram pressure with broadside drive-bys: most of the cycle noses the target (kill
		// pressure so short probes still resolve), then a brief pass window aims a few meters
		// BESIDE it so side-mounted weapons — player's AND the enemy's — sweep through real
		// bearing windows the way a human duelist's drive-by would.
		_autoDrivePhaseSeconds += dt;
		var dist = to.Length();
		if (dist < 20f && (_autoDrivePhaseSeconds % 9f) >= 6f)
		{
			var side = new Vector3(-to.Z, 0f, to.X).Normalized();
			var aimPoint = enemy.GlobalPosition + side * 5.5f;
			to = aimPoint - player.GlobalPosition;
			to.Y = 0f;
			if (to.LengthSquared() < 1f)
				return;
		}

		var forward = -player.GlobalTransform.Basis.Z;
		forward.Y = 0f;
		var angle = forward.SignedAngleTo(to.Normalized(), Vector3.Up);
		const float deadzone = 0.10f;
		if (angle > deadzone)
		{
			Input.ActionPress("ws_steer_left");
			Input.ActionRelease("ws_steer_right");
		}
		else if (angle < -deadzone)
		{
			Input.ActionPress("ws_steer_right");
			Input.ActionRelease("ws_steer_left");
		}
		else
		{
			Input.ActionRelease("ws_steer_left");
			Input.ActionRelease("ws_steer_right");
		}
	}

	private void StartArenaMatch()
	{
		var view = FindArenaView(GetTree().Root);
		if (view == null)
		{
			GD.PrintErr("[ShotHarness] ArenaRealtimeView not found; cannot start match.");
			return;
		}
		view.DebugAutoStartMatch(Tier);
	}

	private void SelectBriefingTier()
	{
		var view = FindArenaView(GetTree().Root);
		view?.DebugSelectTier(Tier);
	}

	private void SelectFirstStoreVehicle()
	{
		var app = App.Instance;
		if (app != null && app.Services.TryGet<GameUiKit.UI.ScreenRouter>(out var router) && router?.Current is StoreView store)
			store.DebugSelectFirstVehicle();
		else
			GD.PrintErr("[ShotHarness] StoreView not current; cannot select vehicle row.");
	}

	private void EnterTournament()
	{
		var view = FindArenaView(GetTree().Root);
		if (view == null)
		{
			GD.PrintErr("[ShotHarness] ArenaRealtimeView not found; cannot enter tournament.");
			return;
		}
		view.DebugEnterTournament();
	}

	private void ForceLose()
	{
		var view = FindArenaView(GetTree().Root);
		view?.DebugForceLose();
	}

	private static void StageHitchedTrailer()
	{
		var app = App.Instance;
		if (app == null || !app.Services.TryGet<GameSession>(out var session) || session == null
			|| !app.Services.TryGet<Core.IO.DefDatabase>(out var defs) || defs == null)
			return;

		session.TryAddMoney(2000, out _, out _);
		if (!session.TryBuyVehicle(defs, "veh_trailer_small", out var buyErr))
		{
			GD.PrintErr($"[ShotHarness] trailer buy failed: {buyErr}");
			return;
		}
		var trailer = System.Linq.Enumerable.FirstOrDefault(
			session.GetOwnedVehicles(),
			v => v.DefinitionId == "veh_trailer_small");
		if (trailer == null)
		{
			GD.PrintErr("[ShotHarness] trailer instance missing after buy.");
			return;
		}
		if (!session.TryHitchTrailer(defs, trailer.InstanceId, out var hitchErr))
			GD.PrintErr($"[ShotHarness] hitch failed: {hitchErr}");
	}

	private static void StartSalvageRaid()
	{
		var app = App.Instance;
		if (app == null || !app.Services.TryGet<GameSession>(out var session) || session == null)
			return;
		if (!session.TryStartSalvageCrewRaid(out var err))
			GD.PrintErr($"[ShotHarness] salvage raid start failed: {err}");
	}

	/// <summary>
	/// On-foot approach: press the move actions that point the driver avatar at the enemy so the
	/// personal weapon's facing-cone gate is satisfied when the probe fires (spec aiming model).
	/// </summary>
	private void ApproachEnemyOnFoot(bool press)
	{
		foreach (var action in new[] { "ws_move_forward", "ws_move_backward", "ws_steer_left", "ws_steer_right" })
			Input.ActionRelease(action);
		if (!press) return;

		var driver = GetTree().GetFirstNodeInGroup("player_driver") as Node3D
			?? GetTree().GetFirstNodeInGroup("driver_pawn") as Node3D;
		var enemy = GetTree().GetFirstNodeInGroup("enemy_vehicle") as Node3D;
		if (driver == null || enemy == null) return;

		var to = enemy.GlobalPosition - driver.GlobalPosition;
		if (MathF.Abs(to.Z) > 1f)
			Input.ActionPress(to.Z < 0 ? "ws_move_forward" : "ws_move_backward");
		if (MathF.Abs(to.X) > 1f)
			Input.ActionPress(to.X < 0 ? "ws_steer_left" : "ws_steer_right");
	}

	/// <summary>
	/// Deterministic on-foot hit staging: park the enemy vehicle ~11m in front of the driver so a
	/// hull hit is guaranteed regardless of where the AI drove (probe-only; the racy walk-chase
	/// left the chip-damage path unexercised across runs).
	/// </summary>
	private void StageEnemyBesideDriver()
	{
		var driver = GetTree().GetFirstNodeInGroup("player_driver") as Node3D
			?? GetTree().GetFirstNodeInGroup("driver_pawn") as Node3D;
		var enemy = GetTree().GetFirstNodeInGroup("enemy_vehicle") as Node3D;
		if (driver == null || enemy == null)
		{
			GD.PrintErr("[ShotHarness] StageEnemyBesideDriver: pawns missing.");
			return;
		}
		var toCenter = (new Vector3(0f, driver.GlobalPosition.Y, 0f) - driver.GlobalPosition);
		toCenter.Y = 0f;
		var dir2 = toCenter.LengthSquared() > 1f ? toCenter.Normalized() : Vector3.Forward;
		enemy.GlobalPosition = driver.GlobalPosition + dir2 * 11f + Vector3.Up * 0.05f;
		if (enemy is CharacterBody3D body)
			body.Velocity = Vector3.Zero;
		GD.Print("[ShotHarness] Enemy staged 11m from on-foot driver.");
	}

	private void ForceEnemyMobilityKill()
	{
		var app = App.Instance;
		if (app == null || !app.Services.TryGet<GameUiKit.UI.ScreenRouter>(out var router)) return;
		if (router?.Current is ArenaRealtimeView arena)
			arena.DebugForceEnemyMobilityKill();
		else
			GD.PrintErr("[ShotHarness] ForceEnemyMobilityKill: arena view not active.");
	}

	private static void StartRoadAmbush()
	{
		var app = App.Instance;
		if (app == null || !app.Services.TryGet<GameSession>(out var session) || session == null)
			return;
		if (!session.TryStartRoadEncounter(Math.Clamp(Tier, 1, 5), out var err))
			GD.PrintErr($"[ShotHarness] road ambush start failed: {err}");
	}

	private static void StartScavengeSite()
	{
		var app = App.Instance;
		if (app == null || !app.Services.TryGet<GameSession>(out var session) || session == null)
			return;
		if (!session.TryStartScavengeEncounter(out var err))
			GD.PrintErr($"[ShotHarness] scavenge start failed: {err}");
	}

	private static void StageInterception()
	{
		var app = App.Instance;
		if (app == null || !app.Services.TryGet<GameSession>(out var session) || session == null
			|| !app.Services.TryGet<Core.IO.DefDatabase>(out var defs) || defs == null)
			return;

		session.TryAddMoney(6000, out _, out _);
		if (!session.TryBuyVehicle(defs, "veh_compact", out var buyErr))
			GD.PrintErr($"[ShotHarness] replacement buy failed: {buyErr}");
		// The capture cleared the active slot; the dealership auto-activates the new chassis.
		session.TryBuyAmmoForActiveVehicle("ammo_mg_50cal", 150, 0, out _);

		var captured = System.Linq.Enumerable.FirstOrDefault(session.GetCapturedVehicles());
		if (captured == null)
		{
			GD.PrintErr("[ShotHarness] no captured vehicle to intercept.");
			return;
		}
		if (!session.TryStartInterceptionEncounter(captured.VehicleInstanceId, out var err))
			GD.PrintErr($"[ShotHarness] interception start failed: {err}");
	}

	private static void RunFreightLogicProbe()
	{
		var app = App.Instance;
		if (app == null
			|| !app.Services.TryGet<GameSession>(out var session) || session == null
			|| !app.Services.TryGet<WastelandSurvivor.Core.IO.DefDatabase>(out var defs) || defs == null)
		{
			GD.PrintErr("[FreightLogic] GameSession/DefDatabase missing.");
			return;
		}

		session.CreateStarterVehicleIfMissing(defs);
		session.TryAddMoney(3000, out _, out _);
		session.TryRefuelActiveVehicleToFull(defs, out _, out _);

		var offers = session.GetFreightOffers(defs);
		foreach (var o in offers)
			GD.Print($"[FreightLogic] offer: {o.Units}u {o.CargoDisplayName} -> {o.DestDisplayName} ({o.DistanceKm:0} km) ${o.PayoutUsd}");
		if (offers.Count == 0)
		{
			GD.PrintErr("[FreightLogic] no offers on the board.");
			return;
		}

		// Take the near offer (bucket 0 — sized to fit the starter compact) and require a direct
		// road so the probe stays a one-leg run.
		var city = session.GetCurrentCityDef(defs);
		var pick = offers.FirstOrDefault(o =>
			city?.Roads.Any(r => !r.Closed && string.Equals(r.ToCityId, o.DestCityId, System.StringComparison.OrdinalIgnoreCase)) == true);
		if (pick == null)
		{
			GD.PrintErr("[FreightLogic] no direct-road offer available from this city.");
			return;
		}

		var vehBefore = session.GetActiveVehicle();
		var cargoBefore = vehBefore?.CargoInventory.Values.Sum() ?? 0;
		if (!session.TryAcceptFreightOffer(defs, pick.OfferId, out var acceptErr))
		{
			GD.PrintErr($"[FreightLogic] accept FAILED: {acceptErr}");
			return;
		}
		var vehLoaded = session.GetActiveVehicle();
		var cargoAfter = vehLoaded?.CargoInventory.Values.Sum() ?? 0;
		GD.Print($"[FreightLogic] accepted: {pick.Units}u {pick.CargoDisplayName} -> {pick.DestDisplayName}; cargo units {cargoBefore} -> {cargoAfter}; contract={(session.GetActiveFreightContract() != null ? "ACTIVE" : "null")}");

		var moneyBefore = session.GetMoneyUsd();
		if (!session.TryTravelTo(defs, pick.DestCityId, out var ambushTier, out var travelErr))
		{
			GD.PrintErr($"[FreightLogic] travel FAILED: {travelErr}");
			return;
		}
		var moneyAfter = session.GetMoneyUsd();
		var vehDelivered = session.GetActiveVehicle();
		var cargoDelivered = vehDelivered?.CargoInventory.Values.Sum() ?? 0;
		GD.Print($"[FreightLogic] traveled (ambushTier={ambushTier}): money ${moneyBefore} -> ${moneyAfter} (payout ${pick.PayoutUsd} - toll/fuel), cargo units now {cargoDelivered}, contract={(session.GetActiveFreightContract() == null ? "CLEARED" : "STILL ACTIVE")}, delivered flag=+${session.LastLegDeliveredFreightPayout}, lifetime={session.Save.Player.FreightContractsDelivered}");

		var rerolled = session.GetFreightOffers(defs);
		GD.Print($"[FreightLogic] new board at {session.Save.Player.CurrentCityId}: {rerolled.Count} offers, first={(rerolled.Count > 0 ? $"{rerolled[0].Units}u {rerolled[0].CargoDisplayName} -> {rerolled[0].DestDisplayName}" : "-")}");
	}

	private static void RunBountyLogicProbe()
	{
		var app = App.Instance;
		if (app == null
			|| !app.Services.TryGet<GameSession>(out var session) || session == null
			|| !app.Services.TryGet<WastelandSurvivor.Core.IO.DefDatabase>(out var defs) || defs == null)
		{
			GD.PrintErr("[BountyLogic] GameSession/DefDatabase missing.");
			return;
		}

		session.CreateStarterVehicleIfMissing(defs);
		session.TryAddMoney(3000, out _, out _);
		session.TryRefuelActiveVehicleToFull(defs, out _, out _);

		var offers = session.GetBountyOffers(defs);
		foreach (var o in offers)
			GD.Print($"[BountyLogic] poster: {o.TargetName} on the {o.RoadLabel}, tier {o.Tier}, ${o.RewardUsd}");
		if (offers.Count == 0)
		{
			GD.PrintErr("[BountyLogic] no posters on the board.");
			return;
		}

		// Lowest-tier poster keeps the probe's forced fight cheap to resolve.
		var pick = offers.OrderBy(o => o.Tier).First();
		if (!session.TryAcceptBounty(defs, pick.BountyId, out var acceptErr))
		{
			GD.PrintErr($"[BountyLogic] accept FAILED: {acceptErr}");
			return;
		}
		GD.Print($"[BountyLogic] accepted: {pick.TargetName}, contract={(session.GetActiveBountyContract() != null ? "ACTIVE" : "null")}");

		if (!session.TryTravelTo(defs, pick.RoadToCityId, out var ambushTier, out var travelErr))
		{
			GD.PrintErr($"[BountyLogic] travel FAILED: {travelErr}");
			return;
		}
		GD.Print($"[BountyLogic] traveled haunted leg: bountyTier={session.LastLegBountyTier} (ambushTier={ambushTier}, expected 0)");
		if (session.LastLegBountyTier <= 0)
		{
			GD.PrintErr("[BountyLogic] bounty trigger did NOT fire on the haunted leg.");
			return;
		}

		if (!session.TryStartBountyHunt(out var startErr))
		{
			GD.PrintErr($"[BountyLogic] bounty hunt start FAILED: {startErr}");
			return;
		}
		var enc = session.GetCurrentEncounter();
		GD.Print($"[BountyLogic] encounter staged: IsBountyHunt={enc?.IsBountyHunt}, target={enc?.BountyTargetName}, reward=${enc?.BountyRewardUsd}, tier={enc?.Tier}");

		var veh = session.GetActiveVehicle();
		if (veh == null || enc is not { Outcome: null })
		{
			GD.PrintErr("[BountyLogic] active vehicle/encounter missing.");
			return;
		}

		var moneyBefore = session.GetMoneyUsd();
		if (!session.ResolveArenaEncounterRealtime("win", veh, 0,
				session.GetDriverArmor(), session.GetDriverHp(),
				new[] { "BountyLogic probe win." }, out _, out var resErr))
		{
			GD.PrintErr($"[BountyLogic] resolve FAILED: {resErr}");
			return;
		}
		var moneyAfter = session.GetMoneyUsd();
		GD.Print($"[BountyLogic] WIN: money ${moneyBefore} -> ${moneyAfter} (head worth ${pick.RewardUsd}), contract={(session.GetActiveBountyContract() == null ? "CLEARED" : "STILL ACTIVE")}, lifetime={session.Save.Player.BountiesClaimed}");

		var rerolled = session.GetBountyOffers(defs);
		GD.Print($"[BountyLogic] new board at {session.Save.Player.CurrentCityId}: {rerolled.Count} posters, first={(rerolled.Count > 0 ? rerolled[0].TargetName : "-")}");
	}

	/// <summary>Accept the first WANTED poster (session API) so the active-bounty UI can be captured.</summary>
	private static void AcceptFirstBounty()
	{
		var app = App.Instance;
		if (app == null
			|| !app.Services.TryGet<GameSession>(out var session) || session == null
			|| !app.Services.TryGet<WastelandSurvivor.Core.IO.DefDatabase>(out var defs) || defs == null)
		{
			GD.PrintErr("[ShotHarness] GameSession/DefDatabase missing; cannot stage bounty.");
			return;
		}

		var offer = session.GetBountyOffers(defs).FirstOrDefault();
		if (offer == null)
		{
			GD.PrintErr("[ShotHarness] no bounty posters on the board.");
			return;
		}

		if (!session.TryAcceptBounty(defs, offer.BountyId, out var err))
			GD.PrintErr($"[ShotHarness] bounty accept FAILED: {err}");
		else
			GD.Print($"[ShotHarness] bounty staged: {offer.TargetName} on the {offer.RoadLabel} (tier {offer.Tier}, ${offer.RewardUsd})");
	}

	/// <summary>
	/// Accept the first freight offer that fits the active chain (session API), so the Ops-tab
	/// freight ledger's active-contract state can be captured.
	/// </summary>
	private static void AcceptFirstFreightOffer()
	{
		var app = App.Instance;
		if (app == null
			|| !app.Services.TryGet<GameSession>(out var session) || session == null
			|| !app.Services.TryGet<WastelandSurvivor.Core.IO.DefDatabase>(out var defs) || defs == null)
		{
			GD.PrintErr("[ShotHarness] GameSession/DefDatabase missing; cannot stage freight contract.");
			return;
		}

		var free = session.GetFreightFreeStorageUnits(defs);
		var offer = session.GetFreightOffers(defs).FirstOrDefault(o => o.Units <= free);
		if (offer == null)
		{
			GD.PrintErr($"[ShotHarness] no freight offer fits the chain (free={free}u).");
			return;
		}

		if (!session.TryAcceptFreightOffer(defs, offer.OfferId, out var err))
			GD.PrintErr($"[ShotHarness] freight accept FAILED: {err}");
		else
			GD.Print($"[ShotHarness] freight staged: {offer.Units}u {offer.CargoDisplayName} -> {offer.DestDisplayName} (${offer.PayoutUsd})");
	}

	/// <summary>
	/// Enter the city tournament and win round 1 via the session API (no combat sim), so the
	/// briefing's bracket strip can be captured in its mid-bracket state.
	/// </summary>
	private static void WinOneTournamentRound()
	{
		var app = App.Instance;
		if (app == null || !app.Services.TryGet<GameSession>(out var session) || session == null)
		{
			GD.PrintErr("[ShotHarness] GameSession missing; cannot stage tournament bracket.");
			return;
		}

		if (!session.TryEnterTournament(out var err))
		{
			GD.PrintErr($"[ShotHarness] tournament enter FAILED: {err}");
			return;
		}

		var veh = session.GetActiveVehicle();
		if (veh == null || session.GetCurrentEncounter() is not { Outcome: null })
		{
			GD.PrintErr("[ShotHarness] tournament round-1 encounter missing.");
			return;
		}

		if (!session.ResolveArenaEncounterRealtime("win", veh, 0,
				session.GetDriverArmor(), session.GetDriverHp(),
				new[] { "Bracket-shot probe win." }, out _, out var resErr))
		{
			GD.PrintErr($"[ShotHarness] round resolve FAILED: {resErr}");
			return;
		}

		var t = session.GetActiveTournament();
		GD.Print($"[ShotHarness] bracket staged: round={(t?.CurrentRound ?? -1) + 1}/{t?.RoundTiers.Length ?? 0} winnings=${t?.WinningsUsd ?? 0}");
	}

	private static void RunTournamentLogicProbe()
	{
		var app = App.Instance;
		if (app == null || !app.Services.TryGet<GameSession>(out var session) || session == null)
		{
			GD.PrintErr("[TournLogic] GameSession missing.");
			return;
		}

		session.TryAddMoney(3000, out _, out _);
		var startMoney = session.GetMoneyUsd();
		GD.Print($"[TournLogic] bankroll=${startMoney}");

		if (!session.TryEnterTournament(out var err))
		{
			GD.PrintErr($"[TournLogic] enter FAILED: {err}");
			return;
		}

		var t = session.GetActiveTournament();
		GD.Print($"[TournLogic] entered: rounds=[{string.Join(",", t?.RoundTiers ?? System.Array.Empty<int>())}] fee=${t?.EntryFeeUsd} money=${session.GetMoneyUsd()}");

		for (var guard = 0; guard < 6 && session.GetActiveTournament() != null; guard++)
		{
			var enc = session.GetCurrentEncounter();
			if (enc is not { Outcome: null })
			{
				if (!session.TryStartNextTournamentRound(out var nextErr))
				{
					GD.PrintErr($"[TournLogic] next round FAILED: {nextErr}");
					return;
				}
				enc = session.GetCurrentEncounter();
			}

			var veh = session.GetActiveVehicle();
			if (veh == null || enc == null)
			{
				GD.PrintErr("[TournLogic] active vehicle/encounter missing.");
				return;
			}

			if (!session.ResolveArenaEncounterRealtime("win", veh, 0,
					session.GetDriverArmor(), session.GetDriverHp(),
					new[] { "TournLogic probe win." }, out _, out var resErr))
			{
				GD.PrintErr($"[TournLogic] resolve FAILED: {resErr}");
				return;
			}

			var after = session.GetActiveTournament();
			GD.Print(after == null
				? $"[TournLogic] CHAMPION! money=${session.GetMoneyUsd()} (net {session.GetMoneyUsd() - startMoney:+#;-#;0})"
				: $"[TournLogic] round won (tier {enc.Tier}); nextRound={after.CurrentRound + 1}/{after.RoundTiers.Length} winnings=${after.WinningsUsd} money=${session.GetMoneyUsd()}");
		}

		if (session.GetActiveTournament() != null)
			GD.PrintErr("[TournLogic] bracket never completed (guard exhausted).");
	}

	private static ArenaRealtimeView? FindArenaView(Node root)
	{
		if (root is ArenaRealtimeView v) return v;
		foreach (var child in root.GetChildren())
		{
			if (child is Node n && FindArenaView(n) is { } found)
				return found;
		}
		return null;
	}

	private void OpenPauseMenu()
	{
		var pause = GetTree().Root.FindChild("PauseMenuOverlay", true, false) as PauseMenuOverlay;
		pause?.Open();
	}

	private void EnsureOutDir()
	{
		try
		{
			var abs = ProjectSettings.GlobalizePath(OutDir);
			System.IO.Directory.CreateDirectory(abs);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[ShotHarness] Could not create out dir '{OutDir}': {ex.Message}");
		}
	}

	private void Capture(string label)
	{
		try
		{
			var img = GetViewport().GetTexture().GetImage();
			_shotCounter++;
			var path = $"{OutDir.TrimEnd('/', '\\')}/{_shotCounter:00}_{label}.png";
			var err = img.SavePng(path);
			GD.Print(err == Error.Ok
				? $"[ShotHarness] Saved {path}"
				: $"[ShotHarness] FAILED to save {path}: {err}");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[ShotHarness] Capture '{label}' failed: {ex.Message}");
		}
	}

	private void Quit()
	{
		GD.Print("[ShotHarness] Done; quitting.");
		GetTree().Quit();
	}

	/// <summary>Stages the travel-journey interstitial with a representative ambush leg for filming.</summary>
	private void StageJourneyOverlay()
	{
		try
		{
			var app = App.Instance;
			if (app == null) return;
			if (!app.Services.TryGet<GameUiKit.UI.ScreenRouter>(out var router) || router?.Current is not Control host)
				return;

			var info = new TravelJourneyOverlay.JourneyInfo
			{
				FromName = "Detroit",
				ToName = "Toledo",
				DistanceKm = 94f,
				FuelUnitsBurned = 11.3f,
				ArrivalFuelUnits = 27.4f,
				FuelCapacityUnits = 45f,
				TollUsd = 40,
				StationStop = true,
				StationCostUsd = 36,
				FreightPayout = 0,
				InterruptKind = "ambush",
				VehicleIcon = null,
			};
			TravelJourneyOverlay.Show(host, info, () => GD.Print("[ShotHarness] journey overlay finished"));
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[ShotHarness] StageJourneyOverlay failed: {ex.Message}");
		}
	}

	// ---------------------------------------------------------------------------------------------
	// Engine-audio sweep probe (--shot=enginesweep): records the Engines bus to a WAV while a
	// scripted throttle profile runs, so engine-audio changes can be verified numerically (pitch
	// continuity / no tone stabs) without a human listener. Analysis happens offline on the WAV.
	// ---------------------------------------------------------------------------------------------
	private AudioEffectRecord? _sweepRecord;
	private int _sweepRecordBus = -1;
	private int _sweepRecordEffectIdx = -1;

	private void StartEngineRecording()
	{
		try
		{
			var idx = AudioServer.GetBusIndex("Engines");
			if (idx < 0)
			{
				GD.PrintErr("[Sweep] Engines bus not found; recording skipped.");
				return;
			}
			_sweepRecord = new AudioEffectRecord();
			_sweepRecordBus = idx;
			_sweepRecordEffectIdx = AudioServer.GetBusEffectCount(idx);
			AudioServer.AddBusEffect(idx, _sweepRecord);
			_sweepRecord.SetRecordingActive(true);
			GD.Print("[Sweep] Recording Engines bus...");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[Sweep] Failed to start recording: {ex.Message}");
		}
	}

	private void StopEngineRecordingAndSave()
	{
		try
		{
			if (_sweepRecord == null) return;
			_sweepRecord.SetRecordingActive(false);
			var rec = _sweepRecord.GetRecording();
			if (rec != null)
			{
				var path = $"{OutDir.TrimEnd('/', '\\')}/enginesweep.wav";
				var err = rec.SaveToWav(path);
				GD.Print($"[Sweep] Saved {path} err={err} lenSec={rec.GetLength():0.00}");
			}
			else
			{
				GD.PrintErr("[Sweep] No recording captured.");
			}
			if (_sweepRecordBus >= 0 && _sweepRecordEffectIdx >= 0)
				AudioServer.RemoveBusEffect(_sweepRecordBus, _sweepRecordEffectIdx);
			_sweepRecord = null;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[Sweep] Failed to save recording: {ex.Message}");
		}
	}
}
