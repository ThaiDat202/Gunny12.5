using System;
using System.Collections.Generic;
using Bussiness;
using Bussiness.Managers;
using Game.Base.Packets;
using Game.Server.Farm;
using Game.Server.GameUtils;
using Game.Server.Managers;
using Game.Server.Team;
using SqlDataProvider.Data;

namespace Game.Server.Packets.Client;

[PacketHandler(345, "购买物品")]
public class NewBuyGoodsHandler : IPacketHandler
{
	public int HandlePacket(GameClient client, GSPacketIn packet)
	{
		SpecialItemBoxInfo specialValue = new SpecialItemBoxInfo();
		Dictionary<int, ItemInfo> dictionary = new Dictionary<int, ItemInfo>();
		string translateId = "UserBuyItemHandler.Success";
		bool flag = false;
		int num = packet.ReadInt();
		packet.ReadInt();
		int iD = packet.ReadInt();
		int num2 = packet.ReadInt();
		string text = packet.ReadString();
		packet.ReadBoolean();
		string text2 = packet.ReadString();
		int num3 = packet.ReadInt();
		bool isBinds = packet.ReadBoolean();
		packet.ReadInt();
		ConsortiaInfo consortiaInfo = ConsortiaMgr.FindConsortiaInfo(client.Player.PlayerCharacter.ConsortiaID);
		ShopItemInfo shopItemInfoById = ShopMgr.GetShopItemInfoById(iD);
		if (shopItemInfoById == null)
		{
			SendBuyGoodsPacket44(client, eMessageType.ERROR, "UserBuyItemHandler.CantBuy");
			return 1;
		}
		if (shopItemInfoById.ShopID == 139)
		{
			// Shop nông trại 139 (FARM_SHOP): client hiển thị/thanh toán bằng Cỏ Vàng
			// (tiền nông trại) chứ không phải Xu/Huy Chương như shop thường → xử lý riêng.
			return BuyFarmShopGoods(client, shopItemInfoById, num, isBinds);
		}
		switch (num3)
		{
		case -1:
		{
			ItemInfo itemInfo2 = ItemInfo.CreateFromTemplate(ItemMgr.FindItemTemplate(shopItemInfoById.TemplateID), 1, 104);
			if (client.Player.PlayerCharacter.jampsCurrency >= shopItemInfoById.AValue1 * num * itemInfo2.Count)
			{
				client.Player.AddJampsCurrency(-shopItemInfoById.AValue1 * num * itemInfo2.Count);
				itemInfo2.Count *= num;
				client.Player.AddTemplate(itemInfo2);
				GSPacketIn gSPacketIn2 = new GSPacketIn(44, client.Player.PlayerId);
				client.Out.SendMessage(eMessageType.Normal, LanguageMgr.GetTranslation("UserBuyItemHandler.Success"));
				gSPacketIn2.WriteInt(1);
				gSPacketIn2.WriteInt(3);
				client.Player.SendTCP(gSPacketIn2);
			}
			else if (client.Player.PlayerCharacter.TeamDayActive.selfAllActive >= shopItemInfoById.AValue1 * num * itemInfo2.Count)
			{
				TeamMgr.ReduceScore(client.Player, shopItemInfoById.AValue1 * num * itemInfo2.Count);
				itemInfo2.Count *= num;
				client.Player.AddTemplate(itemInfo2);
				GSPacketIn gSPacketIn3 = new GSPacketIn(44, client.Player.PlayerId);
				client.Out.SendMessage(eMessageType.Normal, LanguageMgr.GetTranslation("UserBuyItemHandler.Success"));
				gSPacketIn3.WriteInt(1);
				gSPacketIn3.WriteInt(3);
				client.Player.SendTCP(gSPacketIn3);
			}
			else
			{
				GSPacketIn gSPacketIn4 = new GSPacketIn(44, client.Player.PlayerId);
				client.Out.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Bạn không đủ điểm phiêu lưu."));
				gSPacketIn4.WriteInt(1);
				gSPacketIn4.WriteInt(3);
				client.Player.SendTCP(gSPacketIn4);
			}
			break;
		}
		case 0:
		{
			if (shopItemInfoById == null && !ShopMgr.IsOnShop(shopItemInfoById.ID) && !shopItemInfoById.CanBuy && !ShopMgr.CanBuy(shopItemInfoById.ShopID, consortiaInfo?.ShopLevel ?? 1, ref isBinds, client.Player.PlayerCharacter.ConsortiaID, client.Player.PlayerCharacter.Riches))
			{
				client.Out.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("UserBuyItemHandler.CantBuy"));
				return 1;
			}
			ItemInfo itemInfo = ItemInfo.CreateFromTemplate(ItemMgr.FindItemTemplate(shopItemInfoById.TemplateID), 1, 104);
			if (shopItemInfoById.BuyType == 0)
			{
				if (num2 == 1)
				{
					itemInfo.ValidDate = shopItemInfoById.AUnit;
				}
				if (num2 == 2)
				{
					itemInfo.ValidDate = shopItemInfoById.BUnit;
				}
				if (num2 == 3)
				{
					itemInfo.ValidDate = shopItemInfoById.CUnit;
				}
			}
			else
			{
				if (num2 == 1)
				{
					itemInfo.Count = shopItemInfoById.AUnit;
				}
				if (num2 == 2)
				{
					itemInfo.Count = shopItemInfoById.BUnit;
				}
				if (num2 == 3)
				{
					itemInfo.Count = shopItemInfoById.CUnit;
				}
			}
			if (num2 <= 3 && num2 >= 0)
			{
				itemInfo.Color = text ?? "";
				itemInfo.Skin = text2 ?? "";
				itemInfo.IsBinds = isBinds || Convert.ToBoolean(shopItemInfoById.IsBind);
				ShopMgr.SetItemType(shopItemInfoById, num2, ref specialValue);
				if (isBinds)
				{
					specialValue.DDTMoney = (int)specialValue.Money;
					specialValue.Money = 0L;
				}
				if (shopItemInfoById.ShopID == 2)
				{
					specialValue.Medal = specialValue.DDTMoney + (int)specialValue.Money;
					specialValue.Money = 0L;
					specialValue.DDTMoney = 0;
				}
				if ((specialValue.Money <= 0 || !isBinds) && (specialValue.Offer <= 0 || ShopCondition.isOffer(num2)) && (specialValue.DDTMoney <= 0 || (isBinds && (specialValue.DamageScore <= 0 || ShopCondition.isWorldBoss(num2)))) && (specialValue.PetScore <= 0 || ShopCondition.isPetScrore(num2)) && (specialValue.HardCurrency <= 0 || ShopCondition.isLabyrinth(num2)) && (specialValue.LeagueMoney <= 0 || ShopCondition.isLeague(num2)) && (specialValue.iCount <= 0 || ShopCondition.isSearchGoods(num2)) && (specialValue.SummerScore <= 0 || ShopCondition.isCatchInsect(num2)) && (specialValue.FishScore <= 0 || ShopCondition.isFishScoreShop(num2)) && (specialValue.ElfCoin <= 0 || ShopCondition.isElfShop(shopItemInfoById.ShopID)))
				{
					if (!dictionary.ContainsKey(itemInfo.TemplateID))
					{
						dictionary.Add(itemInfo.TemplateID, itemInfo);
					}
					else
					{
						dictionary[itemInfo.TemplateID].Count += itemInfo.Count;
					}
				}
				int num4 = specialValue.PayValue();
				if (dictionary.Count == 0)
				{
					client.Out.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("UserBuyItemHandler.NotSell"));
					return 1;
				}
				if (specialValue.iTemplateID > 0 && num4 == 0)
				{
					int itemCount = client.Player.GetItemCount(specialValue.iTemplateID);
					if (itemCount <= 0 || itemCount < specialValue.iCount)
					{
						client.Out.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("UserBuyItemHandler.FailByPermission"));
					}
					else
					{
						flag = client.Player.RemoveTemplate(specialValue.iTemplateID, specialValue.iCount);
					}
				}
				else if (num4 > 0)
				{
					if (specialValue.Money * num > client.Player.PlayerCharacter.Money)
					{
						translateId = "UserBuyItemHandler.NoMoney";
					}
					if (specialValue.Gold * num > client.Player.PlayerCharacter.Gold)
					{
						translateId = "UserBuyItemHandler.NoGold";
					}
					if (specialValue.Offer * num > client.Player.PlayerCharacter.Offer)
					{
						translateId = "UserBuyItemHandler.NoOffer";
					}
					if (specialValue.DDTMoney * num > client.Player.PlayerCharacter.DDTMoney)
					{
						translateId = "UserBuyItemHandler.GiftToken";
					}
					if (specialValue.Medal * num > client.Player.PlayerCharacter.medal)
					{
						translateId = "UserBuyItemHandler.Medal";
					}
					if (specialValue.DamageScore * num > client.Player.PlayerCharacter.damageScores)
					{
						translateId = "UserBuyItemHandler.FailByPermission";
					}
					if (specialValue.PetScore * num > client.Player.PlayerCharacter.petScore)
					{
						translateId = "UserBuyItemHandler.FailByPermission";
					}
					if (specialValue.HardCurrency * num > client.Player.PlayerCharacter.hardCurrency)
					{
						translateId = "UserBuyItemHandler.FailByPermission";
					}
					if (specialValue.LeagueMoney * num > client.Player.PlayerCharacter.LeagueMoney)
					{
						translateId = "UserBuyItemHandler.FailByPermission";
					}
					if (specialValue.SummerScore * num > client.Player.Extra.Info.SummerScore)
					{
						translateId = "UserBuyItemHandler.SummerScore";
					}
					if (specialValue.ElfCoin * num > client.Player.Extra.Info.Coin)
					{
						translateId = "UserBuyItemHandler.NoElfCoin";
					}
					else
					{
						int num5 = specialValue.FishScore * num;
						int? num6 = client.Player.Home?.Data?.fishScore;
						int valueOrDefault = num6.GetValueOrDefault();
						if ((num5 > valueOrDefault) & num6.HasValue)
						{
							translateId = "UserBuyItemHandler.FailByPermission";
						}
						else if (specialValue.UseableScore == 0)
						{
							client.Player.RemoveMoney((int)specialValue.Money * num);
							client.Player.RemoveGold(specialValue.Gold * num);
							client.Player.RemoveOffer(specialValue.Offer * num);
							client.Player.RemoveGiftToken(specialValue.DDTMoney * num);
							client.Player.RemoveMedal(specialValue.Medal * num);
							client.Player.RemoveDamageScores(specialValue.DamageScore * num);
							client.Player.RemovePetScore(specialValue.PetScore * num);
							client.Player.RemoveHardCurrency(specialValue.HardCurrency * num);
							client.Player.RemoveLeagueMoney(specialValue.LeagueMoney * num);
							client.Player.RemoveSummerScore(specialValue.SummerScore * num);
							client.Player.RemoveFishScore(specialValue.FishScore * num);
							client.Player.RemoveCoin(specialValue.ElfCoin * num);
							flag = true;
						}
					}
				}
				if (flag)
				{
					foreach (ItemInfo value in dictionary.Values)
					{
						value.Count *= num;
						client.Player.AddTemplate(value);
					}
				}
				else
				{
					translateId = "UserBuyItemHandler.Fail";
				}
				GSPacketIn gSPacketIn = new GSPacketIn(44, client.Player.PlayerId);
				client.Out.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation(translateId));
				gSPacketIn.WriteInt(1);
				gSPacketIn.WriteInt(3);
				client.Player.SendTCP(gSPacketIn);
			}
			return 0;
		}
		}
		return 0;
	}

	/// <summary>
	/// Mua vật phẩm shop nông trại 139 (FARM_SHOP — tab "Cửa hàng" trong bảng Quản lý):
	/// giá = AValue1 (quy ước là Cỏ Vàng / tiền nông trại) * số lượng.
	/// Item CategoryID 87 (nông sản/vật liệu nâng cấp khu) cộng vào kho nông trại,
	/// item khác (Quả Hồn, thức ăn pet...) cộng vào túi thường như shop thường.
	/// </summary>
	private int BuyFarmShopGoods(GameClient client, ShopItemInfo shopItemInfo, int num, bool isBinds)
	{
		int count = ((num > 0) ? num : 1);
		long cost = (long)shopItemInfo.AValue1 * count;
		ItemTemplateInfo template = ItemMgr.FindItemTemplate(shopItemInfo.TemplateID);
		PlayerManor manor = client.Player.Manor;
		if (template == null || cost <= 0 || cost > int.MaxValue || manor == null)
		{
			SendBuyGoodsPacket44(client, eMessageType.ERROR, "UserBuyItemHandler.Fail");
			return 0;
		}
		if (!manor.IsLoaded)
		{
			manor.LoadFromDatabase();
		}
		if (!manor.IsLoaded)
		{
			SendBuyGoodsPacket44(client, eMessageType.ERROR, "UserBuyItemHandler.Fail");
			return 0;
		}
		int personalLimit = shopItemInfo.LimitPersonalCount;
		int alreadyBought = manor.GetShop139Bought(shopItemInfo.ID);
		if (personalLimit > 0 && alreadyBought + count > personalLimit)
		{
			client.Out.SendMessage(eMessageType.Normal, "Vật phẩm này giới hạn " + personalLimit + " cái/tháng, bạn đã mua " + alreadyBought + ".");
			SendBuyGoodsPacket44(client, eMessageType.ERROR, "UserBuyItemHandler.CantBuy");
			return 0;
		}
		if (!manor.TrySpendMoney((int)cost))
		{
			SendBuyGoodsPacket44(client, eMessageType.ERROR, "UserBuyItemHandler.NoMoney");
			return 0;
		}
		bool added;
		if (template.CategoryID == 87)
		{
			ManorItemTemplate farmItem = ManorConfigMgr.GetItem(template.TemplateID);
			int property = ((farmItem != null) ? farmItem.Property1 : 1);
			added = manor.AddGoods(template.TemplateID, property, count);
		}
		else
		{
			ItemInfo itemInfo = ItemInfo.CreateFromTemplate(template, count, 104);
			added = false;
			if (itemInfo != null)
			{
				itemInfo.IsBinds = isBinds;
				added = client.Player.AddTemplate(itemInfo);
			}
		}
		if (!added)
		{
			// Cộng item thất bại → trả lại tiền nông trại đã trừ để tránh mất tiền.
			manor.AddMoney((int)cost);
			SendBuyGoodsPacket44(client, eMessageType.ERROR, "UserBuyItemHandler.Fail");
			return 0;
		}
		manor.AddShop139Purchase(shopItemInfo.ID, count);
		manor.AddLog("Mua " + count + " " + template.Name + " ở shop nông trại (" + cost + " Cỏ Vàng).");
		manor.SendUpdateGoods();
		SendBuyGoodsPacket44(client, eMessageType.Normal, "UserBuyItemHandler.Success");
		return 0;
	}

	/// <summary>Gửi message + packet 44 (1,3) giống cuối handler gốc (client đóng dialog mua).</summary>
	private static void SendBuyGoodsPacket44(GameClient client, eMessageType type, string translateId)
	{
		GSPacketIn gSPacketIn = new GSPacketIn(44, client.Player.PlayerId);
		client.Out.SendMessage(type, LanguageMgr.GetTranslation(translateId));
		gSPacketIn.WriteInt(1);
		gSPacketIn.WriteInt(3);
		client.Player.SendTCP(gSPacketIn);
	}
}
