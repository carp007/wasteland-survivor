# Refactoring progress

This file tracks **completed** refactor steps.

Guidelines:
- Each step should be small and easy to review.
- Each step must ship as a zip so we can roll back easily.
- Prefer “pure extraction” steps that keep gameplay behavior unchanged.

---

## Step 01 — SceneBinder + first adoption (VehicleStatusHud)

**Goal:** establish a reusable, high-signal node binding pattern and prove it on one HUD.

### Changes
- Added `Scripts/Framework/SceneBinding/SceneBinder.cs`
  - `Req<T>(path)` for required nodes with better error messages
  - `Opt<T>(path)` for optional nodes
  - `ReqFallback<T>(primary, fallback)` for transitional scene-tree shapes
- Updated `Scripts/UI/VehicleStatusHud.cs` to use `SceneBinder` in `EnsureBound()`
  - Removes local `GetNodeWithFallback` helper
  - Preview nodes now bind through `Opt<T>`

### Behavior changes
- None intended. This is an internal refactor only.

### Why this matters
- Node-tree churn is common while iterating on UI. When something breaks, the binder error includes:
  - the HUD root path/name
  - expected vs actual node type
  - the exact path that failed

### Next candidates for adoption
- `ArenaRealtimeView` HUD binding + fallback binding
- `PlayerStatusHud`, `TargetStatusHud`, `RadarHud` (all are heavy on GetNode strings)

---

## Step 02 — Adopt SceneBinder in ArenaRealtimeView + core HUDs

**Goal:** reduce iteration churn by making the biggest HUD-heavy scene (`ArenaRealtimeView`) and the compact
HUD controls (`PlayerStatusHud`, `TargetStatusHud`) use the same binding helper and error reporting.

### Changes
- Updated `Scripts/UI/ArenaRealtimeView.cs`
  - Added `EnsureBound()` which binds all required UI nodes via `SceneBinder`
  - Keeps existing VehicleStatusHud fallback behavior (no new HUD logic)
- Updated `Scripts/UI/PlayerStatusHud.cs` and `Scripts/UI/TargetStatusHud.cs`
  - Added `EnsureBound()` and migrated `GetNode(...)` bindings to `SceneBinder`

### Behavior changes
- None intended. This is a binding refactor only.

### Why this matters
- `ArenaRealtimeView` is usually the first place we see “HUD is broken / node not found / wrong type” issues.
  This makes those failures easier to diagnose in seconds instead of minutes.


---

## Step 03 — Adopt SceneBinder in menu views + shared UI controls

**Goal:** spread the same typed binding pattern outside the arena HUD so that menu screens and reusable controls
fail fast with clear errors when scene trees change.

### Changes
- Updated menu/views to use `SceneBinder` instead of ad-hoc `GetNode/GetNodeOrNull` bindings:
  - `Scripts/UI/BootSplashView.cs`
  - `Scripts/UI/CityShell.cs`
  - `Scripts/UI/GarageView.cs`
  - `Scripts/UI/WorkshopView.cs`
- Updated shared controls to use `SceneBinder` for required child nodes:
  - `Scripts/UI/ValueBar.cs`
  - `Scripts/UI/ActionPromptOverlay.cs`

### Behavior changes
- None intended. Binding failures now throw earlier with higher-signal errors (expected/actual node types and paths).

### Why this matters
- Menu screens change frequently during iteration. Consistent binding patterns reduce time spent chasing:
  - renamed/moved nodes
  - mismatched node types
  - partially-updated UI scenes

---

## Step 04 — Introduce ScreenRouter and migrate primary screen transitions

**Goal:** centralize UI screen swapping so navigation is consistent and easier to evolve (future: push/pop stack,
transitions, and overlays).

### Changes
- Added `Scripts/Framework/UI/ScreenRouter.cs`
  - `TryReplace(scenePath)` – behavior-neutral screen swap (clears managed screens, instantiates a new one)
  - `TryPush(scenePath)` / `TryPop()` – included for future work, but not relied on yet
  - Consistent logging when a scene path is missing or fails to load
- Updated `Scripts/App/AppRoot.cs`
  - Creates a `ScreenRouter` for the main `UIRoot` CanvasLayer and registers it in `App.Services`
  - Boot splash and CityShell are now shown via the router
- Updated UI screens to use the router when available (with a safe fallback to the previous parent-swap logic):
  - `Scripts/UI/CityShell.cs` (Garage / Workshop / Arena)
  - `Scripts/UI/GarageView.cs` (Back)
  - `Scripts/UI/WorkshopView.cs` (Back)
  - `Scripts/UI/ArenaRealtimeView.cs` (Return to City)

### Behavior changes
- None intended. Navigation should look and behave the same, but is now routed through a shared helper.

### Why this matters
- We reduce duplicated “load scene → add child → queue free” blocks.
- Future refactors (like push/pop view stacks, transitions, and modal dialogs) can be implemented once.


---

## Step 05 — Centralize UI scene paths + navigation call sites

