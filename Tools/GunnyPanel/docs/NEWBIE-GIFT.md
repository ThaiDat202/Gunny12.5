# Quà Tân Thủ — Hoạt động hấp dẫn (nhận 1 lần)

> Trạng thái: **đã dựng xong hạ tầng, chờ user test in-game** (2026-11-07 21:42).
> Phần 3 nhóm item chưa rõ template (biến hình thú 70 / bùa thú truyền thuyết / biến hình PET 36)
> đang chờ user gửi danh sách template ID.

## 1. Cách hoạt động

- **Client**: file `C:\Gunny\Gunny12.6\WebSite\Request\ActiveList.xml` (zlib) chứa danh sách
  hoạt động của màn "Hoạt động hấp dẫn → Giờ Event".
  - Đã thêm entry: `ActiveID="2026110701"`, `Title="Quà Tân Thủ"`, `HasKey="3"`, `IsOnly="1"`,
    `Type="1"`, thời gian 2026-11-07 → 2035-12-31 (do DATEDIFF giây quá lớn sẽ overflow nếu 2099).
  - Backup bản gốc: `ActiveList.xml.bak-YYYYMMDD-HHMMSS`.
  - Nén/giải nén: `_tools/tankxml.py decompress|compress` (zlib level 9).
- **Server claim**: client gửi **packet 11** (`sendActivePullDown`) khi bấm "Nhận thưởng".
  - Handler mới: `Full Source\game.server\Game.Server.Packets.Client\ActivePullDownHandler.cs`.
  - Nếu `activeID == 2026110701`: kiểm tra bảng `Db_Tank.dbo.Sys_Users_NewbieGift`;
    nếu chưa có → ghi nhận (mỗi nhân vật 1 lần) → build **42 item** theo đúng thông số
    (TemplateID, count, strengthen, 4 chỉ số compose, IsGold/goldValidDate) →
    `WorldEventMgr.SendItemsToMails` (mỗi mail tối đa 5 item) → gửi message
    `ActiveBussiness.Msg0` (Nhận thành công).
  - Nếu đã có → gửi message mặc định `ActiveBussiness.Msg6`
    ("Bạn đã nhận phần thưởng này rồi.") — đúng lựa chọn "chỉ báo lỗi mặc định" của user.
- **DB cấu hình (dự phòng/đối chiếu)**: bảng `Active` + `Active_Award` cũng đã có row
  (ActiveID 2026110701, 42 award) — hiện handler không dùng tới vì custom claim,
  giữ để tra cứu; có backup `Active_bak_20261107`, `Active_Award_bak_20261107`.
- **SP_Active_PullDown** đã được mở rộng thêm `IsGold`/`GoldValidDate` cho `Active_Award`
  (handler custom không dùng, nhưng giữ tương thích nếu sau này dùng lại đường SP).

## 2. Danh sách quà hiện tại (42 item)

| Nhóm | Template | Thông số |
|---|---|---|
| Nón | 1779, 1880 | +15, ATK/DEF/AGI/LUCK +50, dát vàng 365 ngày |
| Áo | 5740, 5856 | +15, +50×4, dát vàng 365 ngày |
| Vũ khí | 7196 | +15, +50×4, dát vàng 365 ngày |
| Nhẫn | 95484 ×2 | +50×4 |
| Vòng tay | 85484 ×2 | +50×4 |
| Nhẫn Cầu Hôn | 9522 | +50×4 |
| Vũ khí phụ | 17017, 17016 | +15, +50×4 |
| Kính | 2360, 2461 | count 1 |
| Tóc | 3351, 3465 | count 1 |
| Mặt | 4360, 4461 | count 1 |
| Mắt | 6308, 6414 | count 1 |
| Set quần áo | 13689, 13688 | count 1 |
| Dây chuyền | 14057, 14045 | count 1 |
| Cánh | 15178 | count 1 |
| Bong bóng | 16991 | count 1 |
| Huy chương | 50202, 50309, 50505 | count 1 |
| Bùa hộ thân | 390101 | count 1 |
| PET | 35250101, 35210201, 35220101 | count 1 |
| Thức ăn PET | 334103 | ×999.999 |
| VK/Nón/Áo PET | 30108/30107/30105, 31108/31107/31105, 32108/32107/32105 | count 1 |
| Thần Hộ Mệnh | 124313 | count 1 |

