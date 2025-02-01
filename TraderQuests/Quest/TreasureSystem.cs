using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using ServerSync;
using TraderQuests.Behaviors;
using TraderQuests.translations;
using UnityEngine;
using UnityEngine.UI;
using YamlDotNet.Serialization;

namespace TraderQuests.Quest;

public static class TreasureSystem
{
    private static readonly string TreasureFolderPath = TraderQuestsPlugin.FolderPath + Path.DirectorySeparatorChar + "Treasures";
    private static readonly CustomSyncedValue<string> ServerTreasures = new(TraderQuestsPlugin.ConfigSync, "ServerTreasureConfigs", "");
    private static List<TreasureData.TreasureConfig> Configs = new();
    private static GameObject? TreasureBarrel;
    public static readonly Dictionary<string, TreasureData> AllTreasures = new();
    public static Dictionary<string, TreasureData> AvailableTreasures = new();
    public static readonly Dictionary<string, TreasureData> ActiveTreasures = new();
    private static readonly Dictionary<string, TreasureData> CompletedTreasures = new();
    public static TreasureData? SelectedTreasure;
    public static TreasureData? SelectedActiveTreasure;

    private static double LastLoadedTime;
    private static readonly List<TreasureData> CurrentTreasures = new();

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        private static void Postfix(ZNetScene __instance)
        {
            if (__instance.GetPrefab("barrell") is not { } barrel) return;
            GameObject clone = UnityEngine.Object.Instantiate(barrel, TraderQuestsPlugin.Root.transform, false);
            clone.name = "TreasureBarrel_RS";
            if (clone.TryGetComponent(out DropOnDestroyed dropOnDestroyed)) UnityEngine.Object.Destroy(dropOnDestroyed);
            clone.AddComponent<Treasure>();
            clone.AddComponent<HoverText>();
            clone.AddComponent<Beacon>().m_range = 50f;
            if (!__instance.m_prefabs.Contains(clone)) __instance.m_prefabs.Add(clone);
            __instance.m_namedPrefabs[clone.name.GetStableHashCode()] = clone;
            TreasureBarrel = clone;
        }
    }

    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static class ObjectDB_Awake_Patch
    {
        private static void Postfix()
        {
            if (!ZNetScene.instance) return;
            SetupTreasures();
        }
    }

    public static void UpdateMinimap()
    {
        // Remove all pins
        foreach (var treasure in ActiveTreasures.Values)
        {
            if (treasure.PinData is null) continue;
            Minimap.m_instance.RemovePin(treasure.PinData);
        }
        // Add all pins
        foreach (var treasure in ActiveTreasures.Values)
        {
            if (treasure.PinData is not null) continue;
            Minimap.PinData pin = Minimap.m_instance.AddPin(treasure.Position, Minimap.PinType.Boss, treasure.Config.Name, false, false);
            pin.m_icon = treasure.Icon;
            treasure.PinData = pin;
        }
    }

    public static void CheckActiveTreasures(Player player)
    {
        if (ActiveTreasures.Count <= 0) return;
        Vector3 playerPos = player.transform.position;
        TreasureData? closestTreasure = null;
        float closestDistance = float.MaxValue;
        foreach (var treasure in ActiveTreasures.Values)
        {
            if (treasure.Spawned) continue;
            float distance = Vector3.Distance(treasure.Position, playerPos);
            if (distance < closestDistance || closestTreasure is null)
            {
                closestTreasure = treasure;
                closestDistance = distance;
            }
        }

        if (closestTreasure is null) return;

        if (!QuestSystem.IsWithinQuestLocation(closestTreasure.Position, playerPos, 50f)) return;

        Vector3 spawnPoint = QuestSystem.FindSpawnPoint(closestTreasure.Position, 10f);
        if (spawnPoint == Vector3.zero) return;

        closestTreasure.Position = spawnPoint;
        closestTreasure.Spawned = true;

        ActiveTreasures.Remove(closestTreasure.Config.UniqueID);
        CompletedTreasures[closestTreasure.Config.UniqueID] = closestTreasure;

        Minimap.m_instance.RemovePin(closestTreasure.PinData);
        Spawn(closestTreasure);
    }

    private static void Spawn(TreasureData data)
    {
        if (TreasureBarrel is null) return;
        var prefab = UnityEngine.Object.Instantiate(TreasureBarrel, data.Position, Quaternion.identity);
        var component = prefab.GetComponent<Treasure>();
        component.SetData(data);
    }

    public static void Init()
    {
        Configs = LoadFiles();

        ServerTreasures.ValueChanged += () =>
        {
            if (!ZNet.instance || ZNet.instance.IsServer()) return;
            if (ServerTreasures.Value.IsNullOrWhiteSpace()) return;
            var deserializer = new DeserializerBuilder().Build();
            try
            {
                Configs = deserializer.Deserialize<List<TreasureData.TreasureConfig>>(ServerTreasures.Value);
                LastLoadedTime = 0.0;
                SetupTreasures();
            }
            catch
            {
                TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to deserialize server files");
            }
        };
    }

    public static void UpdateServerTreasures()
    {
        if (Configs.Count <= 0) return;
        var serializer = new SerializerBuilder().Build();
        ServerTreasures.Value = serializer.Serialize(Configs);
    }

    private static void SetupTreasures()
    {
        foreach (var config in Configs)
        {
            TreasureData treasure = new TreasureData(config);
            treasure.Setup();
            if (!treasure.IsValid) continue;
            AllTreasures[treasure.Config.UniqueID] = treasure;
        }

        AvailableTreasures = new Dictionary<string, TreasureData>(AllTreasures);
        LoadPlayerData();
    }

    public static void SaveToPlayer()
    {
        if (!Player.m_localPlayer) return;
        var serializer = new SerializerBuilder().Build();
        Player.m_localPlayer.m_customData.Remove("ActiveTreasures");
        Player.m_localPlayer.m_customData.Remove("CompletedTreasures");
        if (ActiveTreasures.Count > 0)
        {
            Dictionary<string, string> treasurePos = new();
            foreach (var treasure in ActiveTreasures.Values)
            {
                treasurePos[treasure.Config.UniqueID] = treasure.FormatPosition();
            }

            Player.m_localPlayer.m_customData["ActiveTreasures"] = serializer.Serialize(treasurePos);
        }

        if (CompletedTreasures.Count > 0)
        {
            Dictionary<string, long> completedTreasures = new();
            foreach (var treasure in CompletedTreasures.Values)
            {
                completedTreasures[treasure.Config.UniqueID] = treasure.CompletedOn;
            }

            Player.m_localPlayer.m_customData["CompletedTreasures"] = serializer.Serialize(completedTreasures);
        }
    }

    private static void LoadPlayerData()
    {
        if (!Player.m_localPlayer) return;
        var deserializer = new DeserializerBuilder().Build();
        if (Player.m_localPlayer.m_customData.TryGetValue("ActiveTreasures", out string activeData))
        {
            var activeTreasures = deserializer.Deserialize<Dictionary<string, string>>(activeData);
            foreach (var kvp in activeTreasures)
            {
                if (AvailableTreasures.TryGetValue(kvp.Key, out TreasureData treasureData))
                {
                    treasureData.LoadPosition(kvp.Value);
                    treasureData.Activate(false);
                }
            }
        }

        if (Player.m_localPlayer.m_customData.TryGetValue("CompletedTreasures", out string completedData))
        {
            var completedTreasures = deserializer.Deserialize<Dictionary<string, long>>(completedData);
            foreach (var kvp in completedTreasures)
            {
                if (AvailableTreasures.TryGetValue(kvp.Key, out TreasureData treasureData))
                {
                    treasureData.SetCompleted(true, kvp.Value);
                    AvailableTreasures.Remove(kvp.Key);
                }
            }
        }
    }

    private static List<TreasureData.TreasureConfig> LoadFiles()
    {
        if (!Directory.Exists(TraderQuestsPlugin.FolderPath)) Directory.CreateDirectory(TraderQuestsPlugin.FolderPath);
        if (!Directory.Exists(TreasureFolderPath)) Directory.CreateDirectory(TreasureFolderPath);
        var files = Directory.GetFiles(TreasureFolderPath, "*.yml", SearchOption.AllDirectories);
        if (files.Length <= 0)
        {
            var data = GetDefaultTreasures();
            var serializer = new SerializerBuilder().Build();
            foreach (var config in data)
            {
                var fileName = $"{config.UniqueID}.yml";
                File.WriteAllText(TreasureFolderPath + Path.DirectorySeparatorChar + fileName, serializer.Serialize(config));
            }
            return data;
        }
        List<TreasureData.TreasureConfig> configs = new();
        var deserializer = new DeserializerBuilder().Build();
        foreach (var file in files)
        {
            try
            {
                configs.Add(deserializer.Deserialize<TreasureData.TreasureConfig>(File.ReadAllText(file)));
            }
            catch
            {
                TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to deserialize file: " + Path.GetFileName(file));
            }
        }

        return configs;
    }

    public static void LoadAvailable()
    {
        if (TraderUI.m_item is null || !TraderUI.m_instance || !ZNet.m_instance) return;

        if (CompletedTreasures.Count > 0)
        {
            List<TreasureData> treasuresToRemove = new();
            foreach (var treasure in CompletedTreasures.Values)
            {
                if (treasure.CoolDownPassed())
                {
                    AvailableTreasures[treasure.Config.UniqueID] = treasure;
                    treasuresToRemove.Add(treasure);
                }
            }

            foreach (var treasure in treasuresToRemove)
            {
                CompletedTreasures.Remove(treasure.Config.UniqueID);
            }
        }

        if (LastLoadedTime == 0.0 || CurrentTreasures.Count <= 0 || ZNet.m_instance.GetTimeSeconds() - LastLoadedTime >
            TraderQuestsPlugin.TreasureCooldown.Value * 60)
        {
            CurrentTreasures.Clear();
            List<TreasureData> treasures = new List<TreasureData>(AvailableTreasures.Values.ToList());
            for (int index = 0; index < TraderQuestsPlugin.MaxTreasureDisplayed.Value; ++index)
            {
                if (GetRandomWeightedTreasure(treasures) is { } treasure)
                {
                    CurrentTreasures.Add(treasure);
                    treasures.Remove(treasure);
                }
            }
            LastLoadedTime = ZNet.m_instance.GetTimeSeconds();
        }
        
        foreach (var treasure in CurrentTreasures)
        {
            SetupItem(UnityEngine.Object.Instantiate(TraderUI.m_item, TraderUI.m_instance.m_listRoot), treasure, false);

        }
        TraderUI.m_instance.ResizeListRoot(AvailableTreasures.Count);
    }

    private static TreasureData? GetRandomWeightedTreasure(List<TreasureData> data)
    {
        if (data.Count <= 0) return null;
        float totalWeight = data.Sum(bounty => bounty.Config.Weight);
        if (totalWeight <= 0) return data[UnityEngine.Random.Range(0, data.Count)];

        float randomValue = UnityEngine.Random.value * totalWeight;
        float cumulativeWeight = 0f;
        
        foreach (var treasure in data)
        {
            cumulativeWeight += treasure.Config.Weight;
            if (randomValue <= cumulativeWeight)
            {
                return treasure;
            }
        }

        return data[UnityEngine.Random.Range(0, data.Count)];
    }

    public static void LoadActive()
    {
        if (TraderUI.m_item is null) return;
        foreach (var treasure in ActiveTreasures.Values)
        {
            SetupItem(UnityEngine.Object.Instantiate(TraderUI.m_item, TraderUI.m_instance.m_activeRoot), treasure, true);
        }
        TraderUI.m_instance.ResizeActiveListRoot(ActiveTreasures.Count);   
    }
    
    private static void SetupItem(GameObject item, TreasureData data, bool active)
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
                SelectedActiveTreasure = data;
            }
            else
            {
                if (!QuestSystem.FindSpawnLocation(data.Biome, out Vector3 position))
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Failed to generate spawn location");
                    return;
                }
                data.Position = position;
                SelectedTreasure = data;
            }
            BountySystem.SelectedActiveBounty = null;
            TraderUI.m_instance.m_selectButtonText.color = enable ? new Color32(255, 164, 0, 255) : Color.gray;
            TraderUI.m_instance.SetTooltip(data.GetTooltip());
        });
    }

    private static List<TreasureData.TreasureConfig> GetDefaultTreasures()
    {
        return new List<TreasureData.TreasureConfig>()
        {
            new TreasureData.TreasureConfig()
            {
                UniqueID = "MeadowTreasure.0001",
                Name = "Meadow Treasure I",
                Biome = "Meadows",
                CurrencyPrefab = "Coins",
                Price = 10,
                Rewards = new List<TreasureData.TreasureConfig.RewardConfig>()
                {
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "Wood",
                        Amount = 10,
                    },
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "TraderCoin_RS",
                        Amount = 1,
                    },
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "Club",
                        Amount = 1,
                        Quality = 3
                    },
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "Wood",
                        Amount = 10,
                    },
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "TraderCoin_RS",
                        Amount = 1,
                    },
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "Club",
                        Amount = 1,
                        Quality = 3
                    },
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "Wood",
                        Amount = 10,
                    },
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "TraderCoin_RS",
                        Amount = 1,
                    },
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "Club",
                        Amount = 1,
                        Quality = 3
                    },
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "Wood",
                        Amount = 10,
                    },
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "TraderCoin_RS",
                        Amount = 1,
                    },
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "Club",
                        Amount = 1,
                        Quality = 3
                    },
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "Wood",
                        Amount = 10,
                    },
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "TraderCoin_RS",
                        Amount = 1,
                    },
                    new TreasureData.TreasureConfig.RewardConfig()
                    {
                        PrefabName = "Club",
                        Amount = 1,
                        Quality = 3
                    }
                }
            }
        };
    }


    public class TreasureData
    {
        public readonly TreasureConfig Config;
        public bool IsValid = true;
        public Heightmap.Biome Biome;
        public Sprite? Icon;
        public Sprite? CurrencyIcon;
        private string CurrencySharedName = "";
        public Vector3 Position;
        public Minimap.PinData? PinData;
        public readonly List<RewardData> Rewards = new();
        private bool Completed;
        public long CompletedOn;
        public bool Spawned;

        public void Setup()
        {
            if (!ValidateBiome()) IsValid = false;
            if (!ValidateRewards()) IsValid = false;
            if (!ValidateIcon()) IsValid = false;
            if (!ValidateCurrency()) IsValid = false;
        }

        public string GetTooltip()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append($"\n<color=yellow>{Config.Name}</color>\n\n");
            stringBuilder.Append($"{Keys.Location}: {Biome}\n");
            stringBuilder.Append($"{Keys.Distance}: {Mathf.FloorToInt(Vector3.Distance(Player.m_localPlayer.transform.position, Position))}\n");
            stringBuilder.Append($"{Keys.Rewards}:\n");
            foreach (var reward in Rewards)
            {
                stringBuilder.Append($"{reward.SharedName} x{reward.Config.Amount}\n");
            }

            return stringBuilder.ToString();
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

        public void SetCompleted(bool completed, long completedOn)
        {
            Completed = completed;
            CompletedOn = completedOn;
            CompletedTreasures[Config.UniqueID] = this;
        }

        public bool CoolDownPassed() => DateTime.Now.Ticks > Config.Cooldown + CompletedOn;

        public bool HasRequirements()
        {
            if (!Player.m_localPlayer) return false;
            if (!Config.RequiredKey.IsNullOrWhiteSpace() && !Player.m_localPlayer.HaveUniqueKey(Config.RequiredKey)) return false;
            if (ActiveTreasures.Count >= TraderQuestsPlugin.MaxActiveTreasures.Value) return false;
            return Player.m_localPlayer.GetInventory().CountItems(CurrencySharedName) >= Config.Price;
        }

        public bool Activate(bool checkRequirements = true)
        {
            if (checkRequirements && !HasRequirements())
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Missing requirements");
                return false;
            }

            AvailableTreasures.Remove(Config.UniqueID);
            ActiveTreasures[Config.UniqueID] = this;
            SelectedTreasure = null;
            UpdateMinimap();
            return true;
        }

        private bool ValidateCurrency()
        {
            if (ObjectDB.m_instance.GetItemPrefab(Config.CurrencyPrefab) is not { } prefab || !prefab.TryGetComponent(out ItemDrop component)) return false;
            CurrencySharedName = component.m_itemData.m_shared.m_name;
            CurrencyIcon = component.m_itemData.GetIcon();
            return true;
        }

        private bool ValidateBiome()
        {
            if (!Enum.TryParse(Config.Biome, true, out Biome)) return false;
            return true;
        }

        private bool ValidateRewards()
        {
            foreach (var config in Config.Rewards)
            {
                var reward = new RewardData(config);
                reward.Setup();
                if (!reward.IsValid) return false;
                Rewards.Add(reward);
            }

            return true;
        }

        private bool ValidateIcon()
        {
            if (ObjectDB.m_instance.GetItemPrefab(Config.IconPrefab) is not { } prefab ||
                !prefab.TryGetComponent(out ItemDrop component)) return false;
            Icon = component.m_itemData.GetIcon();
            return true;
        }

        public TreasureData(TreasureConfig config) => Config = config;

        public class RewardData
        {
            public readonly TreasureConfig.RewardConfig Config;
            public bool IsValid = true;
            public GameObject Prefab = null!;
            public string SharedName = "";

            public void Setup()
            {
                if (!ValidateReward()) IsValid = false;
            }

            private bool ValidateReward()
            {
                if (ObjectDB.instance.GetItemPrefab(Config.PrefabName) is not { } prefab || !prefab.TryGetComponent(out ItemDrop component))
                {
                    return false;
                }

                Prefab = prefab;
                SharedName = component.m_itemData.m_shared.m_name;
                return true;
            }

            public RewardData(TreasureConfig.RewardConfig config) => Config = config;
        }

        [Serializable]
        public class TreasureConfig
        {
            public string UniqueID = "";
            public string Name = "";
            public float Weight = 1f;
            public string RequiredKey = "";
            public string Biome = "";
            public string IconPrefab = "TraderCoin_RS";
            public string CurrencyPrefab = "";
            public int Price;
            public long Cooldown;
            public List<RewardConfig> Rewards = new();

            [Serializable]
            public class RewardConfig
            {
                public string PrefabName = "";
                public int Amount;
                public int Quality;
            }
        }
    }
}