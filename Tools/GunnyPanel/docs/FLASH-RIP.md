# FLASH-RIP — Tool Rip Flash VNG (`/FlashRip`)

Trang **`/FlashRip`** (sidebar **Rip Flash**) mirror toàn bộ flash UI của nhà phát hành VNG từ CDN
`https://gunny.vcdn.vn/flash/` về một thư mục local. Mục đích: lấy bộ flash chính thức mới nhất để so sánh /
cập nhật client, **không đụng tới 2 file core `2.png`, `4.png`** (gắn với code client).

## Cách dùng

> **Lưu ý khởi động panel:** phải chạy với ContentRoot là thư mục project để serve được CSS/JS/ảnh
> (`_tools\start-panel.ps1` đã làm sẵn). Nếu chạy `GunnyPanel.exe` trực tiếp từ `bin\Debug\net10.0`
> thì `/css/site.css`, `/img/*` sẽ 404 và giao diện mất style.

1. Mở **http://127.0.0.1:91/FlashRip** (đăng nhập admin).
2. Giữ mặc định (Nguồn = `https://gunny.vcdn.vn/flash/`, Reference = `WebSite\Flash`, Đích = `WebSite\FlashVNG`,
   Loại trừ = `2.png` + `4.png`).
3. Bấm **🔍 Quét thử (dry-run)** → biết CDN có bao nhiêu file, không tải gì.
4. Bấm **⬇ Tải thật về thư mục đích** → theo dõi tiến độ realtime, log, thống kê.
5. Xem **Kết quả rip**: số file OK / 404 / lỗi / bỏ qua / loại trừ, dung lượng, tốc độ, thống kê theo đuôi file,
   danh sách file lỗi/404; tải `_rip-report.json` hoặc `_rip-manifest.json` (SHA-256 từng file).
6. **Lịch sử rip** ở cuối trang: bấm “Xem” để mở lại report của từng lần chạy.

## Tham số

| Tham số | Mặc định | Ý nghĩa |
|---|---|---|
| Source | `https://gunny.vcdn.vn/flash/` | Gốc flash trên CDN VNG (http/https) |
| Reference | `C:\Gunny\Gunny12.6\WebSite\Flash` | Cây file local dùng làm **danh sách** cần rip (CDN không cho liệt kê thư mục) |
| Target | `C:\Gunny\Gunny12.6\WebSite\FlashVNG` | Thư mục đích — mặc định là folder **mới**, không ghi đè client đang chạy |
| Exclude | `2.png`, `4.png` | Mỗi dòng 1 pattern; so với tên file hoặc đường dẫn tương đối; hỗ trợ `*` (vd `ui/cn_trad/*`, `*.rar`) |
| Ghi đè | bật | Tắt = chỉ tải file còn thiếu; file đã có được **HEAD so size với CDN**, chỉ `skipped` khi khớp — lệch size/hỏng sẽ tải lại |
| Cho phép ghi đè trực tiếp | tắt | Mặc định **chặn** khi Target == Reference (bảo vệ client đang chạy). Chỉ bật khi thật sự muốn đồng bộ tại chỗ |
| MaxFiles | `0` (tất cả) | Giới hạn số file để thử nhanh |
| Concurrency | `8` | Số luồng tải song song (2–16) |

## Cách hoạt động

1. Quét **Reference** (đệ quy), bỏ các file khớp **Exclude** + bỏ file nội bộ `_rip-report.json`, `_rip-manifest.json`.
2. Với mỗi file: URL = `Source` + đường dẫn tương đối (percent-encode từng đoạn).
   - **Dry-run**: gửi `HEAD` → đếm file CDN có / 404 / lỗi (không ghi file).
   - **Tải thật**: `GET` song song, ghi `file.part` rồi `Move` nguyên tử (cleanup `.part` trong `finally`);
     404/403/410 → `missing` (bỏ qua); lỗi mạng/5xx → retry 2 lần rồi đánh `failed`. Không dừng cả job vì 1 file lỗi.
