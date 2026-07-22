# Assistant Playbook (what works / what doesn’t)

This doc exists to prevent repeating workflow dead-ends across ChatGPT threads.

## Visual iteration (USE THIS — it changed everything)
- **Never do visual work blind.** A screenshot harness exists: `Scripts/App/ScreenshotHarness.cs`. Run
  `"C:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe" --path <project> --windowed -- --shot=<city|garage|workshop|arena|pause> --shot-dir=<abs dir> [--shot-tier=N]`
  and read the PNGs back. Arena mode starts a real match, auto-drives at the enemy (with wedge recovery), holds fire, and captures an engagement sequence.
- Screenshot runs use a throwaway sandbox save (`user://savegame.screenshot.json`, deleted each run) with a granted starter vehicle. NEVER let harness runs touch the real `user://savegame.json` — an early version of the harness drained the player's missile ammo and persisted a synthetic encounter.
- If running from CLI shows "Cannot instantiate C# script because the associated class could not be found" (e.g., `ResponsivePanelLayout`), the Godot-side assemblies are stale: run `godot --headless --path <project> --build-solutions --quit` to resync, and `--import --quit` after adding new asset files (e.g., fonts) so `.import` metadata exists for game runs.
- The user often has the Godot editor open while we work; after a session of CLI dotnet builds they should let the editor rebuild (or restart it) before playing.
- When a visual element "renders wrong," crop the full-res PNG (System.Drawing via PowerShell) before theorizing — two "bugs" this session (standing barriers, black walls) were camera foreshortening + unlit south faces, not geometry errors.

## Economy + travel extension points (2026-06-11, builds 121-122)
- Parts economy: `SessionStore` (buy/sell/consume/return via `PlayerProfileState.PartsInventory`), prices via `PriceUsd` on weapon/engine/computer defs (0 = not sold). The Workshop's dropdowns FILTER to owned+installed and its Apply does a multiset diff (consume on install, return on uninstall). New purchasable part types should follow the same pattern.
- Travel: `SessionWorld.TryTravelTo` (cost + ambush roll) → CityShell shows a ROAD AMBUSH modal and rolls into the realtime combat flow via `TryStartArenaEncounter(ambushTier)`. A future dedicated "roadside" world variant should branch inside ArenaWorld rather than forking the combat view.
- Arena tiers are data: `ArenaEnemyBuilds` in vehicle_builds.json + briefing tier cards (1-3 scene-authored, 4-5 runtime-added in `EnsureExtraTierCards`); `SessionEncounters.TryStartArenaEncounter` accepts 1..5. Purses scale ×tier in `EncounterRewardGenerator`.
- Music: `MusicDirector` under AppRoot (city/combat via static `CombatActive`), Music bus trim -8dB in AudioBusUtil + matching constant in GameSettingsStore.

## Hard-won facts from the 50-iteration loop (2026-06-11, builds 118-120)
- **Camera zoom is sacred:** the user explicitly wants the high RTS / "toy cars" framing (FollowCameraRig offset 0,29,23). Never pull the camera closer for a third-person feel; improve readability by scaling VFX/markers up instead.
- **Car pack variant materials:** the VISIBLE meshes use bare material names ("Body", "Glass", "Wheel"); only hidden variant meshes carry "_N" suffixes. Any material-swap regex must match both or paint changes silently no-op. Also never gate material walks on `IsVisibleInTree()` during off-tree configuration — use `Visible`.
- **No box meshes for airborne VFX:** flat-lit cubes (smoke puffs, muzzle flashes) read as bright white CARDS stuck to vehicles from the top-down camera. Use low-segment spheres.
- **Driver chip-through rule (spec):** drivers are injured only by direct driver hits or damage overflowing through a DESTROYED section. A per-hit chip applied through intact armor kills drivers in seconds once weapons have automatic cadence (this exact bug shipped briefly with the 150ms .50cal).
- **Weapon cadence drives feel:** MGs ~150ms automatic; missiles slow heavy salvos (2200ms/30dmg).
- The screenshot harness pages drawer tabs, runs splash captures (`--shot=splash`), auto-drives the arena with wedge recovery, and drops mines mid-fight. LOOP_STATE.md pattern (in .shots/) worked well for tracking long improvement loops.

