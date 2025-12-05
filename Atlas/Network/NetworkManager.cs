using System;
using System.Collections;
using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using Atlas.Models;
using Atlas.Utils;

namespace Atlas.Network
{
    /// <summary>
    /// Manages all network communication for map sharing
    /// </summary>
    public class NetworkManager
    {
        // RPC names
        private const string RPC_REQUEST_FULL_SYNC = "Atlas_RequestFullSync";
        private const string RPC_FULL_SYNC_RESPONSE = "Atlas_FullSyncResponse";
        private const string RPC_EXPLORATION_UPDATE = "Atlas_ExplorationUpdate";
        private const string RPC_BROADCAST_EXPLORATION = "Atlas_BroadcastExploration";
        private const string RPC_PIN_ADDED = "Atlas_PinAdded";
        private const string RPC_PIN_REMOVED = "Atlas_PinRemoved";
        private const string RPC_PIN_UPDATED = "Atlas_PinUpdated";
        private const string RPC_BROADCAST_PIN = "Atlas_BroadcastPin";

        // Jotunn Custom RPCs
        private CustomRPC _requestFullSyncRPC;
        private CustomRPC _fullSyncResponseRPC;
        private CustomRPC _explorationUpdateRPC;
        private CustomRPC _broadcastExplorationRPC;
        private CustomRPC _pinAddedRPC;
        private CustomRPC _pinRemovedRPC;
        private CustomRPC _pinUpdatedRPC;
        private CustomRPC _broadcastPinRPC;

        // Track pending sync requests
        private HashSet<long> _pendingSyncRequests = new HashSet<long>();

        // Flag to indicate we're currently receiving sync data
        public bool IsReceivingSync { get; private set; }

        public NetworkManager()
        {
            RegisterRPCs();
        }

        private void RegisterRPCs()
        {
            try
            {
                // Full sync RPCs
                _requestFullSyncRPC = Jotunn.Managers.NetworkManager.Instance.AddRPC(
                    RPC_REQUEST_FULL_SYNC,
                    OnRequestFullSyncServer,
                    OnRequestFullSyncClient
                );

                _fullSyncResponseRPC = Jotunn.Managers.NetworkManager.Instance.AddRPC(
                    RPC_FULL_SYNC_RESPONSE,
                    OnFullSyncResponseServer,
                    OnFullSyncResponseClient
                );

                // Exploration RPCs
                _explorationUpdateRPC = Jotunn.Managers.NetworkManager.Instance.AddRPC(
                    RPC_EXPLORATION_UPDATE,
                    OnExplorationUpdateServer,
                    OnExplorationUpdateClient
                );

                _broadcastExplorationRPC = Jotunn.Managers.NetworkManager.Instance.AddRPC(
                    RPC_BROADCAST_EXPLORATION,
                    OnBroadcastExplorationServer,
                    OnBroadcastExplorationClient
                );

                // Pin RPCs
                _pinAddedRPC = Jotunn.Managers.NetworkManager.Instance.AddRPC(
                    RPC_PIN_ADDED,
                    OnPinAddedServer,
                    OnPinAddedClient
                );

                _pinRemovedRPC = Jotunn.Managers.NetworkManager.Instance.AddRPC(
                    RPC_PIN_REMOVED,
                    OnPinRemovedServer,
                    OnPinRemovedClient
                );

                _pinUpdatedRPC = Jotunn.Managers.NetworkManager.Instance.AddRPC(
                    RPC_PIN_UPDATED,
                    OnPinUpdatedServer,
                    OnPinUpdatedClient
                );

                _broadcastPinRPC = Jotunn.Managers.NetworkManager.Instance.AddRPC(
                    RPC_BROADCAST_PIN,
                    OnBroadcastPinServer,
                    OnBroadcastPinClient
                );

                Plugin.Log.LogInfo("Network RPCs registered successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to register network RPCs: {ex.Message}");
            }
        }

        public void Cleanup()
        {
            _pendingSyncRequests.Clear();
        }

        #region Public Send Methods

        /// <summary>
        /// Request a full map sync from the server (called by client on join)
        /// </summary>
        public void RequestFullSync()
        {
            if (Plugin.IsServer())
            {
                // We are the server, no need to request
                LogDebug("Server doesn't need to request sync");
                return;
            }

            LogDebug("Requesting full map sync from server");
            var pkg = new ZPackage();
            pkg.Write(GetLocalPlayerId());
            _requestFullSyncRPC.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
        }

