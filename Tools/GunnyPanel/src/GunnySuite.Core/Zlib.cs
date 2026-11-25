using System.IO.Compression;

namespace GunnySuite.Core;

/// <summary>zlib (RFC 1950) - dung cho cac file XML cua Gunny quest server.</summary>
public static class Zlib
{
    public static byte[] Compress(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            z.Write(data, 0, data.Length);
        return ms.ToArray();
    }

    public static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var z = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        z.CopyTo(output);
        return output.ToArray();
    }

    public static bool LooksZlib(byte[] data) => data.Length >= 2 && data[0] == 0x78;
    public static bool LooksXml(byte[] data) => data.Length > 0 && data[0] == (byte)'<';
}
