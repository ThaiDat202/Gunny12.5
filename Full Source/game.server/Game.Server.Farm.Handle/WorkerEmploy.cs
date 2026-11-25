using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/2 = EMP_LOYD — thuê thợ / mua đứt thợ.
/// request: [int workerUserID]
/// </summary>
[FarmHandleAttbute(2)]
public class WorkerEmploy : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int workerUserId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		Player.Manor.HireWorker(workerUserId);
		return true;
	}
}
