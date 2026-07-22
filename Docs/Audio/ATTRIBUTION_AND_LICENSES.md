# Audio Attribution & Licenses

## Car Engines Sound Pack Vol. 1 (Magic Sound Effects)
- Source: itch.io listing (Magic Sound Effects)
- Product: **Car Engines Sound Pack Vol. 1**
- Notes: Pack includes city/sport/diesel/off-road vehicle engine sounds, including seamless RPM loops.

### License summary (per the product page)
- Non-exclusive, non-transferable license for personal & commercial use.
- You may edit/modify the assets.
- You may not repackage/resell/sublicense/redistribute the assets (or derivative works) as standalone audio files or sound libraries.
- You may not claim ownership of the audio assets themselves.

### Files used (initial export)
| Our folder | Our file prefix | Source prefix |
|---|---|---|
| `Assets/Audio/Vehicles/Engines/` | `veh_engine_i4_compact_*` | `car_city_a_*` |
| `Assets/Audio/Vehicles/Engines/` | `veh_engine_v8_muscle_*` | `car_sport_b_*` |
| `Assets/Audio/Vehicles/Engines/` | `veh_engine_diesel_truck_*` | `car_diesel_a_*` |


## Added 2026-06-11 (8-hour build run)
- **Music**: "Mechanolith" and "Industrial Cinematic" by Kevin MacLeod (incompetech.com), Licensed under Creative Commons: By Attribution 4.0 License (http://creativecommons.org/licenses/by/4.0/). Files: Assets/Audio/Music/mechanolith_kmacleod.mp3, industrial_cinematic_kmacleod.mp3. In-game credit required: 'Music: Kevin MacLeod (incompetech.com), CC BY 4.0'.
- **Textures (CC0, Poly Haven)**: corrugated_iron_02 (Assets/Images/Textures/Metal/corrugated_iron_diff_2k.jpg), gravel_road (Assets/Images/Textures/Ground/gravel_road_diff_2k.jpg). polyhaven.com, CC0 — no attribution required.
- **UI Fonts (OFL)**: Rajdhani (Indian Type Foundry), Inter (Rasmus Andersson) — Resources/UI/Fonts/ with bundled OFL.txt licenses.

## Added 2026-07-02 (5-hour build loop) — all CC0, no attribution required (credit lines optional)

### Audio
- **Kenney Sci-Fi Sounds** (kenney.nl/assets/sci-fi-sounds, CC0): wpn_rocket_fire_a.wav + wpn_missile_fire_a.wav (thrusterFire_000/002), exp_small_a.wav + exp_med_a/b.wav (explosionCrunch_000/001/002), veh_crash_light_a.wav (impactMetal_000) — Assets/Audio/Weapons, Explosions, Vehicles/Impacts.
- **Kenney Impact Sounds** (kenney.nl/assets/impact-sounds, CC0): wpn_dropper_deploy_a.wav (impactPlate_heavy_004), veh_hit_metal_a-d.wav (impactMetal medium/heavy), impact_concrete_a-c.wav (impactMining_000/002/004) — Assets/Audio/Weapons, Vehicles/Impacts, World.
- **25 CC0 bang/firework sfx** by rubberduck (opengameart.org/content/25-cc0-bang-firework-sfx, CC0): wpn_mg_fire_a.wav (shot_03), wpn_ac_20mm_fire_a.wav (cannon_04), exp_small_b.wav (bang_03).
- **100 CC0 SFX** by rubberduck (opengameart.org/content/100-cc0-sfx, CC0): veh_crash_med_a.wav (metal_12).
- **Chunky Explosion** by Joth (opengameart.org/content/chunky-explosion, CC0): exp_big_a.wav.
- **Crash! Collision** by qubodup (opengameart.org/content/crash-collision, CC0): veh_crash_heavy_a.wav.
- **Kenney Interface Sounds** (kenney.nl/assets/interface-sounds, CC0): ui_click_a.wav, ui_hover_a.wav, ui_confirm_a.wav, ui_error_a.wav — Assets/Audio/UI.
- **RPG Sound Pack** by artisticdude (opengameart.org/content/rpg-sound-pack, CC0): ui_purchase_a.wav (inventory/coin2.wav).
- **Kenney Music Jingles** (kenney.nl/assets/music-jingles, CC0): stinger_win_a.ogg (jingles_HIT15), stinger_loss_a.ogg (jingles_HIT09) — Assets/Audio/Music.
- **Tire skid + ambience loops** (see Assets/Audio/Vehicles/Tires/veh_tire_skid_asphalt_loop_a.ogg, Assets/Audio/Ambience/amb_wind_desert_loop_a.ogg, amb_city_industrial_loop_a.ogg): sourced CC0 per the 2026-07-02 asset run manifest.

## Legacy audio (pre-manifest) — provenance unknown, replace before distribution
2026-07-02 audit: these files ship in `Assets/Audio` but predate this manifest and have no recorded
source. Treat them as unlicensed placeholders — replace each with a tracked CC0/licensed equivalent
(or confirm its provenance and record it here) before any public/distributed build.

| File | Referenced by | Status |
|---|---|---|
| `Assets/Audio/Weapons/VehicleMounted/50CalMachineGun/fire.wav` | `Data/Config/weapon_visuals.json` (mg_50cal fire sound) | legacy - provenance unknown, replace before distribution |
| `Assets/Audio/Vehicles/Impacts/50calhit.mp3` | nothing (staged, unreferenced) | legacy - provenance unknown, replace before distribution |
| `Assets/Audio/tire_pop.wav` | `Scripts/UI/ArenaRealtimeView.cs` (tire-destroyed cue) | legacy - provenance unknown, replace before distribution |
| `Assets/Audio/ui_hit.wav` | `Scripts/UI/ArenaRealtimeView.cs` (hit-confirm fallback) | legacy - provenance unknown, replace before distribution |
| `Assets/Audio/ui_miss.wav` | nothing (staged, unreferenced) | legacy - provenance unknown, replace before distribution |
| `Assets/Audio/Vehicles/ignition.wav` | `Scripts/Audio/UiSfx.cs` ("ignition" cue, city travel departure) | legacy - provenance unknown, replace before distribution |

### Known sourcing gaps (wanted assets, not yet staged)
- Dedicated missile-thruster loop: `Scripts/Arena/HomingMissileVfx3D.cs` currently reuses
  `veh_engine_i4_compact_loop_very_high_a.ogg` pitched up at -18 dB.
- ~~Arena crowd bed + swells~~: staged 2026-07-03 (see below).
- ~~EV engine loop set~~: `veh_engine_ev_loop_*_a.ogg` staged 2026-07-03 (see below); code still
  maps Electric to `i4_compact` in `VehiclePawn.ComputeEngineArchetypeId` — switch it to `"ev"`
  to activate.

## Added 2026-07-03 (audio gap-fill run) — all CC0 / original, no attribution required

### Arena crowd bed + swells (`Assets/Audio/Ambience/`)
All archive.org licenses verified on the item pages at download time (both display "CC0 1.0 Universal").

- **amb_crowd_arena_loop_a.ogg** — 60 s seamless loop cut from
  `R08-38-Cheering Crowd at Sporting Event.wav` (segment 38–98 s), from **"Red Library: Crowds
  Sports"** (https://archive.org/details/Red_Library_Crowds_Sports), part of the **USC Optical
  Sound Effects Library** collection (digitized tapes provided by Craig Smith of USC; uploaded by
  Jason Scott / Internet Archive, 2023). License: **CC0 1.0 Universal** per the item page.
  Processing: band-limited 80 Hz–6.5 kHz, 4 s equal-power tail-into-head crossfade for the loop
  seam, normalized to −21 LUFS (matches `amb_wind_desert_loop_a.ogg`); mono 48 kHz Vorbis q4.
- **amb_crowd_swell_a.ogg** — 4.4 s cheer burst cut from
  `R07-01-Crowd Cheering at Sporting Event.wav` (3.2–7.6 s), same source/license as above.
  Normalized −16 LUFS, faded head/tail.
- **amb_crowd_swell_b.ogg** — 4.3 s reaction burst cut from
  `R19-35-Golf Crowd Reacts to Good Shot.wav` (1.6–5.9 s), same source/license as above.
  Normalized −16 LUFS, faded head/tail.
- **amb_crowd_swell_c.ogg** — 4.3 s applause/whoop burst cut from
  `CRWDApls-CU_Crowd Applause, Cheering, Yelling, Whooping_Nicholas Judy_TDC.wav` (33.5–37.8 s),
  from **"The Designer's Choice UCS Collection - CROWDS"**
  (https://archive.org/details/Designers-Choice-Collection-Crowds). Recorded and released by the
  author himself, **Nicholas A. Judy (The Designer's Choice)**; item page displays **CC0 1.0
  Universal** and the description states "All sound effects are available to you on a complete
  royalty-free basis... You can credit me or not." Normalized −16 LUFS, stereo.

### EV engine whine loop set (`Assets/Audio/Vehicles/Engines/`) — original, synthesized in-project
- **veh_engine_ev_loop_idle_a.ogg / _low_ / _mid_ / _high_ / _very_high_a.ogg** — five 6 s seamless
  mono loops following the 5-layer archetype naming in `Docs/Audio/Engine_Archetypes.md`
  (archetype id `ev`). Not sourced from any third party: generated for this project with ffmpeg
  (additive sine harmonics — motor hum 52→162 Hz + inverter whine 640→2950 Hz — plus low-passed
  pink noise, slow AM texture, equal-power loop seam). Loudness matched to the `i4_compact` layer
  curve (−30 / −23 / −18 / −14 / −11 LUFS idle→very_high). Project-owned; treat as CC0.
  NOTE: not yet wired — `VehiclePawn.ComputeEngineArchetypeId` still returns `"i4_compact"` for
  `FuelType.Electric`; change it to `"ev"` to enable.

### 3D Models (CC0)
- **Kenney Car Kit 3.1** (kenney.nl/assets/car-kit, CC0): 50 GLB vehicle/debris models — Assets/Models/Vehicles/KenneyCarKit/ (requires Textures/colormap.png alongside).
- **Kenney Blaster Kit 2.1** (kenney.nl/assets/blaster-kit, CC0): 40 GLB weapon models — Assets/Models/Weapons/KenneyBlasterKit/ (requires Textures/colormap.png alongside).
- **Kenney Racing Kit 2.0** (kenney.nl/assets/racing-kit, CC0): barriers, grandstands, billboards, flags, light posts — Assets/Models/Arena/RacingKit/.
- **Poly Haven props** (polyhaven.com, CC0): barrel_stove, concrete_road_barrier, metal_trash_can, old_tyre (2k glTF) — Assets/Models/Arena/Obstacles/<slug>/.
