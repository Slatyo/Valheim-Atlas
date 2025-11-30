using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Atlas.Patches
{
    /// <summary>
    /// Harmony patches for the Minimap class
    /// </summary>
    [HarmonyPatch(typeof(Minimap))]
    public static class MinimapPatches
    {
        // References to our labels
        private static GameObject _smallMapLabel;
        private static GameObject _largeMapLabel;

        /// <summary>
        /// Patch for Minimap.Explore - applies radius multiplier and captures exploration events
        /// </summary>
        [HarmonyPatch(nameof(Minimap.Explore), typeof(Vector3), typeof(float))]
        [HarmonyPrefix]
        public static void Explore_Prefix(ref float radius)
        {
            // Apply exploration radius multiplier from JSON config (server-enforced)
            float multiplier = Config.JsonConfig.Instance?.ExplorationRadiusMultiplier ?? 100f;

            // Clamp to valid range (0% to 10000%) - already validated in JsonConfig but double-check
            multiplier = Mathf.Clamp(multiplier, 0f, 10000f);

            // Convert percentage to multiplier and apply
            radius *= multiplier / 100f;
        }

        /// <summary>
        /// Patch for Minimap.Explore - captures exploration events after radius is applied
        /// </summary>
        [HarmonyPatch(nameof(Minimap.Explore), typeof(Vector3), typeof(float))]
        [HarmonyPostfix]
        public static void Explore_Postfix(Vector3 p, float radius)
        {
            // Skip if exploration manager is applying remote data
            if (Plugin.ExplorationManager?.IsApplyingRemote == true)
                return;

            // Skip if exploration sharing is disabled
            if (Plugin.ConfigManager?.EnableExplorationSharing?.Value != true)
                return;

            // Notify exploration manager of local exploration (radius already has multiplier applied)
            Plugin.ExplorationManager?.OnLocalExploration(p, radius);
        }

        /// <summary>
        /// Patch to force "Visible to other players" always ON
        /// </summary>
        [HarmonyPatch(nameof(Minimap.Update))]
        [HarmonyPostfix]
        public static void Update_Postfix(Minimap __instance)
        {
            // Force public position to always be enabled (if config enabled)
            if (Plugin.ConfigManager?.ForcePublicPosition?.Value == true)
            {
                // m_publicPosition is a Toggle in newer Valheim versions
                if (__instance.m_publicPosition != null && !__instance.m_publicPosition.isOn)
                {
                    __instance.m_publicPosition.isOn = true;
                }
            }

            // Update label visibility based on config
            UpdateLabelVisibility();
        }

        private static void UpdateLabelVisibility()
        {
            bool show = Plugin.ConfigManager?.ShowMapLabel?.Value ?? true;

            if (_smallMapLabel != null)
                _smallMapLabel.SetActive(show);

            if (_largeMapLabel != null)
                _largeMapLabel.SetActive(show);
        }

        /// <summary>
        /// Patch to add "Atlas" label when minimap is set up
        /// </summary>
        [HarmonyPatch(nameof(Minimap.Start))]
        [HarmonyPostfix]
        public static void Start_Postfix(Minimap __instance)
        {
            CreateLabels(__instance);
        }

        /// <summary>
        /// Create the Atlas labels on both small and large maps
        /// </summary>
        private static void CreateLabels(Minimap minimap)
        {
            try
            {
                // Create label on small minimap
                if (minimap.m_smallRoot != null && _smallMapLabel == null)
                {
                    _smallMapLabel = CreateLabel(minimap.m_smallRoot.transform, "AtlasLabelSmall", 12, new Vector2(5, -5));
                }

                // Create label on large map
                if (minimap.m_largeRoot != null && _largeMapLabel == null)
                {
                    _largeMapLabel = CreateLabel(minimap.m_largeRoot.transform, "AtlasLabelLarge", 16, new Vector2(10, -10));
                }

                Plugin.Log.LogInfo("Atlas labels added to minimap");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to create Atlas labels: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a single label on a map root
        /// </summary>
        private static GameObject CreateLabel(Transform parent, string name, int fontSize, Vector2 position)
        {
            // Create a new GameObject for our label
            GameObject labelObj = new GameObject(name);
            labelObj.transform.SetParent(parent, false);

            // Add RectTransform for positioning
            RectTransform rectTransform = labelObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1); // Top-left
            rectTransform.anchorMax = new Vector2(0, 1); // Top-left
            rectTransform.pivot = new Vector2(0, 1);     // Top-left pivot
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = new Vector2(120, 25);

            // Add Text component
            Text text = labelObj.AddComponent<Text>();
            text.text = "Atlas";
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.UpperLeft;
            text.color = new Color(1f, 0.85f, 0.4f, 0.9f); // Golden/amber color
            text.raycastTarget = false; // Don't block mouse clicks

            // Try to use the game's default font
            Font gameFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (gameFont != null)
            {
                text.font = gameFont;
            }

            // Add outline for better visibility against the map
            Outline outline = labelObj.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.6f);
            outline.effectDistance = new Vector2(1, -1);

            return labelObj;
        }

        /// <summary>
        /// Patch for Minimap.AddPin - captures pin creation events
        /// </summary>
        [HarmonyPatch(nameof(Minimap.AddPin))]
        [HarmonyPostfix]
        public static void AddPin_Postfix(
            Minimap __instance,
            Minimap.PinData __result,
            Vector3 pos,
            Minimap.PinType type,
            string name,
            bool save,
            bool isChecked)
        {
            // Skip if pin manager is applying remote data
            if (Plugin.PinManager?.IsApplyingRemote == true)
                return;

            // Skip if pin sharing is disabled
            if (Plugin.ConfigManager?.EnablePinSharing?.Value != true)
                return;

            // Only track saved pins
            if (!save)
                return;

            // Skip if result is null
            if (__result == null)
                return;

            // Notify pin manager of local pin addition
            Plugin.PinManager?.OnLocalPinAdded(__result);
        }

        /// <summary>
        /// Patch for Minimap.RemovePin - captures pin removal events
        /// </summary>
        [HarmonyPatch(nameof(Minimap.RemovePin), typeof(Minimap.PinData))]
        [HarmonyPrefix]
        public static void RemovePin_Prefix(Minimap.PinData pin)
        {
            // Skip if pin manager is applying remote data
            if (Plugin.PinManager?.IsApplyingRemote == true)
                return;

            // Skip if pin sharing is disabled
            if (Plugin.ConfigManager?.EnablePinSharing?.Value != true)
                return;

            // Skip if pin is null
            if (pin == null)
                return;

            // Notify pin manager of local pin removal
            Plugin.PinManager?.OnLocalPinRemoved(pin);
        }

        /// <summary>
        /// Patch for when Minimap is set up - good time to request sync
        /// </summary>
        [HarmonyPatch(nameof(Minimap.SetMapData))]
        [HarmonyPostfix]
        public static void SetMapData_Postfix(Minimap __instance)
        {
            // This is called when the map is initialized with saved data
            // Good opportunity to request full sync from server
            if (Plugin.IsClient())
            {
                Plugin.Log.LogInfo("Map data loaded, requesting full sync from server...");
                Plugin.NetworkManager?.RequestFullSync();
            }
        }
    }
}
