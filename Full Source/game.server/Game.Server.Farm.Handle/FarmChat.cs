using Game.Base.Packets;
using Game.Server.Farm;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/38 — chat kênh nông trại. Client gửi [byte channel=38][UTF msg];
/// server relay bằng packet 19 (scene chat) cho mọi người đang xem cùng nông trại (kể cả người gửi).
/// </summary>
[FarmHandleAttbute(38)]
public class FarmChat : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		byte channel = (packet.DataLeft >= 1) ? packet.ReadByte() : (byte)38;
		string msg = (packet.DataLeft >= 2) ? packet.ReadString() : string.Empty;
		FarmSceneTracker.SendFarmChat(Player, channel, msg);
		return true;
	}
}
