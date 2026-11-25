using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.Farm;
using Game.Server.GameUtils;

namespace Game.Server.Farm.Handle;

/// <summary>
/// Client Gunny 12.6 (NÃ´ng tráº¡i 2.0) dÃ¹ng subtype 81/24 = REQUEST_FARM_INFO.
/// request : [int farmUserID]
/// response: farm info (xem PlayerManor.SendManorInfo) â€” 6 khu, thá»£, goods, exp...
/// Náº¿u lÃ  nÃ´ng tráº¡i cá»§a chÃ­nh mÃ¬nh thÃ¬ gá»­i kÃ¨m 81/26 (tiá»n/value/exp + tÃºi nÃ´ng sáº£n)
/// vÃ  81/34 (sá»‘ lÆ°á»£t mua thá»©c Äƒn pet) Ä‘á»ƒ má»Ÿ Ä‘á»§ giao diá»‡n.
/// </summary>
[FarmHandleAttbute(24)]
public class RequestFarmInfo : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		int farmUserID = (packet.DataLeft >= 4) ? packet.ReadInt() : Player.PlayerId;
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		Player.Manor.ProcessGrowth(true);
		FarmSceneTracker.Enter(Player, farmUserID);
		PlayerManor.SendManorInfo(Player, farmUserID, Player.Manor);
		FarmActivityMgr.NotifyIfFighting(Player, farmUserID);
		if (farmUserID == Player.PlayerId && Player.Manor.IsLoaded)
		{
			Player.Manor.SendUpdateGoods();
			Player.Manor.SendPetFoodCount();
			Player.Manor.SendTaskList();
		}
		return true;
	}
}

