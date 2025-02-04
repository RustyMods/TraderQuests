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
using YamlDotNet.Serialization;
using Random = UnityEngine.Random;

namespace TraderQuests.Quest;

public static class Shop
{
    private static readonly string ShopFolderPath = TraderQuestsPlugin.FolderPath + Path.DirectorySeparatorChar + "Store";
    private static readonly CustomSyncedValue<string> ServerStore = new(TraderQuestsPlugin.ConfigSync, "ServerStoreData", "");
    private static List<ItemConfig> Configs = new();
    private static readonly List<StoreItem> AllItems = new();
    public static StoreItem? SelectedItem;
    public static StoreItem? SelectedSaleItem;

    private static double LastLoadedTime;
    private static List<StoreItem> AvailableItems = new();
    private static List<StoreItem> OnSaleItems = new();
    public static void Init()
    {
        if (!Directory.Exists(TraderQuestsPlugin.FolderPath)) Directory.CreateDirectory(TraderQuestsPlugin.FolderPath);
        if (!Directory.Exists(ShopFolderPath)) Directory.CreateDirectory(ShopFolderPath);
        var files = Directory.GetFiles(ShopFolderPath, "*.yml", SearchOption.AllDirectories);
        if (files.Length <= 0)
        {
            Configs = LoadDefaults();
            var serializer = new SerializerBuilder().Build();
            var filePath = ShopFolderPath + Path.DirectorySeparatorChar + "0001.Shop.yml";
            File.WriteAllText(filePath, serializer.Serialize(Configs));
        }
        else
        {
            var deserializer = new DeserializerBuilder().Build();
            foreach (var file in files)
            {
                try
                {
                    Configs = deserializer.Deserialize<List<ItemConfig>>(file);
                }
                catch
                {
                    TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to deserialize file: " + Path.GetFileName(file));
                }
            }
        }
        Configs.Add(GetTraderRingConfig());
        
        ServerStore.ValueChanged += () =>
        {
            if (!ZNet.m_instance || ZNet.m_instance.IsServer()) return;
            if (ServerStore.Value.IsNullOrWhiteSpace()) return;
            var deserializer = new DeserializerBuilder().Build();
            try
            {
                Configs = deserializer.Deserialize<List<ItemConfig>>(ServerStore.Value);
                Configs.Add(GetTraderRingConfig());
                LastLoadedTime = 0.0;
                SetupStore();
            }
            catch
            {
                TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to deserialize server store");
            }
        };
    }
    public static void UpdateServerStore()
    {
        if (!ZNet.m_instance || !ZNet.m_instance.IsServer()) return;
        if (Configs.Count <= 0) return;
        var serializer = new SerializerBuilder().Build();
        ServerStore.Value = serializer.Serialize(Configs);
    }
    private static ItemConfig GetTraderRingConfig()
    {
        return new ItemConfig()
        {
            PrefabName = "TraderRing_RS",
            CurrencyPrefab = "TraderCoin_RS",
            Price = 100,
            OnSalePrice = 80,
            RequiredKey = "defeated_bonemass",
            Weight = 0.1f
        };
    }
    private static void SetupStore()
    {
        foreach (var config in Configs)
        {
            var item = new StoreItem(config);
            item.Setup();
            if (!item.IsValid) continue;
            AllItems.Add(item);
        }
    }
    private static List<ItemConfig> LoadDefaults()
    {
        return new()
        {
            // Bronze Tier Weapons
            new ItemConfig()
            {
                PrefabName = "SwordBronze",
                Stack = 1,
                Quality = 2,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 10,
                RequiredKey = "defeated_eikthyr"
            },
            new ItemConfig()
            {
                PrefabName = "MaceBronze",
                Stack = 1,
                Quality = 2,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 12,
                RequiredKey = "defeated_eikthyr"
            },
            new ItemConfig()
            {
                PrefabName = "AtgeirBronze",
                Stack = 1,
                Quality = 2,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 15,
                RequiredKey = "defeated_eikthyr"
            },

            // Bronze Tier Armor
            new ItemConfig()
            {
                PrefabName = "HelmetBronze",
                Stack = 1,
                Quality = 2,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 8,
                RequiredKey = "defeated_eikthyr"
            },
            new ItemConfig()
            {
                PrefabName = "ArmorBronzeChest",
                Stack = 1,
                Quality = 2,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 15,
                RequiredKey = "defeated_eikthyr"
            },

            // Consumables & Special Items
            new ItemConfig()
            {
                PrefabName = "MeadHealthMinor",
                Stack = 10,
                Quality = 1,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 3,
                RequiredKey = "defeated_eikthyr"
            },
            new ItemConfig()
            {
                PrefabName = "ArrowFire",
                Stack = 20,
                Quality = 1,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 1,
                RequiredKey = "defeated_eikthyr"
            },

            // Unique / Special Items
            new ItemConfig()
            {
                PrefabName = "DragonEgg",
                Stack = 3,
                Quality = 1,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 50,
                CanBeOnSale = false,
                RequiredKey = "defeated_dragon"
            },

            // Rare Tier Weapons
            new ItemConfig()
            {
                PrefabName = "SwordSilver",
                Stack = 1,
                Quality = 3,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 50,
                RequiredKey = "defeated_bonemass"
            },

            // Special Consumables
            new ItemConfig()
            {
                PrefabName = "MeadFrostResist",
                Stack = 10,
                Quality = 1,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 25,
                RequiredKey = "defeated_bonemass"
            },

            // Rare Tier Armor
            new ItemConfig()
            {
                PrefabName = "HelmetDrake",
                Stack = 1,
                Quality = 3,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 30,
                RequiredKey = "defeated_bonemass"
            },
            new ItemConfig()
            {
                PrefabName = "ArmorWolfChest",
                Stack = 1,
                Quality = 3,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 60,
                RequiredKey = "defeated_bonemass"
            }
        };
    }
    public static void LoadAvailable()
    {
        if (!TraderUI.m_instance || TraderUI.m_item is null || !ZNet.m_instance) return;
        if (LastLoadedTime == 0.0 || ZNet.m_instance.GetTimeSeconds() - LastLoadedTime > TraderQuestsPlugin.ShopCooldown.Value * 60)
        {
            List<StoreItem> storeItems = AllItems.Where(item => item.HasRequiredKey()).ToList();
            List<StoreItem> items = new List<StoreItem>();
            List<StoreItem> saleItems = new List<StoreItem>();

            for (int index = 0; index < TraderQuestsPlugin.MaxSaleItems.Value; ++index)
            {
                if (GetRandomWeightedItem(storeItems.Where(storeItem => storeItem .Config.CanBeOnSale).ToList()) is { } item)
                {
                    saleItems.Add(item);
                    storeItems.Remove(item);
                }
            }

            for (int index = 0; index < TraderQuestsPlugin.MaxShopItems.Value; ++index)
            {
                if (GetRandomWeightedItem(storeItems) is { } item)
                {
                    items.Add(item);
                    storeItems.Remove(item);
                }
            }

            AvailableItems = items;
            OnSaleItems = saleItems;

            LastLoadedTime = ZNet.m_instance.GetTimeSeconds();
        }
        
        foreach (var data in AvailableItems)
        {
            SetupItem(UnityEngine.Object.Instantiate(TraderUI.m_item, TraderUI.m_instance.m_listRoot), data, false);
        }
        TraderUI.m_instance.ResizeListRoot(AvailableItems.Count);
        foreach (var data in OnSaleItems)
        {
            SetupItem(UnityEngine.Object.Instantiate(TraderUI.m_item, TraderUI.m_instance.m_activeRoot), data, true);
        }
        TraderUI.m_instance.ResizeActiveListRoot(OnSaleItems.Count);
    }
    private static StoreItem? GetRandomWeightedItem(List<StoreItem> data)
    {
        if (data.Count <= 0) return null;
        float totalWeight = data.Sum(bounty => bounty.Config.Weight);
        if (totalWeight <= 0) return data[Random.Range(0, data.Count)];

        float randomValue = Random.value * totalWeight;
        float cumulativeWeight = 0f;
        
        foreach (var treasure in data)
        {
            cumulativeWeight += treasure.Config.Weight;
            if (randomValue <= cumulativeWeight)
            {
                return treasure;
            }
        }

        return data[Random.Range(0, data.Count)];
    }
    private static void SetupItem(GameObject item, StoreItem data, bool onSale)
    {
        if (!item.TryGetComponent(out ItemUI component)) return;
        bool enable = data.HasRequirements(onSale);
        
        component.SetName(enable ? data.SharedName + " x" + data.Config.Stack : data.SharedName, enable);
        component.SetIcon(data.Icon, enable);
        component.SetCurrency(data.CurrencyIcon, enable);
        component.SetPrice(onSale ? data.Config.OnSalePrice.ToString() : data.Config.Price.ToString(), enable);
        component.SetSelected(enable);
        component.m_button.onClick.AddListener(() => data.OnSelect(component, enable, onSale));
    }

