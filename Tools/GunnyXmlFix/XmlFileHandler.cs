using System;
using System.IO;
using System.Text;
using System.Web;

namespace GunnyFix
{
    /// <summary>
    /// Handler thay thế cho PetSkillInfo.ashx / PetSkillElementInfo.ashx /
    /// MagicStoneTemplate.ashx / subweaponevolutiontemplate.ashx.
    ///
    /// Lý do: các handler gốc sinh lại XML từ DB bằng bộ ghi THIẾU trường
    /// (petskillinfo 14/18, petskillelementinfo 5/6, magicstonetemplate 10/21,
    /// subweaponevolutiontemplate 4/7) và ghi đè file client tải về.
    ///
    /// Handler này trả file XML đầy đủ trong _fixed_xml (giải nén zlib nếu cần,
    /// giống bản gốc isCompress:false), không bao giờ ghi đè bằng bản thiếu trường.
    /// </summary>
    public sealed class XmlFileHandler : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            try
            {
                string appPath = HttpRuntime.AppDomainAppPath;
                string requestName = Path.GetFileNameWithoutExtension(context.Request.Path);
                if (string.IsNullOrEmpty(requestName)) requestName = string.Empty;

                string liveName;
                string fixedName;
                switch (requestName.ToLowerInvariant())
                {
                    case "petskillinfo":
                        liveName = "petskillinfo.xml"; fixedName = "PetSkillInfo.xml"; break;
                    case "petskillelementinfo":
                        liveName = "petskillelementinfo.xml"; fixedName = "PetSkillElementInfo.xml"; break;
                    case "magicstonetemplate":
                        liveName = "magicstonetemplate.xml"; fixedName = "MagicStoneTemplate.xml"; break;
                    case "subweaponevolutiontemplate":
                        liveName = "subweaponevolutiontemplate.xml"; fixedName = "SubWeaponEvolutionTemplate.xml"; break;
                    default:
                        context.Response.StatusCode = 404;
                        context.Response.Write("GunnyFix: unknown handler " + requestName);
                        return;
                }

                string fixedPath = Path.Combine(Path.Combine(appPath, "_fixed_xml"), fixedName);
                string sourcePath = File.Exists(fixedPath) ? fixedPath : Path.Combine(appPath, liveName);
                if (!File.Exists(sourcePath))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Write("GunnyFix: missing " + fixedName);
                    return;
                }

                byte[] bytes = File.ReadAllBytes(sourcePath);

                // Đồng bộ file live (client tải tĩnh) nếu đang lệch kích thước
                try
                {
                    string livePath = Path.Combine(appPath, liveName);
                    if (!File.Exists(livePath) || new FileInfo(livePath).Length != bytes.Length)
                        File.WriteAllBytes(livePath, bytes);
                }
                catch { /* bỏ qua nếu không ghi được */ }

                byte[] payload = IsZlib(bytes) ? Inflate(bytes) : bytes;

                context.Response.ContentType = "text/xml";
                context.Response.ContentEncoding = Encoding.UTF8;
                context.Response.BinaryWrite(payload);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                context.Response.Write("GunnyFix error: " + ex.Message);
            }
        }

        private static bool IsZlib(byte[] b)
        {
            return b != null && b.Length > 2 && b[0] == 0x78;
        }

        private static byte[] Inflate(byte[] zlib)
        {
            using (var ms = new MemoryStream(zlib, 2, zlib.Length - 2))
            using (var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Decompress))
            using (var outMs = new MemoryStream())
            {
                ds.CopyTo(outMs);
                return outMs.ToArray();
            }
        }
    }
}