3. Ghi **`_rip-report.json`** (toàn bộ file + trạng thái + thống kê) và **`_rip-manifest.json`**
   (chỉ file OK: `path`, `url`, `size`, `sha256`) vào thư mục đích — ghi kiểu atomic (`.tmp` → `Move`).
4. Lưu **bản sao theo job** ở `data\rip\jobs\<jobId>-report.json` / `-manifest.json` để **xem lại lịch sử**
   không bị lần chạy sau (cùng Target) ghi đè; trang chỉ hiển thị report khi đúng `JobId` (chống nhầm report).
5. Job chạy nền qua **JobQueue** của panel (`/Jobs`), có thể huỷ giữa chừng; log + audit như các job khác.
6. Trước khi tải: pre-check dung lượng trống của ổ đích; chặn job trùng Target đang chờ/chạy (tránh tải 2 lần).
7. Phân quyền: role **Develop/Admin** mới được chạy/huỷ job; **Moderator** chỉ xem trang + report.

## Kết quả lần rip thật đầu tiên (2026-11-09)

- Nguồn: `https://gunny.vcdn.vn/flash/` → Đích: `C:\Gunny\Gunny12.6\WebSite\FlashVNG`
- **549 file OK / 159,2 MB / 27,5 giây** (5,8 MB/s) · **853 file CDN không có** (404) · **0 lỗi** · loại trừ **2 file** (`2.png`, `4.png`).
- Kiểm chứng: 549/549 file khớp size với manifest recon (`data/rip/_recon/flash-manifest.json`), không thừa file;
  hash mẫu (`1.png`, `Loading.swf`, `ui/vietnam/swf/game.swf`, `ui/vietnam/xml/game.xml`, `shape.swf`) trùng byte với CDN.
- `WebSite\Flash\2.png` (3.327.001 B) và `4.png` (7.196.761 B) **không bị sửa**.

## QA độc lập (2026-11-09)

- **88 kiểm tra: 87 PASS / 1 FAIL** — FAIL duy nhất là panel chạy sai ContentRoot khiến static assets 404
  (đã sửa bằng `_tools\start-panel.ps1`; CSS/ảnh giờ trả 200). Chi tiết: `data/rip/_verify/verify-report.md`.
- Các lỗi MED/LOW do QA phát hiện đã xử lý: attribution report theo job, verify size khi `Overwrite=false`,
  chặn Target == Reference (cờ “Cho phép ghi đè trực tiếp”), phân quyền role, atomic report, cleanup `.part`,
  manifest note, chặn job trùng Target, pre-check dung lượng.
- QA xác nhận: **SHA256 cả 549 file** khớp report; hash 5 file khớp CDN; `2.png`/`4.png` local hash = baseline trước rip.

## Lưu ý quan trọng

- **CDN không có directory listing** → tool chỉ rip được các file có trong **Reference**. Muốn thêm file ngoài reference,
  thêm path đó vào reference (hoặc copy danh sách cần rip vào 1 folder rỗng làm reference).
- **2.png / 4.png** trên CDN khác bản local (CDN: 3.661.530 / 8.320.764 B; local: 3.327.001 / 7.196.761 B)
  → tuyệt đối giữ loại trừ này khi định ghi đè client.
- Nếu đổi **Target = `WebSite\Flash`** (đồng bộ trực tiếp vào client), phải bật “Cho phép ghi đè trực tiếp”,
  và nên thêm `config.xml`, `config_vng.xml` + `2.png`, `4.png` vào Exclude — `config.xml` trên CDN trỏ về
  server test cũ (`10.10.4.148`), ghi đè sẽ làm client lỗi.
- **Khởi động panel đúng cách**: dùng `_tools\start-panel.ps1` (hoặc `run-test.bat`) để ContentRoot = thư mục project,
  nếu không toàn bộ `/css`, `/img`, `/js` sẽ 404.
- Tool chỉ bind localhost, dùng cookie auth của panel; không mở public.
