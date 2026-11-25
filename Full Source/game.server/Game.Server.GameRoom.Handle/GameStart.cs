using System;
using System.Collections.Generic;
using Bussiness;
using Game.Base.Packets;
using Game.Logic;
using Game.Server.BattleSkill;
using Game.Server.Buffer;
using Game.Server.GameObjects;
using Game.Server.Managers;
using Game.Server.Rooms;
using SqlDataProvider.Data;

namespace Game.Server.GameRoom.Handle;

[GameRoomHandleAttbute(7)]
public class GameStart : IGameRoomCommandHadler
{
	public bool CommandHandler(GamePlayer Player, GSPacketIn packet)
	{
		BaseRoom currentRoom = Player.CurrentRoom;
		if (currentRoom != null && currentRoom.Host == Player)
		{
			if (currentRoom.RoomType == eRoomType.EntertainmentPK && !Player.MoneyDirect(100))
			{
				return false;
			}
			List<GamePlayer> players = currentRoom.GetPlayers();
			bool flag = false;
			if (currentRoom.RoomType == eRoomType.Dungeon && currentRoom.IsElementPVE(currentRoom.MapId))
			{
				currentRoom.RoomType = eRoomType.Element_PVE;
			}
			int ticketID = currentRoom.GetTicketID(currentRoom.MapId);
			if (ticketID != 0)
			{
				foreach (GamePlayer item in players)
				{
					if (item.PropBag.GetItemCount(ticketID) <= 0)
					{
						Player.CurrentRoom.IsPlaying = false;
						Player.CurrentRoom.SendCancelPickUp();
						item.SendMessage("玩家: [" + item.PlayerCharacter.NickName + "] 无此副本门票,无法开启副本.");
						return false;
					}
				}
				foreach (GamePlayer item2 in players)
				{
					if (item2.RemoveTemplate(ticketID, 1))
					{
						item2.SendMessage("扣除成功,剩余门票:" + item2.PropBag.GetItemCount(ticketID) + "张.");
					}
				}
			}
			// Phó bản Nguyên Tố (ải viên chinh - tab Nguyên Tố): điều kiện vào trận.
			// Giới hạn bằng ngày mở (ElementCopyTimeLimited), số lần/ngày/ải (ElementCopyPerDayMaxCount)
			// và hoạt bát (ElementDungeonCostEnergy) — bỏ yêu cầu vé 100000010..16.
			if (currentRoom.RoomType == eRoomType.Element_PVE)
			{
				string elementGateError = CheckElementDungeonGate(players, currentRoom);
				if (elementGateError != null)
				{
					Player.CurrentRoom.IsPlaying = false;
					Player.CurrentRoom.SendCancelPickUp();
					Player.SendMessage(elementGateError);
					return false;
				}
			}
			// Pho ban su kien (Pve Type 24): gioi han 3 lan/ngay, reset 0h
			if (currentRoom.RoomType == eRoomType.Dungeon && currentRoom.MapId > 0)
			{
				SqlDataProvider.Data.PveInfo eventPve = PveInfoMgr.GetPveInfoById(currentRoom.MapId);
				if (eventPve != null && eventPve.Type == 24)
				{
					string eventGateError = CheckEventDungeonGate(players, currentRoom);
					if (eventGateError != null)
					{
						Player.CurrentRoom.IsPlaying = false;
						Player.CurrentRoom.SendCancelPickUp();
						Player.SendMessage(eventGateError);
						return false;
					}
				}
			}
			if (currentRoom.RoomType == eRoomType.TEAM_ROOM)
			{
				DayOfWeek dayOfWeek = DateTime.Now.DayOfWeek;
				if (dayOfWeek != 0 && dayOfWeek != DayOfWeek.Saturday)
				{
					using List<GamePlayer>.Enumerator enumerator3 = players.GetEnumerator();
					if (enumerator3.MoveNext())
					{
						GamePlayer current3 = enumerator3.Current;
						Player.CurrentRoom.IsPlaying = false;
						Player.CurrentRoom.SendCancelPickUp();
						current3.SendMessage("战队赛只有周末才开放哦~~敬请期待...");
						return false;
					}
				}
				int num = 0;
				foreach (GamePlayer item3 in players)
				{
					if (num != 0)
					{
						if (item3.PlayerCharacter.Team.teamID != num)
						{
							Player.CurrentRoom.IsPlaying = false;
							Player.CurrentRoom.SendCancelPickUp();
							item3.SendMessage("不是相同战队,无法开始比赛.");
							return false;
						}
					}
					else
					{
						num = item3.PlayerCharacter.Team.teamID;
					}
				}
			}
			if (Player.MainWeapon == null)
			{
				Player.SendMessage(LanguageMgr.GetTranslation("Game.Server.SceneGames.NoEquip"));
				flag = true;
			}
			else if (currentRoom.RoomType == eRoomType.FarmBoss && (players.Count == 1 || players.Count > 2))
			{
				Player.SendMessage(LanguageMgr.GetTranslation("GameStart.Msg2"));
				flag = true;
			}
			else
			{
				if (currentRoom.RoomType == eRoomType.SingleBattle && currentRoom.PlayerCount > 2)
				{
					flag = true;
				}
				if (currentRoom.RoomType == eRoomType.SpecialActivityDungeon || currentRoom.RoomType == eRoomType.SpecialDungeon3 || currentRoom.RoomType == eRoomType.SpecialDungeon2 || currentRoom.RoomType == eRoomType.SpecialDungeon1)
				{
					string format = "{0}" + LanguageMgr.GetTranslation("GameRoomRoomSetupChange.Msg5");
					int dungeonTicketId = currentRoom.GetDungeonTicketId(currentRoom.MapId);
					int num2 = 0;
					string text = "";
					// Chỉ kiểm tra vé khi phó bản thực sự có vé; trước đây GetDungeonTicketId trả 0
					// khiến mọi phòng boss đặc biệt (map 27/30/32) luôn bị chặn.
					if (dungeonTicketId > 0)
					{
						foreach (GamePlayer item4 in players)
						{
							if (item4.PropBag.GetItemCount(dungeonTicketId) <= 0)
							{
								text = ((item4.PlayerCharacter.ID != Player.PlayerCharacter.ID) ? (text + item4.PlayerCharacter.NickName) : (text + LanguageMgr.GetTranslation("GameRoomRoomSetupChange.Msg4")));
								text += ",";
								num2++;
							}
						}
					}
					if (num2 > 0)
					{
						text = text.Substring(0, text.Length - 1);
						Player.SendMessage(string.Format(format, text));
						flag = true;
					}
				}
				if (!flag)
				{
					foreach (GamePlayer item5 in players)
					{
						if (item5 == null || item5.PlayerCharacter.isViewer)
						{
							continue;
						}
						switch (currentRoom.RoomType)
						{
						case eRoomType.ActivityDungeon:
							if (item5.Actives.Info.activityTanabataNum == 0)
							{
								item5.Actives.Info.activityTanabataNum++;
							}
							break;
						case eRoomType.SpecialActivityDungeon:
						case eRoomType.SpecialDungeon1:
						case eRoomType.SpecialDungeon2:
						case eRoomType.SpecialDungeon3:
							item5.PropBag.RemoveTemplate(currentRoom.GetDungeonTicketId(currentRoom.MapId, currentRoom.HardLevel), 1);
							break;
						}
						if (currentRoom.RoomType == eRoomType.Lanbyrinth || currentRoom.RoomType == eRoomType.Dungeon)
						{
							item5.Extra.KingBlessStrengthEnchance(isUp: true);
						}
						else
						{
							item5.Extra.KingBlessStrengthEnchance(isUp: false);
						}
						item5.PetBag.ReduceHunger();
						if (currentRoom.RoomType == eRoomType.Entertainment || currentRoom.RoomType == eRoomType.EntertainmentPK)
						{
							item5.ClearFightBag();
							BufferList.CreatePayBuffer(400, 20000, 1)?.Start(item5);
						}
						item5.PlayerCharacter.ringFlag = false;
						if (CanAddLoveBuff(item5, players))
						{
							ConsortiaBufferTempInfo consortiaBufferTempInfo = ConsortiaExtraMgr.FindConsortiaBuffInfo(item5.LovelSKillID);
							if (consortiaBufferTempInfo != null)
							{
								item5.PlayerCharacter.ringFlag = true;
								item5.PlayerCharacter.loveBuffLevel = consortiaBufferTempInfo.level;
								item5.PlayerCharacter.loveBuffValue = consortiaBufferTempInfo.value;
							}
						}
					}
					RoomMgr.StartGame(Player.CurrentRoom);
			if (Player.CurrentRoom != null && (Player.CurrentRoom.RoomType == eRoomType.SpecialDungeon3 || IsEventDungeonRoom(Player.CurrentRoom)))
			{
				Console.WriteLine("[PVE-DEBUG] Push battle skills room=" + Player.CurrentRoom.RoomId + " map=" + Player.CurrentRoom.MapId + " roomType=" + (int)Player.CurrentRoom.RoomType);
				foreach (GamePlayer battleSkillPlayer in Player.CurrentRoom.GetPlayers())
				{
					if (battleSkillPlayer != null) BattleSkillOut.SendInfo(battleSkillPlayer);
				}
			}
				}
			}
			if (flag)
			{
				Player.CurrentRoom.IsPlaying = false;
				Player.CurrentRoom.SendCancelPickUp();
			}
		}
		return true;
	}

