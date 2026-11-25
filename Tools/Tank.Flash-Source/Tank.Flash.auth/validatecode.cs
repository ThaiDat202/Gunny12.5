using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Web;
using System.Web.SessionState;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using Bussiness;

namespace Tank.Flash.auth;

public class validatecode : IHttpHandler, IRequiresSessionState
{
	public bool IsReusable => false;

	public void ProcessRequest(HttpContext context)
	{
		string text = CheckCode.GenerateCheckCode();
		byte[] buffer = CheckCode.CreateImage(text);
		context.Session["CheckCode"] = text;
		context.Response.ClearContent();
		context.Response.ContentType = "image/Gif";
		context.Response.BinaryWrite(buffer);
	}
}
public class ValidateCode : Page
{
	protected HtmlForm form1;

	public static Color[] colors = new Color[4]
	{
		Color.Blue,
		Color.DarkRed,
		Color.Green,
		Color.Gold
	};

	private void CreateCheckCodeImage(string checkCode)
	{
		if (checkCode == null || checkCode.Trim() == string.Empty)
		{
			return;
		}
		Bitmap bitmap = new Bitmap((int)Math.Ceiling((double)checkCode.Length * 40.5), 44);
		bitmap.MakeTransparent();
		Graphics graphics = Graphics.FromImage(bitmap);
		try
		{
			Random random = new Random();
			Color color = colors[random.Next(colors.Length)];
			graphics.Clear(Color.Transparent);
			for (int i = 0; i < 2; i++)
			{
				int num = random.Next(bitmap.Width);
				random.Next(bitmap.Width);
				int num2 = random.Next(bitmap.Height);
				random.Next(bitmap.Height);
				graphics.DrawArc(new Pen(color, 2f), -num, -num2, bitmap.Width * 2, bitmap.Height, 45, 100);
			}
			Font font = new Font("Arial", 24f, FontStyle.Bold | FontStyle.Italic);
			LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, bitmap.Width, bitmap.Height), color, color, 1.2f, isAngleScaleable: true);
			graphics.DrawString(checkCode, font, brush, 2f, 2f);
			int num3 = 40;
			Math.Sin(Math.PI * (double)num3 / 180.0);
			Math.Cos(Math.PI * (double)num3 / 180.0);
			Math.Atan(Math.PI * (double)num3 / 180.0);
			if (num3 > 0)
			{
				_ = bitmap.Width;
			}
			new TextureBrush(bitmap).RotateTransform(30f);
			bitmap.Save("c:\\1.png", ImageFormat.Png);
			MemoryStream memoryStream = new MemoryStream();
			bitmap.Save(memoryStream, ImageFormat.Png);
			Response.ClearContent();
			Response.ContentType = "image/Png";
			Response.BinaryWrite(memoryStream.ToArray());
		}
		finally
		{
			graphics.Dispose();
			bitmap.Dispose();
		}
	}

	private string GenerateCheckCode()
	{
		string text = string.Empty;
		Random random = new Random();
		for (int i = 0; i < 4; i++)
		{
			int num = random.Next();
			text += (char)(65 + (ushort)(num % 26));
		}
		return text;
	}

	public static Bitmap KiRotate(Bitmap bmp, float angle, Color bkColor)
	{
		int num = bmp.Width + 2;
		int num2 = bmp.Height + 2;
		PixelFormat format = ((!(bkColor == Color.Transparent)) ? bmp.PixelFormat : PixelFormat.Format32bppArgb);
		Bitmap bitmap = new Bitmap(num, num2, format);
		Graphics graphics = Graphics.FromImage(bitmap);
		graphics.Clear(bkColor);
		graphics.DrawImageUnscaled(bmp, 1, 1);
		graphics.Dispose();
		GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddRectangle(new RectangleF(0f, 0f, num, num2));
		Matrix matrix = new Matrix();
		matrix.Rotate(angle);
		RectangleF bounds = graphicsPath.GetBounds(matrix);
		Bitmap bitmap2 = new Bitmap((int)bounds.Width, (int)bounds.Height, format);
		Graphics graphics2 = Graphics.FromImage(bitmap2);
		graphics2.Clear(bkColor);
		graphics2.TranslateTransform(0f - bounds.X, 0f - bounds.Y);
		graphics2.RotateTransform(angle);
		graphics2.InterpolationMode = InterpolationMode.HighQualityBilinear;
		graphics2.DrawImageUnscaled(bitmap, 0, 0);
		graphics2.Dispose();
		bitmap.Dispose();
		return bitmap2;
	}

	protected void Page_Load(object sender, EventArgs e)
	{
		string text = CheckCode.GenerateCheckCode();
		byte[] buffer = CheckCode.CreateImage(text);
		Session["CheckCode"] = text;
		Response.ClearContent();
		Response.ContentType = "image/Gif";
		Response.BinaryWrite(buffer);
	}
}
