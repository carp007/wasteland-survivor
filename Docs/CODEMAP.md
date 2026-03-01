# Code Map (fast onboarding)

This doc is the **“where do I look?”** guide for the codebase.

If you're brand new to the project, read in this order:
1) `Docs/AI_README.md`
2) `Docs/PROJECT_STATE.md`
3) **this file**
4) `Docs/REFRACTORING_PLAN.md` (design notes for the next cleanup pass)

---

## Mental model (30 seconds)

**Boot**
- `Scripts/App/App.cs` is an **Autoload** singleton (`App`). It boots definitions + save, and registers services.

**UI**
- `Scenes/Main.tscn` + `Scripts/App/AppRoot.cs` host the UI root and global overlays.
- “Views” are Godot scenes under `Scenes/UI/*` with controller scripts in `Scripts/UI/*`.

**Game state**
- Persistent state is a pure JSON record tree (`Scripts/Core/State/*`), stored in `user://savegame.json`.
- `Scripts/Game/GameSession.cs` is the *only* API UI should use to mutate state.

**Arena**
- `Scripts/UI/ArenaRealtimeView.cs` runs the realtime combat loop and commits the results back to `GameSession`.
- `Scripts/Arena/ArenaWorld.cs` builds the arena environment procedurally.
- `Scripts/Arena/VehiclePawn.cs` and `Scripts/Arena/DriverPawn.cs` are the runtime pawns.

---

## Entry points

### Godot entry points
- Main scene: `Scenes/Main.tscn`
- Autoload: `Scripts/App/App.cs` (must be added as Autoload named `App`)

### Startup flow
1) Godot loads `Main.tscn`
2) Autoload `App` runs `_Ready()` → boot
3) `AppRoot._Ready()` waits for boot → boot splash → `CityShell`

---

## Folder map

### `Scenes/`
- `Scenes/Main.tscn`: root node graph (WorldRoot + UIRoot + overlays)
- `Scenes/UI/*`: UI scenes (City shell, Garage, Workshop, Arena view, HUD pieces)
- `Scenes/Arena/*`: 3D arena scenes (minimal; most geometry is built in code)
- `Scenes/Audio/*`: reusable audio node(s)

### `Scripts/App/`
- Boot + root scene orchestration.

### `Scripts/Game/`
- `GameSession`: facade used by UI
- `Game/Session/*`: focused “domain services” that mutate save state via `SessionContext`
- `Game/Systems/*`: near-pure math/logic helpers (no node dependencies)

### `Scripts/Core/`
- `Core/Defs/*`: definition record types (loaded from JSON)
- `Core/IO/*`: definition + save loading/saving
- `Core/State/*`: persisted state record types
- `Core/Sim/*`: lightweight command/event payloads (used for decoupling)

### `Scripts/Arena/`
- Runtime pawns + arena world + small VFX nodes.

### `Scripts/UI/`
- UI controllers for scenes; keep them “thin” where possible.
- `Scripts/UI/GameScenes.cs` is the central catalog of common UI scene paths (avoid scattering string literals).

### `Scripts/Audio/`
- Audio bus layout helpers + the layered engine audio node.

### `UiKit/` (shared UI/dialog toolkit project)
- `UiKit/WastelandSurvivor.UiKit.csproj`: reusable UI toolkit project referenced by the main game.
- `UiKit/Scripts/Framework/SceneBinding/SceneBinder.cs`: typed `GetNode` helper with better error messages.
- `UiKit/Scripts/Framework/UI/ScreenRouter.cs`: centralized screen navigation (replace/push/pop) for Control-based UI scenes.
- `UiKit/Scripts/Framework/UI/UiNav.cs`: tiny helper that prefers `ScreenRouter` (when supplied) and falls back to legacy parent-swap navigation.
- `UiKit/Scripts/Framework/UI/ModalHost.cs` + `IModalService.cs`: reusable modal/dialog hosting (dim background + stacked modals).
- `UiKit/Scripts/Framework/UI/DialogCard.cs`: reusable title/body/button "dialog card" used by `ModalService`.
- `UiKit/Scripts/Framework/UI/ModalDialogStyle.cs`: styling hooks injected by the game (so UiKit stays game-agnostic).

---

## Data-driven content

### Definitions (`Data/Defs/**`)
These are “game content” records (vehicles, weapons, ammo, engines, armor, etc).

Loaded by: `Scripts/Core/IO/DefLoader.cs`

### Runtime config (`Data/Config/**`)
These are tuning/config files that aren’t “content defs”, e.g.:
- `ui_theme.json` (palette + sizing)
- `boot_splash.json` (startup splash sequence)
- `driver_pawn.json` (on-foot tuning + avatar config)
- `weapon_visuals.json` (optional weapon model + SFX paths)

Most configs have a small “config store” class near the system that uses them.

---

## Common “how do I…?”

### Add a new UI screen
1) Add a `.tscn` under `Scenes/UI/`
2) Add a controller script under `Scripts/UI/`
3) Add a path constant to `Scripts/UI/GameScenes.cs`
4) Add a method to `IGameNavigator`/`GameNavigator` if it’s a top-level destination (recommended)
5) Navigate to it via `App.Instance.Services.Get<IGameNavigator>()` from UI scripts (or use `ScreenRouter` directly in `AppRoot`)

### Add a new definition type / content category
1) Add a record under `Scripts/Core/Defs`
2) Add a folder under `Data/Defs/<Category>`
3) Update `DefLoader.LoadAll()` to load + validate it
4) Add it to `DefDatabase`

### Make a gameplay change that must persist
- Model it in `Scripts/Core/State/*`
- Add a migration step in `Scripts/Game/Systems/SaveMigration.cs`
- Expose the mutation via `GameSession` → Session service

### Debug / logging
- `GameLog` writes to both the Godot output and the in-game console overlay.
- Toggle console: `~` (tilde/backtick)

---

## Conventions and gotchas

### Immutability and “with” updates
Most state is records. Prefer:
- Read current
- `var next = current with { ... }`
- Replace via `SessionContext.Replace(next)`

Avoid mutating lists/dictionaries in-place (clone first).

### Avoid direct node dependencies in “systems”
`Scripts/Game/Systems/*` should ideally not depend on Godot nodes so we can reuse them later.

### Assets are not shipped in AI zips
AI-delivered zips exclude `Assets/`. Any doc that references assets should include expected paths and setup instructions.
See `Docs/Assets/*`.