Chờ bổ sung: full phụ kiện biến hình thú (70), full bùa thú truyền thuyết (mỗi loại 3),
full phụ kiện biến hình PET (36) — cần template ID.

## 3. File/backup

- `_tools/newbie-gift/items.json` — danh sách item cấu hình.
- `_tools/newbie-gift/patch_activelist.py` — script thêm entry vào ActiveList.xml (đã chạy).
- `_tools/tankxml.py` — công cụ nén/giải nén XML client.
- Backup: `ActiveList.xml.bak-*`, `Game.Server.dll.bak-newbie-*`, các bảng `*_bak_20261107`.
- Handler: `Game.Server.Packets.Client/ActivePullDownHandler.cs`, đã build
  `game.server` và deploy `Game.Server.dll` (3.222.528 bytes) vào Road, restart Road.

## 4. Test

1. Đăng nhập game, mở **Hoạt động hấp dẫn → Giờ Event** phải thấy mục **Quà Tân Thủ**.
2. Bấm **Nhận thưởng** → nhận mail (khoảng 9 mail × 5 item); kiểm tra item:
   nón/áo/vũ khí +15, 4 chỉ số 50, viền vàng 365 ngày; nhẫn/vòng tay +50; PET...
3. Bấm lại → nhận message mặc định "Bạn đã nhận phần thưởng này rồi." (không nhận thêm).
4. Reset test cho 1 nhân vật:
   `DELETE FROM Db_Tank.dbo.Sys_Users_NewbieGift WHERE UserID=<id>;`
   và xoá mail/goods tương ứng nếu muốn test lại.

## 5. Đã kiểm thử nội bộ (2026-11-07 21:46)

- Gọi thẳng logic handler qua Road API (`feature=newbie_gift`, dùng chung hàm
  `ActivePullDownHandler.GrantNewbieGiftForUser`) cho user test2:
  - Lần 1: OK → tạo **9 mail** "Quà Tân Thủ" (Type 83), **44 item** (42 template, nhẫn/vòng tay ×2).
    Verify DB: nón/áo/vũ khí `StrengthenLevel=15`, 4 compose = 50, `IsGold=1`,
    `goldValidDate=365`; VK phụ +15/+50; ring/bracelet +50; PET/Thần Hộ Mệnh đủ.
  - Lần 2: trả đúng message `ActiveBussiness.Msg6` = **"Bạn đã nhận phần thưởng này rồi."**,
    không tạo thêm mail; bảng `Sys_Users_NewbieGift` = 1 dòng.
- Đã dọn toàn bộ dữ liệu test của user2 sau khi kiểm tra.
- Bug đã fix trong quá trình test (để handler chạy được):
  - `EquipBag` không liên quan; lỗi chính: `AddGoods` NRE do item thiếu cột chuỗi NOT NULL
    (`Skin/Color/latentEnergyCurStr/latentEnergyNewStr`) → đã set `""`.
  - Các cột datetime NOT NULL phải khác `DateTime.MinValue` → set `latentEnergyEndTime`,
    `AdvanceDate`, `RemoveDate` = thời điểm hiện tại (+50 năm cho latent).
- Road `Error.log` sau test: sạch.
- Client: `GET http://127.0.0.1:88/ActiveList.xml` đã trả entry `ActiveID=2026110701`,
  `Title="Quà Tân Thủ"` (port 88 = Request folder). Chỉ cần tải lại client để thấy.

## 6. Còn chờ user

1. Đăng nhập game → mở Hấp dẫn kiểm tra mục **Quà Tân Thủ** và bấm Nhận thưởng.
2. Gửi template ID cho 3 nhóm item còn thiếu để bổ sung vào danh sách quà.

## 7. Cấu hình quà từ DB (round 2) — thêm item không cần build lại

- Bảng mới `Db_Tank.dbo.Sys_NewbieGiftItems` (TemplateID PK, Count, StrengthenLevel,
  AttackCompose/DefendCompose/AgilityCompose/LuckCompose, IsGold, GoldValidDate, SortOrder).
- Handler đọc danh sách từ bảng này (thay vì hard-code) → khi bổ sung 3 nhóm item còn thiếu
  chỉ cần INSERT vào bảng, **không cần rebuild Game.Server.dll**.
