# Thần Hộ Mệnh (Elf) — hoàn thiện tab Linh Bảo + Kỹ năng

> Trạng thái: **đã code + build `Game.Server.dll` + deploy + restart Road.Service** (2026-11-09).
> Phạm vi: `C:\Gunny\Gunny12.6\Full Source\game.server` (chỉ DLL Game.Server), `Service\Service\Road\Languages`,
> dữ liệu test gửi qua hộp thư. Không sửa client, không đổi giao thức cũ.

## 1. Hiện trạng trước khi sửa (bằng chứng từ log live)

Người chơi mở **Thần Hộ Mệnh** → tab **Linh Bảo** thì server báo:

```
ElfLogicProcessor PackageType:ELF_EQUIP_OPERATE, OnGameData is Error:
System.Collections.Generic.KeyNotFoundException ... ElfHandleMgr.LoadCommandHandler
```

Lỗi thứ hai khi mở túi/trang bị (packet 71 `UserEquipList`):

```
NullReferenceException at AbstractPacketLib.SendUserEquip ... line 2303
```

Nguyên nhân gốc (đã xác minh trong source + client decompile):

1. **Thiếu handler `ElfPackageType.ELF_EQUIP_OPERATE = 18`** → client bấm mặc/tháo Linh Bảo (664/18) bị
   `KeyNotFoundException`, không phản hồi.
2. **Packet 707 (Linh Bảo) bị định tuyến sai**: `[PacketHandler(707)] ElfEquipHander` gọi
   `ElfHandler.ProcessData` (ElfLogicProcessor đọc byte đầu thành `ElfPackageType`) ⇒ request 707/3
   (`GET_ELFEQUIP`) bị chạy như 664/3 (`EXERCISE`), 707/4 (`UPGRADE_ELFEQUIP`) bị chạy như 664/4
   (`ADD_GROWTH`) → sai/hỏng dữ liệu Elf.
3. **`Sys_Users_ElfEquip` chưa từng được load/save** trong `GamePlayer` (bảng có sẵn nhưng rỗng, EF model
   không được dùng) ⇒ không thể hiển thị/mặc/nâng cấp Linh Bảo.
4. **707/1 và 707/2 gửi dữ liệu giả**: 707/1 lấy `ElfType` của *elf* thay vì level Linh Bảo từng hệ;
   707/2 gửi `elfID` thay vì `record ID` của Linh Bảo, và `quality` của elf thay vì quality Linh Bảo.
   Đồng thời 707/1 NRE nếu elf có TempID lạ.
5. **`AbstractPacketLib.SendUserEquip`** (VERSION>=12100):
   - `sys_Users_Elf.EquipTempId` NRE khi người chơi **không có elf ra trận**;
   - ghi `WriteInt(0)` cho `lingStoneStr` trong khi client đọc `readUTF()` (2 byte length + data)
     ⇒ **lệch con trỏ** packet 71, làm hỏng các field phía sau (bag/trang bị).
6. **Kỹ năng**: đọc sách không chọn đúng sách bind; khi thay skill mà khoá hết skill thì
   `ElementAt` ném exception; **không trừ Xu khoá skill** (`ElfLockSkillPayMoney`) dù client đã xác nhận giá.
7. **Dung hợp kỹ năng (664/12)**: số skill sau dung hợp có thể **vượt MaxSkillCount** (cộng lock + tối đa num2);
   elf phụ bị xoá khỏi bộ nhớ nhưng **không xoá DB** ⇒ relog quay lại.
8. **Bỏ Thần Hộ Mệnh (664/7)**: chỉ xoá khỏi bộ nhớ, **không xoá DB** ⇒ relog quay lại; không chặn xoá elf
   đang mặc Linh Bảo.

## 2. Đã hoàn thiện

### 2.1 Linh Bảo (elf equipment) — packet 664/18 + 707/1,2,3,4

