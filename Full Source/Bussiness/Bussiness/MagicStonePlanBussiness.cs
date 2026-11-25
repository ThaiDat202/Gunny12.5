using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using log4net;
using SqlDataProvider.BaseClass;

namespace Bussiness;

/// <summary>
/// 1 dòng bộ Ma Ngọc (plan) của người chơi — bảng Db_Tank.dbo.Sys_Users_MagicStonePlan.
/// Client mới (magicStone.views.MagicJadeView) hiển thị tối đa 4 bộ + nút "Th\u00EAm";
/// server cấu hình tối đa bằng Server_Config.MagicJadePlanMaxCount (= 6).
/// </summary>
public class MagicStonePlanData
{
	public int PlanID { get; set; }
	public int UserID { get; set; }
	public string PlanName { get; set; }
	public bool IsActive { get; set; }
	public DateTime CreatedAt { get; set; }
}

/// <summary>1 ô ngọc (slot 1..9) trong loadout của 1 bộ Ma Ngọc (bảng Sys_Users_MagicStonePlanItem).</summary>
public class MagicStonePlanItemData
{
	public int Slot { get; set; }
	public int ItemID { get; set; }
	public int TemplateID { get; set; }
}

/// <summary>
/// Truy cập bảng bộ Ma Ngọc + đọc các key cấu hình Ma Ngọc/Plan từ Db_Tank_All.dbo.Server_Config.
/// Thêm mới hoàn toàn (không sửa signature cũ), dùng cùng chuỗi kết nối conString (Db_Tank) như
/// PlayerBussiness, và crosszoneString (Db_Tank_All) để đọc Server_Config.
/// Bảng được tự tạo (idempotent) nếu chưa có — server chạy được ngay sau khi deploy DLL, không cần
/// chạy script SQL thủ công (script vẫn nằm trong báo cáo để Lead/QA kiểm tra).
/// </summary>
public class MagicStonePlanBussiness : BaseBussiness
{
	private const string PlanTable = "Sys_Users_MagicStonePlan";

	private const string CreatePlanTableSql =
		"IF OBJECT_ID('dbo.Sys_Users_MagicStonePlan','U') IS NULL " +
		"BEGIN " +
		"CREATE TABLE dbo.Sys_Users_MagicStonePlan " +
		"(PlanID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sys_Users_MagicStonePlan PRIMARY KEY, " +
		"UserID int NOT NULL, " +
		"PlanName nvarchar(50) NOT NULL CONSTRAINT DF_Sys_Users_MagicStonePlan_PlanName DEFAULT (N'B\u1ED9 1'), " +
		"IsActive bit NOT NULL CONSTRAINT DF_Sys_Users_MagicStonePlan_IsActive DEFAULT (0), " +
		"CreatedAt datetime NOT NULL CONSTRAINT DF_Sys_Users_MagicStonePlan_CreatedAt DEFAULT (GETDATE())); " +
		"CREATE INDEX IX_Sys_Users_MagicStonePlan_UserID ON dbo.Sys_Users_MagicStonePlan(UserID); " +
		"END";

