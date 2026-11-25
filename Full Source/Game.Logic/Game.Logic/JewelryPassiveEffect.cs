using System;
using System.Collections.Generic;
using Game.Logic.Effects;
using Game.Logic.Phy.Object;

namespace Game.Logic;

/// <summary>
/// Hiệu ứng passive thật cho kỹ năng đặc biệt Tẩy luyện trang sức.
/// ID 1087..1156, mỗi nhóm 5 cấp.
/// </summary>
public sealed class JewelryPassiveEffect
{
    private readonly Player m_owner;
    private readonly int m_id;
    private readonly int m_level;
    private readonly int m_group;
    private readonly Random m_random = new Random(Guid.NewGuid().GetHashCode());

    private int m_stacks;
    private int m_hits;
    private bool m_used;
    private bool m_defApplied;
    private int m_healRemaining;
    private int m_healTurns;
    private readonly Dictionary<Player, DamageTakenDebuff> m_debuffs = new Dictionary<Player, DamageTakenDebuff>();

    private bool IsPvpOnly
    {
        get
        {
            // Khát Vọng, Bụi Gai, Cuồng Bạo: mô tả ghi rõ PVP.
            return m_group == 5 || m_group == 8 || m_group == 9;
        }
    }

    public JewelryPassiveEffect(Player owner, int id)
    {
        m_owner = owner;
        m_id = id;
        m_level = ((id - 1087) % 5) + 1;
        m_group = (id - 1087) / 5;
    }

    public void Start()
    {
        if (m_owner == null) return;
        if (IsPvpOnly && !(m_owner.Game is PVPGame)) return;

        switch (m_group)
        {
            case 0: // Hồi Ma: hạ địch hồi MP pet, mỗi trận 1 lần
                m_owner.AfterKillingLiving += OnKillForMp;
                break;
            case 1: // Đánh Trước: đầu trận 2-6% nhận 30 MP
                m_owner.GameStarted += OnGameStarted;
                break;
            case 2: // Bị Thương: đánh trúng tích stack tăng sát thương mục tiêu nhận
                m_owner.OnMakeDamageEvent += OnMakeDamageWound;
                break;
            case 3: // Cường Lực: hạ địch +2-6% sát thương nền, tối đa 5 stack
            case 4: // Huyết Mạch: hạ địch +2-6% HP tối đa, tối đa 5 stack
                m_owner.AfterKillingLiving += OnKillForStack;
                break;
            case 5: // Khát Vọng: hạ địch hút 4-12% sát thương, hồi trong 2 turn
                m_owner.AfterKillingLiving += OnKillForLifesteal;
                m_owner.BeginSelfTurn += OnSelfTurnLifesteal;
                break;
            case 6: // Phù Hộ: khi chết hồi HP cho đồng đội thấp HP nhất
            case 7: // Phù Hộ: khi chết tịnh hóa 1-3 đồng đội
                m_owner.Died += OnOwnerDied;
                break;
            case 8: // Bụi Gai: khi bị đánh, 2-6% phản 50% sát thương
                m_owner.BeforeTakeDamage += OnBrambleBeforeTakeDamage;
                break;
            case 9: // Cuồng Bạo: đánh mục tiêu HP thấp, 5% gây thêm 50% HP tối đa
                m_owner.OnMakeDamageEvent += OnMakeDamageRampage;
                break;
            case 10: // Nhẫn Thuật: bị đánh 20 lần hồi 10-20% HP, mỗi trận 1 lần
                m_owner.BeforeTakeDamage += OnNinjaBeforeTakeDamage;
                break;
            case 11: // Kháng Xuyên Giáp
            case 12: // Kháng Bỏng
            case 13: // Kháng Đóng Băng
                m_owner.GameStarted += OnGameStartedDefence;
                break;
        }
    }

    private void OnGameStartedDefence(Living living)
    {
        if (m_defApplied) return;
        m_defApplied = true;
        switch (m_group)
        {
            case 11:
                m_owner.BaseGuard += m_owner.BaseGuard * (m_level * 2) / 100.0;
                break;
            case 12:
                m_owner.FireDefence += m_owner.FireDefence * (m_level * 2) / 100.0;
                break;
            case 13:
                m_owner.WaterDefence += m_owner.WaterDefence * (m_level * 0.5) / 100.0;
                break;
        }
    }

    private void OnKillForMp(Living killer, Living target, int damageAmount, int criticalAmount)
    {
        if (m_used) return;
        m_used = true;
        m_owner.AddPetMP(20 + 5 * (m_level - 1));
    }

    private void OnGameStarted(Living living)
    {
        if (m_used) return;
        m_used = true;
        int chance = m_level + 1; // 2..6%
        if (m_random.Next(100) < chance) m_owner.AddPetMP(30);
    }

    private void OnMakeDamageWound(Living target, Living source, ref int damageAmount, ref int criticalAmount)
    {
        if (source != m_owner) return;
        if (!(target is Player targetPlayer) || targetPlayer.Team == m_owner.Team) return;
        if (!m_debuffs.TryGetValue(targetPlayer, out DamageTakenDebuff debuff))
        {
            debuff = new DamageTakenDebuff(targetPlayer, m_level + 1);
            m_debuffs[targetPlayer] = debuff;
            debuff.Start();
        }
        debuff.AddStack();
    }

