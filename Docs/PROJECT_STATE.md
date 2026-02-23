# Project State (Canonical “where we are”)

## North star
See **Master Game Design Prompt** (project file). That document is authoritative even if early prototypes are simplified.

## Engine / tech
- Godot 4.6.1 Mono
- C# / net8.0
- Keyboard-only combat first (mouse not required)
- Startup forces **fullscreen** and ensures the **root viewport renders at native fullscreen resolution**. (F11 toggles fullscreen/windowed.)

## Current playable loop (stable)
- City shell UI (includes an Exit button)
- Garage + Workshop/Loadout
- Start encounter → realtime arena combat → rewards → repairs → return to city

## 3D transition (2.5D)
- The project now uses **3D models with a fixed top-down/RTS camera** (2.5D).
- `Scenes/Main.tscn` includes a `WorldRoot` (`Node3D`) used for 3D scenes.
- Arena uses `Scenes/UI/ArenaRealtimeView.tscn`, which instantiates `Scenes/Arena/ArenaWorld.tscn` into `WorldRoot`.
- `Scenes/Arena/ArenaWorld.tscn` and `Scenes/Arena/VehiclePawn.tscn` are intentionally **minimal**; the arena floor/walls and pawn collision/visuals are generated procedurally in code for robustness.
- VehiclePawn now supports **weapon mount points** (fixed + turret) driven by vehicle defs and can render **mounted weapon visuals** (proxy boxes for now).
- Camera is locked to the active pawn (currently the vehicle; later: driver on-foot).

## Controls (arena)
- W/S = throttle forward / brake-reverse
- A/D = steering
- Space = fire
- Tab = cycle targets
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
- VehicleStatusHud also shows **Mass** (vehicle + weapons + ammo + towing).
- Player gets immediate feedback on shots: **center hit marker** + hit/miss SFX; tire-pop SFX plays when a tire is destroyed.

- Target HUD is a dedicated **TargetStatusHud** (upper-left): single-line, centered layout with HP/AP **bars** (values inside the bars).
- A simple **3D target indicator** (procedural glowing ring + marker) follows the selected target so Tab targeting is readable without relying solely on HUD text.
- A top-right **PlayerStatusHud** shows **Driver HP + Driver AP (armor points)** side-by-side (compact width):
  - Driver HP is persistent (default 50).
  - Driver AP is the equipped armor buffer (Basic Kevlar by default).
- A top-right **VehicleStatusHud** (under PlayerStatusHud) shows **vehicle section + tire HP/AP** around a small top-down vehicle preview.
  - Includes a compact **Weapons** list (mounted weapons + ammo counts).
  - Includes a **Speed** bar (gold) under the weapons list.
- VehiclePawn exposes section/tire/driver hitboxes to support **positional damage**.
- If the app restarts mid-encounter, Arena Start will resume the active encounter instead of blocking.

## AI (current)
- Drives toward the player
- Fires and consumes ammo
- Basic “unstuck” behavior (reverse + turn when stuck/colliding)

## Save/state
Primary state records:
- `SaveGameState`
- `PlayerProfileState`
- `VehicleInstanceState`
- `EncounterState`

Notes:
- `PlayerProfileState` includes `DriverHpMax/DriverHp` and equipped armor (`EquippedArmorId`, `DriverArmorMax/DriverArmor`).
- Save migration bumped to **v6**.

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

## Current priorities
A) Refactor stabilization (no behavior changes)
- Keep GameSession small by pushing responsibilities into `Scripts/Game/Session/*`.
- Continue moving pure logic into `Systems/*`.
- Reduce duplication across encounter resolution paths.

B) Realtime combat playability (after A is stable)
- Readability improvements (scale/UI)
- Health bars + target indicator
- Simple VFX
- Simple handling/traction tuning

## Screen overlays
- Overlays live under `Scenes/Main.tscn` → `OverlayRoot` (CanvasLayer layer=100) so they always stay above active UI.
- `ConsoleOverlay`: bottom-left overlay (~60% screen width), toggled with tilde (~).
  - Expanded: shaded header + scrollable history + command input.
  - Collapsed: **one-line** mode showing the most recent entry (header hidden).
  - Mouse interactive (click-to-focus, draggable scrollbar, working collapse/expand).
  - Typed lines + colors: Debug (blue), Status (white), Input (gold), Error (red).
  - Replaces the old arena-only “Combat Log”.
  - Built-in commands: `help`, `clear`, `version`.

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

