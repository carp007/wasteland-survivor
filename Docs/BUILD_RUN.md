# Build / Run Quickstart

## Godot wiring
1) Project Settings → **Autoload**
- Add `res://Scripts/App/App.cs`
- Name: `App`
- Enable

2) Project Settings → **Run** → Main Scene
- `res://Scenes/Main.tscn`

## Packaging rules (important)
- Do **not** commit/ship `.godot/`.
- For downloadable zips, also exclude: `Assets/` and `.git/`.
- If you replace the project by unzipping over an existing folder, prefer deleting the target folder first.
  - If you must unzip over an existing folder, be careful of stale `.cs` files that can cause duplicate-type compile errors.


## IDE / solution
- Open `Wasteland Survivor.sln` to see both projects:
  - `Wasteland Survivor` (main game)
  - `WastelandSurvivor.UiKit` (shared UI/dialog toolkit)

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
