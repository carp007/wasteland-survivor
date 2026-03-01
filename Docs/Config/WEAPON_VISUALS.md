# Weapon visuals config

This project can render mounted weapons using either:
- a simple **proxy box** (default), or
- a configured **3D model scene** loaded at runtime.

Config file:
- `res://Data/Config/weapon_visuals.json`

## Format

```json
{
  "Weapons": {
    "wpn_mg_50cal": {
      "ScenePath": "res://Assets/Models/Weapons/VehicleMounted/50CalMachineGun/scene.gltf",
      "Scale": 2.0,
      "DesiredLength": 1.15,
      "AutoAlignYaw": true,
      "DebugAlignment": false,
      "MountPointNodeName": null,
      "MuzzleNodeName": null,

      "FireSoundPath": "res://Assets/Audio/Weapons/VehicleMounted/50CalMachineGun/fire.wav",
      "HitVehicleSoundPath": "res://Assets/Audio/Vehicles/Impacts/50calhit.mp3",
      "HitWorldSoundPath": null
    }
  }
}
```

### Fields
- `ScenePath`: PackedScene / glTF to instantiate for the weapon visual.
- `Scale`: Optional. Multiplies the visual scale before other alignment steps (good for tiny/huge imports). Default `1.0`.
- `DesiredLength`: Optional. Scales the weapon so its longest X/Z dimension equals this length (in meters-ish world units).
- `AutoAlignYaw`: If true, attempts to auto-rotate the model so its forward direction aligns with vehicle forward (-Z).
- `DebugAlignment`: If true, prints yaw alignment diagnostics to the Godot Output.
- `MountPointNodeName`: Optional helper node name in the model hierarchy to use as the attachment origin.
- `MuzzleNodeName`: Optional helper node name in the model hierarchy to use as the muzzle position/direction.

### Weapon audio fields
- `FireSoundPath`: Optional. One-shot fire sound (played each time the weapon fires).
- `HitVehicleSoundPath`: Optional. One-shot impact sound when the projectile hits a vehicle.
- `HitWorldSoundPath`: Reserved for future use (when hitting environment).

## Alignment behavior
When a model visual is configured:
1) The model is (optionally) scaled to `DesiredLength`.
2) The model is shifted so its mount point becomes the weapon root origin (0,0,0).
   - Uses `MountPointNodeName` if present, otherwise uses the model bounds center.
3) If `AutoAlignYaw` is enabled, the model is yaw-rotated so its forward axis points toward -Z.
   - If a muzzle helper node exists, the vector from mount→muzzle is preferred.
   - Otherwise the forward axis is estimated via PCA on mesh bounds points (XZ plane).
4) A `Muzzle` Marker3D is positioned using a muzzle helper node if present, otherwise using the most-forward mesh point.

These heuristics are meant to reduce manual per-model tweaking, but some models may still require
adding helper nodes (MountPoint/Muzzle) or overriding rotation in the future.
