using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using log4net;

namespace Game.Server.Elf;

/// <summary>
/// Cấu hình "Linh Bảo" (elf equipment) cho hệ Thần Hộ Mệnh.
///
/// Client 12.6 load file <c>WebSite\Request/TS_LinkPalTemplate.xml</c> (zlib) và dựng
/// <c>ElfModel._equipTemp[type][quality][level]</c>; mỗi dòng có:
///   Id, TemplateID (item template 480xx), Type (1..6), Quality (1..5), Level (1..50),
///   Attack/Defence/Agility/Lucky/Damage/Blood, SkillID, ItemTempId1/Param1, ItemTempId2/Param2.
///
/// Server đọc CHÍNH file đó (cùng nguồn với client) để:
///  - map item TemplateID (CategoryID=91 trong túi) -&gt; (Type, Quality) khi người chơi
///    kích hoạt Linh Bảo (707/3),
///  - validate + trừ nguyên liệu khi nâng cấp (707/4),
///  - tính chỉ số / giới hạn cấp.
/// </summary>
public static class ElfEquipConfigMgr
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private static readonly object SyncRoot = new object();

	private static bool _loaded;

	/// <summary>Id (1-based) -&gt; dòng cấu hình.</summary>
	private static readonly Dictionary<int, ElfEquipLevelInfo> _byId = new Dictionary<int, ElfEquipLevelInfo>();

	/// <summary>(type, quality, level) -&gt; dòng cấu hình.</summary>
	private static readonly Dictionary<long, ElfEquipLevelInfo> _byTypeQualityLevel = new Dictionary<long, ElfEquipLevelInfo>();

	/// <summary>Item TemplateID (480xx) -&gt; dòng level 1 (để suy ra Type/Quality).</summary>
	private static readonly Dictionary<int, ElfEquipLevelInfo> _itemTemplateBase = new Dictionary<int, ElfEquipLevelInfo>();

	/// <summary>(type, quality) -&gt; level lớn nhất có trong cấu hình.</summary>
	private static readonly Dictionary<int, int> _maxLevel = new Dictionary<int, int>();

	private static string RequestDir
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

	public static bool IsLoaded
	{
		get { return _loaded; }
	}

	public static int Count
	{
		get
		{
			EnsureLoaded();
			return _byId.Count;
		}
	}

	/// <summary>Nạp cấu hình Linh Bảo (idempotent, lazy).</summary>
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
				Load();
			}
			catch (Exception exception)
			{
				Log.Error("ElfEquipConfigMgr load error", exception);
			}
			finally
			{
				_loaded = true;
			}
		}
	}

	private static void Load()
	{
		string path = Path.Combine(RequestDir, "TS_LinkPalTemplate.xml");
		if (!File.Exists(path) && Directory.Exists(RequestDir))
		{
			string[] files = Directory.GetFiles(RequestDir, "*.xml");
			foreach (string candidate in files)
			{
				if (string.Equals(Path.GetFileName(candidate), "TS_LinkPalTemplate.xml", StringComparison.OrdinalIgnoreCase))
				{
					path = candidate;
					break;
				}
			}
		}
		if (!File.Exists(path))
		{
			Log.Warn("ElfEquipConfigMgr: missing file " + path);
			return;
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
		XDocument doc = XDocument.Parse(Encoding.UTF8.GetString(xml));
		if (doc.Root == null)
		{
			return;
		}
		foreach (XElement item in doc.Root.Descendants())
		{
			if (!string.Equals(item.Name.LocalName, "Item", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			ElfEquipLevelInfo info = new ElfEquipLevelInfo
			{
				Id = GetInt(item, "Id"),
				TemplateId = GetInt(item, "TemplateID"),
				Type = GetInt(item, "Type"),
				Quality = GetInt(item, "Quality"),
				Level = GetInt(item, "Level"),
				Attack = GetInt(item, "Attack"),
				Defence = GetInt(item, "Defence"),
				Agility = GetInt(item, "Agility"),
				Lucky = GetInt(item, "Lucky"),
				Damage = GetInt(item, "Damage"),
				Blood = GetInt(item, "Blood"),
				Hp = GetInt(item, "Hp"),
				MagicAttack = GetInt(item, "MagicAttack"),
				MagicDefence = GetInt(item, "MagicDefence"),
				Crit = GetInt(item, "Crit"),
				SunderArmor = GetInt(item, "SunderArmor"),
				ViolenceInjury = GetInt(item, "ViolenceInjury"),
				Speed = GetInt(item, "Speed"),
				TricKill = GetInt(item, "TricKill"),
				Armor = GetInt(item, "Armor"),
				SkillId = GetInt(item, "SkillID"),
				ItemTempId1 = GetInt(item, "ItemTempId1"),
				Param1 = GetInt(item, "Param1"),
				ItemTempId2 = GetInt(item, "ItemTempId2"),
				Param2 = GetInt(item, "Param2")
			};
			if (info.Id <= 0 || info.TemplateId <= 0 || info.Type <= 0 || info.Quality <= 0 || info.Level <= 0)
			{
				continue;
			}
			_byId[info.Id] = info;
			_byTypeQualityLevel[PackKey(info.Type, info.Quality, info.Level)] = info;
			if (info.Level == 1)
			{
				_itemTemplateBase[info.TemplateId] = info;
			}
			int maxKey = PackTypeQuality(info.Type, info.Quality);
			if (!_maxLevel.TryGetValue(maxKey, out int max) || info.Level > max)
			{
				_maxLevel[maxKey] = info.Level;
			}
		}
		Log.Warn(string.Format("ElfEquipConfigMgr: loaded {0} LinkPal rows, {1} item templates, {2} type/quality groups",
			_byId.Count, _itemTemplateBase.Count, _maxLevel.Count));
	}

	private static int GetInt(XElement element, string name)
	{
		XAttribute attribute = element.Attribute(name);
		if (attribute == null)
		{
			return 0;
		}
		int value;
		return int.TryParse(attribute.Value.Trim(), out value) ? value : 0;
	}

	private static long PackKey(int type, int quality, int level)
	{
		return ((long)type << 32) | ((long)quality << 16) | (uint)level;
	}

	private static int PackTypeQuality(int type, int quality)
	{
		return (type << 8) | quality;
	}

	/// <summary>Item TemplateID (CategoryID=91) -&gt; (Type, Quality).</summary>
	public static bool TryGetTypeQuality(int itemTemplateId, out int type, out int quality)
	{
		EnsureLoaded();
		type = 0;
		quality = 0;
		if (!_itemTemplateBase.TryGetValue(itemTemplateId, out ElfEquipLevelInfo info))
		{
			return false;
		}
		type = info.Type;
		quality = info.Quality;
		return true;
	}

	/// <summary>Lấy dòng cấu hình theo (type, quality, level); null nếu không có.</summary>
	public static ElfEquipLevelInfo GetLevelInfo(int type, int quality, int level)
	{
		EnsureLoaded();
		_byTypeQualityLevel.TryGetValue(PackKey(type, quality, level), out ElfEquipLevelInfo info);
		return info;
	}

	/// <summary>Level tối đa của một (type, quality); 0 nếu không có cấu hình.</summary>
	public static int GetMaxLevel(int type, int quality)
	{
		EnsureLoaded();
		return _maxLevel.TryGetValue(PackTypeQuality(type, quality), out int max) ? max : 0;
	}

	/// <summary>Các quality có cấu hình cho 1 type.</summary>
	public static List<int> GetQualities(int type)
	{
		EnsureLoaded();
		List<int> result = new List<int>();
		foreach (KeyValuePair<int, int> pair in _maxLevel)
		{
			if ((pair.Key >> 8) == type)
			{
				result.Add(pair.Key & 0xFF);
			}
		}
		result.Sort();
		return result;
	}

	/// <summary>Lấy dòng cấu hình kế tiếp (level + 1); null nếu đã max level.</summary>
	public static ElfEquipLevelInfo GetNextLevelInfo(int type, int quality, int currentLevel)
	{
		return GetLevelInfo(type, quality, currentLevel + 1);
	}
}

/// <summary>Một dòng TS_LinkPalTemplate.xml (1 cấp của 1 Linh Bảo).</summary>
public class ElfEquipLevelInfo
{
	public int Id;
	public int TemplateId;
	public int Type;
	public int Quality;
	public int Level;
	public int Attack;
	public int Defence;
	public int Agility;
	public int Lucky;
	public int Damage;
	public int Blood;
	public int Hp;
	public int MagicAttack;
	public int MagicDefence;
	public int Crit;
	public int SunderArmor;
	public int ViolenceInjury;
	public int Speed;
	public int TricKill;
	public int Armor;
	public int SkillId;
	public int ItemTempId1;
	public int Param1;
	public int ItemTempId2;
	public int Param2;
}
