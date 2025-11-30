using UnityEngine;

namespace Atlas.Utils
{
    /// <summary>
    /// Helper class for coordinate conversions between world space and map pixels
    /// </summary>
    public static class CoordinateHelper
    {
        // Valheim's map texture size
        public const int MapTextureSize = 2048;

        // World size (radius from center)
        public const float WorldRadius = 10500f;

        /// <summary>
        /// Convert world position to map pixel coordinates
        /// </summary>
        public static (int pixelX, int pixelY) WorldToPixel(Vector3 worldPos)
        {
            // Normalize to 0-1 range
            float normalizedX = (worldPos.x + WorldRadius) / (WorldRadius * 2f);
            float normalizedZ = (worldPos.z + WorldRadius) / (WorldRadius * 2f);

            // Convert to pixel coordinates
            int pixelX = Mathf.Clamp(Mathf.RoundToInt(normalizedX * MapTextureSize), 0, MapTextureSize - 1);
            int pixelY = Mathf.Clamp(Mathf.RoundToInt(normalizedZ * MapTextureSize), 0, MapTextureSize - 1);

            return (pixelX, pixelY);
        }

        /// <summary>
        /// Convert map pixel coordinates to world position (at y=0)
        /// </summary>
        public static Vector3 PixelToWorld(int pixelX, int pixelY)
        {
            float normalizedX = (float)pixelX / MapTextureSize;
            float normalizedZ = (float)pixelY / MapTextureSize;

            float worldX = (normalizedX * WorldRadius * 2f) - WorldRadius;
            float worldZ = (normalizedZ * WorldRadius * 2f) - WorldRadius;

            return new Vector3(worldX, 0f, worldZ);
        }

        /// <summary>
        /// Convert world radius to pixel radius
        /// </summary>
        public static int WorldRadiusToPixels(float worldRadius)
        {
            float pixelsPerUnit = MapTextureSize / (WorldRadius * 2f);
            return Mathf.CeilToInt(worldRadius * pixelsPerUnit);
        }

        /// <summary>
        /// Convert pixel radius to world radius
        /// </summary>
        public static float PixelRadiusToWorld(int pixelRadius)
        {
            float unitsPerPixel = (WorldRadius * 2f) / MapTextureSize;
            return pixelRadius * unitsPerPixel;
        }

        /// <summary>
        /// Get the chunk coordinates for a pixel position
        /// </summary>
        public static (int chunkX, int chunkY) PixelToChunk(int pixelX, int pixelY, int chunkSize)
        {
            return (pixelX / chunkSize, pixelY / chunkSize);
        }

        /// <summary>
        /// Get the local position within a chunk
        /// </summary>
        public static (int localX, int localY) PixelToLocalChunk(int pixelX, int pixelY, int chunkSize)
        {
            return (pixelX % chunkSize, pixelY % chunkSize);
        }

        /// <summary>
        /// Get the number of chunks for the full map
        /// </summary>
        public static int GetChunkCount(int chunkSize)
        {
            return MapTextureSize / chunkSize;
        }

        /// <summary>
        /// Clamp pixel coordinates to valid range
        /// </summary>
        public static (int x, int y) ClampPixel(int x, int y)
        {
            return (
                Mathf.Clamp(x, 0, MapTextureSize - 1),
                Mathf.Clamp(y, 0, MapTextureSize - 1)
            );
        }

        /// <summary>
        /// Check if a world position is within the map bounds
        /// </summary>
        public static bool IsInMapBounds(Vector3 worldPos)
        {
            return Mathf.Abs(worldPos.x) <= WorldRadius && Mathf.Abs(worldPos.z) <= WorldRadius;
        }

        /// <summary>
        /// Calculate the distance between two pixel positions
        /// </summary>
        public static float PixelDistance(int x1, int y1, int x2, int y2)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
