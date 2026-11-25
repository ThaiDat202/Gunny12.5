using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/15 — mua vật phẩm shop nông trại (ShopID 135):
/// [int templateID][int count][int moneyType] (moneyType 1 = tiền nông trại, 2 = DDTMoney/vé, 3 = Money).
/// </summary>
[FarmHandleAttbute(15)]
public class FarmBuyGoods : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int templateId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		int count = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		int moneyType = (packet.DataLeft >= 4) ? packet.ReadInt() : 1;
		return Player.Manor.BuyGoods(templateId, count, moneyType);
	}
}
