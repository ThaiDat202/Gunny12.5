using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/18 = TASK_BEGIN — client mở bảng tin, xin danh sách nhiệm vụ.
/// request: rỗng → response 81/18 (flag=1).
/// </summary>
[FarmHandleAttbute(18)]
public class TaskBegin : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		Player.Manor.RefreshTaskProgress();
		Player.Manor.SendTaskList();
		return true;
	}
}
