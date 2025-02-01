using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using ServerSync;
using TraderQuests.API;
using TraderQuests.Behaviors;
using TraderQuests.translations;
using UnityEngine;
using UnityEngine.UI;
using YamlDotNet.Serialization;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace TraderQuests.Quest;

public static class BountySystem
{
    private static readonly string BountyFolderPath = TraderQuestsPlugin.FolderPath + Path.DirectorySeparatorChar + "Bounties";
    private static readonly string RecordFilePath = TraderQuestsPlugin.FolderPath + Path.DirectorySeparatorChar + "BountyRecord.yml";
    private static readonly Dictionary<long, List<string>> RecordedCompletedBounties = new();
    private static readonly CustomSyncedValue<string> ServerKillRecord = new(TraderQuestsPlugin.ConfigSync, "ServerKillRecord", "");
    private static readonly CustomSyncedValue<string> ServerBounties = new(TraderQuestsPlugin.ConfigSync, "ServerBountyConfigs", "");
    private static List<BountyConfig> Configs = new();

    public static BountyData? SelectedBounty;
    public static BountyData? SelectedActiveBounty;
    private static readonly Dictionary<string, BountyData> AllBounties = new();
    public static Dictionary<string, BountyData> AvailableBounties = new();
    public static readonly Dictionary<string, BountyData> ActiveBounties = new();
    public static Dictionary<string, long> CompletedBounties = new();
    private static readonly List<BountyData> CurrentBounties = new();

    private static double LastLoadedTime;

    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static class ObjectDB_Awake_Patch
    {
        private static void Postfix()
        {
            if (!ZNetScene.instance) return;
            SetupBounties();
        }
    }

    public static void UpdateMinimap()
    {
        // Remove all pins
        foreach (var bounty in ActiveBounties.Values)
        {
            if (bounty.PinData is null) continue;
            Minimap.instance.RemovePin(bounty.PinData);
        }
        // Add all pins
        foreach (var bounty in ActiveBounties.Values)
        {
            if (bounty.PinData is not null) continue;
            Minimap.PinData pin = Minimap.m_instance.AddPin(bounty.Position, Minimap.PinType.Boss, bounty.Config.Name, false, false);
            pin.m_icon = bounty.Icon;
            bounty.PinData = pin;
        }
    }

    public static void CheckActiveBounties(Player player)
    {
        if (ActiveBounties.Count <= 0) return;

        Vector3 playerPos = player.transform.position;
        BountyData? closestBounty = null;
        float closestDistance = float.MaxValue;

        foreach (BountyData bounty in ActiveBounties.Values)
        {
            if (bounty.Spawned) continue;

            float distance = Vector3.Distance(bounty.Position, playerPos);
            if (distance < closestDistance || closestBounty is null)
            {
                closestBounty = bounty;
                closestDistance = distance;
            }
        }

        if (closestBounty is null) return;

        if (!QuestSystem.IsWithinQuestLocation(closestBounty.Position, playerPos, 10f)) return;

        Vector3 spawnPoint = QuestSystem.FindSpawnPoint(playerPos, 10f);
        if (spawnPoint == Vector3.zero) return;

        closestBounty.Position = spawnPoint;
        closestBounty.Spawned = true;

        Minimap.m_instance.RemovePin(closestBounty.PinData);
        TraderQuestsPlugin.Plugin.QueuedBountyData = closestBounty;
        Effects.PreSpawnEffects.Create(closestBounty.Position, Quaternion.identity);
        TraderQuestsPlugin.Plugin.Invoke(nameof(TraderQuestsPlugin.DelayedSpawn), 5f);
    }

    public static void Init()
    {
        Configs = LoadFiles();

        ServerBounties.ValueChanged += () =>
        {
            if (!ZNet.instance || ZNet.instance.IsServer()) return;
            if (ServerBounties.Value.IsNullOrWhiteSpace()) return;
            var deserializer = new DeserializerBuilder().Build();
            try
            {
                Configs = deserializer.Deserialize<List<BountyConfig>>(ServerBounties.Value);
                LastLoadedTime = 0.0;
                SetupBounties();
            }
            catch
            {
                TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to deserialize server bounties");
            }
        };
    }

    public static void UpdateServerBounties()
    {
        if (Configs.Count <= 0) return;
        var serializer = new SerializerBuilder().Build();
        ServerBounties.Value = serializer.Serialize(Configs);
    }

