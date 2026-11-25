# ELF (Thần Hộ Mệnh) — audit server/DB/dữ liệu (trước & sau khi sửa)

Ngày: 2026-11-09 · Người thực hiện: Lead (thay teammate `elf-server-auditor` — teammate dừng giữa chừng,
các file phân tích thô nằm ở `data/elf-work/server/`). Tài liệu client: [`ELF-PROTOCOL.md`](ELF-PROTOCOL.md).
Tài liệu tính năng + cách test: [`ELF-FEATURES.md`](ELF-FEATURES.md).

## 1. Trạng thái 5 tab sau khi sửa

| Tab | Trước | Sau | Ghi chú |
|---|---|---|---|
| Thần Hộ Mệnh (tab 0) | Chạy được (664/1,2,3,4,6,7,10,13,15,16,17 có handler) | OK | Bỏ (664/7) giờ xoá DB thật; kích hoạt (664/10) lưu DB ngay |
| Kỹ năng (tab 1) | Học/dung hợp chạy nhưng: chọn sách sai bind, không trừ Xu khoá, khoá hết skill → exception, dung hợp vượt MaxSkillCount, elf phụ không mất thật | **OK** | `Skill.cs`, `Blend.cs` đã sửa |
| Triệu hồi (tab 2) | Server có handler 664/8 + box 12241/12242 trong `Shop_Goods_Box`; `SummonCount1/2` lưu ở `Sys_Users_Extra` | OK | Dữ liệu đủ; đã gửi Sách Thần test |
| Đổi (tab 3) | Shop 133 (24 goods) có trong `Shop` + `ShopItemList.xml`; `NewBuyGoodsHandler` đã có nhánh `ShopCondition.isElfShop` trừ Đá Thánh (`Extra.Info.Coin`) + push 664/14 | OK | Không cần sửa; coin user1=400 |
| Linh Bảo (tab 4) | **Hỏng hoàn toàn**: thiếu handler 664/18, 707 route sai, `Sys_Users_ElfEquip` không load/save, 707/1-2 gửi dữ liệu giả, 707/3-4 không có handler | **OK** | Đã thêm 5 file mới + sửa 6 file (xem ELF-FEATURES §3) |

## 2. Dữ liệu (đã kiểm tra trực tiếp DB)

| Bảng | Kết quả | Dùng cho |
|---|---|---|
| `Db_Tank_All.TS_LinkPalTemplate` | **1.500 dòng** = 6 Type × 5 Quality × 50 Level; cột Id,TemplateID,Type,Quality,Level,Attack,Defence,Agility,Lucky,Damage,Blood,SkillID,ItemTempId1,Param1,ItemTempId2,Param2 | cấu hình Linh Bảo (server đọc file XML cùng nội dung ở `WebSite\Request\TS_LinkPalTemplate.xml`; log xác nhận `loaded 1500 LinkPal rows, 30 item templates, 30 type/quality groups`) |
| `Db_Tank_All.Shop_Goods` (CategoryID=91) | 30 item 48001–48030 (Linh Bảo), MaxCount=1 | item trong túi → 707/3 |
| `Db_Tank_All.Shop_Goods` (CategoryID=86) | 177 item 451xx | sách kỹ năng → `GamePlayer.AddItem` chuyển thành `Sys_Users_SkillBooks` |
| `Db_Tank_All.Shop_Goods_Box` | có box ra 48001–48030, sách 451xx, 11241/11242, v.v. | nguồn Linh Bảo/sách từ hoạt động |
| `Db_Tank_All.Elf_Template_Info` | 18 dòng (6 hệ × 3–5 sao), `ElfType` 1..6, `MaxSkillCount` 5/6/8 | thuộc tính Thần Hộ Mệnh |
| `Db_Tank_All.Elf_SkillBook` | 26 dòng: 45101…; `Randoms`=700/250/50 (‰), `SkillTempId`, `Description` | tỉ lệ học sách |
| `Db_Tank.Sys_Users_Elf` | user1: 4 elf (0=Thần Nước TempID 33 = hệ 4, 5★; 1=13; 2=43; 3=23), elf 0 IsFight | có sẵn |
| `Db_Tank.Sys_Users_ElfEquip` | 0 dòng (trước đây chưa từng được dùng) | giờ được load/save bởi `GamePlayer` |
| `Db_Tank.Sys_Users_SkillBooks` | 0 dòng → tab Kỹ năng trống (đã gửi sách test) | |
| `Db_Tank.Sys_Users_Extra.Coin` | user1 = **400 Đá Thánh**; SummonCount1=10, SummonCount2=50 | tiền cho shop 133 / lượt triệu hồi |

