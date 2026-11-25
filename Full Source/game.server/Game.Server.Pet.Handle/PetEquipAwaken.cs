using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Bussiness.Managers;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.GameUtils;
using Game.Server.Pet;
using log4net;
using SqlDataProvider.Data;

namespace Game.Server.Pet.Handle;

/// <summary>
/// Xử lý packet GameServer opcode 68, sub-code 64 - cửa sổ "Đào Tạo Pet"
/// (PetsSystemAwakeView của client 12.6) gồm 3 tab:
///
///   type = 1 - Thức Tỉnh (One Awake):
///     Client gửi: writeInt(1); writeInt(equipBagType); writeInt(equipPlace);
///                 writeInt(stoneBagType); writeInt(stonePlace);
///     Nguyên liệu: 1 trang bị pet (CategoryID 50/51/52, Quality = 5) + 1 đá thức tỉnh
///                  (item bất kỳ có Template.Property1 = 132, ví dụ 12570).
///     Tỉ lệ thành công = equip.Template.Property4 / 1000 (client hiển thị Property4/10 %).
///     Luôn tiêu hao cả trang bị lẫn 1 đá, kể cả khi thất bại.
///     Thành công: tạo 1 trang bị Quality = 6 cùng CategoryID (30101..30130 /
///     31101..31130 / 32101..32130), kèm 2 skill thức tỉnh của KindID tương ứng.
///
///   type = 2 - Thức Tỉnh Sâu (Super Awake):
///     Client gửi: writeInt(2); writeInt(equipBagType); writeInt(equipPlace);
///     Nguyên liệu: 1 trang bị Quality 6/7 có trong mapping TS_PetEquipAwakenSuper.xml
///                  + NeedCount đá PetExtConfigMgr.SuperAwakenStoneId (PropBag).
///     Luôn thành công, tạo trang bị NewEquipID trong mapping.
///
///   type = 3 - Dung Hợp (Blend):
///     Client gửi: writeInt(3); writeInt(equip1BagType); writeInt(equip1Place);
///                 writeInt(equip2BagType); writeInt(equip2Place);
///     Nguyên liệu: 2 trang bị Quality = 8 cùng TemplateID có trong mapping
///                  + NeedCount đá PetExtConfigMgr.BlendStoneId (PropBag).
///     Luôn thành công, tạo trang bị NewEquipID trong mapping.
///
/// Response cho cả 3 tab (client lắng nghe PkgEvent 68/37 ở OneAwakeView,
/// SuperAwakeView, SuperBlendAwakeView):
///   writeByte(37); writeBoolean(ok);
///   nếu ok: writeInt(itemId); writeInt(skillId1); writeInt(skillId2); writeInt(kindId);
///   (kindId chính là pet KindID - client đọc vào AwakenEquipInfo.belongPetId).
///
/// Record bảng Pet_Equip_Awake_Data được thêm qua
/// PetInventory.AddAwakenEquipRecord (insert DB ngay bằng SP_User_Add_eqAwakenPet).
/// </summary>
[PetHandleAttbute(64)]
public class PetEquipAwaken : IPetCommandHadler
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private const short PacketCode = 68;

	private const byte ResultCode = 37;

	private const int EquipAwakenCategoryMin = 50;

	private const int EquipAwakenCategoryMax = 52;

	/// <summary>Template.Property1 của "đá thức tỉnh" (client check Property1 == "132").</summary>
	private const int AwakenStoneProperty1 = 132;

	/// <summary>Vùng túi chính của EquipBag (PlayerEquipInventory dùng slot 31..79).</summary>
	private const int MainBagStart = 31;

	private const int MainBagEnd = 80;

	/// <summary>RemoveType khi tạo trang bị mới (giống PetAwaken cũ).</summary>
	private const int NewItemRemoveType = 102;

	/// <summary>Các pet KindID có skill thức tỉnh (dùng làm fallback khi không suy ra được).</summary>
	private static readonly int[] AwakenKindIds = new int[10] { 5, 6, 7, 9, 10, 11, 12, 15, 27, 32 };

	private readonly Random m_random = new Random();

	public bool CommandHandler(GamePlayer player, GSPacketIn packet)
	{
		if (player == null || packet == null)
		{
			return false;
		}
		// PetLogicProcessor đã đọc byte sub-code (64) trước khi gọi handler,
		// nên con trỏ packet đang nằm ở phần tham số của client.
		bool responseSent = false;
		try
		{
			if (packet.DataLeft < 4)
			{
				log.Error("PetEquipAwaken: packet 68/64 thiếu dữ liệu type, player=" + GetPlayerName(player));
				Fail(player, "Yêu cầu không hợp lệ.", ref responseSent);
				return false;
			}
			int type = packet.ReadInt();
			switch (type)
			{
			case 1:
				HandleOneAwake(player, packet, ref responseSent);
				break;
			case 2:
				HandleSuperAwake(player, packet, ref responseSent);
				break;
			case 3:
				HandleBlend(player, packet, ref responseSent);
				break;
			default:
				log.Warn("PetEquipAwaken: type=" + type + " không hợp lệ, player=" + GetPlayerName(player));
				Fail(player, "Yêu cầu không hợp lệ.", ref responseSent);
				break;
			}
		}
		catch (Exception exception)
		{
			log.Error("PetEquipAwaken: lỗi xử lý packet 68/64, player=" + GetPlayerName(player), exception);
			if (!responseSent)
			{
				Fail(player, "Có lỗi xảy ra, vui lòng thử lại.", ref responseSent);
			}
		}
		return true;
	}

	/// <summary>Tab 1 - Thức Tỉnh: 1 trang bị Quality 5 + 1 đá thức tỉnh.</summary>
	private void HandleOneAwake(GamePlayer player, GSPacketIn packet, ref bool responseSent)
	{
		if (packet.DataLeft < 16)
		{
			log.Error("PetEquipAwaken OneAwake: packet thiếu dữ liệu, player=" + GetPlayerName(player));
			Fail(player, "Yêu cầu không hợp lệ.", ref responseSent);
			return;
		}
		int equipBagType = packet.ReadInt();
		int equipPlace = packet.ReadInt();
		int stoneBagType = packet.ReadInt();
		int stonePlace = packet.ReadInt();

		PlayerInventory equipBag = GetBag(player, equipBagType);
		PlayerInventory stoneBag = GetBag(player, stoneBagType);
		if (equipBag == null || stoneBag == null)
		{
			Fail(player, "Túi chứa vật phẩm không hợp lệ.", ref responseSent);
			return;
		}
		if (ReferenceEquals(equipBag, stoneBag) && equipPlace == stonePlace)
		{
			Fail(player, "Vật phẩm được chọn không hợp lệ.", ref responseSent);
			return;
		}

		ItemInfo equip = equipBag.GetItemAt(equipPlace);
		if (!IsValidPetEquip(equip))
		{
			Fail(player, "Trang bị thức tỉnh không hợp lệ.", ref responseSent);
			return;
		}
		ItemTemplateInfo equipTemplate = equip.Template;
		if (equipTemplate.Quality != 5)
		{
			Fail(player, "Chỉ có thể thức tỉnh trang bị pet phẩm chất 5.", ref responseSent);
			return;
		}

		ItemInfo stone = stoneBag.GetItemAt(stonePlace);
		if (stone == null || stone.Template == null || stone.Count < 1 || !stone.IsValidItem() || stone.Template.Property1 != AwakenStoneProperty1)
		{
			Fail(player, "Vui lòng chọn đá thức tỉnh hợp lệ.", ref responseSent);
			return;
		}

		// Tỉ lệ thành công: Property4 = 200 nghĩa là 20% (client hiển thị Property4 / 10 %).
		int successRate = equipTemplate.Property4;
		if (successRate < 0)
		{
			successRate = 0;
		}
		if (successRate > 1000)
		{
			successRate = 1000;
		}
		bool success = m_random.Next(1000) < successRate;

		// Kiểm tra trước khi tiêu hao: template kết quả + skill + chỗ trống trong túi.
		ItemTemplateInfo resultTemplate = null;
		int kindId = 0;
		int skillId1 = 0;
		int skillId2 = 0;
		if (success)
		{
			// Server gốc (PetAwaken.cs cũ) chọn ngẫu nhiên 1 trong 3 CategoryID
			// (vũ khí/nón/giáp pet). Client cũng ghi "nhận được 1 món trang bị
			// thức tỉnh ngẫu nhiên" nên giữ hành vi random này.
			int resultCategory = EquipAwakenCategoryMin + m_random.Next(EquipAwakenCategoryMax - EquipAwakenCategoryMin + 1);
			resultTemplate = PickQuality6Template(resultCategory, out kindId);
			if (resultTemplate == null)
			{
				log.Error("PetEquipAwaken OneAwake: không có trang bị Quality 6 hợp lệ cho CategoryID=" + resultCategory + ", player=" + GetPlayerName(player));
				Fail(player, "Cấu hình thức tỉnh trang bị chưa đúng.", ref responseSent);
				return;
			}
			if (!TryRollAwakenSkills(kindId, out skillId1, out skillId2))
			{
				log.Error("PetEquipAwaken OneAwake: không có skill thức tỉnh cho KindID=" + kindId + " (TemplateID=" + resultTemplate.TemplateID + "), player=" + GetPlayerName(player));
				Fail(player, "Cấu hình kỹ năng thức tỉnh chưa đúng.", ref responseSent);
				return;
			}
			if (!HasRoomForResult(player, equipBag, equip, 1, stoneBag, stone, 1))
			{
				Fail(player, "Túi đã đầy, vui lòng dọn túi trước.", ref responseSent);
				return;
			}
		}

		// Tiêu hao nguyên liệu: luôn mất trang bị + 1 đá, kể cả khi roll thất bại.
		bool consumed;
		equipBag.BeginChanges();
		stoneBag.BeginChanges();
		try
		{
			consumed = equipBag.RemoveItemAt(equipPlace);
			if (consumed)
			{
				consumed = stoneBag.RemoveCountFromStack(stone, 1);
			}
		}
		finally
		{
			stoneBag.CommitChanges();
			equipBag.CommitChanges();
		}
		if (!consumed)
		{
			log.Error("PetEquipAwaken OneAwake: tiêu hao nguyên liệu thất bại, player=" + GetPlayerName(player));
			Fail(player, "Không thể tiêu hao nguyên liệu, vui lòng thử lại.", ref responseSent);
			return;
		}

		if (!success)
		{
			player.SendMessage("Thức tỉnh thất bại.");
			SendResult(player, false, 0, 0, 0, 0);
			responseSent = true;
			return;
		}

		ItemInfo newItem = CreateResultItem(player, resultTemplate);
		if (newItem == null)
		{
			Fail(player, "Có lỗi khi tạo trang bị thức tỉnh, vui lòng liên hệ GM.", ref responseSent);
			return;
		}
		ItemAwakenPet record = new ItemAwakenPet();
		record.itemId = newItem.ItemID;
		record.petId = kindId;
		record.skillId1 = skillId1;
		record.skillId2 = skillId2;
		player.PetBag.AddAwakenEquipRecord(record);

		player.SendMessage("Thức tỉnh thành công.");
		SendResult(player, true, record.itemId, record.skillId1, record.skillId2, record.petId);
		responseSent = true;
	}

	/// <summary>Tab 2 - Thức Tỉnh Sâu: 1 trang bị Quality 6/7, tiêu hao đá theo mapping.</summary>
	private void HandleSuperAwake(GamePlayer player, GSPacketIn packet, ref bool responseSent)
	{
		if (packet.DataLeft < 8)
		{
			log.Error("PetEquipAwaken SuperAwake: packet thiếu dữ liệu, player=" + GetPlayerName(player));
			Fail(player, "Yêu cầu không hợp lệ.", ref responseSent);
			return;
		}
		int equipBagType = packet.ReadInt();
		int equipPlace = packet.ReadInt();

		PlayerInventory equipBag = GetBag(player, equipBagType);
		if (equipBag == null)
		{
			Fail(player, "Túi chứa trang bị không hợp lệ.", ref responseSent);
			return;
		}
		ItemInfo equip = equipBag.GetItemAt(equipPlace);
		if (!IsValidPetEquip(equip))
		{
			Fail(player, "Trang bị thức tỉnh không hợp lệ.", ref responseSent);
			return;
		}
		ItemTemplateInfo equipTemplate = equip.Template;
		if (equipTemplate.Quality != 6 && equipTemplate.Quality != 7)
		{
			Fail(player, "Chỉ có thể thức tỉnh sâu trang bị pet phẩm chất 6 hoặc 7.", ref responseSent);
			return;
		}

		int newEquipId;
		int needCount;
		if (!PetExtConfigMgr.TryGetSuperAwaken(equipTemplate.TemplateID, out newEquipId, out needCount) || newEquipId <= 0 || needCount <= 0)
		{
			Fail(player, "Trang bị này không thể thức tỉnh sâu.", ref responseSent);
			return;
		}
		int stoneId = PetExtConfigMgr.SuperAwakenStoneId;
		if (stoneId <= 0)
		{
			log.Error("PetEquipAwaken SuperAwake: chưa cấu hình SuperAwakenStoneId, player=" + GetPlayerName(player));
			Fail(player, "Chưa cấu hình đá thức tỉnh sâu.", ref responseSent);
			return;
		}
		int stoneCount = player.PropBag.GetItemCount(stoneId);
		if (stoneCount < needCount)
		{
			Fail(player, "Không đủ đá thức tỉnh sâu (cần " + needCount + " viên).", ref responseSent);
			return;
		}
		ItemTemplateInfo resultTemplate = ItemMgr.FindItemTemplate(newEquipId);
		if (resultTemplate == null)
		{
			log.Error("PetEquipAwaken SuperAwake: không tìm thấy ItemTemplate NewEquipID=" + newEquipId + ", player=" + GetPlayerName(player));
			Fail(player, "Cấu hình trang bị thức tỉnh chưa đúng.", ref responseSent);
			return;
		}
		int kindId = ResolveAwakenKindId(player, equip, null, newEquipId, equipTemplate.TemplateID);
		if (kindId <= 0)
		{
			log.Error("PetEquipAwaken SuperAwake: không xác định được KindID cho TemplateID=" + equipTemplate.TemplateID + ", player=" + GetPlayerName(player));
			Fail(player, "Cấu hình kỹ năng thức tỉnh chưa đúng.", ref responseSent);
			return;
		}
		// Nâng cấp trang bị ĐÃ thức tỉnh: giữ nguyên cặp skill của record cũ (bản chất là cùng
		// một món đồ, chỉ lên cấp) thay vì roll lại skill ngẫu nhiên như bản trước.
		// Nếu trang bị chưa có record (mua/đổi trực tiếp món Q6/Q7) thì roll theo KindID.
		ItemAwakenPet oldRecord = player.PetBag.GetAwakenRecord(equip.ItemID);
		int skillId1 = 0;
		int skillId2 = 0;
		if (oldRecord != null && oldRecord.skillId1 > 0)
		{
			skillId1 = oldRecord.skillId1;
			skillId2 = oldRecord.skillId2;
		}
		else if (!TryRollAwakenSkills(kindId, out skillId1, out skillId2))
		{
			// KindID mới (pet 35..39, 101...) chưa có bảng skill thức tỉnh trong DB -> cho phép
			// tạo trang bị không kèm skill thay vì chặn tính năng nâng cấp.
			if (PetExtConfigMgr.GetRandomAwakenSkill(kindId) > 0)
			{
				log.Error("PetEquipAwaken SuperAwake: không có skill thức tỉnh cho KindID=" + kindId + ", player=" + GetPlayerName(player));
				Fail(player, "Cấu hình kỹ năng thức tỉnh chưa đúng.", ref responseSent);
				return;
			}
			skillId1 = 0;
			skillId2 = 0;
		}
		if (!HasRoomForResult(player, equipBag, equip, 1, null, null, 0))
		{
			Fail(player, "Túi đã đầy, vui lòng dọn túi trước.", ref responseSent);
			return;
		}

		// Tiêu hao: trang bị + NeedCount đá trong PropBag.
		bool consumed;
		equipBag.BeginChanges();
		player.PropBag.BeginChanges();
		try
		{
			consumed = equipBag.RemoveItemAt(equipPlace);
			if (consumed)
			{
				consumed = player.PropBag.RemoveTemplate(stoneId, needCount);
			}
		}
		finally
		{
			player.PropBag.CommitChanges();
			equipBag.CommitChanges();
		}
		if (!consumed)
		{
			log.Error("PetEquipAwaken SuperAwake: tiêu hao nguyên liệu thất bại, player=" + GetPlayerName(player));
			Fail(player, "Không thể tiêu hao nguyên liệu, vui lòng thử lại.", ref responseSent);
			return;
		}

		ItemInfo newItem = CreateResultItem(player, resultTemplate);
		if (newItem == null)
		{
			Fail(player, "Có lỗi khi tạo trang bị thức tỉnh, vui lòng liên hệ GM.", ref responseSent);
			return;
		}
		ItemAwakenPet record = new ItemAwakenPet();
		record.itemId = newItem.ItemID;
		record.petId = kindId;
		record.skillId1 = skillId1;
		record.skillId2 = skillId2;
		player.PetBag.AddAwakenEquipRecord(record);

		player.SendMessage("Thức tỉnh thành công.");
		SendResult(player, true, record.itemId, record.skillId1, record.skillId2, record.petId);
		responseSent = true;
	}

	/// <summary>Tab 3 - Dung Hợp: 2 trang bị Quality 8 cùng TemplateID, tiêu hao đá dung hợp.</summary>
	private void HandleBlend(GamePlayer player, GSPacketIn packet, ref bool responseSent)
	{
		if (packet.DataLeft < 16)
		{
			log.Error("PetEquipAwaken Blend: packet thiếu dữ liệu, player=" + GetPlayerName(player));
			Fail(player, "Yêu cầu không hợp lệ.", ref responseSent);
			return;
		}
		int equip1BagType = packet.ReadInt();
		int equip1Place = packet.ReadInt();
		int equip2BagType = packet.ReadInt();
		int equip2Place = packet.ReadInt();

		PlayerInventory equip1Bag = GetBag(player, equip1BagType);
		PlayerInventory equip2Bag = GetBag(player, equip2BagType);
		if (equip1Bag == null || equip2Bag == null)
		{
			Fail(player, "Túi chứa trang bị không hợp lệ.", ref responseSent);
			return;
		}
		if (ReferenceEquals(equip1Bag, equip2Bag) && equip1Place == equip2Place)
		{
			Fail(player, "Hai trang bị được chọn không hợp lệ.", ref responseSent);
			return;
		}
		ItemInfo equip1 = equip1Bag.GetItemAt(equip1Place);
		ItemInfo equip2 = equip2Bag.GetItemAt(equip2Place);
		if (!IsValidPetEquip(equip1) || !IsValidPetEquip(equip2))
		{
			Fail(player, "Trang bị dung hợp không hợp lệ.", ref responseSent);
			return;
		}
		ItemTemplateInfo template1 = equip1.Template;
		ItemTemplateInfo template2 = equip2.Template;
		if (template1.Quality != 8 || template2.Quality != 8)
		{
			Fail(player, "Chỉ có thể dung hợp trang bị pet phẩm chất 8.", ref responseSent);
			return;
		}
		if (template1.TemplateID != template2.TemplateID)
		{
			Fail(player, "Hai trang bị dung hợp phải cùng loại.", ref responseSent);
			return;
		}

		int newEquipId;
		int needCount;
		if (!PetExtConfigMgr.TryGetSuperAwaken(template1.TemplateID, out newEquipId, out needCount) || newEquipId <= 0 || needCount <= 0)
		{
			Fail(player, "Trang bị này không thể dung hợp.", ref responseSent);
			return;
		}
		int stoneId = PetExtConfigMgr.BlendStoneId;
		if (stoneId <= 0)
		{
			log.Error("PetEquipAwaken Blend: chưa cấu hình BlendStoneId, player=" + GetPlayerName(player));
			Fail(player, "Chưa cấu hình đá dung hợp.", ref responseSent);
			return;
		}
		int stoneCount = player.PropBag.GetItemCount(stoneId);
		if (stoneCount < needCount)
		{
			Fail(player, "Không đủ đá dung hợp (cần " + needCount + " viên).", ref responseSent);
			return;
		}
		ItemTemplateInfo resultTemplate = ItemMgr.FindItemTemplate(newEquipId);
		if (resultTemplate == null)
		{
			log.Error("PetEquipAwaken Blend: không tìm thấy ItemTemplate NewEquipID=" + newEquipId + ", player=" + GetPlayerName(player));
			Fail(player, "Cấu hình trang bị dung hợp chưa đúng.", ref responseSent);
			return;
		}
		int kindId = ResolveAwakenKindId(player, equip1, equip2, newEquipId, template1.TemplateID);
		if (kindId <= 0)
		{
			log.Error("PetEquipAwaken Blend: không xác định được KindID cho TemplateID=" + template1.TemplateID + ", player=" + GetPlayerName(player));
			Fail(player, "Cấu hình kỹ năng dung hợp chưa đúng.", ref responseSent);
			return;
		}
		// Dung hợp 2 trang bị Q8 cùng TemplateID -> Q9. Giữ cặp skill của record cũ (ưu tiên
		// trang bị 1) để món đồ sau khi dung hợp vẫn giữ nguyên skill thức tỉnh.
		ItemAwakenPet oldRecord1 = player.PetBag.GetAwakenRecord(equip1.ItemID);
		ItemAwakenPet oldRecord2 = player.PetBag.GetAwakenRecord(equip2.ItemID);
		ItemAwakenPet oldRecord = (oldRecord1 != null && oldRecord1.skillId1 > 0) ? oldRecord1 : ((oldRecord2 != null && oldRecord2.skillId1 > 0) ? oldRecord2 : null);
		int skillId1 = 0;
		int skillId2 = 0;
		if (oldRecord != null)
		{
			skillId1 = oldRecord.skillId1;
			skillId2 = oldRecord.skillId2;
		}
		else if (!TryRollAwakenSkills(kindId, out skillId1, out skillId2))
		{
			if (PetExtConfigMgr.GetRandomAwakenSkill(kindId) > 0)
			{
				log.Error("PetEquipAwaken Blend: không có skill thức tỉnh cho KindID=" + kindId + ", player=" + GetPlayerName(player));
				Fail(player, "Cấu hình kỹ năng dung hợp chưa đúng.", ref responseSent);
				return;
			}
			skillId1 = 0;
			skillId2 = 0;
		}
		if (!HasRoomForResult(player, equip1Bag, equip1, 1, equip2Bag, equip2, 1))
		{
			Fail(player, "Túi đã đầy, vui lòng dọn túi trước.", ref responseSent);
			return;
		}

		// Tiêu hao: 2 trang bị + NeedCount đá trong PropBag.
		bool consumed;
		equip1Bag.BeginChanges();
		equip2Bag.BeginChanges();
		player.PropBag.BeginChanges();
		try
		{
			consumed = equip1Bag.RemoveItemAt(equip1Place);
			if (consumed)
			{
				consumed = equip2Bag.RemoveItemAt(equip2Place);
			}
			if (consumed)
			{
				consumed = player.PropBag.RemoveTemplate(stoneId, needCount);
			}
		}
		finally
		{
			player.PropBag.CommitChanges();
			equip2Bag.CommitChanges();
			equip1Bag.CommitChanges();
		}
		if (!consumed)
		{
			log.Error("PetEquipAwaken Blend: tiêu hao nguyên liệu thất bại, player=" + GetPlayerName(player));
			Fail(player, "Không thể tiêu hao nguyên liệu, vui lòng thử lại.", ref responseSent);
			return;
		}

		ItemInfo newItem = CreateResultItem(player, resultTemplate);
		if (newItem == null)
		{
			Fail(player, "Có lỗi khi tạo trang bị dung hợp, vui lòng liên hệ GM.", ref responseSent);
			return;
		}
		ItemAwakenPet record = new ItemAwakenPet();
		record.itemId = newItem.ItemID;
		record.petId = kindId;
		record.skillId1 = skillId1;
		record.skillId2 = skillId2;
		player.PetBag.AddAwakenEquipRecord(record);

		player.SendMessage("Dung hợp thành công.");
		SendResult(player, true, record.itemId, record.skillId1, record.skillId2, record.petId);
		responseSent = true;
	}

	/// <summary>
	/// Tạo trang bị kết quả trong túi chính (EquipBag), lưu DB ngay để lấy ItemID thật.
	/// Trả về null nếu không tạo/lưu được.
	/// </summary>
	private ItemInfo CreateResultItem(GamePlayer player, ItemTemplateInfo resultTemplate)
	{
		if (player == null || resultTemplate == null)
		{
			return null;
		}
		ItemInfo itemInfo;
		// Gói BeginChanges/CommitChanges: tránh gửi packet cập nhật túi khi ItemID còn = 0
		// (client sẽ thấy item "trắng" nếu update được gửi trước khi insert DB).
		player.EquipBag.BeginChanges();
		try
		{
			itemInfo = ItemInfo.CreateFromTemplate(resultTemplate, 1, NewItemRemoveType);
			if (itemInfo == null)
			{
				log.Error("PetEquipAwaken: CreateFromTemplate trả về null, TemplateID=" + resultTemplate.TemplateID + ", player=" + GetPlayerName(player));
				return null;
			}
			itemInfo.IsBinds = true;
			itemInfo.ValidDate = m_random.Next(5, 15);
			itemInfo.IsDirty = true;
			if (!player.EquipBag.AddItem(itemInfo))
			{
				log.Error("PetEquipAwaken: EquipBag.AddItem thất bại, TemplateID=" + resultTemplate.TemplateID + ", player=" + GetPlayerName(player));
				return null;
			}
			player.EquipBag.SaveNewItemToDatabase();
		}
		catch (Exception exception)
		{
			log.Error("PetEquipAwaken: lỗi tạo trang bị mới, TemplateID=" + resultTemplate.TemplateID + ", player=" + GetPlayerName(player), exception);
			return null;
		}
		finally
		{
			player.EquipBag.CommitChanges();
		}
		if (itemInfo.ItemID <= 0)
		{
			log.Error("PetEquipAwaken: lưu trang bị mới vào DB thất bại (ItemID=0), TemplateID=" + resultTemplate.TemplateID + ", player=" + GetPlayerName(player));
			return null;
		}
		return itemInfo;
	}

	/// <summary>
	/// Chọn ngẫu nhiên 1 trang bị Quality 6 cùng CategoryID (dãy trong PetExtConfigMgr).
	/// Chỉ chọn template tồn tại trong ItemMgr và suy ra được KindID có skill thức tỉnh.
	/// </summary>
	private ItemTemplateInfo PickQuality6Template(int categoryId, out int kindId)
	{
		kindId = 0;
		int[] equipIds = PetExtConfigMgr.GetQuality6EquipIds(categoryId);
		if (equipIds == null || equipIds.Length == 0)
		{
			return null;
		}
		List<ItemTemplateInfo> candidates = new List<ItemTemplateInfo>();
		List<int> candidateKinds = new List<int>();
		for (int i = 0; i < equipIds.Length; i++)
		{
			ItemTemplateInfo template = ItemMgr.FindItemTemplate(equipIds[i]);
			if (template == null)
			{
				continue;
			}
			// Loài pet của trang bị lấy theo bảng DesignRow -> KindID (PetEquipKindMap.xml).
			// Nhờ vậy kết quả thức tỉnh có thể rơi vào MỌI loài pet có skill thức tỉnh
			// (dòng 01..08 + Rồng Băng Giá dòng 22 + Bạch Diệm Thánh Hổ dòng 26), thay vì
			// chỉ 9 dòng đầu như bản trước. Dòng không xác định được KindID sẽ bị bỏ qua để
			// không tạo record petId sai loại.
			int candidateKind = PetExtConfigMgr.GetPetEquipKindId(template.TemplateID);
			if (candidateKind <= 0)
			{
				continue;
			}
			if (PetExtConfigMgr.GetRandomAwakenSkill(candidateKind) <= 0)
			{
				continue;
			}
			candidates.Add(template);
			candidateKinds.Add(candidateKind);
		}
		if (candidates.Count == 0)
		{
			return null;
		}
		int index = m_random.Next(candidates.Count);
		kindId = candidateKinds[index];
		return candidates[index];
	}

	/// <summary>
	/// Xác định KindID cho record thức tỉnh: ưu tiên record cũ của trang bị,
	/// sau đó TemplateID, NewEquipID, cuối cùng là 1 KindID bất kỳ có skill thức tỉnh.
	/// </summary>
	private static int ResolveAwakenKindId(GamePlayer player, ItemInfo primaryEquip, ItemInfo secondaryEquip, int newEquipId, int primaryTemplateId)
	{
		int kindId = GetRecordedKindId(player, (primaryEquip == null) ? 0 : primaryEquip.ItemID);
		if (kindId <= 0)
		{
			kindId = GetRecordedKindId(player, (secondaryEquip == null) ? 0 : secondaryEquip.ItemID);
		}
		if (kindId <= 0)
		{
			kindId = PetExtConfigMgr.GetPetEquipKindId(primaryTemplateId);
		}
		if (kindId <= 0)
		{
			kindId = PetExtConfigMgr.GetPetEquipKindId(newEquipId);
		}
		// Không loại bỏ KindID chỉ vì loài đó chưa có skill thức tỉnh trong Pet_Special_Skills
		// (các pet mới 35..39, 101): trang bị vẫn phải thuộc đúng loài để chặn/gắn đúng.
		if (kindId <= 0)
		{
			kindId = FindAnyAwakenKindId();
		}
		return kindId;
	}

	/// <summary>KindID lưu trong record Pet_Equip_Awake_Data của trang bị (nếu có).</summary>
	private static int GetRecordedKindId(GamePlayer player, int itemId)
	{
		if (player == null || player.PetBag == null || player.PetBag.ItemAwakens == null || itemId <= 0)
		{
			return 0;
		}
		List<ItemAwakenPet> records = player.PetBag.ItemAwakens;
		for (int i = 0; i < records.Count; i++)
		{
			ItemAwakenPet record = records[i];
			if (record != null && record.itemId == itemId)
			{
				return record.petId;
			}
		}
		return 0;
	}

	/// <summary>Tìm 1 KindID bất kỳ có skill thức tỉnh (fallback cuối cùng).</summary>
	private static int FindAnyAwakenKindId()
	{
		for (int i = 0; i < AwakenKindIds.Length; i++)
		{
			if (PetExtConfigMgr.GetRandomAwakenSkill(AwakenKindIds[i]) > 0)
			{
				return AwakenKindIds[i];
			}
		}
		return 0;
	}

	/// <summary>
	/// Roll 2 skill thức tỉnh khác nhau cho KindID.
	/// Lưu ý: PetSpecialSkillMgr.GetSkillAwaken tạo Random mới mỗi lần gọi nên trong cùng
	/// một tick có thể trả về cùng giá trị; vì vậy có retry giới hạn thời gian (Stopwatch)
	/// thay vì vòng lặp vô hạn như code cũ.
	/// </summary>
	private bool TryRollAwakenSkills(int kindId, out int skillId1, out int skillId2)
	{
		skillId1 = 0;
		skillId2 = 0;
		if (kindId <= 0)
		{
			return false;
		}
		for (int i = 0; i < 8 && skillId1 <= 0; i++)
		{
			skillId1 = PetExtConfigMgr.GetRandomAwakenSkill(kindId);
		}
		if (skillId1 <= 0)
		{
			return false;
		}
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.ElapsedMilliseconds < 80)
		{
			int candidate = PetExtConfigMgr.GetRandomAwakenSkill(kindId);
			if (candidate > 0 && candidate != skillId1)
			{
				skillId2 = candidate;
				return true;
			}
			Thread.Sleep(1);
		}
		// KindID chỉ có 1 skill thức tỉnh: chấp nhận lặp skill để không chặn người chơi.
		skillId2 = skillId1;
		log.Warn("PetEquipAwaken: KindID=" + kindId + " chỉ trả về một skill thức tỉnh, dùng lặp skill.");
		return true;
	}

	/// <summary>
	/// Kiểm tra túi chính còn chỗ cho trang bị kết quả. Trang bị nguyên liệu nằm trong
	/// vùng túi chính (slot 31..79) sẽ giải phóng 1 ô sau khi bị tiêu hao.
	/// </summary>
	private static bool HasRoomForResult(GamePlayer player, PlayerInventory consumedBag1, ItemInfo consumedItem1, int consumeCount1, PlayerInventory consumedBag2, ItemInfo consumedItem2, int consumeCount2)
	{
		if (player == null || player.EquipBag == null)
		{
			return false;
		}
		if (player.EquipBag.FindFirstEmptySlot(MainBagStart, MainBagEnd) >= 0)
		{
			return true;
		}
		if (WillFreeMainBagSlot(consumedBag1, consumedItem1, consumeCount1))
		{
			return true;
		}
		return WillFreeMainBagSlot(consumedBag2, consumedItem2, consumeCount2);
	}

	private static bool WillFreeMainBagSlot(PlayerInventory bag, ItemInfo item, int consumeCount)
	{
		if (bag == null || item == null || consumeCount <= 0)
		{
			return false;
		}
		if (bag.BagType != (int)eBageType.EquipBag)
		{
			return false;
		}
		if (consumeCount < item.Count)
		{
			return false;
		}
		return item.Place >= MainBagStart && item.Place < MainBagEnd;
	}

	/// <summary>Chỉ chấp nhận túi chính (EquipBag) / túi hành trang (PropBag) như client gửi.</summary>
	private static PlayerInventory GetBag(GamePlayer player, int bagType)
	{
		if (player == null)
		{
			return null;
		}
		switch ((eBageType)bagType)
		{
		case eBageType.EquipBag:
			return player.EquipBag;
		case eBageType.PropBag:
			return player.PropBag;
		default:
			return null;
		}
	}

	private static bool IsValidPetEquip(ItemInfo item)
	{
		return item != null && item.Template != null && item.Count > 0 && item.IsEquipPet() && item.IsValidItem();
	}

	private void Fail(GamePlayer player, string message, ref bool responseSent)
	{
		if (responseSent)
		{
			return;
		}
		responseSent = true;
		try
		{
			if (!string.IsNullOrEmpty(message))
			{
				player.SendMessage(message);
			}
		}
		catch (Exception exception)
		{
			log.Error("PetEquipAwaken: gửi thông báo lỗi thất bại, player=" + GetPlayerName(player), exception);
		}
		SendResult(player, false, 0, 0, 0, 0);
	}

	/// <summary>Gửi response opcode 68 sub-code 37 theo đúng format client 12.6 đọc.</summary>
	private void SendResult(GamePlayer player, bool ok, int itemId, int skillId1, int skillId2, int kindId)
	{
		if (player == null)
		{
			return;
		}
		try
		{
			GSPacketIn response = new GSPacketIn(PacketCode);
			response.WriteByte(ResultCode);
			response.WriteBoolean(ok);
			if (ok)
			{
				response.WriteInt(itemId);
				response.WriteInt(skillId1);
				response.WriteInt(skillId2);
				response.WriteInt(kindId);
			}
			player.SendTCP(response);
		}
		catch (Exception exception)
		{
			log.Error("PetEquipAwaken: gửi response 68/37 thất bại, player=" + GetPlayerName(player), exception);
		}
	}

	private static string GetPlayerName(GamePlayer player)
	{
		try
		{
			if (player != null && player.PlayerCharacter != null)
			{
				return player.PlayerCharacter.NickName;
			}
		}
		catch
		{
			// bỏ qua - chỉ dùng cho log
		}
		return "?";
	}
}
