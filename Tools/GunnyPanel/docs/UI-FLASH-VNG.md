# Đồng bộ UI client theo Flash NPH VNG (10/11/2026)

## 1. Vấn đề

UI trong game (Sảnh, icon EVENT/CHIẾN BẢO/THƯƠNG MẠI/Đạo sặc/Nhiệm Vụ/Định Thân Tà + nút GM)
đang bị **cũ / lẫn bản local** so với bộ Flash của nhà phát hành VNG.

Nguyên nhân đã tìm ra (2 lớp):

1. **2 file bundle UI gốc vẫn là bản cũ** trong `WebSite`:
   - `WebSite\2.png` = bundle cũ (mã hoá, 3.327.001 B)
   - `WebSite\4.png` = bundle cũ (mã hoá, 7.196.761 B)
   - Trong khi `1.png/3.png/5.png` đã là bản VNG mới (plain `CWS`, 326.480 / 2.717.101 / 1.931.065 B).
   - `2.png/4.png` là **module code** (`DDTPassInfoGameLoading.GAME = "2.png"`,
     `CodeModuleLoader.addLoadURL("4.png")`), nên được xử lý riêng.
2. **Cache Flash Player trên máy đang giữ UI cũ từ client dev 04/11**:
   - `flash-2-png.sol` = 3.310.533 B  (≈ bundle local cũ 3.310.489 B + 44)
   - `flash-4-png.sol` = 7.150.543 B  (≈ bundle local cũ 7.150.499 B + 44)
   - Nhiều `.sol` khác (ddthallicon, game.swf, ddtroom...) có size khớp bản UI cũ ở
     `Desktop\Dev Game\...` chứ không khớp bản VNG mới trên server hiện tại.

## 2. Đã làm

### 2.1. Thay 2.png / 4.png bằng bản NPH VNG

Nguồn publisher (đã đối chiếu **1/3/5 khớp hash tuyệt đối** với bản VNG đang chạy):

| File | Bản cũ | Bản VNG mới | Backup |
|---|---|---|---|
| `WebSite\2.png` | 3.327.001 B (encrypted) | 3.662.701 B (CWS) | `data\ui-vng-backup\20261110-1655\2.png` |
| `WebSite\4.png` | 7.196.761 B (encrypted) | 8.318.992 B (CWS) | `data\ui-vng-backup\20261110-1655\4.png` |
| `WebSite\Flash\2.png` | như trên | như trên | `...\Flash_2.png` |
| `WebSite\Flash\4.png` | như trên | như trên | `...\Flash_4.png` |

Hash bản mới: `2.png = 4756C3E892FB4524…`, `4.png = 244C194BAB098E62…`
(root và `Flash\` giống nhau); HTTP trả về `magic=CWS`.

Kiểm tra hợp lệ:
- Header `CWS` version 28, length declared == actual (2.png 8.311.460 B; 4.png 19.895.686 B).
- FFDec đọc được: `2.png` → 4.141 script; `4.png` → 8.974 script.
- Client `ModuleLoader.handleModule`: `NewCrypto.decry` trả nguyên bản nếu không có flag `^_^`,
  sau đó kiểm tra 3 byte `C,W,S` → `loadBytes` luôn. Vì vậy **plain CWS được client nhận**
  (đúng như 1/3/5.png hiện tại).

### 2.2. Bổ sung file publisher còn thiếu

- `WebSite\Flash\ui\vietnam\bones.png` (5.167 B) — lấy từ `FlashVNG`, đã thêm vào
  `WebSite\Flash\ui\vietnam\` (và bản root tương ứng đã có).
- Đã so toàn bộ `FlashVNG` với `WebSite\Flash`: chỉ thiếu đúng file này.

### 2.3. Xoá cache Flash cũ

- Backup: `GunnyPanel\data\ui-cache-backup\20261110-1700\` (172 file `.sol`).
- Xoá **170 file cache asset** (`flash-*`, `image-*`, `cmfriendicon-*`, `ui-*`,
  `sound-*`, `xml-*`, `Loading*`) trong:
  `%APPDATA%\Macromedia\Flash Player\#SharedObjects\72PAW9JY\127.0.0.1\`
- Giữ `#7road\files.sol` (index) và `Flash\Loading.swf\road.sol` (settings).
- Lần đăng nhập tới client sẽ tải lại UI từ server (VNG mới), có thể lâu hơn lần đầu.

## 3. Test

1. Thoát hẳn game (không chỉ về Sảnh).
2. Mở lại, đăng nhập.
3. UI Sảnh / icon / toolbar / loading phải là bộ VNG mới (không còn giao diện cũ).
4. Nếu client báo lỗi hoặc trắng UI: chạy rollback bên dưới rồi báo lại (kèm log/ảnh).

## 4. Rollback

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Gunny\ToolGunny\GunnyPanel\_tools\rollback-ui-2-4.ps1"
```

Script khôi phục 4 file 2/4 cũ + cache `.sol` cũ, ghi log
`data\gmdev\rollback-ui-2-4.log`.

## 5. File/đường dẫn liên quan

- Client được IIS phục vụ tại `http://127.0.0.1/` (root `C:\Gunny\Gunny12.6\WebSite`),
  flash site = `http://127.0.0.1/flash/` → `WebSite\Flash`.
- Nguồn publisher đối chiếu: `WebSite\FlashVNG` (rip VNG 09/11) và
  `Desktop\Dev Game\...\WebSite\*.vng-bak` (rip VNG 04/11).
- `2.png` = module loading/game; `4.png` = code module UI — **không sửa nội dung bên trong**,
  chỉ thay bằng đúng bundle publisher + backup đầy đủ.
