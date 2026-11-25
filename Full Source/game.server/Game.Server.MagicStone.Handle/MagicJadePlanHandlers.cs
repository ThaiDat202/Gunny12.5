using System;
using System.Collections.Generic;
using System.Reflection;
using Bussiness;
using MagicStonePlanSnapshot = Bussiness.MagicStonePlanBussiness.MagicStonePlanSnapshot;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.Packets;
using log4net;

namespace Game.Server.MagicStone.Handle;

/// <summary>
/// 258/19 â€” Mua thÃªm bá»™ Ma Ngá»c (client: MagicJadeView "Th\u00EAm" -> sendMagicStoneAddPlan).
/// KhÃ´ng data. Giá»›i háº¡n Server_Config.MagicJadePlanMaxCount (máº·c Ä‘á»‹nh 6, client hiá»‡n tá»‘i Ä‘a 4 bá»™),
/// phÃ­ Server_Config.PlanBuyPoint (máº·c Ä‘á»‹nh 1000). Sau khi thÃªm, push láº¡i PLAN_INFO (258/22).
/// </summary>
[MagicStoneHandleAttbute(19)]
public class MagicJadeBuyPlan : IMagicStoneCommandHadler
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		try
		{
			if (Player.PlayerCharacter.HasBagPassword && Player.PlayerCharacter.IsLocked)
			{
				Player.Out.SendMessage(eMessageType.Normal, LanguageMgr.GetTranslation("Bag.Locked"));
				return false;
			}
			using MagicStonePlanBussiness plan = new MagicStonePlanBussiness();
			int userId = Player.PlayerCharacter.ID;
			List<MagicStonePlanData> plans = plan.GetPlans(userId, true);
			int maxCount = plan.GetPlanMaxCount();
			if (plans.Count >= maxCount)
			{
				Player.SendMessage("\u0110\u00E3 \u0111\u1EA1t s\u1ED1 b\u1ED9 Ma Ng\u1ECDc t\u1ED1i \u0111a (" + maxCount + " b\u1ED9).");
				return false;
			}
			int price = plan.GetPlanBuyPoint();
			if (price > 0 && !Player.MoneyDirect(price))
			{
				Player.SendMessage("Kh\u00F4ng \u0111\u1EE7 ti\u1EC1n \u0111\u1EC3 mua b\u1ED9 Ma Ng\u1ECDc m\u1EDBi (" + price + ").");
				return false;
			}
			string name = "B\u1ED9 " + (plans.Count + 1);
			// Bo moi KHONG active va KHONG copy layout hien tai: day la 1 bo doc lap,
			// nguoi choi chon bo de thao 9 o ngoc cu va tu gan cac Ma Ngoc khac vao.
			if (!plan.InsertPlan(userId, name, false))
			{
				MagicJadeHelper.RefundMoney(Player, price);
				Player.SendMessage("Mua b\u1ED9 Ma Ng\u1ECDc th\u1EA5t b\u1EA1i, ti\u1EC1n \u0111\u00E3 \u0111\u01B0\u1EE3c ho\u00E0n l\u1EA1i.");
				return false;
			}
			MagicJadeHelper.SendPlanInfo(Player, true);
			Player.SendMessage("\u0110\u00E3 mua b\u1ED9 Ma Ng\u1ECDc tr\u1ED1ng \"" + name + "\" (ph\u00ED " + price + "). Ch\u1ECDn b\u1ED9 trong combo \u0111\u1EC3 g\u1EAFn 9 Ma Ng\u1ECDc ri\u00EAng cho b\u1ED9 n\u00E0y.");
			return true;
		}
		catch (Exception ex)
		{
			Log.Error("MagicJadeBuyPlan error playerID " + Player.PlayerId, ex);
			Player.SendMessage("Mua b\u1ED9 Ma Ng\u1ECDc th\u1EA5t b\u1EA1i (l\u1ED7i h\u1EC7 th\u1ED1ng). Vui l\u00F2ng th\u1EED l\u1EA1i.");
			return false;
		}
	}
}

