using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/21 = LOVE — yêu thương thợ (tăng feeling).
/// request: [int workerUserID]
/// </summary>
[FarmHandleAttbute(21)]
public class WorkerLove : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int workerUserId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		Player.Manor.WorkerLove(workerUserId);
		return true;
	}
}
