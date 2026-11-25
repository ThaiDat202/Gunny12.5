using System;
using System.Collections.Generic;
using System.Reflection;
using Bussiness;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.GameUtils;
using Game.Server.Packets;
using Game.Server.Statics;
using log4net;
using SqlDataProvider.Data;

namespace Game.Server.MagicStone.Handle;

/// <summary>
/// 258/18 — Reset Pro (Xilian) cho Ma Ngọc (CategoryID 90).
/// Client: magicStone.views.MagicStoneTransform.__xilianHandler ->
/// GameSocketOut.magicjadeResetPro(lockArr, lockSkill):
///   [byte 18][int lockCount][lockCount x int vị trí 1..4 trong magicJadeStr][boolean lockSkill]
/// Server random lại Hole2..Hole5 (property type 1..14) cho các vị trí KHÔNG bị khóa,
/// random Hole1 (nhóm kỹ năng 1..N) nếu không khóa kỹ năng.
/// Tiêu thụ Template.Property7 "\u0110\u00E1 Ma Ng\u1ECDc" (12268) + tiền theo số lock
/// (Server_Config.MagicJadeLockPoints = 1000,2000,5000,10000 cho 1..4 lock).
/// </summary>
[MagicStoneHandleAttbute(18)]
public class MagicJadeResetPro : IMagicStoneCommandHadler
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		try
		{
			return Handle(Player, packet);
		}
		catch (Exception ex)
		{
			Log.Error("MagicJadeResetPro error playerID " + Player.PlayerId, ex);
			Player.SendMessage("Reset Pro th\u1EA5t b\u1EA1i (l\u1ED7i h\u1EC7 th\u1ED1ng). Vui l\u00F2ng th\u1EED l\u1EA1i.");
			return false;
		}
	}

	private static bool Handle(GamePlayer Player, GSPacketIn packet)
	{
		if (Player.PlayerCharacter.HasBagPassword && Player.PlayerCharacter.IsLocked)
		{
			Player.Out.SendMessage(eMessageType.Normal, LanguageMgr.GetTranslation("Bag.Locked"));
			return false;
		}
		MagicJadeHelper.EnsurePlanInfoPushed(Player);

		// 1) Đọc request: số lock, danh sách vị trí (1-based), cờ khóa kỹ năng.
		int lockCount = packet.ReadInt();
		if (lockCount < 0 || lockCount > 4)
		{
			Player.SendMessage("S\u1ED1 v\u1ECB tr\u00ED kh\u00F3a kh\u00F4ng h\u1EE3p l\u1EC7.");
			return false;
		}
		HashSet<int> lockedSlots = new HashSet<int>();
		for (int i = 0; i < lockCount; i++)
		{
			int slot = packet.ReadInt();
			if (slot < 1 || slot > 4 || !lockedSlots.Add(slot))
			{
				Player.SendMessage("Danh s\u00E1ch v\u1ECB tr\u00ED kh\u00F3a kh\u00F4ng h\u1EE3p l\u1EC7.");
				return false;
			}
		}
		bool lockSkill = packet.ReadBoolean();

		int totalLocks = lockedSlots.Count + (lockSkill ? 1 : 0);
		if (totalLocks > 4)
		{
			Player.SendMessage("Ch\u1EC9 \u0111\u01B0\u1EE3c kh\u00F3a t\u1ED1i \u0111a 4 m\u1EE5c (k\u1EC3 c\u1EA3 k\u1EF9 n\u0103ng).");
			return false;
		}

		PlayerMagicStoneInventory bag = Player.MagicStoneBag;
		ItemInfo item = bag.GetItemAt(31);
		if (item == null || item.Template == null)
		{
			Player.SendMessage("H\u00E3y \u0111\u1EB7t Ma Ng\u1ECDc c\u1EA7n reset v\u00E0o \u00F4 chuy\u1EC3n \u0111\u1ED5i.");
			return false;
		}
		if (item.Template.CategoryID != 90)
		{
			Player.SendMessage("\u00D4 chuy\u1EC3n \u0111\u1ED5i ph\u1EA3i l\u00E0 Ma Ng\u1ECDc (Ma Th\u1EA1ch h\u00E3y d\u00F9ng Chuy\u1EC3n \u0111\u1ED5i tr\u01B0\u1EDBc).");
			return false;
		}

		// 2) Tiêu thụ nguyên liệu "\u0110\u00E1 Ma Ng\u1ECDc" (đúng số lượng client hiển thị = Template.Property7).
		int needMaterial = item.Template.Property7;
		if (needMaterial < 0)
		{
			needMaterial = 0;
		}
		int haveMaterial = MagicJadeHelper.GetMaterialCount(Player);
		if (needMaterial > 0 && haveMaterial < needMaterial)
		{
			Player.SendMessage("C\u1EA7n " + needMaterial + " \u0110\u00E1 Ma Ng\u1ECDc \u0111\u1EC3 reset (\u0111ang c\u00F3 " + haveMaterial + ").");
			return false;
		}

		// 3) Tính tiền theo số lock (client hiển thị từ cùng key cấu hình MagicJadeLockPoints).
		int needMoney = 0;
		if (totalLocks > 0)
		{
			int[] lockPoints;
			using (MagicStonePlanBussiness plan = new MagicStonePlanBussiness())
			{
				lockPoints = plan.GetLockPoints();
			}
			if (lockPoints != null && lockPoints.Length > 0)
			{
				int index = Math.Min(totalLocks - 1, lockPoints.Length - 1);
				needMoney = Math.Max(0, lockPoints[index]);
			}
		}

		if (needMaterial > 0 && !MagicJadeHelper.ConsumeMaterial(Player, needMaterial))
		{
			Player.SendMessage("Kh\u00F4ng th\u1EC3 tr\u1EEB \u0110\u00E1 Ma Ng\u1ECDc (t\u00FAi v\u1EEBa thay \u0111\u1ED5i). Vui l\u00F2ng th\u1EED l\u1EA1i.");
			return false;
		}
		if (needMoney > 0 && !Player.MoneyDirect(MoneyType.Money, needMoney))
		{
			MagicJadeHelper.RefundMaterial(Player, needMaterial);
			Player.SendMessage("Kh\u00F4ng \u0111\u1EE7 ti\u1EC1n \u0111\u1EC3 reset (" + needMoney + "). Nguy\u00EAn li\u1EC7u \u0111\u01B0\u1EE3c ho\u00E0n l\u1EA1i.");
			return false;
		}

		// 4) Random lại các ô không khóa (slot 1 -> Hole2, 2 -> Hole3, 3 -> Hole4, 4 -> Hole5).
		int[] weights = null;
		using (MagicStonePlanBussiness plan = new MagicStonePlanBussiness())
		{
			weights = plan.GetAttrRandoms();
		}
		int hole2 = item.Hole2;
		int hole3 = item.Hole3;
		int hole4 = item.Hole4;
		int hole5 = item.Hole5;
		if (!lockedSlots.Contains(1))
		{
			hole2 = MagicJadeHelper.RollPropertyType(weights);
		}
		if (!lockedSlots.Contains(2))
		{
			hole3 = MagicJadeHelper.RollPropertyType(weights);
		}
		if (!lockedSlots.Contains(3))
		{
			hole4 = MagicJadeHelper.RollPropertyType(weights);
		}
		if (!lockedSlots.Contains(4))
		{
			hole5 = MagicJadeHelper.RollPropertyType(weights);
		}
		item.Hole2 = hole2;
		item.Hole3 = hole3;
		item.Hole4 = hole4;
		item.Hole5 = hole5;
		if (!lockSkill)
		{
			item.Hole1 = MagicJadeHelper.RollSkillGroup();
		}
		bag.UpdateItem(item);

		string lockText = (totalLocks > 0) ? ("Kh\u00F3a " + totalLocks + " m\u1EE5c, ph\u00ED " + needMoney + " ti\u1EC1n") : "Kh\u00F4ng kh\u00F3a";
		Player.SendMessage("Reset Pro th\u00E0nh c\u00F4ng (" + lockText + "): thu\u1ED9c t\u00EDnh " + hole2 + "," + hole3 + "," + hole4 + "," + hole5
			+ ", k\u1EF9 n\u0103ng nh\u00F3m " + item.Hole1 + ". \u0110\u00E3 d\u00F9ng " + needMaterial + " \u0110\u00E1 Ma Ng\u1ECDc.");
		return true;
	}
}