- Sau khi gửi mail, handler gọi `CenterServiceClient.MailNotice(userId, zoneId)` để client
  đang online thấy mail mới ngay.
- Đã test lại (user2): 9 mail / 44 item; item đúng +15/+50/gold 365; lần 2 báo
  "Bạn đã nhận phần thưởng này rồi."; đã dọn dữ liệu test sau khi kiểm tra.
- Road `Error.log` sạch; Road API 36 feature, panel OK.

## 8. Bổ sung 3 nhóm item còn thiếu (round 3) — tự suy ra từ DB game

- **70 item biến hình thú**: toàn bộ `Db_Tank_All.dbo.Mount_Draw_Template.TemplateId`
  (các “Thẻ phụ kiện …” - category 11, ví dụ 11971 Kiến Lửa … 12965 Nguyệt Ảnh Đồng).
- **36 item biến hình PET**: toàn bộ `Db_Tank_All.dbo.Pet_Form_Data.TemplateID`
  (các “Thẻ triệu hồi …” - category 62, 62001…62039).
- **Bùa thú truyền thuyết**: 11 item CategoryID=68, Quality=6 (Bùa Truyền thuyết, Bùa Bạo Kích/Bạo Thương/
  Bảo Vệ/Kháng Bạo/Miễn Thương/Phá Giáp/POW/Tất Kháng/Tốc Độ - Truyền thuyết, Ấn Bùa Truyền Thuyết),
  mỗi loại **×3**.
- Đã thêm vào `Sys_NewbieGiftItems` (không cần build lại): tổng **159 template**.
- Test lại handler (user2): **33 mail**, **161 item**; đủ 70 mount + 36 petform + 11 charm (×3);
  sau test đã dọn sạch dữ liệu user2.
- Nếu user muốn danh sách bùa thú khác (ví dụ chỉ 9 loại, bỏ Ấn Bùa) hoặc item biến hình thú/PET
  theo template khác → chỉ cần sửa bảng `Sys_NewbieGiftItems` (không rebuild).

## 9. Round 4 — sửa đường claim HTTP (SP) + chống nhận 2 lần chéo

- Phát hiện `SP_Active_PullDown` gốc bị lỗi: không có tham số `@Result` (PlayerBussiness gọi sẽ lỗi),
  các `select 0..10` không `RETURN` nên claim lần 2 vẫn có thể chạy tiếp và phát item.
- Đã backup SP gốc (`data/_activity_debug/SP_Active_PullDown_original.sql`) và sửa:
  - Thêm `@Result int output`.
  - Mọi nhánh lỗi/đã nhận → `Set @Result=<code> Return` (7=chưa bắt đầu, 8=hết hạn,
    2=user sai, 6=đã nhận, 1/4/5/10... giữ nguyên ý nghĩa gốc).
  - Nhờ vậy website `ActivePullDown.ashx` (nếu client dùng đường này) nhận đúng mã và message
    `ActiveBussiness.Msg6` = "Bạn đã nhận phần thưởng này rồi."
- Nhân bản đủ **159 item** từ `Sys_NewbieGiftItems` → `Active_Award` (kèm IsGold/GoldValidDate)
  để đường SP/`.ashx` cũng phát đúng toàn bộ quà, không chỉ 42 món.
- Chống nhận 2 lần giữa 2 đường claim:
  - Handler packet 11 kiểm tra cả `Sys_Users_NewbieGift` **và** `Active_Number`;
     khi nhận thì ghi vào cả 2 bảng.
  - SP vẫn kiểm tra `Active_Number` như cũ → nếu đã nhận qua packet thì SP trả 6, và ngược lại
    nếu đã nhận qua SP thì packet trả "Bạn đã nhận phần thưởng này rồi.".
- **Kiểm thử**:
  - API claim trước → `Sys_Users_NewbieGift=1`, `Active_Number=1`; gọi SP → `@Result=6` ✅
  - SP claim trước → 33 mail/161 item; gọi API → báo đã nhận, **không** thêm mail ✅
  - Đã dọn dữ liệu test2.
- Client cache: thêm `Cache-Control: no-cache` cho static file trong `WebSite/Request/Web.config`
  (`<clientCache cacheControlMode="DisableCache" />`) và mirror entry Quà Tân Thủ sang
  `Request/vng-quest/ActiveList.xml` — đảm bảo client tải bản mới.

