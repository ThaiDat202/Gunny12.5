# BẢN SAO LƯU — BK Gunny 13.0 (2026-11-21)

> Bản sao đầy đủ để chạy lại game khi bản chính gặp sự cố.
> Tạo lúc: **2026-11-21 ~21:50** · Nguồn: `C:\Gunny\Gunny12.5`
> Đích: `C:\Gunny\Gunny12.5` · Dung lượng: **~1,72 GB** (18.054 file)

---

## 1. Nội dung bản sao

| Thành phần | Ghi chú |
|---|---|
| `Service\` | 3 service: `Road.Service.exe`, `Center.Service.exe`, `Fighting.Service.exe` + DLL mới nhất |
| `Website\` | Web chính (client tải `2.png`, `4.png`) + `Website\Request` (XML/API) |
| `Tools\GunnyPanel\app\` | Panel GM (đã build bản mới nhất, `appsettings.json` giữ nguyên) |
| `Full Source\` | Toàn bộ source C# của service/website (để build lại) |
| `Database\` | **8 bản backup SQL** (xem mục 2) |
| `RUN-SERVICES.bat` | Start cả 3 service (chạy Administrator) |
| `RUN-127.0.0.1-90.bat` | Start panel GM |
| `Service\Service\Run.bat` | Start service (bản gốc của service) |

### Đã lược bỏ khỏi bản sao

| Loại | Chi tiết |
|---|---|
| Thư mục `Database\` cũ | 10,4 GB file `.bak` cũ + script rời (đã thay bằng 8 bản backup mới ở mục 2) |
| Log | Toàn bộ `*.log`, thư mục `logs\` |
| Backup rời | `*.bak`, `*.bak-*`, `*.before-*` (kể cả `.bak-fp-*`) |
| Script dev | 62 file `*.ps1` (giữ duy nhất `Tools\GunnyPanel\app\data\mail-notice.ps1` vì panel gọi lúc chạy) |
| Batch dev | 3 file `.bat` (giữ **3 file cần để chạy**: `RUN-SERVICES.bat`, `RUN-127.0.0.1-90.bat`, `Service\Service\Run.bat`) |
| JSON dev/build | 48 file `.json` trong `bin/obj/_backup/_dev/dist/FlashVNG` (giữ 17 file cấu hình panel) |
| Build tạm | `obj\`, `*.pdb`, `*.cache` |

> Không xoá gì trong bản chính — chỉ dọn trong bản sao.

---

## 2. Database trong bản sao (`Database\`)

| File | Loại | Dung lượng | Nội dung |
|---|---|---|---|
| `Db_Tank_FULL.bak` | FULL | 7,0 MB | DB chính (nhân vật, item, hoạt động…) — 214 bảng |
| `Db_Tank_All_FULL.bak` | FULL | 9,7 MB | DB cấu hình server (Shop_Goods, Active, Cloth…) — 254 bảng |
| `Db_GM_FULL.bak` | FULL | 0,8 MB | DB panel GM |
| `Db_Count_FULL.bak` | FULL | 1,4 MB | DB đếm/account phụ |
| `Db_Membership_FULL.bak` | FULL | 1,7 MB | DB tài khoản web |
| `Db_Logs_FULL.bak` | FULL | 0,7 MB | DB log |
| **`Db_Tank_CLEAN.bak`** | CLEAN | 6,9 MB | `Db_Tank` **đã xoá 22 bảng backup rác** → còn **192 bảng** |
| **`Db_Tank_All_CLEAN.bak`** | CLEAN | 9,5 MB | `Db_Tank_All` **đã xoá 7 bảng backup rác** → còn **247 bảng** |

Tất cả 8 file đã được `RESTORE VERIFYONLY ... WITH CHECKSUM` → *"The backup set on file 1 is valid."*

### Các bảng rác đã xoá khỏi bản CLEAN

`Db_Tank` (22):
`bak_active_20261120_2140`, `bak_active_award_20261120`, `bak_active_award_20261120_now`,
`bak_dt153_Gemstone`, `bak_dt153_Goods`, `bak_dt153_Goods2_20261120`, `bak_dt153_ListBox`,
`bak_dt153_mail2_20261120`, `bak_dt153_Messages`, `bak_gem_user8_20261120`,
`bak_giftitems_20261120`, `bak_giftitems_preGem_20261120`, `bak_goods_latent_20261119`,
`bak_mark_20261120`, `bak_maxcount_20261120_1128`, `bak_newbie_items_20261120`,
`bak_shopgoods_maxcount_20261120`, `bak_sp_Active_PullDown_20261120`,
`Sys_NewbieGiftItems_bak_20261119`, `Sys_User_Gemstone_CHIENHON_bak_20261121_0900`,
`Sys_User_Gemstone_CHIENHON_bak_20261121_1836`, `User_Avatar_Colection_bak_20261121_0832`

`Db_Tank_All` (7):
`Active_Award_bak2_20261119`, `Active_bak_dsh_20261119`, `Active_bak2_20261119`,
`bak_active_20261120_2140`, `bak_activeaward_20261120`, `bak_gmactivity_20261120_2145`,
`GM_Activity_bak_dsh_20261119`

### Database KHÔNG đưa vào bản sao (rác, không service nào dùng)

`Db_TankSchemaTemp`, `BKpet`, `BKorigT`, `BKpetOld`, `BKcmpA` (~866 MB/db trên server chính).
Không có connection string nào trỏ tới các DB này — có thể DROP trên server chính nếu cần giải phóng ~4,3 GB.

---

## 3. Những gì đã cập nhật vào bản chính (changelog)

### 3.1 Quà Tân Thủ (2026-11-20)

- Thêm 5 món vào `Sys_NewbieGiftItems` → 289 dòng, đồng bộ `Active_Award` (289) ở cả `Db_Tank` + `Db_Tank_All`:
  `12269` Đá Dung Hợp ×9.999 · `201567` Đá Manh Hóa ×999.999 · `11165` Linh Châu Kỹ Năng ×99.999 ·
  `12286` Pha Lê Linh Hồn ×999.999 · `12287` Linh Nguyên-Nước ×99.999.
- `Shop_Goods.MaxCount`: `12286`, `201567` → **999999**.
- `Active.AwardContent`: mở rộng cột `Db_Tank` từ `nvarchar(2000)` → `nvarchar(max)`; dựng lại 65 dòng / 2.039 ký tự (ghi cả 2 DB).
- Build lại `ActiveList.xml`.

### 3.2 Dọn hoạt động hấp dẫn (2026-11-20)

- Xoá 5 hoạt động khỏi `Active` (cả 2 DB): `76885` Hướng Dẫn Nạp Xu, `77198`, `77201`, `77202`, `77203` (các hoạt động CODE).
  → `ActiveList.xml` chỉ còn **Quà Tân Thủ** (`2026110701`).
- Xoá hoạt động GM **"Quà Nạp Ma Pháp"** (`8c578177-…`) khỏi `GM_Activity` → còn 65 hoạt động; build lại `GmActivityInfo.xml`.

### 3.3 Bộ Sưu Tập (Avatar Collection) — 2026-11-21

- Sửa `GmToolService.AvatarAsync` (panel): điều kiện "đã full" kiểm tra thêm `EmptyDress` + `Expired`,
  upsert dữ liệu hợp lệ, đọc lại xác thực. Build + deploy `GunnyPanel.dll`
  (backup `app\GunnyPanel.dll.bak-20261121-0820`).
- Dữ liệu `User_Avatar_Colection`: đã sửa các nick hỏng (8, 999002) rồi reset toàn bộ để test.

### 3.4 Chiến Hồn (Battle Soul) — 2026-11-21

- Panel: thêm `GmToolService.BattleSoulAsync` — **upsert đủ 5 slot** (`EquipPlace 2,3,5,11,13` ↔
  `FigSpiritId 100001,100004,100003,100002,100005`, value `6,0,0|6,0,1|6,0,2`),
  dòng ngọc chiếm slot được trả về túi (`EquipPlace=0`), kiểm tra lại 5/5 slot.
- `RoadGmApiClient`: `battle_soul` chuyển sang **LiveFeature** (panel ghi DB rồi gọi Road GM API áp live).
- Dữ liệu: **8/8 tài khoản** hiện đủ 5/5 slot đúng loại (test1, test2, hú hú, hihi, hahaa, Đạt, Hồ Thái Đạt, Hạt Đồ).
- Build + deploy `GunnyPanel.dll` (backup `app\GunnyPanel.dll.bak-20261121-0855`).

### 3.5 Lực chiến (FightPower) hiển thị 64-bit — 2026-11-21

- **Client SWF** `Website\2.png`, `4.png` (+ `Flash\`): `BasePlayer` đổi `int/uint → Number` cho
  `FightPower`/`FightPower_Show`; `PlayerInfoView`, `AcademyMemberItem`, `AcademyPlayerPanel` dùng `FightPower_Show`.
  Backup: `2.png.bak-fp-20261121-0915`, `4.png.bak-fp-20261121-0915`.
- **Website XML**: thêm handler `GunnyFix.ConsortiaUsersListFix` (`Website\Request\Bin\GunnyFix.Consortia.dll`),
  `ConsortiaUsersListRaw.ashx` (proxy handler gốc) + `ConsortiaUsersList.ashx` trỏ handler mới.
  Handler thêm attribute `ExFightPower` (phần vượt 32-bit) → danh sách Guild hiển thị đúng.
  Backup: `ConsortiaUsersList.ashx.bak-fp-20261121-0920`.
- **Trần chỉ số lực chiến** (`GamePlayer.UpdateFightPower`) — đồng bộ mọi tài khoản, kể cả nick tạo sau:
  `FightPowerCapBasicSum=125350`, `FightPowerCapBaseAttack=11377`, `FightPowerCapBaseDefence=10536`
  (đọc từ `Server_Config`, có thể chỉnh không cần build); hệ số chia giữ `200.000.000`.
  → Nick vượt trần (Hồ Thái Đạt 45,86 tỷ) bị kéo về **~3,52 tỷ**; nick dưới trần không đổi.
- Build `Road.Service.sln -c Release` (0 error) và **copy 6 DLL** vào `Service\Service\Road`
  (backup `*.bak-fp-20261121-182325`): `Game.Server`, `Bussiness`, `SqlDataProvider`, `Game.Base`, `Game.Logic`, `EntityDatabase`.

### 3.6 Tài liệu đã tạo/cập nhật (trong bản sao)

| File | Nội dung |
|---|---|
| `docs\HUONG-DAN-THEM-QUA-TAN-THU.md` | Quy trình thêm/sửa quà tân thủ, cảnh báo Reload làm mất `Active_Award`, mục xoá hoạt động, mục GM activity |
| `Tools\GunnyPanel\docs\AVATAR-COLLECTION.md` | Bộ Sưu Tập: cơ chế, sự cố placeholder, cách sửa |
| `Tools\GunnyPanel\docs\BATTLE-SOUL.md` | Chiến Hồn: 5 slot, upsert, reset test |
| `Tools\GunnyPanel\docs\FIGHTPOWER-LONG.md` | Lực chiến 64-bit: client SWF, XML `ExFightPower`, trần chỉ số, deploy |

---

## 4. Cách khôi phục

### 4.1 Khôi phục file

```powershell
# 1) Dừng service cũ (nếu còn chạy)
Get-Process Road.Service,Center.Service,Fighting.Service -ErrorAction SilentlyContinue | Stop-Process -Force

