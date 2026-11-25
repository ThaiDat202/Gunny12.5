using System;
using System.Collections.Generic;
using System.Linq;
using Bussiness;
using EntityDatabase.PlayerModels;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.Packets;
using SqlDataProvider.Data;

namespace Game.Server.Elf.Handle;

/// <summary>
/// Packet 707/4 UPGRADE_ELFEQUIP — nâng cấp (Luyện) Linh Bảo.
///
/// Client: ElfEquipInfoView.as -&gt; <c>sendUpgradeElfEquip(nextTemplateID, type, ItemTempId1, ItemTempId2)</c>
/// gửi 4 int: item TemplateID của dòng level kế tiếp (480xx), Type, ItemTempId1, ItemTempId2.
///
/// Server: xác định (type, quality) từ TemplateID, level hiện tại = max level của các Linh Bảo
/// cùng Type, kiểm tra/từ nguyên liệu theo dòng level kế tiếp trong TS_LinkPalTemplate.xml,
/// tăng Level cho mọi record cùng Type (client dùng level chung theo hệ), push 707/4 (type, level).
/// </summary>
public class ElfEquipUpgrade
{
	public int CommandHandler(GamePlayer player, GSPacketIn packet)
	{
		int nextTemplateId = packet.ReadInt();
		int type = packet.ReadInt();
		packet.ReadInt();
		packet.ReadInt();
		if (!ElfEquipConfigMgr.TryGetTypeQuality(nextTemplateId, out int configType, out int quality) || configType != type)
		{
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.EquipUpgrade.ConfigMissing"));
			return 0;
		}
		List<Sys_Users_ElfEquip> owned = ElfMgr.GetElfEquips(player).Where((Sys_Users_ElfEquip e) => e.Type == type).ToList();
		if (owned.Count == 0)
		{
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.EquipUpgrade.NotFound"));
			return 0;
		}
		int currentLevel = owned.Max((Sys_Users_ElfEquip e) => (e.Level > 0) ? e.Level : 1);
		ElfEquipLevelInfo next = ElfEquipConfigMgr.GetNextLevelInfo(type, quality, currentLevel);
		if (next == null)
		{
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.EquipUpgrade.MaxLevel"));
			return 0;
		}
		if (next.ItemTempId1 > 0 && next.Param1 > 0 && player.GetItemCount(next.ItemTempId1) < next.Param1)
		{
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.EquipUpgrade.NoItem"));
			return 0;
		}
		if (next.ItemTempId2 > 0 && next.Param2 > 0 && player.GetItemCount(next.ItemTempId2) < next.Param2)
		{
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.EquipUpgrade.NoItem"));
			return 0;
		}
		if (next.ItemTempId1 > 0 && next.Param1 > 0 && !player.RemoveTemplate(next.ItemTempId1, next.Param1))
		{
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.EquipUpgrade.NoItem"));
			return 0;
		}
		if (next.ItemTempId2 > 0 && next.Param2 > 0 && !player.RemoveTemplate(next.ItemTempId2, next.Param2))
		{
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.EquipUpgrade.NoItem"));
			return 0;
		}
		foreach (Sys_Users_ElfEquip equip in owned)
		{
			equip.Level = next.Level;
		}
		player.SaveElfDataToDatabase();
		ElfMgr.Out.SendUpgradeElfEquip(player, next.Type, next.Level);
		ElfMgr.Out.SendUpdateElfEquipLvInfo(player);
		player.EquipBag.UpdatePlayerProperties();
		player.SendMessage(eMessageType.Normal, LanguageMgr.GetTranslation("Elf.EquipUpgrade.Success", next.Level));
		return 0;
	}
}
