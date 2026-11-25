using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/5 = FIRE_EMP — sa thải thợ.
/// request: [int workerUserID] → trả 81/2 success=false
/// </summary>
[FarmHandleAttbute(5)]
public class WorkerFire : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int workerUserId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		Player.Manor.FireWorker(workerUserId);
		return true;
	}
}
