using System;

namespace SqlDataProvider.Data;

/// <summary>
/// Dữ liệu 1 dòng BXH nông trại (ManorWealth.xml / ManorAbility.xml — client FarmRankInfo).
/// </summary>
public class UserManorRankInfo
{
	public int UserID { get; set; }
	public string NickName { get; set; }
	public int Sex { get; set; }
	public string Style { get; set; }
	public string Colors { get; set; }

	/// <summary>Exp nông trại (cho BXH Tài Phú: TotalGP).</summary>
	public int Exp { get; set; }

	/// <summary>Tiền nông trại (cho BXH Tài Phú: Money/Wealth).</summary>
	public int Money { get; set; }

	public int FarmExp { get; set; }
	public int FishExp { get; set; }
	public int MineExp { get; set; }
	public int RanchExp { get; set; }

	/// <summary>Tên chủ hiện tại nếu người này đang làm thợ (BXH Lao Động).</summary>
	public string BossNickName { get; set; }
}
