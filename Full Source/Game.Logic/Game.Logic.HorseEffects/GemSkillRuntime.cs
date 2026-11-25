using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Logic.PetSkills;
using Game.Logic.Phy.Object;

namespace Game.Logic.HorseEffects;

/// <summary>
/// Hiệu ứng thực tế cho 15 nhóm Ngọc Kỹ Năng (skill 320xx..334xx).
/// </summary>
public static class GemSkillRuntime
{
    private static readonly Random Rng = new Random();

    public static void Apply(Player player, int skillId)
    {
        if (player == null) return;
        int group = skillId / 100;
        int level = skillId % 100;
        if (level < 1) level = 1;
        if (level > 10) level = 10;
        bool pvp = player.Game is PVPGame;

        switch (group)
        {
            case 320: // Suy Yếu: mọi địch nhận thêm 6-15% damage, 1 turn, PVP
                if (!pvp) return;
                foreach (Player enemy in player.Game.GetAllEnemyPlayers(player))
                {
                    if (enemy == null) continue;
                    new DamageTakenBuff(enemy, 1, 5 + level).Start();
                    ShowMark(enemy);
                }
                break;

            case 321: // Tăng Tốc: bản thân +15-25% damage, 2 turn
                new OutgoingDamageBuff(player, 2, 14 + level).Start();
                break;

            case 322: // Đạn Nổ: khi chết gây 20-40% HP quanh 300, PVP
                if (!pvp) return;
                new ExplodeOnDeath(player, ExplodePercents[level - 1]).Start();
                break;

            case 323: // Kháng Bạo: 20-30% không bị bạo kích, 2 turn
                new CritResistBuff(player, 2, CritResistPercents[level - 1]).Start();
                break;

            case 324: // Khiêu Khích: giảm 16-25% damage, taunt 2 turn, PVP
                if (!pvp) return;
                new IncomingDamageReduction(player, 2, 15 + level).Start();
                new TauntBuff(player, 2).Start();
                break;

            case 325: // Khiên Chắn: đồng đội HP thấp nhất nhận khiên 15-30% HP, 2 turn
                {
                    Player ally = player.Game.GetAllTeamPlayers(player)
                        .Where(x => x != null && x.IsLiving)
                        .OrderBy(x => x.MaxBlood <= 0 ? 2.0 : (double)x.Blood / x.MaxBlood)
                        .FirstOrDefault();
                    if (ally != null)
                        new ShieldBuff(ally, 2, (int)(player.MaxBlood * ShieldPercents[level - 1] / 100.0)).Start();
                }
                break;

            case 326: // Khỏe Mạnh: +50-110 thể lực, 1 turn
                player.AddEnergy(EnergyValues[level - 1]);
                break;

            case 327: // Né Tránh: 5-20% né, 2 turn, PVP
                if (!pvp) return;
                new DodgeBuff(player, 2, DodgePercents[level - 1]).Start();
                break;

            case 328: // Dịch Chuyển: turn sau hồi MP 1-10 (không thực hiện teleport trong bản này)
                new MpNextTurn(player, level).Start();
                break;

            case 329: // Bạo Kích: +15-25% tỉ lệ bạo kích, 1 turn
                new CritBuff(player, 1, CritPercents[level - 1]).Start();
                break;

            case 330: // Giải Trừ: 30-50% xóa ngẫu nhiên 1 pet passive địch, PVP
                if (!pvp) return;
                foreach (Player enemy in player.Game.GetAllEnemyPlayers(player))
                {
                    if (enemy == null) continue;
                    if (Rng.Next(100) >= DispelPercents[level - 1]) continue;
                    RemoveRandomPetPassive(enemy);
                }
                break;

            case 331: // Cầu Phúc: random 1 trong 3 buff 10-20%, 2 turn
                {
                    int pct = BlessPercents[level - 1];
                    int pick = Rng.Next(3);
                    if (pick == 0) new OutgoingDamageBuff(player, 2, pct).Start();
                    else if (pick == 1) new IncomingDamageReduction(player, 2, pct).Start();
                    else new CritBuff(player, 2, pct).Start();
                }
                break;

            case 332: // Ma Hóa: +8-18% 4 chỉ số, 2 turn
                new StatBuff(player, 2, StatPercents[level - 1]).Start();
                break;

            case 333: // Suy Xét: +25% damage 2 turn + đánh dấu địch tàng hình 2 turn
                new OutgoingDamageBuff(player, 2, 25).Start();
                MarkHiddenEnemy(player, 2);
                break;

            case 334: // Nhược Hóa: địch -10-20% damage, 1 turn, PVP
                if (!pvp) return;
                foreach (Player enemy in player.Game.GetAllEnemyPlayers(player))
                {
                    if (enemy == null) continue;
                    new OutgoingDamageBuff(enemy, 1, -WeakPercents[level - 1]).Start();
                }
                break;
        }
    }

