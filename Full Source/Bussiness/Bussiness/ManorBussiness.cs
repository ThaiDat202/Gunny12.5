using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using SqlDataProvider.Data;

namespace Bussiness;

/// <summary>
/// Tầng truy cập dữ liệu cho Nông trại 2.0 (Gunny 12.6) — các bảng:
///   dbo.Sys_User_Manor       (trạng thái tổng: exp/money/value/expvalue/buff)
///   dbo.Sys_User_ManorArea   (6 khu type 1..6)
///   dbo.Sys_User_ManorGoods  (kho nông sản)
///   dbo.Sys_User_ManorWorker (thợ)
///   dbo.Sys_User_ManorTask   (nhiệm vụ bảng tin theo ngày — phase 2.2)
///
/// Phase 2.2 bổ sung: task CRUD (81/18-19-25), tra cứu thợ theo WorkerUserID
/// (thuê / mua đứt / sa thải), danh sách ứng viên tuyển thợ 81/30
/// (JOIN Sys_Users_Detail + LEFT JOIN Sys_User_ManorWorker).
///
/// Quy ước:
///  - API là static cho tiện gọi từ handler (theo yêu cầu task-2: static GetManor).
///    Mỗi lần gọi tạo 1 instance BaseBussiness riêng rồi dispose — an toàn đa luồng,
///    không cần người gọi new/dispose.
///  - Đọc dữ liệu : SELECT inline + SqlDataReader (tham số là int đã validate, an toàn injection).
///  - Ghi dữ liệu : INSERT/UPDATE/DELETE inline có SqlParameter.
///  - Mọi lỗi (SQL/connection) đều được try/catch + ghi log qua BaseBussiness.log,
///    trả null (object) / false (bool) / list rỗng (list) — không ném exception ra handler.
///  - DateTime nhỏ hơn 1753-01-01 (DateTime.MinValue) được chuẩn hoá về 1900-01-01
///    để không bị lỗi "SqlDateTime overflow" của SQL Server.
///
/// Schema: data\sql\manor_tables.sql + data\sql\manor_task_table.sql (GunnyPanel).
/// </summary>
public class ManorBussiness : BaseBussiness
{
	private const int AreaTypeMin = 1;
	private const int AreaTypeMax = 6;

	private const string SqlSelectManor =
		"SELECT UserID, Exp, Money, [Value], ExpValue, BuyFoodCount, BuffRate, BuffDate, ProtectDate, BuyFoodDate, LastUpdate "
		+ "FROM dbo.Sys_User_Manor WHERE UserID = {0}";

	private const string SqlSelectAreas =
		"SELECT ID, UserID, AreaType, [Level], LevelTwo, IsPlant, PlantTempId, PlantDate, RipeDate, "
		+ "FoodTempId, FoodDate, ProduceCount, LastUpdate "
		+ "FROM dbo.Sys_User_ManorArea WHERE UserID = {0} ORDER BY AreaType";

	private const string SqlSelectArea =
		"SELECT ID, UserID, AreaType, [Level], LevelTwo, IsPlant, PlantTempId, PlantDate, RipeDate, "
		+ "FoodTempId, FoodDate, ProduceCount, LastUpdate "
		+ "FROM dbo.Sys_User_ManorArea WHERE UserID = {0} AND AreaType = {1}";

	private const string SqlSelectAreaId =
		"SELECT ID FROM dbo.Sys_User_ManorArea WHERE UserID = {0} AND AreaType = {1}";

	private const string SqlSelectGoods =
		"SELECT ID, UserID, TemplateId, [Property], [Count] "
		+ "FROM dbo.Sys_User_ManorGoods WHERE UserID = {0} ORDER BY TemplateId, [Property]";

	private const string SqlSelectGoodsId =
		"SELECT ID FROM dbo.Sys_User_ManorGoods WHERE UserID = {0} AND TemplateId = {1} AND [Property] = {2}";

	private const string WorkerSelectColumns =
		"ID, OwnerID, WorkerUserID, [Name], [Title], Feeling, Energy, FarmExp, FishExp, MineExp, "
		+ "RanchExp, IsWorking, WorkType, WorkBeginDate, EmployProtectDate, ProtectPrice, Sex, Colors, Style, HiredDate";

	private const string SqlSelectWorkers =
		"SELECT " + WorkerSelectColumns + " FROM dbo.Sys_User_ManorWorker WHERE OwnerID = {0} ORDER BY ID";

	private const string SqlSelectWorkerId =
		"SELECT ID FROM dbo.Sys_User_ManorWorker WHERE OwnerID = {0} AND WorkerUserID = {1}";

	private const string SqlSelectWorker =
		"SELECT " + WorkerSelectColumns + " FROM dbo.Sys_User_ManorWorker WHERE OwnerID = {0} AND WorkerUserID = {1}";

	private const string SqlSelectWorkerByWorkerUserID =
		"SELECT TOP 1 " + WorkerSelectColumns + " FROM dbo.Sys_User_ManorWorker WHERE WorkerUserID = {0} ORDER BY ID";

	private const string SqlSelectWorkersByWorkerUserID =
		"SELECT " + WorkerSelectColumns + " FROM dbo.Sys_User_ManorWorker WHERE WorkerUserID = {0} ORDER BY ID";

	private const string SqlCountWorkingWorkers =
		"SELECT COUNT(*) FROM dbo.Sys_User_ManorWorker WHERE OwnerID = {0} AND IsWorking = 1";

	private const string SqlCountWorkingWorkersByType =
		"SELECT COUNT(*) FROM dbo.Sys_User_ManorWorker WHERE OwnerID = {0} AND IsWorking = 1 AND WorkType = {1}";

	private const string SqlSelectUserNickName =
		"SELECT NickName FROM dbo.Sys_Users_Detail WHERE UserID = {0}";

	private const string SqlSelectManorTasks =
		"SELECT ID, UserID, TaskDate, TaskId, TemplateId, Condition1, Condition2, Condition3, IsComplete, IsReward, UpdateDate "
		+ "FROM dbo.Sys_User_ManorTask WHERE UserID = {0} AND TaskDate = CONVERT(date, '{1}', 23) ORDER BY ID";

	private const string SqlSelectManorTaskId =
		"SELECT ID FROM dbo.Sys_User_ManorTask WHERE UserID = {0} AND TaskDate = CONVERT(date, '{1}', 23) AND TaskId = {2}";

	/// <summary>
	/// Ứng viên tuyển thợ (81/30): user (trừ chính mình) LEFT JOIN bảng thợ để biết đang bị ai thuê.
	/// {0} = excludeUserId (int), {1} = keyword đã escape SQL (string), {2} = limit (int đã validate).
	/// </summary>
	private const string SqlSelectManorCandidates =
		"SELECT TOP {2} u.UserID, ISNULL(u.NickName, N'') AS NickName, u.Grade, u.Sex, u.Colors, u.Style, "
		+ "ISNULL(w.OwnerID, 0) AS OwnerID, ISNULL(pm.ProtectDate, CONVERT(datetime, '19000101')) AS ProtectDate "
		+ "FROM dbo.Sys_Users_Detail u LEFT JOIN dbo.Sys_User_ManorWorker w ON w.WorkerUserID = u.UserID "
		+ "LEFT JOIN dbo.Sys_User_Manor pm ON pm.UserID = u.UserID "
		+ "WHERE u.UserID <> {0} AND ('{1}' = N'' OR u.NickName LIKE N'%' + N'{1}' + N'%') "
		+ "ORDER BY u.Grade DESC, u.UserID";

	private const int CandidateKeywordMaxLength = 50;

	private const int CandidateLimitDefault = 20;

	private const int CandidateLimitMax = 100;

	private static readonly DateTime SqlMinDate = new DateTime(1900, 1, 1);

	private static readonly DateTime SqlMinValidDate = new DateTime(1753, 1, 1);

	#region API static — Manor

	/// <summary>Lấy trạng thái nông trại của user; trả null nếu chưa có dòng dữ liệu.</summary>
	public static UserManorInfo GetManor(int userId)
	{
		if (userId <= 0)
		{
			return null;
		}
		return Execute("GetManor", (ManorBussiness b) => b.GetManorCore(userId), null);
	}

	/// <summary>Thêm mới dòng nông trại (lỗi/false nếu đã tồn tại). Với user mới nên dùng <see cref="CreateDefaultManor"/>.</summary>
	public static bool AddManor(UserManorInfo info)
	{
		if (info == null || info.UserID <= 0)
		{
			return false;
		}
		return Execute("AddManor", (ManorBussiness b) => b.InsertManor(info), false);
	}

	/// <summary>Cập nhật toàn bộ trạng thái nông trại (Exp/Money/Value/ExpValue/BuyFoodCount/Buff...).</summary>
	public static bool UpdateManor(UserManorInfo info)
	{
		if (info == null || info.UserID <= 0)
		{
			return false;
		}
		return Execute("UpdateManor", (ManorBussiness b) => b.UpdateManorCore(info), false);
	}

	/// <summary>Upsert: có dòng thì UPDATE, chưa có thì INSERT.</summary>
	public static bool SaveManor(UserManorInfo info)
	{
		if (info == null || info.UserID <= 0)
		{
			return false;
		}
		return Execute("SaveManor", (ManorBussiness b) => b.SaveManorCore(info), false);
	}

	#endregion

	#region API static — Area

	/// <summary>Lấy danh sách 6 khu của user (sắp xếp theo AreaType).</summary>
	public static List<UserManorAreaInfo> GetAreas(int userId)
	{
		if (userId <= 0)
		{
			return new List<UserManorAreaInfo>();
		}
		return Execute("GetAreas", (ManorBussiness b) => b.GetAreasCore(userId), new List<UserManorAreaInfo>());
	}

