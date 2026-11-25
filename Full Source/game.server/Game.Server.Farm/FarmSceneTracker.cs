using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.GameUtils;
using Game.Server.Managers;
using log4net;

namespace Game.Server.Farm;

/// <summary>
/// Theo dõi người chơi đang xem nông trại nào (để chat nông trại 81/38, di chuyển 81/37, rời scene 81/39).
/// </summary>
public static class FarmSceneTracker
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private static readonly object Sync = new object();

	// playerId -> farmUserId (chủ nông trại đang xem)
	private static readonly Dictionary<int, int> FarmOf = new Dictionary<int, int>();

	public static void Enter(GamePlayer player, int farmUserId)
	{
		if (player == null || farmUserId <= 0)
		{
			return;
		}
		lock (Sync)
		{
			FarmOf[player.PlayerId] = farmUserId;
		}
	}

	public static void Leave(int playerId)
	{
		lock (Sync)
		{
			FarmOf.Remove(playerId);
		}
	}

	public static int GetFarm(int playerId)
	{
		lock (Sync)
		{
			int farm;
			return FarmOf.TryGetValue(playerId, out farm) ? farm : 0;
		}
	}

	/// <summary>Danh sách người chơi online đang xem cùng nông trại (kể cả chủ).</summary>
	public static List<GamePlayer> GetViewers(int farmUserId)
	{
		List<GamePlayer> result = new List<GamePlayer>();
		List<int> ids = new List<int>();
		lock (Sync)
		{
			foreach (KeyValuePair<int, int> kv in FarmOf)
			{
				if (kv.Value == farmUserId)
				{
					ids.Add(kv.Key);
				}
			}
		}
		foreach (int id in ids)
		{
			GamePlayer p = WorldMgr.GetPlayerById(id);
			if (p != null)
			{
				result.Add(p);
			}
			else
			{
				LockedRemove(id);
			}
		}
		return result;
	}

	private static void LockedRemove(int playerId)
	{
		lock (Sync)
		{
			FarmOf.Remove(playerId);
		}
	}

	/// <summary>Chat nông trại (81/38) → gửi packet 19 cho tất cả người xem cùng farm (kèm người gửi).</summary>
	public static void SendFarmChat(GamePlayer sender, byte channel, string message)
	{
		if (sender == null || sender.PlayerCharacter == null)
		{
			return;
		}
		string msg = message == null ? string.Empty : message.Trim();
		if (msg.Length == 0)
		{
			return;
		}
		if (msg.Length > 100)
		{
			msg = msg.Substring(0, 100);
		}
		int farmUserId = GetFarm(sender.PlayerId);
		if (farmUserId <= 0)
		{
			farmUserId = sender.PlayerId;
		}
		int zoneId = GetAppInt("AreaID", 1001);
		List<GamePlayer> viewers = GetViewers(farmUserId);
		foreach (GamePlayer viewer in viewers)
		{
			GSPacketIn pkg = new GSPacketIn(19, sender.PlayerId);
			pkg.WriteInt(zoneId);
			pkg.WriteByte((channel == 0) ? (byte)38 : channel);
			pkg.WriteBoolean(false);
			PlayerManor.WriteUTF(pkg, sender.PlayerCharacter.NickName ?? string.Empty);
			PlayerManor.WriteUTF(pkg, msg);
			viewer.SendTCP(pkg);
		}
	}

	public static void BroadcastMove(GamePlayer player, int x, int y)
	{
		if (player == null)
		{
			return;
		}
		int farmUserId = GetFarm(player.PlayerId);
		if (farmUserId <= 0)
		{
			return;
		}
		foreach (GamePlayer viewer in GetViewers(farmUserId))
		{
			if (viewer.PlayerId == player.PlayerId)
			{
				continue;
			}
			GSPacketIn pkg = new GSPacketIn(81, player.PlayerId);
			pkg.WriteByte(37);
			pkg.WriteInt(player.PlayerId);
			pkg.WriteInt(x);
			pkg.WriteInt(y);
			viewer.SendTCP(pkg);
		}
	}

	public static void BroadcastExit(GamePlayer player)
	{
		if (player == null)
		{
			return;
		}
		int farmUserId = GetFarm(player.PlayerId);
		Leave(player.PlayerId);
		if (farmUserId <= 0)
		{
			return;
		}
		foreach (GamePlayer viewer in GetViewers(farmUserId))
		{
			GSPacketIn pkg = new GSPacketIn(81, player.PlayerId);
			pkg.WriteByte(39);
			pkg.WriteInt(farmUserId);
			pkg.WriteInt(player.PlayerId);
			viewer.SendTCP(pkg);
		}
	}

	private static int GetAppInt(string key, int fallback)
	{
		int v;
		return int.TryParse(ConfigurationManager.AppSettings[key], out v) ? v : fallback;
	}
}
