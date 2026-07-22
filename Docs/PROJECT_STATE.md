## 2026-07-22 build loop #6 (builds 182-183+) — play-test response: front door, structure, drivers, the road, the sidearm
- **Real TITLE SCREEN / main menu** (`TitleScreenView`): studio splash → full-bleed title art
  (new UiKit `CoverArtControl` cover-crops with a TOP anchor so the lettering never truncates
  again; slow Ken Burns drift) with CONTINUE / NEW GAME (confirm + full wipe via
  `GameSession.ResetToNewGame`) / EXIT. `--shot=title`.
- **Engine audio "organ keys" fixed and PROVEN**: 5-layer crossfade now rate-matches (each RPM
  layer pitch-bends toward current RPM — `UseRpmPitchTracking`, ratio 2.3) and a rolling
  drivetrain keeps drive layers engaged (AI throttle pulses no longer stab tones on/off). New
  `--shot=enginesweep` records the Engines bus to WAV through a scripted throttle profile;
  A/B analysis: legacy locks onto 217↔260 Hz plateaus (58 pitch jumps >12%), fixed glides
  195→235 Hz continuously.
- **Identity underglow strips REMOVED** (the "yellow/red lines beside cars"); contact shadow +
  facing wedge + hull trim + target brackets carry identity.
- **Weapon proportions**: `weapon_visuals.json` gained per-axis `WidthScale`/`HeightScale`
  (applied at the weapon root, re-applied after mount snap) + trimmed lengths — mounted guns
  read as hardware, not chunky blaster props.
- **Store rows show the real car**: `VehiclePortraitIconFactory` freezes the 3D showroom
  turntable once per def into a cached thumbnail; dealership rows (and the travel overlay)
  use it, silhouettes remain the loading fallback.
- **STRUCTURED ARENAS**: four deterministic interior archetypes (PILLARS / RING / LANES /
  CROSSBUNKERS) seeded by city+tier; obstacle registry (`ArenaWorld.ObstacleFootprints`).
- **AI DRIVING OVERHAUL**: PD steering + slew-limited wheel, planned detours off the obstacle
  registry, LOS-repossession through cover gates, hysteretic stance changes,
  reposition-through-cover; 9 new `arena_ai.json` knobs. Accuracy 43%→54%, recoveries ~0,
  barrier-wasted shots 12→2; loop-5 cadence fixes intact.
- **TRAVEL JOURNEY INTERSTITIAL** (`TravelJourneyOverlay` from `CityShell.SetCity` →
  `PresentTravelOutcome`): skippable scrolling-highway beat with km ticker, fuel needle,
  receipts, flavor lines, and event-interrupt flash. Roadmap: Docs/TRAVEL_BUILDOUT_PLAN.md
  (Stage 2 = HIGHWAY combat venue).
- **ON-FOOT PERSONAL WEAPONS (stages 1+2)**: spec section in MASTER_GAME_SPEC; four
  `PersonalWeaponDefinition` defs; Driver Store sells weapons/ammo + two new vest tiers
  (ladder 50/58/65/85/110 AP); on-foot fire key shoots the equipped weapon at the locked
  target through the normal locational-damage path; profile-carried ammo pools commit at
  match resolve; clones always wake armed. Docs/ONFOOT_COMBAT_PLAN.md.
- **Cars grounded**: hull-shader desaturation + side-panel wear + automotive paint response;
  player gold deepened. (Play-test note: "arcade but fairly realistic".)

## 2026-07-22 build loop #5 (builds 172-181) — bounties, computer ladder, combat truth, the show
- **WANTED BOUNTIES** (save v12): every city's Ops tab posts 2 named raiders (greenhorn at the
  road's own raider tier + marquee at +1; per-tier intel lines), each haunting one road leg;
  traveling it forces the fight ("BOUNTY SIGHTED", preempts other road events, full capture
  stakes); win pays the head price on top of a 45% highway purse; lose/flee keeps the target at
  large. Haunted leg drawn as a pulsing red reticle on the overworld map + a danger line on the
  route card. `SessionBounties`; `--shot=bountylogic`; cityfreight harness stages a live bounty.
- **Targeting computers honor the spec**: beyond-cap weapons degrade to FIXED-ANGLE (10° cone
  opportunistic fire, turrets lock to neutral, missiles dumb-fire; [FIXED] HUD tags) instead of
  going dark — and weapon slots 4-5 now exist at all (they didn't; no computer could ever light a
  4th weapon). Ladder: tc_basic $800 (starters ship it) → tc_advanced $2,700 → NEW tc_tactical
  4-group $6,800 (CONVOY fields it: 2→4 firing slots measured).
- **Missile truthfulness**: fuse fires at the hull-sphere crossing, falloff measured from hull
  surface — locked hits land 28-33 on a base-30 sheet (was a constant 14-16 since missiles
  shipped); blasts route 65% into the facing section. Counterplay: TrackingStrength 0.45
  (≈293°/s), MISSILE INBOUND hot-red toast + danger cue on every enemy launch.
- **Difficulty curve truthed then re-centered**: t3 unstuck (8→24-26 enemy shots; minFireDot/
  attack-run/disengage) then de-spiked (missile rack 6→3: incoming 246-290 → 161-186, probe
  driver survives); all-tier bumper-contact breakaway kills the t5 ram-grind; t4 weapons-silence
  root-caused (astern throttle-floor override + ±π steer flip-flop — OUTRIDER 5→34-40 shots).
  Gameplay quick wins: starter Compact runs its intended Gas V6 (was silently on the 90 kW
  electric fallback — dead vehicle-side AllowedEngineClasses vocabulary deleted), post-matrix
  damage logs w/ matchup notes, positional mine owner-grace, 20mm priced ($5 — was free-riding),
  AP = 1.5x ball at 0.95x multipliers, freight pays load x distance (far ~$2,450, near-spam -25%).
- **Combat VFX 2.0**: tapered blooming tracers (gold/red-orange), kill white-flash + expanding
  shockwave annulus + ember fountain, pulsing mine hazard rings, volumetric tension-colored tow
  cable with hook. **LIGHT THE SHOW**: floor value zones + city-code tier stencils ("PIt-05") +
  lane numerals + in-bowl colored pools + jumbotron wash; tier-spectacle scalar (t1 scrappy pit →
  t5 gold title ring/crown ticks/gold pools). **Yards**: hull-detailed derelicts, crushed-car
  piles, dressed staging pen, yard crane w/ airborne wreck; painted-metal gate booms; close-range
  crowd figures. UI: toast feed upper-left (cap 2), one 3D-portrait standard (store details/
  workshop/garage), locked cards keep readable labels.
- **Audio reactivity**: stadium concrete-bowl reverb (yards/highways dry), danger low-pass +
  duck sweep below 35% driver HP (auto-decays), MISSILE INBOUND cue.
- **Judge round 11**: visual B- → B (t5 C+→B, haul C+→B, menus A-), gameplay B- → B (spec
  fidelity B+); all round-10 findings CONFIRMED FIXED. Open: t4 SIDEWINDER gun conversion,
  driver-kill scaling at t3+, perimeter fill/scavenge midfield (in flight at loop close).

