using System;

namespace SqlDataProvider.Data;

/// <summary>Hạn mức mua cá nhân của shop nông trại 139 theo tháng (bảng Sys_User_ManorShopLimit).</summary>
public class UserManorShopLimitInfo
{
	public int ID { get; set; }
	public int UserID { get; set; }
	public int GoodsID { get; set; }
	public int MonthKey { get; set; }
	public int BuyCount { get; set; }
	public DateTime UpdateDate { get; set; }
}
