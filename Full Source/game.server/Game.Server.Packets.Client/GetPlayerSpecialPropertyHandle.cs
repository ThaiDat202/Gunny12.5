using System;
using System.Collections.Generic;
using Game.Base.Packets;
using Game.Server.Managers;

namespace Game.Server.Packets.Client;

[PacketHandler(321, "客户端日记")]
public class GetPlayerSpecialPropertyHandler : IPacketHandler
{
	// Phó bản: sub-type 4..11 trả số lần vượt ải còn lại theo (mapId, ải).
	// Thứ tự map khớp client: RoomManager.SPECIAL_BOSS_MAPID=[27,30,32], EMBLEM_BOSS_MAPID=[33,34,35,36,37].
	private static readonly int[] TrialDungeonMaps = new int[8] { 27, 30, 32, 33, 34, 35, 36, 37 };

	public int HandlePacket(GameClient client, GSPacketIn packet)
	{
		int num = packet.ReadInt();
		GSPacketIn gSPacketIn = new GSPacketIn(321);
		gSPacketIn.WriteInt(num);
		switch (num)
		{
			case 1:
				gSPacketIn.WriteInt(client.Player.PlayerCharacter.ticketNum);
				gSPacketIn.WriteInt((!BeautyVoteMgr.CanJoin(client.Player.PlayerId)) ? 1 : 0);
				break;
			case 2:
				gSPacketIn.WriteInt(3);
				gSPacketIn.WriteInt(0);
				gSPacketIn.WriteInt(client.Player.Extra.Info.coupleNum);
				gSPacketIn.WriteInt(1);
				gSPacketIn.WriteInt(client.Player.Extra.Info.dungeonNum);
				gSPacketIn.WriteInt(2);
				gSPacketIn.WriteInt(client.Player.Extra.Info.propsNum);
				break;
			case 3:
			{
				Dictionary<int, int> dungeonCount = client.Player.Extra.GetDungeonCountForClient();
				gSPacketIn.WriteInt(dungeonCount.Count);
				foreach (KeyValuePair<int, int> item in dungeonCount)
				{
					gSPacketIn.WriteInt(item.Key);
					gSPacketIn.WriteInt(item.Value);
				}
				break;
			}
			case 4:
			case 5:
			case 6:
			case 7:
			case 8:
			case 9:
			case 10:
			case 11:
			{
				int mapId = TrialDungeonMaps[num - 4];
				Dictionary<int, int> stages = client.Player.Extra.GetTrialDungeonCountForMap(mapId);
				gSPacketIn.WriteInt(stages.Count);
				foreach (KeyValuePair<int, int> item2 in stages)
				{
					gSPacketIn.WriteInt(item2.Key);
					gSPacketIn.WriteInt(item2.Value);
				}
				break;
			}
			case 40:
				gSPacketIn.WriteInt(0);
				break;
			default:
				Console.WriteLine("GETPLAYERSPECIAL: {0}", num);
				break;
		}
		client.Out.SendTCP(gSPacketIn);
		return 0;
	}
}
