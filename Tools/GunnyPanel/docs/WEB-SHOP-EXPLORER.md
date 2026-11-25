# Web Shop Explorer — trang `/Shop`

Trang tra cứu read-only cho toàn bộ vật phẩm trong `Db_Tank_All.dbo.Shop_Goods`
(**15.060 item**), phục vụ dev/GM tra template, chỉ số, ảnh, thể loại.

## Vị trí

- Trang: `http://<panel>/Shop` (dev/test: `http://127.0.0.1:93/Shop`)
- File:
  - `src/GunnyPanel/Pages/Shop.cshtml`
  - `src/GunnyPanel/Pages/Shop.cshtml.cs`
  - `docs/WEB-SHOP-EXPLORER.md` (tài liệu này)
- Nav link `/Shop` do Lead thêm vào `_Layout.cshtml` (trang này không sửa layout).
- CSS riêng nằm trong `<style>` của `Shop.cshtml`, mọi class có prefix `shop-`.

## Nguồn dữ liệu (chỉ SELECT)

| Dữ liệu | Nguồn |
| --- | --- |
| Item | `Db_Tank_All.dbo.Shop_Goods` (conn `Db_Tank_All`) |
| Tên thể loại | `Db_Gm.dbo.Category` (LEFT JOIN theo `CategoryID`) |
| Fallback tên thể loại | `Db_Tank_All.dbo.Shop_Goods_Categorys` (LEFT JOIN theo `ID`) |
| Không có tên | hiển thị `Category <N>` |
| Ảnh item | base `Vng:ItemImageBase` (mặc định `https://gunny.vcdn.vn/`) |

Mọi câu lệnh đều là `SELECT`, có tham số hoá (`@q`, `@like`, `@cat`, `@quality`, `@skip`, `@take`).
Trang **không có form POST**, không ghi DB.

## Chức năng

- **Tìm kiếm `q`**: `Name LIKE`, `Remark LIKE`, **TemplateID chính xác** (so khớp chuỗi ID).
- **Lọc Category**: kèm số item mỗi thể loại (`CategoryID, COUNT(*) GROUP BY`), thứ tự theo CategoryID.
- **Lọc Quality**: kèm số item mỗi mức (`Quality, COUNT(*) GROUP BY`).
- **Sắp xếp**: TemplateID tăng dần (mặc định), Tên A→Z, Level cao→thấp, Quality cao→thấp, Theo thể loại.
- **Phân trang**: 24 item/trang, dùng partial `_Pager` (`GunnyPanel.Pages.Shared.Pager`);
  link phân trang giữ nguyên `q / cat / quality / sort / sel`.
- **Grid item**: ảnh, tên, TemplateID, thể loại, Quality/Level/MaxCount,
  tag hỗ trợ `Đúc vàng` / `4 chỉ số` / `Cấp độ`;
  ảnh thiếu hoặc lỗi tải (`onerror`) thì ẩn ảnh và hiện chữ `Không có ảnh` / `Ảnh lỗi`.
- **Xem chi tiết `?sel=<TemplateID>`** (giữ nguyên filter + trang):
  - Ảnh lớn, tên, thể loại, Quality, Level, tag hỗ trợ.
  - Thông tin chung: TemplateID, tên, thể loại, Level, Quality, MaxCount, NeedLevel,
    NeedSex, Valid, Count, AddTime.
  - Chỉ số & trang bị: Attack, Defence, Agility, Luck, Colors, Hole, SuitId, RefineryLevel.
  - Giá & thu hồi: FloorPrice, ReclaimValue, ReclaimType, BindType, FusionType, FusionRate, FusionNeedRate.
  - Cờ `Can*`: CanStrengthen, CanCompose, CanDrop, CanEquip, CanUse, CanDelete, CanRecycle, CanTransfer.
  - Nút **Copy TemplateID** (Clipboard API, fallback `document.execCommand('copy')`).
  - Nút **Mở rộng tất cả trường**: bảng 48 cột kỹ thuật `SELECT` được của `Shop_Goods`
    (Script, Data, Colors, Property1..8, Slot/Hole, Fusion*, BindType, Reclaim*, ...).
  - TemplateID không tồn tại → thông báo "Không tìm thấy TemplateID ... trong Shop_Goods".
- **Tag hỗ trợ** (giống mapping trong `UserDetail`):
  - Đúc vàng: Category 1 (Nón), 5 (Áo), 7 (Vũ khí).
  - 4 chỉ số: Category 1, 5, 7, 8 (Vòng tay), 9/29 (Nhẫn), 17 (Vũ khí phụ).
  - Cấp độ (0–15): Category 1, 5, 7, 17.

## Ghi chú kỹ thuật

- **Mapping ảnh item**: copy nguyên logic `BuildItemImageUrl` từ `Pages/UserDetail.cshtml.cs`
  (theo `CategoryID` + `Pic` + `NeedSex`: `NeedSex = 1` dùng thư mục `m`, còn lại `f`).
- **Collation**: `Db_Tank_All` dùng `SQL_Latin1_General_CP1_CI_AS`, `Db_Gm` dùng `Chinese_PRC_CI_AS`.
  Biểu thức `COALESCE(c.Name, s.Name, N'Category N')` và `GROUP BY ... MAX(...)` vì vậy
  phải bọc `COLLATE DATABASE_DEFAULT` cho từng cột tên, nếu không SQL Server báo
  *"Cannot resolve collation conflict"*. Trang đã áp dụng đúng và đã test.
