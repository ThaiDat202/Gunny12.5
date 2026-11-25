# Web Award picker fix (07/11/2026)

## 1. Không còn nhảy trang khi nhập TemplateID

- Trước: ô `TemplateID` dùng JS `window.location.href` + tự chuyển sau 650ms; nếu trang picker không nằm đúng iframe có thể làm trang ngoài đổi URL `/Users/Details?id=...`.
- Sau:
  - Bỏ hoàn toàn JS auto-redirect.
  - Thay bằng `<form method="get">` ngay trong iframe picker.
  - Form giữ `id`, `cat`, `picker=1`, `embed=1`; submit chỉ tải lại iframe, trang ngoài đứng yên.
  - Bấm `Xem` hoặc Enter trên form để chọn TemplateID.

## 2. Thêm tìm theo tên

Trong cửa sổ chọn vật phẩm có thêm:

- `Tìm theo tên` → input `name="q"` → lọc `Shop_Goods.Name LIKE %q%`.
- `TemplateID` → input `name="sel"` → xem đúng item.
- Kết hợp được cả tên + TemplateID + category.

Backend đã hỗ trợ `q` từ trước (`LoadAwardPickerAsync`: `g.Name LIKE @like`).

## 3. Thông báo gửi thành công

Đã rút gọn `SendItemCoreAsync`:

```
Vật phẩm đã gửi về người dùng
```

Không còn hiện tên TemplateID/category/số lượng/chỉ số.

- AJAX trả JSON: `{ ok: true, message: "Vật phẩm đã gửi về người dùng" }`.
- Toast ngoài hiển thị đúng câu này.

## 4. Test

- Build panel: **0 lỗi**.
- `GET /Users/Details?id=1&cat=0&picker=1&embed=1&q=Nhẫn`:
  - Có `award-name-search`, `name="q"`, `name="sel"`.
  - **Không còn** `window.location`, `data-base`.
- `POST /Users/Details?handler=SendItemAjax` với item test:
  ```json
  {"ok":true,"message":"Vật phẩm đã gửi về người dùng"}
  ```
- Đã xoá mail/item test sau khi test.

## 5. Panel hiện tại

- URL: `http://127.0.0.1:91`
- Process: **PID 28028**
- Login test: `admin / Admin@123`
- Đã khởi động lại từ đúng **content root**:
  `C:\Gunny\ToolGunny\GunnyPanel\src\GunnyPanel`
- Kiểm tra:
  - `/Login` → 200
  - `/css/site.css` → 200
  - `Error`/stderr rỗng.
- Trước đó panel bị dừng (connection refused); restart lại từ thư mục `bin` còn làm mất static files (CSS 404) → đã chạy lại từ project root.
