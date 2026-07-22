# On-Foot Combat Plan (personal weapons + armor)

Status: **DATA + SPEC layer landed 2026-07-22** (JSON defs + `PersonalWeaponDefinition` record + spec
section in `Docs/MASTER_GAME_SPEC.md`). Runtime integration is staged below and intentionally NOT
wired yet — the integration edits touch existing files and belong to a build-owning session.

Play-test request driving this: *"when the player is outside the vehicle, they should have a basic
weapon. One of the stores should sell different weapons the player can have on their person. Armor
as well."*

---

## Design intent (one paragraph)

On-foot is high-risk utility, not a shooter. Personal weapons are (a) last-resort defense while
exposed during tire swaps / salvage / tow hookups, and (b) a finishing tool: chip damage against
hulls, real damage only through already-destroyed sections via the existing overflow rules. No
personal weapon may out-DPS a vehicle-mounted MG (`wpn_mg` = 4 dmg / 120 ms ≈ 33 dps; the on-foot
ceiling is the SMG at ≈ 23 dps). The player is never unarmed: a fresh clone always wakes with the
basic 9mm sidearm.

---

## What already exists (audit, 2026-07-22)

- **Driver Store** — `Scripts/UI/DriverStoreView.cs` ("Clinic & Outfitter", scene constant
  `GameScenes.DriverStoreView` line 26). Sells two `DriverUpgradeDefinition` kinds from
  `Data/Defs/DriverUpgrades/`: `Cybernetic` (max-HP implants, clone-facility-gated) and `Armor`
  (vests). `Rebuild()` (line 163) enumerates `defs.DriverUpgrades` — **new vest JSONs go live with
  zero code changes**.
- **Driver armor** — single vest slot on `PlayerProfileState` (`EquippedArmorId`,
  `DriverArmorMax/DriverArmor`, lines 35-37). Purchase/trade-in logic:
  `SessionStore.TryBuyDriverArmor` (line 308) + `GetDriverArmorTradeInValue` (line 240, 40% of
  list). Existing ladder before this pass: `armor_kevlar_basic` 50 AP (legacy `ArmorDefinition`,
  `Data/Defs/Armor/kevlar_basic.json`, default equip) → `drv_vest_composite` 65 AP $450 →
  `drv_vest_assault_rig` 85 AP $1,100.
- **On-foot pawn** — `Scripts/Arena/DriverPawn.cs` (thin wrapper over GamePawnKit `HumanoidPawn`;
  ground marker pattern at lines 52-92 shows how to attach extra visuals). Tuning in
  `Data/Config/driver_pawn.json` via `DriverPawnConfigStore` (loaded in `ArenaRealtimeView` line 371).
- **Enemies already engage the on-foot driver** — `ResolveEnemyAimTarget` (`ArenaRealtimeView.cs`
  line 1823), aim-at-body correction lines 4819-4828, hit routing lines 4921-4922 →
  `OnHitDriverOnFoot` (line 5296) → `ApplyDriverDamage` (line 5400, AP absorbs before HP). Vehicle
  collision damage on foot: `UpdateDriverCollisionDamage` (line 5310).
- **What is missing**: the player cannot fire back on foot. The fire gate at
  `ArenaRealtimeView.cs` line 795 (`_playerHpRuntime > 0 && _playerControlMode ==
  PlayerControlMode.Vehicle`) explicitly excludes on-foot mode.

---

## Data schema

### `PersonalWeaponDefinition` (NEW record, `Scripts/Core/Defs/PersonalWeaponDefinition.cs`)

Loaded from `Data/Defs/PersonalWeapons/*.json`. Field conventions follow `WeaponDefinition`
(`CooldownMs`, `BaseDamage`, `MassKg`, `PriceUsd`) and `DriverUpgradeDefinition` (`Flavor`).
Enums serialize as strings like every other def.