| Thành phần | Nội dung |
|---|---|
| Nguồn cấu hình | `Game.Server.Elf\ElfEquipConfigMgr` đọc `WebSite\Request\TS_LinkPalTemplate.xml` (zlib, cùng file client dùng): 1.500 dòng = 6 hệ × 5 quality × 50 level; có stats, SkillID, nguyên liệu (`ItemTempId1/Param1`, `ItemTempId2/Param2`) |
| Kích hoạt Linh Bảo | `Handle\ElfEquipGet` (707/3): client double-click item **CategoryID 91** (48001–48030) trong túi → server map `TemplateID → (Type, Quality)`, xoá item, tạo record `Sys_Users_ElfEquip` (level = level hiện tại của hệ), lưu DB ngay để có `ID`, push 707/2 + 707/1 |
| Mặc/tháo | `Handle\ElfEquipOperate` (664/18): `(elfId, equipId)`; **toggle** — gửi lại đúng record đang mặc = tháo; mặc record khác tự tháo record cũ/elf khác; cập nhật `Sys_Users_Elf.ElfEquipId/EquipTempId`, `Sys_Users_ElfEquip.IsEquip`; push 664/1 + 707/1 + 707/2 |
| Nâng cấp | `Handle\ElfEquipUpgrade` (707/4): `(nextTemplateID, type, itemMat1, itemMat2)`; server tự tra (type, quality), level hiện tại (max theo hệ), dòng level kế tiếp, kiểm tra + trừ **12286 Pha Lê Linh Hồn** và **Linh Nguyên** đúng hệ (Lửa 12289, Gió 12290, Đất 12288, Nước 12287, Sáng 12293, Tối 12294), tăng level mọi record cùng hệ, push 707/4 `(type, newLevel)` + 707/1 |
| Push chuẩn | `ElfPacketsOut.SendUpdateElfEquipLvInfo` (707/1: count + n×(type, level)), `SendUpdateElfEquipInfo` (707/2: count + n×(recordID, type, quality)), `SendUpgradeElfEquip` (707/4) |
| Lưu trữ | `GamePlayer.LoadFromDatabase` load `Sys_Users_ElfEquip`; `GamePlayer.SaveElfDataToDatabase()` lưu ngay (insert/update `Sys_Users_Elf` + `Sys_Users_ElfEquip`, EF sinh `ID`); `SavePlayerInfoToDatabase` (logout) cũng lưu collection này |
| Định tuyến 707 | `ElfEquipHander` → processor riêng `ElfEquipLogicProcessor` (đọc `ElfEquipPackageType`), không còn đi nhầm sang `ElfLogicProcessor` |

Giới hạn: type 1..6; quality 1..5; level 1..50. Nâng cấp **không tốn Đá Thánh** — chỉ nguyên liệu.
Mỗi Thần Hộ Mệnh mặc được 1 Linh Bảo; Linh Bảo đang mặc không xuất hiện trong danh sách chọn.

### 2.2 Kỹ năng (skills) — packet 664/5, 664/12

- Học sách: `Handle\Skill.cs` chọn đúng record sách theo `TemplateID + IsBind` (trước đây bỏ qua bind),
  validate danh sách skill khoá thuộc elf, trừ **Xu khoá kỹ năng** theo `ServerConfig`:
  `ElfLockSkillPayMoney = 200,500,1500,3000,5000,8500,12000` (theo số skill khoá), không đủ Xu thì
  không tiêu sách. Chặn trường hợp khoá toàn bộ skill (trước đây ném `ArgumentOutOfRangeException`).
- Response học skill: 664/5 `(elfId, skillList csv)` — sách giảm 1 và push 664/9.
- Dung hợp: `Handle\Blend.cs` giới hạn đúng `MaxSkillCount` (3★=5, 4★=6, 5★=8), validate + trừ Xu khoá,
  **xoá elf phụ khỏi DB** (`DeleteElfFromDatabase`), giữ elf chính + push 664/5/664/7.
- Danh sách sách kỹ năng: item **CategoryID 86** (45101…); nhận item là tự chuyển vào
  `Sys_Users_SkillBooks` + push 664/9 (đường `GamePlayer.AddItem/StackItemToAnother` sẵn có).

### 2.3 Chỉ số Linh Bảo áp vào nhân vật

- `ElfMgr.GetElfEquipBonus` tính Attack/Defence/Agility/Lucky/HP/Damage của Linh Bảo đang mặc
  (theo Type/Quality/Level hiện tại trong `TS_LinkPalTemplate`).
- `PlayerEquipInventory.UpdatePlayerProperties` cộng các chỉ số này vào Attack/Defence/Agility/Luck/HP
  của nhân vật (khi mặc/tháo/nâng cấp → gọi `EquipBag.UpdatePlayerProperties()` để tính lại ngay).
