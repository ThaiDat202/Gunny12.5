# Gunny Request Studio (Gunny 12.6)

Công cụ **Rip request → Import database → Bomb / Weapon UI** cho server Gunny (DDTank) private 12.6.
Thay thế bộ `GunTools.exe` cũ, có giao diện đồ hoạ hiện đại (dark theme), có chế độ dòng lệnh (CLI) để tự động hoá,
và tích hợp sẵn phần **bomb / crater / vũ khí trong trận** mà trước đây phải dùng `tool rip bomb` thủ công.

> Đường dẫn chuẩn: `C:\Gunny\ToolGunny\GunnyRequestStudio\`
> File chạy: `dist\GunnyRequestStudio.exe` (giao diện) · `dist\GunnyRequestStudio.exe --cli ...` (dòng lệnh)

---

## 1. Tính năng

| Nhóm | Chức năng |
|---|---|
| **Rip Request** | Mirror toàn bộ request XML từ nhà phát hành (`https://quest.gn.zing.vn/`) vào `WebSite\Request\vng-quest`; tự nhận diện tên request từ thư mục Request + mirror + `names.txt`; song song nhiều luồng, retry, ghi file atomic (`.part`), so hash SHA-256; sau đó **promote** các file template (map được bảng CSDL) vào `WebSite\Request` (client dùng), file cũ được backup `*.bak-yyyyMMdd-HHmmss`. Báo cáo: `report` + `manifest` JSON. |
| **Import DB** | Quét toàn bộ XML trong mirror → tự động **map XML sang bảng** trong `Db_Tank_All` (theo tên + tập attribute + fuzzy cột `Defense/Defence`…), hiển thị bảng map, cho phép lọc/chọn/dry-run; import bằng `SqlBulkCopy` trong **transaction**, có **backup bảng** (`<Table>_bak_yyyyMMdd_HHmmss`), tự bỏ qua bảng không đổi (so hash manifest), tự xử lý: cột NOT NULL thiếu (fill/auto-ID), khoá chính trùng (dedupe), chuỗi vượt độ dài cột (cắt bớt + cảnh báo), xung đột 2 XML cùng map 1 bảng. |
| **Bomb / Weapon** | Quét id bomb tham chiếu từ `BallList.xml` (Crater) + `bombconfig.xml` (Common/CommonAddWound/CommonMultiBall/Special); rip asset client `WebSite\image\bomb\` (`crater/<id>/crater.png`, `craterBrink.png`, `bullet/bullet<id>.swf`, `blastOut/blastOut<id>.swf`) từ `https://res.gn.zing.vn/`; **tạo mask `.bomb` còn thiếu** từ `crater.png` (alpha ≥ 128) và deploy vào các thư mục Bomb của service (Fight/Road + Full Source debug bin), không ghi đè mặc định, có backup khi ghi đè; kiểm tra `resourceSite` trong `WebSite\config.xml` xem host nào thiếu asset gì. |
| **Giải mã XML** | File `.xml` request của Gunny là **zlib-compressed**. Trang này giải nén ra XML đọc được: xem trước có tô màu, metadata (dung lượng nén/giải nén, root, số item, bảng CSDL map được), **giải mã 1 file / cả thư mục** (giữ cấu trúc), và **nén lại** (`.xml` thường → `.xml` zlib) để đưa vào `WebSite\Request`. Kéo-thả file/thư mục vào trang cũng được. CLI: `--cli decode [file|folder] [--out DIR] [--encode] [--print]`. Report: `reports\decode-report.json`. |
| **Cài đặt** | Sửa toàn bộ đường dẫn/URL/connection string, test kết nối SQL Server, lưu/khôi phục mặc định; mở nhanh các thư mục. |
| **CLI** | `--cli rip|map|import|audit|bomb|decode|test-db|names|inspect` để chạy tự động/script hoá. |
| **An toàn dữ liệu server** | Khi import, tool **giữ nguyên các cột server-local** mà XML publisher không gửi (Game_Map.PosX/PosX1, Ball.Delay/HasTunnel/Name, NPC_Info.X/Y/Attack..., Shop_Goods.Remark/Valid, Pve_Info.*GameScript...) theo khoá chính; điền default thay vì NULL; **bỏ qua các bảng publisher không tương thích** (SkipTables); tự **kiểm tra server-safety** sau import (cảnh báo nếu Game_Map mất spawn hoặc Ball mất HasTunnel → Fight.exe không khởi động được). |
| **Kiểm tra kiểu dữ liệu** | `--cli audit` quét toàn bộ XML publisher vs kiểu cột CSDL local, phát hiện **publisher đổi int → long/bigint** hoặc giá trị vượt phạm vi (ví dụ FightPower 169 tỷ trong file `CelebByConsortia*`). Import cũng clamp + cảnh báo thay vì âm thầm mất dữ liệu. Report: `reports\type-audit.json`. |
| **Khác** | Log file `logs\`, report JSON trong `reports\`, settings `settings.json`, mapping override `table-map.overrides.json`, chế độ `--shots` xuất ảnh từng trang (dùng cho tài liệu/QA). |

## 2. Yêu cầu

- Windows 10/11, **.NET Framework 4.8** (có sẵn trên Windows).
- **SQL Server** chứa `Db_Tank_All` (mặc định `DESKTOP-SMLR4VU`, `sa/123456` — sửa trong tab **Cài đặt**).
- .NET SDK 8+ (chỉ cần khi build từ source; `dotnet build`).
- Quyền ghi vào `C:\Gunny\Gunny12.6\WebSite\Request`, `Service\Service\*\Bomb`, `WebSite\image`.
- Truy cập Internet tới `quest.gn.zing.vn` (rip request) và `res.gn.zing.vn` (asset bomb).

## 3. Build & chạy

```bat
cd C:\Gunny\ToolGunny\GunnyRequestStudio
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -Clean
dist\GunnyRequestStudio.exe
```

Bản build gồm: `GunnyRequestStudio.exe`, `GunnyRequestStudio.Core.dll`, `settings.json`,
`table-map.overrides.json`, `README.md`.

### CLI

```bat
dist\GunnyRequestStudio.exe --cli test-db
dist\GunnyRequestStudio.exe --cli names
dist\GunnyRequestStudio.exe --cli rip  [--no-refresh] [--no-promote] [--dry] [--keep] [--concurrency 8]
dist\GunnyRequestStudio.exe --cli map
dist\GunnyRequestStudio.exe --cli import [--only a,b] [--table T] [--dry] [--force] [--no-backup] [--batch 2000]
                                        [--no-preserve] [--allow-local-loss] [--skip-table A,B]
