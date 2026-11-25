using System;

namespace SqlDataProvider.Data;

/// <summary>
/// Một thợ (worker) làm việc tại nông trại — bảng dbo.Sys_User_ManorWorker.
/// Unique theo (OwnerID, WorkerUserID).
/// Mapping các subtype worker 81/2 (hire), 81/30 (list), 81/12-13 (work begin/end),
/// 81/20-22 (beat/love/gift), 81/3 (protect), 81/4 (title), 81/8 (lock time)...
/// </summary>
public class UserManorWorkerInfo : DataObject
{
	/// <summary>ID identity trong DB (0 = chưa insert).</summary>
	public int ID { get; set; }

	/// <summary>UserID chủ nông trại (người thuê thợ).</summary>
	public int OwnerID { get; set; }

	/// <summary>UserID của nhân vật làm thợ.</summary>
	public int WorkerUserID { get; set; }

	/// <summary>Tên hiển thị của thợ.</summary>
	public string Name { get; set; }

	/// <summary>Danh hiệu/chức danh của thợ (đặt qua 81/4).</summary>
	public string Title { get; set; }

	/// <summary>Tâm trạng (feeling) 0..100.</summary>
	public int Feeling { get; set; }

	/// <summary>Thể lực (energy).</summary>
	public int Energy { get; set; }

	/// <summary>Exp nghề nông.</summary>
	public int FarmExp { get; set; }

	/// <summary>Exp nghề câu cá.</summary>
	public int FishExp { get; set; }

	/// <summary>Exp nghề mỏ.</summary>
	public int MineExp { get; set; }

	/// <summary>Exp nghề đồng cỏ/ranch.</summary>
	public int RanchExp { get; set; }

	/// <summary>Thợ đang làm việc hay không.</summary>
	public bool IsWorking { get; set; }

	/// <summary>Loại công việc đang làm (khớp AreaType 1..4 hoặc enum server).</summary>
	public int WorkType { get; set; }

	/// <summary>Thời điểm bắt đầu làm việc.</summary>
	public DateTime WorkBeginDate { get; set; }

	/// <summary>Mốc hết hạn bảo hộ (không bị đánh/trộm) — 81/3.</summary>
	public DateTime EmployProtectDate { get; set; }

	/// <summary>Giá bảo hộ.</summary>
	public int ProtectPrice { get; set; }

	/// <summary>Giới tính của thợ.</summary>
	public bool Sex { get; set; }

	/// <summary>Màu sắc (chuỗi màu client gửi, vd "1,2,3").</summary>
	public string Colors { get; set; }

	/// <summary>Kiểu dáng/style (chuỗi style client gửi).</summary>
	public string Style { get; set; }

	/// <summary>Thời điểm thuê thợ.</summary>
	public DateTime HiredDate { get; set; }
}
