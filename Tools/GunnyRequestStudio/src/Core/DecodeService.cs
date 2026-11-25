using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace GunnyRequestStudio.Core
{
    /// <summary>Ket qua giai ma / nen 1 file.</summary>
    public class DecodeResult
    {
        public string Path;
        public string Name;
        public string Status;              // ok | unchanged | skipped | failed
        public bool WasCompressed;
        public long FileSize;              // kich thuoc file goc (tren dia)
        public long DecodedSize;           // kich thuoc sau khi giai nen (bytes XML)
        public int ItemCount;
        public string Root;
        public string Wrapper;
        public string TableName;
        public string MapStatus;
        public string OutputPath;
        public string Error;
        public string XmlText;
        public double Seconds;
        public string Summary
        {
            get
            {
                return string.Format("{0} · {1} · nén {2} → giải nén {3}",
                    Status, WasCompressed ? "zlib" : "xml thường",
                    Paths.FormatSize(FileSize), Paths.FormatSize(DecodedSize));
            }
        }
    }

    /// <summary>
    /// Giai ma file .xml request cua Gunny: file tren dia la zlib-compressed XML.
    /// Ho tro: xem truoc, giai ma 1 file / ca thu muc, nen lai (.xml thuong -> .xml nen)
    /// de dua vao WebSite\Request.
    /// </summary>
    public class DecodeService
    {
        private readonly Logger _log;
        private readonly TableMapper _mapper;

        public DecodeService(Logger log, TableMapper mapper = null)
        {
            _log = log ?? new Logger();
            _mapper = mapper;
        }

        // ------------------------------------------------------------ giai ma

        /// <summary>Doc file, giai nen va (tuy chon) phan tich XML + map bang CSDL.</summary>
        public DecodeResult DecodeFile(string path, bool pretty = true, bool analyze = true)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = new DecodeResult { Path = path, Name = Path.GetFileNameWithoutExtension(path ?? string.Empty) };
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    r.Status = "failed";
                    r.Error = "không tìm thấy file";
                    return r;
                }
                var raw = File.ReadAllBytes(path);
                r.FileSize = raw.Length;
                r.WasCompressed = XmlCodec.LooksCompressed(raw);
                var text = XmlCodec.DecodeToText(raw);
                if (string.IsNullOrEmpty(text))
                {
                    r.Status = "failed";
                    r.Error = r.WasCompressed ? "giải nén xong nhưng không phải XML" : "không phải file XML/zlib";
                    return r;
                }
                r.DecodedSize = Encoding.UTF8.GetByteCount(text);
                if (pretty) text = PrettyXml(text);
                r.XmlText = text;
                if (analyze)
                {
                    try
                    {
                        var info = XmlCodec.Inspect(path, raw);
                        r.Root = info.RootElement;
                        r.Wrapper = info.Wrapper;
                        r.ItemCount = info.ItemCount;
                        if (_mapper != null && info.Attributes.Count > 0)
                        {
                            var map = _mapper.Resolve(info);
                            r.TableName = map.TableName;
                            r.MapStatus = map.Status;
                        }
                    }
                    catch (Exception ex)
                    {
                        r.Error = "phân tích XML: " + ex.Message;
                    }
                }
                r.Status = "ok";
                return r;
            }
            catch (Exception ex)
            {
                r.Status = "failed";
                r.Error = ex.Message;
                return r;
            }
            finally
            {
                sw.Stop();
                r.Seconds = Math.Round(sw.Elapsed.TotalSeconds, 3);
            }
        }

        /// <summary>Giai ma nhieu file trong thu muc ra thu muc dich (giu cau truc thu muc neu can).</summary>
        public List<DecodeResult> DecodeDirectory(string srcDir, string dstDir, string searchPattern, bool preserveStructure,
            bool overwrite, bool analyze, Action<DecodeResult> progress, CancellationToken ct)
        {
            var results = new List<DecodeResult>();
            if (string.IsNullOrEmpty(srcDir) || !Directory.Exists(srcDir))
            {
                _log.Warn("Giải mã: không thấy thư mục nguồn " + srcDir);
                return results;
            }
            var files = Directory.GetFiles(srcDir, string.IsNullOrEmpty(searchPattern) ? "*.xml" : searchPattern,
                SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            _log.Step("Giải mã " + files.Count + " file từ " + srcDir + " → " + dstDir);
            int ok = 0, skip = 0, fail = 0;
            foreach (var f in files)
            {
                ct.ThrowIfCancellationRequested();
                var rel = preserveStructure
                    ? f.Substring(srcDir.TrimEnd('\\', '/').Length).TrimStart('\\', '/')
                    : Path.GetFileName(f);
                var outPath = Path.Combine(dstDir, rel);
                var res = DecodeFile(f, true, analyze);
                if (res.Status == "ok")
                {
                    try
                    {
                        if (File.Exists(outPath) && !overwrite)
                        {
                            res.Status = "skipped";
                            res.Error = "đã tồn tại (bật Ghi đè để thay)";
                            skip++;
                        }
                        else
                        {
                            Paths.EnsureDir(Path.GetDirectoryName(outPath));
                            var bytes = Encoding.UTF8.GetBytes(res.XmlText);
                            Paths.WriteAtomic(outPath, bytes);
                            res.OutputPath = outPath;
                            ok++;
                        }
                    }
                    catch (Exception ex)
                    {
                        res.Status = "failed";
                        res.Error = "ghi file: " + ex.Message;
                        fail++;
                    }
                }
                else fail++;
                results.Add(res);
                var h = progress;
                if (h != null) { try { h(res); } catch { } }
            }
            _log.Okay(string.Format("Giải mã xong: OK={0}, bỏ qua={1}, lỗi={2} ({3} file)", ok, skip, fail, files.Count));
            return results;
        }

        /// <summary>Chi phan tich (khong ghi file) - dung cho xem truoc hang loat.</summary>
        public List<DecodeResult> Scan(string srcDir, string searchPattern, Action<DecodeResult> progress, CancellationToken ct)
        {
            var results = new List<DecodeResult>();
            if (string.IsNullOrEmpty(srcDir) || !Directory.Exists(srcDir)) return results;
            var files = Directory.GetFiles(srcDir, string.IsNullOrEmpty(searchPattern) ? "*.xml" : searchPattern,
                SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var f in files)
            {
                ct.ThrowIfCancellationRequested();
                var res = DecodeFile(f, true, true);
                results.Add(res);
                var h = progress;
                if (h != null) { try { h(res); } catch { } }
            }
            return results;
        }

        // ------------------------------------------------------------ nen lai

        /// <summary>Nen 1 file XML thuong thanh .xml zlib de dua vao Request.</summary>
        public DecodeResult EncodeFile(string xmlPath, string outPath, bool overwrite = true)
        {
            var r = new DecodeResult { Path = xmlPath, Name = Path.GetFileNameWithoutExtension(xmlPath ?? string.Empty) };
            try
            {
                if (!File.Exists(xmlPath)) { r.Status = "failed"; r.Error = "không thấy file"; return r; }
                var raw = File.ReadAllBytes(xmlPath);
                r.FileSize = raw.Length;
                var text = Encoding.UTF8.GetString(raw);
                if (XmlCodec.LooksCompressed(raw))
                {
                    // da nen san: giai ma roi nen lai de chuan hoa
                    text = XmlCodec.DecodeToText(raw) ?? text;
                }
                var packed = XmlCodec.Compress(text);
                r.DecodedSize = Encoding.UTF8.GetByteCount(text);
                r.WasCompressed = true;
                if (string.IsNullOrEmpty(outPath)) outPath = xmlPath;
                if (File.Exists(outPath) && !overwrite)
                {
                    r.Status = "skipped";
                    r.Error = "đã tồn tại";
                    return r;
                }
                Paths.EnsureDir(Path.GetDirectoryName(outPath));
                Paths.WriteAtomic(outPath, packed);
                r.OutputPath = outPath;
                r.Status = "ok";
                return r;
            }
            catch (Exception ex)
            {
                r.Status = "failed";
                r.Error = ex.Message;
                return r;
            }
        }

        /// <summary>Nen ca thu muc XML thuong -> .xml zlib.</summary>
        public List<DecodeResult> EncodeDirectory(string srcDir, string dstDir, string searchPattern, bool preserveStructure,
            bool overwrite, Action<DecodeResult> progress, CancellationToken ct)
        {
            var results = new List<DecodeResult>();
            if (string.IsNullOrEmpty(srcDir) || !Directory.Exists(srcDir)) return results;
            var files = Directory.GetFiles(srcDir, string.IsNullOrEmpty(searchPattern) ? "*.xml" : searchPattern,
                SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            _log.Step("Nén " + files.Count + " file XML → " + dstDir);
            foreach (var f in files)
            {
                ct.ThrowIfCancellationRequested();
                var rel = preserveStructure
                    ? f.Substring(srcDir.TrimEnd('\\', '/').Length).TrimStart('\\', '/')
                    : Path.GetFileName(f);
                var outPath = Path.Combine(dstDir, rel);
                var r = EncodeFile(f, outPath, overwrite);
                results.Add(r);
                var h = progress;
                if (h != null) { try { h(r); } catch { } }
            }
            int ok = results.Count(x => x.Status == "ok");
            _log.Okay("Nén xong: OK=" + ok + "/" + files.Count);
            return results;
        }

        // ------------------------------------------------------------ helpers

        /// <summary>Pretty-print XML (thu tu thuoc tinh giu nguyen).</summary>
        public static string PrettyXml(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return xml;
            try
            {
                var doc = XDocument.Parse(xml, LoadOptions.None);
                return doc.ToString();
            }
            catch
            {
                return xml;
            }
        }

        /// <summary>So sanh nhanh: file co phai zlib XML khong.</summary>
        public static bool IsPackedXml(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                using (var fs = File.OpenRead(path))
                {
                    int b0 = fs.ReadByte();
                    return b0 == 0x78 || b0 == 0x1F;
                }
            }
            catch { return false; }
        }
    }
}
