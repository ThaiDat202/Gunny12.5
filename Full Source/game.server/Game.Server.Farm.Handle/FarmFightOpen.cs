using Game.Base.Packets;
using Game.Server.Farm;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/45 — mở hoạt động Sâu Bọ cho nông trại của mình (kiểm tra khung giờ trong ServerConfig).
/// </summary>
[FarmHandleAttbute(45)]
public class FarmFightOpen : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		FarmActivityMgr.Open(Player);
		return true;
	}
}
