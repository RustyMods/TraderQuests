using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ItemManager;
using JetBrains.Annotations;
using LocalizationManager;
using ServerSync;
using TraderQuests.Behaviors;
using TraderQuests.Quest;
using TraderQuests.translations;
using UnityEngine;

namespace TraderQuests
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class TraderQuestsPlugin : BaseUnityPlugin
    {
        internal const string ModName = "TraderQuests";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private static readonly string ConfigFileName = ModGUID + ".cfg";
        private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource TraderQuestsLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        public static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        public enum Toggle { On = 1, Off = 0 }

        public static readonly AssetBundle Assets = GetAssetBundle("traderquestbundle");
        public static TraderQuestsPlugin Plugin = null!;
        public static GameObject Root = null!;
        public static readonly string FolderPath = Paths.ConfigPath + Path.DirectorySeparatorChar + "TraderQuests";
        
        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<Vector2> PanelPosition = null!;
        public static ConfigEntry<TraderUI.FontOptions> Font = null!;
        public static ConfigEntry<double> BountyCooldown = null!;
        public static ConfigEntry<int> MaxBountyDisplayed = null!;
        public static ConfigEntry<int> MaxActiveBounties = null!;
        public static ConfigEntry<double> TreasureCooldown = null!;
        public static ConfigEntry<int> MaxTreasureDisplayed = null!;
        public static ConfigEntry<int> MaxActiveTreasures = null!;
        public static ConfigEntry<double> ShopCooldown = null!;
        public static ConfigEntry<int> MaxShopItems = null!;
        public static ConfigEntry<int> MaxSaleItems = null!;
        public static ConfigEntry<Traders> AffectedTraders = null!;
        public static ConfigEntry<string> CustomTrader = null!;
        public BountySystem.BountyData? QueuedBountyData;

        public enum Traders
        {
            None, Haldor, Hildir, Custom, All
        }


        private void InitConfigs()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            PanelPosition = config("2 - Settings", "Position", new Vector2(-100f, 50f), new ConfigDescription("Set position of panel", null, new ConfigurationManagerAttributes()
            {
                Order = 0
            }));
            PanelPosition.SettingChanged += (_, _) =>
            {
                if (!TraderUI.m_instance) return;
                TraderUI.m_instance.UpdatePanelPosition();
            };
            Font = config("2 - Settings", "Font", TraderUI.FontOptions.AveriaSerifLibre, new ConfigDescription("Font options", null, new ConfigurationManagerAttributes()
            {
                Order = 1,
            }));
            Font.SettingChanged += (_, _) =>
            {
                if (!TraderUI.m_instance) return;
                TraderUI.m_instance.SetFont();
            };
            AffectedTraders = config("2 - Settings", "Trader", Traders.All, new ConfigDescription("Traders that have access to panel", null, new ConfigurationManagerAttributes()
            {
                Order = 2
            }));
            CustomTrader = config("2 - Settings", "Custom Trader", "TravelingHaldor", "Add prefab name of custom trader to have access to panel");
            
            BountyCooldown = config("Bounty", "Cooldown", 60.0, "Set duration between new bounties, in minutes");
            MaxBountyDisplayed = config("Bounty", "Max Available", 10, "Set max amount of available bounties displayed");
            MaxActiveBounties = config("Bounty", "Max Active", 10, "Set max amount of active bounties");

            TreasureCooldown = config("Treasure", "Cooldown", 60.0, "Set duration between new treasures, in minutes");
            MaxTreasureDisplayed = config("Treasure", "Max Available", 10, "Set max amount of available treasures displayed");
            MaxActiveTreasures = config("Treasure", "Max Active", 10, "Set max amount of active treasures");
            
            ShopCooldown = config("Shop", "Cooldown", 60.0, "Set duration between new shop items, in minutes");
            MaxShopItems = config("Shop", "Max Available", 10, "Set max amount of available items to purchase");
            MaxSaleItems = config("Shop", "Max On Sale", 5, "Set max amount of items can be on sale");
        }

        public void Awake()
        {
            Localizer.Load(); 
            Plugin = this;
            Root = new GameObject("root");
            DontDestroyOnLoad(Root);
            Root.SetActive(false);
            InitConfigs();
            TraderUI.LoadAssets();

            if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);
            TreasureSystem.Init();
            BountySystem.Init();
            Shop.Init();

            Item TraderToken = new Item(Assets, "TraderCoin_RS");
            TraderToken.Name.English("Trader Token");
            TraderToken.Description.English("Special token for completing quests");
            TraderToken.Configurable = Configurability.Disabled;
            
            Item TraderRing = new Item(Assets, "TraderRing_RS");
            TraderRing.Name.English("Trader Ring");
            TraderRing.Description.English("Helps find trader treasures");
            TraderRing.Configurable = Configurability.Disabled;
            var component = TraderRing.Prefab.GetComponent<ItemDrop>();
            var status = ScriptableObject.CreateInstance<TreasureFinder>();
            status.name = "SE_TreasureFinder";
            status.m_name = Keys.TraderRing;
            status.m_icon = component.m_itemData.GetIcon();
            component.m_itemData.m_shared.m_equipStatusEffect = status;

            BountySystem.SetupSync();
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        public void DelayedSpawn()
        {
            if (QueuedBountyData is null) return;
            foreach (BountySystem.BountyData.CreatureData creature in QueuedBountyData.Creatures.Values)
            {
                GameObject spawn = Instantiate(creature.Prefab, QueuedBountyData.Position, Quaternion.identity);
                Bounty component = spawn.AddComponent<Bounty>();
                component.SetData(QueuedBountyData, creature);
            }
            Effects.DoneSpawnEffects.Create(QueuedBountyData.Position, Quaternion.identity);
        }
        
        private static AssetBundle GetAssetBundle(string fileName)
        {
            Assembly execAssembly = Assembly.GetExecutingAssembly();
            string resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(fileName));
            using Stream? stream = execAssembly.GetManifestResourceStream(resourceName);
            return AssetBundle.LoadFromStream(stream);
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                TraderQuestsLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                TraderQuestsLogger.LogError($"There was an issue loading your {ConfigFileName}");
                TraderQuestsLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }
    }

    public static class KeyboardExtensions
    {
        public static bool IsKeyDown(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey) &&
                   shortcut.Modifiers.All(Input.GetKey);
        }

        public static bool IsKeyHeld(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKey(shortcut.MainKey) &&
                   shortcut.Modifiers.All(Input.GetKey);
        }
    }
}