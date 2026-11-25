using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>81/31 — bán nông sản lấy tiền nông trại (giá = ReclaimValue trong TemplateManorList).</summary>
[FarmHandleAttbute(31)]
public class FarmSellGoods : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int templateId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		int count = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		return Player.Manor.SellGoods(templateId, count);
	}
}
