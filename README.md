# Vehicles

An all-in-one **personal vehicle** plugin for the game **Rust** (Facepunch),
built for the **Oxide/uMod** framework. Single file, no hard dependencies.

It lets players spawn, recall, locate and despawn their own vehicles, gated by
permissions, prices and cooldowns — with **Discord webhook logging** and a
**public API + hooks** so the wider "compatible plugin" ecosystem (shops,
detectors, admin tools, etc.) can integrate with it.

> **Note on Karuza's custom vehicles:** the cars/planes/UFOs/hovercraft sold at
> karuza.dev are proprietary 3D **asset bundles**. A plugin can only spawn
> prefabs that exist in the game/server, so those exact models can't be baked
> into a plugin. Instead this plugin gives you a **custom-variant framework**:
> take any base-game chassis and reskin/retune it (toughness, speed, fuel,
> no-decay, owner-lock, custom name, price tier) to create vehicles that *feel*
> custom — and it's fully config-driven, so you can add as many variants as you
> like (see "Custom variants & reaching 157" below).

## Features

- **All-in-one, Oxide-only** — one `Vehicles.cs`, no Carbon required.
- **157-vehicle catalog** out of the box — base vehicles, themed showcase
  variants, and an auto-generated set of tiered variants (Stock, Sport, Racing,
  Armored, Heavy, Hauler, Hunter, VIP, Elite, Ghost) across every chassis.
- **Custom-variant framework** — per-vehicle **skin**, **health/toughness**,
  **speed/handling** (best-effort), **no-decay** and **owner-lock** modifiers.
- **Multi-ownership** — own **many** vehicles at once, including multiple of
  the same type (configurable cap, default 25; `0` = unlimited).
- **Experimental drivable Bradley** — spawn and *drive* the real Bradley tank.
- **Per-vehicle permission, price, cooldown and fuel.**
- **Economy** via **Economics** (coins) or **ServerRewards** (RP) — both
  optional; free if neither is installed.
- **Recall / remove / removeall / where** management (acts on your nearest
  vehicle of a type) with loot-safety, building-blocked, water and cap checks.
- **Discord webhook logging** of spawns, recalls, removals, denials and API
  actions (throttled to avoid rate limits).
- **Public API + hooks** for compatible plugins.
- **Server console / RCON commands** for testing and admin spawning.
- Fully **configurable** and **localized**.

## Vehicles included

minicopter · scrap transport heli · attack heli · sedan · 2/3/4-module cars ·
rowboat · RHIB · tugboat · kayak · solo & duo submarines · hot air balloon ·
ridable horse · snowmobile · tomaha snowmobile · pedal bike · motorbike ·
motorbike + sidecar · magnet crane* · work cart*

`*` magnet crane and work cart ship **disabled** (crane needs open ground, work
cart needs rails) — enable them in the config if you want them.

**Themed variants** (same chassis, different feel): Sport Minicopter, Armored
Minicopter, Gunship, War Helicopter, Sport Sedan, Monster Car, Hauler,
Superbike, War Horse, Racing Snowmobile, Speedboat, Yacht, Attack Submarine.

**Premium showcase vehicles** (`/apache`, `/motorhome`, `/bradley`):

| Vehicle | Built on | Notes |
| --- | --- | --- |
| **Apache Gunship** | Attack helicopter | Armored, owner-locked, no-decay — a stand-in for a Karuza-style custom Apache (his exact model is a proprietary asset). |
| **Motorhome** | 4-module car (storage modules) | Tough RV with onboard storage, no-decay. Stand-in for a custom motorhome. |
| **Bradley Combat** | **Real** `bradley_apc` tank | Spawns the genuine game Bradley. **Drivable** is enabled on this entry (experimental). **Shipped disabled** — set `"Enabled": true` in config to use it. |

> Apache and Motorhome reuse base chassis because Karuza's actual 3D models are
> paid asset bundles a plugin can't contain. Bradley uses the real in-game
> entity, so it's the genuine tank.

### Drivable Bradley (experimental)