	private const string CreatePlanItemTableSql =
		"IF OBJECT_ID('dbo.Sys_Users_MagicStonePlanItem','U') IS NULL " +
		"BEGIN " +
		"CREATE TABLE dbo.Sys_Users_MagicStonePlanItem " +
		"(PlanID int NOT NULL, " +
		"Slot int NOT NULL, " +
		"ItemID int NOT NULL, " +
		"TemplateID int NOT NULL, " +
		"StrengthenLevel int NOT NULL CONSTRAINT DF_MSPI_Lv DEFAULT (0), " +
		"StrengthenExp int NOT NULL CONSTRAINT DF_MSPI_Exp DEFAULT (0), " +
		"AttackCompose int NOT NULL CONSTRAINT DF_MSPI_Atk DEFAULT (0), " +
		"DefendCompose int NOT NULL CONSTRAINT DF_MSPI_Def DEFAULT (0), " +
		"AgilityCompose int NOT NULL CONSTRAINT DF_MSPI_Agi DEFAULT (0), " +
		"LuckCompose int NOT NULL CONSTRAINT DF_MSPI_Luck DEFAULT (0), " +
		"MagicAttack int NOT NULL CONSTRAINT DF_MSPI_MAtk DEFAULT (0), " +
		"MagicDefence int NOT NULL CONSTRAINT DF_MSPI_MDef DEFAULT (0), " +
		"Hole1 int NOT NULL CONSTRAINT DF_MSPI_H1 DEFAULT (0), " +
		"Hole2 int NOT NULL CONSTRAINT DF_MSPI_H2 DEFAULT (0), " +
		"Hole3 int NOT NULL CONSTRAINT DF_MSPI_H3 DEFAULT (0), " +
		"Hole4 int NOT NULL CONSTRAINT DF_MSPI_H4 DEFAULT (0), " +
		"Hole5 int NOT NULL CONSTRAINT DF_MSPI_H5 DEFAULT (0), " +
		"Hole6 int NOT NULL CONSTRAINT DF_MSPI_H6 DEFAULT (0), " +
		"IsBinds bit NOT NULL CONSTRAINT DF_MSPI_Bind DEFAULT (1), " +
		"CONSTRAINT PK_Sys_Users_MagicStonePlanItem PRIMARY KEY (PlanID, Slot)); " +
		"END";

	// Cac cot snapshot de "giu ngoc gan chat voi bo": khi doi bo, item duoc luu full trang thai vao bo,
	// khong tha ra tui. Cot nao thieu thi ALTER ADD (idempotent).
	private static readonly string[] SnapshotColumns = new string[]
	{
		"StrengthenLevel int NOT NULL CONSTRAINT DF_MSPI_Lv DEFAULT (0)",
		"StrengthenExp int NOT NULL CONSTRAINT DF_MSPI_Exp DEFAULT (0)",
		"AttackCompose int NOT NULL CONSTRAINT DF_MSPI_Atk DEFAULT (0)",
		"DefendCompose int NOT NULL CONSTRAINT DF_MSPI_Def DEFAULT (0)",
		"AgilityCompose int NOT NULL CONSTRAINT DF_MSPI_Agi DEFAULT (0)",
		"LuckCompose int NOT NULL CONSTRAINT DF_MSPI_Luck DEFAULT (0)",
		"MagicAttack int NOT NULL CONSTRAINT DF_MSPI_MAtk DEFAULT (0)",
		"MagicDefence int NOT NULL CONSTRAINT DF_MSPI_MDef DEFAULT (0)",
		"Hole1 int NOT NULL CONSTRAINT DF_MSPI_H1 DEFAULT (0)",
		"Hole2 int NOT NULL CONSTRAINT DF_MSPI_H2 DEFAULT (0)",
		"Hole3 int NOT NULL CONSTRAINT DF_MSPI_H3 DEFAULT (0)",
		"Hole4 int NOT NULL CONSTRAINT DF_MSPI_H4 DEFAULT (0)",
		"Hole5 int NOT NULL CONSTRAINT DF_MSPI_H5 DEFAULT (0)",
		"Hole6 int NOT NULL CONSTRAINT DF_MSPI_H6 DEFAULT (0)",
		"IsBinds bit NOT NULL CONSTRAINT DF_MSPI_Bind DEFAULT (1)"
	};

	private static void EnsureSnapshotColumns(Sql_DbObject dbo)
	{
		string[] columns = new string[]
		{
			"StrengthenLevel","StrengthenExp","AttackCompose","DefendCompose","AgilityCompose","LuckCompose",
			"MagicAttack","MagicDefence","Hole1","Hole2","Hole3","Hole4","Hole5","Hole6","IsBinds"
		};
		for (int i = 0; i < columns.Length; i++)
		{
			dbo.Exesqlcomm("IF COL_LENGTH('dbo.Sys_Users_MagicStonePlanItem','" + columns[i] + "') IS NULL ALTER TABLE dbo.Sys_Users_MagicStonePlanItem ADD " + SnapshotColumns[i]);
		}
	}

