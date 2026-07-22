WASTELAND SURVIVOR — MASTER GAME SPEC (Ultimate Target)

High concept
- Single-player (v1) top-down/isometric vehicular-combat RPG/sandbox inspired by Origin’s AutoDuel.
- Core fantasy: build/customize armed vehicles, fight AI opponents (arena + open world), salvage/tow wrecks, grow your garage and character over time.
- Future-ready for multiplayer, quests, and expanded world, but v1 is single-player.

Setting & world
- Semi–post-apocalyptic Earth with fragmented law; cities retain basic infrastructure.
- Detroit is the starting/main city. Full map ultimately contains 8 cities (major cities within a few hundred miles: e.g., Chicago/Pittsburgh/Cleveland/Saginaw range), positioned roughly like real life.
- Pre-generated world with roads between cities; side roads to gas/electric stations, buildings, and abandoned structures containing salvage.
- “Road Closed” barriers used to gate expansion during early versions.

City model & progression
- Each city has a Store and Garage/Vehicle Storage; some cities also have an Arena with tournaments (AutoDuel-style).
- In v1 and likely forever: city interaction is menu-based (buttons/menus to enter Store/Garage/Arena), not free-roam city driving.
- Roaming merchants exist in the overworld; interacting uses the same store UI as cities.
- Economy uses USD. Income sources: salvage and arena winnings (later: casino in some cities).

Player character & cloning
- Player has health (HP). HUD always shows player HP as bar + numeric; cybernetic upgrades can increase max HP.
- Character is “cloneable”: if killed, player respawns as a clone at the last facility where memory was uploaded (v1 may simplify respawn, but design targets this).
- Player retains money and any vehicles stored in garages across cities after death.
- Vehicle recovery gameplay: AI (and player) can salvage/tow defeated vehicles; player may intercept and reclaim their vehicle after respawn.

Core gameplay pillars
1) Vehicular combat (primary)
2) Deep vehicle customization (weapons/armor/engine/targeting computer/tires/cargo/towing)
3) Salvage, towing, logistics (weight, cargo capacity, trailers, chained towing)
4) Arena tournaments + overworld travel/encounters
5) Expandable systems for later quests and multiplayer

Vehicles & customization
- Vehicles have classes: motorcycle, compact, subcompact, sedan, sports, light truck, heavy truck, SUV, semi truck, light tank, heavy tank (tanks likely later).
- Engines are class-constrained (e.g., small engines fit most, larger engines require larger classes).
- Fuel types: electric, gasoline, diesel. Cities refuel/recharge; stations exist on roads between cities.
- Vehicles have storage capacity for ammo, spare fuel, salvaged parts/gear, and cargo. Cargo weight impacts acceleration/top speed.
- Trailers can be added; trailers are treated like vehicles (locational damage, weight, cargo).
- Players can tow trailers and defeated vehicles; towing can be chained (truck → trailer → salvaged vehicle, etc.) if engine power can move the total weight.

Damage model (vehicle + driver)
- Locational vehicle damage: front, rear, left, right, top, bottom/undercarriage, and each tire (supports variable tire counts).
- Each section has armor points: base armor + upgrades. More armor adds weight and reduces speed/acceleration.
- When armor in a section reaches zero, further damage to that area can injure the driver (especially rear/sides/top/bottom). Drivers can be killed while the vehicle remains salvageable.
- Tires have armor; tougher tires weigh more. Losing a tire should impact drivability (preferably hard-but-possible control; fallback is “undrivable until repaired”).
- Only road-side repair in the field: replace a tire with a spare. Requires exiting vehicle and interacting at the wheel; other repairs require a garage.

Weapons, targeting computer, and ammo
- Weapons mount to locations: front/rear/left/right/top; some vehicles have multiple mounts.
- Weapon mounting constraints: certain weapons only allowed on certain mount locations; some are fixed direction, some have limited arc, top mounts may have 360°.
- Targeting computer governs:
  - how many weapons can be controlled/active at once,
  - whether mounted weapons can auto-track a locked target.
  - If computer is insufficient, some mounts must be fixed angle rather than auto-tracking.
- Weapon types include:
  - dumb-fire weapons (fixed direction),
  - tracking missiles (lock-on),
  - droppers: oil slick and smoke screen (rear-deploy systems),
  - mines (requires underside armor relevance).
