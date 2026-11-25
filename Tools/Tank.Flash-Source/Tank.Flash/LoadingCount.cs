using System;
using System.Web.UI;

namespace Tank.Flash;

public class LoadingCount : Page
{
	protected void Page_Load(object sender, EventArgs e)
	{
		Response.Write($"{LoadingManager.LoadingCount}");
	}
}
