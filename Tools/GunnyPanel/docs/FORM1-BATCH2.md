# Form1.cs — Batch 2 (07/11/2026)

> **Bản 4 — Sổ tay Chí Tôn level 21, full cả bộ sưu tập vũ khí; deploy lúc 09:33.**
> Process: **PID 12908**, port `127.0.0.1:8527`, hash `0EE4B143…127278C4`.
> Backup: `Road.Service.exe.bak-20261107-093352`.
> Form1 đang mở sẵn; chỉ chạy 1 instance Road.Service.

## Sổ tay thám hiểm — Chí Tôn level 21

- `JampsManualItemList` có Level 21 = **Chí Tôn** (sau Vua Mạo Hiểm Lv4).
- Đã set:
  - `Sys_Users_ExplorerManualInfo.manualLevel = 21`
  - `progress = 1`, `maxProgress = 1`
  - `havePage = 45`, `jampsCurrency = 9.999.999`
  - `Sys_Users_ExplorerManual.ManualLevel = 21`
- 45 page + 630 debris đã full.
- User 1 hiện: `manualLevel=21` (Chí Tôn).

## Bộ sưu tập vũ khí — đã full

- Bổ sung nhóm **Sex = 3** (16 bộ vũ khí) vào `User_Avatar_Colection`.
- `ActiveCount` = **COUNT(*) toàn bộ dòng group** (kể cả dòng trùng TemplateID),
  `ActiveDress` tương ứng đủ số item; `endTime = 2099-12-31`.
- Kết quả user 1: **111 bộ** (58 nam + 37 nữ + 16 vũ khí), không bộ rỗng, không duplicate.
- Ví dụ bộ vũ khí: Nai tơ 18 item, Trổ tài 18, Uy chấn 18, ...


## Bộ sưu tập trang phục/vũ khí — full

- Nút **Phụ Kiện → Bộ Sưu Tập** (`button34`) đã có handler.
- Xoá collection cũ của user, tạo lại toàn bộ từ `Db_Tank_All.dbo.Cloth_Property_Template` + `Cloth_Group_Template`.
- Với mỗi bộ (95 bộ có item):
  - `ActiveCount` = số template cần.
  - `ActiveDress` = toàn bộ TemplateID của bộ (nam/nữ).
  - `endTime = 2099-12-31` → collection hợp lệ.
- Kết quả user 1: **95 collection**, `ActiveDress` đầy đủ, không bộ rỗng.
- User online cần relog để nạp lại bộ sưu tập.

## Sổ tay thám hiểm — sửa đúng bảng

- Bảng game thực tế đọc là **`Sys_Users_ExplorerManualInfo`** (số ít), không phải `Sys_Users_ExplorerManual`.
- Đã cập nhật:
  - `manualLevel = 20`
  - `progress = 1`, `maxProgress = 1`
  - `havePage = 45`
  - `jampsCurrency = 9.999.999`
- Kèm 45 page + 630 debris trong bảng con.
- User 1 hiện: `manualLevel=20, progress=1/1, havePage=45`; online reload dùng `InitExplorerManualInfo` đọc đúng bảng mới.


## Đã sửa lỗi login do album thẻ

Log cũ:
```
System.NullReferenceException: Can not found cardbooklet with templateId = 0 and profile = 5
 at Game.Server.CardNewSystem.CardNewSystemMgr.GetSuitValue(...)
 at Game.Server.GameObjects.GamePlayer.UpdateCardInfo()
...
```
Nguyên nhân: album cũ có thẻ `314150 (Hồn Thẻ)` không có dòng `Card_Template_Info.CardType = 4`;
khi login, `CardBag` zero-hoá thẻ không hợp lệ (`TemplateID=0, CardType=0`) → `GetSuitValue` crash.

Đã sửa:
- Chỉ insert thẻ có `INNER JOIN Db_Tank_All.dbo.Card_Template_Info` với `CardType = 4`.
- Loại `314150 (Hồn Thẻ)`; album còn **82 thẻ hợp lệ**.
- Xoá dòng lỗi `TemplateID=0/CardType=0` của user 1.
- Kiểm tra: `AlbumCards=82`, `BadRows=0`; `Error.log` hiện **rỗng**.


## Đã hoàn thành trong Batch 2

