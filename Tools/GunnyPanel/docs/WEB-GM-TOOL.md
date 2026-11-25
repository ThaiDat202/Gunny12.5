# Web GM Tool — port từ desktop

## Vị trí

- Trang: `http://127.0.0.1:91/Users/Details?id=<user>`
- Khu vực: **Công cụ GM — Set Full**
- Chỉ hiện với user/role **Admin/Develop**.
- Không ảnh hưởng app desktop (`GunnyDesktopTool`).

## Chức năng

- Cấp Vua Gà (tự level 70)
- Bảo Vệ (tự level 50)
- Vinh Dự
- Nhẫn Cầu Hôn
- Sổ Tay Thám Hiểm
- Danh Hiệu
- Hỗ Trợ Lên Cấp / Tẩy Luyện
- Tiềm Năng
- Phụ Ma / Đúc Hồn (chỉ Nhẫn/Vòng Tay đang trang bị)
- Châu Báu
- Ma Thạch
- Bộ Sưu Tập
- Full Album Thẻ
- **SET FULL USER**: chạy lần lượt từng bước, có progress, không reload trang.

## Chống timeout

- Mỗi nút là 1 request AJAX riêng tới:
  `POST /Users/Details?handler=GmTool`
- Set Full chạy tuần tự từng feature, cập nhật progress.
- Đã test Châu Báu: 65 mail + 6 mũi khoan trong **0.18s**.
- Nếu sau này có tác vụ rất nặng, có thể chuyển sang `JobQueue` của panel.

## An toàn

- CSRF token tự động (`__RequestVerificationToken`).
- Chỉ `Admin/Develop` (`CanWrite`).
- Audit log: `user.gm.<feature>`.
- Xác nhận trước khi Set Full.

## Online / notify

- **v0.26**: mỗi nút GM gọi **Road GM API** (`127.0.0.1:9601`) để áp thay đổi **live** cho user đang online
  (in-memory + tính lại chỉ số + packet) — **không cần đăng nhập lại**. Nếu API tắt/lỗi thì fallback
  ghi DB như cũ (khi đó user cần relogin). Chi tiết: [`ROAD-GM-API.md`](ROAD-GM-API.md).
- **Châu Báu / Ma Thạch**: do Road GM API xử lý cả online lẫn offline — mở 6 drill live, mail châu báu;
  Ma Thạch gửi qua `PlayerBussiness` nên **đủ 6 chỉ số** khi mở thư.
- Fix gốc v0.26: **Vinh Dự cộng HP** (sửa `PlayerEquipInventory` trong `Game.Server.dll`) — trước đó
  code bỏ sót biến `hp` nên Blood của Vinh Dự (và HP từ trang bị/avatar/mark) không được cộng.
- Feature dạng mail khi offline vẫn vào hộp thư; user online có thể gọi thêm `MailNotice` (CenterService)
  nếu cần client cập nhật hộp thư ngay.

## Test

- Page `Users/Details?id=1`: có `gm-tool-card`, đủ nút.
- AJAX feature `king`: OK.
- AJAX feature `beads`: OK, 65 mail + 6 drill; đã dọn data test.
- Build: 0 lỗi.
- Panel process: **PID 1528**, port `91`.

## Toast (v0.28)

- Khi bấm từng nút GM: toast góc phải chỉ hiện
  `Đã nâng cấp tối đa <Tên tính năng> thành công` (✓) hoặc
  `Đã nâng cấp tối đa <Tên tính năng> thất bại` (✗).
- Chi tiết kỹ thuật/lỗi live nằm ở dòng tiến trình (`#gm-progress`, hover để xem) + audit log.
## v0.29 — Thêm nút port từ Form1 desktop

Các nhóm nút mới trong **Công cụ GM — Set Full** (đều qua Road GM API, online + offline):

| Nhóm | Nút |
|---|---|
| Tu luyện | Cơ Bản · TT Cấp 2 · Nguyên Tố |
| Thẻ bài | Lên Cấp · Đột Phá |
| Vật tổ | Lên Cấp · Tăng Bậc |
| Miếu thần | Chúc Phúc · Thăng Hoa |
| Dát vàng | Vũ Khí · Nón · Quần Áo |
| Tăng sao | Vũ Khí · Nón · Quần Áo |
| Khác | Chiến Hồn · Chế Tạo — Tạo · Tiến Hoá VK Phụ |

