using System;
using System.Collections.Generic;
using Bussiness;
using Bussiness.Managers;
using Game.Base.Packets;
using Game.Logic;
using Game.Server.Farm;
using Game.Server.GameObjects;
using Game.Server.Managers;
using Game.Server.Packets;
using SqlDataProvider.Data;

namespace Game.Server.GameUtils;

/// <summary>
/// Trạng thái Nông trại 2.0 của một người chơi (Farm 2.0 — client Gunny 12.6).
/// Dữ liệu lưu ở Db_Tank: Sys_User_Manor / Sys_User_ManorArea / Sys_User_ManorGoods / Sys_User_ManorWorker
/// (xem Bussiness.ManorBussiness).
///
/// Ghi chú protocol (client farm/_96$41.as):
///  - 81/24 farm info: [int userID][UTF name][int exp][int landLevel][int mineLevel][int buffRate][Date buffDate]
///      [int areaCount]{ [int type][int level][bool isPlant][int plantTempId][Date plantDate][int plantRipeDate]
///                       [int foodTempId][Date foodDate] }
///      [int workerCount]{ worker... }[int goodsCount]{ [int templateID][int property][int count] }
///  - 81/17 area update: [int type][int level][int levelTwo][bool isPlant][int plantTempId][Date plantDate]
///      [int plantRipeDate][int foodTempId][Date foodDate]
///  - 81/26 goods/self update: [int money][int value][int exp][int count]{[int templateID][int property][int count]}
///  - 81/34 pet food: [int buyExpRemainNum]
///  - 81/33 slacker query: [int count]{[int userID]}
/// </summary>
public class PlayerManor
{
	private readonly GamePlayer m_player;

	private readonly object m_lock = new object();

	private UserManorInfo m_info;

	private readonly Dictionary<int, UserManorAreaInfo> m_areas = new Dictionary<int, UserManorAreaInfo>();

	// key = ((long)property << 32) | (uint)templateId
	private readonly Dictionary<long, UserManorGoodsInfo> m_goods = new Dictionary<long, UserManorGoodsInfo>();

	private List<UserManorWorkerInfo> m_workers = new List<UserManorWorkerInfo>();

	private bool m_loaded;

	private bool m_loadError;

	// workerUserID -> lần tick làm việc gần nhất (theo phiên online)
	private readonly Dictionary<int, DateTime> m_workerTick = new Dictionary<int, DateTime>();

	private static readonly Random Rnd = new Random();

	/// <summary>AreaType (1..6) — xem farm.data.FarmAreaType của client.</summary>
	public const int AreaFarm = 1;
	public const int AreaFish = 2;
	public const int AreaMine = 3;
	public const int AreaRanch = 4;
	public const int AreaFarmLand = 5;
	public const int AreaHoeUpgrade = 6;

	/// <summary>Túi nông trại (bit 1&lt;&lt;bagType trong ManorItemTemplate.Property1).</summary>
	public const int BagFarmSeeds = 0;
	public const int BagFarmStore = 1;
	public const int BagRanchSeeds = 2;
	public const int BagRanchFodder = 3;
	public const int BagRanchStore = 4;
	public const int BagFishSeeds = 5;
	public const int BagFishFodder = 6;
	public const int BagFishStore = 7;
	public const int BagMineStore = 8;
	public const int BagMineHoe = 9;
	public const int BagFeed = 10;

	public PlayerManor(GamePlayer player)
	{
		m_player = player;
	}

	public GamePlayer Player => m_player;

	public UserManorInfo Info => m_info;

	public bool IsLoaded => m_loaded;

	public bool LoadError => m_loadError;

	// ------------------------------------------------------------------ load/save

	public void LoadFromDatabase()
	{
		if (m_loaded)
		{
			return;
		}
		lock (m_lock)
		{
			try
			{
				ManorConfigMgr.EnsureLoaded();
				int userId = m_player.PlayerId;
				UserManorInfo info = ManorBussiness.GetManor(userId);
				if (info == null)
				{
					ManorBussiness.CreateDefaultManor(userId);
					info = ManorBussiness.GetManor(userId);
				}
				if (info == null)
				{
					m_loadError = true;
					return;
				}
				m_info = info;
				if (m_info.BuyFoodCount <= 0)
				{
					m_info.BuyFoodCount = 30;
				}
				EnsureDailyPetFoodReset(false);
				EnsureShopMonth();
				ManorBussiness.SaveManor(m_info);
				m_areas.Clear();
				foreach (UserManorAreaInfo area in ManorBussiness.GetAreas(userId))
				{
					m_areas[area.AreaType] = area;
				}
				EnsureDefaultAreas(userId);
				m_goods.Clear();
				foreach (UserManorGoodsInfo goods in ManorBussiness.GetGoods(userId))
				{
					m_goods[GoodsKey(goods.TemplateId, goods.Property)] = goods;
				}
				m_workers = ManorBussiness.GetWorkers(userId) ?? new List<UserManorWorkerInfo>();
				GrantStarterKitIfNeeded(userId);
				m_loaded = true;
				ProcessGrowth(false);
			}
			catch (Exception ex)
			{
				m_loadError = true;
				Log.Error("PlayerManor.LoadFromDatabase error uid=" + m_player.PlayerId, ex);
			}
		}
	}

	private void EnsureDefaultAreas(int userId)
	{
		for (int type = AreaFarm; type <= AreaHoeUpgrade; type++)
		{
			if (!m_areas.ContainsKey(type))
			{
				UserManorAreaInfo area = new UserManorAreaInfo
				{
					UserID = userId,
					AreaType = type,
					Level = 1,
					LevelTwo = 1,
					IsPlant = false,
					PlantTempId = 0,
					PlantDate = DateTime.Now,
					RipeDate = 0,
					FoodTempId = 0,
					FoodDate = DateTime.Now,
					ProduceCount = 0,
					LastUpdate = DateTime.Now
				};
				if (ManorBussiness.SaveArea(area))
				{
					m_areas[type] = area;
				}
			}
		}
	}

	/// <summary>
	/// Tặng bộ khởi đầu một lần cho nông trại mới: 1.000 tiền nông trại + 5 hạt giống cỏ tươi (332139)
	/// để có thể test ngay vòng mua/bán/trồng/thu hoạch. Có thể bỏ bằng cách xoá dòng trong DB.
	/// </summary>
	private void GrantStarterKitIfNeeded(int userId)
	{
		if (m_goods.Count > 0 || (m_info != null && m_info.Money > 0))
		{
			return;
		}
		m_info.Money = 1000;
		ManorBussiness.SaveManor(m_info);
		ManorItemTemplate seed = ManorConfigMgr.GetItem(332139);
		if (seed != null)
		{
			AddGoods(seed.TemplateID, seed.Property1, 5);
		}
	}

	public void SaveToDatabase()
	{
		if (!m_loaded)
		{
			return;
		}
		lock (m_lock)
		{
			try
			{
				if (m_info != null)
				{
					ManorBussiness.SaveManor(m_info);
				}
				foreach (UserManorAreaInfo area in m_areas.Values)
				{
					ManorBussiness.SaveArea(area);
				}
				foreach (UserManorGoodsInfo goods in m_goods.Values)
				{
					ManorBussiness.SaveGoods(goods);
				}
			}
			catch (Exception ex)
			{
				Log.Error("PlayerManor.SaveToDatabase error uid=" + m_player.PlayerId, ex);
			}
		}
	}

