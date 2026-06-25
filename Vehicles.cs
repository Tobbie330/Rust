using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Vehicles", "Tobbie", "1.0.0")]
    [Description("Spawn, recall, locate and manage personal vehicles with permissions, cooldowns, ownership and economy support.")]
    public class Vehicles : RustPlugin
    {
        // Optional economy integrations – resolved at runtime, no hard dependency.
        [PluginReference] private Plugin Economics, ServerRewards;

        private const string PermAdmin = "vehicles.admin";
        private const int LowGradeFuelId = -946369541; // lowgradefuel

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static double Now => (DateTime.UtcNow - Epoch).TotalSeconds;

        private Configuration config;
        private StoredData storedData;

        // Fast lookups built at load time.
        private readonly Dictionary<string, string> commandToKey = new Dictionary<string, string>();
        private readonly Dictionary<ulong, OwnedVehicle> vehiclesByNetId = new Dictionary<ulong, OwnedVehicle>();

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Main chat command")]
            public string Command = "vehicle";

            [JsonProperty("Message prefix")]
            public string Prefix = "<color=#5dade2>[Vehicles]</color> ";

            [JsonProperty("Maximum vehicles a player may own at once (0 = unlimited)")]
            public int MaxVehicles = 3;

            [JsonProperty("Block spawning while building blocked")]
            public bool BlockWhenBuildingBlocked = true;

            [JsonProperty("Only the owner (and admins) may mount their vehicle")]
            public bool OwnerOnlyMount = false;

            [JsonProperty("Refuse to despawn/recall vehicles that still contain items")]
            public bool ProtectVehiclesWithLoot = true;

            [JsonProperty("Players with the admin permission spawn for free and ignore cooldowns")]
            public bool AdminBypass = true;

            [JsonProperty("Vehicles")]
            public Dictionary<string, VehicleSettings> Vehicles = DefaultVehicles();

            public static Dictionary<string, VehicleSettings> DefaultVehicles() => new Dictionary<string, VehicleSettings>
            {
                ["minicopter"] = new VehicleSettings
                {
                    DisplayName = "Minicopter",
                    Permission = "minicopter",
                    Commands = new[] { "mini", "minicopter" },
                    Prefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                    Price = 500, Currency = "Economics", Cooldown = 600, Fuel = 50
                },
                ["scraptransport"] = new VehicleSettings
                {
                    DisplayName = "Scrap Transport Heli",
                    Permission = "scraptransport",
                    Commands = new[] { "scrapheli" },
                    Prefab = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
                    Price = 1500, Currency = "Economics", Cooldown = 1200, Fuel = 100
                },
                ["attackheli"] = new VehicleSettings
                {
                    DisplayName = "Attack Helicopter",
                    Permission = "attackheli",
                    Commands = new[] { "attackheli" },
                    Prefab = "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab",
                    Price = 3000, Currency = "Economics", Cooldown = 1800, Fuel = 100
                },
                ["sedan"] = new VehicleSettings
                {
                    DisplayName = "Sedan",
                    Permission = "sedan",
                    Commands = new[] { "car", "sedan" },
                    Prefab = "assets/content/vehicles/sedan_a/sedantest.entity.prefab",
                    Price = 250, Currency = "Economics", Cooldown = 300, Fuel = 50
                },
                ["modularcar"] = new VehicleSettings
                {
                    DisplayName = "Modular Car",
                    Permission = "modularcar",
                    Commands = new[] { "modcar" },
                    Prefab = "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab",
                    Price = 800, Currency = "Economics", Cooldown = 900, Fuel = 50
                },
                ["rowboat"] = new VehicleSettings
                {
                    DisplayName = "Rowboat",
                    Permission = "rowboat",
                    Commands = new[] { "boat", "rowboat" },
                    Prefab = "assets/content/vehicles/boats/rowboat/rowboat.prefab",
                    Price = 200, Currency = "Economics", Cooldown = 300, Fuel = 50, RequiresWater = true
                },
                ["rhib"] = new VehicleSettings
                {
                    DisplayName = "RHIB",
                    Permission = "rhib",
                    Commands = new[] { "rhib" },
                    Prefab = "assets/content/vehicles/boats/rhib/rhib.prefab",
                    Price = 600, Currency = "Economics", Cooldown = 600, Fuel = 50, RequiresWater = true
                },
                ["submarinesolo"] = new VehicleSettings
                {
                    DisplayName = "Solo Submarine",
                    Permission = "submarinesolo",
                    Commands = new[] { "sub", "submarine" },
                    Prefab = "assets/content/vehicles/submarine/submarinesolo.entity.prefab",
                    Price = 700, Currency = "Economics", Cooldown = 600, Fuel = 50, RequiresWater = true
                },
                ["hotairballoon"] = new VehicleSettings
                {
                    DisplayName = "Hot Air Balloon",
                    Permission = "hotairballoon",
                    Commands = new[] { "hab", "balloon" },
                    Prefab = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab",
                    Price = 400, Currency = "Economics", Cooldown = 600, Fuel = 50
                },
                ["ridablehorse"] = new VehicleSettings
                {
                    DisplayName = "Ridable Horse",
                    Permission = "ridablehorse",
                    Commands = new[] { "horse" },
                    Prefab = "assets/content/vehicles/horse/ridablehorse2.prefab",
                    Price = 150, Currency = "Economics", Cooldown = 300, Fuel = 0
                },
                ["snowmobile"] = new VehicleSettings
                {
                    DisplayName = "Snowmobile",
                    Permission = "snowmobile",
                    Commands = new[] { "snowmobile" },
                    Prefab = "assets/content/vehicles/snowmobiles/snowmobile.prefab",
                    Price = 350, Currency = "Economics", Cooldown = 300, Fuel = 50
                },
            };
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
            // key -> net id of the owned vehicle
            public Dictionary<string, ulong> Owned = new Dictionary<string, ulong>();
            // key -> unix time the vehicle was last spawned (cooldown anchor)
            public Dictionary<string, double> LastSpawn = new Dictionary<string, double>();
        }

        private class OwnedVehicle
        {
            public ulong OwnerId;
            public string Key;
        }

        private void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                storedData = null;
            }
            if (storedData?.Players == null)
                storedData = new StoredData();
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
            // Reconcile stored ownership with entities currently in the world.
            var removed = 0;
            foreach (var pd in storedData.Players)
            {
                var stale = new List<string>();
                foreach (var owned in pd.Value.Owned)
                {
                    var entity = FindEntity(owned.Value);
                    if (entity == null || entity.IsDestroyed)
                    {
                        stale.Add(owned.Key);
                        removed++;
                        continue;
                    }
                    vehiclesByNetId[owned.Value] = new OwnedVehicle { OwnerId = pd.Key, Key = owned.Key };
                }
                foreach (var key in stale)
                    pd.Value.Owned.Remove(key);
            }
            if (removed > 0)
            {
                SaveData();
                Puts($"Cleared {removed} vehicle reference(s) that no longer exist in the world.");
            }
        }

        private void OnServerSave() => SaveData();

        private void Unload() => SaveData();

        #endregion

        #region Commands

        private void CmdVehicle(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (args.Length == 0)
            {
                ShowHelp(player);
                return;
            }

            var sub = args[0].ToLower();
            switch (sub)
            {
                case "help":
                case "list":
                    ShowHelp(player);
                    return;
                case "recall":
                    if (args.Length < 2) { Message(player, "SpecifyVehicle"); return; }
                    RecallVehicle(player, ResolveKey(args[1]));
                    return;
                case "remove":
                case "no":
                case "despawn":
                    if (args.Length < 2) { Message(player, "SpecifyVehicle"); return; }
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
            if (!commandToKey.TryGetValue(command.ToLower(), out key))
            {
                Message(player, "UnknownVehicle");
                return;
            }
            SpawnVehicle(player, key);
        }

        // Lets players type either the config key or any of its aliases.
        private string ResolveKey(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;
            input = input.ToLower();
            if (config.Vehicles.ContainsKey(input)) return input;
            string key;
            return commandToKey.TryGetValue(input, out key) ? key : input;
        }

        #endregion

        #region Core actions

        private void SpawnVehicle(BasePlayer player, string key)
        {
            VehicleSettings settings;
            if (string.IsNullOrEmpty(key) || !config.Vehicles.TryGetValue(key, out settings))
            {
                Message(player, "UnknownVehicle");
                return;
            }
            if (!settings.Enabled)
            {
                Message(player, "VehicleDisabled");
                return;
            }
            if (!HasPermission(player, settings.FullPermission))
            {
                Message(player, "NoPermission");
                return;
            }

            var data = storedData.Get(player.userID);

            // One of each type at a time.
            if (data.Owned.ContainsKey(key) && FindEntity(data.Owned[key]) != null)
            {
                Message(player, "AlreadyOwned", settings.DisplayName);
                return;
            }
            ScrubMissing(data, key);

            if (config.BlockWhenBuildingBlocked && player.IsBuildingBlocked())
            {
                Message(player, "BuildingBlocked");
                return;
            }

            var bypass = config.AdminBypass && HasPermission(player, PermAdmin);

            if (config.MaxVehicles > 0 && CountOwned(data) >= config.MaxVehicles && !bypass)
            {
                Message(player, "MaxVehicles", config.MaxVehicles);
                return;
            }

            // Cooldown.
            if (!bypass)
            {
                double last;
                if (data.LastSpawn.TryGetValue(key, out last))
                {
                    var remaining = settings.Cooldown - (Now - last);
                    if (remaining > 0)
                    {
                        Message(player, "OnCooldown", settings.DisplayName, FormatTime(remaining));
                        return;
                    }
                }
            }

            // Spawn position / water requirement.
            Vector3 position;
            Quaternion rotation;
            GetSpawnPoint(player, settings, out position, out rotation);
            if (settings.RequiresWater && !IsInWater(position))
            {
                Message(player, "NotInWater");
                return;
            }

            // Cost.
            if (!bypass && settings.Price > 0)
            {
                if (!Charge(player, settings.Price, settings.Currency))
                {
                    Message(player, "CannotAfford", settings.Price, CurrencyName(settings.Currency));
                    return;
                }
            }

            var entity = GameManager.server.CreateEntity(settings.Prefab, position, rotation);
            if (entity == null)
            {
                PrintError($"Failed to create entity for '{key}' – check the prefab path: {settings.Prefab}");
                // Refund if we already charged.
                if (!bypass && settings.Price > 0) Refund(player, settings.Price, settings.Currency);
                Message(player, "SpawnFailed");
                return;
            }

            entity.OwnerID = player.userID;
            entity.Spawn();
            TryGiveFuel(entity, settings.Fuel);

            data.Owned[key] = entity.net.ID.Value;
            data.LastSpawn[key] = Now;
            vehiclesByNetId[entity.net.ID.Value] = new OwnedVehicle { OwnerId = player.userID, Key = key };
            SaveData();

            if (!bypass && settings.Price > 0)
                Message(player, "SpawnedPaid", settings.DisplayName, settings.Price, CurrencyName(settings.Currency));
            else
                Message(player, "Spawned", settings.DisplayName);
        }

        private void RecallVehicle(BasePlayer player, string key)
        {
            VehicleSettings settings;
            if (string.IsNullOrEmpty(key) || !config.Vehicles.TryGetValue(key, out settings))
            {
                Message(player, "UnknownVehicle");
                return;
            }

            var data = storedData.Get(player.userID);
            ulong netId;
            BaseEntity entity = null;
            if (data.Owned.TryGetValue(key, out netId))
                entity = FindEntity(netId);

            if (entity == null || entity.IsDestroyed)
            {
                ScrubMissing(data, key);
                Message(player, "NothingToRecall", settings.DisplayName);
                return;
            }

            if (IsOccupied(entity))
            {
                Message(player, "VehicleOccupied");
                return;
            }
            if (config.ProtectVehiclesWithLoot && HasLoot(entity))
            {
                Message(player, "VehicleHasLoot");
                return;
            }
            if (config.BlockWhenBuildingBlocked && player.IsBuildingBlocked())
            {
                Message(player, "BuildingBlocked");
                return;
            }

            Vector3 position;
            Quaternion rotation;
            GetSpawnPoint(player, settings, out position, out rotation);
            if (settings.RequiresWater && !IsInWater(position))
            {
                Message(player, "NotInWater");
                return;
            }

            var rb = entity.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            entity.transform.SetPositionAndRotation(position, rotation);
            entity.transform.hasChanged = true;
            entity.UpdateNetworkGroup();
            entity.SendNetworkUpdateImmediate();

            Message(player, "Recalled", settings.DisplayName);
        }

        private void RemoveVehicle(BasePlayer player, string key)
        {
            VehicleSettings settings;
            if (string.IsNullOrEmpty(key) || !config.Vehicles.TryGetValue(key, out settings))
            {
                Message(player, "UnknownVehicle");
                return;
            }

            var data = storedData.Get(player.userID);
            ulong netId;
            BaseEntity entity = null;
            if (data.Owned.TryGetValue(key, out netId))
                entity = FindEntity(netId);

            if (entity == null || entity.IsDestroyed)
            {
                ScrubMissing(data, key);
                Message(player, "NothingToRemove", settings.DisplayName);
                return;
            }
            if (IsOccupied(entity))
            {
                Message(player, "VehicleOccupied");
                return;
            }
            if (config.ProtectVehiclesWithLoot && HasLoot(entity))
            {
                Message(player, "VehicleHasLoot");
                return;
            }

            entity.Kill(); // OnEntityKill cleans the data + memory map.
            Message(player, "Removed", settings.DisplayName);
        }

        private void WhereVehicle(BasePlayer player, string key)
        {
            VehicleSettings settings;
            if (string.IsNullOrEmpty(key) || !config.Vehicles.TryGetValue(key, out settings))
            {
                Message(player, "UnknownVehicle");
                return;
            }

            var data = storedData.Get(player.userID);
            ulong netId;
            BaseEntity entity = null;
            if (data.Owned.TryGetValue(key, out netId))
                entity = FindEntity(netId);

            if (entity == null || entity.IsDestroyed)
            {
                ScrubMissing(data, key);
                Message(player, "NothingToFind", settings.DisplayName);
                return;
            }

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
            if (storedData.Players.TryGetValue(owned.OwnerId, out pd))
            {
                ulong stored;
                if (pd.Owned.TryGetValue(owned.Key, out stored) && stored == id)
                {
                    pd.Owned.Remove(owned.Key);
                    SaveData();
                }
            }
        }

        private object CanMountEntity(BasePlayer player, BaseMountable mountable)
        {
            if (!config.OwnerOnlyMount || player == null || mountable == null) return null;

            var vehicle = mountable as BaseVehicle ?? mountable.VehicleParent();
            if (vehicle?.net == null) return null;

            OwnedVehicle owned;
            if (!vehiclesByNetId.TryGetValue(vehicle.net.ID.Value, out owned)) return null;

            if (owned.OwnerId == player.userID) return null;
            if (HasPermission(player, PermAdmin)) return null;

            Message(player, "NotYourVehicle");
            return false;
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
                if (!fuel.MoveToContainer(container.inventory))
                    fuel.Remove();
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
                // Ignore the fuel tank – it is part of the vehicle, not player loot.
                if (container.ShortPrefabName != null && container.ShortPrefabName.Contains("fuel")) continue;
                if (container.inventory.itemList != null && container.inventory.itemList.Count > 0)
                    return true;
            }
            return false;
        }

        private BaseEntity FindEntity(ulong netId)
        {
            if (netId == 0) return null;
            return BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BaseEntity;
        }

        private void ScrubMissing(PlayerData data, string key)
        {
            ulong netId;
            if (data.Owned.TryGetValue(key, out netId) && FindEntity(netId) == null)
            {
                data.Owned.Remove(key);
                vehiclesByNetId.Remove(netId);
            }
        }

        private int CountOwned(PlayerData data)
        {
            var count = 0;
            foreach (var pair in data.Owned)
                if (FindEntity(pair.Value) != null) count++;
            return count;
        }

        private bool HasPermission(BasePlayer player, string perm)
            => permission.UserHasPermission(player.UserIDString, perm);

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

        #region Locating helpers

        private static string Compass(Vector3 from, Vector3 to)
        {
            var dir = to - from;
            var angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;
            string[] points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return points[Mathf.RoundToInt(angle / 45f) % 8];
        }

        // Standard Rust map grid reference, e.g. "G14".
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
            foreach (var kvp in config.Vehicles)
            {
                var s = kvp.Value;
                if (!s.Enabled || !HasPermission(player, s.FullPermission)) continue;

                var cmd = (s.Commands != null && s.Commands.Length > 0) ? "/" + s.Commands[0] : $"/{config.Command} {kvp.Key}";
                var cost = s.Price > 0 ? $"{s.Price} {CurrencyName(s.Currency)}" : Lang("Free", player);
                lines.Add(Lang("HelpEntry", player, s.DisplayName, cmd, cost, FormatTime(s.Cooldown)));
            }
            lines.Add(Lang("HelpFooter", player, config.Command));
            player.ChatMessage(string.Join("\n", lines));
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
                ["AlreadyOwned"] = "You already own a {0}. Use recall or remove it first.",
                ["MaxVehicles"] = "You already own the maximum of <color=#f1c40f>{0}</color> vehicles.",
                ["NotInWater"] = "That vehicle must be spawned in water. Stand near deep water and look at it.",
                ["SpawnFailed"] = "Something went wrong spawning that vehicle.",
                ["Spawned"] = "Your <color=#2ecc71>{0}</color> has been spawned in front of you.",
                ["SpawnedPaid"] = "Your <color=#2ecc71>{0}</color> has been spawned for <color=#f1c40f>{1} {2}</color>.",
                ["Recalled"] = "Your <color=#2ecc71>{0}</color> has been recalled to you.",
                ["Removed"] = "Your <color=#2ecc71>{0}</color> has been removed.",
                ["NothingToRecall"] = "You have no {0} to recall.",
                ["NothingToRemove"] = "You have no {0} to remove.",
                ["NothingToFind"] = "You have no {0} to locate.",
                ["VehicleOccupied"] = "You can't do that while the vehicle is occupied.",
                ["VehicleHasLoot"] = "That vehicle still has items inside it. Empty it first.",
                ["NotYourVehicle"] = "This vehicle belongs to someone else.",
                ["WhereInfo"] = "Your <color=#2ecc71>{0}</color> is <color=#f1c40f>{1}m</color> to the <color=#f1c40f>{2}</color> (grid {3}).",
                ["Free"] = "Free",
                ["HelpHeader"] = "<color=#5dade2>===== Vehicles =====</color>",
                ["HelpEntry"] = "<color=#2ecc71>{0}</color> — {1}  |  cost: {2}  |  cooldown: {3}",
                ["HelpFooter"] = "Manage: <color=#f1c40f>/{0} recall|remove|where <name></color>",
            }, this);
        }

        #endregion
    }
}
