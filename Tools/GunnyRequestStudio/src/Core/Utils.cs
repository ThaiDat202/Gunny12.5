using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace GunnyRequestStudio.Core
{
    public static class Paths
    {
        public static string BaseDir
        {
            get
            {
                var home = Environment.GetEnvironmentVariable("GRS_HOME");
                if (!string.IsNullOrWhiteSpace(home)) return home;
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        public static string Resolve(string path, string fallbackRelativeTo = null)
        {
            if (string.IsNullOrWhiteSpace(path)) return BaseDir;
            if (Path.IsPathRooted(path)) return path;
            var b = string.IsNullOrEmpty(fallbackRelativeTo) ? BaseDir : fallbackRelativeTo;
            return Path.GetFullPath(Path.Combine(b, path));
        }

        public static string ReportsDir(AppSettings s)
        {
            var dir = Resolve(s == null ? "reports" : s.ReportsDir);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public static string OverridesPath()
        {
            return Path.Combine(BaseDir, "table-map.overrides.json");
        }

        public static string SettingsPath()
        {
            return Path.Combine(BaseDir, "settings.json");
        }

        public static string LogPath()
        {
            var dir = Path.Combine(BaseDir, "logs");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "gunnyrequeststudio-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
        }

        public static void EnsureDir(string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        public static void WriteAtomic(string path, byte[] data)
        {
            EnsureDir(Path.GetDirectoryName(path));
            var tmp = path + ".part";
            try
            {
                File.WriteAllBytes(tmp, data);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";
            return (bytes / 1024.0 / 1024.0 / 1024.0).ToString("0.00") + " GB";
        }
    }

    public static class HttpUtil
    {
        public class Result
        {
            public int Status;
            public byte[] Bytes;
            public string Error;
            public long Length;
            public bool IsOk { get { return Status == 200 && Bytes != null; } }
            public bool IsMissing { get { return Status == 404 || Status == 403 || Status == 410; } }
        }

        static HttpUtil()
        {
            try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls; } catch { }
            try { ServicePointManager.DefaultConnectionLimit = 64; } catch { }
            try { ServicePointManager.Expect100Continue = false; } catch { }
        }

        public static string Combine(string baseUrl, string relativePath)
        {
            if (string.IsNullOrEmpty(baseUrl)) return relativePath;
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            if (string.IsNullOrEmpty(relativePath)) return baseUrl;
            var parts = relativePath.Replace('\\', '/').Split('/');
            var sb = new StringBuilder(baseUrl);
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(Uri.EscapeDataString(parts[i]));
            }
            return sb.ToString();
        }

        public static Result Get(string url, int timeoutMs = 20000, int retries = 2)
        {
            return Get(url, timeoutMs, retries, null);
        }

        public static Result Get(string url, int timeoutMs, int retries, Action<long, long> progress)
        {
            Exception last = null;
            for (int attempt = 0; attempt <= retries; attempt++)
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.Method = "GET";
                    req.Timeout = timeoutMs;
                    req.ReadWriteTimeout = timeoutMs;
                    req.AllowAutoRedirect = true;
                    req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    req.UserAgent = "GunnyRequestStudio/1.0";
                    req.KeepAlive = false;
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    {
                        var status = (int)resp.StatusCode;
                        var len = resp.ContentLength;
                        if (len <= 0) len = 0;
                        using (var s = resp.GetResponseStream())
                        using (var ms = new MemoryStream())
                        {
                            var buf = new byte[81920];
                            int read;
                            long total = 0;
                            while ((read = s.Read(buf, 0, buf.Length)) > 0)
                            {
                                ms.Write(buf, 0, read);
                                total += read;
                                if (progress != null) progress(total, len);
                            }
                            return new Result { Status = status, Bytes = ms.ToArray(), Length = total };
                        }
                    }
                }
                catch (WebException wex)
                {
                    last = wex;
                    var http = wex.Response as HttpWebResponse;
                    if (http != null)
                    {
                        var code = (int)http.StatusCode;
                        try { http.Close(); } catch { }
                        if (code >= 400 && code < 500) return new Result { Status = code, Error = "HTTP " + code };
                    }
                    if (attempt < retries) Thread.Sleep(250 * (attempt + 1));
                }
                catch (Exception ex)
                {
                    last = ex;
                    if (attempt < retries) Thread.Sleep(250 * (attempt + 1));
                }
            }
            return new Result { Status = 0, Error = last == null ? "unknown" : last.Message };
        }

        public static Result Head(string url, int timeoutMs = 15000, int retries = 1)
        {
            for (int attempt = 0; attempt <= retries; attempt++)
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.Method = "HEAD";
                    req.Timeout = timeoutMs;
                    req.AllowAutoRedirect = true;
                    req.UserAgent = "GunnyRequestStudio/1.0";
                    req.KeepAlive = false;
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    {
                        return new Result { Status = (int)resp.StatusCode, Length = resp.ContentLength };
                    }
                }
                catch (WebException wex)
                {
                    var http = wex.Response as HttpWebResponse;
                    if (http != null)
                    {
                        var code = (int)http.StatusCode;
                        try { http.Close(); } catch { }
                        if (code < 500) return new Result { Status = code, Error = "HTTP " + code };
                    }
                    if (attempt < retries) Thread.Sleep(200);
                }
                catch
                {
                    if (attempt < retries) Thread.Sleep(200);
                }
            }
            return new Result { Status = 0, Error = "network error" };
        }
    }
}

