# Next Tasks (keep this small and current)

## Current focus
On-foot interaction foundation (enter/exit + prompts) and a first “hazard” weapon (mines), while keeping the city/garage/workshop loop stable.

## Recently completed (see `CHANGELOG.md` for full details)
- Escape pause menu overlay (Settings stub + Exit confirmation).
- Reverse engine audio no longer goes silent; reverse behaves like 1st gear.
- HUD reliability fixes (VehicleStatusHud binding + safe fallback) and HUD alignment polish.
- Driver exit/on-foot prototype (E to exit/enter; follow camera + radar switch; collision damage).
- Arena flow improvements (post-match salvage phase with hold G to exit).

## Next small steps
1) Interactables:
   - Expand Action Prompt to support multiple interactables (loot/salvage, tires, tow points).
   - Optional: subtle ground ring/outline for the current interactable.
2) Tire repair prototype:
   - Walk to a tire → press E → consume a spare tire item and restore that tire’s HP/AP.
3) Land mines (Mine Dropper weapon):
   - Drop a mine behind the vehicle; detonate on vehicle contact (tire damage is the priority).
4) Weapon visuals:
   - Continue improving `weapon_visuals.json` workflow; standardize helper nodes (`MountPoint`, `Muzzle`) in imported weapon scenes.
5) Vehicle damage consequences:
   - Reduce speed/handling when a tire HP reaches 0 (already partially present — make it more obvious).
6) Weight/capacity foundation:
   - Start showing a “mass / capacity” bar and keep the math in `VehicleMassMath`.
7) Audio polish:
   - Add a real tire skid/squeal loop/one-shot under `Assets/Audio/Vehicles/Tires/` and wire it to the existing hooks.
