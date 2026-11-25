using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace Game.Server.GameServerScript.AI.Messions;

/// <summary>
/// Phó bản Nguyên Tố - map 38 "Pháo Đài Hắc Ám" (Pve_Info 38, Game_Map 70037).
/// Độ khó: 38xxx / 381xx / 382xx / 383xx.
/// </summary>
public class YS38001 : AMissionControl
{
	private SimpleBoss boss = null;

	private SimpleNpc add1 = null;

	private SimpleNpc add2 = null;

	private int bossID = 38018;

	private int addID1 = 38001;

	private int addID2 = 38008;

	private int kill = 0;

	public override int CalculateScoreGrade(int score)
	{
		base.CalculateScoreGrade(score);
		if (score > 1750)
		{
			return 3;
		}
		if (score > 1675)
		{
			return 2;
		}
		if (score > 1600)
		{
			return 1;
		}
		return 0;
	}

	private void SelectTier()
	{
		int tier = (int)base.Game.HandLevel;
		if (tier < 0 || tier > 3)
		{
			tier = 0;
		}
		int offset = tier * 100;
		bossID = 38018 + offset;
		addID1 = 38001 + offset;
		addID2 = 38008 + offset;
	}

	public override void OnPrepareNewSession()
	{
		base.OnPrepareNewSession();
		SelectTier();
		int[] npcIds = new int[3] { bossID, addID1, addID2 };
		int[] npcIds2 = new int[1] { bossID };
		base.Game.LoadResources(npcIds);
		base.Game.LoadNpcGameOverResources(npcIds2);
		base.Game.AddLoadingFile(2, "image/bomb/blastOut/blastOut51.swf", "shootMovie51");
		base.Game.AddLoadingFile(2, "image/bomb/bullet/bullet51.swf", "bullet51");
		base.Game.SetMap(70037);
	}

	public override void OnStartGame()
	{
		base.OnStartGame();
		base.Game.IsBossWar = "38001";
		boss = base.Game.CreateBoss(bossID, 1616, 565, -1, 1, "");
		boss.SetRelateDemagemRect(-42, -200, 84, 194);
		add1 = base.Game.CreateNpc(addID1, 1100, 620, 1, -1);
		add2 = base.Game.CreateNpc(addID2, 1300, 640, 1, 1);
	}

	public override void OnNewTurnStarted()
	{
		base.OnNewTurnStarted();
	}

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
	}

	public override bool CanGameOver()
	{
		if (boss != null && !boss.IsLiving)
		{
			kill++;
			return true;
		}
		if (base.Game.TotalTurn > base.Game.MissionInfo.TotalTurn)
		{
			return true;
		}
		return false;
	}

	public override int UpdateUIData()
	{
		base.UpdateUIData();
		return base.Game.TotalKillCount;
	}

	public override void OnGameOver()
	{
		base.OnGameOver();
		base.Game.IsWin = boss != null && !boss.IsLiving;
	}
}
