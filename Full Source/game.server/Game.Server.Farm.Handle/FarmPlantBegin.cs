using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>81/11 — gieo hạt / bắt đầu khai thác (areaType 1 farm, 2 fish, 3 mine, 4 ranch; templateId 0 = dừng).</summary>
[FarmHandleAttbute(11)]
public class FarmPlantBegin : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int areaType = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		int templateId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		return Player.Manor.PlantBegin(areaType, templateId);
	}
}
