using BepInEx.Configuration;

// Minimap is from assembly_valheim (global namespace)

namespace Atlas.Config
{
    /// <summary>
    /// Manages all configuration settings for the Atlas mod
    /// </summary>
    public class ConfigManager
    {
        private readonly ConfigFile _config;

        // General Settings
        public ConfigEntry<bool> EnableExplorationSharing { get; private set; }
        public ConfigEntry<bool> EnablePinSharing { get; private set; }

        // Exploration Settings
        public ConfigEntry<int> SyncIntervalMs { get; private set; }
        public ConfigEntry<int> ExplorationChunkSize { get; private set; }
        public ConfigEntry<float> MinExplorationRadius { get; private set; }
        // Note: ExplorationRadiusMultiplier is in Atlas.json (JsonConfig)

        // Pin Settings
        public ConfigEntry<int> MaxPinsPerPlayer { get; private set; }
        public ConfigEntry<int> MaxTotalPins { get; private set; }
        public ConfigEntry<bool> ShareDeathPins { get; private set; }
        public ConfigEntry<bool> ShareBossPins { get; private set; }
        public ConfigEntry<bool> SharePortalPins { get; private set; }
        public ConfigEntry<bool> ShareBedPins { get; private set; }
        public ConfigEntry<float> PinDeduplicationDistance { get; private set; }

        // Storage Settings
        public ConfigEntry<bool> PersistOnServerRestart { get; private set; }
        public ConfigEntry<int> AutoSaveIntervalSeconds { get; private set; }
        public ConfigEntry<bool> CreateBackups { get; private set; }

        // UI Settings
        public ConfigEntry<bool> ForcePublicPosition { get; private set; }
        public ConfigEntry<bool> ShowMapLabel { get; private set; }

        // Debug Settings
        public ConfigEntry<bool> DebugMode { get; private set; }

        public ConfigManager(ConfigFile config)
        {
            _config = config;

            // Ensure configs are saved properly
            _config.SaveOnConfigSet = false;

            BindGeneralSettings();
            BindExplorationSettings();
            BindPinSettings();
            BindStorageSettings();
            BindUISettings();
            BindDebugSettings();

            _config.Save();
            _config.SaveOnConfigSet = true;

            Plugin.Log.LogInfo("Configuration bound successfully");
        }

        private void BindGeneralSettings()
        {
            EnableExplorationSharing = _config.Bind(
                "1. General",
                "EnableExplorationSharing",
                true,
                new ConfigDescription(
                    "Enable sharing of fog-of-war exploration between all players",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }
                )
            );

