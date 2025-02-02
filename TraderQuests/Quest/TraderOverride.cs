using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using YamlDotNet.Serialization;

namespace TraderQuests.Quest;

public static class TraderOverride
{
    private static readonly string TraderFolderPath = TraderQuestsPlugin.FolderPath + Path.DirectorySeparatorChar + "Trader";
    private static readonly Dictionary<string, List<TraderItem>> OverrideData = new();

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        private static void Postfix(ZNetScene __instance)
        {
            if (!Directory.Exists(TraderQuestsPlugin.FolderPath)) Directory.CreateDirectory(TraderQuestsPlugin.FolderPath);
            if (!Directory.Exists(TraderFolderPath)) Directory.CreateDirectory(TraderFolderPath);
            var serializer = new SerializerBuilder().Build();
            
            foreach (GameObject prefab in __instance.m_prefabs.Where(p => p.GetComponent<Trader>()))
            {
                var name = prefab.name.Replace("(Clone)", string.Empty);
                string filePath = Path.Combine(TraderFolderPath, name + ".yml");
                if (!prefab.TryGetComponent(out Trader component)) continue;
                var items = component.m_items
                    .Select(item => new TraderItem()
                    {
                        PrefabName =  item.m_prefab.name,
                        Stack = item.m_stack,
                        Price = item.m_price,
                        RequiredKey = item.m_requiredGlobalKey
                    })
                    .ToList();
                if (!OverrideData.ContainsKey(name))
                {
                    OverrideData[name] = items;
                }
                if (File.Exists(filePath)) continue;
                File.WriteAllText(filePath, serializer.Serialize(items));
            }
        }
    }

    public static void LoadFiles()
    {
        if (!Directory.Exists(TraderQuestsPlugin.FolderPath)) Directory.CreateDirectory(TraderQuestsPlugin.FolderPath);
        if (!Directory.Exists(TraderFolderPath)) Directory.CreateDirectory(TraderFolderPath);
        var files = Directory.GetFiles(TraderFolderPath, "*.yml");
        if (files.Length <= 0) return;
        var deserializer = new DeserializerBuilder().Build();
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var prefabName = fileName.Replace(".yml", string.Empty);
            try
            {
                OverrideData[prefabName] = deserializer.Deserialize<List<TraderItem>>(File.ReadAllText(file));
            }
            catch
            {
                TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Failed to deserialize file: " + fileName);
            }
        }
    }
    
    [HarmonyPatch(typeof(Trader), nameof(Trader.GetAvailableItems))]
    private static class Trader_GetAvailableItems_Patch
    {
        private static void Postfix(Trader __instance, ref List<Trader.TradeItem> __result)
        {
            if (TraderQuestsPlugin.OverrideStore.Value is TraderQuestsPlugin.Toggle.Off) return;
            if (!OverrideData.TryGetValue(__instance.name.Replace("(Clone)", string.Empty), out List<TraderItem> data)) return;
            List<Trader.TradeItem> availableItems = new List<Trader.TradeItem>();
            foreach (var tradeItem in data)
            {
                if (!string.IsNullOrWhiteSpace(tradeItem.RequiredKey) && !ZoneSystem.instance.GetGlobalKey(tradeItem.RequiredKey)) continue;
                if (ObjectDB.m_instance.GetItemPrefab(tradeItem.PrefabName) is not { } prefab || !prefab.TryGetComponent(out ItemDrop component)) continue;
                availableItems.Add(new Trader.TradeItem()
                {
                    m_prefab = component,
                    m_stack = tradeItem.Stack,
                    m_price = tradeItem.Price,
                    m_requiredGlobalKey = tradeItem.RequiredKey
                });
            }

            __result = availableItems;
        }
    }

    [Serializable]
    public class TraderItem
    {
        public string PrefabName = "";
        public int Stack;
        public int Price;
        public string RequiredKey = "";
    }
}