	private bool CanAddLoveBuff(GamePlayer seft, List<GamePlayer> list)
	{
		foreach (GamePlayer item in list)
		{
			if (item != null && seft != null)
			{
			}
		}
		return false;
	}

	/// <summary>Kiểm tra điều kiện vào phó bản Nguyên Tố: ngày mở + số lần/ngày/ải + hoạt bát.</summary>
	private static string CheckEventDungeonGate(List<GamePlayer> players, BaseRoom room)
	{
		foreach (GamePlayer player in players)
		{
			if (player == null)
			{
				continue;
			}
			if (room.currentFloor > 1 && player.GetTrialDungeonCount(room.MapId, room.currentFloor + 9) <= 0)
			{
				return "Bạn cần vượt qua Ải " + (room.currentFloor - 1) + " trước khi vào Ải " + room.currentFloor + ".";
			}
			if (player.GetTrialDungeonCount(room.MapId, 1) >= 3)
			{
				return "Bạn đã hết lượt vào phó bản sự kiện này hôm nay (3/3). Hãy quay lại vào ngày mai.";
			}
			if (GameProperties.IsMissionEnergyEnable && !player.MissionEnergyEmpty(GameProperties.NormalDungeonCostEnergy))
			{
				return "Không đủ hoạt bát để vào phó bản sự kiện (cần " + GameProperties.NormalDungeonCostEnergy + " hoạt bát).";
			}
		}
		return null;
	}

