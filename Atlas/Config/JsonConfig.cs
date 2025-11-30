using System;
using System.IO;
using UnityEngine;

namespace Atlas.Config
{
    /// <summary>
    /// JSON configuration for server-enforced settings
    /// </summary>
    [Serializable]
    public class JsonConfig
    {
        public float ExplorationRadiusMultiplier = 100f;

        private static string ConfigPath => Path.Combine(BepInEx.Paths.ConfigPath, "Atlas.json");

        private static JsonConfig _instance;
        public static JsonConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    Load();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Load config from JSON file, create default if not exists
        /// </summary>
        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    _instance = JsonUtility.FromJson<JsonConfig>(json);
                    Plugin.Log.LogInfo($"Loaded JSON config from {ConfigPath}");

                    // Validate and clamp values
                    _instance.Validate();
                }
                else
                {
                    _instance = new JsonConfig();
                    Save();
                    Plugin.Log.LogInfo($"Created default JSON config at {ConfigPath}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to load JSON config: {ex.Message}");
                _instance = new JsonConfig();
            }
        }

        /// <summary>
        /// Save current config to JSON file
        /// </summary>
        public static void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(_instance, true);
                File.WriteAllText(ConfigPath, json);
                Plugin.Log.LogInfo($"Saved JSON config to {ConfigPath}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to save JSON config: {ex.Message}");
            }
        }

        /// <summary>
        /// Validate and clamp all values to acceptable ranges
        /// </summary>
        private void Validate()
        {
            // Clamp ExplorationRadiusMultiplier: 0% to 10000%
            if (ExplorationRadiusMultiplier < 0f)
            {
                Plugin.Log.LogWarning($"ExplorationRadiusMultiplier {ExplorationRadiusMultiplier} is below 0%, clamping to 0%");
                ExplorationRadiusMultiplier = 0f;
            }
            else if (ExplorationRadiusMultiplier > 10000f)
            {
                Plugin.Log.LogWarning($"ExplorationRadiusMultiplier {ExplorationRadiusMultiplier} exceeds 10000%, clamping to 10000%");
                ExplorationRadiusMultiplier = 10000f;
            }
        }

        /// <summary>
        /// Reload config from file (useful for runtime changes)
        /// </summary>
        public static void Reload()
        {
            _instance = null;
            Load();
        }
    }
}
