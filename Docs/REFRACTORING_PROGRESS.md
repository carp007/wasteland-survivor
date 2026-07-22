## Step 13 — Arena hit/raycast + damage helper extraction

**Goal:** finish the last high-value arena-runtime extraction by moving shot hit parsing and direct vehicle-damage bookkeeping out of `ArenaRealtimeView` before returning to feature work.

### Changes
- Added `Scripts/Arena/ArenaRaycastUtil.cs`
  - owns arena shot raycasts, hitbox parsing, section/tire detection, driver-hit detection, and self-hit exclusion logic
- Added `Scripts/Arena/ArenaDamageResolver.cs`
  - owns direct-hit vehicle damage application, tire-pop detection, explosion-to-vehicle resolution, and driver chip-through calculation
- Updated `Scripts/UI/ArenaRealtimeView.cs`
  - now delegates ballistic/missile/mine hit parsing and vehicle-damage bookkeeping to the shared arena helpers

### Behavior changes
- None intended. This was a refactor-only pass aimed at preserving combat behavior while reducing controller size and duplication.

### Why this matters
- The arena screen no longer owns the lowest-level raycast + hitbox + vehicle-damage rules.
- Future ammo types and combat features now have clearer extension points (`ArenaRaycastUtil`, `ArenaDamageResolver`) instead of expanding `ArenaRealtimeView` again.
- This closes the planned “one more high-value extraction” note from the previous pass, so the next thread can move back to feature work.

---

## Step 12 — Arena post-match flow + post-encounter presenter

**Goal:** keep shrinking `ArenaRealtimeView` by extracting the arena results / hold-to-continue flow and the post-encounter panel state-building logic into focused helpers.

### Changes
- Added `Scripts/Arena/ArenaPostMatchFlow.cs`
  - owns match-ended modal lifecycle
  - owns fallback hold-to-continue timing when the modal service is unavailable
  - owns post-match outcome / killed-state bookkeeping and reset behavior
- Added `Scripts/Arena/ArenaPostEncounterPresenter.cs`
  - builds a small post-encounter view-model from `GameSession` + `DefDatabase`
  - centralizes rewards text plus repair / patch button text and disabled-state rules
- Updated `Scripts/UI/ArenaRealtimeView.cs`
  - now delegates post-match hold flow and post-panel state calculation to the new helpers

### Behavior changes
- None intended. The arena still resolves to the same salvage / repair panel flow after the hold-to-continue interaction.

### Why this matters
- `ArenaRealtimeView` sheds another chunk of post-match/results responsibility and moves closer to being a thin screen adapter.
- Reward/repair button rules are now easier to reason about and reuse in future arena/results UI passes.

---

## Step 11 — Arena targeting helper + boot splash config-store migration

**Goal:** keep shrinking `ArenaRealtimeView` by moving another arena-only runtime concern out of the screen, while also extending the shared JSON-config store path to another config-backed UI system.

### Changes
- Added `Scripts/Arena/ArenaTargetingController.cs`
  - owns arena target validity, target cycling, and target-indicator syncing for player-selected enemy vehicles
  - exposes a shared `ResolveEnemyAimTarget(...)` helper so enemy firing logic can target the player vehicle or on-foot driver through one reusable seam
- Updated `Scripts/UI/ArenaRealtimeView.cs`
  - now uses `ArenaTargetingController` instead of directly managing selected-target / cycle-index bookkeeping
- Added `Scripts/UI/BootSplashConfigStore.cs`
  - moves boot splash config loading onto the shared `JsonConfigStore` base instead of hand-rolled JSON parsing logic in `BootSplashView`

### Behavior changes
- None intended. This is primarily an organization / reuse refactor pass.
- Startup splash and menu PNG backgrounds continue using the shared no-skew cover behavior.

### Why this matters
- The arena screen is slightly smaller and now has one less controller concern to own directly.
- The boot splash joins the same shared config-loading path already used by arena AI, vehicle builds, weapon visuals, driver config, and UI theme data.

---

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
- Added `UiKit/GameUiKit.csproj` (Godot.NET.Sdk class library).
- Added `Wasteland Survivor.sln` containing:
  - `Wasteland Survivor` (main game project)
  - `GameUiKit` (shared UI/dialog toolkit)
