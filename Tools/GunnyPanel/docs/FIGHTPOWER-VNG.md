# Lực chiến (FightPower) — đồng bộ với Gunny VNG

Cập nhật 2026-11-09 02:22 (DLL `DE72A4CC19F8B25F8BE8998765292C8A90552860712481EE9E50473F9D1CFA74`).

## 1. Vì sao trước đây số lực chiến "không giống VNG"

Client Gunny 2023 (và VNG hiện tại) hiển thị lực chiến theo **2 trường trong packet**:

| Trường | Kiểu | Ý nghĩa |
|---|---|---|
| `FightPower` | int (32-bit) | Giá trị cũ, đã bị giảm bởi cờ `reduzirFC` trong bảng `Server_Config` (VNG bật `reduzirFC=True` → chia công thức cho **1.000.000.000**) |
| `FightPower_Show` | long (64-bit) | Giá trị **hiển thị** theo công thức gốc, KHÔNG bị giảm (`reduzirFC=False` → chia cho **200.000.000**), client tự cộng `FightPower_Show(extra) + FightPower(int)` để ra số hiển thị |

Trước khi patch, server ghi `WriteLong(0L)` cho mọi vị trí `FightPower_Show` trong packet
(comment `//_loc24_.FightPower_Show = param1.readLong();` trong `BaseGame.cs`), nên client chỉ
hiển thị giá trị int cũ đã bị giảm 5 lần — vì vậy số lực chiến thấp hơn / khác VNG.

## 2. Những gì đã sửa

- `SqlDataProvider.Data.PlayerInfo`: thêm property `long FightPower_Show` (phần cộng thêm).
- `GamePlayer.UpdateFightPower()`: tính thêm giá trị theo công thức gốc
  (`divisor = 200.000.000`), lưu phần chênh lệch vào `PlayerCharacter.FightPower_Show`.
  Có log `FightPower_Show: player=... legacy=... full=... show=...` để đối chiếu.
- Gửi giá trị thật thay cho `WriteLong(0L)` tại 4 chỗ:
  - `AbstractPacketLib.SendLoginSuccess` (packet 1),
  - `AbstractPacketLib.SendUpdatePublicPlayer` (packet 67),
  - `AbstractPacketLib.SendUserEquip` (packet 74),
  - `Game.Logic.BaseGame` (packet vào trận, nhánh `VERSION >= 12100`).
- Thêm 2 khoá cấu hình vào `Db_Tank_All.dbo.Server_Config` (chỉnh không cần sửa code):
  - `FightPowerShowDivisor = 200000000` — hệ số chia công thức hiển thị.
  - `FightPowerShowRate = 1` — hệ số nhân thêm vào phần lực chiến hiển thị (tinh chỉnh).

> Lưu ý: 2 khoá này được `ManorConfigMgr` nạp lúc server khởi động, nên sau khi đổi giá trị
> cần restart Road.Service để áp dụng.

## 3. Cách kiểm tra / tinh chỉnh

1. Relog nhân vật → xem số Lực chiến ở sảnh (cạnh tên nhân vật) và trong túi (tab Thông tin).
2. Log `GameServer.log` có dòng `FightPower_Show: player=1 ... legacy=181800898 full=... show=...`.
   - Số hiển thị client = `legacy + show` (≈ `full`).
3. Nếu muốn khớp đúng một con số VNG cụ thể `X`:
   - giữ nguyên `FightPowerShowDivisor=200000000`, chỉnh
     `FightPowerShowRate = (X - legacy) / (full - legacy)` (làm tròn 2 chữ số),
   - restart Road.Service.
4. Nếu thấy số quá lớn/nhỏ so với VNG: tăng/giảm `FightPowerShowDivisor` tương ứng tỉ lệ thuận
   với `full` (ví dụ 400000000 sẽ giảm phần lớn xuống ~1 nửa).

## 4. Hạn chế hiện tại

- `FightPower_Show` chưa lưu vào DB (`Sys_Users_Detail` không có cột tương ứng) → **người chơi offline**
  khi hiện trong danh sách bạn bè/room có thể vẫn hiện giá trị int cũ. Người đang online thì đúng.
- Có thể mở rộng sau: thêm cột `FightPower_Show bigint` vào `Sys_Users_Detail` + map vào
  `PlayerBussiness.GetUserInfoSingle` để xếp hạng/hiển thị offline cũng đúng.