## Hard-won arena facts (2026-06-11 overhaul)
- The fog-of-war "can't see the arena" bug was the 40m blackout curtains occluding the south chase camera, NOT the fog plane. Out-of-bounds darkness now comes from the arena WorldEnvironment background color. Do not reintroduce tall occluder geometry between the south start lane and the bowl — the camera sits ~13m behind the pawn.
- Keep the north-south spawn axis clear of cover. A barrier line across the jousting lane makes AI and player ram it nose-first every match.
- Obstacle props live in `Assets/Models/ArenaObstacles` (NOT `Assets/Models/Arena/Obstacles`). `TryAttachPackedVisual` supports `fitLongestSideMeters` + `groundLocalY` to normalize arbitrary-scale gltf imports; use it for any new prop.
- Camera framing: `FollowCameraRig` offset (0, 16.5, 13) with combat lookahead toward the lock and `AddShake` for impacts. The old (0, 29, 23) rendered cars ~24px tall.
- Weapon cadence drives combat feel: MGs ~150ms automatic cadence, missiles slow heavy salvos. The pre-overhaul 1Hz .50cal + 0.9s/54dmg lock-on missiles made fights end in seconds with nothing visible.

## UI direction (2026-06-11 foundation)
- Bundled OFL fonts: Rajdhani (display: titles/buttons/tabs) + Inter (body) under `Resources/UI/Fonts/`, wired through `GameUiTheme` (`DefaultFont`, per-type fonts, `StyleHeading`). Run the editor import after adding font files.
- Design language: flat, dark, near-opaque "command console" panels; hairline white borders (alpha ~0.07-0.14); accent colors reserved for selection/CTA/hover states; sections separated by tone + a 3px left accent bar, not glowing borders. Avoid reintroducing translucent frosted panels with bright cyan borders on everything.
- The generated banner art (`city_banner`, `workshop_banner`, `garage_circuit_header`) is intentionally hidden — display-font headings carry the headers. Don't re-enable without real art.

## Baseline access (canonical)
- **Source of truth:** the latest uploaded `wasteland-survivor.zip`. Always extract that zip first and work from the extracted project contents unless the user explicitly says otherwise.
- Deliverables should remain drop-in zips that exclude `Assets/`, `.godot/`, `.git/`, and generated build/IDE folders.

### What *does* work reliably
- Editing files in place directly via API tools.
- Before shipping a zip deliverable, verify that critical source trees are still present in the packaged workspace: `Scripts/Core`, `Scripts/Game/Navigation`, `Scripts/Game/Session`, `Scripts/Game/Systems`, `UiKit/Scripts`, `PawnKit/Pawns`, and required `Data/Defs` JSON files. A packaging regression that drops those folders will compile-break the whole solution.

## Engineering guardrails
- Always favor proper OOP design and SOLID principles when writing or refactoring code. Keep presentation logic, state mutation, and rendering helpers separated instead of letting large UI classes absorb everything.

## Versioning / release notes
Whenever completing a significant change:
- Bump `VERSION.txt` build id
- Add a short entry to `CHANGELOG.md`
- Update workflow/docs when process changes

