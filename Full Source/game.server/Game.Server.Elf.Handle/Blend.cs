using System.Collections.Generic;
using System.Linq;
using Bussiness;
using EntityDatabase.PlayerModels;
using EntityDatabase.ServerModels;
using Game.Base.Packets;
using Game.Server.Packets;
using Game.Server.GameObjects;
using MoreLinq;

namespace Game.Server.Elf.Handle;

[ElfHandle(12)]
public class Blend : IElfCommandHandler
{
	public int CommandHandler(GamePlayer player, GSPacketIn packet)
	{
		int elfId = packet.ReadInt();
		int viceElfId = packet.ReadInt();
		List<int> lockSkill = new List<int>();
		int num = packet.ReadInt();
		if (num > 7)
		{
			return 0;
		}
		for (int j = 0; j < num; j++)
		{
			lockSkill.Add(packet.ReadInt());
		}
		Sys_Users_Elf elf = player.PlayerCharacter.ElfInfo.FirstOrDefault((Sys_Users_Elf e) => e.ElfID == elfId);
		if (elf == null)
		{
			player.SendMessage(LanguageMgr.GetTranslation("Elf.Blend.ElfNotFound"));
			return 0;
		}
		Sys_Users_Elf viceElf = player.PlayerCharacter.ElfInfo.FirstOrDefault((Sys_Users_Elf e) => e.ElfID == viceElfId);
		if (viceElf == null)
		{
			player.SendMessage(LanguageMgr.GetTranslation("Elf.Skill.ViceElfNotFound"));
			return 0;
		}
		Elf_Template_Info elf_Template_Info = ElfMgr.Elfs.FirstOrDefault((Elf_Template_Info e) => e.TemplateID == elf.TempID);
		if (elf_Template_Info == null)
		{
			player.SendMessage(LanguageMgr.GetTranslation("Elf.Skill.ServerSideError"));
			return 0;
		}
		Elf_Template_Info elf_Template_Info2 = ElfMgr.Elfs.FirstOrDefault((Elf_Template_Info e) => e.TemplateID == viceElf.TempID);
		if (elf_Template_Info2 == null)
		{
			player.SendMessage(LanguageMgr.GetTranslation("Elf.Skill.ServerSideError"));
			return 0;
		}
		List<int> list = (string.IsNullOrEmpty(elf.SkillList) ? new List<int>() : elf.SkillList.Split(',').ToList().Select(int.Parse)
			.ToList());
		List<int> collection = (string.IsNullOrEmpty(viceElf.SkillList) ? new List<int>() : viceElf.SkillList.Split(',').ToList().Select(int.Parse)
			.ToList());
		List<int> list2 = new List<int>();
		List<int> list3 = new List<int>();
		List<int> list4 = new List<int>();
		int num2 = int.Parse(elf_Template_Info.MaxSkillCount);
		if (lockSkill.Count > num2)
		{
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.Skill.ServerSideError"));
			return 0;
		}
		if (lockSkill.Any((int s) => !list.Contains(s)))
		{
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.Skill.ServerSideError"));
			return 0;
		}
		int lockCost = GetLockCost(lockSkill.Count);
		if (lockCost > 0 && player.PlayerCharacter.Money < lockCost)
		{
			player.SendMessage(eMessageType.ERROR, LanguageMgr.GetTranslation("Elf.Blend.NoMoney"));
			return 0;
		}
		list.RemoveAll((int i) => lockSkill.Contains(i));
		list2.AddRange(list);
		list2.AddRange(collection);
		list3.AddRange(list2.Distinct());
		list4.AddRange(lockSkill);
		int remain = num2 - lockSkill.Count;
		if (remain > 0)
		{
			list4.AddRange(list3.Shuffle().Take((list3.Count >= remain) ? remain : list3.Count));
		}
		elf.SkillList = string.Join(",", list4);
		if (elf.CultivateGoodFeling < 20)
		{
			elf.GoodFeling++;
			elf.CultivateGoodFeling++;
			ElfMgr.Out.SendUpdateFeeling(player, elf);
		}
		if (lockCost > 0)
		{
			player.RemoveMoney(lockCost);
		}
		player.SaveElfDataToDatabase();
		player.DeleteElfFromDatabase(viceElf);
		ElfMgr.Out.SendUpdateSkill(player, elf);
		ElfMgr.Out.SendDiscardElf(player, viceElfId);
		player.SendMessage(LanguageMgr.GetTranslation("Elf.Blend.Success"));
		return 0;
	}

	private static int GetLockCost(int lockCount)
	{
		if (lockCount <= 0)
		{
			return 0;
		}
		string raw = Game.Server.Farm.ManorConfigMgr.GetServerConfig("ElfLockSkillPayMoney");
		if (string.IsNullOrEmpty(raw))
		{
			return 0;
		}
		string[] parts = raw.Split(',');
		int index = lockCount - 1;
		if (index < 0 || index >= parts.Length)
		{
			index = parts.Length - 1;
		}
		int cost;
		return int.TryParse(parts[index].Trim(), out cost) ? cost : 0;
	}
}