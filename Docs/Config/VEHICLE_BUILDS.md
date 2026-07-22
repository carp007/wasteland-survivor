# Vehicle build presets

`Data/Config/vehicle_builds.json` centralizes simple starter/enemy build presets so we can tune common loadouts without editing gameplay code.

## Current consumers
- `SessionGarage.CreateStarterVehicle(...)`
- `ArenaRealtimeView` enemy vehicle creation via `VehicleBuildFactory`

## Shape
- `StarterBuilds[]` — presets keyed by `VehicleDefinitionId` for player-friendly default builds
- `ArenaEnemyBuilds[]` — presets keyed by `Tier` for current arena opponents

Each preset can define:
- vehicle definition id
- engine id
- targeting computer id
- mounted weapons by mount id
- starting ammo inventory
- cargo inventory
- optional plating levels

Arena enemy presets also support:
- `MirrorPlayerWeaponsWhenPossible` — copies the player loadout on overlapping mounts after the preset is built

## Why this exists
This is the first step toward data-driven vehicle/content build-out. Future passes can expand this into:
- named opponent rosters
- city/vendor stock lists
- difficulty-specific loadouts
- visual presets / paint jobs / VFX presets
