# HƯỚNG DẪN THÊM / SỬA QUÀ TÂN THỦ (Gunny BK 13.0)

> Tài liệu cho session mới: mọi thứ cần biết để thêm – bớt – sửa quà tân thủ,
> reset trạng thái nhận, và kiểm tra kết quả.
> Cập nhật: 2026-11-20.

---

## 0. Môi trường

| Thành phần | Giá trị |
|---|---|
| Thư mục server | `C:\Gunny\Gunny12.5` |
| SQL Server | `DESKTOP-SMLR4VU` — `sa / 123456` |
| DB chính (game/road) | `Db_Tank` |
| DB cross-zone | `Db_Tank_All` |
| Website Request (client tải XML) | IIS site `Request` — `http://127.0.0.1:88` |
| Website chính | IIS site `Gunny` — `http://127.0.0.1` → `C:\Gunny\Gunny12.5\Website` |
| Road GM API | `http://127.0.0.1:9601` — token: `Tools\GunnyPanel\app\data\road-gm-api-token.txt` |
| CenterService SOAP | `http://127.0.0.1:4109/CenterServicePhP/` |
| ActivityID của Quà Tân Thủ | **`2026110701`** |

Công cụ dòng lệnh dùng trong tài liệu:

```bat
sqlcmd -S DESKTOP-SMLR4VU -U sa -P 123456 -d Db_Tank -C -i file.sql
```

> `-C` bắt buộc (ODBC 18 yêu cầu trust server certificate).

---

## 1. QUAN TRỌNG: quà tân thủ có **2 nguồn dữ liệu**

| Đường nhận | Ai gọi | Đọc bảng | Ghi trạng thái |
|---|---|---|---|
| **Trong game** (nút *Nhận* ở “Hoạt động hấp dẫn”) | client → web `ActivePullDown.ashx` → SP `SP_Active_PullDown` | **`Db_Tank.dbo.Active_Award`** (ActiveID = 2026110701) | `Active_Number` (AwardID = `2026110701-3-<UserID>`) |
| **GM API / panel** (`/gm/apply {feature:"newbie_gift"}`) | `ActivePullDownHandler.GrantNewbieGiftForUser` (Road) | **`Db_Tank.dbo.Sys_NewbieGiftItems`** | `Sys_Users_NewbieGift` + `Active_Number` |

### Hệ quả

* Muốn quà **đầy đủ ở cả 2 đường** → phải cập nhật **CẢ HAI bảng**.
* Quên `Active_Award` → bấm *Nhận* trong game **không có thư nào** (nhưng vẫn bị đánh dấu “đã nhận”).
* Quên `Sys_NewbieGiftItems` → test/grant qua GM API không có món mới.
* **Mô tả quà** (`Active.AwardContent`) client đọc từ **`Db_Tank_All`** (Web.config: `ServerData` → `Db_Tank_All`, `PlayerData` → `Db_Tank`; `ActiveList.ashx` dùng `ServerData`).
  → Đặt cùng một nội dung ở **cả `Db_Tank.dbo.Active` và `Db_Tank_All.dbo.Active`**.
* `Db_Tank_All.dbo.Active_Award` là bản cũ, **SP không đọc**; nhưng nên mirror cho khớp cho dễ so sánh.
* Quà được gửi **qua hộp thư**, mỗi thư **5 món** (`WorldEventMgr.SendItemsToMail`, SP cũng 5 món/mail).

---

## 2. Cấu trúc 2 bảng quà

### 2.1 `Db_Tank.dbo.Sys_NewbieGiftItems` (nguồn GM API)

| Cột | Ý nghĩa |
|---|---|
| `TemplateID` | ID vật phẩm (`Shop_Goods.TemplateID`) |
| `Count` | số lượng tặng |
| `StrengthenLevel`, `AttackCompose`, `DefendCompose`, `AgilityCompose`, `LuckCompose` | cường hoá / chỉ số ghép |
| `IsGold`, `GoldValidDate` | dát vàng |
| `SortOrder` | thứ tự (chỉ để sắp xếp) |

