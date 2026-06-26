using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Vehicles", "Tobbie", "4.0.1")]
    [Description("All-in-one Rust vehicle system: a 157-vehicle catalog of base + custom-variant vehicles with permissions, prices, cooldowns, multi-ownership, stat modifiers, an experimental drivable Bradley, Discord logging and a public API.")]
    public class Vehicles : RustPlugin
    {
        [PluginReference] private Plugin Economics, ServerRewards;

        private const string PermAdmin = "vehicles.admin";
        private const int LowGradeFuelId = -946369541; // lowgradefuel
        private const int ColorGreen = 3066993, ColorBlue = 3447003, ColorOrange = 15105570, ColorRed = 15158332, ColorPurple = 10181046;

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static double Now => (DateTime.UtcNow - Epoch).TotalSeconds;
        private static readonly object[] EmptyArgs = new object[0];

        private Configuration config;
        private StoredData storedData;

        private readonly Dictionary<string, string> commandToKey = new Dictionary<string, string>();
        private readonly Dictionary<ulong, OwnedVehicle> vehiclesByNetId = new Dictionary<ulong, OwnedVehicle>();

        private readonly Queue<string> discordQueue = new Queue<string>();
        private static readonly Dictionary<string, string> DiscordHeaders = new Dictionary<string, string> { ["Content-Type"] = "application/json" };

        // Best-effort speed/handling fields probed via reflection across vehicle types (no-ops if absent).
        private static readonly string[] SpeedFields = { "engineThrust", "engineThrustMax", "engineForce", "engineForceMax", "steerForce", "steeringScale", "thrust", "topSpeed", "maxSpeedFwd", "moveForceMax", "torqueScale" };

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Main chat command")]
            public string Command = "vehicle";

            [JsonProperty("Message prefix")]
            public string Prefix = "<color=#5dade2>[Vehicles]</color> ";

            [JsonProperty("Maximum vehicles a player may own at once (0 = unlimited)")]
            public int MaxVehicles = 25;

            [JsonProperty("Allow owning more than one of the same vehicle type")]
            public bool AllowMultiplePerType = true;

            [JsonProperty("Block spawning while building blocked")]
            public bool BlockWhenBuildingBlocked = true;

            [JsonProperty("Only the owner (and admins) may mount their vehicle")]
            public bool OwnerOnlyMount = false;

            [JsonProperty("Refuse to despawn/recall vehicles that still contain items")]
            public bool ProtectVehiclesWithLoot = true;

            [JsonProperty("Players with the admin permission spawn for free and ignore cooldowns")]
            public bool AdminBypass = true;

            [JsonProperty("Discord logging")]
            public DiscordSettings Discord = new DiscordSettings();

            [JsonProperty("Vehicles")]
            public Dictionary<string, VehicleSettings> Vehicles = DefaultVehicles();

            public static Dictionary<string, VehicleSettings> DefaultVehicles()
            {
                var dict = new Dictionary<string, VehicleSettings>
                {
                    ["minicopter"] = V("Minicopter", "minicopter", "assets/content/vehicles/minicopter/minicopter.entity.prefab", 500, 600, 50, new[] { "mini", "minicopter" }),
                    ["scraptransport"] = V("Scrap Transport Heli", "scraptransport", "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab", 1500, 1200, 100, new[] { "scrapheli" }),
                    ["attackheli"] = V("Attack Helicopter", "attackheli", "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab", 3000, 1800, 100, new[] { "attackheli" }),
                    ["sedan"] = V("Sedan", "sedan", "assets/content/vehicles/sedan_a/sedantest.entity.prefab", 250, 300, 50, new[] { "car", "sedan" }),
                    ["modularcar2"] = V("2-Module Car", "modularcar2", "assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab", 500, 600, 50, new[] { "modcar2" }),
                    ["modularcar3"] = V("3-Module Car", "modularcar3", "assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab", 650, 750, 50, new[] { "modcar3" }),
                    ["modularcar4"] = V("4-Module Car", "modularcar4", "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab", 800, 900, 50, new[] { "modcar", "modcar4" }),
                    ["rowboat"] = Water("Rowboat", "rowboat", "assets/content/vehicles/boats/rowboat/rowboat.prefab", 200, 300, 50, new[] { "boat", "rowboat" }),
                    ["rhib"] = Water("RHIB", "rhib", "assets/content/vehicles/boats/rhib/rhib.prefab", 600, 600, 50, new[] { "rhib" }),
                    ["tugboat"] = Water("Tugboat", "tugboat", "assets/content/vehicles/boats/tugboat/tugboat.prefab", 2000, 1800, 50, new[] { "tug", "tugboat" }),
                    ["kayak"] = Water("Kayak", "kayak", "assets/content/vehicles/kayak/kayak.prefab", 100, 180, 0, new[] { "kayak" }),
                    ["submarinesolo"] = Water("Solo Submarine", "submarinesolo", "assets/content/vehicles/submarine/submarinesolo.entity.prefab", 700, 600, 50, new[] { "sub", "submarine" }),
                    ["submarineduo"] = Water("Duo Submarine", "submarineduo", "assets/content/vehicles/submarine/submarineduo.entity.prefab", 900, 750, 50, new[] { "subduo" }),
                    ["hotairballoon"] = V("Hot Air Balloon", "hotairballoon", "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab", 400, 600, 50, new[] { "hab", "balloon" }),
                    ["ridablehorse"] = V("Ridable Horse", "ridablehorse", "assets/content/vehicles/horse/ridablehorse2.prefab", 150, 300, 0, new[] { "horse" }),
                    ["snowmobile"] = V("Snowmobile", "snowmobile", "assets/content/vehicles/snowmobiles/snowmobile.prefab", 350, 300, 50, new[] { "snowmobile" }),
                    ["tomaha"] = V("Tomaha Snowmobile", "tomaha", "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab", 350, 300, 50, new[] { "tomaha" }),
                    ["pedalbike"] = V("Pedal Bike", "pedalbike", "assets/content/vehicles/bikes/pedalbike.prefab", 50, 120, 0, new[] { "bike", "pedalbike" }),
                    ["motorbike"] = V("Motorbike", "motorbike", "assets/content/vehicles/bikes/motorbike.prefab", 300, 300, 50, new[] { "motorbike" }),
                    ["motorbikesidecar"] = V("Motorbike + Sidecar", "motorbikesidecar", "assets/content/vehicles/bikes/motorbike_sidecar.prefab", 400, 360, 50, new[] { "sidecar" }),

                    // Themed variants
                    ["sportmini"] = Mod(V("Sport Minicopter", "sportmini", "assets/content/vehicles/minicopter/minicopter.entity.prefab", 900, 700, 75, new[] { "sportmini" }), hp: 1.2f, speed: 1.5f),
                    ["tankmini"] = Mod(V("Armored Minicopter", "tankmini", "assets/content/vehicles/minicopter/minicopter.entity.prefab", 1200, 900, 75, new[] { "tankmini" }), hp: 3f, speed: 0.85f, noDecay: true),
                    ["gunship"] = Mod(V("Gunship", "gunship", "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab", 2500, 1500, 120, new[] { "gunship" }), hp: 1.75f),
                    ["warheli"] = Mod(V("War Helicopter", "warheli", "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab", 5000, 2400, 120, new[] { "warheli" }), hp: 2f, noDecay: true, lockOwner: true),
                    ["sportsedan"] = Mod(V("Sport Sedan", "sportsedan", "assets/content/vehicles/sedan_a/sedantest.entity.prefab", 600, 450, 60, new[] { "sportcar" }), hp: 1.2f, speed: 1.4f),
                    ["monstercar"] = Mod(V("Monster Car", "monstercar", "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab", 1500, 1100, 75, new[] { "monster" }), hp: 2.5f, speed: 1.2f, noDecay: true),
                    ["hauler"] = Mod(V("Hauler", "hauler", "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab", 1100, 900, 75, new[] { "hauler" }), hp: 1.6f),
                    ["superbike"] = Mod(V("Superbike", "superbike", "assets/content/vehicles/bikes/motorbike.prefab", 700, 450, 60, new[] { "superbike" }), hp: 0.9f, speed: 1.6f),
                    ["warhorse"] = Mod(V("War Horse", "warhorse", "assets/content/vehicles/horse/ridablehorse2.prefab", 500, 450, 0, new[] { "warhorse" }), hp: 2f, speed: 1.3f),
                    ["racesled"] = Mod(V("Racing Snowmobile", "racesled", "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab", 700, 450, 60, new[] { "racesled" }), hp: 0.9f, speed: 1.5f),
                    ["speedboat"] = Mod(Water("Speedboat", "speedboat", "assets/content/vehicles/boats/rhib/rhib.prefab", 1000, 700, 60, new[] { "speedboat" }), hp: 1.1f, speed: 1.5f),
                    ["yacht"] = Mod(Water("Yacht", "yacht", "assets/content/vehicles/boats/tugboat/tugboat.prefab", 3500, 2400, 60, new[] { "yacht" }), hp: 2f, noDecay: true, lockOwner: true),
                    ["attacksub"] = Mod(Water("Attack Submarine", "attacksub", "assets/content/vehicles/submarine/submarineduo.entity.prefab", 1400, 1000, 60, new[] { "attacksub" }), hp: 1.5f, speed: 1.3f),

                    // Premium showcase vehicles (stand-ins for Karuza-style customs, built on real chassis/entities)
                    ["apache"] = Mod(V("Apache Gunship", "apache", "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab", 6000, 2400, 150, new[] { "apache" }), hp: 2f, speed: 1.1f, noDecay: true, lockOwner: true),
                    ["motorhome"] = Mod(V("Motorhome", "motorhome", "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab", 2500, 1800, 100, new[] { "motorhome", "rv" }), hp: 2.5f, noDecay: true, dist: 8f),

                    // Heavy / special vehicles – disabled by default (crane needs open ground, workcart needs rails).
                    ["magnetcrane"] = Disabled(V("Magnet Crane", "magnetcrane", "assets/content/vehicles/crane_magnet/magnetcrane.entity.prefab", 800, 900, 50, new[] { "crane" })),
                    ["workcart"] = Disabled(V("Work Cart", "workcart", "assets/content/vehicles/trains/workcart/workcart.entity.prefab", 1000, 1200, 50, new[] { "workcart", "train" })),
                };

                // Bradley spawns the REAL game tank. Disabled by default; Drivable = experimental driving, otherwise tamed (AI off).
                var bradley = Mod(V("Bradley Combat", "bradley", "assets/prefabs/npc/m2bradley/bradley_apc.prefab", 10000, 3600, 0, new[] { "bradley", "apc" }), hp: 1f, noDecay: true, lockOwner: true, dist: 8f, disableAI: true);
                bradley.Drivable = true;
                dict["bradley"] = Disabled(bradley);

                FillCatalog(dict, 157);
                return dict;
            }

            private static VehicleSettings V(string name, string perm, string prefab, double price, double cd, int fuel, string[] cmds)
                => new VehicleSettings { DisplayName = name, Permission = perm, Prefab = prefab, Price = price, Cooldown = cd, Fuel = fuel, Commands = cmds };

            private static VehicleSettings Water(string name, string perm, string prefab, double price, double cd, int fuel, string[] cmds)
            {
                var v = V(name, perm, prefab, price, cd, fuel, cmds);
                v.RequiresWater = true;
                return v;
            }

            private static VehicleSettings Disabled(VehicleSettings v) { v.Enabled = false; return v; }

            private static VehicleSettings Mod(VehicleSettings v, ulong skin = 0, float hp = 1f, float speed = 1f,
                bool noDecay = false, bool lockOwner = false, float dist = 4f, bool disableAI = false)
            {
                v.SkinId = skin;
                v.HealthMultiplier = hp;
                v.SpeedMultiplier = speed;
                v.NoDecay = noDecay;
                v.LockToOwner = lockOwner;
                v.SpawnDistance = dist;
                v.DisableAI = disableAI;
                return v;
            }

            // Generates tiered variants on top of the curated entries until the catalog reaches `target` vehicles.
            private static void FillCatalog(Dictionary<string, VehicleSettings> dict, int target)
            {
                var chassis = new[]
                {
                    new Chassis("minicopter", "Minicopter", "assets/content/vehicles/minicopter/minicopter.entity.prefab", 500, 600, 50, false),
                    new Chassis("attackheli", "Attack Heli", "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab", 3000, 1800, 100, false),
                    new Chassis("transportheli", "Transport Heli", "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab", 1500, 1200, 100, false),
                    new Chassis("sedan", "Sedan", "assets/content/vehicles/sedan_a/sedantest.entity.prefab", 250, 300, 50, false),
                    new Chassis("car", "Modular Car", "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab", 800, 900, 50, false),
                    new Chassis("rowboat", "Rowboat", "assets/content/vehicles/boats/rowboat/rowboat.prefab", 200, 300, 50, true),
                    new Chassis("rhib", "RHIB", "assets/content/vehicles/boats/rhib/rhib.prefab", 600, 600, 50, true),
                    new Chassis("submarine", "Submarine", "assets/content/vehicles/submarine/submarineduo.entity.prefab", 900, 750, 50, true),
                    new Chassis("horse", "Horse", "assets/content/vehicles/horse/ridablehorse2.prefab", 150, 300, 0, false),
                    new Chassis("snowmobile", "Snowmobile", "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab", 350, 300, 50, false),
                    new Chassis("motorbike", "Motorbike", "assets/content/vehicles/bikes/motorbike.prefab", 300, 300, 50, false),
                    new Chassis("balloon", "Balloon", "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab", 400, 600, 50, false),
                    new Chassis("kayak", "Kayak", "assets/content/vehicles/kayak/kayak.prefab", 100, 180, 0, true),
                    new Chassis("tugboat", "Tugboat", "assets/content/vehicles/boats/tugboat/tugboat.prefab", 2000, 1800, 50, true),
                    new Chassis("bike", "Bike", "assets/content/vehicles/bikes/pedalbike.prefab", 50, 120, 0, false),
                    new Chassis("sidecar", "Sidecar", "assets/content/vehicles/bikes/motorbike_sidecar.prefab", 400, 360, 50, false),
                };

                var tiers = new[]
                {
                    new Tier("stock", "Stock", 1.0, 1.0f, 1.0f, false, false),
                    new Tier("sport", "Sport", 1.6, 1.1f, 1.4f, false, false),
                    new Tier("racing", "Racing", 2.2, 1.0f, 1.7f, false, false),
                    new Tier("armored", "Armored", 2.5, 2.5f, 0.9f, true, false),
                    new Tier("heavy", "Heavy", 3.0, 3.5f, 0.8f, true, false),
                    new Tier("hauler", "Hauler", 1.8, 1.6f, 0.95f, false, false),
                    new Tier("hunter", "Hunter", 2.0, 1.3f, 1.2f, false, false),
                    new Tier("vip", "VIP", 4.0, 2.0f, 1.3f, true, true),
                    new Tier("elite", "Elite", 5.0, 2.5f, 1.4f, true, true),
                    new Tier("ghost", "Ghost", 3.5, 1.2f, 1.5f, true, true),
                };

                foreach (var c in chassis)
                {
                    foreach (var t in tiers)
                    {
                        if (dict.Count >= target) return;
                        var key = c.Key + "_" + t.Suffix;
                        if (dict.ContainsKey(key)) continue;
                        dict[key] = new VehicleSettings
                        {
                            Enabled = true,
                            DisplayName = t.Adj + " " + c.Name,
                            Permission = c.Key + t.Suffix,
                            Commands = new string[0],
                            Prefab = c.Prefab,
                            Price = Math.Round(c.Price * t.PriceMult),
                            Currency = "Economics",
                            Cooldown = c.Cooldown,
                            Fuel = c.Fuel,
                            RequiresWater = c.Water,
                            HealthMultiplier = t.Hp,
                            SpeedMultiplier = t.Speed,
                            NoDecay = t.NoDecay,
                            LockToOwner = t.Lock
                        };
                    }
                }
            }

            private class Chassis
            {
                public readonly string Key, Name, Prefab;
                public readonly double Price, Cooldown;
                public readonly int Fuel;
                public readonly bool Water;
                public Chassis(string key, string name, string prefab, double price, double cd, int fuel, bool water)
                { Key = key; Name = name; Prefab = prefab; Price = price; Cooldown = cd; Fuel = fuel; Water = water; }
            }

            private class Tier
            {
                public readonly string Suffix, Adj;
                public readonly double PriceMult;
                public readonly float Hp, Speed;
                public readonly bool NoDecay, Lock;
                public Tier(string suffix, string adj, double priceMult, float hp, float speed, bool noDecay, bool lockOwner)
                { Suffix = suffix; Adj = adj; PriceMult = priceMult; Hp = hp; Speed = speed; NoDecay = noDecay; Lock = lockOwner; }
            }
        }

        private class DiscordSettings
        {
            [JsonProperty("Webhook URL (leave empty to disable)")] public string WebhookUrl = "";
            [JsonProperty("Bot username")] public string Username = "Vehicles";
            [JsonProperty("Avatar URL (optional)")] public string AvatarUrl = "";
            [JsonProperty("Log spawns")] public bool LogSpawns = true;
            [JsonProperty("Log recalls")] public bool LogRecalls = true;
            [JsonProperty("Log removals")] public bool LogRemovals = true;
            [JsonProperty("Log denials (no permission / cooldown / cannot afford)")] public bool LogDenials = false;
            [JsonProperty("Log admin and API actions")] public bool LogAdmin = true;
        }

        private class VehicleSettings
        {
            [JsonProperty("Enabled")] public bool Enabled = true;
            [JsonProperty("Display name")] public string DisplayName = "Vehicle";
            [JsonProperty("Permission suffix (vehicles.<suffix>)")] public string Permission = "vehicle";
            [JsonProperty("Spawn commands")] public string[] Commands = new string[0];
            [JsonProperty("Prefab")] public string Prefab = string.Empty;
            [JsonProperty("Price (0 = free)")] public double Price = 0;
            [JsonProperty("Currency (Economics or ServerRewards)")] public string Currency = "Economics";
            [JsonProperty("Cooldown in seconds")] public double Cooldown = 300;
            [JsonProperty("Low grade fuel to add on spawn")] public int Fuel = 50;
            [JsonProperty("Spawn distance in front of player")] public float SpawnDistance = 4f;
            [JsonProperty("Requires water to spawn")] public bool RequiresWater = false;

            // Custom-vehicle modifiers
            [JsonProperty("Skin ID (0 = none)")] public ulong SkinId = 0;
            [JsonProperty("Health multiplier (1 = default toughness)")] public float HealthMultiplier = 1f;
            [JsonProperty("Speed multiplier (best-effort, 1 = default)")] public float SpeedMultiplier = 1f;
            [JsonProperty("Protect from decay")] public bool NoDecay = false;
            [JsonProperty("Lock to owner (overrides global owner-only mount)")] public bool LockToOwner = false;
            [JsonProperty("Disable AI (for NPC-based entities like Bradley)")] public bool DisableAI = false;
            [JsonProperty("Drivable (experimental, Bradley only)")] public bool Drivable = false;

            [JsonIgnore] public string FullPermission => "vehicles." + Permission;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            PrintWarning("Generated a new configuration file.");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception("Configuration is null.");
            }
            catch (Exception ex)
            {
                PrintError($"Configuration file is invalid; using defaults.\n{ex.Message}");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        #endregion

        #region Data

        private class StoredData
        {
            public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();

            public PlayerData Get(ulong id)
            {
                PlayerData data;
                if (!Players.TryGetValue(id, out data))
                    Players[id] = data = new PlayerData();
                return data;
            }
        }

        private class PlayerData
        {
            public List<OwnedRecord> Vehicles = new List<OwnedRecord>();
            public Dictionary<string, double> LastSpawn = new Dictionary<string, double>();
        }

        private class OwnedRecord
        {
            public string Key;
            public ulong NetId;
            public double SpawnTime;
        }

        private class OwnedVehicle
        {
            public ulong OwnerId;
            public string Key;
        }

        private void LoadData()
        {
            try { storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name); }
            catch { storedData = null; }
            if (storedData?.Players == null) storedData = new StoredData();
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        #endregion

        #region Lifecycle

        private void Init()
        {
            LoadData();
            permission.RegisterPermission(PermAdmin, this);

            foreach (var kvp in config.Vehicles)
            {
                var key = kvp.Key.ToLower();
                var settings = kvp.Value;
                permission.RegisterPermission(settings.FullPermission, this);

                if (settings.Commands == null) continue;
                foreach (var c in settings.Commands)
                {
                    if (string.IsNullOrWhiteSpace(c)) continue;
                    var cmd = c.ToLower();
                    if (cmd == config.Command.ToLower())
                    {
                        PrintWarning($"Command '/{cmd}' for '{key}' clashes with the main command and was skipped.");
                        continue;
                    }
                    if (commandToKey.ContainsKey(cmd))
                    {
                        PrintWarning($"Command '/{cmd}' is defined more than once; keeping the first ('{commandToKey[cmd]}').");
                        continue;
                    }
                    commandToKey[cmd] = key;
                    AddCovalenceCommand(cmd, nameof(CmdSpawnDirect));
                }
            }

            AddCovalenceCommand(config.Command, nameof(CmdVehicle));
        }

        private void OnServerInitialized()
        {
            var removed = 0;
            foreach (var pd in storedData.Players)
            {
                for (var i = pd.Value.Vehicles.Count - 1; i >= 0; i--)
                {
                    var rec = pd.Value.Vehicles[i];
                    var entity = FindEntity(rec.NetId);
                    if (entity == null || entity.IsDestroyed)
                    {
                        pd.Value.Vehicles.RemoveAt(i);
                        removed++;
                        continue;
                    }
                    vehiclesByNetId[rec.NetId] = new OwnedVehicle { OwnerId = pd.Key, Key = rec.Key };
                }
            }
            if (removed > 0)
            {
                SaveData();
                Puts($"Cleared {removed} vehicle reference(s) that no longer exist in the world.");
            }

            if (!string.IsNullOrEmpty(config.Discord.WebhookUrl))
                timer.Every(2f, ProcessDiscordQueue);

            var anyNoDecay = false;
            foreach (var v in config.Vehicles.Values)
                if (v.NoDecay) { anyNoDecay = true; break; }
            if (!anyNoDecay) Unsubscribe(nameof(OnEntityTakeDamage));
        }

        private void OnServerSave() => SaveData();

        private void Unload() => SaveData();

        #endregion

        #region Commands

        private void CmdVehicle(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (args.Length == 0) { ShowHelp(player); return; }

            var sub = args[0].ToLower();
            switch (sub)
            {
                case "help":
                    ShowHelp(player);
                    return;
                case "list":
                case "catalog":
                    ShowCatalog(player, args.Length > 1 ? args[1] : null);
                    return;
                case "recall":
                    if (args.Length < 2) { Message(player, "SpecifyVehicle"); return; }
                    RecallVehicle(player, ResolveKey(args[1]));
                    return;
                case "removeall":
                    RemoveAll(player);
                    return;
                case "remove":
                case "no":
                case "despawn":
                    if (args.Length < 2) { Message(player, "SpecifyVehicle"); return; }
                    if (args[1].ToLower() == "all") { RemoveAll(player); return; }
                    RemoveVehicle(player, ResolveKey(args[1]));
                    return;
                case "where":
                case "find":
                    if (args.Length < 2) { Message(player, "SpecifyVehicle"); return; }
                    WhereVehicle(player, ResolveKey(args[1]));
                    return;
                default:
                    SpawnVehicle(player, ResolveKey(sub));
                    return;
            }
        }

        private void CmdSpawnDirect(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (player == null) return;

            string key;
            if (!commandToKey.TryGetValue(command.ToLower(), out key)) { Message(player, "UnknownVehicle"); return; }
            SpawnVehicle(player, key);
        }

        private string ResolveKey(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;
            input = input.ToLower();
            if (config.Vehicles.ContainsKey(input)) return input;
            string key;
            return commandToKey.TryGetValue(input, out key) ? key : input;
        }

        #endregion

        #region Core spawn

        private class SpawnResult
        {
            public BaseEntity Entity;
            public VehicleSettings Settings;
            public string Key;
            public string ErrorKey;
            public object[] ErrorArgs;
            public bool Paid;
            public bool Success => Entity != null;
        }

        private SpawnResult TrySpawn(BasePlayer player, string key, bool forceFree, bool forceNoCooldown, bool fromApi)
        {
            var r = new SpawnResult { Key = key };

            VehicleSettings settings;
            if (string.IsNullOrEmpty(key) || !config.Vehicles.TryGetValue(key, out settings)) { r.ErrorKey = "UnknownVehicle"; return r; }
            r.Settings = settings;

            if (!settings.Enabled) { r.ErrorKey = "VehicleDisabled"; return r; }

            // Admins (with the bypass permission) can spawn any vehicle without each per-vehicle permission.
            var isAdmin = config.AdminBypass && HasPermission(player, PermAdmin);
            if (!fromApi && !isAdmin && !HasPermission(player, settings.FullPermission)) { r.ErrorKey = "NoPermission"; return r; }

            var data = storedData.Get(player.userID);
            ScrubMissing(data);

            if (!config.AllowMultiplePerType && CountOfType(data, key) > 0)
            {
                r.ErrorKey = "AlreadyOwned"; r.ErrorArgs = new object[] { settings.DisplayName }; return r;
            }

            if (config.BlockWhenBuildingBlocked && player.IsBuildingBlocked()) { r.ErrorKey = "BuildingBlocked"; return r; }

            var bypass = forceFree || isAdmin;
            var noCooldown = forceNoCooldown || bypass;

            if (config.MaxVehicles > 0 && data.Vehicles.Count >= config.MaxVehicles && !bypass)
            {
                r.ErrorKey = "MaxVehicles"; r.ErrorArgs = new object[] { config.MaxVehicles }; return r;
            }

            if (!noCooldown)
            {
                double last;
                if (data.LastSpawn.TryGetValue(key, out last))
                {
                    var remaining = settings.Cooldown - (Now - last);
                    if (remaining > 0) { r.ErrorKey = "OnCooldown"; r.ErrorArgs = new object[] { settings.DisplayName, FormatTime(remaining) }; return r; }
                }
            }

            Vector3 position;
            Quaternion rotation;
            GetSpawnPoint(player, settings, out position, out rotation);
            if (settings.RequiresWater && !IsInWater(position)) { r.ErrorKey = "NotInWater"; return r; }

            var hookResult = Interface.CallHook("CanSpawnVehicle", player, key);
            if (hookResult != null)
            {
                r.ErrorKey = "BlockedByPlugin";
                r.ErrorArgs = new object[] { hookResult as string ?? "another plugin" };
                return r;
            }

            var charged = false;
            if (!bypass && settings.Price > 0)
            {
                if (!Charge(player, settings.Price, settings.Currency))
                {
                    r.ErrorKey = "CannotAfford"; r.ErrorArgs = new object[] { settings.Price, CurrencyName(settings.Currency) }; return r;
                }
                charged = true;
            }

            var entity = GameManager.server.CreateEntity(settings.Prefab, position, rotation);
            if (entity == null)
            {
                PrintError($"Failed to create entity for '{key}' – check the prefab path: {settings.Prefab}");
                if (charged) Refund(player, settings.Price, settings.Currency);
                r.ErrorKey = "SpawnFailed"; return r;
            }

            entity.OwnerID = player.userID;
            entity.Spawn();
            TryGiveFuel(entity, settings.Fuel);
            NextTick(() => ApplyModifications(entity, settings));

            data.Vehicles.Add(new OwnedRecord { Key = key, NetId = entity.net.ID.Value, SpawnTime = Now });
            data.LastSpawn[key] = Now;
            vehiclesByNetId[entity.net.ID.Value] = new OwnedVehicle { OwnerId = player.userID, Key = key };
            SaveData();

            r.Entity = entity;
            r.Paid = charged;
            Interface.CallHook("OnVehiclesVehicleSpawned", entity, player, key);
            return r;
        }

        private void SpawnVehicle(BasePlayer player, string key)
        {
            var r = TrySpawn(player, key, false, false, false);
            if (!r.Success)
            {
                Message(player, r.ErrorKey, r.ErrorArgs ?? EmptyArgs);
                if (config.Discord.LogDenials && r.ErrorKey != "UnknownVehicle")
                    DiscordLog("⛔ Spawn Denied", $"**{r.Settings?.DisplayName ?? key}** — {r.ErrorKey}", player, ColorRed);
                return;
            }

            var s = r.Settings;
            if (r.Paid) Message(player, "SpawnedPaid", s.DisplayName, s.Price, CurrencyName(s.Currency));
            else Message(player, "Spawned", s.DisplayName);

            if (config.Discord.LogSpawns)
            {
                var grid = GetGrid(r.Entity.transform.position);
                var desc = r.Paid
                    ? $"Purchased **{s.DisplayName}** for **{s.Price} {CurrencyName(s.Currency)}** at grid **{grid}**."
                    : $"Spawned **{s.DisplayName}** at grid **{grid}**.";
                DiscordLog("🚗 Vehicle Spawned", desc, player, r.Paid ? ColorPurple : ColorGreen);
            }
        }

        #endregion

        #region Manage (recall / remove / where)

        private void RecallVehicle(BasePlayer player, string key)
        {
            VehicleSettings settings;
            if (string.IsNullOrEmpty(key) || !config.Vehicles.TryGetValue(key, out settings)) { Message(player, "UnknownVehicle"); return; }

            var data = storedData.Get(player.userID);
            var entity = NearestOwned(player, data, key);
            if (entity == null) { Message(player, "NothingToRecall", settings.DisplayName); return; }

            if (IsOccupied(entity)) { Message(player, "VehicleOccupied"); return; }
            if (config.ProtectVehiclesWithLoot && HasLoot(entity)) { Message(player, "VehicleHasLoot"); return; }
            if (config.BlockWhenBuildingBlocked && player.IsBuildingBlocked()) { Message(player, "BuildingBlocked"); return; }

            Vector3 position;
            Quaternion rotation;
            GetSpawnPoint(player, settings, out position, out rotation);
            if (settings.RequiresWater && !IsInWater(position)) { Message(player, "NotInWater"); return; }

            var rb = entity.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

            entity.transform.SetPositionAndRotation(position, rotation);
            entity.transform.hasChanged = true;
            entity.UpdateNetworkGroup();
            entity.SendNetworkUpdateImmediate();

            Interface.CallHook("OnVehiclesVehicleRecalled", entity, player, key);
            Message(player, "Recalled", settings.DisplayName);
            if (config.Discord.LogRecalls)
                DiscordLog("📍 Vehicle Recalled", $"Recalled **{settings.DisplayName}** to grid **{GetGrid(position)}**.", player, ColorBlue);
        }

        private void RemoveVehicle(BasePlayer player, string key)
        {
            VehicleSettings settings;
            if (string.IsNullOrEmpty(key) || !config.Vehicles.TryGetValue(key, out settings)) { Message(player, "UnknownVehicle"); return; }

            var data = storedData.Get(player.userID);
            var entity = NearestOwned(player, data, key);
            if (entity == null) { Message(player, "NothingToRemove", settings.DisplayName); return; }

            if (IsOccupied(entity)) { Message(player, "VehicleOccupied"); return; }
            if (config.ProtectVehiclesWithLoot && HasLoot(entity)) { Message(player, "VehicleHasLoot"); return; }

            Interface.CallHook("OnVehiclesVehicleRemoved", entity, player, key);
            entity.Kill();
            Message(player, "Removed", settings.DisplayName);
            if (config.Discord.LogRemovals)
                DiscordLog("🗑️ Vehicle Removed", $"Removed **{settings.DisplayName}**.", player, ColorOrange);
        }

        private void RemoveAll(BasePlayer player)
        {
            var data = storedData.Get(player.userID);
            ScrubMissing(data);
            var killed = 0;
            for (var i = data.Vehicles.Count - 1; i >= 0; i--)
            {
                var entity = FindEntity(data.Vehicles[i].NetId);
                if (entity == null || entity.IsDestroyed) continue;
                if (IsOccupied(entity)) continue;
                if (config.ProtectVehiclesWithLoot && HasLoot(entity)) continue;
                entity.Kill();
                killed++;
            }
            Message(player, "RemovedAll", killed);
            if (killed > 0 && config.Discord.LogRemovals)
                DiscordLog("🗑️ Vehicles Removed", $"Removed **{killed}** vehicle(s).", player, ColorOrange);
        }

        private void WhereVehicle(BasePlayer player, string key)
        {
            VehicleSettings settings;
            if (string.IsNullOrEmpty(key) || !config.Vehicles.TryGetValue(key, out settings)) { Message(player, "UnknownVehicle"); return; }

            var data = storedData.Get(player.userID);
            var entity = NearestOwned(player, data, key);
            if (entity == null) { Message(player, "NothingToFind", settings.DisplayName); return; }

            var distance = Vector3.Distance(player.transform.position, entity.transform.position);
            var direction = Compass(player.transform.position, entity.transform.position);
            var grid = GetGrid(entity.transform.position);
            Message(player, "WhereInfo", settings.DisplayName, Mathf.RoundToInt(distance), direction, grid);
        }

        #endregion

        #region Hooks

        private void OnEntityKill(BaseNetworkable networkable)
        {
            var entity = networkable as BaseEntity;
            if (entity?.net == null) return;
            var id = entity.net.ID.Value;
            OwnedVehicle owned;
            if (!vehiclesByNetId.TryGetValue(id, out owned)) return;

            vehiclesByNetId.Remove(id);
            PlayerData pd;
            if (storedData.Players.TryGetValue(owned.OwnerId, out pd) && pd.Vehicles.RemoveAll(rec => rec.NetId == id) > 0)
                SaveData();
        }

        private object CanMountEntity(BasePlayer player, BaseMountable mountable)
        {
            if (player == null || mountable == null) return null;

            var vehicle = mountable as BaseVehicle ?? mountable.VehicleParent();
            if (vehicle?.net == null) return null;

            OwnedVehicle owned;
            if (!vehiclesByNetId.TryGetValue(vehicle.net.ID.Value, out owned)) return null;

            VehicleSettings settings;
            config.Vehicles.TryGetValue(owned.Key, out settings);
            var locked = config.OwnerOnlyMount || (settings != null && settings.LockToOwner);
            if (!locked) return null;

            if (owned.OwnerId == player.userID) return null;
            if (HasPermission(player, PermAdmin)) return null;

            Message(player, "NotYourVehicle");
            return false;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity?.net == null || info?.damageTypes == null) return null;
            OwnedVehicle owned;
            if (!vehiclesByNetId.TryGetValue(entity.net.ID.Value, out owned)) return null;
            VehicleSettings settings;
            if (!config.Vehicles.TryGetValue(owned.Key, out settings) || !settings.NoDecay) return null;
            if (info.damageTypes.Get(Rust.DamageType.Decay) > 0f)
                info.damageTypes.Scale(Rust.DamageType.Decay, 0f);
            return null;
        }

        #endregion

        #region Public API

        public bool IsVehicle(BaseEntity entity)
            => entity?.net != null && vehiclesByNetId.ContainsKey(entity.net.ID.Value);

        public string GetVehicleType(BaseEntity entity)
        {
            if (entity?.net == null) return null;
            OwnedVehicle o;
            return vehiclesByNetId.TryGetValue(entity.net.ID.Value, out o) ? o.Key : null;
        }

        public ulong GetVehicleOwnerId(BaseEntity entity)
        {
            if (entity?.net == null) return 0;
            OwnedVehicle o;
            return vehiclesByNetId.TryGetValue(entity.net.ID.Value, out o) ? o.OwnerId : 0;
        }

        // All net ids the player currently owns.
        public List<ulong> GetOwnedVehicles(ulong userId)
        {
            var list = new List<ulong>();
            PlayerData pd;
            if (storedData.Players.TryGetValue(userId, out pd))
                foreach (var rec in pd.Vehicles)
                    if (FindEntity(rec.NetId) != null) list.Add(rec.NetId);
            return list;
        }

        public string GetVehicleDisplayName(string key)
        {
            VehicleSettings s;
            return config.Vehicles.TryGetValue(key ?? string.Empty, out s) ? s.DisplayName : null;
        }

        public BaseEntity SpawnVehicleForPlayer(BasePlayer player, string key, bool free = true)
        {
            if (player == null) return null;
            var r = TrySpawn(player, ResolveKey(key), free, free, true);
            if (r.Success && config.Discord.LogAdmin)
                DiscordLog("🛠️ API Spawn", $"**{r.Settings.DisplayName}** granted via API/shop.", player, ColorPurple);
            return r.Entity;
        }

        public bool DespawnVehicle(BaseEntity entity)
        {
            if (!IsVehicle(entity)) return false;
            entity.Kill();
            return true;
        }

        #endregion

        #region Console commands (server console / RCON)

        [ConsoleCommand("vehicles.give")]
        private void CcmdGive(ConsoleSystem.Arg arg)
        {
            if (!IsConsoleAdmin(arg)) { arg.ReplyWith("You are not allowed to use this command."); return; }
            if (arg.Args == null || arg.Args.Length < 2)
            {
                arg.ReplyWith("Usage: vehicles.give <steamId|name> <vehicleKey>  (see vehicles.list)");
                return;
            }

            var target = FindOnlinePlayer(arg.Args[0]);
            if (target == null) { arg.ReplyWith($"No online player matched '{arg.Args[0]}'."); return; }

            var key = ResolveKey(arg.Args[1]);
            if (!config.Vehicles.ContainsKey(key ?? string.Empty)) { arg.ReplyWith($"Unknown vehicle '{arg.Args[1]}'. Try vehicles.list."); return; }

            var entity = SpawnVehicleForPlayer(target, key, true);
            arg.ReplyWith(entity != null
                ? $"Spawned '{key}' for {target.displayName} ({target.UserIDString})."
                : $"Failed to spawn '{key}' – check the prefab path / server console for errors.");
        }

        [ConsoleCommand("vehicles.list")]
        private void CcmdList(ConsoleSystem.Arg arg)
        {
            if (!IsConsoleAdmin(arg)) { arg.ReplyWith("You are not allowed to use this command."); return; }
            var filter = arg.Args != null && arg.Args.Length > 0 ? arg.Args[0].ToLower() : null;
            var sb = new System.Text.StringBuilder();
            var shown = 0;
            foreach (var kvp in config.Vehicles)
            {
                if (filter != null && kvp.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var s = kvp.Value;
                sb.AppendLine($"  {kvp.Key,-22} enabled={s.Enabled,-5} price={s.Price,-7} -> {s.Prefab}");
                shown++;
            }
            arg.ReplyWith($"Vehicles ({shown}/{config.Vehicles.Count} shown):\n" + sb);
        }

        private bool IsConsoleAdmin(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null) return true; // server console / RCON
            var p = arg.Connection.player as BasePlayer;
            return p != null && p.IsAdmin;
        }

        private BasePlayer FindOnlinePlayer(string nameOrId)
        {
            ulong id;
            if (ulong.TryParse(nameOrId, out id))
            {
                var byId = BasePlayer.FindByID(id);
                if (byId != null) return byId;
            }
            foreach (var p in BasePlayer.activePlayerList)
                if (p.displayName != null && p.displayName.IndexOf(nameOrId, StringComparison.OrdinalIgnoreCase) >= 0)
                    return p;
            return null;
        }

        #endregion

        #region Drivable Bradley (experimental)

        public class BradleyDriveController : MonoBehaviour
        {
            public BradleyAPC apc;
            public BaseMountable seat;
            public float moveSpeed = 9f;
            public float turnSpeed = 50f;
            private Rigidbody rb;
            private float netTimer;

            private void Awake()
            {
                apc = GetComponent<BradleyAPC>();
                rb = GetComponent<Rigidbody>();
            }

            private void FixedUpdate()
            {
                if (apc == null || apc.IsDestroyed) { Destroy(this); return; }
                var driver = seat != null && !seat.IsDestroyed ? seat.GetMounted() : null;
                if (driver == null) return;

                var input = driver.serverInput;
                if (input == null) return;

                var fwd = (input.IsDown(BUTTON.FORWARD) ? 1f : 0f) - (input.IsDown(BUTTON.BACKWARD) ? 1f : 0f);
                var turn = (input.IsDown(BUTTON.RIGHT) ? 1f : 0f) - (input.IsDown(BUTTON.LEFT) ? 1f : 0f);

                var t = apc.transform;
                if (Mathf.Abs(turn) > 0.01f)
                    t.rotation = t.rotation * Quaternion.Euler(0f, turn * turnSpeed * Time.fixedDeltaTime, 0f);

                if (Mathf.Abs(fwd) > 0.01f)
                {
                    var delta = t.forward * fwd * moveSpeed * Time.fixedDeltaTime;
                    if (rb != null && !rb.isKinematic) rb.MovePosition(rb.position + delta);
                    else t.position = t.position + delta;
                }

                netTimer += Time.fixedDeltaTime;
                if (netTimer >= 0.1f)
                {
                    netTimer = 0f;
                    t.hasChanged = true;
                    try { apc.SendNetworkUpdate(); } catch { }
                }
            }
        }

        private void SetupDrivableBradley(BaseEntity entity)
        {
            var apc = entity as BradleyAPC;
            if (apc == null) return;
            try
            {
                apc.enabled = false; // AI off

                var seat = GameManager.server.CreateEntity(
                    "assets/prefabs/deployable/chair/chair.deployed.prefab",
                    apc.transform.position + Vector3.up * 2.2f,
                    apc.transform.rotation) as BaseMountable;

                if (seat == null) { PrintWarning("Drivable Bradley: failed to create driver seat (chair prefab)."); return; }
                seat.OwnerID = apc.OwnerID;
                seat.Spawn();
                seat.SetParent(apc, true);

                var ctrl = apc.gameObject.AddComponent<BradleyDriveController>();
                ctrl.seat = seat;
            }
            catch (Exception ex)
            {
                PrintWarning($"Drivable Bradley setup failed: {ex.Message}");
            }
        }

        #endregion

        #region Modifiers

        private void ApplyModifications(BaseEntity entity, VehicleSettings s)
        {
            if (entity == null || entity.IsDestroyed || s == null) return;

            if (s.SkinId != 0)
            {
                entity.skinID = s.SkinId;
                entity.SendNetworkUpdate();
            }

            if (s.HealthMultiplier > 0f && Math.Abs(s.HealthMultiplier - 1f) > 0.001f)
            {
                var combat = entity as BaseCombatEntity;
                if (combat != null)
                {
                    var newMax = combat.MaxHealth() * s.HealthMultiplier;
                    combat.InitializeHealth(newMax, newMax);
                }
            }

            if (s.SpeedMultiplier > 0f && Math.Abs(s.SpeedMultiplier - 1f) > 0.001f)
                ApplySpeedMultiplier(entity, s.SpeedMultiplier);

            if (s.Drivable)
                SetupDrivableBradley(entity);
            else if (s.DisableAI)
            {
                var apc = entity as BradleyAPC;
                if (apc != null) apc.enabled = false;
            }
        }

        private void ApplySpeedMultiplier(BaseEntity entity, float mult)
        {
            try
            {
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.DeclaredOnly;
                var changed = false;
                foreach (var name in SpeedFields)
                {
                    var t = entity.GetType();
                    while (t != null && t != typeof(object))
                    {
                        var f = t.GetField(name, flags);
                        if (f != null && f.FieldType == typeof(float))
                        {
                            f.SetValue(entity, (float)f.GetValue(entity) * mult);
                            changed = true;
                            break;
                        }
                        t = t.BaseType;
                    }
                }
                if (changed) entity.SendNetworkUpdate();
            }
            catch (Exception ex)
            {
                PrintWarning($"Speed modifier could not be applied to {entity.ShortPrefabName}: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        private void GetSpawnPoint(BasePlayer player, VehicleSettings settings, out Vector3 position, out Quaternion rotation)
        {
            var forward = player.eyes.HeadForward();
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = player.transform.forward;
            forward.Normalize();

            position = player.transform.position + forward * settings.SpawnDistance;
            rotation = Quaternion.Euler(0f, player.eyes.rotation.eulerAngles.y, 0f);

            if (settings.RequiresWater)
                position.y = Mathf.Max(TerrainMeta.WaterMap.GetHeight(position), 0f) + 0.5f;
            else
                position.y = GetGroundHeight(position) + 1f;
        }

        private float GetGroundHeight(Vector3 position)
        {
            RaycastHit hit;
            var origin = position + Vector3.up * 100f;
            if (Physics.Raycast(origin, Vector3.down, out hit, 200f,
                    LayerMask.GetMask("Terrain", "World", "Construction", "Default"), QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return TerrainMeta.HeightMap.GetHeight(position);
        }

        private bool IsInWater(Vector3 position)
        {
            var terrain = TerrainMeta.HeightMap.GetHeight(position);
            var water = TerrainMeta.WaterMap.GetHeight(position);
            return water - terrain > 1.0f;
        }

        private void TryGiveFuel(BaseEntity entity, int amount)
        {
            if (amount <= 0) return;
            var vehicle = entity as BaseVehicle;
            if (vehicle == null) return;
            try
            {
                var fuelSystem = vehicle.GetFuelSystem();
                var container = fuelSystem?.GetFuelContainer();
                if (container?.inventory == null) return;
                var fuel = ItemManager.CreateByItemID(LowGradeFuelId, amount);
                if (fuel == null) return;
                if (!fuel.MoveToContainer(container.inventory)) fuel.Remove();
            }
            catch (Exception ex)
            {
                PrintWarning($"Could not add fuel to '{entity.ShortPrefabName}': {ex.Message}");
            }
        }

        private bool IsOccupied(BaseEntity entity)
        {
            var vehicle = entity as BaseVehicle;
            if (vehicle != null) return vehicle.AnyMounted();
            var mountable = entity as BaseMountable;
            return mountable != null && mountable.IsMounted();
        }

        private bool HasLoot(BaseEntity entity)
        {
            foreach (var container in entity.GetComponentsInChildren<StorageContainer>())
            {
                if (container?.inventory == null) continue;
                if (container.ShortPrefabName != null && container.ShortPrefabName.Contains("fuel")) continue;
                if (container.inventory.itemList != null && container.inventory.itemList.Count > 0) return true;
            }
            return false;
        }

        private BaseEntity FindEntity(ulong netId)
        {
            if (netId == 0) return null;
            return BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BaseEntity;
        }

        private void ScrubMissing(PlayerData data)
        {
            for (var i = data.Vehicles.Count - 1; i >= 0; i--)
            {
                var rec = data.Vehicles[i];
                if (FindEntity(rec.NetId) == null)
                {
                    vehiclesByNetId.Remove(rec.NetId);
                    data.Vehicles.RemoveAt(i);
                }
            }
        }

        private int CountOfType(PlayerData data, string key)
        {
            var c = 0;
            foreach (var rec in data.Vehicles)
                if (rec.Key == key && FindEntity(rec.NetId) != null) c++;
            return c;
        }

        private BaseEntity NearestOwned(BasePlayer player, PlayerData data, string key)
        {
            ScrubMissing(data);
            BaseEntity best = null;
            var bestDist = float.MaxValue;
            foreach (var rec in data.Vehicles)
            {
                if (rec.Key != key) continue;
                var e = FindEntity(rec.NetId);
                if (e == null || e.IsDestroyed) continue;
                var d = Vector3.Distance(player.transform.position, e.transform.position);
                if (d < bestDist) { bestDist = d; best = e; }
            }
            return best;
        }

        private bool HasPermission(BasePlayer player, string perm)
            => permission.UserHasPermission(player.UserIDString, perm);

        // Admins (with bypass) may use any vehicle; everyone else needs the per-vehicle permission.
        private bool CanUse(BasePlayer player, VehicleSettings settings)
            => (config.AdminBypass && HasPermission(player, PermAdmin)) || HasPermission(player, settings.FullPermission);

        #endregion

        #region Economy

        private string CurrencyName(string currency)
            => string.Equals(currency, "ServerRewards", StringComparison.OrdinalIgnoreCase) ? "RP" : "coins";

        private bool Charge(BasePlayer player, double price, string currency)
        {
            if (string.Equals(currency, "ServerRewards", StringComparison.OrdinalIgnoreCase))
            {
                if (ServerRewards == null) { PrintWarning("ServerRewards not loaded; spawn allowed for free."); return true; }
                var points = ServerRewards.Call<int>("CheckPoints", player.userID);
                if (points < price) return false;
                ServerRewards.Call("TakePoints", player.userID, (int)price);
                return true;
            }

            if (Economics == null) { PrintWarning("Economics not loaded; spawn allowed for free."); return true; }
            var balance = Economics.Call<double>("Balance", player.userID);
            if (balance < price) return false;
            return Economics.Call<bool>("Withdraw", player.userID, price);
        }

        private void Refund(BasePlayer player, double price, string currency)
        {
            if (string.Equals(currency, "ServerRewards", StringComparison.OrdinalIgnoreCase))
                ServerRewards?.Call("AddPoints", player.userID, (int)price);
            else
                Economics?.Call("Deposit", player.userID, price);
        }

        #endregion

        #region Discord webhook

        private void DiscordLog(string title, string description, BasePlayer player, int color)
        {
            if (string.IsNullOrEmpty(config.Discord.WebhookUrl)) return;

            var fields = new List<Dictionary<string, object>>();
            if (player != null)
            {
                fields.Add(new Dictionary<string, object> { ["name"] = "Player", ["value"] = player.displayName, ["inline"] = true });
                fields.Add(new Dictionary<string, object> { ["name"] = "Steam ID", ["value"] = player.UserIDString, ["inline"] = true });
            }

            var embed = new Dictionary<string, object>
            {
                ["title"] = title,
                ["description"] = description,
                ["color"] = color,
                ["fields"] = fields,
                ["footer"] = new Dictionary<string, object> { ["text"] = $"Vehicles • {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC" }
            };

            var payload = new Dictionary<string, object>
            {
                ["username"] = string.IsNullOrEmpty(config.Discord.Username) ? "Vehicles" : config.Discord.Username,
                ["embeds"] = new[] { embed }
            };
            if (!string.IsNullOrEmpty(config.Discord.AvatarUrl)) payload["avatar_url"] = config.Discord.AvatarUrl;

            discordQueue.Enqueue(JsonConvert.SerializeObject(payload));
        }

        private void ProcessDiscordQueue()
        {
            if (discordQueue.Count == 0 || string.IsNullOrEmpty(config.Discord.WebhookUrl)) return;
            var payload = discordQueue.Dequeue();

            webrequest.Enqueue(config.Discord.WebhookUrl, payload, (code, response) =>
            {
                if (code == 429) discordQueue.Enqueue(payload);
                else if (code != 204 && code != 200) PrintWarning($"Discord webhook returned HTTP {code}: {response}");
            }, this, RequestMethod.POST, DiscordHeaders);
        }

        #endregion

        #region Locating helpers

        private static string Compass(Vector3 from, Vector3 to)
        {
            var dir = to - from;
            var angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;
            string[] points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return points[Mathf.RoundToInt(angle / 45f) % 8];
        }

        private static string GetGrid(Vector3 position)
        {
            var mapSize = TerrainMeta.Size.x;
            var offset = mapSize / 2f;
            const float gridCell = 146.3f;
            var x = position.x + offset;
            var z = position.z + offset;
            var col = Mathf.FloorToInt(x / gridCell);
            var row = Mathf.FloorToInt((mapSize - z) / gridCell);

            var letters = string.Empty;
            var n = col;
            do
            {
                letters = (char)('A' + n % 26) + letters;
                n = n / 26 - 1;
            } while (n >= 0);

            return letters + row;
        }

        #endregion

        #region Messaging

        private void ShowHelp(BasePlayer player)
        {
            var lines = new List<string> { Lang("HelpHeader", player) };
            var featured = 0;
            var total = 0;
            foreach (var kvp in config.Vehicles)
            {
                var s = kvp.Value;
                if (!s.Enabled || !CanUse(player, s)) continue;
                total++;
                if (s.Commands == null || s.Commands.Length == 0) continue; // only show featured (with shortcut commands)
                var cmd = "/" + s.Commands[0];
                var cost = s.Price > 0 ? $"{s.Price} {CurrencyName(s.Currency)}" : Lang("Free", player);
                lines.Add(Lang("HelpEntry", player, s.DisplayName, cmd, cost, FormatTime(s.Cooldown)));
                featured++;
            }
            lines.Add(Lang("HelpFooter", player, config.Command, total - featured));
            player.ChatMessage(string.Join("\n", lines));
        }

        private void ShowCatalog(BasePlayer player, string filter)
        {
            var keys = new List<string>();
            foreach (var kvp in config.Vehicles)
            {
                if (!kvp.Value.Enabled || !CanUse(player, kvp.Value)) continue;
                if (filter != null && kvp.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                keys.Add(kvp.Key);
            }
            if (keys.Count == 0) { Message(player, "CatalogEmpty"); return; }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Lang("CatalogHeader", player, keys.Count));
            for (var i = 0; i < keys.Count; i += 6)
                sb.AppendLine(string.Join(", ", keys.GetRange(i, Math.Min(6, keys.Count - i))));
            player.ChatMessage(sb.ToString());
        }

        private string FormatTime(double seconds)
        {
            if (seconds <= 0) return "0s";
            var t = TimeSpan.FromSeconds(Math.Ceiling(seconds));
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes}m";
            if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds}s";
            return $"{t.Seconds}s";
        }

        private string Lang(string key, BasePlayer player, params object[] args)
        {
            var msg = lang.GetMessage(key, this, player?.UserIDString);
            return args.Length > 0 ? string.Format(msg, args) : msg;
        }

        private void Message(BasePlayer player, string key, params object[] args)
        {
            if (player == null) return;
            player.ChatMessage(config.Prefix + Lang(key, player, args));
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use that vehicle.",
                ["UnknownVehicle"] = "Unknown vehicle. Type the main command for a list.",
                ["VehicleDisabled"] = "That vehicle is currently disabled.",
                ["SpecifyVehicle"] = "Specify a vehicle, e.g. <color=#f1c40f>minicopter</color>.",
                ["BuildingBlocked"] = "You can't do that while building blocked.",
                ["OnCooldown"] = "Your {0} is on cooldown for another <color=#f1c40f>{1}</color>.",
                ["CannotAfford"] = "You can't afford that. It costs <color=#f1c40f>{0} {1}</color>.",
                ["AlreadyOwned"] = "You already own a {0} and multiple of this type are disabled.",
                ["MaxVehicles"] = "You already own the maximum of <color=#f1c40f>{0}</color> vehicles.",
                ["NotInWater"] = "That vehicle must be spawned in water. Stand near deep water and look at it.",
                ["BlockedByPlugin"] = "Spawning was blocked by {0}.",
                ["SpawnFailed"] = "Something went wrong spawning that vehicle.",
                ["Spawned"] = "Your <color=#2ecc71>{0}</color> has been spawned in front of you.",
                ["SpawnedPaid"] = "Your <color=#2ecc71>{0}</color> has been spawned for <color=#f1c40f>{1} {2}</color>.",
                ["Recalled"] = "Your nearest <color=#2ecc71>{0}</color> has been recalled to you.",
                ["Removed"] = "Your nearest <color=#2ecc71>{0}</color> has been removed.",
                ["RemovedAll"] = "Removed <color=#f1c40f>{0}</color> of your vehicles.",
                ["NothingToRecall"] = "You have no {0} to recall.",
                ["NothingToRemove"] = "You have no {0} to remove.",
                ["NothingToFind"] = "You have no {0} to locate.",
                ["VehicleOccupied"] = "You can't do that while the vehicle is occupied.",
                ["VehicleHasLoot"] = "That vehicle still has items inside it. Empty it first.",
                ["NotYourVehicle"] = "This vehicle belongs to someone else.",
                ["WhereInfo"] = "Your nearest <color=#2ecc71>{0}</color> is <color=#f1c40f>{1}m</color> to the <color=#f1c40f>{2}</color> (grid {3}).",
                ["Free"] = "Free",
                ["CatalogEmpty"] = "No vehicles match.",
                ["CatalogHeader"] = "<color=#5dade2>Vehicle catalog ({0}):</color>",
                ["HelpHeader"] = "<color=#5dade2>===== Vehicles =====</color>",
                ["HelpEntry"] = "<color=#2ecc71>{0}</color> — {1}  |  cost: {2}  |  cooldown: {3}",
                ["HelpFooter"] = "+ <color=#f1c40f>{1}</color> more — use <color=#f1c40f>/{0} <name></color> or <color=#f1c40f>/{0} catalog</color>. Manage: <color=#f1c40f>/{0} recall|remove|removeall|where <name></color>",
            }, this);
        }

        #endregion
    }
}