- Updated `Wasteland Survivor.csproj`
  - Excludes `UiKit/**` from compilation (prevents duplicate types).
  - Adds a `<ProjectReference>` to `UiKit/GameUiKit.csproj`.
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

## 2026-03-01 (Build: 2026-03-01-12)

- Fix: UiKit compile error (UiNav missing brace).
- Rename: UiKit project renamed to **GameUiKit** (generic), with namespaces moved to `GameUiKit.*`.

---

## Step 15 — Attribute-driven scene bindings (SceneAutoBinder)

**Goal:** reduce repetitive UI binding boilerplate while keeping the same high-signal binding errors.

### Changes
- Added `UiKit/Scripts/Framework/SceneBinding/BindAttribute.cs`
  - Declarative field binding metadata (required vs optional; optional fallback paths).
- Added `UiKit/Scripts/Framework/SceneBinding/SceneAutoBinder.cs`
  - Reflection-based binder that uses `SceneBinder` under the hood.
  - Caches binding metadata per type.
- Migrated `Scripts/UI/CityShell.cs` to use attribute-driven binding.

### Behavior changes
- None intended.

### Why this matters
- Reduces friction when adding new UI elements and iterating on scene trees.
- Keeps UI scripts focused on gameplay/menu behavior instead of repetitive node lookup code.

---

## Step 16 — Auto-bound base classes + migrate GarageView

**Goal:** make it harder to forget binding calls (optional base classes) and expand adoption of attribute-driven bindings.

### Changes
- Added `UiKit/Scripts/Framework/SceneBinding/AutoBoundControl.cs` and `AutoBoundNode.cs`
  - These are optional base classes that call `SceneAutoBinder.Apply(...)` in `_Ready()`.
  - Current screens may keep manual calls; future screens can inherit and keep scripts smaller.
- Migrated `Scripts/UI/GarageView.cs` to `[Bind]` fields + `SceneAutoBinder`.
  - Removed `EnsureBound()` boilerplate.

### Behavior changes
- None intended.

### Why this matters
- Reduces repetitive binding code and reduces risk of introducing “forgot to bind node” bugs during UI iteration.

---

## 2026-03-02 (Build: 2026-03-02-01)

### Fix
- Step 15 follow-up: ensured `BindAttribute` + `SceneAutoBinder` are present in `UiKit/GameUiKit`.
  - `CityShell` already referenced these, which caused compilation failures until the files were added.

---

## Step 19 — Migrate WorkshopView to attribute bindings

**Goal:** expand adoption of `[Bind]` + `SceneAutoBinder` to the largest menu screen with the most node bindings.

### Changes
- Migrated `Scripts/UI/WorkshopView.cs` to use `[Bind]` fields + `SceneAutoBinder.Apply(...)`.
  - Removed `EnsureBound()` and `SceneBinder` boilerplate.
  - Node paths preserved exactly; failures still produce high-signal binder errors.

### Behavior changes
- None intended.

### Why this matters
- `WorkshopView` is frequently edited as we add parts, mounts, and ammo flows; this reduces churn and the chance of broken node lookups during UI iteration.

---

## Step 20 — Migrate ArenaRealtimeView to attribute bindings

**Goal:** remove `EnsureBound()` boilerplate from the arena controller while keeping behavior exactly the same.

### Changes
- Updated `Scripts/UI/ArenaRealtimeView.cs`
  - Added `[Bind]` attributes for all UI nodes previously bound in `EnsureBound()`.
  - Replaced `EnsureBound()` with `SceneAutoBinder.Apply(...)`.
  - Preserved Vehicle HUD fallback behavior via a small `PostBindInit()` method.

### Behavior changes
- None intended.

### Why this matters
- `ArenaRealtimeView` is the largest/most frequently iterated realtime UI script; this reduces binding churn and keeps node lookup errors high-signal.


---

## Step 21 — Migrate core HUD scripts to attribute bindings

**Goal:** reduce UI binding boilerplate in reusable HUD controls and keep node lookup failures high-signal.

### Changes
- Migrated these HUD scripts to `[Bind]` + `SceneAutoBinder`:
  - `Scripts/UI/PlayerStatusHud.cs`
  - `Scripts/UI/TargetStatusHud.cs`
  - `Scripts/UI/RadarHud.cs`
  - `Scripts/UI/VehicleStatusHud.cs`
