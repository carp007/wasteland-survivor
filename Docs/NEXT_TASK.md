# Next Tasks (keep this small and current)

## Loop #6 (2026-07-22) shipped builds 182-183+ — see PROJECT_STATE.md / CHANGELOG.md.
Play-test response loop. Headlines: real TITLE SCREEN main menu (art never truncates, NEW GAME
wipe flow), engine-audio "organ keys" fixed and proven via WAV A/B (`--shot=enginesweep`),
underglow ground lines removed, weapon models slimmed to hardware proportions, store rows show
real 3D showroom thumbnails, STRUCTURED ARENA LAYOUTS (PILLARS/RING/LANES/CROSSBUNKERS per
city+tier), AI DRIVING OVERHAUL (PD steering, planned detours, cover play — accuracy 43%→54%,
recoveries ~0), TRAVEL JOURNEY interstitial (skippable road-trip beat with receipts + event
interrupts), ON-FOOT PERSONAL WEAPONS stages 1+2 (four guns + armor ladder at the Clinic &
Outfitter; fire key works on foot; clones wake armed), cars grounded (deeper gold, automotive
paint response), city hub headline is now the CITY NAME.

## FIRST: human play-test (nothing since build 125 has human minutes on it)
**Restart the Godot editor first** (new assemblies + imports). Copy
`%APPDATA%\Godot\app_userdata\Wasteland Survivor\savegame.json` somewhere safe first.

### Golden path 1 — The new front door
Launch → studio card → title screen: full "WASTELAND SURVIVOR" lettering visible? CONTINUE lands
in the city; NEW GAME warns before wiping. City hub header reads DETROIT.

### Golden path 2 — Engine + travel feel
Drive the arena: does the engine finally sound like one machine revving through gears (no organ)?
Then travel a leg: the road-trip interstitial should play (skippable, receipts ticking) and cut
to RAIDERS INBOUND when a leg rolls hot.

### Golden path 3 — Arena structure + smarter drivers
Fight t1→t5: each venue should have a READABLE layout (ring wall, lanes, pillar blocks, corner
bunkers) and enemies should drive like they mean it — smooth arcs, cutting through gates, ducking
behind cover when hurt. Yellow/red ground lines beside cars must be GONE.

### Golden path 4 — The sidearm
Clinic & Outfitter: buy nothing — you already carry the free Rusthound 9mm (60 rds). In a fight,
stop, exit (E), hold fire: pistol tracers at the locked enemy, ammo readout under your vitals.
Then buy the Doorbreaker 12g and feel the difference point-blank.

## Judge round (loop #6): visual B-, gameplay B- — all 7 confirmed P1s fixed in build 184,
## most P2s + the loop-5 t4 carry-over closed in build 185. Remaining backlog for loop #7:
- ECONOMY (design decisions, deliberately left to the designer): vest swap restores full AP →
  downgrading a shredded vest undercuts the repair service, and trade-in excess is forfeited
  silently; shotgun per-pellet Math.Max(1,...) floor doubles its designed anti-hull chip; shared
  ammo-pool cap quirk on equip-switch (pistol↔SMG share pammo_9mm with different caps).
- Garage Service Bay/Upgrades tabs half-empty; travel map legend is a cryptic single text run;
  section-caved toasts can overlap the salvage-phase header; player car still reads simple at
  combat zoom (next step would be real per-class detail meshes, not shader work).
- On-foot probe: aim/range/cone/whiff/cover-block all verified live; a clean HULL-hit assert is
  still racy (walking driver vs driving car) — consider a deterministic close-range staging
  (spawn override) if it matters. Post-185 telemetry: t3 LANES = 47 enemy shots (was 11-13),
  t4 SIDEWINDER = 57 shots / 8 part hits (was 10/2).

## Known debt / watch items
- Journey overlay: interrupt timing/pacing is a first cut; Stage 2 = HIGHWAY combat venue for
  road fights (plan in Docs/TRAVEL_BUILDOUT_PLAN.md).
- On-foot stage 3 backlog (per-weapon SFX, salvage-phase fire policy, AI bail-outs) in
  Docs/ONFOOT_COMBAT_PLAN.md; open design questions listed there.
- t3 raw enemy fire volume sits below the old no-cover baseline (13-17 vs 28) — quality metrics
  up (accuracy 54%, wasted shots 2); bump t3 firePauseSeconds 0.75→0.5 if human minutes want more
  pressure. t4 SIDEWINDER hit conversion still the loop-5 open item.
- AI detour planner over-approximates thin barriers as circles (occasionally wide detours).
- Personal-weapon fire SFX reuses the MG sample at -10dB (stage 3 item).
- Legacy audio provenance (6 files) still pre-distribution debt.

## Verification checklist (fast)
- `dotnet build "Wasteland Survivor.csproj"` → 0 errors.
- Harness additions this loop: `--shot=title`, `--shot=journey`, `--shot=driverstore`,
  `--shot=onfoot`, `--shot=enginesweep` (records Engines bus to WAV). Existing:
  arena t1-5 / bountylogic / freightlogic / tournament / scavenge / haul / city / garage /
  workshop / store / pause / splash / cityfreight.
- `[ArenaLayout]` log line names each venue's archetype; `[AiDbg]` totals line carries
  recovN/wallS/jerk/det counters.
