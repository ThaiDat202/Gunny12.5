using System;
using System.Web;
using Bussiness;

namespace Tank.Flash;

public class CreatShortCut : IHttpHandler
{
	public bool IsReusable => false;

	public void ProcessRequest(HttpContext context)
	{
		try
		{
			string text = context.Request.QueryString["gameurl"];
			string text2 = context.Request.UserAgent.ToUpper();
			string text3 = LanguageMgr.GetTranslation("Game.ProductionName") + ".url";
			if (!text2.Contains("MS") || !text2.Contains("IE"))
			{
				text3 = ((!text2.Contains("FIREFOX")) ? HttpUtility.UrlEncode(text3) : ("\"" + text3 + "\""));
			}
			else
			{
				text3 = HttpUtility.UrlEncode(text3);
			}
			context.Response.ContentType = "application/octet-stream;";
			context.Response.AddHeader("Content-Disposition", "attachment;filename=" + text3);
			context.Response.Write("[InternetShortcut]\n");
			context.Response.Write("URL=" + text + "\n");
			context.Response.Write("IDList=\n");
			context.Response.Write("IconFile=\n");
			context.Response.Write("IconIndex=1\n");
			context.Response.Write("[{000214A0-0000-0000-C000-000000000046}]\n");
			context.Response.Write("Prop3=19,2\n");
			context.ApplicationInstance.CompleteRequest();
		}
		catch (Exception ex)
		{
			context.Response.Write("Error:" + ex);
		}
	}
}
