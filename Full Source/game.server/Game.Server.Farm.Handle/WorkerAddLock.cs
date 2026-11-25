using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/8 = ADD_LOCK_TIME — mua tự bảo vệ bản thân (chưa hỗ trợ đầy đủ, trả lại 81/1).
/// request: [int days]
/// </summary>
[FarmHandleAttbute(8)]
public class WorkerAddLock : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int days = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		Player.Manor.BuySelfProtect(days);
		return true;
	}
}