**Goal:** remove hard-coded scene path strings from UI scripts and standardize navigation behind a tiny helper so
future UI iteration doesn’t require hunting string literals or duplicating router-fallback logic.

### Changes
- Added `Scripts/UI/GameScenes.cs`
  - Central catalog of common UI scene paths (`CityShell`, `GarageView`, `WorkshopView`, etc.)
  - `TryGetArenaEntry()` resolves the best available arena entry scene from a candidate list
- Added `Scripts/Framework/UI/UiNav.cs`
  - `UiNav.Replace(current, scenePath)` prefers `ScreenRouter` when available
  - Falls back to legacy parent-swap navigation when `ScreenRouter` isn’t registered
- Updated navigation call sites to use `GameScenes` + `UiNav` (no intended behavior change):
  - `Scripts/App/AppRoot.cs` (boot splash + city shell)
  - `Scripts/UI/CityShell.cs` (garage / workshop / arena)
  - `Scripts/UI/GarageView.cs` (back)
  - `Scripts/UI/WorkshopView.cs` (back)
  - `Scripts/UI/ArenaRealtimeView.cs` (return to city)
  - `Scripts/UI/ArenaRealtimeView.cs` (VehicleStatusHud scene path)

### Behavior changes
- None intended.

### Why this matters
- Scene renames/moves are now a **single-file update** (`GameScenes.cs`).
- UI scripts stop duplicating “router if present else legacy” blocks.


---

## Step 06 — Introduce IGameNavigator facade and migrate UI screens

**Goal:** UI screens should not know about routing implementation details (router vs legacy) or even which
scene-path constant to use. They call a game-level navigation API and the rest is handled centrally.

### Changes
- Added `Scripts/Game/Navigation/IGameNavigator.cs` + `Scripts/Game/Navigation/GameNavigator.cs`
  - `ToCityShell / ToGarage / ToWorkshop / ToArena`
  - Arena entry selection (candidate scene resolution) moved into the navigator
- Registered `IGameNavigator` in `Scripts/App/AppRoot.cs`
- Updated UI screens to use `IGameNavigator` instead of calling `UiNav` directly:
  - `Scripts/UI/CityShell.cs`
  - `Scripts/UI/GarageView.cs`
  - `Scripts/UI/WorkshopView.cs`
  - `Scripts/UI/ArenaRealtimeView.cs`

### Behavior changes
- None intended.

### Why this matters
- Future navigation improvements (push/pop view stacks, transitions, modals) can be implemented centrally.
- UI scripts no longer need to import `Framework.UI` or know scene-path constants.


---

## Step 07 — Harden ValueBar lifecycle (fix resize-before-ready NRE)

**Goal:** eliminate a common Godot lifecycle footgun where `NotificationResized` can fire before `_Ready`,
causing UI controls that bind child nodes in `_Ready` to throw null reference exceptions.

### Changes
- Updated `Scripts/UI/ValueBar.cs`
  - Adds a `_bound` guard and makes `EnsureBound()` idempotent.
  - Guards `NotificationResized` so it won’t touch bound nodes before `_Ready`.
  - Makes `SetValues/SetCustom/UpdateFill` no-op until the node is ready (prevents early calls from crashing).

### Behavior changes
- None intended. This is purely to prevent spurious runtime errors during screen instantiation/layout.

### Why this matters
- We want reusable HUD primitives (like `ValueBar`) that are robust across scenes and future projects.


---

## Step 08 — Introduce ModalHost + IModalService

**Goal:** establish reusable infrastructure for modal dialogs/confirmations without each screen hand-rolling overlays.

### Changes
- Added `Scripts/Framework/UI/ModalHost.cs`
  - Full-screen overlay that hosts stacked modal content
  - Supports dim background, optional centering, Escape-to-close, and optional pause
- Added `Scripts/Framework/UI/IModalService.cs` + `Scripts/Framework/UI/ModalService.cs`
  - Small service abstraction so game/UI code can show modals without parenting nodes manually
- Updated `Scripts/App/AppRoot.cs`
  - Ensures a `ModalHost` exists under `OverlayRoot`
  - Registers `IModalService` in `App.Services`

### Behavior changes
- None intended. The service is not yet used by game UI; it is infrastructure for future steps.

### Why this matters
- Standardizes dialogs (confirmations, settings, loot, etc.) so new screens can use a consistent mechanism.
- Keeps UI screens focused on game behavior, not scene-tree wiring.


---

## Step 09 — Adopt IModalService in PauseMenuOverlay

**Goal:** replace ad-hoc pause-menu dialogs (settings stub + exit confirm) with standardized, reusable modals.

### Changes
- Updated `Scripts/UI/PauseMenuOverlay.cs`
  - Pause menu now uses `IModalService` to show:
    - Settings stub dialog (Close)
    - Exit confirmation (Exit / Cancel)
  - Pause menu no longer maintains separate internal panels for settings/exit.
  - Escape behavior is now safer: if a modal is open, ModalHost consumes Escape and the pause menu stays open.
