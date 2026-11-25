using Game.Base.Packets;
using Game.Server.Farm;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/37 — client di chuyển trong farm hoạt động; server broadcast 81/37 cho người cùng farm.
/// </summary>
[FarmHandleAttbute(37)]
public class FarmFightMove : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		int x = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		int y = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		FarmActivityMgr.Move(Player, x, y);
		return true;
	}
}