## Visual polish notes
- If the user asks for the menus to feel more like a side panel/drawer, move the shell to the left edge and reflow the interior into a denser vertical stack before inventing more scaling tricks. Only add tabs/sub-pages once the drawer still feels overloaded after that first compaction pass.
- Premium menu screens now follow the **HTML-first hybrid** direction documented in `Docs/UI/HTML_FIRST_HYBRID_DIRECTION.md`: use HTML/CSS mockups as the visual-authoring/reference layer, then distill them into shared Godot runtime components/styles. Do not jump straight to an embedded browser for core screens unless there is a concrete packaging/runtime reason to do so.
- Once the HUD preview is actually visible, keep improving fidelity on the scene-owned `VehiclePreviewCanvas` itself (class silhouette, mount markers, damage/tire overlays, tow cues) instead of reopening the old viewport/camera path.
- If the `VehicleStatusHud` preview keeps failing or going blank after repeated viewport/camera fixes, stop iterating on `SubViewport` wiring and use a deterministic in-HUD top-down render path instead. For gameplay purposes the preview only needs to read as the active player vehicle, not be a fully live 3D model feed.
- If the deterministic in-HUD preview still appears blank, assume the issue is not the camera math at all: draw directly into the exact scene-owned `VehiclePreviewHost` control instead of spawning a runtime child control under it. The runtime child path can leave you updating a different control than the one that is actually visible on-screen.
- Menu-style screens should prefer `GameUiKit.UI.OptionalBackgroundPresenter` for optional art under `Assets/Images/...` and `GameUiKit.UI.ResponsivePanelLayout` for viewport-aware panel sizing instead of repeating bespoke background/layout logic per view.
- Premium management screens should not fill the whole monitor by default. The current direction is a **left-edge drawer** that stays under about half-screen width so the background scene remains visible; prefer changing the responsive bounds and content flow over trying to globally scale every child control.
- If the management screens still look centered after a drawer-layout pass, stop relying on detached scene helper nodes for shell placement. Drive the drawer rect directly from the owning screen script (`CityShell`, `GarageView`, `WorkshopView`) and keep the scene-authored panel offsets on the left as a fallback baseline.
- Once a drawer is structurally correct, prefer tabs/sub-menus for overload relief (`Garage`: Fleet vs Service Bay, `Workshop`: Overview vs Hardpoints vs Ammo) instead of forcing every section into one tall scroll stack.
- City Hub needed the same treatment once the shell moved left: the command-first default should keep only current city, active vehicle context, and major services visible, while travel and save/exit live behind their own tabs. Compact route rows worked better than oversized destination cards for this drawer.
- When moving new vehicle-summary text into UI screens, keep the shared helper namespace import (`using WastelandSurvivor.Game.Systems;`) with the screen. The drawer refactor added Workshop summary calls to `VehiclePresentation` / `VehicleRecoveryValueMath`, and dropping that import causes a clean compile failure in the main game project even though the scene/layout changes themselves are fine.
- Garage service flow is clearer when **Vehicle Details**, **Service Bay**, and **Upgrades** are separate views. Do not keep selected-vehicle inspection, repairs, and permanent upgrades in one tall service tab just because the shell has spare height.
- `ResponsivePanelLayout.VisualScale` should shrink the shell through the calculated layout bounds, not by leaving the bounds large and only scaling the target Control transform. On these premium menu screens the transform-only approach was not producing a visible size change, while layout-bounds scaling reliably reveals more background art.
- `ResponsivePanelLayout` should size premium menu shells from the viewport's visible rect, not `DisplayServer.WindowGetSize()` unless the viewport size is unavailable. On Windows/high-DPI setups the display-server size can reflect physical pixels, which makes the shell read much larger than intended and can wipe out a requested 70% scale-down.
- If a user says the premium shell is “smaller but not scaled,” stop spending passes on Control-transform tricks and use the win to tighten the internal layout instead: reduce empty-card expansion, compact repeated stat tiles/previews, and let important actions/telemetry consume the saved space. The user cares more about efficient, polished use of the floating shell than about mathematically scaling every child by the same ratio.
- For desktop menu screens that can grow taller than the available viewport, keep the title / summary / primary actions anchored and wrap the variable-height body in a `ScrollContainer`. Also make sure `ResponsivePanelLayout` never forces its target larger than the actual viewport; shrinking below the authored minimum is better than clipping the whole panel off-screen.
- Godot's docs recommend anchors for basic multi-resolution placement and Containers for complex inner layouts. For menu screens, use anchors/offsets to place the outer `PanelContainer` against the viewport (or stretch it toward the bottom margin), then let nested `VBoxContainer`/`GridContainer`/`ScrollContainer` nodes handle the internal layout. Do not try to manually position children that live under a `Container`, because the container will overwrite that work on resize.
- `icon_max_width` is a Button theme constant, not a normal property. Use `AddThemeConstantOverride("icon_max_width", value)` (and `h_separation` as needed), and pre-size oversized generated SVG icons before assignment when you need predictable in-button icon sizing.
- Full-screen PNG art (menu backgrounds, splash/title/studio images, similar future screens) should also run through `GameUiKit.UI.FullscreenTextureRectUtil.ConfigureCover(...)` so the image fills the viewport while preserving aspect ratio instead of skewing.
- Arena combat weapon-slot and missile-lock rules should prefer the shared helpers in `Scripts/Arena/ArenaWeaponLoadoutResolver.cs` and `Scripts/Arena/ArenaEnemyFirePlanner.cs` instead of re-embedding mount-ordering / targeting-computer logic in UI controllers.
- Default starter/enemy vehicle loadouts now live in `Data/Config/vehicle_builds.json` and should be consumed through `VehicleBuildFactory` rather than hard-coding new builds directly in UI/session code.
- Prefer `Scripts/Core/IO/JsonConfigStore.cs` for new JSON-backed tuning/config files instead of hand-rolling file existence checks, deserialization, caching, and fallback logic in each feature.
- Arena target-selection state now lives in `Scripts/Arena/ArenaTargetingController.cs`; future enemy-count growth should extend that helper rather than reintroducing selected-target / cycle-index bookkeeping directly inside `ArenaRealtimeView`.
- Arena post-match hold-to-continue flow now lives in `Scripts/Arena/ArenaPostMatchFlow.cs`, and the rewards/repair button-state builder lives in `Scripts/Arena/ArenaPostEncounterPresenter.cs`; future arena-results work should extend those helpers instead of re-expanding `ArenaRealtimeView` with modal lifecycle / reward-panel bookkeeping.
- Garage fleet-management salvage now lives behind `SessionGarage` plus `VehicleRecoveryValueMath`; future sell/strip/claim work should extend those helpers instead of embedding vehicle-removal/value heuristics directly in `GarageView`.
- Garage list previews should prefer the deterministic `VehiclePreviewCanvas` + `VehiclePreviewTextureFactory` path for frozen fleet-entry icons instead of trying to embed live 3D viewport feeds into menu lists.
- Garage mockup-derived authored art now lives under `Resources/UI/Generated/` and should be accessed through `GarageArtCatalog` rather than hard-coding texture paths inside the view/controller. Keep authored hero/list art optional and retain the deterministic preview fallback for unsupported vehicle classes.
- Arena shot hit parsing now lives in `Scripts/Arena/ArenaRaycastUtil.cs`, and shared direct/explosive vehicle damage bookkeeping now lives in `Scripts/Arena/ArenaDamageResolver.cs`; future combat features should extend those helpers instead of re-embedding raycast hitbox parsing, tire-pop detection, or chip-through math in `ArenaRealtimeView`.
- Arena direct-hit damage should not fall back to `ApplyDamageToVehicle(...)` when the ray clearly struck a vehicle body collider. If a shot reaches a `VehiclePawn` but misses dedicated section hitboxes, infer front/rear/left/right/top/undercarriage from the local-space impact point so rear and side hits do not silently become front damage.
- Boot splash config now goes through `BootSplashConfigStore` (`JsonConfigStore`); future splash-sequence tuning should extend that store/config model instead of hand-parsing `boot_splash.json` in the view.
- Vehicle control code that can come from either player or AI should prefer `GamePawnKit.Pawns.VehicleControlIntent` plus `VehiclePawnBase.ApplyControlIntent(...)` / `ClearControlIntent()` rather than directly poking `ThrottleInput` / `SteerInput` in multiple controllers.
- Arena enemy driving / firing tuning now lives in `Data/Config/arena_ai.json` via `ArenaAiConfigStore`; future AI behavior passes should extend that config before adding more hard-coded magic numbers to `ArenaRealtimeView`.
- ArenaWorld interior cover should now prefer the optional asset-backed obstacle staging folder `Assets/Models/Arena/Obstacles/`. Concrete barriers are the primary structural cover pieces, while tires / trash cans / wrecked cars are secondary props. Keep primitive/texture fallbacks in code so the arena still builds when imported `.blend` scenes are unavailable on the local machine.
- Arena obstacle props that can be rotated in layout (barriers, wrecks, tire rows, trash cans) should rotate their collision bodies too. Leaving only the visual rotated creates oversized axis-aligned hitboxes that feel unfair and also confuses the feeler-based AI.
- Arena enemy navigation now has a lightweight feeler-based obstacle avoidance / unstuck layer in `ArenaVehicleAiDriver`. Future AI passes should extend that shared helper instead of pushing obstacle-recovery logic back into `ArenaRealtimeView`.
- For projectile VFX, prefer reusable helper entry points (`ArenaVfx.SpawnProjectileTrail`, `ArenaVfx.SpawnProjectileImpact`) plus small code-only effect nodes under `Scripts/Arena/`. That keeps new ammo types from hard-coding one-off missile-only visuals and avoids asset-path dependencies in deliverable zips.
- When extracting arena/shared helpers that accept `DefDatabase`, remember `DefDatabase` lives in `WastelandSurvivor.Core.IO`, not `Core.Defs`. Missing that import will compile-break only the main game project after the helper compiles conceptually fine.
- For top-down / RTS-camera surfaces, subtle PBR-only texture swaps can disappear at gameplay height. When improving ground materials, prefer adding larger-scale breakup (grime/oil/patch variation or macro masks) instead of relying only on fine normal-map detail.
- `VehicleStatusHud` preview `SubViewport`s should keep their own `World3D` instead of sharing the live arena world; otherwise the preview can leak the main arena camera/world feed and show the wrong subject/angle.
- For the `VehicleStatusHud` top-down preview, do **not** try to keep the car forward-facing by yawing the camera. Keep the camera fixed straight-down in the isolated preview world and rotate a preview pivot opposite the live vehicle yaw; camera-yaw Euler rotations were causing incorrect horizon/arena-looking shots in the HUD.
- If `VehicleStatusHud` preview changes appear to do nothing in-game, verify `ArenaRealtimeView` actually resolved the typed `VehicleStatusHud` instance. The plain-control fallback keeps the bars working but does **not** run the preview-camera logic, which can make the HUD look stuck on the arena feed even after preview code changes.
- If the HUD preview still looks like the live arena/world feed, rebuild the preview `SubViewport` tree from code instead of trusting a stale scene-authored viewport, and pass the live player vehicle directly from `ArenaRealtimeView` into `VehicleStatusHud` so the preview does not depend only on group lookups.
- If the `VehicleStatusHud` preview still appears unchanged after camera-code edits, assume the bug is a **stale scene-authored preview viewport** rather than bad camera math. Keep the scene preview slot as a plain host panel/control and create the one real `SubViewportContainer` + `SubViewport` from code under that host; otherwise Godot can keep rendering the baked viewport feed while the runtime code mutates a different path.
- If the HUD preview switches from the wrong arena feed to a **blank/black** preview, do **not** go back to camera-angle tweaks. Treat that as the next root cause: the preview clone path is likely dropping imported mesh node types. Duplicate the live `Visual` subtree directly, strip cameras/lights/collision/audio/runtime-only nodes from the duplicate, and fit the resulting clone to the preview viewport using computed bounds.
- If the HUD preview still goes blank after the runtime viewport is wired correctly, stop duplicating/sanitizing the live `Visual` subtree. Use the runtime-built `SubViewport` with the **live arena `World3D`** and drive a dedicated orthographic follow camera directly above the player vehicle; that path avoids imported-model clone/render edge cases and shows the real vehicle.
- Arena floor polish should go through the same layered shader path even when the user has external asphalt assets installed; fallback-only improvements can be invisible on the user's machine if the external pack is still taking priority.
- Avoid large axis-aligned rectangular patch masks/decals for the arena floor. From the RTS camera they read like kitchen tile/slabs. Prefer anti-tiling macro blending and sparse organic crack/patch/oil overlays instead.
- For large ground storytelling on the arena floor, prefer one UV-mapped full-floor overlay texture (or world-space projected overlay) over multiple large transparent PlaneMesh decals. The latter makes their rectangular bounds obvious from the RTS camera even when the texture itself is organic.

