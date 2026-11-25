using System;
using System.Collections.Generic;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;
using SqlDataProvider.Data;

namespace Game.Server.GameServerScript.AI.Messions
{
    /// <summary>
    /// Mission 1 ai cua pho ban su kien. Mission_Info.Param1 = pho ban (1..5), Param2 = ai (1..4).
    /// NPC family: base + (ai-1)*100 + slot (vi du Kho Bau Nguoi Ca: ai1 2002001.., ai2 2002101..).
    /// Do kho (HandLevel) chi tang chi so quai/boss, khong doi ai.
    /// </summary>
    public class EventDungeonStageMission : AMissionControl
    {
        private int Dungeon = 1;
        private int Stage = 1;
        private int BaseId;
        private int MapId;
        private int[] BossSlots = new int[0];
        private int[] MinionSlots = new int[0];
        private float StatScale = 1f;

        private readonly List<SimpleNpc> Minions = new List<SimpleNpc>();
        private readonly List<SimpleBoss> Bosses = new List<SimpleBoss>();
        private readonly List<SimpleNpc> Chests = new List<SimpleNpc>();
        private SimpleNpc Statue;
        private SimpleNpc Mast;
        private bool Unsealed;
        private bool Lost;

        private int NpcId(int slot)
        {
            return BaseId + slot;
        }

        private void LoadConfig()
        {
            MissionInfo info = base.Game.MissionInfo;
            Dungeon = (info != null && info.Param1 > 0) ? info.Param1 : 1;
            Stage = (info != null && info.Param2 >= 1) ? info.Param2 : 1;
            int tier = (int)base.Game.HandLevel;
            if (tier < 0) tier = 0;
            if (tier > 3) tier = 3;
            StatScale = 1f + 0.25f * tier;
            switch (Dungeon)
            {
                case 1:
                    BaseId = 2002000 + (Stage - 1) * 100;
                    MapId = 1503 + Stage;
                    if (Stage == 1) { BossSlots = new int[1] { 4 }; MinionSlots = new int[2] { 1, 2 }; }
                    else if (Stage == 2) { BossSlots = new int[1] { 6 }; MinionSlots = new int[3] { 5, 5, 7 }; }
                    else if (Stage == 3) { BossSlots = new int[4] { 8, 8, 10, 10 }; MinionSlots = new int[0]; }
                    else { BossSlots = new int[0]; MinionSlots = new int[0]; }
                    break;
                case 2:
                    BaseId = 2001000 + (Stage - 1) * 100;
                    MapId = Stage == 2 ? 1500 : (Stage == 3 ? 1502 : (Stage == 4 ? 1503 : 1499));
                    BossSlots = new int[1] { 4 };
                    MinionSlots = new int[2] { 1, 2 };
                    break;
                case 3:
                    BaseId = 2003000 + (Stage - 1) * 100;
                    MapId = 40050;
                    BossSlots = new int[1] { Stage == 2 ? 12 : (Stage == 3 ? 22 : (Stage == 4 ? 32 : 4)) };
                    MinionSlots = new int[2] { 1, 2 };
                    break;
                case 4:
                    BaseId = 2004000 + (Stage - 1) * 100;
                    MapId = 40051;
                    BossSlots = new int[1] { Stage == 2 ? 21 : (Stage == 3 ? 31 : (Stage == 4 ? 32 : 1)) };
                    MinionSlots = new int[2] { 2, 22 };
                    break;
                default:
                    BaseId = 2005000 + (Stage - 1) * 100;
                    MapId = 40052 + Stage;
                    BossSlots = new int[1] { 1 };
                    MinionSlots = new int[2] { 2, 11 };
                    break;
            }
        }

        public override void OnPrepareNewSession()
        {
            base.OnPrepareNewSession();
            LoadConfig();
            List<int> ids = new List<int>();
            foreach (int slot in MinionSlots) ids.Add(NpcId(slot));
            foreach (int slot in BossSlots) ids.Add(NpcId(slot));
            if (Dungeon == 1 && Stage == 4)
            {
                ids.Add(NpcId(11));
                ids.Add(NpcId(12));
                ids.Add(NpcId(13));
            }
            base.Game.LoadResources(ids.ToArray());
            List<int> bossIds = new List<int>();
            foreach (int slot in BossSlots) bossIds.Add(NpcId(slot));
            if (Dungeon == 1 && Stage == 4) bossIds.Add(NpcId(12));
            base.Game.LoadNpcGameOverResources(bossIds.ToArray());
            base.Game.SetMap(MapId);
        }

        private void ApplyScale(Living living)
        {
            if (living == null) return;
            if (StatScale > 1f)
            {
                living.Blood = (int)(living.Blood * StatScale);
            }
        }

        private SimpleBoss CreateBossAt(int npcId, int x, int y)
        {
            SimpleBoss boss = base.Game.CreateBoss(npcId, x, y, -1, 1, "");
            if (boss != null)
            {
                boss.SetRelateDemagemRect(-40, -170, 80, 170);
                ApplyScale(boss);
                Bosses.Add(boss);
            }
            return boss;
        }

