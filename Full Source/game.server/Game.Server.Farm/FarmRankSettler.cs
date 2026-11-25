using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using Bussiness;
using Bussiness.Managers;
using Game.Server.GameObjects;
using Game.Server.Managers;
using Game.Server.Packets;
using log4net;
using SqlDataProvider.Data;

namespace Game.Server.Farm;

/// <summary>
/// Chốt thưởng BXH nông trại theo tuần (BXH Tài Phú + BXH Lao Động).
/// Config: ManorWealthsOrderReward / ManorAbilityOrderReward = "min-max,itemId|..."
/// Mỗi tuần (WeekKey = year*100 + dayOfYear/7+1) chốt 1 lần; gửi thư (hỗ trợ cả offline).
/// </summary>
public static class FarmRankSettler
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private static readonly object Sync = new object();

	private static bool _running;

	private class RankRange
	{
		public int Min;
		public int Max;
		public int ItemId;
	}

	public static void CheckAndSettle()
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
			int weekKey = CurrentWeekKey();
			if (ManorBussiness.IsManorWeekSettled(weekKey))
			{
				return;
			}
			ManorBussiness.MarkManorWeekSettled(weekKey);
			int zoneId = GetZoneId();
			int countWealth = Settle(weekKey, 1, "BXH Tài Phú", ManorBussiness.GetManorWealthRank(100), "ManorWealthsOrderReward", zoneId);
			int countAbility = Settle(weekKey, 2, "BXH Lao Động", ManorBussiness.GetManorAbilityRank(100), "ManorAbilityOrderReward", zoneId);
			NotifyAll("BXH nông trại tuần " + weekKey + " đã chốt — thưởng đã gửi qua thư (" + (countWealth + countAbility) + " người).");
			Log.Warn("FarmRankSettler: settled week " + weekKey + " wealth=" + countWealth + " ability=" + countAbility);
		}
		catch (Exception ex)
		{
			Log.Error("FarmRankSettler.CheckAndSettle error", ex);
		}
		finally
		{
			lock (Sync)
			{
				_running = false;
			}
		}
	}

	private static int Settle(int weekKey, int rankType, string title, List<UserManorRankInfo> rows, string configKey, int zoneId)
	{
		if (rows == null || rows.Count == 0)
		{
			return 0;
		}
		List<RankRange> ranges = ParseRanges(ManorConfigMgr.GetServerConfig(configKey));
		if (ranges.Count == 0)
		{
			return 0;
		}
		int sent = 0;
		for (int i = 0; i < rows.Count; i++)
		{
			UserManorRankInfo row = rows[i];
			int rank = i + 1;
			int itemId = 0;
			foreach (RankRange range in ranges)
			{
				if (rank >= range.Min && rank <= range.Max)
				{
					itemId = range.ItemId;
					break;
				}
			}
			if (itemId <= 0)
			{
				continue;
			}
			if (!ManorBussiness.AddManorRankReward(weekKey, rankType, row.UserID, rank, itemId))
			{
				continue;
			}
			ItemTemplateInfo template = ItemMgr.FindItemTemplate(itemId);
			if (template == null)
			{
				continue;
			}
			ItemInfo item = ItemInfo.CreateFromTemplate(template, 1, 104);
			if (item == null)
			{
				continue;
			}
			List<ItemInfo> list = new List<ItemInfo> { item };
			WorldEventMgr.SendItemsToMail(list, row.UserID, row.NickName ?? string.Empty, zoneId,
				"Thưởng " + title, "Hạng " + rank + " — " + title + " tuần " + weekKey + ". Chúc mừng!");
			sent++;
			GamePlayer online = WorldMgr.GetPlayerById(row.UserID);
			if (online != null)
			{
				online.Out.SendMessage(eMessageType.Normal, "Chúc mừng! Bạn đạt hạng " + rank + " " + title + ", thưởng đã gửi vào thư.");
			}
		}
		return sent;
	}

	private static List<RankRange> ParseRanges(string raw)
	{
		List<RankRange> list = new List<RankRange>();
		if (string.IsNullOrEmpty(raw))
		{
			return list;
		}
		foreach (string part in raw.Split('|'))
		{
			string seg = part.Trim();
			if (seg.Length == 0)
			{
				continue;
			}
			string[] kv = seg.Split(',');
			if (kv.Length != 2)
			{
				continue;
			}
			string[] range = kv[0].Trim().Split('-');
			int min, max, itemId;
			if (range.Length == 2 && int.TryParse(range[0].Trim(), out min) && int.TryParse(range[1].Trim(), out max) && int.TryParse(kv[1].Trim(), out itemId) && itemId > 0)
			{
				list.Add(new RankRange { Min = min, Max = max, ItemId = itemId });
			}
		}
		return list;
	}

	private static int CurrentWeekKey()
	{
		DateTime now = DateTime.Now;
		return now.Year * 100 + (now.DayOfYear / 7 + 1);
	}

	private static int GetZoneId()
	{
		int zone;
		return int.TryParse(ConfigurationManager.AppSettings["AreaID"], out zone) && zone > 0 ? zone : 1001;
	}

	private static void NotifyAll(string text)
	{
		foreach (GamePlayer p in WorldMgr.GetAllPlayers())
		{
			if (p != null)
			{
				p.Out.SendMessage(eMessageType.Normal, text);
			}
		}
	}
}