dist\GunnyRequestStudio.exe --cli audit [thu_muc] [--out reps.json] [--print]   (kiem tra int -> long / kieu du lieu)
dist\GunnyRequestStudio.exe --cli bomb scan|rip|mask|all       (bomb: xem tab Bomb hoặc bombcli)
dist\GunnyRequestStudio.exe --cli decode "file.xml" [--out DIR] [--print]     (xem/giai ma 1 file)
dist\GunnyRequestStudio.exe --cli decode "thu_muc" --out "thu_muc_out"         (giai ma ca thu muc)
dist\GunnyRequestStudio.exe --cli decode "thu_muc_xml" --encode --out "packed" (nen lai -> .xml zlib)
```

Biến môi trường `GRS_HOME` có thể trỏ tới thư mục chứa `settings.json`/`reports` (mặc định = thư mục exe).

## 3.1. Giao diện

Giao diện theo **theme sáng giống bản web Gunny Panel**: nền kem ấm `#F6F4EF`, card trắng bo góc + bóng nhẹ,
sidebar navy `#2D4A76` với dải amber, điểm nhấn vàng amber `#C98A3C`, chữ navy `#2A3550`.
Bảng dữ liệu, ô nhập, log console đều dùng nền sáng, viền mảnh, dễ đọc.
Ảnh chụp 6 trang: `_dev\final4-shots\01-dashboard.png` … `06-settings.png` (chụp bằng `_dev\capture-page.ps1 -Index N`).
Màu sắc tập trung ở `src\Core\UiShared\Theme.cs` (đổi palette tại đây nếu muốn theme khác).

## 4. Quy trình chuẩn (3 bước)