Nguyên liệu nâng Linh Bảo theo hệ: Type1=12289 (Lửa), 2=12290 (Gió), 3=12288 (Đất), 4=12287 (Nước),
5=12293 (Sáng), 6=12294 (Tối); luôn cần 12286 Pha Lê Linh Hồn. Cấp càng cao càng tốn (xem XML).

## 3. `GameProperties.VERSION` — đã xác minh

`GameProperties.Load` đọc từ bảng `Server_Config` qua `SP_Server_Config_Single`: thủ tục trả các dòng cùng
`Name`, code `GetServerPropertyByKey` lấy **dòng cuối** trong reader. Bảng có 2 dòng `ClientVersion`
(ID 1554 = 10200, ID 1677 = 12100) → giá trị dùng là **12100** (khớp `Road.Service.exe.config` không override
và khớp việc client 12.6 luôn đọc các field guang/an + elfEquipId). Vì vậy mọi nhánh `if (VERSION >= 12100)`
trong code Elf là nhánh đang chạy — đúng như sửa đổi lần này.

## 4. Lỗi đã sửa (P0) — evidence

| # | Lỗi | File:line (trước) | Sửa |
|---|---|---|---|
| 1 | 707 route vào `ElfLogicProcessor` ⇒ 707/3 chạy như 664/3 EXERCISE, 707/4 như 664/4 ADD_GROWTH | `Game.Server.Packets.Client\ElfEquipHander.cs:15` | route sang `ElfEquipLogicProcessor` mới |
| 2 | Thiếu handler `ELF_EQUIP_OPERATE (18)` → KeyNotFoundException (log live 00:41) | `Game.Server.Elf\ElfLogicProcessor.cs:27`, `ElfHandleMgr.cs:13` | thêm `Handle\ElfEquipOperate.cs` |
| 3 | `Sys_Users_ElfEquip` không load/save ở bất kỳ đâu | `GamePlayer.cs` load ~4400 / save ~4595 | thêm load + loop save + `SaveElfDataToDatabase`/`DeleteElfFromDatabase` |
| 4 | 707/1 lấy ElfType của elf + level giả 1; 707/2 gửi elfID thay vì record ID; NRE nếu TempID lạ | `ElfPacketsOut.cs:268-298` | dựng lại từ `Sys_Users_ElfEquip` (level theo hệ, record ID, quality) |
| 5 | `SendUserEquip`: `sys_Users_Elf.EquipTempId` NRE khi không có elf ra trận | `AbstractPacketLib.cs:2303` (log live 00:39) | null-safe |
| 6 | `SendUserEquip`: ghi `WriteInt(0)` cho `lingStoneStr` nhưng client đọc `readUTF()` ⇒ lệch packet 71 | `AbstractPacketLib.cs:2313` | `WriteString("")` |
| 7 | Bỏ elf (664/7) không xoá DB; không chặn elf đang mặc Linh Bảo | `Discard.cs` | xoá DB qua EF, chặn `Elf.Discard.EquipOn` |
| 8 | Dung hợp (664/12): có thể vượt MaxSkillCount; elf phụ không xoá DB | `Blend.cs` | giới hạn `MaxSkillCount - lock`, xoá DB |
| 9 | Học skill (664/5): bỏ qua bind; không trừ Xu khoá; khoá hết skill → `ElementAt` exception | `Skill.cs` | chọn sách theo bind, trừ `ElfLockSkillPayMoney`, chặn khoá hết |
| 10 | Linh Bảo không cộng chỉ số nhân vật; packet 164 gửi `ElfEquip = 0` | `PlayerEquipInventory.cs`, `PlayerProperty.cs`, `GamePlayer.GetBaseDamage/GetBaseDefence`, `AbstractPacketLib.cs:1482/1522/1537` | thêm source `ElfEquip`, cộng Attack/Defence/Agility/Luck/HP + Damage/Armor total, gửi giá trị thật |

