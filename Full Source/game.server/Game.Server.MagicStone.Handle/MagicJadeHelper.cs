using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Bussiness;
using MagicStonePlanSnapshot = Bussiness.MagicStonePlanBussiness.MagicStonePlanSnapshot;
using Bussiness.Managers;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.GameUtils;
using Game.Server.Statics;
using log4net;
using SqlDataProvider.Data;

namespace Game.Server.MagicStone.Handle;

/// <summary>
/// Helper dÃ¹ng chung cho cÃ¡c handler Ma Ngá»c má»›i:
///  - Gá»­i packet 258/22 (PLAN_INFO) cho client: int count; má»—i bá»™: int id, UTF tÃªn, int state (1 = active).
///    Client magicStone.MagicStoneManager::__planInfo Ä‘á»c Ä‘Ãºng Ä‘á»‹nh dáº¡ng nÃ y.
///  - Random property type 1..14 (cÃ³ trá»ng sá»‘ theo Server_Config.MagicJadeAttrRandoms náº¿u há»£p lá»‡),
///    random nhÃ³m ká»¹ nÄƒng Hole1 1..N (N = sá»‘ nhÃ³m trong Server_Config.MagicJadeSkill1, máº·c Ä‘á»‹nh 4).
///  - TiÃªu thá»¥ / hoÃ n tráº£ váº­t pháº©m "\u0110\u00E1 Ma Ng\u1ECDc" (item 12268) trong PropBag.
///  - Ãp chá»‰ sá»‘ Ma Ngá»c theo Db_Tank_All.dbo.Magicstone_Template.
/// Má»i lá»—i DB/gá»­i packet Ä‘á»u bá»‹ báº¯t (chá»‰ log), khÃ´ng lÃ m rÆ¡i handler.
/// </summary>
public static class MagicJadeHelper
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	/// <summary>Item "\u0110\u00E1 Ma Ng\u1ECDc" (CategoryID 11) â€” nguyÃªn liá»‡u transform/xilian.</summary>
	public const int MagicJadeStoneItemId = 12268;

	private static readonly Random Rng = new Random();
	private static readonly object RngSync = new object();

	private static readonly object PushSync = new object();
	private static readonly ConditionalWeakTable<GamePlayer, object> PlanInfoPushed = new ConditionalWeakTable<GamePlayer, object>();
	private static readonly object PushSentinel = new object();

	// ------------------------------------------------------------------ random

	public static int Next(int maxExclusive)
	{
		lock (RngSync)
		{
			return Rng.Next(maxExclusive);
		}
	}

	/// <summary>Random property type 1..14. Náº¿u weights Ä‘á»§ 14 pháº§n tá»­ thÃ¬ random theo trá»ng sá»‘.</summary>
	public static int RollPropertyType(int[] weights)
	{
		if (weights == null || weights.Length < 14)
		{
			return Next(14) + 1;
		}
		int total = 0;
		for (int i = 0; i < 14; i++)
		{
			if (weights[i] > 0)
			{
				total += weights[i];
			}
		}
		if (total <= 0)
		{
			return Next(14) + 1;
		}
		int roll = Next(total);
		for (int i = 0; i < 14; i++)
		{
			if (weights[i] > 0)
			{
				roll -= weights[i];
				if (roll < 0)
				{
					return i + 1;
				}
			}
		}
		return 14;
	}

	/// <summary>Random nhÃ³m ká»¹ nÄƒng (Hole1) trong khoáº£ng 1..sá»‘ nhÃ³m cáº¥u hÃ¬nh.</summary>
	public static int RollSkillGroup()
	{
		int groups = 4;
		try
		{
			using (MagicStonePlanBussiness plan = new MagicStonePlanBussiness())
			{
				groups = plan.GetSkillGroupCount();
			}
		}
		catch (Exception ex)
		{
			Log.Error("MagicJadeHelper.RollSkillGroup read config error", ex);
		}
		if (groups < 1)
		{
			groups = 1;
		}
		return Next(groups) + 1;
	}

	/// <summary>GÃ¡n chá»‰ sá»‘ 6 thuá»™c tÃ­nh theo Magicstone_Template (template Ma Ngá»c + level hiá»‡n táº¡i).</summary>
	public static void ApplyMagicStoneStats(ItemInfo item, int templateId, int level)
	{
		if (item == null)
		{
			return;
		}
		if (level < 1)
		{
			level = 1;
		}
		MagicStoneInfo info = MagicStoneMgr.FindMagicStoneByLevel(templateId, level);
		if (info == null)
		{
			info = MagicStoneMgr.FindMagicStoneByLevel(templateId, 1);
		}
		if (info == null)
		{
			Log.Error("MagicJadeHelper.ApplyMagicStoneStats: no Magicstone_Template for templateID " + templateId);
			return;
		}
		item.AttackCompose = info.Attack;
		item.DefendCompose = info.Defence;
		item.AgilityCompose = info.Agility;
		item.LuckCompose = info.Luck;
		item.MagicAttack = info.MagicAttack;
		item.MagicDefence = info.MagicDefence;
	}

	public static int GetMagicStoneExp(int templateId, int level)
	{
		if (level < 1)
		{
			level = 1;
		}
		MagicStoneInfo info = MagicStoneMgr.FindMagicStoneByLevel(templateId, level);
		return (info != null) ? info.Exp : 0;
	}

	// ------------------------------------------------------------------ material

	public static int GetMaterialCount(GamePlayer player)
	{
		if (player == null || player.PropBag == null)
		{
			return 0;
		}
		return player.PropBag.GetItemCount(MagicJadeStoneItemId);
	}

	/// <summary>Trá»« "\u0110\u00E1 Ma Ng\u1ECDc" tá»« PropBag (khÃ´ng trá»« náº¿u count &lt;= 0).</summary>
	public static bool ConsumeMaterial(GamePlayer player, int count)
	{
		if (count <= 0)
		{
			return true;
		}
		if (player == null)
		{
			return false;
		}
		return player.RemoveTemplate(MagicJadeStoneItemId, count);
	}

	/// <summary>HoÃ n tráº£ "\u0110\u00E1 Ma Ng\u1ECDc" (dÃ¹ng khi thao tÃ¡c tháº¥t báº¡i sau khi Ä‘Ã£ trá»« nguyÃªn liá»‡u).</summary>
	public static void RefundMaterial(GamePlayer player, int count)
	{
		if (count <= 0 || player == null)
		{
			return;
		}
		try
		{
			ItemTemplateInfo template = ItemMgr.FindItemTemplate(MagicJadeStoneItemId);
			if (template == null)
			{
				return;
			}
			ItemInfo item = ItemInfo.CreateFromTemplate(template, count, 102);
			item.IsBinds = true;
			item.Count = count;
			player.AddTemplate(item);
		}
		catch (Exception ex)
		{
			Log.Error("MagicJadeHelper.RefundMaterial error playerID " + player.PlayerId, ex);
		}
	}

	/// <summary>HoÃ n tiá»n (dÃ¹ng khi mua bá»™ tháº¥t báº¡i sau khi Ä‘Ã£ trá»« tiá»n).</summary>
	public static void RefundMoney(GamePlayer player, int money)
	{
		if (money <= 0 || player == null)
		{
			return;
		}
		try
		{
			player.AddMoney(money);
		}
		catch (Exception ex)
		{
			Log.Error("MagicJadeHelper.RefundMoney error playerID " + player.PlayerId, ex);
		}
	}

	// ------------------------------------------------------------------ plan info (258/22)

	/// <summary>Gá»­i packet 258 byte 22 (PLAN_INFO). force=false: má»—i phiÃªn chá»‰ gá»­i 1 láº§n.</summary>
	public static void SendPlanInfo(GamePlayer player, bool force)
	{
		if (player == null || player.PlayerCharacter == null || player.Client == null || !player.IsActive)
		{
			return;
		}
		lock (PushSync)
		{
			if (!force && PlanInfoPushed.TryGetValue(player, out _))
			{
				return;
			}
			List<MagicStonePlanData> plans;
			using (MagicStonePlanBussiness plan = new MagicStonePlanBussiness())
			{
				plans = plan.GetPlans(player.PlayerCharacter.ID, true);
				// Neu bo active chua co loadout (bo cu tao truoc tinh nang loadout, hoac bo vua mo),
				// tu dong "nhan" 9 o ngoc dang cam hien tai lam loadout cua bo do.
				try
				{
					MagicStonePlanData active = (plans != null) ? plans.Find(p => p.IsActive) : null;
					if (active != null && plan.GetPlanSnapshots(active.PlanID).Count == 0)
					{
						List<MagicStonePlanSnapshot> snapshot = SnapshotPlanJades(player);
						if (snapshot.Count > 0)
						{
							plan.SavePlanSnapshots(player.PlayerCharacter.ID, active.PlanID, snapshot);
						}
					}
				}
				catch (Exception exAdopt)
				{
					Log.Error("MagicJadeHelper.SendPlanInfo adopt active loadout error", exAdopt);
				}
			}
			if (plans == null)
			{
				return;
			}
			GSPacketIn packet = new GSPacketIn(258, player.PlayerId);
			packet.WriteByte(22);
			packet.WriteInt(plans.Count);
			foreach (MagicStonePlanData planData in plans)
			{
				packet.WriteInt(planData.PlanID);
				packet.WriteString(planData.PlanName ?? "");
				packet.WriteInt(planData.IsActive ? 1 : 0);
			}
			player.SendTCP(packet);
			if (!PlanInfoPushed.TryGetValue(player, out _))
			{
				PlanInfoPushed.Add(player, PushSentinel);
			}
		}
	}

	/// <summary>Gá»­i plan info náº¿u chÆ°a gá»­i trong phiÃªn hiá»‡n táº¡i (fallback khi login push bá»‹ client bá» lá»¡).</summary>
	public static void EnsurePlanInfoPushed(GamePlayer player)
	{
		try
		{
			SendPlanInfo(player, false);
		}
		catch (Exception ex)
		{
			Log.Error("MagicJadeHelper.EnsurePlanInfoPushed error", ex);
		}
	}

	/// <summary>
	/// Háº¹n gá»­i plan info sau 6 giÃ¢y ká»ƒ tá»« lÃºc login: client Ä‘Äƒng kÃ½ listener PLAN_INFO trong
	/// MainResourceLoader.initSetup() (sau khi táº£i xong XML/language) nÃªn gá»­i ngay trong cá»¥m packet
	/// login cÃ³ thá»ƒ bá»‹ bá» lá»¡. KhÃ´ng áº£nh hÆ°á»Ÿng login náº¿u lá»—i (try/catch toÃ n bá»™).
	/// </summary>
	public static void SchedulePlanInfoPush(GamePlayer player)
	{
		if (player == null)
		{
			return;
		}
		try
		{
			Timer timer = null;
			timer = new Timer(delegate(object state)
			{
				try
				{
					GamePlayer gamePlayer = state as GamePlayer;
					if (gamePlayer != null && gamePlayer.IsActive)
					{
						SendPlanInfo(gamePlayer, false);
					}
				}
				catch (Exception ex)
				{
					Log.Error("MagicJadeHelper.SchedulePlanInfoPush callback error", ex);
				}
				finally
				{
					if (timer != null)
					{
						timer.Dispose();
					}
				}
			}, player, 6000, Timeout.Infinite);
		}
		catch (Exception ex)
		{
			Log.Error("MagicJadeHelper.SchedulePlanInfoPush error", ex);
		}
	}

	// ------------------------------------------------------------------ plan loadout (swap 9 viÃªn theo bá»™)

	private const int JadeFirstSlot = 21;
	private const int JadeSlotCount = 9;

	/// <summary>Chá»¥p 9 Ã´ ngá»c hiá»‡n táº¡i (Place 21..29) thÃ nh danh sÃ¡ch loadout (Slot 1..9).</summary>
	public static List<MagicStonePlanItemData> SnapshotLoadout(GamePlayer player)
	{
		List<MagicStonePlanItemData> list = new List<MagicStonePlanItemData>();
		if (player == null || player.MagicStoneBag == null)
		{
			return list;
		}
		for (int i = 0; i < JadeSlotCount; i++)
		{
			int place = JadeFirstSlot + i;
			ItemInfo item = player.MagicStoneBag.GetItemAt(place);
			if (item != null && item.Template != null && item.Template.CategoryID == 90)
			{
				list.Add(new MagicStonePlanItemData
				{
					Slot = i + 1,
					ItemID = item.ItemID,
					TemplateID = item.TemplateID
				});
			}
		}
		return list;
	}

	/// <summary>
	/// Ãp loadout 1 bá»™ vÃ o 9 Ã´ 21..29: hoÃ¡n Ä‘á»•i item hiá»‡n táº¡i vá»›i item Ä‘Ã­ch (theo ItemID),
	/// Ã´ khÃ´ng cÃ³ item Ä‘Ã­ch thÃ¬ Ä‘Æ°a item hiá»‡n táº¡i ra tÃºi (slot 32+).
	/// Tráº£ vá» sá»‘ Ã´ Ä‘Ã£ thay Ä‘á»•i.
	/// </summary>
	public static int ApplyLoadout(GamePlayer player, Dictionary<int, int> loadout)
	{
		if (player == null || player.MagicStoneBag == null || loadout == null)
		{
			return 0;
		}
		PlayerMagicStoneInventory bag = player.MagicStoneBag;
		int changed = 0;
		for (int i = 0; i < JadeSlotCount; i++)
		{
			int slot = i + 1;
			int place = JadeFirstSlot + i;
			ItemInfo current = bag.GetItemAt(place);
			int desiredItemId = 0;
			if (!loadout.TryGetValue(slot, out desiredItemId))
			{
				desiredItemId = 0;
			}

			if (desiredItemId <= 0)
			{
				// bá»™ nÃ y khÃ´ng cÃ³ ngá»c á»Ÿ Ã´ -> Ä‘Æ°a item hiá»‡n táº¡i ra tÃºi
				if (current != null)
				{
					int free = bag.FindFirstEmptySlot(32);
					if (free >= 0 && bag.MoveItem(place, free, 1))
					{
						changed++;
					}
				}
				continue;
			}

			if (current != null && current.ItemID == desiredItemId)
			{
				continue; // Ä‘Ã£ Ä‘Ãºng Ã´
			}

			ItemInfo wanted = bag.GetItemByItemID(desiredItemId);
			if (wanted == null)
			{
				continue; // item khÃ´ng cÃ²n trong tÃºi (Ä‘Ã£ bÃ¡n/ghÃ©p) -> giá»¯ nguyÃªn Ã´
			}

			if (current != null)
			{
				// hoÃ¡n Ä‘á»•i: Ä‘Æ°a item hiá»‡n táº¡i vÃ o chá»— item Ä‘Ã­ch, item Ä‘Ã­ch vá» Ã´ ngá»c
				if (bag.MoveItem(place, wanted.Place, 1))
				{
					changed++;
				}
			}
			else if (bag.MoveItem(wanted.Place, place, 1))
			{
				changed++;
			}
		}
		if (changed > 0)
		{
			bag.UpdateChangedPlaces();
		}
		return changed;
	}

	// ------------------------------------------------------------------ snapshot "ngoc gan chat voi bo"

	/// <summary>Chá»¥p full tráº¡ng thÃ¡i 9 Ã´ ngá»c hiá»‡n táº¡i (khÃ´ng thay Ä‘á»•i gÃ¬).</summary>
	public static List<MagicStonePlanSnapshot> SnapshotPlanJades(GamePlayer player)
	{
		List<MagicStonePlanSnapshot> list = new List<MagicStonePlanSnapshot>();
		if (player == null || player.MagicStoneBag == null)
		{
			return list;
		}
		for (int i = 0; i < JadeSlotCount; i++)
		{
			ItemInfo item = player.MagicStoneBag.GetItemAt(JadeFirstSlot + i);
			if (item == null || item.Template == null || item.Template.CategoryID != 90)
			{
				continue;
			}
			list.Add(new MagicStonePlanSnapshot
			{
				Slot = i + 1,
				TemplateID = item.TemplateID,
				StrengthenLevel = item.StrengthenLevel,
				StrengthenExp = item.StrengthenExp,
				AttackCompose = item.AttackCompose,
				DefendCompose = item.DefendCompose,
				AgilityCompose = item.AgilityCompose,
				LuckCompose = item.LuckCompose,
				MagicAttack = item.MagicAttack,
				MagicDefence = item.MagicDefence,
				Hole1 = item.Hole1,
				Hole2 = item.Hole2,
				Hole3 = item.Hole3,
				Hole4 = item.Hole4,
				Hole5 = item.Hole5,
				Hole6 = item.Hole6,
				IsBinds = item.IsBinds
			});
		}
		return list;
	}

	/// <summary>ThÃ¡o 9 viÃªn ngá»c Ä‘ang cáº¯m ra khá»i tÃºi (sau khi Ä‘Ã£ lÆ°u snapshot vÃ o bá»™ cÅ©).</summary>
	public static void RemoveEquippedJades(GamePlayer player)
	{
		if (player == null || player.MagicStoneBag == null)
		{
			return;
		}
		PlayerMagicStoneInventory bag = player.MagicStoneBag;
		for (int i = 0; i < JadeSlotCount; i++)
		{
			ItemInfo item = bag.GetItemAt(JadeFirstSlot + i);
			if (item != null && item.Template != null && item.Template.CategoryID == 90)
			{
				bag.RemoveItem(item);
			}
		}
	}

	/// <summary>
	/// Táº¡o láº¡i 9 viÃªn ngá»c tá»« snapshot cá»§a bá»™ Ä‘Ã­ch vÃ o cÃ¡c Ã´ 21..29.
	/// Ã” nÃ o khÃ´ng cÃ³ snapshot thÃ¬ Ä‘á»ƒ trá»‘ng (item cÃ²n sÃ³t Ä‘Æ°á»£c Ä‘Æ°a ra tÃºi 32+).
	/// </summary>
	public static int MaterializePlanJades(GamePlayer player, List<MagicStonePlanSnapshot> snapshots)
	{
		if (player == null || player.MagicStoneBag == null)
		{
			return 0;
		}
		PlayerMagicStoneInventory bag = player.MagicStoneBag;
		Dictionary<int, MagicStonePlanSnapshot> map = new Dictionary<int, MagicStonePlanSnapshot>();
		if (snapshots != null)
		{
			foreach (MagicStonePlanSnapshot s in snapshots)
			{
				if (s != null && s.Slot >= 1 && s.Slot <= JadeSlotCount)
				{
					map[s.Slot] = s;
				}
			}
		}
		int created = 0;
		for (int i = 0; i < JadeSlotCount; i++)
		{
			int place = JadeFirstSlot + i;
			ItemInfo current = bag.GetItemAt(place);
			if (current != null)
			{
				int free = bag.FindFirstEmptySlot(32);
				if (free >= 0)
				{
					bag.MoveItem(place, free, 1);
				}
				else
				{
					continue; // tÃºi Ä‘áº§y, bá» qua Ã´
				}
			}
			MagicStonePlanSnapshot snap;
			if (!map.TryGetValue(i + 1, out snap) || snap == null || snap.TemplateID <= 0)
			{
				continue;
			}
			ItemTemplateInfo template = ItemMgr.FindItemTemplate(snap.TemplateID);
			if (template == null)
			{
				Log.Error("MagicJadeHelper.MaterializePlanJades: missing template " + snap.TemplateID);
				continue;
			}
			ItemInfo item = ItemInfo.CreateFromTemplate(template, 1, 102);
			if (item == null)
			{
				continue;
			}
			item.TemplateID = snap.TemplateID;
			item.Count = 1;
			item.Place = place;
			item.BagType = 41;
			item.IsBinds = snap.IsBinds;
			item.IsExist = true;
			item.IsUsed = true;
			item.ValidDate = 0;
			item.StrengthenLevel = snap.StrengthenLevel;
			item.StrengthenExp = snap.StrengthenExp;
			item.AttackCompose = snap.AttackCompose;
			item.DefendCompose = snap.DefendCompose;
			item.AgilityCompose = snap.AgilityCompose;
			item.LuckCompose = snap.LuckCompose;
			item.MagicAttack = snap.MagicAttack;
			item.MagicDefence = snap.MagicDefence;
			item.Hole1 = snap.Hole1;
			item.Hole2 = snap.Hole2;
			item.Hole3 = snap.Hole3;
			item.Hole4 = snap.Hole4;
			item.Hole5 = snap.Hole5;
			item.Hole6 = snap.Hole6;
			if (bag.AddItemTo(item, place))
			{
				created++;
			}
		}
		bag.UpdateChangedPlaces();
		return created;
	}
}
