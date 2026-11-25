# NEW SESSION — CONTEXT HANDOFF (BK Gunny 12.5)

> **Đọc file này ĐẦU TIÊN trong phiên mới.**
> Cập nhật lần cuối: **2026-11-22 ~22:20 (Asia/Bangkok)** · Server đang chạy bình thường.
> Mọi số liệu trong đây là trạng thái thực tế tại thời điểm trên — nếu lệch, kiểm tra lại trước khi hành động.

---

## 0. Cách dùng file này

1. Đọc mục **1 → 4** để biết môi trường, đường dẫn, cách build/deploy.
2. Đọc mục **6** để biết những gì đã làm (tránh làm lại / tránh phá).
3. Đọc mục **8** để biết lỗi còn tồn (đừng tự sửa nếu user chưa duyệt).
4. Tuân thủ **mục 7 (quy tắc an toàn)** — đặc biệt: *không restart service được nếu không có quyền Admin*, *không deploy lệch 1 phía khi packet thay đổi*.
5. Đọc `docs\TRANG-THAI-CLIENT-DB-WEBSHOP.md` — trạng thái client / database / webshop đã khảo sát 22/11 (14 vấn đề, có ID + bằng chứng). **Sửa xong mục nào thì chạy `docs\refresh-trang-thai.ps1`** để tài liệu tự cập nhật trạng thái + ghi changelog.

---

## 1. Môi trường & đường dẫn

