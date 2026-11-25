<%@ WebHandler Language="C#" Class="FarmIMListLoad" %>
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Web;
using System.Xml.Linq;

/// <summary>
/// Friend list cho client (thay Tank.Request.IMListLoad) — hỗ trợ cả tham số "id" (client gửi)
/// và "selfid" (handler gốc đọc). Dữ liệu: Sys_Users_Friends join Sys_Users_Detail (Db_Tank).
/// Trả XML nén zlib (CompressRequestLoader type 7).
/// </summary>
public class FarmIMListLoad : IHttpHandler
{
	public bool IsReusable { get { return false; } }

	public void ProcessRequest(HttpContext context)
	{
		bool flag = false;
		string message = "Fail!";
		XElement root = new XElement("Result");
		try
		{
			string rawId = context.Request["id"];
			if (string.IsNullOrEmpty(rawId)) rawId = context.Request["selfid"];
			int userId;
			if (int.TryParse((rawId ?? "").Trim(), out userId) && userId > 0)
			{
				AddCustomList(root, 0, "Bạn bè");
				AddCustomList(root, 1, "Danh sách đen");
				for (int i = 10; i <= 19; i++) AddCustomList(root, i, "");

				string conn = null;
				var cs = ConfigurationManager.ConnectionStrings["PlayerData"];
				if (cs != null) conn = cs.ConnectionString;
				if (string.IsNullOrEmpty(conn)) conn = ConfigurationManager.AppSettings["conString"];
				if (!string.IsNullOrEmpty(conn))
				{
					using (SqlConnection c = new SqlConnection(conn))
					{
						c.Open();
						string sql = "SELECT f.FriendID, f.Remark, f.Relation, ISNULL(u.NickName,N'') AS NickName, "
							+ "ISNULL(u.Grade,0) AS Grade, ISNULL(u.Sex,0) AS Sex, ISNULL(u.Colors,N'') AS Colors, "
							+ "ISNULL(u.Style,N'') AS Style, ISNULL(u.State,0) AS State "
							+ "FROM dbo.Sys_Users_Friends f INNER JOIN dbo.Sys_Users_Detail u ON u.UserID = f.FriendID "
							+ "WHERE f.UserID = @uid AND ISNULL(f.IsExist,1) = 1";
						using (SqlCommand cmd = new SqlCommand(sql, c))
						{
							cmd.Parameters.AddWithValue("@uid", userId);
							using (SqlDataReader r = cmd.ExecuteReader())
							{
								while (r.Read())
								{
									int friendId = Convert.ToInt32(r["FriendID"]);
									string nick = Convert.ToString(r["NickName"]);
									string remark = Convert.ToString(r["Remark"]);
									string colors = Convert.ToString(r["Colors"]);
									string style = Convert.ToString(r["Style"]);
									int grade = Convert.ToInt32(r["Grade"]);
									int sex = Convert.ToInt32(r["Sex"]);
									int state = Convert.ToInt32(r["State"]);
									int relation = Convert.ToInt32(r["Relation"]);
									root.Add(new XElement("Item",
										new XAttribute("ID", friendId),
										new XAttribute("NickName", nick),
										new XAttribute("LoginName", ""),
										new XAttribute("Style", style),
										new XAttribute("Sex", sex != 0),
										new XAttribute("Colors", colors),
										new XAttribute("Grade", grade),
										new XAttribute("Hide", 0),
										new XAttribute("ConsortiaName", ""),
										new XAttribute("TotalCount", 0),
										new XAttribute("EscapeCount", 0),
										new XAttribute("WinCount", 0),
										new XAttribute("Offer", 0),
										new XAttribute("Relation", relation),
										new XAttribute("Repute", 0),
										new XAttribute("State", state > 0 ? 1 : 0),
										new XAttribute("Nimbus", 0),
										new XAttribute("DutyName", ""),
										new XAttribute("AchievementPoint", 0),
										new XAttribute("Rank", ""),
										new XAttribute("FightPower", 0),
										new XAttribute("ApprenticeshipState", 0),
										new XAttribute("IsBeauty", false),
										new XAttribute("BBSFriends", false),
										new XAttribute("Mark", remark),
										new XAttribute("Birthday", "2026-01-01 00:00:00"),
										new XAttribute("UserName", ""),
										new XAttribute("IsMarried", false),
										new XAttribute("LastDate", "2026-01-01 00:00:00")));
								}
							}
						}
					}
				}
			}
			flag = true;
			message = "Success!";
		}
		catch (Exception ex)
		{
			message = "Fail: " + ex.Message;
		}
		root.Add(new XAttribute("value", flag ? "true" : "false"));
		root.Add(new XAttribute("message", message));

		byte[] data = Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));
		byte[] compressed;
		using (MemoryStream ms = new MemoryStream())
		{
			zlib.ZOutputStream z = new zlib.ZOutputStream(ms, 9);
			z.Write(data, 0, data.Length);
			z.Close();
			compressed = ms.ToArray();
		}
		context.Response.ContentType = "text/plain";
		context.Response.BinaryWrite(compressed);
	}

	private static void AddCustomList(XElement root, int id, string name)
	{
		root.Add(new XElement("customList", new XAttribute("ID", id), new XAttribute("Name", name)));
	}
}
