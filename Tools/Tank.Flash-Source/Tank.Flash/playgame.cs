using System;
using System.Configuration;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;

namespace Tank.Flash;

public class playgame : Page
{
	private string _content = "";

	private string autoParam = "";

	protected HtmlHead Head1;

	public string Content => _content;

	public string LoginOnUrl => ConfigurationManager.AppSettings["LoginOnUrl"];

	public string SiteTitle
	{
		get
		{
			if (ConfigurationManager.AppSettings["SiteTitle"] != null)
			{
				return ConfigurationManager.AppSettings["SiteTitle"];
			}
			return "DDTank";
		}
	}

	public string Config => ConfigurationManager.AppSettings["FlashConfig"];

	public string Flash => ConfigurationManager.AppSettings["FlashSite"];

	public string AutoParam => autoParam;

	protected void Page_Load(object sender, EventArgs e)
	{
		try
		{
			if (Session["username"] == null && string.IsNullOrEmpty(Session["username"].ToString()))
			{
				Response.Redirect(LoginOnUrl, endResponse: false);
				return;
			}
			if (!LoadingManager.Login(Request["user"], Session["password"].ToString()))
			{
				LoadingManager.Remove(Session["username"].ToString());
				Response.Redirect(LoginOnUrl, endResponse: false);
				return;
			}
			if ("1" == ConfigurationManager.AppSettings["content2"])
			{
				string text = Request["content2"];
				if (!string.IsNullOrEmpty(text))
				{
					_content = text;
				}
				else
				{
					Response.Redirect(LoginOnUrl, endResponse: false);
				}
				return;
			}
			string text2 = HttpUtility.UrlDecode(Request["user"]);
			string text3 = HttpUtility.UrlDecode(Request["key"]);
			HttpUtility.UrlDecode(Request["config"]);
			string str = ((Request["editby"] == null) ? "" : HttpUtility.UrlDecode(Request["editby"]));
			if (!string.IsNullOrEmpty(text2) && !string.IsNullOrEmpty(text3))
			{
				_content = "user=" + HttpUtility.UrlEncode(text2) + "&key=" + HttpUtility.UrlEncode(text3);
				autoParam = "editby=" + HttpUtility.UrlEncode(str);
			}
			else
			{
				Response.Redirect(LoginOnUrl, endResponse: false);
			}
		}
		catch
		{
			Response.Redirect(LoginOnUrl, endResponse: false);
		}
	}
}
