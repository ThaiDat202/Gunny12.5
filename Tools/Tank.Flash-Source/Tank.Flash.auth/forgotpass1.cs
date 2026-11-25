using System.Web;

namespace Tank.Flash.auth;

public class forgotpass1 : IHttpHandler
{
	public bool IsReusable => false;

	public void ProcessRequest(HttpContext context)
	{
		string s = "Request False!";
		context.Response.Write(s);
	}
}