	/// <summary>Lấy 1 khu theo AreaType (1..6); null nếu chưa có.</summary>
	public static UserManorAreaInfo GetArea(int userId, int areaType)
	{
		if (userId <= 0 || areaType < AreaTypeMin || areaType > AreaTypeMax)
		{
			return null;
		}
		return Execute("GetArea", (ManorBussiness b) => b.GetAreaCore(userId, areaType), null);
	}

	/// <summary>Lưu 1 khu: ID &lt;= 0 thì INSERT (hoặc UPDATE theo UserID+AreaType), ngược lại UPDATE theo ID.</summary>
	public static bool SaveArea(UserManorAreaInfo info)
	{
		if (info == null || info.UserID <= 0 || info.AreaType < AreaTypeMin || info.AreaType > AreaTypeMax)
		{
			return false;
		}
		return Execute("SaveArea", (ManorBussiness b) => b.SaveAreaCore(info), false);
	}

	#endregion

	#region API static — Goods

	/// <summary>Lấy toàn bộ kho nông sản của user.</summary>
	public static List<UserManorGoodsInfo> GetGoods(int userId)
	{
		if (userId <= 0)
		{
			return new List<UserManorGoodsInfo>();
		}
		return Execute("GetGoods", (ManorBussiness b) => b.GetGoodsCore(userId), new List<UserManorGoodsInfo>());
	}

	/// <summary>Lưu 1 loại nông sản: ID &lt;= 0 thì INSERT/UPDATE theo (UserID, TemplateId, Property), ngược lại UPDATE theo ID.</summary>
	public static bool SaveGoods(UserManorGoodsInfo info)
	{
		if (info == null || info.UserID <= 0)
		{
			return false;
		}
		return Execute("SaveGoods", (ManorBussiness b) => b.SaveGoodsCore(info), false);
	}

	/// <summary>Xoá 1 dòng nông sản theo ID.</summary>
	public static bool DeleteGoods(int id)
	{
		if (id <= 0)
		{
			return false;
		}
		return Execute("DeleteGoods", (ManorBussiness b) => b.DeleteGoodsCore(id), false);
	}

	/// <summary>Xoá 1 dòng nông sản theo khoá unique (UserID, TemplateId, Property).</summary>
	public static bool DeleteGoods(int userId, int templateId, int property)
	{
		if (userId <= 0)
		{
			return false;
		}
		return Execute("DeleteGoods", (ManorBussiness b) => b.DeleteGoodsCore(userId, templateId, property), false);
	}

	#endregion

	#region API static — Worker

	/// <summary>Lấy danh sách thợ của nông trại (theo OwnerID).</summary>
	public static List<UserManorWorkerInfo> GetWorkers(int ownerId)
	{
		if (ownerId <= 0)
		{
			return new List<UserManorWorkerInfo>();
		}
		return Execute("GetWorkers", (ManorBussiness b) => b.GetWorkersCore(ownerId), new List<UserManorWorkerInfo>());
	}

	/// <summary>Lưu 1 thợ: ID &lt;= 0 thì INSERT/UPDATE theo (OwnerID, WorkerUserID), ngược lại UPDATE theo ID.</summary>
	public static bool SaveWorker(UserManorWorkerInfo info)
	{
		if (info == null || info.OwnerID <= 0 || info.WorkerUserID <= 0)
		{
			return false;
		}
		return Execute("SaveWorker", (ManorBussiness b) => b.SaveWorkerCore(info), false);
	}

	/// <summary>Xoá 1 thợ theo ID (đuổi thợ).</summary>
	public static bool DeleteWorker(int id)
	{
		if (id <= 0)
		{
			return false;
		}
		return Execute("DeleteWorker", (ManorBussiness b) => b.DeleteWorkerCore(id), false);
	}

	/// <summary>Xoá 1 thợ theo khoá unique (OwnerID, WorkerUserID).</summary>
	public static bool DeleteWorker(int ownerId, int workerUserID)
	{
		if (ownerId <= 0 || workerUserID <= 0)
		{
			return false;
		}
		return Execute("DeleteWorker", (ManorBussiness b) => b.DeleteWorkerCore(ownerId, workerUserID), false);
	}

	#endregion

	#region API static — Task (nhiệm vụ bảng tin 81/18 · 81/19 · 81/25)

	/// <summary>
	/// Lấy danh sách task bảng tin của user trong 1 ngày (TaskDate = <paramref name="date"/>.Date).
	/// Trả list rỗng nếu chưa có / lỗi DB.
	/// </summary>
	public static List<UserManorTaskInfo> GetManorTasks(int userId, DateTime date)
	{
		if (userId <= 0)
		{
			return new List<UserManorTaskInfo>();
		}
		DateTime taskDate = date.Date;
		return Execute("GetManorTasks", (ManorBussiness b) => b.GetManorTasksCore(userId, taskDate), new List<UserManorTaskInfo>());
	}

	/// <summary>
	/// Lưu 1 task: ID &gt; 0 thì UPDATE theo ID; ID &lt;= 0 thì tìm dòng cũ theo
	/// (UserID, TaskDate, TaskId) để UPDATE, chưa có mới INSERT rồi gán <see cref="UserManorTaskInfo.ID"/>.
	/// TaskDate được chuẩn hoá về 00:00:00 (date.Date); nếu chưa set (MinValue) thì dùng hôm nay.
	/// UpdateDate tự set = DateTime.Now.
	/// </summary>
	public static bool SaveManorTask(UserManorTaskInfo info)
	{
		if (info == null || info.UserID <= 0 || info.TaskId <= 0)
		{
			return false;
		}
		return Execute("SaveManorTask", (ManorBussiness b) => b.SaveManorTaskCore(info), false);
	}

	/// <summary>Xoá toàn bộ task của user trong 1 ngày (reset bảng tin cuối ngày / tạo lại list).</summary>
	public static bool DeleteManorTasks(int userId, DateTime date)
	{
		if (userId <= 0)
		{
			return false;
		}
		DateTime taskDate = date.Date;
		return Execute("DeleteManorTasks", (ManorBussiness b) => b.DeleteManorTasksCore(userId, taskDate), false);
	}

	#endregion

	#region API static — Tra cứu thợ & ứng viên tuyển thợ (phase 2.2)

	/// <summary>
	/// Lấy dòng thợ đầu tiên (ORDER BY ID) theo WorkerUserID — dùng khi cần biết
	/// "user này đang làm thợ cho ai". Null nếu chưa làm thợ ở đâu.
	/// </summary>
	public static UserManorWorkerInfo GetWorkerByWorkerUserID(int workerUserId)
	{
		if (workerUserId <= 0)
		{
			return null;
		}
		return Execute("GetWorkerByWorkerUserID", (ManorBussiness b) => b.GetWorkerByWorkerUserIDCore(workerUserId), null);
	}

	/// <summary>Lấy toàn bộ dòng thợ của 1 user (trường hợp dữ liệu cũ bị nhiều chủ) — ORDER BY ID.</summary>
	public static List<UserManorWorkerInfo> GetWorkersByWorkerUserID(int workerUserId)
	{
		if (workerUserId <= 0)
		{
			return new List<UserManorWorkerInfo>();
		}
		return Execute("GetWorkersByWorkerUserID", (ManorBussiness b) => b.GetWorkersByWorkerUserIDCore(workerUserId), new List<UserManorWorkerInfo>());
	}

	/// <summary>Lấy 1 dòng thợ theo khoá unique (OwnerID, WorkerUserID); null nếu chưa có.</summary>
	public static UserManorWorkerInfo GetWorker(int ownerId, int workerUserId)
	{
		if (ownerId <= 0 || workerUserId <= 0)
		{
			return null;
		}
		return Execute("GetWorker", (ManorBussiness b) => b.GetWorkerCore(ownerId, workerUserId), null);
	}

	/// <summary>
	/// Danh sách ứng viên để thuê thợ (81/30 WORKER_LIST): tìm theo từ khoá tên
	/// (chuỗi rỗng = tất cả), loại trừ chính mình, sắp xếp Grade giảm dần.
	/// <paramref name="limit"/> &lt;= 0 dùng mặc định 20, tối đa 100.
	/// OwnerID = 0: chưa có chủ (thuê thường); khác 0: đang làm cho chủ khác (mua đứt).
	/// </summary>
	public static List<ManorCandidateInfo> GetManorCandidates(int excludeUserId, string keyword, int limit)
	{
		int safeLimit = ((limit <= 0) ? CandidateLimitDefault : limit);
		if (safeLimit > CandidateLimitMax)
		{
			safeLimit = CandidateLimitMax;
		}
		string safeKeyword = NormalizeString(keyword, CandidateKeywordMaxLength).Trim();
		return Execute("GetManorCandidates", (ManorBussiness b) => b.GetManorCandidatesCore(excludeUserId, safeKeyword, safeLimit), new List<ManorCandidateInfo>());
	}

	/// <summary>Lấy NickName trong Sys_Users_Detail; trả chuỗi rỗng nếu không tìm thấy / lỗi.</summary>
	public static string GetUserNickName(int userId)
	{
		if (userId <= 0)
		{
			return string.Empty;
		}
		return Execute("GetUserNickName", (ManorBussiness b) => b.GetUserNickNameCore(userId), string.Empty);
	}

	/// <summary>
	/// Đếm số thợ đang làm việc (IsWorking = 1) của nông trại; <paramref name="workType"/> &gt; 0
	/// thì đếm thêm theo WorkType (1 Nông Dân, 2 Dân Chài, 3 Thợ Mỏ, 4 Chăn Nuôi). Lỗi DB trả 0.
	/// </summary>
	public static int GetManorWorkerCount(int ownerId, int workType)
	{
		if (ownerId <= 0)
		{
			return 0;
		}
		return Execute("GetManorWorkerCount", (ManorBussiness b) => b.GetManorWorkerCountCore(ownerId, workType), 0);
	}