	private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(PlayerManor));

	// ------------------------------------------------------------------ areas

	public UserManorAreaInfo GetArea(int areaType)
	{
		lock (m_lock)
		{
			UserManorAreaInfo area;
			m_areas.TryGetValue(areaType, out area);
			return area;
		}
	}

	public List<UserManorAreaInfo> GetAreas()
	{
		lock (m_lock)
		{
			return new List<UserManorAreaInfo>(m_areas.Values);
		}
	}

	public bool SaveArea(UserManorAreaInfo area)
	{
		if (area == null)
		{
			return false;
		}
		lock (m_lock)
		{
			area.LastUpdate = DateTime.Now;
			bool ok = ManorBussiness.SaveArea(area);
			if (ok)
			{
				m_areas[area.AreaType] = area;
			}
			return ok;
		}
	}

	public int GetLandLevel()
	{
		UserManorAreaInfo farm = GetArea(AreaFarm);
		return farm == null ? 1 : Math.Max(1, farm.LevelTwo);
	}

	public int GetMineLevel()
	{
		UserManorAreaInfo mine = GetArea(AreaMine);
		return mine == null ? 1 : Math.Max(1, mine.LevelTwo);
	}

	// ------------------------------------------------------------------ goods

	private static long GoodsKey(int templateId, int property)
	{
		return ((long)property << 32) | (uint)templateId;
	}

	public int GetGoodsCount(int templateId, int property = 0)
	{
		lock (m_lock)
		{
			UserManorGoodsInfo goods;
			return m_goods.TryGetValue(GoodsKey(templateId, property), out goods) ? goods.Count : 0;
		}
	}

	public List<UserManorGoodsInfo> GetGoods()
	{
		lock (m_lock)
		{
			return new List<UserManorGoodsInfo>(m_goods.Values);
		}
	}

	public bool AddGoods(int templateId, int property, int count)
	{
		if (count <= 0)
		{
			return false;
		}
		lock (m_lock)
		{
			long key = GoodsKey(templateId, property);
			UserManorGoodsInfo goods;
			if (m_goods.TryGetValue(key, out goods))
			{
				goods.Count += count;
			}
			else
			{
				goods = new UserManorGoodsInfo
				{
					UserID = m_player.PlayerId,
					TemplateId = templateId,
					Property = property,
					Count = count
				};
				m_goods[key] = goods;
			}
			if (!ManorBussiness.SaveGoods(goods))
			{
				return false;
			}
			return true;
		}
	}

	public bool RemoveGoods(int templateId, int property, int count)
	{
		if (count <= 0)
		{
			return false;
		}
		lock (m_lock)
		{
			long key = GoodsKey(templateId, property);
			UserManorGoodsInfo goods;
			if (!m_goods.TryGetValue(key, out goods) || goods.Count < count)
			{
				return false;
			}
			goods.Count -= count;
			if (goods.Count <= 0)
			{
				m_goods.Remove(key);
				return ManorBussiness.DeleteGoods(goods.ID);
			}
			return ManorBussiness.SaveGoods(goods);
		}
	}

	// ------------------------------------------------------------------ economy

	public bool TrySpendMoney(int amount)
	{
		if (amount <= 0 || m_info == null)
		{
			return amount <= 0;
		}
		lock (m_lock)
		{
			if (m_info.Money < amount)
			{
				return false;
			}
			m_info.Money -= amount;
			return ManorBussiness.SaveManor(m_info);
		}
	}

	public bool AddMoney(int amount)
	{
		if (amount == 0 || m_info == null)
		{
			return amount == 0;
		}
		lock (m_lock)
		{
			m_info.Money = Math.Max(0, m_info.Money + amount);
			m_info.Value = Math.Max(0, m_info.Value + amount);
			return ManorBussiness.SaveManor(m_info);
		}
	}

	public bool AddFarmExp(int exp)
	{
		if (exp == 0 || m_info == null)
		{
			return false;
		}
		lock (m_lock)
		{
			m_info.Exp = Math.Max(0, m_info.Exp + exp);
			return ManorBussiness.SaveManor(m_info);
		}
	}

	// ------------------------------------------------------------------ packet helpers

	/// <summary>UTF string theo định dạng client: [short length][utf8 bytes].</summary>
	public static void WriteUTF(GSPacketIn pkg, string value)
	{
		byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty);
		pkg.WriteShort((short)bytes.Length);
		pkg.Write(bytes, 0, bytes.Length);
	}

	/// <summary>Gửi 81/17 — cập nhật một khu nông trại.</summary>
	public void SendUpdateArea(UserManorAreaInfo area)
	{
		if (area == null)
		{
			return;
		}
		GSPacketIn pkg = new GSPacketIn(81, m_player.PlayerId);
		pkg.WriteByte(17);
		pkg.WriteInt(area.AreaType);
		pkg.WriteInt(area.Level);
		pkg.WriteInt(area.LevelTwo);
		pkg.WriteBoolean(area.IsPlant);
		pkg.WriteInt(area.PlantTempId);
		pkg.WriteDateTime(area.PlantDate);
		pkg.WriteInt(area.RipeDate);
		pkg.WriteInt(area.FoodTempId);
		pkg.WriteDateTime(area.FoodDate);
		m_player.SendTCP(pkg);
	}

	/// <summary>Gửi 81/26 — tiền/giá trị/exp + toàn bộ túi nông sản của chính mình.</summary>
	public void SendUpdateGoods()
	{
		if (m_info == null)
		{
			return;
		}
		GSPacketIn pkg = new GSPacketIn(81, m_player.PlayerId);
		pkg.WriteByte(26);
		pkg.WriteInt(m_info.Money);
		pkg.WriteInt(m_info.Value);
		pkg.WriteInt(m_info.Exp);
		List<UserManorGoodsInfo> list = GetGoods();
		pkg.WriteInt(list.Count);
		foreach (UserManorGoodsInfo goods in list)
		{
			pkg.WriteInt(goods.TemplateId);
			pkg.WriteInt(goods.Property);
			pkg.WriteInt(goods.Count);
		}
		m_player.SendTCP(pkg);
	}

	/// <summary>Gửi 81/34 — số lượt mua thức ăn pet còn lại.</summary>
	public void SendPetFoodCount()
	{
		int remain = m_info == null ? 20 : m_info.BuyFoodCount;
		GSPacketIn pkg = new GSPacketIn(81, m_player.PlayerId);
		pkg.WriteByte(34);
		pkg.WriteInt(remain);
		m_player.SendTCP(pkg);
	}

	/// <summary>Gửi 81/33 — danh sách bạn bè đang có thợ "lười" (slacker). Phase worker sẽ dùng.</summary>
	public void SendSlackerQuery(List<int> slackerUserIds)
	{
		GSPacketIn pkg = new GSPacketIn(81, m_player.PlayerId);
		pkg.WriteByte(33);
		if (slackerUserIds == null)
		{
			pkg.WriteInt(0);
		}
		else
		{
			pkg.WriteInt(slackerUserIds.Count);
			foreach (int id in slackerUserIds)
			{
				pkg.WriteInt(id);
			}
		}
		m_player.SendTCP(pkg);
	}

	/// <summary>Gửi farm info (81/24) của nông trại ownerId cho người chơi hiện tại.</summary>
	public static void SendManorInfo(GamePlayer viewer, int ownerId, PlayerManor selfManor)
	{
		bool isSelf = viewer.PlayerId == ownerId;
		UserManorInfo info = null;
		List<UserManorAreaInfo> areas = null;
		List<UserManorWorkerInfo> workers = null;
		List<UserManorGoodsInfo> goods = null;
		string name = string.Empty;

		if (isSelf && selfManor != null && selfManor.m_loaded)
		{
			info = selfManor.m_info;
			areas = selfManor.GetAreas();
			workers = new List<UserManorWorkerInfo>(selfManor.m_workers);
			goods = selfManor.GetGoods();
			name = viewer.PlayerCharacter.NickName;
		}
		else
		{
			ManorBussiness.CreateDefaultManor(ownerId);
			info = ManorBussiness.GetManor(ownerId);
			areas = ManorBussiness.GetAreas(ownerId);
			workers = ManorBussiness.GetWorkers(ownerId) ?? new List<UserManorWorkerInfo>();
			goods = new List<UserManorGoodsInfo>();
			GamePlayer online = WorldMgr.GetPlayerById(ownerId);
			if (online != null)
			{
				name = online.PlayerCharacter.NickName;
			}
		}
		if (info == null)
		{
			info = new UserManorInfo { Exp = 0, Money = 0, Value = 0, BuffRate = 0, BuffDate = DateTime.Now, BuyFoodCount = 20 };
		}
		if (areas == null)
		{
			areas = new List<UserManorAreaInfo>();
		}

		int landLevel = 1;
		int mineLevel = 1;
		foreach (UserManorAreaInfo area in areas)
		{
			if (area.AreaType == AreaFarm)
			{
				landLevel = Math.Max(1, area.LevelTwo);
			}
			else if (area.AreaType == AreaMine)
			{
				mineLevel = Math.Max(1, area.LevelTwo);
			}
		}

		GSPacketIn pkg = new GSPacketIn(81, viewer.PlayerId);
		pkg.WriteByte(24);
		pkg.WriteInt(ownerId);
		WriteUTF(pkg, name);
		pkg.WriteInt(info.Exp);
		pkg.WriteInt(landLevel);
		pkg.WriteInt(mineLevel);
		pkg.WriteInt(info.BuffRate);
		pkg.WriteDateTime(info.BuffDate);

		pkg.WriteInt(areas.Count);
		foreach (UserManorAreaInfo area in areas)
		{
			pkg.WriteInt(area.AreaType);
			pkg.WriteInt(area.Level);
			pkg.WriteBoolean(area.IsPlant);
			pkg.WriteInt(area.PlantTempId);
			pkg.WriteDateTime(area.PlantDate);
			pkg.WriteInt(area.RipeDate);
			pkg.WriteInt(area.FoodTempId);
			pkg.WriteDateTime(area.FoodDate);
		}

		if (workers == null)
		{
			workers = new List<UserManorWorkerInfo>();
		}
		pkg.WriteInt(workers.Count);
		foreach (UserManorWorkerInfo worker in workers)
		{
			pkg.WriteInt(worker.WorkerUserID);
			WriteUTF(pkg, worker.Name);
			WriteUTF(pkg, worker.Title);
			pkg.WriteInt(worker.Feeling);
			pkg.WriteInt(worker.Energy);
			pkg.WriteInt(worker.FarmExp);
			pkg.WriteInt(worker.FishExp);
			pkg.WriteInt(worker.MineExp);
			pkg.WriteInt(worker.RanchExp);
			pkg.WriteBoolean(worker.IsWorking);
			pkg.WriteInt(worker.WorkType);
			pkg.WriteDateTime(worker.WorkBeginDate);
			pkg.WriteDateTime(worker.EmployProtectDate);
			pkg.WriteInt(worker.ProtectPrice);
			pkg.WriteBoolean(worker.Sex);
			WriteUTF(pkg, worker.Colors);
			WriteUTF(pkg, worker.Style);
		}

		if (goods == null)
		{
			goods = new List<UserManorGoodsInfo>();
		}
		pkg.WriteInt(goods.Count);
		foreach (UserManorGoodsInfo g in goods)
		{
			pkg.WriteInt(g.TemplateId);
			pkg.WriteInt(g.Property);
			pkg.WriteInt(g.Count);
		}
		viewer.SendTCP(pkg);
	}

	/// <summary>Gửi 81/1 — bảng Quản lý nông trại: info bản thân (đang làm thợ cho ai / tự bảo vệ) + log.</summary>
	public void SendManageInfo()
	{
		DateTime now = DateTime.Now;
		UserManorWorkerInfo own = ManorBussiness.GetWorkerByWorkerUserID(m_player.PlayerId);
		int grade = GetFarmLevelGrade();
		int skillGrade = 0;
		int farmExp = 0;
		int fishExp = 0;
		int mineExp = 0;
		int ranchExp = 0;
		if (own != null)
		{
			farmExp = own.FarmExp;
			fishExp = own.FishExp;
			mineExp = own.MineExp;
			ranchExp = own.RanchExp;
			skillGrade = GradeByCareerExp(ManorConfigMgr.LevelTypeFarmSkill, own.FarmExp)
				+ GradeByCareerExp(ManorConfigMgr.LevelTypeFishSkill, own.FishExp)
				+ GradeByCareerExp(ManorConfigMgr.LevelTypeMineSkill, own.MineExp)
				+ GradeByCareerExp(ManorConfigMgr.LevelTypeRanchSkill, own.RanchExp);
		}
		GSPacketIn pkg = new GSPacketIn(81, m_player.PlayerId);
		pkg.WriteByte(1);
		WriteUTF(pkg, own == null ? string.Empty : own.Title);
		pkg.WriteBoolean(own != null); // isEmploy
		WriteSafeDate(pkg, own != null ? own.EmployProtectDate : now);
		pkg.WriteInt(0);
		bool isProtect = m_info != null && m_info.ProtectDate > now;
		pkg.WriteBoolean(isProtect);
		WriteSafeDate(pkg, (m_info != null && m_info.ProtectDate.Year >= 1900) ? m_info.ProtectDate : now);
		pkg.WriteInt(0);
		pkg.WriteInt(ManorConfigMgr.GetRansomPrice(grade));
		pkg.WriteInt(ManorConfigMgr.GetAddTimePrice(grade));
		pkg.WriteInt(skillGrade);
		pkg.WriteInt(farmExp);
		pkg.WriteInt(fishExp);
		pkg.WriteInt(mineExp);
		pkg.WriteInt(ranchExp);
		pkg.WriteInt(own == null ? 0 : own.OwnerID); // bossID
		if (own != null && own.OwnerID > 0)
		{
			WriteUTF(pkg, ManorBussiness.GetUserNickName(own.OwnerID) ?? string.Empty); // bossName
			WriteUTF(pkg, string.Empty); // bossTitle
			pkg.WriteInt(0); // bossExp
			pkg.WriteInt(0); // bossValue
			pkg.WriteInt(0); // bossMoney
			pkg.WriteBoolean(false); // bossSex
			WriteUTF(pkg, string.Empty); // bossColor
			WriteUTF(pkg, string.Empty); // bossStyle
		}
		List<UserManorLogInfo> logs = ManorBussiness.GetManorLogs(m_player.PlayerId, 20);
		pkg.WriteInt(logs.Count);
		foreach (UserManorLogInfo log in logs)
		{
			pkg.WriteInt(0);
			WriteUTF(pkg, log.Content ?? string.Empty);
		}
		m_player.SendTCP(pkg);
	}

	// ------------------------------------------------------------------ worker helpers (phase worker)

	public List<UserManorWorkerInfo> GetWorkers()
	{
		lock (m_lock)
		{
			return new List<UserManorWorkerInfo>(m_workers);
		}
	}

	public UserManorWorkerInfo GetWorker(int workerUserId)
	{
		lock (m_lock)
		{
			foreach (UserManorWorkerInfo w in m_workers)
			{
				if (w.WorkerUserID == workerUserId)
				{
					return w;
				}
			}
			return null;
		}
	}

	public bool SaveWorker(UserManorWorkerInfo worker)
	{
		if (worker == null)
		{
			return false;
		}
		lock (m_lock)
		{
			if (!ManorBussiness.SaveWorker(worker))
			{
				return false;
			}
			UserManorWorkerInfo existing = GetWorker(worker.WorkerUserID);
			if (existing == null)
			{
				m_workers.Add(worker);
			}
			return true;
		}
	}

	public bool DeleteWorker(int workerUserId)
	{
		lock (m_lock)
		{
			UserManorWorkerInfo existing = GetWorker(workerUserId);
			if (existing != null)
			{
				m_workers.Remove(existing);
				return ManorBussiness.DeleteWorker(m_player.PlayerId, workerUserId);
			}
			return false;
		}
	}

	// ================================================================== gameplay (Farm 2.0)

	/// <summary>Túi nông trại tương ứng với khu (1 farm, 2 fish, 3 mine, 4 ranch).</summary>
	public static int BagTypeForArea(int areaType)
	{
		switch (areaType)
		{
		case AreaFarm:
			return BagFarmSeeds;
		case AreaFish:
			return BagFishSeeds;
		case AreaMine:
			return BagMineHoe;
		case AreaRanch:
			return BagRanchSeeds;
		default:
			return -1;
		}
	}

	private int GetAreaSpeed(int areaType)
	{
		UserManorAreaInfo area = GetArea(areaType);
		int grade = area == null ? 1 : Math.Max(1, area.Level);
		ManorUpgradeTemplate conf = ManorConfigMgr.GetUpgrade(areaType, grade);
		int speed = (conf != null && conf.PlantSpeedValue > 0) ? conf.PlantSpeedValue : 1000;
		if (m_info != null && m_info.BuffRate > 0 && m_info.BuffDate > DateTime.Now)
		{
			long adjusted = (long)speed * 100 / (100 + m_info.BuffRate);
			if (adjusted > 0)
			{
				speed = (int)adjusted;
			}
		}
		return speed;
	}

	private int GetFarmLevelGrade()
	{
		return ManorConfigMgr.GetGradeByExp(ManorConfigMgr.LevelTypeFarm, m_info == null ? 0 : m_info.Exp);
	}

	/// <summary>
	/// Xử lý tăng trưởng (lazy): cây chín -> tự thu hoạch; mỏ -> tự khai thác theo chu kỳ;
	/// cá/gia súc -> tự cho ăn mỗi 30 phút khi có foodTempId, đủ điểm -> thu hoạch.
	/// Gọi ở mọi packet farm và khi mở nông trại (kể cả lúc offline đã chín).
	/// </summary>
	public void ProcessGrowth(bool sendUpdates)
	{
		if (!m_loaded)
		{
			return;
		}
		List<UserManorAreaInfo> changed = new List<UserManorAreaInfo>();
		bool goodsChanged = false;
		DateTime now = DateTime.Now;
		try
		{
			ProcessWorkerTick(now, sendUpdates);
		}
		catch (Exception ex2)
		{
			Log.Error("PlayerManor.ProcessWorkerTick error uid=" + m_player.PlayerId, ex2);
		}
		foreach (UserManorAreaInfo area in GetAreas())
		{
			bool changedOne = false;
			try
			{
				switch (area.AreaType)
				{
				case AreaFarm:
					changedOne = ProcessFarmGrowth(area, now);
					break;
				case AreaMine:
					changedOne = ProcessMineGrowth(area, now);
					break;
				case AreaFish:
				case AreaRanch:
					changedOne = ProcessAnimalGrowth(area, now);
					break;
				}
			}
			catch (Exception ex)
			{
				Log.Error("PlayerManor.ProcessGrowth error uid=" + m_player.PlayerId + " type=" + area.AreaType, ex);
			}
			if (changedOne)
			{
				changed.Add(area);
				goodsChanged = true;
			}
		}
		if (changed.Count == 0)
		{
			return;
		}
		if (!sendUpdates)
		{
			return;
		}
		foreach (UserManorAreaInfo area2 in changed)
		{
			SendUpdateArea(area2);
		}
		if (goodsChanged)
		{
			SendUpdateGoods();
		}
	}

	private bool ProcessFarmGrowth(UserManorAreaInfo area, DateTime now)
	{
		if (!area.IsPlant || area.PlantTempId <= 0)
		{
			return false;
		}
		ManorItemTemplate seed = ManorConfigMgr.GetItem(area.PlantTempId);
		if (seed == null)
		{
			ResetArea(area, now);
			SaveArea(area);
			return true;
		}
		if (area.RipeDate <= 0)
		{
			return false;
		}
		DateTime ripeAt = area.PlantDate.AddSeconds(area.RipeDate);
		if (now < ripeAt)
		{
			return false;
		}
		ManorItemTemplate product = ManorConfigMgr.GetItem(seed.Property4);
		if (product != null && seed.Property2 > 0)
		{
			AddGoods(product.TemplateID, product.Property1, seed.Property2);
		}
		if (seed.Property6 > 0)
		{
			AddFarmExp(seed.Property6);
		}
		if (seed.Property7 > 0)
		{
			AddWorkerHarvestExp(AreaFarm, seed.Property7);
		}
		AddLog("Thu hoạch " + (product != null ? product.Name : "nông sản") + " x" + seed.Property2 + " (+" + seed.Property6 + " EXP).");
		ResetArea(area, now);
		SaveArea(area);
		return true;
	}

	private bool ProcessMineGrowth(UserManorAreaInfo area, DateTime now)
	{
		if (!area.IsPlant || area.PlantTempId <= 0)
		{
			return false;
		}
		ManorItemTemplate hoe = ManorConfigMgr.GetItem(area.PlantTempId);
		if (hoe == null)
		{
			ResetArea(area, now);
			SaveArea(area);
			return true;
		}
		if (area.FoodDate == DateTime.MinValue)
		{
			area.FoodDate = area.PlantDate;
		}
		int interval = hoe.Property3 * 60 * GetAreaSpeed(AreaMine) / 1000;
		if (interval <= 0)
		{
			interval = 3600;
		}
		double elapsed = (now - area.FoodDate).TotalSeconds;
		int produce = (int)(elapsed / interval);
		if (produce <= 0)
		{
			return false;
		}
		if (produce > 200)
		{
			produce = 200;
		}
		List<ManorItemTemplate> ores = new List<ManorItemTemplate>();
		foreach (ManorItemTemplate item in ManorConfigMgr.GetItemsByBag(BagMineStore))
		{
			if (item.Property3 > 0)
			{
				ores.Add(item);
			}
		}
		if (ores.Count == 0)
		{
			ores = ManorConfigMgr.GetItemsByBag(BagMineStore);
		}
		Random random = new Random();
		for (int i = 0; i < produce; i++)
		{
			if (ores.Count > 0)
			{
				ManorItemTemplate ore = ores[random.Next(ores.Count)];
				AddGoods(ore.TemplateID, ore.Property1, 1);
			}
			if (hoe.Property6 > 0)
			{
				AddFarmExp(hoe.Property6);
			}
		}
		area.FoodDate = area.FoodDate.AddSeconds((long)interval * produce);
		SaveArea(area);
		AddLog("Khai thác hầm mỏ: nhận " + produce + " quặng (+" + (hoe.Property6 * produce) + " EXP).");
		if (hoe.Property7 > 0)
		{
			AddWorkerHarvestExp(AreaMine, hoe.Property7 * produce);
		}
		return true;
	}

	private bool ProcessAnimalGrowth(UserManorAreaInfo area, DateTime now)
	{
		if (!area.IsPlant || area.PlantTempId <= 0)
		{
			return false;
		}
		ManorItemTemplate young = ManorConfigMgr.GetItem(area.PlantTempId);
		if (young == null)
		{
			ResetArea(area, now);
			SaveArea(area);
			return true;
		}
		if (area.FoodDate == DateTime.MinValue)
		{
			area.FoodDate = area.PlantDate;
		}
		bool changed = false;
		while (now - area.FoodDate >= TimeSpan.FromMinutes(30.0))
		{
			if (area.FoodTempId <= 0)
			{
				area.FoodDate = now;
				SaveArea(area);
				return changed;
			}
			ManorItemTemplate fodder = ManorConfigMgr.GetItem(area.FoodTempId);
			if (fodder == null || GetGoodsCount(fodder.TemplateID, fodder.Property1) <= 0)
			{
				area.FoodDate = now;
				SaveArea(area);
				return changed;
			}
			RemoveGoods(fodder.TemplateID, fodder.Property1, 1);
			area.ProduceCount += fodder.Property4;
			area.FoodDate = area.FoodDate.AddMinutes(30.0);
			changed = true;
			if (area.ProduceCount >= young.Property3)
			{
				break;
			}
		}
		if (area.ProduceCount >= young.Property3)
		{
			ManorItemTemplate adult = ManorConfigMgr.GetItem(young.Property4);
			if (adult != null && young.Property2 > 0)
			{
				AddGoods(adult.TemplateID, adult.Property1, young.Property2);
			}
			if (young.Property6 > 0)
			{
				AddFarmExp(young.Property6);
			}
			if (young.Property7 > 0)
			{
				AddWorkerHarvestExp(AreaFish == area.AreaType ? AreaFish : AreaRanch, young.Property7);
			}
			AddLog("Thu hoạch " + (adult != null ? adult.Name : "vật nuôi") + " x" + young.Property2 + ".");
			ResetArea(area, now);
			changed = true;
		}
		SaveArea(area);
		return changed;
	}

	private static void ResetArea(UserManorAreaInfo area, DateTime now)
	{
		area.IsPlant = false;
		area.PlantTempId = 0;
		area.PlantDate = now;
		area.RipeDate = 0;
		area.FoodTempId = 0;
		area.FoodDate = now;
		area.ProduceCount = 0;
	}

	/// <summary>81/11 — gieo hạt/khai thác (areaType 1 farm / 2 fish / 3 mine / 4 ranch, templateId 0 = dừng).</summary>
	public bool PlantBegin(int areaType, int templateId)
	{
		if (!m_loaded || m_info == null)
		{
			return false;
		}
		ProcessGrowth(true);
		UserManorAreaInfo area = GetArea(areaType);
		int bag = BagTypeForArea(areaType);
		if (area == null || bag < 0)
		{
			return false;
		}
		DateTime now = DateTime.Now;
		if (templateId == 0)
		{
			ResetArea(area, now);
			SaveArea(area);
			SendUpdateArea(area);
			return true;
		}
		ManorItemTemplate item = ManorConfigMgr.GetItem(templateId);
		if (item == null || !item.BelongsToBag(bag))
		{
			return false;
		}
		if (item.Property2 > 0 && GetFarmLevelGrade() < item.Property5)
		{
			return false;
		}
		if (GetGoodsCount(item.TemplateID, item.Property1) <= 0)
		{
			return false;
		}
		if (!RemoveGoods(item.TemplateID, item.Property1, 1))
		{
			return false;
		}
		area.IsPlant = true;
		area.PlantTempId = templateId;
		area.PlantDate = now;
		area.FoodDate = now;
		area.FoodTempId = 0;
		area.ProduceCount = 0;
		if (areaType == AreaFarm)
		{
			area.RipeDate = item.Property3 * 60 * GetAreaSpeed(AreaFarm) / 1000;
		}
		else if (areaType == AreaMine)
		{
			area.RipeDate = item.Property3 * 60 * GetAreaSpeed(AreaMine) / 1000;
		}
		else
		{
			area.RipeDate = item.Property3;
		}
		SaveArea(area);
		SendUpdateArea(area);
		SendUpdateGoods();
		AddLog("Đã gieo " + item.Name + " ở khu " + areaType + ".");
		return true;
	}

	/// <summary>81/28 — đặt/huỷ thức ăn tự động (cá: túi 6, gia súc: túi 3).</summary>
	public bool SetFodder(int areaType, int templateId)
	{
		if (!m_loaded)
		{
			return false;
		}
		ProcessGrowth(true);
		UserManorAreaInfo area = GetArea(areaType);
		if (area == null)
		{
			return false;
		}
		if (areaType != AreaFish && areaType != AreaRanch)
		{
			return true;
		}
		if (templateId == 0)
		{
			area.FoodTempId = 0;
			area.FoodDate = DateTime.Now;
			SaveArea(area);
			SendUpdateArea(area);
			return true;
		}
		int bag = areaType == AreaFish ? BagFishFodder : BagRanchFodder;
		ManorItemTemplate item = ManorConfigMgr.GetItem(templateId);
		if (item == null || !item.BelongsToBag(bag))
		{
			return false;
		}
		area.FoodTempId = templateId;
		area.FoodDate = DateTime.Now;
		SaveArea(area);
		SendUpdateArea(area);
		return true;
	}

	/// <summary>81/14 — nâng cấp khu (1 farm, 2 fish, 3 mine, 4 ranch, 5 đất, 6 cuốc).</summary>
	public bool UpgradeArea(int upgradeType)
	{
		if (!m_loaded || m_info == null)
		{
			return false;
		}
		ProcessGrowth(true);
		UserManorAreaInfo area;
		int currentGrade;
		if (upgradeType >= AreaFarm && upgradeType <= AreaRanch)
		{
			area = GetArea(upgradeType);
			if (area == null)
			{
				return false;
			}
			currentGrade = area.Level;
		}
		else if (upgradeType == AreaFarmLand)
		{
			area = GetArea(AreaFarm);
			currentGrade = area == null ? 0 : area.LevelTwo;
		}
		else if (upgradeType == AreaHoeUpgrade)
		{
			area = GetArea(AreaMine);
			currentGrade = area == null ? 0 : area.LevelTwo;
		}
		else
		{
			return false;
		}
		ManorUpgradeTemplate conf = ManorConfigMgr.GetUpgrade(upgradeType, currentGrade + 1);
		if (conf == null)
		{
			return false;
		}
		if (upgradeType >= AreaFarm && upgradeType <= AreaRanch)
		{
			if (GetFarmLevelGrade() < conf.NeedGrade1)
			{
				return false;
			}
			if (conf.NeedGrade2 > 0 && currentGrade < conf.NeedGrade2)
			{
				return false;
			}
		}
		else
		{
			if (currentGrade != conf.NeedGrade2 - 1)
			{
				return false;
			}
			UserManorAreaInfo baseArea = upgradeType == AreaFarmLand ? GetArea(AreaFarm) : GetArea(AreaMine);
			if (baseArea == null || baseArea.Level < conf.NeedGrade2)
			{
				return false;
			}
		}
		ManorItemTemplate need1 = conf.NeedItem1 > 0 ? ManorConfigMgr.GetItem(conf.NeedItem1) : null;
		ManorItemTemplate need2 = conf.NeedItem2 > 0 ? ManorConfigMgr.GetItem(conf.NeedItem2) : null;
		if (conf.NeedItem1 > 0 && conf.NeedItemCount1 > 0)
		{
			if (need1 == null || GetGoodsCount(conf.NeedItem1, need1.Property1) < conf.NeedItemCount1)
			{
				return false;
			}
		}
		if (conf.NeedItem2 > 0 && conf.NeedItemCount2 > 0)
		{
			if (need2 == null || GetGoodsCount(conf.NeedItem2, need2.Property1) < conf.NeedItemCount2)
			{
				return false;
			}
		}
		if (need1 != null && conf.NeedItemCount1 > 0)
		{
			RemoveGoods(need1.TemplateID, need1.Property1, conf.NeedItemCount1);
		}
		if (need2 != null && conf.NeedItemCount2 > 0)
		{
			RemoveGoods(need2.TemplateID, need2.Property1, conf.NeedItemCount2);
		}
		if (upgradeType >= AreaFarm && upgradeType <= AreaRanch)
		{
			area.Level = currentGrade + 1;
		}
		else
		{
			area.LevelTwo = currentGrade + 1;
		}
		SaveArea(area);
		SendUpdateArea(area);
		SendUpdateGoods();
		AddLog("Nâng cấp khu " + upgradeType + " lên Lv" + (upgradeType >= AreaFarm && upgradeType <= AreaRanch ? area.Level : area.LevelTwo) + ".");
		return true;
	}

	/// <summary>81/31 — bán nông sản lấy tiền nông trại (giá = ReclaimValue).</summary>
	public bool SellGoods(int templateId, int count)
	{
		if (!m_loaded || m_info == null || count <= 0)
		{
			return false;
		}
		ProcessGrowth(true);
		int property;
		int owned = GetGoodsCountAnyProperty(templateId, out property);
		if (owned <= 0 || owned < count)
		{
			m_player.Out.SendMessage(eMessageType.Normal, "Bạn không đủ số lượng nông sản để bán.");
			return false;
		}
		int price = 0;
		ManorItemTemplate item = ManorConfigMgr.GetItem(templateId);
		if (item != null)
		{
			price = item.ReclaimValue;
			property = item.Property1;
		}
		if (price <= 0)
		{
			ItemTemplateInfo template = ItemMgr.FindItemTemplate(templateId);
			if (template != null)
			{
				price = template.ReclaimValue;
			}
		}
		if (price <= 0)
		{
			m_player.Out.SendMessage(eMessageType.Normal, "Vật phẩm này không thể bán.");
			return false;
		}
		if (!RemoveGoods(templateId, property, count))
		{
			m_player.Out.SendMessage(eMessageType.Normal, "Không thể bán vật phẩm này, vui lòng thử lại.");
			return false;
		}
		long total = (long)price * count;
		AddMoney((int)Math.Min(int.MaxValue, total));
		SendUpdateGoods();
		string soldName = item != null ? item.Name : ("#" + templateId);
		m_player.Out.SendMessage(eMessageType.Normal, "Đã bán " + count + " vật phẩm, nhận " + total + " Cỏ Vàng.");
		AddLog("Bán " + count + " " + soldName + " được " + total + " Cỏ Vàng.");
		return true;
	}

	private int GetGoodsCountAnyProperty(int templateId, out int property)
	{
		property = 0;
		int total = 0;
		lock (m_lock)
		{
			foreach (KeyValuePair<long, UserManorGoodsInfo> kv in m_goods)
			{
				if ((int)(kv.Key & 0xFFFFFFFFL) != templateId)
				{
					continue;
				}
				total += kv.Value.Count;
				property = kv.Value.Property;
			}
		}
		return total;
	}

	/// <summary>81/15 — mua vật phẩm nông trại từ shop 135 (moneyType 1=farm money, 2=vé/DDTMoney, 3=xu).</summary>
	public bool BuyGoods(int goodsId, int count, int moneyType)
	{
		if (!m_loaded || m_info == null || count <= 0 || count > 999)
		{
			return false;
		}
		ProcessGrowth(true);
		ShopItemInfo shop = ManorConfigMgr.GetFarmShopItemByGoodsId(goodsId);
		if (shop == null)
		{
			return false;
		}
		int templateId = shop.TemplateID;
		int price = moneyType == 1 ? shop.AValue1 : (moneyType == 2 ? shop.AValue2 : shop.AValue3);
		if (price <= 0)
		{
			return false;
		}
		long total = (long)price * count;
		if (total > int.MaxValue)
		{
			return false;
		}
		bool paid = false;
		if (moneyType == 1)
		{
			paid = TrySpendMoney((int)total);
		}
		else if (moneyType == 2)
		{
			if (m_player.PlayerCharacter.DDTMoney >= (int)total)
			{
				m_player.RemoveGiftToken((int)total);
				paid = true;
			}
		}
		else if (m_player.PlayerCharacter.Money >= (int)total)
		{
			m_player.RemoveMoney((int)total);
			paid = true;
		}
		if (!paid)
		{
			return false;
		}
		ManorItemTemplate item = ManorConfigMgr.GetItem(templateId);
		if (item == null)
		{
			return false;
		}
		AddGoods(templateId, item.Property1, count);
		SendUpdateGoods();
		try
		{
			m_player.Out.SendUpdatePublicPlayer(m_player.PlayerCharacter, m_player.BattleData.MatchInfo);
		}
		catch
		{
		}
		return true;
	}

	/// <summary>68/19 — mua thức ăn pet (giảm BuyFoodCount, cộng item 334102).</summary>
	public bool BuyPetFood(bool useBind)
	{
		if (!m_loaded || m_info == null)
		{
			return false;
		}
		EnsureDailyPetFoodReset(true);
		if (m_info.BuyFoodCount < 1)
		{
			return false;
		}
		ManorPetFoodPrice price = ManorConfigMgr.GetPetFoodPriceByRemain(m_info.BuyFoodCount);
		if (price == null || price.Money <= 0)
		{
			return false;
		}
		bool paid = false;
		if (useBind)
		{
			if (m_player.PlayerCharacter.DDTMoney >= price.Money)
			{
				m_player.RemoveGiftToken(price.Money);
				paid = true;
			}
		}
		else if (m_player.PlayerCharacter.Money >= price.Money)
		{
			m_player.RemoveMoney(price.Money);
			paid = true;
		}
		if (!paid)
		{
			return false;
		}
		ItemTemplateInfo goods = ItemMgr.FindItemTemplate(334102);
		if (goods != null)
		{
			ItemInfo itemInfo = ItemInfo.CreateFromTemplate(goods, price.ItemCount, 102);
			if (itemInfo != null)
			{
				itemInfo.IsBinds = true;
				m_player.AddTemplate(itemInfo, itemInfo.Template.BagType, price.ItemCount, eGameView.RouletteTypeGet);
			}
		}
		m_info.BuyFoodCount = Math.Max(0, m_info.BuyFoodCount - 1);
		ManorBussiness.SaveManor(m_info);
		AddLog("Mua 1 thức ăn pet (còn " + m_info.BuyFoodCount + " lượt hôm nay).");
		GSPacketIn pkg = new GSPacketIn(68, m_player.PlayerId);
		pkg.WriteByte(19);
		pkg.WriteInt(m_info.BuyFoodCount);
		m_player.SendTCP(pkg);
		try
		{
			m_player.Out.SendUpdatePublicPlayer(m_player.PlayerCharacter, m_player.BattleData.MatchInfo);
		}
		catch
		{
		}
		return true;
	}

	// ================================================================== phase 2.2 — worker / task

	private readonly List<UserManorTaskInfo> m_tasks = new List<UserManorTaskInfo>();

	private DateTime m_taskDate = DateTime.MinValue;

	private static void WriteSafeDate(GSPacketIn pkg, DateTime dt)
	{
		if (dt.Year < 1900)
		{
			dt = new DateTime(1900, 1, 1);
		}
		pkg.WriteDateTime(dt);
	}

	private static int GradeByCareerExp(int levelType, int exp)
	{
		return ManorConfigMgr.GetGradeByExp(levelType, exp);
	}

	private ManorCandidateInfo FindCandidate(int userId)
	{
		foreach (ManorCandidateInfo cand in ManorBussiness.GetManorCandidates(m_player.PlayerId, string.Empty, 500))
		{
			if (cand.UserId == userId)
			{
				return cand;
			}
		}
		return null;
	}

	public void SendWorkerList(string keyword)
	{
		List<ManorCandidateInfo> list = ManorBussiness.GetManorCandidates(m_player.PlayerId, keyword ?? string.Empty, 30);
		GSPacketIn pkg = new GSPacketIn(81, m_player.PlayerId);
		pkg.WriteByte(30);
		pkg.WriteInt(list.Count);
		foreach (ManorCandidateInfo cand in list)
		{
			UserManorInfo candManor = ManorBussiness.GetManor(cand.UserId);
			UserManorWorkerInfo owned = cand.OwnerID > 0 ? ManorBussiness.GetWorkerByWorkerUserID(cand.UserId) : null;
			pkg.WriteInt(cand.UserId);
			WriteUTF(pkg, cand.NickName);
			pkg.WriteInt(0); // value
			pkg.WriteInt(candManor == null ? 0 : candManor.Exp);
			pkg.WriteInt(0); // fishExp
			pkg.WriteInt(0); // mineExp
			pkg.WriteInt(0); // ranchExp
			pkg.WriteInt(cand.OwnerID);
			WriteUTF(pkg, cand.OwnerID > 0 ? (ManorBussiness.GetUserNickName(cand.OwnerID) ?? string.Empty) : string.Empty);
			pkg.WriteBoolean(cand.Sex != 0);
			WriteUTF(pkg, cand.Colors ?? string.Empty);
			WriteUTF(pkg, cand.Style ?? string.Empty);
			pkg.WriteInt(cand.OwnerID == 0 ? ManorConfigMgr.GetHirePrice(cand.Grade) : ManorConfigMgr.GetRansomPrice(cand.Grade));
			WriteSafeDate(pkg, cand.ProtectDate);
			WriteSafeDate(pkg, owned != null ? owned.EmployProtectDate : new DateTime(1900, 1, 1));
		}
		m_player.SendTCP(pkg);
	}

	private void WriteWorkerBody(GSPacketIn pkg, UserManorWorkerInfo w)
	{
		WriteUTF(pkg, w.Name);
		WriteUTF(pkg, w.Title);
		pkg.WriteInt(w.Feeling);
		pkg.WriteInt(w.Energy);
		pkg.WriteInt(w.FarmExp);
		pkg.WriteInt(w.FishExp);
		pkg.WriteInt(w.MineExp);
		pkg.WriteInt(w.RanchExp);
		pkg.WriteBoolean(w.IsWorking);
		pkg.WriteInt(w.WorkType);
		WriteSafeDate(pkg, w.WorkBeginDate);
		WriteSafeDate(pkg, w.EmployProtectDate);
		pkg.WriteInt(w.ProtectPrice);
	}

	public void SendEmployeResult(bool success, UserManorWorkerInfo worker, int workerUserId)
	{
		GSPacketIn pkg = new GSPacketIn(81, m_player.PlayerId);
		pkg.WriteByte(2);
		pkg.WriteBoolean(success);
		pkg.WriteInt(workerUserId);
		if (success && worker != null)
		{
			WriteWorkerBody(pkg, worker);
			pkg.WriteBoolean(worker.Sex);
			WriteUTF(pkg, worker.Colors ?? string.Empty);
			WriteUTF(pkg, worker.Style ?? string.Empty);
		}
		m_player.SendTCP(pkg);
	}

	public void SendUpdateWorker(UserManorWorkerInfo worker)
	{
		if (worker == null)
		{
			return;
		}
		GSPacketIn pkg = new GSPacketIn(81, m_player.PlayerId);
		pkg.WriteByte(32);
		pkg.WriteInt(m_player.PlayerId);
		pkg.WriteInt(worker.WorkerUserID);
		WriteWorkerBody(pkg, worker);
		m_player.SendTCP(pkg);
	}

	public bool HireWorker(int workerUserId)
	{
		if (!m_loaded || m_info == null || workerUserId <= 0 || workerUserId == m_player.PlayerId)
		{
			return false;
		}
		if (GetWorker(workerUserId) != null)
		{
			return false;
		}
		ManorCandidateInfo cand = FindCandidate(workerUserId);
		if (cand == null)
		{
			SendEmployeResult(false, null, workerUserId);
			return false;
		}
		if (cand.ProtectDate > DateTime.Now)
		{
			m_player.Out.SendMessage(eMessageType.Normal, "Ứng viên đang tự bảo vệ đến " + cand.ProtectDate.ToString("dd/MM HH:mm") + ", không thể thuê.");
			SendEmployeResult(false, null, workerUserId);
			return false;
		}
		UserManorWorkerInfo owned = ManorBussiness.GetWorkerByWorkerUserID(workerUserId);
		bool buyout = owned != null;
		int price;
		bool paid;
		if (!buyout)
		{
			price = ManorConfigMgr.GetHirePrice(cand.Grade);
			paid = price <= 0 || TrySpendMoney(price);
		}
		else
		{
			price = ManorConfigMgr.GetRansomPrice(cand.Grade);
			paid = false;
			if (m_player.PlayerCharacter.DDTMoney >= price)
			{
				m_player.RemoveGiftToken(price);
				paid = true;
			}
		}
		if (!paid)
		{
			SendEmployeResult(false, null, workerUserId);
			return false;
		}
		if (buyout)
		{
			ManorBussiness.DeleteWorker(owned.ID);
			GamePlayer oldOwner = WorldMgr.GetPlayerById(owned.OwnerID);
			if (oldOwner != null)
			{
				oldOwner.Out.SendMessage(eMessageType.Normal, "Thợ " + owned.Name + " của bạn đã bị " + m_player.PlayerCharacter.NickName + " mua đứt.");
			}
			GamePlayer workerPlayer = WorldMgr.GetPlayerById(workerUserId);
			if (workerPlayer != null)
			{
				workerPlayer.Out.SendMessage(eMessageType.Normal, "Bạn đã được " + m_player.PlayerCharacter.NickName + " mua đứt làm thợ.");
			}
		}
		UserManorWorkerInfo w = new UserManorWorkerInfo
		{
			OwnerID = m_player.PlayerId,
			WorkerUserID = workerUserId,
			Name = cand.NickName,
			Title = string.Empty,
			Feeling = 100,
			Energy = 100,
			FarmExp = 0,
			FishExp = 0,
			MineExp = 0,
			RanchExp = 0,
			IsWorking = false,
			WorkType = 0,
			WorkBeginDate = DateTime.Now,
			EmployProtectDate = DateTime.Now,
			ProtectPrice = price,
			Sex = cand.Sex != 0,
			Colors = cand.Colors ?? string.Empty,
			Style = cand.Style ?? string.Empty,
			HiredDate = DateTime.Now
		};
		if (!ManorBussiness.SaveWorker(w))
		{
			SendEmployeResult(false, null, workerUserId);
			return false;
		}
		if (GetWorker(workerUserId) == null)
		{
			m_workers.Add(w);
		}
		SendEmployeResult(true, w, workerUserId);
		SendUpdateGoods();
		AddLog((buyout ? "Mua đứt thợ " : "Thuê thợ ") + w.Name + " với giá " + price + ".");
		return true;
	}

	public bool FireWorker(int workerUserId)
	{
		if (!m_loaded)
		{
			return false;
		}
		UserManorWorkerInfo w = GetWorker(workerUserId);
		if (w == null)
		{
			return false;
		}
		DeleteWorker(workerUserId);
		SendEmployeResult(false, null, workerUserId);
		AddLog("Đã sa thải thợ " + w.Name + ".");
		return true;
	}

	public bool Freedom()
	{
		if (!m_loaded || m_info == null)
		{
			return false;
		}
		UserManorWorkerInfo own = ManorBussiness.GetWorkerByWorkerUserID(m_player.PlayerId);
		if (own == null)
		{
			return false;
		}
		int grade = ManorConfigMgr.GetGradeByExp(ManorConfigMgr.LevelTypeFarm, m_info.Exp);
		int price = ManorConfigMgr.GetRansomPrice(grade);
		if (!TrySpendMoney(price))
		{
			return false;
		}
		ManorBussiness.DeleteWorker(own.ID);
		AddLog("Đã tự chuộc thân khỏi nông trại chủ cũ.");
		SendManageInfo();
		SendUpdateGoods();
		return true;
	}

	public bool AddWorkerTime(int workerUserId, int days)
	{
		if (!m_loaded || days <= 0 || days > 365)
		{
			return false;
		}
		UserManorWorkerInfo w = GetWorker(workerUserId);
		if (w == null)
		{
			return false;
		}
		ManorCandidateInfo cand = FindCandidate(workerUserId);
		int grade = cand == null ? 1 : cand.Grade;
		int price = ManorConfigMgr.GetAddTimePrice(grade) * days;
		if (m_player.PlayerCharacter.DDTMoney < price)
		{
			return false;
		}
		m_player.RemoveGiftToken(price);
		DateTime baseDate = w.EmployProtectDate > DateTime.Now ? w.EmployProtectDate : DateTime.Now;
		w.EmployProtectDate = baseDate.AddDays(days);
		w.ProtectPrice = price;
		SaveWorker(w);
		SendUpdateWorker(w);
		return true;
	}

	public bool SetWorkerTitle(int workerUserId, string title, bool isBind)
	{
		if (!m_loaded)
		{
			return false;
		}
		UserManorWorkerInfo w = GetWorker(workerUserId);
		if (w == null)
		{
			return false;
		}
		int price = ManorConfigMgr.OtherNameMoney;
		if (m_player.PlayerCharacter.DDTMoney < price)
		{
			return false;
		}
		m_player.RemoveGiftToken(price);
		w.Title = (title ?? string.Empty).Trim();
		if (w.Title.Length > 50)
		{
			w.Title = w.Title.Substring(0, 50);
		}
		SaveWorker(w);
		SendUpdateWorker(w);
		return true;
	}

	public bool FeedWorker(int workerUserId, int foodTemplateId, int count)
	{
		if (!m_loaded || count <= 0 || count > 999)
		{
			return false;
		}
		UserManorWorkerInfo w = GetWorker(workerUserId);
		if (w == null)
		{
			return false;
		}
		ManorItemTemplate food = ManorConfigMgr.GetItem(foodTemplateId);
		if (food == null || !food.BelongsToBag(BagFeed))
		{
			return false;
		}
		if (GetGoodsCount(foodTemplateId, food.Property1) < count)
		{
			return false;
		}
		if (!RemoveGoods(foodTemplateId, food.Property1, count))
		{
			return false;
		}
		w.Energy = Math.Min(100, w.Energy + food.Property4 * count);
		SaveWorker(w);
		SendUpdateWorker(w);
		SendUpdateGoods();
		return true;
	}

	private int GetWorkMax(int areaType)
	{
		UserManorAreaInfo area = GetArea(areaType);
		int grade = area == null ? 1 : Math.Max(1, area.Level);
		ManorUpgradeTemplate conf = ManorConfigMgr.GetUpgrade(areaType, grade);
		return conf == null ? 0 : conf.WorkMax;
	}

	private int CountWorking(int areaType)
	{
		int n = 0;
		foreach (UserManorWorkerInfo w in GetWorkers())
		{
			if (w.IsWorking && w.WorkType == areaType)
			{
				n++;
			}
		}
		return n;
	}

	public bool WorkerWorkBegin(int workType, int workerUserId)
	{
		if (!m_loaded || workType < AreaFarm || workType > AreaRanch)
		{
			return false;
		}
		UserManorWorkerInfo w = GetWorker(workerUserId);
		if (w == null || w.Energy <= 0)
		{
			return false;
		}
		if (CountWorking(workType) >= GetWorkMax(workType))
		{
			return false;
		}
		w.IsWorking = true;
		w.WorkType = workType;
		w.WorkBeginDate = DateTime.Now;
		SaveWorker(w);
		SendUpdateWorker(w);
		return true;
	}

	public bool WorkerWorkEnd(int workType, int workerUserId)
	{
		if (!m_loaded)
		{
			return false;
		}
		UserManorWorkerInfo w = GetWorker(workerUserId);
		if (w == null)
		{
			return false;
		}
		w.IsWorking = false;
		w.WorkType = 0;
		SaveWorker(w);
		SendUpdateWorker(w);
		return true;
	}

	public bool WorkerBeat(int workerUserId)
	{
		UserManorWorkerInfo w = GetWorker(workerUserId);
		if (!m_loaded || w == null)
		{
			return false;
		}
		w.Feeling = Math.Max(0, w.Feeling - 10);
		SaveWorker(w);
		SendUpdateWorker(w);
		return true;
	}

	public bool WorkerLove(int workerUserId)
	{
		UserManorWorkerInfo w = GetWorker(workerUserId);
		if (!m_loaded || w == null)
		{
			return false;
		}
		w.Feeling = Math.Min(100, w.Feeling + 5);
		SaveWorker(w);
		SendUpdateWorker(w);
		return true;
	}

	public bool WorkerGift(int workerUserId)
	{
		UserManorWorkerInfo w = GetWorker(workerUserId);
		if (!m_loaded || w == null)
		{
			return false;
		}
		int price = ManorConfigMgr.GiftMoney;
		if (m_player.PlayerCharacter.DDTMoney < price)
		{
			return false;
		}
		m_player.RemoveGiftToken(price);
		w.Feeling = Math.Min(100, w.Feeling + 10);
		SaveWorker(w);
		SendUpdateWorker(w);
		SendUpdateGoods();
		return true;
	}

	/// <summary>Chu kỳ làm việc của thợ: giảm thể lực/tâm trạng, tăng exp nghề, tự nghỉ khi kiệt sức.</summary>
	private void ProcessWorkerTick(DateTime now, bool sendUpdates)
	{
		foreach (UserManorWorkerInfo w in GetWorkers())
		{
			if (!w.IsWorking)
			{
				m_workerTick.Remove(w.WorkerUserID);
				continue;
			}
			DateTime last;
			if (!m_workerTick.TryGetValue(w.WorkerUserID, out last))
			{
				m_workerTick[w.WorkerUserID] = now;
				continue;
			}
			int minutes = ManorConfigMgr.WorkEventMinute;
			if (minutes <= 0)
			{
				minutes = 20;
			}
			double elapsedMin = (now - last).TotalMinutes;
			if (elapsedMin < minutes)
			{
				continue;
			}
			int cycles = Math.Max(1, (int)(elapsedMin / minutes));
			if (cycles > 50)
			{
				cycles = 50;
			}
			m_workerTick[w.WorkerUserID] = last.AddMinutes((long)minutes * cycles);
			w.Energy = Math.Max(0, w.Energy - ManorConfigMgr.WorkDecPhysic * cycles);
			w.Feeling = Math.Max(0, w.Feeling - ManorConfigMgr.WorkDecFeeling * cycles);
			// sự kiện ngẫu nhiên trong ca làm việc
			if (Rnd.Next(100) < ManorConfigMgr.WorkEventRandom)
			{
				int ev = Rnd.Next(3);
				if (ev == 0)
				{
					w.Feeling = Math.Max(0, w.Feeling - 10);
				}
				else if (ev == 1)
				{
					w.Feeling = Math.Min(100, w.Feeling + 5);
				}
				else
				{
					w.Energy = Math.Min(100, w.Energy + 5);
				}
			}
			int careerExp = 2 * cycles;
			switch (w.WorkType)
			{
			case AreaFarm:
				w.FarmExp += careerExp;
				break;
			case AreaFish:
				w.FishExp += careerExp;
				break;
			case AreaMine:
				w.MineExp += careerExp;
				break;
			case AreaRanch:
				w.RanchExp += careerExp;
				break;
			}
			if (w.Energy <= 0 || w.Feeling <= 0)
			{
				w.IsWorking = false;
				w.WorkType = 0;
				m_workerTick.Remove(w.WorkerUserID);
			}
			SaveWorker(w);
			if (sendUpdates)
			{
				SendUpdateWorker(w);
			}
		}
	}

	/// <summary>Khi thu hoạch khu: cộng exp nghề cho thợ đang làm khu đó.</summary>
	public void AddWorkerHarvestExp(int workType, int exp)
	{
		if (!m_loaded || exp <= 0)
		{
			return;
		}
		foreach (UserManorWorkerInfo w in GetWorkers())
		{
			if (!w.IsWorking || w.WorkType != workType)
			{
				continue;
			}
			switch (workType)
			{
			case AreaFarm:
				w.FarmExp += exp;
				break;
			case AreaFish:
				w.FishExp += exp;
				break;
			case AreaMine:
				w.MineExp += exp;
				break;
			case AreaRanch:
				w.RanchExp += exp;
				break;
			}
			SaveWorker(w);
			SendUpdateWorker(w);
		}
	}

	/// <summary>Đập thợ lười ở nông trại bạn bè (giảm feeling của thợ đó).</summary>
	public static void BeatFriendWorker(int farmUserId, int workerUserId)
	{
		if (farmUserId <= 0 || workerUserId <= 0)
		{
			return;
		}
		UserManorWorkerInfo w = ManorBussiness.GetWorker(farmUserId, workerUserId);
		if (w == null)
		{
			return;
		}
		w.Feeling = Math.Max(0, w.Feeling - 10);
		ManorBussiness.SaveWorker(w);
	}

	/// <summary>Danh sách friendID có farm đang có thợ "lười" (đang làm nhưng tâm trạng thấp).</summary>
	public static List<int> GetSlackerFriends(int[] friendIds)
	{
		List<int> result = new List<int>();
		if (friendIds == null)
		{
			return result;
		}
		foreach (int friendId in friendIds)
		{
			foreach (UserManorWorkerInfo w in ManorBussiness.GetWorkers(friendId))
			{
				if (w.IsWorking && w.Feeling <= 20)
				{
					result.Add(friendId);
					break;
				}
			}
		}
		return result;
	}

	// ------------------------------------------------------------------ tasks (bảng tin)

	private void EnsureDailyTasks()
	{
		DateTime today = DateTime.Today;
		if (m_taskDate == today && m_tasks.Count > 0)
		{
			return;
		}
		m_tasks.Clear();
		m_tasks.AddRange(ManorBussiness.GetManorTasks(m_player.PlayerId, today));
		m_taskDate = today;
		if (m_tasks.Count > 0)
		{
			return;
		}
		List<ManorTaskTemplate> all = ManorConfigMgr.GetAllTasks();
		List<ManorTaskTemplate> type1 = new List<ManorTaskTemplate>();
		List<ManorTaskTemplate> type2 = new List<ManorTaskTemplate>();
		all.Sort((a, b) => a.TemplateId.CompareTo(b.TemplateId));
		foreach (ManorTaskTemplate t in all)
		{
			if (t.TaskType == 1)
			{
				type1.Add(t);
			}
			else if (t.TaskType == 2)
			{
				type2.Add(t);
			}
		}
		int seed = today.DayOfYear;
		HashSet<int> chosen = new HashSet<int>();
		CreateDailyTasksOfType(type1, seed, chosen, today);
		CreateDailyTasksOfType(type2, seed + 5, chosen, today);
	}

	private void CreateDailyTasksOfType(List<ManorTaskTemplate> pool, int startIndex, HashSet<int> chosen, DateTime date)
	{
		if (pool.Count == 0)
		{
			return;
		}
		int index = Math.Abs(startIndex) % pool.Count;
		int created = 0;
		int guard = pool.Count * 2;
		while (created < 3 && guard-- > 0)
		{
			ManorTaskTemplate t = pool[index % pool.Count];
			index++;
			if (chosen.Add(t.TemplateId))
			{
				CreateTask(t, date);
				created++;
			}
			if (chosen.Count >= pool.Count)
			{
				break;
			}
		}
	}

	private void CreateTask(ManorTaskTemplate t, DateTime date)
	{
		UserManorTaskInfo task = new UserManorTaskInfo
		{
			UserID = m_player.PlayerId,
			TaskDate = date,
			TaskId = t.TemplateId,
			TemplateId = t.TemplateId,
			Condition1 = 0,
			Condition2 = 0,
			Condition3 = 0,
			IsComplete = false,
			IsReward = false,
			UpdateDate = DateTime.Now
		};
		if (ManorBussiness.SaveManorTask(task))
		{
			m_tasks.Add(task);
		}
	}

	private int GetTaskGoodsProperty(int templateId)
	{
		ManorItemTemplate item = ManorConfigMgr.GetItem(templateId);
		return item == null ? 1 : item.Property1;
	}

	private static int ParseTaskInt(string value)
	{
		int v;
		return int.TryParse((value ?? string.Empty).Trim(), out v) ? v : 0;
	}

	public void RefreshTaskProgress()
	{
		EnsureDailyTasks();
		int farmGrade = GetFarmLevelGrade();
		foreach (UserManorTaskInfo task in m_tasks)
		{
			ManorTaskTemplate temp = ManorConfigMgr.GetTask(task.TemplateId);
			if (temp == null)
			{
				continue;
			}
			if (temp.TaskType == 2)
			{
				int needItem = ParseTaskInt(temp.Condition1);
				int needCount = ParseTaskInt(temp.Condition2);
				int needLevel = ParseTaskInt(temp.Condition3);
				if (needItem > 0)
				{
					task.Condition1 = GetGoodsCount(needItem, GetTaskGoodsProperty(needItem));
				}
				task.IsComplete = task.Condition1 >= needCount && farmGrade >= needLevel;
			}
			else
			{
				task.IsComplete = task.Condition1 >= ParseTaskInt(temp.Condition1);
			}
			ManorBussiness.SaveManorTask(task);
		}
	}

	public void SendTaskList()
	{
		EnsureDailyTasks();
		GSPacketIn pkg = new GSPacketIn(81, m_player.PlayerId);
		pkg.WriteByte(18);
		pkg.WriteInt(1); // flag != 0 -> client refresh UI
		pkg.WriteInt(m_tasks.Count);
		foreach (UserManorTaskInfo task in m_tasks)
		{
			pkg.WriteInt(task.TaskId);
			pkg.WriteInt(task.TemplateId);
			pkg.WriteInt(task.Condition1);
			pkg.WriteInt(task.Condition2);
			pkg.WriteInt(task.Condition3);
			pkg.WriteBoolean(task.IsComplete);
			pkg.WriteBoolean(task.IsReward);
		}
		m_player.SendTCP(pkg);
	}

	public void AddTaskProgress(int taskType, int amount)
	{
		EnsureDailyTasks();
		foreach (UserManorTaskInfo task in m_tasks)
		{
			if (task.IsReward)
			{
				continue;
			}
			ManorTaskTemplate temp = ManorConfigMgr.GetTask(task.TemplateId);
			if (temp == null || temp.TaskType != 1)
			{
				continue;
			}
			task.Condition1 += amount;
			task.IsComplete = task.Condition1 >= ParseTaskInt(temp.Condition1);
			ManorBussiness.SaveManorTask(task);
		}
	}

	public bool FinishTask(int taskId)
	{
		EnsureDailyTasks();
		UserManorTaskInfo task = null;
		foreach (UserManorTaskInfo t in m_tasks)
		{
			if (t.TaskId == taskId)
			{
				task = t;
				break;
			}
		}
		if (task == null || task.IsReward)
		{
			return false;
		}
		ManorTaskTemplate temp = ManorConfigMgr.GetTask(task.TemplateId);
		if (temp == null)
		{
			return false;
		}
		if (temp.TaskType == 2)
		{
			int needItem = ParseTaskInt(temp.Condition1);
			int needCount = ParseTaskInt(temp.Condition2);
			int needLevel = ParseTaskInt(temp.Condition3);
			if (needItem <= 0 || needCount <= 0)
			{
				m_player.Out.SendMessage(eMessageType.Normal, "Nhiệm vụ không hợp lệ.");
				return false;
			}
			if (GetFarmLevelGrade() < needLevel)
			{
				m_player.Out.SendMessage(eMessageType.Normal, "Cần Nông Trại Lv" + needLevel + " để nộp nhiệm vụ này.");
				return false;
			}
			int property = GetTaskGoodsProperty(needItem);
			int owned = GetGoodsCountAnyProperty(needItem, out property);
			if (owned < needCount)
			{
				ItemTemplateInfo itemTemplate = ItemMgr.FindItemTemplate(needItem);
				string itemName = itemTemplate == null ? ("#" + needItem) : itemTemplate.Name;
				m_player.Out.SendMessage(eMessageType.Normal, "Cần " + needCount + " " + itemName + " để nộp (bạn có " + owned + ").");
				return false;
			}
			if (!RemoveGoods(needItem, property, needCount))
			{
				m_player.Out.SendMessage(eMessageType.Normal, "Không thể nộp vật phẩm, vui lòng thử lại.");
				return false;
			}
		}
		else
		{
			// Nhiệm vụ loại 1 (đập thợ lười): yêu cầu đủ tiến độ thật (tăng qua 81/25).
			int target = ParseTaskInt(temp.Condition1);
			if (task.Condition1 < target)
			{
				m_player.Out.SendMessage(eMessageType.Normal, "Chưa đủ tiến độ nhiệm vụ (" + task.Condition1 + "/" + target + "). Hãy đến nông trại bạn bè đập thợ lười.");
				return false;
			}
		}
		GrantTaskReward(temp.RewardItemId1, temp.RewardCount1);
		GrantTaskReward(temp.RewardItemId2, temp.RewardCount2);
		AddLog("Hoàn thành nhiệm vụ: " + (temp.TaskName ?? "") + ".");
		task.IsComplete = true;
		task.IsReward = true;
		ManorBussiness.SaveManorTask(task);
		m_player.Out.SendMessage(eMessageType.Normal, "Đã hoàn thành nhiệm vụ, nhận thưởng!");
		SendTaskList();
		SendUpdateGoods();
		return true;
	}

	private void GrantTaskReward(int templateId, int count)
	{
		if (templateId <= 0 || count <= 0)
		{
			return;
		}
		ItemTemplateInfo template = ItemMgr.FindItemTemplate(templateId);
		if (template == null)
		{
			return;
		}
		ItemInfo item = ItemInfo.CreateFromTemplate(template, count, 104);
		if (item == null)
		{
			return;
		}
		if (!m_player.AddTemplate(item))
		{
			List<ItemInfo> list = new List<ItemInfo> { item };
			m_player.SendItemsToMail(list, LanguageMgr.GetTranslation("PlayerFarm.Msg3"), LanguageMgr.GetTranslation("PlayerFarm.Msg3"), eMailType.ItemOverdue);
		}
	}

	/// <summary>Đập 1 thợ đang làm việc (ngẫu nhiên) ở farm bạn bè: feeling -10, lưu DB, trả worker.</summary>
	public static UserManorWorkerInfo BeatSlackerWorker(int farmUserId)
	{
		if (farmUserId <= 0)
		{
			return null;
		}
		List<UserManorWorkerInfo> working = new List<UserManorWorkerInfo>();
		foreach (UserManorWorkerInfo w in ManorBussiness.GetWorkers(farmUserId))
		{
			if (w.IsWorking)
			{
				working.Add(w);
			}
		}
		if (working.Count == 0)
		{
			return null;
		}
		UserManorWorkerInfo target = working[new Random().Next(working.Count)];
		target.Feeling = Math.Max(0, target.Feeling - 10);
		ManorBussiness.SaveWorker(target);
		return target;
	}

	// ================================================================== phase 2.4 — log / hạn mức shop / tự bảo vệ

	private readonly Dictionary<int, int> m_shopLimitBought = new Dictionary<int, int>();

	private int m_shopMonthKey;

	private int m_logCountSinceTrim;

	/// <summary>Reset lượt mua thức ăn pet mỗi ngày (mặc định 30 lượt/ngày).</summary>
	public void EnsureDailyPetFoodReset(bool save)
	{
		if (m_info == null)
		{
			return;
		}
		if (m_info.BuyFoodDate.Year >= 1900 && m_info.BuyFoodDate.Date >= DateTime.Today)
		{
			return;
		}
		m_info.BuyFoodCount = 30;
		m_info.BuyFoodDate = DateTime.Today;
		if (save)
		{
			ManorBussiness.SaveManor(m_info);
		}
	}

	/// <summary>Nạp hạn mức mua shop 139 của tháng hiện tại (reset theo tháng).</summary>
	public void EnsureShopMonth()
	{
		int monthKey = DateTime.Now.Year * 100 + DateTime.Now.Month;
		if (m_shopMonthKey == monthKey)
		{
			return;
		}
		m_shopLimitBought.Clear();
		foreach (UserManorShopLimitInfo row in ManorBussiness.GetManorShopLimits(m_player.PlayerId, monthKey))
		{
			m_shopLimitBought[row.GoodsID] = row.BuyCount;
		}
		m_shopMonthKey = monthKey;
		ManorBussiness.ClearOldManorShopLimits(m_player.PlayerId, monthKey);
	}

	public int GetShop139Bought(int goodsID)
	{
		EnsureShopMonth();
		int bought;
		return m_shopLimitBought.TryGetValue(goodsID, out bought) ? bought : 0;
	}

	public bool AddShop139Purchase(int goodsID, int count)
	{
		EnsureShopMonth();
		if (!ManorBussiness.AddManorShopLimit(m_player.PlayerId, goodsID, m_shopMonthKey, count))
		{
			return false;
		}
		m_shopLimitBought[goodsID] = GetShop139Bought(goodsID) + count;
		return true;
	}

	public bool IsSelfProtected
	{
		get
		{
			return m_info != null && m_info.ProtectDate > DateTime.Now;
		}
	}

	/// <summary>81/8 — mua tự bảo vệ bản thân N ngày (Xu/ngày), chặn bị thuê/mua đứt.</summary>
	public bool BuySelfProtect(int days)
	{
		if (!m_loaded || m_info == null || days <= 0 || days > 365)
		{
			return false;
		}
		int grade = GetFarmLevelGrade();
		long cost = (long)ManorConfigMgr.GetAddTimePrice(grade) * days;
		if (cost > int.MaxValue || m_player.PlayerCharacter.DDTMoney < (int)cost)
		{
			m_player.Out.SendMessage(eMessageType.Normal, "Không đủ Xu để tự bảo vệ (" + cost + " Xu).");
			return false;
		}
		m_player.RemoveGiftToken((int)cost);
		DateTime baseDate = m_info.ProtectDate > DateTime.Now ? m_info.ProtectDate : DateTime.Now;
		m_info.ProtectDate = baseDate.AddDays(days);
		ManorBussiness.SaveManor(m_info);
		AddLog("Tự bảo vệ bản thân " + days + " ngày (đến " + m_info.ProtectDate.ToString("dd/MM/yyyy") + ").");
		m_player.Out.SendMessage(eMessageType.Normal, "Đã mua tự bảo vệ " + days + " ngày.");
		SendUpdateGoods();
		SendManageInfo();
		return true;
	}

	/// <summary>Ghi nhật ký nông trại (tự giới hạn 50 dòng).</summary>
	public void AddLog(string content)
	{
		if (!m_loaded || string.IsNullOrEmpty(content))
		{
			return;
		}
		ManorBussiness.AddManorLog(m_player.PlayerId, 0, content);
		m_logCountSinceTrim++;
		if (m_logCountSinceTrim >= 10)
		{
			m_logCountSinceTrim = 0;
			ManorBussiness.TrimManorLogs(m_player.PlayerId, 50);
		}
	}
}
