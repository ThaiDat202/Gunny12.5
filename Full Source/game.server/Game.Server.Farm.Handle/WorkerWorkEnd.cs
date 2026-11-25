using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/13 = WORK_END — cho thợ nghỉ.
/// request: [int workType][int workerUserID]
/// </summary>
[FarmHandleAttbute(13)]
public class WorkerWorkEnd : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int workType = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		int workerUserId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		Player.Manor.WorkerWorkEnd(workType, workerUserId);
		return true;
	}
}
