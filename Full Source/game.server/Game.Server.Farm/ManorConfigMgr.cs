using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Bussiness;
using log4net;
using SqlDataProvider.Data;

namespace Game.Server.Farm;

/// <summary>
/// Cấu hình Nông trại 2.0 (Farm 2.0, client 12.6).
/// Nguồn dữ liệu: các file XML (zlib) trong thư mục WebSite\Request:
///  - templatemanorlist.xml : danh sách ItemTemplate của nông sản/hạt giống/vật phẩm nông trại
///  - ts_manorplant.xml     : bảng nâng cấp 6 khu (UpgradeType 1..6) theo Grade
///  - ts_upgradetemplate.xml: bảng cấp/exp (Types 109..113: trang trại, kỹ thuật nông trại/cá/mỏ/đồng cỏ)
///  - ts_manortask.xml      : nhiệm vụ bảng tin
///  - petexpitemprice.xml   : giá mua thức ăn pet (Count -> Money/ItemCount)
/// </summary>
public static class ManorConfigMgr
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private static readonly object SyncRoot = new object();

	private static bool _loaded;

	private static readonly Dictionary<int, ManorItemTemplate> Items = new Dictionary<int, ManorItemTemplate>();

	private static readonly Dictionary<int, Dictionary<int, ManorUpgradeTemplate>> Upgrades = new Dictionary<int, Dictionary<int, ManorUpgradeTemplate>>();

	private static readonly Dictionary<int, Dictionary<int, ManorLevelTemplate>> Levels = new Dictionary<int, Dictionary<int, ManorLevelTemplate>>();

	private static readonly Dictionary<int, ManorTaskTemplate> Tasks = new Dictionary<int, ManorTaskTemplate>();

	private static readonly List<ManorPetFoodPrice> PetFoodPrices = new List<ManorPetFoodPrice>();

	/// <summary>TemplateTypes của ts_upgradetemplate.xml dùng cho nông trại.</summary>
	public const int LevelTypeFarm = 109;
	public const int LevelTypeFarmSkill = 110;
	public const int LevelTypeFishSkill = 111;
	public const int LevelTypeMineSkill = 112;
	public const int LevelTypeRanchSkill = 113;

	public static string RequestDir
	{
		get
		{
			string dir = ConfigurationManager.AppSettings["Request"];
			if (string.IsNullOrEmpty(dir))
			{
				dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Request");
			}
			return dir;
		}
	}

	public static void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}
		lock (SyncRoot)
		{
			if (_loaded)
			{
				return;
			}
			try
			{
				LoadItems();
				LoadUpgrades();
				LoadLevels();
				LoadTasks();
				LoadPetFoodPrices();
			}
			catch (Exception ex)
			{
				Log.Error("ManorConfigMgr load error", ex);
			}
			finally
			{
				_loaded = true;
			}
		}
	}

	private static XDocument LoadRequestXml(string fileName)
	{
		string path = Path.Combine(RequestDir, fileName);
		if (!File.Exists(path))
		{
			Log.Warn("ManorConfigMgr: missing file " + path);
			return null;
		}
		byte[] raw = File.ReadAllBytes(path);
		byte[] xml;
		try
		{
			xml = Game.Base.Marshal.Uncompress(raw);
		}
		catch
		{
			xml = raw;
		}
		return XDocument.Parse(Encoding.UTF8.GetString(xml));
	}

	private static int GetInt(XElement e, string name, int fallback = 0)
	{
		XAttribute a = e.Attribute(name);
		if (a == null)
		{
			return fallback;
		}
		int v;
		return int.TryParse(a.Value, out v) ? v : fallback;
	}

	private static string GetStr(XElement e, string name)
	{
		XAttribute a = e.Attribute(name);
		return a == null ? string.Empty : a.Value;
	}

	private static void LoadItems()
	{
		Items.Clear();
		XDocument doc = LoadRequestXml("templatemanorlist.xml");
		if (doc == null || doc.Root == null)
		{
			return;
		}
		foreach (XElement e in doc.Root.Descendants("Item"))
		{
			ManorItemTemplate item = new ManorItemTemplate
			{
				TemplateID = GetInt(e, "TemplateID"),
				Name = GetStr(e, "Name"),
				Description = GetStr(e, "Description"),
				CategoryID = GetInt(e, "CategoryID"),
				Level = GetInt(e, "Level"),
				Quality = GetInt(e, "Quality"),
				MaxCount = GetInt(e, "MaxCount"),
				Pic = GetStr(e, "Pic"),
				NeedLevel = GetInt(e, "NeedLevel"),
				ReclaimValue = GetInt(e, "ReclaimValue"),
				ReclaimType = GetInt(e, "ReclaimType"),
				Property1 = GetInt(e, "Property1"),
				Property2 = GetInt(e, "Property2"),
				Property3 = GetInt(e, "Property3"),
				Property4 = GetInt(e, "Property4"),
				Property5 = GetInt(e, "Property5"),
				Property6 = GetInt(e, "Property6"),
				Property7 = GetInt(e, "Property7"),
				Property8 = GetInt(e, "Property8")
			};
			if (item.TemplateID > 0)
			{
				Items[item.TemplateID] = item;
			}
		}
		Log.Warn("ManorConfigMgr: loaded " + Items.Count + " manor item templates");
	}

	private static void LoadUpgrades()
	{
		Upgrades.Clear();
		XDocument doc = LoadRequestXml("ts_manorplant.xml");
		if (doc == null || doc.Root == null)
		{
			return;
		}
		foreach (XElement e in doc.Root.Descendants("Item"))
		{
			ManorUpgradeTemplate t = new ManorUpgradeTemplate
			{
				UpgradeType = GetInt(e, "UpgradeType"),
				Grade = GetInt(e, "Grade"),
				NeedGrade1 = GetInt(e, "NeedGrade1"),
				NeedGrade2 = GetInt(e, "NeedGrade2"),
				NeedItem1 = GetInt(e, "NeedItem1"),
				NeedItemCount1 = GetInt(e, "NeedItemCount1"),
				NeedItem2 = GetInt(e, "NeedItem2"),
				NeedItemCount2 = GetInt(e, "NeedItemCount2"),
				PlantSpeedValue = GetInt(e, "PlantSpeedValue"),
				Style = GetInt(e, "Style"),
				WorkMax = GetInt(e, "WorkMax"),
				DropId = GetInt(e, "DropId"),
				UnlockItem = GetInt(e, "UnlockItem")
			};
			Dictionary<int, ManorUpgradeTemplate> byGrade;
			if (!Upgrades.TryGetValue(t.UpgradeType, out byGrade))
			{
				byGrade = new Dictionary<int, ManorUpgradeTemplate>();
				Upgrades[t.UpgradeType] = byGrade;
			}
			byGrade[t.Grade] = t;
		}
		Log.Warn("ManorConfigMgr: loaded " + Upgrades.Count + " manor upgrade types");
	}

	private static void LoadLevels()
	{
		Levels.Clear();
		XDocument doc = LoadRequestXml("ts_upgradetemplate.xml");
		if (doc == null || doc.Root == null)
		{
			return;
		}
		foreach (XElement e in doc.Root.Descendants("Item"))
		{
			int types = GetInt(e, "Types", -1);
			if (types < LevelTypeFarm || types > LevelTypeRanchSkill)
			{
				continue;
			}
			ManorLevelTemplate t = new ManorLevelTemplate
			{
				Types = types,
				TemplateId = GetInt(e, "TemplateId"),
				Grades = GetInt(e, "Grades"),
				Data = GetInt(e, "Data"),
				TemplateName = GetStr(e, "TemplateName"),
				ActiveObj = GetStr(e, "ActiveObj"),
				Param1 = GetInt(e, "Param1"),
				Param2 = GetInt(e, "Param2"),
				Param3 = GetInt(e, "Param3"),
				Param4 = GetInt(e, "Param4")
			};
			Dictionary<int, ManorLevelTemplate> byGrade;
			if (!Levels.TryGetValue(types, out byGrade))
			{
				byGrade = new Dictionary<int, ManorLevelTemplate>();
				Levels[types] = byGrade;
			}
			byGrade[t.Grades] = t;
		}
		Log.Warn("ManorConfigMgr: loaded " + Levels.Count + " manor level tables");
	}

	private static void LoadTasks()
	{
		Tasks.Clear();
		XDocument doc = LoadRequestXml("ts_manortask.xml");
		if (doc == null || doc.Root == null)
		{
			return;
		}
		foreach (XElement e in doc.Root.Descendants("Item"))
		{
			ManorTaskTemplate t = new ManorTaskTemplate
			{
				TemplateId = GetInt(e, "TemplateId"),
				TaskType = GetInt(e, "TaskType"),
				TaskName = GetStr(e, "TaskName"),
				Condition1 = GetStr(e, "Condition1"),
				Condition2 = GetStr(e, "Condition2"),
				Condition3 = GetStr(e, "Condition3"),
				RewardItemId1 = GetInt(e, "RewardItemId1"),
				RewardCount1 = GetInt(e, "RewardCount1"),
				RewardItemId2 = GetInt(e, "RewardItemId2"),
				RewardCount2 = GetInt(e, "RewardCount2"),
				TaskDescp = GetStr(e, "TaskDescp")
			};
			if (t.TemplateId > 0)
			{
				Tasks[t.TemplateId] = t;
			}
		}
		Log.Warn("ManorConfigMgr: loaded " + Tasks.Count + " manor tasks");
	}

	private static void LoadPetFoodPrices()
	{
		PetFoodPrices.Clear();
		XDocument doc = LoadRequestXml("petexpitemprice.xml");
		if (doc == null || doc.Root == null)
		{
			return;
		}
		foreach (XElement e in doc.Root.Descendants("Item"))
		{
			PetFoodPrices.Add(new ManorPetFoodPrice
			{
				Count = GetInt(e, "Count"),
				Money = GetInt(e, "Money"),
				ItemCount = GetInt(e, "ItemCount")
			});
		}
		PetFoodPrices.Sort((a, b) => a.Count.CompareTo(b.Count));
		Log.Warn("ManorConfigMgr: loaded " + PetFoodPrices.Count + " pet food prices");
	}

	public static ManorItemTemplate GetItem(int templateId)
	{
		EnsureLoaded();
		ManorItemTemplate item;
		Items.TryGetValue(templateId, out item);
		return item;
	}

	/// <summary>Các item thuộc một "túi" nông trại (bagType 0..10, tương ứng bit 1&lt;&lt;bagType trong Property1).</summary>
	public static List<ManorItemTemplate> GetItemsByBag(int bagType)
	{
		EnsureLoaded();
		int flag = 1 << bagType;
		List<ManorItemTemplate> result = new List<ManorItemTemplate>();
		foreach (ManorItemTemplate item in Items.Values)
		{
			if ((item.Property1 & flag) != 0)
			{
				result.Add(item);
			}
		}
		return result;
	}

	public static ManorUpgradeTemplate GetUpgrade(int upgradeType, int grade)
	{
		EnsureLoaded();
		Dictionary<int, ManorUpgradeTemplate> byGrade;
		ManorUpgradeTemplate t;
		if (Upgrades.TryGetValue(upgradeType, out byGrade) && byGrade.TryGetValue(grade, out t))
		{
			return t;
		}
		return null;
	}

	public static ManorUpgradeTemplate GetMaxUpgrade(int upgradeType)
	{
		EnsureLoaded();
		Dictionary<int, ManorUpgradeTemplate> byGrade;
		if (!Upgrades.TryGetValue(upgradeType, out byGrade) || byGrade.Count == 0)
		{
			return null;
		}
		ManorUpgradeTemplate max = null;
		foreach (ManorUpgradeTemplate t in byGrade.Values)
		{
			if (max == null || t.Grade > max.Grade)
			{
				max = t;
			}
		}
		return max;
	}

	public static int GetMaxUpgradeGrade(int upgradeType)
	{
		ManorUpgradeTemplate max = GetMaxUpgrade(upgradeType);
		return max == null ? 0 : max.Grade;
	}

	public static ManorLevelTemplate GetLevelTemplate(int levelType, int grade)
	{
		EnsureLoaded();
		Dictionary<int, ManorLevelTemplate> byGrade;
		ManorLevelTemplate t;
		if (Levels.TryGetValue(levelType, out byGrade) && byGrade.TryGetValue(grade, out t))
		{
			return t;
		}
		return null;
	}

	/// <summary>Quy đổi exp ra Grade (1..N) theo bảng exp (Types 109..113).</summary>
	public static int GetGradeByExp(int levelType, int exp)
	{
		EnsureLoaded();
		Dictionary<int, ManorLevelTemplate> byGrade;
		if (!Levels.TryGetValue(levelType, out byGrade) || byGrade.Count == 0)
		{
			return 1;
		}
		int best = 1;
		foreach (ManorLevelTemplate t in byGrade.Values)
		{
			if (exp >= t.Data && t.Grades > best)
			{
				best = t.Grades;
			}
		}
		return best;
	}

	public static ManorLevelTemplate GetNextLevelTemplate(int levelType, int exp)
	{
		EnsureLoaded();
		Dictionary<int, ManorLevelTemplate> byGrade;
		if (!Levels.TryGetValue(levelType, out byGrade))
		{
			return null;
		}
		int grade = GetGradeByExp(levelType, exp);
		ManorLevelTemplate next;
		return byGrade.TryGetValue(grade + 1, out next) ? next : null;
	}

	public static ManorTaskTemplate GetTask(int templateId)
	{
		EnsureLoaded();
		ManorTaskTemplate t;
		Tasks.TryGetValue(templateId, out t);
		return t;
	}

	public static List<ManorPetFoodPrice> GetPetFoodPrices()
	{
		EnsureLoaded();
		return new List<ManorPetFoodPrice>(PetFoodPrices);
	}

	/// <summary>Giá mua thức ăn pet theo số lượt mua còn lại (buyExpRemainNum 0..n).</summary>
	public static ManorPetFoodPrice GetPetFoodPriceByRemain(int buyExpRemainNum)
	{
		EnsureLoaded();
		int count = 31 - buyExpRemainNum;
		foreach (ManorPetFoodPrice p in PetFoodPrices)
		{
			if (p.Count == count)
			{
				return p;
			}
		}
		return PetFoodPrices.Count > 0 ? PetFoodPrices[0] : null;
	}

	// ------------------------------------------------------------------ farm shop (ShopID = 135)

	private static readonly object ShopSync = new object();

	private static Dictionary<int, ShopItemInfo> _farmShop;

	private static Dictionary<int, ShopItemInfo> _farmShopById;

	/// <summary>Shop nông trại = ShopID 135 trong DB (Bảng Shop: AValue1 = giá tiền nông trại, AValue2 = DDTMoney, AValue3 = Money).</summary>
	public static ShopItemInfo GetFarmShopItem(int templateId)
	{
		EnsureShop();
		ShopItemInfo item;
		return _farmShop != null && _farmShop.TryGetValue(templateId, out item) ? item : null;
	}

	/// <summary>Client gửi ShopItemInfo.GoodsID = cột ID của bảng Shop (vd 332139135), không phải TemplateID.</summary>
	public static ShopItemInfo GetFarmShopItemByGoodsId(int goodsId)
	{
		EnsureShop();
		ShopItemInfo item;
		return _farmShopById != null && _farmShopById.TryGetValue(goodsId, out item) ? item : null;
	}

	private static void EnsureShop()
	{
		if (_farmShop != null)
		{
			return;
		}
		lock (ShopSync)
		{
			if (_farmShop != null)
			{
				return;
			}
			Dictionary<int, ShopItemInfo> dict = new Dictionary<int, ShopItemInfo>();
			Dictionary<int, ShopItemInfo> dictById = new Dictionary<int, ShopItemInfo>();
			try
			{
				using ProduceBussiness bussiness = new ProduceBussiness();
				foreach (ShopItemInfo item in bussiness.GetALllShop())
				{
					if (item.ShopID == 135)
					{
						if (!dict.ContainsKey(item.TemplateID))
						{
							dict[item.TemplateID] = item;
						}
						dictById[item.ID] = item;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("ManorConfigMgr load shop 135 error", ex);
			}
			_farmShopById = dictById;
			_farmShop = dict;
			Log.Warn("ManorConfigMgr: loaded " + dict.Count + " farm shop items");
		}
	}

	// ------------------------------------------------------------------ ServerConfig keys (Manor*)

	private static readonly object ConfigSync = new object();

	private static Dictionary<string, string> _serverConfig;

	private static void EnsureServerConfig()
	{
		if (_serverConfig != null)
		{
			return;
		}
		lock (ConfigSync)
		{
			if (_serverConfig != null)
			{
				return;
			}
			Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				XDocument doc = LoadRequestXml("ServerConfig.xml");
				if (doc != null && doc.Root != null)
				{
					foreach (XElement e in doc.Root.Descendants("Item"))
					{
						string name = GetStr(e, "Name").Trim();
						string value = GetStr(e, "Value");
						if (name.Length > 0)
						{
							dict[name] = value;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("ManorConfigMgr load ServerConfig error", ex);
			}
			_serverConfig = dict;
			Log.Warn("ManorConfigMgr: loaded " + dict.Count + " ServerConfig keys");
		}
	}

	public static string GetServerConfig(string name)
	{
		EnsureServerConfig();
		string value;
		return _serverConfig.TryGetValue(name, out value) ? value : null;
	}

	private static int GetServerConfigInt(string name, int fallback)
	{
		int v;
		return int.TryParse((GetServerConfig(name) ?? string.Empty).Trim(), out v) ? v : fallback;
	}

	private class GradePrice
	{
		public int Min;
		public int Max;
		public int Price;
	}

	private static List<GradePrice> ParseGradePrices(string raw)
	{
		List<GradePrice> list = new List<GradePrice>();
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
			int min;
			int max;
			int price;
			if (range.Length == 2 && int.TryParse(range[0].Trim(), out min) && int.TryParse(range[1].Trim(), out max) && int.TryParse(kv[1].Trim(), out price))
			{
				list.Add(new GradePrice { Min = min, Max = max, Price = price });
			}
		}
		return list;
	}

	private static int GetPriceByGrades(string configName, int grade)
	{
		List<GradePrice> list = ParseGradePrices(GetServerConfig(configName));
		int best = 0;
		foreach (GradePrice p in list)
		{
			if (grade >= p.Min && grade <= p.Max)
			{
				return p.Price;
			}
			if (grade > p.Max)
			{
				best = p.Price;
			}
		}
		return best;
	}

	/// <summary>Giá thuê thợ (Cỏ Vàng) theo cấp nhân vật — ManorEmploryMoneys.</summary>
	public static int GetHirePrice(int grade)
	{
		EnsureServerConfig();
		return GetPriceByGrades("ManorEmploryMoneys", grade);
	}

	/// <summary>Giá tự chuộc thân / mua đứt (theo cấp) — ManorFreeMoneys.</summary>
	public static int GetRansomPrice(int grade)
	{
		EnsureServerConfig();
		return GetPriceByGrades("ManorFreeMoneys", grade);
	}

	/// <summary>Giá bảo vệ hợp đồng thợ (Xu/ngày) — ManorAddTimeMoneys.</summary>
	public static int GetAddTimePrice(int grade)
	{
		EnsureServerConfig();
		return GetPriceByGrades("ManorAddTimeMoneys", grade);
	}

	public static int OtherNameMoney
	{
		get
		{
			EnsureServerConfig();
			return GetServerConfigInt("ManorOtherNameMoney", 200);
		}
	}

	public static int GiftMoney
	{
		get
		{
			EnsureServerConfig();
			return GetServerConfigInt("ManorGiftMoney", 35);
		}
	}

	public static int BeatLoveCD
	{
		get
		{
			EnsureServerConfig();
			return GetServerConfigInt("ManorBeatLoveCD", 100);
		}
	}

	public static int WorkEventMinute
	{
		get
		{
			EnsureServerConfig();
			return GetServerConfigInt("ManorWorkEventMinute", 20);
		}
	}

	public static int WorkEventRandom
	{
		get
		{
			EnsureServerConfig();
			return GetServerConfigInt("ManorWorkEventRandom", 25);
		}
	}

	public static int WorkDecPhysic
	{
		get
		{
			EnsureServerConfig();
			return GetServerConfigInt("ManorWorkDecPhysic", 3);
		}
	}

	public static int WorkDecFeeling
	{
		get
		{
			EnsureServerConfig();
			return GetServerConfigInt("ManorWorkDecFeeling", 3);
		}
	}

	public static int FarmHavestMinute
	{
		get
		{
			EnsureServerConfig();
			return GetServerConfigInt("FarmHavestMinute", 30);
		}
	}

	public static int TaskBoxId
	{
		get
		{
			EnsureServerConfig();
			return GetServerConfigInt("ManorTaskBoxId", 1122728);
		}
	}

	/// <summary>Toàn bộ template nhiệm vụ bảng tin.</summary>
	public static List<ManorTaskTemplate> GetAllTasks()
	{
		EnsureLoaded();
		return new List<ManorTaskTemplate>(Tasks.Values);
	}
}

public class ManorItemTemplate
{
	public int TemplateID;
	public string Name;
	public string Description;
	public int CategoryID;
	public int Level;
	public int Quality;
	public int MaxCount;
	public string Pic;
	public int NeedLevel;
	public int ReclaimValue;
	public int ReclaimType;
	public int Property1;
	public int Property2;
	public int Property3;
	public int Property4;
	public int Property5;
	public int Property6;
	public int Property7;
	public int Property8;

	/// <summary>Bitmask túi nông trại (bit 1&lt;&lt;bagType).</summary>
	public bool BelongsToBag(int bagType)
	{
		return (Property1 & (1 << bagType)) != 0;
	}
}

public class ManorUpgradeTemplate
{
	public int UpgradeType;
	public int Grade;
	public int NeedGrade1;
	public int NeedGrade2;
	public int NeedItem1;
	public int NeedItemCount1;
	public int NeedItem2;
	public int NeedItemCount2;
	public int PlantSpeedValue;
	public int Style;
	public int WorkMax;
	public int DropId;
	public int UnlockItem;
}

public class ManorLevelTemplate
{
	public int Types;
	public int TemplateId;
	public int Grades;
	public int Data;
	public string TemplateName;
	public string ActiveObj;
	public int Param1;
	public int Param2;
	public int Param3;
	public int Param4;
}

public class ManorTaskTemplate
{
	public int TemplateId;
	public int TaskType;
	public string TaskName;
	public string Condition1;
	public string Condition2;
	public string Condition3;
	public int RewardItemId1;
	public int RewardCount1;
	public int RewardItemId2;
	public int RewardCount2;
	public string TaskDescp;
}

public class ManorPetFoodPrice
{
	public int Count;
	public int Money;
	public int ItemCount;
}
