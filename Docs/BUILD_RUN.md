# Build / Run Quickstart

## Godot wiring
1) Project Settings → **Autoload**
- Add `res://Scripts/App/App.cs`
- Name: `App`
- Enable

2) Project Settings → **Run** → Main Scene
- `res://Scenes/Main.tscn`

## Godot constraints
- Do **not** commit/ship `.godot/`.


## IDE / solution
- Open `Wasteland Survivor.sln` to see both projects:
  - `Wasteland Survivor` (main game)
  - `GameUiKit` (shared UI/dialog toolkit)

## Smoke tests
- City shell opens
- Garage renders vehicles
- Workshop: buy ammo, repair, scrap patch, upgrade plating
- Arena: drive, shoot, Tab-target, enemy drives/fires
- Note: Arena is the canonical implementation (3D world + fixed 2.5D camera). Legacy 2D arena has been removed.
- Win encounter → rewards applied → return to city

## Overlay sanity
- `~` toggles the Console overlay.
- Console defaults to hidden/closed and sits near the bottom-left (~60% screen width).

- Esc opens the pause menu overlay.
