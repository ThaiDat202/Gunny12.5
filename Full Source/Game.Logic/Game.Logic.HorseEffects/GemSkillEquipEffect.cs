using Game.Logic.Phy.Object;

namespace Game.Logic.HorseEffects;

/// <summary>
/// Cầu nối kích hoạt Ngọc Kỹ Năng khi người chơi bấm G.
/// </summary>
public sealed class GemSkillEquipEffect : BaseHorseEffect
{
    private readonly int m_skillId;

    public GemSkillEquipEffect(int skillId)
        : base(eHorseEffectType.GemSkillEquipEffect, "0")
    {
        m_skillId = skillId;
    }

    protected override void OnAttachedToPlayer(Player player)
    {
        player.PlayerBuffSkillHorse += OnPlayerBuffSkillHorse;
    }

    protected override void OnRemovedFromPlayer(Player player)
    {
        player.PlayerBuffSkillHorse -= OnPlayerBuffSkillHorse;
    }

    private void OnPlayerBuffSkillHorse(Living living)
    {
        if (!(living is Player player) || player.PetEffects.CurrentHorseSkill != m_skillId) return;
        GemSkillRuntime.Apply(player, m_skillId);
    }
}