Bảng **không có** cột ID; mỗi TemplateID chỉ nên xuất hiện 1 lần.

### 2.2 `Db_Tank.dbo.Active_Award` (nguồn claim trong game)

| Cột | Giá trị dùng cho quà |
|---|---|
| `ID` | **không phải identity** → phải tự đánh số |
| `ActiveID` | `2026110701` |
| `ItemID` | = TemplateID |
| `Count` | số lượng |
| `ValidDate` | 0 |
| `StrengthenLevel`, `AttackCompose`, `DefendCompose`, `LuckCompose`, `AgilityCompose` | như trên |
| `Gold`, `Money` | 0 |
| `Sex` | **0** (cả nam & nữ) |
| `Mark` | **0** |
| `IsGold`, `GoldValidDate` | dát vàng |

SP lọc `WHERE ActiveID=@ActiveID AND (Sex=@sex OR Sex=0) AND Mark=@Mark`.

---

## 3. Quy trình thêm vật phẩm (5 bước)

### Bước 1 — Backup

```sql
USE Db_Tank;
IF OBJECT_ID('bak_newbie_items_20261231') IS NULL
    SELECT * INTO bak_newbie_items_20261231 FROM Sys_NewbieGiftItems;
IF OBJECT_ID('bak_active_award_20261231') IS NULL
    SELECT * INTO bak_active_award_20261231 FROM Active_Award WHERE ActiveID = 2026110701;
```

### Bước 2 — Thêm vào `Sys_NewbieGiftItems`

```sql
DECLARE @TemplateID int = 12262;   -- ID vật phẩm muốn thêm
DECLARE @Count      int = 999999;

INSERT INTO Sys_NewbieGiftItems
    (TemplateID, [Count], StrengthenLevel, AttackCompose, DefendCompose,
     AgilityCompose, LuckCompose, IsGold, GoldValidDate, SortOrder)
SELECT @TemplateID, @Count, 0,0,0,0,0, 0, 0,
       ISNULL((SELECT MAX(SortOrder) FROM Sys_NewbieGiftItems), 0) + 1
WHERE NOT EXISTS (SELECT 1 FROM Sys_NewbieGiftItems WHERE TemplateID = @TemplateID);
```

> Trang bị (MaxCount = 1): để `Count` = số món (1, 2, 4…) — **KHÔNG** để 999999
> (handler sẽ nhân bản thành 999999 món riêng lẻ).

### Bước 3 — Đồng bộ sang `Active_Award`

Cách nhanh nhất: dựng lại toàn bộ từ `Sys_NewbieGiftItems` (an toàn, ID tự đánh lại):

```sql
USE Db_Tank;
DELETE FROM Active_Award WHERE ActiveID = 2026110701;

INSERT INTO Active_Award
    (ID, ActiveID, ItemID, [Count], ValidDate, StrengthenLevel, AttackCompose,
     DefendCompose, LuckCompose, AgilityCompose, Gold, Money, Sex, Mark, IsGold, GoldValidDate)
SELECT ROW_NUMBER() OVER (ORDER BY SortOrder, TemplateID) AS ID,
       2026110701, TemplateID, [Count], 0, StrengthenLevel, AttackCompose,
       DefendCompose, LuckCompose, AgilityCompose, 0, 0, 0, 0, IsGold, GoldValidDate
FROM Sys_NewbieGiftItems;
```

Kiểm tra: `SELECT COUNT(*) FROM Active_Award WHERE ActiveID=2026110701;` phải bằng số dòng `Sys_NewbieGiftItems`.

### Bước 4 — Giới hạn stack (`Shop_Goods.MaxCount`)

Nếu `Count` của quà **lớn hơn** `Shop_Goods.MaxCount` thì khi vào túi sẽ bị **chia nhiều ô**
(hoặc cắt). Muốn 1 ô chứa đủ:

