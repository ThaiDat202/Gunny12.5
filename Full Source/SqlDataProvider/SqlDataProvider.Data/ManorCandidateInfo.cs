using System;

namespace SqlDataProvider.Data;

/// <summary>
/// Một ứng viên để tuyển làm thợ nông trại — kết quả JOIN tạm giữa
/// dbo.Sys_Users_Detail và dbo.Sys_User_ManorWorker (không có bảng riêng).
/// Mapping protocol 81/30 (WORKER_LIST):
///   userid = UserId, name = NickName, value = Grade,
///   sex = (Sex != 0), colors = Colors, style = Style, bossID = OwnerID.
/// OwnerID = 0  → ứng viên chưa bị ai thuê (thuê bằng Cỏ Vàng).
/// OwnerID != 0 → đang làm cho chủ khác (muốn lấy phải "mua đứt" bằng Xu).
/// </summary>
public class ManorCandidateInfo : DataObject
{
	/// <summary>UserID của ứng viên (Sys_Users_Detail.UserID).</summary>
	public int UserId { get; set; }

	/// <summary>Tên nhân vật (Sys_Users_Detail.NickName).</summary>
	public string NickName { get; set; }

	/// <summary>Cấp nhân vật — dùng xếp hạng danh sách ứng viên.</summary>
	public int Grade { get; set; }

	/// <summary>Giới tính: 0 = nam, 1 = nữ (bit Sex trong DB, đọc ra int).</summary>
	public int Sex { get; set; }

	/// <summary>Chuỗi màu của nhân vật (client dùng để hiện avatar).</summary>
	public string Colors { get; set; }

	/// <summary>Chuỗi style/hình thể của nhân vật (client dùng để hiện avatar).</summary>
	public string Style { get; set; }

	/// <summary>UserID chủ hiện tại (0 = chưa bị ai thuê).</summary>
	public int OwnerID { get; set; }

	/// <summary>Hạn tự bảo vệ của ứng viên (1900 = không bảo vệ).</summary>
	public DateTime ProtectDate { get; set; }
}
