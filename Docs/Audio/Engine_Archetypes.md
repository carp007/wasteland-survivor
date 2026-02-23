# Engine archetypes (initial 3)

This document maps **Wasteland Survivor** engine archetypes to source files from **Car Engines Sound Pack Vol. 1 (Magic Sound Effects)**.

## RPM layer mapping
The source pack provides seamless loops at **five speeds**, named `engine_loop_0x` … `engine_loop_4x`.

We map them to our layer names as follows:

| Pack loop | Layer name | Notes |
|---|---|---|
| `engine_loop_0x` | `idle` | stationary / very low RPM |
| `engine_loop_1x` | `low` | gentle acceleration |
| `engine_loop_2x` | `mid` | cruising |
| `engine_loop_3x` | `high` | hard acceleration |
| `engine_loop_4x` | `very_high` | near-redline / top end |

All exported files in `Assets/Audio/Vehicles/Engines/` are **mono OGG** derived from the pack’s **mono WAV** sources.

---

## Archetype: `engine_i4_compact`
**Source vehicle:** `car_city_a`

| Our file | Source file |
|---|---|
| `veh_engine_i4_compact_loop_idle_a.ogg` | `SFXs/Mono/City/car_city_a_engine_loop_0x.wav` |
| `veh_engine_i4_compact_loop_low_a.ogg` | `SFXs/Mono/City/car_city_a_engine_loop_1x.wav` |
| `veh_engine_i4_compact_loop_mid_a.ogg` | `SFXs/Mono/City/car_city_a_engine_loop_2x.wav` |
| `veh_engine_i4_compact_loop_high_a.ogg` | `SFXs/Mono/City/car_city_a_engine_loop_3x.wav` |
| `veh_engine_i4_compact_loop_very_high_a.ogg` | `SFXs/Mono/City/car_city_a_engine_loop_4x.wav` |

## Archetype: `engine_v8_muscle`
**Source vehicle:** `car_sport_b`

| Our file | Source file |
|---|---|
| `veh_engine_v8_muscle_loop_idle_a.ogg` | `SFXs/Mono/Sport/car_sport_b_engine_loop_0x.wav` |
| `veh_engine_v8_muscle_loop_low_a.ogg` | `SFXs/Mono/Sport/car_sport_b_engine_loop_1x.wav` |
| `veh_engine_v8_muscle_loop_mid_a.ogg` | `SFXs/Mono/Sport/car_sport_b_engine_loop_2x.wav` |
| `veh_engine_v8_muscle_loop_high_a.ogg` | `SFXs/Mono/Sport/car_sport_b_engine_loop_3x.wav` |
| `veh_engine_v8_muscle_loop_very_high_a.ogg` | `SFXs/Mono/Sport/car_sport_b_engine_loop_4x.wav` |

## Archetype: `engine_diesel_truck`
**Source vehicle:** `car_diesel_a`

| Our file | Source file |
|---|---|
| `veh_engine_diesel_truck_loop_idle_a.ogg` | `SFXs/Mono/Diesel/car_diesel_a_engine_loop_0x.wav` |
| `veh_engine_diesel_truck_loop_low_a.ogg` | `SFXs/Mono/Diesel/car_diesel_a_engine_loop_1x.wav` |
| `veh_engine_diesel_truck_loop_mid_a.ogg` | `SFXs/Mono/Diesel/car_diesel_a_engine_loop_2x.wav` |
| `veh_engine_diesel_truck_loop_high_a.ogg` | `SFXs/Mono/Diesel/car_diesel_a_engine_loop_3x.wav` |
| `veh_engine_diesel_truck_loop_very_high_a.ogg` | `SFXs/Mono/Diesel/car_diesel_a_engine_loop_4x.wav` |

---

## Notes / next iteration
- If you prefer a different “character” for any archetype, we can swap the source vehicle letter (e.g. `car_city_f` instead of `car_city_a`) without changing any code—only this mapping and the exported files.
- The pack includes **accel/decel** and **RPM transition** one-shots; we didn’t export those yet to keep the initial integration tight.