- Preserved existing soft/fallback behavior where needed:
  - `VehicleStatusHud` continues to fail soft (logs a binder error and stops updating if the scene layout is missing nodes).
  - Fallback node paths are now expressed via `BindAttribute.Fallback`.

### Behavior changes
- None intended.

### Why this matters
- HUD controls get iterated frequently while tuning layout and readability; moving them to attribute-driven binding reduces churn.
- Using `BindAttribute.Fallback` keeps the “don’t break if the HUD scene layout differs slightly” behavior during transitional layouts.


---

## Step 22 — Audit HUD helpers for early-notification safety

**Goal:** eliminate a common Godot lifecycle footgun where `NotificationResized` or parent scripts can run before `_Ready()`, causing missed initial HUD rendering or null dereferences.

### Changes
- Updated `Scripts/UI/ValueBar.cs`:
  - Caches the most recent `SetValues(...)` call when invoked before `_Ready()` and applies it after layout settles (via `CallDeferred`).
  - Recomputes fill offsets on `NotificationResized` using the last known percentage, keeping the bar visually correct after layout changes.
  - Existing guard remains: `NotificationResized` will not touch bound nodes before `_Ready()`.

### Behavior changes
- None intended (only affects edge cases where the bar was updated before it was ready or before layout finalized).

### Why this matters
- Prevents “empty bar until next update tick” issues when a HUD script fires one-time updates during instantiation.
- Adds additional safety around Godot’s early layout notifications without changing normal runtime behavior.


---

## Step 23 — Introduce GamePawnKit and migrate DriverPawn into HumanoidPawn

**Goal:** begin refactoring core game objects into a reusable (non-game-specific) toolkit project.

### Changes
- Added new shared gameplay library: `PawnKit/GamePawnKit.csproj`.
- Added reusable pawn abstractions:
  - `GamePawnKit.Pawns.IPawn`
  - `GamePawnKit.Pawns.IHumanoidPawn`
  - `GamePawnKit.Pawns.IVehiclePawn`
- Added `GamePawnKit.Pawns.HumanoidPawn` (renamed from the previous `DriverPawn` implementation).
- Updated `Scripts/Arena/DriverPawn.cs` to a backwards-compatible wrapper that inherits `HumanoidPawn` so existing scenes continue working.
- Added a small mapper from `DriverAvatarConfig` -> `HumanoidAvatarConfig`.

### Behavior changes
- None intended.

### Why this matters
- Establishes a clean place to move shared pawn/gameplay code without mixing it into Wasteland Survivor-specific systems.


---

## Step 24 — Extract reusable VehiclePawn movement core into GamePawnKit

**Goal:** begin separating vehicle movement/handling from Wasteland Survivor-specific systems so the pawn can be reused across games.

### Changes
- Added `PawnKit/Pawns/VehiclePawnBase.cs`:
  - Contains the shared kinematic-style vehicle movement step (`StepVehicleMovement`) and tuning parameters.
  - Implements `IVehiclePawn` / `IPawn` with minimal lifecycle events (additive; not used by current gameplay yet).
  - Provides virtual hooks (`UpdateRuntimeDerivedStats`, `ComputeMassAccelFactor`, `ResolveVehicleOverlap`) for game-specific logic.
- Updated `Scripts/Arena/VehiclePawn.cs`:
  - Now inherits `VehiclePawnBase`.
  - Moves the movement/handling math into the base via `StepVehicleMovement(dt)`.
  - Keeps all Wasteland Survivor-specific visuals, loadout, hitboxes, VFX, turrets, and audio behavior in `VehiclePawn`.

### Behavior changes
- None intended.

### Why this matters
- Creates a clean seam between “vehicle pawn physics” and “game-specific vehicle systems”, enabling reuse and easier iteration/testing.


---

## Step 24a — Build fix (restore fire cooldown surface)

**Goal:** restore missing `VehiclePawn` firing cooldown members that are part of the current Arena firing loop.

### Changes
- Restored these members on `Scripts/Arena/VehiclePawn.cs`:
  - `FireCooldownSeconds`
  - `FireCooldownRemaining`

### Behavior changes
- None intended.

### Why this matters
- `ArenaRealtimeView` uses these properties to coordinate weapon fire timing; dropping them breaks the build.


---

## Step 25 — Extract generic vehicle telemetry surface (IVehicleTelemetry)

