using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// Client 12.6 dùng 81/30 = WORKER_LIST.
/// request : [UTF keyword] (rỗng = tất cả)
/// response: danh sách ứng viên thuê thợ (xem PlayerManor.SendWorkerList).
/// </summary>
[FarmHandleAttbute(30)]
public class WorkerList : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		string keyword = (packet.DataLeft >= 2) ? packet.ReadString() : string.Empty;
		Player.Manor.SendWorkerList(keyword);
		return true;
	}
}