```sql
UPDATE s SET s.MaxCount = 999999
FROM Shop_Goods s JOIN Sys_NewbieGiftItems g ON g.TemplateID = s.TemplateID
WHERE s.MaxCount > 1 AND g.[Count] > s.MaxCount;

-- kiểm tra còn món nào vượt giới hạn
SELECT g.TemplateID, s.Name, g.[Count], s.MaxCount
FROM Sys_NewbieGiftItems g JOIN Shop_Goods s ON s.TemplateID = g.TemplateID
WHERE g.[Count] > s.MaxCount AND s.MaxCount > 1;
```

> Chỉ nâng `MaxCount` cho item **stack được** (`MaxCount > 1`).
> Trang bị `MaxCount = 1` phải giữ nguyên.

### Bước 5 — Mô tả quà (`Active.AwardContent`)

> **Kiểm tra kiểu cột trước** — đã gặp trường hợp cột vẫn là `nvarchar(2000)` (bị cắt
> giữa dòng). Mở rộng nếu cần:

```sql
-- nếu max_length = 4000 (2000 ký tự) thì mở rộng
ALTER TABLE Db_Tank.dbo.Active ALTER COLUMN AwardContent nvarchar(max) NULL;
-- Db_Tank_All.dbo.Active hiện đã là nvarchar(max)
```

```sql
-- ví dụ: mỗi item 1 dòng, bắt đầu bằng "- "
UPDATE Active
SET AwardContent =
    N'Quà Tân Thủ:' + CHAR(10) +
    N'- Dùi Huyền Tinh x999.999' + CHAR(10) +
    N'- Ngọc-May Mắn Lv1 x1' + CHAR(10) +
    N'- ...'
WHERE ActiveID = 2026110701;
```

* Cột là `nvarchar(max)` (đã mở rộng — bản gốc chỉ 2000 ký tự).
* **Phải ghi vào cả 2 DB** (`Db_Tank` và `Db_Tank_All`) — client đọc bản `Db_Tank_All`.
* Dùng **CHAR(10)** (LF) — không dùng `CHAR(13)+CHAR(10)` để tránh `&#xD;` trong XML.
* Client render trong `activeEvents.award_txt` (wordWrap = true) → xuống dòng hiển thị đúng.

---

## 4. Ngọc / Đá: đi thẳng vào **túi Ngọc** (không qua thư)

32 loại ngọc/đá (CategoryID 89, TemplateID **47001–47037**) được SP
`SP_Active_PullDown` chèn thẳng vào `Sys_User_Gemstone` (túi Ngọc – khảm/tăng cấp),
`EquipPlace = 0`, level 1, chuỗi `1,0,0|1,0,1|1,0,2`.

### Quy tắc

1. **KHÔNG** đưa ngọc/đá vào `Sys_NewbieGiftItems` / `Active_Award`
   → nếu có, chúng sẽ vào túi thường qua mail.
2. Nếu thêm **loại ngọc/đá mới** (TemplateID ngoài 47001–47037) thì phải sửa block này
   trong SP:

```sql
-- xem block hiện tại (nằm ngay trước "Commit Tran")
SELECT OBJECT_DEFINITION(OBJECT_ID('SP_Active_PullDown'));
```

Block:

```sql
IF @ActiveID = 2026110701
BEGIN
    INSERT INTO Sys_User_Gemstone (UserID, FigSpiritId, FigSpiritIdValue, EquipPlace)
    SELECT @UserID, v.TemplateID, '1,0,0|1,0,1|1,0,2', 0
    FROM (VALUES (47001),(47002), /* ... */ (47037)) AS v(TemplateID)
    WHERE NOT EXISTS (SELECT 1 FROM Sys_User_Gemstone g
                      WHERE g.UserID = @UserID AND g.FigSpiritId = v.TemplateID);
END
```

