using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/1 — client gửi khi mở bảng Quản lý nông trại (sendFarmReqFarmInfo, không payload).
/// Response 81/1: title(UTF), isEmploy(bool), employProtectDate(Date), int, isProtect(bool),
/// protectDate(Date), int, ransomMoney, protectMoney, skillTotalGrade, farmExp, fishExp, mineExp,
/// ranchExp, bossID (nếu !=0 đọc tiếp boss info), logCount + log strings.
/// </summary>
[FarmHandleAttbute(1)]
public class ManageInfo : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		Player.Manor.SendManageInfo();
		return true;
	}
}
