# Form1.cs — Batch 1 (Châu Báu + Ma Thạch) — 07/11/2026

> **ĐÃ BUILD & DEPLOY lên `Service\Service\Road` lúc 08:25 07/11/2026 (bản 4 — mail ma thạch).**
> Backup: `Road.Service.exe.bak-20261107-081948`, `Bussiness.dll.bak-20261107-081948`.
> Process hiện tại: PID `22192`, port game `127.0.0.1:8527`,
> exe hash `3D424C8A…3124D407`, Bussiness hash `922DCD7D…F710F69`.
> (Road đang chạy non-elevated bằng `__COMPAT_LAYER=RunAsInvoker` vì UAC bị hủy;
> khi bạn restart bằng Run.bat như cũ nó sẽ chạy elevated bình thường.)

## Cập nhật lần 2

- Châu báu chính: gửi **18** mỗi loại (`1122540`, `313623`, `313622`, `313621`).
- Châu báu đặc biệt: vẫn gửi **1** mỗi loại (`311545`, `312519`, `312521`, `311541`, `311540`).
- **Ma Thạch**:
  - Nếu user **chưa có ma thạch**: gửi **8× `100906`** qua **hộp thư**, mỗi viên **level 10 / full exp / đủ 6 chỉ số**
    (Công, Thủ, Nhanh, May, Ma công, Ma kháng).
  - Nếu đã có: nâng toàn bộ ma thạch hiện có lên max level/exp + đủ 6 chỉ số.
  - Đã sửa `Bussiness.PlayerBussiness.SendMailAndItem`: bổ sung `ApplyMagicStoneMailStats` để mail item ma thạch
    có đủ `MagicAttack`, `MagicDefence`, `StrengthenExp`, 4 compose stats — hết lỗi hiển thị 0 chỉ số.
  - Max level lấy động từ `Magicstone_Template`; **hiện DB chỉ có Level 1–10**, không phải 20.
    `LevelUp` của MagicStone cũng hard-code level 10. Nếu muốn max 20 phải bổ sung data + sửa code.

## Cập nhật lần 3 — fix ma thạch thiếu chỉ số

- Nguyên nhân: `PlayerBussiness.SendMailAndItem` chỉ set 4 compose stats (Công/Thủ/Nhanh/May),
  **không set `MagicAttack` / `MagicDefence` / `StrengthenExp`** → ma thạch gửi qua mail bị 0 chỉ số.
- Đã sửa: tạo `ItemInfo` bằng `MagicStoneMgr.GetMagicStoneInfo(template, maxLevel)`, gán đủ **6 chỉ số**:
  Công, Thủ, Nhanh nhẹn, May mắn, Ma công, Ma kháng; thêm trực tiếp vào `MagicStoneBag` (online) hoặc
  `Sys_Users_Goods` BagType 41 (offline).
- Test reflection trên exe deployed:
  `TemplateID=100906, StrengthenLevel=10, StrengthenExp=1134400, Attack/Defend/Agility/Luck/MagicAttack/MagicDefence=154`.
- Exe deployed: PID `35352`, hash `B75502B0…0B820A3`, backup `Road.Service.exe.bak-20261107-081032`.

## Xác nhận từ user

- Xuyên Lv21 / H.Nhân Lv21: gửi **1** mỗi loại (đúng code hiện tại).
- **Không** tự gắn châu báu vào lỗ; chỉ mở khoan + gửi vào hộp thư.
- Build & deploy Road.Service: **đã thực hiện**.

## ID chính thức dùng trong code

| Mục | TemplateID | Ghi chú |
|---|---|---|
| Châu Báu Nhanh-ST Lv21 | `1122540` | gửi 15 |
| Siêu Giáp Lv21 | `313623` | gửi 15 |
| Siêu ST Lv21 | `313622` | gửi 15 |
| ST-Giáp Lv21 | `313621` | gửi 15 |
| Th.Báo Lv21 | `311545` | gửi 1 |
| Ch.Đào Lv21 | `312519` | gửi 1 |
| Hấp Thụ Lv21 | `312521` | gửi 1 |
| Xuyên Lv21 | `311541` | gửi 1 |
| H.Nhân Lv21 | `311540` | gửi 1 |
| Ma Thạch Tứ Tượng - Truyền Thuyết | `122318` | không dùng nữa |
| Ma Thạch Tứ Tượng (quality 6) | `100906` | gửi 8 + nâng max exp |
| Nguồn sức mạnh | `11689` | dùng cho Tiềm Năng (chưa code) |
| Sổ Tay Thám Hiểm | `1122910` | dùng cho Sổ tay (chưa code) |

## Đã thêm vào Form1.cs