    private void OnKillForStack(Living killer, Living target, int damageAmount, int criticalAmount)
    {
        if (m_stacks >= 5) return;
        m_stacks++;
        if (m_group == 3)
        {
            m_owner.BaseDamage += m_owner.BaseDamage * (m_level + 1) / 100.0;
        }
        else
        {
            int add = (int)(m_owner.MaxBlood * (m_level + 1) / 100.0);
            m_owner.MaxBlood += add;
            m_owner.Blood += add;
        }
    }

    private void OnKillForLifesteal(Living killer, Living target, int damageAmount, int criticalAmount)
    {
        int heal = (int)((damageAmount + criticalAmount) * (2 + 2 * m_level) / 100.0);
        if (heal <= 0) return;
        m_healRemaining += heal;
        m_healTurns = 2;
    }

    private void OnSelfTurnLifesteal(Living living)
    {
        if (m_healRemaining <= 0 || m_healTurns <= 0) return;
        int part = Math.Max(1, m_healRemaining / m_healTurns);
        m_owner.AddBlood(part);
        m_healRemaining -= part;
        m_healTurns--;
    }

    private void OnOwnerDied(Living living)
    {
        if (m_used) return;
        m_used = true;
        List<Player> allies = GetAllies();
        if (allies.Count == 0) return;

        if (m_group == 6)
        {
            Player lowest = null;
            double lowestRatio = 2.0;
            foreach (Player p in allies)
            {
                double ratio = p.MaxBlood <= 0 ? 0 : (double)p.Blood / p.MaxBlood;
                if (ratio < lowestRatio)
                {
                    lowestRatio = ratio;
                    lowest = p;
                }
            }
            if (lowest != null)
            {
                int[] percent = { 10, 12, 15, 18, 20 };
                int heal = (int)(lowest.MaxBlood * percent[m_level - 1] / 100.0);
                lowest.AddBlood(heal);
            }
        }
        else
        {
            int count = Math.Min(3, m_level);
            for (int i = 0; i < count && allies.Count > 0; i++)
            {
                int idx = m_random.Next(allies.Count);
                ClearNegativeEffects(allies[idx]);
                allies.RemoveAt(idx);
            }
        }
    }

    private void OnBrambleBeforeTakeDamage(Living living, Living source, ref int damageAmount, ref int criticalAmount)
    {
        if (m_used || source == null || source == m_owner) return;
        int chance = m_level + 1; // 2..6%
        if (m_random.Next(100) >= chance) return;
        m_used = true;
        int reflect = Math.Max(1, damageAmount / 2);
        source.AddBlood(-reflect, 1);
        if (source.Blood <= 0) source.Die();
    }

    private void OnMakeDamageRampage(Living target, Living source, ref int damageAmount, ref int criticalAmount)
    {
        if (source != m_owner || target == null || !target.IsLiving) return;
        int hpPercent = target.MaxBlood <= 0 ? 100 : (int)(target.Blood * 100L / target.MaxBlood);
        int[] thresholdByLevel = { 10, 12, 15, 18, 20 };
        int threshold = thresholdByLevel[m_level - 1];
        if (hpPercent > threshold) return;
        if (m_random.Next(100) >= 5) return;
        damageAmount += target.MaxBlood / 2;
    }

    private void OnNinjaBeforeTakeDamage(Living living, Living source, ref int damageAmount, ref int criticalAmount)
    {
        if (m_used) return;
        m_hits++;
        if (m_hits < 20) return;
        m_used = true;
        int[] percent = { 10, 12, 15, 18, 20 };
        int heal = (int)(m_owner.MaxBlood * percent[m_level - 1] / 100.0);
        m_owner.AddBlood(heal);
    }

    private List<Player> GetAllies()
    {
        var result = new List<Player>();
        try
        {
            foreach (Player p in m_owner.Game.Players.Values)
            {
                if (p != null && p != m_owner && p.IsLiving && p.Team == m_owner.Team) result.Add(p);
            }
        }
        catch { }
        return result;
    }

    private static void ClearNegativeEffects(Player player)
    {
        try
        {
            player.EffectList.StopEffect(typeof(ContinueReduceBloodEffect));
            player.EffectList.StopEffect(typeof(IceFronzeEffect));
            player.EffectList.StopEffect(typeof(ReduceStrengthEffect));
        }
        catch { }
    }

    private sealed class DamageTakenDebuff
    {
        private readonly Player m_target;
        private readonly int m_percent;
        private int m_stacks;
        private int m_turns = 2;
        private bool m_active;

        public DamageTakenDebuff(Player target, int percent)
        {
            m_target = target;
            m_percent = percent;
        }

        public void Start()
        {
            m_active = true;
            m_target.BeforeTakeDamage += OnBeforeTakeDamage;
            m_target.BeginSelfTurn += OnBeginSelfTurn;
        }

        public void AddStack()
        {
            m_stacks = Math.Min(5, m_stacks + 1);
            m_turns = 2;
        }

        private void OnBeforeTakeDamage(Living living, Living source, ref int damageAmount, ref int criticalAmount)
        {
            if (!m_active || m_stacks <= 0) return;
            damageAmount = (int)Math.Ceiling(damageAmount * (100 + m_stacks * m_percent) / 100.0);
        }

        private void OnBeginSelfTurn(Living living)
        {
            if (!m_active) return;
            m_turns--;
            if (m_turns <= 0) Stop();
        }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.BeforeTakeDamage -= OnBeforeTakeDamage;
            m_target.BeginSelfTurn -= OnBeginSelfTurn;
        }
    }
}