- Updated `Scripts/Framework/UI/IModalService.cs` + `Scripts/Framework/UI/ModalService.cs`
  - `ShowMessage` and `ShowConfirm` accept optional `ModalOptions` so callers can control dim/escape/autofocus.

### Behavior changes
- None intended. This is a presentation refactor to standardize dialogs.

### Why this matters
- New UI screens can use the same modal system for confirmations/settings/loot without hand-rolling overlays.
- Keeps pause/menu UI smaller and more reusable across future Godot projects.


---

## Step 10 — Extract reusable DialogCard (shared dialog layout)

**Goal:** avoid duplicating dialog scaffolding (title/body/button area) across the codebase and make
custom dialogs easier to build going forward.

### Changes
- Added `Scripts/Framework/UI/DialogCard.cs`
  - Reusable dialog panel with title/body labels and a button/content area.
  - Supports pre-Ready configuration (title/body/buttons can be set before the node enters the tree).
  - Styling is injected via callbacks so the control can be reused in future projects.
- Updated `Scripts/Framework/UI/ModalService.cs`
  - `ShowMessage` / `ShowConfirm` now build dialogs using `DialogCard`.
  - Keeps the prior look (gold title) by applying project-specific styling via `DialogCard.DialogStyler`.

### Behavior changes
- None intended.

### Why this matters
- New modals (settings screens, loot panels, multi-step confirmations) can reuse the same dialog shell.
- Keeps the modal framework easier to carry forward into future Godot projects.


---

## Step 11 — Register core services in AppRoot._EnterTree (lifecycle hardening)

**Goal:** eliminate a common Godot lifecycle footgun where UI children can run `_Ready()` before the parent has
registered services (router/modal/navigation). This reduces the need for “lazy service resolution” in UI scripts.

### Changes
- Updated `Scripts/App/AppRoot.cs`
  - Registers core services in `_EnterTree()` instead of `_Ready()`:
    - `ScreenRouter`
    - `IModalService`
    - `IGameNavigator`
  - `_Ready()` now only subscribes to boot events and triggers the initial UI flow.

### Behavior changes
- None intended.

### Why this matters
- Godot calls `_Ready()` **bottom-up** (children first). Registering services in `_EnterTree()` ensures children
  can safely resolve services during `_Ready()`.


---

## Step 12 — Fix App.Services overwrite (DI stability) + tighten PauseMenuOverlay binding

**Goal:** ensure UI navigation services remain registered after boot, and reduce reliance on deferred binding.

### Changes
- Updated `Scripts/App/App.cs`
  - Removed `Services = new GameServices()` during boot.
  - Added a guard comment explaining why replacing the registry breaks UI navigation (services are registered
    by `AppRoot` early).
- Updated `Scripts/UI/PauseMenuOverlay.cs`
  - Binds `IModalService` immediately in `_Ready()` (with a deferred retry as a safety net).
- Updated `Docs/ASSISTANT_PLAYBOOK.md`
  - Added a lifecycle note: never replace the `GameServices` instance at runtime.

### Behavior changes
- Fixes broken city-menu navigation caused by `App` overwriting the service registry during boot.

### Why this matters
- Keeps DI/service lookups consistent and avoids “works once, then breaks” ordering issues.
- Makes it easier to build new UI screens without sprinkling deferred/lazy service resolution everywhere.


---

## Step 13 — Introduce UiKit project (shared UI/dialog toolkit)

**Goal:** create a reusable UI/dialog toolkit project inside the solution so UI refactoring can continue in a
clean, copyable library that can be reused across future Godot/C# games.

### Changes
- Added `UiKit/WastelandSurvivor.UiKit.csproj` (Godot.NET.Sdk class library).
- Added `Wasteland Survivor.sln` containing:
  - `Wasteland Survivor` (main game project)
  - `WastelandSurvivor.UiKit` (shared UI/dialog toolkit)
- Updated `Wasteland Survivor.csproj`
  - Excludes `UiKit/**` from compilation (prevents duplicate types).
  - Adds a `<ProjectReference>` to `UiKit/WastelandSurvivor.UiKit.csproj`.
- Moved reusable UI/framework code into UiKit:
  - `SceneBinder` (node binding helper)
  - `ScreenRouter` (screen navigation)
  - `ModalHost`, `IModalService`, `ModalService`, `DialogCard` (modal/dialog infrastructure)
  - `UiNav` (router-first navigation helper; now game-agnostic)
- Added `ModalDialogStyle` so UiKit dialogs do not depend on `GameUiTheme`.
  - `AppRoot` injects the game’s theme/styling hooks when registering `IModalService`.

### Behavior changes
- None intended.

### Why this matters
- Establishes a clear “portable UI toolkit” boundary.
- Prevents game-specific UI from becoming a monolith as features expand.
- Makes future UI refactors safer: we can harden toolkit components once and reuse them.