    private static List<BountyConfig> LoadFiles()
    {
        if (!Directory.Exists(TraderQuestsPlugin.FolderPath)) Directory.CreateDirectory(TraderQuestsPlugin.FolderPath);
        if (!Directory.Exists(BountyFolderPath)) Directory.CreateDirectory(BountyFolderPath);
        var files = Directory.GetFiles(BountyFolderPath, "*.yml", SearchOption.AllDirectories);
        if (files.Length <= 0)
        {
            var data = GetDefaultBounties();
            var serializer = new SerializerBuilder().Build();
            foreach (var config in data)
            {
                var fileName = $"{config.UniqueID}.yml";
                File.WriteAllText(BountyFolderPath + Path.DirectorySeparatorChar + fileName, serializer.Serialize(config));
            }

            return data;
        }

        List<BountyConfig> configs = new();
        foreach (var file in files)
        {
            var deserializer = new DeserializerBuilder().Build();
            try
            {
                configs.Add(deserializer.Deserialize<BountyConfig>(File.ReadAllText(file)));
            }
            catch
            {
                TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to deserialize file: " + Path.GetFileName(file));
            }
        }

        return configs;
    }
    
    private static void SetupBounties()
    {
        if (!ZNetScene.instance || !ObjectDB.instance) return;
        foreach (BountyConfig config in Configs)
        {
            BountyData bounty = new BountyData(config);
            bounty.Setup();
            if (!bounty.IsValid) continue;
            AllBounties[bounty.Config.UniqueID] = bounty;
        }
        AvailableBounties = new Dictionary<string, BountyData>(AllBounties);
        LoadPlayerData();
    }
    
    public static void LoadAvailable()
    {
        if (TraderUI.m_item is null || !TraderUI.m_instance || !ZNet.m_instance) return;
        if (CompletedBounties.Count > 0)
        {
            List<BountyData> bountiesToRemove = new();
            foreach (KeyValuePair<string, long> kvp in CompletedBounties)
            {
                if (!AllBounties.TryGetValue(kvp.Key, out BountyData data)) continue;
                if (data.CooldownPassed())
                {
                    AvailableBounties[data.Config.UniqueID] = data;
                    bountiesToRemove.Add(data);
                }
            }

            foreach (var bounty in bountiesToRemove)
            {
                CompletedBounties.Remove(bounty.Config.UniqueID);
            }
        }

        if (CurrentBounties.Count <= 0 || LastLoadedTime == 0.0 || ZNet.m_instance.GetTimeSeconds() - LastLoadedTime > TraderQuestsPlugin.BountyCooldown.Value * 60)
        {
            CurrentBounties.Clear();
            List<BountyData> bounties = AvailableBounties.Values.ToList();
            for (int index = 0; index < TraderQuestsPlugin.MaxBountyDisplayed.Value; ++index)
            {
                if (GetRandomWeightedBounty(bounties) is not { } bounty) continue;
                bounties.Remove(bounty);
                CurrentBounties.Add(bounty);
            }

            LastLoadedTime = ZNet.m_instance.GetTimeSeconds();
        }
        
        foreach (var bounty in CurrentBounties)
        {
            SetupItem(Object.Instantiate(TraderUI.m_item, TraderUI.m_instance.m_listRoot), bounty, false);
        }
        
        TraderUI.m_instance.ResizeListRoot(CurrentBounties.Count);
    }

    private static BountyData? GetRandomWeightedBounty(List<BountyData> data)
    {
        if (data.Count <= 0) return null;
        float totalWeight = data.Sum(bounty => bounty.Config.Weight);
        if (totalWeight <= 0) return data[Random.Range(0, data.Count)];

        float randomValue = Random.value * totalWeight;
        float cumulativeWeight = 0f;
        
        foreach (var bounty in data)
        {
            cumulativeWeight += bounty.Config.Weight;
            if (randomValue <= cumulativeWeight)
            {
                return bounty;
            }
        }

        return data[Random.Range(0, data.Count)];
    }

    public static void LoadActive()
    {
        if (TraderUI.m_item is null || !TraderUI.m_instance) return;
        foreach (var bounty in ActiveBounties.Values)
        {
            SetupItem(Object.Instantiate(TraderUI.m_item, TraderUI.m_instance.m_activeRoot), bounty, true);
        }
        TraderUI.m_instance.ResizeActiveListRoot(ActiveBounties.Count);
    }
    
