using Bussiness;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.Packets;

namespace Game.Server.Pet.Handle;

/// <summary>
/// 68/19 — mua thức ăn pet (Nông trại 2.0 dùng chung gói với Pet).
/// Client gửi [bool useBind]; server trả 68/19 [int buyExpRemainNum].
/// Trước đây handler đọc player.Farm.CurrentFarm (hệ nông trại cũ) nên không khớp
/// với state nông trại mới; nay dùng PlayerManor.BuyFoodCount.
/// </summary>
[PetHandleAttbute(19)]
public class BuyPetExpItem : IPetCommandHadler
{
	public bool CommandHandler(GamePlayer player, GSPacketIn packet)
	{
		bool useBind = packet.ReadBoolean();
		if (player.PlayerCharacter.HasBagPassword && player.PlayerCharacter.IsLocked)
		{
			player.Out.SendMessage(eMessageType.Normal, LanguageMgr.GetTranslation("Bag.Locked"));
			return false;
		}
		if (!player.Manor.IsLoaded)
		{
			player.Manor.LoadFromDatabase();
		}
		player.Manor.BuyPetFood(useBind);
		return false;
	}
}