- `PlayerProperty` thêm nguồn `ElfEquip`; `GamePlayer.GetBaseDamage/GetBaseDefence` cộng Damage/Armor;
  packet 164 (`SendUpdatePlayerProperty`) gửi giá trị `ElfEquip` thật cho Attack/Defence/Agility/Luck/HP/Damage
  (trước đây ghi 0) → dialog chi tiết thuộc tính hiển thị đúng nguồn "Linh Bảo".

### 2.4 Sửa lỗi phụ

- `SendUserEquip`: null-safe elf đang chiến đấu; `lingStoneStr` ghi đúng UTF (`WriteString("")`)
  → hết NRE và hết lệch packet 71.
- Bỏ Thần Hộ Mệnh (664/7): xoá DB thật, chặn khi đang mặc Linh Bảo (`Elf.Discard.EquipOn`).
- Kích hoạt Thần Hộ Mệnh (664/10): lưu DB ngay sau khi tạo.
- Thêm khoá ngôn ngữ: `Elf.EquipGet.*`, `Elf.EquipUpgrade.*`, `Elf.EquipOperate.*`, `Elf.Discard.EquipOn`,
  `Elf.Skill.NoMoney`, `Elf.Blend.NoMoney` trong `Service\Service\Road\Languages\Language-zh_cn.txt`.

## 3. File thay đổi (game.server)

| File | Loại | Nội dung |
|---|---|---|
| `Game.Server.Elf\ElfEquipConfigMgr.cs` | **mới** | Load/parse `TS_LinkPalTemplate.xml` (zlib từ `WebSite\Request`), API map TemplateID→Type/Quality, level info, max level |
| `Game.Server.Elf\ElfEquipLogicProcessor.cs` | **mới** | Processor riêng cho packet 707 |
| `Game.Server.Packets\ElfEquipPackageType.cs` | **mới** | Enum 707/1..4 |
| `Game.Server.Elf.Handle\ElfEquipGet.cs` | **mới** | Handler 707/3 — nhận Linh Bảo từ túi |
| `Game.Server.Elf.Handle\ElfEquipUpgrade.cs` | **mới** | Handler 707/4 — nâng cấp Linh Bảo |
| `Game.Server.Elf.Handle\ElfEquipOperate.cs` | **mới** | Handler 664/18 — mặc/tháo (toggle) |
| `Game.Server.Elf\ElfMgr.cs` | sửa | `GetElfEquips`, `GetElfEquipLevel`, `GetElf`, `AddElfEquipRecord`; `Init` nạp `ElfEquipConfigMgr` |
| `Game.Server.Elf\ElfPacketsOut.cs` | sửa | 707/1, 707/2 dữ liệu thật; thêm `SendUpgradeElfEquip` (707/4) |
| `Game.Server.Elf.Handle\Skill.cs` | sửa | chọn sách theo bind, validate/trừ Xu khoá, chặn khoá hết skill |
| `Game.Server.Elf.Handle\Blend.cs` | sửa | giới hạn MaxSkillCount, trừ Xu khoá, xoá DB elf phụ |
| `Game.Server.Elf.Handle\Discard.cs` | sửa | chặn elf đang mặc Linh Bảo, xoá DB |
| `Game.Server.Elf.Handle\ActivateElf.cs` | sửa | lưu DB ngay |
| `Game.Server.GameObjects\GamePlayer.cs` | sửa | load/save `Sys_Users_ElfEquip`, `SaveElfDataToDatabase`, `DeleteElfFromDatabase`, `ElfEquipHandler` |
| `Game.Base.Packets\AbstractPacketLib.cs` | sửa | null-safe elf; sửa `lingStoneStr` (UTF); gửi `ElfEquip` thật ở packet 164 |
| `Game.Server.Packets.Client\ElfEquipHander.cs` | sửa | route 707 sang `ElfEquipHandler` |
| `Game.Server.GameUtils\PlayerProperty.cs` | sửa | thêm nguồn `ElfEquip` (prop + Damage/Armor) |
| `Game.Server.GameUtils\PlayerEquipInventory.cs` | sửa | cộng bonus Linh Bảo vào chỉ số nhân vật |

Build: MSBuild (net472), `Game.Server.dll` 3.336.192 bytes, sha256 `9EC7EFAFF21F2F81F84977946AFD7BCACA1EE1722DD9AC0EDAE509A367E20987`.
Deploy: copy vào `Service\Service\Road` + restart Road.Service (đã làm lúc 01:29, ports 8527/9601 OK).

