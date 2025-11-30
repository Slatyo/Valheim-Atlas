using System;
using System.IO;
using System.IO.Compression;

namespace Atlas.Utils
{
    /// <summary>
    /// Helper class for compressing and decompressing data
    /// </summary>
    public static class CompressionHelper
    {
        /// <summary>
        /// Compress a byte array using GZip
        /// </summary>
        public static byte[] Compress(byte[] data)
        {
            if (data == null || data.Length == 0)
                return data;

            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
                {
                    gzip.Write(data, 0, data.Length);
                }
                return output.ToArray();
            }
        }

        /// <summary>
        /// Decompress a GZip compressed byte array
        /// </summary>
        public static byte[] Decompress(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0)
                return compressedData;

            using (var input = new MemoryStream(compressedData))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                return output.ToArray();
            }
        }

        /// <summary>
        /// Compress a ZPackage's data
        /// </summary>
        public static byte[] CompressPackage(ZPackage pkg)
        {
            if (pkg == null) return null;
            return Compress(pkg.GetArray());
        }

        /// <summary>
        /// Decompress data into a ZPackage
        /// </summary>
        public static ZPackage DecompressToPackage(byte[] compressedData)
        {
            if (compressedData == null) return null;
            byte[] decompressed = Decompress(compressedData);
            return new ZPackage(decompressed);
        }

        /// <summary>
        /// Get the compression ratio of data
        /// </summary>
        public static float GetCompressionRatio(int originalSize, int compressedSize)
        {
            if (originalSize == 0) return 0;
            return 1f - ((float)compressedSize / originalSize);
        }

        /// <summary>
        /// Write compressed data to a ZPackage (includes length header for decompression)
        /// </summary>
        public static void WriteCompressed(ZPackage pkg, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                pkg.Write(0); // Original length
                pkg.Write(0); // Compressed length
                return;
            }

            byte[] compressed = Compress(data);

            pkg.Write(data.Length);         // Original length
            pkg.Write(compressed.Length);   // Compressed length
            pkg.Write(compressed);          // Compressed data
        }

        /// <summary>
        /// Read compressed data from a ZPackage
        /// </summary>
        public static byte[] ReadCompressed(ZPackage pkg)
        {
            int originalLength = pkg.ReadInt();
            int compressedLength = pkg.ReadInt();

            if (originalLength == 0 || compressedLength == 0)
                return new byte[0];

            byte[] compressed = pkg.ReadByteArray();
            return Decompress(compressed);
        }
    }
}
