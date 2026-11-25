using System;
using Bussiness;
using Game.Base.Packets;
using Game.Logic;
using Game.Server.GameObjects;
using Game.Server.GameUtils;
using SqlDataProvider.Data;

namespace Game.Server.Pet.Handle;

[PetHandleAttbute(20)]
public class AddPetEquip : IPetCommandHadler
{
	public bool CommandHandler(GamePlayer player, GSPacketIn packet)
	{
		int bageType = packet.ReadInt();
		int slot = packet.ReadInt();
		int place = packet.ReadInt();
		PlayerInventory inventory = player.GetInventory((eBageType)bageType);
		if (inventory == null)
		{
			player.SendMessage("Túi chứa trang bị không hợp lệ.");
			return false;
		}
		ItemInfo itemAt = inventory.GetItemAt(slot);
		UsersPetInfo targetPet = player.PetBag.GetPetAt(place);
		if (itemAt == null || !itemAt.IsEquipPet() || !itemAt.IsValidItem())
		{
			player.SendMessage(LanguageMgr.GetTranslation("AddPetEquip.WrongItem"));
			return false;
		}
		if (targetPet == null)
		{
			player.SendMessage("Không tìm thấy thú cưng.");
			return false;
		}

		// Chỉ cho phép trang bị trang bị đúng loại với thú cưng:
		//  - Trang bị ĐÃ thức tỉnh: record Pet_Equip_Awake_Data lưu pet KindID mà trang bị thuộc về.
		//  - Trang bị CHƯA thức tỉnh: suy ra KindID từ tên/bộ trang bị qua PetEquipKindMap.xml
		//    (vd "Vũ Khí Rùa Thức Tỉnh" -> Huyền Vũ kind 18), nhờ vậy chặn được cả đồ Q6-Q9
		//    chưa có record thay vì thả tự do như trước.
		ItemAwakenPet awakenRecord = player.PetBag.GetAwakenRecord(itemAt.ItemID);
		PetTemplateInfo petTemplate = PetMgr.FindPetTemplate(targetPet.TemplateID);
		int requiredKindId = 0;
		if (awakenRecord != null && awakenRecord.petId > 0)
		{
			requiredKindId = awakenRecord.petId;
		}
		else
		{
			requiredKindId = PetExtConfigMgr.GetPetEquipKindId(itemAt.TemplateID);
		}
		if (petTemplate != null && requiredKindId > 0 && petTemplate.KindID != requiredKindId)
		{
			player.SendMessage(awakenRecord != null
				? "Trang bị thức tỉnh này chỉ có thể trang bị cho thú cưng cùng loại."
				: "Trang bị này chỉ có thể trang bị cho thú cưng cùng loại.");
			return false;
		}

		if (player.PetBag.CheckEqPetLevel(place, itemAt))
		{
			player.SendMessage(LanguageMgr.GetTranslation("AddPetEquip.WrongLevel"));
			return false;
		}
		if (!itemAt.IsUsed)
		{
			itemAt.IsUsed = true;
			itemAt.BeginDate = DateTime.Now;
		}
		if (player.PetBag.AddEqPet(place, itemAt))
		{
			inventory.TakeOutItem(itemAt);
			// Rebuild kỹ năng cho ĐÚNG pet nhận trang bị (code cũ dùng player.Pet - pet đang
			// xuất trận - nên trang bị cho pet khác không được cộng skill thức tỉnh).
			player.PetBag.RebuildPetSkill(targetPet);
			player.PetBag.OnChangedPetEquip(place);
			player.PetBag.UpdatePet(targetPet);
			player.PetBag.SaveToDatabase(saveAdopt: false);
			player.SendMessage(LanguageMgr.GetTranslation("AddPetEquip.Success"));
			if (awakenRecord != null)
			{
				player.SendMessage("Kỹ năng thức tỉnh đã được kích hoạt.");
			}
		}
		else
		{
			player.SendMessage(LanguageMgr.GetTranslation("AddPetEquip.Fail"));
		}
		return false;
	}
}
