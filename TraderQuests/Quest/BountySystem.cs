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
    private static Dictionary<string, BountyData> AvailableBounties = new();
    public static readonly Dictionary<string, BountyData> ActiveBounties = new();
    private static Dictionary<string, long> CompletedBounties = new();
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
            List<BountyData> bounties = AvailableBounties.Values.Where(bounty => bounty.HasRequiredKey()).ToList();
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
        if (!item.TryGetComponent(out ItemUI component)) return;
        bool enable = data.HasRequirements();
        
        component.SetName(data.Config.Name, enable);
        component.SetIcon(data.Icon, enable);
        component.SetCurrency(data.CurrencyIcon, enable);
        component.SetPrice(data.Config.Price.ToString(), enable);
        component.SetSelected(enable);
        component.m_button.onClick.AddListener(() => data.OnSelected(component, enable, active));
    }
    private static List<BountyConfig> GetDefaultBounties()
    {
        return new List<BountyConfig>()
        {
            // 1. Meadows - Neck Bounty (Easy)
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
                    new() { UniqueID = "NeckBoss", PrefabName = "Neck", OverrideName = "Nekken", Level = 3, IsBoss = true },
                    new() { UniqueID = "NeckMinion.0001", PrefabName = "Neck", OverrideName = "Nekken Minion", Level = 1 },
                    new() { UniqueID = "NeckMinion.0002", PrefabName = "Neck", OverrideName = "Nekken Minion", Level = 1 }
                },
                Rewards = new List<BountyConfig.RewardConfig>()
                {
                    new() { PrefabName = "Coins", Amount = 50 },
                    new() { PrefabName = "TraderCoin_RS", Amount = 1 }
                }
            },

            // 2. Black Forest - Greydwarf Shaman Hunt
            new BountyConfig()
            {
                UniqueID = "ShamanBounty.0001",
                Name = "Forest Corruption",
                CurrencyItem = "Coins",
                Price = 20,
                IconPrefab = "TrophyGreydwarfShaman",
                Biome = "BlackForest",
                RequiredKey = "defeated_gdking",
                Cooldown = 1200f,
                Creatures = new List<BountyConfig.CreatureConfig>()
                {
                    new() { UniqueID = "ShamanBoss", PrefabName = "Greydwarf_Shaman", OverrideName = "Corrupted Shaman", Level = 2, IsBoss = true },
                    new() { UniqueID = "Greydwarf.0001", PrefabName = "Greydwarf", Level = 1 },
                    new() { UniqueID = "Greydwarf.0002", PrefabName = "Greydwarf", Level = 1 }
                },
                Rewards = new List<BountyConfig.RewardConfig>()
                {
                    new() { PrefabName = "Coins", Amount = 75 },
                    new() { PrefabName = "Resin", Amount = 10 },
                    new() { PrefabName = "TraderCoin_RS", Amount = 2 }
                }
            },

            // 3. Swamp - Draugr Invasion
            new BountyConfig()
            {
                UniqueID = "DraugrBounty.0001",
                Name = "The Rotting Horde",
                CurrencyItem = "Coins",
                Price = 40,
                IconPrefab = "TrophyDraugr",
                Biome = "Swamp",
                RequiredKey = "defeated_bonemass",
                Cooldown = 1500f,
                Creatures = new List<BountyConfig.CreatureConfig>()
                {
                    new() { UniqueID = "DraugrBoss", PrefabName = "Draugr_Elite", OverrideName = "Draugr Warlord", Level = 3, IsBoss = true },
                    new() { UniqueID = "DraugrMinion.0001", PrefabName = "Draugr", Level = 2 },
                    new() { UniqueID = "DraugrMinion.0002", PrefabName = "Draugr", Level = 2 },
                    new() { UniqueID = "DraugrArcher.0003", PrefabName = "Draugr_Ranged", Level = 2 }
                },
                Rewards = new List<BountyConfig.RewardConfig>()
                {
                    new() { PrefabName = "Coins", Amount = 150 },
                    new() { PrefabName = "WitheredBone", Amount = 3 },
                    new() { PrefabName = "TraderCoin_RS", Amount = 3 }
                }
            },

            // 4. Mountains - Fenring Stalker
            new BountyConfig()
            {
                UniqueID = "FenringBounty.0001",
                Name = "The Howling Nightmare",
                CurrencyItem = "Coins",
                Price = 75,
                IconPrefab = "TrophyFenring",
                Biome = "Mountain",
                RequiredKey = "defeated_dragon",
                Cooldown = 1800f,
                Creatures = new List<BountyConfig.CreatureConfig>()
                {
                    new() { UniqueID = "FenringAlpha", PrefabName = "Fenring", OverrideName = "Alpha Fenring", Level = 3, IsBoss = true },
                    new() { UniqueID = "FenringMinion.0001", PrefabName = "Fenring", Level = 2 },
                    new() { UniqueID = "FenringMinion.0002", PrefabName = "Fenring", Level = 2 }
                },
                Rewards = new List<BountyConfig.RewardConfig>()
                {
                    new() { PrefabName = "Coins", Amount = 250 },
                    new() { PrefabName = "WolfPelt", Amount = 5 },
                    new() { PrefabName = "TraderCoin_RS", Amount = 3 }
                }
            },

            // 5. Plains - Fulings’ Warband
            new BountyConfig()
            {
                UniqueID = "FulingBounty.0001",
                Name = "Plains Warlords",
                CurrencyItem = "Coins",
                Price = 120,
                IconPrefab = "TrophyGoblinBrute",
                Biome = "Plains",
                RequiredKey = "defeated_yagluth",
                Cooldown = 2000f,
                Creatures = new List<BountyConfig.CreatureConfig>()
                {
                    new() { UniqueID = "FulingBerserkerBoss", PrefabName = "GoblinBrute", OverrideName = "Berserker Warchief", Level = 3, IsBoss = true },
                    new() { UniqueID = "FulingWarrior.0001", PrefabName = "Goblin", Level = 3 },
                    new() { UniqueID = "FulingWarrior.0002", PrefabName = "Goblin", Level = 3 },
                    new() { UniqueID = "FulingShaman.0003", PrefabName = "GoblinShaman", Level = 3 }
                },
                Rewards = new List<BountyConfig.RewardConfig>()
                {
                    new() { PrefabName = "Coins", Amount = 400 },
                    new() { PrefabName = "BlackMetalScrap", Amount = 10 },
                    new() { PrefabName = "TraderCoin_RS", Amount = 4 }
                }
            },
            // 6. Ocean - Serpent Hunt
            new BountyConfig()
            {
                UniqueID = "SerpentBounty.0001",
                Name = "The Deep Terror",
                CurrencyItem = "Coins",
                Price = 200,
                IconPrefab = "TrophySerpent",
                Biome = "Ocean",
                RequiredKey = "defeated_bonemass",
                Cooldown = 2500f,
                Creatures = new List<BountyConfig.CreatureConfig>()
                {
                    new() { UniqueID = "SerpentBoss", PrefabName = "Serpent", OverrideName = "Leviathan Serpent", Level = 3, IsBoss = true }
                },
                Rewards = new List<BountyConfig.RewardConfig>()
                {
                    new() { PrefabName = "Coins", Amount = 500 },
                    new() { PrefabName = "SerpentMeat", Amount = 5 },
                    new() { PrefabName = "TraderCoin_RS", Amount = 2 }
                }
            },

            // 7. Mistlands - Seeker Swarm
            new BountyConfig()
            {
                UniqueID = "SeekerBounty.0001",
                Name = "The Crawling Nightmare",
                CurrencyItem = "Coins",
                Price = 250,
                IconPrefab = "TrophySeeker",
                Biome = "Mistlands",
                RequiredKey = "defeated_queen",
                Cooldown = 3000f,
                Creatures = new List<BountyConfig.CreatureConfig>()
                {
                    new() { UniqueID = "SeekerBoss", PrefabName = "SeekerBrute", OverrideName = "Seeker Prime", Level = 3, IsBoss = true },
                    new() { UniqueID = "SeekerMinion.0001", PrefabName = "Seeker", Level = 2 },
                    new() { UniqueID = "SeekerMinion.0002", PrefabName = "Seeker", Level = 2 }
                },
                Rewards = new List<BountyConfig.RewardConfig>()
                {
                    new() { PrefabName = "Coins", Amount = 600 },
                    new() { PrefabName = "RoyalJelly", Amount = 5 },
                    new() { PrefabName = "TraderCoin_RS", Amount = 5 }
                }
            },

            // 8. Swamp - Surtling Overlord
            new BountyConfig()
            {
                UniqueID = "SurtlingBounty.0001",
                Name = "The Fireborn Tyrant",
                CurrencyItem = "Coins",
                Price = 300,
                IconPrefab = "TrophySurtling",
                Biome = "Swamp",
                RequiredKey = "defeated_bonemass",
                Cooldown = 3500f,
                Creatures = new List<BountyConfig.CreatureConfig>()
                {
                    new() { UniqueID = "SurtlingBoss", PrefabName = "Surtling", OverrideName = "Inferno Lord", Level = 3, IsBoss = true },
                    new() { UniqueID = "SurtlingMinion.0001", PrefabName = "Surtling", Level = 2 },
                    new() { UniqueID = "SurtlingMinion.0002", PrefabName = "Surtling", Level = 2 }
                },
                Rewards = new List<BountyConfig.RewardConfig>()
                {
                    new() { PrefabName = "Coins", Amount = 700 },
                    new() { PrefabName = "Coal", Amount = 20 },
                    new() { PrefabName = "SurtlingCore", Amount = 3 },
                    new() { PrefabName = "TraderCoin_RS", Amount = 3 }
                }
            },

            // 9. Deep North - Ice Golem Onslaught
            new BountyConfig()
            {
                UniqueID = "IceGolemBounty.0001",
                Name = "Frozen Colossus",
                CurrencyItem = "Coins",
                Price = 350,
                IconPrefab = "TrophySGolem",
                Biome = "DeepNorth",
                RequiredKey = "defeated_queen",
                Cooldown = 4000f,
                Creatures = new List<BountyConfig.CreatureConfig>()
                {
                    new() { UniqueID = "IceGolemBoss", PrefabName = "StoneGolem", OverrideName = "Frost Titan", Level = 3, IsBoss = true },
                    new() { UniqueID = "IceGolemMinion.0001", PrefabName = "StoneGolem", Level = 2 }
                },
                Rewards = new List<BountyConfig.RewardConfig>()
                {
                    new() { PrefabName = "Coins", Amount = 800 },
                    new() { PrefabName = "Crystal", Amount = 5 },
                    new() { PrefabName = "TraderCoin_RS", Amount = 4 }
                }
            },

            // 10. Swamp - Abomination Slaughter
            new BountyConfig()
            {
                UniqueID = "AbominationBounty.0001",
                Name = "The Swamp Horror",
                CurrencyItem = "Coins",
                Price = 180,
                IconPrefab = "TrophyAbomination",
                Biome = "Swamp",
                RequiredKey = "defeated_bonemass",
                Cooldown = 2500f,
                Creatures = new List<BountyConfig.CreatureConfig>()
                {
                    new() { UniqueID = "AbominationBoss", PrefabName = "Abomination", OverrideName = "Ancient Horror", Level = 3, IsBoss = true }
                },
                Rewards = new List<BountyConfig.RewardConfig>()
                {
                    new() { PrefabName = "Coins", Amount = 500 },
                    new() { PrefabName = "Root", Amount = 10 },
                    new() { PrefabName = "TraderCoin_RS", Amount = 3 }
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
        private GameObject CurrencyPrefab = null!;
        private string CurrencySharedName = "";
        private Heightmap.Biome Biome = Heightmap.Biome.Meadows;
        public Sprite Icon = null!;
        public Sprite CurrencyIcon = null!;
        public Vector3 Position = Vector3.zero;
        public Minimap.PinData? PinData;
        public readonly Dictionary<string, CreatureData> Creatures = new();
        private readonly List<RewardData> Rewards = new();
        public int KilledCreatureCount;
        private bool Completed;
        public bool Spawned;
        private long CompletedOn;
        public BountyData(BountyConfig config) => Config = config;

        public void OnSelected(ItemUI component, bool enable, bool active)
        {
            TraderUI.m_instance.DeselectAll();
            TraderUI.m_instance.SetDefaultButtonTextColor();
            component.OnSelected(true);
            TraderUI.m_instance.SetSelectionButtons(Keys.Select, Keys.Cancel);
            if (active)
            {
                TraderUI.m_instance.SetSelectionButtons(Keys.Select, Completed ? Keys.Collect : Keys.Cancel);
                SelectedActiveBounty = this;
            }
            else
            {
                if (!QuestSystem.FindSpawnLocation(Biome, out Vector3 position))
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Failed to generate spawn location");
                    return;
                }
                Position = position;
                SelectedBounty = this;
                TreasureSystem.SelectedTreasure = null;
                TraderUI.m_instance.m_selectButtonText.color = enable ? new Color32(255, 164, 0, 255) : Color.gray;
            }
            TraderUI.m_instance.SetTooltip(GetTooltip());
            TraderUI.m_instance.SetCurrencyIcon(CurrencyIcon);
            TraderUI.m_instance.SetCurrentCurrency(Player.m_localPlayer.GetInventory().CountItems(CurrencySharedName).ToString());
        }

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

            CompletedBounties[Config.UniqueID] = CompletedOn;
            ActiveBounties.Remove(Config.UniqueID);
        }

        public void SetCompleted(bool completed, long timeCompleted)
        {
            Completed = completed;
            CompletedOn = timeCompleted;
        }

        public bool IsComplete() => Completed;

        public bool CooldownPassed() => DateTime.Now.Ticks > Config.Cooldown + CompletedOn;

        private string GetTooltip()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append($"\n<color=yellow>{Config.Name}</color>\n\n");
            stringBuilder.Append($"{Keys.Location}: <color=orange>{Biome}</color>\n");
            stringBuilder.Append($"{Keys.Distance}: <color=orange>{Mathf.FloorToInt(Vector3.Distance(Player.m_localPlayer.transform.position, Position))}</color>\n");
            stringBuilder.Append($"<color=orange>{Keys.Rewards}:</color>\n");
            if (AlmanacClassAPI.installed && Config.AlmanacExp > 0)
            {
                stringBuilder.Append($"{Keys.AlmanacExp}: <color=orange>{Config.AlmanacExp}</color>");
            }
            if (EpicMMOSystemAPI.state is EpicMMOSystemAPI.API_State.Ready && Config.EpicMMOExp > 0)
            {
                stringBuilder.Append($"{Keys.EpicMMOExp}: <color=orange>{Config.EpicMMOExp}</color>");
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
            CurrencyPrefab = prefab;
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
            if (!HasRequiredKey()) return false;
            if (ActiveBounties.Count >= TraderQuestsPlugin.MaxActiveBounties.Value) return false;
            return Player.m_localPlayer.GetInventory().CountItems(CurrencySharedName) >= Config.Price;
        }

        public bool HasRequiredKey()
        {
            if (Config.RequiredKey.IsNullOrWhiteSpace()) return true;
            return Player.m_localPlayer.HaveUniqueKey(Config.RequiredKey);
        }

        public bool Deactivate(bool returnCost)
        {
            if (returnCost)
            {
                if (!Player.m_localPlayer.GetInventory().HaveEmptySlot()) return false;
                if (!Player.m_localPlayer.GetInventory().AddItem(CurrencyPrefab, Config.Price)) return false;
            }
            AvailableBounties[Config.UniqueID] = this;
            CurrentBounties.Add(this);
            ActiveBounties.Remove(Config.UniqueID);
            return true;
        }
        public bool Activate(bool checkRequirements = true)
        {
            if (checkRequirements && !HasRequirements())
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Missing requirements");
                return false;
            }

            AvailableBounties.Remove(Config.UniqueID);
            CurrentBounties.Remove(this);
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

        private class RewardData
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