| Field | Type | Meaning |
|---|---|---|
| `Id` | string | `pw_*` id (namespace-distinct from `wpn_*` vehicle weapons). |
| `DisplayName` | string | Store/HUD name. |
| `WeaponClass` | enum | `Sidearm` / `Smg` / `Shotgun` / `Rifle` (roles per master spec). |
| `BaseDamage` | float | Damage per pellet vs drivers/on-foot targets, before the ±15% roll. |
| `PelletsPerShot` | int | 1 for everything except the shotgun (7). Each pellet rolls spread independently. |
| `CooldownMs` | int | Per-shot cooldown (same unit as `WeaponDefinition.CooldownMs`). |
| `RangeMeters` | float | Hard hitscan range; auto-aim only engages targets inside it. |
| `SpreadRadians` | float | Yaw jitter per pellet (matches the arena's spread convention). |
| `VehicleDamageMultiplier` | float | Scales damage vs vehicle sections (chip damage; ≤ 0.5). |
| `AmmoId` | string | Personal ammo pool key (`pammo_*`). Pistol + SMG share `pammo_9mm`. |
| `AmmoCapacity` | int | Max rounds of `AmmoId` carried while this weapon is equipped (single equipped slot ⇒ unambiguous). |
| `AmmoPricePer10Usd` | int | Driver Store price per 10-round box. |
| `StartingAmmo` | int | Rounds granted on purchase (topped up to at most `AmmoCapacity`). |
| `MassKg` | float | Carried mass (future: exo-frame carry-weight interplay; cosmetic today). |
| `PriceUsd` | int | Store price. 0 = not sold (convention shared with `WeaponDefinition`). |
| `Flavor` | string | One fiction line under the stat line (store row convention). |

**Personal ammo pools are NOT `AmmoDefinition` entries** and are NOT stored on
`VehicleInstanceState.AmmoInventory`. They live on the player profile (see Stage 1 state changes) so
they follow the driver through cloning and vehicle swaps. `pammo_*` ids are resolved only against
`PersonalWeapons` defs.

### Shipped defs (`Data/Defs/PersonalWeapons/`)

| Def | Class | Dmg × pellets / cooldown | DPS | Range | Vehicle mult | Ammo (cap / start / $per10) | Price |
|---|---|---|---|---|---|---|---|
| `pw_pistol_9mm` | Sidearm | 6 × 1 / 450 ms | ~13 | 25 m | 0.20 | `pammo_9mm` 60 / 60 / $5 | $120 |
| `pw_smg_9mm` | Smg | 3 × 1 / 130 ms | ~23 | 20 m | 0.20 | `pammo_9mm` 180 / 90 / $5 | $650 |
| `pw_shotgun_12g` | Shotgun | 3 × 7 / 950 ms | ~22 pt-blank | 12 m | 0.15 | `pammo_12g` 36 / 24 / $15 | $900 |
| `pw_hunting_rifle` | Rifle | 16 × 1 / 1500 ms | ~11 | 55 m | 0.40 | `pammo_308` 40 / 20 / $25 | $1,400 |

Balance anchors: driver pools are 50-160 EHP (50 HP + 50-110 AP), so the pistol needs ~8 s of
uninterrupted hits to drop a basic-kevlar driver — defensive, not oppressive. All DPS sits below
the cheapest vehicle MG. Rifle range (55 m) intentionally under-reaches vehicle guns (90 m).

### Armor tiers (shipped as `DriverUpgradeDefinition`, `Data/Defs/DriverUpgrades/`)

The live store-sold armor schema is `DriverUpgradeDefinition` with `Kind: "Armor"` — NOT the legacy
`ArmorDefinition` (`Data/Defs/Armor/` is only the basic-kevlar default lookup used by
`SaveMigration.cs` line 67 and a display-name fallback in `DriverStoreView` line 178). New tiers
were therefore added in the DriverUpgrades schema so they appear in the store immediately:

| Def | AP | Price | Slot in ladder |
|---|---|---|---|
| `armor_kevlar_basic` (existing, legacy schema) | 50 | free default | tier 0 |
| **`drv_vest_kevlar_heavy` (NEW)** | 58 | $180 | bridge tier 0 → composite |
| `drv_vest_composite` (existing) | 65 | $450 | mid |
| `drv_vest_assault_rig` (existing) | 85 | $1,100 | high |
| **`drv_vest_exo_plate` (NEW)** | 110 | $2,600 | top (fiction hook for the post-v1 powered exo-frame) |

---

## Staged rollout

### Stage 1 — equipped sidearm fires at the locked/nearest target

Player state (edits to existing files — see checklist):
- `PlayerProfileState`: add `EquippedPersonalWeaponId` (default `"pw_pistol_9mm"`),
  `OwnedPersonalWeaponIds` (default `new()` — treat the default sidearm as implicitly owned), and
  `PersonalAmmoInventory` (`Dictionary<string,int>`, default `new()`; treat a missing `pammo_9mm`
  entry as "never initialized" and seed it to the pistol's `StartingAmmo` on first arena load).
  All-additive defaults ⇒ **no save-version bump / migration step needed** (same pattern as
  `InstalledCyberneticIds`, `PlayerProfileState.cs` lines 39-42).

Firing loop (all inside `Scripts/UI/ArenaRealtimeView.cs`, one new private method
`TryFirePersonalWeapon()` + one new field `_personalWeaponCooldown`):
1. **Input**: at the fire gate (lines 791-805), add an `else if (_playerControlMode ==
   PlayerControlMode.Driver && IsPlayerOnFoot())` branch: `if (fire1) TryFirePersonalWeapon();`.
   Reuses `ws_fire` / `ws_fire_1` exactly as registered at lines 6443-6451 — no new input actions.
2. **Aim** (top-down auto-aim, keyboard-first): target = `_targeting.SelectedVehicleTarget ??
   _enemyPawn` (the same resolution `TryFire` uses at line 4723; selection maintained by
   `UpdateTargetSelection`, line 4125). Engage only if `Distance2D(driver, target) <=
   def.RangeMeters` AND the target sits inside a ~100° cone of the driver avatar's facing;
   otherwise fire down the facing line (shots whiff — readable feedback that you're pointed wrong).
3. **Muzzle**: `DriverPawn` has no mount points — fire from `_driverPawn.GlobalPosition +
   Vector3.Up * 1.2f` (chest height; mirrors the enemy's aim-at-driver offset at line 4826).
4. **Shot resolution**: per pellet, jitter direction by `SpreadRadians` (pattern at lines
   4852-4853), raycast via `ArenaRaycastUtil.Raycast(_driverPawn, from, from + dir *
   def.RangeMeters)` (pattern at line 4863), tracer via `ArenaVfx.SpawnShot(_arenaWorld, from,
   impactPos, fromPlayer: true, hit: hit.Hit)` (line 4905), fire SFX via `PlaySfx3D` (line 4875 —
   Stage 1 can reuse the existing MG fire sample).
5. **Damage**: `damage = BaseDamage * (0.85 + rand*0.30)`, then if the hit lands on the enemy
   VEHICLE apply `VehicleDamageMultiplier` and route through the existing locational path
   `OnHit(isPlayer: true, damage, hit, ammoDef: null)` (call site pattern at line 4924) so section
   armor, driver overflow through destroyed sections, and the damage log all behave identically.
   This is what makes the sidearm a *finisher* with zero new damage code.
6. **Ammo**: decrement the in-memory personal pool (mirror of `PersonalAmmoInventory` loaded at
   encounter start next to the driver vitals at lines 4031-4034); on empty play the `"click"` SFX +
   log (pattern at lines 4748-4751). Commit consumed ammo once at resolve time alongside driver
   HP/AP — extend `GameSession.ResolveArenaEncounterRealtime` (signature at `GameSession.cs` lines
   205-212) / `SessionEncounters` with the post-match pool (single-commit pattern per CODEMAP).
7. **Cooldown**: plain `_personalWeaponCooldown` float ticked in `_Process`; no interaction with
   vehicle slot cooldowns.
8. **HUD**: add a compact third row to `PlayerStatusHud` (`Scripts/UI/PlayerStatusHud.cs`) —
   equipped weapon name + `AMMO n/cap`, visible only while `IsPlayerOnFoot()` (the
   `VehicleStatusHud` already hides on foot, so the top-right column has room).
9. **AI response**: none needed — enemies already prosecute the on-foot driver (audit above).

### Stage 2 — store variety + armor tiers

- `DriverStoreView.Rebuild()` (line 163): add a `"PERSONAL WEAPONS"` section between the
  cybernetics loop (ends line 191) and the `"BODY ARMOR"` section (line 193). New
  `AddPersonalWeaponRow` mirrors `AddArmorRow` (line 300): stat line
  `"{class role} · {dmg} dmg · {range} m · {cooldown} ms"`, flavor line, Buy/Equip button, plus an
  ammo sub-row (`Buy 10 — ${AmmoPricePer10Usd}`, capped at `AmmoCapacity`).
- `SessionStore`: `TryBuyPersonalWeapon` / `TryEquipPersonalWeapon` / `TryBuyPersonalAmmo`
  following the `TryBuyDriverArmor` shape (line 308) — validate def + price, `_ctx.Replace(...
  with ...)`, status line. Owned weapons are kept (list), unlike the vest trade-in single slot:
  swapping between owned personal weapons is free at the store (open question 3).
- Armor tiers: **already live** — the two new vest JSONs need no code.

### Stage 3 — in progress (bail-outs SHIPPED 2026-07-22, build 186)

- **DONE — AI bail-out duels**: tier-3+ mobility-killed enemies bail out (red hostile identity
  ring, tier-appropriate sidearm: 9mm at t3 / SMG at t4+), strafe-jink at pistol range, and
  return fire (chip vs the player's hull, full damage vs the player on foot). All player damage
  paths route to their vest→HP pool: vehicle guns at full damage, personal weapons at full
  driver damage, run-downs at speed. Below ~25% HP they SURRENDER (same win, no forced
  execution); driver kill or surrender both leave the hull salvage-whole. Tiers 1-2 keep the
  onboarding surrender. Probe: `--shot=bailout` (stages tier-3 city + forces the mobility kill).
- Remaining stage-3 backlog:
  - Per-weapon fire/impact SFX + muzzle flash via a `personal_weapon_visuals.json` config store
    (personal fire currently reuses the MG sample at reduced volume).
  - Fire-while-moving animation blend on the Mixamo avatar (`driver_pawn.json` `avatar` block).
  - On-foot fire during the post-win salvage phase and overworld events (combat-phase only).
  - Duelist weapon pickup from killed/surrendered drivers (loot hook).
  - Exo-frame armor tier with on-foot move-speed/carry bonuses (`MassKg` becomes meaningful).

---

## Integration checklist (edits to EXISTING files — deliberately not done in this pass)

1. `Scripts/Core/IO/DefLoader.cs` — `LoadAll()` (line 35): add
   `var personalWeapons = LoadCategory<PersonalWeaponDefinition>($"{rootPath}/PersonalWeapons", messages, "PersonalWeapons");`
   after line 46; pass into the `DefDatabase` initializer (lines 52-62) and extend `LoadResult`
   with a `PersonalWeaponCount` (defaulted param, keeps existing call sites compiling). Add a
   `ValidatePersonalWeapons` pass: `BaseDamage > 0`, `CooldownMs >= 60`, `RangeMeters > 0`,
   `PelletsPerShot >= 1`, `VehicleDamageMultiplier` in (0, 1], non-empty `AmmoId`.
2. `Scripts/Core/IO/DefDatabase.cs` — add
   `public IReadOnlyDictionary<string, PersonalWeaponDefinition> PersonalWeapons { get; init; } = new Dictionary<string, PersonalWeaponDefinition>();`.
3. `Scripts/Core/State/PlayerProfileState.cs` — the three additive fields from Stage 1.
4. `Scripts/Game/Session/SessionStore.cs` — the three Try* methods from Stage 2.
5. `Scripts/UI/DriverStoreView.cs` — `Rebuild()` personal-weapons section + `AddPersonalWeaponRow`.
6. `Scripts/UI/ArenaRealtimeView.cs` — fire-gate branch (line 795 area), `TryFirePersonalWeapon()`,
   personal ammo runtime mirror + resolve-time commit, HUD hookup.
7. `Scripts/UI/PlayerStatusHud.cs` — on-foot weapon/ammo row.
8. `Docs/PROJECT_STATE.md`, `CHANGELOG.md`, `VERSION.txt` — build-session bookkeeping when the
   runtime lands.

## Open design questions

1. **Should personal fire be allowed during the post-win salvage phase?** Combat is over; firing at
   a surrendered/disabled hull could grief the salvage value. Current plan: combat phase only.
2. **Ammo restock between tournament rounds** — the pit crew restocks vehicle ammo; personal ammo
   probably should NOT restock (it's your pocket, not the rig), reinforcing "last resort."
3. **Free weapon swaps at the store vs a small gunsmith fee** — free keeps friction low; a fee
   ($25?) would match the game's "every service costs" fiction.
4. **Does `armor_kevlar_basic` migrate into the DriverUpgrades schema?** Unifying would delete the
   legacy `ArmorDefinition` fallback paths (`SaveMigration.cs` line 67, `DriverStoreView` line 178)
   but touches save compatibility — recommend a dedicated cleanup pass, not a rider on this feature.
5. **Should vest tier scale armor repair cost?** Field repair is currently a flat
   `GameBalance.DriverArmorRepairCostPerPointUsd = 3` (line 203); an exo plate repairing at kevlar
   prices is generous. Cheap fix: per-point cost = `max(3, PriceUsd / ArmorPoints / 8)`.
