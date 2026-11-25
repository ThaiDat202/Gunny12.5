using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Bussiness;
using Bussiness.Managers;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.GameUtils;
using Game.Server.Managers;
using Game.Server.Packets;
using log4net;
using SqlDataProvider.Data;

namespace Game.Server.Farm;

/// <summary>
/// Hoạt động Sâu Bọ (farm fight activity) — bật theo khung giờ trong ServerConfig:
///   ManorActivityConfig   = "6,0|10:00-18:00|5|15"  (các thứ cho phép | khung giờ | max quái | giây hồi quái)
///   ManorActivityScoreRange = "300,800,1500,99999"  (mốc điểm)
///   ManorActivityAwardRange = "item1,item2,...|..." (vật phẩm thưởng theo mốc)
/// Quái dùng asset CDN: image/farm/fightliving/farmliving{npcType}.swf với npcType 713/714 (đã kiểm chứng 200 OK).
/// Trạng thái lưu in-memory theo vòng hoạt động ~30 phút.
/// </summary>
public static class FarmActivityMgr
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private static readonly object Sync = new object();

	private static readonly Dictionary<int, FightState> States = new Dictionary<int, FightState>();

	private static readonly int[] MasterNpcTypes = new int[] { 713, 714 };

	private static Timer _timer;

	private static bool _started;

	private class FighterPos
	{
		public int X;
		public int Y;
	}

	private class FightMaster
	{
		public int Id;
		public int NpcType;
		public int X;
		public int Y;
		public int State;
	}

	private class FightState
	{
		public int FarmId;
		public DateTime Start;
		public DateTime End;
		public DateTime LastSpawn;
		public int NextMasterId = 1;
		public bool Fighting = true;
		public readonly Dictionary<int, FighterPos> Players = new Dictionary<int, FighterPos>();
		public readonly Dictionary<int, FightMaster> Masters = new Dictionary<int, FightMaster>();
		public readonly Dictionary<int, int> Scores = new Dictionary<int, int>();
		public readonly HashSet<int> Notified = new HashSet<int>();
	}

	public static void Start()
	{
		lock (Sync)
		{
			if (_started)
			{
				return;
			}
			_started = true;
			_timer = new Timer(Tick, null, 5000, 3000);
			Log.Warn("FarmActivityMgr: started");
		}
	}

	// ------------------------------------------------------------------ config

	private static bool ParseWindow(out HashSet<int> days, out TimeSpan begin, out TimeSpan end, out int maxMasters, out int respawnSeconds)
	{
		days = new HashSet<int>();
		begin = new TimeSpan(10, 0, 0);
		end = new TimeSpan(18, 0, 0);
		maxMasters = 5;
		respawnSeconds = 15;
		string raw = ManorConfigMgr.GetServerConfig("ManorActivityConfig");
		if (string.IsNullOrEmpty(raw))
		{
			return false;
		}
		try
		{
			string[] parts = raw.Split('|');
			if (parts.Length >= 1)
			{
				foreach (string d in parts[0].Split(','))
				{
					int day;
					if (int.TryParse(d.Trim(), out day) && day >= 0 && day <= 6)
					{
						days.Add(day);
					}
				}
			}
			if (parts.Length >= 2)
			{
				string[] times = parts[1].Split('-');
				if (times.Length == 2)
				{
					TimeSpan b, e;
					if (TimeSpan.TryParse(times[0].Trim(), out b) && TimeSpan.TryParse(times[1].Trim(), out e))
					{
						begin = b;
						end = e;
					}
				}
			}
			if (parts.Length >= 3)
			{
				int m;
				if (int.TryParse(parts[2].Trim(), out m) && m > 0 && m <= 10)
				{
					maxMasters = m;
				}
			}
			if (parts.Length >= 4)
			{
				int rr;
				if (int.TryParse(parts[3].Trim(), out rr) && rr >= 5 && rr <= 120)
				{
					respawnSeconds = rr;
				}
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsInWindow()
	{
		HashSet<int> days;
		TimeSpan begin, end;
		int maxMasters, respawn;
		if (!ParseWindow(out days, out begin, out end, out maxMasters, out respawn))
		{
			return false;
		}
		DateTime now = DateTime.Now;
		if (days.Count > 0 && !days.Contains((int)now.DayOfWeek))
		{
			return false;
		}
		TimeSpan t = now.TimeOfDay;
		return t >= begin && t <= end;
	}

	// ------------------------------------------------------------------ open/close

	/// <summary>81/45 — mở hoạt động cho nông trại của chính mình.</summary>
	public static void Open(GamePlayer owner)
	{
		if (owner == null)
		{
			return;
		}
		HashSet<int> days;
		TimeSpan begin, end;
		int maxMasters, respawn;
		if (!ParseWindow(out days, out begin, out end, out maxMasters, out respawn) || !IsInWindow())
		{
			owner.Out.SendMessage(eMessageType.Normal, "Hoạt động Sâu Bọ chỉ mở trong khung giờ quy định (Thứ 7, Chủ Nhật 10:00-18:00).");
			return;
		}
		lock (Sync)
		{
			FightState existing;
			if (States.TryGetValue(owner.PlayerId, out existing) && existing.Fighting)
			{
				owner.Out.SendMessage(eMessageType.Normal, "Nông trại của bạn đang trong hoạt động Sâu Bọ.");
				return;
			}
			DateTime now = DateTime.Now;
			DateTime endTime = now.AddMinutes(30);
			DateTime windowEnd = now.Date.Add(end);
			if (windowEnd < endTime)
			{
				endTime = windowEnd;
			}
			if (endTime <= now.AddMinutes(1))
			{
				owner.Out.SendMessage(eMessageType.Normal, "Đã gần hết khung giờ hoạt động, không thể mở.");
				return;
			}
			FightState st = new FightState
			{
				FarmId = owner.PlayerId,
				Start = now,
				End = endTime,
				LastSpawn = now
			};
			for (int i = 0; i < maxMasters; i++)
			{
				SpawnMaster(st);
			}
			States[owner.PlayerId] = st;
			owner.Manor.AddLog("Mở hoạt động Sâu Bọ (kết thúc " + endTime.ToString("HH:mm") + ").");
			Broadcast46();
			owner.Out.SendMessage(eMessageType.Normal, "Đã mở hoạt động Sâu Bọ! Quái xuất hiện tại nông trại của bạn.");
		}
	}

	private static void SpawnMaster(FightState st)
	{
		Random r = new Random();
		FightMaster m = new FightMaster
		{
			Id = st.NextMasterId++,
			NpcType = MasterNpcTypes[r.Next(MasterNpcTypes.Length)],
			X = 260 + r.Next(480),
			Y = 230 + r.Next(200),
			State = 1
		};
		st.Masters[m.Id] = m;
	}

	private static void Close(FightState st)
	{
		st.Fighting = false;
		try
		{
			AwardScores(st);
		}
		catch (Exception ex)
		{
			Log.Error("FarmActivityMgr.AwardScores error farm=" + st.FarmId, ex);
		}
		Broadcast46();
	}

	private static void AwardScores(FightState st)
	{
		List<string> tiers = ParseAwardTiers();
		List<int> thresholds = ParseScoreThresholds();
		if (st.Scores.Count == 0)
		{
			return;
		}
		Random r = new Random();
		foreach (KeyValuePair<int, int> kv in st.Scores)
		{
			int score = kv.Value;
			if (score <= 0)
			{
				continue;
			}
			int tier = -1;
			for (int i = 0; i < thresholds.Count; i++)
			{
				if (score >= thresholds[i])
				{
					tier = i;
				}
			}
			if (tier < 0 || tier >= tiers.Count)
			{
				continue;
			}
			GamePlayer player = WorldMgr.GetPlayerById(kv.Key);
			if (player == null)
			{
				continue;
			}
			string[] items = tiers[tier].Split(',');
			if (items.Length == 0)
			{
				continue;
			}
			int itemId;
			if (!int.TryParse(items[r.Next(items.Length)].Trim(), out itemId) || itemId <= 0)
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
			player.SendItemsToMail(list, "Phần thưởng hoạt động Sâu Bọ", "Thưởng hoạt động Sâu Bọ (điểm: " + score + ")", eMailType.Active);
			if (score >= 1500 && player.Manor != null && player.Manor.Info != null)
			{
				player.Manor.Info.BuffRate = 10;
				player.Manor.Info.BuffDate = DateTime.Now.AddHours(2);
				ManorBussiness.SaveManor(player.Manor.Info);
				player.Out.SendMessage(eMessageType.Normal, "Nhận buff tốc độ nông trại +10% trong 2 giờ (điểm hoạt động >= 1500).");
			}
			player.Out.SendMessage(eMessageType.Normal, "Hoạt động Sâu Bọ kết thúc — bạn đạt " + score + " điểm, thưởng đã gửi vào thư.");
			try
			{
				if (player.Manor != null && player.Manor.IsLoaded)
				{
					player.Manor.AddLog("Hoạt động Sâu Bọ: đạt " + score + " điểm, nhận thưởng qua thư.");
				}
			}
			catch
			{
			}
		}
	}

	private static List<int> ParseScoreThresholds()
	{
		List<int> list = new List<int>();
		string raw = ManorConfigMgr.GetServerConfig("ManorActivityScoreRange");
		if (string.IsNullOrEmpty(raw))
		{
			list.Add(300);
			list.Add(800);
			list.Add(1500);
			return list;
		}
		foreach (string p in raw.Split(','))
		{
			int v;
			if (int.TryParse(p.Trim(), out v) && v > 0)
			{
				list.Add(v);
			}
		}
		return list;
	}

	private static List<string> ParseAwardTiers()
	{
		List<string> list = new List<string>();
		string raw = ManorConfigMgr.GetServerConfig("ManorActivityAwardRange");
		if (string.IsNullOrEmpty(raw))
		{
			return list;
		}
		foreach (string tier in raw.Split('|'))
		{
			list.Add(tier.Trim());
		}
		return list;
	}

	// ------------------------------------------------------------------ tick / notifications

	private static void Tick(object state)
	{
		try
		{
			List<int> close = new List<int>();
			List<FightState> changed = new List<FightState>();
			lock (Sync)
			{
				DateTime now = DateTime.Now;
				foreach (KeyValuePair<int, FightState> kv in States)
				{
					FightState st = kv.Value;
					if (!st.Fighting)
					{
						continue;
					}
					if (now >= st.End)
					{
						close.Add(st.FarmId);
						continue;
					}
					int maxMasters, respawn;
					HashSet<int> days;
					TimeSpan begin, end2;
					ParseWindow(out days, out begin, out end2, out maxMasters, out respawn);
					if (st.Masters.Count < maxMasters && (now - st.LastSpawn).TotalSeconds >= respawn)
					{
						SpawnMaster(st);
						st.LastSpawn = now;
						changed.Add(st);
					}
				}
				foreach (int farmId in close)
				{
					FightState st = States[farmId];
					CloseLocked(st);
					States.Remove(farmId);
				}
			}
			foreach (FightState st in changed)
			{
				PushMastersUpdate(st);
			}
		}
		catch (Exception ex)
		{
			Log.Error("FarmActivityMgr.Tick error", ex);
		}
	}

	private static void CloseLocked(FightState st)
	{
		st.Fighting = false;
		try
		{
			AwardScores(st);
		}
		catch (Exception ex)
		{
			Log.Error("FarmActivityMgr.AwardScores error farm=" + st.FarmId, ex);
		}
		TryBroadcast46();
	}

	private static void Broadcast46()
	{
		foreach (GamePlayer p in WorldMgr.GetAllPlayers())
		{
			if (p != null)
			{
				p.SendTCP(Build46());
			}
		}
	}

	public static void TryBroadcast46()
	{
		try
		{
			Broadcast46();
		}
		catch (Exception ex)
		{
			Log.Error("FarmActivityMgr.Broadcast46 error", ex);
		}
	}

	private static GSPacketIn Build46()
	{
		GSPacketIn pkg = new GSPacketIn(81);
		pkg.WriteByte(46);
		pkg.WriteBoolean(false);
		int count = 0;
		foreach (FightState st in States.Values)
		{
			if (st.Fighting)
			{
				count++;
			}
		}
		pkg.WriteInt(count);
		foreach (FightState st in States.Values)
		{
			if (!st.Fighting)
			{
				continue;
			}
			pkg.WriteInt(st.FarmId);
			pkg.WriteDateTime(st.Start);
			pkg.WriteDateTime(st.End);
			pkg.WriteBoolean(true);
		}
		return pkg;
	}

	public static void SendCurrentState(GamePlayer player)
	{
		if (player == null)
		{
			return;
		}
		player.SendTCP(Build46());
	}

	/// <summary>Khi vào farm đang trong hoạt động: thông báo 81/46 (một lần/vòng) để client biết mà bật fight.</summary>
	public static void NotifyIfFighting(GamePlayer viewer, int farmId)
	{
		if (viewer == null)
		{
			return;
		}
		lock (Sync)
		{
			FightState st;
			if (!States.TryGetValue(farmId, out st) || !st.Fighting)
			{
				return;
			}
			if (st.Notified.Contains(viewer.PlayerId))
			{
				return;
			}
			st.Notified.Add(viewer.PlayerId);
			GSPacketIn pkg = new GSPacketIn(81, viewer.PlayerId);
			pkg.WriteByte(46);
			pkg.WriteBoolean(false);
			pkg.WriteInt(1);
			pkg.WriteInt(st.FarmId);
			pkg.WriteDateTime(st.Start);
			pkg.WriteDateTime(st.End);
			pkg.WriteBoolean(true);
			viewer.SendTCP(pkg);
		}
	}

	// ------------------------------------------------------------------ scene packets

	private static List<GamePlayer> GetViewers(int farmId)
	{
		return FarmSceneTracker.GetViewers(farmId);
	}

	private static void WritePlayer(GSPacketIn pkg, GamePlayer p)
	{
		PlayerInfo info = p.PlayerCharacter;
		pkg.WriteInt(p.PlayerId);
		PlayerManor.WriteUTF(pkg, info.NickName ?? string.Empty);
		pkg.WriteInt(info.Grade);
		FighterPos pos = GetOrCreatePos(p.PlayerId);
		pkg.WriteInt(pos.X);
		pkg.WriteInt(pos.Y);
		pkg.WriteInt(0); // state
		pkg.WriteBoolean(info.Sex);
		PlayerManor.WriteUTF(pkg, info.Style ?? string.Empty);
		PlayerManor.WriteUTF(pkg, info.Colors ?? string.Empty);
		pkg.WriteBoolean(info.typeVIP > 0);
		pkg.WriteInt(info.VIPLevel);
	}

	private static FighterPos GetOrCreatePos(int playerId)
	{
		lock (Sync)
		{
			foreach (FightState st in States.Values)
			{
				FighterPos pos;
				if (st.Players.TryGetValue(playerId, out pos))
				{
					return pos;
				}
			}
		}
		return new FighterPos { X = 420, Y = 320 };
	}

	private static void WriteMasters(GSPacketIn pkg, FightState st)
	{
		pkg.WriteInt(st.Masters.Count);
		foreach (FightMaster m in st.Masters.Values)
		{
			pkg.WriteInt(m.Id);
			pkg.WriteInt(m.NpcType);
			pkg.WriteInt(m.X);
			pkg.WriteInt(m.Y);
			pkg.WriteInt(m.State);
		}
	}

	/// <summary>81/36 — trả scene hoạt động cho người vào farm.</summary>
	public static void HandleSceneRequest(GamePlayer player)
	{
		if (player == null)
		{
			return;
		}
		int farmId = FarmSceneTracker.GetFarm(player.PlayerId);
		if (farmId <= 0)
		{
			farmId = player.PlayerId;
		}
		lock (Sync)
		{
			FightState st;
			if (!States.TryGetValue(farmId, out st) || !st.Fighting)
			{
				GSPacketIn empty = new GSPacketIn(81, player.PlayerId);
				empty.WriteByte(36);
				empty.WriteInt(farmId);
				empty.WriteInt(0);
				empty.WriteInt(0);
				player.SendTCP(empty);
				return;
			}
			if (!st.Players.ContainsKey(player.PlayerId))
			{
				st.Players[player.PlayerId] = new FighterPos { X = 420, Y = 320 };
			}
			if (!st.Scores.ContainsKey(player.PlayerId))
			{
				st.Scores[player.PlayerId] = 0;
			}
			GSPacketIn pkg = new GSPacketIn(81, player.PlayerId);
			pkg.WriteByte(36);
			pkg.WriteInt(farmId);
			List<GamePlayer> viewers = GetViewers(farmId);
			pkg.WriteInt(viewers.Count);
			foreach (GamePlayer v in viewers)
			{
				WritePlayer(pkg, v);
			}
			WriteMasters(pkg, st);
			player.SendTCP(pkg);

			// thông báo những người khác có người mới vào scene (81/44)
			foreach (GamePlayer v2 in viewers)
			{
				if (v2.PlayerId == player.PlayerId)
				{
					continue;
				}
				GSPacketIn upd = new GSPacketIn(81, v2.PlayerId);
				upd.WriteByte(44);
				upd.WriteInt(farmId);
				upd.WriteInt(1);
				WritePlayer(upd, player);
				upd.WriteInt(0);
				v2.SendTCP(upd);
			}
			SendRankToViewers(st);
		}
	}

	private static void PushMastersUpdate(FightState st)
	{
		foreach (GamePlayer v in GetViewers(st.FarmId))
		{
			GSPacketIn pkg = new GSPacketIn(81, v.PlayerId);
			pkg.WriteByte(44);
			pkg.WriteInt(st.FarmId);
			pkg.WriteInt(0);
			WriteMasters(pkg, st);
			v.SendTCP(pkg);
		}
	}

	public static void Move(GamePlayer player, int x, int y)
	{
		if (player == null)
		{
			return;
		}
		int farmId = FarmSceneTracker.GetFarm(player.PlayerId);
		if (farmId <= 0)
		{
			return;
		}
		lock (Sync)
		{
			FightState st;
			if (!States.TryGetValue(farmId, out st) || !st.Fighting)
			{
				return;
			}
			st.Players[player.PlayerId] = new FighterPos { X = x, Y = y };
		}
		foreach (GamePlayer v in GetViewers(farmId))
		{
			if (v.PlayerId == player.PlayerId)
			{
				continue;
			}
			GSPacketIn pkg = new GSPacketIn(81, player.PlayerId);
			pkg.WriteByte(37);
			pkg.WriteInt(player.PlayerId);
			pkg.WriteInt(x);
			pkg.WriteInt(y);
			v.SendTCP(pkg);
		}
	}

	public static void Exit(GamePlayer player)
	{
		if (player == null)
		{
			return;
		}
		int farmId = FarmSceneTracker.GetFarm(player.PlayerId);
		FarmSceneTracker.BroadcastExit(player);
		if (farmId <= 0)
		{
			return;
		}
		lock (Sync)
		{
			FightState st;
			if (States.TryGetValue(farmId, out st))
			{
				st.Players.Remove(player.PlayerId);
			}
		}
	}

	public static void ClickMaster(GamePlayer player, int masterId)
	{
		if (player == null || masterId <= 0)
		{
			return;
		}
		int farmId = FarmSceneTracker.GetFarm(player.PlayerId);
		if (farmId <= 0)
		{
			return;
		}
		FightMaster master = null;
		lock (Sync)
		{
			FightState st;
			if (!States.TryGetValue(farmId, out st) || !st.Fighting)
			{
				return;
			}
			if (!st.Masters.TryGetValue(masterId, out master))
			{
				return;
			}
			st.Masters.Remove(masterId);
			int score;
			st.Scores.TryGetValue(player.PlayerId, out score);
			st.Scores[player.PlayerId] = score + 100;
			st.LastSpawn = DateTime.Now;
			// trả về packet 43 + 40 ngoài lock
		}
		foreach (GamePlayer v in GetViewers(farmId))
		{
			GSPacketIn remove = new GSPacketIn(81, v.PlayerId);
			remove.WriteByte(43);
			remove.WriteInt(farmId);
			remove.WriteInt(masterId);
			v.SendTCP(remove);
		}
		player.Out.SendMessage(eMessageType.Normal, "Đã tiêu diệt quái Sâu Bọ (+100 điểm).");
		try
		{
			if (player.Manor != null && player.Manor.IsLoaded)
			{
				player.Manor.AddLog("Hoạt động Sâu Bọ: tiêu diệt quái (+100 điểm).");
			}
		}
		catch
		{
		}
		lock (Sync)
		{
			FightState st;
			if (States.TryGetValue(farmId, out st))
			{
				SendRankToViewers(st);
			}
		}
	}

	public static void RequestRank(GamePlayer player)
	{
		if (player == null)
		{
			return;
		}
		int farmId = FarmSceneTracker.GetFarm(player.PlayerId);
		if (farmId <= 0)
		{
			farmId = player.PlayerId;
		}
		lock (Sync)
		{
			FightState st;
			if (States.TryGetValue(farmId, out st) && st.Fighting)
			{
				SendRank(st, player);
				return;
			}
		}
		GSPacketIn pkg = new GSPacketIn(81, player.PlayerId);
		pkg.WriteByte(40);
		pkg.WriteInt(farmId);
		pkg.WriteInt(0);
		pkg.WriteInt(0);
		player.SendTCP(pkg);
	}

	private static void SendRankToViewers(FightState st)
	{
		foreach (GamePlayer v in GetViewers(st.FarmId))
		{
			SendRank(st, v);
		}
	}

	private static void SendRank(FightState st, GamePlayer target)
	{
		List<KeyValuePair<int, int>> list = new List<KeyValuePair<int, int>>(st.Scores);
		list.Sort((a, b) => b.Value.CompareTo(a.Value));
		int myScore;
		st.Scores.TryGetValue(target.PlayerId, out myScore);
		GSPacketIn pkg = new GSPacketIn(81, target.PlayerId);
		pkg.WriteByte(40);
		pkg.WriteInt(st.FarmId);
		pkg.WriteInt(myScore);
		int count = Math.Min(list.Count, 20);
		pkg.WriteInt(count);
		for (int i = 0; i < count; i++)
		{
			GamePlayer p = WorldMgr.GetPlayerById(list[i].Key);
			string name = p != null && p.PlayerCharacter != null ? p.PlayerCharacter.NickName : ("#" + list[i].Key);
			PlayerManor.WriteUTF(pkg, name ?? string.Empty);
			pkg.WriteInt(list[i].Value);
		}
		target.SendTCP(pkg);
	}
}
