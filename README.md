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
> into a plugin. This plugin covers **every base-game Rust vehicle** and is
> fully **config-driven**, so any custom prefab path you own can be added as a
> new entry without touching code.

## Features

- **All-in-one, Oxide-only** — one `Vehicles.cs`, no Carbon required.
- **22 base-game vehicles** out of the box (see list below).
- **Per-vehicle permission, price, cooldown and fuel.**
- **Economy** via **Economics** (coins) or **ServerRewards** (RP) — both
  optional; free if neither is installed.
- **Ownership** — one of each type per player, tracked across restarts;
  optional owner-only mounting.
- **Recall / remove / where** management with loot-safety, building-blocked,
  water and max-vehicle checks.
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
| `/vehicle` / `/vehicle help` | List vehicles you can spawn, with price & cooldown |
| `/vehicle <name>` | Spawn a vehicle |
| `/mini`, `/car`, `/boat`, `/horse`, … | Per-vehicle spawn shortcuts |
| `/vehicle recall <name>` | Teleport your vehicle back to you |
| `/vehicle remove <name>` | Despawn your vehicle |
| `/vehicle where <name>` | Distance, compass direction and grid of your vehicle |

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
"minicopter": {
  "Enabled": true,
  "Display name": "Minicopter",
  "Permission suffix (vehicles.<suffix>)": "minicopter",
  "Spawn commands": [ "mini", "minicopter" ],
  "Prefab": "assets/content/vehicles/minicopter/minicopter.entity.prefab",
  "Price (0 = free)": 500.0,
  "Currency (Economics or ServerRewards)": "Economics",
  "Cooldown in seconds": 600.0,
  "Low grade fuel to add on spawn": 50,
  "Spawn distance in front of player": 4.0,
  "Requires water to spawn": false
}
```

Add new vehicles by copying an entry, giving it a unique key, and setting the
`Prefab` path + `Spawn commands`. Reload with `oxide.reload Vehicles`.

## Notes

- Prefab paths target current Rust builds. If Facepunch renames a prefab, set
  the new path in the config — no code change needed.
- Economy and Discord are optional and resolved at runtime.

## License

Provided as-is for use on your own Rust server. Modify freely.