/// <summary>
/// 258/20 â€” Chá»n bá»™ Ma Ngá»c active (client: MagicJadeView combo -> sendChangeMigicStonePlan(planId)).
/// Data: int planID. Server set active trong Sys_Users_MagicStonePlan rá»“i push láº¡i PLAN_INFO.
/// Ghi chÃº: server 12.6 chÆ°a cÃ³ há»‡ thá»‘ng lÆ°u riÃªng ná»™i dung tá»«ng bá»™ ngá»c (bag swap) â€” xem bÃ¡o cÃ¡o.
/// </summary>
[MagicStoneHandleAttbute(20)]
public class MagicJadeChangePlan : IMagicStoneCommandHadler
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		try
		{
			int planId = packet.ReadInt();
			if (planId <= 0)
			{
				Player.SendMessage("B\u1ED9 Ma Ng\u1ECDc kh\u00F4ng h\u1EE3p l\u1EC7.");
				return false;
			}
			using MagicStonePlanBussiness plan = new MagicStonePlanBussiness();
			int userId = Player.PlayerCharacter.ID;
			List<MagicStonePlanData> plans = plan.GetPlans(userId, true);
			MagicStonePlanData target = plans.Find(p => p.PlanID == planId);
			if (target == null)
			{
				Player.SendMessage("Kh\u00F4ng t\u00ECm th\u1EA5y b\u1ED9 Ma Ng\u1ECDc c\u1EA7n ch\u1ECDn.");
				return false;
			}
			MagicStonePlanData oldActive = plans.Find(p => p.IsActive);
			// Chon lai dung bo dang active: chi dam bao bo co snapshot (neu chua co thi nhan 9 o hien tai).
			if (oldActive != null && oldActive.PlanID == planId)
			{
				if (plan.GetPlanSnapshots(planId).Count == 0)
				{
					List<MagicStonePlanSnapshot> snap0 = MagicJadeHelper.SnapshotPlanJades(Player);
					if (snap0.Count > 0)
					{
						plan.SavePlanSnapshots(userId, planId, snap0);
					}
				}
				MagicJadeHelper.SendPlanInfo(Player, true);
				Player.SendMessage("\u0110ang d\u00F9ng b\u1ED9 Ma Ng\u1ECDc \"" + target.PlanName + "\".");
				return true;
			}
			// 1) Chot snapshot 9 o ngoc hien tai vao bo cu (giu ngoc gan chat voi bo).
			List<MagicStonePlanSnapshot> currentSnap = MagicJadeHelper.SnapshotPlanJades(Player);
			if (oldActive != null)
			{
				plan.SavePlanSnapshots(userId, oldActive.PlanID, currentSnap);
			}
			// 2) Doi active -> thao 9 vien cua bo cu khoi tui -> tao lai 9 vien cua bo dich tu snapshot.
			if (!plan.SetActivePlan(userId, planId))
			{
				Player.SendMessage("\u0110\u1ED5i b\u1ED9 Ma Ng\u1ECDc th\u1EA5t b\u1EA1i. Vui l\u00F2ng th\u1EED l\u1EA1i.");
				return false;
			}
			List<MagicStonePlanSnapshot> targetSnap = plan.GetPlanSnapshots(planId);
			MagicJadeHelper.RemoveEquippedJades(Player);
			int created = MagicJadeHelper.MaterializePlanJades(Player, targetSnap);
			MagicJadeHelper.SendPlanInfo(Player, true);
			Player.SendMessage("\u0110\u00E3 ch\u1ECDn b\u1ED9 Ma Ng\u1ECDc \"" + target.PlanName + "\" (" + created + " \u00F4 ng\u1ECDc).");
			return true;
		}
		catch (Exception ex)
		{
			Log.Error("MagicJadeChangePlan error playerID " + Player.PlayerId, ex);
			Player.SendMessage("\u0110\u1ED5i b\u1ED9 Ma Ng\u1ECDc th\u1EA5t b\u1EA1i (l\u1ED7i h\u1EC7 th\u1ED1ng). Vui l\u00F2ng th\u1EED l\u1EA1i.");
			return false;
		}
	}
}

/// <summary>
/// 258/21 â€” Äá»•i tÃªn bá»™ Ma Ngá»c (client: MagicJadeResetName -> sendResetMagicPlanName(planId, name)).
/// Data: int planID + UTF name (tá»‘i Ä‘a 50 kÃ½ tá»± theo cá»™t nvarchar(50)).
/// </summary>
[MagicStoneHandleAttbute(21)]
public class MagicJadeRenamePlan : IMagicStoneCommandHadler
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		try
		{
			int planId = packet.ReadInt();
			string name = packet.ReadString();
			if (planId <= 0)
			{
				Player.SendMessage("B\u1ED9 Ma Ng\u1ECDc kh\u00F4ng h\u1EE3p l\u1EC7.");
				return false;
			}
			name = (name ?? "").Trim();
			if (name.Length == 0)
			{
				Player.SendMessage("T\u00EAn b\u1ED9 Ma Ng\u1ECDc kh\u00F4ng \u0111\u01B0\u1EE3c \u0111\u1EC3 tr\u1ED1ng.");
				return false;
			}
			if (name.Length > 50)
			{
				name = name.Substring(0, 50);
			}
			using MagicStonePlanBussiness plan = new MagicStonePlanBussiness();
			int userId = Player.PlayerCharacter.ID;
			List<MagicStonePlanData> plans = plan.GetPlans(userId, true);
			MagicStonePlanData target = plans.Find(p => p.PlanID == planId);
			if (target == null)
			{
				Player.SendMessage("Kh\u00F4ng t\u00ECm th\u1EA5y b\u1ED9 Ma Ng\u1ECDc c\u1EA7n \u0111\u1ED5i t\u00EAn.");
				return false;
			}
			if (!plan.RenamePlan(userId, planId, name))
			{
				Player.SendMessage("\u0110\u1ED5i t\u00EAn b\u1ED9 Ma Ng\u1ECDc th\u1EA5t b\u1EA1i. Vui l\u00F2ng th\u1EED l\u1EA1i.");
				return false;
			}
			MagicJadeHelper.SendPlanInfo(Player, true);
			Player.SendMessage("\u0110\u00E3 \u0111\u1ED5i t\u00EAn b\u1ED9 Ma Ng\u1ECDc th\u00E0nh \"" + name + "\".");
			return true;
		}
		catch (Exception ex)
		{
			Log.Error("MagicJadeRenamePlan error playerID " + Player.PlayerId, ex);
			Player.SendMessage("\u0110\u1ED5i t\u00EAn b\u1ED9 Ma Ng\u1ECDc th\u1EA5t b\u1EA1i (l\u1ED7i h\u1EC7 th\u1ED1ng). Vui l\u00F2ng th\u1EED l\u1EA1i.");
			return false;
		}
	}
}