        private SimpleNpc CreateNpcAt(int npcId, int x, int y)
        {
            SimpleNpc npc = base.Game.CreateNpc(npcId, x, y, 1, -1);
            if (npc != null)
            {
                ApplyScale(npc);
                Minions.Add(npc);
            }
            return npc;
        }

        public override void OnStartGame()
        {
            base.OnStartGame();
            base.Game.IsBossWar = NpcId(BossSlots.Length > 0 ? BossSlots[0] : 12).ToString();
            int width = base.Game.Map.Info.ForegroundWidth;
            int height = base.Game.Map.Info.ForegroundHeight;
            int groundY = (int)(height * 0.52);
            int x = width - 300;
            foreach (int slot in BossSlots)
            {
                CreateBossAt(NpcId(slot), x, groundY);
                x -= 140;
            }
            int i = 0;
            foreach (int slot in MinionSlots)
            {
                if (Dungeon == 1 && Stage == 2 && slot == 7)
                {
                    Mast = CreateNpcAt(NpcId(slot), x, groundY);
                }
                else
                {
                    CreateNpcAt(NpcId(slot), x, groundY);
                }
                x -= 85;
                i++;
                if (i >= 5) break;
            }
            if (Dungeon == 1 && Stage == 4)
            {
                Statue = CreateNpcAt(NpcId(11), width - 260, groundY);
                int chestX = width - 520;
                for (int c = 0; c < 4; c++)
                {
                    SimpleNpc chest = base.Game.CreateNpc(NpcId(13), chestX, groundY, 1, -1);
                    if (chest != null)
                    {
                        ApplyScale(chest);
                        Chests.Add(chest);
                    }
                    chestX -= 90;
                }
            }
        }

        private bool AllBossesDead()
        {
            if (Bosses.Count == 0) return false;
            foreach (SimpleBoss boss in Bosses)
            {
                if (boss.IsLiving) return false;
            }
            return true;
        }

        private bool AllChestsDead()
        {
            if (Chests.Count == 0) return false;
            foreach (SimpleNpc chest in Chests)
            {
                if (chest.IsLiving) return false;
            }
            return true;
        }

        public override bool CanGameOver()
        {
            if (base.Game.TurnIndex > base.Game.MissionInfo.TotalTurn - 1)
            {
                return true;
            }
            base.CanGameOver();
            if (Dungeon == 1 && Stage == 2 && Mast != null && !Mast.IsLiving)
            {
                Lost = true;
                return true;
            }
            if (Dungeon == 1 && Stage == 4)
            {
                if (!Unsealed && AllChestsDead())
                {
                    Unsealed = true;
                    if (Statue != null && Statue.IsLiving)
                    {
                        base.Game.RemoveLiving(Statue, true);
                    }
                    int groundY = (int)(base.Game.Map.Info.ForegroundHeight * 0.52);
                    CreateBossAt(NpcId(12), base.Game.Map.Info.ForegroundWidth - 280, groundY);
                    return false;
                }
                return AllBossesDead();
            }
            return AllBossesDead();
        }

        public override void OnGameOver()
        {
            base.OnGameOver();
            base.Game.IsWin = !Lost && AllBossesDead();
        }
    }
}

namespace Game.Server.GameServerScript.AI.Game
{
    public class KhoBauNguoiCaGame : APVEGameControl
    {
        public override void OnCreated()
        {
            base.OnCreated();
            base.Game.SetupMissions("320001,320002,320003,320004");
            base.Game.TotalMissionCount = 4;
        }

        public override void OnPrepated()
        {
            base.OnPrepated();
        }
    }

    public class XuongHoiNuocGame : APVEGameControl
    {
        public override void OnCreated()
        {
            base.OnCreated();
            base.Game.SetupMissions("320011,320012,320013,320014");
            base.Game.TotalMissionCount = 4;
        }

        public override void OnPrepated()
        {
            base.OnPrepated();
        }
    }

    public class HangDungNhamGame : APVEGameControl
    {
        public override void OnCreated()
        {
            base.OnCreated();
            base.Game.SetupMissions("320021,320022,320023,320024");
            base.Game.TotalMissionCount = 4;
        }

        public override void OnPrepated()
        {
            base.OnPrepated();
        }
    }

    public class DoiDauAcQuyGame : APVEGameControl
    {
        public override void OnCreated()
        {
            base.OnCreated();
            base.Game.SetupMissions("320031,320032,320033,320034");
            base.Game.TotalMissionCount = 4;
        }

        public override void OnPrepated()
        {
            base.OnPrepated();
        }
    }

    public class ChienDichTroiSaoGame : APVEGameControl
    {
        public override void OnCreated()
        {
            base.OnCreated();
            base.Game.SetupMissions("320041,320042,320043,320044");
            base.Game.TotalMissionCount = 4;
        }

        public override void OnPrepated()
        {
            base.OnPrepated();
        }
    }
}
