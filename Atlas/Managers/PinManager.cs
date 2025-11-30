using System;
using System.Collections.Generic;
using System.Linq;
using Atlas.Models;
using UnityEngine;

namespace Atlas.Managers
{
    /// <summary>
    /// Manages map pin sharing between players
    /// </summary>
    public class PinManager
    {
        // All shared pins by ID
        private Dictionary<string, SharedPin> _sharedPins = new Dictionary<string, SharedPin>();

        // Mapping from shared pin ID to local PinData (for tracking)
        private Dictionary<string, Minimap.PinData> _pinDataMapping = new Dictionary<string, Minimap.PinData>();

        // Pins waiting to be synced
        private List<SharedPin> _pendingAdditions = new List<SharedPin>();
        private List<string> _pendingRemovals = new List<string>();
        private List<SharedPin> _pendingUpdates = new List<SharedPin>();

        // Flag to prevent feedback loops when applying remote data
        public bool IsApplyingRemote { get; private set; }

        // Timer for batched syncing
        private float _lastSyncTime;

        // Count pins per player
        private Dictionary<long, int> _pinsPerPlayer = new Dictionary<long, int>();

        public PinManager()
        {
            _lastSyncTime = Time.time;
        }

        /// <summary>
        /// Called every frame to process batched updates
        /// </summary>
        public void Update()
        {
            if (!Plugin.ConfigManager.EnablePinSharing.Value)
                return;

            // Sync interval (use exploration sync interval for pins too)
            float syncInterval = Plugin.ConfigManager.SyncIntervalMs.Value / 1000f;
            if (Time.time - _lastSyncTime >= syncInterval)
            {
                ProcessPendingPins();
                _lastSyncTime = Time.time;
            }
        }

        /// <summary>
        /// Called when the local player adds a pin
        /// </summary>
        public void OnLocalPinAdded(Minimap.PinData pinData)
        {
            if (!Plugin.ConfigManager.EnablePinSharing.Value)
                return;

            if (IsApplyingRemote)
                return;

            if (pinData == null)
                return;

            // Check if this pin type should be shared
            if (!Plugin.ConfigManager.ShouldSharePinType(pinData.m_type))
            {
                LogDebug($"Pin type {pinData.m_type} not configured for sharing");
                return;
            }

            // Create shared pin
            long playerId = Player.m_localPlayer?.GetPlayerID() ?? 0;
            string playerName = Player.m_localPlayer?.GetPlayerName() ?? "Unknown";

            var sharedPin = new SharedPin(pinData, playerId, playerName);

            // Check for duplicates
            var duplicate = FindDuplicatePin(sharedPin);
            if (duplicate != null)
            {
                LogDebug($"Pin is duplicate of {duplicate.Id}, skipping");
                return;
            }

            // Queue for sync
            _pendingAdditions.Add(sharedPin);

            LogDebug($"Queued pin for addition: {sharedPin}");
        }

        /// <summary>
        /// Called when the local player removes a pin
        /// </summary>
        public void OnLocalPinRemoved(Minimap.PinData pinData)
        {
            if (!Plugin.ConfigManager.EnablePinSharing.Value)
                return;

            if (IsApplyingRemote)
                return;

            if (pinData == null)
                return;

            // Find the shared pin that matches this pin data
            var sharedPinId = FindSharedPinIdByPinData(pinData);
            if (sharedPinId != null)
            {
                _pendingRemovals.Add(sharedPinId);
                LogDebug($"Queued pin for removal: {sharedPinId}");
            }
        }

        /// <summary>
        /// Called when the local player updates a pin (e.g., checks it)
        /// </summary>
        public void OnLocalPinUpdated(Minimap.PinData pinData)
        {
            if (!Plugin.ConfigManager.EnablePinSharing.Value)
                return;

            if (IsApplyingRemote)
                return;

            if (pinData == null)
                return;

            var sharedPinId = FindSharedPinIdByPinData(pinData);
            if (sharedPinId != null && _sharedPins.TryGetValue(sharedPinId, out var sharedPin))
            {
                sharedPin.Checked = pinData.m_checked;
                sharedPin.ModifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                _pendingUpdates.Add(sharedPin);
                LogDebug($"Queued pin for update: {sharedPin}");
            }
        }

