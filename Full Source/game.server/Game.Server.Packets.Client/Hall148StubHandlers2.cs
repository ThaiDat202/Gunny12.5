using System;
using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Packets.Client;

/// <summary>
/// 14.8 — Lõi ngựa (748). Client đọc: Byte sub; case 1 (initAllData):
/// Int coreCount, mỗi core {Int ID, Int tempID, UTF proStr, Bool x3}, Int holeCount, mỗi hole {Int,Int,Int}, Int luckValue
/// </summary>
[PacketHandler(748, "Lõi ngựa (14.8 stub)")]
public class HorseCoreStubHandler : IPacketHandler
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
			GSPacketIn p = new GSPacketIn(748, player.PlayerCharacter.ID);
			p.WriteByte(1);
			p.WriteInt(0);
			p.WriteInt(0);
			p.WriteInt(0);
			player.SendTCP(p);
		}
		return 1;
	}
}

/// <summary>
/// 14.8 — Tỉ lệ GM (314). Client đọc 2 số nguyên: expRate, offerRate (x100).
/// </summary>
[PacketHandler(314, "Tỉ lệ GM (14.8 stub)")]
public class GameRateStubHandler : IPacketHandler
{
	public int HandlePacket(GameClient client, GSPacketIn packet)
	{
		GamePlayer player = client.Player;
		if (player?.PlayerCharacter == null)
		{
			return 1;
		}
		GSPacketIn p = new GSPacketIn(314, player.PlayerCharacter.ID);
		p.WriteInt(100);
		p.WriteInt(100);
		player.SendTCP(p);
		return 1;
	}
}

/// <summary>
/// 14.8 — Mục tiêu Guild (412). Subtype 1 client đọc 4 int (tiến độ 3 mục + 1).
/// </summary>
[PacketHandler(412, "Mục tiêu Guild (14.8 stub)")]
public class ConsortionActiveTargetStubHandler : IPacketHandler
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
			GSPacketIn p = new GSPacketIn(412, player.PlayerCharacter.ID);
			p.WriteByte(1);
			p.WriteInt(0);
			p.WriteInt(0);
			p.WriteInt(0);
			p.WriteInt(0);
			player.SendTCP(p);
		}
		return 1;
	}
}

/// <summary>
/// 14.8 — Mayland (681). Subtype 3 client đọc: Int count (danh sách nhiệm vụ).
/// </summary>
[PacketHandler(681, "Mayland (14.8 stub)")]
public class MaylandStubHandler : IPacketHandler
{
	public int HandlePacket(GameClient client, GSPacketIn packet)
	{
		GamePlayer player = client.Player;
		if (player?.PlayerCharacter == null)
		{
			return 1;
		}
		byte sub = packet.ReadByte();
		if (sub == 3)
		{
			GSPacketIn p = new GSPacketIn(681, player.PlayerCharacter.ID);
			p.WriteByte(3);
			p.WriteInt(0);
			player.SendTCP(p);
		}
		return 1;
	}
}