## If assets are required
If a change requires new/changed files under `Assets/`:
- List the required paths explicitly in the response, and clearly state what the user needs to add or download if they are not generatable.
- If a visual improvement can be shipped as generated project content instead, prefer placing it outside `Assets/` (for example under `Generated/`) so it survives AI deliverable zips.

## Audio work
- Canonical audio checklist: `Docs/Audio/AUDIO_CHECKLIST.md`
- Licensing manifest: `Docs/Audio/ATTRIBUTION_AND_LICENSES.md`

## Godot 4 quirks (avoid repeat bugs)
- When refactoring JSON-backed config helpers, avoid reusing the name `ConfigPath` for enclosing-class constants when the store inherits `JsonConfigStore`. Qualify the outer constant (or give the store its own constant) or the compiler may bind to the base instance property instead. Likewise, if a store is moved outside its original containing class, bring any path constants with it so the constructor still compiles.
- **Autoload service registry lifecycle:** The `App` autoload owns `App.Services`. If any code replaces the
  `GameServices` instance at runtime (e.g., `Services = new GameServices()` during boot), it will silently
  discard UI service registrations added by `AppRoot` (router/modals/navigation) and break menu navigation.
  Always keep the same `GameServices` instance and only add/overwrite individual registrations.
- **Calling engine methods by string** (`CallDeferred`, `Call`, UndoRedo, etc.): use the engine's **snake_case** name (e.g., `"move_to_front"`), not the C# PascalCase wrapper.
- **Exported `NodePath` defaults**: avoid `NodePath.Empty` in exported property initializers. In this Godot C# surface it is not available and can break both the script compile and the generated default-value source. Prefer `new("")` for an empty exported path default.
- **Spatial shader world position**: Godot 4 spatial shaders do **not** provide `WORLD_POSITION`. Reconstruct fragment world position from view-space `VERTEX` using the built-in `INV_VIEW_MATRIX` (preferred) to avoid needing custom uniforms.
- **.tscn Node3D transforms**: when adding simple “anchor” helpers in scenes, prefer `position = Vector3(x, y, z)` (and `rotation` / `scale` if needed) over serializing a full `Transform3D(...)`. In some cases the full transform serialization can trigger a parse error on load.


