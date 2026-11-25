using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Packets.Client;

/// <summary>
/// 14.8 — Tổng thành tựu &amp; Danh hiệu (opcode 736).
/// Client 14.8 yêu cầu: 1 = lấy toàn bộ, 2 = nhận thưởng theo ID, 4 = điểm shop.
/// Server 13.0 chưa có hệ thống này nên trả dữ liệu rỗng đúng layout để client
/// không treo / không lỗi parse.
/// </summary>
[PacketHandler(736, "Tổng thành tựu và danh hiệu (14.8)")]
public class TotalAchvAndTitleHandler : IPacketHandler
{
	public int HandlePacket(GameClient client, GSPacketIn packet)
	{
		GamePlayer player = client.Player;
		if (player == null || player.PlayerCharacter == null)
		{
			return 1;
		}
		byte sub = packet.ReadByte();
		switch (sub)
		{
		case 1:
		{
			GSPacketIn p = new GSPacketIn(736, player.PlayerCharacter.ID);
			p.WriteByte(1);
			p.WriteInt(0);
			p.WriteInt(0);
			player.SendTCP(p);
			break;
		}
		case 2:
		{
			int achvId = packet.ReadInt();
			GSPacketIn p = new GSPacketIn(736, player.PlayerCharacter.ID);
			p.WriteByte(3);
			p.WriteInt(1);
			p.WriteInt(achvId);
			p.WriteBoolean(false);
			p.WriteBoolean(false);
			player.SendTCP(p);
			break;
		}
		case 4:
		{
			GSPacketIn p = new GSPacketIn(736, player.PlayerCharacter.ID);
			p.WriteByte(4);
			p.WriteInt(0);
			player.SendTCP(p);
			break;
		}
		}
		return 1;
	}
}
