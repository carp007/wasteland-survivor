# Human play-test log

The canonical record of real human minutes on the game and what came of them. AI loops respond to
these notes; probe/telemetry numbers never outrank them.

---

## Session 2: 2026-07-22 — build ~181 (post loop #5), the loop #6 driver

First human session since the build-125 era. Thirteen notes; every one was addressed in loop #6
(builds 182-185, commit d2778d7) — dispositions below.

| # | Note (paraphrased) | Disposition |
|---|---|---|
| 1 | Title screen "Wasteland Survivor" truncated; generate a better title screen | FIXED 182 — real main menu, art top-anchored (never crops), Ken Burns drift |
| 2 | Mounted weapons decent but not proportional; resize or replace models | FIXED 182 — per-axis WidthScale/HeightScale slimming + trimmed lengths |
| 3 | UI improved but still feels like a business application | IMPROVED 183-185 — city-name headline, journey interstitial, showroom thumbnails, gold focus ring; judge B- with backlog (garage tabs, map legend → follow-ups landed 186) |
| 4 | Travel between cities is important — start building it out | BUILT 183 — journey interstitial (Stage 1) + HIGHWAY combat venue (Stage 2); Stage 3 roadmap in TRAVEL_BUILDOUT_PLAN.md |
| 5 | Parts store needs better background images | PARTIAL — store uses the darkened city backdrop; backdrop presence is a standing item |
| 6 | Vehicle icons in Parts Store look pretty bad | FIXED 183/185 — cached 3D showroom thumbnails, front-3/4 for class identity |
| 7 | Yellow lines beside player / red-orange beside AI make no sense (target indicators are great — keep) | FIXED 182 — underglow identity strips removed |
| 8 | Engine sounds like someone randomly hitting organ keys — worse than before | FIXED 182 — rate-matched crossfade + drivetrain floor; WAV A/B proof via --shot=enginesweep |
| 9 | Player outside vehicle should have a basic weapon; a store should sell personal weapons + armor; spec it | SHIPPED 183-185 — spec section, 4 weapons, Clinic & Outfitter sales, arena firing, armor ladder 50/58/65/85/110; bail-out duels in progress (build 186) |
| 10 | Arena obstacles random; want structure for movement strategy | FIXED 183 — PILLARS/RING/LANES/CROSSBUNKERS archetypes, deterministic per city+tier |
| 11 | Graphics better; keep improving; arcade-but-realistic; cars a bit cartoony | IMPROVED 183 — hull desaturation/wear, automotive paint response, deeper player gold; next step = real per-class detail meshes |
| 12 | "AI" drivers are terrible; need a much more sophisticated system | FIXED 183 — PD steering, planned detours, cover play; accuracy 43%→54%, recoveries ~0 |
| 13 | Keep iterating on UI + gameplay generally | Loop #6 (+ judge round) is the response |

**Still awaiting human minutes:** everything loop #6 built (builds 182+). The four golden paths in
NEXT_TASK.md are the suggested route.

---

## Session 1: ~2026-06-11 era — builds ≤125

Feedback delivered across the early screenshot-driven sessions; headline outcomes: the camera-zoom
identity rule ("toy cars" framing is the game's charm — never zoom in), the "never do visual work
blind" rule and the screenshot harness that followed, fog-of-war blackout fix, obstacle asset path
fix. See ASSISTANT_PLAYBOOK.md for the distilled rules.
