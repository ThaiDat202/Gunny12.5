using Game.Base.Packets;
using Game.Server.Farm;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/36 — client vào farm đang hoạt động, xin danh sách người chơi + quái (81/36).
/// </summary>
[FarmHandleAttbute(36)]
public class FarmFightSceneInfo : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		FarmActivityMgr.HandleSceneRequest(Player);
		return true;
	}
}
