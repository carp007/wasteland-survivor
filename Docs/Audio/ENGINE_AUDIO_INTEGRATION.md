# Engine Audio Integration (layered RPM)

This build provides a positional, layered engine sound system for vehicles in the 3D arena.

## What’s implemented
- Reusable component scene: `Scenes/Audio/VehicleEngineAudio.tscn`
  - Script: `Scripts/Audio/VehicleEngineAudio.cs`
  - 5 loop layers: `idle / low / mid / high / very_high`
  - Crossfades volumes based on a smoothed `rpm01` value (0..1)
  - Subtle pitch scaling with RPM
  - Uses `AudioStreamPlayer3D` so it is positional
- Minimal audio bus setup at runtime:
  - `SFX` (send → `Master`)
  - `Engines` (send → `SFX`)
  - `Tires` (send → `SFX`)
  - Created automatically on boot (`AudioBusUtil.EnsureBuses()`)

## Asset conventions
Engine loop assets are expected under:
- `Assets/Audio/Vehicles/Engines/`

Naming format:
- `veh_engine_{archetype}_loop_{layer}_a.ogg`

Where:
- archetype: `i4_compact` | `v8_muscle` | `diesel_truck`
- layer: `idle` | `low` | `mid` | `high` | `very_high`

Notes:
- The code tolerates archetype ids prefixed with `engine_` (it strips the prefix).
- Looping should be enabled in the import settings; we also attempt to force looping at runtime for common stream types.

## How it’s attached
`VehiclePawn` creates an `EngineAudio` child node at runtime (keeps `VehiclePawn.tscn` minimal).

Archetype selection (v1 heuristic):
- If an installed engine exists and its `FuelType` is `Diesel` → `diesel_truck`
- Otherwise by `VehicleClass`:
  - `Compact` → `i4_compact`
  - `LightTruck` → `diesel_truck`
  - else → `v8_muscle`

## Telemetry
`VehiclePawn` implements `IVehicleAudioTelemetry` (`Scripts/Audio/IVehicleAudioTelemetry.cs`) so the audio component can read:
- speed (m/s)
- max speed (m/s)
- throttle intensity (0..1)
- brake intensity (0..1)

## Automatic transmission (v2)
The RPM driver now supports a simple **automatic transmission** model so holding the accelerator (W) produces a more realistic pattern:
- RPM climbs within a gear
- upshift triggers near redline
- RPM drops briefly after shift

This is audio-only (it doesn’t change physics/acceleration), but it makes the engine layers feel much less "linear".

Key properties (on `VehicleEngineAudio`):
- `UseAutomaticTransmission` (default: true)
- `GearCount` (default: 4)
- `ShiftUpRpm01`, `ShiftDownRpm01`
- `ShiftMinThrottle01`
- `ShiftCooldownSeconds`
- `ShiftRpmDrop01` (how far RPM falls right after an upshift)

If you prefer the original behavior, set `UseAutomaticTransmission = false` and tune `SpeedWeight` / `ThrottleWeight`.

## Hard-brake skid placeholder
`VehicleEngineAudio` can also play a "skid" sound when braking hard for a sustained moment at speed.

Default behavior:
- Trigger when `brake01 >= 0.75` AND `speed >= 7 m/s` for at least `0.25s`
- Re-triggers every `~0.18s` while condition holds (placeholder-friendly)

Placeholder stream:
- By default it tries to play `res://Assets/Audio/tire_pop.wav`.
- Replace with a real skid loop/one-shot later by assigning `BrakeSkidStream` in the inspector (or changing the exported placeholder path).

## Tuning knobs
Open `VehicleEngineAudio.tscn` (or the node instance in the running scene) and tweak exported properties:
- `EngineVolumeDb`, per-layer trims
- `RpmSmoothing`
- `PitchMin` / `PitchMax`
- Transmission settings (above)
- Skid thresholds + repeat time
