using System;
using System.Reflection;
using Bussiness;
using Bussiness.Managers;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.GameUtils;
using Game.Server.Packets;
using log4net;
using SqlDataProvider.Data;

namespace Game.Server.MagicStone.Handle;

/// <summary>
/// 258/10 — Chuyển đổi Ma Thạch (CategoryID 61) thành Ma Ngọc (CategoryID 90).
/// Client: magicStone.views.MagicStoneTransform (ô Place 31 của MagicStoneBag/BagType 41),
/// request GameSocketOut.magicStoneTransform() = byte 10, không có data.
///  - Template.Property8 của Ma Thạch = template Ma Ngọc đích (100906 -> 110906, ...).
///  - Template.Property7 = số "\u0110\u00E1 Ma Ng\u1ECDc" (item 12268) cần tiêu thụ.
///  - Giữ StrengthenLevel/Exp, cập nhật 6 chỉ số theo Magicstone_Template của template đích,
///    random Hole1 = nhóm kỹ năng, Hole2..5 = 4 property type 1..14.
/// Toàn bộ logic bọc try/catch: không làm rơi processor, không để mất item.
/// </summary>
[MagicStoneHandleAttbute(10)]
public class MagicStoneTransform : IMagicStoneCommandHadler
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		try
		{
			return Handle(Player);
		}
		catch (Exception ex)
		{
			Log.Error("MagicStoneTransform error playerID " + Player.PlayerId, ex);
			Player.SendMessage("Chuy\u1EC3n \u0111\u1ED5i Ma Ng\u1ECDc th\u1EA5t b\u1EA1i (l\u1ED7i h\u1EC7 th\u1ED1ng). Vui l\u00F2ng th\u1EED l\u1EA1i.");
			return false;
		}
	}

	private static bool Handle(GamePlayer Player)
	{
		if (Player.PlayerCharacter.HasBagPassword && Player.PlayerCharacter.IsLocked)
		{
			Player.Out.SendMessage(eMessageType.Normal, LanguageMgr.GetTranslation("Bag.Locked"));
			return false;
		}
		MagicJadeHelper.EnsurePlanInfoPushed(Player);

		PlayerMagicStoneInventory bag = Player.MagicStoneBag;
		ItemInfo source = bag.GetItemAt(31);
		if (source == null || source.Template == null)
		{
			Player.SendMessage("H\u00E3y \u0111\u1EB7t Ma Th\u1EA1ch c\u1EA7n chuy\u1EC3n \u0111\u1ED5i v\u00E0o \u00F4 chuy\u1EC3n \u0111\u1ED5i.");
			return false;
		}
		if (source.Template.CategoryID != 61)
		{
			Player.SendMessage("Ch\u1EC9 c\u00F3 th\u1EC3 chuy\u1EC3n \u0111\u1ED5i Ma Th\u1EA1ch (Ma Ng\u1ECDc \u0111\u00E3 chuy\u1EC3n \u0111\u1ED5i r\u1ED3i).");
			return false;
		}
		int targetTemplateId = source.Template.Property8;
		if (targetTemplateId <= 0)
		{
			Player.SendMessage("Ma Th\u1EA1ch n\u00E0y kh\u00F4ng c\u00F3 c\u1EA5u h\u00ECnh chuy\u1EC3n \u0111\u1ED5i th\u00E0nh Ma Ng\u1ECDc.");
			return false;
		}
		ItemTemplateInfo targetTemplate = ItemMgr.FindItemTemplate(targetTemplateId);
		if (targetTemplate == null || targetTemplate.CategoryID != 90)
		{
			Player.SendMessage("C\u1EA5u h\u00ECnh Ma Ng\u1ECDc \u0111\u00EDch kh\u00F4ng h\u1EE3p l\u1EC7 (TemplateID " + targetTemplateId + ").");
			return false;
		}

		int needMaterial = source.Template.Property7;
		if (needMaterial <= 0)
		{
			needMaterial = 1;
		}
		int haveMaterial = MagicJadeHelper.GetMaterialCount(Player);
		if (haveMaterial < needMaterial)
		{
			Player.SendMessage("C\u1EA7n " + needMaterial + " \u0110\u00E1 Ma Ng\u1ECDc \u0111\u1EC3 chuy\u1EC3n \u0111\u1ED5i (\u0111ang c\u00F3 " + haveMaterial + ").");
			return false;
		}

		int level = source.StrengthenLevel;
		if (level < 1)
		{
			level = 1;
		}
		int exp = source.StrengthenExp;
		if (exp <= 0)
		{
			exp = MagicJadeHelper.GetMagicStoneExp(targetTemplateId, level);
		}

		// 1) Clone item sang template Ma Ngọc đích (giữ Place/BagType của ô 31), rồi ghi lại các trường riêng.
		ItemInfo result = ItemInfo.CloneFromTemplate(targetTemplate, source);
		if (result == null)
		{
			Player.SendMessage("Kh\u00F4ng t\u1EA1o \u0111\u01B0\u1EE3c Ma Ng\u1ECDc t\u1EEB d\u1EEF li\u1EC7u hi\u1EC7n t\u1EA1i.");
			return false;
		}
		result.ItemID = 0;
		result.TemplateID = targetTemplate.TemplateID;
		result.Count = 1;
		result.Place = source.Place;
		result.BagType = source.BagType;
		result.IsBinds = true;
		result.IsUsed = true;
		result.IsExist = true;
		result.ValidDate = 0;
		result.StrengthenLevel = level;
		result.StrengthenExp = exp;
		result.Hole1 = MagicJadeHelper.RollSkillGroup();
		result.Hole6 = 0;

		// 2) 4 property type id ngẫu nhiên vào Hole2..Hole5 (đúng quy ước magicJadeStr 4 phần tử).
		int[] weights = GetAttrWeights();
		result.Hole2 = MagicJadeHelper.RollPropertyType(weights);
		result.Hole3 = MagicJadeHelper.RollPropertyType(weights);
		result.Hole4 = MagicJadeHelper.RollPropertyType(weights);
		result.Hole5 = MagicJadeHelper.RollPropertyType(weights);

		// 3) 6 chỉ số theo Magicstone_Template của Ma Ngọc đích ở level hiện tại.
		MagicJadeHelper.ApplyMagicStoneStats(result, targetTemplateId, level);

		// 4) Trừ nguyên liệu "\u0110\u00E1 Ma Ng\u1ECDc".
		if (!MagicJadeHelper.ConsumeMaterial(Player, needMaterial))
		{
			Player.SendMessage("Kh\u00F4ng th\u1EC3 tr\u1EEB \u0110\u00E1 Ma Ng\u1ECDc (t\u00FAi v\u1EEBa thay \u0111\u1ED5i). Vui l\u00F2ng th\u1EED l\u1EA1i.");
			return false;
		}

		// 5) Thay item tại ô 31. Nếu thất bại: hoàn nguyên liệu + hoàn item cũ.
		bag.RemoveItem(source);
		if (!bag.AddItemTo(result, 31))
		{
			bool added = bag.AddItem(result);
			if (!added)
			{
				bag.AddItemTo(source, 31);
				MagicJadeHelper.RefundMaterial(Player, needMaterial);
				Player.SendMessage("T\u00FAi Ma Th\u1EA1ch \u0111\u00E3 \u0111\u1EA7y, kh\u00F4ng th\u1EC3 chuy\u1EC3n \u0111\u1ED5i. Nguy\u00EAn li\u1EC7u \u0111\u01B0\u1EE3c ho\u00E0n l\u1EA1i.");
				return false;
			}
		}
		bag.UpdateChangedPlaces();

		Player.SendMessage("Chuy\u1EC3n \u0111\u1ED5i th\u00E0nh c\u00F4ng: " + targetTemplate.Name + " c\u1EA5p " + level
			+ ", \u0111\u00E3 d\u00F9ng " + needMaterial + " \u0110\u00E1 Ma Ng\u1ECDc. K\u1EF9 n\u0103ng: nh\u00F3m " + result.Hole1
			+ ", thu\u1ED9c t\u00EDnh: " + result.Hole2 + "," + result.Hole3 + "," + result.Hole4 + "," + result.Hole5 + ".");
		return true;
	}

	private static int[] GetAttrWeights()
	{
		try
		{
			using (MagicStonePlanBussiness plan = new MagicStonePlanBussiness())
			{
				return plan.GetAttrRandoms();
			}
		}
		catch (Exception)
		{
			return null;
		}
	}
}
