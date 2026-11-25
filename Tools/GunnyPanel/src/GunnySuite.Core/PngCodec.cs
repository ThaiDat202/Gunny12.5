using System.IO.Compression;
using System.Text;

namespace GunnySuite.Core;

/// <summary>
/// PNG encoder/decoder toi gian (khong phu thuoc thu vien ngoai):
/// - Ghi: PNG 8-bit grayscale, filter 0 (dung cho mask bomb).
/// - Doc: ho tro PNG 8-bit khong interlace, color type 0/2/3/4/6 (gray/RGB/palette/gray-alpha/RGBA).
/// </summary>
public static class PngCodec
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    public sealed class Image
    {
        public int Width { get; init; }
        public int Height { get; init; }
        /// <summary>RGBA 8-bit, length = Width * Height * 4.</summary>
        public byte[] Rgba { get; init; } = Array.Empty<byte>();
    }

    // ---------------- Write ----------------

    public static void WriteMaskPng(string path, int width, int height, bool[] pixels)
    {
        if (width <= 0 || height <= 0) throw new InvalidDataException("Kich thuoc PNG khong hop le");
        if (pixels.Length != width * height) throw new InvalidDataException("So pixel khong khop");

        // scanlines: 1 filter byte + width byte (grayscale)
        var raw = new byte[height * (1 + width)];
        var offset = 0;
        var index = 0;
        for (var y = 0; y < height; y++)
        {
            raw[offset++] = 0; // filter None
            for (var x = 0; x < width; x++)
                raw[offset++] = pixels[index++] ? (byte)255 : (byte)0;
        }

        var ihdr = new byte[13];
        WriteBE32(ihdr, 0, (uint)width);
        WriteBE32(ihdr, 4, (uint)height);
        ihdr[8] = 8;   // bit depth
        ihdr[9] = 0;   // color type grayscale
        ihdr[10] = 0;  // compression
        ihdr[11] = 0;  // filter
        ihdr[12] = 0;  // interlace

        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
        file.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        WriteChunk(file, "IHDR", ihdr);
        WriteChunk(file, "IDAT", ZlibCompress(raw));
        WriteChunk(file, "IEND", Array.Empty<byte>());
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(data, 0, data.Length);
        return output.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBE32(length, 0, (uint)data.Length);
        stream.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        var crc = Crc32(typeBytes, data);
        var crcBytes = new byte[4];
        WriteBE32(crcBytes, 0, crc);
        stream.Write(crcBytes);
    }

    // ---------------- Read ----------------

    public static Image Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
            throw new InvalidDataException("Khong phai file PNG: " + path);

        var offset = 8;
        var width = 0;
        var height = 0;
        byte depth = 0, colorType = 0, interlace = 0;
        byte[]? palette = null;
        byte[]? transparency = null;
        using var idat = new MemoryStream();

        while (offset + 8 <= bytes.Length)
        {
            var length = (int)ReadBE32(bytes, offset);
            var type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
            offset += 8;
            if (length < 0 || offset + length + 4 > bytes.Length)
                throw new InvalidDataException("Chunk PNG bi cat: " + type);

            switch (type)
            {
                case "IHDR":
                    width = (int)ReadBE32(bytes, offset);
                    height = (int)ReadBE32(bytes, offset + 4);
                    depth = bytes[offset + 8];
                    colorType = bytes[offset + 9];
                    interlace = bytes[offset + 12];
                    break;
                case "PLTE":
                    palette = new byte[length];
                    Buffer.BlockCopy(bytes, offset, palette, 0, length);
                    break;
                case "tRNS":
                    transparency = new byte[length];
                    Buffer.BlockCopy(bytes, offset, transparency, 0, length);
                    break;
                case "IDAT":
                    idat.Write(bytes, offset, length);
                    break;
                case "IEND":
                    offset = bytes.Length;
                    break;
            }

            offset += length + 4; // data + crc
        }

        if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            throw new InvalidDataException($"PNG kich thuoc khong hop le: {width}x{height}");
        if (depth != 8)
            throw new InvalidDataException($"Chi ho tro PNG 8-bit (file nay {depth}-bit)");
        if (interlace != 0)
            throw new InvalidDataException("Chua ho tro PNG interlace (hay luu lai dang non-interlaced)");

        var channels = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => throw new InvalidDataException("Color type PNG khong ho tro: " + colorType)
        };
        var stride = width * channels;
        var expected = (stride + 1) * height;

        idat.Position = 0;
        byte[] filtered;
        using (var zlib = new ZLibStream(idat, CompressionMode.Decompress, leaveOpen: true))
        using (var plain = new MemoryStream())
        {
            zlib.CopyTo(plain);
            filtered = plain.ToArray();
        }
        if (filtered.Length < expected)
            throw new InvalidDataException("Du lieu PNG thieu (bi cat?)");

        var pixels = Unfilter(filtered, stride, height, channels);
        var rgba = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                var i = row + x * channels;
                var o = (y * width + x) * 4;
                switch (colorType)
                {
                    case 0:
                        rgba[o] = rgba[o + 1] = rgba[o + 2] = pixels[i];
                        rgba[o + 3] = 255;
                        break;
                    case 2:
                        rgba[o] = pixels[i];
                        rgba[o + 1] = pixels[i + 1];
                        rgba[o + 2] = pixels[i + 2];
                        rgba[o + 3] = 255;
                        break;
                    case 3:
                    {
                        if (palette == null) throw new InvalidDataException("PNG palette thieu PLTE");
                        var pi = pixels[i];
                        if (pi * 3 + 2 >= palette.Length) pi = 0;
                        rgba[o] = palette[pi * 3];
                        rgba[o + 1] = palette[pi * 3 + 1];
                        rgba[o + 2] = palette[pi * 3 + 2];
                        rgba[o + 3] = transparency != null && pi < transparency.Length ? transparency[pi] : (byte)255;
                        break;
                    }
                    case 4:
                        rgba[o] = rgba[o + 1] = rgba[o + 2] = pixels[i];
                        rgba[o + 3] = pixels[i + 1];
                        break;
                    default: // 6
                        rgba[o] = pixels[i];
                        rgba[o + 1] = pixels[i + 1];
                        rgba[o + 2] = pixels[i + 2];
                        rgba[o + 3] = pixels[i + 3];
                        break;
                }
            }
        }

        return new Image { Width = width, Height = height, Rgba = rgba };
    }

    private static byte[] Unfilter(byte[] filtered, int stride, int height, int bpp)
    {
        var result = new byte[stride * height];
        var src = 0;
        for (var y = 0; y < height; y++)
        {
            var filter = filtered[src++];
            var rowStart = y * stride;
            var prevStart = rowStart - stride;
            for (var i = 0; i < stride; i++)
            {
                int raw = filtered[src + i];
                int left = i >= bpp ? result[rowStart + i - bpp] : 0;
                int up = y > 0 ? result[prevStart + i] : 0;
                int upLeft = y > 0 && i >= bpp ? result[prevStart + i - bpp] : 0;
                int value = filter switch
                {
                    0 => raw,
                    1 => raw + left,
                    2 => raw + up,
                    3 => raw + (left + up) / 2,
                    4 => raw + Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException("Filter PNG khong ho tro: " + filter)
                };
                result[rowStart + i] = (byte)(value & 0xFF);
            }
            src += stride;
        }
        return result;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        return pb <= pc ? b : c;
    }

    // ---------------- Helpers ----------------

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(byte[] typeBytes, byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in typeBytes) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint ReadBE32(byte[] data, int offset) =>
        (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);

    private static void WriteBE32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }
}
