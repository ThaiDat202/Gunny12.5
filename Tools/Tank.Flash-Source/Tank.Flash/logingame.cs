using System;
using System.Configuration;
using System.Web;
using System.Web.UI;
using Bussiness.Interface;
using Road.Flash;

namespace Tank.Flash;

public class logingame : Page
{
	public string LoginOnUrl => ConfigurationManager.AppSettings["LoginOnUrl"];

	public static string FlashUrl => ConfigurationManager.AppSettings["FlashUrl"];

	protected void Page_Load(object sender, EventArgs e)
	{
		if (Session["username"] == null && string.IsNullOrEmpty(Session["username"].ToString()))
		{
			Response.Redirect(LoginOnUrl, endResponse: false);
		}
		else if (!LoadingManager.Login(Session["username"].ToString(), Session["password"].ToString()))
		{
			Response.Redirect(LoginOnUrl, endResponse: false);
		}
		try
		{
			string text = Session["username"].ToString();
			string text2 = Guid.NewGuid().ToString();
			string text3 = BaseInterface.ConvertDateTimeInt(DateTime.Now).ToString();
			string text4 = string.Empty;
			if (string.IsNullOrEmpty(text4))
			{
				text4 = BaseInterface.GetLoginKey;
			}
			string text5 = BaseInterface.md5(text + text2 + text3.ToString() + text4);
			string text6 = BaseInterface.RequestContent(BaseInterface.LoginUrl + "?content=" + HttpUtility.UrlEncode(text + "|" + text2 + "|" + text3.ToString() + "|" + text5));
			if (text6 == "0")
			{
				string url = FlashUrl + "?user=" + HttpUtility.UrlEncode(text) + "&key=" + HttpUtility.UrlEncode(text2);
				if ("1" == ConfigurationManager.AppSettings["content2"])
				{
					text2 = ((Session["password"] == null) ? text2 : Session["password"].ToString());
					string iv = "5C90D3C2C576A773";
					string privateKey = "5628eb9a3485fbf61f51927b8a8eee03c5962c6b64847aeb";
					string plainText = text + "|" + text2;
					url = FlashUrl + "?content2=" + CryptoHelper.TripleDesEncrypt(privateKey, plainText, ref iv);
				}
				Response.Redirect(url, endResponse: false);
			}
			else
			{
				Response.Write(text6);
			}
		}
		catch (Exception ex)
		{
			Response.Write(ex.ToString());
		}
	}
}
