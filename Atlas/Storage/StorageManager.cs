using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Atlas.Models;
using Atlas.Utils;
using UnityEngine;

namespace Atlas.Storage
{
    /// <summary>
    /// Manages persistence of shared map data to disk
    /// </summary>
    public class StorageManager
    {
        private const string FOLDER_NAME = "Atlas";
        private const string EXPLORATION_FILE = "exploration.dat";
        private const string PINS_FILE = "pins.json";
        private const string METADATA_FILE = "metadata.json";
        private const int DATA_VERSION = 1;

        // Auto-save timer
        private float _lastAutoSaveTime;

        // Track if data has changed since last save
        private bool _isDirty;

        public StorageManager()
        {
            _lastAutoSaveTime = Time.time;
        }

        /// <summary>
        /// Get the save path for the current world
        /// </summary>
        private string GetSavePath()
        {
            if (ZNet.instance == null)
                return null;

            string worldName = ZNet.instance.GetWorldName();
            if (string.IsNullOrEmpty(worldName))
                return null;

            // Use the world save path
            string worldSavePath = World.GetWorldSavePath(FileHelpers.FileSource.Local);
            return Path.Combine(worldSavePath, FOLDER_NAME, worldName);
        }

        /// <summary>
        /// Ensure the save directory exists
        /// </summary>
        private bool EnsureSaveDirectory()
        {
            string savePath = GetSavePath();
            if (string.IsNullOrEmpty(savePath))
                return false;

            try
            {
                if (!Directory.Exists(savePath))
                {
                    Directory.CreateDirectory(savePath);
                    Plugin.Log.LogInfo($"Created save directory: {savePath}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to create save directory: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save all map data
        /// </summary>
        public void SaveAll()
        {
            if (!Plugin.ConfigManager.PersistOnServerRestart.Value)
                return;

            if (!Plugin.IsServer())
                return;

            if (!EnsureSaveDirectory())
                return;

            string savePath = GetSavePath();

            try
            {
                // Create backup if enabled
                if (Plugin.ConfigManager.CreateBackups.Value)
                {
                    CreateBackup(savePath);
                }

                // Save exploration data
                SaveExplorationData(savePath);

                // Save pins
                SavePins(savePath);

                // Save metadata
                SaveMetadata(savePath);

                _isDirty = false;
                Plugin.Log.LogInfo("Map data saved successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to save map data: {ex.Message}");
            }
        }

        /// <summary>
        /// Load all map data
        /// </summary>
        public void LoadAll()
        {
            if (!Plugin.ConfigManager.PersistOnServerRestart.Value)
                return;

            if (!Plugin.IsServer())
                return;

            string savePath = GetSavePath();
            if (string.IsNullOrEmpty(savePath) || !Directory.Exists(savePath))
            {
                Plugin.Log.LogInfo("No saved map data found");
                return;
            }

            try
            {
                // Load exploration data
                LoadExplorationData(savePath);

                // Load pins
                LoadPins(savePath);

                Plugin.Log.LogInfo("Map data loaded successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to load map data: {ex.Message}");
            }
        }

        /// <summary>
        /// Save exploration chunks to file
        /// </summary>
        private void SaveExplorationData(string savePath)
        {
            var chunks = Plugin.ExplorationManager.GetAllChunks();
            if (chunks == null || chunks.Count == 0)
                return;

            string filePath = Path.Combine(savePath, EXPLORATION_FILE);

            using (var stream = new FileStream(filePath, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                // Write version
                writer.Write(DATA_VERSION);

                // Write chunk count
                writer.Write(chunks.Count);

                // Serialize each chunk
                foreach (var chunk in chunks)
                {
                    var pkg = new ZPackage();
                    chunk.Serialize(pkg);
                    byte[] data = pkg.GetArray();

                    // Write chunk data length and data
                    writer.Write(data.Length);
                    writer.Write(data);
                }
            }

            LogDebug($"Saved {chunks.Count} exploration chunks");
        }

        /// <summary>
        /// Load exploration chunks from file
        /// </summary>
        private void LoadExplorationData(string savePath)
        {
            string filePath = Path.Combine(savePath, EXPLORATION_FILE);
            if (!File.Exists(filePath))
                return;

            var chunks = new List<ExplorationChunk>();

            using (var stream = new FileStream(filePath, FileMode.Open))
            using (var reader = new BinaryReader(stream))
            {
                // Read version
                int version = reader.ReadInt32();
                if (version > DATA_VERSION)
                {
                    Plugin.Log.LogWarning($"Exploration data version {version} is newer than supported {DATA_VERSION}");
                }

                // Read chunk count
                int count = reader.ReadInt32();

                // Read each chunk
                for (int i = 0; i < count; i++)
                {
                    int dataLength = reader.ReadInt32();
                    byte[] data = reader.ReadBytes(dataLength);

                    var pkg = new ZPackage(data);
                    var chunk = ExplorationChunk.Deserialize(pkg);
                    chunks.Add(chunk);
                }
            }

            Plugin.ExplorationManager.LoadFromStorage(chunks);
            LogDebug($"Loaded {chunks.Count} exploration chunks");
        }

        /// <summary>
        /// Save pins to JSON file
        /// </summary>
        private void SavePins(string savePath)
        {
            var pins = Plugin.PinManager.GetAllPins();
            string filePath = Path.Combine(savePath, PINS_FILE);

            // Simple JSON serialization (avoiding external dependencies)
            var sb = new StringBuilder();
            sb.AppendLine("[");

            for (int i = 0; i < pins.Count; i++)
            {
                var pin = pins[i];
                sb.AppendLine("  {");
                sb.AppendLine($"    \"Id\": \"{EscapeJson(pin.Id)}\",");
                sb.AppendLine($"    \"PositionX\": {pin.Position.x},");
                sb.AppendLine($"    \"PositionY\": {pin.Position.y},");
                sb.AppendLine($"    \"PositionZ\": {pin.Position.z},");
                sb.AppendLine($"    \"Type\": {(int)pin.Type},");
                sb.AppendLine($"    \"Name\": \"{EscapeJson(pin.Name)}\",");
                sb.AppendLine($"    \"Checked\": {pin.Checked.ToString().ToLower()},");
                sb.AppendLine($"    \"CreatedBy\": {pin.CreatedBy},");
                sb.AppendLine($"    \"CreatorName\": \"{EscapeJson(pin.CreatorName)}\",");
                sb.AppendLine($"    \"CreatedAt\": {pin.CreatedAt},");
                sb.AppendLine($"    \"ModifiedAt\": {pin.ModifiedAt}");
                sb.Append("  }");

                if (i < pins.Count - 1)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }

            sb.AppendLine("]");

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            LogDebug($"Saved {pins.Count} pins");
        }

        /// <summary>
        /// Load pins from JSON file
        /// </summary>
        private void LoadPins(string savePath)
        {
            string filePath = Path.Combine(savePath, PINS_FILE);
            if (!File.Exists(filePath))
                return;

            string json = File.ReadAllText(filePath, Encoding.UTF8);
            var pins = ParsePinsJson(json);

            Plugin.PinManager.LoadFromStorage(pins);
            LogDebug($"Loaded {pins.Count} pins");
        }

        /// <summary>
        /// Parse pins from JSON (simple parser to avoid dependencies)
        /// </summary>
        private List<SharedPin> ParsePinsJson(string json)
        {
            var pins = new List<SharedPin>();

            // Simple state-based JSON parser for our specific format
            int index = 0;

            while (index < json.Length)
            {
                // Find next object start
                int objStart = json.IndexOf('{', index);
                if (objStart == -1) break;

                // Find object end
                int objEnd = json.IndexOf('}', objStart);
                if (objEnd == -1) break;

                string objJson = json.Substring(objStart, objEnd - objStart + 1);

                try
                {
                    var pin = ParsePinObject(objJson);
                    if (pin != null)
                        pins.Add(pin);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"Failed to parse pin: {ex.Message}");
                }

                index = objEnd + 1;
            }

            return pins;
        }

        /// <summary>
        /// Parse a single pin object from JSON
        /// </summary>
        private SharedPin ParsePinObject(string json)
        {
            var pin = new SharedPin();

            pin.Id = GetJsonStringValue(json, "Id") ?? Guid.NewGuid().ToString();
            pin.Position = new Vector3(
                GetJsonFloatValue(json, "PositionX"),
                GetJsonFloatValue(json, "PositionY"),
                GetJsonFloatValue(json, "PositionZ")
            );
            pin.Type = (Minimap.PinType)GetJsonIntValue(json, "Type");
            pin.Name = GetJsonStringValue(json, "Name") ?? "";
            pin.Checked = GetJsonBoolValue(json, "Checked");
            pin.CreatedBy = GetJsonLongValue(json, "CreatedBy");
            pin.CreatorName = GetJsonStringValue(json, "CreatorName") ?? "Unknown";
            pin.CreatedAt = GetJsonLongValue(json, "CreatedAt");
            pin.ModifiedAt = GetJsonLongValue(json, "ModifiedAt");

            return pin;
        }

        // Simple JSON parsing helpers
        private string GetJsonStringValue(string json, string key)
        {
            string pattern = $"\"{key}\": \"";
            int start = json.IndexOf(pattern);
            if (start == -1) return null;

            start += pattern.Length;
            int end = json.IndexOf("\"", start);
            if (end == -1) return null;

            return UnescapeJson(json.Substring(start, end - start));
        }

        private float GetJsonFloatValue(string json, string key)
        {
            string pattern = $"\"{key}\": ";
            int start = json.IndexOf(pattern);
            if (start == -1) return 0;

            start += pattern.Length;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-' || json[end] == 'E' || json[end] == 'e' || json[end] == '+'))
                end++;

            if (float.TryParse(json.Substring(start, end - start), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float result))
                return result;
            return 0;
        }

        private int GetJsonIntValue(string json, string key)
        {
            return (int)GetJsonFloatValue(json, key);
        }

        private long GetJsonLongValue(string json, string key)
        {
            return (long)GetJsonFloatValue(json, key);
        }

        private bool GetJsonBoolValue(string json, string key)
        {
            string pattern = $"\"{key}\": ";
            int start = json.IndexOf(pattern);
            if (start == -1) return false;

            start += pattern.Length;
            return json.Substring(start, 4).ToLower() == "true";
        }

        private string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private string UnescapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }

        /// <summary>
        /// Save metadata file
        /// </summary>
        private void SaveMetadata(string savePath)
        {
            string filePath = Path.Combine(savePath, METADATA_FILE);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"Version\": {DATA_VERSION},");
            sb.AppendLine($"  \"LastSaveTime\": {DateTimeOffset.UtcNow.ToUnixTimeSeconds()},");
            sb.AppendLine($"  \"WorldName\": \"{EscapeJson(ZNet.instance?.GetWorldName() ?? "Unknown")}\",");
            sb.AppendLine($"  \"ModVersion\": \"{Plugin.PluginVersion}\"");
            sb.AppendLine("}");

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Create backup of existing files
        /// </summary>
        private void CreateBackup(string savePath)
        {
            try
            {
                string backupPath = savePath + "_backup";

                // Delete old backup
                if (Directory.Exists(backupPath))
                {
                    Directory.Delete(backupPath, true);
                }

                // Copy current to backup
                if (Directory.Exists(savePath))
                {
                    CopyDirectory(savePath, backupPath);
                    LogDebug("Created backup of map data");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to create backup: {ex.Message}");
            }
        }

        /// <summary>
        /// Copy a directory recursively
        /// </summary>
        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        /// <summary>
        /// Mark data as dirty (needs saving)
        /// </summary>
        public void MarkDirty()
        {
            _isDirty = true;
        }

        /// <summary>
        /// Check for auto-save interval
        /// </summary>
        public void CheckAutoSave()
        {
            if (!Plugin.IsServer()) return;
            if (!Plugin.ConfigManager.PersistOnServerRestart.Value) return;
            if (!_isDirty) return;

            float interval = Plugin.ConfigManager.AutoSaveIntervalSeconds.Value;
            if (Time.time - _lastAutoSaveTime >= interval)
            {
                SaveAll();
                _lastAutoSaveTime = Time.time;
            }
        }

        private void LogDebug(string message)
        {
            if (Plugin.ConfigManager?.DebugMode?.Value == true)
            {
                Plugin.Log.LogDebug($"[Storage] {message}");
            }
        }
    }
}
