# User Detail — Ban chat & gửi thư tùy chỉnh (Phase B.1/B.2)

> Trạng thái: **hoàn thành (v0.25)** — bổ sung 2 thao tác ghi có xác nhận + audit trong
> `/Users/Details?id=`, dùng DB trực tiếp như các thao tác Ban/Edit hiện có.

## Vị trí

Khung **“Cấm chat & thư tùy chỉnh”** trên trang `/Users/Details?id=<user>` (chỉ role Admin/Develop).

## 1. Ban chat / Mở ban chat (B.1)

| Mục | Giá trị |
|---|---|
| Cột DB | `Db_Tank.dbo.Sys_Users_Detail.IsBanChat` (bit), `BanChatEndDate` (datetime) |
| Ban chat | `UPDATE ... SET IsBanChat = 1, BanChatEndDate = now + số_ngày WHERE UserID = @id` |
| Mở ban chat | `UPDATE ... SET IsBanChat = 0, BanChatEndDate = now WHERE UserID = @id` |
| Xác nhận | Nhập lại **NickName** (giống Ban/Edit/SendItem) |
| Giới hạn | Số ngày 1–3650 (`Math.Clamp`) |
| Audit | `user.banchat` (id, nick, days, until, reason, online), `user.unbanchat` |
| Hiệu lực | Server đọc cờ khi người chơi **đăng nhập** ⇒ nếu đang online cần **Kick** hoặc đăng nhập lại (UI có ghi chú) |

## 2. Gửi thư tùy chỉnh (B.2)

Gửi thư **không kèm vật phẩm** vào hộp thư (`User_Messages`), có thể kèm tiền tệ.

| Mục | Giá trị |
|---|---|
| Bảng | `Db_Tank.dbo.User_Messages` |
| Cách ghi | `INSERT` trực tiếp với `Type = 51` (thư GM hiển thị trong hộp thư), `Sender = 'GM Panel'`, `ValidDate = 240` |
| Trường nhập | Tiêu đề (≤500), Nội dung (≤4000), Vàng (`Gold`), Xu (`Money`), Xu khóa (`GiftToken`) — mỗi loại 0..2,147,483,647 |
| Xác nhận | Nhập lại **NickName** |
| Online | Nếu `State > 0`, gọi `CenterService.MailNotice(userId, zoneId)` (qua `_tools/notify-mail.ps1` như Award) để client cập nhật hộp thư ngay |
| Audit | `user.mail` (id, nick, title, gold, money, giftToken, mailNotified) |

> Ghi chú kỹ thuật: trong DB test, `SP_Admin_SendMail` bị **thiếu thân procedure**
> (chỉ có comment + khai báo biến, không có INSERT) nên **không dùng**; `SP_Mail_Send` cũng lỗi
> cú pháp khi EXEC. Vì vậy panel dùng `INSERT` trực tiếp với đúng danh sách cột mà proc gốc dùng.
> Nếu sau này DB được sửa lại proc, có thể chuyển sang `EXEC SP_Mail_Send`.

## An toàn

- Chỉ Admin/Develop (`CanWrite` — server-side).
- Bắt buộc nhập lại NickName; so sánh `OrdinalIgnoreCase` với DB.
- Validate khoảng giá trị trước khi ghi; tham số hoá SQL 100%.
- CSRF token tự động (form POST Razor Pages).
- Mọi thao tác ghi audit JSON line trong `data/logs/audit-*.log`.

## Test đã chạy (2026-11-07, user test2 / UserID=2)

| Test | Kết quả |
|---|---|
| `BanChat` 3 ngày, xác nhận đúng nick | 200, toast “Đã cấm chat test2 đến 10/11/2026 …”; DB `IsBanChat=True`, `BanChatEndDate=+3 ngày` |
| `UnbanChat` | 200, toast “Đã mở ban chat cho test2.”; DB `IsBanChat=False` |
| `BanChat` với NickName sai | 200 + lỗi “NickName xác nhận không khớp — không thực hiện ban chat.”; DB **không đổi** |
| `SendMail` (tiêu đề test) | 200, toast “Đã gửi mail …”; DB có bản ghi `Type=51`, đúng tiêu đề/nội dung |
| `SendMail` với NickName sai | 200 + lỗi “NickName xác nhận không khớp — không gửi mail.”; **không** tạo bản ghi |
| Audit log | `user.banchat`, `user.unbanchat`, `user.mail` đầy đủ |
| Dọn dữ liệu test | Đã xoá thư test khỏi `User_Messages`; `IsBanChat` trả về `False` (đúng trạng thái ban đầu) |

## File thay đổi

| File | Thay đổi |
|---|---|
| `src/GunnyPanel/Pages/UserDetail.cshtml.cs` | handlers `OnPostBanChatAsync`, `OnPostUnbanChatAsync`, `OnPostSendMailAsync` + `ErrorMail` |
| `src/GunnyPanel/Pages/UserDetail.cshtml` | khung “Cấm chat & thư tùy chỉnh” (2 form) |
| `docs/WEB-USER-EXTRAS.md` | tài liệu này |

## Hạn chế

- Ban chat là ghi DB — người chơi online cần đăng nhập lại/kick mới thấy hiệu lực (game không có
  packet cập nhật cờ chat live qua API hiện có).
- Thư tùy chỉnh không hỗ trợ đính kèm item (dùng **Award** cho item); nếu client đang online mà
  MailNotice lỗi, thư vẫn nằm trong hộp thư và người chơi thấy khi mở lại.
- Chưa có chức năng xoá thư trong panel (cần duyệt riêng).