    public class StoreItem
    {
        public bool IsValid = true;
        public readonly ItemConfig Config;
        private ItemDrop Prefab = null!;
        public Sprite Icon = null!;
        public Sprite CurrencyIcon = null!;
        private string CurrencySharedName = "";
        public string SharedName = "";

        public void Setup()
        {
            if (!GetItem()) IsValid = false;
            if (!GetCurrency()) IsValid = false;
        }

        public void OnSelect(ItemUI component, bool enable, bool onSale)
        {
            TraderUI.m_instance.DeselectAll();
            TraderUI.m_instance.SetDefaultButtonTextColor();
            component.OnSelected(true);
            if (onSale)
            {
                SelectedSaleItem = this;
                SelectedItem = null;
                TraderUI.m_instance.SetSelectionButtons(Keys.Select, Keys.Buy);
                TraderUI.m_instance.m_cancelButtonText.color = enable ? new Color32(255, 164, 0, 255) : Color.gray;
            }
            else
            {
                SelectedItem = this;
                SelectedSaleItem = null;
                TraderUI.m_instance.SetSelectionButtons(Keys.Buy, Keys.Select);
                TraderUI.m_instance.m_selectButtonText.color = enable ? new Color32(255, 164, 0, 255) : Color.gray;
            }
            TraderUI.m_instance.SetTooltip(GetTooltip());
            TraderUI.m_instance.SetCurrencyIcon(CurrencyIcon);
            TraderUI.m_instance.SetCurrentCurrency(Player.m_localPlayer.GetInventory().CountItems(CurrencySharedName).ToString());
        }

