using System.Collections.Generic;
using System.Linq;
using Bussiness;
using EntityDatabase.PlayerModels;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.Packets;

namespace Game.Server.Elf.Handle;

/// <summary>
/// Packet 664/18 ELF_EQUIP_OPERATE — mặc/tháo Linh Bảo cho một Thần Hộ Mệnh.
///
/// Client: <c>sendDressELfEquip(elfId, equipId)</c> — gọi từ ElfEquipView, ElfPropertyView,
/// ElfSkillView khi double-click Linh Bảo; equipId = 0 nghĩa là tháo.
///
/// Server cập nhật Sys_Users_Elf.ElfEquipId / EquipTempId + Sys_Users_ElfEquip.IsEquip,
/// lưu DB ngay rồi push 664/1, 707/1, 707/2 để client vẽ lại.
/// </summary>
[ElfHandle(18)]
public class ElfEquipOperate : IElfCommandHandler
{
	public int CommandHandler(GamePlayer player, GSPacketIn packet)
	{
		int elfId = packet.ReadInt();
		int equipId = packet.ReadInt();
		Sys_Users_Elf elf = ElfMgr.GetElf(player, elfId);
		if (elf == null)
		{
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.EquipOperate.ElfNotFound"));
			return 0;
		}
		List<Sys_Users_ElfEquip> equips = ElfMgr.GetElfEquips(player);
		// Client khong co packet thao rieng: double-click o Linh Bao dang mac se gui lai
		// 664/18 voi cung equipId -> coi nhu TOGGLE (thao ra).
		if (equipId > 0 && elf.ElfEquipId == equipId)
		{
			equipId = 0;
		}
		if (equipId <= 0)
		{
			Sys_Users_ElfEquip old = equips.FirstOrDefault((Sys_Users_ElfEquip e) => e.ID == elf.ElfEquipId);
			if (old != null)
			{
				old.IsEquip = false;
			}
			elf.ElfEquipId = 0;
			elf.EquipTempId = 0;
		}
		else
		{
			Sys_Users_ElfEquip record = equips.FirstOrDefault((Sys_Users_ElfEquip e) => e.ID == equipId);
			if (record == null)
			{
				player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.EquipOperate.EquipNotFound"));
				return 0;
			}
			foreach (Sys_Users_Elf other in player.PlayerCharacter.ElfInfo)
			{
				if (other.ElfID != elfId && other.ElfEquipId == record.ID)
				{
					other.ElfEquipId = 0;
					other.EquipTempId = 0;
				}
			}
			Sys_Users_ElfEquip old = equips.FirstOrDefault((Sys_Users_ElfEquip e) => e.ID == elf.ElfEquipId);
			if (old != null && old.ID != record.ID)
			{
				old.IsEquip = false;
			}
			record.IsEquip = true;
			elf.ElfEquipId = record.ID;
			elf.EquipTempId = record.TemplateID;
		}
		player.SaveElfDataToDatabase();
		ElfMgr.Out.SendUpdateElf(player);
		ElfMgr.Out.SendUpdateElfEquipInfo(player);
		ElfMgr.Out.SendUpdateElfEquipLvInfo(player);
		player.EquipBag.UpdatePlayerProperties();
		return 0;
	}
}
