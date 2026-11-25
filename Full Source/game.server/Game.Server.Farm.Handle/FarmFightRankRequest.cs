using Game.Base.Packets;
using Game.Server.Farm;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/40 — client xin BXH điểm hoạt động Sâu Bọ của farm đang xem.
/// </summary>
[FarmHandleAttbute(40)]
public class FarmFightRankRequest : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		FarmActivityMgr.RequestRank(Player);
		return true;
	}
}