- `ModalHost` centers whatever root control you pass to it. If a specific modal needs non-centered placement (for example a post-match prompt around 25% from the top), wrap the dialog in a fullscreen `Control` and position the dialog manually inside that wrapper before calling `IModalService.Show(...)`.

- If a custom scripted UI control is critical for flow (for example the arena `BtnStart` hold-to-activate control), prefer binding the scene node as a generic `Control` plus a typed runtime cast/replacement step instead of hard-failing the whole screen load on a type mismatch. This keeps the menu usable when a stale script resource temporarily loads as its base Godot type.
- Arena briefing background art is expected at `res://Assets/Images/Arena/Arena1.png` on the user machine. Because `Assets/` is excluded from AI deliverable zips, code should treat that texture as optional and fall back cleanly if it is absent.
- Garage background art is expected at `res://Assets/Images/Garage/Garage1.png`, and Workshop background art is expected at `res://Assets/Images/Workshop/Workshop1.png`. Like the arena background, both must remain optional because `Assets/` is excluded from AI deliverable zips.

## Multi-project setup (UiKit)
- The solution now includes a shared UI/dialog toolkit project: `UiKit/GameUiKit.csproj`.
- The main game project references it via `<ProjectReference>` in `Wasteland Survivor.csproj`.
- **Important:** the main csproj explicitly excludes `UiKit/**` from compilation to avoid duplicate type definitions.
  If you add new files to UiKit, make sure they live under `UiKit/` and that `UiKit/**` is still excluded from the main project.

