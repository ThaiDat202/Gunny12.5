using System;

namespace SqlDataProvider.Data;

/// <summary>
/// Trạng thái một khu (object) của Nông trại 2.0 — bảng dbo.Sys_User_ManorArea.
/// Mỗi user có 6 dòng AreaType 1..6:
///   1 = FARM (đất trồng), 2 = FISH (câu cá), 3 = MINE (mỏ),
///   4 = RANCH (đồng cỏ), 5 = FARM_LAND (đất mở rộng), 6 = HOE_UPGRADE (cuốc/nâng cấp).
/// Mapping protocol 81/24: mỗi object gồm
///   type, level, isPlant, plantTempId, plantDate, plantRipeDate(=RipeDate),
///   foodTempId, foodDate. LevelTwo là landLevel (type 1) / mineLevel (type 3).
/// </summary>
public class UserManorAreaInfo : DataObject
{
	/// <summary>ID identity trong DB (0 = chưa insert).</summary>
	public int ID { get; set; }

	/// <summary>UserID chủ nông trại.</summary>
	public int UserID { get; set; }

	/// <summary>Loại khu: 1..6 (xem summary class).</summary>
	public int AreaType { get; set; }

	/// <summary>Cấp/grade hiện tại của khu.</summary>
	public int Level { get; set; }

	/// <summary>Cấp phụ: landLevel với AreaType 1, mineLevel với AreaType 3.</summary>
	public int LevelTwo { get; set; }

	/// <summary>Khu đang có cây trồng hay không.</summary>
	public bool IsPlant { get; set; }

	/// <summary>TemplateID cây đang trồng (TS_ManorPlant).</summary>
	public int PlantTempId { get; set; }

	/// <summary>Thời điểm gieo trồng.</summary>
	public DateTime PlantDate { get; set; }

	/// <summary>Mốc/thời gian chín (int theo protocol — thường là số giây còn lại hoặc unix time).</summary>
	public int RipeDate { get; set; }

	/// <summary>TemplateID thức ăn đang dùng cho thú (protocol gửi foodTempId).</summary>
	public int FoodTempId { get; set; }

	/// <summary>Thời điểm cho thú ăn / dùng thức ăn.</summary>
	public DateTime FoodDate { get; set; }

	/// <summary>Sản lượng đã thu hoạch (cột dự phòng, phục vụ thống kê/nhiệm vụ).</summary>
	public int ProduceCount { get; set; }

	/// <summary>Lần cập nhật dòng cuối cùng.</summary>
	public DateTime LastUpdate { get; set; }
}
