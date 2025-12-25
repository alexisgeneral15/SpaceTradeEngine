using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Sistema de compresión sin pérdida para datos de juego.
    /// Usa DEFLATE/GZip para guardados y transferencia de red.
    /// </summary>
    public static class DataCompression
    {
        // Niveles de compresión predefinidos
        public enum CompressionLevel
        {
            Fast = 0,      // Más rápido, menos compresión
            Balanced = 1,  // Balance velocidad/ratio
            Maximum = 2    // Máxima compresión, más lento
        }

        /// <summary>
        /// Comprime datos usando DEFLATE (sin pérdida)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static byte[] Compress(byte[] data, CompressionLevel level = CompressionLevel.Balanced)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            using var outputStream = new MemoryStream();
            var compressionLevel = level switch
            {
                CompressionLevel.Fast => System.IO.Compression.CompressionLevel.Fastest,
                CompressionLevel.Maximum => System.IO.Compression.CompressionLevel.SmallestSize,
                _ => System.IO.Compression.CompressionLevel.Optimal
            };

            using (var deflateStream = new DeflateStream(outputStream, compressionLevel))
            {
                deflateStream.Write(data, 0, data.Length);
            }

            return outputStream.ToArray();
        }

        /// <summary>
        /// Descomprime datos DEFLATE
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static byte[] Decompress(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0)
                return Array.Empty<byte>();

            using var inputStream = new MemoryStream(compressedData);
            using var deflateStream = new DeflateStream(inputStream, System.IO.Compression.CompressionMode.Decompress);
            using var outputStream = new MemoryStream();
            
            deflateStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }

        /// <summary>
        /// Comprime string a bytes comprimidos
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] CompressString(string text, CompressionLevel level = CompressionLevel.Balanced)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<byte>();

            var bytes = Encoding.UTF8.GetBytes(text);
            return Compress(bytes, level);
        }

        /// <summary>
        /// Descomprime bytes a string
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string DecompressString(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0)
                return string.Empty;

            var bytes = Decompress(compressedData);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Comprime archivo y guarda resultado
        /// </summary>
        public static void CompressFile(string inputPath, string outputPath, CompressionLevel level = CompressionLevel.Balanced)
        {
            var data = File.ReadAllBytes(inputPath);
            var compressed = Compress(data, level);
            File.WriteAllBytes(outputPath, compressed);
        }

        /// <summary>
        /// Descomprime archivo
        /// </summary>
        public static void DecompressFile(string inputPath, string outputPath)
        {
            var compressed = File.ReadAllBytes(inputPath);
            var decompressed = Decompress(compressed);
            File.WriteAllBytes(outputPath, decompressed);
        }

        /// <summary>
        /// Calcula ratio de compresión
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetCompressionRatio(int originalSize, int compressedSize)
        {
            if (originalSize == 0) return 0f;
            return 1f - ((float)compressedSize / originalSize);
        }

        /// <summary>
        /// Compresión Run-Length Encoding para arrays homogéneos
        /// Útil para mapas de tiles o datos repetitivos
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static byte[] CompressRLE(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            using var output = new MemoryStream();
            byte currentValue = data[0];
            int count = 1;

            for (int i = 1; i < data.Length; i++)
            {
                if (data[i] == currentValue && count < 255)
                {
                    count++;
                }
                else
                {
                    output.WriteByte((byte)count);
                    output.WriteByte(currentValue);
                    currentValue = data[i];
                    count = 1;
                }
            }

            // Último bloque
            output.WriteByte((byte)count);
            output.WriteByte(currentValue);

            return output.ToArray();
        }

        /// <summary>
        /// Descomprime RLE
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static byte[] DecompressRLE(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0 || compressedData.Length % 2 != 0)
                return Array.Empty<byte>();

            using var output = new MemoryStream();
            
            for (int i = 0; i < compressedData.Length; i += 2)
            {
                byte count = compressedData[i];
                byte value = compressedData[i + 1];
                
                for (int j = 0; j < count; j++)
                    output.WriteByte(value);
            }

            return output.ToArray();
        }

        /// <summary>
        /// Delta encoding para arrays de números (útil para posiciones)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static int[] EncodeDelta(int[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<int>();

            var result = new int[values.Length];
            result[0] = values[0];

            for (int i = 1; i < values.Length; i++)
                result[i] = values[i] - values[i - 1];

            return result;
        }

        /// <summary>
        /// Decodifica delta encoding
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static int[] DecodeDelta(int[] deltas)
        {
            if (deltas == null || deltas.Length == 0)
                return Array.Empty<int>();

            var result = new int[deltas.Length];
            result[0] = deltas[0];

            for (int i = 1; i < deltas.Length; i++)
                result[i] = result[i - 1] + deltas[i];

            return result;
        }
    }
}