1. **Rip Request** → bấm *Quét tên request* → *Rip + Promote vào Request*.
   - Mirror: `WebSite\Request\vng-quest\*.xml` (bản gốc nhà phát hành).
   - Client dùng: `WebSite\Request\*.xml` (template đã promote, có backup file cũ).
   - Report: `reports\request-rip-report.json` (+ manifest).
2. **Import DB** → bấm *Quét & phân tích* → kiểm tra bảng map → *Import tất cả (template)*.
   - Backup từng bảng trước khi ghi đè; transaction rollback nếu lỗi.
   - Report: `reports\import-report.json`; manifest hash: `reports\import-manifest.json`.
3. **Bomb / Weapon** → *Quét Bomb* → *Rip asset client* → *Tạo mask thiếu + deploy vào Service*.
   - Mask mới vào `Service\Fight\Bomb`, `Service\Road\Bomb` (và `Full Source\*\bin\Debug\net472\Bomb` nếu có).
   - Report: `reports\bomb-report.json`.
4. **Restart server** để nạp dữ liệu mới: chạy `C:\Gunny\Gunny12.6\Service\Service\Run.bat` (chuột phải → *Run as administrator*).
   Tool không tự restart service (cần quyền admin); dữ liệu DB/mask đã sẵn sàng sau khi restart.

## 5. Cấu trúc mã nguồn

```
GunnyRequestStudio\
├─ src\
│  ├─ Core\                 (thư viện dùng chung)
│  │  ├─ Models.cs          DTO + cấu hình
│  │  ├─ XmlCodec.cs        giải nén zlib + đọc XML template (hỗ trợ cấu trúc lồng nhau)
│  │  ├─ TableMapper.cs     map XML → bảng CSDL (exact/auto/override/ambiguous)
│  │  ├─ DbImporter.cs      import: backup, transaction + giu cot server-local + kiem tra an toan
│  │  ├─ TypeAudit.cs       audit kieu du lieu publisher vs CSDL (int -> long...)
│  │  ├─ RipService.cs      mirror + promote request (song song, atomic, manifest)
│  │  ├─ BombService.cs     scan id, rip asset, tạo/deploy mask .bomb, check resourceSite
│  │  ├─ Overrides.cs       đọc/ghi table-map.overrides.json (Map + Fills)
│  │  ├─ Cli.cs             lệnh dòng lệnh
│  │  ├─ Utils.cs           HTTP, đường dẫn, ghi file atomic
│  │  └─ UiShared\          Theme, PageBase, StudioContext (service container)
│  └─ App\                  (WinForms)
│     ├─ Program.cs         entry + `--cli` + `--shots`
│     ├─ MainForm.cs        khung: sidebar, topbar, status bar
│     └─ Pages\             DashboardPage, RipPage, ImportPage, DecodePage, BombPage, SettingsPage
├─ _dev\                    dev harness (bombcli, leadcli, uiA, uiB, verify)
├─ dist\                    output build (exe + dll + config + README)
├─ reports\                 report JSON các lần chạy
├─ build.ps1
└─ table-map.overrides.json (bản gốc; build.ps1 copy vào dist)
```

## 6. Mapping XML → bảng CSDL

- Ưu tiên 1: **override thủ công** trong `table-map.overrides.json` (Map).
- Ưu tiên 2: **trùng tên chuẩn hoá** + attribute coverage ≥ 90%.
- Ưu tiên 3: **tập attribute khớp** (coverage ≥ 90%, ít cột thừa nhất).
- Bảng hệ thống/backup/bảng động (Sys_*, Site_*, users, *_bak*, log…) bị loại trừ.
- XML động (ranking, record, thông báo…) không map được → trạng thái `none` và **không import** (đúng: server tự sinh runtime).
- Khi 2 XML cùng map 1 bảng: tool chọn file có độ tin cậy cao hơn (exact > override > auto) rồi bỏ qua file còn lại (cảnh báo trong report).
- File hiện có **152 template** import được / 261 XML mirror (109 file còn lại là runtime/dynamic hoặc không có bảng).
- Muốn sửa mapping: thêm `{"Xml":"ten_file","Table":"Ten_Bang"}` hoặc `{"Xml":"...","Table":"none"}` (bỏ qua) vào mảng `Map`;
  muốn điền cột NOT NULL thiếu: mảng `Fills` (`{"Table":"Ball","Columns":[{"Name":"Delay","Value":"0"}]}`).