        /// <summary>
        /// Send exploration update to server
        /// </summary>
        public void SendExplorationUpdate(List<ExplorationChunk> chunks)
        {
            if (chunks == null || chunks.Count == 0) return;

            var pkg = new ZPackage();
            pkg.Write(GetLocalPlayerId());
            pkg.Write(chunks.Count);

            foreach (var chunk in chunks)
            {
                chunk.Serialize(pkg);
            }

            // Compress the package data
            byte[] compressed = CompressionHelper.Compress(pkg.GetArray());
            var compressedPkg = new ZPackage();
            compressedPkg.Write(compressed.Length);
            compressedPkg.Write(compressed);

            if (Plugin.IsServer())
            {
                // Server: merge into storage and broadcast to clients
                Plugin.ExplorationManager.MergeChunks(chunks);
                BroadcastExplorationToClients(chunks, GetLocalPlayerId());
            }
            else
            {
                // Send to server
                _explorationUpdateRPC.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), compressedPkg);
            }

            LogDebug($"Sent exploration update with {chunks.Count} chunks");
        }

        /// <summary>
        /// Send a new pin to the server
        /// </summary>
        public void SendPinAdded(SharedPin pin)
        {
            if (pin == null) return;

            var pkg = new ZPackage();
            pkg.Write(GetLocalPlayerId());
            pin.Serialize(pkg);

            if (Plugin.IsServer())
            {
                // Add locally (with minimap apply) and broadcast to clients
                Plugin.PinManager.AddSharedPin(pin, true);
                BroadcastPinToClients(pin, PinAction.Added, GetLocalPlayerId());
            }
            else
            {
                _pinAddedRPC.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
            }

            LogDebug($"Sent pin added: {pin}");
        }

        /// <summary>
        /// Send a pin removal to the server
        /// </summary>
        public void SendPinRemoved(string pinId)
        {
            if (string.IsNullOrEmpty(pinId)) return;

            var pkg = new ZPackage();
            pkg.Write(GetLocalPlayerId());
            pkg.Write(pinId);

            if (Plugin.IsServer())
            {
                // Remove locally (with minimap apply) and broadcast to clients
                Plugin.PinManager.RemoveSharedPin(pinId, true);
                BroadcastPinRemovalToClients(pinId, GetLocalPlayerId());
            }
            else
            {
                _pinRemovedRPC.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
            }

            LogDebug($"Sent pin removed: {pinId}");
        }

        /// <summary>
        /// Send a pin update to the server
        /// </summary>
        public void SendPinUpdated(SharedPin pin)
        {
            if (pin == null) return;

            var pkg = new ZPackage();
            pkg.Write(GetLocalPlayerId());
            pin.Serialize(pkg);

            if (Plugin.IsServer())
            {
                // Update locally (with minimap apply) and broadcast to clients
                Plugin.PinManager.UpdateSharedPin(pin, true);
                BroadcastPinToClients(pin, PinAction.Updated, GetLocalPlayerId());
            }
            else
            {
                _pinUpdatedRPC.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
            }

            LogDebug($"Sent pin updated: {pin}");
        }

        #endregion

        #region Server-side RPC Handlers

        private IEnumerator OnRequestFullSyncServer(long sender, ZPackage pkg)
        {
            if (!Plugin.IsServer())
            {
                yield break;
            }

            long playerId = pkg.ReadLong();
            LogDebug($"Received full sync request from player {playerId}");

            // Prevent duplicate requests
            if (_pendingSyncRequests.Contains(playerId))
            {
                LogDebug($"Ignoring duplicate sync request from {playerId}");
                yield break;
            }

            _pendingSyncRequests.Add(playerId);

            try
            {
                // Compile all map data
                var syncData = new MapSyncData
                {
                    WorldName = ZNet.instance?.GetWorldName() ?? "Unknown",
                    ExplorationChunks = Plugin.ExplorationManager.GetAllChunks(),
                    Pins = Plugin.PinManager.GetAllPins()
                };

                // Serialize and compress
                var responsePkg = new ZPackage();
                syncData.Serialize(responsePkg);

                byte[] compressed = CompressionHelper.Compress(responsePkg.GetArray());
                var compressedPkg = new ZPackage();
                compressedPkg.Write(compressed.Length);
                compressedPkg.Write(compressed);

                // Send to the requesting player
                _fullSyncResponseRPC.SendPackage(sender, compressedPkg);

                Plugin.Log.LogInfo($"Sent full sync to player {playerId}: {syncData.ExplorationChunks?.Count ?? 0} chunks, {syncData.Pins?.Count ?? 0} pins");
            }
            finally
            {
                _pendingSyncRequests.Remove(playerId);
            }

            yield return null;
        }

        private IEnumerator OnExplorationUpdateServer(long sender, ZPackage pkg)
        {
            if (!Plugin.IsServer())
            {
                yield break;
            }

            // Decompress
            int compressedLength = pkg.ReadInt();
            byte[] compressed = pkg.ReadByteArray();
            byte[] decompressed = CompressionHelper.Decompress(compressed);
            var dataPkg = new ZPackage(decompressed);

            long playerId = dataPkg.ReadLong();
            int chunkCount = dataPkg.ReadInt();

            var chunks = new List<ExplorationChunk>(chunkCount);
            for (int i = 0; i < chunkCount; i++)
            {
                chunks.Add(ExplorationChunk.Deserialize(dataPkg));
            }

            LogDebug($"Received {chunkCount} exploration chunks from player {playerId}");

            // Merge into server's map data
            Plugin.ExplorationManager.MergeChunks(chunks);

            // Apply to server's minimap as well (host needs to see other players' exploration)
            Plugin.ExplorationManager.ApplyChunks(chunks);

            // Broadcast to all clients (including sender for consistency)
            BroadcastExplorationToClients(chunks, sender);

            yield return null;
        }

        private IEnumerator OnPinAddedServer(long sender, ZPackage pkg)
        {
            if (!Plugin.IsServer())
            {
                yield break;
            }

            long playerId = pkg.ReadLong();
            var pin = SharedPin.Deserialize(pkg);

            LogDebug($"Received pin added from player {playerId}: {pin}");

            // Validate and add
            if (Plugin.PinManager.CanAddPin(playerId))
            {
                // Add to server's pin list AND apply to server's minimap (host needs to see it)
                Plugin.PinManager.AddSharedPin(pin, true);

                // Broadcast to all clients
                BroadcastPinToClients(pin, PinAction.Added, sender);
            }
            else
            {
                Plugin.Log.LogWarning($"Player {playerId} has reached pin limit");
            }

            yield return null;
        }

        private IEnumerator OnPinRemovedServer(long sender, ZPackage pkg)
        {
            if (!Plugin.IsServer())
            {
                yield break;
            }

            long playerId = pkg.ReadLong();
            string pinId = pkg.ReadString();

            LogDebug($"Received pin removed from player {playerId}: {pinId}");

            // Remove from server AND from server's minimap (host needs to see removal)
            Plugin.PinManager.RemoveSharedPin(pinId, true);
            BroadcastPinRemovalToClients(pinId, sender);

            yield return null;
        }

        private IEnumerator OnPinUpdatedServer(long sender, ZPackage pkg)
        {
            if (!Plugin.IsServer())
            {
                yield break;
            }

            long playerId = pkg.ReadLong();
            var pin = SharedPin.Deserialize(pkg);

            LogDebug($"Received pin updated from player {playerId}: {pin}");

            // Update on server AND on server's minimap (host needs to see updates)
            Plugin.PinManager.UpdateSharedPin(pin, true);
            BroadcastPinToClients(pin, PinAction.Updated, sender);

            yield return null;
        }

        // Unused server-side handlers for client RPCs
        private IEnumerator OnFullSyncResponseServer(long sender, ZPackage pkg) { yield break; }
        private IEnumerator OnBroadcastExplorationServer(long sender, ZPackage pkg) { yield break; }
        private IEnumerator OnBroadcastPinServer(long sender, ZPackage pkg) { yield break; }

        #endregion

        #region Client-side RPC Handlers

        private IEnumerator OnFullSyncResponseClient(long sender, ZPackage pkg)
        {
            IsReceivingSync = true;

            try
            {
                // Decompress
                int compressedLength = pkg.ReadInt();
                byte[] compressed = pkg.ReadByteArray();
                byte[] decompressed = CompressionHelper.Decompress(compressed);
                var dataPkg = new ZPackage(decompressed);

                var syncData = MapSyncData.Deserialize(dataPkg);

                Plugin.Log.LogInfo($"Received full sync: {syncData.ExplorationChunks?.Count ?? 0} chunks, {syncData.Pins?.Count ?? 0} pins");

                // Apply exploration data
                if (syncData.ExplorationChunks != null)
                {
                    Plugin.ExplorationManager.ApplyChunks(syncData.ExplorationChunks);
                }

                // Apply pins
                if (syncData.Pins != null)
                {
                    Plugin.PinManager.ApplyPins(syncData.Pins);
                }
            }
            finally
            {
                IsReceivingSync = false;
            }

            yield return null;
        }

        private IEnumerator OnBroadcastExplorationClient(long sender, ZPackage pkg)
        {
            if (Plugin.IsServer())
            {
                yield break;
            }

            // Decompress
            int compressedLength = pkg.ReadInt();
            byte[] compressed = pkg.ReadByteArray();
            byte[] decompressed = CompressionHelper.Decompress(compressed);
            var dataPkg = new ZPackage(decompressed);

            long fromPlayerId = dataPkg.ReadLong();
            int chunkCount = dataPkg.ReadInt();

            var chunks = new List<ExplorationChunk>(chunkCount);
            for (int i = 0; i < chunkCount; i++)
            {
                chunks.Add(ExplorationChunk.Deserialize(dataPkg));
            }

            LogDebug($"Received {chunkCount} exploration chunks broadcast");

            // Apply to local map
            Plugin.ExplorationManager.ApplyChunks(chunks);

            yield return null;
        }

        private IEnumerator OnBroadcastPinClient(long sender, ZPackage pkg)
        {
            if (Plugin.IsServer())
            {
                yield break;
            }

            long fromPlayerId = pkg.ReadLong();
            var action = (PinAction)pkg.ReadInt();
            var pin = SharedPin.Deserialize(pkg);

            LogDebug($"Received pin broadcast: {action} {pin}");

            switch (action)
            {
                case PinAction.Added:
                    Plugin.PinManager.AddSharedPin(pin, true);
                    break;
                case PinAction.Updated:
                    Plugin.PinManager.UpdateSharedPin(pin, true);
                    break;
                case PinAction.Removed:
                    Plugin.PinManager.RemoveSharedPin(pin.Id, true);
                    break;
            }

            yield return null;
        }

        // Unused client-side handlers for server RPCs
        private IEnumerator OnRequestFullSyncClient(long sender, ZPackage pkg) { yield break; }
        private IEnumerator OnExplorationUpdateClient(long sender, ZPackage pkg) { yield break; }
        private IEnumerator OnPinAddedClient(long sender, ZPackage pkg) { yield break; }
        private IEnumerator OnPinRemovedClient(long sender, ZPackage pkg) { yield break; }
        private IEnumerator OnPinUpdatedClient(long sender, ZPackage pkg) { yield break; }

        #endregion

        #region Broadcast Helpers

        private void BroadcastExplorationToClients(List<ExplorationChunk> chunks, long excludePlayer)
        {
            if (chunks == null || chunks.Count == 0) return;
            if (ZNet.instance == null) return;

            var pkg = new ZPackage();
            pkg.Write(excludePlayer);
            pkg.Write(chunks.Count);

            foreach (var chunk in chunks)
            {
                chunk.Serialize(pkg);
            }

            byte[] compressed = CompressionHelper.Compress(pkg.GetArray());
            var compressedPkg = new ZPackage();
            compressedPkg.Write(compressed.Length);
            compressedPkg.Write(compressed);

            // Send to ALL connected peers (including sender so everyone has same data)
            var peers = ZNet.instance.GetPeers();
            LogDebug($"Broadcasting exploration to {peers.Count} peers");

            foreach (var peer in peers)
            {
                // Create a fresh package for each peer
                var peerPkg = new ZPackage();
                peerPkg.Write(compressed.Length);
                peerPkg.Write(compressed);
                _broadcastExplorationRPC.SendPackage(peer.m_uid, peerPkg);
                LogDebug($"Sent exploration to peer {peer.m_uid}");
            }
        }

        private void BroadcastPinToClients(SharedPin pin, PinAction action, long excludePlayer)
        {
            if (pin == null) return;
            if (ZNet.instance == null) return;

            // Send to ALL connected peers (including sender so everyone has same data)
            var peers = ZNet.instance.GetPeers();
            LogDebug($"Broadcasting pin {action} to {peers.Count} peers: {pin}");

            foreach (var peer in peers)
            {
                // Create a fresh package for each peer
                var pkg = new ZPackage();
                pkg.Write(excludePlayer);
                pkg.Write((int)action);
                pin.Serialize(pkg);
                _broadcastPinRPC.SendPackage(peer.m_uid, pkg);
                LogDebug($"Sent pin to peer {peer.m_uid}");
            }
        }

        private void BroadcastPinRemovalToClients(string pinId, long excludePlayer)
        {
            var pin = new SharedPin { Id = pinId };
            BroadcastPinToClients(pin, PinAction.Removed, excludePlayer);
        }

        #endregion

        #region Utility

        private long GetLocalPlayerId()
        {
            return Player.m_localPlayer?.GetPlayerID() ?? 0;
        }

        private void LogDebug(string message)
        {
            if (Plugin.ConfigManager?.DebugMode?.Value == true)
            {
                Plugin.Log.LogDebug($"[Network] {message}");
            }
        }

        #endregion
    }

    /// <summary>
    /// Actions that can be performed on pins
    /// </summary>
    public enum PinAction
    {
        Added = 0,
        Removed = 1,
        Updated = 2
    }
}
