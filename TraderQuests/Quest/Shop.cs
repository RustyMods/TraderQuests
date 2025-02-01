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
            for (var index = 0; index < Configs.Count; index++)
            {
                var config = Configs[index];
                var data = serializer.Serialize(config);
                var filePath = ShopFolderPath + Path.DirectorySeparatorChar + $"{index:D4}" + ".yml";
                File.WriteAllText(filePath, data);
            }
        }
        else
        {
            var deserializer = new DeserializerBuilder().Build();
            foreach (var file in files)
            {
                try
                {
                    Configs.Add(deserializer.Deserialize<ItemConfig>(File.ReadAllText(file)));
                    LastLoadedTime = 0.0;
                    SetupStore();
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
            }
            catch
            {
                TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to deserialize server store");
            }
        };
    }

    public static void UpdateServerStore()
    {
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
            new ItemConfig()
            {
                PrefabName = "SwordBronze",
                Stack = 1,
                Quality = 2,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 5,
                RequiredKey = "defeated_eikthyr"
            },
            new ItemConfig()
            {
                PrefabName = "MaceBronze",
                Stack = 1,
                Quality = 2,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 5,
                RequiredKey = "defeated_eikthyr"
            },
            new ItemConfig()
            {
                PrefabName = "AtgeirBronze",
                Stack = 1,
                Quality = 2,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 5,
                RequiredKey = "defeated_eikthyr"
            },
            new ItemConfig()
            {
                PrefabName = "AtgeirBronze",
                Stack = 1,
                Quality = 2,
                CurrencyPrefab = "TraderCoin_RS",
                Price = 5,
                RequiredKey = "defeated_eikthyr"
            },
        };
    }

    public static void LoadAvailable()
    {
        if (!TraderUI.m_instance || TraderUI.m_item is null || !ZNet.m_instance) return;
        if (LastLoadedTime == 0.0 || ZNet.m_instance.GetTimeSeconds() - LastLoadedTime > TraderQuestsPlugin.ShopCooldown.Value * 60)
        {
            List<StoreItem> storeItems = new(AllItems);
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
        bool enable = data.HasRequirements(onSale);
        Text name = Utils.FindChild(item.transform, "$text_name").GetComponent<Text>();
        name.text = Localization.instance.Localize(data.SharedName);
        name.color = enable ? new Color32(255, 255, 255, 255) : new Color32(150, 150, 150, 255);
        Image icon = Utils.FindChild(item.transform, "$image_icon").GetComponent<Image>();
        icon.sprite = data.Icon;
        icon.color = enable ? Color.white : Color.gray;
        Image currency = Utils.FindChild(item.transform, "$image_currency").GetComponent<Image>();
        currency.sprite = data.CurrencyIcon;
        currency.color = enable ? Color.white : Color.gray;
        Text currencyText = Utils.FindChild(item.transform, "$text_currency").GetComponent<Text>();
        currencyText.text = onSale ? data.Config.OnSalePrice.ToString() : data.Config.Price.ToString();
        currencyText.color = enable ? new Color32(255, 164, 0, 255) : new Color32(150, 150, 150, 255);
        Transform selected = item.transform.Find("$image_selected");
        selected.GetComponent<Image>().color = enable ? new Color32(255, 164, 0, 255) : new Color32(255, 164, 0, 200);
        
        item.GetComponent<Button>().onClick.AddListener(() =>
        {
            TraderUI.m_instance.DeselectAll();
            selected.gameObject.SetActive(true);
            if (onSale)
            {
                SelectedSaleItem = data;
                SelectedItem = null;
                TraderUI.m_instance.SetSelectionButtons(Keys.Select, Keys.Buy);
            }
            else
            {
                SelectedItem = data;
                SelectedSaleItem = null;
                TraderUI.m_instance.SetSelectionButtons(Keys.Buy, Keys.Select);
            }
            
            TraderUI.m_instance.SetTooltip(data.GetTooltip());
        });
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

        public bool HasRequirements(bool onSale)
        {
            if (!Config.RequiredKey.IsNullOrWhiteSpace() && !Player.m_localPlayer.HaveUniqueKey(Config.RequiredKey)) return false;
            if (onSale) return Player.m_localPlayer.GetInventory().CountItems(CurrencySharedName) > Config.OnSalePrice;
            return Player.m_localPlayer.GetInventory().CountItems(CurrencySharedName) > Config.Price;
        }

        public string GetTooltip()
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
            return true;
        }

        private bool GetItem()
        {
            if (ObjectDB.m_instance.GetItemPrefab(Config.PrefabName) is not { } prefab || !prefab.TryGetComponent(out ItemDrop component)) return false;
            Prefab = component;
            Icon = component.m_itemData.GetIcon();
            SharedName = component.m_itemData.m_shared.m_name;
            return true;
        }

        private bool GetCurrency()
        {
            if (ObjectDB.m_instance.GetItemPrefab(Config.CurrencyPrefab) is not { } prefab ||
                !prefab.TryGetComponent(out ItemDrop component)) return false;
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