### Preferred UI binding style
- For new UI scripts, prefer attribute-driven bindings:
  - Mark fields with `GameUiKit.SceneBinding.[Bind("Path/To/Node")]`
  - Call `SceneAutoBinder.Apply(this, nameof(MyView))` in `_Ready()`
- This keeps UI scripts small and ensures binding errors remain high-signal via `SceneBinder`.

Optional convenience:
- `GameUiKit.SceneBinding.AutoBoundControl` / `AutoBoundNode` automatically run `SceneAutoBinder` in `_Ready()`.
  - If a screen inherits one of these, make sure to call `base._Ready()`.

- If the HUD preview is still blank after the runtime viewport itself is definitely the one on screen, stop sharing the live arena world. Use an isolated preview-only `VehiclePawn` proxy inside the HUD `SubViewport`, copy the live vehicle's visual settings before the proxy enters the tree, then sync the preview pivot against the live vehicle yaw. That path avoids both stale-feed bugs and the imported-mesh clone failures.

- Vehicle HUD preview: prefer a one-shot captured portrait texture of the player vehicle over a continuously live SubViewport feed. The live feed path caused repeated wiring/render dead-ends in prior threads.

- If a hidden `SubViewport` is being used only to capture a one-shot HUD portrait/texture, remember hidden SubViewports default to `UPDATE_WHEN_VISIBLE` and will never render off-screen. Force `render_target_update_mode = UPDATE_ALWAYS` (or `UPDATE_ONCE`) for the capture pass, then disable updates after the texture is assigned.

