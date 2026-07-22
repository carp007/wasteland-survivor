# Travel Buildout Plan (city-to-city travel as real gameplay)

Status: Stage 1 landed 2026-07-22 (loop #6). Play-test driver: *"Travel between cities is an
important part of the game. We should start building that out."*

## Where travel stands

The mechanical layer is already deep (road graph, fuel + mid-route stations, tolls, ambush/bounty/
merchant/salvage-crew/scavenge rolls, freight delivery, capture stakes) — but the *experience* was
"click a route card, instantly get a modal." Travel resolved like a spreadsheet, not a drive.

## Staged direction

### Stage 1 — journey interstitial (LANDED, build 183)
`Scripts/UI/TravelJourneyOverlay.cs`, shown by `CityShell.SetCity` between the (already-atomic)
travel commit and the outcome modals (`PresentTravelOutcome`). Skippable ~5-7s presentation:
scrolling highway band + roadside silhouettes, FROM → TO header, km ticker + leg progress, fuel
needle draining departure→arrival, receipts (toll / station top-up / freight payout), rolling road
flavor lines, and a hard red/amber interrupt flash ("RAIDERS INBOUND", "MERCHANT CONVOY AHEAD", …)
when the leg's headline event takes over. All state is committed before it plays — the overlay is
pure presentation, so skipping is always safe.

### Stage 2 — HIGHWAY combat venue (LANDED, build 183)
`ArenaVenueKind.Highway`: road ambushes (`IsRoadAmbush`) and bounty hunts (`IsBountyHunt`) fight
on a straight two-lane asphalt ribbon down the joust axis — center dashes + edge lines, 4m
guardrail segments with per-segment cover collision (weathered gaps), rust-detailed shoulder
derelicts, power poles, a leaning billboard. Stadium shell/crowd/floor paint suppressed; interior
archetype cover still spawns (reads as highway debris/median walls). `SpawnHighwayDressing` in
`ArenaWorld.cs`; venue pick beside the SalvageYard branch in `ArenaRealtimeView`. Harness:
`--shot=highway`. Future polish: interception/salvage-crew raids could move here too (kept on the
yard for now — the tow-rig staging fiction), and spawn logic could bias hostiles ahead/behind by
event type.

### Stage 3 — journey agency (later)
Decision points DURING the interstitial instead of pre-rolled outcomes: station stop choice
(pay premium fuel vs risk arriving dry), side-road detours (scavenge site vs time/ambush risk),
"weigh station" shakedowns on freight runs, weather bands that modify the next fight's grip.
Requires moving the event roll out of `TryTravelTo` into a per-leg script the overlay can walk —
keep the atomic-commit guarantee per decision, not per leg.

### Stage 4 — free-drive overworld (post-v1 question)
The master spec keeps city interaction menu-based, but an AutoDuel-style zoomed overworld drive
(top-down, roads as real geometry, encounters as proximity triggers) remains the long-term
fantasy. Everything in Stages 1-3 (venue kinds, per-leg event scripts, journey UI) is designed to
survive that transition: the overworld would replace the *presentation* layer, not the resolution
layer.

## Files
- `Scripts/UI/TravelJourneyOverlay.cs` — Stage 1 overlay (self-contained; road band is pure _Draw).
- `Scripts/UI/CityShell.cs` — `SetCity` (pre-commit leg snapshot + interrupt kind) and
  `PresentTravelOutcome` (the pre-existing outcome chain, extracted).
- Stage 2 lands in `Scripts/Arena/ArenaWorld.cs` (`ArenaVenueKind`) + encounter-start venue pick.
