# Phân tích client 2.png / 4.png (bản cũ vs bản mới) — 10/11/2026

Nguồn SWF đã giải mã của người dùng:
- Cũ: `C:\Users\hotha\Desktop\2.png 4.png\2.png 4.png cũ\...\2.swf` / `4.swf`
- Mới: `C:\Users\hotha\Desktop\2.png 4.png\2.png 4.png mới\...\2.swf` / `4.swf`

Đã export script bằng FFDec vào `C:\Gunny\ToolGunny\_analysis\ui24\`.

## 1. Quy mô khác biệt

| Bundle | Cũ | Mới | Class mới | Class bị bỏ | Class chung bị sửa |
|---|---:|---:|---:|---:|---:|
| 2.png | 3.675 | 4.141 | +745 | -279 | 618 |
| 4.png | 7.439 | 8.974 | +1.975 | -440 | 2.338 |

Module mới xuất hiện nhiều: `activity`, `uigeneral`, `labyrinth`, `store`, `dragonMark`,
`soulMark`, `KongFuMeeting`, `biblenew`, `dragonsMagic`, `petSoulCall`, `conscrusade`,
`forcesbattle`, `caveloot`, `magicTailor`, `retreatingpvp`, `PetAvatarBattle`,
`PlayerSceneSuit`, `WarOrder`... → đây là **client version mới hơn hẳn**, không phải
chỉ đổi giao diện.

## 2. Test phối hợp 2/4 (môi trường test riêng, không đụng live)

Đã test bằng 37abc (Flash 26) và GunLauncher CEF (Flash 32), dùng đúng SWF người dùng giải mã:

| Cặp | Kết quả |
|---|---|
| old2 + old4 | OK — load đủ UI, vào game |
| **new2 + old4** | **Dừng ngay sau `2.png` (200), đen màn hình** |
| **old2 + new4** | **OK — load 23 file `ui/vietnam/swf/*`, vào game** |
| new2 + new4 | Dừng ngay sau `2.png` (đen) |

Thử thêm:
- Flash 26 và Flash 32: cùng lỗi với new2.
- `config.xml` của NPH (CDN) thay cho config local: vẫn lỗi new2.
- Không có exception JS/console; request `2.png` luôn 200.

## 3. Kết luận

- **`4.png` mới tương thích ngược** với client/server hiện tại: `old2 + new4` chạy được.
- **`2.png` mới là phần không tương thích**: chỉ cần thay new2 vào là client đứng ở bước load module,
  trước khi kết nối server / trước khi load UI tiếp theo. Đây là lỗi phía client module/version,
  không phải lỗi server trả file.
- Client mới (2.png) mang theo ~745 class mới và 618 class sửa; phần lớn là **hệ thống mới cần
  protocol/server mới**. Vì vậy "sửa vài dòng client" không đủ để chạy new2 với server cũ.

## 4. Các hướng khả thi

### A. Giữ server hiện tại — dùng old2 + new4 (đã chạy được)
- Có thể đưa `new4` vào live (backup cũ), giữ `old2`.
- Nhưng giao diện nhìn vẫn như cũ (new4 không đổi look Sảnh; ảnh so sánh old2+old4 và old2+new4 giống nhau).

### B. Backport UI mới vào old2 (khuyến nghị nếu chỉ cần giao diện NPH)
- Dùng **old2 (code cũ, tương thích server)** làm nền.
- Extract tài nguyên/hình ảnh/layout từ **new2**, inject vào old2 (FFDec).
- Không đổi protocol server; giữ nguyên server cũ.
- Cần chọn màn hình ưu tiên (Sảnh/Toolbar/...).

### C. Nâng server lên đúng version của client mới
- Cần **source/build server mới** tương ứng new2/new4 (protocol, handler, config).
- Không có source mới thì không thể "port mù".

### D. Patch riêng new2 để bỏ hệ thống mới
- Về lý thuyết làm được nhưng phải sửa/loại bỏ hàng trăm class mới + dependency;
  rủi ro cao, cần xác định chính xác lỗi load của new2 trước.

## 5. Trạng thái hiện tại
- Live `http://127.0.0.1/`: vẫn 2/4 cũ, hoạt động bình thường.
- Test site `http://localhost:8088/` + 37abc/GunLauncher dùng để thử; có thể đổi cặp 2/4 tùy ý.