# 2) Copy bản sao về vị trí chạy (đổi tên tuỳ ý)
robocopy "<thu-muc-nguon>" "<thu-muc-dich>" /E /R:1 /W:1
```

### 4.2 Khôi phục database

```sql
-- Ví dụ khôi phục Db_Tank từ bản FULL (bản đầy đủ)
RESTORE DATABASE Db_Tank FROM DISK = N'C:\Gunny\Gunny12.5\Database\Db_Tank_FULL.bak'
WITH MOVE N'Db_Tank_Data' TO N'C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\Db_Tank.MDF',
     MOVE N'Db_Tank_Log'  TO N'C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\Db_Tank.LDF',
     REPLACE;

-- Bản CLEAN (đã xoá bảng rác) — cùng cú pháp, đổi tên file .bak
-- Db_Tank_All: logical name cũng là Db_Tank_Data / Db_Tank_Log
```

> Trước khi RESTORE phải `ALTER DATABASE <db> SET SINGLE_USER WITH ROLLBACK IMMEDIATE;`
> và stop 3 service để không còn kết nối.

### 4.3 Chạy game

```bat
RUN-SERVICES.bat        (chuột phải → Run as administrator)
RUN-127.0.0.1-90.bat    (panel GM, nếu cần)
```

Port kiểm tra: `7539` (Fight), `8527` (Road), `9601` (Road GM API), `4109` (Center), `1593` (Center SOAP).
Website: `http://127.0.0.1` (site Gunny) · `http://127.0.0.1:88` (site Request) · Panel: `http://127.0.0.1:90`.