## 10. Round 5 — sửa đúng DB cấu hình: `Db_Tank_All` (không phải Db_Tank)

- **Nguyên nhân chính khiến client không thấy event**: chức năng "Hoạt động" (ActiveList) lấy dữ liệu
  từ **`Db_Tank_All.dbo.Active` / `Active_Award`** (crosszone DB), không phải `Db_Tank`.
  `ActiveList.ashx` rebuild `WebSite/Request/ActiveList.xml` từ DB crosszone này.
  - DB crosszone ban đầu chỉ có `ActiveID=1` ("Quà Vui Vẻ"), `Active_Award` trống.
- Đã mirror sang `Db_Tank_All`:
  - `Active`: thêm row `ActiveID=2026110701` ("Quà Tân Thủ", HasKey=3, IsOnly=1, HasKey..., 2026→2035).
  - `Active_Award`: thêm **159 dòng** đúng thông số (ItemID/Count/Strengthen/4 compose/IsGold/GoldValidDate),
    thêm cột `IsGold`, `GoldValidDate` cho bảng crosszone.
- Chạy lại `http://127.0.0.1:88/ActiveList.ashx` (tool chính thức) → file `ActiveList.xml` hiện chứa **cả 2**
  hoạt động: `Quà Tân Thủ` + `Quà Vui Vẻ` (đã kiểm tra nội dung giải nén).
- Claim vẫn dùng **`Db_Tank`** (SP `SP_Active_PullDown` + bảng `Active/Active_Award` local) —
  đã cấu hình đầy đủ ở cả 2 DB nên cả đường packet 11 và HTTP `.ashx` đều phát quà đúng.
- Đã test lại HTTP claim: lần 1 OK + 33 mail, lần 2 báo đã nhận; dữ liệu test đã dọn.

## 11. Round 7 — XÁC NHẬN IN-GAME + xử lý mất item do túi đầy

- **User1 đã claim thành công trong game lúc 22:01:49** (ngày 2026-11-07):
  - `Active_Number` có dòng `2026110701-3-1` (UserID=1, PullDown=1).
  - Có 19 mail Type52 "Bạn nhận được phần thưởng hoạt động!" + 35 mail Type8
    "Hành trang đã đầy. Gửi vào thư." (overflow khi túi đầy), kèm item đã vào túi.
- **Phát hiện**: lúc claim, túi user1 rất đầy nên luồng "Nhận tất cả" của game làm **thất lạc 45 món**
  cuối (5 mount + 28 petform + 11 charms + 62004). Đã đối chiếu chính xác danh sách thiếu.
- **Đã bù đủ 45 món còn thiếu** cho user1 qua mail (9 mail, Title "Quà Tân Thủ (bù)",
  Content nhắc dọn túi trước khi nhận) → kiểm tra hợp (túi + mail): **159/159 template**.
- **Kiểm tra claim lần 2** (HTTP, user1): trả đúng
  `<Result value="true" message="Bạn đã nhận phần thưởng này rồi." />` (không phát thêm mail).
- Cập nhật `Active.Content` (cả 2 DB) thêm ghi chú: **"hãy dọn túi trước khi nhận mail để tránh mất vật phẩm"**;
  rebuild lại `ActiveList.xml` (vẫn giữ Quà Vui Vẻ + Quà Tân Thủ).
- Chạy lại `_tools/newbie-gift/verify.ps1`: **RESULT: ALL CHECKS PASSED**.

## 12. Cập nhật 2026-11-07 (theo yêu cầu) — đổi trứng PET & Thần Hộ Mệnh

- **Trứng PET**: bỏ `35210201` ("Tôn Ngộ Không 6" - bị lỗi), thay bằng
  **`35210101` ("Tôn Ngộ Không 5")**.
- **Thần Hộ Mệnh**: bỏ `124313` ("Thần Hộ Mệnh 5 Sao-Tự Chọn"), thay bằng **4 hồn 5 sao**:
  - `1122190` Hồn Lửa 5 Sao
  - `1122193` Hồn Nước 5 Sao
  - `1122196` Hồn Gió 5 Sao
  - `1122199` Hồn Đất 5 Sao