            EnablePinSharing = _config.Bind(
                "1. General",
                "EnablePinSharing",
                true,
                new ConfigDescription(
                    "Enable sharing of map pins/markers between all players",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }
                )
            );
        }

        private void BindExplorationSettings()
        {
            SyncIntervalMs = _config.Bind(
                "2. Exploration",
                "SyncIntervalMs",
                5000,
                new ConfigDescription(
                    "How often to batch and sync exploration updates (milliseconds)",
                    new AcceptableValueRange<int>(1000, 30000),
                    new ConfigurationManagerAttributes { IsAdminOnly = true }
                )
            );

            ExplorationChunkSize = _config.Bind(
                "2. Exploration",
                "ExplorationChunkSize",
                64,
                new ConfigDescription(
                    "Size of exploration chunks in pixels (higher = more efficient, lower = more granular)",
                    new AcceptableValueRange<int>(16, 128),
                    new ConfigurationManagerAttributes { IsAdminOnly = true, Advanced = true }
                )
            );

            MinExplorationRadius = _config.Bind(
                "2. Exploration",
                "MinExplorationRadius",
                10f,
                new ConfigDescription(
                    "Minimum exploration radius to trigger a sync (prevents spam from tiny movements)",
                    new AcceptableValueRange<float>(1f, 100f),
                    new ConfigurationManagerAttributes { IsAdminOnly = true, Advanced = true }
                )
            );
            // Note: ExplorationRadiusMultiplier is configured in BepInEx/config/Atlas.json
        }

        private void BindPinSettings()
        {
            MaxPinsPerPlayer = _config.Bind(
                "3. Pins",
                "MaxPinsPerPlayer",
                100,
                new ConfigDescription(
                    "Maximum number of pins each player can create",
                    new AcceptableValueRange<int>(10, 500),
                    new ConfigurationManagerAttributes { IsAdminOnly = true }
                )
            );

            MaxTotalPins = _config.Bind(
                "3. Pins",
                "MaxTotalPins",
                1000,
                new ConfigDescription(
                    "Maximum total pins across all players",
                    new AcceptableValueRange<int>(100, 5000),
                    new ConfigurationManagerAttributes { IsAdminOnly = true }
                )
            );

            ShareDeathPins = _config.Bind(
                "3. Pins",
                "ShareDeathPins",
                true,
                new ConfigDescription(
                    "Include death markers in sharing",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }
                )
            );

            ShareBossPins = _config.Bind(
                "3. Pins",
                "ShareBossPins",
                true,
                new ConfigDescription(
                    "Include boss markers in sharing",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }
                )
            );

            SharePortalPins = _config.Bind(
                "3. Pins",
                "SharePortalPins",
                true,
                new ConfigDescription(
                    "Include portal markers in sharing",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }
                )
            );

            ShareBedPins = _config.Bind(
                "3. Pins",
                "ShareBedPins",
                true,
                new ConfigDescription(
                    "Include bed/spawn markers in sharing",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }
                )
            );

            PinDeduplicationDistance = _config.Bind(
                "3. Pins",
                "PinDeduplicationDistance",
                25f,
                new ConfigDescription(
                    "Distance threshold for pin deduplication (pins of same type/name within this distance are considered duplicates)",
                    new AcceptableValueRange<float>(5f, 100f),
                    new ConfigurationManagerAttributes { IsAdminOnly = true, Advanced = true }
                )
            );
        }

        private void BindStorageSettings()
        {
            PersistOnServerRestart = _config.Bind(
                "4. Storage",
                "PersistOnServerRestart",
                true,
                new ConfigDescription(
                    "Save map data to file so it persists across server restarts",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }
                )
            );

            AutoSaveIntervalSeconds = _config.Bind(
                "4. Storage",
                "AutoSaveIntervalSeconds",
                300,
                new ConfigDescription(
                    "How often to auto-save map data (seconds)",
                    new AcceptableValueRange<int>(60, 1800),
                    new ConfigurationManagerAttributes { IsAdminOnly = true }
                )
            );

            CreateBackups = _config.Bind(
                "4. Storage",
                "CreateBackups",
                true,
                new ConfigDescription(
                    "Create backup files before saving",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }
                )
            );
        }

        private void BindUISettings()
        {
            ForcePublicPosition = _config.Bind(
                "5. UI",
                "ForcePublicPosition",
                true,
                new ConfigDescription(
                    "Force 'Visible to other players' to always be ON for all players",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }
                )
            );

            ShowMapLabel = _config.Bind(
                "5. UI",
                "ShowMapLabel",
                true,
                new ConfigDescription(
                    "Show 'Atlas' label on the minimap",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = false }
                )
            );
        }

        private void BindDebugSettings()
        {
            DebugMode = _config.Bind(
                "6. Debug",
                "DebugMode",
                false,
                new ConfigDescription(
                    "Enable debug logging (verbose)",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }
                )
            );
        }

        /// <summary>
        /// Check if a pin type should be shared based on configuration
        /// </summary>
        public bool ShouldSharePinType(Minimap.PinType pinType)
        {
            if (!EnablePinSharing.Value) return false;

            switch (pinType)
            {
                case Minimap.PinType.Death:
                    return ShareDeathPins.Value;
                case Minimap.PinType.Boss:
                    return ShareBossPins.Value;
                case Minimap.PinType.Icon3: // Portal
                    return SharePortalPins.Value;
                case Minimap.PinType.Bed:
                    return ShareBedPins.Value;
                default:
                    return true; // Share all other types
            }
        }
    }

    /// <summary>
    /// Attribute for BepInEx ConfigurationManager integration
    /// </summary>
    public class ConfigurationManagerAttributes
    {
        public bool? IsAdminOnly;
        public bool? Advanced;
        public int? Order;
    }
}
