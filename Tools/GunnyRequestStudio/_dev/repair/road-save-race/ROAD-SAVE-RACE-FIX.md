# Sửa lỗi Road: NullReferenceException trong GamePlayer.saveTeamSelfActive (2026-11-10 08:37)

## Log lỗi

```
2026-11-10 08:37:57,331 - [4] - ERROR - Game.Server.GameObjects.GamePlayer
Error saving all datas of playerID: 7 , !
System.NullReferenceException: Object reference not set to an instance of an object.
   at Game.Server.GameObjects.GamePlayer.saveTeamSelfActive()
   at Game.Server.GameObjects.GamePlayer.SaveIntoDatabase()
```

## Nguyên nhân

- `GamePlayer.Login()` gọi `WorldMgr.AddPlayer(...)` **trước khi** `LoadFromDatabase()` chạy xong
  (GamePlayer.cs: `WorldMgr.AddPlayer` ở đầu `Login`, `LoadFromDatabase()` ngay sau đó).
- Thread `SaveTimerProc` (GameServer.cs) định kỳ quét `WorldMgr.GetAllPlayers()` và gọi `SaveIntoDatabase()`
  cho **mọi player** trong danh sách.
- Nếu SaveTimer chạy đúng lúc player vừa login (đang giữa `LoadFromDatabase`):
  - `m_character` là object tạm từ login packet (NickName rỗng, các collection runtime chưa load)
  - `PlayerCharacter.TeamSelfActive` còn **null**
  → `saveTeamSelfActive()` ném NullReferenceException; SaveIntoDatabase báo lỗi và dừng lưu (các phần trước đã lưu).
- PlayerID 7 (nhân vật mới tạo) login lúc 08:37:57 trùng nhịp SaveTimer → lỗi xuất hiện một lần.

## Đã sửa (source gốc: `Full Source\game.server`)

1. `GamePlayer` thêm cờ `m_loaded` + property `Loaded`:
   - `Login()`: sau khi `LoadFromDatabase()` thành công mới set `m_loaded = true`.
   - `SaveIntoDatabase()`: nếu `!m_loaded || m_character == null` → bỏ qua (return false), không lưu player đang login dở.
2. `GameServer.SaveTimerProc`: bỏ qua player chưa `Loaded` (`if (gamePlayer == null || !gamePlayer.Loaded) continue;`).
3. Phòng thủ thêm:
   - `saveTeamSelfActive()`: nếu `PlayerCharacter == null` return; nếu `TeamSelfActive == null` khởi tạo list rỗng.
   - `saveTeamDayActive()`: nếu `TeamDayActive == null` khởi tạo object mới với `UserID`.
4. Build lại `Game.Server.dll` + `Road.Service.exe`, deploy vào `Service\Service\Road`
   (kèm `Game.Server.pdb` / `Road.Service.pdb` để lần sau log có **số dòng**), restart stack.

## Kiểm chứng

- 3 service lên: Center 1593 / Fight 7539 / Road 8527 + GM API 9601; `Error.log` cả 3 = 0 byte; health `ok=true`.
- Log deploy: `RUN-STACK-OK`; DLL mới `Game.Server.dll` 3.367.424 bytes (08:43), PDB 1.695.200 bytes.
- PlayerID 7 vẫn tồn tại trong DB (`UserName=haha123`, `NickName=Đạt`, Grade=20) — dữ liệu chính đã lưu từ trước lỗi;
  bảng `Sys_Users_TeamSelfActive` vốn rỗng (0 dòng) nên không mất dữ liệu team.
- Lỗi chỉ xảy ra 1 lần lúc 08:37:57 và không lặp lại sau restart.

## Việc cần làm

- User login lại nhân vật (Đạt / haha123). Nếu còn lỗi, xem `Service\Service\Road\logs\Error.log`
  (lần này có PDB nên stack sẽ kèm số dòng) rồi gửi lại.

## File

- Source đã sửa: `Full Source\game.server\Game.Server.GameObjects\GamePlayer.cs`, `Game.Server\GameServer.cs`.
- Script patch: `_dev\repair\road-save-race\patch-save-race.py` (mô tả các thay đổi).
- Bản deploy: `Service\Service\Road` (backup cũ: các thư mục `_backup-fp-long-*`).
