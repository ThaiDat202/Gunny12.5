using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Linq;

namespace GunnyRequestStudio.Core
{
    /// <summary>
    /// Doc/ghi XML request cua Gunny (zlib-compressed theo chuan publisher).
    /// Cau truc file: zlib(deflate) cua XML, root Result co the chua
    /// ItemTemplate/Store/RuneTemplate -> Item|item (cac dong du lieu).
    /// </summary>
    public static class XmlCodec
    {
        private static readonly string[] Wrappers = new[] { "ItemTemplate", "Store", "RuneTemplate" };

        // ------------------------------------------------------------ decode

        public static bool LooksCompressed(byte[] data)
        {
            if (data == null || data.Length < 3) return false;
            if (data[0] == 0x78) return true;                 // zlib
            if (data[0] == 0x1F && data[1] == 0x8B) return true; // gzip
            return false;
        }

        public static byte[] Decompress(byte[] data)
        {
            if (data == null) return null;
            if (data.Length >= 3 && data[0] == 0x1F && data[1] == 0x8B)
            {
                using (var ms = new MemoryStream(data))
                using (var gz = new GZipStream(ms, CompressionMode.Decompress))
                using (var outMs = new MemoryStream())
                {
                    gz.CopyTo(outMs);
                    return outMs.ToArray();
                }
            }
            if (data.Length >= 2 && data[0] == 0x78)
            {
                try { return Inflate(data, 2, data.Length - 2); } catch { }
            }
            // raw deflate (khong header)
            try { return Inflate(data, 0, data.Length); } catch { }
            return null;
        }

        private static byte[] Inflate(byte[] data, int offset, int count)
        {
            using (var ms = new MemoryStream(data, offset, count))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            using (var outMs = new MemoryStream())
            {
                var buf = new byte[81920];
                int read;
                while ((read = ds.Read(buf, 0, buf.Length)) > 0) outMs.Write(buf, 0, read);
                return outMs.ToArray();
            }
        }

        /// <summary>Tra ve noi dung XML dang text (giai nen neu can). Null neu khong phai XML.</summary>
        public static string DecodeToText(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            string text = null;
            if (LooksCompressed(data))
            {
                var raw = Decompress(data);
                if (raw != null && raw.Length > 0) text = DecodeText(raw);
            }
            if (text == null) text = DecodeText(data);
            if (text == null) return null;
            var t = text.TrimStart('\uFEFF', '\u00EF', '\u00BB', '\u00BF', ' ', '\r', '\n', '\t');
            if (!t.StartsWith("<")) return null;
            return text;
        }

