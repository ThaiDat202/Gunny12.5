using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/12 = WORK_BEGIN — cho thợ bắt đầu làm việc ở khu (1 farm, 2 fish, 3 mine, 4 ranch).
/// request: [int workType][int workerUserID]
/// </summary>
[FarmHandleAttbute(12)]
public class WorkerWorkBegin : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int workType = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		int workerUserId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		Player.Manor.WorkerWorkBegin(workType, workerUserId);
		return true;
	}
}
