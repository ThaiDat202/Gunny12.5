using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/4 = SET_TITLE — đổi danh hiệu cho thợ (Xu).
/// request: [int workerUserID][UTF title][bool isBind]
/// </summary>
[FarmHandleAttbute(4)]
public class WorkerSetTitle : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int workerUserId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		string title = (packet.DataLeft >= 2) ? packet.ReadString() : string.Empty;
		bool isBind = (packet.DataLeft >= 1) ? packet.ReadBoolean() : false;
		Player.Manor.SetWorkerTitle(workerUserId, title, isBind);
		return true;
	}
}
