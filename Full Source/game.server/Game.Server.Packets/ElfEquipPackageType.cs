namespace Game.Server.Packets;

/// <summary>
/// Sub-type của packet 707 (ELF_EQUIP) — hệ "Linh Bảo" thuộc Thần Hộ Mệnh.
/// Client <c>elf/ElfEquipPackageType.as</c> gửi 3 (GET_ELFEQUIP) và 4 (UPGRADE_ELFEQUIP).
/// Server push 1 (level từng hệ), 2 (danh sách Linh Bảo sở hữu), 4 (kết quả nâng cấp).
/// </summary>
public enum ElfEquipPackageType
{
	UPDATE_ELFEQUIPLVINFO = 1,
	UPDATE_ELFEQUIP = 2,
	GET_ELFEQUIP = 3,
	UPGRADE_ELFEQUIP = 4
}