> `Sys_User_Gemstone.ID` là identity → không cần chèn ID.
> `EquipPlace = 0` = trong túi; 1–8 = đã khảm vào lỗ ngọc.

---

## 5. Reset trạng thái “đã nhận” để test lại

```sql
USE Db_Tank;
DECLARE @uid int = 8;   -- UserID nhân vật test

DELETE FROM Sys_Users_NewbieGift WHERE UserID = @uid;
DELETE FROM Active_Number
WHERE AwardID = '2026110701-3-' + CAST(@uid AS nvarchar(20));
```

Xoá luôn thư + item đính kèm (nếu muốn test sạch):

```sql
-- item đính kèm nằm trong Sys_Users_Goods với UserID = 0
DELETE g FROM Sys_Users_Goods g
WHERE g.UserID = 0 AND g.ItemID IN (
    SELECT TRY_CAST(Annex1 AS int) FROM User_Messages WHERE ReceiverID = @uid
    UNION SELECT TRY_CAST(Annex2 AS int) FROM User_Messages WHERE ReceiverID = @uid
    UNION SELECT TRY_CAST(Annex3 AS int) FROM User_Messages WHERE ReceiverID = @uid
    UNION SELECT TRY_CAST(Annex4 AS int) FROM User_Messages WHERE ReceiverID = @uid
    UNION SELECT TRY_CAST(Annex5 AS int) FROM User_Messages WHERE ReceiverID = @uid
);

DELETE FROM User_Messages WHERE ReceiverID = @uid;
```

Xoá túi (tuỳ chọn):

```sql
DELETE FROM Sys_Users_Goods WHERE UserID = @uid;
DELETE FROM Sys_User_Gemstone WHERE UserID = @uid;
DELETE FROM Sys_Users_ListBox WHERE UserID = @uid;
```

> Nên **kick nhân vật** trước khi sửa dữ liệu túi (server giữ inventory trong RAM,
> logout sẽ ghi đè lại). Xem mục 6.

---

## 6. Kick nhân vật (bắt buộc trước khi sửa túi)

```powershell
$soap = '<?xml version="1.0" encoding="utf-8"?>' +
'<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body>' +
'<KitoffUser xmlns="http://tempuri.org/"><playerID>8</playerID><zoneId>1001</zoneId>' +
'<msg>GM cap nhat du lieu - vui long dang nhap lai</msg></KitoffUser>' +
'</soap:Body></soap:Envelope>'

Invoke-WebRequest -Uri 'http://127.0.0.1:4109/CenterServicePhP/' -Method POST `
  -ContentType 'text/xml; charset=utf-8' `
  -Headers @{ SOAPAction = '"http://tempuri.org/ICenterService/KitoffUser"' } `
  -Body ([Text.Encoding]::UTF8.GetBytes($soap)) -UseBasicParsing
```

Kiểm tra: `SELECT State FROM Sys_Users_Detail WHERE UserID=8;` → `0` = offline.

---

## 7. Áp dụng thay đổi cho server

> ⚠️ **CẢNH BÁO đã kiểm chứng (2026-11-20):** gửi `Reload("active|...")` sẽ làm Road ghi lại
> cấu hình active vào `Db_Tank` và **xoá trắng `Db_Tank.Active_Award`** (→ bấm *Nhận* trong game
> không có thư) đồng thời **cắt `Db_Tank.Active.AwardContent` còn 2000 ký tự**.
> `Db_Tank_All` **không** bị ảnh hưởng.
>
> **Quy trình đúng:** sửa DB → build lại `ActiveList.xml` → reload/restart Road (nếu cần)
> → **chạy lại Bước 3 (mirror `Active_Award`) + Bước 5 (AwardContent)** cho `Db_Tank` → kiểm tra.
> Nếu không cần Road nạp lại `ActiveInfo` thì **đừng reload** sau khi đã sửa DB.

### 7.1 Reload nhẹ (không cần restart)

Gửi `Reload("active|<md5>")` tới CenterService:

```powershell
$loginKey = 'QY-16-WAN-0668-2555555-7ROAD-dandantang-trminhpc773377'
$md5 = [BitConverter]::ToString(
    [Security.Cryptography.MD5]::Create().ComputeHash(
        [Text.Encoding]::UTF8.GetBytes('active' + $loginKey)
    )).Replace('-','').ToLower()

