using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace Game.Server.GameServerScript.AI.Messions;

/// <summary>
/// Phó bản Nguyên Tố - map 37 "Đại Chiến Rồng" (Pve_Info 37, Game_Map 70041).
/// Hai boss Rồng Lửa Tà Ác (trái/phải): 37010/37011, 37110/37111, 37210/37211, 37310/37311.
/// </summary>
public class YS37001 : AMissionControl
{
	private SimpleBoss bossLeft = null;

	private SimpleBoss bossRight = null;

	private SimpleNpc add = null;

	private int bossIDLeft = 37010;

	private int bossIDRight = 37011;

	private int addID = 37004;

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
		bossIDLeft = 37010 + offset;
		bossIDRight = 37011 + offset;
		addID = 37004 + offset;
	}

	public override void OnPrepareNewSession()
	{
		base.OnPrepareNewSession();
		SelectTier();
		int[] npcIds = new int[3] { bossIDLeft, bossIDRight, addID };
		int[] npcIds2 = new int[2] { bossIDLeft, bossIDRight };
		base.Game.LoadResources(npcIds);
		base.Game.LoadNpcGameOverResources(npcIds2);
		base.Game.AddLoadingFile(2, "image/bomb/blastOut/blastOut51.swf", "shootMovie51");
		base.Game.AddLoadingFile(2, "image/bomb/bullet/bullet51.swf", "bullet51");
		base.Game.SetMap(70041);
	}

	public override void OnStartGame()
	{
		base.OnStartGame();
		base.Game.IsBossWar = "37001";
		bossLeft = base.Game.CreateBoss(bossIDLeft, 1602, 515, -1, 1, "");
		bossRight = base.Game.CreateBoss(bossIDRight, 1662, 266, 1, 1, "");
		bossLeft.SetRelateDemagemRect(bossLeft.NpcInfo.X, bossLeft.NpcInfo.Y, bossLeft.NpcInfo.Width, bossLeft.NpcInfo.Height);
		bossRight.SetRelateDemagemRect(bossRight.NpcInfo.X, bossRight.NpcInfo.Y, bossRight.NpcInfo.Width, bossRight.NpcInfo.Height);
		add = base.Game.CreateNpc(addID, 900, 700, 1, -1);
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
		if (bossLeft != null && !bossLeft.IsLiving && bossRight != null && !bossRight.IsLiving)
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
		base.Game.IsWin = bossLeft != null && !bossLeft.IsLiving && bossRight != null && !bossRight.IsLiving;
	}
}
