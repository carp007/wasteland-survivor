# Refactoring plan (proposal only)

This is a **proposal** based on a review of the current codebase.

Progress tracking lives in `Docs/REFRACTORING_PROGRESS.md`.

## Goals
1) Make it faster to add new features (weapons, hazards, vehicle parts, UI screens).
2) Reduce “giant controller” files by extracting reusable components.
3) Create components that can be reused in **future Godot/C# projects**, not just Wasteland Survivor.
4) Keep save/state clean and migration-friendly.

---

## Current hotspots (highest leverage)

### 1) `ArenaRealtimeView` is doing too much
It currently owns:
- encounter lifecycle (start/stop/resolve)
- input dispatch (vehicle vs driver)
- targeting selection
- weapon firing rules + ammo consumption
- runtime damage + VFX + SFX
- HUD binding and fallback binding

**Risk:** changes become fragile and hard to test.

### 2) `VehiclePawn` mixes multiple responsibilities
It currently includes:
- movement/handling
- visuals/import alignment
- hitboxes/section mapping
- mounts + muzzle logic
- engine audio telemetry
- blown-tire VFX hooks

### 3) Node lookup + binding patterns repeat
A lot of UI scripts do “GetNode/Path strings” and then have ad-hoc null/fallback logic.

### 4) “Runtime arena state” is not a first-class object
Combat runtime state lives as a big set of fields instead of a small “arena runtime model”.

---

## Proposed extraction modules (reusable building blocks)

### A) Reusable “game framework” modules (good for future projects)

1) **ViewStack / ScreenRouter**
- Push/pop UI views with transitions
- Centralize view instantiation and disposal
- Optional: pause/overlay management hooks
- Reusable in any menu-driven game/app

2) **SceneBinder**
- Typed binding helper that:
  - resolves nodes by path once
  - validates expected structure
  - provides a consistent error message with scene path + node path
- Greatly reduces “why is my HUD missing?” bugs

3) **InputContext**
- One place to map Godot inputs to game intents
- Supports layered contexts (Vehicle, Driver, Menu)
- Easier to rebind controls later

4) **RuntimeConfigStore**
- Small base class for JSON config files:
  - lazy load
  - cached + explicit reload
  - helpful validation errors
- Reusable for any “tuning” configs

5) **Telemetry + Audio layer**
- A simple `ITelemetrySource` pattern + helpers to map telemetry → audio layers
- You already have `IVehicleAudioTelemetry`; formalize it for reuse

---

### B) Arena-specific modules (but still reusable)

1) **EncounterRuntime (state machine)**
Extract a pure-ish object to own:
- match phases (PreFight, Combat, Salvage, Results)
- timers, post-match hold, etc.
- in-memory runtime state for player/enemy

`ArenaRealtimeView` becomes a thin adapter:
- reads inputs
- forwards to runtime object
- renders UI

2) **TargetingSystem**
- Tracks valid targets, selection rules, and cycling
- Exposes “current target” + events
- Reusable for other combat games

3) **WeaponSystem**
- Owns cooldown, ammo selection/consumption
- Owns “fire request” → “raycast/hit result”
- Emits events for VFX/SFX + damage application

4) **DamageSystem**
- Applies damage to section/tire/driver based on hit collider tags
- Keeps the logic out of UI and pawn code

5) **HazardSystem**
- Owns runtime hazards like mines
- Handles spawn, trigger detection, detonation, and cleanup

---

### C) VehiclePawn splitting plan

Proposed components:
- `VehicleController3D` (movement + handling only)
- `VehicleVisualRig` (proxy mesh vs imported mesh alignment)
- `VehicleHitboxRig` (section+tire colliders and mapping helpers)
- `VehicleMountRig` (mount markers + muzzle markers + turret yaw constraints)
- `VehicleAudioRig` (binds `IVehicleAudioTelemetry` to `VehicleEngineAudio`)

The VehiclePawn remains a shallow “composition root”.

---

## Suggested refactor phases (safe ordering)

### Phase 1: Pure extraction + zero behavior changes
- Introduce small classes that wrap existing code, leaving call sites intact
- Add unit-style tests where practical (pure systems only)
- Goal: “same behavior, smaller files”

### Phase 2: Normalize binding + conventions
- Add `SceneBinder` and convert a couple of UI scenes first (Arena view + VehicleStatusHud)
- Add minimal validation + console errors on bind failures

### Phase 3: Arena runtime model
- Extract `EncounterRuntime` and move runtime fields into a single object
- Add a small phase enum and explicit transitions
- Keep UI rendering separate

### Phase 4: VehiclePawn split
- Extract movement controller first (lowest risk)
- Extract mounts/hitboxes next
- Keep exported tuning fields on the pawn as the editor-facing surface

### Phase 5: Optional reuse packaging
- Move reusable modules into `Scripts/Framework/*` or a separate `.csproj` (if desired later)
- Keep it “copyable” at first (no NuGet required)

---

## Refactor guardrails
- No save format changes without a migration.
- Prefer small, reviewable steps (one extraction at a time).
- Keep Godot node tree expectations explicit (fail fast with clear logs).
- Avoid adding “global singletons” other than the existing `App` autoload.