$soap = '<?xml version="1.0" encoding="utf-8"?>' +
'<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body>' +
"<Reload xmlns=`"http://tempuri.org/`"><type>active|$md5</type></Reload>" +
'</soap:Body></soap:Envelope>'
```

### 7.2 Restart Road (chắc ăn hơn)

```powershell
# stop
Get-Process Road.Service -ErrorAction SilentlyContinue | Stop-Process -Force
# start
Start-Process 'C:\Gunny\Gunny12.5\Service\Service\Road\Road.Service.exe' `
  -WorkingDirectory 'C:\Gunny\Gunny12.5\Service\Service\Road' -WindowStyle Minimized
```

Hoặc dùng script có sẵn: `C:\Gunny\Gunny12.5\restart-road.ps1` (cần Administrator).

Kiểm tra sau restart:

```powershell
foreach($p in 7539,8527,9601,4109,1593){
  '{0}: {1}' -f $p, $(if(Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue){'LISTEN'}else{'ĐÓNG'})
}
```

> **Lưu ý:** SP `SP_Active_PullDown` được SQL Server biên dịch lại ngay khi ALTER —
> không cần restart. Reload/restart chỉ cần khi muốn Road nạp lại `ActiveInfo`
> (mô tả, thời gian) hoặc để chắc chắn.

---

## 8. Kiểm tra kết quả

### 8.1 Dữ liệu

```sql
-- 2 nguồn phải khớp số dòng
SELECT (SELECT COUNT(*) FROM Db_Tank.dbo.Sys_NewbieGiftItems)          AS GM_API,
       (SELECT COUNT(*) FROM Db_Tank.dbo.Active_Award
        WHERE ActiveID = 2026110701)                                  AS InGame;

-- món vượt giới hạn stack (phải = 0)
SELECT COUNT(*) FROM Sys_NewbieGiftItems g
JOIN Shop_Goods s ON s.TemplateID = g.TemplateID
WHERE g.[Count] > s.MaxCount AND s.MaxCount > 1;
```

### 8.2 Mô tả hiển thị cho client

```powershell
Invoke-WebRequest 'http://127.0.0.1:88/ActiveList.ashx' -UseBasicParsing   # → Build: ActiveList.xml, Success!
```

File `Website\Request\ActiveList.xml` là **zlib** — giải nén để đọc `AwardContent`.

### 8.3 Test gửi quà qua GM API

```powershell
$tok = (Get-Content 'C:\Gunny\Gunny12.5\Tools\GunnyPanel\app\data\road-gm-api-token.txt' -Raw).Trim()
Invoke-RestMethod -Uri 'http://127.0.0.1:9601/gm/apply' -Method POST `
  -Headers @{ 'X-GM-Token' = $tok } -ContentType 'application/json' `
  -Body '{"userId":8,"feature":"newbie_gift"}'
```

Kết quả mong đợi: `ok:true`, `message:"Đã gửi Quà Tân Thủ vào hộp thư."`
Sau đó kiểm tra mail:

```sql
SELECT TOP 5 ID, Title, SendTime, Annex1Name, Annex2Name, Annex3Name
FROM User_Messages WHERE ReceiverID = 8 ORDER BY ID DESC;
```

### 8.4 Test claim trong game

Reset cờ (mục 5) → đăng nhập → bấm **Nhận**. Kiểm tra:

```sql
SELECT COUNT(*) FROM User_Messages WHERE ReceiverID = 8;                      -- số thư quà
SELECT COUNT(*) FROM Sys_User_Gemstone WHERE UserID = 8 AND EquipPlace = 0;   -- ngọc/đá trong túi Ngọc
SELECT COUNT(*) FROM Active_Number WHERE AwardID = '2026110701-3-8';          -- phải = 1 sau khi nhận
```

---

## 9. Danh sách quà hiện tại (tham chiếu)

* **9 món đặc biệt** — `Count = 999999`, `MaxCount = 999999`:
  `12262` Dùi Huyền Tinh · `12264` Đá Khắc · `11030–11033` Đá Chu Tước/Huyền Vũ/Thanh Long/Bạch Hổ 5 ·
  `12249` Phỉ Thúy · `12250` Mã Não · `12251` Hắc Diệu.
* **Món thêm 2026-11-20** (thêm vào `Sys_NewbieGiftItems` + `Active_Award`):

  | TemplateID | Tên | Count | MaxCount |
  |---|---|---|---|
  | `12269` | Đá Dung Hợp | 9.999 | 999.999 |
  | `201567` | Đá Manh Hóa | 999.999 | **999.999** (đã nâng từ 99.999) |
  | `11165` | Linh Châu Kỹ Năng | 99.999 | 9.999.999 (giữ nguyên) |
  | `12286` | Pha Lê Linh Hồn | 999.999 | **999.999** (đã nâng từ 99.999) |
  | `12287` | Linh Nguyên-Nước | 99.999 | 99.999 (đủ, không chia ô) |

* **Ngọc/Đá Lv1** (47001–47037): 1 viên/loại → vào **túi Ngọc**, không qua thư.
* **Trang bị** (MaxCount = 1): giữ `Count` gốc (1/2/4…).
* Tổng số dòng quà hiện tại: **289** (284 + 5 món thêm 2026-11-20; sau khi tách 32 ngọc/đá sang túi Ngọc).

> `Active.AwardContent` có thể dài hơn 284 dòng — cột đã là `nvarchar(max)`.

---

## 10. Lỗi thường gặp & cách xử lý

| Hiện tượng | Nguyên nhân | Cách sửa |
|---|---|---|
| Bấm *Nhận* trong game không có thư nào | `Active_Award` rỗng/thiếu dòng | Chạy Bước 3 (đồng bộ lại) |
| Bấm *Nhận* báo “đã nhận rồi” | còn cờ `Active_Number` | Xoá theo mục 5 |
| Qua GM API thiếu món mới | chưa thêm vào `Sys_NewbieGiftItems` | Bước 2 |
| Món vào túi bị chia nhiều ô | `MaxCount` < `Count` | Bước 4 |
| Sau khi `Reload("active")` mất `Active_Award` / mô tả bị cắt 2000 ký tự | Road ghi lại cấu hình active vào `Db_Tank` | Chạy lại Bước 3 + Bước 5 (mục 7) |
| Ngọc/đá nằm ở túi thường | đã thêm vào 2 bảng quà | Xoá khỏi 2 bảng; giữ trong block SP |
| Ngọc/đá không vào túi Ngọc | block gem trong SP thiếu loại đó | Sửa block (mục 4) |
| Mặc đồ từ quà bị lỗi `FormatException` (latentEnergy) | `latentEnergyCurStr` rỗng | `UPDATE Sys_Users_Goods SET latentEnergyCurStr='0,0,0,0', latentEnergyNewStr='0,0,0,0' WHERE ISNULL(latentEnergyCurStr,'')=''` |
| Mô tả quà 1 dòng | `AwardContent` không có `CHAR(10)` | Dùng `+ CHAR(10) +` giữa các dòng |
| Mô tả có ký tự lạ `&#xD;` | dùng CRLF | Chỉ dùng `CHAR(10)` |

---

## 11. File / script liên quan trong repo