        public bool HasRequirements(bool onSale)
        {
            if (!HasRequiredKey()) return false;
            if (onSale) return Player.m_localPlayer.GetInventory().CountItems(CurrencySharedName) > Config.OnSalePrice;
            return Player.m_localPlayer.GetInventory().CountItems(CurrencySharedName) > Config.Price;
        }

        public bool HasRequiredKey()
        {
            if (Config.RequiredKey.IsNullOrWhiteSpace()) return true;
            return Player.m_localPlayer.HaveUniqueKey(Config.RequiredKey);
        }

        private string GetTooltip()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append($"\n<color=yellow>{SharedName}</color>\n\n");
            var itemData = Prefab.m_itemData.Clone();
            itemData.m_quality = Config.Quality;
            itemData.m_durability = itemData.GetMaxDurability();
            stringBuilder.Append(itemData.GetTooltip(Config.Stack));
            return stringBuilder.ToString();
        }

        public bool Purchase(bool onSale)
        {
            if (!HasRequirements(onSale)) return false;
            if (!Player.m_localPlayer.GetInventory().HaveEmptySlot()) return false;
            var item = Prefab.m_itemData.Clone();
            item.m_quality = Config.Quality;
            item.m_stack = Config.Stack;
            item.m_durability = item.GetMaxDurability();
            if (!Player.m_localPlayer.GetInventory().AddItem(item)) return false;
            Player.m_localPlayer.GetInventory().RemoveItem(CurrencySharedName, onSale ? Config.OnSalePrice : Config.Price);
            if (onSale) OnSaleItems.Remove(this);
            else AvailableItems.Remove(this);
            return true;
        }

        private bool GetItem()
        {
            if (ObjectDB.m_instance.GetItemPrefab(Config.PrefabName) is not { } prefab ||
                !prefab.TryGetComponent(out ItemDrop component))
            {
                TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to validate store item: " + Config.PrefabName);
                return false;
            }
            Prefab = component;
            Icon = component.m_itemData.GetIcon();
            SharedName = component.m_itemData.m_shared.m_name;
            return true;
        }

        private bool GetCurrency()
        {
            if (ObjectDB.m_instance.GetItemPrefab(Config.CurrencyPrefab) is not { } prefab ||
                !prefab.TryGetComponent(out ItemDrop component))
            {
                TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to validate currency: " + Config.PrefabName);
                return false;
            }
            CurrencyIcon = component.m_itemData.GetIcon();
            CurrencySharedName = component.m_itemData.m_shared.m_name;
            if (Config.OnSalePrice <= 0)
            {
                Config.OnSalePrice = Config.Price / 2;
            }
            return true;
        }

        public StoreItem(ItemConfig config) => Config = config;
    }

    [Serializable]
    public class ItemConfig
    {
        public string PrefabName = "";
        public int Stack = 1;
        public int Quality = 1;
        public string CurrencyPrefab = "Coins";
        public int Price;
        public int OnSalePrice;
        public string RequiredKey = "";
        public float Weight = 1f;
        public bool CanBeOnSale = true;
    }

    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static class ObjectDB_Awake_Patch
    {
        private static void Postfix()
        {
            if (!ZNetScene.instance) return;
            SetupStore();
        }
    }
}