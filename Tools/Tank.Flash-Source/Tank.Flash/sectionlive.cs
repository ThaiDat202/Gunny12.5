using System.Web;
using System.Web.SessionState;

namespace Tank.Flash;

public class sectionlive : IHttpHandler, IRequiresSessionState
{
	public bool IsReusable => false;

	public void ProcessRequest(HttpContext context)
	{
		string text = context.Request["skiplive"];
		try
		{
			if (text == "false")
			{
				LoadingManager.Remove(context.Session["username"].ToString());
				context.Response.Write("clear");
			}
			else if (context.Session["username"] != null && !string.IsNullOrEmpty(context.Session["username"].ToString()))
			{
				string name = context.Session["username"].ToString();
				string pass = context.Session["password"].ToString();
				if (LoadingManager.Login(name, pass))
				{
					context.Response.Write("live");
				}
				else
				{
					context.Response.Write("die");
				}
			}
			else
			{
				context.Response.Write("die");
			}
		}
		catch
		{
			context.Response.Write("sessionfail");
		}
	}
}
