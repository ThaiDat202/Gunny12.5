using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Bussiness.Managers;
using log4net;

namespace Game.Server.Pet;

/// <summary>
/// Cấu hình mở rộng cho hệ thống PET của client 12.6:
///  - Số ô pet mở sẵn (miễn phí) + tổng số ô pet tối đa.
///  - Bảng giá mở ô pet (PetCellUnlockPrice.xml) — dùng chung với client.
///  - Bảng nâng cấp trang bị thức tỉnh (TS_PetEquipAwakenSuper.xml).
///  - Id đá thức tỉnh / dung hợp (ServerConfig.xml: PetEquipSuperAwakenConfig).
///
/// Dữ liệu đọc từ thư mục WebSite\Request (một số file nén zlib — dùng
/// Game.Base.Marshal.Uncompress giống ManorConfigMgr).
/// </summary>
public static class PetExtConfigMgr
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private static readonly object SyncRoot = new object();

	private static bool _loaded;

	/// <summary>Số ô pet mở sẵn miễn phí (mặc định 8).</summary>
	private static int _freeCellCount = 8;

	/// <summary>Tổng số ô pet tối đa (mặc định 50, khớp client 10 trang x 5 ô).</summary>
	private static int _maxPetCellCount = 50;

	/// <summary>Giá mở ô theo priceIndex (1 = ô đầu tiên phải trả tiền).</summary>
	private static Dictionary<int, int> _cellPrices = new Dictionary<int, int>();

	/// <summary>EquipID -&gt; (NewEquipID, NeedCount) cho thức tỉnh sâu / dung hợp.</summary>
	private static Dictionary<int, AwakenSuperEntry> _awakenSuper = new Dictionary<int, AwakenSuperEntry>();

	/// <summary>
	/// DesignRow (TemplateID % 1000, 101..130) -> Pet KindID của bộ trang bị pet.
	/// Nạp từ WebSite\Request/PetEquipKindMap.xml. Dùng để chặn trang bị pet CHƯA có record
	/// thức tỉnh nhưng rõ ràng thuộc 1 loài pet (vd "Vũ Khí Rùa Thức Tỉnh" -> Huyền Vũ kind 18).
	/// </summary>
	private static Dictionary<int, int> _petEquipKindMap = new Dictionary<int, int>();

	private static int _superAwakenStoneId = 12570;

	private static int _blendStoneId = 12269;

	private class AwakenSuperEntry
	{
		public int NewEquipId;

		public int NeedCount;
	}

	private static readonly int[] Quality6Cat50 = BuildRange(30101, 36);

	private static readonly int[] Quality6Cat51 = BuildRange(31101, 36);

	private static readonly int[] Quality6Cat52 = BuildRange(32101, 36);

	/// <summary>Chữ số cuối TemplateID -&gt; Pet KindID (theo code pet cũ).</summary>
	private static int MapKindByLastDigit(int digit)
	{
		return digit switch
		{
			1 => 5, 
			2 => 9, 
			3 => 6, 
			4 => 7, 
			5 => 11, 
			6 => 10, 
			7 => 12, 
			8 => 15, 
			9 => 27, 
			_ => -1
		};
	}

	public static int FreeCellCount
	{
		get
		{
			EnsureLoaded();
			return _freeCellCount;
		}
	}

	public static int MaxPetCellCount
	{
		get
		{
			EnsureLoaded();
			return _maxPetCellCount;
		}
	}

	/// <summary>Id đá dùng cho Thức Tỉnh Sâu (tab thứ 2) — mặc định 12570.</summary>
	public static int SuperAwakenStoneId
	{
		get
		{
			EnsureLoaded();
			return _superAwakenStoneId;
		}
	}

	/// <summary>Id đá dùng cho Dung Hợp (tab thứ 3) — mặc định 12269.</summary>
	public static int BlendStoneId
	{
		get
		{
			EnsureLoaded();
			return _blendStoneId;
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
				LoadPetConfig();
				LoadCellPrices();
				LoadAwakenSuper();
				LoadAwakenStones();
				LoadPetEquipKindMap();
				Log.Warn(string.Format("PetExtConfigMgr: loaded freeCells={0}, maxCells={1}, prices={2}, awakenSuper={3}, equipKindMap={4}, superStone={5}, blendStone={6}", _freeCellCount, _maxPetCellCount, _cellPrices.Count, _awakenSuper.Count, _petEquipKindMap.Count, _superAwakenStoneId, _blendStoneId));
			}
			catch (Exception exception)
			{
				Log.Error("PetExtConfigMgr load error", exception);
			}
			finally
			{
				_loaded = true;
			}
		}
	}

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

	private static XDocument LoadRequestXml(string fileName)
	{
		string path = Path.Combine(RequestDir, fileName);
		if (!File.Exists(path))
		{
			string dir = RequestDir;
			if (Directory.Exists(dir))
			{
				string[] files = Directory.GetFiles(dir, "*.xml");
				foreach (string candidate in files)
				{
					if (string.Equals(Path.GetFileName(candidate), fileName, StringComparison.OrdinalIgnoreCase))
					{
						path = candidate;
						break;
					}
				}
			}
		}
		if (!File.Exists(path))
		{
			Log.Warn("PetExtConfigMgr: missing file " + path);
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
		try
		{
			return XDocument.Parse(Encoding.UTF8.GetString(xml));
		}
		catch (Exception exception)
		{
			Log.Error("PetExtConfigMgr: parse fail " + path, exception);
			return null;
		}
	}

	private static bool IsItemElement(XElement element)
	{
		return string.Equals(element.Name.LocalName, "item", StringComparison.OrdinalIgnoreCase);
	}

	private static int GetIntAttribute(XElement element, string name, int fallback = -1)
	{
		XAttribute attribute = element.Attribute(name);
		if (attribute == null)
		{
			return fallback;
		}
		int value;
		return int.TryParse(attribute.Value.Trim(), out value) ? value : fallback;
	}

	private static void LoadPetConfig()
	{
		XDocument doc = LoadRequestXml("petconfiginfo.xml");
		if (doc == null || doc.Root == null)
		{
			return;
		}
		foreach (XElement item in doc.Root.Descendants())
		{
			if (!IsItemElement(item))
			{
				continue;
			}
			int free = GetIntAttribute(item, "MaxFreePetCount");
			if (free >= 0)
			{
				_freeCellCount = free;
			}
			int max = GetIntAttribute(item, "MaxPetCount");
			if (max > 0)
			{
				_maxPetCellCount = max;
			}
		}
		if (_freeCellCount < 0)
		{
			_freeCellCount = 8;
		}
		if (_maxPetCellCount < _freeCellCount)
		{
			_maxPetCellCount = _freeCellCount;
		}
	}

	/// <summary>
	/// Bảng giá mở ô pet: client 12.6 đọc từ TS_UpgradeTemplate.xml với các dòng
	/// Types="103" (Grades = priceIndex bắt đầu từ 1, Data = giá Xu).
	/// Dự phòng: PetCellUnlockPrice.xml (format riêng) nếu file chính không có dòng 103.
	/// </summary>
	private static void LoadCellPrices()
	{
		Dictionary<int, int> prices = new Dictionary<int, int>();
		XDocument doc = LoadRequestXml("TS_UpgradeTemplate.xml");
		if (doc != null && doc.Root != null)
		{
			foreach (XElement item in doc.Root.Descendants())
			{
				if (!IsItemElement(item))
				{
					continue;
				}
				int type = GetIntAttribute(item, "Types");
				if (type != 103)
				{
					continue;
				}
				int grades = GetIntAttribute(item, "Grades");
				int data = GetIntAttribute(item, "Data");
				if (grades > 0 && data >= 0)
				{
					prices[grades] = data;
				}
			}
		}
		if (prices.Count == 0)
		{
			XDocument fallback = LoadRequestXml("PetCellUnlockPrice.xml");
			if (fallback != null && fallback.Root != null)
			{
				foreach (XElement item in fallback.Root.Descendants())
				{
					if (!IsItemElement(item))
					{
						continue;
					}
					int id = GetIntAttribute(item, "ID");
					int money = GetIntAttribute(item, "money");
					if (id > 0 && money >= 0)
					{
						prices[id] = money;
					}
				}
			}
		}
		if (prices.Count == 0)
		{
			Log.Warn("PetExtConfigMgr: khong doc duoc bang gia mo o pet (TS_UpgradeTemplate Types=103 / PetCellUnlockPrice.xml)");
		}
		_cellPrices = prices;
	}

	private static void LoadAwakenSuper()
	{
		Dictionary<int, AwakenSuperEntry> mapping = new Dictionary<int, AwakenSuperEntry>();
		XDocument doc = LoadRequestXml("TS_PetEquipAwakenSuper.xml");
		if (doc != null && doc.Root != null)
		{
			foreach (XElement item in doc.Root.Descendants())
			{
				if (!IsItemElement(item))
				{
					continue;
				}
				int equipId = GetIntAttribute(item, "EquipID");
				int newEquipId = GetIntAttribute(item, "NewEquipID");
				int needCount = GetIntAttribute(item, "NeedCount");
				if (equipId > 0 && newEquipId > 0 && needCount > 0)
				{
					mapping[equipId] = new AwakenSuperEntry
					{
						NewEquipId = newEquipId,
						NeedCount = needCount
					};
				}
			}
		}
		else
		{
			Log.Warn("PetExtConfigMgr: TS_PetEquipAwakenSuper.xml not found - khong the nang cap trang bi thuc tinh");
		}
		_awakenSuper = mapping;
	}

	private static void LoadAwakenStones()
	{
		try
		{
			string raw = Game.Server.Farm.ManorConfigMgr.GetServerConfig("PetEquipSuperAwakenConfig");
			if (!string.IsNullOrEmpty(raw))
			{
				string[] parts = raw.Split(',');
				int stone;
				if (parts.Length > 0 && int.TryParse(parts[0].Trim(), out stone) && stone > 0)
				{
					_superAwakenStoneId = stone;
				}
				if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out stone) && stone > 0)
				{
					_blendStoneId = stone;
				}
				return;
			}
		}
		catch (Exception exception)
		{
			Log.Warn("PetExtConfigMgr: ServerConfig PetEquipSuperAwakenConfig not available", exception);
		}
		try
		{
			string awakenGood = Game.Server.Farm.ManorConfigMgr.GetServerConfig("PetAwakenGoodId");
			int stone;
			if (!string.IsNullOrEmpty(awakenGood) && int.TryParse(awakenGood.Trim(), out stone) && stone > 0)
			{
				_superAwakenStoneId = stone;
			}
		}
		catch (Exception exception2)
		{
			Log.Warn("PetExtConfigMgr: ServerConfig PetAwakenGoodId not available", exception2);
		}
	}

	/// <summary>Giá Xu để mở ô tiếp theo; priceIndex bắt đầu từ 1 (ô đầu tiên phải trả tiền).</summary>
	/// <summary>
	/// Nạp bảng DesignRow -> Pet KindID cho trang bị pet (PetEquipKindMap.xml).
	/// Dòng 101..108 là bộ trang bị gốc (Vuốt Rồng, Sừng Noel, ...) theo quy ước cũ;
	/// dòng 109..130 là trang bị của từng loài pet mới (tên vật phẩm ghi rõ loài).
	/// Dòng không có trong file sẽ được fallback theo quy ước cũ (nếu 101..108) hoặc -1.
	/// </summary>
	private static void LoadPetEquipKindMap()
	{
		Dictionary<int, int> mapping = new Dictionary<int, int>();
		XDocument doc = LoadRequestXml("PetEquipKindMap.xml");
		if (doc != null && doc.Root != null)
		{
			foreach (XElement item in doc.Root.Descendants())
			{
				string localName = item.Name.LocalName;
				if (!string.Equals(localName, "item", StringComparison.OrdinalIgnoreCase) && !string.Equals(localName, "Row", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				int designRow = GetIntAttribute(item, "DesignRow");
				if (designRow < 100 || designRow > 999)
				{
					int templateId = GetIntAttribute(item, "TemplateID");
					if (templateId > 0)
					{
						designRow = templateId % 1000;
					}
				}
				int kindId = GetIntAttribute(item, "KindID");
				if (designRow >= 100 && kindId > 0)
				{
					mapping[designRow] = kindId;
				}
			}
		}
		if (mapping.Count == 0)
		{
			Log.Warn("PetExtConfigMgr: PetEquipKindMap.xml not found/empty - chỉ chặn được trang bị thức tỉnh và dòng 01..08");
		}
		_petEquipKindMap = mapping;
	}

	public static int GetCellPrice(int priceIndex)
	{
		EnsureLoaded();
		int money;
		if (priceIndex > 0 && _cellPrices.TryGetValue(priceIndex, out money))
		{
			return money;
		}
		return 0;
	}

	/// <summary>
	/// Tổng Xu cần trả để mở thêm <paramref name="times"/> ô, bắt đầu từ trạng thái
	/// đã mở <paramref name="unlockedCount"/> ô. Công thức khớp client:
	/// priceIndex đầu tiên = unlockedCount - FreeCellCount + 1.
	/// Trả 0 nếu cấu hình giá chưa đủ (handler sẽ báo lỗi).
	/// </summary>
	public static int GetUnlockCost(int unlockedCount, int times)
	{
		EnsureLoaded();
		if (times <= 0)
		{
			return 0;
		}
		int priceIndex = unlockedCount - _freeCellCount + 1;
		if (priceIndex < 1)
		{
			priceIndex = 1;
		}
		long total = 0L;
		for (int i = 0; i < times; i++)
		{
			int price = GetCellPrice(priceIndex + i);
			if (price <= 0)
			{
				return 0;
			}
			total += price;
		}
		if (total <= 0L)
		{
			return 0;
		}
		return (total > int.MaxValue) ? int.MaxValue : ((int)total);
	}

	/// <summary>Lấy mapping nâng cấp trang bị thức tỉnh (EquipID -&gt; NewEquipID/NeedCount).</summary>
	public static bool TryGetSuperAwaken(int equipTemplateId, out int newEquipId, out int needCount)
	{
		EnsureLoaded();
		newEquipId = 0;
		needCount = 0;
		AwakenSuperEntry entry;
		if (equipTemplateId > 0 && _awakenSuper.TryGetValue(equipTemplateId, out entry))
		{
			newEquipId = entry.NewEquipId;
			needCount = entry.NeedCount;
			return true;
		}
		return false;
	}

	/// <summary>
	/// Suy ra Pet KindID từ chữ số cuối TemplateID của trang bị thức tỉnh.
	/// Trả -1 nếu không map được (các TemplateID tận cùng 0).
	/// </summary>
	public static int GetAwakenKindId(int equipTemplateId)
	{
		EnsureLoaded();
		if (equipTemplateId <= 0)
		{
			return -1;
		}
		return MapKindByLastDigit(equipTemplateId % 10);
	}

	/// <summary>Danh sách 30 template trang bị pet Quality=6 theo CategoryID (50/51/52).</summary>
	public static int[] GetQuality6EquipIds(int categoryId)
	{
		EnsureLoaded();
		switch (categoryId)
		{
		case 50:
			return Quality6Cat50;
		case 51:
			return Quality6Cat51;
		case 52:
			return Quality6Cat52;
		default:
			return null;
		}
	}

	/// <summary>Random 1 skill thức tỉnh của KindID; trả 0 nếu KindID không có skill thức tỉnh.</summary>
	public static int GetRandomAwakenSkill(int kindId)
	{
		EnsureLoaded();
		if (kindId <= 0)
		{
			return 0;
		}
		try
		{
			return PetSpecialSkillMgr.GetSkillAwaken(kindId);
		}
		catch (Exception exception)
		{
			Log.Warn("PetExtConfigMgr: GetSkillAwaken fail kindId=" + kindId, exception);
			return 0;
		}
	}

	/// <summary>
	/// Có phải skill thức tỉnh không (Pet_Special_Skills.IsAwaken = 1).
	/// Dùng để đánh dấu exclusiveID trong chuỗi Skill của pet — client dựa vào đó
	/// vẽ viền vàng (asset.petsBag.awaken.awakenSkillBorder) cho skill thức tỉnh.
	/// </summary>
	public static bool IsAwakenSkillId(int skillId)
	{
		if (skillId <= 0)
		{
			return false;
		}
		try
		{
			var list = PetSpecialSkillMgr.m_petSpecialSkills;
			if (list == null)
			{
				return false;
			}
			for (int i = 0; i < list.Count; i++)
			{
				var skill = list[i];
				if (skill != null && skill.SkillID == skillId && skill.IsAwaken)
				{
					return true;
				}
			}
		}
		catch (Exception exception)
		{
			Log.Warn("PetExtConfigMgr: IsAwakenSkillId fail skillId=" + skillId, exception);
		}
		return false;
	}

	/// <summary>
	/// Suy ra pet KindID của 1 trang bị pet CHƯA có record thức tỉnh, dựa vào quy ước
	/// chữ số cuối TemplateID cho dòng đầu tiên (2 chữ số cuối 01..09).
	/// Ví dụ 30101/31101/32101/140101/150101/160101 -&gt; KindID 5.
	/// Trả -1 nếu không xác định được (các ID tận 0 hoặc dòng ngoài quy ước).
	/// </summary>
	public static int GetPetEquipKindId(int templateId)
	{
		EnsureLoaded();
		if (templateId <= 0)
		{
			return -1;
		}
		int index = templateId % 1000;
		int kindId;
		if (_petEquipKindMap.TryGetValue(index, out kindId) && kindId > 0)
		{
			return kindId;
		}
		// Fallback cho bộ trang bị gốc dòng 01..08 (chưa có trong PetEquipKindMap.xml).
		if (index >= 101 && index <= 108)
		{
			return MapKindByLastDigit(index % 10);
		}
		return -1;
	}

	private static int[] BuildRange(int start, int count)
	{
		int[] array = new int[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = start + i;
		}
		return array;
	}
}
