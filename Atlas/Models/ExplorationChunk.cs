using System;

// ZPackage is from assembly_valheim.dll (global namespace)

namespace Atlas.Models
{
    /// <summary>
    /// Represents a chunk of exploration data for efficient network transfer
    /// </summary>
    [Serializable]
    public class ExplorationChunk
    {
        /// <summary>
        /// Chunk X position in the grid
        /// </summary>
        public int ChunkX { get; set; }

        /// <summary>
        /// Chunk Y position in the grid
        /// </summary>
        public int ChunkY { get; set; }

        /// <summary>
        /// Compressed pixel data for this chunk (1 bit per pixel, packed into bytes)
        /// </summary>
        public byte[] ExploredPixels { get; set; }

        /// <summary>
        /// Unix timestamp when this chunk was last modified
        /// </summary>
        public long LastModified { get; set; }

        /// <summary>
        /// Size of each chunk in pixels (square)
        /// </summary>
        public const int ChunkSize = 64;

        public ExplorationChunk()
        {
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public ExplorationChunk(int chunkX, int chunkY) : this()
        {
            ChunkX = chunkX;
            ChunkY = chunkY;
            // Each pixel is 1 bit, so we need ChunkSize * ChunkSize / 8 bytes
            ExploredPixels = new byte[(ChunkSize * ChunkSize) / 8];
        }

        /// <summary>
        /// Get the explored state of a pixel within this chunk
        /// </summary>
        public bool GetPixel(int localX, int localY)
        {
            if (ExploredPixels == null) return false;

            int index = localY * ChunkSize + localX;
            int byteIndex = index / 8;
            int bitIndex = index % 8;

            if (byteIndex >= ExploredPixels.Length) return false;

            return (ExploredPixels[byteIndex] & (1 << bitIndex)) != 0;
        }

        /// <summary>
        /// Set the explored state of a pixel within this chunk
        /// </summary>
        public void SetPixel(int localX, int localY, bool explored)
        {
            if (ExploredPixels == null)
                ExploredPixels = new byte[(ChunkSize * ChunkSize) / 8];

            int index = localY * ChunkSize + localX;
            int byteIndex = index / 8;
            int bitIndex = index % 8;

            if (byteIndex >= ExploredPixels.Length) return;

            if (explored)
                ExploredPixels[byteIndex] |= (byte)(1 << bitIndex);
            else
                ExploredPixels[byteIndex] &= (byte)~(1 << bitIndex);

            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// OR-merge another chunk's data into this one (union of explored areas)
        /// </summary>
        public void Merge(ExplorationChunk other)
        {
            if (other?.ExploredPixels == null) return;

            if (ExploredPixels == null)
                ExploredPixels = new byte[(ChunkSize * ChunkSize) / 8];

            for (int i = 0; i < Math.Min(ExploredPixels.Length, other.ExploredPixels.Length); i++)
            {
                ExploredPixels[i] |= other.ExploredPixels[i];
            }

            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Check if this chunk has any explored pixels
        /// </summary>
        public bool HasExploredPixels()
        {
            if (ExploredPixels == null) return false;

            for (int i = 0; i < ExploredPixels.Length; i++)
            {
                if (ExploredPixels[i] != 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Serialize the chunk to a ZPackage for network transfer
        /// </summary>
        public void Serialize(ZPackage pkg)
        {
            pkg.Write(ChunkX);
            pkg.Write(ChunkY);
            pkg.Write(LastModified);

            if (ExploredPixels != null)
            {
                pkg.Write(ExploredPixels.Length);
                pkg.Write(ExploredPixels);
            }
            else
            {
                pkg.Write(0);
            }
        }

        /// <summary>
        /// Deserialize a chunk from a ZPackage
        /// </summary>
        public static ExplorationChunk Deserialize(ZPackage pkg)
        {
            var chunk = new ExplorationChunk
            {
                ChunkX = pkg.ReadInt(),
                ChunkY = pkg.ReadInt(),
                LastModified = pkg.ReadLong()
            };

            int length = pkg.ReadInt();
            if (length > 0)
            {
                chunk.ExploredPixels = pkg.ReadByteArray();
            }

            return chunk;
        }

        /// <summary>
        /// Convert world coordinates to chunk coordinates
        /// </summary>
        public static (int chunkX, int chunkY) WorldToChunk(int pixelX, int pixelY)
        {
            return (pixelX / ChunkSize, pixelY / ChunkSize);
        }

        /// <summary>
        /// Convert pixel coordinates to local chunk coordinates
        /// </summary>
        public static (int localX, int localY) PixelToLocal(int pixelX, int pixelY)
        {
            return (pixelX % ChunkSize, pixelY % ChunkSize);
        }

        public override string ToString()
        {
            return $"ExplorationChunk[{ChunkX},{ChunkY}] LastMod={LastModified}";
        }
    }
}