| File | Mục đích |
|---|---|
| `Database\fix-markscheme-permanent.sql` | Fix vĩnh viễn lỗi `GetUserMarkScheme` (trigger) |
| `Database\fix-pet-skill-equip.sql` | (Tham khảo) auto-gắn kỹ năng pet — **không dùng**, đã hoàn lại |
| `restart-road.ps1` | Restart Road (cần Administrator) |
| `Tools\GunnyPanel\app\data\road-gm-api-token.txt` | Token gọi GM API |
| `Tools\GunnyXmlFix\XmlFileHandler.cs` | Handler thay thế 4 `.ashx` XML (pet skill/magic stone/subweapon) |
| `Website\Request\_fixed_xml\` | Bản XML đầy đủ trường cho 4 handler trên |

---

## 12. Checklist nhanh (copy cho session mới)

- [ ] Backup `Sys_NewbieGiftItems` + `Active_Award` (mục 3.1)
- [ ] Thêm/sửa dòng trong `Sys_NewbieGiftItems`
- [ ] Đồng bộ `Active_Award` (chạy lại toàn bộ bằng ROW_NUMBER) — **cả `Db_Tank` và `Db_Tank_All`**
- [ ] Nâng `Shop_Goods.MaxCount` nếu `Count` lớn hơn
- [ ] Cập nhật `Active.AwardContent` (mỗi item 1 dòng, `CHAR(10)`) — **cả 2 DB**, nhớ mở rộng cột thành `nvarchar(max)`
- [ ] Ngọc/đá: chỉ nằm trong block SP → `Sys_User_Gemstone`
- [ ] Build lại `ActiveList.xml` (`http://127.0.0.1:88/ActiveList.ashx`)
- [ ] Reload `active` hoặc restart Road (nếu cần) → **sau đó chạy LẠI Bước 3 + Bước 5** vì reload xoá `Active_Award` và cắt `AwardContent` còn 2000 ký tự
- [ ] Reset cờ + xoá thư của nhân vật test (kick trước)
- [ ] Test bằng GM API và/hoặc bấm *Nhận* trong game
- [ ] Kiểm tra mail / túi Ngọc / `Active_Number`

---

## 13. Xoá bớt hoạt động khác (chỉ giữ Quà Tân Thủ)

`ActiveList.xml` lấy danh sách từ **`Db_Tank_All.dbo.Active`** (Web.config: `ServerData`).
Xoá hoạt động = xoá dòng trong `Active` của **cả 2 DB** rồi build lại XML.

### 13.1 Đã thực hiện 2026-11-20 (chỉ còn `2026110701`)

Đã xoá 5 dòng:

| ActiveID | Title |
|---|---|
| `76885` | Hướng Dẫn Nạp Xu |
| `77198` | CODE Xem Live Fanpage T10 |
| `77201` | CODE Khảo Sát GunnyPC |
| `77202` | CODE Xem Live Fanpage T11 |
| `77203` | CODE Fan Cứng Tháng 11 |

Backup: `Db_Tank.dbo.bak_active_20261120_2140` và `Db_Tank_All.dbo.bak_active_20261120_2140` (6 dòng).

### 13.2 Quy trình xoá / khôi phục

```sql
USE Db_Tank;
-- backup
SELECT * INTO bak_active_YYYYMMDD FROM Active;
SELECT * INTO Db_Tank_All.dbo.bak_active_YYYYMMDD FROM Db_Tank_All.dbo.Active;

-- xoá mọi hoạt động trừ Quà Tân Thủ
DELETE FROM Active_Award  WHERE ActiveID <> 2026110701;
DELETE FROM Active_Number WHERE ActiveID <> 2026110701;
DELETE FROM Db_Tank_All.dbo.Active_Award WHERE ActiveID <> 2026110701;
DELETE FROM Active              WHERE ActiveID <> 2026110701;
DELETE FROM Db_Tank_All.dbo.Active WHERE ActiveID <> 2026110701;
```

