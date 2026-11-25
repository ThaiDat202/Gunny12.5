using System;
using System.Reflection;
using Game.Base.Packets;
using Game.Server.Elf.Handle;
using Game.Server.GameObjects;
using Game.Server.Packets;
using log4net;

namespace Game.Server.Elf;

/// <summary>
/// Xử lý packet 707 (ELF_EQUIP) — hệ "Linh Bảo" của Thần Hộ Mệnh.
///
/// Trước đây packet 707 bị route vào ElfLogicProcessor (đọc byte đầu như ElfPackageType),
/// khiến request 707/3 (GET_ELFEQUIP) bị hiểu thành 664/3 (EXERCISE) và 707/4
/// (UPGRADE_ELFEQUIP) thành 664/4 (ADD_GROWTH) → lỗi/không phản hồi.
/// </summary>
public class ElfEquipLogicProcessor : AbstractElfProcessor
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private readonly ElfEquipGet _getHandler = new ElfEquipGet();

	private readonly ElfEquipUpgrade _upgradeHandler = new ElfEquipUpgrade();

	public override void OnGameData(GamePlayer player, GSPacketIn packet)
	{
		ElfEquipPackageType packageType = (ElfEquipPackageType)packet.ReadByte();
		try
		{
			switch (packageType)
			{
			case ElfEquipPackageType.GET_ELFEQUIP:
				_getHandler.CommandHandler(player, packet);
				break;
			case ElfEquipPackageType.UPGRADE_ELFEQUIP:
				_upgradeHandler.CommandHandler(player, packet);
				break;
			default:
				// 707/1, 707/2 là server -> client; client không gửi lên.
				break;
			}
		}
		catch (Exception exception)
		{
			Log.Error(string.Format("ElfEquipLogicProcessor PackageType:{1}, OnGameData is Error: {0}", exception, packageType));
			Log.Error("IP: " + player.Client.TcpEndpoint);
		}
	}
}
