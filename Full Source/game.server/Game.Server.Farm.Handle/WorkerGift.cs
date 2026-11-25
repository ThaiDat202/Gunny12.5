using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/22 = GIFT — tặng quà thợ (Xu, tăng feeling).
/// request: [int workerUserID]
/// </summary>
[FarmHandleAttbute(22)]
public class WorkerGift : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int workerUserId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		Player.Manor.WorkerGift(workerUserId);
		return true;
	}
}
