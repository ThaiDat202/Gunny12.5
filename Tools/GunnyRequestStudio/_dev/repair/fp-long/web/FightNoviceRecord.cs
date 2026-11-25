using System;
using System.IO;
using System.Web;
using System.Xml.Linq;

namespace Tank.Request
{
    /// <summary>
    /// Handler con thieu trong Tank.Request.dll cua package Gunny 12.6.
    /// Client (RegisterLuncher) goi FightNoviceRecord.ashx?userId=..&state=..&target=..&rnd=..
    /// sau khi tao nhan vat de ghi "train log"; client khong parse response
    /// nhung van can HTTP 200 + XML hop le de tranh loi.
    /// </summary>
    public class FightNoviceRecord : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            bool ok = false;
            string message = "fail!";
            try
            {
                var req = context.Request;
                string userId = req["userId"];
                string target = req["target"];
                string state = req["state"];
                string rnd = req["rnd"];

                try
                {
                    string logPath = context.Server.MapPath("~/logs/fightnovicerecord.log");
                    string line = string.Format("{0:yyyy-MM-dd HH:mm:ss}\tuserId={1}\tstate={2}\ttarget={3}\trnd={4}",
                        DateTime.Now, userId, state, target, rnd);
                    File.AppendAllText(logPath, line + Environment.NewLine);
                }
                catch { }

                ok = !string.IsNullOrEmpty(userId);
                message = ok ? "Success!" : "invalid userId";
            }
            catch (Exception ex)
            {
                message = "fail: " + ex.Message;
            }

            var xml = new XElement("Result",
                new XAttribute("value", ok),
                new XAttribute("message", message));
            context.Response.ContentType = "text/plain";
            context.Response.Write(xml.ToString(SaveOptions.DisableFormatting));
        }
    }
}