    private static readonly int[] ExplodePercents = { 20, 22, 24, 26, 28, 30, 32, 34, 36, 40 };
    private static readonly int[] CritResistPercents = { 20, 21, 22, 23, 24, 25, 26, 27, 28, 30 };
    private static readonly int[] ShieldPercents = { 15, 16, 17, 18, 19, 20, 22, 25, 28, 30 };
    private static readonly int[] EnergyValues = { 50, 55, 60, 65, 70, 75, 80, 90, 100, 110 };
    private static readonly int[] DodgePercents = { 5, 6, 7, 8, 10, 12, 14, 16, 18, 20 };
    private static readonly int[] CritPercents = { 15, 16, 17, 18, 19, 20, 21, 22, 23, 25 };
    private static readonly int[] DispelPercents = { 30, 32, 34, 36, 38, 40, 42, 44, 46, 50 };
    private static readonly int[] BlessPercents = { 10, 11, 12, 13, 14, 15, 16, 17, 18, 20 };
    private static readonly int[] StatPercents = { 8, 9, 10, 11, 12, 13, 14, 15, 16, 18 };
    private static readonly int[] WeakPercents = { 10, 11, 12, 13, 14, 15, 16, 17, 18, 20 };

    private static void ShowMark(Player target)
    {
        try
        {
            target.PetEffectTrigger = true;
            target.Game.SendPlayerPicture(target, 20050, true);
        }
        catch { }
    }

    private static void RemoveRandomPetPassive(Player target)
    {
        try
        {
            ArrayList list = target.PetEffectList.List;
            if (list == null || list.Count == 0) return;
            (list[Rng.Next(list.Count)] as AbstractPetEffect)?.Stop();
        }
        catch { }
    }

    private static void MarkHiddenEnemy(Player owner, int turns)
    {
        Player target = owner.Game.GetAllEnemyPlayers(owner).FirstOrDefault(p =>
            p != null && (p.IsHide || p.PetEffectList.GetOfType(ePetEffectType.PetStealthPosture_HidePassive) != null));
        if (target != null) new MarkEffect(target, turns).Start();
    }

    // ---------- Hiệu ứng damage đầu ra ----------
    private sealed class OutgoingDamageBuff
    {
        private readonly Player m_owner;
        private readonly int m_percent;
        private int m_turns;
        private bool m_active;

        public OutgoingDamageBuff(Player owner, int turns, int percent)
        {
            m_owner = owner;
            m_turns = turns;
            m_percent = percent;
        }

        public void Start()
        {
            m_active = true;
            m_owner.OnMakeDamageEvent += OnMakeDamage;
            m_owner.BeginSelfTurn += OnTurn;
            m_owner.Died += OnDied;
        }

        private void OnMakeDamage(Living target, Living source, ref int damageAmount, ref int criticalAmount)
        {
            if (!m_active || source != m_owner) return;
            damageAmount = (int)Math.Ceiling(damageAmount * (100 + m_percent) / 100.0);
        }

