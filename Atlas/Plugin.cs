using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;
using Atlas.Config;
using Atlas.Managers;
using Atlas.Network;
using Atlas.Storage;

namespace Atlas
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.atlas.valheim";
        public const string PluginName = "Atlas";
        public const string PluginVersion = "1.0.0";

        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }

        private Harmony _harmony;

        public static ConfigManager ConfigManager { get; private set; }
        public static NetworkManager NetworkManager { get; private set; }
        public static ExplorationManager ExplorationManager { get; private set; }
        public static PinManager PinManager { get; private set; }
        public static StorageManager StorageManager { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Log.LogInfo($"{PluginName} v{PluginVersion} is loading...");

            ConfigManager = new ConfigManager(Config);
            JsonConfig.Load();
            Log.LogInfo($"Config loaded - ExplorationRadiusMultiplier: {JsonConfig.Instance.ExplorationRadiusMultiplier}%");

            StorageManager = new StorageManager();
            NetworkManager = new NetworkManager();
            ExplorationManager = new ExplorationManager();
            PinManager = new PinManager();

            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll();

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded");
        }

        private void Update()
        {
            ExplorationManager?.Update();
            PinManager?.Update();
        }

        private void OnDestroy()
        {
            StorageManager?.SaveAll();
            NetworkManager?.Cleanup();
            ExplorationManager?.Cleanup();
            PinManager?.Cleanup();
            _harmony?.UnpatchSelf();
        }

        public static bool IsServer() => ZNet.instance != null && ZNet.instance.IsServer();
        public static bool IsClient() => ZNet.instance != null && !ZNet.instance.IsServer();
        public static bool IsSinglePlayer() => ZNet.instance != null && !ZNet.instance.IsDedicated() && ZNet.instance.GetNrOfPlayers() <= 1;
        public static bool IsDedicatedServer() => ZNet.instance != null && ZNet.instance.IsDedicated();
    }
}
