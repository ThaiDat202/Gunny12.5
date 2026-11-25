using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>81/14 — nâng cấp khu: 1 farm, 2 fish, 3 mine, 4 ranch, 5 đất (land), 6 cuốc (hoe).</summary>
[FarmHandleAttbute(14)]
public class FarmUpPlant : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int upgradeType = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		return Player.Manor.UpgradeArea(upgradeType);
	}
}
