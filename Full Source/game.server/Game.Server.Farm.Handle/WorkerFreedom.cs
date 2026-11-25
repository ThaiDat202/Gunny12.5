using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/6 = FREEDOM — tự chuộc thân khi đang làm thợ cho nông trại khác.
/// request: rỗng
/// </summary>
[FarmHandleAttbute(6)]
public class WorkerFreedom : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		Player.Manor.Freedom();
		return true;
	}
}