        private static string DecodeText(byte[] raw)
        {
            if (raw == null || raw.Length == 0) return null;
            try
            {
                if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
                    return Encoding.UTF8.GetString(raw, 3, raw.Length - 3);
                // detect UTF-16 BOM
                if (raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE) return Encoding.Unicode.GetString(raw, 2, raw.Length - 2);
                if (raw.Length >= 2 && raw[0] == 0xFE && raw[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(raw, 2, raw.Length - 2);
                return Encoding.UTF8.GetString(raw);
            }
            catch { return null; }
        }

        // ------------------------------------------------------------ encode

        /// <summary>Nen XML thanh zlib (dinh dang publisher: 0x78 0x9C + deflate + adler32).</summary>
        public static byte[] Compress(string xml)
        {
            var raw = Encoding.UTF8.GetBytes(xml ?? string.Empty);
            using (var outMs = new MemoryStream())
            {
                outMs.WriteByte(0x78);
                outMs.WriteByte(0x9C);
                using (var ds = new DeflateStream(outMs, CompressionMode.Compress, true))
                {
                    ds.Write(raw, 0, raw.Length);
                }
                var adler = Adler32(raw);
                outMs.WriteByte((byte)(adler >> 24));
                outMs.WriteByte((byte)(adler >> 16));
                outMs.WriteByte((byte)(adler >> 8));
                outMs.WriteByte((byte)adler);
                return outMs.ToArray();
            }
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (var x in data) { a = (a + x) % 65521; b = (b + a) % 65521; }
            return (b << 16) | a;
        }

        // ------------------------------------------------------------ inspect

        public static XmlDocInfo Inspect(string filePath, byte[] data = null)
        {
            var info = new XmlDocInfo { FilePath = filePath, Name = Path.GetFileNameWithoutExtension(filePath ?? string.Empty) };
            try
            {
                if (data == null) data = File.ReadAllBytes(filePath);
                info.SizeBytes = data.Length;
                info.IsCompressed = LooksCompressed(data);
                var text = DecodeToText(data);
                if (text == null) { info.Error = "not-xml"; return info; }
                var doc = XDocument.Parse(text, LoadOptions.None);
                if (doc.Root == null) { info.Error = "empty"; return info; }
                info.RootElement = doc.Root.Name.LocalName;

                XElement wrap = null;
                foreach (var w in Wrappers)
                {
                    var el = doc.Root.Element(w);
                    if (el != null) { wrap = el; info.Wrapper = w; break; }
                }
                var container = wrap ?? doc.Root;

                // Tim element "dong du lieu": uu tien Item/item (o bat ky do sau nao),
                // neu khong co thi chon nhom element co nhieu attribute nhat.
                var allAttrElements = container.Descendants().Where(e => e.HasAttributes).ToList();
                var itemGroups = allAttrElements
                    .Where(e => e.Name.LocalName == "Item" || e.Name.LocalName == "item")
                    .GroupBy(e => e.Name.LocalName)
                    .OrderByDescending(g => g.Count()).ToList();
                XElement first = null;
                if (itemGroups.Count > 0)
                {
                    first = itemGroups[0].First();
                }
                else
                {
                    var g = allAttrElements.GroupBy(e => e.Name.LocalName)
                        .OrderByDescending(gr => gr.Max(x => x.Attributes().Count(a => !a.IsNamespaceDeclaration)))
                        .ThenByDescending(gr => gr.Count())
                        .FirstOrDefault();
                    if (g != null) first = g.First();
                }
                if (first == null) return info;
                info.ItemElement = first.Name.LocalName;

                // Ho tro cau truc long nhau: <Result><Card ID="1"><Item .../></Card></Result>
                // => gop attribute cua element cha vao tung dong (Card/Item, Skill/SkillDetail...)
                var parent = first.Parent;
                bool mergeParent = parent != null && parent != container && parent != doc.Root && parent.HasAttributes;
                XElement parentTemplate = mergeParent ? parent : null;

                var attrOrder = new List<string>();
                if (mergeParent)
                {
                    foreach (var attr in parentTemplate.Attributes())
                        if (!attr.IsNamespaceDeclaration) attrOrder.Add(attr.Name.LocalName);
                }
                foreach (var attr in first.Attributes())
                {
                    if (attr.IsNamespaceDeclaration) continue;
                    if (!attrOrder.Any(x => string.Equals(x, attr.Name.LocalName, StringComparison.OrdinalIgnoreCase)))
                        attrOrder.Add(attr.Name.LocalName);
                }
                info.Attributes = attrOrder;

                var rows = new List<Dictionary<string, string>>();
                var itemNodes = allAttrElements.Where(e => e.Name.LocalName == info.ItemElement).ToList();
                foreach (var e in itemNodes)
                {
                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    // gop attribute cua cha (neu co) - vi du <Card CardID=".."><Item .../></Card>
                    var pe = e.Parent;
                    if (pe != null && pe != container && pe != doc.Root && pe.HasAttributes)
                    {
                        foreach (var attr in pe.Attributes())
                            if (!attr.IsNamespaceDeclaration) row[attr.Name.LocalName] = attr.Value ?? string.Empty;
                    }
                    foreach (var attr in e.Attributes())
                        if (!attr.IsNamespaceDeclaration) row[attr.Name.LocalName] = attr.Value ?? string.Empty;
                    rows.Add(row);
                }
                info.ItemCount = rows.Count;
                info.Rows = rows;
                return info;
            }
            catch (Exception ex)
            {
                info.Error = ex.Message;
                return info;
            }
        }

        public static XmlDocInfo InspectPackage(string name, byte[] data)
        {
            var tmp = Path.Combine(Path.GetTempPath(), "grs-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                File.WriteAllBytes(tmp, data);
                var info = Inspect(tmp, data);
                info.Name = name;
                return info;
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }

        // ------------------------------------------------------------ helpers

        public static string NormalizeName(string s)
        {
            if (s == null) return string.Empty;
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        public static string StripXmlExtension(string name)
        {
            if (name == null) return string.Empty;
            if (name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) return name.Substring(0, name.Length - 4);
            return name;
        }
    }
}


