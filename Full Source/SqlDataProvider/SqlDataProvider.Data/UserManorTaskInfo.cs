using System;

namespace SqlDataProvider.Data;

/// <summary>
/// Một nhiệm vụ bảng tin (uỷ thác) được giao cho user theo ngày —
/// bảng dbo.Sys_User_ManorTask. Unique theo (UserID, TaskDate, TaskId).
/// Mapping protocol:
///   81/18 TASK_BEGIN  : id = TaskId, templateID = TemplateId,
///                       condition1..3 = Condition1..3,
///                       isComplete/isReward = IsComplete/IsReward.
///   81/19 TASK_REWARD : client gửi lại TaskId (instance id).
///   81/25             : tăng Condition1 (số thợ lười đã đập) cho task TaskType 1.
/// Điều kiện gốc (đích) của task nằm trong TS_ManorTask.xml, không lưu DB.
/// </summary>
public class UserManorTaskInfo : DataObject
{
	/// <summary>ID identity trong DB (0 = chưa insert).</summary>
	public int ID { get; set; }

	/// <summary>UserID chủ sở hữu task.</summary>
	public int UserID { get; set; }

	/// <summary>Ngày giao task (luôn chuẩn hoá về 00:00:00 — reset theo ngày).</summary>
	public DateTime TaskDate { get; set; }

	/// <summary>Instance id gửi client trong 81/18; client gửi lại trong 81/19.</summary>
	public int TaskId { get; set; }

	/// <summary>TemplateID nhiệm vụ trong TS_ManorTask.xml (1..23).</summary>
	public int TemplateId { get; set; }

	/// <summary>Tiến độ hiện tại 1 (nghĩa theo TaskType: số thợ lười đã đập / tiến độ nộp).</summary>
	public int Condition1 { get; set; }

	/// <summary>Tiến độ hiện tại 2.</summary>
	public int Condition2 { get; set; }

	/// <summary>Tiến độ hiện tại 3.</summary>
	public int Condition3 { get; set; }

	/// <summary>Đã đạt điều kiện hoàn thành chưa (client bật nút Nộp/Hoàn thành).</summary>
	public bool IsComplete { get; set; }

	/// <summary>Đã nhận thưởng chưa (client ẩn nút + đếm "Hôm nay đã xong uỷ thác").</summary>
	public bool IsReward { get; set; }

	/// <summary>Lần ghi dòng cuối cùng (dự phòng, debug).</summary>
	public DateTime UpdateDate { get; set; }
}