        /// <summary>
        /// Process pending pin operations
        /// </summary>
        private void ProcessPendingPins()
        {
            // Process additions
            foreach (var pin in _pendingAdditions)
            {
                Plugin.NetworkManager.SendPinAdded(pin);
            }
            _pendingAdditions.Clear();

            // Process removals
            foreach (var pinId in _pendingRemovals)
            {
                Plugin.NetworkManager.SendPinRemoved(pinId);
            }
            _pendingRemovals.Clear();

            // Process updates
            foreach (var pin in _pendingUpdates)
            {
                Plugin.NetworkManager.SendPinUpdated(pin);
            }
            _pendingUpdates.Clear();
        }

        /// <summary>
        /// Add a shared pin (from network or local)
        /// </summary>
        public void AddSharedPin(SharedPin pin, bool applyToMinimap)
        {
            if (pin == null) return;

            // Check total pin limit
            if (_sharedPins.Count >= Plugin.ConfigManager.MaxTotalPins.Value)
            {
                Plugin.Log.LogWarning($"Total pin limit reached ({Plugin.ConfigManager.MaxTotalPins.Value})");
                return;
            }

            // Check for duplicates
            var duplicate = FindDuplicatePin(pin);
            if (duplicate != null)
            {
                // Keep the older one
                if (pin.CreatedAt > duplicate.CreatedAt)
                {
                    LogDebug($"Ignoring newer duplicate pin {pin.Id}");
                    return;
                }
                else
                {
                    // Remove the newer duplicate
                    RemoveSharedPin(duplicate.Id, applyToMinimap);
                }
            }

            _sharedPins[pin.Id] = pin;

            // Update per-player count
            if (!_pinsPerPlayer.ContainsKey(pin.CreatedBy))
                _pinsPerPlayer[pin.CreatedBy] = 0;
            _pinsPerPlayer[pin.CreatedBy]++;

            if (applyToMinimap)
            {
                ApplyPinToMinimap(pin);
            }

            LogDebug($"Added shared pin: {pin}");
        }

        /// <summary>
        /// Remove a shared pin
        /// </summary>
        public void RemoveSharedPin(string pinId, bool removeFromMinimap)
        {
            if (string.IsNullOrEmpty(pinId)) return;

            if (_sharedPins.TryGetValue(pinId, out var pin))
            {
                // Update per-player count
                if (_pinsPerPlayer.ContainsKey(pin.CreatedBy))
                    _pinsPerPlayer[pin.CreatedBy]--;

                _sharedPins.Remove(pinId);

                if (removeFromMinimap && _pinDataMapping.TryGetValue(pinId, out var pinData))
                {
                    RemovePinFromMinimap(pinData);
                    _pinDataMapping.Remove(pinId);
                }

                LogDebug($"Removed shared pin: {pinId}");
            }
        }

        /// <summary>
        /// Update a shared pin
        /// </summary>
        public void UpdateSharedPin(SharedPin pin, bool updateMinimap)
        {
            if (pin == null) return;

            if (_sharedPins.ContainsKey(pin.Id))
            {
                _sharedPins[pin.Id] = pin;

                if (updateMinimap && _pinDataMapping.TryGetValue(pin.Id, out var pinData))
                {
                    pinData.m_checked = pin.Checked;
                    pinData.m_name = pin.Name;
                }

                LogDebug($"Updated shared pin: {pin}");
            }
        }

        /// <summary>
        /// Apply a list of pins from full sync
        /// </summary>
        public void ApplyPins(List<SharedPin> pins)
        {
            if (pins == null) return;

            IsApplyingRemote = true;

            try
            {
                // Clear existing pins first
                ClearAllPins();

                foreach (var pin in pins)
                {
                    AddSharedPin(pin, true);
                }

                Plugin.Log.LogInfo($"Applied {pins.Count} shared pins");
            }
            finally
            {
                IsApplyingRemote = false;
            }
        }

