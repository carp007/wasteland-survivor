# HTML-First Hybrid UI Direction

## Decision
For premium menu/management screens, use **HTML/CSS mockups as the design-authoring layer** and keep **native Godot UI as the runtime layer** for now.

## Why this direction
- AI tools can generate stronger layout, spacing, hierarchy, and visual language in HTML/CSS than we are currently achieving by improvising directly inside Godot scenes.
- The game still benefits from native Godot runtime UI for input handling, screen routing, save-state integration, controller/key prompts, and lower packaging risk.
- This lets us keep shipping incremental UI upgrades without taking on the full dependency cost of an embedded browser stack in the main game loop.

## Practical rule
1. Create/iterate premium screen mockups in HTML/CSS first when a screen needs a major visual pass.
2. Distill those mockups into a **shared premium component language** inside Godot:
   - screen shell
   - section panels
   - inset cards
   - metric tiles
   - hero/banner art
   - consistent icon sizing / spacing rules
3. Keep all gameplay-critical UI runtime-native unless there is a clear reason to embed web tech.

## Not the baseline (for now)
Embedded webview / CEF inside the game is **experimental**, not the default path for core screens. It may still be useful later for:
- launcher / patch notes
- account flows
- mod browser
- external tools / admin panels

## Current rollout plan
1. Garage established the premium mockup-driven art direction.
2. Workshop moved onto the same shell/section language.
3. City Hub now follows that same system, giving the city management loop one cohesive visual family.
4. On desktop, premium management screens should now behave like **left-edge drawers / side panels** instead of centered floating dialogs: keep them under roughly half-screen width so the background art remains visible.
5. The placement for those drawers is now best driven by the owning screen script, not only by detached scene helper nodes, so the shell can be forced into the correct left-edge position on the user's real build.
6. Once the drawer footprint feels right, spend the next passes on **layout density**: remove stretched empty cards, compact repeated stat tiles/previews, and restructure sections so the drawer uses its space efficiently.
7. If a drawer still feels overloaded after density cleanup, introduce sub-menus / tabs rather than forcing every section onto one long page. Garage/Workshop now follow that rule.
8. Next, tighten row-level polish inside Workshop/Garage and then revisit HUD/overlay screens with the same design tokens.

## Implementation notes
- Prefer shared style creators in `Scripts/UI/GeneratedUiArt.cs` over ad hoc styleboxes per screen.
- Prefer reusable premium cards/panels over deeply custom one-off scene trees.
- Optional authored textures remain optional and must keep clean runtime fallbacks.