### 4.4 Lưu ý khi chạy lại

- `Tools\GunnyPanel\app\appsettings.json` đang trỏ đường dẫn tuyệt đối `C:\Gunny\Gunny12.5\...`
  → nếu chạy bản sao ở thư mục khác, sửa lại các path trong file này.
- Client game cache SWF: nếu client cũ, xoá cache `%LOCALAPPDATA%\37abc\User Data\Default\Cache`
  và `...\Pepper Data\Shockwave Flash\WritableRoot\#SharedObjects`.
- Sau khi restore DB, giá trị `FightPower` trong `Sys_Users_Detail` được tính lại khi người chơi đăng nhập.

---

## 5. Kiểm tra nhanh sau khi restore

```sql
-- 2 nguồn quà tân thủ khớp nhau
SELECT (SELECT COUNT(*) FROM Db_Tank.dbo.Sys_NewbieGiftItems) AS GiftItems,
       (SELECT COUNT(*) FROM Db_Tank.dbo.Active_Award WHERE ActiveID=2026110701) AS ActiveAwards;

-- Chiến Hồn 5 slot cho từng nick
SELECT UserID, COUNT(*) AS Slots,
       SUM(CASE WHEN FigSpiritId IN (100001,100002,100003,100004,100005) THEN 1 ELSE 0 END) AS DungLoai
FROM Db_Tank.dbo.Sys_User_Gemstone WHERE EquipPlace IN (2,3,5,11,13) GROUP BY UserID;

-- Bảng rác (bản CLEAN phải = 0)
SELECT COUNT(*) FROM Db_Tank.sys.tables WHERE name LIKE '%[_]bak%' OR name LIKE 'bak[_]%';
```

