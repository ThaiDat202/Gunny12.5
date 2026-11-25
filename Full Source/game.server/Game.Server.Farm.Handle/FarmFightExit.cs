using Game.Base.Packets;
using Game.Server.Farm;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/39 — client rời scene farm/hoạt động; server broadcast 81/39 và xoá khỏi danh sách người xem.
/// </summary>
[FarmHandleAttbute(39)]
public class FarmFightExit : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		FarmActivityMgr.Exit(Player);
		return true;
	}
}