    private static void SetupItem(GameObject item, BountyData data, bool active)
    {
        bool enable = data.HasRequirements();
        Text name = Utils.FindChild(item.transform, "$text_name").GetComponent<Text>();
        name.text = data.Config.Name;
        name.color = enable ? new Color32(255, 255, 255, 255) : new Color32(150, 150, 150, 255);
        Image icon = Utils.FindChild(item.transform, "$image_icon").GetComponent<Image>();
        icon.sprite = data.Icon;
        icon.color = enable ? Color.white : Color.gray;
        Image currency = Utils.FindChild(item.transform, "$image_currency").GetComponent<Image>();
        currency.sprite = data.CurrencyIcon;
        currency.color = enable ? Color.white : Color.gray;
        Text currencyText = Utils.FindChild(item.transform, "$text_currency").GetComponent<Text>();
        currencyText.text = data.Config.Price.ToString();
        currencyText.color = enable ? new Color32(255, 164, 0, 255) : new Color32(150, 150, 150, 255);
        Transform selected = item.transform.Find("$image_selected");
        selected.GetComponent<Image>().color = enable ? new Color32(255, 164, 0, 255) : new Color32(255, 164, 0, 200);
        
        item.GetComponent<Button>().onClick.AddListener(() =>
        {
            TraderUI.m_instance.DeselectAll();
            selected.gameObject.SetActive(true);
            TraderUI.m_instance.SetSelectionButtons(Keys.Select, Keys.Cancel);
            if (active)
            {
                TraderUI.m_instance.SetSelectionButtons(Keys.Select, data.Completed ? Keys.Collect : Keys.Cancel);
                SelectedActiveBounty = data;
            }
            else
            {
                if (!QuestSystem.FindSpawnLocation(data.Biome, out Vector3 position))
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Failed to generate spawn location");
                    return;
                }
                data.Position = position;
                SelectedBounty = data;
                TreasureSystem.SelectedTreasure = null;
                TraderUI.m_instance.m_selectButtonText.color = enable ? new Color32(255, 164, 0, 255) : Color.gray;
            }
            TraderUI.m_instance.SetTooltip(data.GetTooltip());
        });
    }

    private static List<BountyConfig> GetDefaultBounties()
    {
        return new List<BountyConfig>()
        {
            new BountyConfig()
            {
                UniqueID = "NeckBounty.0001",
                Name = "Nekken Bounty",
                CurrencyItem = "Coins",
                Price = 10,
                IconPrefab = "TrophyNeck",
                Biome = "Meadows",
                RequiredKey = "defeated_eikthyr",
                Cooldown = 1000f,
                Creatures = new List<BountyConfig.CreatureConfig>()
                {
                    new BountyConfig.CreatureConfig()
                    {
                        UniqueID = "NeckBoss",
                        PrefabName = "Neck",
                        OverrideName = "Nekken",
                        Level = 3,
                        IsBoss = true
                    },
                    new()
                    {
                        UniqueID = "NeckMinion.0001",
                        PrefabName = "Neck",
                        OverrideName = "Nekken Minion",
                        Level = 1,
                    },
                    new()
                    {
                        UniqueID = "NeckMinion.0002",
                        PrefabName = "Neck",
                        OverrideName = "Nekken Minion",
                        Level = 1,
                    }
                },
                Rewards = new List<BountyConfig.RewardConfig>()
                {
                    new()
                    {
                        PrefabName = "Coins",
                        Amount = 50
                    },
                }
            }
        };
    }

    public static void RecordKill(string recordID, long playerID)
    {
        if (!ZNet.m_instance.IsServer())
        {
            // send message to server to run this method
            var server = ZNet.m_instance.GetServerPeer();
            var pkg = new ZPackage();
            pkg.Write(recordID);
            pkg.Write(playerID);
            server.m_rpc.Invoke(nameof(QuestSystem.RPC_RecordKill), pkg);
        }
        else
        {
            if (RecordedCompletedBounties.TryGetValue(playerID, out List<string> records))
            {
                records.Add(recordID);
            }
            else
            {
                RecordedCompletedBounties[playerID] = new List<string>()
                {
                    recordID
                };
            }
            UpdateRecordFile(true);
        }
    }

    public static void RemoveRecord(long playerID, string recordID)
    {
        if (ZNet.m_instance.IsServer())
        {
            if (!RecordedCompletedBounties.TryGetValue(playerID, out List<string> records)) return;
            records.Remove(recordID);
            UpdateRecordFile(false);
        }
        else
        {
            // send message to server to run this method
            var server = ZNet.m_instance.GetServerPeer();
            var pkg = new ZPackage();
            pkg.Write(recordID);
            server.m_rpc.Invoke(nameof(QuestSystem.RPC_RemoveRecord), pkg);
        }
    }

    public static void SetupSync()
    {
        ServerKillRecord.ValueChanged += () =>
        {
            if (ServerKillRecord.Value.IsNullOrWhiteSpace()) return;
            var deserializer = new DeserializerBuilder().Build();
            var data = deserializer.Deserialize<Dictionary<long, List<string>>>(ServerKillRecord.Value);
            CheckRecord(data);
        };
    }

    private static void UpdateRecordFile(bool sync)
    {
        var serializer = new SerializerBuilder().Build();
        var data = serializer.Serialize(RecordedCompletedBounties);
        if (!Directory.Exists(TraderQuestsPlugin.FolderPath)) Directory.CreateDirectory(TraderQuestsPlugin.FolderPath);
        File.WriteAllText(RecordFilePath, data);
        if (sync)
        {
            ServerKillRecord.Value = data;
        }
    }

    private static void CheckRecord(Dictionary<long, List<string>> data)
    {
        if (ActiveBounties.Count <= 0) return;
        // Creature Unique ID example: BountyID.0001:CreatureID.0001

        if (data.TryGetValue(Player.m_localPlayer.GetPlayerID(), out List<string> creatures))
        {
            foreach (string ID in creatures)
            {
                var parts = ID.Split(':');
                var bountyID = parts[0];
                var creatureID = parts[1];
                if (!ActiveBounties.TryGetValue(bountyID, out BountyData bounty)) continue;
                if (!bounty.Creatures.TryGetValue(creatureID, out BountyData.CreatureData creature)) continue;
                if (!creature.Killed)
                {
                    creature.Killed = true;
                    ++bounty.KilledCreatureCount;
                }
                UpdateServerRecord(ID);

            }
        }
        
        foreach (var bounty in ActiveBounties.Values)
        {
            if (bounty.KilledCreatureCount < bounty.Creatures.Count) continue;
            bounty.SetCompleted(true, DateTime.Now.Ticks);
        }
    }

    private static void UpdateServerRecord(string RecordID)
    {
        if (ZNet.m_instance.IsServer())
        {
            RemoveRecord(Player.m_localPlayer.GetPlayerID(), RecordID);
        }
        else
        {
            var server = ZNet.m_instance.GetServerPeer();
            var pkg = new ZPackage();
            pkg.Write(RecordID);
            server.m_rpc.Invoke(nameof(QuestSystem.RPC_RemoveRecord), pkg);
        }
    }

    public static void SaveToPlayer()
    {
        if (!Player.m_localPlayer) return;
        var serializer = new SerializerBuilder().Build();
        if (ActiveBounties.Count > 0)
        {
            Dictionary<string, string> BountyPos = new();
            foreach (var bounty in ActiveBounties.Values)
            {
                BountyPos[bounty.Config.UniqueID] = bounty.FormatPosition();
            }

            Player.m_localPlayer.m_customData["ActiveBounties"] = serializer.Serialize(BountyPos);
        }

        if (CompletedBounties.Count > 0)
        {
            Player.m_localPlayer.m_customData["CompletedBounties"] = serializer.Serialize(CompletedBounties);
        }
    }

    private static void LoadPlayerData()
    {
        if (!Player.m_localPlayer) return;
        var deserializer = new DeserializerBuilder().Build();
        if (Player.m_localPlayer.m_customData.TryGetValue("ActiveBounties", out string activeData))
        {
            Dictionary<string, string> activeBounties = deserializer.Deserialize<Dictionary<string, string>>(activeData);
            foreach (var kvp in activeBounties)
            {
                if (AvailableBounties.TryGetValue(kvp.Key, out BountyData bountyData))
                {
                    bountyData.LoadPosition(kvp.Value);
                    bountyData.Activate(false);
                }
            }
        }

        if (Player.m_localPlayer.m_customData.TryGetValue("CompletedBounties", out string completedData))
        {
            Dictionary<string, long> completedBounties =
                deserializer.Deserialize<Dictionary<string, long>>(completedData);
            foreach (var kvp in completedBounties)
            {
                if (AvailableBounties.TryGetValue(kvp.Key, out BountyData bountyData))
                {
                    bountyData.SetCompleted(true, kvp.Value);
                }
            }

            CompletedBounties = completedBounties;
        }
    }

    [Serializable]
    public class BountyConfig
    {
        public string UniqueID = "";
        public string Name = "";
        public float Weight = 1f;
        public string CurrencyItem = "";
        public string IconPrefab = "";
        public int Price;
        public string Biome = "";
        public string RequiredKey = "";
        public float Cooldown;
        public List<CreatureConfig> Creatures = new();
        public List<RewardConfig> Rewards = new();
        public int EpicMMOExp;
        public int AlmanacExp;
        
        [Serializable]
        public class CreatureConfig
        {
            public string UniqueID = "";
            public string PrefabName = "";
            public string OverrideName = "";
            public int Level = 1;
            public bool IsBoss;
        }

        [Serializable]
        public class RewardConfig
        {
            public string PrefabName = "";
            public int Amount;
            public int Quality;
        }
    }

    public class BountyData
    {
        public readonly BountyConfig Config;
        public bool IsValid = true;
        private string CurrencySharedName = "";
        public Heightmap.Biome Biome = Heightmap.Biome.Meadows;
        public Sprite? Icon;
        public Sprite? CurrencyIcon;
        public Vector3 Position = Vector3.zero;
        public Minimap.PinData? PinData;
        public readonly Dictionary<string, CreatureData> Creatures = new();
        public readonly List<RewardData> Rewards = new();
        public int KilledCreatureCount;
        public bool Completed;
        public bool Spawned;
        public long CompletedOn;

        public BountyData(BountyConfig config) => Config = config;

        public void CollectReward(Player player)
        {
            foreach (var reward in Rewards)
            {
                if (!player.GetInventory().HaveEmptySlot())
                {
                    // spawn item in front of player
                    var prefab = Object.Instantiate(reward.Prefab, player.transform.position, Quaternion.identity);
                    if (prefab.TryGetComponent(out ItemDrop component))
                    {
                        component.m_itemData.m_stack = reward.Config.Amount;
                        component.m_itemData.m_quality = reward.Config.Quality;
                    }
                    ItemDrop.OnCreateNew(prefab);
                }
                else
                {
                    var item = reward.Item.m_itemData.Clone();
                    item.m_quality = Math.Max(reward.Config.Quality, 1);
                    item.m_stack = reward.Config.Amount;
                    player.GetInventory().AddItem(item);
                }
            }

            if (AlmanacClassAPI.installed && Config.AlmanacExp > 0)
            {
                AlmanacClassAPI.AddEXP(Config.AlmanacExp);
            }

            if (EpicMMOSystemAPI.state is EpicMMOSystemAPI.API_State.Ready && Config.EpicMMOExp > 0)
            {
                EpicMMOSystemAPI.AddExp(Config.EpicMMOExp);
            }
        }

        public void SetCompleted(bool completed, long timeCompleted)
        {
            Completed = completed;
            CompletedOn = timeCompleted;
        }

        public bool IsComplete() => Completed;

        public bool CooldownPassed() => DateTime.Now.Ticks > Config.Cooldown + CompletedOn;

        public string GetTooltip()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append($"\n<color=yellow>{Config.Name}</color>\n\n");
            stringBuilder.Append($"{Keys.Location}: {Biome}\n");
            stringBuilder.Append($"{Keys.Distance}: {Mathf.FloorToInt(Vector3.Distance(Player.m_localPlayer.transform.position, Position))}\n");
            stringBuilder.Append($"{Keys.Rewards}:\n");
            if (AlmanacClassAPI.installed && Config.AlmanacExp > 0)
            {
                stringBuilder.Append($"{Keys.AlmanacExp}: {Config.AlmanacExp}");
            }
            if (EpicMMOSystemAPI.state is EpicMMOSystemAPI.API_State.Ready && Config.EpicMMOExp > 0)
            {
                stringBuilder.Append($"{Keys.EpicMMOExp}: {Config.EpicMMOExp}");
            }
            foreach (RewardData reward in Rewards)
            {
                stringBuilder.Append($"{reward.SharedName} x{reward.Config.Amount}\n");
            }

            return stringBuilder.ToString();
        }

        public void Setup()
        {
            if (!ValidateIcon()) IsValid = false;
            if (!ValidateCurrency()) IsValid = false;
            if (!ValidateReward()) IsValid = false;
            if (!ValidateCreatures()) IsValid = false;
            if (!ValidateBiome()) IsValid = false;
        }

        private bool ValidateBiome()
        {
            if (!Enum.TryParse(Config.Biome, true, out Biome))
            {
                TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to validate biome: " + Config.UniqueID);
                return false;
            }
            return true;
        }

        private bool ValidateCreatures()
        {
            foreach (var config in Config.Creatures)
            {
                var creature = new CreatureData(config, Config.UniqueID);
                creature.Setup();
                if (!creature.IsValid)
                {
                    TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to validate creatures: " + Config.UniqueID);
                    return false;
                }
                Creatures[creature.Config.UniqueID] = creature;
            }

            return true;
        }

        private bool ValidateReward()
        {
            foreach (var config in Config.Rewards)
            {
                var reward = new RewardData(config);
                reward.Setup();
                if (!reward.IsValid)
                {
                    TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to validate reward: " + Config.UniqueID);
                    return false;
                }
                Rewards.Add(reward);
            }
            return true;
        }

        private bool ValidateCurrency()
        {
            if (ObjectDB.instance.GetItemPrefab(Config.CurrencyItem) is not { } prefab ||
                !prefab.TryGetComponent(out ItemDrop component))
            {
                TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to validate currency: " + Config.UniqueID);
                return false;
            }
            CurrencyIcon = component.m_itemData.GetIcon();
            CurrencySharedName = component.m_itemData.m_shared.m_name;
            return true;

        }

        private bool ValidateIcon()
        {
            if (ObjectDB.instance.GetItemPrefab(Config.IconPrefab) is { } iconPrefab &&
                iconPrefab.TryGetComponent(out ItemDrop component))
            {
                Icon = component.m_itemData.GetIcon();
                return true;
            }
            TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to validate icon: " + Config.UniqueID);
            return false;
        }

        public bool HasRequirements()
        {
            if (!Player.m_localPlayer) return false;
            if (!Config.RequiredKey.IsNullOrWhiteSpace() && !Player.m_localPlayer.HaveUniqueKey(Config.RequiredKey)) return false;
            if (ActiveBounties.Count >= TraderQuestsPlugin.MaxActiveBounties.Value) return false;
            return Player.m_localPlayer.GetInventory().CountItems(CurrencySharedName) >= Config.Price;
        }

        public bool Activate(bool checkRequirements = true)
        {
            if (checkRequirements && !HasRequirements())
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Missing requirements");
                return false;
            }

            AvailableBounties.Remove(Config.UniqueID);
            ActiveBounties[Config.UniqueID] = this;
            SelectedBounty = null;
            UpdateMinimap();
            return true;
        }

        public string FormatPosition() => $"{Position.x}_{Position.y}_{Position.z}";

        public void LoadPosition(string data)
        {
            var parts = data.Split('_');
            Position = new Vector3(
                float.TryParse(parts[0], out float x) ? x : 0f,
                float.TryParse(parts[1], out float y) ? y : 0f, 
                float.TryParse(parts[2], out float z) ? z : 0f);
        }
        
        public class CreatureData
        {
            public readonly BountyConfig.CreatureConfig Config;
            public readonly string BountyID;
            public bool IsValid = true;
            public bool Killed;
            public GameObject Prefab = null!;

            public CreatureData(BountyConfig.CreatureConfig config, string bountyID)
            {
                Config = config;
                BountyID = bountyID;
            }

            public void Setup()
            {
                if (!GetCreature()) IsValid = false;
            }
            
            private bool GetCreature()
            {
                if (ZNetScene.instance.GetPrefab(Config.PrefabName) is not { } prefab) return false;
                Prefab = prefab;
                return true;
            }
        }

        public class RewardData
        {
            public readonly BountyConfig.RewardConfig Config;
            public bool IsValid = true;
            public GameObject Prefab = null!;
            public ItemDrop Item = null!;
            public string SharedName = "";

            public void Setup()
            {
                if (!GetPrefab()) IsValid = false;
            }

            private bool GetPrefab()
            {
                if (ObjectDB.instance.GetItemPrefab(Config.PrefabName) is not { } prefab || !prefab.TryGetComponent(out ItemDrop component))
                {
                    return false;
                }
                Prefab = prefab;
                Item = component;
                Item.m_itemData.m_dropPrefab = prefab;
                SharedName = component.m_itemData.m_shared.m_name;
                return true;
            }

            public RewardData(BountyConfig.RewardConfig config) => Config = config;
        }
    }
}