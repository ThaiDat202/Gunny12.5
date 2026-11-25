using System;
using System.Reflection;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.Statics;
using log4net;

namespace Game.Server.Pet.Handle;

/// <summary>
/// Mở ô pet — packet 68, subcode 10.
///
/// Client gửi (GameSocketOut.sendPetCellUnlock):
///   writeByte(10); writeBoolean(isBind); writeInt(times);
///     isBind = true  -> thanh toán bằng Xu khóa (DDTMoney)
///     isBind = false -> thanh toán bằng Xu (Money)
///   times = số ô muốn mở thêm (client tính từ ô người chơi bấm:
///           times = place - unlockedCellNum + 1).
///
/// Server trả (PetsBagManager.petCellUnLockHandler):
///   writeByte(10); writeInt(unlockedCellNum);
///
/// Giá do PetExtConfigMgr.GetUnlockCost(unlocked, times) tính theo đúng công thức
/// của client (priceIndex bắt đầu = unlocked - FreeCellCount + 1, 1-based).
/// </summary>
[PetHandleAttbute(10)]
public class PetCellUnlock : IPetCommandHadler
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public bool CommandHandler(GamePlayer player, GSPacketIn packet)
	{
		// PetLogicProcessor đã đọc byte subcode (10) trước khi gọi handler,
		// nên con trỏ packet đang nằm ở phần tham số của client.
		bool isBind = packet.ReadBoolean();
		int times = packet.ReadInt();
		int userId = player.PlayerCharacter.ID;
		if (times <= 0)
		{
			Log.Warn("PetCellUnlock: times=" + times + " không hợp lệ, userID=" + userId);
			return false;
		}
		int unlocked = player.PetBag.UnlockedCellCount;
		int maxCells = PetExtConfigMgr.MaxPetCellCount;
		if (unlocked >= maxCells)
		{
			player.SendMessage("Bạn đã mở tối đa số ô pet.");
			return false;
		}
		// Không cho mở vượt quá tổng số ô pet tối đa.
		times = Math.Min(times, maxCells - unlocked);
		int cost = PetExtConfigMgr.GetUnlockCost(unlocked, times);
		if (cost <= 0)
		{
			player.SendMessage("Chưa cấu hình giá mở ô pet.");
			return false;
		}
		// isBind = true: trả bằng Xu khóa (DDTMoney), ngược lại: Xu thường (Money).
		// Kiểm tra đủ tiền trước để tránh bug cũ của MoneyDirect (khi thiếu DDTMoney
		// lại trừ Xu thường / có thể không trừ gì mà vẫn trả true).
		if (isBind)
		{
			if (player.PlayerCharacter.DDTMoney < cost)
			{
				player.SendMessage("Bạn không đủ Xu khóa để mở ô pet.");
				return false;
			}
		}
		else if (player.PlayerCharacter.Money < cost)
		{
			player.SendMessage("Bạn không đủ Xu để mở ô pet.");
			return false;
		}
		MoneyType moneyType = (isBind ? MoneyType.DDTMoney : MoneyType.Money);
		if (!player.MoneyDirect(moneyType, cost))
		{
			return false;
		}
		if (!player.PetBag.UnlockPetCells(times))
		{
			player.SendMessage("Mở ô pet thất bại, vui lòng thử lại.");
			return false;
		}
		GSPacketIn packetOut = new GSPacketIn(68);
		packetOut.WriteByte(10);
		packetOut.WriteInt(player.PetBag.UnlockedCellCount);
		player.SendTCP(packetOut);
		player.SendMessage("Đã mở thêm " + times + " ô pet.");
		return true;
	}
}
