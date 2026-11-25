namespace GunnySuite.Core;

/// <summary>
/// File .bomb cua Gunny/DDTank: 8 byte header (width, height int32 LE) +
/// mask 1bpp, stride = (width >> 3) + 1, bit 1 = vung bi pha.
/// PNG ho tro qua PngCodec (tu viet, khong phu thuoc thu vien ngoai).
/// </summary>
public static class Bomb
{
    public sealed class Mask
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public bool[] Pixels { get; set; } = Array.Empty<bool>();

        public int Count
        {
            get
            {
                var n = 0;
                for (var i = 0; i < Pixels.Length; i++) if (Pixels[i]) n++;
                return n;
            }
        }
    }

    public static Mask Read(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < 8) throw new InvalidDataException("File quá ngắn: " + path);

        var width = BitConverter.ToInt32(data, 0);
        var height = BitConverter.ToInt32(data, 4);
        if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            throw new InvalidDataException($"Kích thước vô lý {width}x{height}: {path}");

        var stride = (width >> 3) + 1;
        var need = 8 + stride * height;
        if (data.Length != need)
            throw new InvalidDataException($"{Path.GetFileName(path)}: cần {need} byte cho {width}x{height}, thực tế {data.Length}");

        var mask = new Mask { Width = width, Height = height, Pixels = new bool[width * height] };
        for (var y = 0; y < height; y++)
        {
            var rowStart = 8 + y * stride;
            for (var x = 0; x < width; x++)
            {
                var b = data[rowStart + (x >> 3)];
                if (((b >> (7 - (x & 7))) & 1) != 0) mask.Pixels[y * width + x] = true;
            }
        }
        return mask;
    }

    public static void Write(string path, Mask mask)
    {
        var stride = (mask.Width >> 3) + 1;
        var data = new byte[8 + stride * mask.Height];
        var header = BitConverter.GetBytes(mask.Width);
        Buffer.BlockCopy(header, 0, data, 0, 4);
        header = BitConverter.GetBytes(mask.Height);
        Buffer.BlockCopy(header, 0, data, 4, 4);

        for (var y = 0; y < mask.Height; y++)
        {
            var rowStart = 8 + y * stride;
            for (var x = 0; x < mask.Width; x++)
            {
                if (!mask.Pixels[y * mask.Width + x]) continue;
                data[rowStart + (x >> 3)] |= (byte)(1 << (7 - (x & 7)));
            }
        }

        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        File.WriteAllBytes(path, data);
    }

    /// <summary>Mask -> PNG (mask trắng trên nền đen).</summary>
    public static void ToPng(Mask mask, string pngPath) =>
        PngCodec.WriteMaskPng(pngPath, mask.Width, mask.Height, mask.Pixels);

    /// <summary>PNG -> Mask: pixel alpha > 0 và độ sáng >= threshold.</summary>
    public static Mask FromPng(string pngPath, int threshold = 128)
    {
        var image = PngCodec.Read(pngPath);
        var mask = new Mask { Width = image.Width, Height = image.Height, Pixels = new bool[image.Width * image.Height] };
        for (var y = 0; y < image.Height; y++)
        {
            var row = y * image.Width * 4;
            for (var x = 0; x < image.Width; x++)
            {
                var o = row + x * 4;
                var a = image.Rgba[o + 3];
                if (a == 0) continue;
                var luma = (image.Rgba[o] * 299 + image.Rgba[o + 1] * 587 + image.Rgba[o + 2] * 114) / 1000;
                if (luma >= threshold) mask.Pixels[y * image.Width + x] = true;
            }
        }
        return mask;
    }
}
