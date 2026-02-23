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
- v1 focus: on-foot is utility (enter/exit, tire replace, loot/salvage, attach tow). Later: character weapons/armor/clothing via “driver store.”

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