**Goal:** make vehicle pawns easier to integrate with audio/UI/AI systems across games by exposing a tiny, reusable telemetry interface.

### Changes
- Added `PawnKit/Pawns/IVehicleTelemetry.cs` (generic vehicle telemetry surface).
- Implemented `IVehicleTelemetry` in `GamePawnKit.Pawns.VehiclePawnBase`:
  - `GetSpeedMps`, `GetForwardSpeedSignedMps`, `GetMaxSpeedMps`, `GetThrottle01`, `GetBrake01`
  - Added a protected helper `GetForwardSpeedSignedMpsInternal()` so subclasses can reuse the basis math without duplicating it.
- Updated `Scripts/Audio/IVehicleAudioTelemetry.cs` to inherit from `IVehicleTelemetry`.
- Removed duplicate telemetry methods from `Scripts/Arena/VehiclePawn.cs` (now provided by the base).
- Removed `[Obsolete]` from `DriverPawn` to avoid warning spam from Godot source-generated binding code.

### Behavior changes
- None intended.

### Why this matters
- Creates a stable, game-agnostic seam between pawns and systems like HUD, audio, and AI.
- Reduces duplication (vehicle speed/throttle/brake computations live in one place).


---

## Step 26 — Add possession + enter/exit interaction seams (GamePawnKit)

**Goal:** make pawns easier to integrate with controllers/UI/AI across games by standardizing a small possession surface and defining generic enter/exit interfaces.

### Changes
- Added `PawnKit/Pawns/IPossessablePawn.cs`:
  - `ControllerId` + `Possessed` / `Unpossessed` / `ControllerIdChanged` events.
  - Overload `SetPlayerControlled(bool enabled, int? controllerId)`.
- Updated `IHumanoidPawn` and `IVehiclePawn` to inherit from `IPossessablePawn`.
- Implemented `IPossessablePawn` in:
  - `GamePawnKit.Pawns.HumanoidPawn`
  - `GamePawnKit.Pawns.VehiclePawnBase`
- Added `PawnKit/Pawns/IEnterExit.cs` defining `IEnterable` and `IExitable` (not wired into gameplay yet).

### Behavior changes
- None intended.

### Why this matters
- Gives HUD/audio/AI a consistent way to detect who is controlling a pawn (and react to possession changes) without reaching into game-specific scripts.
- Establishes a clean interface for future enter/exit gameplay (on-foot utility) without forcing Wasteland Survivor-specific assumptions into the reusable kit.


---

## Step 27 — Wire enter/exit + possession seams into ArenaRealtimeView

**Goal:** use the new generic interfaces in live gameplay code while keeping behavior the same.

### Changes
- `GamePawnKit.Pawns.VehiclePawnBase` now implements `IEnterable` / `IExitable` with a minimal occupancy model.
  - No scene-tree manipulation in the kit; games keep control of spawn/visibility.
- `ArenaRealtimeView` now wires the existing E-to-exit / E-to-enter flow via these interfaces:
  - When the player "enters" the vehicle, the `DriverPawn` is stowed (hidden + collision disabled) instead of freed.
  - When exiting, the stowed pawn is reactivated and positioned next to the vehicle.
  - This keeps `Entered`/`Exited` events meaningful (the same pawn instance is used).
- `ArenaRealtimeView.SetPlayerControlledEntity` now drives `IPossessablePawn.SetPlayerControlled(true/false, controllerId)` for the active pawn (controllerId=0).

### Behavior changes
- None intended.

### Why this matters
- Makes future interactable systems simpler: vehicles can be treated as `IEnterable`/`IExitable` without special casing.
- Provides a clean hook for UI/audio/AI systems to react to player control transitions using kit-level interfaces/events.


---

## Step 28 — Interactables v1 (action prompt candidate selection)

**Goal:** make the in-world action prompt ready for multiple nearby interactables (enter vehicle, tires, loot, tow points) by selecting the best candidate by priority + distance.

### Changes
- Added `Scripts/UI/ActionPromptCandidate.cs` (lightweight candidate model: target/world-anchor, action text, key text, priority, distance).
- `Scripts/UI/ActionPromptOverlay.cs`:
  - Migrated to `[Bind]` + `SceneAutoBinder`.
  - Added `SetCandidates(...)` to choose the best candidate (priority desc, distance asc).
  - Added `ActiveTarget` + `ActiveTargetChanged` hook for optional highlight systems.