- For one-shot HUD portrait capture, be careful computing bounds on duplicated/off-tree preview models: `MeshInstance3D.IsVisibleInTree()` stays false until the preview subtree is actually attached to the scene tree. If you use that check while building the snapshot viewport, the portrait capture will silently fall back forever. Use the mesh's own `Visible` flag (or attach first, then measure) when gathering preview AABBs.

- If the one-shot hidden-SubViewport portrait capture still falls back to the deterministic preview, stop duplicating off-screen vehicle visuals and crop a one-time portrait directly from the already-rendered main arena viewport around the live player vehicle screen position. That path is much less brittle and still satisfies the “single top-down snapshot at match start” goal.
- `VehiclePawn` does not expose a public `VehicleDef` property. HUD/UI code should not reference `vp.VehicleDef`; use already-available `VehicleDefinition` data passed into the HUD, or keep preview sizing logic generic.

- Build 2026-03-16-53: Vehicle HUD preview now mounts a live embedded `SubViewportContainer` directly under the scene-owned `VehiclePreviewCanvas` and renders the real vehicle `Visual` subtree there. This replaces the failing crop/hidden-capture experiments and keeps the actual preview visible even if texture freeze/readback fails.

- Build 2026-03-16-54: if the HUD vehicle preview is still fighting cloned live `Visual` subtrees or one-shot texture capture, stop duplicating the live model entirely and instantiate a dedicated preview-only `VehiclePawn` scene inside the HUD SubViewport. Reusing the same vehicle scene/loadout pipeline is much more reliable than duplicating imported meshes from the live arena pawn.
- If the HUD vehicle preview still does not appear when using a preview-only `VehiclePawn`, stop depending on `SubViewportContainer` layout/activation and present the preview `SubViewport` through a plain `TextureRect` fed by `viewport.GetTexture()` instead. Also explicitly enable `own_world_3d` and force `render_target_update_mode = UPDATE_ALWAYS`; otherwise the preview viewport can sit in the tree without ever drawing the isolated 3D scene.

- Garage fleet rows now use custom `VehicleListCard` controls inside a `ScrollContainer` instead of `ItemList`. Future garage list polish should extend that card control rather than trying to force more layout richness through `ItemList` row/icon limitations.
- Generated SVG icons applied to `Button` / `OptionButton` controls can render comically large at native size. Route those through `GeneratedUiArt.ApplyIcon(..., maxWidth)` so `icon_max_width` is clamped explicitly, especially for bottom action rows on Garage/Workshop.
- For on-foot arena salvage interactions, prefer a compact anchored multi-action prompt list (shared target + distinct key badges like `E` / `T` / `R`) over single "best action" prompt selection. The single-best-candidate approach hides valid tow/salvage actions whenever vehicle entry wins the priority comparison.
- Arena salvage/towing overlays should avoid the right side of the screen while the vehicle HUD is visible. Put the tow/recovery guidance panel on the left, and prefer world-space start-box highlighting for recovery-zone feedback instead of stacking more text over the HUD cluster.
- Post-match auto-finish (for example returning to the start box with a tow attached) must keep updating even after the hold-to-continue flow is reset/closed. If the loading overlay is shown from an auto-finish path, drive that countdown from its own state flag instead of gating it behind `AwaitingPostMatchExit`, or the loading dialog can soft-lock on screen forever.
- `ResponsivePanelLayout` now supports `GrowWidthToMaxPercent` / `GrowHeightToMaxPercent` for desktop menu screens that should actually use their configured viewport percentages on large monitors instead of staying stuck at the smaller authored panel size.

