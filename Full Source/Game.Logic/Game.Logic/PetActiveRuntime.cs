using System;
using System.Collections.Generic;
using System.Linq;
using Game.Logic.Actions;
using Game.Logic.Phy.Object;

namespace Game.Logic;

/// <summary>
/// Phase 1: hiệu ứng phụ cho các kỹ năng PET active 0..1000.
/// Chỉ xử lý các element mà InitPetSkillEffect chưa có class.
/// </summary>
public static class PetActiveRuntime
{
    private static readonly Random Rng = new Random();

    public static void Apply(Player player, int skillId)
    {
        if (player == null) return;
        switch (skillId)
        {
            case 40:
            case 41:
            case 42:
            case 43:
                {
                    int value = skillId <= 41 ? (skillId == 40 ? 100 : 300) : (skillId == 42 ? 100 : 300);
                    new CombatStatBuff(player, Allies(player), 2, skillId <= 41 ? 0 : value, skillId <= 41 ? value : 0).Start();
                }
                break;

            case 91:
            case 92:
                {
                    foreach (Player ally in Allies(player)) ally.AddBlood((int)(ally.MaxBlood * (skillId == 91 ? 5 : 10) / 100.0));
                    foreach (Player enemy in player.Game.GetAllEnemyPlayers(player))
                    {
                        if (enemy == null || !enemy.IsLiving) continue;
                        enemy.RemoveBlood((int)(enemy.MaxBlood * (skillId == 91 ? 5 : 10) / 100.0));
                        if (enemy.Blood <= 0) enemy.Die();
                    }
                }
                break;

            case 130:
            case 131:
                new HpCostDamageBuff(player, 3, skillId == 130 ? 500 : 800, skillId == 130 ? 150 : 300).Start();
                break;

            case 291:
                {
                    Player target = Target(player);
                    if (target != null)
                    {
                        new ControlBuff(target, 3, true, 5).Start();
                    }
                }
                break;

            case 310:
                foreach (Player enemy in player.Game.GetAllEnemyPlayers(player))
                {
                    if (enemy == null) continue;
                    new ControlBuff(enemy, 1, false, 0).Start();
                    enemy.AddPetMP(-20);
                }
                break;

            case 509:
                new OnHitDebuffBuff(player, 20, 2).Start();
                break;

            case 514:
            case 515:
            case 516:
                {
                    Player target = Target(player);
                    if (target != null) new BurnBuff(target, 2, skillId == 514 ? 20 : skillId == 515 ? 25 : 30).Start();
                }
                break;

            case 774:
            case 775:
            case 776:
                {
                    Player target = Target(player);
                    if (target != null) new MarkStackBuff(target, 5).Start();
                }
                break;

            case 781:
            case 782:
                {
                    Player target = Target(player);
                    if (target != null)
                    {
                        int dmg = Math.Max(1, (int)(target.MaxBlood * 15 / 100 + player.BaseDamage));
                        target.RemoveBlood(dmg);
                        if (target.Blood <= 0) target.Die();
                    }
                }
                break;

            case 846:
                {
                    List<Player> allies = Allies(player).OrderBy(x => Rng.Next()).Take(2).ToList();
                    foreach (Player ally in allies) new DamageReductionBuff(ally, 3, 20, 0, 0).Start();
                }
                break;

            case 847:
                new CombatStatBuff(player, new List<Player> { player }, 3, 0, (int)(player.BaseGuard * 80 / 100.0)).Start();
                new CritResistBuff(player, 3, 90).Start();
                break;

            case 850:
                foreach (Player enemy in player.Game.GetAllEnemyPlayers(player))
                {
                    if (enemy == null || !enemy.IsLiving) continue;
                    int dmg = Math.Max(1, (int)(player.BaseDamage * 1.5 + player.BaseGuard * 3));
                    enemy.RemoveBlood(dmg);
                    if (enemy.Blood <= 0) enemy.Die();
                    else new ControlBuff(enemy, 2, true, 0).Start();
                }
                break;

            case 874:
                new DamageReductionBuff(player, 2, 30, 0, 0).Start();
                new CritResistBuff(player, 2, 100).Start();
                break;

            case 611:
            case 612:
            case 613:
                {
                    int pct = skillId == 611 ? 30 : skillId == 612 ? 40 : 50;
                    foreach (Player ally in Allies(player)) new DamageReductionBuff(ally, 2, pct, 0, 0).Start();
                }
                break;

            case 627:
                new DamageReductionBuff(player, 2, 0, 0, 0).Start();
                new OutgoingDamageBuff(player, new List<Player> { player }, 2, 20).Start();
                break;

            case 450:
            case 471:
                {
                    int energy = skillId == 450 ? 120 : 80;
                    if (player.Energy > energy) player.Energy = energy;
                    new OutgoingDamageBuff(player, new List<Player> { player }, 1, 30).Start();
                }
                break;

            case 453:
            case 454:
            case 474:
            case 475:
            case 501:
                // Ảo ảnh tồn tại tối đa 3 turn (tự mất sớm nếu hết máu).
                new ClonePostureBuff(player, 3, skillId).Start();
                break;

            case 1704:
            case 1711:
            case 1718:
                new ClonePostureBuff(player, 3, skillId).Start();
                break;

            case 456:
            case 477:
                new OnHitControlBuff(player, 2).Start();
                break;

            case 457:
            case 458:
            case 478:
            case 479:
                {
                    player.Blood = Math.Max(1, player.Blood * 20 / 100);
                    int ignore = skillId == 457 ? 40 : skillId == 458 ? 60 : skillId == 478 ? 30 : 40;
                    new IgnoreGuardBuff(player, 1, ignore).Start();
                }
                break;

            case 502:
            case 1705:
            case 1712:
            case 1719:
                {
                    player.Blood = Math.Max(1, player.Blood / 2);
                    new IgnoreGuardBuff(player, 1, 65).Start();
                    if (player.IsHide)
                        new OutgoingDamageBuff(player, new List<Player> { player }, 1, 25).Start();
                }
                break;

            case 577:
            case 578:
                {
                    int hp = player.MaxBlood;
                    new StatBuff(player, new List<Player> { player }, 2, 0, 0, 0, 0, hp).Start();
                }
                break;

            case 783:
            case 784:
                {
                    new OutgoingDamageBuff(player, new List<Player> { player }, 2, 20).Start();
                    new StealthBuff(player, 2).Start();
                }
                break;

            case 787:
                foreach (Player enemy in player.Game.GetAllEnemyPlayers(player))
                {
                    if (enemy == null || !enemy.IsLiving) continue;
                    enemy.RemoveBlood(Math.Max(1, (int)(player.BaseDamage * 0.5)));
                    if (enemy.Blood <= 0) enemy.Die();
                    new BurnBuff(enemy, 2, 8).Start();
                }
                break;

            case 835:
            case 836:
                {
                    foreach (Player ally in Allies(player)) new DamageReductionBuff(ally, 2, 20, 0, 0).Start();
                    player.AddBlood(300);
                }
                break;

            case 28:
            case 29:
            case 30:
                {
                    int[] luck = { 100, 300, 500 };
                    new StatBuff(player, new List<Player> { player }, 2, 0, 0, 0, luck[skillId - 28], 0).Start();
                }
                break;

            case 59:
            case 60:
                {
                    int heal = skillId == 59 ? 1500 : 3000;
                    foreach (Player ally in Allies(player)) ally.AddBlood(heal);
                }
                break;

            case 89:
            case 90:
                {
                    int[] stat = { 100, 300 };
                    int[] hp = { 1000, 2000 };
                    int idx = skillId - 89;
                    new StatBuff(player, Allies(player), 3, stat[idx], stat[idx], stat[idx], stat[idx], hp[idx]).Start();
                }
                break;

            case 150:
            case 151:
                new CritBuff(player, 3, skillId == 150 ? 20 : 50).Start();
                break;

            case 210:
            case 211:
            case 218:
            case 219:
                {
                    Player target = Target(player);
                    if (target == null) return;
                    int amount = skillId == 210 ? 1 : skillId == 211 ? 2 : skillId == 218 ? 1 : 2;
                    target.AddPetMP(-amount);
                    if (skillId == 218 || skillId == 219) player.AddPetMP(amount);
                }
                break;

            case 289:
                {
                    Player target = Target(player);
                    if (target == null) return;
                    new EnemyStatDebuff(target, 3, 20).Start();
                }
                break;

            case 363:
                new CritBuff(player, 2, 20).Start();
                break;

            case 323:
                new LifestealBuff(player, 2, 35).Start();
                break;

            case 375:
                foreach (Player enemy in player.Game.GetAllEnemyPlayers(player))
                {
                    if (enemy == null || !enemy.IsLiving) continue;
                    int dmg = Math.Max(1, (int)(player.BaseDamage * 0.6 * 5));
                    enemy.RemoveBlood(dmg);
                    if (enemy.Blood <= 0) enemy.Die();
                }
                break;

            case 465:
            case 466:
            case 467:
            case 519:
            case 520:
                {
                    Player target = Target(player);
                    if (target != null)
                        new OutgoingDamageBuff(player, new List<Player> { target }, 2, skillId == 519 ? -20 : skillId == 520 ? -30 : -30).Start();
                }
                break;

            case 886:
                if (!(player.Game is PVPGame)) return;
                {
                    Player target = Target(player);
                    if (target != null)
                    {
                        int reduce = Math.Max(1, target.MaxBlood * 12 / 100);
                        target.MaxBlood = Math.Max(1, target.MaxBlood - reduce);
                        if (target.Blood > target.MaxBlood) target.Blood = target.MaxBlood;
                    }
                }
                break;

            case 529:
                new OutgoingDamageBuff(player, Allies(player), 3, 10).Start();
                break;

            case 864:
            case 869:
                new DamageReductionBuff(player, 1, 25, 500, 15).Start();
                break;

            case 865:
            case 870:
                new ReflectDamageBuff(player, 3, 50).Start();
                break;

            case 889:
                new HealOverTimeBuff(player, 3, 8).Start();
                break;
        }
    }

