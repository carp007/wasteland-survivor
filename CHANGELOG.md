# Changelog (Project-local)

## Build: 2026-03-01-11
- Refactor Step 13: Added a new `WastelandSurvivor.UiKit` project (shared UI/dialog toolkit) and referenced it from the main Godot project.
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
