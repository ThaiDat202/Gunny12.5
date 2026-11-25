using Game.Base.Packets;
using Game.Server.GameObjects;
using SqlDataProvider.Data;

namespace Game.Server.Farm.Handle;

/// <summary>
/// Client Gunny 12.6 (NÃ´ng tráº¡i 2.0) dÃ¹ng subtype 81/27 = UPDATE_PLANT_DATA
/// (FarmMainView gá»­i má»—i 60 giÃ¢y Ä‘á»ƒ xin dá»¯ liá»‡u cÃ¢y trá»“ng/thÃº má»›i).
/// Server tráº£ láº¡i tráº¡ng thÃ¡i cÃ¡c khu (81/17) + tÃºi nÃ´ng sáº£n/tiá»n (81/26).
/// </summary>
[FarmHandleAttbute(27)]
public class FarmUpdatePlantData : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		Player.Manor.ProcessGrowth(true);
		foreach (UserManorAreaInfo area in Player.Manor.GetAreas())
		{
			Player.Manor.SendUpdateArea(area);
		}
		Player.Manor.SendUpdateGoods();
		Player.Manor.SendTaskList();
		return true;
	}
}

