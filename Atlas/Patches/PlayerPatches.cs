using HarmonyLib;
using UnityEngine;

namespace Atlas.Patches
{
    /// <summary>
    /// Harmony patches for Player-related functionality
    /// </summary>
    [HarmonyPatch(typeof(Player))]
    public static class PlayerPatches
    {
        /// <summary>
        /// Patch for when player dies - optionally share death pin
        /// </summary>
        [HarmonyPatch(nameof(Player.OnDeath))]
        [HarmonyPostfix]
        public static void OnDeath_Postfix(Player __instance)
        {
            // Only process for local player
            if (__instance != Player.m_localPlayer)
                return;

            // Check if death pins should be shared
            if (!Plugin.ConfigManager.EnablePinSharing.Value)
                return;

            if (!Plugin.ConfigManager.ShareDeathPins.Value)
                return;

            // The death pin is added by the game automatically
            // Our AddPin patch will handle it
            if (Plugin.ConfigManager.DebugMode.Value)
            {
                Plugin.Log.LogDebug($"Player died at {__instance.transform.position}");
            }
        }

        /// <summary>
        /// Patch for when player sets spawn point - optionally share bed pin
        /// </summary>
        [HarmonyPatch(nameof(Player.SetLocalPlayer))]
        [HarmonyPostfix]
        public static void SetLocalPlayer_Postfix(Player __instance)
        {
            // Called when local player is set
            if (__instance == null)
                return;

            if (Plugin.ConfigManager.DebugMode.Value)
            {
                Plugin.Log.LogDebug($"Local player set: {__instance.GetPlayerName()}");
            }
        }
    }

}
