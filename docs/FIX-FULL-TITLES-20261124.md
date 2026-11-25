# Fix lỗi “Full danh hiệu” hết hạn sau 1 ngày — 2026-11-24

**Trạng thái:** ĐÃ SỬA PANEL + ĐÃ REPAIR DB. Tool ẩn trong `Road.Service` đã sửa source và build thành công nhưng **chủ động không deploy**: từ 2026-11-25 chỉ dùng panel web `localhost:90`, Form1 desktop đã ẩn (`ShowGMForm=false`).

## 1. Triệu chứng

Bấm GM **Full danh hiệu** cho user, một thời gian sau (thực tế ~3 ngày) toàn bộ danh hiệu biến mất. Trước đó client vẫn hiển thị như còn hạn.

## 2. Nguyên nhân

`Sys_User_Rank.Validate` là **số ngày hiệu lực kể từ `BeginDate`**, không phải cờ 0/1:

- `Validate = 0` → vĩnh viễn
- `Validate = 1` → 1 ngày
- `Validate = N` → N ngày

Code xác thực: `Full Source\SqlDataProvider\SqlDataProvider.Data\UserRankInfo.cs`, `IsValidRank()`.

Tool GM cũ ghi `Validate = 1` và `EndDate = '2099-12-31'`:

- `Tools\GunnyPanel\src\GunnyPanel\Services\GmToolService.cs` — `TitlesAsync`
- `Full Source\Road.Service\Road.Service.Helper.Forms\_hidden\Form1.cs` — `button42_Click_AllTitles`

`EndDate` không tham gia kiểm tra hiệu lực. Server chỉ gửi `EndDate` cho client trong packet 34 (`AbstractPacketLib.SendUserRanks`), nên client tưởng title còn hạn đến 2099. Server chỉ lọc lại danh sách khi login (`PlayerRank.LoadFromDatabase`), vì vậy title chỉ biến mất khi relogin/restart.

## 3. Đã sửa

### 3.1 Panel

File: `Tools\GunnyPanel\src\GunnyPanel\Services\GmToolService.cs`.

- Đổi insert sang `Validate = 0` (vĩnh viễn).
- Pre-check đúng: `Validate = 0 AND IsExit = 1` thay vì `Validate = 1 AND EndDate > GETDATE()`.
- Bỏ `DELETE FROM Sys_User_Rank WHERE UserID = @uid`.
- Dùng `UPDATE` các title đã có + `INSERT ... WHERE NOT EXISTS` cho title còn thiếu, bọc `SET XACT_ABORT ON; BEGIN TRAN; ... COMMIT;`.
- Giữ nguyên title event/achievement và row ID; không xoá dữ liệu nếu lỗi.

### 3.2 Tool ẩn trong Road.Service

File: `Full Source\Road.Service\Road.Service.Helper.Forms\_hidden\Form1.cs`.

- Sửa cùng logic upsert, `Validate = 0`, có transaction.
- Đã build `Road.Service.csproj` Release: **0 error**.
- **Không deploy (quyết định 2026-11-25):** chỉ dùng panel web `localhost:90`; Form1 desktop đã ẩn bằng `ShowGMForm=false` trong `Service\Service\Road\Road.Service.exe.config`. Source fix giữ lại để dùng khi nào bật lại form desktop.
- Bản build `Road.Service.exe` 2026-11-24 nằm ở `Full Source\Road.Service\bin\Release\net472\`, không copy vào `Service\Service\Road\`.

### 3.3 Repair DB

Backup bảng:

```sql
SELECT * INTO dbo.Sys_User_Rank_bak_20261124 FROM dbo.Sys_User_Rank;
-- 3.624 row
```

Repair:

```sql
UPDATE dbo.Sys_User_Rank
SET Validate = 0
WHERE Validate = 1
  AND EndDate = '2099-12-31';
-- 3.607 row
```

Kết quả kiểm tra:

| Hạng mục | Kết quả |
|---|---|
| Row còn `Validate=1, EndDate=2099` | 0 |
| Tổng row `Sys_User_Rank` | 3.624 (không đổi) |
| User 8 | 766 row, 766 vĩnh viễn |
| Pre-check user 8 | Expected=766, Missing=0 |
| `HonorID` trỏ title hết hạn | 0 |

Kiểm tra lại ngày 2026-11-25 sau khi panel/server restart: vẫn **3.624 row**, **0 row cũ**, user 8 **766/766 vĩnh viễn**, `BrokenHonorRows = 0`.

## 4. Deploy panel

- Build Release: `GunnyPanel.dll` 1.041.920 bytes, 2026-11-24 10:57:21.
- Backup DLL cũ: `Tools\GunnyPanel\app\GunnyPanel.dll.bak-20261124`.
- Copy DLL mới vào `Tools\GunnyPanel\app`, restart panel.
- Deploy lần 1 (2026-11-24 10:58): PID 27816, port 90 LISTEN, `http://127.0.0.1:90/` → HTTP 200.
- Sau khi OpenCode server restart, panel được start lại lúc 2026-11-25 19:24: PID 35728, port 90 LISTEN, HTTP 200.
- Không đụng Road/Fighting/Center. Hiện Road/Fight/Center không chạy; không thuộc phạm vi fix này.

## 5. Rollback

### Panel

```powershell
Copy-Item 'Tools\GunnyPanel\app\GunnyPanel.dll.bak-20261124' 'Tools\GunnyPanel\app\GunnyPanel.dll' -Force
# restart GunnyPanel
```

### DB

```sql
UPDATE r
SET r.Validate = b.Validate,
    r.BeginDate = b.BeginDate,
    r.EndDate = b.EndDate,
    r.IsExit = b.IsExit
FROM dbo.Sys_User_Rank r
JOIN dbo.Sys_User_Rank_bak_20261124 b ON b.ID = r.ID;
```

Nếu code mới đã insert/xoá row, cần đối chiếu lại toàn bộ bảng backup thay vì chỉ update theo ID.

### Form1 / Road.Service

Source cũ đã backup:

- `Full Source\Road.Service\Road.Service.Helper.Forms\_hidden\Form1.cs.bak-20261124`

Nếu đã deploy Road.Service mới và cần rollback, copy lại `Road.Service.exe` cũ và restart stack.

## 6. Lưu ý

- Với `Validate = 0`, packet 34 gửi hạn `DateTime.Now.AddYears(1)` theo quy ước “vĩnh viễn” của server. Client có thể hiển thị “còn ~1 năm”, nhưng server coi là vĩnh viễn và mỗi lần login gửi lại mốc mới.
- Một số title đặc biệt do `SP_GetSingleUserRank` quản lý theo điều kiện (602, 6041/6042, 6120/6121, 6125, 6131–6133, 6152/6153...) có thể bị xoá/ẩn dù tool đã thêm. Đây là logic game, không phải lỗi hết hạn hàng loạt.
- User cần relogin để `PlayerRank.LoadFromDatabase()` nạp lại danh sách title.