- Ammo can have multiple types; ammo type + projectile velocity affect damage.
- Armor types exist; some armor types can negate certain ammo types (design for counters/rock-paper-scissors).
- HUD in vehicle shows: speed, locational armor, mounted weapons by location, ammo, vehicle total weight, total towed weight, etc.

On-foot gameplay (secondary)
- Player can exit vehicles and enter other vehicles (including salvaged enemy vehicles if the driver is killed and the vehicle is usable).
- Baseline on-foot verbs: enter/exit, tire replace, loot/salvage, attach tow, hijack drivable wrecks.
- Design intent: on-foot is HIGH-RISK UTILITY, not a shooter. Personal weapons exist as last-resort defense while exposed and as a finishing tool against crippled vehicles/exposed drivers; they must never out-damage vehicle mounts or make fighting on foot a preferred strategy.

Personal weapons (driver-carried)
- Single equipped personal-weapon slot on the driver (mirrors the single armor-vest slot). A fresh clone always wakes with at least the basic 9mm sidearm — the player is never unarmed outside the vehicle.
- Sold at the Driver Store (“Clinic & Outfitter”) alongside body armor and cybernetics. Personal ammo is bought there too and is carried on the driver: per-person ammo pools, separate from any vehicle’s ammo stores.
- Weapon classes and combat roles:
  - Sidearm (9mm pistol): starter/backup. Modest damage, moderate range, forgiving cooldown. The “always armed” guarantee.
  - SMG (9mm): close-range sustained spray. Highest on-foot DPS but short range and wide spread; still below any vehicle-mounted MG.
  - Shotgun (12-gauge): point-blank multi-pellet burst. Devastating inside ~12 m, useless beyond; the tire-swap ambush deterrent.
  - Hunting rifle: long-range single shots. Best per-shot punch and reach, slow cooldown; the deliberate finisher.
- Personal fire against vehicles is chip damage (per-weapon vehicle-damage multiplier); its real value is finishing already-stripped sections and injuring drivers through destroyed sections via the normal overflow rules.
- Aiming model (keyboard-first, top-down): auto-aim at the locked/nearest target when it is inside the weapon’s range and the driver’s facing cone; otherwise shots travel down the facing line. No mouse aiming.

Personal armor (driver-worn)
- Single equipped vest slot providing driver Armor Points (AP): a buffer that absorbs driver damage before HP (in and out of the vehicle).
- Kevlar ladder sold at the Driver Store with old-vest trade-in: Basic Kevlar 50 AP (free default) → Heavy Kevlar 58 AP → Composite Vest 65 AP → Assault Rig 85 AP → Exo Plate Carrier 110 AP.
- Later: a powered exo-frame armor tier that also buffs on-foot speed/carry weight (post-v1; pairs with cybernetics at clone facilities).
- Ammo-vs-armor counters stay a vehicle-side system; personal armor remains a simple AP pool by design.

Clone / driver-kill interaction (on-foot)
- Driver HP/AP applies identically in and out of the vehicle; on foot the driver just has no hull between themselves and the guns.
- Dying on foot is a normal driver death: clone respawn at the last upload facility, clone fee, vehicle left where it stood (capture/reclaim rules apply).
- Equipped vest, equipped personal weapon, and carried personal ammo persist through cloning — they are profile equipment, not cargo.
- Killing an enemy driver who has bailed out on foot (later, once AI bail-outs exist) counts as a driver kill: the hull stays intact for salvage, making the personal-weapon finisher the cleanest way to take a vehicle whole.

AI goals
- AI should feel like players (at least some opponents): fight, salvage, tow, sell/repair, and use the same rules.
- If AI defeats the player, they attempt salvage/towing like a player would—creating opportunities for the player to intercept and reclaim assets.

Visual / presentation goals
- Mostly 2D feel but acceptable to implement in 3D with locked isometric camera.
- Should look reasonably modern; not intentionally retro, but “slightly old school” is acceptable.
- Vehicles should show visible damage per side/section (light damage at high armor %, severe damage/near-destruction at low armor %, possible fire/smoke effects).

Non-negotiables (identity of the game)
- AutoDuel-style vehicle combat + tournaments
- Locational damage with driver-kill possibility
- Deep customization constrained by weight, storage, engines, and targeting computer
- Salvage + towing (including chained towing) as a core progression mechanic
- Clone-based respawn tied to facilities (design target even if simplified early)