## 2026-07-21 build loop #4 (builds 141-161) — hull fidelity, venue kinds, new classes, freight
- **Vehicle fidelity P0 closed** (`VehicleHullDetailer`): top-projected per-archetype hull detail
  shader on the single-mesh Kenney bodies (smoked-glass cabin zones, panel seams, two-tone roof,
  bed ribs, stripes, rust, edge-AO, skirt shading) — archetype from model FILENAME (sedan/sports/
  van/truck/industrial/boxtop/suv) because enemy presets override models independently of class.
  Weapon models repainted gunmetal; the recurring "white ball" was the muzzle core-pop sphere
  blooming (now small additive pop).
- **ArenaVenueKind** (Stadium | SalvageYard): scavenge sites, salvage-crew raids, and interceptions
  now build the bowl as a wreck yard — rust-tinted derelict husks (with obstacle collision), junk
  heaps, tire stacks; stadium shell/pennants skipped, floor markings shader-suppressed
  (`markings_strength 0`), crowd audio gated off (`AmbienceDirector.CrowdPresent`).
- **Cross-lighting**: north floodlight pair runs a contrast temperature vs the city palette + a low
  north fill — breaks the single-amber-wash monotony. Speed/RPM are ticked instrument gauges
  (RPM sweeps green→gold→red).
- **New classes**: SUV (veh_suv, Composite, 120° top turret) and HEAVY TRUCK (veh_heavy_truck,
  Steel, 6 mounts incl. side guns), Diesel V8 engine; enum values appended after Trailer.
- **FREIGHT CONTRACTS** (save v11): every city's Travel tab hosts a FREIGHT OFFICE — 3 deterministic
  offers (near/mid/far; reroll seed = `Player.FreightContractsDelivered`); accepting loads real
  cargo units into the active chain's `CargoInventory` (capacity-checked, distributed active-first),
  which weigh the chain via the normal mass math; +15% ambush chance while loaded
  (`GameBalance.FreightAmbushChanceBonus`); delivery auto-completes on arrival (payout atomic with
  the travel commit). `SessionFreight` service; `--shot=freightlogic` probes the whole loop.
- Crowd swells fire on destruction toasts + kills (1.4s cooldown); gear shifts clunk.
- **Builds 146-147 (judge round 6 response)**: SUV/heavy-truck starter presets + OUTRIDER/CONVOY
  enemy variants + `--shot-vehicle=` harness flag; AI **attack-run cadence** fixes kite/orbit fire
  starvation (T2/T4/T5 fire counts ~3x; t5 kills the harness bot); driver overflow is
  section-dependent (front 25% / sides 55% / rear-top-under 75% / tires 30%) closing the 6-20s
  driver-tunnel TTK; `ImpactFlashVfx3D` soft additive hit flashes + textured billboard smoke;
  pennant bunting re-routed to the perimeter square (the mid-frame X is gone); road-ambush wins
  pay 45%/no-ammo (freight ambush bonus is real risk now); tournament entry $100/tier at low
  brackets + champion salvage-rights scrap; freight offers show storage feasibility; freight
  destination is a pulsing crate glyph on the overworld map.
- **Builds 148-154 (judge round 7 response; re-grade C+ → B-, all round-6 fixes confirmed)**:
  combat HUD near-opaque + compact RPM form; live 3D turntable portraits (`VehiclePortraitViewport`)
  in Garage hero card + Workshop showcase (vector canvas = fallback); procedural FLATBED TRAILER
  proxy (trailers rendered as generic cars on the hitch before); floor got an 11m concrete panel
  grid + hazard-chevron collar + faded diagonal spokes; match-end dialog backed/roomier with the
  toast stack below it; wrecks vent a standing smolder column + ember sparks; floodlight beam
  cones cut (read as translucent slabs from the top-down camera); Service Bay shows locational
  integrity ("LF 0/11AP 9/16HP BREACHED"); on-foot driver gets a shadow disc + gold identity ring.
