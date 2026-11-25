using System.Collections.Generic;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.GameUtils;

namespace Game.Server.Farm.Handle;

/// <summary>
/// Client Gunny 12.6 (Nông trại 2.0) dùng subtype 81/33 = SLACKER_QUERY:
///   request : [int count][int userID...] (danh sách friendID)
///   response: [int count][int userID...] (friend đang có thợ "lười" = đang làm nhưng tâm trạng thấp)
/// </summary>
[FarmHandleAttbute(33)]
public class FeedFarmPoultry : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int count = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		List<int> friends = new List<int>();
		for (int i = 0; i < count && packet.DataLeft >= 4; i++)
		{
			friends.Add(packet.ReadInt());
		}
		Player.Manor.SendSlackerQuery(PlayerManor.GetSlackerFriends(friends.ToArray()));
		return true;
	}
}
