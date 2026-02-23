# Next Tasks (keep this small and current)

## Current focus
3D/2.5D foundation + keep the existing city/garage/workshop loop stable.

## Just completed
- Global **Console** overlay (tilde toggles visibility) with compact UI.
- Console can collapse into a true **one-line** mode (header hidden).
- Introduced **typed log lines**:
  - Debug (blue)
  - Status/Game (white)
  - Input/Command (gold)
  - Error (red)
- Boot/startup messages now log as **Debug**.
- Common game actions (repairs/upgrades/ammo/city/encounter) log as **Status**.
- Console history is **bounded** and trims in batches to avoid long-run performance issues.
- Added basic console commands: `help`, `clear`, `version`.
- Console mouse interaction fixed (buttons/scrollbar/input clickable).

- Arena now uses a **3D world with a fixed top-down/RTS camera** (2.5D):
  - `Scenes/UI/ArenaRealtimeView.tscn` instantiates `Scenes/Arena/ArenaWorld.tscn` into `WorldRoot`.
  - `Scripts/Arena/*` contains the canonical arena world + vehicle pawn.
- Removed legacy **2D arena** content and removed `3D` suffixes from arena folder/class/namespace names.
- Fixed arena start reliability by making `ArenaWorld` safe to interact with immediately after instantiation.
- Fixed arena rendering by forcing the arena camera current and explicitly enabling processing for the follow rig and vehicle pawns.
- Added step-by-step arena start debug logging + exception reporting to the Console.
- Improved arena Start diagnostics (full stack trace logging) to pinpoint the current exception.
- Fixed a build break in `ArenaRealtimeView` caused by escaped quotes inside an interpolated string expression.
- Fixed a build break in `ArenaRealtimeView` caused by mistakenly escaped quotes in the enemy loadout fallback (Build: 2026-02-22-19).
- Fixed the current Arena Start NRE by adding defensive scene loading + robust scene root type checks.

- Fixed arena **scene parse errors** (Godot could not load `ArenaWorld.tscn` / `VehiclePawn.tscn`):
  - Replaced both scenes with **minimal** (Godot-4-compatible) `.tscn` files.
  - Arena floor/walls are now generated procedurally by `ArenaWorld`.
  - Vehicle collision/mesh are now generated procedurally by `VehiclePawn`.

- Fixed **steering inversion** (A/D) in the 3D arena vehicle pawn.
- Added minimal, code-only **firing VFX** (tracer + muzzle flash + hit flash) visible from the fixed top-down camera.
- Fixed arena firing correctness:
  - Tracer now renders as a true 3D line from muzzle → impact.
  - Shots fire forward (no auto-aim) and only apply damage when the ray hits the intended pawn.
- Arena Start now **resumes** a previously active encounter (after restart) instead of blocking with "An encounter is already active".
- Leaving the arena while combat is live now **forces a flee resolution** to prevent the save from being stuck with an active encounter.

- Refactored **GameSession** from many partials into a clean, object-oriented foundation:
  - `GameSession` is now a thin facade.
  - `SessionContext` owns and persists `SaveGameState`.
  - Focused services: `SessionWorld`, `SessionGarage`, `SessionEncounters`.

- Fixed a compile error introduced during the refactor (`SessionGarage` missing `DefDatabase` namespace import).

- Added player/driver **Armor Points (AP)** as an extra HP buffer:
  - AP absorbs hits first; once depleted, damage reduces HP.
  - Added post-encounter **Repair Armor** action (money, not scrap).
- Added a new top-right **PlayerStatusHud** showing **HP + AP** side-by-side with percent-based colors.

- Refined combat foundation toward the north-star damage model:
  - Added real **driver HP** (50 by default) + an equipped armor slot (Basic Kevlar).
  - Vehicles now track **section + tire HP/AP** (no single vehicle HP pool).
  - Added a top-right **VehicleStatusHud** showing section/tire status around a vehicle image placeholder.
  - Implemented **positional hit mapping** via VehiclePawn hitboxes (sections/tires/driver).

- UI polish pass on the new HUD:
  - Compact PlayerStatusHud (smaller bars, vertically centered).
  - VehicleStatusHud tightened width, centered labels, improved spacing.
  - Armor bars repositioned (Front/Rear: armor on top; Left/Right: armor on left).
  - Slightly darker green/blue palette and smaller in-bar font.
  - Rotated in-bar text for vertical bars.

- Fixed HUD regressions introduced during the polish pass:
  - Corrected `PlayerStatusHud.tscn` node parenting (prevents arena-start exceptions).
  - Restored live bar values (no more 0/0 placeholders during combat).
  - Front/Rear vehicle bars are now smaller and centered.

- Added a dedicated **TargetStatusHud** (upper-left):
  - Shows `Target: <name>` and the **enemy driver HP/AP** bars.
  - Arena HUD panel moved down to sit below TargetStatusHud.
  - Removed speed/enemy HP from the Arena HUD text.

- VehicleStatusHud improvements:
  - Reduced overall width; vehicle placeholder image is now square/taller.
  - Tires show **AP above HP**.
  - Added **Weapons list** (mounted weapons + ammo counts).
  - Added **Speed bar** (gold) under the weapons list.

