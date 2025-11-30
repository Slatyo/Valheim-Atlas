using System;
using System.Collections.Generic;

// ZPackage is from assembly_valheim.dll (global namespace)

namespace Atlas.Models
{
    /// <summary>
    /// Container for full map synchronization data sent to new players
    /// </summary>
    [Serializable]
    public class MapSyncData
    {
        /// <summary>
        /// Version of the sync data format for compatibility
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// All exploration chunks with explored areas
        /// </summary>
        public List<ExplorationChunk> ExplorationChunks { get; set; }

        /// <summary>
        /// All shared pins
        /// </summary>
        public List<SharedPin> Pins { get; set; }

        /// <summary>
        /// Unix timestamp of when this sync data was generated
        /// </summary>
        public long GeneratedAt { get; set; }

        /// <summary>
        /// World name this data belongs to
        /// </summary>
        public string WorldName { get; set; }

        public MapSyncData()
        {
            ExplorationChunks = new List<ExplorationChunk>();
            Pins = new List<SharedPin>();
            GeneratedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Serialize the full sync data to a ZPackage
        /// </summary>
        public void Serialize(ZPackage pkg)
        {
            pkg.Write(Version);
            pkg.Write(WorldName ?? "");
            pkg.Write(GeneratedAt);

            // Write exploration chunks
            pkg.Write(ExplorationChunks?.Count ?? 0);
            if (ExplorationChunks != null)
            {
                foreach (var chunk in ExplorationChunks)
                {
                    chunk.Serialize(pkg);
                }
            }

            // Write pins
            pkg.Write(Pins?.Count ?? 0);
            if (Pins != null)
            {
                foreach (var pin in Pins)
                {
                    pin.Serialize(pkg);
                }
            }
        }

        /// <summary>
        /// Deserialize sync data from a ZPackage
        /// </summary>
        public static MapSyncData Deserialize(ZPackage pkg)
        {
            var data = new MapSyncData
            {
                Version = pkg.ReadInt(),
                WorldName = pkg.ReadString(),
                GeneratedAt = pkg.ReadLong()
            };

            // Read exploration chunks
            int chunkCount = pkg.ReadInt();
            data.ExplorationChunks = new List<ExplorationChunk>(chunkCount);
            for (int i = 0; i < chunkCount; i++)
            {
                data.ExplorationChunks.Add(ExplorationChunk.Deserialize(pkg));
            }

            // Read pins
            int pinCount = pkg.ReadInt();
            data.Pins = new List<SharedPin>(pinCount);
            for (int i = 0; i < pinCount; i++)
            {
                data.Pins.Add(SharedPin.Deserialize(pkg));
            }

            return data;
        }

        public override string ToString()
        {
            return $"MapSyncData v{Version} World={WorldName} Chunks={ExplorationChunks?.Count ?? 0} Pins={Pins?.Count ?? 0}";
        }
    }
}