- Cấu hình `Sys_NewbieGiftItems`: **162 dòng**; đã mirror `Active_Award` sang **cả 2 DB**
  (`Db_Tank`, `Db_Tank_All`) và rebuild `ActiveList.xml` bằng `ActiveList.ashx`;
  cập nhật nội dung `Content/AwardContent` (ghi rõ PET Tôn Ngộ Không 5 sao + 4 hồn).
- **User1**:
  - Chuyển 2 trứng Tôn Ngộ Không 6 trong túi thành **Tôn Ngộ Không 5** (cùng ItemID).
  - Gửi mail "Quà Tân Thủ (thay Thần Hộ Mệnh)" chứa **4 hồn 5 sao**
    (do item `124313` của user1 không còn trong túi/mail — có thể đã dùng trước đó).
- **Test claim mới (user2)**: 33 mail / 164 item; có `35210101` + 4 hồn; **không còn**
  `35210201` và `124313`; sau test đã dọn dữ liệu user2.
- `_tools/newbie-gift/items.json` cập nhật 162 dòng; `verify.ps1` cập nhật và chạy:
  **RESULT: ALL CHECKS PASSED**.

## 13. Sửa lỗi mail hồn 5 sao bị rỗng (2026-11-07 22:2x)

- **Nguyên nhân**: lúc dọn dữ liệu test user2, câu xóa test
  `DELETE FROM Sys_Users_Goods WHERE UserID=0 AND Place=-1` đã xóa nhầm **goods đính kèm mail của user1**
  (goods trong mail có UserID=0, Place=-1) → mail còn Annex ID nhưng item không tồn tại nên client hiện ô rỗng.
- **Khắc phục**:
  - Xóa các mail `Quà Tân Thủ%` / mail bị hỏng của user1 (annex trỏ tới goods không tồn tại).
  - Tính lại toàn bộ template còn thiếu (túi + mail hợp lệ) → tạo lại goods + mail **"Quà Tân Thủ (bù 2)"**
    (5 item/mail, có `Remark` chuẩn GiftToken/Annex như mail hệ thống).
  - Kết quả: user1 có **162/162 template** (túi + mail), **0 mail hỏng**; 4 hồn 5 sao nằm trong mail
    (ItemID 2887-2890: Lửa/Nước/Gió/Đất).
  - Trứng PET trong túi user1: `35210101` (Tôn Ngộ Không 5) — đã thay xong.
- **Lưu ý quy trình**: các lần dọn test sau chỉ xóa goods theo **mail ItemID/Annex cụ thể của user test**,
  không xóa toàn bộ `UserID=0 AND Place=-1`.

## 14. Fix hệ thống Thần Hộ Mệnh (Elf) — 2026-11-07 22:5x

- **Log Road.Service** khi user kích hoạt hồn:
  `Game.Server.Elf.ElfLogicProcessor PackageType:ACTIVATE_ELF ... KeyNotFoundException at ElfMgr.AddElf`.
- **Nguyên nhân 1 (crash)**: `ElfMgr.AddElf` lấy `GuangGrowth`/`AnGrowth` từ **sai dictionary**
  (`dictionary` thay vì `dictionary2`) → `KeyNotFoundException`.
  → Đã sửa: `Gui`/`AnGrowth = dictionary2[...]`; build lại `Game.Server.dll`, deploy, restart Road.
- **Nguyên nhân 2 (mất item)**: `ActivateElf` gọi `player.RemoveTemplate(...)` **trước** `ElfMgr.AddElf`;
  khi AddElf lỗi, item đã bị xóa → user mất 4 hồn.
  → Đã sửa thứ tự: tạo Elf trước (try/catch), chỉ xóa item khi tạo thành công.
- **Đã trả lại 4 hồn cho user1** vào túi PET (BagType=1): ItemID 2892-2895 (Lửa/Nước/Gió/Đất),
  đặt ở place 4/5/6/1 (không trùng).
- `Sys_Users_Elf` hiện 0 dòng → sau khi user thử lại, kỳ vọng tạo được Elf và item vào Thần Hộ Mệnh.
- Ghi chú: đây là fix source game server (`Full Source/game.server/Game.Server.Elf/ElfMgr.cs` +
  `Game.Server.Elf.Handle/ActivateElf.cs`), đã deploy **Game.Server.dll** mới và restart Road.

