using System;

namespace SqlDataProvider.Data;

/// <summary>
/// Trạng thái tổng của Nông trại 2.0 (Manor) của một user — bảng dbo.Sys_User_Manor.
/// Mapping protocol 81/24 (REQUEST_FARM_INFO): Exp, BuffRate, BuffDate,
/// landLevel = Area type 1 LevelTwo, mineLevel = Area type 3 LevelTwo.
/// </summary>
public class UserManorInfo : DataObject
{
	/// <summary>UserID chủ nông trại (khoá chính).</summary>
	public int UserID { get; set; }

	/// <summary>Exp nông trại.</summary>
	public int Exp { get; set; }

	/// <summary>Tiền nông trại (selfMoney).</summary>
	public int Money { get; set; }

	/// <summary>Giá trị nông trại (selfValue).</summary>
	public int Value { get; set; }

	/// <summary>Exp value của nông trại (selfExp).</summary>
	public int ExpValue { get; set; }

	/// <summary>Số lượt mua thức ăn còn lại (dùng cho 81/34 FarmPetFoodQuery).</summary>
	public int BuyFoodCount { get; set; }

	/// <summary>Tỉ lệ buff nông trại (%).</summary>
	public int BuffRate { get; set; }

	/// <summary>Thời điểm buff bắt đầu/hết hạn.</summary>
	public DateTime BuffDate { get; set; }

	/// <summary>Hạn tự bảo vệ bản thân (không bị thuê/mua đứt); 1900 = không bảo vệ.</summary>
	public DateTime ProtectDate { get; set; }

	/// <summary>Ngày reset lượt mua thức ăn pet (BuyFoodCount reset đầu ngày).</summary>
	public DateTime BuyFoodDate { get; set; }

	/// <summary>Lần cập nhật dòng cuối cùng.</summary>
	public DateTime LastUpdate { get; set; }
}