	public const string ConfigPlanMaxCount = "MagicJadePlanMaxCount";
	public const string ConfigPlanBuyPoint = "PlanBuyPoint";
	public const string ConfigLockPoints = "MagicJadeLockPoints";
	public const string ConfigAttrRandoms = "MagicJadeAttrRandoms";
	public const string ConfigSkill1 = "MagicJadeSkill1";

	private static readonly ILog PlanLog = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private static readonly object SyncRoot = new object();
	private static bool _tableEnsured;
	private static Dictionary<string, string> _serverConfig;
	private static Sql_DbObject _configDb;

	// Sql_DbObject của project dispose luôn SqlConnection sau GetDataTableBySqlcomm, nên KHÔNG thể
	// tái sử dụng 1 instance cho nhiều thao tác (read rồi write). Mỗi thao tác DB dùng 1 instance mới.
	private static Sql_DbObject NewDb()
	{
		return new Sql_DbObject("AppConfig", "conString");
	}

	private static readonly int[] DefaultLockPoints = new int[4] { 1000, 2000, 5000, 10000 };
	private static readonly int[] DefaultAttrRandoms = new int[14]
	{
		800, 2500, 500, 2200, 500, 2500, 800, 1500, 800, 1500, 800, 1500, 800, 1500
	};

	/// <summary>Tạo bảng bộ Ma Ngọc nếu chưa có (chạy 1 lần / process, an toàn đa luồng).</summary>
	public void EnsurePlanTable()
	{
		if (_tableEnsured)
		{
			return;
		}
		lock (SyncRoot)
		{
			if (_tableEnsured)
			{
				return;
			}
			try
			{
				using Sql_DbObject dbo = NewDb();
				dbo.Exesqlcomm(CreatePlanTableSql);
				dbo.Exesqlcomm(CreatePlanItemTableSql);
			}
			catch (Exception ex)
			{
				PlanLog.Error("MagicStonePlanBussiness.EnsurePlanTable error", ex);
			}
			_tableEnsured = true;
		}
	}