- `Scripts/UI/ArenaRealtimeView.cs`:
  - Now feeds the action prompt via candidate list (currently only the existing vehicle "Enter" hint).
  - Added disabled-by-default interactable highlight helper (`EnableInteractHighlight = false`).

### Behavior changes
- None intended.

### Why this matters
- Allows us to add new interactables without turning `ArenaRealtimeView` into a pile of one-off prompt conditions.
- Provides a generic selection policy (priority + distance) so "best" interactions win automatically.


---

## Step 29 — Hold-to-activate button (GameUiKit) + arena match-end dialog

**Goal:** add a reusable “hold to activate” button control to the shared UI kit and use it to replace the arena end-of-match hold prompt.

### Changes
- UiKit: added `HoldToActivateButton`:
  - Button-like panel with a progress fill behind the text.
  - Base background is white @ 0.1 alpha.
  - Hold can be driven by either mouse-down on the control or holding a configured shortcut key.
  - Releases reset progress immediately; activation fires once per hold.
- `ArenaRealtimeView`:
  - Replaced the post-match “hold G to exit” label prompt with a modal dialog:
    - Title: **Match Ended**
    - Body: `HoldToActivateButton` labeled **Continue (Hold G)**
  - Hold duration reuses the existing `ExitHoldSecondsRequired` constant.
  - Old input-driven hold is kept as a fallback if the modal service is unavailable.

### Behavior changes
- None intended (interaction surface only).

### Why this matters
- Centralizes a commonly needed interaction pattern (confirm / continue / destructive actions) into the reusable UI toolkit.
- Removes game-specific one-off hold logic from screens, making future dialogs/screens easier to build consistently.


---

## Step 29a — Fix: HoldToActivateButton signal name collision

**Goal:** fix a build error caused by a duplicate `Activated` member name.

### Changes
- `HoldToActivateButton`: removed a redundant C# event named `Activated` that conflicted with Godot's generated signal event wrapper for the `Activated` signal.

### Behavior changes
- None intended.


---

## Step 29b — Fix: HoldToActivateButton layout + modal host processing

**Goal:** fix the match-end dialog usability issues (button not advancing / text wrapping vertically) and ensure modals can be used during normal (unpaused) gameplay.

### Changes
- `HoldToActivateButton`:
  - Removed the `CenterContainer` layout that could collapse the label width and force per-character wrapping.
  - Label now fills the control and disables autowrap (clips instead of wrapping).
  - Improved mixed mouse+key release handling and shortcut detection (checks physical + keycode).
- `ModalHost`:
  - Runs in `ProcessMode.Always` and handles Escape-to-close via `_Input` so it works even when a Control has focus.
  - Modal content now runs in `ProcessMode.Always` so dialogs remain interactive both paused and unpaused.
- `ArenaRealtimeView`:
  - After opening the match-end modal, releases GUI focus so Escape continues to reach the global pause menu.

### Behavior changes
- Modals now work during normal gameplay (unpaused), not just while the scene tree is paused.

---

## Step 11 — Extract arena weapon-slot + targeting helpers and roll responsive layout into arena panels

**Goal:** keep moving combat rules out of `ArenaRealtimeView`, share loadout/targeting logic between player and AI paths, and extend the new desktop-safe layout helper to arena-specific menu panels.

### Changes
- Added `Scripts/Arena/ArenaWeaponLoadoutResolver.cs`
  - Resolves mounted weapons by slot using shared mount ordering rules
  - Resolves installed targeting computers
  - Resolves missile lock candidates + aim points
- Added `Scripts/Arena/ArenaEnemyFirePlanner.cs`
  - Centralizes AI fire-slot scoring so enemy weapon choice uses the same loadout resolution path as player weapons
- Updated `Scripts/UI/ArenaRealtimeView.cs`
  - Replaced embedded weapon-slot / missile-lock helper logic with calls into the new arena helpers
  - Removed duplicated ammo-consumption logic in favor of shared `AmmoMath`
- Updated `Scenes/UI/ArenaRealtimeView.tscn`
  - Added `ResponsivePanelLayout` helpers for the arena briefing and salvage/results panels

### Behavior changes
- None intended. This is primarily structural cleanup plus safer responsive panel sizing for more desktop resolutions.

