using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Farm.Handle;

/// <summary>81/28 — đặt/huỷ thức ăn tự động cho cá (area 2) / gia súc (area 4); templateId 0 = huỷ.</summary>
[FarmHandleAttbute(28)]
public class FarmPlantSpeed : IFarmCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		if (!Player.Manor.IsLoaded)
		{
			Player.Manor.LoadFromDatabase();
		}
		int areaType = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		int templateId = (packet.DataLeft >= 4) ? packet.ReadInt() : 0;
		return Player.Manor.SetFodder(areaType, templateId);
	}
}
