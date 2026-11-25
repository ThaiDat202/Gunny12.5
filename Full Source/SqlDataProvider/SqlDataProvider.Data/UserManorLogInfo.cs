using System;

namespace SqlDataProvider.Data;

/// <summary>Một dòng nhật ký nông trại (bảng Sys_User_ManorLog) — hiển thị trong 81/1.</summary>
public class UserManorLogInfo
{
	public int ID { get; set; }
	public int UserID { get; set; }
	public int LogType { get; set; }
	public string Content { get; set; }
	public DateTime CreateDate { get; set; }
}
