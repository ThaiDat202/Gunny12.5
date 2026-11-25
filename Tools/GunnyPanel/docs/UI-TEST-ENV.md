# Môi trường test UI (2.png / 4.png bản VNG mới) — 10/11/2026

## Mục đích
Test riêng bộ UI VNG mới **không ảnh hưởng game live** (`http://127.0.0.1/`).

- Bản live: giữ nguyên 2/4 + cache cũ, đang chạy bình thường.
- Bản test: `C:\Gunny\Gunny12.6\WebSiteTest`, chạy IIS Express tại:
  **http://localhost:8088/**
- Trong bản test:
  - `2.png` / `4.png` (root + `Flash\`) = bundle publisher VNG mới
    (3.662.701 / 8.318.992 B, `CWS`).
  - `Web.config`: `LoginOnUrl`, `FlashUrl`, `FlashConfig`, `FlashSite` → `http://localhost:8088/...`
  - `config.xml` + `Flash\config.xml`: `FLASHSITE/SITE/FIRSTPAGE/...` → `http://localhost:8088/...`;
    `REQUEST_PATH` vẫn `http://127.0.0.1:88/` (dùng request server live).
  - Cache Flash theo host **localhost** (tách khỏi `127.0.0.1`), nên không dính cache cũ.
  - `PlayGame.aspx` có **logger overlay** góc trái dưới: bắt `window.onerror`,
    `unhandledrejection`, `console.error/warn`, `fetch`/`XHR` lỗi (status >= 400)
    và hiển thị trực tiếp trên màn hình.

## Cách dùng
1. Mở Chrome: `http://localhost:8088/`
2. Đăng nhập tài khoản game như bình thường.
3. Vào game → nếu lỗi, chụp **overlay đỏ góc trái dưới** + mở F12 → **Console** chụp lỗi.
4. Gửi ảnh/log lại để phân tích.

## Dừng / dọn
- Dừng IIS Express: kill process `iisexpress.exe` (PID ghi ở log hoặc `Get-Process iisexpress`).
- Có thể xoá `C:\Gunny\Gunny12.6\WebSiteTest` sau khi xong (bản copy ~0.89 GB).
- Game live không bị ảnh hưởng bởi thư mục/port này.

## Ghi chú
- Nếu overlay không hiện lỗi nhưng game vẫn đen: lỗi nằm trong Flash/Ruffle runtime
  (không phải JS) → cần Console F12 hoặc log Ruffle; gửi ảnh Console.
- `2.png`/`4.png` là module code; không sửa nội dung, chỉ thay nguyên bundle publisher.