### 1. Ma Thạch — gửi mail khi chưa có
- Nếu user chưa có ma thạch: gửi **8× `100906`** qua hộp thư, **level 10**, full exp, đủ **6 chỉ số**.
- Nếu đã có: nâng ma thạch hiện có lên max exp/level + đủ 6 chỉ số.
- Đã sửa `Bussiness.PlayerBussiness.SendMailAndItem` (thêm `ApplyMagicStoneMailStats`) để mail item có
  `MagicAttack`, `MagicDefence`, `StrengthenExp` và 4 compose stats.
- Test: `StrengthenLevel=10, StrengthenExp=1134400, 6 chỉ số = 154`.
- Max level hiện tại của server là **10** (dữ liệu `Magicstone_Template`); muốn 20 phải bổ sung data + code.

### 2. Sổ Tay Thám Hiểm — full (hỗ trợ online)
- Nút **Sổ Tay Thám Hiểm → Tiến Hóa** (`button35`) đã được gắn handler.
- Cập nhật **`Sys_Users_ExplorerManualInfo`**: `manualLevel=20`, `progress=1`, `maxProgress=1`,
  `havePage=45`, `jampsCurrency=9999999`.
- Cập nhật thêm bảng `Sys_Users_ExplorerManual` (level 20/45 page) để đồng bộ.
- Bảng con: 45 trang active + 630 mảnh debris.
- Nếu user **đang online**: reload `ExplorerManualInfo` + pages/debris, gọi `UpdatePlayerProperties()` và
  `SendUpdatePublicPlayer()` → full ngay.
- Đã test user 1: level 20, progress 1/1, havePage 45, 45 page, 630 debris.

### 3. Album Thẻ — Full Album (mới)
- Thêm nút **Full Album** trong nhóm **Thẻ Bài** (`buttonFullAlbum`, cạnh Lên Cấp/Đột Phá).
- Xoá các thẻ album cũ ở slot >= 5, giữ nguyên 5 thẻ đang trang bị.
- Thêm **82 thẻ hợp lệ** (distinct `TS_CardBooklet.TemplateId` có `Card_Template_Info.CardType=4`),
  quality 4, level 30, stats lấy từ `Card_Template_Info`.
- Slot album: 5 → 86.
- Đã test SQL trên user 1: `AlbumCards=82`, `Templates=82`, `BadRows=0`.
- User đang online cần relog để client nạp lại album.

### 4. Tiến Hoá VK Phụ — full level
- Nút **Tiến Hóa VK Phụ → Tiến Hóa** (`button45`) đã được gắn handler.
- Online: lấy `Player.SecondWeapon`, set `curExp = max Exp` từ `SubWeaponEvolutionTemplateMgr`,
  cập nhật `StoreBag`, `UpdateProperties`, `SendUpdatePublicPlayer`.
- Offline: hiện báo cần online (chưa có đường lấy item VK phụ offline an toàn).

### 5. Các mục đã có sẵn trong Form1
- **Thẻ Bài**: `button16` (CardLevel 90 / stats 240), `button17` (SuitLevel 31).
- **Chiến Hồn**: `button28` — full `FigSpiritIdValue` cho 5 vị trí `{11,5,2,3,13}`.
- **Tăng Sao**: `button20/21/22` — set `UserEquipGhostInfo.Level = 10` cho Nón/Quần áo/Vũ khí.

## Chưa làm được (cần bạn cung cấp cơ chế/item)

### Tiềm Năng (Nguồn sức mạnh)
- Source server hiện **không có** class/API xử lý “Tiềm Năng” hay item `11689`.
- Cần biết: item nào dùng, field DB nào lưu tiềm năng, 6 món trang bị nào được nâng.
  Nếu chỉ cần gửi item, có thể gửi **6× `11689`**; nhưng “1 click full” cần logic server.

### Phụ Ma / Đúc Hồn
- Source server **không có** API/tên field Phụ Ma/Đúc Hồn; chỉ có Ghost/FigSpirit (Chiến Hồn).
- Cần bạn cho biết: field DB hoặc item liên quan (ví dụ `Hole5/6`, `FigSpiritIdValue`, ...).

## Hướng dẫn test
1. Mở Form1 (console Road → `gm`).
2. Chọn UserID.
3. **Sổ Tay Thám Hiểm → Tiến Hóa**: kiểm tra DB hoặc relogin xem full level 20/45 trang.
4. **Tiến Hóa VK Phụ → Tiến Hóa**: user phải online và đang trang bị VK phụ.
5. **Ma Thạch**:
   - Chưa có ma thạch → nhận 8 mail `100906` level 10, đủ 6 chỉ số.
   - Đã có → nâng full exp/level + đủ 6 chỉ số.