## 15. Cập nhật 2026-11-08 09:3x — bỏ bùa khỏi quà + xác nhận logic Phụ Ma/Đúc Hồn

### Quà Tân Thủ
- Bỏ 2 template khỏi `Sys_NewbieGiftItems`:
  - `1120560` — Bùa Truyền thuyết (×3)
  - `1120712` — Ấn Bùa Truyền Thuyết (×3)
- Còn lại **160 template**; 9 loại bùa thú truyền thuyết (Bạo Kích/Bạo Thương/Bảo Vệ/Kháng Bạo/
  Miễn Thương/Phá Giáp/POW/Tất Kháng/Tốc Độ) ×3 vẫn giữ.
- Mirror `Active_Award` sang cả 2 DB (160 dòng/DB), cập nhật AwardContent ("bùa thú truyền thuyết (9 loại) x3"),
  rebuild `ActiveList.xml`; `verify.ps1` cập nhật và chạy **ALL CHECKS PASSED**.
- Không còn "Quà Vui Vẻ" trong XML (đã xoá trước đó).

### Phụ Ma / Đúc Hồn — xác nhận logic theo yêu cầu
- **Không có** bất kỳ Nhẫn/Vòng tay/Nhẫn cầu hôn nào trong túi đồ (BagType 0):
  - Panel: `CountOwnedRingsAsync` = 0 → báo **"Bạn chưa có nhẫn/vòng tay."** (không gọi API).
  - Road API (nếu gọi trực tiếp): `EnsureRingsEquipped` = 0 → **"Bạn chưa trang bị nhẫn và vòng tay."**
- **Có** item (đã trang bị HOẶC đang nằm trong túi): tool tự trang bị vào ô 7/8/9/10/16
  (tùy nhóm: Nhẫn/Vòng tay/Nhẫn cầu hôn), sau đó nâng:
  - Phụ Ma → `MagicLevel=40`, `MagicExp=298100` (max Lv40)
  - Đúc Hồn → `enchantSpiritLevel=20` (max)
  - `RefreshProperties` gọi `EquipBag.SaveToDatabase()` → lưu DB ngay, không mất khi relog/restart.
- Đã bỏ yêu cầu mới về đá 11166/12214 theo chỉ đạo (không sửa).

## 16. Fix luật ô Nhẫn Cầu Hôn (2026-11-08 10:00)

- **Bug gốc**: `SqlDataProvider.Data.Equip.isWeddingRing` trả về **true cho mọi nhẫn**
  (`default: return true`) → server cho phép nhẫn thường (95484…) vào ô **Nhẫn Cầu Hôn (16)**.
  → Đã sửa: chỉ 9022, 9122, 9222, 9322, 9422, 9522 (Vật Định Tình +1..+5) là wedding ring, còn lại false.
- **Auto-trang-bị Phụ Ma/Đúc Hồn** giờ chia ô đúng:
  - Vòng tay (cat 8/28) → ô **7, 8**
  - Nhẫn thường (cat 9/29, không phải Vật Định Tình) → ô **9, 10**
  - Vật Định Tình → **chỉ ô 16** (Nhẫn Cầu Hôn)
- Đã build `SqlDataProvider.dll`, `Game.Server.dll`, `Road.Service.exe`; **chờ deploy** (Road đang chạy
  quyền Administrator nên file bị khóa — cần user đóng Road rồi deploy + restart).
- Quà Tân Thủ: đã đúng theo ý user (160 món, chỉ còn Quà Tân Thủ trong Hấp dẫn).

## 17. Deploy + test live (2026-11-08 10:1x)

- Đã deploy `SqlDataProvider.dll` (fix isWeddingRing), `Game.Server.dll`, `Road.Service.exe`;
  Road chạy quyền thường (PID 21876), log sạch.
- **Test live trên user5 “hahaa” đang online**:
  - `phuma` / `duchon` tự trang bị đúng ô:
    - Ô 7, 8: Vòng tay/Bao tay (category 8)
    - Ô 9, 10: Nhẫn thường (category 9, không phải Vật Định Tình)
    - Ô 16: **Vật định tình+5** (template 9522) — duy nhất
    - Item dư (1 vòng tay, 1 nhẫn) vẫn nằm trong túi do hết ô — đúng.
  - DB lưu ngay: `MagicLevel=40`, `MagicExp=298100`, `enchantSpiritLevel=20` cho các nhẫn đã trang bị.
  - `amulet_level` → bùa StrengthenLevel 50, StrengthenExp 10000.
  - `potential`, `avatar` trả OK live.
