using System.Collections.Generic;
using System.Drawing;
using Game.Logic.Phy.Object;

namespace Game.Logic.PetSkills.ActiveSkills;

public class PetClonePosture : BasePetEffect
{
	private int m_probability;

	private int m_currentId;

	private int m_count;

	private int m_delay;

	private Dictionary<Living, int[]> oldPosition = new Dictionary<Living, int[]>();

	public PetClonePosture(int count, int probability, int skillId, int delay, string elementID)
		: base(ePetEffectType.PetClonePosture, elementID)
	{
		m_probability = ((probability == -1) ? 10000 : probability);
		m_currentId = skillId;
		m_count = count;
		m_delay = delay;
	}

	public override bool Start(Living living)
	{
		if (living.PetEffectList.GetOfType(ePetEffectType.PetClonePosture) is PetClonePosture petClonePosture)
		{
			petClonePosture.m_probability = ((m_probability > petClonePosture.m_probability) ? m_probability : petClonePosture.m_probability);
			return true;
		}
		return base.Start(living);
	}

	protected override void OnAttachedToPlayer(Player player)
	{
		player.PlayerBuffSkillPet += player_PlayerBuffSkillPet;
	}

	protected override void OnRemovedFromPlayer(Player player)
	{
		player.PlayerBuffSkillPet -= player_PlayerBuffSkillPet;
	}

	private void player_PlayerBuffSkillPet(Player player)
	{
		if (player.PetEffects.CurrentUseSkill != m_currentId || !(player.Game is PVPGame))
		{
			return;
		}
		// Việc di chuyển và tạo hình ảnh phân thân do PetActiveRuntime.ClonePostureBuff
		// xử lý để đảm bảo lấy đúng vị trí ban đầu trước khi di chuyển.
	}
}
