using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;
using zlib;

namespace GunnyFix
{
    /// <summary>
    /// Handler thay thế cho ConsortiaUsersList.ashx (danh sách thành viên Guild).
    ///
    /// Lý do: handler gốc (Tank.Request.dll) ghi thuộc tính FightPower dạng int
    /// (clamp 2.147.483.647) và KHÔNG có ExFightPower, trong khi client đọc
    /// @ExFightPower (long) để cộng bù phần vượt 32-bit.
    ///
    /// Cách làm: gọi lại handler gốc qua ConsortiaUsersListRaw.ashx (giữ nguyên 100%
    /// thuộc tính cũ), rồi chỉ thêm ExFightPower = FightPower - min(FightPower, int.MaxValue)
    /// để client hiển thị đúng tổng lực chiến (int + phần bù).
    /// Response gốc là zlib (0x78 ...) nên handler cũng trả zlib y như vậy.
    /// </summary>
    public sealed class ConsortiaUsersListFix : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            bool compressed = true;
            try
            {
                string query = context.Request.Url != null ? context.Request.Url.Query : string.Empty;
                string url = "http://127.0.0.1:88/ConsortiaUsersListRaw.ashx" + query;

                byte[] raw;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "GunnyFix/ConsortiaUsersList");
                    raw = wc.DownloadData(url);
                }

                compressed = raw.Length > 1 && raw[0] == 0x78;
                string xml = compressed ? Inflate(raw) : Encoding.UTF8.GetString(raw);

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);

                XmlNodeList items = doc.SelectNodes("/Result/Item");
                List<int> ids = new List<int>();
                if (items != null)
                {
                    foreach (XmlNode n in items)
                    {
                        XmlAttribute a = n.Attributes != null ? n.Attributes["UserID"] : null;
                        int id;
                        if (a != null && int.TryParse(a.Value, out id) && !ids.Contains(id)) ids.Add(id);
                    }
                }

                Dictionary<int, long> power = LookupFightPower(ids);

                if (items != null)
                {
                    foreach (XmlNode n in items)
                    {
                        XmlAttribute a = n.Attributes != null ? n.Attributes["UserID"] : null;
                        int id;
                        long full;
                        if (a == null || !int.TryParse(a.Value, out id) || !power.TryGetValue(id, out full)) continue;

                        // Client: FightPower_Show = FightPower(int) + FightPower_Show(long)
                        // => phần bù = phần vượt quá int.MaxValue (giống FightPower_PacketShow của server).
                        long extra = full - Math.Min(full, (long)int.MaxValue);
                        if (extra < 0) extra = 0;

                        if (n.Attributes["ExFightPower"] == null)
                        {
                            XmlAttribute ex = doc.CreateAttribute("ExFightPower");
                            ex.Value = extra.ToString();
                            n.Attributes.Append(ex);
                        }
                        else
                        {
                            n.Attributes["ExFightPower"].Value = extra.ToString();
                        }
                    }
                }

                WriteResponse(context, doc.OuterXml, compressed);
            }
            catch (Exception ex)
            {
                string msg = (ex.Message ?? "error").Replace("\"", "'").Replace("<", " ").Replace(">", " ");
                WriteResponse(context, "<Result value=\"false\" message=\"" + msg + "\" />", compressed);
            }
        }

        private static string Inflate(byte[] raw)
        {
            using (MemoryStream ms = new MemoryStream(raw))
            using (ZInputStream zis = new ZInputStream(ms))
            using (MemoryStream outp = new MemoryStream())
            {
                byte[] buf = new byte[8192];
                int n;
                while ((n = zis.read(buf, 0, buf.Length)) > 0)
                {
                    outp.Write(buf, 0, n);
                }
                return Encoding.UTF8.GetString(outp.ToArray());
            }
        }

        private static void WriteResponse(HttpContext context, string xml, bool compressed)
        {
            byte[] data = Encoding.UTF8.GetBytes(xml);
            byte[] body;
            if (compressed)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    ZOutputStream zos = new ZOutputStream(ms, zlibConst.Z_DEFAULT_COMPRESSION);
                    try
                    {
                        zos.Write(data, 0, data.Length);
                        zos.finish();
                    }
                    finally
                    {
                        try { zos.Close(); } catch { }
                    }
                    body = ms.ToArray();
                }
            }
            else
            {
                body = data;
            }

            context.Response.ContentType = "text/xml";
            context.Response.BinaryWrite(body);
        }

        private static Dictionary<int, long> LookupFightPower(List<int> ids)
        {
            Dictionary<int, long> result = new Dictionary<int, long>();
            if (ids == null || ids.Count == 0) return result;

            string cs = ConfigurationManager.AppSettings["conString"];
            if (string.IsNullOrEmpty(cs))
                cs = "Data Source=DESKTOP-SMLR4VU;Initial Catalog=Db_Tank;User ID=sa;Password=123456;TrustServerCertificate=True";

            StringBuilder sql = new StringBuilder();
            sql.Append("SELECT UserID, FightPower FROM Db_Tank.dbo.Sys_Users_Detail WHERE UserID IN (");
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) sql.Append(',');
                sql.Append(ids[i]);
            }
            sql.Append(')');

            using (SqlConnection conn = new SqlConnection(cs))
            using (SqlCommand cmd = new SqlCommand(sql.ToString(), conn))
            {
                conn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int uid = r.GetInt32(0);
                        long fp = r.IsDBNull(1) ? 0L : r.GetInt64(1);
                        result[uid] = fp;
                    }
                }
            }
            return result;
        }
    }
}