Luật đã sửa: Phụ Ma/Đúc Hồn chỉ item **đang trang bị** (cho phép Nhẫn Cầu Hôn); Hỗ Trợ bắt buộc
trang bị Bùa Hộ Thân; Tiềm Năng chỉ Kính/Mắt/Mặt/Tóc/Set; Ma Thạch mail đủ 6 chỉ số
(154 mỗi chỉ số, Exp 1.134.400); Bộ Sưu Tập online 1 click full.

## v0.30 — UI 3 cột giống Form1 + thêm Dây Chuyền

- Layout: 3 cột **Túi / Cá Nhân / Tiệm Rèn**, nhóm + thứ tự nút y như Form1 desktop.
- Nhãn nút ngắn như Form1; toast dùng `data-gm-label` để hiển thị tên đầy đủ
  (vd nút "Đột Phá" ở nhóm Cấp Vua Gà → "Đã nâng cấp tối đa Cấp Vua Gà thành công").
- 2 feature mới: `necklace_train` (Luyện, necklaceExp=630000) và `necklace_cast`
  (Đúc, necklaceCastLevel=40) — online dùng `NecklaceStrengthMgr.UpdateNecklaceInfo/SendNecklaceCastInfo`.
- Road API: 35 feature. Screenshot: `_tools/screenshots/gm-tool-v030.png`.

## v0.31 — Căn giữa nút trong nhóm

- Nhóm 1 nút: nút 50% căn giữa; nhóm 2 nút: 2 cột đều; nhóm 3 nút: 3 cột đều (CSS grid,
  class `count-1/count-2/count-3`). Screenshot: `_tools/screenshots/gm-tool-v031c.png`.

## Bổ sung — luật Dát Vàng (+12…+15) và kết quả 4 nút cuối

- Sau khi user trang bị đủ, 4 nút cuối đã **chạy live thành công**:
  `gold_weapon`, `gold_hat`, `gold_cloth` → "Đã dát vàng Vũ Khí/Nón/Quần Áo.";
  `subweapon_evolve` → "Đã tiến hoá VK phụ lên level tối đa." ⇒ **33/33 feature đã test live OK**
  (trước khi áp luật cường hoá).
- **Luật mới (theo yêu cầu user)**: Dát Vàng chỉ cho phép khi trang bị có **cường hoá +12 đến +15**.
  - Online: kiểm tra `item.StrengthenLevel` → <12 hoặc >15 báo
    `Cần cường hoá +12 đến +15 để dát vàng (hiện tại +N).`
  - Offline: query `Sys_Users_Goods.StrengthenLevel` tương tự.
  - Đã verify: user2 item +5 → API trả đúng message trên; item +15 → thành công.
- Lưu ý: các món của test1 đã được dát vàng trước khi áp luật (đang +0) nên vẫn giữ trạng thái gold;
  lần sau muốn dát lại phải cường hoá +12…+15.

## Luật Dát Vàng cuối cùng (theo yêu cầu user) — 2026-11-07 20:17

- Chỉ **Nón (category 1, ô 0) / Quần Áo (5, ô 4) / Vũ Khí (7, ô 6) ĐANG TRANG BỊ** mới được dát vàng;
  item trong túi hoặc ô khác thể loại → báo lỗi, không dát.
- Nếu `StrengthenLevel < 12` → **tự set lên +12 rồi dát vàng**.
- Nếu `+12 … +15` → **giữ nguyên strengthen hiện tại**, chỉ thêm dát vàng.
- Đã verify:
  - Offline user2: Nón +5 → sau khi bấm thành **+12 + IsGold=1**; ô không có đồ → báo không có trang bị.
  - Live user1: `gold_weapon/gold_hat/gold_cloth` OK (trước đó +0 → được set +12 khi dát).
- **Live test cuối: 33/33 feature OK, 0 lỗi** (log `data/live-test-v029-20261107-201706.json`).
  DB sau live: PhuMa=5, DucHon=5 (2 nhẫn + 2 vòng tay + Nhẫn Cầu Hôn), PotentialApplied=6/Wrong=0,
  Avatar 111 group, Amulet=50, Texp/CardInfo/Totem/Temple/Gemstone đúng.