        /// <summary>
        /// Apply a single pin to the game's minimap
        /// </summary>
        private void ApplyPinToMinimap(SharedPin pin)
        {
            if (Minimap.instance == null) return;

            IsApplyingRemote = true;

            try
            {
                // Create pin data manually to ensure all fields are set correctly
                Minimap.PinData pinData = new Minimap.PinData();
                pinData.m_type = pin.Type;
                pinData.m_name = pin.Name ?? "";
                pinData.m_pos = pin.Position;
                pinData.m_icon = Minimap.instance.GetSprite(pin.Type);
                pinData.m_save = true;
                pinData.m_checked = pin.Checked;
                pinData.m_ownerID = 0L; // 0 makes it appear as "your own"

                // Add to the minimap's pin list
                Minimap.instance.m_pins.Add(pinData);

                // Store mapping
                _pinDataMapping[pin.Id] = pinData;

                LogDebug($"Applied pin to minimap: {pin.Name} at {pin.Position}");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Failed to apply pin to minimap: {ex.Message}");
            }
            finally
            {
                IsApplyingRemote = false;
            }
        }

        /// <summary>
        /// Remove a pin from the minimap
        /// </summary>
        private void RemovePinFromMinimap(Minimap.PinData pinData)
        {
            if (Minimap.instance == null || pinData == null) return;

            IsApplyingRemote = true;

            try
            {
                Minimap.instance.RemovePin(pinData);
            }
            finally
            {
                IsApplyingRemote = false;
            }
        }

        /// <summary>
        /// Clear all shared pins from minimap
        /// </summary>
        private void ClearAllPins()
        {
            IsApplyingRemote = true;

            try
            {
                foreach (var pinData in _pinDataMapping.Values)
                {
                    if (Minimap.instance != null && pinData != null)
                    {
                        Minimap.instance.RemovePin(pinData);
                    }
                }

                _pinDataMapping.Clear();
                _sharedPins.Clear();
                _pinsPerPlayer.Clear();
            }
            finally
            {
                IsApplyingRemote = false;
            }
        }

        /// <summary>
        /// Get all shared pins
        /// </summary>
        public List<SharedPin> GetAllPins()
        {
            return _sharedPins.Values.ToList();
        }

        /// <summary>
        /// Check if a player can add more pins
        /// </summary>
        public bool CanAddPin(long playerId)
        {
            if (!_pinsPerPlayer.TryGetValue(playerId, out var count))
                count = 0;

            return count < Plugin.ConfigManager.MaxPinsPerPlayer.Value;
        }

        /// <summary>
        /// Find a duplicate pin
        /// </summary>
        private SharedPin FindDuplicatePin(SharedPin pin)
        {
            float threshold = Plugin.ConfigManager.PinDeduplicationDistance.Value;

            foreach (var existing in _sharedPins.Values)
            {
                if (existing.IsDuplicateOf(pin, threshold))
                {
                    return existing;
                }
            }

            return null;
        }

        /// <summary>
        /// Find the shared pin ID that matches a local PinData
        /// </summary>
        private string FindSharedPinIdByPinData(Minimap.PinData pinData)
        {
            // First check direct mapping
            foreach (var kvp in _pinDataMapping)
            {
                if (kvp.Value == pinData)
                    return kvp.Key;
            }

            // If not found in mapping, try to match by properties
            foreach (var pin in _sharedPins.Values)
            {
                if (pin.MatchesPinData(pinData))
                    return pin.Id;
            }

            return null;
        }

        /// <summary>
        /// Load pins from storage
        /// </summary>
        public void LoadFromStorage(List<SharedPin> pins)
        {
            if (pins == null) return;

            _sharedPins.Clear();
            _pinsPerPlayer.Clear();

            foreach (var pin in pins)
            {
                _sharedPins[pin.Id] = pin;

                if (!_pinsPerPlayer.ContainsKey(pin.CreatedBy))
                    _pinsPerPlayer[pin.CreatedBy] = 0;
                _pinsPerPlayer[pin.CreatedBy]++;
            }

            LogDebug($"Loaded {pins.Count} pins from storage");
        }

        public void Cleanup()
        {
            ClearAllPins();
            _pendingAdditions.Clear();
            _pendingRemovals.Clear();
            _pendingUpdates.Clear();
        }

        private void LogDebug(string message)
        {
            if (Plugin.ConfigManager?.DebugMode?.Value == true)
            {
                Plugin.Log.LogDebug($"[Pin] {message}");
            }
        }
    }
}
