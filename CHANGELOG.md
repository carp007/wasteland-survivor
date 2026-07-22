- Build 2026-07-22-186 (8-hour loop #6, iterations 7-8 — continuation after commit d2778d7: bail-out duels, garage consoles, economy fixes):
  - **ENEMY BAIL-OUT DUELS** (spec: driver-killed hulls stay salvage-whole; on-foot stage 3 begins): mobility-killed tier-3+ enemies no longer surrender — the driver bails out with a red hostile identity ring and a tier-appropriate sidearm (9mm at t3, SMG at t4+), strafe-jinks at pistol range, and shoots back (chip vs your hull, real damage vs you on foot). Every player damage path works on them: vehicle guns hit at full damage, personal weapons at full driver damage, and running them down at speed hurts them badly ("DRIVER RUN DOWN"). Kill the driver and the intact hull is yours. Tiers 1-2 keep the onboarding-friendly surrender. `--shot=bailout` films the duel deterministically (probe: bailed driver landed pistol rounds on player sections at 4-24m).
  - **On-foot aim overfly ROOT-CAUSED** (deterministic probe win): the 0.35x-flattened aim ray from chest height sailed clean OVER car hulls at close range — every close shot died mid-air. True aim line from shoulder height: probe went 0/9 hits → 9/9 locational section hits at 6-9m.
  - **Garage tabs are real consoles now** (judge P1 follow-up, via implementation agent): Service Bay = per-section hull-integrity grid (AP/HP bars, SOLID/WORN/BREACHED flags) + per-tire tiles + fuel/range/refuel-cost tiles + a state-reactive Chief Mechanic's log; Upgrades = armor/tire plating ladders with INSTALLED/NEXT states and honest install deltas (armor + kg + top-speed % change) + a shop-floor assessment card.
  - **Economy fixes** (judge P2s): vest trade-in scales with REMAINING armor (shredded rigs no longer trade at full value — the swap-cycle exploit is closed); shotgun anti-hull chip accumulates across pellets per shot instead of flooring each pellet at 1 (was ~2x designed damage).
  - **Polish**: salvage-phase toasts drop below the tow guidance card instead of covering its header; the overworld map's cryptic legend text run is a drawn legend strip with real glyph samples (arena tier badge, clone-lab cross, fuel pump, closed-road dash, WANTED reticle, freight crate).
  - **Human minutes recorded**: `Docs/PLAYTEST_NOTES.md` is the new canonical play-test log — the 2026-07-22 13-note session on build ~181 (which drove this entire loop) is logged with per-item dispositions.

- Build 2026-07-22-185 (8-hour loop #6, iteration 6 — P2 sweep + loop-5 carry-over close):
  - **On-foot idle body-aiming**: a standing driver pivots toward the locked target (movement still owns facing while walking) — without it the facing-cone gate was unwinnable against an orbiting vehicle on keyboard. Probe now lands shots on target/cover at correct ranges; a clean hull-hit assert remains racy (walking driver vs driving car) and is noted in NEXT_TASK.
  - **9mm tracer reads as a pistol crack** (judge P2): `ArenaVfx.SpawnShot` gained a small-arms scale — personal weapons draw at 38% tracer width and ~7m cap instead of the vehicle guns' 19m ordnance beam.
  - **t4 SIDEWINDER conversion closed** (loop-5 carry-over, re-confirmed by this loop's judge): the sports chassis' empty F2 mount now pairs a .50cal with the 20mm — probe went from 10 shots/2 hits to **57 shots/8 part hits**.
  - **t3 LANES volume verified**: post-thinning probe measured **47 enemy shots** (judge probe 11-13, loop-5 open-arena baseline 28).
  - **RING joust corridor truly clear** (judge P2): first arc barrier moved 26°→31° so its tangent span stays outside |x|<9; verified on camera.
  - **Store thumbnails tell classes apart** (judge follow-up): frozen list icons render from a front-3/4 (148°) angle — hatch/notchback/racer/pickup/wagon/box-van silhouettes distinct at 72px.
  - **Title menu focus ring is warm gold** (judge P2) instead of theme cyan.

- Build 2026-07-22-184 (8-hour loop #6, iteration 5 — judge-round response; both judges opened at B-):
  - **On-foot ammo readout ticks live** (judge P1): the counter updated only on stat events and froze at the seed value while firing — now refreshed on the 10 Hz HUD tick.
  - **On-foot aim honors the spec** (judge P1): fires at the LOCKED target (Tab selection, fallback encounter enemy) only when it is inside the weapon's range AND a ~100° facing cone; otherwise the shot travels down the facing line and whiffs — the spec's "you're pointed wrong" feedback. The onfoot harness probe now walks the driver at the enemy before firing so the chip-damage path is exercised for real, not just whiffed.
  - **Smoke screens finally look like smoke** (judge P1): the countermeasure cloud was 6-8 UNTEXTURED faceted spheres reading as giant gray polygons center-frame; its puffs now use SmokePuff3D's shared blotchy soft-billboard sprites with a warm-gray tint.
  - **LANES mid wall thinned again** (judge P1): the t3 probe measured enemy fire under half the loop-5 baseline — the center row drops to one inner block + outer pair per side, opening a wide |x| 15-23 firing window.
  - **Journey interstitial de-loading-screened** (judge P1): real horizon scenery (water towers, power poles, wreck piles, dead trees at 100-190 px), near-shoulder burned-out car silhouettes on full parallax, a dust trail kicked up behind the vehicle chip, and a legible skip hint.
  - **Scroll affordance** (judge P1): city hub tabs + Clinic & Outfitter keep their scrollbars visible whenever content overflows (Accept Contract / Prairie Wolf rows were clipping at the panel edge with no cue).
  - **NEW GAME issues the starter chassis immediately** (judge P2): a fresh run opens drivable instead of hub-locked.
  - Judge verdicts also cleared: the journey chip "wrong vehicle" claim (harness staging artifact — the real flow uses the cached showroom portrait).

- Build 2026-07-22-183 (8-hour loop #6, iterations 2-4 — structured arenas, AI driving overhaul, travel journey, on-foot weapons):
  - **STRUCTURED ARENA LAYOUTS** (play-test: "obstacles are pretty random... I'd like some structure so there's strategy to movement"): four named interior archetypes — PILLARS (symmetric cover blocks + firing lanes), RING (broken circular wall with four gates), LANES (parallel walls with leapfrog gaps), CROSSBUNKERS (L-shaped corner bunkers + open center) — chosen deterministically per city+tier so every venue keeps its identity. N-S jousting lane stays clear (playbook); salvage-yard venues inherit the archetype. `[ArenaLayout]` log line names the draw.
  - **AI DRIVING OVERHAUL** (play-test: "AI drivers are terrible"): PD steering with slew-limited wheel (no more bang-bang jitter), planned detours around a new arena obstacle registry instead of last-second feeler panic, LOS-repossession (cover-blocked enemies pursue around the blocker — they visibly cut through ring gates), hysteretic tactical transitions, and reposition-through-cover when taking sustained fire. Nine new data-driven knobs in `arena_ai.json`. Telemetry: recovery activations ~0-1/fight (was constant), wall-contact ≈1 s/fight, shot accuracy 43%→54%, barrier-wasted shots 12→2; loop-5 fire-cadence fixes confirmed intact at all five tiers.
  - **TRAVEL JOURNEY INTERSTITIAL** (play-test: "travel between cities... start building that out"): committing a leg now plays a skippable ~5-7s road trip — scrolling highway with roadside silhouettes, FROM → TO header, km ticker + leg progress, fuel needle draining to the arrival level, toll/station/freight receipts, rolling road-flavor lines — cut short by a red "RAIDERS INBOUND" (or bounty/merchant/salvage/scavenge) interrupt flash when the leg's event takes over. Pure presentation over the already-atomic travel commit; skipping is always safe. `--shot=journey` harness target; roadmap in Docs/TRAVEL_BUILDOUT_PLAN.md (next: HIGHWAY combat venue for road fights).
  - **ON-FOOT PERSONAL WEAPONS — stages 1+2 shipped** (play-test: "player outside the vehicle should have a basic weapon... store should sell weapons and armor, spec it out"): master spec gained a full on-foot combat section (design intent: high-risk utility, never out-guns a vehicle mount); new `PersonalWeaponDefinition` def category with four weapons (Rusthound 9mm free-issue sidearm, Gutterstorm SMG, Doorbreaker 12g, Prairie Wolf .308); Driver Store sells them (buy/equip/ammo boxes) plus two new vest tiers (Heavy Kevlar 58 AP $180, Exo Plate Carrier 110 AP $2,600 — ladder now 50/58/65/85/110); on-foot in the arena the fire key shoots the equipped weapon at the locked target (hitscan pellets, chip damage vs hulls via the normal locational path — a finisher, not a main gun), with a weapon/ammo readout under the driver vitals. Personal ammo pools live on the driver profile, survive cloning, and commit once at match resolve. A fresh clone always wakes armed with the 9mm. Docs/ONFOOT_COMBAT_PLAN.md tracks the staged rollout.
  - **Cars grounded** (play-test: "a bit cartoony"): hull shader pulls paint saturation down slightly, streaks faint wear onto side panels, and moves the paint response from plastic (rough 0.62 / metal 0.20) to automotive (0.52 / 0.30); player gold deepened from toy-bright (0.93,0.76,0.12) to (0.84,0.66,0.14).
  - **HIGHWAY combat venue** (travel Stage 2): road ambushes and WANTED bounty hunts now fight ON the road — a straight two-lane asphalt ribbon runs the joust axis (dashes, edge lines), 4m guardrail segments with real cover collision line the outer thirds (weathered gaps included), dead traffic rots on the shoulders, power poles march the far edges past a leaning billboard. Stadium shell, crowd audio, and floor paint all suppressed; the interior archetype cover still spawns and reads as highway debris. `--shot=highway` stages a real road ambush and films it.
  - Harness: `--shot=driverstore`, `--shot=journey`, `--shot=onfoot` (drives, brakes, bails out, fires the sidearm on camera), `--shot=highway`.

- Build 2026-07-22-182 (8-hour loop #6, iteration 1 — play-test P0 response: title screen, ground lines, engine audio):
  - **Real title screen / main menu**: the boot splash (studio card) now lands on a new `TitleScreenView` — full-bleed title art with CONTINUE (when a save exists) / NEW GAME (confirm + full save wipe via `GameSession.ResetToNewGame`) / EXIT, build id + studio footer, keyboard focus flow. The art renders through a new UiKit `CoverArtControl` that cover-crops with a TOP anchor, so the baked "WASTELAND SURVIVOR" lettering is never truncated again (the old center-crop cut it off on 16:9). Harness target: `--shot=title`.
  - **Mystery ground lines removed** (play-test): the emissive side "underglow" strips (gold beside the player, red-orange beside enemies) read as meaningless lines on the ground and are gone; contact shadow, facing wedge, hull red-trim, and target indicators carry vehicle identity unchanged.
  - **Engine audio no longer "organ keys"** (play-test regression): the 5-layer RPM crossfade played every loop at its recorded pitch, so RPM sweeps stepped through five discrete tones, and the hard idle/drive gate turned every AI throttle pulse into a new tone stab. Layers now rate-match — each bends toward the current RPM (`UseRpmPitchTracking`, ratio 2.3, clamped) so crossfades happen at matched perceived engine speed — and a rolling drivetrain keeps its drive layers engaged at wheel-tracking RPM instead of snapping to idle. Gearbox calmed (0.5s shift cooldown, wider downshift hysteresis) and idle/drive blend softened. **Verified with a new `--shot=enginesweep` harness target** that records the Engines bus to WAV through a scripted idle→sweep→coast→pulse profile: legacy audio locks onto two discrete tones (217↔260 Hz plateaus, 58 pitch jumps >12%); fixed audio glides 195→235 Hz continuously through the same ramp.

- Build 2026-07-22-181 (10-hour loop #5, iteration 13 — round-11 visual board closed, via implementation agent):
  - **Perimeter void filled** (N-1): a yard-safe wall-bounce albedo lift + faint cool value floor over the outer band, plus 8 low-energy perimeter pool spots (tier-scaled) — near-wall strips measured +10 luma while midfield stayed flat; the last dead quarter of the combat frame reads as dim venue now.
  - **Scavenge midfield tells a story** (N-2): 6 wheel-rut tracks (incl. the derelict approach line), 14 extra oil-biased stains, 7 collision-free part-cluster micro-piles, 3 tire stacks, 2 cool work-light pools — all deterministic, corridor kept clear. Field lesson: the sun-lit beige lot needs ~2x the alpha/energy the dark stadium asphalt needs.
  - **Stencils read as one token** (N-3): thicker overlapping segment strokes, centered one-bar glyphs, tighter pitch — "P It -04" → "PIt-04" on camera in both palette families.
  - **Enemies read hostile without brackets** (N-4): hue-preserving luma floor under dark paints + faint red edge/skirt emission + fixed red-orange underglow, gated to live combatants only (derelicts/trailers stay neutral).
  - **The kill moment is finally photographable** (N-5): the harness watches for the alive→destroyed transition and fires a 3-frame burst (t0/flash/ring) — the build-176 white flash and shockwave ring read at 1x with zero additional tuning needed.

- Build 2026-07-22-180 (10-hour loop #5, iterations 11-12 — round-11 gameplay response + bounty visibility):
  - **The tier-3 death spike is gone** (round-11 P0): WARRIG's missile rack cut 6→3, missile TrackingStrength 0.55→0.45 (≈293°/s — hard maneuvering finally matters, symmetric for player missiles), t3 fire pacing re-centered. Measured: incoming 246-290 → 161-186 with the probe driver surviving all three runs (was dead in 2 of 3); missile share of t3 damage 62-90% → 30-38%.
  - **The t4 weapons-silence anomaly root-caused** (two compounding AI bugs): with the target astern, anti-creep throttle floors overrode the reverse-recovery command (full throttle AWAY from the player at bearing dot -1.00 for 15+ seconds, just above the stuck-detector's gate), while ±π heading jitter flip-flopped steering into net-zero yaw. Fixed with facing-away-gated floors, latched turn direction with hysteresis, and a stall K-turn for blocked reverse swings; tier-5 ram surges exempted (they were the floor override working by accident — parity verified). OUTRIDER: 5 shots then silence → 34-40 shots/15-20 hits; worst away-streak 13s → 2.5-3s.
  - **MISSILE INBOUND**: every enemy tracking-missile launch fires a hot-red toast in the left feed + a danger cue — the smoke-dropper counterplay finally has a reaction window. Tier intel cards stop lying by omission (missiles now named at t3+).
  - **Bounty commitment is visible** (round-11 P1, orchestrator): the overworld map draws a pulsing hot-red WANTED reticle + dashed overlay on the haunted leg, route cards for that exact leg warn "WANTED: <name> · Tier N · $X — ambushes on sight", and the WANTED board gets an on-ramp — poster 0 is a GREENHORN head at the leg's own raider tier (poster 1 the +1 marquee), with per-tier intel lines ("runs a MISSILE rig — smoke breaks locks") on posters and the active card. Harness cityfreight target now stages a live bounty and captures the marked travel tab.
  - Known leftover for the next round: t4 SIDEWINDER's single front 20mm still converts few gun hits (2/window) — needs a gunner-cone or preset change, out of knob scope.

- Build 2026-07-22-179 (10-hour loop #5, iteration 9 — UI polish via implementation agent; round-10 P2-9/10/11, closing the round-10 board):
  - **Climax moments stay visible**: destruction toasts moved from a center-screen column to a compact left-side feed under the target HUD, capped at 2 (newest pushes oldest), lifetime 2.2s→1.5s, text-hugging width instead of full-width banners.
  - **One portrait standard everywhere**: the store DETAILS pane renders the real 3D turntable portrait (pedestal rig included) for selected vehicles via a factory-fresh synthetic instance; workshop/garage/store share a tightened camera fit so the car fills ~50-70% of the box (was ~20% letterboxed in workshop); garage fleet thumbnails get a cached shadow-weighted gamma lift (legible at 72x56, was near-black).
  - **Locked cards are readable**: tier cards and bracket UPCOMING nodes dim their CHROME (near-black stylebox + hairline border) instead of alpha-fading the whole card — labels hold ~8.7:1 contrast, the [LOCKED] kicker is finally legible. Applied to city tier caps and mid-tournament locks.
  - With this, every finding from judge round 10 (both boards) has a shipped, screenshot- or probe-verified fix except the P2 tire-compound backlog item.

- Build 2026-07-22-178 (10-hour loop #5, iteration 8 — yard + staging fidelity via implementation agent; round-10 P1-5/P1-6):
  - **Derelict husks read as dead cars**: the hull-detail system gains a derelict variant (26 rust blooms, stripped-panel holes, dust-grey glass, matte uniform kills the specular wash-out that made wreck top faces near-white) replacing the flat chocolate repaint; hull-detail seeding switched to stable FNV-1a (was per-process GetHashCode).
  - **The yard is a wrecking business now**: 11 deterministic crushed-car piles (2-3 flattened hull-detailed chassis each) ring the perimeter, the "huge empty brick pen" staging box holds two parked husks + edge litter, junk visible over the wall tops, and a procedural dark-steel yard crane holds a crushed car mid-lift (screenshot-iterated placement — east positions hide behind the HUD). Oil pools + rust bleed ground the clusters (soft radial quads; floor shader untouched). Fight bowl + exit corridor stay clear; obstacle bodies verified not to trap the drive-out.
  - **Staging props stop being programmer art**: start beam/gate bars are painted-metal booms (team-color diagonal hazard tile, bolt seams, wear) with the pulsing exit affordance moved to thin emissive edge strips; the two pure-black masses (north skyline silhouettes + jumbotron frame) join the shaded triplanar-concrete family; the crowd texture regenerated at 2.5cm/px so near-gate spectators read as shoulder-blob figures with bench shadows and ~10% empty seats (far-view statistically unchanged).
  - Regression: build-177 floor spectacle fully intact at t5 (two variants), salvageraid + haul healthy. Leftover: lossaftermath's north-gate close-up uncaptured (tow choreography wedged on barriers twice) — same materials verified at the south gate; new harness step will catch it next clean run.

- Build 2026-07-22-177 (10-hour loop #5, iteration 7 — "LIGHT THE SHOW" + tier-spectacle scalar, via implementation agent; the round's two P0 visual items):
  - **The floor is the stage now** (P0-1): the bowl gets value-contrast zones (lighter poured center stage with an eroded keyline, mid maneuvering field, darker service band), a 7-segment stencil renderer painting city-code floor graphics ("PIt-05" — the digits are the TIER, so the venue literally advertises the bracket), lane numerals behind both start rows, triple-chevron midfield arrows, broadcast corner brackets — all erosion-masked through the existing paint-wear pipeline. 2-3 saturated in-bowl light pools (straight-down spots; runtime gobo projectors black out on D3D12 — documented), plus a slow color-cycling jumbotron field wash.
  - **Tier 5 finally looks like the title fight** (P0-3): a 0..1 spectacle scalar from encounter tier drives pool count/energy/saturation, floor-paint boldness, crowd shimmer, pennant density — and tier 5 alone gets a gold title ring with crown ticks and gold stage pools. Tier 1 reads as a scrappy pit (code stencil only, 2 dim pools). Composes with city palettes (Detroit orange "dEt-02" vs Pittsburgh amber "PIt-05" verified).
  - Salvage yards and city UI regression-clean (all spectacle uniforms default 0; yard suppression path intact). Harness gains --shot-city= for same-venue tier A/Bs.

- Build 2026-07-22-176 (10-hour loop #5, iteration 6 — COMBAT VFX 2.0 via implementation agent, + audio reactivity wave):
  - **Tracers stop being salmon slabs** (visual judge P0-2): hitscan streaks are tapered cross-ribbons (width eases to zero — no box caps) with a white-hot core that crosses the bloom threshold and a thin faction halo (player warm gold-white, enemy hot red-orange); the .50cal projectile bolt gets the same white-hot-core + halo treatment. 13m/19m burn-out behavior preserved.
  - **Kills hit the screen** (P1-4): the dim orange disc is replaced by a two-pulse white-hot flash core, an expanding ground-plane shockwave annulus (torus, not the old cylinder-disc), an additive three-layer fireball, and an ember fountain — all tuned to bloom under ACES; the smolder aftermath is untouched.
  - **Mines read as area threats** (P2-7): new self-animating marker — dark puck, blinking core dome, slow-breathing ~1.5m hazard ring; armed pulses hot red-orange, unarmed sits dim amber; renders above oil slicks.
  - **The tow cable is a cable** (P2-8): volumetric dark sheath + tension-colored core (gray→amber→red with near-snap pulse), catenary sag, and a steel hook/coupler node riding the wreck-end hitch — all four cable users (haul, salvage, interception, victor tow-out) upgrade for free.
  - **Audio reactivity wave** (orchestrator): stadium fights get a restrained concrete-bowl reverb on the SFX tree (wreck yards/highways/menus stay dry — gated by the existing CrowdPresent flag); below 35% driver HP the music bed progressively low-passes (20.5kHz→1kHz sweep) and ducks up to -4.5dB, with automatic decay outside combat so the city never stays muffled.

- Build 2026-07-22-175 (10-hour loop #5, iteration 5 — missile truthfulness + the tier-3 stalemate, via implementation agent):
  - **Missiles finally deal their sheet damage**: the proximity fuse detonated 2.4m from the target's CENTER, so the splash falloff always sat on its 0.50 floor — every locked missile in the game has been landing 14-16 damage on a base-30 sheet since missiles shipped. The fuse now fires where the flight path crosses the target's hull sphere (exact segment/sphere intersection — also fixes frame-step tunneling at 55 m/s), and falloff is measured from the hull surface. Measured: 13 consecutive locked hits at 28-33. Near-misses and lost-lock shots keep the old floor.
  - **Blasts hit what they visibly slam into**: missile/rocket detonations route 65% of blast into the facing cardinal section (undercarriage share drops to 30%, secondary), with facing-section overflow leaking to the driver through the same per-section soft-spot factors as gunfire. A 33-damage blast caved LEFT + UNDERCARRIAGE on camera with zero prior left-side gunfire. Mines keep their undercarriage-payload identity (separate path, untouched).
  - **Tier 3 unstuck**: the 8 km/h nose-grind stalemate (8 enemy shots per fight vs 15/19 at t2/t4) came from the tier's strictest-in-game aim gate + shortest attack runs + weakest disengage (minFireDot 0.7→0.5, attackRunDuration 1.8→2.4s, nearThrottle -0.3→-0.55). Measured: 24-26 enemy shots + 4-5 missiles, fights now reach DISABLED/near-death outcomes.
  - **Bumper-contact breakaway** (new data-driven AI state, all tiers): near-contact at crawl speed for >0.8s commits the AI to a reverse-arc pull-out before resuming its stance — kills the tier-5 ram-grind (frames show 10-25m pass separation) while preserving close-brawl fire windows (crawl gate tuned 12→10 km/h after an A/B showed 12 swallowed tier-5's brawl: 6-7 shots vs 13).
  - Balance watch item for the next judge round: tier-3 missile rolls are now genuinely dangerous (5 locked missiles ≈ 150 damage). Sheet-truthful by design; purse/armor balance at t3 is the open question.

- Build 2026-07-22-174 (10-hour loop #5, iteration 4 — the targeting-computer system honors the spec, via implementation agent):
  - **Fixed-angle fallback replaces OFFLINE** (spec: "if computer is insufficient, some mounts must be fixed angle rather than auto-tracking"): weapons beyond the computer's control-group cap now lock to their mount's neutral bearing and fire opportunistically when the target crosses a ~10° cone — degraded turrets stop tracking, degraded missiles dumb-fire without lock. Player and AI symmetric; droppers stay exempt. HUD tags flip [OFFLINE]→[FIXED] (dim raised so the slot reads usable), with a match-start log line explaining each locked mount.
  - **The slot space was the second half of the bug**: weapon slots 4-5 didn't exist anywhere in the fire paths, so 4-weapon platforms could never use their side guns with ANY computer. Slots extended with mount dedupe (no double cooldown tracks).
  - **Computer ladder**: new Tactical Targeting Array (tc_tactical, 4 groups / AutoAim 3 / $6,800) tops tc_basic ($800) and tc_advanced ($2,700); the tier-5 CONVOY war truck fields it. Measured: CONVOY 2 → 4 distinct firing slots (side MGs x10-15 per fight during rams/passes); player Heavy Truck fires side guns + degraded forward-locked rocket.
  - **Starters ship tc_basic**: the $2,700 computer upgrade is now the game's first meaningful electronics decision (degraded mounts keep starters fully playable — verified t1 win with a dumb-fired missile landing the killing blow). Harness gains --shot-variant= (force a specific enemy variant) and a broadside drive-by phase in auto-drive.

- Build 2026-07-22-173 (10-hour loop #5, iteration 3 — gameplay judge round 10 quick wins, all five verified by probe):
  - **The starter Compact gets its engine back**: `vehicle_builds.json` has always asked for the 140 kW Gas V6, but `gas_v6.json` never listed `Compact` as an allowed class, so `VehicleBuildFactory` silently downgraded every new-game starter (and the tier-1 SCRAPPER) to the 90 kW electric fallback. Gas V6 now fits Compacts, the never-read `VehicleDefinition.AllowedEngineClasses` twin vocabulary is deleted (engine-side `AllowedVehicleClasses` is the single source of truth), and `ResolveEngineId` logs authoring drift instead of hiding it.
  - **Combat logs tell the truth about the armor matrix**: hit lines now print the POST-matrix applied damage plus a matchup note when the matrix moved the number ("[AP vs Steel +25%]") — the rock-paper-scissors ammo layer was invisible because logs printed the pre-matrix roll.
  - **Own mines can no longer kill a parked dropper**: owner immunity is positional (holds until the owner has genuinely LEFT the trigger zone once) instead of a 1.1-second timer.
  - **Specialty ammo pricing rationalized**: 20mm shells get their missing def price ($5/rd — they were falling through to the $2 ballistic baseline, making the $3,800 autocannon the best-value gun in the game by accident); AP rounds now cost 1.5x ball ($3) with multipliers set to 0.95x their ball variants (9mm AP 0.85→0.95, .50 AP 1.575→1.66) so AP is a real counter-pick against Steel instead of a strict downgrade.
  - **Freight pays for the haul, not the hop**: contract payout is now load x distance (heavy far contracts jumped ~$1,100 → ~$2,450; near-hop spam nerfed ~25%) so the heavy-truck/trailer/V12 investment the freight pillar exists to motivate is finally the rational play.

- Build 2026-07-22-172 (10-hour loop #5, iteration 2): **WANTED BOUNTY CONTRACTS** (spec: expandable quest systems + "AI should feel like players") — every city's Ops tab posts a WANTED BOARD with two named raiders (deterministic per city; reroll seed = lifetime heads claimed), each haunting one road leg. Accepting is free (one active contract); traveling the haunted leg in either direction forces the fight — "BOUNTY SIGHTED" — preempting ordinary road events, with full capture stakes. Winning pays the head price on top of the 45% highway purse and clears the contract atomically; losing or fleeing leaves the target at large. Save v12 (additive: `ActiveBountyContract`, `Player.BountiesClaimed`). New `--shot=bountylogic` probe walks board→accept→forced trigger→flagged encounter→win payout→board reroll (verified: RUSTY CRUZ t3, $866 head, contract cleared, board rerolled at destination); Ops WANTED cards screenshot-verified.

- Build 2026-07-21-171 (10-hour loop #4, iteration 31): both iter-30 leads closed — AmmoDefinition.TrackingStrength now drives missile turn rate (lerp 140-480 deg/s; missile_std's 0.55 matches the old hardcoded 320, defs without the field keep 320 exactly), opening per-ammo missile agility variants; and the tournament pit-crew restock prices ammo through the same def-driven resolution as the workshop (shared GameBalance.GetAmmoUnitPriceUsd), so specialty AP/HE rounds can no longer restock BELOW shelf price. Verified via arena t3 missile probe (lock/track/hit on camera) and a full tournamentlogic champion run (net +$776).

- Build 2026-07-21-170 (10-hour loop #4, iteration 30): ammo pricing is def-driven — `AmmoDefinition.UnitPriceUsd` makes the AP/HE prices in Data/Defs/Ammo the live source of truth (they had been silently ignored since they shipped, shadowed by a hardcoded workshop map that now survives only as a fallback); verified via a JSON price probe flowing straight to the workshop UI, final prices byte-identical to baseline, pit-crew restock untouched. New leads logged: wire TrackingStrength into missile turn rate (mapped so today's 0.55 = current behavior); pit-crew AP restock undercuts the workshop ($3 vs $4 — the markup fiction is backwards for specialty rounds).

- Build 2026-07-21-169 (10-hour loop #4, iteration 29): shaded arena wall faces stop reading as polygon voids — with one directional sun, any sun-averse face of the brick chute/perimeter/terrace walls collapsed to jet black (the scavenge drive-out corridor framed one wall as lit brick and its twin as a screen-dominating black slab); all PBR wall boxes now carry a faint self-emission sampled from their own albedo at the same world-triplanar UVs, so shadowed faces show dim brick coursing. Sun-lit faces measured byte-identical; arena t3 sweep regression-free. Also closed the t2/t4 floor-darkness lead by pixel measurement (within ~9%, frame content — not a bug) and logged two def-audit leads (dead TrackingStrength knob; ammo pricing split between hardcoded map and ignored JSON fields).

- Build 2026-07-21-168: regression-proofed the off-tree weapon-alignment spam fixed in build 162 — `WeaponVisualFactory.ForceUpdateTransformsRecursive` now early-outs when the weapon root isn't inside the scene tree (off-tree there is nothing to flush; alignment already reads accumulated local transforms), so any future preview path that configures a pawn before entering the tree can no longer re-introduce the 57-errors-per-run `"!is_inside_tree()"` spam. Verified via --shot=arena: 0 native transform errors, HUD preview weapon alignment pixel-identical to the pre-change baseline.

- Build 2026-07-21-168 (10-hour loop #4, iteration 28): long-range gunfire stops painting wall-to-wall laser slabs — the hitscan tracer drew its full muzzle-to-impact beam (up to 90m on joust misses: a screen-crossing 0.7m salmon ribbon caught on camera at t4); tracers now cap at 19m with a dissolving burn-out tail, leaving close-range fights pixel-identical. Screenshot-verified across 3 tier-4 runs with A/B crops.

- Build 2026-07-21-167 (10-hour loop #4, iteration 27): the pitch-black polygon voids flanking the south-gate staging frames are gone — they were the skyline's unshaded silhouette masses sitting inside the lit apron (huge unlit top faces at close camera range, root-caused via a color-coded elimination probe that exonerated the curtains/fog/background suspects), now rebuilt as shaded concrete service structures in the ring/stands material family; and every HUD value bar's text gains a thin dark outline, fixing the white speed readout washing out on the bright high-speed fill. Screenshot-verified on haul staging + arena t3 sweeps; menus regression-free.

- Build 2026-07-21-166 (10-hour loop #4, iteration 26): towed trailers finally read as flatbeds at gameplay zoom — plank deck with tonal alternation, wear runners, a red/white hazard tail bar, fenders, and deck cargo (spare tire + toolbox on the utility, webbing-strapped crate on the tandem) replace the blank brown slab; and the combat identity VFX (team underglow sills + facing wedge) no longer draws on Trailer-class pawns, killing the floating white sticks that flanked every hauled trailer. Screenshot-verified via 3x A/B crops on the haul beat; combatant identity visuals regression-free.

- Build 2026-07-21-165 (10-hour loop #4, iteration 25): living vehicles finally show their locational damage at gameplay zoom — the per-section damage smoke/fire anchors were fixed compact-car offsets, so on tall hulls (vans/trucks) the wisps spawned INSIDE the opaque mesh and drew fully occluded (a triple-caved WARRIG read factory-fresh); anchors now scale to the actual hull footprint/roofline, with two-tone drifting wisps and hull-scaled burn glows keeping 'hurt but fighting' visually distinct from a dead wreck's heavy soot column. Screenshot-verified with 3x A/B crops at t3; severity gates and wreck plume regression-free.

- Build 2026-07-21-164 (10-hour loop #4, iteration 24): arena walls stop smearing — BoxMesh UV-atlas stretching painted every long wall's TOP face (the face the top-down camera actually sees) as motion-blur streaks; all PBR-textured arena boxes (perimeter walls, start/exit chutes, backdrop terraces, scrap piles) now sample world-space triplanar, so brick tiles at true scale on every face and adjacent segments continue seamlessly. Screenshot-verified on interception + arena t3 with pixel-level A/B crops.

- Build 2026-07-21-163 (10-hour loop #4, iteration 23): wreck aftermath finally reads at gameplay zoom — the smolder was running but INVISIBLE (near-black puffs on a dark floor, and a vertical column projects onto the hull's own footprint from the top-down camera); wrecks now stream a two-tone soot/fire-lit-ash plume that drifts downwind across the floor (pre-seeded at the kill so the fireball clears into an established column), with bigger pulsing embers, a stronger smolder light pool, and an ash ring in the floor scorch. Screenshot-verified at t1 and t3.

- Build 2026-07-21-162 (10-hour loop #4, iteration 22 — hygiene close-out): the last off-tree transform spam (57 native errors/run from the combat HUD's preview pawn configuring before its viewport entered the tree) is gone — the HUD now mirrors the portrait component's enter-tree-first order, verified 0 errors with the preview rendering identically; PROJECT_STATE/NEXT_TASK synced through build 161 (play-test script now targets 161+).

- Build 2026-07-21-161 (10-hour loop #4, iteration 21): the Workshop engine selector now surfaces the decision data — power, torque, fuel type, efficiency, and TOW CAPACITY (shared VehicleMassMath) under the dropdown, so the Diesel V12's 8,000 kg towing pitch is finally visible at purchase time. Full 9-target regression sweep on build 160: 9/9 PASS (arena t1/t5, freight cycle, tournament champion, scavenge, haul, garage, store, city Ops ledger) — zero regressions across the loop's 20 builds.

- Build 2026-07-21-160 (10-hour loop #4, iteration 20 — agent-implemented, screenshot-verified): the tournament briefing renders a real 3-node BRACKET strip (green-check WON cards, pulsing-gold CURRENT, dimmed UPCOMING, with the running purse + champion bonus + salvage-rights line beneath — and an idle ladder preview above the ENTER TOURNAMENT card); the City Ops tab gains a FREIGHT LEDGER card (in-transit contract status or lifetime deliveries + pointer to the freight office). New harness targets tournamentbracket/cityfreight film both states.

- Build 2026-07-21-159 (10-hour loop #4, iteration 19 — round-9 P1 menu polish, agent-implemented + screenshot-verified): turntable portraits sit on a soft graded shadow-pool pedestal with a thin steel rim ring and warm ground-bounce light (the hard black ellipse is gone; workshop inherits the rig); garage Service Bay/Upgrades tabs open with four-stat tile rows + carded reports (all behavior untouched); the store's DETAILS pane empty state shows a watermark glyph, city kicker, and flavor card instead of a black column.

- Build 2026-07-21-158 (10-hour loop #4, iteration 18 — judge round 9 response; overall grade B- → B, briefing/garage/store now "clear the store-page bar"):
  - **Floor contrast pass** (the judge's named highest-leverage investment): panel tones quantized into 3 distinct poured-slab steps at 2.6x strength, seam grooves doubled, ~1-in-5 panels carry a faded painted wear decal (stencil bay lines / accent square / oil stain, all erosion-masked), and the hazard collar's stripes skew with radius so they finally read as chevron arrows. Screenshot-tuned over five capture iterations.
  - **Wreck aftermath fixed for real**: the "maroon aura" was the destruction fireball's oversized deep-red outer shell PLUS smolder puffs ballooning into flat 8m sheets (growth 0.9/s with rise-damping killing lift). `SmokePuff3D` gained growthRate/riseDamping parameters; wrecks now vent a genuine climbing smoke column over a ragged two-layer scorch decal with pulsing embers and a flickering warm light — no red veil anywhere.

- Build 2026-07-21-157 (10-hour loop #4, iteration 17 — the class ladder tops out):
  - **SEMI TRACTOR** joins the dealership ($21,500, Reactive armor, 4,800 kg, 220 L, five mounts incl. a 200° top ring and twin side guns) — the spec's heavy end of the vehicle ladder. Its identity is TOWING: the new **Diesel V12** ($8,200, 400 kW / 1,800 Nm) is the chained-towing enabler, hauling combinations nothing else can move. Uses the flat-nose truck chassis at 5.8m (the kit's "tractor.glb" turned out to be a FARM tractor — inspected and rejected); store silhouette draws a proper bobtail tractor with fifth-wheel coupler. Verified: 9 vehicles / 5 engines load clean; filmed fighting at tier 3 (all five weapons live, hull detail applied, 5,209 kg on the HUD).

- Build 2026-07-21-156 (10-hour loop #4, iteration 16): salvage yards gain scattered ground litter (30 deterministic flat plates/chunks in scrap tones across the field, exit corridor kept clear) — the open ground between junk clusters reads as a working scrap lot instead of swept concrete. Screenshot-verified with the concrete-panel grid and banner backing all composing together.

- Build 2026-07-21-155 (10-hour loop #4, iteration 15 — verification round 8 response; formal probe verdict: attack-run cadence VERIFIED at ~3x fire rate, overflow rework PARTIAL):
  - **Enemy gunners aim now**: shots flew along the RAW barrel line (aim was only corrected against on-foot drivers), so mid-turn fire sailed into the walls — ROADRUNNER measured 13-of-17 shots into barriers for zero delivered damage. Enemy fire now snaps onto the target's hull when it sits within ~21° of the barrel, on both the hitscan and projectile paths. Tier-4 probe went from ~1 to 9-of-18 on-target with 10 damage events.
  - **Line-of-sight trigger discipline**: the AI holds fire without a clear ray to the target instead of emptying magazines into cover during attack-run passes.
  - **Caged-driver rule**: while every section still has structure, direct driver-hitbox hits glance off the roll cage at 40% — point-blank 20mm fire could still tunnel the driver through an intact hull (tier-5 verification: 15-19 driver damage per shell). Once any section caves, full damage — the spec's driver-kill fantasy is untouched, it just requires a breach first.
  - **Driver deaths log**: both driver-death endings resolved silently; they now announce ("YOUR DRIVER IS DOWN…" / "Enemy driver killed…") like the DISABLE path always did.
  - On-foot driver readability: shadow disc + warm gold identity ring under the humanoid (the tire-swap/salvage/tow beats happen at the fixed 37m camera).

- Build 2026-07-21-154 (10-hour loop #4, iteration 14 — Service Bay/Upgrades density):
  - The Service Bay now reports **locational integrity** ("Sections: LF 0/11AP 9/16HP BREACHED · 2 tires DESTROYED") — the spec's per-section damage model finally reads where repairs are bought; the Upgrades tab states concrete before→after numbers per plating level (+armor per section, +kg) instead of "adds weight".

- Build 2026-07-21-153 (10-hour loop #4, iteration 13 — empty-state polish + full regression sweep):
  - The City Hub / garage empty-state vehicle silhouettes no longer draw destroyed-tire X marks (the placeholder instances shipped with zero-HP tires — it read as a broken icon); PITTSBURGH no longer clips at the travel map's edge.
  - Regression sweep on the day's 13 builds: freight cycle green (this run even rolled an ambush on the loaded delivery leg — the +15% risk firing as designed, delivery still atomic), tournament champion path green (net +$742 + salvage scrap), arena probes clean.

- Build 2026-07-21-152 (10-hour loop #4, iteration 12 — eval round 7 response; re-grade came back C+ → B- with ALL 8 round-6 fixes confirmed):
  - **The floor is a venue now** (the longest-standing visual P0): an 11m concrete-pour panel grid gives the midfield real value zones (per-panel tone shifts + recessed seams), a hazard-chevron collar rings the center circle, and four heavily-faded diagonal sector spokes add large-scale floor graphics — all in-shader, world-space, per-city eroded. (First pass shipped an 8-spoke white asterisk; screenshot iteration cut it to 4 faint diagonals.)
  - **Toast collision fixed**: the match-end dialog got a dark bordered backing and room for three wrapped lines, and the destruction-toast stack moved below its band — no more text compositing into garbage.
  - **Wrecks smolder properly**: destroyed vehicles now vent a standing smoke column (0.5-0.85s cadence, bigger/longer-lived textured puffs) with intermittent ember sparks — the post-kill drama the re-grade called "instantly killed" by inert black hulls.
  - **The blue slab mystery solved**: the floodlight towers' additive beam cones read as translucent slabs from the top-down camera — invisible while all towers were warm, exposed by build 142's cool contrast pair. Beam meshes cut entirely (pools + lamp heads carry the light, per the loop-3 lesson).
  - Briefing tier strip stays in lockstep with the selected tier (no more "tier 5 locked in" over "Tier: 1").

- Build 2026-07-21-151 (10-hour loop #4, iteration 11 — trailers stop being phantom cars):
  - **Procedural flatbed trailer visual**: trailers have no kit model, so every haul beat (DEFEND THE HAUL, garage chains, the store's flagship towing fiction) rendered the towed trailer as a generic CAR on the hitch. Dedicated flatbed proxy now: plank deck, perimeter side rails, A-frame drawbar with coupler aiming up the tow cable, axle-mounted wheels (single axle for the 2-tire utility, tandem for the 4-tire cargo box), and a strapped cargo crate on the tandem hauler. Built both at _Ready and retrofitted at ConfigureLoadout (the pawn's visual builds before the def is known on chain followers). Verified on the tow line via --shot=haul.

- Build 2026-07-21-150 (10-hour loop #4, iteration 10): the Workshop's active-vehicle showcase card adopts the same live 3D turntable portrait as the garage hero card (shared `VehiclePortraitViewport`; vector canvas remains the fallback) — the whole management loop now shows the player's real machine.

- Build 2026-07-21-149 (10-hour loop #4, iteration 9 — the garage gets a real showroom):
  - **Live 3D turntable portrait** (`VehiclePortraitViewport`, reusable): the Vehicle Details hero card now renders the player's ACTUAL chassis — real model, hull-detail shader, team color, mounted weapons — slowly rotating on a grounded display disc under key/rim/fill studio lighting, replacing the flat cartoon vector car that clashed with the photographic garage backdrop (eval round 6). Arena-only identity VFX (underglow sills / facing wedge, which read as floating yellow sticks from the hero angle) and engine audio are stripped from portrait pawns; the vector canvas remains as the automatic fallback if a portrait fails to build.

- Build 2026-07-21-148 (10-hour loop #4, iteration 8 — round-6 P1 cleanup, screenshot-verified):
  - **The combat HUD is opaque now**: pennant flags, husks, and floor decals bled through the translucent right panel and read as stray UI elements overlapping the ammo rows; the vehicle panel gets a dedicated near-opaque dark stylebox + full modulate.
  - RPM readout compacted to "6,202 · G4" (the long form clipped past the bar at redline); store vehicle rows now state the chassis **armor type** (the SUV's Composite pitch over the cheaper Light Truck is finally visible at the shelf); sponsor rings repainted quieter (mostly neutral paint, varied radii, one accent ring) so they stop reading as identical glowing donuts.

- Build 2026-07-21-147 (10-hour loop #4, iteration 7 — eval round 6 response; two judges, nine fixes, all probe/screenshot-verified):
  - **Combat P0 — the AI fires all fight long now**: kite/orbit personalities parked fixed front guns permanently out of bearing (the fire planner can never pick a gun whose muzzle can't reach), so tier-2/4/5 enemies fired 4-5 shots per fight — all during the opening joust, leaving tier 3 the hardest fight in the game. New data-driven **attack-run cadence** (`attackRunIntervalSeconds`/`attackRunDurationSeconds`, duration auto-scales up to 2x when the target sits astern): every few seconds the AI commits to a nose-on pass, guns sweep the target, then the stance resumes. Measured: T2 5→14, T4 5→13, T5 4→14 enemy shots per window — and the tier-5 CONVOY/WARDEN now **kills the auto-driving harness bot** at point-blank, exactly what a headline bracket should do to a starter compact.
  - **Combat P0 — driver-kill tunneling closed**: destroyed-section overflow to the driver is now section-dependent (front 25% — the engine block eats it; sides 55%; rear/top/undercarriage the full 75%, per the spec's "especially rear/sides/top/bottom"; tires 30%). The head-on joust no longer ends every fight in 6-20s through one caved front section — flanking for the soft arcs is the fast kill now. T2/T4 probe fights ended in earned mobility DISABLEs instead of instant driver deaths.
  - **Visual P0 — the impact "white disc" is dead**: vehicle hits spawned an opaque 1.05r emissive sphere (a featureless ~70px white paper disc in every hit frame). New `ImpactFlashVfx3D`: additive billboard with a generated radial-falloff/streak texture, pop-and-fade animation; sparks are soft motes now too. **Smoke stopped being paper cutouts**: puffs are textured billboards (3 generated blotchy-radial variants, random roll, grainy dissolving edges) instead of untextured spheres.
  - **The pennant X is gone**: the two corner-to-corner cable spans bisected every fight frame and read as giant scratches; the bunting now rings the arena perimeter (4 spans, tower to tower) where dressing belongs. Salvage yards also lose the center oil pit + rubber ring (the violet sheen read as a lavender haze blob under the player).
  - **Economy P1s**: road-ambush wins pay 45% purse and zero ammo ("raiders don't hand out purses") — a loaded freight hauler's +15% ambush odds are finally risk, not an income multiplier; low brackets' tournament entry drops to $100/tier and champions collect **salvage rights** (+20+22/tier scrap) — Detroit flawless net rose $553 → $715 + scrap.
  - Oil-slick markers no longer set GlobalPosition off-tree (error spam + possible origin-misplaced puddles).

- Build 2026-07-21-146 (10-hour loop #4, iteration 6 — the new classes join the war properly):
  - **Starter builds for SUV and Heavy Truck** (`starter_suv`, `starter_heavy_truck` in vehicle_builds.json): the heavy truck fields all six mounts (.50 front, twin 9mm side guns, top rocket, mine dropper) behind a Diesel V8. New `--shot-vehicle=<defId>` harness flag films any chassis in a real fight — the heavy truck was verified live at tier 3 (drives, fights, ribbed cargo roof reads great at altitude).
  - **New enemy variants**: tier 4 can now roll OUTRIDER (an olive-drab SUV skirmisher with smoke) and tier 5 can roll CONVOY (a six-mount heavy war truck with side guns and mines) alongside the existing pools — the top brackets finally field the full class ladder.
  - **Off-tree transform spam fixed** (~256 native errors per arena run since the model-weapon era): `WeaponVisualFactory.GatherMeshPointsInLocalSpace` called `GlobalTransform`/`ToLocal` on weapons assembled off-tree — it now accumulates local transforms up the parent chain, which also means muzzle alignment is computed from REAL geometry in every environment instead of silently degrading to identity transforms.
  - Freight board rows now show feasibility inline ("needs 22u free, chain has 8u" + a disabled NO ROOM button) instead of letting the accept fail into an error modal.

- Build 2026-07-21-145 (10-hour loop #4, iteration 5 — audio reactivity pass, all from already-staged CC0 assets):
  - **The crowd finally reacts**: the three staged crowd-swell one-shots had zero call sites — they now fire on every destruction moment (TIRE DESTROYED / SECTION CAVED IN / MOBILITY CRITICAL toasts) and on vehicle kills, with a 1.4s internal cooldown so a multi-toast volley reads as one roar.
  - **Wreck yards don't cheer**: new `AmbienceDirector.CrowdPresent` gate — salvage-yard venues (scavenge/raid/interception) mute both the stadium crowd bed and the reaction swells; the desert wind carries those beats alone.
  - **Gear shifts are audible**: every up/downshift plays a short pitched-up metallic clunk (reuses the staged impact sample, positional, slight pitch variance) — the RPM drop finally has its mechanical tick, on player and nearby AI vehicles alike.

- Build 2026-07-21-144 (10-hour loop #4, iteration 4 — FREIGHT CONTRACTS, probe-verified end to end):
  - **The logistics pillar gets its own income loop** (spec: storage capacity, cargo weight, trailers): every city's Travel tab now hosts a FREIGHT OFFICE posting three deterministic contracts — near (fits the starter compact), mid (wants a truck/SUV or trailer), far (heavy-hauler money, $1,000+). Accepting loads real cargo units into the active chain's storage (active vehicle first, then down the hitch chain), where they consume capacity and weigh the chain through the normal mass math — a loaded hauler is slower, thirstier, and **rolls +15% ambush chance on every leg** (raiders can smell it; DEFEND THE HAUL now has an economic reason to exist). Delivery completes automatically on arrival: payout banked atomically with the travel commit, crates stripped from every link, board rerolled (seed = lifetime deliveries). Abandoning dumps the cargo. Save v11 (`ActiveFreightContract`, `FreightContractsDelivered`; additive).
  - New `--shot=freightlogic` harness probe walks the whole loop through the real session API — verified: 3-tier board, 7u load (cargo 1→8), $387 payout on a 160 km run, cargo stripped, contract cleared, board rerolled at the destination.

- Build 2026-07-21-143 (10-hour loop #4, iteration 3 — two new vehicle classes from the spec list):
  - **SUV** ($8,900, Composite, 2,100 kg, 16 cargo, 95 L, 4 mounts incl. a 120° top turret) and **HEAVY TRUCK** ($14,500, Steel, 3,400 kg, 34 cargo, 140 L, SIX mounts incl. left/right side guns and a 180° top ring) join the dealership — the spec's vehicle-class ladder ("motorcycle … heavy truck, SUV, semi") finally grows past four powered classes. Both use unused Kenney kit chassis (suv.glb / delivery.glb) with correct hull-detail archetypes (new "suv" wagon-greenhouse layout; delivery box maps to the van/cargo-rib layout).
  - **Diesel V8** ($4,800, 250 kW / 900 Nm, thirsty) added for the big chassis; Gas V6 now fits SUVs, Diesel I4 fits SUVs and (as the budget option) heavy trucks. Store class silhouettes (tall SUV greenhouse w/ roof rails; 6-wheel ribbed container truck), HUD preview profiles, class display names, and diesel engine audio for heavy trucks all extended.
  - VehicleClass enum grew Suv/HeavyTruck appended after Trailer so any ordinal serialization stays stable.

- Build 2026-07-21-142 (10-hour loop #4, iteration 2 — the remaining round-5 P1 trio, screenshot-verified):
  - **Broadcast cross-lighting kills the palette monotony**: the north floodlight-tower pair now runs a CONTRASTING temperature against the per-city palette (cool steel vs warm venues, warm amber vs cool ones) plus a low north cross-fill light — the bowl finally has warm/cool depth instead of one amber wash everywhere.
  - **Speed/RPM read as instruments**: both gauges gained segmentation ticks (`ValueBar.GaugeTicks`); the speed fill brightens toward top speed and the RPM bar sweeps green → gold → red like a tach (redline tints the text warm).
  - **Salvage yards stop cosplaying as stadiums** (`ArenaVenueKind`): scavenge sites, rival-crew raids, and tow-line interceptions now dress the bowl as a roadside wreck yard — rust-tinted derelict husk chassis (with obstacle collision, so fights use them as cover), junk heaps with barrels, tire stacks, and NO pennants/crowd/sponsor rings; painted arena floor markings are shader-suppressed (`markings_strength 0`) since a wreck yard has no center circle or start hashes. Mid-field AND the south drive-out band are dressed (both were reading as bare voids in the beat frames).

- Build 2026-07-21-141 (10-hour loop #4, iteration 1 — the round-5 visual P0: vehicle fidelity at the sacred zoom):
  - **Vehicles finally read as cars, not colored boxes** (eval round 5 P0 — "the floor outclasses the cars"): new `VehicleHullDetailer` paints per-archetype detail onto the single-mesh Kenney hulls via a top-projected shader (no UVs needed, rotates with the car, keeps the unambiguous team tint): smoked-glass windshield/rear/side-window zones (the #1 top-down "this is a car" cue), panel seams + hood creases + two-tone roof inset, cargo-bed ribs on trucks/vans/industrials, racing stripes on sports hulls, hood vents, rust patches biased to edges/wheel arches, scratch wear, an edge AO vignette that grounds the silhouette, and skirt shading on the lower hull. Archetype (sedan/sports/van/truck/industrial/boxtop) is inferred from the model filename so enemy preset overrides (taxi/police/van/race/garbage-truck) each get a correct layout. Verified on camera at tiers 1/3/5.
  - **Mounted weapons read as hardware**: the Blaster Kit toy colormap (mint/orange plastic) is repainted into a gunmetal palette with per-part shade/roughness variation — turrets and dropper canisters now read as dark weapon hardware bolted to the hull.
  - **The recurring "white ball" muzzle artifact is dead**: the opaque 0.22r core-pop sphere bloomed into a ~1m white blob that obscured whichever vehicle was firing (visible in every recent screenshot sweep); it is now a small additive pop (0.11r, a=0.72, 35ms) — the directional tongue + warm light pulse carry the read.

- Build 2026-07-04-140 (loop #3, iteration 12 — the board is clear):
  - **DEFEND THE HAUL**: leave a city with a hitched chain and it comes into every encounter with you — the first chain unit rides visibly behind the player on a live tow cable (through ambushes, arena bouts, scavenge yards, and the salvage drive-out), and the WHOLE chain's mass now weighs the arena pawn down (`VehiclePawn.ExternalTowedMassKg` — previously the chain's weight silently vanished in-fight). An ambush while hauling is a genuinely worse fight, exactly as the spec intends. `--shot=haul` films it.
  - With this, every feature the multi-agent audits identified as autonomously buildable has shipped. The remaining roadmap (vehicle fidelity vs the sacred camera, palette direction, and the build-140 play-test) is in human hands — see Docs/NEXT_TASK.md.

- Build 2026-07-03-139 (loop #3, iteration 11 — the last unstarted spec behavior):
  - **RIVAL SALVAGE CREWS** (spec: "AI should feel like players: fight, salvage, tow" — the overworld half): quiet travel legs can now roll a 12% RIVAL SALVAGE CREW event — a crew hauling a prize wreck up the highway, its rust-brown haul visibly chained to its tow line (the interception rig, re-aimed). Engaging is optional; the fight is real (tiers 1-3, full capture stakes if you lose), and dropping the crew converts their haul to scrap on the spot ("HAUL CLAIMED — +N SCRAP"). Filmed via the new `--shot=salvageraid` target.
  - With this, every AI-behavior clause in the master spec has a shipped counterpart: arena crews fight under player rules, victors tow their prizes on camera, and rival crews now roam the highways with salvage in tow.

- Build 2026-07-03-138 (loop #3, iteration 10 — final autonomous polish item): the scavenge derelict now sits inside a breathing gold objective ring (parented to the wreck — towing drags the marker with it), closing eval round 5's "the beat's subject is not identifiable" note. Remaining backlog is human-gated: play-test script in Docs/NEXT_TASK.md.

- Build 2026-07-03-137 (loop #3, iteration 9 — eval round 5 verdicts + quick P1s; session closes at **visual B+ / spec 8.6 avg**):
  - Eval round 5: visual **B+ overall (arena B, menus/store A-)** — fourth consecutive grade improvement (C+ → B- → B → B+); spec non-negotiables scored 8/9/8/9/9. The retrospective's core finding: builds 126-136 have zero human minutes — `Docs/NEXT_TASK.md` is now a build-136 play-test script (three golden paths, real-save v8→v10 migration caution, honest debt list).
  - **Directional muzzle flash**: the blown-out white sphere (read as a rendering error, obscured the vehicle) is now a short hot flash tongue oriented down the fire line + a small core pop; the warm light pulse still carries the read at altitude.
  - **Pennants round 3**: flags nearly doubled (0.85×0.95m), bunting density up ~60%, four saturated alternating colors (red/gold/off-white/city accent) at brighter emission, darker larger drop shadows — the cables finally read as festive overhead rigging, not floor cracks.

- Build 2026-07-03-136 (loop #3, iteration 8 — witnessed towing, part two):
  - **THE RIG RIDES THE LINE**: interception fights now spawn your captured vehicle — in your yellow — physically chained behind the captor on a live tension tow cable, dragged around the arena while you fight ("YOUR RIG IS ON THEIR LINE"). Win and the line is cut on the spot ("LINE CUT — YOUR RIG IS LOOSE"), the hull left sitting where the fight ended while the session returns ownership as before. The dragged pawn is a real configured VehiclePawn (correct chassis/loadout visuals) moved kinematically on the same follow logic as the defeat haul-out.
  - New `--shot=interception` harness target films the whole pipeline: forced loss → capture → dealership replacement → interception resume with the rig on the line. Both halves of "AI crews salvage and tow like players" are now witnessed fiction.

- Build 2026-07-03-135 (loop #3, iteration 7 — the spec audit's final P0):
  - **SCAVENGE SITES** (spec: "side roads to buildings and abandoned structures containing salvage" — previously zero code): quiet, merchant-free travel legs now roll a 16% SIDE ROAD event — a confirm modal offers a collapsed depot, and investigating drops you into a **salvage-only yard**: a heavily rotted derelict (preset chassis, deterministic wear — stripped armor, sagging structure, a dead tire, looted ammo bay, long-dead driver) that the existing salvage rules handle wholesale: strip it for scrap, hitch and tow it home as a real vehicle, or drive on out the pulsing south gate. Zero-reward pre-resolved encounter (the salvage IS the purse), auto-enters past the briefing, `--shot=scavenge` harness target films it.
  - Travel income now has a third leg beyond arena purses and tournaments — exactly the overworld loop the spec sketched.

- Build 2026-07-03-134 (loop #3, iteration 6 — witnessed capture + actor detail, filmed via the new `--shot=lossaftermath` harness target):
  - **WITNESSED CAPTURE** (spec audit P0: "AI crews salvage/tow like players" was pure menu text): lose a fight with your vehicle captured and you now WATCH it happen — the victor drives to your smoldering wreck, hitches it to the real tension tow cable ("YOUR MACHINE IS ON THEIR HOOK"), and hauls it out the south gate while the camera follows your machine leaving; then the clone wakes as before. Fully skippable (fire/Esc), self-recovering, staged only when the vehicle was actually captured. Also fixed en route: the defeat wreck-explosion never fired (an `"loss"`/`"lose"` string mismatch) and the briefing panel used to overlay the world during post-loss sequences.
  - **The actors match the stage** (eval round 4: "the floor now outclasses the cars"): every vehicle gets a soft contact shadow, team-color underglow sills (player warm gold, enemies their preset tint), and an ice-cyan facing wedge on the hood — identity, grounding, and facing all read from 37m up. Locational damage finally shows ON the hull: ragged soot decals appear per side at 15% armor and darken + creep over the roof edge at 0; blown tires leave a flat-tire pad with scattered rubber chips. Wrecks go dark (glow/wedge off) but keep their soot and shadow.
  - **Dressing round 3**: pennant cables sag in real catenary curves (9 segments, 1.6m dip — finally reading as overhead bunting, not floor scratches), and the jumbotron cycles four generated sponsor cards with crossfades instead of rainbow noise.
  - Parts Store vehicle rows now show bold per-class top-down silhouettes (compact/sedan/sports wedge/truck-with-bed/trailers with drawbars).

- Build 2026-07-03-133 (loop #3, iteration 5 — eval round 4 responses; overall grade now **B**, up from C+ at session start):
  - **Chained towing complete** (spec non-negotiable #4, the audit's "one if-statement away"): the hitch API is chain-aware — new links attach at the chain TAIL, trailers can sit anywhere in the chain, and a non-trailer vehicle can ride behind a trailer as the terminating link (truck → trailer → salvaged war rig, exactly the spec fantasy), with the whole-chain engine-capacity check intact. The Garage shows the hitch row on chainable vehicles once the active vehicle is pulling a trailer.
  - **Smoke reads as smoke** (eval P0): puffs spawn at ~55% opacity with warm-brown tone variance and vertical squash so clusters BUILD to density instead of stacking solid gray discs.
  - **The oil pit reads as liquid** (eval P0): soft-edged wet brown-black stain with an iridescent violet rim band and real gloss (the corner light pools now catch a sheen on it) — no more hole-in-the-world ellipse.
  - Tracers thickened again (core 0.22 / glow 0.70 — round-1 sizing was still "borderline invisible in stills"), and the post-match salvage banner word-wraps instead of truncating mid-sentence.
  - Eval round 4 handed the next session its plan: witnessed AI towing (the victor drives to your wreck and hauls it out; interception shows your vehicle on the captor's tow line), overworld scavenge sites, vehicle-detail pass (the floor now outclasses the cars), and value-range/pennant-cable refinements.

- Build 2026-07-03-132 (loop #3, iteration 4 — arena-look batch, screenshot-iterated):
  - **The floor is a place now** (eval round 3 P0 #1 — "flat brown void... fails store-page standards on its own"): all in-shader, per-city seeded, zero new textures — 3-octave packed-dirt/oil-soak patch variation, a center oil pit, a rubber-buildup ring with concentric orbit ruts where fights actually circle, braking streaks into the joust lane, and PAINTED arena markings replacing the debug-white torus: worn two-tone center circle in the city's accent color with an off-white keyline, the exact seven start hashes (now painted, slightly worn), diagonal hazard stripes at all four gate mouths + the exit apron, a dashed midline, and faded sponsor rings — everything cut by a paint-erosion mask and darkened where the rubber says cars drive.
  - **Stadium dressing round 2** (P0 #2 — pennants read as "scratches and litter"): pennant cables are now real steel-toned 0.08m cables with bigger double-sided fluttering flags and — the key fix — soft drop shadows on the ground beneath each flag so height reads from the top-down camera; four new corner light pools reach into the frustum band; a color-cycling jumbotron on the north stands washes the floor edge in broadcast light; six low sponsor banner boards with per-city chevron patterns sit inside the bowl where mid-fight frames actually contain them. The two cross-arena "atmosphere" light shafts were cut after screenshot review — from this camera they rendered as a giant bright X painted on the floor.
  - **Polish trio**: Parts Store rows get real per-category icons (class silhouettes, per-weapon-type glyphs, piston/chip/canister), the locked target gains breathing corner brackets + a floating chevron, the Garage fleet tab fills with "EMPTY BAY — buy chassis at the Parts Store" placeholder slots instead of a void, and the RPM readout finally reads "6,212 RPM · Gear 4".

- Build 2026-07-03-131 (loop #3, iteration 3 — 3-agent features batch + eval round 3 responses):
  - **Enemies actually use their guns** (eval round 3 P0: the fire planner scored slots by raw damage with no cooldown/ammo awareness, so tier 3-5 AI spent whole fights pointing a cooling missile launcher — the 20mm autocannon had never fired in any log): `PickBestSelection` now skips cooling and dry slots so the AI falls back to its main gun. Verified: tier-5 fires MG bursts between missile shots.
  - **Destruction is visible in normal play** (eval P1): TIRE DESTROYED / SECTION CAVED IN / MOBILITY CRITICAL moments now show as fading top-center toasts (player events orange, enemy events gold) — no more tilde-console archaeology.
  - **CASINO** (spec: "later: casino in some cities"): The Rusty Jackpot opens in Chicago and Erie — Wasteland Wheel (staked spins, drawn odds = real odds, ~4.4% house edge; the brief's literal weights would have paid the PLAYER +27% and were corrected) and Highway Dice (2d6 vs the house, EV-neutral, ties push), pit-boss mercy comp after 5 straight busts, all money atomic through SessionWorld. City hub shows a Gambling Den button only where `HasCasino`.
  - **Mid-route fuel stations + road closures** (spec: stations on roads, Road Closed gating): the 4 longest legs gain named highway stations (0.85-0.95x fuel) with automatic mid-route top-ups when the tank would run low — and travel is blocked BEFORE departure if you couldn't afford the mandatory stop (no strandings). The Erie↔Pittsburgh span is CLOSED ("the Wolf Creek span collapsed") — route cards show a red CLOSED tag, the map draws it dashed red, and the loader warns if a closure would ever disconnect the graph (BFS-verified: all 8 cities remain reachable via the 375km detour).
  - **Tier-4/5 enemy variety** (eval P2: Ball ammo was auto-optimal vs the fixed Reactive t5): tiers 4-5 roll from variant pools per encounter (T4: SIDEWINDER Composite sports / STILETTO Steel sedan; T5: JUGGERNAUT Reactive truck / WARDEN Steel gunwagon / WRAITH Composite glass-cannon), seeded by encounter id so resumes re-roll identically; the target HUD shows the variant's name. Ammo counter-shopping finally has a moving target.
  - Eval round 3 verdicts: visual **B-** (up from C+; menus B+/A-, arena C+ — floor variety and dressing visibility are the next P0s), gameplay P0s from round 2 confirmed fixed.

- Build 2026-07-03-130 (loop #3, iteration 2 — 3-agent presentation batch, screenshot-verified):
  - **The arena is a stadium now** (visual judge P0 #2): the agent's frustum analysis proved nothing at the ±55 perimeter could EVER appear in a mid-bowl frame (the fixed camera's top edge ray hits ground 42m out) — so the venue dressing is layered by visibility: pennant bunting lines strung between the in-bowl corner towers (the only cue visible in a dead-center fight — and they read great), a continuous 2.2m concrete ring wall with per-city accent stripe hugging the playable bounds, 3 stepped crowd stands (generated seated-crowd texture, slow shimmer) on N/E/W, six 14m floodlight pylons with beam cones and light pools, gold/red gate dressing at the lane mouths, a PULSING green-gold south exit frame (the "highlighted exit" the salvage HUD always promised), and industrial skyline silhouettes beyond the stands. All procedural, deterministic per city, zero gameplay-footprint changes; the old rocky buttresses/terraces that occupied the stands band are gone.
  - **Combat HUD diet** (visual judge P1): the right panel dropped from ~420×558 to ~310×360 — locational damage now reads off the schematic itself (colored facing ring for front/rear/left/right armor+structure, Top/Under pips, tire pads with dark-red X when destroyed), the 20-bar grid and combat mass-breakdown are gone, and weapons render as icon slots with ammo counters (LOW/OUT tints) plus a cooldown sweep bar fed live from the per-slot cooldowns. Includes a fix for the long-standing stale-script bind failure: when the scene root loads as a plain PanelContainer, the view now adopts the authored child tree into a C#-constructed typed HUD (same recovery the Start button uses), so the typed HUD path finally runs in every environment.
  - **Parts Store overhaul** (visual judge P1): category tabs (VEHICLES/WEAPONS/SYSTEMS/SUPPLIES), icon tiles on every row (vehicle/trailer silhouettes, weapon/engine/computer glyphs), a right-side DETAILS pane comparing the selection against installed/owned gear with colored deltas, footer clipping fixed, wider drawer.
  - Ball ammo now -30% vs Composite (was -15% — imperceptible against range-falloff noise); workshop matchup hints updated to match.

- Build 2026-07-03-129 (loop #3, iteration 1 — resumed 6-agent workflow + gameplay-judge P0 balance pass + visual-judge P0 VFX pass):
  - **Combat is decidable again** (gameplay judge P0: tier-3+ pools outlasted the player's ENTIRE ammo hold — fights literally never ended): enemy driver armor re-curved 50-100 → 50-80, HP rolls 80-130 → 70-110, section-overflow driver transfer 60% → 75%. Instrumented t3: enemy driver AP 60→18 with two sections caved in the first ~18s — projected full-fight TTK ~40-60s (was "15 ammo holds").
  - **The locational game is visible** (judge P0: the deciding damage was invisible — a mobility-kill read as a random win): combat log now announces "FR TIRE DESTROYED", "LEFT SECTION CAVED IN — hits there now reach the driver", and "MOBILITY CRITICAL" on both sides; blast-to-tire transfer cut 85% → 60% now that it's visible. Tire plating raised to the same +3/+6/+10 curve as section plating (it had been left at +1/level — a $950 upgrade that absorbed zero additional blasts).
  - **Tournaments are no longer an EV trap** (judge P0: rounds 2-3 were fought dry): the pit crew now restocks ammo between rounds at track prices (1.6× markup, as much as the wallet affords), and the champion bonus is superlinear (250/600/1100/1700/2500) so marquee gauntlets out-pay farming Detroit.
  - **Weapon-fire VFX pass** (visual judge P0 #1): dual-layer tracers (white-hot core in a fat additive glow shell), 2× muzzle flash with a stronger light splash, impact light pulses, wall-hit dust/sparks so misses read, and missiles draw a hanging smoke arc across the arena.
  - **CC0 audio staged and live**: arena crowd bed + 3 kill swells (AmbienceDirector was already wired), and a 5-layer electric-motor whine set — electric vehicles finally sound electric (`ComputeEngineArchetypeId` → "ev"). All provenance in the attribution manifest.
  - **Ammo-type selection** (spec: ammo counters as a pre-fight decision): 9mm AP / .50 AP / 20mm HE-Frag variants with per-def pricing, a Workshop ammo-feed selector per mount with real matchup hints ("AP — shreds Steel (+25%), blunted by Reactive (-20%)"), resupply priced per selected type.
  - **Clinic & Outfitter** (spec: driver store + cybernetics): new city screen selling driver vests (composite 65 AP / assault rig 85 AP, 40% trade-in) and one-time cybernetic installs (+10/+20 max HP, clone-facility cities only — they cut you open and re-upload). New DriverUpgrades def category; city hub gains the button.
  - **Overworld map panel**: the Travel tab opens with a drawn REGIONAL ROAD NET — positioned city nodes, road graph, tier badges, clone-lab crosses, gold pulsing current city, dimmed beyond-this-hop cities, click-to-select routes.
  - **Per-city arena palettes**: each venue shifts floor tint / light pools / wall tone / prop density (Detroit rust, Chicago steel, Pittsburgh furnace amber...), wired via `ArenaWorld.ArenaCityId`.
  - Also: garage details card always renders the real vehicle (the mismatched purple-buggy hero photo is gone), briefing locked tiers read "[LOCKED]" and ENTER TOURNAMENT is a gold CTA, tier-1 purse variance restored ($150-208).

- Build 2026-07-02-128 (5-hour build loop #2, iteration 2 — 4-agent batch: audio / trailers / AI personalities / destruction VFX, all verified live):
  - **TRAILERS + CHAINED TOWING** (spec non-negotiable #4 — previously zero code): the store's new TRAILERS shelf sells a $900 Utility Trailer (14 cargo units) and $2,200 Cargo Trailer (30 units); trailers are real vehicles (`VehicleClass.Trailer` — locational damage, repairable, sellable, no engine/computer). Hitch one to the active vehicle in any garage city (fleet-card Hitch/Unhitch buttons); the hitch is a chain link (vehicle → trailer → future towed vehicle) with whole-chain tow-capacity checks against engine power, chain mass flowing into top speed and per-leg fuel burn, and departure blocked if the chain outweighs what the engine pulls. Save v10.
  - **Engine power-to-weight**: top speed now scales with installed engine output vs total mass (arena pawn + workshop preview share the same 3-arg `ComputeSpeedFactor`) — a diesel truck hauls what an electric compact visibly cannot.
  - **Per-tier AI personalities** (`arena_ai.json` "tiers" overrides, fully data-driven, old behavior when absent): T1 SCRAPPER timid close-brawler that backs off wounded; T2 ROADRUNNER kiter that flees pursuit and drops oil; T3 WARRIG mid-range pressure tank with tight bursts + defensive smoke when hurt; T4 SIDEWINDER fast flanker whose missiles jump the queue while the player is committed to a turn; T5 JUGGERNAUT relentless executioner that rams inside 16m and seeds mines on crossing passes. Fire discipline (burst/pause), low-HP retreat, re-engage distance, and opportunism windows are new per-tier knobs.
  - **Destruction & salvage payoff**: vehicle kills now spawn a full destruction burst (9-13 tumbling debris chunks with ground bounce, spark motes, 6m shockwave ring, layered fireball, light pulse — all RTS-camera sized, self-freeing) and the wreck chars to a near-black smoldering hulk (ember patches, lazy smoke) through the salvage phase. Tow cable rebuilt as `TowCableVisual`: sags when slack, pulls taut and ramps gray→amber→red with tension, pulses near snap length — and stops leaking a mesh+material every frame. **Verified live: a tier-4 fight ended by mobility kill ("Enemy vehicle DISABLED — the driver pops the hatch"), the new surrender win condition.**
  - **Audio feel**: every chassis finally drives on its own engine archetype (the single shared diesel loop default is gone; equal-power crossfade fixes the -40dB dip between RPM layers — all 15 staged layers audible; Gas V6 vs Diesel I4 is now a hearable purchase); brake skid is a sustained fading squeal instead of machine-gunning the tire-pop sample (tire pop = destroyed-tire cue only); missiles carry a positional thruster whine; dry-fire click on empty weapons; city spends (refuel/ransom/memory upload) click and error properly, travel departs with an ignition turn-over, and the ROAD AMBUSH modal hits with a danger stinger. Crowd bed/swell API is wired but silent until a CC0 crowd loop is staged (sourcing gap logged). Six legacy audio files flagged in the attribution manifest as provenance-unknown.

- Build 2026-07-02-127 (5-hour build loop #2, iteration 1 continued — 5-agent evaluation round + 3-agent implementation batch, screenshot-verified):
  - **ARENA TOURNAMENTS** (spec non-negotiable #1 — previously zero code): every arena city offers a 3-round gauntlet at escalating tiers ([maxTier-2 … maxTier], entry $150×tier, champion bonus $400×tier). Between rounds only a pit-crew patch (35% of missing armor/structure/tires — no restock, no garage), so attrition is the tournament's real cost. Losing/fleeing eliminates (purses earned are kept); dying anywhere mid-bracket forfeits. Briefing gains an ENTER TOURNAMENT card, a gold ROUND k/N strip, and the start button carries the Next Round action; tier cards lock while a bracket runs. `TournamentState` on the save (v9), `--shot=tournament` harness target.
  - **Missile/mine blast driver-bypass fixed** (eval P0 #1): blasts paid 65% damage straight to the driver through intact armor — six missiles (~13s, $210) killed ANY tier. Blast driver injury now follows the master-spec overflow rule (60% through a destroyed undercarriage) plus a small matrix-scaled concussion chip (10% missiles / 5% mines). Verified live: blast driver chip reads 1 vs the old 9-12.
  - **Tier difficulty curve restored** (eval P0 #2): enemy pools were clamped flat 50/50 at every tier (the tier roll was discarded); now the roll is the max (raised to 80-130 for t3-5) and enemy driver armor scales 50/55/65/80/100. Shipped `vehicle_builds.json` had silently disabled enemy missiles — tier 3/4/5 presets now field real missiles (6-8) plus armor/tire plating.
  - **Per-tier enemy visual identity**: tiers 1-5 now spawn a rusted taxi / navy police / olive van / chromed race-future / industrial-red garbage truck (unused Kenney Car Kit GLBs, preset-level `VisualModelPathOverride`/`BodyColorHex`), so the briefing intel names finally match what drives out. Weapon-mount `ArcDegrees` is now enforced (wrap-aware turret yaw clamps) instead of every auto-aim mount being a 360° turret.
  - **Economy retune** (eval P0 #4): purses reshaped to ~100/280/520/800/1150 ±30% (floor $180) — tier 1 no longer loses money after restock, tier 5 no longer prints it; ransom is now 22% of actual vehicle value + tier markup; oil/smoke ammo get real refill policies (were falling into the 200-round ballistic default: "fill" offered 194 oil canisters); ammo `DamageMultiplier` finally applies (20mm shells 1.5× — the $3,800 autocannon stops losing to the $350 MG); win-ammo rewards match your loadout instead of always paying 9mm.
  - **Armor plating is a real tradeoff** (spec non-negotiable #3): plating now gives +3/+6/+10 max armor per section (was +1/+2/+3), weighs +60/+140/+260 kg (tires +30/+70/+120 kg) through the shared mass model (workshop speed preview picks it up automatically), and is priced in dollars ($400/$900/$1,600 armor, $250/$550/$950 tires) instead of pocket-change scrap.
  - **City backdrops + campaign arc** (eval P0 #8): city defs gain `BackdropPath` (path munging that produced 'Fort_wayne.png' removed); cities without art get a deterministic procedural skyline (per-city palette/height, seeded by id). The Parts Store reuses the city backdrop darkened — no more black half-screen. Arena tier caps now form a ladder: Detroit/Toledo 2, Saginaw 1, Erie/Fort Wayne 3, Chicago 4, Pittsburgh 5 (Cleveland none) — the overworld finally has progression pull. Harness stages tier-3+ arena captures in Pittsburgh accordingly.

- Build 2026-07-02-126 (5-hour build loop #2, iteration 1): **the open 12-missile-salvo bug is root-caused and fixed.** The build-125 hull bar (`TargetStatusHud`) constructs its `ValueBar` in code, but `ValueBar.EnsureBound()` hard-required scene children (`Bg`/`Fill`/`Lbl`) and marked itself bound before throwing — so every `RefreshStats()` threw `NullReferenceException`, and in the missile branch the exception unwound `TryFire` *before* `SetSlotCooldown` ran, letting missiles fire every frame until ammo ran out. Fixes: `ValueBar` now builds missing children at runtime (code-constructed bars are first-class), fire cooldowns are committed *before* launch/impact resolution in both the missile and hitscan paths (defense-in-depth), and the harness-only `[MissileDebug]` print is gone. Verified live: tier-3 run shows exactly 2 missile launches for 2 fire-2 taps, zero exceptions; the target-HUD HULL bar actually renders now (it had never displayed), and everything after `SetHullCondition` in `RefreshStats` — which had been dying every frame since 125 — runs again.

- Build 2026-07-02-125 (5-hour build loop, iteration 3 — final window, judged by a 3-agent evaluation panel): combat verifiability + dealership.
  - **Vehicle dealership** (spec: stores sell vehicles — previously sell-only): the Parts Store gained a VEHICLES section selling bare chassis (compact $2,200 / sedan $3,400 / sports $5,200 / light truck $6,800 via `VehicleDefinition.PriceUsd` + `SessionStore.TryBuyVehicle`); the fleet economy is now a full loop (buy → outfit → fight → lose/capture → ransom/intercept → sell).
  - **Enemy guided missile un-bricked** (two stacked defects found by the gameplay judge): the AI fire planner picked computer-OFFLINE slots and wasted its window on back-off, and player-weapon mirroring pushed the preset's own missile past the targeting computer's control-group cap. Tier-3+ enemies now field MG + missile for real.
  - **Combat is now verifiable**: harness runs stream the whole combat log to stdout (`[Combat]` lines with damage numbers/matrix effects/outcomes), fire group 2 (missiles) is exercised in captures, the briefing frame captures the real selected tier, and the 45s harness timeout no longer eats the final frames. Verified live: armor-matrix multipliers visible in the numbers (HE vs Reactive ~0.7x), player missile lock/fire works, and the player WON an instrumented tier-3 fight.
  - **Target HUD hull bar**: the lock readout now shows aggregate section condition, so an entire fight of armor stripping visibly moves a bar (it used to show only driver pools — "my bullets do nothing"). HUD damage tint now also feeds through the fallback HUD path.
  - **One vehicle, one color**: garage (purple) and workshop (green) previews now use the same player yellow as the arena/HUD (`VehiclePresentation.PlayerBodyColor`); garage Vehicle Details debug scaffolding (blank panel, duplicate stat card) removed.
  - **Cargo weight is real** (spec): cargo mass (spare tire 20 kg) feeds the shared mass model → workshop speed preview + arena handling; spare-tire purchases respect `StorageCapacityUnits`.
  - Fixes: ~54k CanvasItem RID leak per match (HUD replacement retried every tick and leaked failed instances), driver overflow damage capped at 60%, muzzle flash rebuilt as hot-orange core + per-shot light pulse, city `ArenaMaxTier` enforced in briefing + service layer (road ambushes/interceptions correctly bypass), missile rate hard-floored at 1.5s (defensive — an instrumented run caught a 12-missile salvo in 0.4s; root cause still open, see NEXT_TASK).

- Build 2026-07-02-124 (5-hour build loop, iteration 2): systems depth + feel polish.
  - **Armor/ammo counter matrix** (spec: rock-paper-scissors): chassis have armor types (compact/sedan Steel, sports Composite, light_truck Reactive) and ammo carries penetration tags (9mm/.50 Ball, 20mm AP, rocket/missile/mine HE); `DamageMatrix` scales every direct hit, missile blast, and mine blast (AP shreds Steel 1.25x but Reactive blunts it 0.8x; HE eats Composite 1.25x but Reactive soaks it 0.7x).
  - **Vehicle contact audio**: tiered ram/crash crunches from real collision speed loss (3/6/10 m/s), plus a lateral-slip tire-squeal loop matched to the skid VFX thresholds. **Music crossfade** (~1.2s equal-power, per-mode resume positions) replaces the hard cut; Kevin MacLeod CC-BY credit now actually renders on the boot splash (license compliance).
  - **Parts Store overhaul**: darkened garage-interior backdrop kills the black half-screen, human-readable stat lines ("Rear-deploy hazard — oil slick cuts chaser traction · 3.6 m blast..."), last row scrolls clear of the footer, and a CONSUMABLES section sells spare tires ($60) into the active vehicle's cargo — the roadside tire swap can finally be restocked.
  - **HUD locational damage** (spec pillar): the mini-vehicle now tints sections body-color → orange → deep red as armor strips and structure fails, in both the drawn silhouette and as an overlay above the live 3D preview; destroyed tires read as a dark red pad with an X.
  - **Roadside merchant** hooked up: quiet travel legs can roll a merchant convoy modal that opens the store. Driver overflow damage through destroyed sections capped at 60% (kill window stays, reaction window exists). Post-match "Ammo Recovered:" shows "none" instead of dangling blank; pastel wreck prop rusted down; muzzle flashes smaller/hot-orange.

- Build 2026-07-02-123 (5-hour build loop, iteration 1 — multi-agent evaluated): the biggest single-build content/system push yet.
  - **8-city overworld with a real road graph** (spec pillar): `Data/Defs/Cities/*.json` (Detroit, Saginaw, Toledo, Cleveland, Erie, Pittsburgh, Fort Wayne, Chicago — positioned Great-Lakes-real, each with per-city services: arena tier caps, clone facilities, fuel price multipliers, flavor) loaded/validated (road symmetry, tier ranges) into `DefDatabase.Cities`. Travel is now per-leg along roads with per-road ambush profiles; the City Hub Travel tab is fully data-driven route cards (distance / fuel / toll / danger / services) plus a FUEL STATION strip.
  - **Fuel is real** (spec): `FuelCapacityUnits` per chassis, consumption per leg from distance x mass / engine efficiency (`TravelMath`), refuel service at city prices, fuel tile in the Garage. Save migration v8 tops off legacy tanks.
  - **Post-defeat vehicle capture + reclaim** (spec non-negotiable): losing a fight now means the victors tow your machine away — ownership suspends into `SaveGameState.CapturedVehicles`, the clone wakes at its memory-upload city, and the Ops tab offers PAY RANSOM (tier-scaled) or INTERCEPTION (win a fight against the captor tier in their city to take the vehicle back). **Memory upload** service ($50) at clone-facility cities re-anchors respawn.
  - **Combat correctness bugs (found by instrumented harness runs)**: per-slot weapon cooldowns — holding fire group 1 starved groups 2/3, so mines/missiles NEVER fired mid-burst; all 3D positional audio spawned at the world origin (GlobalPosition set before AddChild); mine markers rendered at the origin too; skid marks same. Player spread tightened (0.045→0.028 rad) + range damage falloff past 30m — long-lane spam now chips while brawls decide fights.
  - **CC0 asset wave (~250 files, all attribution logged)**: Kenney Car Kit — vehicles now render real per-class models (compact/sedan/sports/truck GLBs with steering front wheels, flat team-color body tint); Kenney Blaster Kit — all 8 weapons have distinct 3D models via `weapon_visuals.json`; Kenney Racing Kit + Poly Haven glTF props — stadium grandstands/billboards/light posts dress the arena rim, obstacle props no longer require Blender (.blend→glTF); weapon fire sounds for every weapon, explosion set, 4-variant metal-hit rotation, concrete world impacts, ramming crunch set (staged), UI click/hover/purchase/error sounds on every button, win/loss music stingers, city/arena ambience loops, tire-skid loop (staged).
  - **Arena look**: floor warmed/lifted, ring/hash contrast up, corner light pools brightened, tracers/impact bursts scaled 60%+ for the RTS camera, match-end wreck explosion (VFX + big boom + shake).
  - **HUD fixes**: Speed/RPM bars no longer clipped under the radar; the mini-vehicle finally shows the player's yellow (two green-default leaks fixed); combat HUDs hidden during the briefing.
  - Dev-voice placeholder copy replaced with diegetic flavor across City/Garage/Workshop; arena availability is def-driven (tooltips name each city's tiers); roadside merchant travel event (18% on quiet legs) staged for the store hookup; tow-capacity enforcement — hitch capacity now comes from engine power, a compact can no longer drag a war rig home.

- Build 2026-06-11-122 (8-hour build run, phase B: overworld-lite travel): **traveling between cities is now a gameplay system** — `SessionWorld.TryTravelTo` charges a $40 road cost (fuel + tolls), blocks departure mid-encounter, and rolls a 45% **raider ambush**: a ROAD AMBUSH modal interrupts arrival and rolls you straight into a tier 1–2 fight (reusing the realtime combat flow), with arrival completing after. City Hub travel routes now go through this path, and per the master spec's city variety, **Cleveland has no arena** (the Enter Arena button disables outside Detroit with an explanatory tooltip). City Hub also gained the "Open Parts Store" service button.

- Build 2026-06-11-121 (8-hour build run, phase A: economy + content): **the Parts Store is in** — a new city screen selling every priced weapon/engine/targeting computer (affordability-aware buy buttons, 55% sell-back, wallet/scrap readout), backed by a real ownership economy: `PlayerProfileState.PartsInventory`, a `SessionStore` service, and a Workshop that now only installs parts you OWN (installing consumes from inventory, uninstalling returns, un-owned applies are blocked with a store hint). **Arena tiers 4–5** (SIDEWINDER sports + JUGGERNAUT war truck, both fielding the new $3,800 20mm Autocannon) with briefing intel cards, and **win purses now scale with tier** (~$600–1800 at tier 5) so the store is reachable. **Clone respawn has stakes**: driver death decants a fresh clone at your last facility for a $150 fee with proper messaging. **Music**: city + combat tracks (Kevin MacLeod, CC-BY, attribution recorded) on a new Music bus with its own Settings slider, switching automatically when a match goes live. New CC0 Poly Haven textures (corrugated iron, gravel road) staged for the road-encounter work.

- Build 2026-06-11-120 (improvement loop complete, iterations 39–50 of 50): **driver survivability bug fixed** — every hit was chipping driver HP through INTACT armor (≈8s driver death under automatic MG fire); per the master spec, drivers now take damage only from direct driver hits or overflow through a destroyed section, verified on-camera with fights now running past 17s with healthy drivers. PlayerStatusHud in-bar labels gained contrast shadows; Garage Service Bay/Upgrades and City Ops pages verified inheriting the design system; final 22-frame all-screens sweep (city/garage/workshop tabs, pause, splash, live arena) confirms the whole game renders on the new flat dark command-console language with Rajdhani/Inter type. Playbook updated with the load-bearing lessons (RTS zoom is sacred, bare-vs-suffixed variant material names, no box meshes for airborne VFX, the chip-through rule).

- Build 2026-06-11-119 (improvement loop, iterations 27–38 of 50): brighter stadium light beams/pools tuned for the RTS camera; HUD mini-vehicle default paint now matches the player's actual yellow (Body_1) instead of generic green; screenshot harness gained a `splash` target (Great Lakes Forge splash verified), per-tab drawer paging, mid-fight mine drops, and a longer combat window — which captured and verified the full LOSS flow (gold-edged banner + themed Salvage & Repairs results) and on-camera enemy section fire at gameplay zoom. Garage Details, Workshop Ammo, City Ops pages verified inheriting the design system with no extra work — the theme is now doing the lifting.

- Build 2026-06-11-118 (improvement loop, iterations 1–26 of 50): camera restored to the RTS/toy-cars zoom per user direction (offset 0,29,23 — documented as load-bearing; gentle lookahead retained). **Two real bug fixes found on camera:** (a) vehicle paint variants never applied — the visible meshes use bare material names ("Body") while only hidden variants carry "_N", so the swap regex never matched; player is now yellow, enemies dark red; (b) the mysterious "white cards" stuck to vehicles were box-mesh smoke puffs and muzzle flashes flat-lit from above — both are soft spheres now. **HUD:** ValueBar rebuilt as custom-drawn rounded/bordered bars with bright fill-cap lines and label shadows (every section/tire/speed/RPM bar upgraded at once); km/h speed gauge; display-font HUD headers; gold lock-target name; radar rebuilt as a proper scope (dark disc, range rings, bearing ticks, rotating sweep, heading-oriented player wedge). **Screens:** arena briefing now uses named tier intel cards (SCRAPPER/ROADRUNNER/WARRIG with loadout hints, gold selection) plus amber warning strip; pause menu gained a PAUSED display title and build-id footer; Settings/modals/post-match/tow-panel/action-prompts all moved onto the display-font + accent language; gold-underline tabs everywhere; theme now covers sliders, scrollbars, and tabs; console overlay solidified. **Arena:** worn painted center ring + start-lane hash markings give the bowl a sports-venue identity; damage smoke/fire rescaled for the RTS camera with emissive fire; post-round text is now a real gold-edged banner strip. Screenshot harness can now page through every drawer tab per screen.

- Build 2026-06-11-117: **visual overhaul pass driven by a real screenshot feedback loop.** Highlights:
  - **Screenshot harness** (`Scripts/App/ScreenshotHarness.cs`): launch with `godot --path . --windowed -- --shot=<city|garage|workshop|arena|pause> --shot-dir=<dir> [--shot-tier=N]` to auto-navigate (including starting + auto-driving a live arena match with a chase driver that steers at the enemy and recovers when wedged), capture PNGs, and quit. Runs against a throwaway sandbox save (`user://savegame.screenshot.json`, recreated per run) so captures never touch the player's real save.
  - **Fog-of-war bug actually fixed**: the 40m blackout curtains from build 103 stood between the chase camera and the entire arena whenever the player was in/near the south start lane, blacking out the screen at every match start. Curtains and the fragile fog overlay are gone; out-of-bounds darkness comes from the WorldEnvironment's near-black background, which can never occlude the camera.
  - **Arena look**: camera pulled from ~37m to ~21m (same pitch — cars finally read as cars), combat lookahead frames the engagement between player and lock, impact camera shake, sun shadows (orthogonal) + steeper sun, neutral dark asphalt (the warm tint read as orange wood), real PolyHaven rock walls preferred over the flat generated rust set, and the real obstacle prop pack now loads (the code pointed at `Assets/Models/Arena/Obstacles`; the actual folder is `Assets/Models/ArenaObstacles`) with auto-fit + ground-snap for imported models.
  - **Arena layout**: the center barrier spine sat exactly on the spawn-to-spawn axis, so AI and players rammed it nose-first every match; cover now flanks an open jousting lane (staggered center clusters).
  - **Combat feel**: .50cal MG retuned from 1000ms/16dmg to 150ms/5dmg (automatic feel, constant tracers), guided missiles from 900ms/54dmg to 2200ms/30dmg (no more 3-second deletes), stretched emissive bullet bolts, thicker emissive tracer beams, bigger muzzle flashes/sparks (all bloom under the arena glow), and dark gunmetal weapon proxies (receiver + barrel) instead of white boxes.
  - **UI foundation**: bundled OFL fonts (Rajdhani display + Inter body, `Resources/UI/Fonts/`), `GameUiTheme` rebuilt as a flat dark command-console design system (near-opaque panels, hairline borders, accent color reserved for states/CTAs, real hover/pressed/focus button states), premium shell/section/inset styles flattened to match (sections separate by tone with a 3px accent bar, not glow), display-font headings on City/Garage/Workshop/Arena briefing, banner squiggle/baked-text art removed.
  - Save-repair note: capture runs before sandboxing consumed the active vehicle's missiles in the real save; restored to 10 (backup at `savegame.json.bak-claude`).

- Build 2026-06-11-116: balance + docs wrap-up for the 2026-06-11 ten-iteration pass. Tier-3 arena enemy (light truck, smoke dropper) now carries more MG ammo (240) and smoke charges (8) to compensate for trading its mine away — it sustains pressure longer and leans harder into its evasive identity. AI that ever resolves an uncontrolled weapon slot now backs off for 0.5s instead of dry-firing every frame (defensive guard; current builds never hit it). PROJECT_STATE / NEXT_TASK / ASSISTANT_PLAYBOOK updated: new extension-point notes (`VehicleDamageVfx`, `ArenaHazardTelemetry`, mount/computer rule helpers, `GameSettingsStore` trim coupling, shared speed-factor math) and an in-game verification checklist for the whole pass.

- Build 2026-06-11-115: real **Settings** page (replaces the pause-menu stub). The pause menu now swaps to a Settings panel with Master / Effects / Engine volume sliders (applied live to the audio buses, layered on top of the intended mix trims so 100% = default mix) and a Fullscreen toggle that switches immediately and persists as the startup preference. Settings persist to `user://settings.json` via a new `GameSettingsStore` (separate from the save game), are loaded+applied during boot, and startup now honors a windowed preference instead of always forcing fullscreen. Escape backs out of Settings to the pause menu first, then resumes.

- Build 2026-06-11-114: Workshop weight/performance UX (PROJECT_STATE priority "weight/capacity UX"). The persistent Workshop context strip now carries a live second line for the **planned** loadout: total mass with chassis/weapons/ammo breakdown plus the estimated top speed as % of stock. The weight→speed formula was extracted from `VehiclePawn` into shared `VehicleMassMath.ComputeSpeedFactor(...)`, so the menu prediction is exactly the math the arena applies at runtime. The line updates live as weapons/ammo dropdowns change and after ammo purchases.

- Build 2026-06-11-113: arena atmosphere pass (all procedural, no assets). Four corner **light towers** now stand inside the bowl: dark metal poles with emissive lamp heads aimed at center, a translucent additive beam cone so the throw reads from the top-down camera, and a real (shadowless) `SpotLight3D` per tower that pools warm light on the floor and brightens vehicles driving through. The arena also gains a scene-owned `WorldEnvironment`: near-black background beyond the blackout curtains, a cool ambient lift so shadowed vehicle sides stay readable, ACES tonemapping, and conservative glow so lamp heads / muzzle flashes / section fires bloom slightly. Everything lives under the arena world node, so it's removed with the arena and doesn't affect menu screens.

- Build 2026-06-11-112: arena HUD awareness polish. (a) The radar now shows **hazard blips**: the player's own mines as gold diamonds (enemy mines stay hidden — that's the point of mines), oil slicks as violet dots, and smoke clouds as gray rings; hazards beyond radar range are omitted instead of pinned to the rim. Hazards publish through a small `ArenaHazardTelemetry` registry at 10 Hz so the radar never reaches into combat-view internals. (b) The HUD weapons list now flags ammo state — `OUT` when empty and `LOW` when a reserve falls under a quarter of that ammo kind's workshop refill target (kind-aware, so 3 missiles read fine while 3 MG rounds read LOW).

- Build 2026-06-11-111: targeting computer combat rules (master-spec pillar). The installed targeting computer now actually governs combat capability for player and AI alike: (a) **control groups** — only the first `MaxActiveWeaponGroups` non-utility weapons are controllable; weapons beyond that read `[OFFLINE]` in the HUD weapons list and refuse to fire with a throttled explanation in the log (rear droppers are exempt utility-release systems and never consume a group); (b) **auto-tracking** — turret mounts only track the locked target when the computer has `AutoAimSlots > 0`, otherwise the turret stays slaved to the hull's forward axis (lock-range missile behavior was already computer-gated). New shared helpers live in `ArenaWeaponLoadoutResolver` (`IsWeaponComputerControlled`, `HasAutoTrackCapability`, `IsUtilityWeapon`). Basic vs Advanced computers are now a real upgrade decision.

- Build 2026-06-11-110: weapon mount-location rules (master-spec customization constraint). `WeaponDefinition` gains `AllowedMountLocations` (empty = any mount) and all three droppers (mine / oil / smoke) are now flagged **Rear-only** in their defs. The Workshop Hardpoints tab filters each mount's weapon list to legal options and annotates restricted weapons ("· rear only"); a legacy loadout with a now-illegal weapon keeps it visible flagged `[!]` so nothing is silently uninstalled. All starter/enemy build presets already mount droppers on the rear B1 hardpoint.

- Build 2026-06-11-109: visible per-section vehicle damage (master-spec visual goal). Vehicles now show where they are hurt: once a section's armor is stripped and structural HP drops, that section starts venting gray smoke wisps (faster and darker as damage worsens), and critically damaged sections gain a flickering fire glow. Implemented as a new `VehicleDamageVfx` child node on each `VehiclePawn`, fed at 10 Hz from the authoritative combat state for both player and enemy; defeated wrecks keep smoking/burning through the salvage phase. `SmokePuff3D` gained a configurable spawn variant (color/size/ttl/rise) reused by this system.

- Build 2026-06-11-108: smoke-screen visual refinement. The smoke cloud is now a wispy multi-puff cluster (two fat core puffs plus several smaller offset billows, each with its own gray tone, drift vector, and breathing pulse) instead of a single translucent sphere, so it churns and rises like real smoke from the top-down camera. Gameplay footprint/LOS behavior is unchanged — the cluster still grows/fades on the same radius and timing the line-of-sight test uses.

- Build 2026-06-11-107: oil-slick readability pass. The puddle is now an irregular multi-blob slick with a faint iridescent sheen (instead of a perfect dark circle), it spreads outward over the first moments and dries up (fades) over its final seconds, and the gameplay slip radius follows the visible spread. Deployment now pops a quick dark droplet splash so the drop moment reads at speed, and the vehicle HUD shows a pulsing **LOW GRIP - OIL** warning strip while the player vehicle is sliding on oil (`VehiclePawn.IsOnOilSlick`).

- Build 2026-06-01-106: added a **smoke-screen dropper** (`wpn_smoke_dropper` / `ammo_smoke_std`), completing the AutoDuel "dropper" trio (mine / oil slick / smoke). It releases a translucent smoke cloud behind the vehicle that spoils line of sight: hitscan shots whose sight line crosses an active cloud scatter (4x spread) and tracking-missile lock cannot be acquired through it (the missile falls through to an unguided shot). The cloud grows then fades over ~7s. Reuses the shared dropper deployment + AI path; arena enemies now field the full trio (mine on Tier-1, oil on Tier-2, smoke on Tier-3). The line-of-sight test is a no-op when no smoke exists, so normal combat is unchanged.

- Build 2026-06-01-105: added an **oil-slick dropper** hazard weapon (`wpn_oil_dropper` / `ammo_oil_std`) — a rear-deploy "dropper" that lays a non-damaging puddle which sharply reduces traction (lateral grip, drive grip, and steering) for any vehicle that drives over it, with grip recovering shortly after the vehicle leaves the oil. It reuses the existing mine deployment path for both the player and the AI (`WeaponType.OilSlickDropper`, `AmmoKind.Oil`), and the Tier-2 arena enemy now carries it so players actually encounter oil hazards in matches. Also upgraded mine detonation to use the shared projectile impact burst (flash + shockwave + sparks) instead of the old zero-length "shot at self" flash hack.

- Build 2026-06-01-104: fixed the arena fog-of-war reading as if it covered the whole screen. The fog overlay plane was anchored at y=2.75 (near wall-top height), so from the tilted top-down camera its opaque "outside" region parallax-projected up across most of the view, leaving only a small cleared window. The overlay is now anchored coplanar with the floor (y~=0.12), which makes it provably shade only the out-of-bounds floor regardless of camera angle, and it has been re-enabled. The clear footprint was aligned 1:1 to the inner wall faces (now there is no parallax) so the full combat bowl + start lanes stay clear, and the fog color was softened to deep-shadow (90% opacity) instead of pure black. Perimeter blackout curtains still mask the default-gray background beyond the arena.

- Build 2026-03-23-103: made north/south start-lane escape targeting much more explicit so arena enemies funnel back through the center opening instead of continuing to chase/fight while trapped against the outer wall. Also lowered/hardened the fog layer, added blackout perimeter curtains so the space beyond the walls reads black from the gameplay camera, removed the extra separator line from the Escape menu, and tightened the post-round banner into a smaller centered multiline message.

- Build 2026-03-23-99: hardened arena enemy containment around the north/south dead-end start lanes so AI now treats those outer boxes as escape/back-to-arena zones instead of repeatedly grinding into the outer wall. Also added a top-down fog-of-war overlay above the arena perimeter so the space outside the main combat bowl fades into shadow instead of staying fully exposed.

- Build 2026-03-22-97: refined the left-drawer management screens around clearer information architecture instead of just resizing the shell. City Hub now uses Overview/Travel/Ops tabs with compact route rows and a cleaner command-first default view, Garage now splits into Fleet/Vehicle Details/Service Bay/Upgrades tabs and trims the oversized detail preview card, and Workshop adds a persistent vehicle-context strip while tightening the hardpoint cards and ammo/loadout tab.

- Build 2026-03-22-96: replaced the drifting centered-shell behavior on the premium management screens with scene-owned left-drawer layout code that directly sizes the panel from the live viewport. City Hub, Garage, and Workshop now author from a real left-edge drawer baseline, while Garage and Workshop also introduce tabbed sub-menus so fleet/service-bay and overview/hardpoints/ammo content no longer fight for one long page.

- Build 2026-03-22-95: shifted the premium management menus from centered floating shells to left-edge drawer panels that stay under half-screen width so the background scenes remain visible. City Hub, Garage, Workshop, and the arena briefing/results now all bias to the left side, City/Garage/Workshop stack their main sections vertically for denser drawer-style flow, and Garage once again uses the original optional `Assets/Images/Garage/Garage1.png` background instead of the generated mockup backdrop.

- Build 2026-03-21-91: added a `VisualScale` option to the shared `ResponsivePanelLayout` helper and set the premium City/Garage/Workshop shells to render at 70% of their prior visual size while keeping their full authored logical layout. This makes the whole management UI read noticeably smaller so more of the background scenes remain visible without reintroducing panel-content clipping.

- Build 2026-03-21-90: scaled the premium City/Garage/Workshop shells down to a floating ~75%-of-viewport presentation so more of the authored background art remains visible around each menu. Continued the planned Workshop polish pass by rebuilding mount rows into labeled service-console cards and ammo rows into clearer resupply cards with reserve badges, progress bars, and cleaner purchase actions.

## 2026-03-21-89
- Fixed the `CityShell` compile break from the premium City Hub pass by importing `WastelandSurvivor.Core.State`, which resolves the new `VehicleInstanceState` references used by the active-vehicle summary helper.
- Updated `Docs/AI_WORKFLOW.md` to match the canonical zip-baseline process so all workflow docs now consistently point future iterations at the latest uploaded `wasteland-survivor.zip`.

## 2026-03-21-88
- Premium-pass converted `CityShell` onto the shared HTML-first hybrid visual system: new shell/header/metric-tile layout, travel route cards, grouped service actions, active-vehicle showcase, and richer city/progression summaries instead of the old plain menu list.
- Added generated `city_banner.svg` art and reused the shared premium style helpers so City Hub now feels visually aligned with Garage and Workshop.
- Corrected the workflow docs back to the canonical zip-baseline process: always start from the latest uploaded `wasteland-survivor.zip` and deliver a drop-in code/docs zip that excludes local assets/build folders.

## 2026-03-21-87
- Added `Docs/UI/HTML_FIRST_HYBRID_DIRECTION.md` and updated onboarding/playbook docs so premium screens now follow an HTML-first hybrid UI process: HTML/CSS mockups drive the visual direction, while runtime UI remains native Godot for now.
- Generalized premium shell/section/inset/metric style helpers in `GeneratedUiArt` so Garage and future management screens can share one stronger visual language instead of screen-specific stylebox drift.
- Rebuilt `WorkshopView.tscn` into a cleaner two-column service-bay layout with grouped premium panels for the active vehicle, actions, powertrain, weapon mounts, and ammo resupply.
- Updated `WorkshopView.cs` bindings and visual configuration to use the shared premium shell/section styling and keep the generated banner/icon treatment consistent with the new direction.

## 2026-03-21 (Build: 2026-03-21-86)

### Garage build fix
- Fixed the new garage selected-vehicle/footer card components to import `WastelandSurvivor.Core.IO`, resolving the `CS0246` `DefDatabase` compile errors introduced in the garage mockup visual pass.

## 2026-03-21 (Build: 2026-03-21-85)

### Garage visual overhaul
- Rebuilt the Garage screen around the approved mockup: fleet roster on the left, selected-vehicle hero panel on the right, and a lower summary strip so the core management view now reads much closer to the target concept art.
- Added authored garage UI art crops derived from the supplied mockup (header circuit treatment, roster/hero vehicle art, and a blurred themed backdrop) and routed them through the new `GarageArtCatalog` helper so the screen still falls back cleanly when a vehicle class has no bespoke art yet.
- Reworked garage fleet entries into richer roster cards with inline readiness metrics and art-backed class previews, while keeping the existing rename / set-active / strip / sell / repair / upgrade flows intact.

## 2026-03-21 (Build: 2026-03-21-83)

### Arena AI / boundary escape
- Hardened arena boundary handling so enemy steering now clamps lane targets to safe drive bounds, blends toward an explicit interior escape point near the outer walls, and caps/brakes/reverses throttle sooner when the vehicle is facing out of bounds.
- Made recovery behavior boundary-aware so wall-stuck enemies try to reverse or drive back toward the arena interior instead of grinding along the perimeter.

### Garage UI
- Reworked the Garage screen into a cleaner full-screen two-column layout with dedicated Fleet, Fleet Actions, Repair Bay, and Permanent Upgrades sections.
- Removed the main screen scroll container so only the fleet roster list scrolls, and condensed the repair/upgrade summaries into clearer multi-line service panels.

## 2026-03-21-81
- Fixed arena locational damage fallback so direct hits that strike a vehicle body collider now infer front/rear/left/right/top/undercarriage from the actual impact point instead of defaulting toward front-section damage.
- Reworked Garage into a two-column layout with fleet/actions on the left and repairs/upgrades on the right so the screen reads more like a management panel and requires less vertical scrolling.

## 2026-03-21 (Build: 2026-03-21-80)

### Build-fix follow-up
- Fixed the arena wall-bias helper so the boundary-pressure accumulator no longer captures an `out` parameter inside a local function, resolving the `CS1628` compile error in `ArenaVehicleAiDriver`.
- Fixed the garage repair-section bulk patch math to use `System.Math` explicitly, resolving the `CS0103` compile errors in `GarageView`.

## 2026-03-20 (Build: 2026-03-20-79)

### Arena AI / navigation
- Added explicit arena-boundary steering bias on top of the feeler/lane system so enemies stop treating the outer wall like a valid pursuit lane. When they get too close to the arena boundary, steering now biases them back toward the playable interior while still preserving aggressive pursuit once a line opens.
- Lane scoring now also considers predicted boundary safety and inward-vs-outward travel direction, which helps the enemy choose lanes that stay driveable instead of committing to wall-hugging lines.
- Recovery now treats “throttle/steer effort while pressed against the arena boundary and facing outward” as a stuck signal, so wall contacts escalate into reverse/turn escape behavior sooner instead of parking forever against the outer wall.

### Arena obstacle collision polish
- Rotated arena prop collision bodies so concrete barriers, wrecks, tires, and trash cans no longer keep oversized axis-aligned collision boxes when the visuals are angled.
- Tightened several small-prop and wreck collision sizes and nudged a few side/corner props outward to preserve clearer lanes and reduce invisible-looking hits.

### Garage UI / repair flow
- Reworked the garage repair controls so they read like actual service actions instead of prototype one-point buttons. The garage now exposes a clear full-service cash repair action plus “patch armor with scrap” / “patch tires with scrap” buttons that repair as much damage as the current scrap balance allows.
- Expanded the garage layout to use more of the screen and widened the repair action row so the new service buttons fit cleanly.
- Repair labels now surface both money and scrap context, along with the current full-repair cost, so the player can understand the tradeoff between cash repairs and scrap patching at a glance.

## 2026-03-20 (Build: 2026-03-20-78)

### Arena AI navigation polish
- Reworked enemy driving around arena cover from a single blocked/not-blocked check into a multi-feeler navigation pass that scores direct, left-lane, and right-lane steering candidates before committing to a route.
- Added persistent lane bias plus short lane-commit timing so enemies stop sawing back and forth at every barrier edge and more often pick a usable driving lane around the new structured cover layout.
- Replaced the old collision-only unstuck trigger with repeated low-progress + control-effort detection, then added a chained recovery state that reverses, biases away from the tighter side, and varies recovery duration/turn direction to avoid getting trapped in endless oscillation.
- Kept the stronger tactical/combat behavior intact so enemies still push aggressively and resume firing once a lane opens.

### Arena layout follow-up
- Nudged the west/east chicane clusters and several corner props outward to preserve clearer side lanes and reduce obvious dead-end trap placements without emptying the arena.

## 2026-03-17 (Build: 2026-03-17-73)

### Arena salvage prompt follow-up
- Fixed the post-enter salvage prompt gating so the arena salvage menu still surfaces towing and strip options after the player temporarily enters and exits the recovered enemy vehicle during the salvage phase.
- Stopped treating the claimed-vehicle flag as a blanket blocker for tow/scrap interactions, which was hiding valid salvage actions after vehicle-entry swaps.

## 2026-03-17 (Build: 2026-03-17-72)

### Arena recovery-zone feedback
- Moved the salvage/tow status HUD panel to the left side of the screen so it no longer overlaps the vehicle HUD during the post-win salvage phase.
- Added a world-space **recovery zone indicator** over the player start box during salvage/recovery, with color/state changes for general salvage guidance, active towing, and final recovery/auto-finish.
- Expanded the tow-status messaging so it explicitly calls out the highlighted recovery zone and confirms when the player has reached it while towing a wreck.

## 2026-03-17 (Build: 2026-03-17-69)

### Arena salvage prompt menu + tow shortcut
- Reworked the arena on-foot salvage prompt into a compact multi-action interaction menu so recovered-vehicle entry no longer hides towing or strip options on the same wreck.
- Changed the drivable enemy-wreck prompt text from **Hijack Vehicle** to **Enter Vehicle** while keeping the same claim-and-drive-home recovery behavior behind the scenes.
- Moved **Attach Tow Cable** to the **T** key and added a dedicated **R** shortcut for **Strip Wreck**, with the prompt menu surfacing all available actions and their keys together.
- Updated the arena/post-encounter wording to describe enemy-vehicle recovery as a claimed vehicle rather than a hijack.

## 2026-03-17 (Build: 2026-03-17-68)

### Arena salvage / hijacking
- Added post-win **enemy vehicle hijacking** in the arena salvage phase. When the defeated enemy vehicle is still drivable enough, the on-foot interaction prompt now lets the player hijack it instead of only stripping or towing it.
- Hijacking now claims the enemy vehicle into the player's garage immediately, switches it to the active vehicle slot, and lets the player drive it out of the arena during the salvage phase while the player's original vehicle remains owned and stored.
- Added a small salvage-side drivability heuristic so only wrecks with enough intact tires/structure can be hijacked and driven; more damaged wrecks still fall back to the strip/tow loop.
- Post-encounter rewards now surface hijacked vehicles alongside towed recoveries and battlefield scrap.

## 2026-03-17 (Build: 2026-03-17-67)

### Menu button polish + shared vehicle showcase cards
- Moved menu/action button icons so they sit directly beside button text with a tighter separation, and nudged button icon sizing up slightly for better readability.
- Added a shared `VehicleShowcaseCard` component and installed it in both the Garage and Workshop screens so each screen now shows a larger active/selected vehicle presentation block with a deterministic preview plus readiness stats (condition, mass, weapons, storage, towing, repairs).

## 2026-03-17 (Build: 2026-03-17-66)

### UI / Presentation
- Increased the authored width and minimum responsive width of the Garage and Workshop screens so both panels render about 25% wider before responsive stretching.
- Replaced the menu icon set with more game-like transparent SVG art and extended icon coverage to the City Hub and pause menu in addition to Garage/Workshop.

- Build 2026-03-17-65: increased the authored and responsive heights of the City Hub, Garage, and Workshop panels so the main menu reads about 20% taller and the Garage/Workshop screens read about 30% taller, while also reducing top/bottom spacing so those two screens reach closer to the bottom of the viewport.

- Build 2026-03-17-64: fixed the latest Garage/Workshop responsive-pass regressions by switching the outer menu-panel layout helper to a more Godot-native anchors/offsets approach, stretching those screens nearly to the bottom of the viewport with preserved margins, and retuning generated UI icons to readable in-button sizes without the earlier oversized/tiny swings.

## 2026-03-17 (Build: 2026-03-17-60)

### Responsive desktop UI fit pass
- Fixed `ResponsivePanelLayout` so it no longer forces panels to stay larger than the real viewport when the screen is smaller than the authored minimum size. This prevents menu panels from hanging off-screen at lower desktop resolutions.
- Reworked City Hub, Garage, and Workshop so their variable-height content lives inside scrollable body regions while the primary action buttons stay anchored inside the panel.
- Swapped several fixed horizontal button rows to grid-based layouts so common desktop widths can compress cleanly without controls overlapping or disappearing.

## 2026-03-17 (Build: 2026-03-17-59)

### Garage / Workshop visual polish
- Replaced the Garage `ItemList` fleet rows with a custom scrollable vehicle-card list so preview images no longer crop awkwardly and each vehicle can show cleaner title / chassis / stat lines.
- Added generated UI art assets (garage/workshop banner strips plus button/action icons) and wired them into the Garage and Workshop screens for a stronger in-world presentation without relying on external `Assets/` art.
- Styled Workshop hardpoint rows and ammo resupply cards with generated icons/panel treatments so the screen reads more like a fitting bay than a plain form.

## 2026-03-17 (Build: 2026-03-17-58)

### Build fix
- Fixed the Garage compile break by correcting the Godot integer vector type used for the fleet-list icon size (`Vector2I`, not `Vector2i`).

## 2026-03-17 (Build: 2026-03-17-57)

### Garage UX
- Made the Garage fleet list shorter/scrollable and removed raw vehicle GUIDs from the player-facing list/details text.
- Added per-vehicle rename support with a modal rename/reset flow backed by persisted vehicle custom names.
- Added deterministic rendered vehicle preview icons to Garage fleet entries so each vehicle is easier to identify at a glance.
- Centralized vehicle display-name / preview-color presentation rules in shared helpers instead of re-embedding them inside `GarageView`.

## 2026-03-16-56
- Added garage-side salvage choices for owned non-active vehicles: the Garage screen now shows strip-for-scrap and sell actions for the currently selected vehicle.
- Added garage valuation math (`VehicleRecoveryValueMath`) so recovered wrecks and spare fleet vehicles surface condition-aware scrap and sale values.
- Added modal confirmations plus session-backed strip/sell mutations that remove the selected vehicle from the player fleet and award scrap or cash immediately.

## 2026-03-16-53
- Refactored the `VehicleStatusHud` preview path again so it no longer depends on main-viewport crop capture or hidden off-screen render targets.
- The HUD now builds the preview from the real vehicle `Visual` subtree inside a live `SubViewportContainer` attached directly to the exact scene-owned `VehiclePreviewCanvas` control.
- Best-effort texture freeze is still attempted after the live preview renders, but if readback fails the embedded live preview stays visible instead of falling back to the green vector icon.

## 2026-03-16-52
- Fixed a compile error in `VehicleStatusHud` by removing the invalid `VehiclePawn.VehicleDef` reference from the arena portrait crop sizing helper.
- Documented that `VehiclePawn` does not expose `VehicleDef` publicly, so HUD/UI code should avoid reaching into pawn internals for preview sizing.

## 2026-03-16-46
- Build 2026-03-16-47: upgraded the deterministic `VehiclePreviewCanvas` so the HUD preview reads more like the original intent: a richer top-down vehicle card with class-specific body shapes, cabin/glass detail, mount rendering, tow indicator, and live damage/tire state overlays driven from the active vehicle runtime.
- Reworked VehicleStatusHud preview again to draw directly into the on-screen `VehiclePreviewHost` control via a dedicated `VehiclePreviewCanvas` script instead of spawning a runtime child control.
- This is aimed at the likely real failure mode from the last several passes: we were mutating preview logic, but not necessarily the exact control that was actually rendering on-screen.
- `VehicleStatusHud.tscn` now binds `VehiclePreviewHost` directly to `Scripts/UI/VehiclePreviewCanvas.cs`, and `VehicleStatusHud.cs` updates that control directly.

## 2026-03-16 (Build: 2026-03-16-45)

### Build fix
- Fixed the latest `VehicleStatusHud` compile break in the deterministic HUD preview path. `DrawColoredPolygon(...)` in Godot C# expects a single `Color`, not a `Color[]`, so the heading-chevron draw call now uses a single tint color and compiles cleanly again.

## 2026-03-16 (Build: 2026-03-16-44)

### HUD / vehicle preview
- Replaced the repeatedly failing `VehicleStatusHud` 3D viewport preview path with a deterministic in-HUD top-down vehicle render. The center box now always draws a readable top-down representation of the active player vehicle using the live vehicle class, body color, and weapon mount layout instead of depending on brittle `SubViewport`/proxy-camera wiring.
- The HUD preview now updates directly from the active player vehicle source each frame, including body-color refresh and a live world-heading marker, so the preview no longer goes blank when the 3D preview path fails.

### Docs
- Updated the assistant playbook/project-state notes to stop retrying the fragile viewport clone path for the vehicle HUD preview.

## 2026-03-16 (Build: 2026-03-16-42)

### HUD / vehicle preview
- Replaced the failing duplicated-visual HUD preview path with a live same-world follow-camera preview. `VehicleStatusHud` now points its runtime-built `SubViewport` at the active arena `World3D` and drives a dedicated orthographic camera directly above the player vehicle, so the HUD shows the real vehicle instead of a stale feed or blank clone.
- The preview camera now updates from the live player pawn each frame and rotates with vehicle yaw, while falling back to the existing static proxy box preview when no active player vehicle is available.

### Docs
- Updated the assistant playbook/project-state notes so future passes stop retrying the duplicated-visual preview dead end and use the runtime viewport + live follow-camera path instead.

## 2026-03-16 (Build: 2026-03-16-41)

### Build / HUD preview
- Fixed the `VehicleStatusHud` compile break introduced in the preview-bounds pass. Replaced the unsupported `MathF.Clamp(...)` calls with `Math.Clamp(...)` and rewrote the bounds merge traversal so it no longer captures the `out` AABB inside a local function.
- Tightened preview bounds collection to walk the duplicated preview tree iteratively and read local AABBs from visual nodes more safely, which should keep the HUD preview path compiling cleanly while preserving the latest isolated-viewport cloning work.

### Cleanup
- Removed the dead-code / nullable warnings that were left behind in `ArenaRealtimeView` around interact-highlighting and vehicle-HUD visibility.

## 2026-03-16 (Build: 2026-03-16-40)

### HUD / vehicle preview
- Fixed the `VehicleStatusHud` preview going blank after the stale-viewport cleanup. The HUD now duplicates the live vehicle `Visual` subtree directly, strips non-visual/runtime-only nodes from that duplicate, and fits the clone into the isolated preview viewport using computed bounds instead of relying on the older mesh-only clone path.
- This closes the likely imported-model edge case where the earlier mesh-only preview clone dropped Godot import node types and produced an empty/black HUD preview even though the preview wiring itself was finally correct.

### Docs
- Updated the assistant playbook and project-state notes with the new HUD-preview cloning rule so future passes do not fall back to the older mesh-only clone dead end.

## 2026-03-16 (Build: 2026-03-16-39)

### HUD / vehicle preview
- Fixed the `VehicleStatusHud` preview root cause more aggressively: the scene no longer ships with a baked `SubViewport`/camera tree for the vehicle preview. `VehicleStatusHud` now builds the only preview viewport at runtime under a plain host panel, which prevents Godot from silently rendering a stale arena-linked viewport feed in the HUD.
- `VehicleStatusHud` now resolves the preview host directly (not only through binder state), recreates the preview container/view/world from that host, and explicitly makes the preview camera current in the isolated viewport.

### Docs
- Updated the assistant playbook and project-state notes with the HUD preview root-cause fix so future passes do not keep tuning the wrong viewport path.

## 2026-03-16 (Build: 2026-03-16-38)

### HUD fixes
- Rebuilt the `VehicleStatusHud` preview viewport tree at runtime so it no longer reuses a stale scene-authored viewport/world feed.
- `ArenaRealtimeView` now passes the live player vehicle directly into `VehicleStatusHud`, which avoids group-resolution drift and keeps the HUD preview wired to the correct pawn.
- Moved the vehicle HUD farther down so it sits cleanly under the player HUD with visible spacing.

## 2026-03-16 (Build: 2026-03-16-37)

### HUD / arena UI
- Fixed the VehicleStatusHud wiring so `ArenaRealtimeView` now actively resolves/replaces the HUD with the typed `VehicleStatusHud` script instead of silently falling back to the plain-control binder. This unblocks the actual preview-camera logic instead of leaving the preview stuck on the live arena feed.
- VehicleStatusHud preview cloning now copies only the vehicle visual mesh hierarchy into the isolated SubViewport world and strips cameras/lights/collision/audio nodes, preventing preview leakage from unrelated world/camera content.
- Moved the VehicleStatusHud slightly lower so it sits below the player HUD with a clearer margin instead of overlapping.

## 2026-03-15 (Build: 2026-03-15-36)

### Arena flow
- Fixed the post-win loading overlay hang when returning to the player start box. The auto-finish timer now continues even if the arena pawns/runtime references change during the transition, and the loading overlay stays up only until the post-encounter panel is actually shown.

### HUD / vehicle preview
- Reworked `VehicleStatusHud` preview logic again: the HUD preview now keeps a fixed straight-down camera in its own isolated `SubViewport` world and rotates the duplicated player-vehicle preview pivot opposite live vehicle yaw. This avoids the previous incorrect camera-angle/world-feed behavior and keeps the player's car facing a stable forward direction in the HUD.

### Arena combat
- Guided missile base damage increased again from 36 to 54 to match the requested 50% bump from the prior tuning pass.

## 2026-03-15 (Build: 2026-03-15-34)

### Arena combat / towing polish
- Guided missiles hit harder again: `wpn_missile` base damage increased by another 50% so lock-on hits feel much more decisive.
- Added a visible tow-cable read for arena salvage towing: the post-win tow now draws a bright cable/chain-style line between the player vehicle and the wreck so it is easier to read that the salvage is attached.

### HUD / presentation
- Fixed the VehicleStatusHud preview path to render inside its own isolated `SubViewport` world instead of leaking the main arena camera/world into the HUD panel.
- The HUD preview camera now follows the player vehicle yaw so the mini top-down car view keeps a stable vehicle-forward perspective while the car turns.
- Moved the match-ended hold-to-continue dialog higher again so it gets further out of the active salvage/play space.

## 2026-03-15 (Build: 2026-03-15-33)

### Arena combat / salvage flow
- Guided missiles hit harder now: base damage and splash radius were increased, and missile splash keeps more of its damage across the blast radius so close lock-on hits feel meaningfully stronger.
- Winning an arena match now tells the player they can salvage or tow the defeated vehicle, and driving the player vehicle back into the south starting box now automatically ends the salvage phase.
- Returning to the start box with a tow attached now immediately recovers the towed vehicle into the player garage as part of ending the round.

### HUD / presentation
- Reworked the VehicleStatusHud center preview to render a dedicated top-down clone of the player vehicle instead of a live shared-world camera feed, which keeps the preview focused on the player car instead of showing the arena from a bad angle.
- Moved the end-of-match continue dialog higher on the screen again so it sits closer to the top.

## 2026-03-15 (Build: 2026-03-15-32)

### Arena salvage / towing
- Added an arena salvage/towing v0 slice to `ArenaRealtimeView`: after a win, the player can exit the vehicle, walk up to the defeated enemy wreck, and either strip it for battlefield scrap or attach a tow line.
- Tow attachment now feeds the existing `TowingState` / `VehicleMassMath` path immediately, so the player vehicle HUD mass and runtime handling reflect the added towed weight during the salvage phase.
- Added a simple post-match tow preview so the defeated enemy vehicle follows behind the player vehicle while it is being towed.
- Exiting the salvage phase with a tow attached now recovers the defeated vehicle into the player's garage as a new owned vehicle, preserving its damaged runtime state as the recovered salvage baseline.

### Encounter / rewards
- Extended encounter state and the post-encounter presenter so the rewards panel can now show extra battlefield scrap recovered and any towed vehicle that was brought home.
- Added focused session helpers for post-win salvage actions (battlefield scrap rewards, active-vehicle tow-state updates, and towed-vehicle recovery) so the new feature does not have to mutate save data directly from the arena UI.

### Docs
- Updated project-state and next-task notes to mark salvage/towing v0 as completed and point the next step at enemy vehicle claiming / hijacking and towing polish.

## 2026-03-15 (Build: 2026-03-15-31)

### Refactor
- Added `Scripts/Arena/ArenaRaycastUtil.cs`, which now owns arena shot raycasts, hitbox parsing, hit-part classification, and self-hit exclusion logic instead of leaving those low-level helpers embedded in `ArenaRealtimeView`.
- Added `Scripts/Arena/ArenaDamageResolver.cs`, which now owns shared direct-hit and explosion vehicle-damage bookkeeping (including tire-pop detection and driver chip-through math) so arena combat screens stop duplicating low-level damage rules.
- Updated `ArenaRealtimeView` to use the new shared hit/damage helpers for ballistic shots, missile blasts, and mines, shrinking another combat-heavy slice out of the giant arena controller.

### Planning
- This is intended to be the last refactor-first pass before returning to feature work in the next thread.

### Docs
- Updated refactoring notes, project state, next-task notes, the assistant playbook, and the handoff prompt to mark the arena-refactor phase as effectively complete and point the next thread at feature work.

## 2026-03-15 (Build: 2026-03-15-30)

### Refactor
- Added `Scripts/Arena/ArenaPostMatchFlow.cs`, which now owns the arena match-ended hold-to-continue flow (modal lifecycle, fallback hold timing, outcome state, and cleanup) instead of keeping that state embedded in `ArenaRealtimeView`.
- Added `Scripts/Arena/ArenaPostEncounterPresenter.cs`, a small presenter/view-model builder that now computes the post-encounter rewards + repair/patch button state from `GameSession` / `DefDatabase` so the arena screen stays focused on wiring instead of reward-panel bookkeeping.
- Updated `ArenaRealtimeView` to consume the new post-match helpers, shrinking another arena-only results-flow slice out of the giant UI controller without changing gameplay intent.

### Cleanup
- Removed a few stale `ArenaRealtimeView` duplicate guard checks while touching the post-encounter flow.

### Docs
- Updated refactoring notes, project state, next-task notes, and the assistant playbook to record the new post-match/results helpers and the remaining recommended refactor target.

## 2026-03-15 (Build: 2026-03-15-29)

### Refactor
- Added `Scripts/Arena/ArenaTargetingController.cs` so arena target selection, target validity, target cycling, and target-indicator syncing live in a reusable helper instead of staying embedded in `ArenaRealtimeView`.
- Updated `ArenaRealtimeView` to use the shared targeting controller for player aim-target selection and enemy target resolution, shrinking another arena-only slice out of the giant UI controller.
- Added `Scripts/UI/BootSplashConfigStore.cs` and migrated `BootSplashView` to the shared `JsonConfigStore` path instead of hand-rolling boot-splash JSON parsing / fallback logic.

### Presentation
- Kept the shared full-screen cover behavior in place for startup splash art and optional PNG menu backgrounds so `studio.png`, `title.png`, and the existing menu-background art continue to fill the screen without skewing while preserving aspect ratio.

### Docs
- Updated refactoring notes, project state, next-task notes, and the assistant playbook to record the new arena targeting helper and boot-splash config-store path.

## 2026-03-15 (Build: 2026-03-15-28)

### Refactor
- Added `GameUiKit.UI.FullscreenTextureRectUtil` so full-screen splash and menu-art `TextureRect` nodes share one reusable "cover without skew" configuration path instead of relying on per-scene setup.
- Updated `OptionalBackgroundPresenter` to enforce the shared full-screen cover behavior whenever optional PNG background art is shown or cleared.

### Presentation
- Updated the boot splash image path (`studio.png` / `title.png`) to use the same aspect-ratio-preserving full-screen cover behavior as the other menu/background PNGs, so startup art now fills the screen without skewing.
- Authored `Scenes/UI/BootSplashView.tscn` with the same cover settings used by the other fullscreen background images for safer scene-level defaults.

### Docs
- Updated refactoring notes, next-task notes, and the assistant playbook to record the new shared full-screen image presentation helper.

## 2026-03-15 (Build: 2026-03-15-27)

### Refactor
- Added `GamePawnKit.Pawns.VehicleControlIntent` plus `VehiclePawnBase.ApplyControlIntent(...)` / `ClearControlIntent()` so player and AI controllers now push vehicle inputs through the same reusable control surface instead of hand-setting throttle / steer fields in multiple places.
- Extracted arena driving heuristics into `Scripts/Arena/ArenaVehicleAiDriver.cs`, shrinking `ArenaRealtimeView` and moving enemy steering / throttle / unstuck / fire-range tuning out of the giant UI controller.
- Added `Scripts/Arena/ArenaAiConfigStore.cs` + `Data/Config/arena_ai.json` so arena AI movement / firing behavior can be tuned from config instead of editing hard-coded numbers in the arena screen.

### Docs
- Updated refactoring docs, project state, next-task notes, and the assistant playbook to document the new shared vehicle-control intent path and arena AI config workflow.

## 2026-03-15 (Build: 2026-03-15-25)

### Refactor
- Added `Scripts/Core/IO/JsonConfigStore.cs`, a shared cached JSON config loader with reload support, default fallbacks, and consistent error logging for runtime-tunable config files.
- Migrated `DriverPawnConfigStore`, `VehicleBuildCatalogStore`, `WeaponVisualConfigStore`, and the UI theme loader to the shared config-store path instead of duplicating file existence checks, JSON loading, caching, and fallback code.
- Added small normalization passes so partially filled config files still produce non-null nested objects / dictionaries when future tuning data grows.

### Docs
- Updated the assistant playbook, code map, next-task notes, project state, and refactoring progress log to document the shared config-store pattern for future refactor passes.

## 2026-03-15 (Build: 2026-03-15-22)

### Build fix
- Fixed the new `VehicleBuildFactory` refactor to import `WastelandSurvivor.Core.IO`, restoring access to `DefDatabase` and resolving the follow-on compile break in the main game project.
- This unblocks the new vehicle build preset / factory path introduced during the broader refactor pass so starter and arena-opponent vehicle creation can compile again.

## 2026-03-15 (Build: 2026-03-15-21)

### Build fix
- Fixed the new `ResponsivePanelLayout` UiKit component to use a Godot-compatible empty `NodePath` default (`new("")`) instead of `NodePath.Empty`, which is not available in this Godot C# API surface. This resolves the UiKit compile failure introduced in Build 20.

### Docs
- Added a small Godot/C# compatibility note to the assistant playbook so future exported `NodePath` properties avoid the same regression.

## 2026-03-15 (Build: 2026-03-15-19)

### Arena missile VFX
- Guided missiles now emit a visible exhaust / trail particle effect while in flight, making lock-on shots easier to read from the fixed top-down combat camera.
- Missile detonations now spawn a dedicated impact / explosion effect with flash, shockwave, sparks, and smoke instead of only relying on the older hit flash.
- The new VFX scaffolding is deliberately reusable: `ArenaVfx` now exposes generic projectile trail / impact helpers, and the reusable projectile trail + impact nodes live under `Scripts/Arena/` so future ammo types can adopt the same pattern.

## 2026-03-15 (Build: 2026-03-15-18)

### Console
- Added a testing console command for player money: `money add <amount>` with `money <amount>` shorthand and `cash` alias support.
- The new command persists the updated balance immediately and reports the resulting total in the console.

## 2026-03-14 (Build: 2026-03-14-17)

### Workshop layout polish
- Made the Workshop screen fit cleanly on shorter displays by shrinking the Weapon Mounts and Ammo Resupply scroll sections so the overall panel no longer falls off the bottom of the screen.
- The Weapon Mounts section remains fully scrollable, but now takes up less vertical space so mounted-weapon ammo buying and the bottom action buttons stay visible.

### Docs
- Updated project-state / next-task notes to record the compact Workshop layout pass.

## 2026-03-14 (Build: 2026-03-14-16)

### Garage / Workshop presentation
- Garage now optionally displays `Assets/Images/Garage/Garage1.png` as its fullscreen background, and Workshop now optionally displays `Assets/Images/Workshop/Workshop1.png` the same way. Both screens fall back cleanly to the themed background color if those local asset files are absent.

### Workshop ammo UX
- Reworked Workshop ammo purchasing around the currently mounted weapons instead of a hard-coded 9mm-only strip. The ammo section now shows one row per mounted ammo type with current/target counts, the mounts using that ammo, kind-aware quick-buy buttons, and a fill-to-target action.
- `Refill All` now refills every ammo-consuming mounted weapon in the current loadout, including missile launchers, rockets, and mine droppers, while still respecting money and target ammo policies.

### Docs
- Updated project state / next-task notes and the assistant playbook to record the new Garage/Workshop background asset expectations and the broader workshop ammo flow.

## 2026-03-14 (Build: 2026-03-14-14)

### Arena environment
- Reworked the arena perimeter to read more like the still-image reference: outer walls now prefer a bundled rusted/industrial wall texture set, and the playable bowl is ringed with heavier wall supports, top rails, stepped spectator-bank silhouettes, and corner scrap masses.
- Added a larger outer apron floor beneath the perimeter structures so the visible area outside the battle floor stops reading like an empty flat void.
- The new wall look ships entirely under `Generated/Textures/Arena/` so it survives the normal drop-in zip workflow without depending on `Assets/`.

### Docs
- Updated project-state / next-task notes to capture the new arena-wall + outside-area pass.

## 2026-03-14 (Build: 2026-03-14-13)

### Arena floor visuals
- Shifted the arena floor from cool gray asphalt toward a warmer dirt/sand palette without removing the recent grime/skid/crack storytelling. The layered floor shader now applies a warm tint for both external asphalt packs and the bundled generated fallback, and the generated fallback albedo/overlay textures were rebalanced to match.

### Docs
- Updated project-state / next-task notes to record the warmer arena floor pass.

## 2026-03-14 (Build: 2026-03-14-12)

### Arena briefing fixes
- Made the arena Start/Resume control self-healing at runtime: if the scene node at `HudPanel/VBox/HBoxActions/BtnStart` is present as a plain `PanelContainer` instead of a loaded `HoldToActivateButton`, `ArenaRealtimeView` now replaces it with a fresh `HoldToActivateButton` instance before wiring signals. This prevents the blank/non-clickable Start action and avoids hard binding failures during scene load.
- Arena briefing now shows the dedicated background image `res://Assets/Images/Arena/Arena1.png` while the briefing dialog is open (if the local asset exists), then hides it automatically once combat starts or post-match UI is showing. Returning to City naturally restores the city background because navigation swaps back to `CityShell`.

### UI resilience
- `CopyControlLayout(...)` now preserves size flags, minimum size, and a few control metadata fields when replacing broken UI controls at runtime, which keeps fallback replacements aligned with the authored scene layout.

### Docs
- Updated project-state / next-task notes and the assistant playbook with the arena start-button fallback + background expectations.

## 2026-03-14 (Build: 2026-03-14-11)

### Build fix
- Repaired the reusable `HoldToActivateButton` API mismatch introduced during the Build 10 recovery merge. The control once again exposes semantic `VisualState` values (`neutral`, `success`, `warning`, `danger`) plus a `Disabled` flag so the arena briefing can tint and gate the Start/Resume action correctly.
- Disabled hold buttons now reset progress immediately, ignore hold activation, dim their label, and keep the semantic border/fill tint so warning/success intent still reads while unavailable.

### Docs
- Updated project-state / next-task notes to capture the Build 11 hold-button API repair.

## 2026-03-14 (Build: 2026-03-14-10)

### Build recovery
- Restored source/content folders that were accidentally omitted from the previous deliverable zip, including `Scripts/Core`, `Scripts/Game/Navigation`, `Scripts/Game/Session`, `Scripts/Game/Systems`, `UiKit/Scripts`, generated arena textures, and JSON definition data under `Data/Defs`.
- Preserved the arena readiness / hold-to-start work from Build 09 while repairing the deliverable so the main project can resolve `GameUiKit`, `WastelandSurvivor.Core`, save/session types, and the shared hold-button control again.

### Docs
- Added a packaging guardrail note to the assistant playbook so future deliverables verify critical source folders before shipping.

## 2026-03-14 (Build: 2026-03-14-09)

### Arena readiness UX
- Arena briefing now checks the active vehicle for repair needs before a match starts and surfaces a clear warning line when the vehicle is damaged.
- Replaced the arena **Start Match** action with a reusable hold-to-activate control bound to **S**; it now reads as a green success action when the active vehicle is fully repaired and a yellow warning action when it is damaged.
- The reusable `HoldToActivateButton` now supports built-in semantic visual states (`neutral`, `success`, `warning`, `danger`) plus a disabled state so future hold actions can convey intent without one-off overrides.

### Docs
- Updated next-task / project-state notes to capture the new arena readiness warning and semantic hold-button states.

## 2026-03-14 (Build: 2026-03-14-08)

### Arena flow / layout
- Removed the unused pre-match **Forfeit Match** button from the arena briefing and reordered the remaining actions so **Back to City** sits on the left and **Start Match** sits on the right.
- Arena vehicles now spawn in dedicated north/south **starting boxes** just outside the arena instead of dropping into the middle of the floor.
- Opened the north/south outer walls at the start-lane entrances and added simple boxed staging lanes with their own floor segments so vehicles can drive into the arena naturally.
- Kept the arena entrances clearer by steering the procedural interior-wall placement away from the new staging-lane launch paths.

### Docs
- Updated project-state / next-task notes to describe the new arena start-box layout and tee up the future door + countdown step.

## 2026-03-14 (Build: 2026-03-14-07)

### UI polish
- Cleaned up the Escape pause menu by removing redundant instructional/header copy and tightening the layout around the core actions.
- Reorganized the arena briefing and post-match salvage screens with clearer section hierarchy, better button grouping, and more game-like labels.
- Polished the city, garage, and workshop menu screens with stronger subtitles, section dividers, and cleaner footer/action grouping.

### Arena flow / visuals
- Moved the post-match hold-to-continue dialog upward so it sits roughly 25% from the top of the screen instead of dead-center.
- Slightly enriched the arena floor story overlay with a few extra subtle scuffs so the surface reads a touch richer without reintroducing obvious tiling.
- Post-match repair buttons now surface fuller state feedback (for example: full / disabled states and explicit repair cost labels).

## 2026-03-14 (Build: 2026-03-14-06)

### UI / menus
- Reworked the global Escape pause menu into a more game-like layout with **Resume Game**, **Save Game**, **Settings**, and **Exit to Desktop** actions.
- Upgraded built-in modal dialog buttons and spacing so confirmation/settings/match-end dialogs feel more consistent with the rest of the UI.
- Polished the city, garage, workshop, and arena setup/post-match screens: cleaner spacing, larger action buttons, better section labels, and more standard game-facing button text (for example **Save Game** instead of **Save Now**).
- City hub status text now formats location and active vehicle names in a more readable player-facing style.

### Visuals
- Gave the arena floor one more light polish pass by regenerating the bundled story overlay with subtler dark skid arcs, stains, and crack lines, plus a slight asphalt texture rebalance for a richer top-down read.

### Behavior changes
- Pause menu now supports saving directly while paused.

---

- Build 2026-03-23-100: hardened arena containment by forcing AI to prioritize inward escape steering whenever it gets trapped near the boundary/start-lane edges, extended the east/west arena walls so the hidden outer apron is no longer reachable, and replaced the simple rectangular fog with a denser plus-shaped fog-of-war footprint that keeps the playable bowl/start lanes readable while properly blacking out the outer side apron.

## 2026-03-14 (Build: 2026-03-14-05)

### Visuals
- Reworked the arena floor again to remove the obvious large rectangular decal planes that were still reading like slabs from the gameplay camera.
- Replaced the scattered patch/oil/crack decal planes with a single full-floor “story overlay” texture so the arena gets wear, skid arcs, stains, and cracks without showing rectangular mesh bounds.
- Simplified the asphalt shader to a more restrained layered material: smaller asphalt repeats for better aggregate readability, much subtler macro breakup, and the new floor overlay providing most of the large-scale visual storytelling.
- Regenerated the bundled macro mask and added `Generated/Textures/Arena/procedural_asphalt_story_overlay.png`.

### Behavior changes
- None intended (visual-only arena floor polish).

---

## 2026-03-14 (Build: 2026-03-14-04)

### Visuals
- Reworked the arena floor again to remove the obvious large rectangular/square breakup that was reading like kitchen tile from the gameplay camera.
- Updated the layered asphalt shader to use anti-tiling blended world-space samples for the base/detail/macro passes instead of a single repeated macro sample.
- Replaced the generated macro mask and repair-patch decal with more organic worn-asphalt variation, and added a new crack decal so the floor reads more like patched industrial asphalt than tiled slabs.
- Rebalanced the deterministic floor-detail placements (patches/oil/skids/cracks) to support the new material without large axis-aligned blocks.

### Behavior changes
- None intended (visual-only arena floor polish).

---

## 2026-03-14 (Build: 2026-03-14-03)

### Visuals
- Fixed the arena floor polish pass so it now affects both cases: when the user has the external Poly Haven asphalt pack and when the build falls back to generated textures.
- Reworked the floor shader into a layered asphalt material: external/base asphalt now gets the same generated macro breakup, repair-patch variation, oil/grime staining, and higher-frequency detail overlay instead of only the fallback path receiving those improvements.
- Added deterministic floor-detail decals (repair patches + oil/skid stains) so the arena surface stops reading like one uniform sheet from the fixed top-down gameplay camera.
- Rebalanced the bundled procedural asphalt textures and macro mask to read much more clearly from the fixed top-down gameplay camera.

### Behavior changes
- None intended (visual-only arena floor upgrade).

---

## 2026-03-14 (Build: 2026-03-14-02)

### Visuals
- Reworked the bundled fallback arena asphalt again so it reads from gameplay height instead of still looking like flat gray.
- Added a generated macro breakup mask (`Generated/Textures/Arena/procedural_asphalt_macro_mask.png`) and upgraded the floor shader to add larger-scale grime, oil stains, and seam/patch variation on top of the asphalt texture.
- Rebalanced the generated asphalt texture set for stronger aggregate and wear detail, and tightened the fallback floor tiling so the surface reads more like worn asphalt from the fixed top-down camera.

### Behavior changes
- None intended (visual-only arena floor polish).

---

## 2026-03-14 (Build: 2026-03-14-01)

### Visuals
- Reworked the arena floor fallback so it no longer drops to a flat gray material when external texture packs are absent.
- Added bundled procedural asphalt PBR textures under `Generated/Textures/Arena/` (albedo + normal + roughness) so the ground keeps readable asphalt detail in AI-delivered builds.
- Arena floor now prefers Poly Haven asphalt when available, otherwise automatically uses the bundled procedural asphalt set before falling back to the legacy shader/material path.

### Behavior changes
- None intended (visual-only arena floor upgrade).

---

## 2026-03-04 (Build: 2026-03-04-02)

### Fixes
- Fixed `HoldToActivateButton` label layout so text no longer wraps per-character in narrow container layouts.
- Fixed modal processing so dialogs remain interactive both paused and unpaused; the match-end hold button now works.
- Match-end dialog now releases GUI focus so **Escape** continues to toggle the global pause menu while the dialog is open.

### Behavior changes
- Modals now process input in both paused and unpaused game states (previously they effectively only worked while paused).

---

## 2026-03-04 (Build: 2026-03-04-01)

### Fixes
- Fixed a build error in `HoldToActivateButton` caused by a duplicate `Activated` member name (conflicted with Godot's generated signal event wrapper).

### Behavior changes
- None intended.

---

## 2026-03-02 (Build: 2026-03-02-14)

### GameUiKit
- Added `HoldToActivateButton` (reusable “hold to activate” button-like control with progress fill; supports mouse hold + shortcut key hold).

### Arena flow
- Replaced the end-of-match "hold G" prompt with a modal dialog titled **"Match Ended"** containing a `HoldToActivateButton` labeled **"Continue (Hold G)"**.
- Hold duration matches the previous post-match exit hold duration (~3 seconds).

### Behavior changes
- None intended (UI interaction method only).

---

## 2026-03-02 (Build: 2026-03-02-13)

### Interactables v1 (action prompt selection)
- `ActionPromptOverlay` now supports multiple prompt candidates and selects the best one by priority (desc) then distance (asc).
- Added `ActionPromptCandidate` (lightweight prompt request model).
- `ArenaRealtimeView` now feeds the action prompt via candidates (currently only the existing "Enter vehicle" hint; behavior should be unchanged).

### UI refactor
- `ActionPromptOverlay` migrated to `[Bind]` + `SceneAutoBinder` (removes manual binder boilerplate).

### Optional
- Added a disabled-by-default interactable highlight hook (`EnableInteractHighlight = false`) for future use.

### Behavior changes
- None intended.

---

## 2026-03-02 (Build: 2026-03-02-12)

### Pawn refactor (wire enter/exit + controller-id seams)
- GamePawnKit: `VehiclePawnBase` now implements `IEnterable`/`IExitable` with a minimal occupancy model (no scene-tree manipulation).
- ArenaRealtimeView: enter/exit flow now uses the interfaces and keeps a stowed `DriverPawn` instance while "inside" the vehicle so `Entered`/`Exited` events remain meaningful.
- ArenaRealtimeView: `SetPlayerControlledEntity` now drives `IPossessablePawn.SetPlayerControlled(true/false, controllerId)` for the currently-controlled pawn (controllerId=0).

### Behavior changes
- None intended.

---

## 2026-03-02 (Build: 2026-03-02-11)

### Pawn refactor (possession + enter/exit seams)
- GamePawnKit: added `IPossessablePawn` with `ControllerId` + possession events and a controller-id overload of `SetPlayerControlled`.
- Updated `IHumanoidPawn` and `IVehiclePawn` to inherit from `IPossessablePawn`.
- Implemented the possession surface in `HumanoidPawn` and `VehiclePawnBase` (additive; no intended behavior change).
- Added tiny generic enter/exit interfaces (`IEnterable`, `IExitable`) for future on-foot interaction work.

---

## 2026-03-02 (Build: 2026-03-02-10)

### Pawn refactor (vehicle telemetry)
- Added `GamePawnKit.Pawns.IVehicleTelemetry` and implemented it in `VehiclePawnBase` (speed/forward-speed/max-speed/throttle/brake helpers).
- Updated `IVehicleAudioTelemetry` to inherit from `IVehicleTelemetry` so audio/HUD systems can reuse the same generic telemetry surface.
- Removed now-duplicated telemetry helpers from `VehiclePawn` (they are provided by the base).

### Cleanup
- Removed `[Obsolete]` from `DriverPawn` to avoid noisy warnings in Godot-generated script binding code.

---

## 2026-03-02 (Build: 2026-03-02-09)

### Build fix
- Restored `VehiclePawn.FireCooldownSeconds` and `VehiclePawn.FireCooldownRemaining` which are used by `ArenaRealtimeView` firing logic and were unintentionally dropped during the VehiclePawnBase extraction.

---

## 2026-03-02 (Build: 2026-03-02-08)

### Pawn refactor (vehicle core extraction)
- Added `GamePawnKit.Pawns.VehiclePawnBase` (reusable vehicle movement/handling core).
- Updated `VehiclePawn` to inherit `VehiclePawnBase` and delegate movement to the shared base (no intended behavior change).

---

## 2026-03-02 (Build: 2026-03-02-07)

### Pawn refactor (scaffolding)
- Added new reusable gameplay library project: `PawnKit/GamePawnKit`.
- Introduced `HumanoidPawn` (generic on-foot pawn) + small pawn interfaces/events in GamePawnKit.
- Converted `DriverPawn` to a backwards-compatible wrapper that inherits `HumanoidPawn` so existing scenes continue working.

---

## 2026-03-02 (Build: 2026-03-02-06)

### HUD safety
- `ValueBar`: caches `SetValues` calls made before `_Ready()` and applies them after layout settles; also recomputes fill offsets after resize notifications to avoid stale fills when Godot sends early layout notifications.

---

## 2026-03-02 (Build: 2026-03-02-05)

### UI refactor
- Migrated core HUD scripts to attribute-driven bindings (`[Bind]` + `SceneAutoBinder`):
  - `PlayerStatusHud`
  - `TargetStatusHud`
  - `RadarHud`
  - `VehicleStatusHud` (keeps fallback/soft-bind behavior)

---

## 2026-03-02 (Build: 2026-03-02-04)

### UI refactor
- Migrated `ArenaRealtimeView` to attribute-driven bindings (`[Bind]` + `SceneAutoBinder`) and removed `EnsureBound()` boilerplate (no intended behavior change).

---

## 2026-03-02 (Build: 2026-03-02-03)

### UI toolkit
- GameUiKit: added `AutoBoundControl` + `AutoBoundNode` base classes (optional convenience) for future UI scripts.

### UI refactor
- Migrated `WorkshopView` to attribute-driven bindings (`[Bind]` + `SceneAutoBinder`) and removed `EnsureBound()` boilerplate.

---

## 2026-03-02 (Build: 2026-03-02-02)

### UI refactor
- Migrated `GarageView` to attribute-driven bindings (`[Bind]` + `SceneAutoBinder`) and removed `EnsureBound()` boilerplate.

---

## 2026-03-02 (Build: 2026-03-02-01)

- Fix: add missing `BindAttribute` + `SceneAutoBinder` sources to `UiKit/GameUiKit` (Step 15 support). This resolves compile errors in `CityShell` when using `[Bind]`.

## 2026-03-01 (Build: 2026-03-01-14)

### Fixes
- Fixed compilation after Step 15: `BindAttribute` + `SceneAutoBinder` were referenced by `CityShell` but were missing from `GameUiKit`.
  - Added the missing files under `UiKit/Scripts/Framework/SceneBinding/`.

---

## 2026-03-01 (Build: 2026-03-01-13)

---

## 2026-03-01 (Build: 2026-03-01-12)

### Fixes
- Fixed UiKit compilation error (missing closing brace in UiNav).
- Renamed UiKit project to a generic name (GameUiKit) and moved namespaces under GameUiKit for reuse in other projects.

# Changelog (Project-local)

## Build: 2026-03-01-11
- Refactor Step 13: Added a new `GameUiKit` project (shared UI/dialog toolkit) and referenced it from the main Godot project.
- Refactor Step 13: Moved reusable UI framework code into UiKit (`SceneBinder`, `ScreenRouter`, modal/dialog infrastructure, `UiNav`).
- Refactor Step 13: Decoupled UiKit dialogs from `GameUiTheme` via `ModalDialogStyle` (style injected at service registration time).
- Refactor Step 13: Added `Wasteland Survivor.sln` so the solution contains both projects.

## Build: 2026-03-01-10
- Fix: Prevent `App` from overwriting `App.Services` during boot (keeps UI navigation services registered).
- Refactor Step 12: `PauseMenuOverlay` binds `IModalService` immediately (with deferred retry as a safety net).
- Docs: Added a lifecycle note to `ASSISTANT_PLAYBOOK.md` to avoid future “service registry reset” regressions.

## Build: 2026-03-01-09
- Fix: Added missing `Scripts/Framework/UI/DialogCard.cs` (step 10 file) so `ModalService` compiles.
- Refactor Step 11: Register core services in `AppRoot._EnterTree()` (router, modal service, game navigator) so they are available before any child `_Ready()` runs.

## Build: 2026-03-01-08
- Refactor Step 10: Added `DialogCard` (reusable dialog layout shell) and migrated `ModalService` to build dialogs from it (no intended behavior change).

## Build: 2026-03-01-07
- Refactor Step 09: `PauseMenuOverlay` now uses `IModalService` for Settings and Exit confirmation dialogs (no intended behavior change).
- Refactor Step 09: `IModalService.ShowMessage` / `ShowConfirm` now accept optional `ModalOptions` (e.g., suppress modal dim when the pause menu already dims the background).

## Build: 2026-03-01-06
- Refactor Step 08: Added `ModalHost` + `IModalService` (generic modal/dialog infrastructure; no intended behavior change).
- Refactor Step 08: `AppRoot` now creates/hosts a `ModalHost` under `OverlayRoot` and registers `IModalService` in `App.Services`.

## Build: 2026-03-01-05
- Fix: `ValueBar.ApplyLabelRotation()` no longer throws during scene instantiation/layout (guards resize notifications that can occur before `_Ready`).
- Refactor Step 07: Hardened `ValueBar` lifecycle and made bindings idempotent (no intended behavior change).

## Build: 2026-03-01-04
- Fix: Removed a stray legacy navigation block in `WorkshopView` that could break compilation.
- Refactor Step 06: Added `IGameNavigator` + `GameNavigator`, registered it in `AppRoot`, and migrated UI screens to call the navigator instead of calling `UiNav` directly (no intended behavior change).

## Build: 2026-03-01-03
- Refactor Step 05: Added `GameScenes` (central UI scene path catalog) and `UiNav` (router-first navigation helper), and migrated menu/arena navigation call sites to remove hard-coded UI scene strings (no intended behavior change).

## Build: 2026-03-01-02
- Refactor Step 04: Added `ScreenRouter` (centralized UI navigation helper) and migrated primary screen transitions to use it (with a safe fallback to legacy parent-swap navigation).

## Build: 2026-03-01-01
- Refactor Step 03: Migrated menu views (`BootSplashView`, `CityShell`, `GarageView`, `WorkshopView`) to `SceneBinder` for consistent node bindings.
- Refactor Step 03: Migrated shared UI controls (`ValueBar`, `ActionPromptOverlay`) to `SceneBinder` (no intended behavior change).

## Build: 2026-02-28-12
- Refactor Step 02: Migrated `ArenaRealtimeView` UI bindings to `SceneBinder` (no intended behavior change).
- Refactor Step 02: Migrated `PlayerStatusHud` and `TargetStatusHud` bindings to `SceneBinder` (no intended behavior change).

## Build: 2026-02-28-11
- Refactor Step 01: Added `SceneBinder` (typed node binding helper with better error messages).
- Refactor Step 01: Migrated `VehicleStatusHud` bindings to `SceneBinder` (no intended behavior change).
- Docs: Added `Docs/REFRACTORING_PROGRESS.md` to track completed refactor steps.

## Build: 2026-02-28-10
- Docs: Added `Docs/CODEMAP.md` (quick onboarding), `Docs/REFRACTORING_PLAN.md` (proposal), and `Docs/Assets/*` setup docs (Mixamo + Poly Haven).
- Docs: Updated `AI_README`, `BUILD_RUN`, `THREAD_HANDOFF_PROMPT`, `PROJECT_STATE`, and simplified `NEXT_TASK` to point to the changelog for history.
- Code: Added standardized file headers across all C# scripts; added/expanded XML summaries for core defs/state records; added navigation notes in the largest runtime files.

## Build: 2026-02-28-09
- Fix: VehicleStatusHud live preview camera now consistently targets the **current** player vehicle (clears stale group entries on match restart; HUD resolves Player vehicle node more robustly) and uses a stable top-down pose update (LookAtFromPosition + MakeCurrent).

## Build: 2026-02-28-08
- Fix: VehicleStatusHud live preview camera is now centered directly above the active vehicle and uses vehicle-forward as screen-up (vehicle always faces "up" in the HUD view).

## Build: 2026-02-28-07
- Fix: Build error in `ArenaRealtimeView` (remove reliance on short-circuit `&&` for vehicle definition resolution; null-safe post-panel visibility).

## Build: 2026-02-28-06
- Fix: Vehicle HUD no longer throws cast/replacement errors; ArenaRealtimeView uses a safe fallback binder when the managed `VehicleStatusHud` script isn't bound.
- Fix: Vehicle HUD is hidden in the pre-fight dialog and only appears during match/post-match when real vehicle data is available.

## Build: 2026-02-28-04
- Fix: VehicleStatusHud values no longer stuck at 0/0. ArenaRealtimeView now force-replaces the HUD with a typed `VehicleStatusHud` instance if Godot left it as a plain PanelContainer after a prior C# compile failure.

## Build: 2026-02-28-02
- Fix: Prevent runtime crash when resolving `VehicleStatusHud` (safe lookup + best-effort repair if the script is missing).

## Build: 2026-02-28-01
- Audio: Reverse engine sound is now forced to remain audible when backing up (reverse command contributes to drive intent / RPM mapping).
- UI: VehicleStatusHud width reduced while keeping its right edge aligned with PlayerStatusHud + RadarHud.
- UX: Added Escape pause menu overlay with Settings (stub dialog) and Exit (with confirmation).

## Build: 2026-02-27-07
- UI: VehicleStatusHud now reserves enough width so it doesn't expand past the right edge (right edge stays aligned with PlayerStatusHud and RadarHud).
- Audio: Reverse engine audio no longer goes silent (brake intent contributes to drive blend; RPM uses blended speed so drift/reverse stays audible).

## Build: 2026-02-27-06
- UI: Fixed VehicleStatusHud alignment so its right edge lines up with PlayerStatusHud and RadarHud (adds safe margin from screen edge).

## Build: 2026-02-27-05
- Fix: `RadarHud` no longer uses `StyleBox.GetContentRect()` (not available in C#). Content rect is computed from content margins.

## Build: 2026-02-27-04
- UI theme expanded across the whole UI (menus + HUD + overlays). Palette is configurable via `Data/Config/ui_theme.json`.
- Console overlay now starts hidden/closed (toggle with tilde/backtick).
- Arena: pre-fight dialog hides during combat and during post-match; added a **Close** button on the results panel to return to pre-fight without leaving the arena view.
- Audio: reverse engine sound now behaves like 1st gear (no gear progression while reversing).
- Arena walls: auto-detect and prefer a Poly Haven *brick* wall pack (folder contains `brick`); fall back to rock wall/metal if not found.

## Build: 2026-02-27-03
- On-foot death: when the driver is killed while outside the vehicle, DriverPawn plays a non-looping death animation (configurable) and stays on the ground.
- Added `deathAnim` to `Data/Config/driver_pawn.json` and updated Mixamo setup docs.

## Build: 2026-02-27-02
- UI: Action prompt now only appears while on-foot (driver mode). It no longer shows while inside a vehicle.

## Build: 2026-02-27-01
- UI: Action prompt key badge has more left/right padding.
- Radar: Stabilized enemy dots by using a flat (yaw-only) heading basis and fixed controlled-entity switching on driver enter/exit.

## Build: 2026-02-26-11
- Fix: Resolved a Godot runtime parse error when loading `Scenes/Arena/VehiclePawn.tscn` by using `position = Vector3(...)` for `InteractPromptAnchor` instead of a serialized `Transform3D(...)`.

## Build: 2026-02-26-10
- Fix: Added missing `ActionPromptOverlay` scene + script that were referenced by `ArenaRealtimeView` (restores successful builds).

## Build: 2026-02-26-09
- Added a styled, world-anchored **Action Prompt** overlay that floats above interactable entities (vehicle enter/exit) and fades in/out.
- Added `InteractPromptAnchor` to `VehiclePawn.tscn` to provide a stable world anchor point for prompts.
- Mixamo driver polish: defaulted avatar yaw offset to 180° and force-loop idle/walk/run clips at runtime.

## Build: 2026-02-26-06
- Build fix: resolved a C# compile error in `DriverPawn` animation logging (use array `Length` instead of LINQ `Count`).

## Build: 2026-02-26-05
- Fixed arena shader compilation by using built-in `INV_VIEW_MATRIX` (removes invalid `MAIN_CAM_INV_VIEW_MATRIX` reference).
- Prevented Poly Haven EXR runtime error spam by treating EXR maps as optional; prefer PNG normal/roughness when present.
- Added `Docs/Assets/POLYHAVEN_PBR_TEXTURES.md` describing the Poly Haven floor/wall texture workflow and EXR caveats.

## Build: 2026-02-26-04
- DriverPawn now supports a Mixamo avatar scene (`.glb` or `.tscn`) loaded at runtime (falls back to capsule placeholder if missing).
- DriverPawn locomotion now uses acceleration/deceleration smoothing and plays idle/walk/run via AnimationPlayer (configurable names).
- Added `Docs/Assets/MIXAMO_DRIVER_SETUP.md` describing the expected local Assets path and a simple conversion workflow.

## Build: 2026-02-26-03
- Enemy AI now targets the on-foot driver when outside the vehicle.
- Vehicle HUD (VehicleStatusHud) is hidden while on-foot.
- On-foot driver takes damage from vehicle collisions (run-over/ram).
- Radar now follows the currently controlled entity via the `player_controlled` group.


## Build: 2026-02-26-02
- Added Phase-1 **Driver Exit / On-Foot** prototype: **E** to exit (when stopped/slow), **WASD** to walk, **E** near vehicle to re-enter.
- Camera follow switches between vehicle and driver automatically.
- Radar/minimap now tracks the currently controlled entity (vehicle or driver).

## Build: 2026-02-26-01
- Vehicle HUD mini-view: live overhead camera now rotates with the player vehicle so the vehicle stays facing up.
- Added a bottom-right **RadarHud** that appears when a vehicle is active and shows enemy positions.
- Vehicle controls: player/enemy inputs are disabled immediately when their driver HP reaches 0.

## Build: 2026-02-25-03
- Added new weapon + ammo defs: **50 cal Machine Gun** (`wpn_mg_50cal`) and **50 cal rounds** (`ammo_mg_50cal`).
- Starter compact loadout: front mount now uses **50 cal MG** (keeps 9mm MG defs available for workshop/testing).
- Weapon visuals: added `Data/Config/weapon_visuals.json` and runtime loader to render configured weapon models (fallback is proxy box).
- Implemented automatic weapon-model alignment (mount origin + yaw auto-align + muzzle estimation) to reduce per-model tweaking.

## Build: 2026-02-25-02
- Engine RPM response: added **throttle-to-RPM smoothing** (separate up/down rates) and a **stall RPM cap** at (near) zero speed, preventing RPM from jumping to redline instantly when tapping W.
- HUD: RPM bar text color changed to **white**.
- HUD: added a small spacer under the RPM row for better vertical breathing room.

## Build: 2026-02-25-01
- Tuned automatic transmission feel: gears now shift by **normalized speed bands** (default 4 gears: 0.18 / 0.40 / 0.68) with downshift hysteresis to prevent gear hunting.
- RPM model adjusted so throttle slip tapers off as wheel RPM rises, making **upshift RPM drops** more noticeable and keeping the RPM bar stable.
- HUD: gear display now shows **R** when backing up (or commanding reverse from a stop).

## Build: 2026-02-24-12
- Fixed Vehicle HUD scene wiring: left/right side HP/AP bars were mis-parented in `VehicleStatusHud.tscn`, causing the HUD to render incorrectly and throwing a NullReferenceException during arena start.
- Added a defensive bind/guard in `VehicleStatusHud` so missing/mismatched UI nodes fail soft instead of crashing the encounter.

## Build: 2026-02-24-11
- Build fix: resolved a C# compile error (CS0111) caused by a duplicate `ApplyLabelStyle` method in `ValueBar`.

## Build: 2026-02-24-10
- Reduced vehicle acceleration (slower ramp to max speed).
- Vehicle HUD polish: side bar padding/centering, Speed label moved beside bar, new RPM+Gear bar, weapon list tightened and ammo ids hidden.

## 2026-02-24 (Build: 2026-02-24-08)

### Boot splash
- Added `gapSeconds` (fade-to-black gap between items) and `defaultOpenSound`/per-item `openSound` support (play a sound as each splash appears).

### Vehicle handling
- Reduced coasting slowdown by lowering default `CoastDecel` and drag values on `VehiclePawn`.

### Engine audio
- Added an optional automatic transmission RPM model (4-gear default) so holding W revs up and shifts; can be disabled via `UseAutomaticTransmission`.


## 2026-02-24 (Build: 2026-02-24-07)

### Boot splash
- Added a configurable boot splash sequence shown on launch (image list + timings loaded from `Data/Config/boot_splash.json`).
- Press **Enter** or **Escape** to skip the entire splash sequence.


## 2026-02-24 (Build: 2026-02-24-06)

### Mines
- Fixed mine visuals spawning far away: mine marker mesh no longer double-applies world position. Mines are grouped under a `Vfx/Mines` node ("mine layer").

### Workshop / Garage ammo
- Added **Refill All** button that refills ammo for **all installed weapons** (based on each weapon's selected ammo type) using simple per-ammo-kind refill targets and costs.
- Ammo UI now shows a multi-weapon ammo summary and the computed refill-all cost.


## 2026-02-24-05
- Build fix: fixed C# compile error in `ArenaRealtimeView` mine explosion logic (nullable `SplashRadius`).


## 2026-02-24-04
- Weapon slot 3: mine dropper now places a persistent mine behind the vehicle (arms after a short delay), triggers on proximity, and explodes with splash damage (tire + undercarriage) and VFX.
- Added VehiclePawn.GetTireWorldPosition() helper used for mine explosion tire targeting.


This is a lightweight, human-written log meant to help new ChatGPT threads (and humans) pick up quickly.

Conventions:
- **Build IDs** are recorded in `VERSION.txt`.
- Keep entries short: what changed, why, and any verification notes.

---

## 2026-02-24 (Build: 2026-02-24-03)

### Arena controls + weapon slots (v1)
- Added input actions: `ws_fire_1` (Space), `ws_fire_2` (Shift), `ws_fire_3` (Ctrl). (Godot doesn’t reliably distinguish left/right modifiers across platforms; Shift/Ctrl act as the requested RightShift/RightCtrl for now.)
- Arena firing now resolves the weapon installed on the corresponding mount (slot 1 = Front, slot 2 = Top turret, slot 3 = Rear) and fires from that mount’s muzzle.
- Ammo consumption is now per-weapon based on the installed weapon’s `SelectedAmmoId` (or the weapon’s first `AmmoTypeIds` entry).
- Added a rear mount `B1` to starter chassis defs and updated starter loadout to include a `Mine Dropper` on `B1` with starting mine ammo.

Verification notes
- Start an arena encounter:
  - Space fires the front MG (consumes `ammo_mg_9mm`).
  - Shift fires the top mount missile (consumes `ammo_missile_std`).
  - Ctrl fires the rear mine dropper mount (consumes `ammo_mine_std`).


## 2026-02-24 (Build: 2026-02-24-02)

### Audio mix
- Boosted engine audibility: set default bus gain for **Engines** (+8 dB) and **Tires** (+3 dB), and increased `VehicleEngineAudio` default volume/3D attenuation settings so engines are clearly audible from the top-down camera.

Verification notes
- Start an arena encounter: engine idle should be noticeably louder than in Build 2026-02-24-01.
- Fire weapons: weapon SFX should still be audible without completely masking engine sound.

## 2026-02-24 (Build: 2026-02-24-01)

### Engine audio hotfix
- Fixed layered engine audio being silent: engine audio now **starts deferred** (after the parent sets archetype/telemetry) and **restarts all layers** when the archetype changes.
- Enforced 3D audibility settings on all engine layer players (even when they come from the .tscn): increased MaxDistance/UnitSize so the top-down camera can hear engines reliably.

### Camera warning spam hotfix
- Fixed repeated Godot warnings `Condition "!is_inside_tree()" is true` from the follow camera: camera rig now ignores targets that are not yet inside the tree and defers its initial snap until safe.

Verification notes
- Start an arena encounter: you should hear engine idle immediately.
- Accelerate/brake: engine intensity crossfades smoothly; no silent engine after spawning.
- No more repeated `!is_inside_tree()` warnings during normal arena play.


## 2026-02-23 (Build: 2026-02-23-15)

### Engine audio (layered RPM v1)
- Added `VehicleEngineAudio` component (5 looping layers with RPM crossfade + subtle pitch), routed to an `Engines` audio bus.
- `VehiclePawn` now spawns `EngineAudio` at runtime and drives it via `IVehicleAudioTelemetry`.
- Added runtime audio buses (`SFX`, `Engines`, `Tires`) on boot; arena UI SFX now routes through `SFX`.
- Added a hard-brake-at-speed **tire skid** hook with a temporary placeholder stream path (replace later with real skid audio).

Verification notes
- Start an arena encounter: you should hear engine idle immediately.
- Hold W to accelerate: engine intensity should increase smoothly (no hard steps).
- Hold brake hard at speed: you should hear the placeholder skid trigger repeatedly.
- If engine loop assets are missing locally, the game should continue running and log warnings (no crash).


## 2026-02-23 (Build: 2026-02-23-14)

### Workflow (zip-based baseline restored)
- Reverted iteration baseline back to **Project source zips** (`wasteland-survivor.zip`) because remote repo pulls (and remote repo snapshot downloads) are unreliable across threads.
- Updated workflow docs to remove “baseline-first (deprecated)” guidance and make the zip flow canonical:
  - `Docs/REPO_WORKFLOW.md` (now describes zip-based iteration)
  - `Docs/AI_WORKFLOW.md`
  - `Docs/ASSISTANT_PLAYBOOK.md`
  - `Docs/Audio/AUDIO_CHECKLIST.md`
  - `README.md`

Verification notes
- Open `Docs/REPO_WORKFLOW.md` and confirm it describes the Project-zip baseline (not remote repo).
- Open `Docs/ASSISTANT_PLAYBOOK.md` and confirm it explicitly says “use the uploaded project zip; don’t loop on remote repo pulls”.

## 2026-02-23 (Build: 2026-02-23-13)

### Audio docs / workflow
- Added `Docs/Audio/` docs (audio checklist + sourcing notes + licensing/credits templates).
- Added missing `Docs/ASSISTANT_PLAYBOOK.md` file to match the docs references.
- Updated `README.md`, `Docs/AI_README.md`, and `Docs/NEXT_TASK.md` to reference the new audio docs.

Verification notes
- Open `Docs/Audio/AUDIO_CHECKLIST.md` and confirm the folder plan + MVP list.
- Confirm `Docs/ASSISTANT_PLAYBOOK.md` exists and documents zip packaging rules.

## 2026-02-23 (Build: 2026-02-23-12)

### Docs / workflow
- Added `Docs/ASSISTANT_PLAYBOOK.md` (internal “what works / what doesn’t” notes) so the assistant stops repeating the same failed repo/packaging attempts.
- Updated `Docs/REPO_WORKFLOW.md`, `Docs/AI_WORKFLOW.md`, `Docs/AI_README.md`, and `README.md` to reference the playbook.

Verification notes
- Open `Docs/ASSISTANT_PLAYBOOK.md` and confirm it documents how to use the Project zip baseline and how to package the deliverable zip.

## 2026-02-23 (Build: 2026-02-23-11)

### Docs / workflow
- Updated iteration workflow to be **baseline-first (deprecated)** (baseline = latest build) instead of “upload latest zip”.
- Added `Docs/REPO_WORKFLOW.md` (canonical step-by-step process).
- Updated packaging rules so AI zips exclude: `.git/`, `.godot/`, `Assets/`.
- Refreshed `README.md`, `Docs/AI_README.md`, `Docs/AI_WORKFLOW.md`, and `Docs/BUILD_RUN.md` to reference the new process.

Verification notes
- Open `Docs/REPO_WORKFLOW.md` and confirm it matches the agreed iteration loop.
- Confirm updated packaging rules appear in `Docs/AI_WORKFLOW.md` and `Docs/AI_README.md`.

## 2026-02-23 (Build: 2026-02-23-10)

### Salvage merge (recover lost features)
- Restored **driving realism** (bicycle-model steering + lateral friction + drag + braking) and runtime-derived stats (weight + tire condition → speed/traction).
- Restored **vehicle mass system** (weapon mass + ammo mass) and surfaced it in VehicleStatusHud.
- Restored **hit feedback**: center-screen hit marker + hit/miss SFX, plus tire-pop SFX on tire destruction.
- Restored **tire VFX**: smoke puffs + skid marks when tires are blown/slipping.
- Added soft-lock safeguards: driver HP no longer stays at 0 after a loss, and save migration revives 0 HP to full (until a real healing flow exists).
- Docs: updated workflow so the user uploads the latest zip before each iteration; AI-delivered zips exclude `.godot/` and `Assets/`.

Verification notes
- Arena: drive (WASD) and confirm the new handling (no spin-in-place at 0 speed, heavier feel, lateral grip).
- Fire (Space): confirm hit marker + hit/miss SFX; destroy a tire and confirm tire-pop SFX + smoke/skid feedback.
- Lose an encounter: confirm you return to city with **full driver HP** (no soft-lock).

---

## 2026-02-22 (Build: 2026-02-22-11)

### HUD/Overlay tweaks
- TargetStatusHud: removed bracket labels around HP/AP bars (cleaner one-line layout).
- ConsoleOverlay: moved to **bottom-left** (still 60% width) to keep the right-side HUD unobstructed.
- Arena HUD panel: removed the ammo count from the stats line (Tier only).

Verification notes
- Run Arena: confirm Target HUD is one line with HP/AP bars (no brackets).
- Confirm Console is anchored bottom-left and doesn’t overlap VehicleStatusHud.
- Confirm Arena panel shows Tier only (no Ammo).

---

## 2026-02-22 (Build: 2026-02-22-10)

### HUD/Overlay polish
- TargetStatusHud: one-line layout with **HP/AP bars** (values inside) instead of raw numbers.
- ConsoleOverlay: width reduced to **60%** (bottom-center).
- Removed DebugOverlay entirely (no bottom status strip).
- VehicleStatusHud: aligned under PlayerStatusHud with extra right margin; center panel now renders a small **top-down vehicle preview** via SubViewport.

Verification notes
- Run Arena: confirm Target HUD shows bars and updates with Tab targeting.
- Confirm Console is bottom-center and no DebugOverlay is present.
- Confirm Vehicle HUD preview renders and faces "up" (front toward screen top).

---

## 2026-02-21 (Build: 2026-02-21-01)

### Refactor stabilization (no gameplay behavior changes)
- Split `GameSession` into additional partials and moved pure logic into `Scripts/Game/Systems/*`.
- Added mutator helpers to reduce repeated “find vehicle → mutate → persist” patterns.
- Reworked encounter win resolution to avoid double-persist.

### Arena cleanup
- Removed turn-based arena interaction scaffolding (legacy prototype code).
- Consolidated `ArenaRT` into `Arena` and removed `Scripts/ArenaRT/*`.

### UI
- DebugOverlay is now a single-line bottom bar (more transparent background, gold text).

### Build safety
- `.godot/` remains excluded from compilation/shipping.
- When replacing the project folder, prefer a clean unzip to avoid stale `.cs` files causing duplicate-type compile errors.

Verification notes
- Build/run.
- Workshop: buy ammo, repair, scrap patch, upgrade plating.
- Arena: confirm realtime controls still work; confirm DebugOverlay position/styling.

---

## 2026-02-21 (Build: 2026-02-21-02)

### UI overlays
- Added a global **Console** overlay above DebugOverlay:
  - `~` toggles visibility
  - collapsible
  - input stub (echoes command in gold, then prints “unrecognized command” in red)
- Removed the arena-only “Combat Log” panel and routed combat/runtime log lines into Console.

### Logging
- Added a lightweight `GameConsole` service and started routing key game actions into it (boot, repairs/upgrades, ammo purchase, city/encounter end).

### Docs (bulletproof thread handoffs)
- Added `Docs/AI_README.md`, `Docs/MASTER_GAME_SPEC.md`, and `Docs/NEXT_TASK.md`.
- Updated existing docs to reference the new onboarding flow and console overlay.

Verification notes
- Build/run.
- Press `~` to toggle Console; try typing a command and press Enter.
- Start an arena encounter and confirm combat log lines appear in Console.

---

## 2026-02-21 (Build: 2026-02-21-03)

### Fixes
- Fixed Console toggle key handling to avoid relying on a version-specific `Key.QuoteLeft` enum member.
  - Uses ASCII keycodes for backtick/tilde (96/126).
- Fixed a leftover call to `RenderLog()` in `ArenaRealtimeView` after moving logs to the global Console.
- Removed a nullable warning in boot error handling (`App.cs`).

Verification notes
- Build/run.
- Press `~` to toggle the Console.
- Start an arena encounter and confirm the “Encounter started / ammo” lines appear in the Console.

---

## 2026-02-21 (Build: 2026-02-21-04)

### Console UI polish
- Compact shaded header with smaller title text (**CONSOLE** in all caps).
- Collapse/expand control now uses a toggle button and also works by clicking the header area.
- Reduced padding and overall vertical footprint (including smaller command input bar).

Verification notes
- Build/run.
- Click the collapse arrow (or the header) to toggle between expanded (scroll + input) and collapsed (single-line) modes.
- Confirm the header is shaded and more compact.

---

## 2026-02-21 (Build: 2026-02-21-05)

### Console: one-line mode + typed log lines
- Header text made smaller.
- Collapse/expand now switches to a true one-line mode (header hidden in collapsed mode).
- Introduced typed console lines: Debug (blue), Status (white), Input (gold), Error (red).
- Startup/boot messages are now Debug; common game actions are Status; command echo is Input.
- Console history is bounded and trims in batches to avoid long-run performance degradation.

Verification notes
- Build/run.
- Click the header arrow to collapse: header should disappear and only the latest line should show.
- Click the arrow on the collapsed bar to expand.
- Verify colors: debug=blue, status=white, input=gold, error=red.

---

## 2026-02-21 (Build: 2026-02-21-06)

### Console fixes + basic commands
- Console header title font size reduced further.
- Collapse/expand twisty now reliably switches between expanded view and true one-line mode (header hidden when collapsed).
- Fixed BBCode escaping so bracketed tags like `[Save]` render without extra backslashes.
- Added basic console commands:
  - `help`
  - `clear` (preserves the echoed input line)
  - `version` (reads `VERSION.txt` and prints the Build line)

Verification notes
- Build/run.
- Click collapse arrow → should switch to one-line bar with header hidden; click expand arrow → returns.
- Enter `help`, `version`, `clear` and confirm output/colors.

---

## 2026-02-21 (Build: 2026-02-21-07)

### Console: mouse interaction + header sizing
- Fixed console overlay mouse interaction so buttons/scrollbar/input can be clicked reliably.
- Console header title font size now matches log line text with minimal top/bottom padding.

Verification notes
- Build/run.
- Click inside console: scroll bar should drag and input should focus on click.
- Collapse/expand twisty should switch between expanded and one-line modes.

---

## 2026-02-21 (Build: 2026-02-21-08)

### Console: reliable mouse input + font alignment
- Moved ConsoleOverlay (and DebugOverlay) to a dedicated `OverlayRoot` CanvasLayer (layer=100) so it stays above active UI and receives mouse input.
- Header title font size now matches log line font size with compact top/bottom padding.
- Adjusted container mouse filters to ensure scrollbar, input, and collapse/expand buttons receive clicks reliably.

Verification notes
- Build/run.
- Click inside the console: input should focus, scrollbar should drag.
- Click the collapse arrow: should switch to one-line mode (header hidden). Click expand arrow to return.

---

## 2026-02-21 (Build: 2026-02-21-09)

### 3D arena foundation (2.5D transition)
- Added a new 3D arena prototype that uses `WorldRoot` (`Node3D`) in `Scenes/Main.tscn`.
- CityShell now prefers `Scenes/UI/ArenaRealtimeView3D.tscn` when opening Arena.
- New 3D arena world + vehicle primitives:
  - `Scenes/Arena3D/ArenaWorld3D.tscn`
  - `Scenes/Arena3D/VehiclePawn3D.tscn`
  - `Scripts/Arena3D/*` (VehiclePawn3D + FollowCameraRig3D + ArenaWorld3D)
- Fixed top-down/RTS-ish camera is locked to the player vehicle.
- Combat uses hitscan raycast; logs tire/body part tags when hit (foundation for locational damage).

Verification notes
- Build/run.
- City → Arena opens the 3D arena.
- Drive (WASD), fire (Space), Tab target.
- Confirm camera follows player vehicle.

---

## 2026-02-21 (Build: 2026-02-21-10)

### Arena: canonical 3D/2.5D naming + remove 2D legacy
- Removed legacy 2D arena scenes and scripts.
- Renamed arena content to remove `3D` suffixes:
  - `Scenes/Arena/ArenaWorld.tscn`
  - `Scenes/Arena/VehiclePawn.tscn`
  - `Scripts/Arena/ArenaWorld.cs`, `Scripts/Arena/VehiclePawn.cs`, `Scripts/Arena/FollowCameraRig.cs`
  - `Scenes/UI/ArenaRealtimeView.tscn` + `Scripts/UI/ArenaRealtimeView.cs`
- CityShell now opens only `Scenes/UI/ArenaRealtimeView.tscn` for Arena.

### Arena: start reliability
- Made `ArenaWorld` safe to interact with immediately after instantiation (no dependency on its `_Ready` having run before UI calls).
- Added better status + console debug messages when the Start button is pressed (so failures are visible).

### Console
- Improved BBCode escape tolerance by normalizing older-style escaped brackets (`\[ ... \]`) back to `[ ... ]` before rendering.

Verification notes
- City → Arena: Start should now spawn actors, log a "Start pressed" debug line, and begin combat.
- Confirm `[Save] Wrote user://savegame.json` no longer shows backslashes.

---

## 2026-02-21 (Build: 2026-02-21-11)

### Arena: make 3D world actually render
- Forced the arena camera to become current immediately after spawning the world.
- Explicitly enabled processing for the follow camera rig and physics processing for vehicle pawns.
- Added step-by-step debug logging (and exception reporting) during Arena Start so failures are visible in the Console.
- Updated status to `Fight!` once actors are spawned.

Verification notes
- City → Arena → Start: you should see floor/walls and the player/enemy vehicle boxes.
- Console should show lines like `Arena: world spawned...`, `Arena: encounter seeded...`, `Arena: actors spawned...`.

---

## 2026-02-21 (Build: 2026-02-21-12)

### Fix: build error in ArenaRealtimeView
- Fixed an invalid interpolated-string expression that used escaped quotes inside C# code (`GetNodeOrNull<Camera3D>("...")`), which broke compilation.

Verification notes
- Build should succeed.

---

## 2026-02-21 (Build: 2026-02-21-13)

### Arena: diagnose Start exception (NRE)
- Added explicit debug lines before/after `TryStartArenaEncounter` so we can pinpoint where Start fails.
- Catch handler now logs the full exception text (including stack trace) into the in-game Console.
- Added a small guard to drop a stale `ArenaWorld` reference if the node was freed.

Verification notes
- City → Arena → Start should log:
  - `Arena: calling TryStartArenaEncounter ...`
  - `Arena: TryStartArenaEncounter ok` (if successful)
- If it fails, Console should now include a stack trace line pointing at the exact file/line.

---

## 2026-02-21 (Build: 2026-02-21-14)

### Fix: Arena Start NRE (EnsureWorld)
- Added defensive scene loading checks before instantiating the arena world and vehicle pawn scenes.
- Avoided eager world spawning in `_Ready`; the world now spawns on Start to reduce timing/race issues.
- Made instantiation robust against script/root-type mismatches by instantiating as `Node` and casting with an explicit error if it doesn't match.

Verification notes
- City → Arena → Start should no longer throw a NullReferenceException in `EnsureWorld`.
- If a scene path or script binding is wrong, the Console should show a clear error ("Failed to load ..." or "root is not ...").

---

## 2026-02-21 (Build: 2026-02-21-15)

### Fix: Arena world/vehicle scenes failed to load (parse errors)
- Replaced `Scenes/Arena/ArenaWorld.tscn` and `Scenes/Arena/VehiclePawn.tscn` with **minimal, Godot-4-compatible** scene files.
- Moved arena **floor + bounds** creation into `ArenaWorld` (procedural geometry) to keep the scene text simple and robust.
- Moved vehicle **collision + mesh** creation into `VehiclePawn` so the pawn scene can remain minimal.

Verification notes
- City → Arena → Start should no longer report parse errors for `ArenaWorld.tscn` / `VehiclePawn.tscn`.
- Console should proceed past scene loading into `Arena: world spawned...` and `Arena: actors spawned...`.
- You should see the arena floor/walls and two box vehicles (player + enemy) with the fixed top-down camera.

---

## 2026-02-21 (Build: 2026-02-21-16)

### Fix: steering inversion (A/D)
- Corrected the sign of yaw rotation so **A=left** and **D=right**.

### Arena: firing feedback (minimal VFX)
- Added cheap, code-only shot VFX:
  - Short-lived **tracer beam** between muzzle and ray end.
  - Tiny **muzzle flash**.
  - Tiny **impact flash** on hit.
- No textures, particles, or extra scene resources required.

### Fix: resume active encounter after restart
- Arena Start now detects an **already active** encounter in the save and **resumes** it instead of erroring.
- Leaving the arena while combat is live now **forces a flee resolution** (persists runtime ammo/damage) so the save cannot get stuck with an active encounter.

Verification notes
- Arena driving: A turns left, D turns right.
- In arena, press Space: you should see a tracer/flash from the fixed camera.
- Start an encounter, close/restart the app, return to Arena → Start: it should resume instead of showing "An encounter is already active".

---

## 2026-02-21 (Build: 2026-02-21-17)

### Refactor: GameSession foundation (OO cleanup)
- Replaced the many-partial `GameSession` implementation with a cleaner object model:
  - `GameSession` is now a thin facade that preserves the existing public API.
  - `SessionContext` owns the in-memory `SaveGameState` and is the only place allowed to replace/persist it.
  - Focused services under `Scripts/Game/Session/*`:
    - `SessionWorld` (city/world state)
    - `SessionGarage` (vehicles/ammo/repairs/upgrades)
    - `SessionEncounters` (encounter lifecycle + rewards)
- Centralized prototype economy knobs in `GameBalance`.

Verification notes
- Smoke test: City → Garage/Workshop/Arena flows should behave the same.
- Confirm repairs/upgrades/ammo purchases still log to the Console and persist.

---

## 2026-02-21 (Build: 2026-02-21-18)

### Fix: build break after GameSession refactor
- Added missing `using WastelandSurvivor.Core.IO;` so `SessionGarage` can reference `DefDatabase`.

Verification notes
- Build should succeed again.

---

## 2026-02-21 (Build: 2026-02-21-19)

### Fix: arena shot direction + damage correctness
- Shots now fire **forward** from the muzzle (no auto-aim).
- Damage now applies **only** when the ray hits the intended pawn (enemy/player), not when hitting walls/obstacles.
- Raycasts exclude the shooter's own hitbox Areas to prevent instant self-hits.

### Fix: tracer placement/readability
- Tracer now renders as an `ImmediateMesh` **3D line** between muzzle and impact point.

Verification notes
- In arena: fire while facing away from the enemy; the tracer should go forward and the enemy should not take damage unless struck.

---

## 2026-02-22 (Build: 2026-02-22-02)

### Fix: Arena HUD target label build error
- Fixed a C# ternary type mismatch (`StringName` vs `string`) when displaying the selected target name.

### UI: targeted enemy + driver HP bar
- Added a simple HUD row showing:
  - `Target: <name>` (or `none`)
  - Driver HP progress bar (currently based on total vehicle HP) with numeric `current/max`.

### Safety: post-encounter actions
- Added defensive guards so post-encounter repair/patch actions fail gracefully if no active vehicle is selected.

Verification notes
- Build should succeed again.
- In arena, confirm Target name updates and the Driver HP bar changes as damage is taken.

---

## 2026-02-22 (Build: 2026-02-22-03)

### Feature: driver armor (AP)
- Added player/driver armor points (AP) as an extra HP buffer stored in the save (`PlayerProfileState.DriverArmor/DriverArmorMax`).
- Incoming hits now reduce AP first; once AP is 0, damage reduces HP.

### UI: PlayerStatusHud (top-right)
- Moved the player HP display out of the Arena HUD panel into a new top-right status panel.
- Status panel shows **HP (left)** and **AP (right)** side-by-side.
- HP bar color changes by percent full:
  - 100%: light green
  - 70–99%: dark green
  - 30–69%: yellow
  - 10–29%: dark red
  - 1–9%: bright red
- AP bar is light blue when full and gets darker as it depletes.

### Post-encounter: repair armor with money
- Added a post-encounter action to **Repair Armor** (restore AP to full) using **money** (not scrap).

### Save/state
- Save version bumped to **v4**; migration ensures driver armor values are initialized and clamped.

Verification notes
- In arena: take hits and confirm AP decreases first, then HP.
- After an encounter: use **Repair Armor** and confirm AP returns to full and money decreases.

---

## 2026-02-22 (Build: 2026-02-22-04)

### UI: PlayerStatusHud cleanup
- Player status is now **one line**:
  - `HP [ 10/10 ]   AP [ 50/50 ]`
- Value text is rendered **inside** each bar (no percent label).

### Feature: real driver HP + equipped armor
- Added persistent **Driver HP** (`DriverHp/DriverHpMax`, default **50**).
- Added an equipped armor slot (`EquippedArmorId`) with a new armor def:
  - **Basic Kevlar** (`armor_kevlar_basic`, 50 AP).
- Save migration bumped to **v6**:
  - Initializes/clamps driver HP + equipped armor.
  - Initializes vehicle section/tire HP when defs are available.

### Feature: vehicle section + tire damage model
- Vehicles now track:
  - **Section HP** (`CurrentHpBySection`) and **Section AP** (`CurrentArmorBySection`) for: Front/Rear/Left/Right/Top/Undercarriage.
  - **Tire HP** (`CurrentTireHp`) and **Tire AP** (`CurrentTireArmor`).
- Added base structural HP to vehicle defs (`BaseHpBySection`, `BaseTireHp`).

### UI: VehicleStatusHud (top-right)
- New top-right HUD (under PlayerStatusHud) shows the **active vehicle** with:
  - Placeholder image box
  - Section HP/AP bars positioned around it
  - Tire status grid (FL/FR/RL/RR)
  - All bars show `current/total` inside the bar.

### Combat: positional hit mapping
- VehiclePawn now exposes section/tire/driver **hitboxes** for ray-hit part identification.
- Arena damage application now uses hit part:
  - Tires take tire damage
  - Sections take section damage
  - Driver takes driver damage (with small chip-through during the transition)

Verification notes
- In arena: take hits and confirm **PlayerStatusHud HP/AP** changes correctly (AP absorbs first).
- Confirm VehicleStatusHud shows section/tire values changing when hit.
- Quick Repair ($) should restore section/tire HP/AP to full.

## 2026-02-22 (Build: 2026-02-22-05)

### UI polish: top-right HUD
- PlayerStatusHud is now **compact** with bars vertically centered and ~50% reduced width.
- VehicleStatusHud no longer stretches unnecessarily; tightened widths and centered key labels.
- Front/Rear bars now show **Armor on top**; Left/Right bars show **Armor on the left**.
- Added extra spacing above the Tires section and centered the tire grid.
- Value text font size reduced slightly inside all bars.

### UI palette tweaks
- Adjusted full-health green to a slightly darker shade.
- Adjusted full-armor blue to be slightly darker.
- Vertical bar text now rotates 90° for readability.

## 2026-02-22 (Build: 2026-02-22-06)

### HUD regression fixes
- Fixed `PlayerStatusHud.tscn` parent paths (scene instantiation errors and arena-start NRE).
- Restored live HUD updates so bars show real `current/total` values (no more `0/0` placeholders during combat).
- VehicleStatusHud: Front/Rear section bars are now **smaller and centered** for better visual balance.

## 2026-02-22 (Build: 2026-02-22-07)

### HUD: TargetStatusHud (upper-left)
- Moved the Target indicator out of the Arena panel into a dedicated **TargetStatusHud** mounted to the upper-left.
- TargetStatusHud now displays **enemy driver HP + AP** using the same bar style as the player.
- Removed enemy HP/speed from the Arena HUD text to reduce clutter.
- Shifted the Arena HUD panel downward to sit below TargetStatusHud.

### HUD: VehicleStatusHud (upper-right)
- Reduced overall VehicleStatusHud width substantially (more compact).
- Vehicle image placeholder is now **square/taller** instead of wide.
- Tires now show **AP above HP**.
- Added **Weapons** list (mounted weapons + ammo counts) under Tires.
- Added **Speed** bar (gold fill, white text) under the Weapons list.

### UI consistency
- Slightly reduced in-bar font size.
- Tweaked full-health green and full-armor blue to be a touch darker.



## 2026-02-22 (Build: 2026-02-22-08)

### HUD space-saving polish
- VehicleStatusHud: removed Front/Rear/Left/Right direction labels (positions are self-explanatory).
- Reduced font size for VehicleStatusHud headers/labels (Vehicle/Top/Under/Tires tire positions/Weapons/Speed) to reclaim space.
- Mid row (left/vehicle/right) is now centered with improved side spacing so left/right bars sit more naturally between border and vehicle image.
- Player/Target HP/AP label font size reduced to improve vertical alignment.

## 2026-02-22 (Build: 2026-02-22-09)

### HUD/UI polish
- TargetStatusHud is now a **single-line** summary: `Target: <name>    HP [cur/max]    AP [cur/max]` (no stacked bars).
- PlayerStatusHud width reduced ~20% and tightened internal padding.
- VehicleStatusHud left/right vertical section bars are now centered more symmetrically between the panel edge and the vehicle placeholder.
- Weapons list readability improved (smaller font, left padding, and light spacing between items).

## 2026-02-22 (Build: 2026-02-22-10)

### HUD/Overlay polish
- TargetStatusHud one-line layout restored **HP/AP bars** with values inside.
- ConsoleOverlay set to **60% screen width**.
- DebugOverlay removed.
- VehicleStatusHud aligned under PlayerStatusHud and shows a **top-down vehicle preview** in the center panel.

## 2026-02-22 (Build: 2026-02-22-11)

### HUD/Overlay tweaks
- TargetStatusHud: removed bracket labels around HP/AP bars.
- ConsoleOverlay moved to **bottom-left** (still 60% width).
- Arena HUD panel: removed ammo count (Tier only).

## 2026-02-22 (Build: 2026-02-22-12)

### Startup display
- Game now forces **fullscreen window mode** at startup.

## 2026-02-22 (Build: 2026-02-22-13)

### Startup display
- Fullscreen startup now also forces the **root viewport** to render at the **native fullscreen resolution** (prevents fullscreen window with a smaller game area).

## 2026-02-22 (Build: 2026-02-22-14)

### Startup display
- Only forces the "native fullscreen resolution" content scale when the window actually reaches fullscreen (or matches screen size).
  - Prevents the game from shrinking/letterboxing inside a small editor-run window when fullscreen isn't applied (common when the game is embedded).


## 2026-02-22 (Build: 2026-02-22-15)

### City menu
- Added an **Exit** button to quit the game.

### Display
- Added **F11** toggle for fullscreen ↔ windowed.
- Toggle reuses the same "only scale when fullscreen actually applies" logic to avoid editor-embedded shrink/letterboxing.

## 2026-02-22 (Build: 2026-02-22-16)

### Targeting
- Added a simple **3D target indicator** (glowing ring + arrow) that follows the currently selected target.

### Weapons + mounts (foundation)
- VehiclePawn now builds a more car-like **proxy 3D model** (still box-based) to replace the old single box mesh.
- Vehicle defs now support mount intent via `Kind` (`Fixed` vs `Turret`) and optional yaw limits.
- VehiclePawn creates mount points from vehicle defs (fixed and turret pivots) and renders **mounted weapon proxy boxes**.
- Turret mounts yaw toward the pawn's `AimWorldPosition`; firing now uses the **weapon muzzle direction** when available.
- Compact vehicle mount `R1` moved from **Rear → Top** and is treated as a **360° turret**.

### Starter loadout
- New starter vehicles now include a basic loadout: **F1 Machine Gun** + **R1 Guided Missile**.

## 2026-02-22 (Build: 2026-02-22-17)

### Target indicator
- Fixed build errors in `TargetIndicator3D` by replacing `TorusMesh` / `ConeMesh` usage with a **procedurally generated ring mesh** (ImmediateMesh triangles) and a simple marker mesh.

## 2026-02-22 (Build: 2026-02-22-18)

### Arena polish + fixes
- Enemy vehicles now spawn with a **default weapon loadout** (matches player mount ids when possible; otherwise uses a starter MG + Missile).
- Fixed proxy **tire orientation** (no longer sideways) and added **front wheel steering visuals** based on actual turn rate.
- Camera pulled back (~30% higher/further) for better combat visibility.
- Added a small **vehicle overlap resolution** fallback to prevent clipping through each other if collision settings are misconfigured.

## 2026-02-22 (Build: 2026-02-22-19)

### Build fix
- Fixed a C# compile break in `ArenaRealtimeView` caused by mistakenly escaped quotes in the enemy loadout fallback logic.


## 2026-02-23 (Build: 2026-02-23-06)

### Weapon firing behavior
- Primary firing now prefers the **Machine Gun mount** (`wpn_mg`) when present, preventing "bullet" weapons from firing from an auto-aim turret mount by default.

## 2026-02-23 (Build: 2026-02-23-09)

### Combat
- Fixed "shots go through enemy" when firing straight from raised weapon mounts:
  - Vehicle hitbox Areas now extend upward so flat-plane bullet rays intersect the target.
  - Raycast now explicitly uses an all-layers collision mask (damage is still gated to the intended pawn).

## 2026-02-25 (Build: 2026-02-25-04)

### Arena floor texture
- Arena floor now uses the tiled concrete texture (when present): `Assets/Images/Textures/Ground/concrete_1.png`.

### Weapon polish
- Removed the loud "miss" SFX (no sound is played when shots miss).
- Weapon visual scaling: `Scale` is now applied *after* `DesiredLength` normalization so per-weapon scaling works.
- Added per-weapon audio volume knobs in `Data/Config/weapon_visuals.json` (starting with `FireVolumeDb`).

## 2026-02-25 (Build: 2026-02-25-05)

### Arena environment
- Arena bounds walls + obstacles now use the same tiled concrete texture (triplanar) as the floor.
- Added several additional interior wall obstacles (deterministic "random" placement).

### Arena flow
- Post-match no longer instantly shows the post-encounter panel.
- After win/lose, a large gold message appears: **"hold G to exit"**.
- Holding **G** for ~3 seconds exits the arena salvage phase and shows the post-encounter panel.

### Enemy AI
- Improved enemy driving slightly (target leading + alignment-aware throttle) to reduce circling/spinning.

## 2026-02-25 (Build: 2026-02-25-06)

### Arena visuals
- Switched arena floor to a Poly Haven PBR texture set (clean_asphalt): albedo + normal + roughness.
- Added `Docs/Assets/POLYHAVEN_CLEAN_ASPHALT.md` with the expected local unzip path for texture files (Assets are not shipped in AI zips).

## 2026-02-25 (Build: 2026-02-25-07)

### Fixes
- Fixed arena shader compilation errors by reconstructing per-fragment world position from view-space `VERTEX` using `MAIN_CAM_INV_VIEW_MATRIX` (Godot 4 spatial shaders don’t expose `WORLD_POSITION`).
- Fixed runtime error in `ConsoleOverlay` by using snake_case when calling engine methods via `CallDeferred`.

### Arena visuals
- Arena floor now prefers the Poly Haven **clean_asphalt** PBR set when present at the expected Assets path; otherwise falls back to the legacy concrete floor material.

## 2026-03-14 (Build: 2026-03-14-15)

### Arena combat
- Implemented the first true **tracking-missile lock-on** pass for arena combat.
- Guided missiles now use the installed targeting computer's **LockRange** and will acquire a live lock when a valid target is selected and within range.
- Locked missiles now travel as visible **homing projectiles** instead of behaving like straight ballistic shots.
- Missile impacts now apply **explosive splash damage** with strong tire/undercarriage pressure, making them feel distinct from machine guns.
- Enemy AI can also use the same lock-on missile flow against the player.

- Build 2026-03-15-20: started a broader refactor operation with reusable UiKit menu helpers (`OptionalBackgroundPresenter`, `ResponsivePanelLayout`) plus JSON-backed vehicle build presets for starter vehicles and arena enemies. Starter/enemy vehicle creation now flows through a shared build factory instead of scattering loadout defaults across UI/session code.

- Build 2026-03-15-23: continued the refactor pass by extracting shared arena weapon-slot / targeting-computer resolution into reusable helpers (`ArenaWeaponLoadoutResolver`, `ArenaEnemyFirePlanner`) and removing duplicated ammo-consumption logic in favor of `AmmoMath`. Arena briefing + salvage panels also now use the shared `ResponsivePanelLayout` pattern so they stay better positioned across desktop resolutions.
- Build 2026-03-15-24: fixed the immediate compile regression from the arena combat refactor by restoring the missing `WastelandSurvivor.Core.IO` namespace imports in `ArenaEnemyFirePlanner` and `ArenaWeaponLoadoutResolver` so `DefDatabase` resolves again.

- Build 2026-03-15-26: fixed the next refactor compile regression by giving `WeaponVisualConfigStore` its own config-path constant after it was moved outside `WeaponVisualFactory`, and by qualifying the UI theme config path inside `GameUiTheme.UiThemeStore` so it no longer collides with `JsonConfigStore.ConfigPath`.

- Build 2026-03-16-43: replaced the failing VehicleStatusHud live-world/clone preview path with an isolated preview-only VehiclePawn proxy rendered in the HUD SubViewport. The proxy now mirrors the player vehicle visuals/loadout/body color and rotates opposite the live vehicle yaw so the preview stays readable from a stable top-down angle.
- Build 2026-03-16-48: replaced the HUD vehicle preview's live re-draw approach with a one-shot captured vehicle portrait. The HUD now keeps the working on-screen preview control, captures a top-down snapshot of the actual player vehicle visual at match start/vehicle change using a hidden SubViewport, and then reuses that texture in the HUD. This keeps the preview closer to the original intended look without fighting a permanently live HUD render path.

- Build 2026-03-16-49: fixed the HUD vehicle portrait capture path by forcing the hidden SubViewport to render even while off-screen (`render_target_update_mode = UPDATE_ALWAYS` during capture). The HUD now keeps the viewport texture directly instead of reading back an image, then disables further viewport updates once the portrait is captured.

- Build 2026-03-16-50: fixed the HUD vehicle portrait capture fallback again by allowing off-tree duplicated preview meshes to contribute bounds during snapshot setup. The capture path was incorrectly rejecting those meshes via `IsVisibleInTree()`, so the portrait viewport never finished building and the HUD kept drawing the simplified fallback icon.

- Build 2026-03-16-51: replaced the failing hidden-SubViewport vehicle HUD portrait capture path with a crop taken directly from the already-rendered main arena viewport around the live player vehicle. This keeps the preview aligned with the real in-game model at match start without depending on duplicated off-screen vehicle visuals.

- Build 2026-03-16-54: rebuilt the VehicleStatusHud preview path again around a dedicated preview-only VehiclePawn scene inside its own HUD SubViewport. The HUD now renders the same 3D vehicle/loadout pipeline as the live arena pawn, fits a fixed straight-down orthographic camera after the preview pawn finishes building, suppresses the old drawn fallback while the viewport is active, and removes preview audio/physics so the top-down vehicle card stays stable and readable.

- Build 2026-03-16-55: replaced the HUD preview presenter path again so the preview-only `SubViewport` now renders through a `TextureRect` fed by the viewport texture instead of relying on a nested `SubViewportContainer`. The HUD preview viewport now also explicitly enables `own_world_3d`, forces `render_target_update_mode = UPDATE_ALWAYS`, and tags the preview pawn with the player-vehicle group so the player body/material variant path matches the live car.

- Build 2026-03-17-61: tightened desktop menu responsiveness again by letting Garage/Workshop panels grow to their viewport-fit percentages on larger screens, shrinking the generated SVG button icons to sane in-button sizes, and reducing the oversized workshop/garage header art so the body content can use more of the window.

- Build 2026-03-17-70: left-aligned the in-world salvage action menu for cleaner key/action columns and shipped towing polish v1 with explicit Detach Tow Cable interactions plus a first tension-based tow-cable snap/failure rule.

- Build 2026-03-17-71: fixed the start-box tow recovery soft-lock by letting post-match auto-finish continue updating even after the salvage hold flow is reset, so the loading overlay now completes and hands off to rewards correctly. Added a new salvage/tow status panel in the arena HUD with start-box distance, tow-state guidance, and cable-tension warning text during the post-win salvage phase.

- Build 2026-03-17-74: improved arena enemy combat AI by adding data-driven orbit/stand-off driving, mount-aware tactical profiles based on installed weapons, and alignment-aware firing so the computer driver spends less time nose-diving into the player and does a better job keeping weapons on target.

- Build 2026-03-17-75: fixed the arena enemy-driver regression where the new tactical steering pass could leave the AI effectively stationary. The computer now uses direct pursuit until it is actually moving and close enough to benefit from orbit/stand-off behavior, so it keeps its smarter firing without forgetting to drive.

- Build 2026-03-20-76: replaced the old random interior arena walls with structured barrier lanes and optional asset-backed obstacle props (concrete road barriers, tires, trash cans, wrecked cars). Added AI obstacle sensing + multi-phase unstuck recovery so enemies can steer around cover and back out when they nose into arena props or walls.
- Build 2026-03-21-82: standardized arena enemies around a front machine gun plus rear mine dropper loadout, switched enemy ammo consumption to use the actual installed ammo inventory so opponents spawn with full configured ammo, and added a dedicated mine-deployment tactic for tailing / pressure situations instead of letting the mine dropper become the AI's primary weapon posture. Survivors now leave arena matches with driver HP and personal armor restored to full, and the post-match save flow persists that reset automatically.
- Build 2026-03-21-84: fixed the ArenaVehicleAiDriver wall-recovery compile regression by renaming the wall-escape locals so they no longer shadow the later recovery-path locals in BuildRecoveryIntent. This clears the CS0136 errors in the arena AI build path.
- Build 2026-03-21-92: fixed premium menu shell scaling so ResponsivePanelLayout now applies VisualScale through the actual layout bounds instead of a Control transform that was not visibly shrinking the City/Garage/Workshop shells. Also tightened the authored garage/workshop/city shell min sizes, button heights, and header art so the smaller floating panels stay readable while revealing more of the background scene.

- Build 2026-03-21-93: fixed the premium menu shell scale pass again by sizing `ResponsivePanelLayout` from the viewport visible rect instead of the raw display-server window size. This avoids DPI-inflated shell bounds on desktop and finally makes the 70%-sized City/Garage/Workshop shells read smaller on screen. Added smaller authored fallback panel bounds in the three premium menu scenes so the reduced shell size still shows up even if a stale compile leaves the layout helper unchanged.

- Build 2026-03-22-94: stopped chasing full Control-transform scaling on the premium management shells and instead tightened the internal layouts so they make better use of the smaller floating panels. City Hub travel/intel sections are now compact instead of stretching into large empty cards, Garage selected-vehicle cards/list rows/footer are denser, and Workshop active/loadout sections were tightened so more useful controls fit onscreen without losing the shared premium look.

- Build 2026-03-22-98: fixed the Workshop drawer compile regression from the new context-summary pass by restoring the missing `WastelandSurvivor.Game.Systems` import in `WorkshopView`, which resolves the `VehiclePresentation` / `VehicleRecoveryValueMath` lookup errors during the main game build.


- Build 2026-03-23-101: replaced the blocking arena match-ended continue modal with a non-modal win banner and a real south exit-gate finish flow, so post-win rounds now end by driving through the highlighted arena exit instead of holding Continue and the pause menu stays usable. Also strengthened perimeter fog and changed arena AI wall recovery to use a short committed containment target so enemies stop seesawing against the outer walls as often.