- `TryGetSelectedUser(out userId, out nickName)`: lấy user theo `UserID_Lab`, tra NickName qua `PlayerBussiness.GetUserSingleByUserID`.
- `SendMailItem(...)`: gửi item qua `SendMailAndItemByNickName` (mail).
- `OpenAllSixDrills(userId)`:
  - Đọc `GetPlayerDrillByID`.
  - Tạo/đảm bảo đủ **6 mũi khoan** (DrillPlace 0–5, BeadPlace 13–18), set `HoleLv = 6`, `HoleExp = 0`.
  - Cập nhật DB (`AddUserUserDrill` / `UpdateUserDrillInfo`).
  - Nếu user online: cập nhật `BeadBag.UserDrills` + `SendPlayerDrill()`.
- `button31_Click` (**Châu Báu → Mở Mũi Khoan**):
  - Mở full 6 mũi khoan.
  - Gửi **18×** mỗi loại: `1122540`, `313623`, `313622`, `313621`.
  - Gửi **1×** mỗi loại: `311545`, `312519`, `312521`, `311541`, `311540`.
- `button37_Click` (**Ma Thạch**):
  - `CountMagicStones`: kiểm tra user đã có ma thạch chưa.
  - `UpgradeAllMagicStones(userId)`: set từng ma thạch về level/exp max và **đủ 6 chỉ số**.
  - Nếu chưa có: gửi 8 mail `100906` level max (mail đã có đủ 6 chỉ số nhờ fix Bussiness).
- `Bussiness.PlayerBussiness.ApplyMagicStoneMailStats`: bổ sung stats/exp cho mail item ma thạch.
- Test reflection `ApplyMagicStoneMailStats(100906, level 10)`:
  `StrengthenLevel=10, StrengthenExp=1134400, Attack/Defend/Agility/Luck/MagicAttack/MagicDefence=154`.
- Gắn `Click` vào designer cho `button31` và `button37`.

## Build verify

- Build bằng **MSBuild (Visual Studio 18 Community)**:
  `MSBuild.exe Road.Service.csproj /p:Configuration=Debug /p:BuildProjectReferences=false` → OK.
- Compile-only kiểm tra Form1: `dotnet build ... -t:Compile -p:BuildProjectReferences=false` → 0 lỗi.
- Deploy: copy `bin\Debug\net472\Road.Service.exe` + `.pdb` sang `Service\Service\Road`, restart service.
- Hash debug/deployed khớp: `F4B0C7DF0F5250EE42A35E776C51AD551E3D9E93F5EE14A15654EEB259A0F93D`.
- Reflection trên exe deployed thấy đủ `button31_Click`, `button37_Click`, `OpenAllSixDrills`, `SendMailItem`.
- Log `Error.log` rỗng; Road kết nối Center `127.0.0.1:1593` và Fighting `127.0.0.1:7539`.

Build toàn bộ solution vẫn có lỗi **pre-existing** ở `game.server` (decompiled trùng class ~9.000 lỗi) — không ảnh hưởng exe Road build theo kiểu trên.

## Cách dùng nhanh

1. Mở Road.Service console (cửa sổ minimized) → gõ `gm` nếu Form1 chưa mở.
2. Chọn **UserID** người chơi.
3. **Châu Báu → Mở Mũi Khoan**: mở 6 lỗ + gửi mail châu báu.
4. **Ma Thạch → Ma Thạch**: gửi 8 Ma Thạch Tứ Tượng - Truyền Thuyết.
5. Người chơi mở hộp thư để nhận.

## Còn lại trong danh sách yêu cầu

| Mục | Trạng thái |
|---|---|
| Thẻ Bài — add full thẻ bài | Chưa làm — cần danh sách TemplateID “full thẻ bài” |
| Sổ Tay Thám Hiểm — full | Chưa làm — cần xác định max level/field hoặc số item `1122910` |
| Tiềm Năng — full 6 món bằng Nguồn sức mạnh | Chưa làm — cần biết cơ chế update (`Sys_Users_...`/API) |
| Chiến Hồn — full 5 | Đã có handler `button28_Click` (FigSpirit 5 vị trí) |
| Phụ Ma / Đúc Hồn — full 5 nhẫn + vòng tay | Cần map handler/DB field |
| Tăng Sao — full 5 cho Vũ khí/Nón/Quần áo | Cần map handler/DB field |
| Tiến Hoá VK Phụ — full level | Cần bảng `Sub_Weapon_Evolution` |
| Các mục còn lại (Tu luyện, Vật tổ, Phụ kiện, Miếu Thần, Ấn, Vua Gà, Bảo Vệ, Dây chuyền, Hỗ trợ, Vinh dự, Danh hiệu, Nhẫn Cầu Hôn, PET, Thú Cưới, Thần Hộ Mệnh) | Đa số đã có handler/UI, cần rà “1 click full” |

## Cần xác nhận

1. `Xuyên Lv21` / `H.Nhân Lv21` gửi **1** hay **15**? (hiện code gửi 1)
2. Có cần **tự gắn châu báu vào 6 lỗ** hay chỉ mở khoan + gửi vào hộp thư? (hiện chỉ mở khoan + gửi)
3. Có được deploy `Road.Service` sau khi build lại không?