## 7. An toàn dữ liệu

- Mỗi bảng import: `SELECT * INTO <Table>_bak_yyyyMMdd_HHmmss FROM <Table>` trước khi `DELETE` + `INSERT` trong **cùng transaction**.
- Bảng không đổi (hash XML như lần import trước) → bỏ qua, không ghi.
- Khoá chính trùng trong XML → giữ 1 dòng, log cảnh báo.
- Cot NOT NULL chưa có trong XML → điền theo `Fills`, hoặc auto-ID cho cột int đầu tiên, còn lại báo lỗi rõ.
- Chuỗi vượt `nvarchar(n)` → cắt bớt đúng độ dài + cảnh báo (publisher đôi khi gửi dài hơn schema local).
- File Request cũ được backup trước khi promote.

### 7.1. Giữ cột server-local (chống lỗi Fight.exe / Road.exe)

Publisher chỉ gửi các cột họ quản lý; server local tự thêm nhiều cột quan trọng (map spawn, ball physics, NPC stats, script PvE...).
Nếu import ghi đè toàn bảng thì các cột này bị mất → **Fight.exe không khởi động** (`MapMgr maps:0`, `BallMgr Init: False`) hoặc Road.exe chết.

Tool xử lý (bật mặc định, checkbox **"Giữ cột server-local"** ở trang Import):

1. **Preserve theo khoá chính**: trước khi `DELETE`, đọc các cột DB không được XML/Fills mô tả, sau đó gán lại giá trị cũ cho từng dòng khớp PK.
2. **Cột nằm trong `Fills`** (ví dụ `Ball.Delay`, `Ball.HasTunnel`) được ưu tiên **giữ giá trị cũ** nếu dòng đã tồn tại; dòng mới lấy giá trị `Fills` (mặc định `200` / `1`).
3. **Điền default thay vì NULL** cho giá trị thiếu (string `''`, số `0`, bit `false`) — tránh NullReference/Cast ở code server hay `(string)reader[...]`.
4. **Fail-safe**: bảng có cột server-local nhưng **không có PK map từ XML** → **từ chối import** (trừ khi nằm trong `AllowLocalColumnLossTables` đã xác nhận vô hại) để không âm thầm phá dữ liệu.
5. **SkipTables — bảng bảo vệ**: các bảng publisher không tương thích code/schema local sẽ được bỏ qua hoàn toàn, giữ dữ liệu gốc.
   Mặc định: `CampWarItems`, `DevilTreasRankRewardList`, `Every_Day_Active_Reward_Template`, `Communal_Active`, `Communal_Active_Award`, `Communal_Active_Exp`.
   Chỉnh trong `table-map.overrides.json` mảng `SkipTables`.
6. **Server-safety check** sau khi import: cảnh báo nếu `Game_Map` toàn `PosX` rỗng, `Ball` không còn `HasTunnel=1`, hoặc `Delay` toàn 0
   (đây chính là 2 nguyên nhân làm Fight.exe chết). Kết quả nằm trong `reports\import-report.json` mục `ServerSafety`.

Đã kiểm chứng cuối 2026-11-09 trên bản DB gốc: import lại `Game_Map` + `Ball` giữ được **532/584** dòng có `PosX`, **1597** dòng `HasTunnel=1`
cũ + 192 ball mới lấy default → `1789/1840`, `Delay=200` đủ **1840/1840**; Fight.exe khởi động lại bình thường.

### 7.2. Kiểm tra kiểu dữ liệu (publisher đổi int → long)

- `--cli audit` (hoặc chạy trong import report) so sánh **giá trị số trong XML** với **kiểu cột CSDL**: `tinyint/smallint/int/bigint/decimal/real/float`.
- Phát hiện sớm publisher đổi kiểu: ví dụ **FightPower/ExFightPower** trong các file `CelebByConsortia*.xml`, `areaceleb*.xml`
  đạt tới **169.105.505.516** (> int32) → publisher đã dùng **64-bit (long)**; nếu map các file này vào bảng local phải tạo cột **bigint**.