	#endregion

	#region API static — Khởi tạo mặc định

	/// <summary>
	/// Tạo dữ liệu nông trại mặc định cho user mới (idempotent — gọi lại vô hại):
	///   - dòng Sys_User_Manor (Exp=0, Money=0, Value=0, ExpValue=0, BuffRate=0, BuffDate=1900-01-01)
	///   - 6 dòng Sys_User_ManorArea AreaType 1..6: Level=1, LevelTwo=1 cho type 1 &amp; 3,
	///     IsPlant=false, PlantTempId/FoodTempId=0, ProduceCount=0 (thiếu type nào tạo type đó)
	///   - goods/workers: để trống, không tạo.
	/// Trả true khi dữ liệu đã sẵn sàng (tạo mới hoặc đã tồn tại), false khi lỗi DB.
	/// </summary>
	public static bool CreateDefaultManor(int userId)
	{
		if (userId <= 0)
		{
			return false;
		}
		return Execute("CreateDefaultManor", (ManorBussiness b) => b.CreateDefaultManorCore(userId), false);
	}

	/// <summary>Bí danh của <see cref="CreateDefaultManor"/> (tên theo mô tả task-2: Manor_CreateDefault).</summary>
	public static bool Manor_CreateDefault(int userId)
	{
		return CreateDefaultManor(userId);
	}

	#endregion

	#region Hạ tầng: tạo instance + try/catch + log giống pattern PlayerBussiness

	/// <summary>
	/// Tạo 1 ManorBussiness (mở Sql_DbObject theo AppConfig/conString), chạy action rồi dispose.
	/// Mọi exception được log lại (BaseBussiness.log) và trả về giá trị onError.
	/// </summary>
	private static T Execute<T>(string methodName, Func<ManorBussiness, T> action, T onError)
	{
		try
		{
			using (ManorBussiness manorBussiness = new ManorBussiness())
			{
				return action(manorBussiness);
			}
		}
		catch (Exception exception)
		{
			if (log != null && log.IsErrorEnabled)
			{
				log.Error("ManorBussiness." + methodName, exception);
			}
			return onError;
		}
	}

	private static DateTime NormalizeDate(DateTime value)
	{
		return (value < SqlMinValidDate) ? SqlMinDate : value;
	}

	/// <summary>Định dạng ngày theo yyyy-MM-dd (invariant) để nhúng vào SQL dạng DATE.</summary>
	private static string FormatSqlDate(DateTime value)
	{
		return value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
	}

	private static string NormalizeString(string value, int maxLength)
	{
		string text = value ?? string.Empty;
		if (maxLength > 0 && text.Length > maxLength)
		{
			return text.Substring(0, maxLength);
		}
		return text;
	}

	/// <summary>
	/// Chuẩn hoá + escape chuỗi trước khi nhúng vào SQL literal (nhân đôi dấu nháy đơn).
	/// Chỉ dùng cho tham số kiểu chuỗi không truyền được qua SqlParameter (FillSqlDataReader).
	/// </summary>
	private static string EscapeSqlString(string value, int maxLength)
	{
		return NormalizeString(value, maxLength).Replace("'", "''");
	}

	private static int ReaderInt(SqlDataReader reader, string column)
	{
		object value = reader[column];
		if (value == null || value == DBNull.Value)
		{
			return 0;
		}
		return Convert.ToInt32(value);
	}

	private static bool ReaderBool(SqlDataReader reader, string column)
	{
		return ReaderInt(reader, column) != 0;
	}

	private static string ReaderString(SqlDataReader reader, string column)
	{
		object value = reader[column];
		if (value == null || value == DBNull.Value)
		{
			return string.Empty;
		}
		return value.ToString();
	}

	private static string ReaderString(SqlDataReader reader, int ordinal)
	{
		object value = reader.GetValue(ordinal);
		if (value == null || value == DBNull.Value)
		{
			return string.Empty;
		}
		return value.ToString();
	}

	private static DateTime ReaderDateTime(SqlDataReader reader, string column)
	{
		object value = reader[column];
		if (value == null || value == DBNull.Value)
		{
			return SqlMinDate;
		}
		return Convert.ToDateTime(value);
	}

	/// <summary>Chạy SELECT trả về 1 giá trị int đầu tiên (dùng lấy ID identity sau INSERT).</summary>
	private int QueryInt(string sql)
	{
		SqlDataReader resultDataReader = null;
		try
		{
			if (!db.FillSqlDataReader(ref resultDataReader, sql))
			{
				return 0;
			}
			if (resultDataReader != null && resultDataReader.Read())
			{
				return ReaderInt(resultDataReader, 0);
			}
			return 0;
		}
		finally
		{
			if (resultDataReader != null && !resultDataReader.IsClosed)
			{
				resultDataReader.Close();
			}
		}
	}

	/// <summary>Chạy SELECT trả về 1 giá trị string đầu tiên (dùng lấy NickName...).</summary>
	private string QueryString(string sql)
	{
		SqlDataReader resultDataReader = null;
		try
		{
			if (!db.FillSqlDataReader(ref resultDataReader, sql))
			{
				return string.Empty;
			}
			if (resultDataReader != null && resultDataReader.Read())
			{
				return ReaderString(resultDataReader, 0);
			}
			return string.Empty;
		}
		finally
		{
			if (resultDataReader != null && !resultDataReader.IsClosed)
			{
				resultDataReader.Close();
			}
		}
	}

	private static int ReaderInt(SqlDataReader reader, int ordinal)
	{
		object value = reader.GetValue(ordinal);
		if (value == null || value == DBNull.Value)
		{
			return 0;
		}
		return Convert.ToInt32(value);
	}

	#endregion

	#region Manor core

	private UserManorInfo GetManorCore(int userId)
	{
		SqlDataReader resultDataReader = null;
		try
		{
			if (!db.FillSqlDataReader(ref resultDataReader, string.Format(SqlSelectManor, userId)))
			{
				return null;
			}
			if (resultDataReader != null && resultDataReader.Read())
			{
				return ReadManor(resultDataReader);
			}
			return null;
		}
		finally
		{
			if (resultDataReader != null && !resultDataReader.IsClosed)
			{
				resultDataReader.Close();
			}
		}
	}

	private static UserManorInfo ReadManor(SqlDataReader reader)
	{
		return new UserManorInfo
		{
			UserID = ReaderInt(reader, "UserID"),
			Exp = ReaderInt(reader, "Exp"),
			Money = ReaderInt(reader, "Money"),
			Value = ReaderInt(reader, "Value"),
			ExpValue = ReaderInt(reader, "ExpValue"),
			BuyFoodCount = ReaderInt(reader, "BuyFoodCount"),
			BuffRate = ReaderInt(reader, "BuffRate"),
			BuffDate = ReaderDateTime(reader, "BuffDate"),
			ProtectDate = ReaderDateTime(reader, "ProtectDate"),
			BuyFoodDate = ReaderDateTime(reader, "BuyFoodDate"),
			LastUpdate = ReaderDateTime(reader, "LastUpdate")
		};
	}

	private bool InsertManor(UserManorInfo info)
	{
		info.LastUpdate = DateTime.Now;
		SqlParameter[] sqlParameters = new SqlParameter[11]
		{
			new SqlParameter("@UserID", SqlDbType.Int) { Value = info.UserID },
			new SqlParameter("@Exp", SqlDbType.Int) { Value = info.Exp },
			new SqlParameter("@Money", SqlDbType.Int) { Value = info.Money },
			new SqlParameter("@Value", SqlDbType.Int) { Value = info.Value },
			new SqlParameter("@ExpValue", SqlDbType.Int) { Value = info.ExpValue },
			new SqlParameter("@BuyFoodCount", SqlDbType.Int) { Value = info.BuyFoodCount },
			new SqlParameter("@BuffRate", SqlDbType.Int) { Value = info.BuffRate },
			new SqlParameter("@BuffDate", SqlDbType.DateTime) { Value = NormalizeDate(info.BuffDate) },
			new SqlParameter("@ProtectDate", SqlDbType.DateTime) { Value = NormalizeDate(info.ProtectDate) },
			new SqlParameter("@BuyFoodDate", SqlDbType.DateTime) { Value = NormalizeDate(info.BuyFoodDate) },
			new SqlParameter("@LastUpdate", SqlDbType.DateTime) { Value = NormalizeDate(info.LastUpdate) }
		};
		return db.Exesqlcomm(
			"INSERT INTO dbo.Sys_User_Manor (UserID, Exp, Money, [Value], ExpValue, BuyFoodCount, BuffRate, BuffDate, ProtectDate, BuyFoodDate, LastUpdate) "
			+ "VALUES (@UserID, @Exp, @Money, @Value, @ExpValue, @BuyFoodCount, @BuffRate, @BuffDate, @ProtectDate, @BuyFoodDate, @LastUpdate)",
			sqlParameters);
	}