The `bradley` entry has `"Drivable": true`. When you spawn it, the plugin
disables the tank's AI, parents a **driver seat** to it, and reads the seated
player's **W/S** (forward/back) and **A/D** (turn) input to drive it. Set
`"Drivable": false` to instead get a tame, parked tank (AI off, non-hostile).

This is a **first pass that needs on-server tuning** — drive force, turn speed
and the seat position are the parts most likely to need adjustment once you
test it on your build. Main-cannon firing under player control is **not** wired
up yet (it's a significant follow-up); right now you drive it, you don't shoot
from it. Load it, try it, and paste any console output so it can be tuned.

## Installation

1. Run [Oxide/uMod](https://umod.org/games/rust) on your Rust server.
2. Drop `Vehicles.cs` into `oxide/plugins/`.
3. A default config is written to `oxide/config/Vehicles.json`.
4. (Optional) Install **Economics** and/or **ServerRewards** to charge for
   vehicles, and set a Discord webhook URL in the config to enable logging.

## Player commands

Main command: `/vehicle` (configurable).

| Command | Description |
| --- | --- |
| `/vehicle` / `/vehicle help` | List featured vehicles + a count of the rest |
| `/vehicle catalog [filter]` | List **every** vehicle you can spawn (optionally filtered, e.g. `/vehicle catalog car`) |
| `/vehicle <name>` | Spawn a vehicle (any catalog key, e.g. `/vehicle car_armored`) |
| `/mini`, `/car`, `/boat`, `/horse`, … | Per-vehicle spawn shortcuts (featured vehicles only) |
| `/vehicle recall <name>` | Recall your **nearest** vehicle of that type |
| `/vehicle remove <name>` | Despawn your **nearest** vehicle of that type |
| `/vehicle removeall` (or `/vehicle remove all`) | Despawn **all** your vehicles |
| `/vehicle where <name>` | Distance, compass direction and grid of your nearest of that type |

Generated catalog vehicles (e.g. `minicopter_sport`, `car_armored`,
`rhib_elite`) don't have chat shortcuts — spawn them with `/vehicle <key>`.
Find keys with `/vehicle catalog` or the console `vehicles.list`.

## Server console / RCON commands

Run these from the server console or any RCON tool (RustAdmin, WebRcon, etc.):

| Command | Description |
| --- | --- |
| `vehicles.list` | Print every configured vehicle (key, enabled, price, prefab) |
| `vehicles.give <steamId\|name> <vehicleKey>` | Spawn a vehicle next to an **online** player (free) — great for testing |

Example: `vehicles.give 76561198000000000 minicopter`

## Permissions

| Permission | Grants |
| --- | --- |
| `vehicles.admin` | Free spawns, no cooldowns, can mount anyone's vehicle |
| `vehicles.<suffix>` | One per vehicle (suffix comes from each vehicle's config) |

```
oxide.grant group default vehicles.minicopter
oxide.grant user "76561198000000000" vehicles.admin
```

## Discord logging

Set a webhook under `Discord logging` in the config:

```json
"Discord logging": {
  "Webhook URL (leave empty to disable)": "https://discord.com/api/webhooks/...",
  "Bot username": "Vehicles",
  "Avatar URL (optional)": "",
  "Log spawns": true,
  "Log recalls": true,
  "Log removals": true,
  "Log denials (no permission / cooldown / cannot afford)": false,
  "Log admin and API actions": true
}
```

Each event is posted as a rich embed (player name, SteamID, vehicle, grid,
cost). Messages are queued and sent ~1 every 2s so a busy server never trips
Discord's rate limit.

## Public API (for shops, detectors, admin tools, …)

Other plugins can reference this plugin and call:

```csharp
[PluginReference] private Plugin Vehicles;

bool   isOurs   = Vehicles.Call<bool>("IsVehicle", entity);
string key      = Vehicles.Call<string>("GetVehicleType", entity);
ulong  ownerId  = Vehicles.Call<ulong>("GetVehicleOwnerId", entity);
var    owned    = Vehicles.Call<Dictionary<string, ulong>>("GetOwnedVehicles", userId);
string display  = Vehicles.Call<string>("GetVehicleDisplayName", key);
var    spawned  = Vehicles.Call<BaseEntity>("SpawnVehicleForPlayer", player, key, true);
bool   removed  = Vehicles.Call<bool>("DespawnVehicle", entity);
```

`SpawnVehicleForPlayer` is what a **vehicle shop** would call to deliver a
purchase; `IsVehicle` / `GetVehicleOwnerId` / `GetVehicleType` are what a
**detector** or **admin tool** would use to identify and act on vehicles.

## Hooks (subscribe from your own plugin)

```csharp
// Return non-null to BLOCK a spawn.
object CanSpawnVehicle(BasePlayer player, string key)

void OnVehiclesVehicleSpawned(BaseEntity entity, BasePlayer player, string key)
void OnVehiclesVehicleRecalled(BaseEntity entity, BasePlayer player, string key)
void OnVehiclesVehicleRemoved(BaseEntity entity, BasePlayer player, string key)
```

## Configuration

A default `oxide/config/Vehicles.json` is generated on first load. Each vehicle
entry looks like:

```json
"sportmini": {
  "Enabled": true,
  "Display name": "Sport Minicopter",
  "Permission suffix (vehicles.<suffix>)": "sportmini",
  "Spawn commands": [ "sportmini" ],
  "Prefab": "assets/content/vehicles/minicopter/minicopter.entity.prefab",
  "Price (0 = free)": 900.0,
  "Currency (Economics or ServerRewards)": "Economics",
  "Cooldown in seconds": 700.0,
  "Low grade fuel to add on spawn": 75,
  "Spawn distance in front of player": 4.0,
  "Requires water to spawn": false,
  "Skin ID (0 = none)": 0,
  "Health multiplier (1 = default toughness)": 1.2,
  "Speed multiplier (best-effort, 1 = default)": 1.5,
  "Protect from decay": false,
  "Lock to owner (overrides global owner-only mount)": false,
  "Disable AI (for NPC-based entities like Bradley)": false,
  "Drivable (experimental, Bradley only)": false
}
```

Top-level options include `Maximum vehicles a player may own at once
(0 = unlimited)` (default **25**) and `Allow owning more than one of the same
vehicle type` (default **true**) — these control the multi-ownership behaviour.

Add new vehicles by copying an entry, giving it a unique key, and setting the
`Prefab` path + `Spawn commands`. Reload with `oxide.reload Vehicles`.

### The 157 catalog

The default config now ships **157 vehicles**: the curated base + showcase
vehicles, plus an auto-generated set of tiered variants — every chassis
(minicopter, attack heli, car, rhib, submarine, horse, …) crossed with ten
tiers (Stock, Sport, Racing, Armored, Heavy, Hauler, Hunter, VIP, Elite, Ghost),
each with its own permission, price and stat profile. Every entry is just a
base chassis (`Prefab`) plus modifiers:

| Modifier | Effect |
| --- | --- |
| `Skin ID` | Applies a workshop/item skin where the chassis supports it |
| `Health multiplier` | Tougher or more fragile (e.g. `2.5` = armored) |
| `Speed multiplier` | Best-effort speed/handling tweak (see note) |
| `Protect from decay` | Vehicle never decays |
| `Lock to owner` | Only the owner (and admins) can mount it |
| `Price` / `Cooldown` | Tier the variant for your economy |

Edit, delete or add to these freely — there's no limit on how many you define.

> **Speed note:** speed/handling is applied best-effort by probing common
> vehicle fields via reflection; it affects most ground/water vehicles well,
> but some chassis (notably helicopters) expose no simple speed value, so the
> multiplier may be a no-op there. Toughness, fuel, skin, no-decay and
> owner-lock always apply.

## Notes

- Prefab paths target current Rust builds. If Facepunch renames a prefab, set
  the new path in the config — no code change needed.
- Economy and Discord are optional and resolved at runtime.

## License

Provided as-is for use on your own Rust server. Modify freely.