- **Builds 156-161 (judge round 9 response + class-ladder top-out)**: salvage yards get scattered
  ground litter (30 deterministic scrap plates/chunks, exit corridor kept clear — the "barren yard"
  note is closed); **SEMI TRACTOR** joins the dealership ($21,500, five mounts incl. a 200° top
  ring) with the **Diesel V12** (400 kW / 1,800 Nm) as the chained-towing enabler (the kit's
  "tractor.glb" was a FARM tractor — rejected; flat-nose truck chassis at 5.8m); **floor contrast
  pass** (slab tones quantized to 3 poured-concrete steps, doubled seam grooves, ~1-in-5 painted
  wear decals, radius-skewed chevron arrows); **wreck "maroon aura" root-caused** (fireball's
  oversized red outer shell + smoke puffs ballooning into flat sheets — `SmokePuff3D` gained
  growthRate/riseDamping; wrecks now vent a climbing smolder column over scorch decals with embers);
  turntable portraits sit on a graded shadow-pool **pedestal rig** (steel rim ring + warm ground
  bounce; workshop inherits it); garage Service Bay/Upgrades tabs open with **stat-tile rows** +
  carded reports; store DETAILS pane gets a real **empty state** (watermark glyph + flavor card);
  tournament briefing renders a 3-node **BRACKET strip** (won/current/upcoming + running purse +
  champion bonus); City Ops gains a **FREIGHT LEDGER** card (in-transit status or lifetime
  deliveries); Workshop engine selector states power/torque/fuel/efficiency/**tow capacity**
  (the V12's 8,000 kg pitch visible at purchase); full 9-target regression sweep on build 160:
  **9/9 PASS** (arena t1/t5, freight, tournament, scavenge, haul, garage, store, city Ops) —
  zero regressions across the loop's 20 builds.

## 2026-07-02 build loop #2 (builds 126-127) — tournaments, combat truth, campaign arc
- **Arena tournaments** (spec non-negotiable #1): 3-round gauntlet per arena city at tiers [max-2..max], entry $150×tier, per-round purses + champion bonus $400×tier, pit-crew 35% patch between rounds (no restock/garage), elimination on loss/flee keeps earned purses, death anywhere mid-bracket forfeits. `SaveGameState.ActiveTournament` (`TournamentState`, save v9), `SessionEncounters.TryEnterTournament/TryStartNextTournamentRound/AbandonTournament`, briefing ENTER TOURNAMENT card + ROUND k/N strip + Next-Round start action, `--shot=tournament` harness target.
- **Missile-salvo root cause** (build 126): code-constructed `ValueBar` (hull bar) threw in `RefreshStats()` unwinding `TryFire` before `SetSlotCooldown`; `ValueBar` now self-builds children, cooldowns commit before launch/impact. Blast driver damage obeys the overflow rule (60% via destroyed undercarriage + 10%/5% matrix-scaled chip) — the flat 65% pre-armor pass-through (6 missiles killed anything in 13s) is gone.
- **Difficulty/economy**: enemy pools tier-scale (HP roll 35-130 by tier is the max; driver armor 50-100); tier 3+ presets field real missiles + plating; per-tier enemy chassis/color identity (taxi/police/van/racer/garbage-truck from Kenney Car Kit via preset `VisualModelPathOverride`/`BodyColorHex`); mount `ArcDegrees` enforced (wrap-aware turret clamps). Purses ~100/280/520/800/1150 ±30% (floor $180); ransom = 22% of vehicle value + tier markup; ammo `DamageMultiplier` live (20mm 1.5×); win ammo matches loadout; oil/smoke refill policies fixed; plating +3/+6/+10 armor per section, +60-260 kg, priced in USD ($400/900/1600). Vehicle-disable win: all-but-one tires gone or all cardinal sections destroyed → enemy surrenders (intact hull = better salvage).
- **Campaign arc**: city `ArenaMaxTier` ladder Detroit/Toledo 2, Saginaw 1, Erie/Fort Wayne 3, Chicago 4, Pittsburgh 5, Cleveland none; city `BackdropPath` + deterministic procedural skylines for cities without art (`CityBackdropArt`); Parts Store reuses the darkened city backdrop.

## 2026-07-02 five-hour build loop, iteration 1 (build 123) — overworld, capture economy, asset wave
- **8-city overworld** (spec): `Data/Defs/Cities/*.json` → `DefDatabase.Cities` (Detroit/Saginaw/Toledo/Cleveland/Erie/Pittsburgh/Fort Wayne/Chicago; per-city arena tier caps, clone facilities, fuel price multipliers, road graph with per-road ambush profiles + loader validation). Travel is per-leg (`SessionWorld.TryTravelTo(defs, cityId, ...)`); City Hub Travel tab is generated route cards + FUEL STATION strip; arena availability is def-driven.
- **Fuel system** (spec): `VehicleDefinition.FuelCapacityUnits`, `TravelMath` (distance × mass / engine efficiency), refuel service (city price multiplier), Garage fuel tile, save v8 migration normalizes legacy tanks to full.
- **Capture/reclaim loop** (spec non-negotiable): arena loss → vehicle ownership suspends into `SaveGameState.CapturedVehicles` (instance + damage preserved in `Vehicles`); clone wakes at `LastRespawnCityId`; City Ops offers pay-ransom (`GameBalance.GetVehicleRansomUsd`) or interception (`SessionEncounters.TryStartInterceptionEncounter` — win returns the vehicle via `EncounterState.RecoverVehicleInstanceId`). **Memory upload** service ($50) at clone-facility cities.
- **Combat correctness** (instrumented `[ShotDebug]` harness runs): per-slot weapon cooldowns on `VehiclePawn` (shared cooldown starved fire groups 2/3 — mines/missiles never fired during MG bursts); `PlaySfx3D`/mine markers/skid marks set GlobalPosition before AddChild (3D audio played at world origin); player spread 0.045→0.028 rad + ballistic range falloff past 30m.
- **Asset wave (~250 CC0 files, attribution in Docs/Audio/ATTRIBUTION_AND_LICENSES.md)**: per-class Kenney Car Kit vehicle models via `VehicleDefinition.VisualModelPath` (flat team tint, steering front wheels, dangerous scene.gltf fallback deleted); Kenney Blaster Kit weapon models + per-weapon fire sounds via `weapon_visuals.json`; impact/explosion audio with variant rotation; `UiSfx` (global button click/hover + purchase/error), `MusicDirector.PlayMatchStinger`, `AmbienceDirector` (city hum / arena wind); Racing Kit stadium dressing hugging the arena walls (±57-60) + Poly Haven glTF props (no Blender dependency).
- **Tow capacity** from engine power (`VehicleMassMath.ComputeTowCapacityKg` — compact can't drag a war rig). Roadside-merchant travel event rolls (UI hookup pending). Dev-voice copy replaced with diegetic flavor.

## 2026-06-11 eight-hour build run (builds 121–122) — spec pillars landed
- **Parts Store + ownership economy** (spec: city Store): `StoreView` screen (City Hub → Open Parts Store) sells every priced weapon/engine/computer; `PlayerProfileState.PartsInventory` + `SessionStore`; the Workshop now only installs OWNED parts (install consumes, uninstall returns, blocked applies point at the store). Sell-back at 55%.
- **Arena tiers 4–5** (SIDEWINDER sports, JUGGERNAUT war truck, both with the new 20mm Autocannon $3,800) and **tier-scaled purses** (~$120-360 ×tier) so progression funds upgrades.
- **Clone respawn with stakes** (spec pillar): driver death = $150 clone fee + facility messaging; money/garage persist.
- **Overworld-lite travel** (spec: roads + encounters): travel costs $40, 45% raider ambush rolls a tier 1–2 fight via a ROAD AMBUSH modal before arrival; Cleveland has **no arena** (city service variety per spec).
- **Audio**: Music bus + Settings slider + `MusicDirector` switching city/combat tracks (Kevin MacLeod CC-BY, attribution recorded); CC0 Poly Haven textures staged (corrugated iron, gravel) for future road-encounter art.

## 2026-06-11 fifty-iteration improvement loop (builds 118–120) — screenshot-driven
User direction locked in: **the high RTS / "toy cars" camera (offset 0,29,23) is part of the game's identity — never change the zoom.** All readability work scales VFX/markers up instead.
- **Bugs found on camera:** vehicle paint variants never applied (visible meshes use bare material names; regex fixed — player yellow, enemies dark red); "white cards" on vehicles were box-mesh smoke/flash VFX (now spheres); drivers died through intact armor in ~8s under automatic MG fire (per-hit chip-through removed per spec — driver damage now only from direct hits or overflow through a destroyed section).
- **HUD:** ValueBar custom-drawn (rounded/bordered, fill-cap line, label shadows); km/h speed gauge; radar is a real scope (rings, ticks, sweep, heading wedge); display-font hierarchy across all HUD panels.
- **Screens:** briefing has named tier intel cards (SCRAPPER/ROADRUNNER/WARRIG); gold-underline tabs; themed sliders/scrollbars; PAUSED title + build-id footer; gold-edged post-round banner; all garage/workshop/city tab pages verified inheriting the design system.
- **Arena:** painted center ring + start hashes; brighter stadium light pools; damage smoke/fire rescaled for RTS height (burning enemies read clearly); win/loss flows verified on-camera.
- Harness now pages drawer tabs, captures the splash, and drops mines mid-fight. Loop log: `.shots/LOOP_STATE.md`.

## 2026-06-11 visual overhaul (build 117) — screenshot-driven
A second same-day pass, this time iterated against real rendered frames via the new screenshot harness (`--shot=` CLI mode; see ASSISTANT_PLAYBOOK "Visual iteration"). Canonical changes:
- **Arena visibility fixed for real**: the build-103 blackout curtains were occluding the chase camera from the south start lane (the actual "fog of war" complaint). Curtains + fog overlay deleted; out-of-bounds darkness now comes from the arena `WorldEnvironment` near-black background.
- **Arena presentation**: camera at ~21m (cars read as cars) with combat lookahead + impact shake; sun shadows; dark neutral asphalt; PolyHaven rock walls; real obstacle prop pack loading (path fixed to `Assets/Models/ArenaObstacles`) with auto-fit/ground-snap; center cover moved off the spawn axis (open jousting lane).
- **Combat feel**: automatic-cadence .50cal (150ms/5dmg), heavy salvo missiles (2200ms/30dmg), emissive bullet bolts/tracers/flashes that bloom, gunmetal weapon proxies.
- **UI design system**: bundled Rajdhani/Inter fonts; `GameUiTheme` + premium drawer styles rebuilt as flat dark command-console language (hairline borders, tonal sections, accent-only states); display-font headings; banner art retired. City/Garage/Workshop/Arena briefing/Pause all pick this up.
- **Tooling**: screenshot runs are sandboxed to `user://savegame.screenshot.json` and never touch the real save.

## 2026-06-11 iteration pass (builds 107–116)
Ten-iteration improvement pass across graphics / gameplay / UI, all compile-verified:
- **Hazard readability:** oil slicks are irregular multi-blob puddles with sheen that spread then dry up (slip radius follows the visual), with a deploy splash and a pulsing **LOW GRIP - OIL** HUD strip; smoke screens are wispy multi-puff clusters that drift/churn (same LOS gameplay footprint).
- **Visible vehicle damage (spec pillar):** new `VehicleDamageVfx` child on every `VehiclePawn` — sections vent gray smoke once armor is stripped and HP drops, escalating to darker smoke + flickering fire at critical; wrecks keep burning through the salvage phase.
- **Mount-location rules (spec pillar):** `WeaponDefinition.AllowedMountLocations`; all three droppers are Rear-only; Workshop filters illegal weapon/mount combos and flags legacy violations `[!]` instead of silently uninstalling.
- **Targeting computer rules (spec pillar):** `MaxActiveWeaponGroups` gates how many non-utility weapons can fire (offline slots read `[OFFLINE]` in the HUD and explain themselves in the log); turret auto-tracking requires `AutoAimSlots > 0`; droppers are exempt utility releases. Applies to player and AI alike (`ArenaWeaponLoadoutResolver` helpers).
- **HUD awareness:** radar shows hazard blips (own mines gold, oil violet, smoke gray rings) via the new `ArenaHazardTelemetry` registry; weapons list flags `OUT` / kind-aware `LOW` ammo.
- **Arena atmosphere:** four procedural corner light towers (emissive heads, additive beam cones, real shadowless spotlights pooling on the floor) plus a scene-owned `WorldEnvironment` (near-black backdrop, cool ambient lift, ACES, conservative glow).
- **Workshop weight UX (priority item):** persistent context strip now shows planned-loadout mass breakdown and estimated top speed % of stock using the same shared `VehicleMassMath.ComputeSpeedFactor` the arena pawn applies.
- **Settings (was a stub):** pause-menu Settings page with Master/Effects/Engines sliders (live, layered over mix trims) + Fullscreen toggle; persisted in `user://settings.json` via `GameSettingsStore`, loaded+applied at boot; startup honors the windowed preference.
- **Balance:** Tier-3 arena enemy carries more MG (240) and smoke (8) ammo to compensate for trading the mine away; AI that ever picks an uncontrolled weapon slot backs off instead of dry-firing.

## Premium menu layout (2026-03-22-96)
- City Hub, Garage, and Workshop now use a **scene-owned left drawer layout** instead of depending on the old premium responsive-node pass for shell placement.
- Garage now splits into **Fleet** and **Service Bay** tabs so the drawer can stay narrow without stacking every section on one page.
- Workshop now splits into **Overview**, **Hardpoints**, and **Ammo** tabs for the same reason.
- The target behavior is: management menus open from the left edge, consume less than roughly half the screen, and preserve the background scene on the right.

> Build 2026-06-01-106 adds the **smoke-screen dropper** (`wpn_smoke_dropper` / `ammo_smoke_std`), completing the dropper trio. It spawns a growing/fading translucent smoke cloud (`SmokeCloudRuntime`) behind the vehicle; `IsLineThroughSmoke(from,to)` (a 2D point-to-segment test that is a no-op when no cloud exists) drives the effect: hitscan shots crossing a cloud get 4x spread and missile lock is denied through smoke (falls through to an unguided shot). Arena enemies now field the full trio — mine on Tier-1, oil on Tier-2, smoke on Tier-3 (`WeaponType.SmokeScreenDropper` / `AmmoKind.Smoke`, shared dropper path).

> Build 2026-06-01-105 adds the **oil-slick dropper** hazard. `wpn_oil_dropper` (ammo `ammo_oil_std`) is a rear-deploy dropper that lays a non-damaging oil puddle; any vehicle whose center crosses it gets a transient traction slip (lower lateral grip + drive grip + steering via a `VehiclePawn` oil-slip timer honored in `UpdateRuntimeDerivedStats`), recovering shortly after leaving the oil. It shares the mine deployment path (`WeaponType.OilSlickDropper` / `AmmoKind.Oil`) for both player and AI, and the Tier-2 arena enemy now carries it so oil shows up in real matches. Mine detonation now reuses the shared `ArenaVfx.SpawnProjectileImpact` burst instead of the old zero-length shot-to-self flash.

> Build 2026-06-01-104 fixes the arena fog-of-war that read as covering the whole screen. The overlay plane was elevated (y=2.75), so the tilted top-down camera parallax-projected its opaque out-of-bounds region up across the view. It is now anchored coplanar with the floor (y~=0.12) — a ground-coplanar plane can only shade ground points, never the play area/sky — and re-enabled, with the clear footprint aligned 1:1 to the inner walls (combat bowl + start lanes stay clear) and the fog softened to deep shadow. Perimeter blackout curtains still mask the gray background beyond the arena.

> Build 2026-03-22-95 shifts the premium management screens into left-edge drawer panels that stay under half-screen width, restoring the background scenes as part of the composition. City Hub, Garage, Workshop, and arena briefing/results now bias left instead of centering, and Garage once again points at the original optional `Assets/Images/Garage/Garage1.png` background.

> Build 2026-03-21-92 fixes the premium menu-shell scaling regression: City Hub, Garage, and Workshop now shrink through their actual responsive layout bounds (not a no-op visual transform), and their authored header/button sizing was tightened so the smaller floating shells reveal more background without immediately feeling cramped.

> Build 2026-03-21-88 brings City Hub onto the shared premium shell language used by Garage and Workshop, so the core city → garage → workshop loop now reads as one cohesive HTML-first hybrid UI family.

> Build 2026-03-21-87 begins the HTML-first hybrid UI rollout: premium screens now use HTML/CSS mockups as the design reference while runtime remains native Godot, and Workshop has started moving onto the same shared premium shell/section language as Garage.

> Build 2026-03-14-17 tightens the Workshop layout so the weapon-mounts section stays compact and scrollable, keeping the ammo-buying flow and bottom actions visible on shorter displays.

# Project State (Canonical “where we are”)

## North star
See `Docs/MASTER_GAME_SPEC.md` (authoritative design north star). It mirrors the longer “Master Game Design Prompt” used to seed new threads.

See also `Docs/CODEMAP.md` for a quick “where to look” map.

## Engine / tech
- Godot 4.6.1 Mono
- C# / net8.0
- Keyboard-only combat first (mouse not required)
- Startup forces **fullscreen** and ensures the **root viewport renders at native fullscreen resolution**. (F11 toggles fullscreen/windowed.)

## Current playable loop (stable)
- City shell UI now uses the shared premium shell/metric-card/travel-card language in a left-edge drawer layout instead of the older plain menu list, while still routing into Garage/Workshop/Arena from native Godot UI.
- Garage + Workshop/Loadout
- Garage and Workshop now share a larger vehicle showcase card so the current build/selection is easier to inspect without leaving those screens.
- Garage now supports fleet-management salvage choices for non-active vehicles (set active / strip for scrap / sell).
- Garage now uses a drawer-oriented management layout: fleet/actions stay grouped first, with selected vehicle, repairs, and upgrades stacked underneath for a denser left-panel flow.
- Garage now also uses mockup-driven art/layout polish: a roster-focused left column, selected-vehicle hero card on the right, and an art-backed footer summary so the management screen feels closer to the intended in-world garage presentation.
- Garage repairs now expose a clearer service flow: full cash repair to restore the active vehicle completely, plus scrap-based armor/tire patch actions that repair as much as the current scrap balance allows.
- Start encounter → realtime arena combat → post-win salvage/towing phase → rewards/repairs → return to city

## 3D transition (2.5D)
- The project now uses **3D models with a fixed top-down/RTS camera** (2.5D).
- `Scenes/Main.tscn` includes a `WorldRoot` (`Node3D`) used for 3D scenes.
- Arena uses `Scenes/UI/ArenaRealtimeView.tscn`, which instantiates `Scenes/Arena/ArenaWorld.tscn` into `WorldRoot`.
- `Scenes/Arena/ArenaWorld.tscn` and `Scenes/Arena/VehiclePawn.tscn` are intentionally **minimal**; the arena floor/walls and pawn collision/visuals are generated procedurally in code for robustness.
- Arena floor now always goes through the same layered asphalt shader, so the gameplay-facing grime/oil/repair-patch breakup applies whether the user has the external Poly Haven asphalt pack or is using the bundled generated fallback (`Generated/Textures/Arena/*`).
- Arena floor also adds deterministic detail decals (repair patches + oil/skid stains + crack overlays) above the base surface, and the layered shader now uses anti-tiling macro sampling so the floor no longer reads like repeated square slabs from gameplay height.
- VehiclePawn now supports **weapon mount points** (fixed + turret) driven by vehicle defs and can render **mounted weapon visuals** (proxy boxes for now).
- Camera is locked to the active pawn (vehicle or driver on-foot).

## Controls (arena)
- W/S = throttle forward / brake-reverse (in vehicle)
- A/D = steering (in vehicle)
- Space = fire
- Tab = cycle targets
- **E** = exit/enter vehicle (contextual; exit only when stopped/slow; enter when close to vehicle)
- On-foot: WASD to move (top-down); **Shift** to sprint
- Post-win salvage phase: while on foot near a defeated enemy wreck, the player can either **strip it for scrap** or **attach a tow line** (when their vehicle is parked nearby).
- If no target selected, closest target auto-selects
- F11 = toggle fullscreen/windowed

## Combat readability (current)
- Minimal firing feedback exists: tracer + small flashes (muzzle + impact).
- Tracer is a true 3D line from muzzle → impact (camera-friendly).
- Shots fire forward and only apply damage when the ray hits the intended pawn.
- Vehicle handling is now tuned toward a more simulation-ish feel:
  - bicycle-model steering (no spin-in-place at 0 speed)
  - lateral friction + drag
  - weight + tire condition impact effective speed/traction
- VehicleStatusHud also shows **Mass** (vehicle + weapons + ammo + towing). Attaching a tow during the salvage phase updates that mass immediately.
- Player gets immediate feedback on shots: **center hit marker** + hit/miss SFX; tire-pop SFX plays when a tire is destroyed.
- Vehicles now play **layered positional engine audio** (5 RPM layers crossfaded + subtle pitch) via the `Engines` audio bus.
- Hard braking at speed triggers a temporary **tire skid** placeholder SFX (replace with real skid audio later).

- Target HUD is a dedicated **TargetStatusHud** (upper-left): single-line, centered layout with HP/AP **bars** (values inside the bars).
- A simple **3D target indicator** (procedural glowing ring + marker) follows the selected target so Tab targeting is readable without relying solely on HUD text.
- A top-right **PlayerStatusHud** shows **Driver HP + Driver AP (armor points)** side-by-side (compact width):
  - Driver HP is persistent (default 50).
  - Driver AP is the equipped armor buffer (Basic Kevlar by default).
- A top-right **VehicleStatusHud** (under PlayerStatusHud) shows **vehicle section + tire HP/AP** around a small top-down vehicle preview.
  - The vehicle preview uses a single runtime-built `SubViewport`; the scene no longer carries a baked preview viewport/camera tree because that path could get stuck showing a stale arena feed.
  - The HUD preview now draws directly into the on-screen `VehiclePreviewHost` control via `VehiclePreviewCanvas` instead of relying on runtime-spawned child controls, cloned vehicle visuals, or viewport/camera paths. This avoids the repeated stale-feed, blank-preview, and wrong-control-path failures.
  - Includes a compact **Weapons** list (mounted weapons + ammo counts).
  - Includes a **Speed** bar (gold) under the weapons list.
- VehiclePawn exposes section/tire/driver hitboxes to support **positional damage**.
- Direct-hit fallback damage now infers the impacted vehicle section from the actual local impact point when the body collider is hit before a dedicated section hitbox, avoiding incorrect front-damage bias on rear/side hits.
- If the app restarts mid-encounter, Arena Start will resume the active encounter instead of blocking.

## AI (current)
- Drives through a shared arena AI driver that emits the same reusable `VehicleControlIntent` surface used by player-controlled vehicles.
- Fires and consumes ammo.
- Uses mount-aware tactical profiles plus orbit/stand-off driving so the enemy tries to stay in a more useful fighting lane instead of only charging straight at the player.
- Enemy driving now falls back to direct pursuit until the vehicle is already moving and close enough for orbit/stand-off positioning, which avoids the recent “good at shooting but not actually driving” regression.
- Navigation now samples multiple forward/side feelers, scores direct/left/right lane candidates, and briefly commits to the best lane so the enemy more reliably drives around the new obstacle lanes instead of repeatedly steering straight into cover.
- Arena driving now also applies an explicit boundary-pressure bias near the outer walls, so enemies prefer to fold back into the playable interior instead of treating the perimeter wall as a valid pursuit line.
- Stuck detection now keys off repeated low movement plus throttle/steer effort (not only raw collisions), and recovery now reverses/turns intelligently with chained recovery attempts to reduce barrier pinning and endless oscillation. Boundary-facing wall pressure also counts as a stuck signal so enemies recover sooner when they nose into the perimeter.
- Core driving / firing thresholds remain data-driven through `Data/Config/arena_ai.json` for faster tuning without code edits.

## Save/state
Primary state records:
- `SaveGameState`
- `PlayerProfileState`
- `VehicleInstanceState`
- `EncounterState`

Notes:
- `PlayerProfileState` includes `DriverHpMax/DriverHp` and equipped armor (`EquippedArmorId`, `DriverArmorMax/DriverArmor`).
- Save migration bumped to **v6**.
- `GameUiKit.Controls.HoldToActivateButton` currently supports semantic visual states plus a disabled mode; the arena briefing Start/Resume action depends on that shared API for its repair-warning tinting.

Definitions load via `DefDatabase` / loaders.

## Architectural direction (important)
- `GameSession` is a **facade**.
- All save mutation flows through a single backbone:
  - `Scripts/Game/Session/SessionContext.cs` owns the in-memory `SaveGameState` and is the only place allowed to replace/persist it.
  - Focused services live under `Scripts/Game/Session/*` (e.g., encounters, garage, world) and depend on `SessionContext`.
- Pure/near-pure logic belongs in `Scripts/Game/Systems/*`.
- Prefer “single commit” patterns during realtime combat:
  - Keep per-frame/per-hit state in memory
  - Persist once on resolution (win/lose/flee)

Note: Build 2026-02-21-19 fixes arena shot correctness (no auto-aim; only damages intended pawn) and improves tracer placement.

## Turn-based arena
- **Deprecated / removed.** Any leftover turn-based arena interaction code should not be reintroduced.

## Folder notes
- `Scripts/Arena/` and `Scenes/Arena/` are the canonical arena implementation (3D world with 2.5D camera).
- Legacy 2D arena content has been removed.

## Build & packaging rules
- Never ship/compile `.godot/` (csproj excludes it).
- Zips should not include `.godot/`.
- Zips should not include `Assets/` (assume Assets is kept locally to keep downloads small).

- Build 2026-03-15-34: guided missiles hit harder again, the VehicleStatusHud preview now renders in its own isolated preview world and follows the player car yaw for a stable top-down read, the match-end dialog sits higher, and towing now draws a visible tow-cable line during the salvage phase.
- Build 2026-03-15-35: returning to the player start box at the end of the salvage phase now freezes the player vehicle (and any towed wreck) immediately and shows a centered loading message while the round transitions into rewards / recovered-vehicle handling.

- Build 2026-03-16-50: fixed the one-shot VehicleStatusHud portrait capture path again by letting duplicated off-tree preview meshes contribute bounds before the hidden capture viewport is attached to the scene tree. The HUD should now use the captured portrait instead of the simplified fallback icon.

## Current priorities
- Build 2026-03-21-89: fixed the `CityShell` compile regression from the new premium City Hub pass and aligned the remaining workflow doc (`Docs/AI_WORKFLOW.md`) with the canonical zip-baseline process.
- Build 2026-03-20-78: arena enemy driving now uses multi-feeler lane scoring, persistent lane bias, and chained recovery behavior so the obstacle-heavy arena plays more cleanly without losing the stronger combat pressure.
- Build 2026-03-17-72: moved the salvage/tow status HUD panel to the left side of the screen so it stops overlapping the vehicle HUD, and added a world-space recovery-zone highlight over the player start box with state changes for salvage guidance, active towing, and recovery completion.
- Build 2026-03-17-68: added post-win enemy vehicle hijacking in the arena salvage phase. Drivable enemy wrecks can now be claimed on-foot, switched into as the active vehicle, and driven out of the arena while the original player vehicle remains owned in the garage.
- Build 2026-03-17-69: replaced the single-action wreck prompt with a compact multi-action salvage menu. Drivable enemy wrecks now present **Enter Vehicle** alongside **Attach Tow Cable** (`T`) and **Strip Wreck** (`R`) when available, so towing is no longer hidden by the recover-and-drive option.
- Build 2026-03-17-71: fixed the start-box tow recovery soft-lock so the post-match loading overlay finishes correctly after towing a wreck home, and added a dedicated salvage/tow status HUD panel with recovery instructions, distance-to-box, and tow-cable tension warnings.
A) Feature work (active)
- Enemy vehicle claiming / salvage-menu interaction is now in; the next towing follow-up should build on the new tow-status HUD with stronger recovery-zone feedback, richer wreck-follow behavior, and clearer salvage consequences.
- Towing polish (clearer tow visuals, detach flow, better wreck follow behavior)
- Land mines / hazard gameplay follow-up
- Weapon visuals + additional projectile/hit VFX rollout
- Vehicle damage consequences + weight/capacity UX

B) Cleanup / maintenance (as needed, not primary)
- The arena refactor pass is considered substantially complete for now.
- `ArenaRealtimeView` now delegates to shared helpers for target selection, post-match flow, post-encounter presentation, hitbox raycasts, and direct/explosive vehicle damage.
- Keep GameSession small by pushing responsibilities into `Scripts/Game/Session/*`.
- Continue moving pure logic into `Systems/*` only when it directly supports feature work.

## Workshop UI notes
- Workshop now uses a drawer-oriented premium service-bay layout: active vehicle, actions, systems, mounts, and ammo are stacked as denser left-panel sections instead of spreading across a centered wide shell.
- Premium menu screens now follow the HTML-first hybrid direction (`Docs/UI/HTML_FIRST_HYBRID_DIRECTION.md`), which treats HTML/CSS mockups as the authoring/reference layer and shared Godot components/styles as the runtime implementation path.
- Garage and Workshop now let their responsive panels grow to the configured viewport-fit percentages on larger desktop screens, and their generated button icons are explicitly size-clamped so the action rows stop wasting vertical space.
- Workshop now uses a more compact vertical layout: the **Weapon Mounts** section is intentionally shorter and scrollable, and the ammo resupply list is also capped so the bottom actions remain visible at common fullscreen heights.
- City Hub, Garage, and Workshop now clamp into left-edge drawer bounds instead of centered wide shells, leaving much more of the background art visible during menu navigation. City already keeps its main body scrollable; Garage/Workshop were also reflowed into denser vertical stacks so the drawer layout uses space more efficiently.

## Screen overlays
- Active UI *screens* under `UIRoot` are swapped via `ScreenRouter` (registered in `App.Services` by `AppRoot`).

- Overlays live under `Scenes/Main.tscn` → `OverlayRoot` (CanvasLayer layer=100) so they always stay above active UI.
- `ConsoleOverlay`: bottom-left overlay (~60% screen width), toggled with tilde (~).
- `PauseMenuOverlay`: Escape opens a polished pause menu with Resume Game, Save Game, Settings (stub), and Exit to Desktop; the menu remains usable while the tree is paused.
  - Expanded: shaded header + scrollable history + command input.
  - Collapsed: **one-line** mode showing the most recent entry (header hidden).
  - Mouse interactive (click-to-focus, draggable scrollbar, working collapse/expand).
  - Typed lines + colors: Debug (blue), Status (white), Input (gold), Error (red).
  - Replaces the old arena-only “Combat Log”.
  - Built-in commands: `help`, `clear`, `version`, `money add <amount>` (with `money <amount>` shorthand).

## Arena HUD
- `PlayerStatusHud`: compact top-right HP/AP for the driver (values inside bars; percent-based color).
- `VehicleStatusHud`: top-right (under PlayerStatusHud) shows section/tire HP/AP around a small top-down vehicle preview.
- `TargetStatusHud`: upper-left single-line target HUD with HP/AP bars (values inside bars).
- `ValueBar`: lightweight bar used across HUD; supports vertical fill and rotates label text for vertical bars.

Notes:
- Build 2026-02-22-06 fixes a `PlayerStatusHud.tscn` parenting regression that caused arena-start exceptions and `0/0` placeholder values.
- Build 2026-02-22-07 adds TargetStatusHud, narrows VehicleStatusHud, and moves speed display into VehicleStatusHud.
- Build 2026-02-22-08 polishes HUD density: removes redundant direction labels in VehicleStatusHud, reduces header/label font sizes, and centers mid-row spacing.
- Build 2026-02-22-09 further polishes HUD readability: TargetStatusHud one-line, PlayerStatusHud narrower, centered left/right bars, and improved weapons list spacing.

- Build 2026-02-22-10 updates HUD/overlays: TargetStatusHud uses one-line HP/AP bars, ConsoleOverlay is 60% width at bottom-center, DebugOverlay removed, VehicleStatusHud aligned under PlayerStatusHud with a top-down vehicle preview.

- Build 2026-02-22-11 tweaks: removed bracket labels from TargetStatusHud bars, moved ConsoleOverlay to bottom-left, and removed ammo from Arena HUD stats line.

- Build 2026-02-22-12: startup now forces fullscreen window mode.
- Build 2026-02-22-13: fullscreen startup now forces the root viewport to render at native fullscreen resolution.
- Build 2026-02-22-14: only applies native fullscreen content scaling when fullscreen is actually achieved (prevents shrinking/letterboxing when the window can't resize, e.g., embedded editor run).
- Build 2026-02-22-15: City shell includes an **Exit** button; added **F11** fullscreen/windowed toggle.
- Build 2026-02-22-16: Added a 3D **target indicator** and began **weapon mount** visuals (proxy weapons mounted to vehicle mount points; turret mounts aim).
- Build 2026-02-22-17: Fixed `TargetIndicator3D` compile errors (procedural ring mesh).
- Build 2026-02-22-18: Enemy spawns with a default weapon loadout; proxy wheel orientation fixed + front wheel steering visuals; camera pulled back; added overlap fallback to prevent vehicle clipping.

- Build 2026-02-23-09: Fixed bullet hit registration when firing straight by extending vehicle hitboxes upward (mounts sit higher than the pawn's movement collider) and making raycasts explicitly test all collision layers.


- Driver Exit / On-foot: E to exit/enter; enemy AI targets driver while on-foot; VehicleStatusHud hides while on-foot; driver takes damage from vehicle collisions.
  - DriverPawn supports a Mixamo avatar scene loaded at runtime (see `Docs/Assets/MIXAMO_DRIVER_SETUP.md`).

- Arena floor visuals now use a restrained layered asphalt base plus a single full-floor story overlay for skids/stains/cracks; this replaced the earlier scattered decal-plane approach because those large decal bounds were reading like slabs from the fixed top-down camera.
- The bundled arena floor overlay/texture set has been lightly rebalanced again so the top-down camera gets clearer wear marks without falling back into obvious rectangular patterning.
- Build 2026-03-14-07: cleaned up the remaining menu-heavy screens (pause menu, city shell, garage, workshop, arena briefing, and salvage/results) and moved the post-match hold-to-continue dialog upward so it reads more like a standard game alert.
- Arena floor story overlay received one more light pass for subtle extra wear without bringing back the obvious rectangular breakup from earlier attempts.
- Arena floor color is now warmer and more dirt/sand-toned while preserving the recent skids, stains, and crack accents. The tint is applied in the shared layered floor shader so the change still shows up even if the user has the external asphalt pack installed.
- Arena perimeter visuals now lean more toward the `Arena1.png` still-image look: bundled rusted/industrial wall textures, heavier outer-wall supports, stepped spectator-bank silhouettes, corner scrap piles, and a larger outer apron floor so the area outside the playable bowl reads like stadium/service space instead of empty flat ground.
- Build 2026-03-14-08: arena matches now stage vehicles in north/south starting boxes outside the main floor, with open wall gaps so both sides drive in from dedicated launch lanes instead of spawning in the arena center.
- Build 2026-03-14-09: the arena briefing now checks the active vehicle for missing repair points, surfaces a repair warning when needed, and uses a reusable hold-to-activate Start/Resume action that reads green when the vehicle is ready and yellow when it is damaged.

- `ArenaRealtimeView` owns its own menu background state. When the pre-fight briefing is visible it can show `res://Assets/Images/Arena/Arena1.png` (if present locally under `Assets/Images/Arena/`); that background must be hidden during live combat/post-match so the 3D world remains visible.
- `GarageView` and `WorkshopView` now follow the same optional-background pattern as the arena/city screens, using `res://Assets/Images/Garage/Garage1.png` and `res://Assets/Images/Workshop/Workshop1.png` when those local `Assets/Images/...` files exist.
- Workshop ammo buying is now mounted-weapon-driven instead of a fixed 9mm quick-buy strip: the screen builds one ammo purchase row per ammo type used by the current loadout, including missiles / rockets / mines, with kind-aware quick-buy amounts plus a fill-to-target action.
- The arena Start/Resume action is expected to be a `GameUiKit.Controls.HoldToActivateButton`, but `ArenaRealtimeView` now includes a runtime replacement path if the scene node loads as a plain `PanelContainer` so the menu remains usable even after a stale script/type mismatch.
- Build 2026-03-15-19: guided missiles now emit a reusable code-only projectile trail while in flight and spawn a dedicated projectile impact burst (flash + shockwave + sparks + smoke) on detonation. The underlying helpers were added in `ArenaVfx` / `Scripts/Arena/` so future ammo types can plug into the same VFX path instead of inventing one-off effects.

- Build 2026-03-15-20: began a larger refactor pass aimed at cleaner expansion. Repeated menu background logic moved into reusable UiKit code, City/Garage/Workshop now use a reusable responsive panel layout helper for better desktop-size behavior, and starter/enemy vehicle defaults moved into `Data/Config/vehicle_builds.json` with a shared `VehicleBuildFactory` used by both session garage creation and arena enemy creation.

- Build 2026-03-15-25: JSON-backed config systems now share the new `JsonConfigStore` base for caching, reload support, default fallbacks, and consistent error handling. Driver pawn tuning, vehicle build presets, weapon visuals, and UI theme loading all now follow the same reusable config-store pattern instead of hand-rolled file I/O in each system.

- Build 2026-03-15-36: fixed the start-box auto-finish loading overlay hang, and reworked the VehicleStatusHud preview to use a fixed top-down camera plus inverse-yaw preview pivot so the player car reads correctly in the HUD instead of drifting into a bad world/camera angle.

- Build 2026-03-16-37: fixed the VehicleStatusHud wiring so ArenaRealtimeView now forces a typed HUD instance when needed (instead of quietly living on the fallback binder), stripped the HUD preview clone down to visual mesh nodes only, and nudged the vehicle HUD lower so it no longer overlaps the player HUD.
- Build 2026-03-16-38: rebuilt the VehicleStatusHud preview viewport tree at runtime and now pass the live player vehicle directly into the HUD, so preview-camera changes hit the actual rendered preview instead of a stale viewport/world feed. The vehicle HUD was also moved farther down to leave clean spacing below the player HUD.

- Build 2026-03-16-41: fixed the latest VehicleStatusHud preview-bounds compile break (`Math.Clamp` + iterative bounds traversal) and cleaned the related ArenaRealtimeView warnings so the current HUD-preview branch is buildable again.

- Build 2026-03-16-43: `VehicleStatusHud` now renders an isolated preview-only `VehiclePawn` proxy inside the HUD viewport instead of sharing the live arena world or cloning the imported visual subtree. This avoids the stale-feed / black-preview failures and keeps the vehicle readable from a stable top-down angle.

- Build 2026-03-16-46: the VehicleStatusHud preview now renders directly in the scene-owned `VehiclePreviewHost` control through a dedicated `VehiclePreviewCanvas` script. This removes the runtime child-control path that could leave us updating a control other than the one actually visible on-screen.
- Build 2026-03-16-47: the HUD vehicle preview stays on the scene-owned `VehiclePreviewCanvas` path but now renders a more faithful top-down vehicle card (class-specific silhouette, cabin/glass, weapon mounts, tow indicator, and live damage/tire overlays) so it reads closer to the original intended mini-vehicle view without going back to brittle viewport/camera logic.

- Build 2026-03-16-48: Vehicle HUD preview now uses a one-shot captured top-down portrait of the actual player vehicle visual instead of a continuously live preview path.

- Build 2026-03-16-49: fixed the one-shot HUD portrait capture path so the hidden SubViewport actually renders off-screen. The HUD now keeps the ViewportTexture directly and disables viewport updates after the capture frame instead of trying to read back an image from a viewport that never updated.

- Build 2026-03-16-51: Vehicle HUD preview now captures a one-time portrait by cropping the already-rendered main arena viewport around the live player vehicle instead of relying on a hidden SubViewport / duplicated visual subtree.

- Build 2026-03-16-53: Vehicle HUD preview now mounts a live embedded `SubViewportContainer` directly under the scene-owned `VehiclePreviewCanvas` and renders the real vehicle `Visual` subtree there. This replaces the failing crop/hidden-capture experiments and keeps the actual preview visible even if texture freeze/readback fails.

- Build 2026-03-16-54: VehicleStatusHud now renders a dedicated preview-only `VehiclePawn` scene inside the HUD viewport instead of duplicating the live vehicle visual subtree. The preview uses the same loadout/body-color/model pipeline as the real arena pawn, fits a fixed top-down orthographic camera once the preview pawn finishes building, and suppresses the fallback drawn silhouette while the live HUD viewport is active.


- Build 2026-03-16-55: VehicleStatusHud now presents its preview-only `VehiclePawn` viewport through a `TextureRect` using the viewport texture instead of a nested `SubViewportContainer`, and the preview viewport now explicitly opts into its own `World3D` plus always-on updates. This is intended to remove the remaining HUD-preview “nothing changed” failure where the preview path existed in code but never visibly took over the on-screen host control.


- Garage vehicle list now supports renameable vehicles, scrollable/icon-backed fleet entries, and hides raw vehicle GUIDs from the player-facing UI.

- Build 2026-03-17-59: Garage fleet rows now use a custom scrollable vehicle-card list with non-cropped rendered previews, richer vehicle meta, and stronger selection styling. Garage and Workshop screens also gained bundled generated banner/icon art so they look more like diegetic control panels even when optional external background images are missing.

- Build 2026-03-17-64: Garage and Workshop now pin their outer panels with viewport-aware anchors/offsets so they stretch much closer to the bottom of the screen while keeping a clean margin; their internal content still flows through scrollable container layouts for smaller desktop resolutions.
- Build 2026-03-20-76: ArenaWorld now prefers structured interior obstacle layouts built from optional asset-backed props under `Assets/Models/Arena/Obstacles/` (concrete barriers, tires, trash cans, wrecked cars) instead of the old random wall boxes, with primitive/texture fallbacks if those imported scenes are unavailable locally. Arena enemy driving also now uses short forward feelers plus a reverse-then-turn unstuck routine so AI drivers recover more reliably after colliding with cover.

- Build 2026-03-21-82: Arena enemies now use a standardized front-gun / rear-mine loadout with explicit mine-laying behavior when the player is tailing them, and enemy ammo consumption now comes from their actual installed ammo inventory instead of a random abstract ammo pool. Arena survivors also now reset driver HP + personal armor to full after each match via the session encounter-resolution path.

- Arena enemy driving now has explicit outer-wall escape steering/throttle clamps plus boundary-aware recovery, but continue validating against obstacle-heavy matches for any remaining perimeter oscillation.
- Garage UI now uses a cleaner two-column service-bay layout with only the fleet roster scrolling; future polish should focus on finer spacing/art rather than reintroducing a long full-screen scroll.

- Build 2026-03-21-90: premium management shells (City/Garage/Workshop) now intentionally float over the desktop background instead of stretching edge-to-edge, and Workshop mount rows were rebuilt into labeled service-console cards while ammo rows gained reserve badges/progress plus cleaner buy/fill actions.
- Build 2026-03-21-91: the shared `ResponsivePanelLayout` now supports a visual shell scale, and City/Garage/Workshop use it at 0.70 so the entire premium UI reads substantially smaller while still preserving the full underlying authored layout for scrolling/input.

- Build 2026-03-21-93: corrected premium menu shell sizing again by basing `ResponsivePanelLayout` on the viewport visible rect instead of the raw display-server window size, which was over-scaling City/Garage/Workshop on high-DPI desktops. The three premium menu scenes now also have smaller authored fallback bounds, so the shell still reads about 70% sized even if the responsive helper does not get a fresh compile.
- Build 2026-03-22-94: premium management screens still use the floating 0.70 shell bounds, but the main improvement path is now internal density rather than trying to visually scale every child control. City Hub travel cards/intel now size to their content instead of stretching into large empty panes, Garage selected/list/footer cards are more compact, and Workshop active/loadout sections were tightened to keep more useful controls visible inside the same shell.