- **Tham số `page`**: Razor Pages dùng route value tên `page` (đường dẫn trang), nên
  `[BindProperty(SupportsGet = true, Name = "page")]` bị route value che và `?page=N` bị bỏ qua
  (lỗi tiềm ẩn tương tự ở `Users`/`UserDetail` — ngoài phạm vi trang này, chưa sửa).
  Trang `/Shop` vì vậy đọc trang hiện tại bằng `OnGetAsync([FromQuery(Name = "page")] int page)`,
  đã test phân trang hoạt động.
- Có **42 TemplateID ≤ 0** (nhỏ nhất `-5600`) trong `Shop_Goods`; trang vẫn tra cứu chi tiết được.
- TemplateID `1101` có `Name` rỗng → hiển thị `(không tên)`.

## Test đã chạy

Môi trường: SQL Server `DESKTOP-SMLR4VU`, build Debug net10.0, chạy port riêng **93**
(`dotnet run --project src\GunnyPanel\GunnyPanel.csproj --no-build --no-launch-profile -- --Panel:Urls=http://127.0.0.1:93`),
login `admin / Admin@123`.

- Build: `dotnet build src\GunnyPanel\GunnyPanel.csproj -c Debug` → **0 lỗi**
  (2 warning CS8604 có sẵn ở `Pages/Db.cshtml.cs`, không liên quan).
- `/Shop` → 200, 54.575 bytes, 15.060 item, 628 trang, 24 item/trang, grid đủ 24 copy button.
- `?page=2` → "Trang 2/628", item bắt đầu `-2000`...; `?page=99999` → clamp về 628; `?page=abc` → trang 1.
- `?q=nhẫn` → 135 item, 6 trang; `?q=búa phá đá` → 1 item đúng tên; `?q=7001` → 1 item (khớp chính xác TemplateID).
- `?cat=9` (Nhẫn) → 184 item, 8 trang, option category được `selected`; pager giữ `cat=9`.
- `?quality=8` → 51 item.
- `?cat=39` → 8 item, tên fallback `Category 39`, ảnh hiện placeholder `Không có ảnh`.
- `?sel=7001` → card chi tiết đầy đủ: Attack 16, Hole `0,-1|...`, AddTime `13/07/2023 09:33`,
  ảnh `https://gunny.vcdn.vn/image/arm/axe/00.png`, tag Đúc vàng + 4 chỉ số + Cấp độ,
  bảng mở rộng 48 trường.
- `?sel=1101` → tag hỗ trợ đủ, tên rỗng hiển thị `(không tên)`.
- `?sel=380020` → thể loại fallback `Category 39`, không có ảnh → placeholder.
- `?sel=-5600` → tra cứu được TemplateID âm.
- `?sel=99999999` → thông báo không tìm thấy, trang vẫn 200.
- `?sel=7001&page=2` → vẫn giữ card chi tiết, pager giữ `sel=7001`.
- `?q=nhẫn&cat=9&quality=4&sort=quality&sel=9001` → chi tiết + filter giữ nguyên;
  link "Đóng chi tiết" trả về `/Shop?q=...&cat=9&quality=4&sort=quality`.
- CSS/JS: `<style>` trong `@@section Head` render đúng (`@media` không bị Razor nuốt),
  JS copy dùng `navigator.clipboard` + fallback `execCommand`, toggle `Mở rộng` hoạt động.

Snippet test nhanh (PowerShell):

```powershell
$base='http://127.0.0.1:93'
$s=New-Object Microsoft.PowerShell.Commands.WebRequestSession
$r=Invoke-WebRequest "$base/Login" -WebSession $s -UseBasicParsing
$tok=[regex]::Match($r.Content,'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
$r2=Invoke-WebRequest "$base/Login" -Method POST -WebSession $s -UseBasicParsing -Body @{ UserName='admin'; Password='Admin@123'; __RequestVerificationToken=$tok }
$r3=Invoke-WebRequest "$base/Shop" -WebSession $s -UseBasicParsing
Write-Host "status=$($r3.StatusCode) len=$($r3.Content.Length)"
```

## Hạn chế

- Chỉ đọc: không sửa/xoá/gửi item (ngoài phạm vi Phase C.1).
- Ảnh tải từ CDN `gunny.vcdn.vn`; máy không có internet thì browser hiện placeholder
  (`Không có ảnh` / `Ảnh lỗi`), trang vẫn hoạt động bình thường.
- Một số CategoryID không có tên ở cả 2 bảng (`39, 43, 63, 65, 68, 69, 70, 72, 78, 79, 81..93`)
  → hiển thị `Category N`; một số category không có mapping ảnh → placeholder.
- Chưa có bộ lọc NeedSex / CanEquip / CanUse / cấp độ... (ngoài yêu cầu); có thể bổ sung sau.
- Trang read-only nên không kiểm tra role; mọi tài khoản đã đăng nhập panel đều xem được.
- Nav link sidebar do Lead thêm; nếu chưa thấy mục "Shop" trong menu thì mở trực tiếp `/Shop`.
