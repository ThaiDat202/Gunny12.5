using Game.Base.Packets;
using Game.Server.Farm;
using Game.Server.GameObjects;
using Game.Server.GameUtils;
using Game.Server.Managers;
using Game.Server.Packets;
using SqlDataProvider.Data;

namespace Game.Server.Farm.Handle;

/// <summary>
/// 81/25 = SLACKER — client bấm búa "thợ lười" ở nông trại bạn bè (không payload).
/// Server xác định farm đang xem qua FarmSceneTracker, đập 1 thợ đang làm việc (feeling -10),
/// push 81/32 cho chủ farm nếu online, cộng tiến độ nhiệm vụ loại 1 + push 81/18.
/// </summary>
[FarmHandleAttbute(25)]
public class WorkerSlacker : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int farmId = FarmSceneTracker.GetFarm(Player.PlayerId);
		if (farmId <= 0)
		{
			farmId = Player.PlayerId;
		}
		if (farmId == Player.PlayerId)
		{
			Player.Out.SendMessage(eMessageType.Normal, "Hãy đến nông trại bạn bè rồi bấm búa để đập thợ lười.");
			return true;
		}
		UserManorWorkerInfo worker = PlayerManor.BeatSlackerWorker(farmId);
		if (worker == null)
		{
			Player.Out.SendMessage(eMessageType.Normal, "Nông trại này không có thợ lười để đập.");
			return true;
		}
		GamePlayer owner = WorldMgr.GetPlayerById(farmId);
		if (owner != null && owner.Manor != null && owner.Manor.IsLoaded)
		{
			owner.Manor.SendUpdateWorker(worker);
		}
		Player.Manor.AddTaskProgress(1, 1);
		Player.Manor.SendTaskList();
		Player.Out.SendMessage(eMessageType.Normal, "Đã đập thợ lười " + worker.Name + " (-10 tâm trạng).");
		return true;
	}
}
