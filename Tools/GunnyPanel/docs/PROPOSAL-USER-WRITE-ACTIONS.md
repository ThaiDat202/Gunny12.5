# ĐỀ XUẤT — Thao tác GHI cho trang Người dùng

> **TRẠNG THÁI (đã được duyệt ngày 06/11/2026):**
> - ✅ **ĐÃ TRIỂN KHAI**: Ban / Unban + sửa Xu (Money) / Vàng (Gold) / GP / Level (Grade)
>   + **xoá vật phẩm soft-delete** + **gửi vật phẩm (Award qua hộp thư)**.
> - Kick player vẫn cần Road GM API (xem `PROPOSAL-ROAD-GM-API.md`), chưa có.
>
> Các thao tác đã triển khai tuân thủ: chỉ role `admin`/`gm`, xác nhận NickName + tick offline,
> từ chối khi nhân vật đang online (trừ Award qua mail), transaction/row-count check, audit giá trị cũ → mới.


## 1. Phạm vi đề xuất (theo mức ưu tiên)

| # | Thao tác | Cách làm | Ghi chú rủi ro |
|---|---|---|---|
| 1 | ✅ **Ban / Unban tài khoản** (đã làm) | `EXEC dbo.SP_Admin_ForbidUser` với `IsExist=0/1`, `ForbidDate`, `ForbidReason` | Player đang online vẫn chơi được đến khi relogin; cần kick qua Road API hoặc chờ relogin |
| 2 | ✅ **Sửa tiền tệ/level** (đã làm) | `UPDATE Sys_Users_Detail SET Money, Gold, GiftToken (Xu khóa), Grade` (Level 1–70) | Server giữ dữ liệu trong RAM → **panel từ chối nếu State>0**; yêu cầu tick xác nhận offline |
| 3 | ✅ **Xoá vật phẩm** (đã làm) | Soft-delete: `UPDATE Sys_Users_Goods SET IsExist=0 WHERE ItemID=@id AND UserID=@uid` (kiểm tra quyền sở hữu + offline) | Có xác nhận trước khi xoá; audit |
| 4 | ✅ **Gửi vật phẩm (Award)** (đã làm) | `EXEC dbo.SP_Admin_SendUserItem` → tạo item + mail `User_Messages` (Type=51); **chọn item từ DB** (search + lọc category theo `Db_Gm.dbo.Category`), hỗ trợ Count/Bind/ValidDate + **4 chỉ số (0–50)** + **đúc vàng N ngày** | Giới hạn theo Category: đúc vàng Cat 5/7; chỉ số Cat 5/7/8/14/17; server-side validate + AJAX TemplateInfo/SearchItems |
| 5 | **Kick player** | **Không làm được chỉ bằng DB** — cần Road GM API | Nếu chưa có API: chỉ ban DB, player bị đẩy ra khi relogin |

## 2. Nguyên tắc an toàn đề xuất

- Mọi thao tác ghi đi qua **job** (job queue sẵn có) → có log/tiến độ/huỷ.
- **Transaction** + kiểm tra row count (affected = 1) trước khi commit.
- **Audit log** ghi: user panel, hành động, tham số, giá trị cũ → giá trị mới.
- **Xác nhận 2 bước** trên UI: nhập lại NickName/ItemID + tick “Tôi hiểu đây là dữ liệu thật”.
- **Whitelist** thao tác/cột; không cho chạy SQL tự do.
- **Backup trước khi sửa hàng loạt**: hiện có sẵn `Db_Tank_Orig` (3267 MB) và `Db_Tank_All_Test`;
  có thể test trên bản `_Test` trước.
- Giới hạn role: `admin` (Develop) và `gm` (Admin); `viewer` chỉ xem.

## 3. UI dự kiến

```
/Users/Details?id=1
  [Ban]  [Unban]  [Sửa tiền/level]  [Kick]   ← chỉ admin/gm
  Vật phẩm: mỗi dòng có [Xoá] (soft delete) + [Gửi thêm item]
```

Khi bấm sẽ mở modal xác nhận + chọn lý do; job chạy nền, kết quả hiện trong Jobs.

## 4. Test plan (trước khi bật trên dữ liệu thật)

1. Test trên DB copy (`Db_Tank_Test` tạo từ backup) hoặc `Db_Tank_All_Test` cho phần Arena/Server.
2. Test ban → login lại bị chặn; unban → login lại OK.
3. Test sửa Money khi offline → relogin thấy đúng; khi online → xác nhận hành vi autosave.
4. Test xoá item → item biến mất khỏi túi, không lỗi load nhân vật.
5. Kiểm tra audit + rollback SQL (script undo sinh kèm).

## 5. Trạng thái duyệt & kết quả test

- ✅ **Đã duyệt & triển khai** mục 1–2 (Ban/Unban + sửa Xu/Vàng/GP/Level) trên trang
  `/Users/Details?id=...` (chỉ admin/gm thấy khung thao tác).
- ✅ Đã test trực tiếp trên `Db_Tank` với user `test1` (đang offline):
  - Sửa Xu +1000, Vàng +1, GP +1 → DB đúng → **restore lại giá trị gốc**;
  - Ban 1 ngày → `IsExist=0`, `ForbidDate` tương lai, `ForbidReason` đúng → **Unban** → `IsExist=1`;
  - Nhập sai NickName → từ chối (không đổi DB);
  - User role `viewer` → **không thấy** khung thao tác và bị chặn ghi;
  - Audit ghi đủ `user.edit`, `user.ban`, `user.unban` (old → new).
- ⏳ **Cần duyệt tiếp**: không còn mục nào trong đề xuất này (đã làm 1–4). Kick player nằm ở đề xuất Road GM API.
- ✅ Đã test Award: gửi 1× TemplateID 7008 → tạo mail (Type=51, Annex1=ItemID) + goods row; sau test đã **dọn dữ liệu test**.
- ✅ Đã test Xoá item: soft-delete ItemID=1 → `IsExist=0` → **restore `IsExist=1`** sau test.

## 6. Ghi chú khi vận hành

- **Trước khi sửa Xu/Vàng/GP/Level: cho nhân vật offline.** Nếu `State > 0` panel sẽ từ chối tự động.
- Sau khi sửa, người chơi cần **đăng nhập lại** để thấy dữ liệu mới (dữ liệu đang nằm trong RAM server).
- Ban khi đang online chỉ chặn ở lần đăng nhập sau — muốn đẩy ngay cần Road GM API (đề xuất riêng).
- Nên backup DB trước khi sửa hàng loạt; có sẵn `Db_Tank_Orig` / `Db_Tank_All_Test` để đối chiếu.

