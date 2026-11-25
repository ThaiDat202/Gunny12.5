using Game.Logic.Phy.Object;

namespace Game.Logic.HorseEffects;

/// <summary>
/// Suy Xét: tăng sát thương đầu ra của bản thân 25% trong 2 turn.
/// Không dùng HorseAddDamageEffect vì effect đó sửa damage nhận vào.
/// </summary>
public sealed class SuyXetDamageEquipEffect : BaseHorseEffect
{
    private readonly int m_currentId;
    private readonly int m_turns;
    private readonly int m_percent;

    public SuyXetDamageEquipEffect(int count, int probability, int type, int skillId, int delay, string elementID, int percent)
        : base(eHorseEffectType.SuyXetDamageEquipEffect, elementID)
    {
        m_currentId = skillId;
        m_turns = count;
        m_percent = percent;
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
        if (!(living is Player player) || player.PetEffects.CurrentHorseSkill != m_currentId) return;
        new SuyXetDamageEffect(player, m_turns, m_percent).Start();
    }

    private sealed class SuyXetDamageEffect
    {
        private readonly Player m_owner;
        private readonly int m_percent;
        private int m_turns;
        private bool m_active;

        public SuyXetDamageEffect(Player owner, int turns, int percent)
        {
            m_owner = owner;
            m_turns = turns;
            m_percent = percent;
        }

        public void Start()
        {
            m_active = true;
            m_owner.OnMakeDamageEvent += OnMakeDamage;
            m_owner.BeginSelfTurn += OnBeginSelfTurn;
            m_owner.Died += OnDied;
        }

        private void OnMakeDamage(Living target, Living source, ref int damageAmount, ref int criticalAmount)
        {
            if (!m_active || source != m_owner) return;
            damageAmount = (int)System.Math.Ceiling(damageAmount * (100 + m_percent) / 100.0);
        }

        private void OnBeginSelfTurn(Living living)
        {
            if (!m_active) return;
            m_turns--;
            if (m_turns <= 0) Stop();
        }

        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_owner.OnMakeDamageEvent -= OnMakeDamage;
            m_owner.BeginSelfTurn -= OnBeginSelfTurn;
            m_owner.Died -= OnDied;
        }
    }
}
