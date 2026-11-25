using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/9 = EAT_FOOD — cho thợ ăn (giảm goods bag 10, tăng energy).
/// request: [int workerUserID][int foodTemplateID][int count]
/// </summary>
[FarmHandleAttbute(9)]
public class WorkerEatFood : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int workerUserId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		int foodTemplateId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		int count = (packet.DataLeft >= 4) ? packet.ReadInt() : 1;
		Player.Manor.FeedWorker(workerUserId, foodTemplateId, count);
		return true;
	}
}
