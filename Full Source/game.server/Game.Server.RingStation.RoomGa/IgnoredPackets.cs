using Game.Base.Packets;

namespace Game.Server.RingStation.RoomGamePkg.TankHandle;

/// <summary>
/// Chat trong trận (packet code 19). Phòng BOT (RingStation) nhận được packet này do
/// Fight server phát lại cho toàn bộ người trong trận; bot không cần xử lý chat nên
/// trước đây rơi vào "LoadCommandHandler code026: 19" (log nhiễu). Handler no-op này
/// giữ cho log sạch mà không ảnh hưởng gì tới chat của người chơi thật.
/// </summary>
[GameCommandAttbute(19)]
public class ChatMessage : IGameCommandHandler
{
	public bool HandleCommand(RingStationRoomLogicProcessor process, VirtualGamePlayer player, GSPacketIn packet)
	{
		return true;
	}
}

/// <summary>GAME_ROOM_REMOVEPLAYER (5) — trước đây không có handler nên bị bỏ qua kèm log lỗi.</summary>
[GameCommandAttbute(5)]
public class GameRoomRemovePlayer : IGameCommandHandler
{
	public bool HandleCommand(RingStationRoomLogicProcessor process, VirtualGamePlayer player, GSPacketIn packet)
	{
		return true;
	}
}

/// <summary>GAME_MISSION_START (82) — trước đây không có handler nên bị bỏ qua kèm log lỗi.</summary>
[GameCommandAttbute(82)]
public class GameMissionStart : IGameCommandHandler
{
	public bool HandleCommand(RingStationRoomLogicProcessor process, VirtualGamePlayer player, GSPacketIn packet)
	{
		return true;
	}
}
