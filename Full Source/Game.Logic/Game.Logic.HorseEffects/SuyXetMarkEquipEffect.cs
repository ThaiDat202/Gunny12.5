using System.Linq;
using Game.Logic.Phy.Object;

namespace Game.Logic.HorseEffects;

/// <summary>
/// Suy Xét: đánh dấu một người chơi địch đang tàng hình, giữ hiệu lực 2 turn.
/// </summary>
public sealed class SuyXetMarkEquipEffect : BaseHorseEffect
{
    private readonly int m_currentId;

    public SuyXetMarkEquipEffect(int count, int probability, int type, int skillId, int delay, string elementID)
        : base(eHorseEffectType.SuyXetMarkEquipEffect, elementID)
    {
        m_currentId = skillId;
        m_count = count;
    }

    private int m_count;

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
        if (!(living is Player player) || player.PetEffects.CurrentHorseSkill != m_currentId) return;
        Player target = player.Game.GetAllEnemyPlayers(player).FirstOrDefault(p =>
            p != null && (p.IsHide || p.PetEffectList.GetOfType(ePetEffectType.PetStealthPosture_HidePassive) != null));
        if (target == null) return;
        new SuyXetMarkEffect(target, m_count).Start();
    }

    private sealed class SuyXetMarkEffect
    {
        private readonly Player m_target;
        private readonly bool m_wasHide;
        private int m_turns;
        private bool m_active;

        public SuyXetMarkEffect(Player target, int turns)
        {
            m_target = target;
            m_wasHide = target.IsHide;
            m_turns = turns;
        }

        public void Start()
        {
            m_active = true;
            m_target.IsHide = false;
            m_target.Game.SendPlayerPicture(m_target, 20050, true);
            m_target.BeginSelfTurn += OnBeginSelfTurn;
            m_target.Died += OnDied;
        }

        private void OnBeginSelfTurn(Living living)
        {
            if (!m_active) return;
            // Đè lại trạng thái ẩn của PET kungfu trong suốt 2 turn đánh dấu.
            m_target.IsHide = false;
            m_target.Game.SendPlayerPicture(m_target, 20050, true);
            m_turns--;
            if (m_turns <= 0) Stop();
        }

        private void OnDied(Living living)
        {
            Stop();
        }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.BeginSelfTurn -= OnBeginSelfTurn;
            m_target.Died -= OnDied;
            m_target.IsHide = m_wasHide;
            m_target.Game.SendPlayerPicture(m_target, 20050, false);
        }
    }
}
