# Audio Checklist (Wasteland Survivor)

This is the canonical checklist for sourcing + organizing audio for the project.

## Folder plan (inside the repo)
Audio lives under `Assets/Audio/`.

- `Assets/Audio/_INBOX/` *(raw downloads; never referenced by the game)*
- `Assets/Audio/_PROCESSED/` *(trimmed/looped/normalized; only reference these in-game)*
- `Assets/Audio/Vehicles/Engines/`
- `Assets/Audio/Vehicles/Tires/`
- `Assets/Audio/Vehicles/Impacts/`
- `Assets/Audio/Vehicles/Damage/`
- `Assets/Audio/Weapons/`
- `Assets/Audio/Explosions/`
- `Assets/Audio/UI/`
- `Assets/Audio/Ambience/`
- `Assets/Audio/Music/` *(optional later)*

> Repo workflow note: our standard AI-delivered project zip excludes `Assets/` to keep downloads small.
> If we need to add/adjust files under `Assets/Audio/`, we do it via a separate small “asset bundle” zip.

## Licensing gate (non-negotiable)
- Prefer: **CC0** or **royalty-free commercial** (no attribution required)
- Allowed with tracking: **CC-BY** (must credit)
- Avoid entirely: **NC / Non-Commercial**, “editorial-only”, unclear licenses

Nothing goes into `_PROCESSED/` without an entry in `Docs/Audio/ATTRIBUTION_AND_LICENSES.md`.

## MVP audio (make the game feel alive)

### Vehicles — Engines (MVP: 3 archetypes)
Goal: unique-ish and believable acceleration without requiring “true” simulation.

For each archetype, collect **loop layers**:
- `idle` loop
- `low` loop
- `mid` loop
- `high` loop

Optional (nice-to-have):
- `load` loop (engine under throttle / strain)

Start with 3 archetypes:
- `engine_v8_muscle` *(hero)*
- `engine_i4_compact`
- `engine_diesel_truck`

Shared one-shots (can be generic for MVP):
- start/ignite
- stop/shutoff
- gear shift / clunk (1–3 variants)
- backfire (optional)

### Vehicles — Tires & movement
- roll loop (asphalt)
- roll loop (dirt/gravel) *(Phase 2 if needed)*
- skid/squeal (short + optional loop)
- gravel spray / pebbles (short bursts)
- bump/thud (2–4 variants)

### Vehicles — Impacts & damage
- metal hit light (4–8 variants)
- metal hit heavy (4–8 variants)
- body crunch (2–4 variants)
- glass crack/shatter (2–4 variants) *(optional if no glass yet)*
- tire pop (1–2 variants)
- damage sputter loop *(optional)*

### Weapons (MVP: 2 families)
Adjust names to match the current build.

- machine gun: fire (short), optional tail, optional bolt/mech click
- cannon/shotgun: fire (short), optional tail
- impacts: metal/armor (4–8), dirt (4–8)
- ricochet / whiz-by (optional)

### Explosions (MVP)
- small explosion (3–6)
- medium explosion (3–6)
- large explosion (2–4)
- debris/shrapnel sweeteners (optional)
- shockwave “thump” (optional)

### Mines (when implemented)
- arm / place
- beep / warning (optional)
- detonate (can reuse small/medium explosion set)

### UI (MVP)
- click / hover (2–4)
- confirm / accept
- cancel / back
- error / denied (optional)
- target select / lock (optional)

### Ambience (MVP)
- wind bed (loop)
- distant desert / industrial rumble (loop)
- arena bed (optional)

## Phase 2 (polish: “sounds like a car game”)

### Engines — expand archetypes (6+)
- `engine_v6`
- `engine_v8_supercharged` *(fun hero upgrade)*
- `engine_sports_highrev` *(bike or tuner)*
- `engine_electric_whine` *(if any EVs/drones)*
- `engine_junker_rattle` *(damaged/janky vibe)*

### Engine one-shots
- rev blip (1–3)
- limiter bounce (1–2)
- turbo spool / blowoff (if used)
- intake roar layer (optional)

### Tires — surface-based
- mud roll
- sand roll
- wet roll
- offroad skid

### Combat depth
- more distinct weapon families (rocket, flame, etc. as needed)
- close vs far explosion variants
- vehicle collision set (light/med/heavy)

### World feedback
- pickup/salvage
- repair / install upgrade
- garage ambience

## Phase 3 (stretch)
- doppler / whiz-by for bullets
- reverb zones (arena vs open desert)
- dynamic mix (duck UI/music on explosions)
- signature engines for bosses

## Naming conventions (keep it searchable)
Format:
- `category_subcategory_detail_variant.ext`

Examples:
- `veh_engine_v8_idle_a.ogg`
- `veh_engine_v8_mid_b.ogg`
- `veh_start_generic_a.wav`
- `veh_tire_skid_asphalt_loop_a.ogg`
- `wpn_mg_fire_a.wav`
- `wpn_cannon_impact_metal_d.wav`
- `exp_medium_b.wav`
- `ui_click_a.wav`
- `amb_wind_desert_loop_a.ogg`

Rules:
- loops include `_loop_`
- variants end with `_a`, `_b`, `_c`...
- keep raw downloads in `_INBOX`, edited in `_PROCESSED`, and only reference `_PROCESSED` in game
