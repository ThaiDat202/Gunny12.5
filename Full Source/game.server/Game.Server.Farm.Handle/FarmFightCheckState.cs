using Game.Base.Packets;
using Game.Server.Farm;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/41 — client kiểm tra trạng thái hoạt động (server trả 81/46).
/// </summary>
[FarmHandleAttbute(41)]
public class FarmFightCheckState : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		FarmActivityMgr.SendCurrentState(Player);
		return true;
	}
}