        private void OnTurn(Living living) { if (--m_turns <= 0) Stop(); }
        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_owner.OnMakeDamageEvent -= OnMakeDamage;
            m_owner.BeginSelfTurn -= OnTurn;
            m_owner.Died -= OnDied;
        }
    }

    // ---------- Hiệu ứng tăng damage phải chịu ----------
    private sealed class DamageTakenBuff
    {
        private readonly Player m_target;
        private readonly int m_percent;
        private int m_turns;
        private bool m_active;

        public DamageTakenBuff(Player target, int turns, int percent)
        {
            m_target = target;
            m_turns = turns;
            m_percent = percent;
        }

        public void Start()
        {
            m_active = true;
            m_target.BeforeTakeDamage += OnBeforeTakeDamage;
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnBeforeTakeDamage(Living living, Living source, ref int damageAmount, ref int criticalAmount)
        {
            if (!m_active) return;
            damageAmount = (int)Math.Ceiling(damageAmount * (100 + m_percent) / 100.0);
        }

        private void OnTurn(Living living) { if (--m_turns <= 0) Stop(); }
        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.BeforeTakeDamage -= OnBeforeTakeDamage;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }

    // ---------- Giảm damage nhận vào ----------
    private sealed class IncomingDamageReduction
    {
        private readonly Player m_target;
        private readonly int m_percent;
        private int m_turns;
        private bool m_active;

        public IncomingDamageReduction(Player target, int turns, int percent)
        {
            m_target = target;
            m_turns = turns;
            m_percent = percent;
        }

        public void Start()
        {
            m_active = true;
            m_target.BeforeTakeDamage += OnBeforeTakeDamage;
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnBeforeTakeDamage(Living living, Living source, ref int damageAmount, ref int criticalAmount)
        {
            if (!m_active) return;
            damageAmount = (int)Math.Ceiling(damageAmount * (100 - m_percent) / 100.0);
            if (damageAmount < 1) damageAmount = 1;
        }

        private void OnTurn(Living living) { if (--m_turns <= 0) Stop(); }
        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.BeforeTakeDamage -= OnBeforeTakeDamage;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }

    // ---------- Khiên ----------
    private sealed class ShieldBuff
    {
        private readonly Player m_target;
        private int m_shield;
        private int m_turns;
        private bool m_active;

        public ShieldBuff(Player target, int turns, int shield)
        {
            m_target = target;
            m_turns = turns;
            m_shield = shield;
        }

        public void Start()
        {
            m_active = true;
            m_target.BeforeTakeDamage += OnBeforeTakeDamage;
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnBeforeTakeDamage(Living living, Living source, ref int damageAmount, ref int criticalAmount)
        {
            if (!m_active || m_shield <= 0) return;
            int total = damageAmount + criticalAmount;
            int absorb = Math.Min(m_shield, total);
            m_shield -= absorb;
            if (criticalAmount > 0)
            {
                int fromCrit = Math.Min(criticalAmount, absorb);
                criticalAmount -= fromCrit;
                absorb -= fromCrit;
            }
            damageAmount = Math.Max(0, damageAmount - absorb);
            if (damageAmount == 0 && criticalAmount == 0) damageAmount = 1;
        }

        private void OnTurn(Living living) { if (--m_turns <= 0) Stop(); }
        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.BeforeTakeDamage -= OnBeforeTakeDamage;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }

    // ---------- Dodge ----------
    private sealed class DodgeBuff
    {
        private readonly Player m_target;
        private readonly int m_percent;
        private int m_turns;
        private bool m_active;
        private bool m_used;

        public DodgeBuff(Player target, int turns, int percent)
        {
            m_target = target;
            m_turns = turns;
            m_percent = percent;
        }

        public void Start()
        {
            m_active = true;
            m_target.BeforeTakeDamage += OnBeforeTakeDamage;
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnBeforeTakeDamage(Living living, Living source, ref int damageAmount, ref int criticalAmount)
        {
            if (!m_active || m_used) return;
            if (Rng.Next(100) < m_percent)
            {
                m_used = true;
                damageAmount = 0;
                criticalAmount = 0;
                Stop();
            }
        }

        private void OnTurn(Living living) { if (--m_turns <= 0) Stop(); }
        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.BeforeTakeDamage -= OnBeforeTakeDamage;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }

    // ---------- Crit immunity ----------
    private sealed class CritResistBuff
    {
        private readonly Player m_target;
        private readonly int m_percent;
        private int m_turns;
        private bool m_active;

        public CritResistBuff(Player target, int turns, int percent)
        {
            m_target = target;
            m_turns = turns;
            m_percent = percent;
        }

        public void Start()
        {
            m_active = true;
            m_target.BeforeTakeDamage += OnBeforeTakeDamage;
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnBeforeTakeDamage(Living living, Living source, ref int damageAmount, ref int criticalAmount)
        {
            if (!m_active) return;
            if (Rng.Next(100) < m_percent) criticalAmount = 0;
        }

        private void OnTurn(Living living) { if (--m_turns <= 0) Stop(); }
        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.BeforeTakeDamage -= OnBeforeTakeDamage;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }

    // ---------- Crit rate ----------
    private sealed class CritBuff
    {
        private readonly Player m_target;
        private readonly int m_percent;
        private int m_turns;
        private bool m_active;

        public CritBuff(Player target, int turns, int percent)
        {
            m_target = target;
            m_turns = turns;
            m_percent = percent;
        }

        public void Start()
        {
            m_active = true;
            m_target.Crit += m_percent;
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnTurn(Living living) { if (--m_turns <= 0) Stop(); }
        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.Crit -= m_percent;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }

    // ---------- All-stat buff ----------
    private sealed class StatBuff
    {
        private readonly Player m_player;
        private readonly int m_percent;
        private readonly int m_attack, m_defence, m_agility, m_luck;
        private int m_turns;
        private bool m_active;

        public StatBuff(Player player, int turns, int percent)
        {
            m_player = player;
            m_turns = turns;
            m_percent = percent;
            m_attack = Math.Max(1, (int)(player.Attack * percent / 100.0));
            m_defence = Math.Max(1, (int)(player.Defence * percent / 100.0));
            m_agility = Math.Max(1, (int)(player.Agility * percent / 100.0));
            m_luck = Math.Max(1, (int)(player.Lucky * percent / 100.0));
        }

        public void Start()
        {
            m_active = true;
            m_player.Attack += m_attack;
            m_player.Defence += m_defence;
            m_player.Agility += m_agility;
            m_player.Lucky += m_luck;
            m_player.BeginSelfTurn += OnTurn;
            m_player.Died += OnDied;
        }

        private void OnTurn(Living living) { if (--m_turns <= 0) Stop(); }
        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_player.Attack -= m_attack;
            m_player.Defence -= m_defence;
            m_player.Agility -= m_agility;
            m_player.Lucky -= m_luck;
            m_player.BeginSelfTurn -= OnTurn;
            m_player.Died -= OnDied;
        }
    }

    // ---------- Explosion on death ----------
    private sealed class ExplodeOnDeath
    {
        private readonly Player m_owner;
        private readonly int m_percent;
        private bool m_active;

        public ExplodeOnDeath(Player owner, int percent)
        {
            m_owner = owner;
            m_percent = percent;
        }

        public void Start()
        {
            m_active = true;
            m_owner.Died += OnDied;
        }

        private void OnDied(Living living)
        {
            if (!m_active) return;
            m_active = false;
            try
            {
                foreach (Player enemy in m_owner.Game.GetAllEnemyPlayers(m_owner))
                {
                    if (enemy == null || !enemy.IsLiving) continue;
                    if (enemy.Distance(m_owner.X, m_owner.Y) > 300) continue;
                    int damage = Math.Max(1, enemy.MaxBlood * m_percent / 100);
                    enemy.RemoveBlood(damage);
                    if (enemy.Blood <= 0) enemy.Die();
                }
            }
            catch { }
        }
    }

    // ---------- Taunt ----------
    private sealed class TauntBuff
    {
        private readonly Player m_owner;
        private readonly List<Player> m_enemies = new List<Player>();
        private int m_turns;
        private bool m_active;

        public TauntBuff(Player owner, int turns)
        {
            m_owner = owner;
            m_turns = turns;
        }

        public void Start()
        {
            m_active = true;
            foreach (Player enemy in m_owner.Game.GetAllEnemyPlayers(m_owner))
            {
                if (enemy == null) continue;
                m_enemies.Add(enemy);
                enemy.targetId = m_owner.Id;
                enemy.BeginSelfTurn += OnEnemyTurn;
            }
            m_owner.Died += OnOwnerDied;
        }

        private void OnEnemyTurn(Living living)
        {
            if (m_active && living != null) living.targetId = m_owner.Id;
        }

        private void OnOwnerDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_owner.Died -= OnOwnerDied;
            foreach (Player enemy in m_enemies)
            {
                if (enemy == null) continue;
                enemy.BeginSelfTurn -= OnEnemyTurn;
            }
            m_enemies.Clear();
        }
    }

    // ---------- MP next turn ----------
    private sealed class MpNextTurn
    {
        private readonly Player m_owner;
        private readonly int m_value;
        private bool m_active;

        public MpNextTurn(Player owner, int value)
        {
            m_owner = owner;
            m_value = value;
        }

        public void Start()
        {
            m_active = true;
            m_owner.BeginSelfTurn += OnTurn;
            m_owner.Died += OnDied;
        }

        private void OnTurn(Living living)
        {
            if (!m_active) return;
            Stop();
            m_owner.AddPetMP(m_value);
        }

        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_owner.BeginSelfTurn -= OnTurn;
            m_owner.Died -= OnDied;
        }
    }

    // ---------- Mark hidden ----------
    private sealed class MarkEffect
    {
        private readonly Player m_target;
        private readonly bool m_wasHide;
        private int m_turns;
        private bool m_active;

        public MarkEffect(Player target, int turns)
        {
            m_target = target;
            m_wasHide = target.IsHide;
            m_turns = turns;
        }

        public void Start()
        {
            m_active = true;
            m_target.IsHide = false;
            ShowMark(m_target);
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnTurn(Living living)
        {
            if (!m_active) return;
            m_target.IsHide = false;
            ShowMark(m_target);
            if (--m_turns <= 0) Stop();
        }

        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
            m_target.IsHide = m_wasHide;
        }
    }
}
