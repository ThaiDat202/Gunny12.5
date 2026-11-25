using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Bussiness;
using log4net;
using SqlDataProvider.Data;

namespace Game.Server.Farm;

/// <summary>
/// Job sinh file BXH nông trại (ManorWealth.xml = Tài Phú, ManorAbility.xml = Lao Động)
/// vào thư mục Request của web (client load qua REQUEST_PATH — vd http://127.0.0.1:88/).
/// Chạy mỗi 5 phút, ghi file zlib đúng format analyzer FarmManorWealthAnalyzer/FarmManorAbilityAnalyzer.
/// </summary>
public static class FarmRankJob
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private static readonly object Sync = new object();

	private static Timer _timer;

	private static bool _running;

	public static void Start()
	{
		lock (Sync)
		{
			if (_timer != null)
			{
				return;
			}
			_timer = new Timer(Tick, null, 3000, 300000);
			Log.Warn("FarmRankJob: started (manor rank xml, every 5 min)");
		}
	}

	private static void Tick(object state)
	{
		lock (Sync)
		{
			if (_running)
			{
				return;
			}
			_running = true;
		}
		try
		{
			Refresh();
			FarmRankSettler.CheckAndSettle();
		}
		catch (Exception ex)
		{
			Log.Error("FarmRankJob.Refresh error", ex);
		}
		finally
		{
			lock (Sync)
			{
				_running = false;
			}
		}
	}

	public static void Refresh()
	{
		ManorConfigMgr.EnsureLoaded();
		string dir = ManorConfigMgr.RequestDir;
		if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
		{
			return;
		}
		DateTime now = DateTime.Now;
		string stamp = now.ToString("MM-dd HH:mm");
		List<UserManorRankInfo> wealth = ManorBussiness.GetManorWealthRank(100);
		List<UserManorRankInfo> ability = ManorBussiness.GetManorAbilityRank(100);
		WriteRankXml(Path.Combine(dir, "ManorWealth.xml"), BuildWealthXml(wealth, stamp));
		WriteRankXml(Path.Combine(dir, "ManorAbility.xml"), BuildAbilityXml(ability, stamp));
	}

	private static void WriteRankXml(string path, string xml)
	{
		byte[] raw = Encoding.UTF8.GetBytes(xml);
		byte[] compressed = Game.Base.Marshal.Compress(raw);
		string tmp = path + ".tmp";
		File.WriteAllBytes(tmp, compressed);
		if (File.Exists(path))
		{
			File.Delete(path);
		}
		File.Move(tmp, path);
	}

	private static string BuildWealthXml(List<UserManorRankInfo> list, string stamp)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append("<Result value=\"true\" message=\"Success!\" date=\"").Append(stamp).Append("\">");
		int order = 0;
		foreach (UserManorRankInfo row in list)
		{
			order++;
			sb.Append("<Item Order=\"").Append(order).Append("\" NickName=\"").Append(Escape(row.NickName))
				.Append("\" UserId=\"").Append(row.UserID)
				.Append("\" TotalGP=\"").Append(row.Exp)
				.Append("\" Money=\"").Append(row.Money)
				.Append("\" Wealth=\"").Append(row.Money)
				.Append("\" Colors=\"").Append(Escape(row.Colors))
				.Append("\" Style=\"").Append(Escape(row.Style))
				.Append("\" Sex=\"").Append(row.Sex)
				.Append("\" AreaId=\"").Append(GetAppInt("AreaID", 1001))
				.Append("\" AreaName=\"").Append(Escape(GetAppString("AreaName", "Gunny")))
				.Append("\" />");
		}
		sb.Append("</Result>");
		return sb.ToString();
	}

	private static string BuildAbilityXml(List<UserManorRankInfo> list, string stamp)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append("<Result value=\"true\" message=\"Success!\" date=\"").Append(stamp).Append("\">");
		int order = 0;
		foreach (UserManorRankInfo row in list)
		{
			order++;
			int totalGrades = ManorConfigMgr.GetGradeByExp(ManorConfigMgr.LevelTypeFarmSkill, row.FarmExp)
				+ ManorConfigMgr.GetGradeByExp(ManorConfigMgr.LevelTypeFishSkill, row.FishExp)
				+ ManorConfigMgr.GetGradeByExp(ManorConfigMgr.LevelTypeMineSkill, row.MineExp)
				+ ManorConfigMgr.GetGradeByExp(ManorConfigMgr.LevelTypeRanchSkill, row.RanchExp);
			sb.Append("<Item Order=\"").Append(order).Append("\" NickName=\"").Append(Escape(row.NickName))
				.Append("\" UserId=\"").Append(row.UserID)
				.Append("\" TotalAbilityGrades=\"").Append(totalGrades)
				.Append("\" BossNickName=\"").Append(Escape(row.BossNickName))
				.Append("\" Colors=\"").Append(Escape(row.Colors))
				.Append("\" Style=\"").Append(Escape(row.Style))
				.Append("\" Sex=\"").Append(row.Sex)
				.Append("\" AreaId=\"").Append(GetAppInt("AreaID", 1001))
				.Append("\" AreaName=\"").Append(Escape(GetAppString("AreaName", "Gunny")))
				.Append("\" />");
		}
		sb.Append("</Result>");
		return sb.ToString();
	}

	private static string GetAppString(string key, string fallback)
	{
		string v = ConfigurationManager.AppSettings[key];
		return string.IsNullOrEmpty(v) ? fallback : v;
	}

	private static int GetAppInt(string key, int fallback)
	{
		int v;
		return int.TryParse(ConfigurationManager.AppSettings[key], out v) ? v : fallback;
	}

	private static string Escape(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
	}
}