---



---

## 6. Cập nhật 2026-11-21 22:00 — viết lại `Service\Service\Run.bat`

`Run.bat` cũ gọi `run-stack.ps1` (file này **không còn tồn tại** ở cả bản chính lẫn bản sao)
nên bấm vào không start được service. Đã **viết lại hoàn toàn**, tự chứa, không phụ thuộc file .ps1 nào:

1. Tự xin quyền **Administrator** (UAC) nếu chưa elevated.
2. `taskkill` 3 service cũ (Road, Fighting, Center).
3. Chờ **SQL Server** sẵn sàng (`Db_Tank_All`).
4. Start **đúng thứ tự**:

| Thứ tự | Service | File | Port |
|---|---|---|---|
| 1 | Center | `Center\Center.Service.exe` | 1593 |
| 2 | Fight | `Fight\Fighting.Service.exe` | 7539 |
| 3 | Road | `Road\Road.Service.exe` | 8527 + 9601 |

5. Mỗi service chờ port LISTEN rồi mới sang service kế (timeout 150/180/210 giây).
6. In bảng trạng thái port 1593/7539/8527/9601 và dừng (hoặc `Run.bat --no-pause` để không dừng).

* Bản gốc trước khi sửa: `Service\Service\Run.bat.bak-20261121-2200`
* Bản sao đã copy cùng nội dung (hash trùng bản chính).

3 file `.bat` cần dùng (đều có trong bản sao):

| File | Chức năng |
|---|---|
| `RUN-SERVICES.bat` | Start cả 3 service (Center → Fight → Road), chờ port 1593/7539/8527/9601 |
| `RUN-127.0.0.1-90.bat` | Start panel GM `GunnyPanel.exe`, mở `http://127.0.0.1:90` |
| `Service\Service\Run.bat` | **Bản mới**: tự chứa, start 3 service dưới quyền Admin (như bảng thứ tự ở trên) |

---

*File này nằm trong bản sao lưu; bản chính không bị thay đổi.*