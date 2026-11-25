using System;
using Bussiness;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.GameUtils;
using SqlDataProvider.Data;

namespace Game.Server.Packets.Client;

/// <summary>
/// Packet 143 - dùng thẻ "Chúc phúc thần gà" (item CategoryID=11, Property1=39,
/// Property2 = 1/2/3 tương ứng gói 30/90/180 ngày).
/// Trước đây server KHÔNG có handler 143 nên bấm dùng thẻ trong túi không có tác dụng.
/// </summary>
[PacketHandler(143, "Chúc phúc thần gà - dùng thẻ")]
public class KingBlessItemHandler : IPacketHandler
{
	public int HandlePacket(GameClient client, GSPacketIn packet)
	{
		if (client == null || client.Player == null)
		{
			return 0;
		}
		GamePlayer player = client.Player;
		int place = packet.ReadInt();
		int bagType = packet.ReadInt();
		if (place < 0 || player.Extra == null)
		{
			return 0;
		}
		PlayerInventory bag = player.GetInventory((eBageType)bagType);
		if (bag == null)
		{
			return 0;
		}
		ItemInfo item = bag.GetItemAt(place);
		if (item == null || item.Template == null || !item.IsValidItem())
		{
			return 0;
		}
		ItemTemplateInfo template = item.Template;
		if (template.CategoryID != 11 || template.Property1 != 39 || template.Property2 < 1 || template.Property2 > 3)
		{
			return 0;
		}
		int index = template.Property2;
		int days = 30 * index;
		if (player.Extra.Info == null)
		{
			return 0;
		}
		if (!player.Extra.Info.KingBlessRenevalDays(days, index))
		{
			player.Out.SendMessage(eMessageType.Normal, "Chúc phúc hiện tại đã ở gói cao hơn, không thể dùng thẻ này.");
			return 0;
		}
		// Dựng lại danh sách buff theo gói mới + tạo buff hồi sinh (HELP_STRAW) nếu có.
		player.Extra.SetupKingBless(isFriend: false, index);
		player.Extra.CreateSaveLifeBuff();

		bool consumed;
		bag.BeginChanges();
		try
		{
			consumed = item.Count > 1 ? bag.RemoveCountFromStack(item, 1) : bag.RemoveItemAt(place);
		}
		finally
		{
			bag.CommitChanges();
		}
		if (!consumed)
		{
			player.Out.SendMessage(eMessageType.Normal, "Không thể sử dụng thẻ, vui lòng thử lại.");
			return 0;
		}
		player.Extra.SaveToDatabase();
		player.Out.SendKingBlessMain(player.Extra);
		player.Out.SendMessage(eMessageType.Normal, "Kích hoạt Chúc phúc thần gà thành công!");
		return 0;
	}
}
