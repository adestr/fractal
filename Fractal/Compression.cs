using System.IO.Compression;

namespace Fractal;

public static class Compression
{
    public static byte[] Compress(int[] data)
    {
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
        {
            using var binaryWriter = new BinaryWriter(gzipStream);
            foreach (var value in data)
            {
                binaryWriter.Write(value);
            }
        }
        return memoryStream.ToArray();
    }

    public static int[] Decompress(byte[] compressedData)
    {
        using var inputStream = new MemoryStream(compressedData);
        using var decompressor = new GZipStream(inputStream, CompressionMode.Decompress);
        //using var binaryReader = new BinaryReader(decompressor);
        var decompressedData = new List<int>();



        using var outputStream = new MemoryStream();
        decompressor.CopyTo(outputStream);
        outputStream.Position = 0;

        using var binaryReader = new BinaryReader(outputStream);

        try
        {

            while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
            {
                decompressedData.Add(binaryReader.ReadInt32());
            }
        }
        catch (EndOfStreamException)
        {
            // Reached the end of the stream, exit the loop
        }
        return decompressedData.ToArray();
    }
}