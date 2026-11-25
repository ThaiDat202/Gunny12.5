using System;
using Bussiness;
using EntityDatabase.PlayerModels;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.Packets;
using SqlDataProvider.Data;

namespace Game.Server.Elf.Handle;

[ElfHandle(10)]
public class ActivateElf : IElfCommandHandler
{
    public int CommandHandler(GamePlayer player, GSPacketIn packet)
    {
        int bagType = packet.ReadInt();
        int place = packet.ReadInt();
        ItemInfo itemAt = player.GetItemAt((eBageType)bagType, place);
        if (itemAt == null)
        {
            player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.ActivateElf.Failed"));
            return 0;
        }
        ItemTemplateInfo template = itemAt.Template;
        Sys_Users_Elf sys_Users_Elf;
        try
        {
            // Tạo Elf trước; nếu lỗi cấu hình sẽ không mất item của người chơi.
            sys_Users_Elf = ElfMgr.AddElf(player, template);
        }
        catch (Exception)
        {
            player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.ActivateElf.Failed"));
            return 0;
        }
        if (sys_Users_Elf != null && player.RemoveTemplate(template, 1))
        {
            // Luu ngay de elf co ID va khong mat khi server tat dot ngot.
            player.SaveElfDataToDatabase();
            player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.ActivateElf.Success"));
        }
        return 0;
    }
}
