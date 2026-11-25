using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace Game.Server.GameServerScript.AI.Messions;

/// <summary>
/// Phó bản Nguyên Tố - map 35 "Phế Tích Thành Gà" (Pve_Info 35, Game_Map 70035).
/// Chọn nhóm NPC theo độ khó: 35xxx thường / 351xx khó / 352xx ác mộng / 353xx anh hùng.
/// </summary>
public class YS35001 : AMissionControl
{
	private SimpleBoss boss = null;

	private SimpleNpc add1 = null;

	private SimpleNpc add2 = null;

	private int bossID = 35004;

	private int addID1 = 35001;

	private int addID2 = 35002;

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
		bossID = 35004 + offset;
		addID1 = 35001 + offset;
		addID2 = 35002 + offset;
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
		base.Game.SetMap(70035);
	}

	public override void OnStartGame()
	{
		base.OnStartGame();
		base.Game.IsBossWar = "35001";
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