| Thành phần | Đường dẫn |
|---|---|
| **Thư mục game (bản đang chạy)** | `C:\Gunny\Gunny12.5` |
| Source server | `C:\Gunny\Gunny12.5\Full Source` (13 project, 4.684 file C#) |
| Service chạy | `C:\Gunny\Gunny12.5\Service\Service\{Road,Center,Fight}` |
| Website (IIS site `Gunny`) | `C:\Gunny\Gunny12.5\Website` |
| Website API (IIS site `Request`, port 88) | `C:\Gunny\Gunny12.5\Website\Request` |
| Panel GM | `C:\Gunny\Gunny12.5\Tools\GunnyPanel\app` |
| Tài liệu | `C:\Gunny\Gunny12.5\docs` |
| Bản cũ (không dùng) | `C:\Gunny\Gunny13.0` (~2,8 GB, có thể xoá sau khi chắc chắn) |
| Bản backup folder cũ | (đã được user đổi tên thành `Gunny12.5` — chính là thư mục đang chạy) |

> **Lịch sử tên thư mục:** `BK_Gunny13.0` → `Gunny13.0` (bản cũ) · backup `BK_Gunny13.0_Backup_20261121` → **`Gunny12.5`** (đang chạy). IIS hiện trỏ trực tiếp `C:\Gunny\Gunny12.5\Website`, junction `BK_Gunny12.5` không còn.

**Công cụ phụ trợ:**

| Công cụ | Đường dẫn |
|---|---|
| FFDec (decompile/rebuild SWF) | `C:\Gunny\ToolGunny\_analysis\ffdec\ffdec.jar` |
| Java | `C:\Program Files\Common Files\Oracle\Java\javapath\java.exe` |
| Python (zlib cho XML client) | `C:\Users\hotha\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe` |
| Client AS3 đã decompile (`4.png`) | `C:\Gunny\ToolGunny\_analysis\4png_scripts\scripts` |
| Client AS3 đã decompile (`2.png`) | `C:\Gunny\ToolGunny\_analysis\client4_scripts\scripts` |
| Preloader AS3 | `C:\Gunny\ToolGunny\_analysis\preloader1_scripts\scripts` |

> ⚠️ Máy chạy **Windows PowerShell 5.1**. File `.ps1` chứa tiếng Việt **phải lưu UTF-8 có BOM**, nếu không sẽ lỗi parse. An toàn nhất: viết `.ps1` không dấu, hoặc `.bat` ASCII.

---

## 2. Database

| Mục | Giá trị |
|---|---|
| SQL Server | `DESKTOP-SMLR4VU` (SQL 2025 / MSSQL17) |
| Tài khoản | `sa` / `123456` |
| DB chính | `Db_Tank` (nhân vật/item), `Db_Tank_All` (cấu hình/template), `Db_GM` (panel), `Db_Count`, `Db_Membership`, `Db_Logs` |
| DB rác (không dùng) | `Db_TankSchemaTemp`, `BKpet`, `BKorigT`, `BKpetOld`, `BKcmpA` (~4,3 GB — có thể DROP khi cần) |

`sqlcmd` có sẵn: `sqlcmd -S DESKTOP-SMLR4VU -U sa -P 123456 -d Db_Tank -C -W -Q "..."`

### Cấu trúc đã thêm trong các phiên trước (KHÔNG xoá)

| Đối tượng | Mô tả |
|---|---|
| `Db_Tank_All.dbo.Suit_Bonus` | Thưởng theo mốc bộ: `SuitId, NeedCount, MinStrengthen, BonusAttack/Defence/Agility/Luck/HP/Damage/Armor/MagicAttack/MagicDefence/ReducePercent/CritReducePercent` — 142 dòng / 128 bộ |
| `SP_Suit_Bonus_All` | SP đọc bảng trên |
| `Suit_Template` cột `Bonus*`, `MinStrengthen` | Cột cũ dùng cho nhánh legacy (vẫn giữ, code ưu tiên `Suit_Bonus`) |
| `Suit_Template_bak_20261122` | Backup trước khi thêm cột |
| `Sys_User_Gemstone_CHIENHON_bak_20261121_0900/1836`, `User_Avatar_Colection_bak_20261121_0832`, `bak_*` khác | Backup dữ liệu test (có thể drop nếu cần dọn) |

---

## 3. Service / port / cách chạy

| Service | Exe | Port | Ghi chú |
|---|---|---|---|
| Center | `Center\Center.Service.exe` | 1593 (TCP) + **4109** (SOAP, bind `::`) + 2008 (WCF) | cần **Admin** (URL ACL `http://+:2008`) |
| Fight | `Fight\Fighting.Service.exe` | 7539 | |
| Road | `Road\Road.Service.exe` | 8527 (game) + 9601 (GM API, localhost) | |
| IIS | — | 80 / 88 | site `Gunny` + `Request` |
| Panel | `Tools\GunnyPanel\app\GunnyPanel.exe` | 90 (localhost) | |

**Script chạy sẵn (đều cần Administrator):**

```bat
C:\Gunny\Gunny12.5\RUN-SERVICES.bat           :: start Center -> Fight -> Road
C:\Gunny\Gunny12.5\RUN-127.0.0.1-90.bat       :: start panel
C:\Gunny\Gunny12.5\Service\Service\Run.bat    :: start/restart 3 service (self-contained, admin)
C:\Gunny\Gunny12.5\DEPLOY-SUIT-BONUS.bat      :: deploy Road-only (stop Road -> copy 6 DLL -> start Road)
C:\Gunny\Gunny12.5\DEPLOY-SUIT-CRIT.bat       :: deploy Road+Fight (packet change: stop cả 2 -> copy -> start Fight -> Road)
C:\Gunny\FIX-IIS-Gunny12.5.bat                :: trỏ IIS về Gunny12.5 (chỉ khi cần, admin)
```

> Agent (AI) chạy **không có quyền Admin** → **không thể stop/start service**. Khi cần restart: tạo script `.bat`/`.ps1` rồi nhờ user chạy "Run as administrator". Tên 2 script `DEPLOY-SUIT-*` là tên lịch sử; nội dung vẫn dùng làm deploy chung.

---

## 4. Build & deploy DLL

```powershell
# Build server
dotnet build "C:\Gunny\Gunny12.5\Full Source\Road.Service.sln"     -c Release --nologo
dotnet build "C:\Gunny\Gunny12.5\Full Source\Fighting.Service.sln" -c Release --nologo
```

**Output:** `<project>\bin\Release\net472\<Assembly>.dll`

| Assembly | Project | Copy vào |
|---|---|---|
| `Game.Server.dll` | `game.server` | Road |
| `Fighting.Server.dll` | `fighting.server` | Fight |
| `Game.Logic.dll` | `Game.Logic` | **Road + Fight** |
| `Bussiness.dll` | `Bussiness` | **Road + Fight** |
| `SqlDataProvider.dll` | `SqlDataProvider` | **Road + Fight** |
| `Game.Base.dll` | `game.base` | **Road + Fight** |
| `EntityDatabase.dll` | `EntityDatabase` | Road + Fight |

⚠️ **Quy tắc vàng:** nếu thay đổi **packet giữa Road ↔ Fight** (file `game.server\Game.Server.Battle\FightServerConnector.cs` và `fighting.server\Fighting.Server\ServerClient.cs`) thì **BẮT BUỘC deploy cả 2 service cùng lúc**, nếu không battle sẽ desync/crash.

⚠️ Không deploy từ `Full Source\_build_release` (bản cũ 11/20).

---

## 5. Client game

- Client SWF thực chất là file `Website\2.png`, `Website\4.png` (và bản copy `Website\Flash\...`). Sửa SWF phải sửa **cả 2 nơi**.
- Config client: `Website\config.xml`, `Website\Flash\config.xml`, `Website\FlashVNG\config.xml`, `Website\Request\config.xml`, `Website\Request\config (4).xml`.
  - Cờ quan trọng: `<SUIT enable="true" />` (bật tooltip bộ + cộng chỉ số bộ phía client).
- Dữ liệu XML client (nén zlib, do server sinh từ DB):
  - `Website\Request\SuitTemplateInfoList.xml` ← `Suit_Template`
  - `Website\Request\SuitPartEquipInfoList.xml` ← `Suit_Part_Equip`
  - Định dạng: `<Result value="true" message="Success!"><item ... /></Result>`, nén zlib level 9 (header `78 DA`).
  - **Sau khi sửa tên/mô tả bộ trong DB phải sinh lại 2 file này**, rồi restart client (client đọc lúc khởi động).
- Quy trình sửa SWF (đã dùng cho FightPower Number patch):
  1. FFDec `-export script <outdir> <swf>` (hoặc dùng bản decompile có sẵn).
  2. Sửa `.as` trong `<outdir>\scripts`.
  3. FFDec `-importScript <in.swf> <out.swf> <outdir>\scripts`.
  4. Copy `out.swf` → `2.png`/`4.png` (Website + Website\Flash), backup `.bak-*` trước.

**Cache client khi test:** xoá
`%LOCALAPPDATA%\37abc\User Data\Default\Cache\*`, `%APPDATA%\Macromedia\Flash Player\#SharedObjects\*`, `%LOCALAPPDATA%\Pepper Data\Shockwave Flash\WritableRoot\#SharedObjects\*`.

---

## 6. Những gì đã làm (đừng làm lại / đừng phá)

### 6.1 Quà Tân Thủ (2026-11-20)
- Thêm 5 món vào `Sys_NewbieGiftItems` + `Active_Award` (289 dòng, cả 2 DB): `12269` ×9.999 · `201567` ×999.999 · `11165` ×99.999 · `12286` ×999.999 · `12287` ×99.999.
- `Shop_Goods.MaxCount` của `12286`, `201567` = **999999**.
- `Active.AwardContent`: `Db_Tank` đổi sang `nvarchar(max)` (2.039 ký tự), rebuild `ActiveList.xml`.
- Hướng dẫn: `docs\HUONG-DAN-THEM-QUA-TAN-THU.md`.

### 6.2 Dọn hoạt động (2026-11-20)
- Xoá 5 Active (76885, 77198, 77201, 77202, 77203) cả 2 DB → chỉ còn **Quà Tân Thủ** (`2026110701`).
- Xoá GM activity **"Quà Nạp Ma Pháp"** → `GM_Activity` còn 65 hoạt động, rebuild `GmActivityInfo.xml`.

### 6.3 Bộ Sưu Tập (Avatar Collection)
- Sửa `GmToolService.AvatarAsync` (điều kiện full kiểm tra `EmptyDress`/`Expired`, upsert chuẩn); panel đã deploy.
- Doc: `Tools\GunnyPanel\docs\AVATAR-COLLECTION.md`.

### 6.4 Chiến Hồn (Battle Soul)
- Panel `BattleSoulAsync` upsert đủ 5 slot (`EquipPlace 2,3,5,11,13` ↔ `100001/100004/100003/100002/100005`, value `6,0,0|6,0,1|6,0,2`); dòng ngọc chiếm slot bị đẩy về túi.
- Đã sửa dữ liệu **8/8 account** đủ 5 slot; backup `Sys_User_Gemstone_CHIENHON_bak_20261121_*`.
- Doc: `Tools\GunnyPanel\docs\BATTLE-SOUL.md`.

### 6.5 FightPower 64-bit + trần chỉ số
- Client SWF: `BasePlayer._fightPower` → `Number`, thêm `ExFightPower` (guild XML handler `GunnyFix.ConsortiaUsersListFix` trong `Website\Request\Bin\GunnyFix.Consortia.dll` + `ConsortiaUsersList.ashx`/`ConsortiaUsersListRaw.ashx`).
- Server: `GamePlayer.UpdateFightPower` clamp theo `Server_Config`: `FightPowerCapBasicSum=125350`, `FightPowerCapBaseAttack=11377`, `FightPowerCapBaseDefence=10536` (divisor giữ `200000000`).
- Doc: `Tools\GunnyPanel\docs\FIGHTPOWER-LONG.md` + `FIGHTPOWER-VNG.md`.

### 6.6 Bộ trang bị (Suit) — mới nhất, đã kiểm chứng
- DB: bảng `Suit_Bonus` (142 dòng / 128 bộ), cột `Suit_Template.Bonus*` + `MinStrengthen`.
- Code: `SuitBonusInfo`, `ProduceBussiness.GetAllSuitBonus`, `SuitMgr.LoadSuitBonuses/FindSuitBonuses` (**phải load trong `ReLoad`** — từng bị thiếu), `GamePlayer.GetSuitBonus`, `PlayerEquipInventory`, `Living.TakeDamage` (crit reduce), packet thêm 3 int `ReduceDamePlus`, `GoldenReduceDamage`, `CritReducePercent` (Road + Fight).
- RingStation sender (`RingStationFightConnector.SendAddRoom`) cũng phải ghi 3 int (0) để khớp reader.
- Đã đổi tên 3 bộ sai: 33 → "Cặp Vòng Tay 10 Tuổi", 34 → "Cặp Nhẫn 10 Tuổi", 101 → "Cặp Nhẫn 16 Tuổi"; sinh lại 2 XML client.
- Đã kiểm chứng: test2 (Cánh + Bong Bóng 17T) `reduceDame=29, critReduce=7`; test1 (chỉ Bong Bóng) `22 / 0`.
- Doc: `docs\SUIT-BONUS-CAP-NHAN-VONG-TAY.md` (đầy đủ cơ chế + deploy + rollback + kiểm chứng).

### 6.7 Đổi tên thư mục & path
- Đã sửa toàn bộ path config sang `C:\Gunny\Gunny12.5`; chi tiết `docs\CHAY-GAME-GUNNY12.5.md`, `docs\CAP-NHAT-BACKUP-20261121.md`.

### 6.8 Audit source (chỉ báo cáo, CHƯA sửa)
- `docs\BAO-CAO-AUDIT-SRC-20261122.md` — 32 SP thiếu, 123 empty catch, 6 điểm NRE, dead code, MD5 auth, Web.config debug, Center 4109 all-interface, v.v.

### 6.9 Patch client: 4 phụ kiện thú cưỡi che mất nhân vật (2026-11-22 22:15) ✅
- Triệu chứng: Xe Hoa Tiên (12973/210), Giỏ Trứng Thần Kỳ (12985/214), Sách Ma Thuật (12993/231), Ghế Nhạc Thiên Hoa (14914/251) — đầu + thân nhân vật bị vật che.
- Nguyên nhân: (a) `HallPlayerDraw.initialize()` vẽ **head dưới lớp mount**; (b) 4 type không có trong `MountSitList` nên dùng tư thế cưỡi trên vật để ngồi.
- Sửa: `starling\scene\hall\player\HallPlayerDraw.as` (head lên trên mount) + `horse\_139$25.as` (`MountSitList += 210,214,231,251`).
- **Phải patch cả `2.png` và `4.png`** (cùng chứa code hall) ở `Website\` và `Website\Flash\`.
- Bản đang chạy: `2.png` 3.335.984 B / `0626ACE0D004` · `4.png` 7.214.962 B / `5C8B518F9D60`.
- Backup: `*.bak-20261122-2210` (2.png), `*.bak-20261122-2148` (4.png). Artifact: `docs\client-patches\`.
- **Quy trình đầy đủ 7 bước + 6 bài học:** `docs\TRANG-THAI-CLIENT-DB-WEBSHOP.md` §11.
- ⚠️ Hall do **`starling.scene.hall.player.*`** vẽ; `hall.player.*` là **dead code** (chỉ có `touchArea`).

---

## 7. Quy tắc an toàn (bắt buộc)

1. **Không restart service** nếu không có quyền Admin → tạo script và nhờ user chạy.
2. **Trước khi ALTER/DROP/UPDATE dữ liệu lớn**: tạo bảng backup `*_bak_YYYYMMDD_HHMM` và nêu rõ cách rollback.
3. **Đổi packet** → deploy **đồng thời** Road + Fight.
4. **Đổi tên/mô tả bộ trong `Suit_Template`** → sinh lại 2 XML client + user restart client.
5. **Không sửa** các mục trong báo cáo audit khi user chưa duyệt.
6. Sau khi sửa DB, nếu ảnh hưởng cột mà code đọc lúc khởi động (ví dụ `Suit_Bonus`) → cần **restart Road**.
7. Khi build: dùng `dotnet build ... -c Release`; kiểm tra **0 Error** trước khi deploy.
8. Log để kiểm tra: `Service\Service\{Road,Fight,Center}\logs\GameServer.log`, `Error.log`; panel `Tools\GunnyPanel\app\data\logs`.
9. **Sau khi sửa xong bất kỳ mục nào trong `TRANG-THAI-CLIENT-DB-WEBSHOP.md` → chạy `docs\refresh-trang-thai.ps1`** (chỉ đọc hệ thống, tự cập nhật bảng trạng thái + changelog). Nếu mục đó vẫn báo `ISSUE` thì sửa chưa đúng.
10. **Patch SWF client:** phải sửa **cả `2.png` và `4.png`** (cả hai chứa code hall), deploy cả `Website\` lẫn `Website\Flash\`, **import chỉ file đã sửa** (FFDec lỗi `fromPool` nếu import toàn bộ), và **user phải thoát hẳn client** rồi mới test. Quy trình đầy đủ: `TRANG-THAI-CLIENT-DB-WEBSHOP.md` §11.

---

## 8. Việc còn tồn (từ audit — cần user duyệt mới sửa)

| Ưu tiên | Việc |
|---|---|
| 1 | 32 stored procedure thiếu (danh sách trong báo cáo audit) — gây tính năng im lặng |
| 2 | 123 `catch { }` rỗng — thêm log hoặc xử lý |
| 3 | 6 điểm NRE (`DevilTurnMgr:52`, `BeadConversion:38`, `TeamMgr:160/379/458`, `GamePlayer:5288`) |
| 4 | `Website\Web.config`: `debug="true"` + `customErrors mode="Off"` |
| 5 | Center SOAP 4109 bind `::` (nên đổi về `127.0.0.1`) |
| 6 | `Mem_UserInfo.IsApproved/IsLockedOut` NULL, salt 3 ký tự |
| 7 | MD5 auth ở `CenterService` + `BaseInterface`; plaintext compare `CenterService:383` (cần xác minh luồng login thật) |
| 8 | Dead code: CS0162 (18), CS1717 (10), CS1522 (2), CS0649 (12) |
| 9 | 22 chỗ nối chuỗi SQL (ManorBussiness 18) |
| 10 | Dọn `Fight\logs\Error.log` entry cũ 15:03:58 (chỉ là lịch sử) |

---

## 9. Lệnh kiểm tra nhanh

```powershell
# Trạng thái service/port
Get-Process Road.Service,Center.Service,Fighting.Service,GunnyPanel -EA SilentlyContinue | Select ProcessName,Id,StartTime
foreach($p in 1593,4109,7539,8527,9601,80,88,90){ '{0}: {1}' -f $p, $(if(Get-NetTCPConnection -LocalPort $p -State Listen -EA SilentlyContinue){'LISTEN'}else{'CLOSED'}) }

# GM API (cần token)
$tok = (Get-Content 'C:\Gunny\Gunny12.5\Tools\GunnyPanel\app\data\road-gm-api-token.txt' -Raw).Trim()
Invoke-RestMethod 'http://127.0.0.1:9601/health' -Headers @{ 'X-GM-Token' = $tok }
Invoke-RestMethod 'http://127.0.0.1:9601/player?userId=2' -Headers @{ 'X-GM-Token' = $tok }

# Suit bonus thực tế trong DB
sqlcmd -S DESKTOP-SMLR4VU -U sa -P 123456 -d Db_Tank_All -C -W -Q "SELECT SuitId,NeedCount,BonusDamage,BonusArmor,BonusMagicAttack,BonusMagicDefence,BonusReducePercent,BonusCritReducePercent FROM Suit_Bonus WHERE SuitId IN (122,123,124)"

# Log gần nhất
Get-Content 'C:\Gunny\Gunny12.5\Service\Service\Road\logs\GameServer.log' -Tail 20
Get-Content 'C:\Gunny\Gunny12.5\Service\Service\Fight\logs\Error.log' -Tail 20
```

---

## 10. Tài liệu liên quan

| File | Nội dung |
|---|---|
| `docs\BAO-CAO-AUDIT-SRC-20261122.md` | Báo cáo audit đầy đủ (lỗi, bằng chứng file:line, mức độ) |
| `docs\TRANG-THAI-CLIENT-DB-WEBSHOP.md` | **File sống**: client, database, webshop — 14 vấn đề có ID + bằng chứng + kế hoạch sửa |
| `docs\refresh-trang-thai.ps1` | Script tự cập nhật file trên (chạy sau mỗi lần sửa) |
| `docs\SUIT-BONUS-CAP-NHAN-VONG-TAY.md` | Bộ trang bị: cơ chế, dữ liệu, code, deploy, rollback, kiểm chứng |
| `docs\CHAY-GAME-GUNNY12.5.md` | Cách chạy server từ `Gunny12.5` |
| `docs\CAP-NHAT-BACKUP-20261121.md` | Nhật ký bản backup + changelog ban đầu |
| `docs\HUONG-DAN-THEM-QUA-TAN-THU.md` | Quy trình quà tân thủ |
| `Tools\GunnyPanel\docs\BATTLE-SOUL.md` | Chiến Hồn |
| `Tools\GunnyPanel\docs\AVATAR-COLLECTION.md` | Bộ Sưu Tập |
| `Tools\GunnyPanel\docs\FIGHTPOWER-LONG.md` | FightPower 64-bit + trần chỉ số |

---

## 11. Trạng thái tại thời điểm bàn giao

| Mục | Giá trị |
|---|---|
| Road.Service | chạy, start 22/11 **16:23:23** |
| Fighting.Service | chạy, start 22/11 **11:55:53** |
| Center.Service | chạy, start 21/11 22:33:29 |
| GunnyPanel | chạy, start 21/11 22:33:57 |
| Port | 80, 88, 90, 1593, 4109, 7539, 8527, 9601 — tất cả LISTEN |
| `Road\Game.Server.dll` | 3.120.640 B — 22/11 **15:48:40** |
| `Road\Bussiness.dll` | 843.776 B — 22/11 **15:48:28** |
| `Fight\Fighting.Server.dll` | 72.704 B — 22/11 11:36:17 |
| `Fight\Game.Logic.dll` | 657.920 B — 22/11 11:33:38 |
| IIS | trỏ trực tiếp `C:\Gunny\Gunny12.5\Website` (không còn junction `BK_Gunny12.5`) |
| Client | `<SUIT enable="true">` ở cả 5 config; XML suit đã sinh lại 22/11 ~16:40 |
| Client SWF (CL-03) | `2.png` 3.335.984 B `0626ACE0D004` · `4.png` 7.214.962 B `5C8B518F9D60` — patch 22/11 **22:15** (cả `Flash\`); backup `*.bak-20261122-2210` / `-2148` |

---

## 12. Prompt gợi ý để mở phiên mới

> *"Đọc file `C:\Gunny\Gunny12.5\docs\NEW-SESSION-CONTEXT.md` rồi tiếp tục hỗ trợ tôi. Server đang chạy từ `C:\Gunny\Gunny12.5`. Không restart service nếu không có script Admin. Không sửa lỗi audit khi tôi chưa duyệt. Trước khi ALTER/DROP DB phải backup và nêu rollback."*

---

*Hết file bàn giao. Nếu có thay đổi lớn (đổi path, đổi port, deploy packet mới), cập nhật lại mục 1/3/11 của file này.*