	private static bool IsEventDungeonRoom(BaseRoom room)
	{
		if (room == null || room.RoomType != eRoomType.Dungeon)
		{
			return false;
		}
		SqlDataProvider.Data.PveInfo pve = PveInfoMgr.GetPveInfoById(room.MapId);
		return pve != null && pve.Type == 24;
	}

	private static string CheckElementDungeonGate(List<GamePlayer> players, BaseRoom room)
	{
		int mapId = room.MapId;
		if (!IsElementDungeonOpenToday(mapId))
		{
			return "Phó bản Nguyên Tố này hôm nay không mở cửa. Hãy chọn phó bản khác.";
		}
		int stage = (int)room.HardLevel;
		if (stage < 1)
		{
			stage = 1;
		}
		if (stage > 4)
		{
			stage = 4;
		}
		int maxCount = GameProperties.ElementCopyPerDayMaxCount;
		foreach (GamePlayer player in players)
		{
			if (player == null)
			{
				continue;
			}
			if (maxCount > 0 && player.GetTrialDungeonCount(mapId, stage) >= maxCount)
			{
				return "Bạn đã hết số lần vượt ải hôm nay của phó bản Nguyên Tố này.";
			}
			if (GameProperties.IsMissionEnergyEnable && !player.MissionEnergyEmpty(GameProperties.ElementDungeonCostEnergy))
			{
				return "Không đủ hoạt bát để vào phó bản Nguyên Tố (cần " + GameProperties.ElementDungeonCostEnergy + " hoạt bát).";
			}
		}
		return null;
	}

	/// <summary>ElementCopyTimeLimited dạng "33-1,5|34-2,6|..." (thứ 1=Mon .. 7=Sun).</summary>
	private static bool IsElementDungeonOpenToday(int mapId)
	{
		string config = GameProperties.ElementCopyTimeLimited;
		if (string.IsNullOrEmpty(config))
		{
			return true;
		}
		foreach (string part in config.Split('|'))
		{
			string[] fields = part.Split('-');
			if (fields.Length < 2)
			{
				continue;
			}
			int configMapId;
			if (!int.TryParse(fields[0].Trim(), out configMapId) || configMapId != mapId)
			{
				continue;
			}
			int today = (int)DateTime.Now.DayOfWeek;
			if (today == 0)
			{
				today = 7;
			}
			foreach (string dayText in fields[1].Split(','))
			{
				int day;
				if (int.TryParse(dayText.Trim(), out day) && day == today)
				{
					return true;
				}
			}
			return false;
		}
		return true;
	}
}