## 4. Test trong game (tài khoản `test1` / user 1)

Đã gửi 30 thư **`[ELF-TEST]`** vào Hộp thư (idempotent, chạy lại không gửi trùng):
[`_tools\elf-send-test-items.ps1`](../_tools/elf-send-test-items.ps1).

1. **Nhận thư**: Hộp thư → nhận các thư `[ELF-TEST]` (Linh Bảo 48001/48002/48005/48009/48013/48021/48026;
   nguyên liệu 12286×3000, 12287×2000, 12288/12289/12290/12293/12294×500; sách kỹ năng 45101/45102/45113/
   45116/45119/45122/45194/45196; Sách Thần 12241/12242×5).
2. **Linh Bảo**: túi Hành trang → **double-click item Linh Bảo** (vd “Châu Báu Hàn Băng” 48001)
   → item biến mất, mở Thần Hộ Mệnh → **Linh Bảo** thấy Linh Bảo trong danh sách.
   - Chọn Thần Hộ Mệnh (vd Thần Nước) → double-click Linh Bảo để mặc; double-click ô đang mặc để tháo.
   - Bấm ô đang mặc → **Luyện**: cần 12286 (+12287 cho hệ Nước) → nâng level, ô “LV:” tăng, chỉ số tăng.
   - Mở **nhân vật → chi tiết thuộc tính** phải thấy dòng nguồn **“Linh Bảo”** cộng Attack/Defence/
     Agility/Luck/HP/Damage (trước đây luôn 0).
   - Relog kiểm tra vẫn giữ: `SELECT * FROM Db_Tank.dbo.Sys_Users_ElfEquip WHERE UserID=1`, cột `ElfEquipId`
     trong `Sys_Users_Elf`.
3. **Kỹ năng**: mở Thần Hộ Mệnh → tab **Kỹ năng** → chọn sách đã nhận → **Học**:
   - Học thành công: skill xuất hiện ở danh sách skill (thanh trái), sách giảm 1.
   - Khi đủ 8 skill (Thần Nước 5★): học thêm sẽ hỏi khoá skill — nếu khoá N skill, server trừ
     Xu đúng giá `ElfLockSkillPayMoney[N-1]`; không đủ Xu báo lỗi, không mất sách.
   - **Dung hợp**: chọn 1 Thần Hộ Mệnh phụ → elf phụ mất (relog không quay lại), skill elf chính
     nhận thêm từ elf phụ, không vượt 8.
4. **Triệu hồi**: dùng Sách Thần-Sơ/Trung (12241/12242) → nhận nguyên liệu/linh bảo theo box, số lượt
   `SummonCount` giảm; kết quả hiện trong bảng thưởng.
5. **Đổi**: tab Đổi hiện shop 133 (24 món), tiền là **Đá Thánh** (`Extra.Info.Coin`; user1 hiện có 400);
   mua thử món rẻ (vd Quả Hồn-Sơ 2 Đá Thánh) → nhận item, số Đá Thánh giảm ngay.
6. **Bỏ**: tháo Linh Bảo rồi Bỏ một Thần Hộ Mệnh không dùng → relog không quay lại.

## 5. Kiểm chứng kỹ thuật

- Build 0 lỗi (`data/elf-work/server/build/build5.log`); DLL chứa `ElfEquipConfigMgr`,
  `ElfEquipLogicProcessor`, `ElfEquipOperate`, `SaveElfDataToDatabase`, `DeleteElfFromDatabase`.
- Log khởi động Road phải có:
  `ElfEquipConfigMgr: loaded 1500 LinkPal rows, 30 item templates, 30 type/quality groups`
  (nạp trong `ElfMgr.Init`).
- Log live sau khi test: không còn `KeyNotFoundException` cho `ELF_EQUIP_OPERATE`,
  không còn `NullReferenceException` ở `SendUserEquip`.
- DB kiểm tra: `Db_Tank.dbo.Sys_Users_ElfEquip` có record sau khi nhận Linh Bảo; `Level` tăng sau Luyện;
  `Sys_Users_Elf.ElfEquipId/EquipTempId` đúng khi mặc/tháo.
- Phân tích client đầy đủ (wire format, field, trigger): [`ELF-PROTOCOL.md`](ELF-PROTOCOL.md).