- **Luật mới**: nhẫn thường không được vào ô Nhẫn Cầu Hôn (16); chỉ Vật Định Tình (+1..+5).
- Quà Tân Thủ giữ nguyên (160 món), Hấp dẫn chỉ còn Quà Tân Thủ.

### ⚠️ Lưu ý deploy DLL (bổ sung sau sự cố 08/11 10:44)

- Bước deploy ở trên đã copy nhầm `SqlDataProvider.dll` (bản build từ `Full Source`) vào
  `WebSite\bin` + `WebSite\Request\Bin` → web ném `MissingMethodException ... set_FightPower(Int64)`
  và cửa sổ Guild hiện hộp tips URL. Đã khôi phục DLL web + viết lại handler
  `ConsortiaRichRankList.ashx`; xem [`FIX-GUILD-WINDOW-ERROR.md`](FIX-GUILD-WINDOW-ERROR.md).
- Từ nay: chỉ deploy `SqlDataProvider.dll` / `Game.Server.dll` / `Road.Service.exe` cho
  `Service\Service\Road`; **không** copy DLL build vào bin của web. Kiểm tra sau deploy bằng
  `_tools\verify-web-dlls.ps1`.


## 18. Khôi phục Quà Tân Thủ sau khi import rip data mới của NPH (2026-11-10)

Dữ liệu NPH ghi đè làm mất phần hiển thị, trong khi cấu hình gốc vẫn còn:
- `Db_Tank_All.dbo.Active` bị thay 5 hoạt động mới, **mất row `ActiveID=2026110701`**.
- `WebSite/Request/ActiveList.xml` bị thay bằng file mới (không có Quà Tân Thủ).
- `Db_Tank_All.dbo.Active_Award` **vẫn còn đủ 160 dòng** của Quà Tân Thủ.
- `Db_Tank` vẫn còn: `Active` (1 row), `Active_Award` (160), `Sys_NewbieGiftItems` (160),
  `Sys_Users_NewbieGift`, SP_Active_PullDown đã có fix RETURN + IsGold.
- `Game.Server.dll` mới vẫn chứa `ActivePullDownHandler` + `Sys_NewbieGiftItems` (build từ source còn handler).

Đã khôi phục:
1. Insert lại row `ActiveID=2026110701` vào `Db_Tank_All.dbo.Active` (copy từ `Db_Tank.dbo.Active`).
2. Chạy `http://127.0.0.1:88/ActiveList.ashx` rebuild `ActiveList.xml`
   → XML hiện có **6 hoạt động**: 5 của NPH + **Quà Tân Thủ** (IsShow=1, 2026→2035).
3. Test lại claim:
   - Packet/API (`newbie_gift`): 33 mail, claim 1 lần; lần 2 báo "Bạn đã nhận phần thưởng này rồi.".
   - HTTP `ActivePullDown.ashx`: 33 mail; lần 2 báo "Bạn đã nhận phần thưởng này rồi.".
   - Đã dọn dữ liệu test user2 sau kiểm tra.
- Lưu ý: sau mỗi lần import data NPH, chỉ cần chạy lại script `_tools/newbie-gift/readd_after_import.ps1`
  (insert Active row + rebuild XML) — không cần build lại server.
- Claim tracking: `Sys_Users_NewbieGift` hiện trống (import làm mất claim cũ) → mọi nhân vật claim lại 1 lần.

## 19. Cập nhật quà Tân Thủ 191 món (2026-11-10 11:3x)

### Thay PET equipment cũ bằng PET Lv4
Bỏ 9 món cũ: 30105, 30107, 30108, 31105, 31107, 31108, 32105, 32107, 32108.
Thêm 9 món:
- Vũ khí: `160108` Lửa Hỏa Khuyển Lv4, `160107` Tử Điện Lv4, `160105` Giáp Hoàng Kim Lv4
- Nón: `161107` Mũ Tứ Tượng Lv4, `161105` Nón Lông Phượng Lv4, `161108` Giáp Hỏa Khuyển Lv4
- Áo: `162105` Gậy Như Ý Lv4, `162107` Giáp Ngộ Đạo Lv4, `162108` Giày Hỏa Khuyển Lv4