	/// <summary>Đọc toàn bộ Server_Config (Db_Tank_All) một lần, cache trong process.</summary>
	private Dictionary<string, string> GetServerConfigMap()
	{
		lock (SyncRoot)
		{
			if (_serverConfig != null)
			{
				return _serverConfig;
			}
			Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				_configDb = new Sql_DbObject("AppConfig", "crosszoneString");
				DataTable table = _configDb.GetDataTableBySqlcomm("Server_Config", "SELECT Name, Value FROM dbo.Server_Config");
				if (table != null)
				{
					foreach (DataRow row in table.Rows)
					{
						string name = Convert.ToString(row["Name"]).Trim();
						if (name.Length > 0)
						{
							map[name] = Convert.ToString(row["Value"]).Trim();
						}
					}
				}
			}
			catch (Exception ex)
			{
				PlanLog.Error("MagicStonePlanBussiness.GetServerConfigMap error", ex);
			}
			_serverConfig = map;
			return _serverConfig;
		}
	}

	public string GetServerConfig(string name, string fallback)
	{
		if (string.IsNullOrEmpty(name))
		{
			return fallback;
		}
		Dictionary<string, string> map = GetServerConfigMap();
		if (map.TryGetValue(name, out string value) && !string.IsNullOrEmpty(value))
		{
			return value;
		}
		return fallback;
	}

	public int GetServerConfigInt(string name, int fallback)
	{
		string raw = GetServerConfig(name, null);
		if (raw != null && int.TryParse(raw.Trim(), out int result))
		{
			return result;
		}
		return fallback;
	}

	public int[] GetServerConfigIntArray(string name, int[] fallback)
	{
		string raw = GetServerConfig(name, null);
		if (string.IsNullOrEmpty(raw))
		{
			return fallback;
		}
		List<int> list = new List<int>();
		string[] parts = raw.Split(',');
		for (int i = 0; i < parts.Length; i++)
		{
			if (int.TryParse(parts[i].Trim(), out int value))
			{
				list.Add(value);
			}
		}
		return (list.Count > 0) ? list.ToArray() : fallback;
	}

	public int GetPlanMaxCount()
	{
		return Math.Max(1, GetServerConfigInt(ConfigPlanMaxCount, 6));
	}

	public int GetPlanBuyPoint()
	{
		return Math.Max(0, GetServerConfigInt(ConfigPlanBuyPoint, 1000));
	}

	public int[] GetLockPoints()
	{
		return GetServerConfigIntArray(ConfigLockPoints, DefaultLockPoints);
	}

	/// <summary>
	/// Trọng số random property type 1..14 (MagicJadeAttrRandoms). Nếu dữ liệu không đủ 14 phần tử
	/// thì trả về null để caller dùng random đều.
	/// </summary>
	public int[] GetAttrRandoms()
	{
		int[] values = GetServerConfigIntArray(ConfigAttrRandoms, DefaultAttrRandoms);
		return (values != null && values.Length >= 14) ? values : null;
	}

	/// <summary>Số nhóm kỹ năng có cấu hình (MagicsJadeSkill1 = các nhóm cách nhau bởi '|'). Mặc định 4.</summary>
	public int GetSkillGroupCount()
	{
		string raw = GetServerConfig(ConfigSkill1, "");
		int groups = 0;
		if (!string.IsNullOrEmpty(raw))
		{
			string[] parts = raw.Split('|');
			for (int i = 0; i < parts.Length; i++)
			{
				if (!string.IsNullOrEmpty(parts[i].Trim()))
				{
					groups++;
				}
			}
		}
		if (groups < 1)
		{
			groups = 4;
		}
		return Math.Min(groups, 6);
	}

	private static List<MagicStonePlanData> ReadPlans(DataTable table)
	{
		List<MagicStonePlanData> list = new List<MagicStonePlanData>();
		if (table == null)
		{
			return list;
		}
		foreach (DataRow row in table.Rows)
		{
			list.Add(new MagicStonePlanData
			{
				PlanID = Convert.ToInt32(row["PlanID"]),
				UserID = Convert.ToInt32(row["UserID"]),
				PlanName = Convert.ToString(row["PlanName"]),
				IsActive = Convert.ToBoolean(row["IsActive"]),
				CreatedAt = Convert.ToDateTime(row["CreatedAt"])
			});
		}
		return list;
	}

	/// <summary>
	/// Lấy danh sách bộ ngọc của user. Nếu user chưa có bộ nào thì tạo bộ mặc định "B\u1ED9 1" active.
	/// Nếu có bộ nhưng không bộ nào active thì set bộ đầu tiên làm active.
	/// </summary>
	public List<MagicStonePlanData> GetPlans(int userId, bool ensureDefault)
	{
		EnsurePlanTable();
		List<MagicStonePlanData> plans = QueryPlans(userId);
		if (plans.Count == 0)
		{
			if (!ensureDefault)
			{
				return plans;
			}
			InsertPlan(userId, "B\u1ED9 1", true);
			return QueryPlans(userId);
		}
		if (ensureDefault && !plans.Exists(p => p.IsActive))
		{
			SetActivePlan(userId, plans[0].PlanID);
			plans = QueryPlans(userId);
		}
		return plans;
	}

	public List<MagicStonePlanData> QueryPlans(int userId)
	{
		string sql = "SELECT PlanID, UserID, PlanName, IsActive, CreatedAt FROM dbo.Sys_Users_MagicStonePlan WHERE UserID = " + userId + " ORDER BY PlanID";
		try
		{
			DataTable table;
			using (Sql_DbObject dbo = NewDb())
			{
				table = dbo.GetDataTableBySqlcomm(PlanTable, sql);
			}
			return ReadPlans(table);
		}
		catch (Exception ex)
		{
			PlanLog.Error("MagicStonePlanBussiness.QueryPlans error userID=" + userId, ex);
			return new List<MagicStonePlanData>();
		}
	}

	public bool InsertPlan(int userId, string planName, bool isActive)
	{
		EnsurePlanTable();
		if (isActive)
		{
			UpdateAllInactive(userId);
		}
		string sql = "INSERT INTO dbo.Sys_Users_MagicStonePlan (UserID, PlanName, IsActive, CreatedAt) VALUES (@uid, @name, @active, GETDATE())";
		SqlParameter[] parameters = new SqlParameter[3]
		{
			new SqlParameter("@uid", SqlDbType.Int) { Value = userId },
			new SqlParameter("@name", SqlDbType.NVarChar, 50) { Value = planName ?? "" },
			new SqlParameter("@active", SqlDbType.Bit) { Value = isActive }
		};
		try
		{
			using (Sql_DbObject dbo = NewDb())
			{
				if (!dbo.Exesqlcomm(sql, parameters))
				{
					return false;
				}
			}
			// Verify thực tế: row đã được tạo (Exesqlcomm không trả rowcount).
			List<MagicStonePlanData> plans = QueryPlans(userId);
			return plans.Exists(p => p.PlanName == planName);
		}
		catch (Exception ex)
		{
			PlanLog.Error("MagicStonePlanBussiness.InsertPlan error userID=" + userId, ex);
			return false;
		}
	}

	private void UpdateAllInactive(int userId)
	{
		string sql = "UPDATE dbo.Sys_Users_MagicStonePlan SET IsActive = 0 WHERE UserID = @uid";
		SqlParameter[] parameters = new SqlParameter[1]
		{
			new SqlParameter("@uid", SqlDbType.Int) { Value = userId }
		};
		try
		{
			using Sql_DbObject dbo = NewDb();
			dbo.Exesqlcomm(sql, parameters);
		}
		catch (Exception ex)
		{
			PlanLog.Error("MagicStonePlanBussiness.UpdateAllInactive error userID=" + userId, ex);
		}
	}

	/// <summary>Đặt 1 bộ làm active. Chỉ áp dụng khi planID thuộc về userId.</summary>
	public bool SetActivePlan(int userId, int planId)
	{
		EnsurePlanTable();
		string sql = "UPDATE dbo.Sys_Users_MagicStonePlan SET IsActive = CASE WHEN PlanID = @pid THEN 1 ELSE 0 END WHERE UserID = @uid";
		SqlParameter[] parameters = new SqlParameter[2]
		{
			new SqlParameter("@uid", SqlDbType.Int) { Value = userId },
			new SqlParameter("@pid", SqlDbType.Int) { Value = planId }
		};
		try
		{
			using (Sql_DbObject dbo = NewDb())
			{
				if (!dbo.Exesqlcomm(sql, parameters))
				{
					return false;
				}
			}
			List<MagicStonePlanData> plans = QueryPlans(userId);
			return plans.Exists(p => p.PlanID == planId && p.IsActive);
		}
		catch (Exception ex)
		{
			PlanLog.Error("MagicStonePlanBussiness.SetActivePlan error userID=" + userId + " planID=" + planId, ex);
			return false;
		}
	}

	public bool RenamePlan(int userId, int planId, string planName)
	{
		EnsurePlanTable();
		if (string.IsNullOrEmpty(planName))
		{
			return false;
		}
		string sql = "UPDATE dbo.Sys_Users_MagicStonePlan SET PlanName = @name WHERE UserID = @uid AND PlanID = @pid";
		SqlParameter[] parameters = new SqlParameter[3]
		{
			new SqlParameter("@uid", SqlDbType.Int) { Value = userId },
			new SqlParameter("@pid", SqlDbType.Int) { Value = planId },
			new SqlParameter("@name", SqlDbType.NVarChar, 50) { Value = planName }
		};
		try
		{
			using (Sql_DbObject dbo = NewDb())
			{
				if (!dbo.Exesqlcomm(sql, parameters))
				{
					return false;
				}
			}
			// Verify thực tế: tên đã đổi + bộ thuộc đúng user (Exesqlcomm không trả rowcount).
			List<MagicStonePlanData> plans = QueryPlans(userId);
			return plans.Exists(p => p.PlanID == planId && p.PlanName == planName);
		}
		catch (Exception ex)
		{
			PlanLog.Error("MagicStonePlanBussiness.RenamePlan error userID=" + userId + " planID=" + planId, ex);
			return false;
		}
	}

	// ------------------------------------------------------------------ loadout (9 ô ngọc / bộ)
	private static bool _itemTableEnsured;

	/// <summary>Tạo bảng loadout item nếu chưa có (idempotent).</summary>
	public void EnsurePlanItemTable()
	{
		if (_itemTableEnsured)
		{
			return;
		}
		lock (SyncRoot)
		{
			if (_itemTableEnsured)
			{
				return;
			}
			try
			{
				using Sql_DbObject dbo = NewDb();
				dbo.Exesqlcomm(CreatePlanItemTableSql);
				EnsureSnapshotColumns(dbo);
			}
			catch (Exception ex)
			{
				PlanLog.Error("MagicStonePlanBussiness.EnsurePlanItemTable error", ex);
			}
			_itemTableEnsured = true;
		}
	}

	/// <summary>Đọc loadout 1 bộ: Slot (1..9) -&gt; ItemID.</summary>
	public Dictionary<int, int> GetPlanLoadout(int planId)
	{
		EnsurePlanItemTable();
		Dictionary<int, int> map = new Dictionary<int, int>();
		string sql = "SELECT Slot, ItemID FROM dbo.Sys_Users_MagicStonePlanItem WHERE PlanID = " + planId + " ORDER BY Slot";
		try
		{
			DataTable table;
			using (Sql_DbObject dbo = NewDb())
			{
				table = dbo.GetDataTableBySqlcomm("Sys_Users_MagicStonePlanItem", sql);
			}
			if (table != null)
			{
				foreach (DataRow row in table.Rows)
				{
					int slot = Convert.ToInt32(row["Slot"]);
					int itemId = Convert.ToInt32(row["ItemID"]);
					if (slot >= 1 && slot <= 9 && itemId > 0)
					{
						map[slot] = itemId;
					}
				}
			}
		}
		catch (Exception ex)
		{
			PlanLog.Error("MagicStonePlanBussiness.GetPlanLoadout error planID=" + planId, ex);
		}
		return map;
	}

	/// <summary>
	/// Lưu loadout 1 bộ (xoá cũ + insert mới). Chỉ cho phép bộ thuộc đúng user.
	/// items rỗng -&gt; xoá hết loadout của bộ.
	/// </summary>
	public bool SavePlanLoadout(int userId, int planId, List<MagicStonePlanItemData> items)
	{
		EnsurePlanItemTable();
		if (planId <= 0 || userId <= 0)
		{
			return false;
		}
		if (QueryPlans(userId).Find(p => p.PlanID == planId) == null)
		{
			return false;
		}
		try
		{
			using (Sql_DbObject dbo = NewDb())
			{
				dbo.Exesqlcomm("DELETE FROM dbo.Sys_Users_MagicStonePlanItem WHERE PlanID = @pid",
					new SqlParameter[1] { new SqlParameter("@pid", SqlDbType.Int) { Value = planId } });
			}
		}
		catch (Exception ex)
		{
			PlanLog.Error("MagicStonePlanBussiness.SavePlanLoadout delete error planID=" + planId, ex);
			return false;
		}
		if (items == null || items.Count == 0)
		{
			return true;
		}
		try
		{
			using Sql_DbObject dbo = NewDb();
			foreach (MagicStonePlanItemData it in items)
			{
				if (it == null || it.Slot < 1 || it.Slot > 9 || it.ItemID <= 0)
				{
					continue;
				}
				dbo.Exesqlcomm(
					"INSERT INTO dbo.Sys_Users_MagicStonePlanItem (PlanID, Slot, ItemID, TemplateID) VALUES (@pid, @slot, @item, @tpl)",
					new SqlParameter[4]
					{
						new SqlParameter("@pid", SqlDbType.Int) { Value = planId },
						new SqlParameter("@slot", SqlDbType.Int) { Value = it.Slot },
						new SqlParameter("@item", SqlDbType.Int) { Value = it.ItemID },
						new SqlParameter("@tpl", SqlDbType.Int) { Value = it.TemplateID }
					});
			}
			return true;
		}
		catch (Exception ex)
		{
			PlanLog.Error("MagicStonePlanBussiness.SavePlanLoadout insert error planID=" + planId, ex);
			return false;
		}
	}

	// ------------------------------------------------------------------ snapshot (giu ngoc gan chat voi bo)

	/// <summary>Snapshot 1 o ngoc cua bo: giu full trang thai item de khi kich hoat lai tao item y nguyen.</summary>
	public class MagicStonePlanSnapshot
	{
		public int Slot;
		public int TemplateID;
		public int StrengthenLevel;
		public int StrengthenExp;
		public int AttackCompose;
		public int DefendCompose;
		public int AgilityCompose;
		public int LuckCompose;
		public int MagicAttack;
		public int MagicDefence;
		public int Hole1;
		public int Hole2;
		public int Hole3;
		public int Hole4;
		public int Hole5;
		public int Hole6;
		public bool IsBinds;
	}

	public List<MagicStonePlanSnapshot> GetPlanSnapshots(int planId)
	{
		EnsurePlanItemTable();
		List<MagicStonePlanSnapshot> list = new List<MagicStonePlanSnapshot>();
		string sql = "SELECT Slot, TemplateID, StrengthenLevel, StrengthenExp, AttackCompose, DefendCompose, " +
			"AgilityCompose, LuckCompose, MagicAttack, MagicDefence, Hole1, Hole2, Hole3, Hole4, Hole5, Hole6, IsBinds " +
			"FROM dbo.Sys_Users_MagicStonePlanItem WHERE PlanID = " + planId + " ORDER BY Slot";
		try
		{
			DataTable table;
			using (Sql_DbObject dbo = NewDb())
			{
				table = dbo.GetDataTableBySqlcomm("Sys_Users_MagicStonePlanItem", sql);
			}
			if (table != null)
			{
				foreach (DataRow row in table.Rows)
				{
					int slot = Convert.ToInt32(row["Slot"]);
					if (slot < 1 || slot > 9)
					{
						continue;
					}
					list.Add(new MagicStonePlanSnapshot
					{
						Slot = slot,
						TemplateID = Convert.ToInt32(row["TemplateID"]),
						StrengthenLevel = Convert.ToInt32(row["StrengthenLevel"]),
						StrengthenExp = Convert.ToInt32(row["StrengthenExp"]),
						AttackCompose = Convert.ToInt32(row["AttackCompose"]),
						DefendCompose = Convert.ToInt32(row["DefendCompose"]),
						AgilityCompose = Convert.ToInt32(row["AgilityCompose"]),
						LuckCompose = Convert.ToInt32(row["LuckCompose"]),
						MagicAttack = Convert.ToInt32(row["MagicAttack"]),
						MagicDefence = Convert.ToInt32(row["MagicDefence"]),
						Hole1 = Convert.ToInt32(row["Hole1"]),
						Hole2 = Convert.ToInt32(row["Hole2"]),
						Hole3 = Convert.ToInt32(row["Hole3"]),
						Hole4 = Convert.ToInt32(row["Hole4"]),
						Hole5 = Convert.ToInt32(row["Hole5"]),
						Hole6 = Convert.ToInt32(row["Hole6"]),
						IsBinds = Convert.ToBoolean(row["IsBinds"])
					});
				}
			}
		}
		catch (Exception ex)
		{
			PlanLog.Error("MagicStonePlanBussiness.GetPlanSnapshots error planID=" + planId, ex);
		}
		return list;
	}

	public bool SavePlanSnapshots(int userId, int planId, List<MagicStonePlanSnapshot> list)
	{
		EnsurePlanItemTable();
		if (userId <= 0 || planId <= 0)
		{
			return false;
		}
		if (QueryPlans(userId).Find(p => p.PlanID == planId) == null)
		{
			return false;
		}
		try
		{
			using (Sql_DbObject dbo = NewDb())
			{
				dbo.Exesqlcomm("DELETE FROM dbo.Sys_Users_MagicStonePlanItem WHERE PlanID = @pid",
					new SqlParameter[1] { new SqlParameter("@pid", SqlDbType.Int) { Value = planId } });
			}
		}
		catch (Exception ex)
		{
			PlanLog.Error("MagicStonePlanBussiness.SavePlanSnapshots delete error planID=" + planId, ex);
			return false;
		}
		if (list == null || list.Count == 0)
		{
			return true;
		}
		try
		{
			using Sql_DbObject dbo = NewDb();
			foreach (MagicStonePlanSnapshot s in list)
			{
				if (s == null || s.Slot < 1 || s.Slot > 9 || s.TemplateID <= 0)
				{
					continue;
				}
				dbo.Exesqlcomm(
					"INSERT INTO dbo.Sys_Users_MagicStonePlanItem " +
					"(PlanID, Slot, ItemID, TemplateID, StrengthenLevel, StrengthenExp, AttackCompose, DefendCompose, " +
					"AgilityCompose, LuckCompose, MagicAttack, MagicDefence, Hole1, Hole2, Hole3, Hole4, Hole5, Hole6, IsBinds) " +
					"VALUES (@pid, @slot, 0, @tpl, @lv, @exp, @atk, @def, @agi, @luck, @matk, @mdef, @h1, @h2, @h3, @h4, @h5, @h6, @bind)",
					new SqlParameter[18]
					{
						new SqlParameter("@pid", SqlDbType.Int) { Value = planId },
						new SqlParameter("@slot", SqlDbType.Int) { Value = s.Slot },
						new SqlParameter("@tpl", SqlDbType.Int) { Value = s.TemplateID },
						new SqlParameter("@lv", SqlDbType.Int) { Value = s.StrengthenLevel },
						new SqlParameter("@exp", SqlDbType.Int) { Value = s.StrengthenExp },
						new SqlParameter("@atk", SqlDbType.Int) { Value = s.AttackCompose },
						new SqlParameter("@def", SqlDbType.Int) { Value = s.DefendCompose },
						new SqlParameter("@agi", SqlDbType.Int) { Value = s.AgilityCompose },
						new SqlParameter("@luck", SqlDbType.Int) { Value = s.LuckCompose },
						new SqlParameter("@matk", SqlDbType.Int) { Value = s.MagicAttack },
						new SqlParameter("@mdef", SqlDbType.Int) { Value = s.MagicDefence },
						new SqlParameter("@h1", SqlDbType.Int) { Value = s.Hole1 },
						new SqlParameter("@h2", SqlDbType.Int) { Value = s.Hole2 },
						new SqlParameter("@h3", SqlDbType.Int) { Value = s.Hole3 },
						new SqlParameter("@h4", SqlDbType.Int) { Value = s.Hole4 },
						new SqlParameter("@h5", SqlDbType.Int) { Value = s.Hole5 },
						new SqlParameter("@h6", SqlDbType.Int) { Value = s.Hole6 },
						new SqlParameter("@bind", SqlDbType.Bit) { Value = s.IsBinds }
					});
			}
			return true;
		}
		catch (Exception ex)
		{
			PlanLog.Error("MagicStonePlanBussiness.SavePlanSnapshots insert error planID=" + planId, ex);
			return false;
		}
	}
}
