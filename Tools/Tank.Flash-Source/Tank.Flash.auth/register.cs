using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using Bussiness;

namespace Tank.Flash.auth;

public class register : IHttpHandler, IRequiresSessionState
{
	private string email;

	private string message;

	private string password;

	private string repassword;

	private bool sex = true;

	private string username;

	private string code;

	protected string SiteTitle
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

	protected bool CheckPara(HttpContext context, ref string message)
	{
		if (context.Session["CheckCode"] != null && !(code.ToLower() != context.Session["CheckCode"].ToString().ToLower()))
		{
			using (MemberShipBussiness memberShipBussiness = new MemberShipBussiness())
			{
				if (memberShipBussiness.ExistsUsername(username))
				{
					message = "exit";
					return false;
				}
			}
			return true;
		}
		message = "Mã xác nhận không đúng!";
		return false;
	}

	protected bool CreateUsername(HttpContext context, ref string message)
	{
		password = FormsAuthentication.HashPasswordForStoringInConfigFile(password, "md5");
		using MemberShipBussiness memberShipBussiness = new MemberShipBussiness();
		return memberShipBussiness.CreateUsername(SiteTitle, username, password, email, "1", "MD5", sex);
	}

	public void ProcessRequest(HttpContext context)
	{
		username = context.Request["username"];
		password = context.Request["password"];
		repassword = context.Request["repassword"];
		email = context.Request["email"];
		sex = context.Request["sex"] == "1";
		code = context.Request["code"];
		message = "Đăng ký thất bại!";
		if (CheckPara(context, ref message) && CreateUsername(context, ref message))
		{
			message = "Đăng ký thành công, đang chuyển vào game..";
		}
		context.Response.Write(message);
	}
}
