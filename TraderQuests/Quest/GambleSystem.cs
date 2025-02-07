using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using ServerSync;
using TraderQuests.Behaviors;
using UnityEngine;
using YamlDotNet.Serialization;
using Random = UnityEngine.Random;

namespace TraderQuests.Quest;

public static class GambleSystem
{
    private static readonly string GambleFolderPath = TraderQuestsPlugin.FolderPath + Path.DirectorySeparatorChar + "Gamble";

    private static readonly CustomSyncedValue<string> ServerGambler = new(TraderQuestsPlugin.ConfigSync, "ServerGambleData", "");
    private static readonly List<GambleItem> Rewards = new();
    private static List<GambleConfig> Configs = new();

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static class ObjectDB_Awake_Patch
    {
        private static void Postfix()
        {
            if (!ZNetScene.instance) return;
            LoadAvailable();
        }
    }

    public static void Init()
    {
        if (!Directory.Exists(TraderQuestsPlugin.FolderPath)) Directory.CreateDirectory(TraderQuestsPlugin.FolderPath);
        if (!Directory.Exists(GambleFolderPath)) Directory.CreateDirectory(GambleFolderPath);
        Configs = GetDefaultConfigs();
        var deserializer = new DeserializerBuilder().Build();
        var files = Directory.GetFiles(GambleFolderPath, "*.yml", SearchOption.AllDirectories);
        if (files.Length <= 0)
        {
            var serializer = new SerializerBuilder().Build();
            var filePath = GambleFolderPath + Path.DirectorySeparatorChar + "0001.Gambler.yml";
            File.WriteAllText(filePath, serializer.Serialize(Configs));
        }
        else
        {
            Configs.Clear();
            foreach (var filePath in files)
            {
                var fileName = Path.GetFileName(filePath);
                try
                {
                    var data = deserializer.Deserialize<List<GambleConfig>>(File.ReadAllText(filePath));
                    Configs.AddRange(data);
                }
                catch
                {
                    TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to deserialize gamble file: " + fileName);
                }
            }
        }
        SetupFileWatch();
        
        ServerGambler.ValueChanged += () =>
        {
            if (!ZNet.m_instance || !ZNet.m_instance.IsServer()) return;
            if (ServerGambler.Value.IsNullOrWhiteSpace()) return;
            try
            {
                Configs = deserializer.Deserialize<List<GambleConfig>>(ServerGambler.Value);
                Rewards.Clear();
                LoadAvailable();
            }
            catch
            {
                TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to deserialize server gambler data");
            }
        };
    }

    public static void UpdateServer()
    {
        var serializer = new SerializerBuilder().Build();
        ServerGambler.Value = serializer.Serialize(Configs);
    }

    private static void SetupFileWatch()
    {
        FileSystemWatcher watcher = new FileSystemWatcher(GambleFolderPath, "*.yml");

        void OnFileChange(object sender, FileSystemEventArgs args) => Reload();

        watcher.Changed += OnFileChange;
        watcher.Deleted += OnFileChange;
        watcher.Created += OnFileChange;

        watcher.IncludeSubdirectories = true;
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.EnableRaisingEvents = true;
    }

    private static void Reload()
    {
        Rewards.Clear();
        Init();
        LoadAvailable();
        UpdateServer();
    }

    private static void LoadAvailable()
    {
        foreach (var config in Configs)
        {
            var item = new GambleItem(config);
            item.Setup();
            if (!item.IsValid) continue;
            Rewards.Add(item);
        }
    }

    public static GambleItem? GetItem()
    {
        if (Rewards.Count <= 0) return null;
        List<GambleItem> availableItems = Rewards.Where(item => item.HasRequirements()).ToList();
        if (availableItems.Count <= 0) return null;
        return availableItems[Random.Range(0, Rewards.Count)];
    }

    private static List<GambleConfig> GetDefaultConfigs()
    {
        return new List<GambleConfig>()
        {
            Add("KnifeFlint", 1, 3),
            Add("TraderCoin_RS", 1),
            Add("TraderCoin_RS", 2),
            Add("TraderCoin_RS", 3),
            Add("Wood", 100, 1, 50f),
            Add("LeatherScraps", 20)
        };
    }

    private static GambleConfig Add(string prefabName, int amount, int quality = 1, float successChance = 33f, int price = 1, string key = "") => new()
    {
        PrefabName = prefabName, Amount = amount, Quality = quality, SuccessChance = successChance,RequiredKey = key, Price = price
    };
    public class GambleItem
    {
        public readonly GambleConfig Config;
        public bool IsValid = true;
        public ItemDrop.ItemData ItemData = null!;
        public string SharedName = "";
        public Sprite Icon = null!;
        public string CurrencySharedName = "";
        public Sprite CurrencyIcon = null!;
        public bool Completed;

        public void Setup()
        {
            if (!ValidateReward()) IsValid = false;
            if (!ValidateCurrency()) IsValid = false;
            ValidateRequiredMatch();
        }

        public bool HasRequirements()
        {
            if (Player.m_localPlayer.GetInventory().CountItems(CurrencySharedName) < Config.Price) return false;
            return Config.RequiredKey.IsNullOrWhiteSpace() || Player.m_localPlayer.HaveUniqueKey(Config.RequiredKey);
        }

        public bool CollectReward()
        {
            if (!Player.m_localPlayer || !Completed) return false;
            if (!Player.m_localPlayer.GetInventory().HaveEmptySlot())
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Inventory is full");
                return false;
            }

            var item = ItemData.Clone();
            item.m_stack = Config.Amount;
            item.m_quality = Config.Quality;
            item.m_durability = item.GetMaxDurability();
            Player.m_localPlayer.GetInventory().AddItem(item);
            Completed = false;
            GambleUI.m_instance.SetupReward(GetItem());
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "New item added to inventory: " + SharedName);
            return true;
        }

        private bool ValidateReward()
        {
            if (ObjectDB.m_instance.GetItemPrefab(Config.PrefabName) is not { } prefab ||
                !prefab.TryGetComponent(out ItemDrop component) ||
                component.m_itemData.m_shared.m_icons.Length <= 0)
            {
                TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to validate gamble reward: " + Config.PrefabName);
                return false;
            }
            ItemData = component.m_itemData;
            ItemData.m_dropPrefab = prefab;
            Icon = component.m_itemData.GetIcon();
            SharedName = component.m_itemData.m_shared.m_name;
            return true;
        }

        private bool ValidateCurrency()
        {
            if (ObjectDB.m_instance.GetItemPrefab(Config.CurrencyPrefab) is not { } prefab ||
                !prefab.TryGetComponent(out ItemDrop component) || component.m_itemData.m_shared.m_icons.Length <= 0)
            {
                TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to validate gamble currency: " +
                                                                 Config.CurrencyPrefab);
                return false;
            }

            CurrencySharedName = component.m_itemData.m_shared.m_name;
            CurrencyIcon = component.m_itemData.GetIcon();
            return true;
        }

        private void ValidateRequiredMatch()
        {
            Config.SuccessChance = Mathf.Clamp(Config.SuccessChance, 0f, 100f);
        }

        public GambleItem(GambleConfig config) => Config = config;
    }
    
    [Serializable]
    public class GambleConfig
    {
        public string PrefabName = "";
        public int Amount = 1;
        public int Quality = 1;
        public string RequiredKey = "";
        public string CurrencyPrefab = "TraderCoin_RS";
        public int Price = 1;
        public float SuccessChance = 100f;
    }
}