- HUD space-saving polish (Build 2026-02-22-08):
  - Removed Front/Rear/Left/Right labels from VehicleStatusHud.
  - Reduced header/label font sizes (Vehicle/Top/Under/Tires/Weapons/Speed and tire position labels).
  - Centered the mid row spacing so left/right bars sit more naturally.
  - Reduced HP/AP label font size in PlayerStatusHud and TargetStatusHud.

- HUD/UI polish (Build 2026-02-22-09):
  - TargetStatusHud is now a **single-line** target summary (no stacked bars).
  - PlayerStatusHud is narrower (~20%) with tighter padding.
  - VehicleStatusHud left/right section bars are centered more symmetrically.
  - Weapons list readability improved (padding + spacing + slightly smaller font).

- HUD/Overlay polish (Build 2026-02-22-10):
  - TargetStatusHud one-line layout restored **HP/AP bars** with values inside.
  - ConsoleOverlay is now **60% screen width** (bottom-center).
  - Removed DebugOverlay completely.
  - VehicleStatusHud is centered under PlayerStatusHud with a small **top-down vehicle preview** in the center panel.

- HUD/Overlay tweaks (Build 2026-02-22-11):
  - TargetStatusHud: removed bracket labels around HP/AP bars.
  - ConsoleOverlay: moved to bottom-left (still 60% width).
  - Arena HUD stats line: removed ammo count (Tier only).

- Startup display (Build 2026-02-22-12):
  - Game now forces **fullscreen** window mode at startup.

- Startup display (Build 2026-02-22-13):
  - Fullscreen startup now also forces the **root viewport** to render at **native fullscreen resolution**.

- Startup display (Build 2026-02-22-14):
  - Only applies native fullscreen content scaling when fullscreen is actually achieved (prevents letterboxing/shrinking when running embedded in the editor).

- City + display convenience (Build 2026-02-22-15):
  - Added **Exit** button to the City shell (main menu).
  - Added **F11** fullscreen/windowed toggle (keeps safe scaling behavior when fullscreen can't apply).

- Targeting + weapon mounting foundation (Build 2026-02-22-16):
  - Added a simple **3D target indicator** (glowing ring + arrow) that follows the selected target.
  - VehiclePawn now builds a slightly more car-like **proxy 3D model** (still box-based).
  - Implemented **weapon mount points** driven by vehicle defs (fixed mounts + turret mounts).
  - Mounted weapons now render as **3D proxy boxes** attached to mount points (collision disabled).
  - Turret mounts yaw to face `AimWorldPosition`; firing now uses the **weapon muzzle direction**.
  - Compact default vehicle mount `R1` moved from **Rear → Top (Turret, 360°)**.

- Target indicator hotfix (Build 2026-02-22-17):
  - Fixed compile errors in `TargetIndicator3D` by using a **procedural ring mesh** (ImmediateMesh triangles) and a simple marker mesh.

- Arena fixes (Build 2026-02-22-18):
  - Enemy vehicles now spawn with a **default weapon loadout** (matches player mount ids when possible; otherwise starter MG + Missile).
  - Fixed proxy **tire orientation** and added **front wheel steering visuals** based on actual turn rate.
  - Camera pulled back (~30% higher/further).
  - Added a small **vehicle overlap resolution** fallback to prevent clipping.

- Salvage merge + combat fix (Build 2026-02-23-10):
  - Restored driving realism + mass-based handling + tire VFX.
  - Hit marker + hit/miss SFX restored; tire-pop SFX on tire destruction.
  - Shots fired straight from raised mounts now reliably register via extended hitboxes + explicit raycast collision mask.

## Next small steps
1) Land mines (Mine Dropper weapon):
   - Drop a mine behind the vehicle; detonate on vehicle contact (especially tires).
   - Keep it simple (no true bullet physics); use hitboxes/overlaps for detection.
2) Weapon visuals (import-ready):
   - Add an optional `VisualScenePath` to weapon defs so we can swap proxy boxes for real imported models.
   - Standardize nodes inside weapon scenes: `MountPoint` + `Muzzle`.
3) Vehicle model integration:
   - Add an optional `VehicleScenePath` to vehicle defs and pull mount markers from the imported scene.
4) Garage adjustments:
   - Support per-mount **yaw limits/offsets** and a simple UI slider for turret aiming range.
5) Make vehicle damage matter:
   - reduce speed/handling when a tire HP reaches 0
   - optional: disable firing when specific sections are destroyed
6) Replace the temporary "chip-through" driver damage with a clearer rule (e.g., driver damage only on Driver hitbox or after section HP reaches 0).
7) Start the **weight / capacity** foundation:
   - add engine objects + compute max capacity
   - show a weight bar in VehicleStatusHud

8) Audio foundation (asset sourcing):
   - Use `Docs/Audio/AUDIO_CHECKLIST.md` as the canonical list.
   - Start by sourcing 3 engine archetypes (V8 muscle, I4 compact, diesel truck) with idle/low/mid/high loops.
   - Keep licensing clean by tracking every source in `Docs/Audio/ATTRIBUTION_AND_LICENSES.md`.