	private bool UpdateManorCore(UserManorInfo info)
	{
		info.LastUpdate = DateTime.Now;
		SqlParameter[] sqlParameters = new SqlParameter[11]
		{
			new SqlParameter("@UserID", SqlDbType.Int) { Value = info.UserID },
			new SqlParameter("@Exp", SqlDbType.Int) { Value = info.Exp },
			new SqlParameter("@Money", SqlDbType.Int) { Value = info.Money },
			new SqlParameter("@Value", SqlDbType.Int) { Value = info.Value },
			new SqlParameter("@ExpValue", SqlDbType.Int) { Value = info.ExpValue },
			new SqlParameter("@BuyFoodCount", SqlDbType.Int) { Value = info.BuyFoodCount },
			new SqlParameter("@BuffRate", SqlDbType.Int) { Value = info.BuffRate },
			new SqlParameter("@BuffDate", SqlDbType.DateTime) { Value = NormalizeDate(info.BuffDate) },
			new SqlParameter("@ProtectDate", SqlDbType.DateTime) { Value = NormalizeDate(info.ProtectDate) },
			new SqlParameter("@BuyFoodDate", SqlDbType.DateTime) { Value = NormalizeDate(info.BuyFoodDate) },
			new SqlParameter("@LastUpdate", SqlDbType.DateTime) { Value = NormalizeDate(info.LastUpdate) }
		};
		return db.Exesqlcomm(
			"UPDATE dbo.Sys_User_Manor SET Exp = @Exp, Money = @Money, [Value] = @Value, ExpValue = @ExpValue, "
			+ "BuyFoodCount = @BuyFoodCount, BuffRate = @BuffRate, BuffDate = @BuffDate, ProtectDate = @ProtectDate, "
			+ "BuyFoodDate = @BuyFoodDate, LastUpdate = @LastUpdate WHERE UserID = @UserID",
			sqlParameters);
	}

	private bool SaveManorCore(UserManorInfo info)
	{
		if (GetManorCore(info.UserID) == null)
		{
			return InsertManor(info);
		}
		return UpdateManorCore(info);
	}

	#endregion

	#region Area core

	private List<UserManorAreaInfo> GetAreasCore(int userId)
	{
		List<UserManorAreaInfo> list = new List<UserManorAreaInfo>();
		SqlDataReader resultDataReader = null;
		try
		{
			if (!db.FillSqlDataReader(ref resultDataReader, string.Format(SqlSelectAreas, userId)))
			{
				return list;
			}
			while (resultDataReader != null && resultDataReader.Read())
			{
				list.Add(ReadArea(resultDataReader));
			}
			return list;
		}
		finally
		{
			if (resultDataReader != null && !resultDataReader.IsClosed)
			{
				resultDataReader.Close();
			}
		}
	}

	private UserManorAreaInfo GetAreaCore(int userId, int areaType)
	{
		SqlDataReader resultDataReader = null;
		try
		{
			if (!db.FillSqlDataReader(ref resultDataReader, string.Format(SqlSelectArea, userId, areaType)))
			{
				return null;
			}
			if (resultDataReader != null && resultDataReader.Read())
			{
				return ReadArea(resultDataReader);
			}
			return null;
		}
		finally
		{
			if (resultDataReader != null && !resultDataReader.IsClosed)
			{
				resultDataReader.Close();
			}
		}
	}

	private static UserManorAreaInfo ReadArea(SqlDataReader reader)
	{
		return new UserManorAreaInfo
		{
			ID = ReaderInt(reader, "ID"),
			UserID = ReaderInt(reader, "UserID"),
			AreaType = ReaderInt(reader, "AreaType"),
			Level = ReaderInt(reader, "Level"),
			LevelTwo = ReaderInt(reader, "LevelTwo"),
			IsPlant = ReaderBool(reader, "IsPlant"),
			PlantTempId = ReaderInt(reader, "PlantTempId"),
			PlantDate = ReaderDateTime(reader, "PlantDate"),
			RipeDate = ReaderInt(reader, "RipeDate"),
			FoodTempId = ReaderInt(reader, "FoodTempId"),
			FoodDate = ReaderDateTime(reader, "FoodDate"),
			ProduceCount = ReaderInt(reader, "ProduceCount"),
			LastUpdate = ReaderDateTime(reader, "LastUpdate")
		};
	}

	private bool SaveAreaCore(UserManorAreaInfo info)
	{
		info.LastUpdate = DateTime.Now;
		if (info.ID <= 0)
		{
			int existingId = QueryInt(string.Format(SqlSelectAreaId, info.UserID, info.AreaType));
			if (existingId > 0)
			{
				info.ID = existingId;
			}
			else
			{
				if (!InsertArea(info))
				{
					return false;
				}
				info.ID = QueryInt(string.Format(SqlSelectAreaId, info.UserID, info.AreaType));
				return info.ID > 0;
			}
		}
		return UpdateArea(info);
	}

	private bool InsertArea(UserManorAreaInfo info)
	{
		SqlParameter[] sqlParameters = new SqlParameter[12]
		{
			new SqlParameter("@UserID", SqlDbType.Int) { Value = info.UserID },
			new SqlParameter("@AreaType", SqlDbType.Int) { Value = info.AreaType },
			new SqlParameter("@Level", SqlDbType.Int) { Value = info.Level },
			new SqlParameter("@LevelTwo", SqlDbType.Int) { Value = info.LevelTwo },
			new SqlParameter("@IsPlant", SqlDbType.Bit) { Value = info.IsPlant },
			new SqlParameter("@PlantTempId", SqlDbType.Int) { Value = info.PlantTempId },
			new SqlParameter("@PlantDate", SqlDbType.DateTime) { Value = NormalizeDate(info.PlantDate) },
			new SqlParameter("@RipeDate", SqlDbType.Int) { Value = info.RipeDate },
			new SqlParameter("@FoodTempId", SqlDbType.Int) { Value = info.FoodTempId },
			new SqlParameter("@FoodDate", SqlDbType.DateTime) { Value = NormalizeDate(info.FoodDate) },
			new SqlParameter("@ProduceCount", SqlDbType.Int) { Value = info.ProduceCount },
			new SqlParameter("@LastUpdate", SqlDbType.DateTime) { Value = NormalizeDate(info.LastUpdate) }
		};
		return db.Exesqlcomm(
			"INSERT INTO dbo.Sys_User_ManorArea (UserID, AreaType, [Level], LevelTwo, IsPlant, PlantTempId, PlantDate, "
			+ "RipeDate, FoodTempId, FoodDate, ProduceCount, LastUpdate) VALUES (@UserID, @AreaType, @Level, @LevelTwo, "
			+ "@IsPlant, @PlantTempId, @PlantDate, @RipeDate, @FoodTempId, @FoodDate, @ProduceCount, @LastUpdate)",
			sqlParameters);
	}

	private bool UpdateArea(UserManorAreaInfo info)
	{
		SqlParameter[] sqlParameters = new SqlParameter[11]
		{
			new SqlParameter("@ID", SqlDbType.Int) { Value = info.ID },
			new SqlParameter("@Level", SqlDbType.Int) { Value = info.Level },
			new SqlParameter("@LevelTwo", SqlDbType.Int) { Value = info.LevelTwo },
			new SqlParameter("@IsPlant", SqlDbType.Bit) { Value = info.IsPlant },
			new SqlParameter("@PlantTempId", SqlDbType.Int) { Value = info.PlantTempId },
			new SqlParameter("@PlantDate", SqlDbType.DateTime) { Value = NormalizeDate(info.PlantDate) },
			new SqlParameter("@RipeDate", SqlDbType.Int) { Value = info.RipeDate },
			new SqlParameter("@FoodTempId", SqlDbType.Int) { Value = info.FoodTempId },
			new SqlParameter("@FoodDate", SqlDbType.DateTime) { Value = NormalizeDate(info.FoodDate) },
			new SqlParameter("@ProduceCount", SqlDbType.Int) { Value = info.ProduceCount },
			new SqlParameter("@LastUpdate", SqlDbType.DateTime) { Value = NormalizeDate(info.LastUpdate) }
		};
		return db.Exesqlcomm(
			"UPDATE dbo.Sys_User_ManorArea SET [Level] = @Level, LevelTwo = @LevelTwo, IsPlant = @IsPlant, "
			+ "PlantTempId = @PlantTempId, PlantDate = @PlantDate, RipeDate = @RipeDate, FoodTempId = @FoodTempId, "
			+ "FoodDate = @FoodDate, ProduceCount = @ProduceCount, LastUpdate = @LastUpdate "
			+ "WHERE ID = @ID",
			sqlParameters);
	}

	#endregion

	#region Goods core

	private List<UserManorGoodsInfo> GetGoodsCore(int userId)
	{
		List<UserManorGoodsInfo> list = new List<UserManorGoodsInfo>();
		SqlDataReader resultDataReader = null;
		try
		{
			if (!db.FillSqlDataReader(ref resultDataReader, string.Format(SqlSelectGoods, userId)))
			{
				return list;
			}
			while (resultDataReader != null && resultDataReader.Read())
			{
				list.Add(new UserManorGoodsInfo
				{
					ID = ReaderInt(resultDataReader, "ID"),
					UserID = ReaderInt(resultDataReader, "UserID"),
					TemplateId = ReaderInt(resultDataReader, "TemplateId"),
					Property = ReaderInt(resultDataReader, "Property"),
					Count = ReaderInt(resultDataReader, "Count")
				});
			}
			return list;
		}
		finally
		{
			if (resultDataReader != null && !resultDataReader.IsClosed)
			{
				resultDataReader.Close();
			}
		}
	}

	private bool SaveGoodsCore(UserManorGoodsInfo info)
	{
		if (info.Count < 0)
		{
			info.Count = 0;
		}
		if (info.ID <= 0)
		{
			int existingId = QueryInt(string.Format(SqlSelectGoodsId, info.UserID, info.TemplateId, info.Property));
			if (existingId > 0)
			{
				info.ID = existingId;
			}
			else
			{
				if (!InsertGoods(info))
				{
					return false;
				}
				info.ID = QueryInt(string.Format(SqlSelectGoodsId, info.UserID, info.TemplateId, info.Property));
				return info.ID > 0;
			}
		}
		return UpdateGoods(info);
	}