- Arena enemies should consume ammo from their actual `VehicleInstanceState.AmmoInventory` just like the player. Avoid reintroducing random abstract enemy ammo pools in arena combat code, especially now that enemy loadouts intentionally mix a primary weapon with utility gear like rear mine droppers.
- Arena enemy wall fixes need to account for the **north/south start-lane dead ends**, not just generic rectangular bounds. If the AI is repeatedly kissing the outer wall, treat those start boxes as high-pressure escape corridors and steer back toward the arena opening/centerline instead of only adding more general wall bias.
- Perimeter fog-of-war works best as a **top-down overlay plane above the outer arena**, with shader alpha derived from world X/Z distance outside the combat bowl. A floor-only darkening pass leaves the stands/supports fully readable because those meshes sit above the overlay.
- The north/south outer apron is not just a visual issue: if the east/west side walls stop at the main arena height, vehicles can leak into the hidden side apron and no amount of steering polish fully fixes that. Keep the arena physically sealed first (longer side walls / blockers), then use the fog shader as a readability layer. The strongest fog footprint for this arena was a **plus-shaped union** (main bowl + start-lane corridor), not a single big rectangle that either left the side apron visible or over-darkened the recovery lanes.
- Per-section visible vehicle damage lives in `Scripts/Arena/VehicleDamageVfx.cs` (child node `DamageVfx` on each pawn, fed at 10 Hz from `ArenaRealtimeView.UpdateVehicleDamageVfx`). Extend that node for new damage feedback (sparks, fluid leaks, scorch) instead of adding one-off smoke/fire logic to `VehiclePawn` or the combat view.
- HUD widgets that need arena hazard positions (radar blips etc.) should read `Scripts/Arena/ArenaHazardTelemetry.cs` (republished at 10 Hz by the combat view) rather than reaching into `ArenaRealtimeView`'s private mine/oil/smoke runtime lists. Only player-knowable hazards belong there (own mines yes, enemy mines no).
- Weapon mount legality is data-driven via `WeaponDefinition.AllowedMountLocations` + `IsMountLocationAllowed(...)`; targeting-computer capability checks live in `ArenaWeaponLoadoutResolver` (`IsWeaponComputerControlled`, `HasAutoTrackCapability`, `IsUtilityWeapon`). New combat/UI features should call those helpers instead of re-deriving mount or computer rules.
- User-facing app settings (audio sliders, fullscreen preference) go through `Scripts/Game/GameSettingsStore.cs` → `user://settings.json`. Audio sliders are user gains layered on top of the fixed mix trims in `AudioBusUtil`; if you change a trim there, update the matching constant in `GameSettingsStore` so 100% keeps meaning "default mix".
- The weight→top-speed penalty must stay shared: `VehicleMassMath.ComputeSpeedFactor` is used by both `VehiclePawn` runtime handling and the Workshop performance preview. Don't fork the formula.
- Mine droppers are utility weapons, not primary posture drivers. Keep `ArenaEnemyFirePlanner` focused on non-utility offensive mounts for normal fire/tactical-profile selection, and trigger mine use through explicit situational behavior (tailing pressure / disengage / recovery) instead of letting a high mine `BaseDamage` flip the whole AI into rear-weapon tactics.

- Arena wall-grind fixes need a **committed containment target**, not just stronger per-frame inward bias. When the enemy gets pinned near a perimeter, lock it onto a short inward escape target for a brief window and suspend normal fire/orbit logic until it is back in usable space; otherwise it can seesaw forward/reverse while continuously re-solving around the same wall.
- The arena post-win flow is clearer as a **non-modal exit instruction** plus a highlighted south exit gate. Avoid reopening a blocking match-ended modal once the salvage phase starts; it fights the global Escape pause menu and is slower than just letting the player drive out to finish.
- If the arena camera can still see under a perimeter fog plane, stop only tuning the plane height/density. Add simple **blackout curtain geometry** just outside the arena walls so the view beyond the wall reads as black from the fixed gameplay camera, then use the floor/plane fog only as a secondary darkening layer.
- If enemies keep grinding the north/south outer wall, stop blending the player target with the escape line while they are still in the start lane. Treat the start lane as a **transit funnel** and fully override the steering target back through the center opening until the pawn is back in the main bowl.
