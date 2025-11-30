using System;
using System.Collections.Generic;
using Atlas.Models;
using Atlas.Utils;
using UnityEngine;

namespace Atlas.Managers
{
    /// <summary>
    /// Manages fog-of-war exploration sharing between players
    /// </summary>
    public class ExplorationManager
    {
        // Dictionary of chunks by position (chunkX, chunkY)
        private Dictionary<(int, int), ExplorationChunk> _chunks = new Dictionary<(int, int), ExplorationChunk>();

        // Chunks that have been modified and need to be synced
        private HashSet<(int, int)> _dirtyChunks = new HashSet<(int, int)>();

        // Timer for batched syncing
        private float _lastSyncTime;

        // Flag to prevent feedback loops when applying remote data
        public bool IsApplyingRemote { get; private set; }

        // Track if we've done initial sync
        private bool _initialSyncComplete;

        public ExplorationManager()
        {
            _lastSyncTime = Time.time;
        }

        /// <summary>
        /// Called every frame to process batched updates
        /// </summary>
        public void Update()
        {
            if (!Plugin.ConfigManager.EnableExplorationSharing.Value)
                return;

            // Check if enough time has passed for a sync
            float syncInterval = Plugin.ConfigManager.SyncIntervalMs.Value / 1000f;
            if (Time.time - _lastSyncTime >= syncInterval && _dirtyChunks.Count > 0)
            {
                SendDirtyChunks();
                _lastSyncTime = Time.time;
            }
        }