	private bool InsertGoods(UserManorGoodsInfo info)
	{
		SqlParameter[] sqlParameters = new SqlParameter[4]
		{
			new SqlParameter("@UserID", SqlDbType.Int) { Value = info.UserID },
			new SqlParameter("@TemplateId", SqlDbType.Int) { Value = info.TemplateId },
			new SqlParameter("@Property", SqlDbType.Int) { Value = info.Property },
			new SqlParameter("@Count", SqlDbType.Int) { Value = info.Count }
		};
		return db.Exesqlcomm(
			"INSERT INTO dbo.Sys_User_ManorGoods (UserID, TemplateId, [Property], [Count]) "
			+ "VALUES (@UserID, @TemplateId, @Property, @Count)",
			sqlParameters);
	}

	private bool UpdateGoods(UserManorGoodsInfo info)
	{
		SqlParameter[] sqlParameters = new SqlParameter[4]
		{
			new SqlParameter("@ID", SqlDbType.Int) { Value = info.ID },
			new SqlParameter("@TemplateId", SqlDbType.Int) { Value = info.TemplateId },
			new SqlParameter("@Property", SqlDbType.Int) { Value = info.Property },
			new SqlParameter("@Count", SqlDbType.Int) { Value = info.Count }
		};
		return db.Exesqlcomm(
			"UPDATE dbo.Sys_User_ManorGoods SET TemplateId = @TemplateId, [Property] = @Property, [Count] = @Count "
			+ "WHERE ID = @ID",
			sqlParameters);
	}

	private bool DeleteGoodsCore(int id)
	{
		SqlParameter[] sqlParameters = new SqlParameter[1]
		{
			new SqlParameter("@ID", SqlDbType.Int) { Value = id }
		};
		return db.Exesqlcomm("DELETE FROM dbo.Sys_User_ManorGoods WHERE ID = @ID", sqlParameters);
	}

	private bool DeleteGoodsCore(int userId, int templateId, int property)
	{
		SqlParameter[] sqlParameters = new SqlParameter[3]
		{
			new SqlParameter("@UserID", SqlDbType.Int) { Value = userId },
			new SqlParameter("@TemplateId", SqlDbType.Int) { Value = templateId },
			new SqlParameter("@Property", SqlDbType.Int) { Value = property }
		};
		return db.Exesqlcomm(
			"DELETE FROM dbo.Sys_User_ManorGoods WHERE UserID = @UserID AND TemplateId = @TemplateId AND [Property] = @Property",
			sqlParameters);
	}

	#endregion

	#region Worker core

	private List<UserManorWorkerInfo> GetWorkersCore(int ownerId)
	{
		List<UserManorWorkerInfo> list = new List<UserManorWorkerInfo>();
		SqlDataReader resultDataReader = null;
		try
		{
			if (!db.FillSqlDataReader(ref resultDataReader, string.Format(SqlSelectWorkers, ownerId)))
			{
				return list;
			}
			while (resultDataReader != null && resultDataReader.Read())
			{
				list.Add(ReadWorker(resultDataReader));
			}
			return list;
		}
		finally
		{
			if (resultDataReader != null && !resultDataReader.IsClosed)
			{
				resultDataReader.Close();
			}
		}
	}

	private static UserManorWorkerInfo ReadWorker(SqlDataReader reader)
	{
		return new UserManorWorkerInfo
		{
			ID = ReaderInt(reader, "ID"),
			OwnerID = ReaderInt(reader, "OwnerID"),
			WorkerUserID = ReaderInt(reader, "WorkerUserID"),
			Name = ReaderString(reader, "Name"),
			Title = ReaderString(reader, "Title"),
			Feeling = ReaderInt(reader, "Feeling"),
			Energy = ReaderInt(reader, "Energy"),
			FarmExp = ReaderInt(reader, "FarmExp"),
			FishExp = ReaderInt(reader, "FishExp"),
			MineExp = ReaderInt(reader, "MineExp"),
			RanchExp = ReaderInt(reader, "RanchExp"),
			IsWorking = ReaderBool(reader, "IsWorking"),
			WorkType = ReaderInt(reader, "WorkType"),
			WorkBeginDate = ReaderDateTime(reader, "WorkBeginDate"),
			EmployProtectDate = ReaderDateTime(reader, "EmployProtectDate"),
			ProtectPrice = ReaderInt(reader, "ProtectPrice"),
			Sex = ReaderBool(reader, "Sex"),
			Colors = ReaderString(reader, "Colors"),
			Style = ReaderString(reader, "Style"),
			HiredDate = ReaderDateTime(reader, "HiredDate")
		};
	}

	private bool SaveWorkerCore(UserManorWorkerInfo info)
	{
		if (info.ID <= 0)
		{
			int existingId = QueryInt(string.Format(SqlSelectWorkerId, info.OwnerID, info.WorkerUserID));
			if (existingId > 0)
			{
				info.ID = existingId;
			}
			else
			{
				if (info.HiredDate < SqlMinValidDate)
				{
					info.HiredDate = DateTime.Now;
				}
				if (!InsertWorker(info))
				{
					return false;
				}
				info.ID = QueryInt(string.Format(SqlSelectWorkerId, info.OwnerID, info.WorkerUserID));
				return info.ID > 0;
			}
		}
		return UpdateWorker(info);
	}

	private bool InsertWorker(UserManorWorkerInfo info)
	{
		SqlParameter[] sqlParameters = new SqlParameter[19]
		{
			new SqlParameter("@OwnerID", SqlDbType.Int) { Value = info.OwnerID },
			new SqlParameter("@WorkerUserID", SqlDbType.Int) { Value = info.WorkerUserID },
			new SqlParameter("@Name", SqlDbType.NVarChar, 50) { Value = NormalizeString(info.Name, 50) },
			new SqlParameter("@Title", SqlDbType.NVarChar, 50) { Value = NormalizeString(info.Title, 50) },
			new SqlParameter("@Feeling", SqlDbType.Int) { Value = info.Feeling },
			new SqlParameter("@Energy", SqlDbType.Int) { Value = info.Energy },
			new SqlParameter("@FarmExp", SqlDbType.Int) { Value = info.FarmExp },
			new SqlParameter("@FishExp", SqlDbType.Int) { Value = info.FishExp },
			new SqlParameter("@MineExp", SqlDbType.Int) { Value = info.MineExp },
			new SqlParameter("@RanchExp", SqlDbType.Int) { Value = info.RanchExp },
			new SqlParameter("@IsWorking", SqlDbType.Bit) { Value = info.IsWorking },
			new SqlParameter("@WorkType", SqlDbType.Int) { Value = info.WorkType },
			new SqlParameter("@WorkBeginDate", SqlDbType.DateTime) { Value = NormalizeDate(info.WorkBeginDate) },
			new SqlParameter("@EmployProtectDate", SqlDbType.DateTime) { Value = NormalizeDate(info.EmployProtectDate) },
			new SqlParameter("@ProtectPrice", SqlDbType.Int) { Value = info.ProtectPrice },
			new SqlParameter("@Sex", SqlDbType.Bit) { Value = info.Sex },
			new SqlParameter("@Colors", SqlDbType.NVarChar, 200) { Value = NormalizeString(info.Colors, 200) },
			new SqlParameter("@Style", SqlDbType.NVarChar, 200) { Value = NormalizeString(info.Style, 200) },
			new SqlParameter("@HiredDate", SqlDbType.DateTime) { Value = NormalizeDate(info.HiredDate) }
		};
		return db.Exesqlcomm(
			"INSERT INTO dbo.Sys_User_ManorWorker (OwnerID, WorkerUserID, [Name], [Title], Feeling, Energy, FarmExp, "
			+ "FishExp, MineExp, RanchExp, IsWorking, WorkType, WorkBeginDate, EmployProtectDate, ProtectPrice, Sex, "
			+ "Colors, Style, HiredDate) VALUES (@OwnerID, @WorkerUserID, @Name, @Title, @Feeling, @Energy, @FarmExp, "
			+ "@FishExp, @MineExp, @RanchExp, @IsWorking, @WorkType, @WorkBeginDate, @EmployProtectDate, @ProtectPrice, "
			+ "@Sex, @Colors, @Style, @HiredDate)",
			sqlParameters);
	}

	private bool UpdateWorker(UserManorWorkerInfo info)
	{
		SqlParameter[] sqlParameters = new SqlParameter[18]
		{
			new SqlParameter("@ID", SqlDbType.Int) { Value = info.ID },
			new SqlParameter("@Name", SqlDbType.NVarChar, 50) { Value = NormalizeString(info.Name, 50) },
			new SqlParameter("@Title", SqlDbType.NVarChar, 50) { Value = NormalizeString(info.Title, 50) },
			new SqlParameter("@Feeling", SqlDbType.Int) { Value = info.Feeling },
			new SqlParameter("@Energy", SqlDbType.Int) { Value = info.Energy },
			new SqlParameter("@FarmExp", SqlDbType.Int) { Value = info.FarmExp },
			new SqlParameter("@FishExp", SqlDbType.Int) { Value = info.FishExp },
			new SqlParameter("@MineExp", SqlDbType.Int) { Value = info.MineExp },
			new SqlParameter("@RanchExp", SqlDbType.Int) { Value = info.RanchExp },
			new SqlParameter("@IsWorking", SqlDbType.Bit) { Value = info.IsWorking },
			new SqlParameter("@WorkType", SqlDbType.Int) { Value = info.WorkType },
			new SqlParameter("@WorkBeginDate", SqlDbType.DateTime) { Value = NormalizeDate(info.WorkBeginDate) },
			new SqlParameter("@EmployProtectDate", SqlDbType.DateTime) { Value = NormalizeDate(info.EmployProtectDate) },
			new SqlParameter("@ProtectPrice", SqlDbType.Int) { Value = info.ProtectPrice },
			new SqlParameter("@Sex", SqlDbType.Bit) { Value = info.Sex },
			new SqlParameter("@Colors", SqlDbType.NVarChar, 200) { Value = NormalizeString(info.Colors, 200) },
			new SqlParameter("@Style", SqlDbType.NVarChar, 200) { Value = NormalizeString(info.Style, 200) },
			new SqlParameter("@HiredDate", SqlDbType.DateTime) { Value = NormalizeDate(info.HiredDate) }
		};
		return db.Exesqlcomm(
			"UPDATE dbo.Sys_User_ManorWorker SET [Name] = @Name, [Title] = @Title, Feeling = @Feeling, "
			+ "Energy = @Energy, FarmExp = @FarmExp, FishExp = @FishExp, MineExp = @MineExp, RanchExp = @RanchExp, "
			+ "IsWorking = @IsWorking, WorkType = @WorkType, WorkBeginDate = @WorkBeginDate, "
			+ "EmployProtectDate = @EmployProtectDate, ProtectPrice = @ProtectPrice, Sex = @Sex, Colors = @Colors, "
			+ "Style = @Style, HiredDate = @HiredDate WHERE ID = @ID",
			sqlParameters);
	}