## 5. Việc còn lại (không chặn; P2)

- **Atlas kỹ năng** (tab Kỹ năng → Bộ sưu tập): thuần client (dữ liệu `TS_ElfSkillBook.xml`), không có packet
  → không cần server.
- **SkillID của Linh Bảo** (passive skill trong `TS_LinkPalTemplate.SkillID`): client hiển thị mô tả; chưa thấy
  server/battle engine đọc → chưa cần cho phạm vi "mở lại tab"; nếu muốn hiệu ứng thụ động cần đọc thêm
  battle `Game.Logic` (tương tự 42 element ID còn thiếu của pet).
- **Giá trị Linh Bảo tác động vào `totalDamage/totalArmor`** đã cộng ở `GetBaseDamage/GetBaseDefence`;
  nếu muốn hiển thị đúng 100% ở mọi dialog phụ khác (ví dụ tooltip so sánh) cần rà thêm các packet khác
  có cột `ElfEquip` (hiện client chỉ đọc ở 164 như liệt kê trong ELF-PROTOCOL §5).
- **Elf Reset (664/17)**: handler `ElfReset.cs` có sẵn, chưa test live (tính năng tẩy thuộc tính luyện) —
  cần thêm dữ liệu/test sau khi có item liên quan.
- Dữ liệu test đã gửi bằng mail `[ELF-TEST]` (30 thư); sau khi user test xong có thể xoá mail/record test.

## 6. Kiểm chứng

- Build 0 lỗi (MSBuild net472), `Game.Server.dll` sha256 `9EC7EFAFF21F2F81F84977946AFD7BCACA1EE1722DD9AC0EDAE509A367E20987`,
  deploy `Service\Service\Road`, restart 01:29:23 → ports 8527/9601 OK, health OK.
- Log khởi động: `ElfEquipConfigMgr: loaded 1500 LinkPal rows, 30 item templates, 30 type/quality groups`.
- `Error.log` sau restart: rỗng.

## 7. Hotfix sau live test 01:42 (2026-11-09)

- **`Skill.cs` bị 0 byte** sau patch tự động → handler code 5 không tồn tại (log live `PackageType:SKILL`
  KeyNotFound 01:33–01:36). Đã khôi phục file + verify reflection: handlers đăng ký = {2,3,4,5,6,7,8,10,11,12,13,16,17,18}.
- **Item Linh Bảo nằm trong túi chưa thành Linh Bảo**: luồng gốc yêu cầu double-click (707/3). Nay thêm
  auto-convert trong `GamePlayer.AddItem`/`StackItemToAnother` (CategoryID 91) và migration lúc login
  (`ElfMgr.ConvertElfEquipItems`) → item có sẵn trong túi của user1 (7 món 48001/48002/48005/48009/48013/48021/48026)
  sẽ tự thành record sau khi relog.
- Hotfix DLL sha256 `522D876D…6B9A`, restart 01:42:31; health OK, Error.log rỗng.
- Lưu ý vận hành: **KHÔNG dùng vòng lặp patch tự động thay thế string trên file source** như lần trước
  (đã làm hỏng `Skill.cs`); các lần sau dùng script ghi có kiểm tra kích thước/đọc lại file.