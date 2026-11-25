# Web Mail Viewer — trang `/Mail` (chỉ đọc)

> Trạng thái: **hoàn thành Phase C.3** (07/11/2026) · màn hình xem hộp thư người chơi
> `Db_Tank.dbo.User_Messages` trong GunnyPanel · **read-only, chỉ SELECT**.

## 1. Mục tiêu & phạm vi

- Xem hộp thư người chơi (mail thường, mail GM/Award, quà VIP/Event…) trực tiếp trong panel.
- Tìm/lọc theo người nhận, tiêu đề/nội dung, loại thư, trạng thái đọc, khoảng thời gian.
- Xem chi tiết 1 thư: nội dung đầy đủ, flags, quà tặng (Gold/Money/GiftToken), annex (vật phẩm đính kèm).
- Thống kê nhanh trong phạm vi lọc: tổng thư, chưa đọc, phân bố `Type`, tổng Gold/Money/GiftToken.
- **Không có chức năng xoá/sửa mail.** Xoá mail (dù chỉ mail test) là thao tác ghi DB → cần **duyệt riêng**
  trong một task/phase khác. Trang không có form POST, không gọi `ExecuteAsync`, không chứa câu lệnh ghi.

## 2. File

| File | Nội dung |
|---|---|
| `src/GunnyPanel/Pages/Mail.cshtml` | UI tiếng Việt: bộ lọc, thống kê, bảng danh sách, chi tiết, CSS riêng prefix `mail-` trong `<style>` |
| `src/GunnyPanel/Pages/Mail.cshtml.cs` | `MailModel` (PageModel) — chỉ SELECT, tham số hoá toàn bộ |
| `docs/WEB-MAIL-VIEWER.md` | Tài liệu này |

Không sửa file khác trong repo ngoài 2 file trên (+ tài liệu này). Nav sidebar do Lead thêm.

## 3. Dữ liệu & truy vấn

### Bảng chính
`Db_Tank.dbo.User_Messages` — 30 cột: `ID, SenderID, Sender, ReceiverID, Receiver, Title, Content, SendTime,
IsRead, IsDelR, IfDelS, IsDelete, Annex1..Annex5, Gold, Money, IsExist, Type, Remark, ValidDate,
Annex1Name..Annex5Name, SendDate, AnnexRemark, GiftToken`.

### Join / tra cứu phụ (đều SELECT)
| Bảng | Mục đích |
|---|---|
| `Db_Tank.dbo.Sys_Users_Detail` (`UserID`, `NickName`) | Hiện NickName người nhận/người gửi; lọc theo `user` |
| `Db_Tank_All.dbo.Shop_Goods` (`TemplateID, Name, CategoryID, Pic, NeedSex`) | Tên/ảnh vật phẩm đính kèm (annex) |
| `Db_Gm.dbo.Category` (`CategoryID, Name`) | Tên thể loại item (Đạo cụ, Trang bị…) |
| `Db_Tank.dbo.Sys_Users_Goods` (`ItemID, TemplateID`) | Fallback khi Annex của thư `Type=51` là **ItemID** (xem 3.2) |

