using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/19 = TASK_FINISH — nộp/hoàn thành nhiệm vụ.
/// request: [int taskId] (TaskId = TemplateId)
/// </summary>
[FarmHandleAttbute(19)]
public class TaskFinish : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int taskId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		Player.Manor.FinishTask(taskId);
		return true;
	}
}