## 6. Hotfix 01:42 ngày 2026-11-09 (sau khi user test)

Hai lỗi phát hiện khi user test lại:

1. **Tab Kỹ năng báo `KeyNotFoundException` code 5** — file `Game.Server.Elf.Handle\Skill.cs` bị
   script vá lỗi làm **rỗng (0 byte)** ở lần build trước ⇒ handler Kỹ năng không được đăng ký.
   Đã khôi phục đầy đủ `Skill.cs` (kèm các fix bind/khoá/trừ Xu) và rebuild; kiểm tra bằng reflection
   trên DLL đang chạy: **14 handler** Elf đã đăng ký gồm code **5** (Skill) và **18** (EquipOperate).
2. **Linh Bảo trong túi nhưng không hiện trong tab Linh Bảo** — item Linh Bảo (CategoryID 91) chỉ được
   server chuyển thành record khi client **double-click** item (707/3). Nay server **tự chuyển**:
   - `GamePlayer.AddItem/StackItemToAnother`: nhận item CategoryID 91 (từ mail/shop/box) → tạo record
     `Sys_Users_ElfEquip` + push 707/2/707/1 (không đặt item vào túi),
   - `ElfMgr.ConvertElfEquipItems` (gọi lúc login): quét Trang bị + Hành trang, chuyển các item Linh Bảo
     còn sót từ trước (7 món đã gửi qua thư) thành record, lưu bag + DB.
   - Handler 707/3 vẫn giữ (có thêm log `ElfEquipGet: ...` để chẩn đoán).
   DLL hotfix: 3.341.312 bytes, sha256 `522D876DB3710B008963CB7FBD629E776D6516297AA4CE63449CEF79F7CF6B9A`,
   restart Road.Service 01:42:31. Sau khi user relog: `Db_Tank.dbo.Sys_Users_ElfEquip` sẽ có 7 dòng
   tương ứng 7 món Linh Bảo đang nằm trong túi; tab Linh Bảo hiển thị danh sách này.

## 7. Hotfix 2 — 01:53 ngày 2026-11-09 (sau khi relog vẫn trống tab Linh Bảo)

- **Đọc log 01:44:24**: user relog đã chạy `ElfMgr.ConvertElfEquipItems` nhưng **0 item** được chuyển.
  Nguyên nhân: hàm `ConvertElfEquipItems` được gọi trong `LoadPlayerInfoFromDatabase()`, mà trong
  `GamePlayer.UpdateItemForUser()` hàm này chạy **TRƯỚC** `m_equipBag.LoadFromDatabase()` /
  `m_propBag.LoadFromDatabase()` → lúc quét, túi còn rỗng.
- **Sửa**: bỏ lời gọi trong `LoadPlayerInfoFromDatabase`, chuyển xuống **cuối `UpdateItemForUser()`**
  (sau khi toàn bộ túi đã load), kèm push lại 707/1 + 707/2; thêm log chẩn đoán
  `ConvertElfEquipItems: player ... equipBag=.. propBag=.. linkPalConfig=..`.
- **Migration dữ liệu hiện tại** (`data/elf-work/server/migrate_user1_linhbao.sql`): chuyển 7 item
  Linh Bảo đang nằm trong túi user1 thành 7 record `Sys_Users_ElfEquip` (ID 1..7:
  48001 T4Q1, 48002 T4Q2, 48005 T3Q1, 48009 T1Q1, 48013 T2Q1, 48021 T5Q1, 48026 T6Q1) và ẩn
  các item đó trong túi (`IsExist=0`, `Place=-1`) — backup rows gốc ở
  `data/elf-work/server/user1_linhbao_bag_rows_backup.txt`. Nhờ vậy tab Linh Bảo có dữ liệu ngay
  lần login kế tiếp, không phụ thuộc thứ tự load túi.
- DLL: 3.341.824 bytes, sha256 `F5A8703B236314CEAEE0FCCE42F38D3980D5BBD703C0E634953A0AAB2D3FD354`,
  restart Road.Service **01:53:18**; config load 01:53:37; Error.log rỗng; health OK.
- Sau relog: `Sys_Users_ElfEquip` đã có 7 dòng, login push 707/2 (count + id/type/quality) → tab
  Linh Bảo hiển thị 7 món. Nếu vẫn trống, log `ConvertElfEquipItems:` sẽ cho biết số item từng túi.