    private static List<Player> Allies(Player player)
    {
        try
        {
            return player.Game.GetAllTeamPlayers(player)
                .Where(x => x != null && x.IsLiving)
                .ToList();
        }
        catch { return new List<Player>(); }
    }

    private static Player Target(Player player)
    {
        try
        {
            if (player.targetId > 0 && player.Game.Players.TryGetValue(player.targetId, out Player target)
                && target != null && target != player && target.Team != player.Team)
                return target;
            return player.Game.GetAllEnemyPlayers(player).FirstOrDefault();
        }
        catch { return null; }
    }

    /// <summary>
    /// Tìm điểm đứng hợp lệ cho teleport Phân Thân, bám sát quỹ đạo 20–80% giữa hai người.
    /// Với mỗi x trên đoạn thẳng: ưu tiên điểm nội suy nếu nó đứng được và sát mặt đất;
    /// nếu điểm đó nằm trong lòng đất thì hạ xuống mặt đất bên dưới hoặc trồi lên mặt đất
    /// phía trên (giới hạn 200px) để không phóng lên nền đất cao hơn hẳn.
    /// </summary>
    private static System.Drawing.Point FindStandablePoint(Player owner, Player target, int t)
    {
        BaseGame game = owner.Game;
        Game.Logic.Phy.Maps.Map map = game.Map;

        for (int cur = t; cur >= 20; cur -= 5)
        {
            int x = owner.X + (target.X - owner.X) * cur / 100;
            int yLine = owner.Y + (target.Y - owner.Y) * cur / 100;

            System.Drawing.Point p = TryStandablePoint(game, x, yLine);
            if (p != System.Drawing.Point.Empty)
            {
                return p;
            }

            // lân cận x (tránh vách đá dựng đứng)
            for (int dx = 8; dx <= 64; dx += 8)
            {
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    int nx = x + dx * sign;
                    if (nx < 0 || nx > map.Bound.Width)
                    {
                        continue;
                    }
                    System.Drawing.Point p2 = TryStandablePoint(game, nx, yLine);
                    if (p2 != System.Drawing.Point.Empty)
                    {
                        return p2;
                    }
                }
            }
        }
        return new System.Drawing.Point(owner.X, owner.Y);
    }

    /// <summary>Điểm đứng được gần (x,y): ưu tiên (x,y), rồi mặt đất bên dưới, rồi trồi lên trên.</summary>
    private static System.Drawing.Point TryStandablePoint(BaseGame game, int x, int y)
    {
        // 1) (x,y) đứng được và sát mặt đất (không bị rơi sau teleport)
        System.Drawing.Point ground = game.FindGroundBelow(x, y, 150);
        if (ground != System.Drawing.Point.Empty && ground.Y - y >= 0 && ground.Y - y <= 24
            && game.IsStandablePoint(x, y))
        {
            return new System.Drawing.Point(x, y);
        }
        // 2) đứng ngay trên mặt đất bên dưới (x,y)
        if (ground != System.Drawing.Point.Empty)
        {
            for (int dy = 1; dy <= 16; dy += 3)
            {
                if (game.IsStandablePoint(x, ground.Y - dy))
                {
                    return new System.Drawing.Point(x, ground.Y - dy);
                }
            }
        }
        // 3) (x,y) nằm trong lòng đất -> trồi lên mặt đất phía trên (tối đa 200px)
        if (game.IsStandablePoint(x, y))
        {
            return new System.Drawing.Point(x, y);
        }
        for (int dy = 8; dy <= 200; dy += 8)
        {
            if (game.IsStandablePoint(x, y - dy))
            {
                return new System.Drawing.Point(x, y - dy);
            }
        }
        return System.Drawing.Point.Empty;
    }

    private sealed class IgnoreGuardBuff
    {
        private readonly Player m_target;
        private readonly int m_turns;
        private readonly int m_oldIgnoreGuard;
        private readonly int m_addPercent;
        private int m_tick;
        private bool m_active;

        public IgnoreGuardBuff(Player target, int turns, int addPercent)
        {
            m_target = target;
            m_turns = turns;
            m_oldIgnoreGuard = target.IgnoreGuard;
            m_addPercent = addPercent;
        }

        public void Start()
        {
            m_active = true;
            m_target.IgnoreGuard = Math.Max(m_target.IgnoreGuard, m_addPercent);
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnTurn(Living living) { if (++m_tick >= m_turns) Stop(); }
        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.IgnoreGuard = m_oldIgnoreGuard;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }

    /// <summary>
    /// Tư Thế Phân Thân.
    /// - Dịch chuyển nhân vật thật về phía địch (SetXY + SendPlayerMove).
    /// - Tạo một ẢO ẢNH thật trên server (<see cref="ShadowPostureLiving"/>) tại vị trí cũ:
    ///   ảo ảnh có HP riêng, bị đạn/nổ trúng thì trừ máu như bình thường; hết số turn
    ///   thì tự mất. Nhân vật thật vẫn nhận damage bình thường nếu bị bắn trúng
    ///   (địch bắn hên xui: trúng ảo ảnh thì chỉ ảo ảnh mất máu).
    /// - Client vẽ ảo ảnh bằng ADD_PLAYER_SHADOWN (157); server gửi lại packet này ở
    ///   mỗi lần chuyển turn (kèm 1 lần delayed sau packet chuyển turn) để hình không
    ///   bị mất khi client xử lý changePlayer.
    /// </summary>
    private sealed class ClonePostureBuff
    {
        /// <summary>Độ trễ (ms) của lần gửi lại hình shadow sau packet chuyển turn.</summary>
        private const int RefreshDelayMs = 450;

        private readonly Player m_owner;
        private readonly int m_turns;
        private readonly int m_skillId;
        private int m_tick;
        private bool m_active;

        private int m_cloneId;
        private int m_oldX;
        private int m_oldY;
        private int m_sendCount;
        private int m_lastImmediateTurn = -1;
        private int m_lastDelayedTurn = -1;
        private int m_lastGroundTurn = -1;
        private ShadowPostureLiving m_shadow;
        private readonly List<Player> m_visualListeners = new List<Player>();

        public ClonePostureBuff(Player owner, int turns, int skillId)
        {
            m_owner = owner;
            m_turns = turns;
            m_skillId = skillId;
        }

        public void Start()
        {
            m_active = true;
            m_oldX = m_owner.X;
            m_oldY = m_owner.Y;
            int oldX = m_oldX;
            int oldY = m_oldY;
            Player target = Target(m_owner);
            if (target != null && target.IsLiving)
            {
                // Vị trí mới: ngẫu nhiên giữa bản thân và kẻ địch, nhưng phải là chỗ
                // ĐỨNG ĐƯỢC trên địa hình (tránh teleport vào trong đất/đá).
                int t = 20 + Rng.Next(61);
                int wantX = m_owner.X + (target.X - m_owner.X) * t / 100;
                int wantY = m_owner.Y + (target.Y - m_owner.Y) * t / 100;
                System.Drawing.Point dest = FindStandablePoint(m_owner, target, t);
                PetCloneDiag.Write(string.Format(
                    "[TELEPORT] owner={0} t={1}% from=({2},{3}) want=({4},{5}) got=({6},{7})",
                    PetCloneDiag.Name(m_owner), t, m_owner.X, m_owner.Y, wantX, wantY, dest.X, dest.Y));
                if (dest.X != m_owner.X || dest.Y != m_owner.Y)
                {
                    m_owner.SetXY(dest.X, dest.Y);
                    // Teleport tức thì: packet 91|72 khiến client đặt thẳng vị trí mới,
                    // không diễn hoạt cảnh đi bộ từ vị trí cũ tới vị trí mới.
                    m_owner.Game.SendLivingBoltmove(m_owner, dest.X, dest.Y);
                    // Nếu toạ độ mới nằm trên không thì để nhân vật rơi xuống đất.
                    m_owner.StartMoving();
                }
            }

            m_cloneId = m_owner.Id + 100000;

            PetCloneDiag.Write(string.Format(
                "[START] owner={0} skill={1} turns={2} gameTurn={3} old=({4},{5}) new=({6},{7}) clone={8} hp={9}/{10}",
                PetCloneDiag.Name(m_owner), m_skillId, m_turns, m_owner.Game.TurnIndex,
                oldX, oldY, m_owner.X, m_owner.Y, m_cloneId, m_owner.Blood, m_owner.MaxBlood));

            CreateShadow(oldX, oldY);

            m_lastImmediateTurn = m_owner.Game.TurnIndex;
            m_lastDelayedTurn = m_owner.Game.TurnIndex;

            m_owner.BeginSelfTurn += OnOwnerTurn;
            m_owner.Died += OnOwnerDied;

            // Client có thể mất hình shadow khi chuyển turn; gửi lại ở đầu turn của
            // tất cả người chơi để ảo ảnh tồn tại qua turn địch.
            try
            {
                foreach (Player p in m_owner.Game.Players.Values)
                {
                    if (p == null) continue;
                    p.BeginSelfTurn += OnAnyPlayerTurn;
                    p.BeginNextTurn += OnAnyPlayerTurn;
                    m_visualListeners.Add(p);
                }
            }
            catch (Exception ex)
            {
                PetCloneDiag.Write("[SUBSCRIBE-ERROR] " + ex.Message);
            }
        }

        /// <summary>
        /// Tạo ảo ảnh thật trên server tại vị trí cũ, copy chỉ số phòng thủ của chủ thể
        /// để sát thương tính lên ảo ảnh giống như tính lên người thật.
        /// </summary>
        private void CreateShadow(int x, int y)
        {
            try
            {
                ShadowPostureLiving shadow = new ShadowPostureLiving(
                    m_cloneId, m_owner.Game, m_owner.Team, m_owner.Name, m_owner.ModelId,
                    m_owner.MaxBlood, 0, m_owner.Direction);

                shadow.BaseGuard = m_owner.BaseGuard;
                shadow.Defence = m_owner.Defence;
                shadow.Agility = m_owner.Agility;
                shadow.Lucky = m_owner.Lucky;
                shadow.Grade = m_owner.Grade;
                shadow.MagicDefence = m_owner.MagicDefence;
                shadow.FireDefence = m_owner.FireDefence;
                shadow.WaterDefence = m_owner.WaterDefence;
                shadow.WindDefence = m_owner.WindDefence;
                shadow.LandDefence = m_owner.LandDefence;
                shadow.LighDefence = m_owner.LighDefence;
                shadow.DrakDefence = m_owner.DrakDefence;

                shadow.SetXY(x, y);
                shadow.Blood = m_owner.Blood;
                shadow.Removed = OnShadowRemoved;

                m_owner.Game.AddShadowLiving(shadow);
                m_shadow = shadow;

                PetCloneDiag.Write(string.Format(
                    "[SHADOW-CREATE] owner={0} shadow={1} pos=({2},{3}) hp={4}/{5} turn={6}",
                    PetCloneDiag.Name(m_owner), shadow.Id, x, y, shadow.Blood, shadow.MaxBlood,
                    m_owner.Game.TurnIndex));
            }
            catch (Exception ex)
            {
                PetCloneDiag.Write("[SHADOW-CREATE-ERROR] " + ex);
            }

            SendShadow("start");
        }

        /// <summary>Gửi (lại) packet ADD_PLAYER_SHADOWN cho client vẽ ảo ảnh.</summary>
        private void SendShadow(string reason)
        {
            try
            {
                m_sendCount++;
                // Dùng vị trí HIỆN TẠI của ảo ảnh (có thể đã rơi xuống khi đất bị đào),
                // không dùng vị trí ban đầu nữa.
                int x = m_oldX;
                int y = m_oldY;
                if (m_shadow != null && !m_shadow.IsRemoved)
                {
                    x = m_shadow.X;
                    y = m_shadow.Y;
                }
                PetCloneDiag.Write(string.Format(
                    "[SEND] n={0} reason={1} owner={2} clone={3} pos=({4},{5}) gameTurn={6} current={7} tick={8}/{9}",
                    m_sendCount, reason, PetCloneDiag.Name(m_owner), m_cloneId, x, y,
                    m_owner.Game.TurnIndex, PetCloneDiag.Name(m_owner.Game.CurrentLiving), m_tick, m_turns));
                m_owner.Game.SendShadowVisual(m_owner, x, y, m_cloneId);
                if (m_shadow != null && !m_shadow.IsRemoved)
                {
                    // Client vừa tạo lại hình ảo ảnh với HP của chủ thể
                    // -> đồng bộ lại đúng HP thật của ảo ảnh (type 3 = set im lặng).
                    m_owner.Game.UpdateBlood(m_shadow, 3, 0);
                }
            }
            catch (Exception ex)
            {
                PetCloneDiag.Write("[SEND-ERROR] " + ex.Message);
            }
        }

        /// <summary>
        /// Gửi lại hình sau khi client đã xử lý xong packet chuyển turn.
        /// Packet BeginSelfTurn/BeginNextTurn được bắn ra TRƯỚC SendGameNextTurn
        /// nên có thể bị màn hình chuyển turn ghi đè.
        /// </summary>
        private void ScheduleRefresh(Living living)
        {
            try
            {
                m_owner.Game.AddAction(new CallFunctionAction(delegate
                {
                    if (!m_active) return;
                    SendShadow("delayed:" + PetCloneDiag.Name(living));
                }, RefreshDelayMs));
            }
            catch (Exception ex)
            {
                PetCloneDiag.Write("[DELAYED-ERROR] " + ex.Message);
            }
        }

        private void OnAnyPlayerTurn(Living living)
        {
            if (!m_active) return;
            if (m_shadow == null || m_shadow.IsRemoved)
            {
                Stop("shadow-gone");
                return;
            }
            int turn = m_owner.Game.TurnIndex;
            // Chỉ 1 lần gửi/lần chuyển turn (event có thể bắn nhiều lần cho
            // nhiều living: BeginNextTurn của tất cả + BeginSelfTurn của người
            // đang tới lượt).
            if (m_lastImmediateTurn != turn)
            {
                m_lastImmediateTurn = turn;
                SendShadow("turn:" + PetCloneDiag.Name(living));
            }
            if (m_lastDelayedTurn != turn)
            {
                m_lastDelayedTurn = turn;
                ScheduleRefresh(living);
            }
            // Địa hình có thể bị đào mất trong turn vừa rồi -> cho ảo ảnh rơi theo đất.
            // Chỉ kiểm tra 1 lần/turn (event chuyển turn bắn nhiều lần cho nhiều living),
            // nếu không sẽ xếp chồng nhiều lệnh rơi và ảo ảnh rơi quá nhanh.
            if (m_shadow != null && m_lastGroundTurn != turn)
            {
                m_lastGroundTurn = turn;
                m_shadow.CheckGround();
            }
        }

        private void OnOwnerTurn(Living living)
        {
            if (!m_active) return;
            m_tick++;
            PetCloneDiag.Write(string.Format(
                "[OWN-TURN] owner={0} tick={1}/{2} gameTurn={3}",
                PetCloneDiag.Name(m_owner), m_tick, m_turns, m_owner.Game.TurnIndex));
            if (m_tick >= m_turns) Stop("turn-end");
        }

        private void OnOwnerDied(Living living) { Stop("owner-died"); }

        private void OnShadowRemoved(ShadowPostureLiving shadow) { Stop("shadow-removed"); }

        private void Stop(string reason)
        {
            if (!m_active) return;
            m_active = false;
            m_owner.BeginSelfTurn -= OnOwnerTurn;
            m_owner.Died -= OnOwnerDied;
            foreach (Player p in m_visualListeners)
            {
                if (p == null) continue;
                p.BeginSelfTurn -= OnAnyPlayerTurn;
                p.BeginNextTurn -= OnAnyPlayerTurn;
            }
            m_visualListeners.Clear();
            PetCloneDiag.Write(string.Format(
                "[STOP] reason={0} owner={1} clone={2} gameTurn={3} sends={4} tick={5}/{6}",
                reason, PetCloneDiag.Name(m_owner), m_cloneId, m_owner.Game.TurnIndex, m_sendCount, m_tick, m_turns));
            if (m_shadow != null)
            {
                m_shadow.Removed = null;
                m_shadow.Remove(reason);
            }
            else
            {
                try { m_owner.Game.SendRemoveShadowVisual(m_cloneId); } catch (Exception ex) { PetCloneDiag.Write("[REMOVE-ERROR] " + ex.Message); }
            }
        }
    }

    private sealed class StealthBuff
    {
        private readonly Player m_target;
        private readonly int m_turns;
        private readonly bool m_wasHide;
        private int m_tick;
        private bool m_active;

        public StealthBuff(Player target, int turns)
        {
            m_target = target;
            m_turns = turns;
            m_wasHide = target.IsHide;
        }

        public void Start()
        {
            m_active = true;
            m_target.IsHide = true;
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnTurn(Living living) { if (++m_tick >= m_turns) Stop(); }
        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.IsHide = m_wasHide;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }

    private sealed class OnHitControlBuff
    {
        private readonly Player m_owner;
        private readonly int m_turns;
        private int m_tick;
        private bool m_active;

        public OnHitControlBuff(Player owner, int turns)
        {
            m_owner = owner;
            m_turns = turns;
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
            if (!m_active || source != m_owner || !(target is Player p)) return;
            new ControlBuff(p, 1, true, 0).Start();
        }

        private void OnTurn(Living living) { if (++m_tick >= m_turns) Stop(); }
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

    private sealed class CombatStatBuff
    {
        private readonly List<Player> m_targets;
        private readonly int m_turns;
        private readonly int m_baseDamage;
        private readonly int m_baseGuard;
        private int m_tick;
        private bool m_active;

        public CombatStatBuff(Player owner, List<Player> targets, int turns, int baseDamage, int baseGuard)
        {
            m_targets = targets;
            m_turns = turns;
            m_baseDamage = baseDamage;
            m_baseGuard = baseGuard;
        }

        public void Start()
        {
            m_active = true;
            foreach (Player p in m_targets)
            {
                if (p == null) continue;
                p.BaseDamage += m_baseDamage;
                p.BaseGuard += m_baseGuard;
                p.BeginSelfTurn += OnTurn;
            }
        }

        private void OnTurn(Living living) { if (++m_tick > m_turns) Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            foreach (Player p in m_targets)
            {
                if (p == null) continue;
                p.BaseDamage -= m_baseDamage;
                p.BaseGuard -= m_baseGuard;
                p.BeginSelfTurn -= OnTurn;
            }
        }
    }

    private sealed class HpCostDamageBuff
    {
        private readonly Player m_target;
        private readonly int m_turns;
        private readonly int m_hpCost;
        private readonly int m_damageAdd;
        private int m_tick;
        private bool m_active;

        public HpCostDamageBuff(Player target, int turns, int hpCost, int damageAdd)
        {
            m_target = target;
            m_turns = turns;
            m_hpCost = hpCost;
            m_damageAdd = damageAdd;
        }

        public void Start()
        {
            m_active = true;
            m_target.BaseDamage += m_damageAdd;
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnTurn(Living living)
        {
            if (!m_active) return;
            m_target.AddBlood(-m_hpCost, 1);
            if (m_target.Blood <= 0) m_target.Die();
            if (++m_tick >= m_turns) Stop();
        }

        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.BaseDamage -= m_damageAdd;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }

    private sealed class ControlBuff
    {
        private readonly Player m_target;
        private readonly int m_turns;
        private readonly bool m_move;
        private readonly int m_hpPercent;
        private int m_tick;
        private bool m_active;

        public ControlBuff(Player target, int turns, bool move, int hpPercent)
        {
            m_target = target;
            m_turns = turns;
            m_move = move;
            m_hpPercent = hpPercent;
        }

        public void Start()
        {
            m_active = true;
            if (m_move) m_target.LockMove = true;
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnTurn(Living living)
        {
            if (!m_active) return;
            if (m_hpPercent > 0)
            {
                m_target.AddBlood(-(m_target.MaxBlood * m_hpPercent / 100), 1);
                if (m_target.Blood <= 0) m_target.Die();
            }
            if (++m_tick >= m_turns) Stop();
        }

        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            if (m_move) m_target.LockMove = false;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }

    private sealed class OnHitDebuffBuff
    {
        private readonly Player m_owner;
        private readonly int m_percent;
        private readonly int m_turns;
        private int m_tick;
        private bool m_active;

        public OnHitDebuffBuff(Player owner, int percent, int turns)
        {
            m_owner = owner;
            m_percent = percent;
            m_turns = turns;
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
            if (!m_active || source != m_owner || !(target is Player p)) return;
            new TakenDamageDebuff(p, m_turns, m_percent).Start();
        }

        private void OnTurn(Living living) { if (++m_tick > m_turns) Stop(); }
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

    private sealed class TakenDamageDebuff
    {
        private readonly Player m_target;
        private readonly int m_turns;
        private readonly int m_percent;
        private int m_tick;
        private bool m_active;

        public TakenDamageDebuff(Player target, int turns, int percent)
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

        private void OnTurn(Living living) { if (++m_tick >= m_turns) Stop(); }
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

    private sealed class BurnBuff
    {
        private readonly Player m_target;
        private readonly int m_turns;
        private readonly int m_percent;
        private int m_tick;
        private bool m_active;

        public BurnBuff(Player target, int turns, int percent)
        {
            m_target = target;
            m_turns = turns;
            m_percent = percent;
        }

        public void Start()
        {
            m_active = true;
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnTurn(Living living)
        {
            if (!m_active) return;
            m_target.AddBlood(-(m_target.MaxBlood * m_percent / 100), 1);
            if (m_target.Blood <= 0) m_target.Die();
            if (++m_tick >= m_turns) Stop();
        }

        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }

    private sealed class MarkStackBuff
    {
        private readonly Player m_target;
        private readonly int m_turns;
        private int m_stacks;
        private int m_tick;
        private bool m_active;

        public MarkStackBuff(Player target, int turns)
        {
            m_target = target;
            m_turns = turns;
        }

        public void Start()
        {
            m_active = true;
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnTurn(Living living)
        {
            if (!m_active) return;
            m_tick++;
            if (m_tick > m_turns) Stop();
        }

        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }

    private sealed class CritResistBuff
    {
        private readonly Player m_target;
        private readonly int m_turns;
        private readonly int m_percent;
        private int m_tick;
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

        private void OnTurn(Living living) { if (++m_tick >= m_turns) Stop(); }
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

    private sealed class StatBuff
    {
        private readonly List<Player> m_targets;
        private readonly int m_turns;
        private readonly int m_attack, m_defence, m_agility, m_luck, m_hp;
        private bool m_active;

        public StatBuff(Player owner, List<Player> targets, int turns, int attack, int defence, int agility, int luck, int hp)
        {
            m_targets = targets;
            m_turns = turns;
            m_attack = attack;
            m_defence = defence;
            m_agility = agility;
            m_luck = luck;
            m_hp = hp;
        }

        public void Start()
        {
            m_active = true;
            foreach (Player p in m_targets)
            {
                if (p == null) continue;
                p.Attack += m_attack;
                p.Defence += m_defence;
                p.Agility += m_agility;
                p.Lucky += m_luck;
                if (m_hp > 0)
                {
                    p.MaxBlood += m_hp;
                    p.Blood += m_hp;
                }
                p.BeginSelfTurn += OnTurn;
            }
        }

        private int m_tick;
        private void OnTurn(Living living)
        {
            if (!m_active) return;
            m_tick++;
            if (m_tick > m_turns) Stop();
        }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            foreach (Player p in m_targets)
            {
                if (p == null) continue;
                p.Attack -= m_attack;
                p.Defence -= m_defence;
                p.Agility -= m_agility;
                p.Lucky -= m_luck;
                if (m_hp > 0)
                {
                    p.MaxBlood -= m_hp;
                    p.Blood -= m_hp;
                }
                p.BeginSelfTurn -= OnTurn;
            }
        }
    }

    private sealed class CritBuff
    {
        private readonly Player m_target;
        private readonly int m_turns;
        private readonly int m_percent;
        private int m_tick;
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

        private void OnTurn(Living living) { if (++m_tick > m_turns) Stop(); }
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

    private sealed class OutgoingDamageBuff
    {
        private readonly List<Player> m_targets;
        private readonly int m_turns;
        private readonly int m_percent;
        private int m_tick;
        private bool m_active;

        public OutgoingDamageBuff(Player owner, List<Player> targets, int turns, int percent)
        {
            m_targets = targets;
            m_turns = turns;
            m_percent = percent;
        }

        public void Start()
        {
            m_active = true;
            foreach (Player p in m_targets)
            {
                if (p == null) continue;
                p.OnMakeDamageEvent += OnMakeDamage;
                p.BeginSelfTurn += OnTurn;
            }
        }

        private void OnMakeDamage(Living target, Living source, ref int damageAmount, ref int criticalAmount)
        {
            if (!m_active) return;
            damageAmount = (int)Math.Ceiling(damageAmount * (100 + m_percent) / 100.0);
        }

        private void OnTurn(Living living) { if (++m_tick > m_turns) Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            foreach (Player p in m_targets)
            {
                if (p == null) continue;
                p.OnMakeDamageEvent -= OnMakeDamage;
                p.BeginSelfTurn -= OnTurn;
            }
        }
    }

    private sealed class EnemyStatDebuff
    {
        private readonly Player m_target;
        private readonly int m_turns;
        private readonly int m_percent;
        private readonly int m_attack, m_defence;
        private int m_tick;
        private bool m_active;

        public EnemyStatDebuff(Player target, int turns, int percent)
        {
            m_target = target;
            m_turns = turns;
            m_percent = percent;
            m_attack = Math.Max(1, (int)(target.Attack * percent / 100));
            m_defence = Math.Max(1, (int)(target.Defence * percent / 100));
        }

        public void Start()
        {
            m_active = true;
            m_target.Attack -= m_attack;
            m_target.Defence -= m_defence;
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnTurn(Living living) { if (++m_tick > m_turns) Stop(); }
        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.Attack += m_attack;
            m_target.Defence += m_defence;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }

    private sealed class DamageReductionBuff
    {
        private readonly Player m_target;
        private readonly int m_turns;
        private readonly int m_percent;
        private readonly int m_flat;
        private readonly int m_stackPercent;
        private int m_tick;
        private bool m_active;

        public DamageReductionBuff(Player target, int turns, int percent, int flat, int stackPercent)
        {
            m_target = target;
            m_turns = turns;
            m_percent = percent;
            m_flat = flat;
            m_stackPercent = stackPercent;
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
            damageAmount = Math.Max(1, (int)Math.Ceiling(damageAmount * (100 - m_percent) / 100.0) - m_flat);
        }

        private void OnTurn(Living living) { if (++m_tick > m_turns) Stop(); }
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

    private sealed class ReflectDamageBuff
    {
        private readonly Player m_target;
        private readonly int m_turns;
        private readonly int m_percent;
        private int m_tick;
        private bool m_active;

        public ReflectDamageBuff(Player target, int turns, int percent)
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
            if (!m_active || source == null || source == m_target) return;
            int reflect = Math.Max(1, damageAmount * m_percent / 100);
            source.AddBlood(-reflect, 1);
            if (source.Blood <= 0) source.Die();
        }

        private void OnTurn(Living living) { if (++m_tick > m_turns) Stop(); }
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

    private sealed class LifestealBuff
    {
        private readonly Player m_target;
        private readonly int m_turns;
        private readonly int m_percent;
        private int m_tick;
        private bool m_active;

        public LifestealBuff(Player target, int turns, int percent)
        {
            m_target = target;
            m_turns = turns;
            m_percent = percent;
        }

        public void Start()
        {
            m_active = true;
            m_target.OnMakeDamageEvent += OnMakeDamage;
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnMakeDamage(Living target, Living source, ref int damageAmount, ref int criticalAmount)
        {
            if (!m_active || source != m_target) return;
            int heal = Math.Max(1, damageAmount * m_percent / 100);
            m_target.AddBlood(heal);
        }

        private void OnTurn(Living living) { if (++m_tick > m_turns) Stop(); }
        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.OnMakeDamageEvent -= OnMakeDamage;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }

    private sealed class HealOverTimeBuff
    {
        private readonly Player m_target;
        private readonly int m_turns;
        private readonly int m_percent;
        private int m_tick;
        private bool m_active;

        public HealOverTimeBuff(Player target, int turns, int percent)
        {
            m_target = target;
            m_turns = turns;
            m_percent = percent;
        }

        public void Start()
        {
            m_active = true;
            m_target.BeginSelfTurn += OnTurn;
            m_target.Died += OnDied;
        }

        private void OnTurn(Living living)
        {
            if (!m_active) return;
            int heal = Math.Max(1, m_target.Blood * m_percent / 100);
            m_target.AddBlood(heal);
            if (++m_tick >= m_turns) Stop();
        }

        private void OnDied(Living living) { Stop(); }

        private void Stop()
        {
            if (!m_active) return;
            m_active = false;
            m_target.BeginSelfTurn -= OnTurn;
            m_target.Died -= OnDied;
        }
    }
}