	private bool DeleteWorkerCore(int id)
	{
		SqlParameter[] sqlParameters = new SqlParameter[1]
		{
			new SqlParameter("@ID", SqlDbType.Int) { Value = id }
		};
		return db.Exesqlcomm("DELETE FROM dbo.Sys_User_ManorWorker WHERE ID = @ID", sqlParameters);
	}

	private bool DeleteWorkerCore(int ownerId, int workerUserID)
	{
		SqlParameter[] sqlParameters = new SqlParameter[2]
		{
			new SqlParameter("@OwnerID", SqlDbType.Int) { Value = ownerId },
			new SqlParameter("@WorkerUserID", SqlDbType.Int) { Value = workerUserID }
		};
		return db.Exesqlcomm(
			"DELETE FROM dbo.Sys_User_ManorWorker WHERE OwnerID = @OwnerID AND WorkerUserID = @WorkerUserID",
			sqlParameters);
	}

	#endregion

	#region Task core (nhiệm vụ bảng tin — phase 2.2)

	private List<UserManorTaskInfo> GetManorTasksCore(int userId, DateTime taskDate)
	{
		List<UserManorTaskInfo> list = new List<UserManorTaskInfo>();
		SqlDataReader resultDataReader = null;
		try
		{
			if (!db.FillSqlDataReader(ref resultDataReader, string.Format(SqlSelectManorTasks, userId, FormatSqlDate(taskDate))))
			{
				return list;
			}
			while (resultDataReader != null && resultDataReader.Read())
			{
				list.Add(ReadManorTask(resultDataReader));
			}
			return list;
		}
		finally
		{
			if (resultDataReader != null && !resultDataReader.IsClosed)
			{
				resultDataReader.Close();
			}
		}
	}

	private static UserManorTaskInfo ReadManorTask(SqlDataReader reader)
	{
		return new UserManorTaskInfo
		{
			ID = ReaderInt(reader, "ID"),
			UserID = ReaderInt(reader, "UserID"),
			TaskDate = ReaderDateTime(reader, "TaskDate"),
			TaskId = ReaderInt(reader, "TaskId"),
			TemplateId = ReaderInt(reader, "TemplateId"),
			Condition1 = ReaderInt(reader, "Condition1"),
			Condition2 = ReaderInt(reader, "Condition2"),
			Condition3 = ReaderInt(reader, "Condition3"),
			IsComplete = ReaderBool(reader, "IsComplete"),
			IsReward = ReaderBool(reader, "IsReward"),
			UpdateDate = ReaderDateTime(reader, "UpdateDate")
		};
	}

	private bool SaveManorTaskCore(UserManorTaskInfo info)
	{
		if (info.TaskDate < SqlMinValidDate)
		{
			info.TaskDate = DateTime.Today;
		}
		info.TaskDate = info.TaskDate.Date;
		info.UpdateDate = DateTime.Now;
		if (info.ID <= 0)
		{
			int existingId = QueryInt(string.Format(SqlSelectManorTaskId, info.UserID, FormatSqlDate(info.TaskDate), info.TaskId));
			if (existingId > 0)
			{
				info.ID = existingId;
			}
			else
			{
				if (!InsertManorTask(info))
				{
					return false;
				}
				info.ID = QueryInt(string.Format(SqlSelectManorTaskId, info.UserID, FormatSqlDate(info.TaskDate), info.TaskId));
				return info.ID > 0;
			}
		}
		return UpdateManorTask(info);
	}

	private bool InsertManorTask(UserManorTaskInfo info)
	{
		SqlParameter[] sqlParameters = new SqlParameter[10]
		{
			new SqlParameter("@UserID", SqlDbType.Int) { Value = info.UserID },
			new SqlParameter("@TaskDate", SqlDbType.Date) { Value = info.TaskDate.Date },
			new SqlParameter("@TaskId", SqlDbType.Int) { Value = info.TaskId },
			new SqlParameter("@TemplateId", SqlDbType.Int) { Value = info.TemplateId },
			new SqlParameter("@Condition1", SqlDbType.Int) { Value = info.Condition1 },
			new SqlParameter("@Condition2", SqlDbType.Int) { Value = info.Condition2 },
			new SqlParameter("@Condition3", SqlDbType.Int) { Value = info.Condition3 },
			new SqlParameter("@IsComplete", SqlDbType.Bit) { Value = info.IsComplete },
			new SqlParameter("@IsReward", SqlDbType.Bit) { Value = info.IsReward },
			new SqlParameter("@UpdateDate", SqlDbType.DateTime) { Value = NormalizeDate(info.UpdateDate) }
		};
		return db.Exesqlcomm(
			"INSERT INTO dbo.Sys_User_ManorTask (UserID, TaskDate, TaskId, TemplateId, Condition1, Condition2, "
			+ "Condition3, IsComplete, IsReward, UpdateDate) VALUES (@UserID, @TaskDate, @TaskId, @TemplateId, "
			+ "@Condition1, @Condition2, @Condition3, @IsComplete, @IsReward, @UpdateDate)",
			sqlParameters);
	}

	private bool UpdateManorTask(UserManorTaskInfo info)
	{
		SqlParameter[] sqlParameters = new SqlParameter[10]
		{
			new SqlParameter("@ID", SqlDbType.Int) { Value = info.ID },
			new SqlParameter("@TaskDate", SqlDbType.Date) { Value = info.TaskDate.Date },
			new SqlParameter("@TaskId", SqlDbType.Int) { Value = info.TaskId },
			new SqlParameter("@TemplateId", SqlDbType.Int) { Value = info.TemplateId },
			new SqlParameter("@Condition1", SqlDbType.Int) { Value = info.Condition1 },
			new SqlParameter("@Condition2", SqlDbType.Int) { Value = info.Condition2 },
			new SqlParameter("@Condition3", SqlDbType.Int) { Value = info.Condition3 },
			new SqlParameter("@IsComplete", SqlDbType.Bit) { Value = info.IsComplete },
			new SqlParameter("@IsReward", SqlDbType.Bit) { Value = info.IsReward },
			new SqlParameter("@UpdateDate", SqlDbType.DateTime) { Value = NormalizeDate(info.UpdateDate) }
		};
		return db.Exesqlcomm(
			"UPDATE dbo.Sys_User_ManorTask SET TaskDate = @TaskDate, TaskId = @TaskId, TemplateId = @TemplateId, "
			+ "Condition1 = @Condition1, Condition2 = @Condition2, Condition3 = @Condition3, IsComplete = @IsComplete, "
			+ "IsReward = @IsReward, UpdateDate = @UpdateDate WHERE ID = @ID",
			sqlParameters);
	}

	private bool DeleteManorTasksCore(int userId, DateTime taskDate)
	{
		SqlParameter[] sqlParameters = new SqlParameter[2]
		{
			new SqlParameter("@UserID", SqlDbType.Int) { Value = userId },
			new SqlParameter("@TaskDate", SqlDbType.Date) { Value = taskDate.Date }
		};
		return db.Exesqlcomm("DELETE FROM dbo.Sys_User_ManorTask WHERE UserID = @UserID AND TaskDate = @TaskDate", sqlParameters);
	}

	#endregion

	#region Worker lookup + Candidate core (phase 2.2)

	private UserManorWorkerInfo GetWorkerByWorkerUserIDCore(int workerUserId)
	{
		SqlDataReader resultDataReader = null;
		try
		{
			if (!db.FillSqlDataReader(ref resultDataReader, string.Format(SqlSelectWorkerByWorkerUserID, workerUserId)))
			{
				return null;
			}
			if (resultDataReader != null && resultDataReader.Read())
			{
				return ReadWorker(resultDataReader);
			}
			return null;
		}
		finally
		{
			if (resultDataReader != null && !resultDataReader.IsClosed)
			{
				resultDataReader.Close();
			}
		}
	}

	private List<UserManorWorkerInfo> GetWorkersByWorkerUserIDCore(int workerUserId)
	{
		List<UserManorWorkerInfo> list = new List<UserManorWorkerInfo>();
		SqlDataReader resultDataReader = null;
		try
		{
			if (!db.FillSqlDataReader(ref resultDataReader, string.Format(SqlSelectWorkersByWorkerUserID, workerUserId)))
			{
				return list;
			}
			while (resultDataReader != null && resultDataReader.Read())
			{
				list.Add(ReadWorker(resultDataReader));
			}
			return list;
		}
		finally
		{
			if (resultDataReader != null && !resultDataReader.IsClosed)
			{
				resultDataReader.Close();
			}
		}
	}