- Với cột `int` gặp giá trị vượt phạm vi: tool **clamp** về biên + ghi cảnh báo kèm gợi ý `ALTER COLUMN ... bigint` (không còn âm thầm mất dữ liệu như bản cũ).
- Kết quả audit 2026-11-09: 1.249 cột số đã kiểm tra; các giá trị > int32 nằm ở **file chưa map bảng local** (FightPower/ExFightPower, `ts_newconsortiaboss_damage`),
  nên **hiện không ảnh hưởng DB**; `Server_Config.Value` = 3.000.000.000 lưu `nvarchar` nên an toàn; `Pve_Info.FightPower` là chuỗi hiển thị (`"3000|10K|20K..."`) không phải số.
- Báo cáo chi tiết: `reports\type-audit.json`.

## 8. Bomb / Weapon UI trong trận (quan trọng)

Thiếu phần này thì các **vũ khí/đạn mới** sẽ không dùng được trong trận (client không tải được bullet/crater, server thiếu mask phá đất).

- Client tải asset theo `resourceSite` (`<SITE value="..."/>` trong `WebSite\config.xml`):
  `image/bomb/crater/<id>/crater.png`, `craterBrink.png`, `image/bomb/bullet/bullet<id>.swf`, `image/bomb/blastOut/blastOut<id>.swf`.
- Server cần mask phá đất `<id>.bomb` (định dạng: int32 width, int32 height, 1bpp, stride = (width>>3)+1, MSB first, bit=1 là ô bị phá).
- Nguồn asset bomb tốt nhất: `https://res.gn.zing.vn/` (có đủ crater/brink/bullet/blastOut; CDN `gunny.vcdn.vn` thiếu brink/blastOut).
- Mask `.bomb` mới được tạo từ `crater.png` bằng ngưỡng **alpha ≥ 128** (đã đo khớp ~99.8% so với mask gốc của publisher trên 98 id kiểm chứng; round-trip `.bomb → PNG → .bomb` giống 100%).
- Sau khi deploy mask, **restart Fight/Road service**.

## 9. Kết quả lần chạy ngày 2026-11-09

- **Rip**: 367 tên request → 272 file tải/cập nhật từ nhà phát hành (61 file mới/cập nhật, 211 không đổi), 95 tên không có trên server (404, file local-only), 0 lỗi; **58 file template được promote** vào `WebSite\Request` (92 file giống hệt giữ nguyên).
- **Mapping**: 261 XML mirror → **152 template import được** (70 exact, 44 auto, 36 override, 2 warn), 109 file runtime/none.
- **Import DB**: **152/152 bảng import thành công, 0 lỗi**; backup 155 bảng `<Table>_bak_20261109_*`.
  - `Ball` 1648 → **1840**, `BallConfig` 808 → **916**, `Shop_Goods` 15150 → **18774**, `Shop` → **7081**,
    `Shop_Goods_Box` 35277 → **44614** (loadboxtemp), `Quest` → **2063**, `NPC_Info` → **3068**,
    `Game_Map` → **584**, `Pve_Info` → **246**, `Server_Config` → **1905**.
- **Bomb / vũ khí trong trận** (chạy 2026-11-09):
  - Id bomb tham chiếu: **1615** (185 crater id từ BallList ∪ 1565 bomb id từ bombconfig; 2 ball không có Crater nên không cần mask).
  - Trước khi chạy: **1458/1615** id đã có mask → thiếu **155** mask cần thiết.
  - Đã mirror **2621 file asset** vào `WebSite\image\bomb` (crater.png 204, craterBrink.png 203, bullet*.swf 383, blastOut*.swf 1831), 0 lỗi.
  - Đã tạo & deploy **171 id / 652 file mask**: Fight 2296→**2451**, Road 2296→**2451**, Full Source debug bin 2280→**2451** mỗi thư mục; **0 id không có nguồn**; không ghi đè file cũ.
  - Kiểm chứng: 2296/2296 mask đúng cấu trúc; round-trip `.bomb→PNG→.bomb` 120/120 giống hệt; mask tạo từ crater.png khớp mask gốc publisher **99,77% trung bình / 99,26% thấp nhất** (98 id).
  - Lưu ý: server nạp mask theo **ball id** (`bomb\{ballInfo.ID}.bomb`), nội dung lấy từ `crater.png` của crater mà ball trỏ tới → tool tự resolve crater nguồn (fallback chính id đó).
  - `SITE` trong `WebSite\config.xml` đang là `http://gunny.vcdn.vn/`: thiếu `blastOut` (152/152) và `craterBrink` (21/21) cho các vũ khí mới; `https://res.gn.zing.vn/` có đủ 173/173 → nên mirror (đã mirror local) hoặc đổi SITE sang `https://res.gn.zing.vn/`.
