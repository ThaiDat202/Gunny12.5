using Game.Base.Packets;
using Game.Server.Farm;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/42 — client click quái Sâu Bọ: server xử lý diệt quái + điểm + push 81/43/81/40.
/// </summary>
[FarmHandleAttbute(42)]
public class FarmFightClickMaster : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		int masterId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		FarmActivityMgr.ClickMaster(Player, masterId);
		return true;
	}
}
