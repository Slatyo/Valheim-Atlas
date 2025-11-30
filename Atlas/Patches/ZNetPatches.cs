using HarmonyLib;

namespace Atlas.Patches
{
    /// <summary>
    /// Harmony patches for ZNet (networking) related functionality
    /// </summary>
    [HarmonyPatch(typeof(ZNet))]
    public static class ZNetPatches
    {
        /// <summary>
        /// Patch for when world is saved - trigger map data save
        /// </summary>
        [HarmonyPatch(nameof(ZNet.SaveWorld))]
        [HarmonyPostfix]
        public static void SaveWorld_Postfix(ZNet __instance)
        {
            // Only save on server
            if (!Plugin.IsServer())
                return;

            Plugin.Log.LogInfo("World save triggered, saving shared map data...");
            Plugin.StorageManager?.SaveAll();
        }

        /// <summary>
        /// Patch for when the server starts - load saved map data
        /// </summary>
        [HarmonyPatch(nameof(ZNet.Start))]
        [HarmonyPostfix]
        public static void Start_Postfix(ZNet __instance)
        {
            // Only load on server after it's started
            if (!__instance.IsServer())
                return;

            Plugin.Log.LogInfo("Server started, loading shared map data...");
            Plugin.StorageManager?.LoadAll();
        }

        /// <summary>
        /// Patch for when the server shuts down - save map data
        /// </summary>
        [HarmonyPatch(nameof(ZNet.Shutdown))]
        [HarmonyPrefix]
        public static void Shutdown_Prefix(ZNet __instance)
        {
            // Only save on server before shutdown
            if (__instance == null || !__instance.IsServer())
                return;

            Plugin.Log.LogInfo("Server shutting down, saving shared map data...");
            Plugin.StorageManager?.SaveAll();
        }
    }

    /// <summary>
    /// Patches for ZRoutedRpc to handle player connections
    /// </summary>
    [HarmonyPatch(typeof(ZRoutedRpc))]
    public static class ZRoutedRpcPatches
    {
        /// <summary>
        /// Patch for when RPC system is ready
        /// </summary>
        [HarmonyPatch(nameof(ZRoutedRpc.SetUID))]
        [HarmonyPostfix]
        public static void SetUID_Postfix(ZRoutedRpc __instance, long uid)
        {
            // Called when our UID is set (we're connected)
            if (Plugin.ConfigManager?.DebugMode?.Value == true)
            {
                Plugin.Log.LogDebug($"ZRoutedRpc UID set: {uid}");
            }
        }
    }

    /// <summary>
    /// Patches for Game class
    /// </summary>
    [HarmonyPatch(typeof(Game))]
    public static class GamePatches
    {
        /// <summary>
        /// Patch for when the game starts loading
        /// </summary>
        [HarmonyPatch(nameof(Game.Start))]
        [HarmonyPostfix]
        public static void Start_Postfix(Game __instance)
        {
            Plugin.Log.LogInfo("Game started");
        }

        /// <summary>
        /// Patch for when player spawns - good time to request sync
        /// </summary>
        [HarmonyPatch(nameof(Game.SpawnPlayer))]
        [HarmonyPostfix]
        public static void SpawnPlayer_Postfix(Game __instance)
        {
            // After player spawns, we should request a full sync
            if (Plugin.IsClient() && Player.m_localPlayer != null)
            {
                // Small delay to ensure everything is set up
                __instance.StartCoroutine(RequestSyncDelayed());
            }
        }

        private static System.Collections.IEnumerator RequestSyncDelayed()
        {
            // Wait a short time for everything to initialize
            yield return new UnityEngine.WaitForSeconds(2f);

            if (Plugin.IsClient() && !Plugin.NetworkManager.IsReceivingSync)
            {
                Plugin.Log.LogInfo("Player spawned, requesting map sync...");
                Plugin.NetworkManager?.RequestFullSync();
            }
        }
    }
}
