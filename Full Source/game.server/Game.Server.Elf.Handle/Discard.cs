using System.Linq;
using EntityDatabase.PlayerModels;
using Game.Base.Packets;
using Bussiness;
using Game.Server.GameObjects;
using Game.Server.Packets;

namespace Game.Server.Elf.Handle;

[ElfHandle(7)]
public class Discard : IElfCommandHandler
{
	public int CommandHandler(GamePlayer player, GSPacketIn packet)
	{
		int elfId = packet.ReadInt();
		Sys_Users_Elf sys_Users_Elf = player.PlayerCharacter.ElfInfo.FirstOrDefault((Sys_Users_Elf e) => e.ElfID == elfId);
		if (sys_Users_Elf == null)
		{
			player.SendMessage("Elf.Fight.NotFound");
			return 0;
		}
		if (sys_Users_Elf.ElfEquipId > 0)
		{
			// Dang mac Linh Bao -> phai thao truoc (client cung chan, server chan lai cho chac).
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.Discard.EquipOn"));
			return 0;
		}
		if (player.DeleteElfFromDatabase(sys_Users_Elf))
		{
			ElfMgr.Out.SendDiscardElf(player, elfId);
			player.SendMessage(LanguageMgr.GetTranslation("Elf.Discard.Success"));
			return 0;
		}
		player.SendMessage("Elf.Discard.Failed");
		return 0;
	}
}
