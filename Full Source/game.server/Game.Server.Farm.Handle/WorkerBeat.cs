using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/20 = BEAT — đánh thợ (giảm feeling).
/// request: [int workerUserID]
/// </summary>
[FarmHandleAttbute(20)]
public class WorkerBeat : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int workerUserId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		Player.Manor.WorkerBeat(workerUserId);
		return true;
	}
}