### Why this matters
- Weapon/loadout rules are now easier to reuse when we split more AI/combat responsibilities out of `ArenaRealtimeView`.
- Player and AI combat paths now lean more on the same shared weapon-resolution logic.
- Arena menu panels now follow the same reusable viewport-aware layout pattern as other major menu screens.


---

## Step 12 — Shared JSON config store + config-backed system cleanup

**Goal:** stop duplicating JSON file I/O / caching / fallback logic across config-backed systems and create a reusable path for future tuning files.

### Changes
- Added `Scripts/Core/IO/JsonConfigStore.cs`
  - Shared cached JSON config loading base with reload support, default fallbacks, and consistent error logging.
- Updated these config-backed systems to use the shared store pattern instead of hand-rolled file loading:
  - `Scripts/Arena/DriverPawnConfigStore.cs`
  - `Scripts/Game/Systems/VehicleBuildCatalogStore.cs`
  - `Scripts/Arena/WeaponVisualFactory.cs` (`WeaponVisualConfigStore`)
  - `Scripts/UI/GameUiTheme.cs` (via an internal `UiThemeStore`)
- Added small normalization passes so partially-filled config files still produce non-null collections / nested objects.

### Behavior changes
- None intended. This is a structural cleanup only.

### Why this matters
- New config-backed systems now have a single pattern to copy instead of duplicating file existence checks, deserialization, caching, and fallback code.
- Reduces null-handling churn when expanding JSON configs for future vehicles, arena rosters, weapon visuals, and other runtime tuning.


## Step 13 — Shared vehicle control intents + config-driven arena AI

**Goal:** reduce direct player/AI control-field pokes, make arena AI easier to tune, and keep shrinking `ArenaRealtimeView` by moving controller logic into reusable helpers.

### Changes
- Added `GamePawnKit.Pawns.VehicleControlIntent` and `VehiclePawnBase.ApplyControlIntent(...)` / `ClearControlIntent()`
  - Player and AI vehicle control paths now share the same input payload surface instead of setting `ThrottleInput` / `SteerInput` in multiple places.
- Added `Scripts/Arena/ArenaVehicleAiDriver.cs`
  - Centralizes enemy driving heuristics (lead, steering, range keeping, unstuck recovery, fire-range thresholds).
- Added `Scripts/Arena/ArenaAiConfigStore.cs` + `Data/Config/arena_ai.json`
  - Arena AI behavior is now driven by a JSON-backed config store instead of hard-coded constants in `ArenaRealtimeView`.
- Updated `Scripts/UI/ArenaRealtimeView.cs`
  - Player vehicle input now builds a `VehicleControlIntent`
  - Enemy AI now delegates movement/tuning decisions to `ArenaVehicleAiDriver`
  - Stop/freeze paths use `ClearControlIntent()` for consistency

### Behavior changes
- None intended. Default AI behavior should feel the same, but the tuning values now live in config for easier iteration.

### Why this matters
- Makes future player/AI/replay/network vehicle-control work share a common surface.
- Removes another chunk of arena-specific decision logic from the giant UI controller.
- Opens a cleaner path to continue refactoring toward controller/brain abstractions without changing gameplay rules first.


---

## Step 14 — Shared full-screen art presentation helper

**Goal:** stop relying on per-scene texture-rect setup for splash / background PNGs and standardize the "fill screen without skew" behavior in reusable UiKit code.

### Changes
- Added `UiKit/Scripts/Framework/UI/FullscreenTextureRectUtil.cs`
  - Centralizes the full-screen keep-aspect-covered configuration for `TextureRect`-based splash / background art
- Updated `UiKit/Scripts/Framework/UI/OptionalBackgroundPresenter.cs`
  - Applies the shared cover configuration whenever optional menu background art is shown or cleared
- Updated `Scripts/UI/BootSplashView.cs` + `Scenes/UI/BootSplashView.tscn`
  - Startup splash art (`studio.png`, `title.png`) now uses the same shared cover behavior as other fullscreen menu backgrounds

### Behavior changes
- Startup splash and fullscreen menu background PNGs now fill the viewport while preserving aspect ratio, instead of depending on scene-specific stretch settings.

### Why this matters
- Future title cards / city art / menu backgrounds have one reusable configuration path instead of repeating `TextureRect` tuning in each screen.
- Reduces the chance of one screen skewing or letterboxing differently from the rest of the game.