Khôi phục 1 hoạt động đã xoá:

```sql
INSERT INTO Active SELECT * FROM bak_active_20261120_2140 WHERE ActiveID = 77203;
INSERT INTO Db_Tank_All.dbo.Active
SELECT * FROM Db_Tank_All.dbo.bak_active_20261120_2140 WHERE ActiveID = 77203;
```

Sau đó **build lại XML** (`http://127.0.0.1:88/ActiveList.ashx`) và kiểm tra:
XML phải đúng số `<Item`; mở panel *Hoạt động hấp dẫn* trong game (có thể phải đăng nhập lại/clear cache client).

> ⚠️ **KHÔNG reload/restart Road sau khi xoá** nếu chưa sẵn sàng chạy lại Bước 3 + Bước 5 (xem mục 7):
> reload sẽ xoá `Active_Award` và cắt `AwardContent` còn 2000 ký tự.

### 13.3 Hệ thống hoạt động KHÁC (chưa đụng tới)

Các bảng/XML dưới đây thuộc màn khác, không nằm trong `ActiveList.xml`:

| Hệ thống | Nơi lưu | Ghi chú |
|---|---|---|
| Hoạt động GM (“Thánh Kiếm Theia”, “Quà Nạp Ma Pháp”…) | `Db_Tank_All.dbo.GM_Activity` (66 dòng) + `GM_Gift`, `GM_Active_Reward` | build ra `GmActivityInfo.xml` |
| Event cấu hình (`ts_activityconfig.xml`) | sinh từ server (handler `ts_activityconfig`) | 5 item: 弹王决斗场, 矿洞寻宝, 星灵礼遇, 绿动奇迹, 薪火同燃 |
| Hoạt động cộng đồng | `Db_Tank_All.dbo.Communal_Active*` (ActiveID 1, 4) | |
| `vng-quest\ActiveList.xml` | file sync từ `quest.gn.zing.vn` | **không** phải file client dùng; job sync có thể ghi đè |

### 13.4 Xoá “Quà Nạp Ma Pháp” (2026-11-20)

* **Nguồn**: `Db_Tank_All.dbo.GM_Activity` — `activityId = 8c578177-8ec9-1aaa-e449-8e4e74ad434b`
  (`activityType=0`, `activityChildType=6`, 05/11 → 02/12/2026). Không có dòng nào trong
  `GM_Gift` / `GM_Active_Reward` / `GM_Active_Condition` / `User_GmActivity*`.
* **Client hiển thị từ XML**: `GmActivityInfo.xml` (build bằng `http://127.0.0.1:88/GmActivityInfo.ashx`).
  Danh sách bên trái do client tự dựng bằng `WonderfulActivityManager.checkActivity()` duyệt `activityData`
  (XML) → **packet 405 chỉ chứa trạng thái**, không chứa tên/mô tả. Vì vậy xoá dòng DB + build lại XML là đủ,
  **không cần restart Road** (client phải đăng nhập lại để nạp lại XML).
* **Backup**: `Db_Tank_All.dbo.bak_gmactivity_20261120_2145` (66 dòng).
* Kết quả: `GM_Activity` còn **65** dòng, `GmActivityInfo.xml` còn **65** `<Activity`, không còn “Quà Nạp Ma Pháp”.

Khôi phục:

```sql
INSERT INTO Db_Tank_All.dbo.GM_Activity
SELECT * FROM Db_Tank_All.dbo.bak_gmactivity_20261120_2145
WHERE activityId = '8c578177-8ec9-1aaa-e449-8e4e74ad434b';
```
rồi build lại: `http://127.0.0.1:88/GmActivityInfo.ashx`.

> Muốn xoá **toàn bộ** hoạt động GM: `DELETE FROM Db_Tank_All.dbo.GM_Activity;` (nhớ backup trước) +
> build lại XML. Không ảnh hưởng Quà Tân Thủ (`Active` / `Active_Award`).
