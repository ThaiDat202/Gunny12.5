using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/3 = ADD_TIME — mua bảo vệ hợp đồng cho thợ (N ngày, Xu).
/// request: [int workerUserID][int days]
/// </summary>
[FarmHandleAttbute(3)]
public class WorkerAddTime : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int workerUserId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		int days = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		Player.Manor.AddWorkerTime(workerUserId, days);
		return true;
	}
}