        /// <summary>
        /// Called when the local player explores an area
        /// </summary>
        public void OnLocalExploration(Vector3 worldPos, float radius)
        {
            if (!Plugin.ConfigManager.EnableExplorationSharing.Value)
                return;

            if (IsApplyingRemote)
                return;

            // Skip very small explorations
            if (radius < Plugin.ConfigManager.MinExplorationRadius.Value)
                return;

            // Convert world position to pixel coordinates
            var (centerX, centerY) = CoordinateHelper.WorldToPixel(worldPos);
            int pixelRadius = CoordinateHelper.WorldRadiusToPixels(radius);

            // Mark all affected chunks as dirty
            int chunkSize = ExplorationChunk.ChunkSize;

            int minX = Math.Max(0, centerX - pixelRadius);
            int maxX = Math.Min(CoordinateHelper.MapTextureSize - 1, centerX + pixelRadius);
            int minY = Math.Max(0, centerY - pixelRadius);
            int maxY = Math.Min(CoordinateHelper.MapTextureSize - 1, centerY + pixelRadius);

            // Get affected chunk range
            int minChunkX = minX / chunkSize;
            int maxChunkX = maxX / chunkSize;
            int minChunkY = minY / chunkSize;
            int maxChunkY = maxY / chunkSize;

            // Mark pixels in each affected chunk
            for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
            {
                for (int chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
                {
                    var chunkKey = (chunkX, chunkY);

                    if (!_chunks.TryGetValue(chunkKey, out var chunk))
                    {
                        chunk = new ExplorationChunk(chunkX, chunkY);
                        _chunks[chunkKey] = chunk;
                    }

                    // Mark pixels within this chunk that are in the exploration radius
                    int chunkStartX = chunkX * chunkSize;
                    int chunkStartY = chunkY * chunkSize;

                    bool chunkModified = false;

                    for (int localX = 0; localX < chunkSize; localX++)
                    {
                        for (int localY = 0; localY < chunkSize; localY++)
                        {
                            int pixelX = chunkStartX + localX;
                            int pixelY = chunkStartY + localY;

                            // Check if pixel is within exploration radius
                            float distance = CoordinateHelper.PixelDistance(centerX, centerY, pixelX, pixelY);
                            if (distance <= pixelRadius)
                            {
                                if (!chunk.GetPixel(localX, localY))
                                {
                                    chunk.SetPixel(localX, localY, true);
                                    chunkModified = true;
                                }
                            }
                        }
                    }

                    if (chunkModified)
                    {
                        _dirtyChunks.Add(chunkKey);
                    }
                }
            }

            LogDebug($"Local exploration at {worldPos} radius {radius} affected {_dirtyChunks.Count} chunks");
        }

        /// <summary>
        /// Send all dirty chunks to the server
        /// </summary>
        private void SendDirtyChunks()
        {
            if (_dirtyChunks.Count == 0) return;

            var chunksToSend = new List<ExplorationChunk>();

            foreach (var key in _dirtyChunks)
            {
                if (_chunks.TryGetValue(key, out var chunk))
                {
                    chunksToSend.Add(chunk);
                }
            }

            _dirtyChunks.Clear();

            if (chunksToSend.Count > 0)
            {
                Plugin.NetworkManager.SendExplorationUpdate(chunksToSend);
                LogDebug($"Sent {chunksToSend.Count} dirty chunks");
            }
        }

        /// <summary>
        /// Merge incoming chunks from another player (server-side)
        /// </summary>
        public void MergeChunks(List<ExplorationChunk> incomingChunks)
        {
            if (incomingChunks == null) return;

            foreach (var incoming in incomingChunks)
            {
                var key = (incoming.ChunkX, incoming.ChunkY);

                if (_chunks.TryGetValue(key, out var existing))
                {
                    existing.Merge(incoming);
                }
                else
                {
                    _chunks[key] = incoming;
                }
            }

            LogDebug($"Merged {incomingChunks.Count} chunks");
        }

        /// <summary>
        /// Apply chunks from the server (client-side)
        /// </summary>
        public void ApplyChunks(List<ExplorationChunk> chunks)
        {
            if (chunks == null || chunks.Count == 0) return;

            IsApplyingRemote = true;

            try
            {
                foreach (var chunk in chunks)
                {
                    var key = (chunk.ChunkX, chunk.ChunkY);

                    if (_chunks.TryGetValue(key, out var existing))
                    {
                        existing.Merge(chunk);
                    }
                    else
                    {
                        _chunks[key] = chunk;
                    }

                    // Apply to the actual minimap
                    ApplyChunkToMinimap(chunk);
                }

                LogDebug($"Applied {chunks.Count} chunks to minimap");
            }
            finally
            {
                IsApplyingRemote = false;
            }
        }

        /// <summary>
        /// Apply a single chunk's exploration data to the game's minimap
        /// </summary>
        private void ApplyChunkToMinimap(ExplorationChunk chunk)
        {
            if (Minimap.instance == null) return;

            int chunkSize = ExplorationChunk.ChunkSize;
            int chunkStartX = chunk.ChunkX * chunkSize;
            int chunkStartY = chunk.ChunkY * chunkSize;
            int textureSize = Minimap.instance.m_textureSize;

            // Directly modify the explored array for efficiency
            bool[] explored = Minimap.instance.m_explored;
            if (explored == null) return;

            bool modified = false;

            for (int localX = 0; localX < chunkSize; localX++)
            {
                for (int localY = 0; localY < chunkSize; localY++)
                {
                    if (chunk.GetPixel(localX, localY))
                    {
                        int pixelX = chunkStartX + localX;
                        int pixelY = chunkStartY + localY;

                        // Bounds check
                        if (pixelX >= 0 && pixelX < textureSize && pixelY >= 0 && pixelY < textureSize)
                        {
                            int index = pixelY * textureSize + pixelX;
                            if (index >= 0 && index < explored.Length && !explored[index])
                            {
                                explored[index] = true;
                                modified = true;
                            }
                        }
                    }
                }
            }

            // Force the minimap to update its texture if we modified anything
            if (modified)
            {
                Minimap.instance.m_fogTexture.Apply();
            }
        }

        /// <summary>
        /// Get all chunks that have exploration data
        /// </summary>
        public List<ExplorationChunk> GetAllChunks()
        {
            // First, capture current minimap state if we're the server
            if (Plugin.IsServer())
            {
                CaptureCurrentMinimapState();
            }

            var result = new List<ExplorationChunk>();

            foreach (var chunk in _chunks.Values)
            {
                if (chunk.HasExploredPixels())
                {
                    result.Add(chunk);
                }
            }

            return result;
        }

        /// <summary>
        /// Capture the current minimap exploration state into chunks
        /// </summary>
        private void CaptureCurrentMinimapState()
        {
            if (Minimap.instance == null) return;

            // Access the explored array
            bool[] explored = Minimap.instance.m_explored;
            if (explored == null) return;

            int textureSize = Minimap.instance.m_textureSize;
            int chunkSize = ExplorationChunk.ChunkSize;
            int chunksPerSide = textureSize / chunkSize;

            for (int chunkX = 0; chunkX < chunksPerSide; chunkX++)
            {
                for (int chunkY = 0; chunkY < chunksPerSide; chunkY++)
                {
                    var key = (chunkX, chunkY);

                    if (!_chunks.TryGetValue(key, out var chunk))
                    {
                        chunk = new ExplorationChunk(chunkX, chunkY);
                    }

                    bool hasExplored = false;
                    int chunkStartX = chunkX * chunkSize;
                    int chunkStartY = chunkY * chunkSize;

                    for (int localX = 0; localX < chunkSize; localX++)
                    {
                        for (int localY = 0; localY < chunkSize; localY++)
                        {
                            int pixelX = chunkStartX + localX;
                            int pixelY = chunkStartY + localY;
                            int index = pixelY * textureSize + pixelX;

                            if (index < explored.Length && explored[index])
                            {
                                chunk.SetPixel(localX, localY, true);
                                hasExplored = true;
                            }
                        }
                    }

                    if (hasExplored)
                    {
                        _chunks[key] = chunk;
                    }
                }
            }
        }

        /// <summary>
        /// Load exploration data from storage
        /// </summary>
        public void LoadFromStorage(List<ExplorationChunk> chunks)
        {
            if (chunks == null) return;

            _chunks.Clear();

            foreach (var chunk in chunks)
            {
                var key = (chunk.ChunkX, chunk.ChunkY);
                _chunks[key] = chunk;
            }

            LogDebug($"Loaded {chunks.Count} chunks from storage");
        }

        /// <summary>
        /// Mark initial sync as complete
        /// </summary>
        public void SetInitialSyncComplete()
        {
            _initialSyncComplete = true;
        }

        /// <summary>
        /// Check if initial sync is complete
        /// </summary>
        public bool IsInitialSyncComplete => _initialSyncComplete;

        public void Cleanup()
        {
            _chunks.Clear();
            _dirtyChunks.Clear();
            _initialSyncComplete = false;
        }

        private void LogDebug(string message)
        {
            if (Plugin.ConfigManager?.DebugMode?.Value == true)
            {
                Plugin.Log.LogDebug($"[Exploration] {message}");
            }
        }
    }
}
