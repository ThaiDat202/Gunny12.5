using System.Configuration;
using System.Web;
using System.Web.SessionState;
using Bussiness;

namespace Tank.Flash;

public class checkuser : IHttpHandler, IRequiresSessionState
{
	public string SiteTitle
	{
		get
		{
			if (ConfigurationManager.AppSettings["SiteTitle"] != null)
			{
				return ConfigurationManager.AppSettings["SiteTitle"];
			}
			return "DanDanTang";
		}
	}

	public bool IsReusable => false;

	public void ProcessRequest(HttpContext context)
	{
		string text = context.Request["username"];
		string text2 = context.Request["password"];
		using MemberShipBussiness memberShipBussiness = new MemberShipBussiness();
		if (memberShipBussiness.CheckUsername(SiteTitle, text, text2))
		{
			context.Session["username"] = text;
			context.Session["password"] = text2;
			LoadingManager.Add(text, text2);
			context.Response.Write("ok");
		}
		else if (memberShipBussiness.CheckUsername("GameAdmin", text, text2))
		{
			context.Session["username"] = text;
			context.Session["password"] = text2;
			LoadingManager.Add(text, text2, isAdmin: true);
			context.Response.Write("ok");
		}
		else
		{
			context.Response.Write("Tài khoản hoặc mật khẩu không đúng!");
		}
	}
}