### 3.1. Không dùng `TRY_CONVERT` được — lưu ý quan trọng
`Db_Tank` và `Db_Tank_All` có **compatibility level = 100** (SQL Server 2008), nên `TRY_CONVERT`/`TRY_CAST`
**không tồn tại** (đã kiểm tra: `'TRY_CONVERT' is not a recognized built-in function name`).
Vì vậy việc chuyển `AnnexN` (chuỗi) → int được làm bằng:
`LTRIM(RTRIM(AnnexN)) <> '' AND LTRIM(RTRIM(AnnexN)) NOT LIKE '%[^0-9]%'` rồi mới `CAST(... AS int)`
(khi tra cứu ID trong C#, dùng `int.TryParse`).

### 3.2. Ý nghĩa cột `Annex1..Annex5`
- **Thư thường / quà VIP / thư hệ thống** (Type 8, 83, …): `AnnexN` là **TemplateID** (chuỗi) của item,
  `AnnexNName` là tên item (nếu server đã lưu).
- **Thư GM gửi vật phẩm/Award từ panel** (Type 51): SP `dbo.SP_Admin_SendUserItem` ghi `Annex1 = ItemID`
  (identity của `Sys_Users_Goods`) vào mail, `Remark` chứa chuỗi `Gold:..,Money:..,Annex1:<ItemID>,Annex2:..`.
  Vì vậy trang tra theo thứ tự:
  1. `AnnexN` → `Shop_Goods.TemplateID`;
  2. nếu không thấy và thư `Type=51` → `AnnexN` → `Sys_Users_Goods.ItemID` → `TemplateID` → `Shop_Goods`;
  3. nếu vẫn không thấy → khớp **theo tên** `AnnexNName` = `Shop_Goods.Name` (ảnh chỉ mang tính tham khảo,
     UI ghi rõ “Khớp theo tên trong Shop_Goods (ảnh tham khảo)”);
  4. không tra được → hiện tên từ `AnnexNName` (nếu có) hoặc `TemplateID <giá trị>`.

> Ghi chú dữ liệu test hiện tại: nhiều mail có `AnnexN` trống nhưng `AnnexNName` có tên (ví dụ
> “Túi quà Vip2”, “LEGEND-Thánh Kiếm Theia”) và các ID nhỏ trong dữ liệu cũ **không khớp**
> `Shop_Goods.TemplateID`; fallback theo tên giúp hiện được tên + ảnh tham khảo. Mail GM Panel
> (Type 51) hiện có `Annex1` trống nên hiển thị “Thư không có annex”.

### 3.3. Cờ trạng thái
`IsRead` (đã đọc), `IsDelR` (người nhận xoá), `IfDelS` (người gửi xoá), `IsDelete` (xoá hẳn),
`IsExist` (còn tồn tại). Mặc định trang lọc `IsDelete = 0`; checkbox **“Gồm thư đã xoá”** bỏ điều kiện này.

## 4. Chức năng & tham số URL

Route: `GET /Mail` (yêu cầu đăng nhập cookie như mọi trang trong `Pages/`; read-only nên không kiểm tra role).

| Tham số | Ý nghĩa |
|---|---|
| `user` | UserID hoặc NickName người nhận (so khớp `ReceiverID` chính xác / `Receiver`, `Sys_Users_Detail.NickName` LIKE) |
| `q` | Từ khoá tiêu đề hoặc nội dung (`Title`/`Content` LIKE, tham số hoá) |
| `type` | Lọc theo `Type` (dropdown tự sinh từ dữ liệu thực tế) |
| `read` | `0` = chưa đọc, `1` = đã đọc, bỏ trống = tất cả |
| `from`, `to` | Khoảng ngày gửi (`SendTime`), định dạng `yyyy-MM-dd`; `to` lấy trọn ngày (so sánh `< ngày kế tiếp`) |
| `del` | `1` = gồm thư đã xoá (`IsDelete=1`), mặc định ẩn |
| `sort` | `time` (mặc định: `SendTime DESC, ID DESC`), `id` (`ID DESC`), `old` (cũ nhất trước) |
| `page` | Trang danh sách, PageSize = **20** (dùng partial `_Pager`) |
| `sel` | ID thư cần xem chi tiết; giữ nguyên toàn bộ bộ lọc + trang hiện tại |

### Nhãn `Type` (đã đối chiếu dữ liệu thực tế)
| Type | Nhãn hiển thị | Ghi chú dữ liệu |
|---|---|---|
| 51 | Thư GM (vật phẩm/Award) | Mail do panel/SP `SP_Admin_SendUserItem` tạo khi gửi Award |
| 8 | Quà VIP/Event | Mail quà tăng cấp VIP (“Túi quà tăng cấp VIP n”) |
| 83 | Thư hệ thống/GM | Mail “G.M phần thưởng” do hệ thống GM gửi |
| khác | Loại N | Nhãn sinh tự động |

Thống kê nhanh luôn tính **trong phạm vi bộ lọc hiện tại** (cùng WHERE với danh sách, không phụ thuộc trang).

## 5. UI

- Card bộ lọc: user, từ khoá, loại thư, trạng thái đọc, từ ngày/đến ngày, sắp xếp, checkbox gồm thư đã xoá;
  nút “Tìm / Lọc”, “Xoá lọc”. Lọc sai định dạng ngày → hiện cảnh báo và bỏ qua điều kiện ngày.
- Card thống kê: 6 ô (tổng, chưa đọc, đã xoá hẳn, tổng Gold/Money/GiftToken) + pills phân bố `Type`
  (kèm số chưa đọc).
- Card chi tiết (khi có `?sel=`): thông tin thư (ID, người gửi/nhận + link `/Users/Details?id=`), loại,
  SendTime/SendDate/ValidDate, flags, Gold/Money/GiftToken, **nội dung đầy đủ** (giữ xuống dòng),
  danh sách annex (tên + ảnh + slot / TemplateID / category / nguồn khớp), `Remark`/`AnnexRemark`.
  Nút “Đóng chi tiết” giữ nguyên bộ lọc.
- Bảng danh sách: ID, người nhận, người gửi, tiêu đề + trích nội dung, loại, thời gian, đọc/xoá flags,
  Gold/Money/GiftToken, số annex, nút “Xem chi tiết”. Phân trang 20 dòng bằng `_Pager`.
- CSS riêng đặt trong `<style>` của `Mail.cshtml`, prefix `mail-` (không sửa `site.css`).
- Badge “Chỉ đọc (SELECT)” + ghi chú xoá mail cần duyệt riêng ngay trên UI.

## 6. An toàn

- 100% câu lệnh trên trang là `SELECT`/`COUNT`/`SUM` trên connection `Db_Tank` (join chéo DB chỉ đọc).
- Mọi input người dùng → tham số SQL (`@user`, `@qLike`, `@type`, `@isRead`, `@fromDate`, `@toDate`,
  `@skip`, `@take`, `@id`, `@tpl*`, `@item*`, `@nm*`); không nối chuỗi giá trị người dùng vào SQL.
- `sort`/`type` chỉ nhận từ whitelist (`SortOrders`); phân trang clamp 1..tổng số trang.
- Không có form POST, không handler ghi DB, không audit ghi (vì không thay đổi dữ liệu).
- Xoá mail: **chưa làm** — cần duyệt riêng; gợi ý khi được duyệt: soft-delete `IsDelete=1`/`IsDelR`
  theo đúng ID + audit + xác nhận 2 lớp, không hard-delete.

## 7. Kiểm thử

Build: `dotnet build src\GunnyPanel\GunnyPanel.csproj -c Debug` → **0 lỗi**
(2 warning CS8604 có sẵn từ `Pages/Db.cshtml.cs`, không thuộc trang Mail).

Smoke test runtime (port riêng 94, login `admin / Admin@123`), dữ liệu thực tế tại thời điểm test:
`User_Messages` có **23 dòng** (Type 8 = 13, Type 51 = 7, Type 83 = 3; tất cả `IsRead=1`, `IsDelete=0`;
UserID 1 = test1, 2 = test2; Gold/Money/GiftToken đều 0).

| URL | Kết quả |
|---|---|
| `/Mail` | HTTP 200 (46.434 bytes) · 20 dòng · pager 1/2 · tổng 23, chưa đọc 0 |
| `/Mail?user=1` | 200 · 16 dòng (test1) |
| `/Mail?user=test2` | 200 · 7 dòng (test2) |
| `/Mail?q=GM` | 200 · 7 dòng (Thư GM/Award) |
| `/Mail?type=51` | 200 · 7 dòng · thống kê theo phạm vi lọc |
| `/Mail?type=83` | 200 · 3 dòng |
| `/Mail?read=0` | 200 · 0 dòng + thông báo “Không tìm thấy thư phù hợp” |
| `/Mail?read=1` | 200 · 23 dòng (pager 1/2) |
| `/Mail?del=1` | 200 · 23 dòng (bao gồm cả `IsDelete`, hiện chưa có dòng nào bị xoá) |
| `/Mail?page=2` | 200 (20.431 bytes) · 3 dòng · dòng 21–23/23 · pager “Trang 2/2” |
| `/Mail?page=9` | 200 · clamp về trang 2 |
| `/Mail?from=2026-11-07&to=2026-11-07` | 200 · 8 dòng (theo SendTime) |
| `/Mail?from=qua-khu` | 200 · cảnh báo sai định dạng ngày, bỏ qua lọc ngày |
| `/Mail?user=1&q=VIP&type=8&read=1&sort=id` | 200 · 13 dòng; link chi tiết giữ đủ filter |
| `/Mail?sel=2` | 200 · chi tiết thư ID 2, annex “Túi quà Vip2”, ảnh `…/unfrightprop/vipjunior/icon.png` (khớp theo tên) |
| `/Mail?sel=14` | 200 · chi tiết thư ID 14 (Type 83), annex “LEGEND-Thánh Kiếm Theia” |
| `/Mail?sel=262` | 200 · chi tiết thư ID 262, annex “Bùa Hộ Thân” |
| `/Mail?sel=331` | 200 · chi tiết thư GM Panel, “Thư không có annex” |
| `/Mail?sel=999999` | 200 · cảnh báo “Không tìm thấy thư ID=999999.” |
| `/Mail?page=2&sel=2` | 200 · vừa giữ trang 2 vừa mở chi tiết |

Ghi chú kỹ thuật đã xử lý trong lúc test:
- **Không bind `page` bằng `[BindProperty]`**: Razor Pages dùng route value tên `page` cho route trang
  (`/Mail`) nên route value lấn query string → bind thất bại. `MailModel` đọc trực tiếp
  `Request.Query["page"]`; `_Pager` vẫn dùng tham số `page` bình thường.
- `AnnexCount` + SQL list phải có khoảng trắng trước `FROM` (lỗi syntax `Incorrect syntax near 'dbo'`
  đã sửa trong quá trình test).

## 8. Hạn chế / việc tiếp theo

- **Chưa xoá/sửa mail** (đúng phạm vi read-only); xoá mail test cần duyệt riêng.
- Chưa xuất CSV danh sách thư (có thể thêm sau nếu cần).
- Chưa resolve annex cho thư `Type=51` nếu `Annex1` trống dù `Remark` có `Annex1:<ItemID>` —
  có thể bổ sung đọc `Remark` khi cần (hiện UI vẫn hiện chuỗi `Remark` gốc).
- Ảnh item lấy theo CDN `Vng:ItemImageBase` (`https://gunny.vcdn.vn/`); offline sẽ không có ảnh
  (UI tự remove `<img>` lỗi, chỉ còn khung 📦).
- `ValidDate` hiển thị số ngày thô như trong DB (0 thường = vĩnh viễn, tuỳ server).
