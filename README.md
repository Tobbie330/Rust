# Vehicles

A personal-vehicle plugin for the game **Rust** (Facepunch), built for the
**Oxide/uMod** modding framework. Because Carbon implements the Oxide API,
the same compiled plugin also runs on **Carbon** servers — no changes needed.

It lets players spawn, recall, locate and despawn their own vehicles
(minicopters, cars, boats, submarines, horses and more) gated by
permissions, prices and cooldowns. The design mirrors the popular custom
vehicle systems such as the ones at [karuza.dev](https://karuza.dev/):
ownership tracking, economy integration, a vehicle locator and loot-safety
checks.

## Features

- **11 ready-to-use vehicles** out of the box — minicopter, scrap transport
  heli, attack heli, sedan, modular car, rowboat, RHIB, solo submarine, hot
  air balloon, ridable horse and snowmobile.
- **Per-vehicle permissions** — `vehicles.<suffix>` controls who may spawn
  each vehicle.
- **Per-vehicle price & cooldown** — charge in **Economics** coins or
  **ServerRewards** RP (both optional; if neither is installed, spawns are
  free).
- **Ownership** — one of each vehicle type per player, tracked across server
  restarts. Optionally restrict mounting to the owner.
- **Recall** — teleport your idle vehicle back to you.
- **Locate** (`where`) — get distance, compass direction and map grid of your
  vehicle, similar to a "vehicle detector".
- **Despawn** (`remove`) — clear a vehicle you no longer want.
- **Loot safety** — recall/remove refuse to act on a vehicle that still has
  items in its storage, so you never lose gear.
- **Automatic fuel** — vehicles spawn with a configurable amount of low grade
  fuel.
- **Building-blocked, water and max-vehicle checks.**
- **Admin bypass** — `vehicles.admin` spawns for free and ignores cooldowns.
- Fully **configurable** and **localized** (lang file).

## Installation

1. Make sure your server runs [Oxide/uMod](https://umod.org/games/rust) or
   [Carbon](https://carbonmod.gg/).
2. Copy `Vehicles.cs` into your server's `oxide/plugins/` folder
   (`carbon/plugins/` on Carbon).
3. The plugin compiles and loads automatically and writes a default config to
   `oxide/config/Vehicles.json`.
4. (Optional) Install **Economics** and/or **ServerRewards** if you want to
   charge for vehicles.

## Commands

The main command is `/vehicle` (configurable). Every vehicle also has its own
shortcut commands.

| Command | Description |
| --- | --- |
| `/vehicle` or `/vehicle help` | List the vehicles you can spawn, with price & cooldown |
| `/vehicle <name>` | Spawn a vehicle (e.g. `/vehicle minicopter`) |
| `/mini`, `/car`, `/boat`, `/horse`, … | Per-vehicle spawn shortcuts |
| `/vehicle recall <name>` | Teleport your vehicle back to you |
| `/vehicle remove <name>` | Despawn your vehicle |
| `/vehicle where <name>` | Show distance, direction and grid of your vehicle |

`<name>` accepts either the config key (`minicopter`) or any of the vehicle's
command aliases (`mini`).

## Permissions

| Permission | Grants |
| --- | --- |
| `vehicles.admin` | Free spawns and no cooldowns (if *Admin bypass* is on); can mount anyone's vehicle |
| `vehicles.minicopter` | Spawn the minicopter |
| `vehicles.sedan` | Spawn the sedan |
| `vehicles.rowboat` | Spawn the rowboat |
| `vehicles.<suffix>` | One per configured vehicle — the suffix comes from each vehicle's config |

Grant with, e.g.:

```
oxide.grant group default vehicles.minicopter
oxide.grant user "76561198000000000" vehicles.admin
```

## Configuration

A default `oxide/config/Vehicles.json` is generated on first load. Top-level
options:

| Option | Default | Meaning |
| --- | --- | --- |
| `Main chat command` | `vehicle` | The primary command name |
| `Message prefix` | `[Vehicles]` | Prefix shown before chat messages |
| `Maximum vehicles a player may own at once (0 = unlimited)` | `3` | Cap on simultaneously owned vehicles |
| `Block spawning while building blocked` | `true` | Prevent spawning in enemy bases |
| `Only the owner (and admins) may mount their vehicle` | `false` | Lock vehicles to their owner |
| `Refuse to despawn/recall vehicles that still contain items` | `true` | Loot-safety guard |
| `Players with the admin permission spawn for free and ignore cooldowns` | `true` | Toggle admin bypass |
| `Vehicles` | (11 entries) | Per-vehicle settings |

Each entry under `Vehicles` supports:

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

To add a new vehicle, copy an entry, give it a unique key, set the `Prefab`
path, choose a `Permission suffix` and `Spawn commands`, then reload the
plugin.

## Localization

All player-facing text lives in `oxide/lang/en/Vehicles.json` and can be
edited or translated to other languages.

## Notes

- Vehicle prefab paths target current Rust builds. If Facepunch renames a
  prefab in a future update, set the new path in the config — no code change
  required.
- Economy integration is optional and resolved at runtime; the plugin works
  with neither, either, or both Economics and ServerRewards installed.

## License

Provided as-is for use on your own Rust server. Modify freely.
