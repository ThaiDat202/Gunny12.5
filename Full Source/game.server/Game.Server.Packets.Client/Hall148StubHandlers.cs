using System;
using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Packets.Client;

/// <summary>
/// 14.8 — Bảng xếp hạng nạp (352): trả về "không có hoạt động" để client bỏ qua.
/// Layout client đọc: UTF actId, Boolean isOpen, (nếu isOpen: Int status ...)
/// </summary>
[PacketHandler(352, "Bảng xếp hạng nạp (14.8 stub)")]
public class RechargeRankStubHandler : IPacketHandler
{
	public int HandlePacket(GameClient client, GSPacketIn packet)
	{
		GamePlayer player = client.Player;
		if (player?.PlayerCharacter == null)
		{
			return 1;
		}
		GSPacketIn p = new GSPacketIn(352, player.PlayerCharacter.ID);
		p.WriteString(string.Empty);
		p.WriteBoolean(false);
		player.SendTCP(p);
		return 1;
	}
}

/// <summary>
/// 14.8 — Đột phá văn hóa (759). Client đọc: subtype 1 -> Int count + danh sách.
/// Trả danh sách rỗng.
/// </summary>
[PacketHandler(759, "Đột phá văn hóa (14.8 stub)")]
public class BreakthroughStubHandler : IPacketHandler
{
	public int HandlePacket(GameClient client, GSPacketIn packet)
	{
		GamePlayer player = client.Player;
		if (player?.PlayerCharacter == null)
		{
			return 1;
		}
		byte sub = packet.ReadByte();
		if (sub == 1)
		{
			GSPacketIn p = new GSPacketIn(759, player.PlayerCharacter.ID);
			p.WriteByte(1);
			p.WriteInt(0);
			player.SendTCP(p);
		}
		return 1;
	}
}

/// <summary>
/// 14.8 — Luyện đồ dự bị (766). Client đọc subtype 1:
/// Int curPoolPoint, Int maxPoint, Int todayNormalPoolScore, Date lastUpdate, Int count(+pairs)
/// </summary>
[PacketHandler(766, "Luyện đồ dự bị (14.8 stub)")]
public class StoreDeputyRefineStubHandler : IPacketHandler
{
	public int HandlePacket(GameClient client, GSPacketIn packet)
	{
		GamePlayer player = client.Player;
		if (player?.PlayerCharacter == null)
		{
			return 1;
		}
		byte sub = packet.ReadByte();
		if (sub == 1)
		{
			GSPacketIn p = new GSPacketIn(766, player.PlayerCharacter.ID);
			p.WriteByte(1);
			p.WriteInt(0);
			p.WriteInt(0);
			p.WriteInt(0);
			p.WriteDateTime(DateTime.Now);
			p.WriteInt(0);
			player.SendTCP(p);
		}
		return 1;
	}
}

/// <summary>
/// 14.8 — Forces Relic (768). Client đọc subtype 1:
/// Int shopScore, Int count (+ cặp Int,Int)
/// </summary>
[PacketHandler(768, "Forces Relic (14.8 stub)")]
public class ForcesRelicStubHandler : IPacketHandler
{
	public int HandlePacket(GameClient client, GSPacketIn packet)
	{
		GamePlayer player = client.Player;
		if (player?.PlayerCharacter == null)
		{
			return 1;
		}
		byte sub = packet.ReadByte();
		if (sub == 1)
		{
			GSPacketIn p = new GSPacketIn(768, player.PlayerCharacter.ID);
			p.WriteByte(1);
			p.WriteInt(0);
			p.WriteInt(0);
			player.SendTCP(p);
		}
		return 1;
	}
}