- **Kiểm chứng**: chạy lại đúng các stored procedure server dùng (`SP_Ball_All`, `SP_Ball_Config_All`, `SP_ItemsBox_All`,
  `SP_Maps_All`, `SP_NPC_Info_All`, `SP_Quest_All`, …) đều trả dữ liệu mới, không lỗi; website request (`:88`) serve XML mới;
  báo cáo QA độc lập: `reports\VERIFY-REPORT.md`.

## 9.1. Hiệu năng (chống giật/lag)

Tool quét nhiều file request + nhiều bảng CSDL nên đã tối ưu:
- **Cache dùng chung**: danh sách XML đã parse và kết quả map bảng được cache trong `StudioContext`
  (`GetRequestDocs`, `MapDoc`, `InvalidateRequestCaches`) — Dashboard/Import/Bomb/Rip không parse lại 275 file / map lại 567 bảng mỗi lần mở trang.
  Cache tự xoá sau khi Rip/promote, giải mã, hoặc đổi cấu hình.
- **Log throttling**: trang đang ẩn không nhận log vào UI; status bar chỉ cập nhật ~6-7 lần/giây (thay vì mỗi dòng log).
- **DataGridView double-buffer** + `SuspendLayout` khi nạp hàng nghìn dòng; bóng card giảm còn 1 lớp vẽ.
- **Quét chạy nền** (`Task.Run`) + cập nhật tiến trình có throttle (10-20 mục/lần), không block UI thread.
- **Lazy page**: 6 trang chỉ được tạo khi mở lần đầu (không build hết lúc khởi động).
- **Đo thực tế** (máy DESKTOP-SMLR4VU): khởi động tới lúc hiện cửa sổ **~1,5 giây (lần đầu)** / **~0,23 giây** các lần sau
  (trước tối ưu: ~9,7 giây); CPU khi rảnh **0%** (trước: ~37% do log đẩy UI + quét nền lặp lại); RAM ~480-520 MB (sau khi nạp đủ dữ liệu).
- Nếu máy yếu vẫn thấy nặng: giảm "Số luồng tải" ở tab Rip (4), tắt "Phân tích map bảng CSDL" ở trang Giải mã khi chỉ cần xem nội dung,
  hoặc dùng Bomb "Chỉ hiện id thiếu mask".

### 9.3. Sự cố 2026-11-09 tối: mail attach crash + Fight.exe không khởi động

- Triệu chứng: nhận đính kèm thư (vũ khí mới 7247) → Road.exe **NullReferenceException** `MailGetAttachHandler.cs:207`; restart service thì Fight.exe chết
  (`MapMgr maps:0, mapInfos:0`) rồi Road chết (`DevilTurnMgr.Init false`).
- Nguyên nhân: (1) service chạy từ 16:11 nạp template **trước khi import 18:51** → template 7247 không có trong RAM → `userItemSingle.Template` null;
  (2) import publisher ghi đè **cột server-local** (`Game_Map.PosX/PosX1`, `Ball.HasTunnel/Delay`, `NPC_Info.X/Y...`) và đổ dữ liệu vào các bảng
  sự kiện không tương thích code local (EF `Percent` float thay vì real, `RewardItemID` int vs string, NULL `AddPropertyByMoney`...).
- Đã sửa: patch `MailGetAttachHandler` (null-check + log rõ ràng, không crash handler), khôi phục cột server-local từ backup/DB gốc, revert 4 bảng sự kiện,
  restart Center → Fight → Road. Kiểm chứng: 3 service lên, Error.log sạch, template 7247 + BallConfig/Ball 14601x nạp OK.
- **Tool đã được nâng cấp để không tái diễn**: mục 7.1 (giữ cột server-local, skip bảng bảo vệ, server-safety check) + 7.2 (audit kiểu dữ liệu).
  Quy trình đúng: **Rip → Kiểm tra audit/DRY-RUN → Import → Restart service**.

