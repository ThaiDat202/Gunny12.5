using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// Client Gunny 12.6 (NÃ´ng tráº¡i 2.0) dÃ¹ng subtype 81/34 = PET_FOOD_QUERY.
/// Client cáº§n sá»‘ lÆ°á»£t mua thá»©c Äƒn thÃº cÃ²n láº¡i (buyExpRemainNum) Ä‘á»ƒ hiá»ƒn thá»‹ nÃºt "Mua".
/// </summary>
[FarmHandleAttbute(34)]
public class FarmPetFoodQuery : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		Player.Manor.ProcessGrowth(true);
		Player.Manor.SendPetFoodCount();
		return true;
	}
}

