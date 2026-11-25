using System.Collections.Generic;
using Bussiness.Managers;
using Game.Base.Packets;
using SqlDataProvider.Data;

namespace Game.Server.Packets.Client;

[PacketHandler(528, "user ac action")]
public class ShopPersonalLimitHandler : IPacketHandler
{
	public int HandlePacket(GameClient client, GSPacketIn packet)
	{
		// Client gửi [int shopID] (farm shop 139: GameSocketOut.sendPersonalLimitShop(139)).
		// Trả về [int shopID][int count]{[int goodsID][int boughtCount]} để client tính
		// personalBuyCnt = LimitPersonalCount - boughtCount (ShopManager.as:60-81).
		int shopID = packet.ReadInt();
		List<ShopItemInfo> items = ShopMgr.FindShopbyShopID(shopID);
		if (items == null)
		{
			items = new List<ShopItemInfo>();
		}
		Game.Server.GameUtils.PlayerManor manor = client.Player.Manor;
		if (shopID == 139 && manor != null)
		{
			if (!manor.IsLoaded)
			{
				manor.LoadFromDatabase();
			}
			manor.EnsureShopMonth();
		}
		GSPacketIn gSPacketIn = new GSPacketIn(528);
		gSPacketIn.WriteInt(shopID);
		gSPacketIn.WriteInt(items.Count);
		foreach (ShopItemInfo item in items)
		{
			gSPacketIn.WriteInt(item.ID);
			gSPacketIn.WriteInt((shopID == 139 && manor != null && manor.IsLoaded) ? manor.GetShop139Bought(item.ID) : 0);
		}
		client.SendTCP(gSPacketIn);
		return 1;
	}
}