	private UserManorWorkerInfo GetWorkerCore(int ownerId, int workerUserId)
	{
		SqlDataReader resultDataReader = null;
		try
		{
			if (!db.FillSqlDataReader(ref resultDataReader, string.Format(SqlSelectWorker, ownerId, workerUserId)))
			{
				return null;
			}
			if (resultDataReader != null && resultDataReader.Read())
			{
				return ReadWorker(resultDataReader);
			}
			return null;
		}
		finally
		{
			if (resultDataReader != null && !resultDataReader.IsClosed)
			{
				resultDataReader.Close();
			}
		}
	}

	private List<ManorCandidateInfo> GetManorCandidatesCore(int excludeUserId, string keyword, int limit)
	{
		List<ManorCandidateInfo> list = new List<ManorCandidateInfo>();
		SqlDataReader resultDataReader = null;
		try
		{
			string sql = string.Format(SqlSelectManorCandidates, excludeUserId, EscapeSqlString(keyword, CandidateKeywordMaxLength), limit);
			if (!db.FillSqlDataReader(ref resultDataReader, sql))
			{
				return list;
			}
			while (resultDataReader != null && resultDataReader.Read())
			{
				list.Add(new ManorCandidateInfo
				{
					UserId = ReaderInt(resultDataReader, "UserID"),
					NickName = ReaderString(resultDataReader, "NickName"),
					Grade = ReaderInt(resultDataReader, "Grade"),
					Sex = ReaderInt(resultDataReader, "Sex"),
					Colors = ReaderString(resultDataReader, "Colors"),
					Style = ReaderString(resultDataReader, "Style"),
					OwnerID = ReaderInt(resultDataReader, "OwnerID"),
					ProtectDate = ReaderDateTime(resultDataReader, "ProtectDate")
				});
			}
			return list;
		}
		finally
		{
			if (resultDataReader != null && !resultDataReader.IsClosed)
			{
				resultDataReader.Close();
			}
		}
	}

	private string GetUserNickNameCore(int userId)
	{
		return QueryString(string.Format(SqlSelectUserNickName, userId));
	}

	private int GetManorWorkerCountCore(int ownerId, int workType)
	{
		string sql = ((workType > 0) ? string.Format(SqlCountWorkingWorkersByType, ownerId, workType) : string.Format(SqlCountWorkingWorkers, ownerId));
		return QueryInt(sql);
	}

	#endregion

	#region CreateDefaultManor core

	private bool CreateDefaultManorCore(int userId)
	{
		UserManorInfo manorInfo = GetManorCore(userId);
		if (manorInfo == null)
		{
			manorInfo = new UserManorInfo
			{
				UserID = userId,
				Exp = 0,
				Money = 0,
				Value = 0,
				ExpValue = 0,
				BuyFoodCount = 0,
				BuffRate = 0,
				BuffDate = SqlMinDate,
				LastUpdate = DateTime.Now
			};
			if (!InsertManor(manorInfo))
			{
				return false;
			}
		}

		List<UserManorAreaInfo> areas = GetAreasCore(userId);
		bool allOk = true;
		for (int areaType = AreaTypeMin; areaType <= AreaTypeMax; areaType++)
		{
			int type = areaType;
			if (areas.Exists((UserManorAreaInfo a) => a.AreaType == type))
			{
				continue;
			}
			UserManorAreaInfo areaInfo = new UserManorAreaInfo
			{
				UserID = userId,
				AreaType = type,
				Level = 1,
				LevelTwo = ((type == 1 || type == 3) ? 1 : 0),
				IsPlant = false,
				PlantTempId = 0,
				PlantDate = SqlMinDate,
				RipeDate = 0,
				FoodTempId = 0,
				FoodDate = SqlMinDate,
				ProduceCount = 0,
				LastUpdate = DateTime.Now
			};
			if (!InsertArea(areaInfo))
			{
				allOk = false;
			}
		}
		return allOk;
	}

	#endregion
	#region BXH nông trại (ManorWealth.xml / ManorAbility.xml)

	/// <summary>BXH Tài Phú: top user theo tiền nông trại (kèm exp farm).</summary>
	public static List<UserManorRankInfo> GetManorWealthRank(int limit)
	{
		if (limit <= 0)
		{
			limit = 100;
		}
		if (limit > 200)
		{
			limit = 200;
		}
		return Execute("GetManorWealthRank", (ManorBussiness b) => b.GetWealthRankCore(limit), new List<UserManorRankInfo>());
	}

	/// <summary>BXH Lao Động: top user theo tổng exp nghề (thợ) + tên chủ hiện tại.</summary>
	public static List<UserManorRankInfo> GetManorAbilityRank(int limit)
	{
		if (limit <= 0)
		{
			limit = 100;
		}
		if (limit > 200)
		{
			limit = 200;
		}
		return Execute("GetManorAbilityRank", (ManorBussiness b) => b.GetAbilityRankCore(limit), new List<UserManorRankInfo>());
	}

	private List<UserManorRankInfo> GetWealthRankCore(int limit)
	{
		List<UserManorRankInfo> list = new List<UserManorRankInfo>();
		string sql = "SELECT TOP(" + limit + ") m.UserID, u.NickName, u.Sex, u.Style, u.Colors, m.Exp, m.Money FROM dbo.Sys_User_Manor m INNER JOIN dbo.Sys_Users_Detail u ON u.UserID = m.UserID ORDER BY m.Money DESC, m.Exp DESC";
		SqlDataReader reader = null;
		try
		{
			if (!db.FillSqlDataReader(ref reader, sql))
			{
				return list;
			}
			while (reader.Read())
			{
				list.Add(new UserManorRankInfo
				{
					UserID = ReaderInt(reader, "UserID"),
					NickName = ReaderString(reader, "NickName"),
					Sex = ReaderInt(reader, "Sex"),
					Style = ReaderString(reader, "Style"),
					Colors = ReaderString(reader, "Colors"),
					Exp = ReaderInt(reader, "Exp"),
					Money = ReaderInt(reader, "Money")
				});
			}
		}
		finally
		{
			if (reader != null)
			{
				reader.Close();
				reader.Dispose();
			}
		}
		return list;
	}

	private List<UserManorRankInfo> GetAbilityRankCore(int limit)
	{
		List<UserManorRankInfo> list = new List<UserManorRankInfo>();
		string sql = "SELECT TOP(" + limit + ") w.WorkerUserID AS UserID, u.NickName, u.Sex, u.Style, u.Colors, "
			+ "SUM(w.FarmExp) AS FarmExp, SUM(w.FishExp) AS FishExp, SUM(w.MineExp) AS MineExp, SUM(w.RanchExp) AS RanchExp, "
			+ "MAX(o.NickName) AS BossNickName "
			+ "FROM dbo.Sys_User_ManorWorker w "
			+ "INNER JOIN dbo.Sys_Users_Detail u ON u.UserID = w.WorkerUserID "
			+ "LEFT JOIN dbo.Sys_Users_Detail o ON o.UserID = w.OwnerID "
			+ "GROUP BY w.WorkerUserID, u.NickName, u.Sex, u.Style, u.Colors "
			+ "ORDER BY (SUM(w.FarmExp) + SUM(w.FishExp) + SUM(w.MineExp) + SUM(w.RanchExp)) DESC, w.WorkerUserID";
		SqlDataReader reader = null;
		try
		{
			if (!db.FillSqlDataReader(ref reader, sql))
			{
				return list;
			}
			while (reader.Read())
			{
				list.Add(new UserManorRankInfo
				{
					UserID = ReaderInt(reader, "UserID"),
					NickName = ReaderString(reader, "NickName"),
					Sex = ReaderInt(reader, "Sex"),
					Style = ReaderString(reader, "Style"),
					Colors = ReaderString(reader, "Colors"),
					FarmExp = ReaderInt(reader, "FarmExp"),
					FishExp = ReaderInt(reader, "FishExp"),
					MineExp = ReaderInt(reader, "MineExp"),
					RanchExp = ReaderInt(reader, "RanchExp"),
					BossNickName = ReaderString(reader, "BossNickName")
				});
			}
		}
		finally
		{
			if (reader != null)
			{
				reader.Close();
				reader.Dispose();
			}
		}
		return list;
	}

	#endregion
	#region Nhật ký nông trại (81/1)

	/// <summary>Ghi 1 dòng nhật ký nông trại (log hiển thị trong bảng Quản lý 81/1).</summary>
	public static bool AddManorLog(int userId, int logType, string content)
	{
		if (userId <= 0 || string.IsNullOrEmpty(content))
		{
			return false;
		}
		return Execute("AddManorLog", (ManorBussiness b) => b.AddManorLogCore(userId, logType, NormalizeString(content, 200)), false);
	}

	/// <summary>Lấy nhật ký mới nhất của user (mới nhất trước), tối đa 50 dòng.</summary>
	public static List<UserManorLogInfo> GetManorLogs(int userId, int limit)
	{
		if (userId <= 0)
		{
			return new List<UserManorLogInfo>();
		}
		if (limit <= 0 || limit > 50)
		{
			limit = 20;
		}
		return Execute("GetManorLogs", (ManorBussiness b) => b.GetManorLogsCore(userId, limit), new List<UserManorLogInfo>());
	}

	/// <summary>Dọn nhật ký cũ, chỉ giữ lại <paramref name="keep"/> dòng mới nhất.</summary>
	public static void TrimManorLogs(int userId, int keep)
	{
		if (userId <= 0 || keep <= 0)
		{
			return;
		}
		Execute("TrimManorLogs", (ManorBussiness b) => b.TrimManorLogsCore(userId, keep), false);
	}