### Phụ kiện thú cưỡi — thêm 17 (đủ full 87/87)
`12966` Khổng Tước, `12964` Gấu Ú Tắm Nắng, `12967` Rồng Vương, `12969` Xe Cổ Tích,
`12971` Hắc Kỳ Mã, `12972` Chổi Bay Rực Rỡ, `12973` Xe Hoa Tiên, `12985` Giỏ Trứng Thần Kỳ,
`12993` Sách Ma Thuật, `14904` Lưu Ly Sứa, `14902` Hắc Hổ Hỏa Ngục, `12999` Xe Vua Gà,
`14906` Hỏa Diệm Mèo Đen, `14905` Quỷ Thỏ Đêm, `14910` Cá Lân May Mắn,
`14914` Ghế Nhạc Thiên Hoa, `14918` Mộng Miêu Tinh Tú.

### Thẻ biến hình PET — thêm 14 (đủ full 50/50)
`62038` Gà Đầu Gấu, `62040` Sư Tử Rồng, `62041` Tiểu Rồng Xanh, `62043` Nàng Tiên Cá,
`62044` Đom Đóm, `62045` Bạch Tuộc Hồng, `62048` Kỳ Lân Trắng, `62049` Snow Baby,
`62050` Thú Mộng Mơ, `62053` Tiểu Mộng Linh, `62057` Hổ Con U Linh, `62055` Băng Tinh Sư,
`62061` Kỳ Lân Cầu Vồng, `62063` Gà Quý Tộc.

Cấu hình: **191 dòng**; mirror `Active_Award` 2 DB (191); AwardContent cập nhật;
rebuild ActiveList.xml. Test claim user2: 39 mail / 193 goods; 9 PET mới, 17 mount, 14 pet card,
**0** PET cũ; đã dọn.

### Lưu ý vận hành
- Claim cũ đã được reset (user 1,3,4,5,7) để nhận bộ quà mới; hiện claim table trống.
- Khi dọn test **không xóa goods theo TemplateID toàn cục** (đã từng làm hỏng mail chưa mở);
  chỉ xóa theo Annex của đúng user/mail test. Đã xóa 142 mail hỏng phát sinh.

## 20. Cập nhật 2026-11-10 12:3x — rollback PET + thay trang bị

- **PET equipment**: rollback 9 món Lv4 (160105/160107/160108, 161105/161107/161108, 162105/162107/162108)
  → trả lại 9 món cũ (30105/30107/30108, 31105/31107/31108, 32105/32107/32108) với **số lượng 2**.
- Giữ nguyên: **87/87 phụ kiện thú cưỡi**, **50/50 thẻ biến hình PET**.
- Thay trang bị (giữ count/prop cũ):
  - `7196` LEGEND-Thánh Kiếm Theia → **`7340` MYTHIC-Phượng Âm Cửu Thiên** (+15, 4 chỉ số +50, dát vàng 365 ngày)
  - `95484` Nhẫn 14 Tuổi +10 → **`95784` Nhẫn 17 Tuổi +10** (×2, +50×4)
  - `85484` Vòng tay 14 Tuổi +10 → **`85784` Vòng Tay 17 Tuổi +10** (×2, +50×4)
  - `14057` Dây Chuyền 14 Tuổi → **`14060` Dây Chuyền 17 Tuổi**
  - `15178` Ngân Hà Rực Rỡ → **`15215` Cánh 17 Tuổi**
  - `16991` Bong Bóng 14 Tuổi → **`169136` Bong Bóng 17 Tuổi**
  - `17017` LEGEND-Thiên Sứ Ban Phúc → **`17105` LEGEND-Thiên Sứ Băng Tuyết** (+15, +50×4)
- Cấu hình vẫn **191 dòng**; mirror 2 DB (191/191); AwardContent cập nhật; rebuild XML.
- Test claim user2: 7340 đúng +15/+50/gold365; 95784/85784 mỗi loại 2 món +50;
  17105 +15/+50; 14060/15215/169136 có mặt; PET cũ mỗi loại 2 món; **không còn** PET Lv4/trang bị cũ.
  Đã dọn user2 bằng script scoped (không xóa goods toàn cục). `verify.ps1` ALL CHECKS PASSED.