## 10. Ghi chú

- `GunTools.exe` cũ (trong `C:\Gunny\ToolGunny\GunTools\...`) chỉ tải XML + sinh file SQL, không tự chạy DB, không phân biệt template/dynamic,
  và không xử lý bomb. Tool mới thay thế hoàn toàn và an toàn hơn (transaction + backup + report).
- `tool rip bomb` (`DDT_Bomb_Convert.exe`) gốc đã obfuscate/không còn dùng được (build .NET 2.0, host res cũ hay timeout);
  logic của nó được tái hiện trong tab **Bomb / Weapon**.
- Không đổi `WebSite\config.xml` tự động: tool chỉ **cảnh báo** nếu `SITE` đang trỏ host thiếu asset; muốn dùng mirror local
  thì sửa `SITE` thành `http://127.0.0.1/` (asset đã được mirror vào `WebSite\image\bomb\`) rồi test lại client.

## 11. Lực chiến cá nhân (FightPower) — đã chuyển int → long

Từ 2026-11-09 tối, **lực chiến cá nhân** đã được nâng lên 64-bit để hỗ trợ > 2.147.483.647:

- **DB**: `Db_Tank.dbo.Sys_Users_Detail.FightPower` = `bigint` (backup: `Sys_Users_Detail_bak_fpbigint`);
  `SP_Users_Update @FightPower bigint`; các view đã refresh. `Sys_Users_Rank_Date` vẫn là int vì cột đó lưu **thứ hạng**, không phải lực chiến.
- **Server**: `PlayerInfo.FightPower`, `GamePlayer.FightPower/WashFightPower` = `long`; công thức tính dùng long/double, bỏ chặn int.MaxValue;
  các DTO (FriendInfo/MarryInfo/Consortia*) = long; AcademyMgr/GetTimeDelay = long; GM API snapshot = long.
- **Gửi client**: giữ tương thích client cũ bằng cơ chế có sẵn `FightPower + FightPower_Show`:
  - `FightPower_PacketInt` = clamp int (tối đa 2.147.483.647)
  - `FightPower_PacketShow` = `FightPower_Show + (FightPower - PacketInt)`
  → client hiển thị tổng `int + long` (AS3 Number, tới 2^53) nên vẫn đúng khi > 2.1 tỷ.
- Các packet chỉ có 1 trường int (consortia list, một số room list, fighting proxy room, center rank) vẫn clamp 2.147.483.647
  — muốn các màn đó vượt 2.1 tỷ cần nâng protocol + patch client (giai đoạn sau).
- Kiểm chứng: đọc/ghi 3.000.000.000 qua `PlayerBussiness` OK; split packet cho tổng 3.680.195.045 đúng;
  3 service chạy, Error.log 0 byte. Chi tiết: `reports\FIGHTPOWER-LONG-REPORT.md`, script: `_dev\repair\fp-long\`.

- **Website Request** (`WebSite\Request`): Bussiness.dll cũ đọc FightPower kiểu int nên bị InvalidCast sau khi DB đổi bigint
  → gây lỗi client "注册形象失败1"; đã rebuild Bussiness.dll tương thích bigint (clamp) + bổ sung handler còn thiếu
  `FightNoviceRecord.ashx` (`Tank.Request.Extra.dll`). Backup/rollback: `_dev\repair\fp-long\web\`.

### Sự cố 2026-11-10 sáng: SaveTimer lưu player đang login

- Log Road: `NullReferenceException at GamePlayer.saveTeamSelfActive` (playerID 7) do SaveTimer quét player vừa được
  `WorldMgr.AddPlayer` nhưng `LoadFromDatabase()` chưa xong (`TeamSelfActive` còn null, NickName rỗng).
- Đã sửa: thêm cờ `Loaded`, `SaveIntoDatabase` bỏ qua player chưa load; `SaveTimerProc` skip; null-guard
  `saveTeamSelfActive`/`saveTeamDayActive`; build/deploy Road kèm PDB (log có số dòng).
- Chi tiết: `_dev\repair\road-save-race\ROAD-SAVE-RACE-FIX.md`, script `patch-save-race.py`.
