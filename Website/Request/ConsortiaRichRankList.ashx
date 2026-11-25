<%@ WebHandler Language="C#" Class="Tank.Request.ConsortiaRichRankList" %>
//
// ConsortiaRichRankList.ashx
// ---------------------------------------------------------------------------------------
// Guild member "riches" ranking used by the guild window ("Thuong tuan" / weekly reward).
//
// Client contract (verified against WebSite\Flash\4.swf - ddt.loader.LoaderCreate):
//   GET ConsortiaRichRankList.ashx?selfid=<id>&key=<md5>&rnd=<n>&consortiaID=<guildID>
//   loader type 6 = RequestLoader extends TextLoader => response MUST be plain (uncompressed)
//   XML UTF-8 text (no zlib, no gzip).
//
//   consortion.analyze.ConsortiaRichRankAnalyze requires:
//     <Result value="true" message="...">
//        <Item Rank="1" UserID="5" ConsortiaID="1" Rich="0" Name="hahaa" />
//        ...
//     </Result>
//   - @value must be exactly "true" (lowercase); otherwise the client raises a load error
//     and opens the "tips" dialog with the request URL.
//   - It reads direct children named "Item" and copies *attributes* whose names match the
//     properties of consortion.data.ConsortiaWeekRewardPlayerVo
//     { Rank:int, UserID:int, ConsortiaID:int, Rich:int, Name:String } - names are
//     case-sensitive, attribute order does not matter, extra attributes are ignored.
//   - consortion.view.selfConsortia.ConsortionWeekRewardView then shows the first 10 rows
//     with Rich >= 1000 and "-" placeholders for the rest.
//
// Data source:
//   Bussiness.ConsortiaBussiness.GetConsortiaUsersPage() -> V_Consortia_Users, i.e. all
//   (online + offline) members of the guild. "Rich" is the same value the game exposes as
//   SqlDataProvider.Data.PlayerInfo.Riches = RichesOffer + RichesRob (guild wealth offered
//   by the member).
//
// Unknown/absent consortiaID is answered with a valid empty list instead of an error, so a
// stale browser request or a player without guild can never trigger the "tips" dialog.
// ---------------------------------------------------------------------------------------
using System;
using System.IO;
using System.Text;
using System.Web;
using System.Reflection;
using Bussiness;
using SqlDataProvider.Data;
using log4net;

namespace Tank.Request
{
    public class ConsortiaRichRankList : IHttpHandler
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private const int MaxEntries = 1000;

        public bool IsReusable
        {
            get { return false; }
        }

        public void ProcessRequest(HttpContext context)
        {
            LogRequest(context);
            bool success = false;
            string message = "Fail!";
            StringBuilder items = new StringBuilder();
            try
            {
                int consortiaID;
                if (!int.TryParse(context.Request["consortiaID"], out consortiaID) || consortiaID <= 0)
                {
                    success = true;
                    message = "Success!";
                }
                else
                {
                    int total = 0;
                    using (ConsortiaBussiness bussiness = new ConsortiaBussiness())
                    {
                        // state -1 = both online and offline members; userID -1 = all users.
                        ConsortiaUserInfo[] users = bussiness.GetConsortiaUsersPage(1, MaxEntries, ref total, 0, consortiaID, -1, -1);
                        Array.Sort(users, CompareRiches);
                        for (int i = 0; i < users.Length; i++)
                        {
                            ConsortiaUserInfo user = users[i];
                            items.Append("<Item");
                            items.Append(" Rank=\"").Append(i + 1).Append("\"");
                            items.Append(" UserID=\"").Append(user.UserID).Append("\"");
                            items.Append(" ConsortiaID=\"").Append(user.ConsortiaID).Append("\"");
                            items.Append(" Rich=\"").Append(user.RichesOffer + user.RichesRob).Append("\"");
                            items.Append(" Name=\"").Append(HtmlEncodeAttribute(user.UserName == null ? string.Empty : user.UserName)).Append("\"");
                            items.Append(" />");
                        }
                    }
                    success = true;
                    message = "Success!";
                }
            }
            catch (Exception exception)
            {
                log.Error("ConsortiaRichRankList", exception);
            }
            WriteResult(context, success, message, items);
        }

        private static void WriteResult(HttpContext context, bool success, string message, StringBuilder items)
        {
            context.Response.ContentType = "text/plain";
            context.Response.Write("<Result value=\"" + (success ? "true" : "false") + "\" message=\"" + message + "\">" + items.ToString() + "</Result>");
        }

        private static int CompareRiches(ConsortiaUserInfo left, ConsortiaUserInfo right)
        {
            int leftRiches = left.RichesOffer + left.RichesRob;
            int rightRiches = right.RichesOffer + right.RichesRob;
            if (leftRiches != rightRiches)
            {
                return rightRiches.CompareTo(leftRiches);
            }
            return left.UserID.CompareTo(right.UserID);
        }

        private static string HtmlEncodeAttribute(string value)
        {
            return HttpUtility.HtmlEncode(value);
        }

        /// <summary>
        /// Small, append-only diagnostic log: one line per request. It makes it possible to
        /// confirm from the real client which query string is sent (the Flash client sends
        /// selfid/key/rnd/consortiaID) without touching IIS logging.
        /// </summary>
        private static void LogRequest(HttpContext context)
        {
            try
            {
                string directory = context.Server.MapPath("~/logs");
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    + "\t" + context.Request.HttpMethod
                    + "\t" + context.Request.RawUrl
                    + "\t" + (context.Request.UserAgent == null ? string.Empty : context.Request.UserAgent);
                File.AppendAllText(Path.Combine(directory, "richrank-requests.log"), line + Environment.NewLine);
            }
            catch
            {
                // Diagnostics must never break the handler.
            }
        }
    }
}
