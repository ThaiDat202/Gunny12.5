using System.Reflection;
using Bussiness;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.Packets;
using SqlDataProvider.Data;
using log4net;

namespace Game.Server.Elf.Handle;

/// <summary>
/// Packet 707/3 GET_ELFEQUIP: ngÆ°á»i chÆ¡i kÃ­ch hoáº¡t má»™t váº­t pháº©m Linh Báº£o
/// (CategoryID = 91, item TemplateID 480xx) Ä‘ang náº±m trong tÃºi.
///
/// Client: BagView.as (item CategoryID 91, double click) â†’
///   <c>sendGetEllfEquip(bagType, place)</c> = 707/3 [int bagType][int place].
/// Server: xoÃ¡ item khá»i tÃºi, táº¡o record Sys_Users_ElfEquip, push 707/2 + 707/1.
/// (Tá»« báº£n vÃ¡ 2026-11-09, item Linh Báº£o cÅ©ng Ä‘Æ°á»£c tá»± chuyá»ƒn ngay khi nháº­n vÃ o tÃºi â€”
/// handler nÃ y giá»¯ láº¡i cho cÃ¡c item Ä‘Ã£ náº±m trong tÃºi tá»« trÆ°á»›c.)
/// </summary>
public class ElfEquipGet
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public int CommandHandler(GamePlayer player, GSPacketIn packet)
	{
		int bagType = packet.ReadInt();
		int place = packet.ReadInt();
		ItemInfo item = player.GetItemAt((eBageType)bagType, place);
		if (item == null || item.Template == null)
		{
			Log.Warn(string.Format("ElfEquipGet: item null player={0} bagType={1} place={2}", player.PlayerId, bagType, place));
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.EquipGet.Failed"));
			return 0;
		}
		Log.Warn(string.Format("ElfEquipGet: player={0} bagType={1} place={2} template={3} category={4}",
			player.PlayerId, bagType, place, item.TemplateID, item.Template.CategoryID));
		if (item.Template.CategoryID != 91)
		{
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.EquipGet.Failed"));
			return 0;
		}
		if (!player.RemoveItem(item))
		{
			Log.Warn(string.Format("ElfEquipGet: RemoveItem failed player={0} template={1}", player.PlayerId, item.TemplateID));
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.EquipGet.Failed"));
			return 0;
		}
		if (!ElfMgr.AddElfEquipFromItem(player, item, notify: true))
		{
			// KhÃ´ng táº¡o Ä‘Æ°á»£c record â†’ tráº£ item láº¡i tÃºi Ä‘á»ƒ ngÆ°á»i chÆ¡i khÃ´ng máº¥t Ä‘á»“.
			player.AddTemplate(item);
			Log.Error(string.Format("ElfEquipGet: AddElfEquipFromItem failed player={0} template={1}", player.PlayerId, item.TemplateID));
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.EquipGet.Failed"));
			return 0;
		}
		try
		{
			player.EquipBag.SaveToDatabase();
			player.PropBag.SaveToDatabase();
		}
		catch
		{
		}
		return 0;
	}
}