	private bool AddManorLogCore(int userId, int logType, string content)
	{
		SqlParameter[] sqlParameters = new SqlParameter[4]
		{
			new SqlParameter("@UserID", SqlDbType.Int) { Value = userId },
			new SqlParameter("@LogType", SqlDbType.Int) { Value = logType },
			new SqlParameter("@Content", SqlDbType.NVarChar, 200) { Value = content },
			new SqlParameter("@CreateDate", SqlDbType.DateTime) { Value = DateTime.Now }
		};
		return db.Exesqlcomm(
			"INSERT INTO dbo.Sys_User_ManorLog (UserID, LogType, Content, CreateDate) VALUES (@UserID, @LogType, @Content, @CreateDate)",
			sqlParameters);
	}

	private List<UserManorLogInfo> GetManorLogsCore(int userId, int limit)
	{
		List<UserManorLogInfo> list = new List<UserManorLogInfo>();
		SqlDataReader reader = null;
		try
		{
			if (!db.FillSqlDataReader(ref reader, "SELECT TOP(" + limit + ") ID, UserID, LogType, Content, CreateDate FROM dbo.Sys_User_ManorLog WHERE UserID = " + userId + " ORDER BY ID DESC"))
			{
				return list;
			}
			while (reader != null && reader.Read())
			{
				list.Add(new UserManorLogInfo
				{
					ID = ReaderInt(reader, "ID"),
					UserID = ReaderInt(reader, "UserID"),
					LogType = ReaderInt(reader, "LogType"),
					Content = ReaderString(reader, "Content"),
					CreateDate = ReaderDateTime(reader, "CreateDate")
				});
			}
			return list;
		}
		finally
		{
			if (reader != null && !reader.IsClosed)
			{
				reader.Close();
			}
		}
	}

	private bool TrimManorLogsCore(int userId, int keep)
	{
		string sql = "DELETE FROM dbo.Sys_User_ManorLog WHERE UserID = " + userId
			+ " AND ID NOT IN (SELECT TOP(" + keep + ") ID FROM dbo.Sys_User_ManorLog WHERE UserID = " + userId + " ORDER BY ID DESC)";
		return db.Exesqlcomm(sql);
	}

	#endregion

	#region Hạn mức mua shop 139 theo tháng

	/// <summary>Danh sách hạn mức đã mua của shop 139 trong tháng (MonthKey = yyyyMM).</summary>
	public static List<UserManorShopLimitInfo> GetManorShopLimits(int userId, int monthKey)
	{
		if (userId <= 0)
		{
			return new List<UserManorShopLimitInfo>();
		}
		return Execute("GetManorShopLimits", (ManorBussiness b) => b.GetManorShopLimitsCore(userId, monthKey), new List<UserManorShopLimitInfo>());
	}

	/// <summary>Cộng thêm số lượng đã mua cho 1 goods của shop 139 (upsert).</summary>
	public static bool AddManorShopLimit(int userId, int goodsId, int monthKey, int count)
	{
		if (userId <= 0 || goodsId <= 0 || monthKey <= 0 || count <= 0)
		{
			return false;
		}
		return Execute("AddManorShopLimit", (ManorBussiness b) => b.AddManorShopLimitCore(userId, goodsId, monthKey, count), false);
	}

	/// <summary>Xóa hạn mức của các tháng cũ (MonthKey &lt; keepMonthKey).</summary>
	public static void ClearOldManorShopLimits(int userId, int keepMonthKey)
	{
		if (userId <= 0 || keepMonthKey <= 0)
		{
			return;
		}
		Execute("ClearOldManorShopLimits", (ManorBussiness b) => b.ClearOldManorShopLimitsCore(userId, keepMonthKey), false);
	}

	private List<UserManorShopLimitInfo> GetManorShopLimitsCore(int userId, int monthKey)
	{
		List<UserManorShopLimitInfo> list = new List<UserManorShopLimitInfo>();
		SqlDataReader reader = null;
		try
		{
			if (!db.FillSqlDataReader(ref reader, "SELECT ID, UserID, GoodsID, MonthKey, BuyCount, UpdateDate FROM dbo.Sys_User_ManorShopLimit WHERE UserID = " + userId + " AND MonthKey = " + monthKey))
			{
				return list;
			}
			while (reader != null && reader.Read())
			{
				list.Add(new UserManorShopLimitInfo
				{
					ID = ReaderInt(reader, "ID"),
					UserID = ReaderInt(reader, "UserID"),
					GoodsID = ReaderInt(reader, "GoodsID"),
					MonthKey = ReaderInt(reader, "MonthKey"),
					BuyCount = ReaderInt(reader, "BuyCount"),
					UpdateDate = ReaderDateTime(reader, "UpdateDate")
				});
			}
			return list;
		}
		finally
		{
			if (reader != null && !reader.IsClosed)
			{
				reader.Close();
			}
		}
	}

	private bool AddManorShopLimitCore(int userId, int goodsId, int monthKey, int count)
	{
		int existing = QueryInt("SELECT ID FROM dbo.Sys_User_ManorShopLimit WHERE UserID = " + userId + " AND GoodsID = " + goodsId + " AND MonthKey = " + monthKey);
		if (existing > 0)
		{
			return db.Exesqlcomm("UPDATE dbo.Sys_User_ManorShopLimit SET BuyCount = BuyCount + " + count + ", UpdateDate = GETDATE() WHERE ID = " + existing);
		}
		SqlParameter[] sqlParameters = new SqlParameter[5]
		{
			new SqlParameter("@UserID", SqlDbType.Int) { Value = userId },
			new SqlParameter("@GoodsID", SqlDbType.Int) { Value = goodsId },
			new SqlParameter("@MonthKey", SqlDbType.Int) { Value = monthKey },
			new SqlParameter("@BuyCount", SqlDbType.Int) { Value = count },
			new SqlParameter("@UpdateDate", SqlDbType.DateTime) { Value = DateTime.Now }
		};
		return db.Exesqlcomm(
			"INSERT INTO dbo.Sys_User_ManorShopLimit (UserID, GoodsID, MonthKey, BuyCount, UpdateDate) VALUES (@UserID, @GoodsID, @MonthKey, @BuyCount, @UpdateDate)",
			sqlParameters);
	}

	private bool ClearOldManorShopLimitsCore(int userId, int keepMonthKey)
	{
		return db.Exesqlcomm("DELETE FROM dbo.Sys_User_ManorShopLimit WHERE UserID = " + userId + " AND MonthKey < " + keepMonthKey);
	}

	#endregion
	#region Thưởng BXH tuần

	/// <summary>Tuần này đã chốt thưởng BXH chưa.</summary>
	public static bool IsManorWeekSettled(int weekKey)
	{
		if (weekKey <= 0)
		{
			return false;
		}
		return Execute("IsManorWeekSettled", (ManorBussiness b) => b.QueryInt("SELECT COUNT(*) FROM dbo.Sys_User_ManorRankSettle WHERE WeekKey = " + weekKey) > 0, false);
	}

	/// <summary>Đánh dấu tuần đã chốt thưởng (idempotent).</summary>
	public static bool MarkManorWeekSettled(int weekKey)
	{
		if (weekKey <= 0)
		{
			return false;
		}
		return Execute("MarkManorWeekSettled", (ManorBussiness b) => b.InsertSettle(weekKey), false);
	}

	/// <summary>Ghi nhận đã phát thưởng cho user (idempotent theo tuần/loại/user).</summary>
	public static bool AddManorRankReward(int weekKey, int rankType, int userId, int rank, int itemId)
	{
		if (weekKey <= 0 || rankType <= 0 || userId <= 0 || itemId <= 0)
		{
			return false;
		}
		return Execute("AddManorRankReward", (ManorBussiness b) => b.InsertRankReward(weekKey, rankType, userId, rank, itemId), false);
	}

	private bool InsertSettle(int weekKey)
	{
		if (QueryInt("SELECT COUNT(*) FROM dbo.Sys_User_ManorRankSettle WHERE WeekKey = " + weekKey) > 0)
		{
			return true;
		}
		SqlParameter[] sqlParameters = new SqlParameter[2]
		{
			new SqlParameter("@WeekKey", SqlDbType.Int) { Value = weekKey },
			new SqlParameter("@SettleDate", SqlDbType.DateTime) { Value = DateTime.Now }
		};
		return db.Exesqlcomm("INSERT INTO dbo.Sys_User_ManorRankSettle (WeekKey, SettleDate) VALUES (@WeekKey, @SettleDate)", sqlParameters);
	}

	private bool InsertRankReward(int weekKey, int rankType, int userId, int rank, int itemId)
	{
		if (QueryInt("SELECT COUNT(*) FROM dbo.Sys_User_ManorRankReward WHERE WeekKey = " + weekKey + " AND RankType = " + rankType + " AND UserID = " + userId) > 0)
		{
			return true;
		}
		SqlParameter[] sqlParameters = new SqlParameter[6]
		{
			new SqlParameter("@WeekKey", SqlDbType.Int) { Value = weekKey },
			new SqlParameter("@RankType", SqlDbType.Int) { Value = rankType },
			new SqlParameter("@UserID", SqlDbType.Int) { Value = userId },
			new SqlParameter("@Rank", SqlDbType.Int) { Value = rank },
			new SqlParameter("@ItemID", SqlDbType.Int) { Value = itemId },
			new SqlParameter("@CreateDate", SqlDbType.DateTime) { Value = DateTime.Now }
		};
		return db.Exesqlcomm("INSERT INTO dbo.Sys_User_ManorRankReward (WeekKey, RankType, UserID, [Rank], ItemID, CreateDate) VALUES (@WeekKey, @RankType, @UserID, @Rank, @ItemID, @CreateDate)", sqlParameters);
	}

	#endregion
}
