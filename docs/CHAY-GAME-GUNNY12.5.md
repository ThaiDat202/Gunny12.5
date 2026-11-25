# Chạy game từ `C:\Gunny\Gunny12.5`

> Cập nhật: 2026-11-21 22:40 · Trạng thái: **website + panel đã chạy**,
> 3 service cần start bằng quyền **Administrator**.

---

## 1. Vị trí & thành phần

```
C:\Gunny\Gunny12.5\                 ← thư mục chạy game (bản sao lưu 21/11, đã sửa path)
C:\Gunny\BK_Gunny12.5\              ← junction tạm → Gunny12.5 (IIS đang trỏ vào đây)
C:\Gunny\Gunny13.0\                 ← bản cũ, không dùng (có thể xoá sau khi kiểm tra)
```

| Thành phần | Trạng thái |
|---|---|
| IIS site `Gunny` (port 80) | 200 OK — `http://127.0.0.1/Index.html`, `2.png` (SWF đã patch 64-bit) |
| IIS site `Request` (port 88) | 200 OK — `ActiveList.ashx`, `ServerConfig.ashx` |
| Panel GM (port 90) | 200 OK — `http://127.0.0.1:90/api/health` |
| Center / Fight / Road | **chưa start** (cần Administrator) |
| Database | dùng chung SQL Server `DESKTOP-SMLR4VU` (Db_Tank, Db_Tank_All, Db_GM, Db_Count, Db_Membership) |

---

## 2. Các path đã sửa trong `Gunny12.5`

| File | Key / nội dung |
|---|---|
| `Tools\GunnyPanel\app\appsettings.json` | `MailNoticeScript`, `GameRoot`, `WebRequestDir`, `MirrorDir`, `BombDir`, `RoadServiceConfig`, `CenterServiceConfig`, `ApkDir`, `DataDir`, `LogDir` |
| `Service\Service\Road\Road.Service.exe.config` | `<add key="Request" ...>` |
| `Website\Web.config` | `ReqPath` |
| `Website\Request\Web.config` | `ReqPath` |
| `Tools\GunnyPanel\app\data\jobs.json` | 67 đường dẫn trong lịch sử job scheduler |
| `Tools\GunnyPanel\src\GunnyPanel\appsettings.json` | template cấu hình |
| `RUN-SERVICES.bat`, `RUN-127.0.0.1-90.bat`, `Service\Service\Run.bat` | tiêu đề + đường dẫn |
| `Tools\Tank.Flash-Source\README-SOURCE.txt` | tài liệu |

Toàn bộ đã trỏ về `C:\Gunny\Gunny12.5\...` (không còn `BK_Gunny13.0`, `Gunny13.0`, `BK_Gunny12.5` trong file cấu hình).

---

## 3. Start server (bắt buộc Administrator)

```bat
C:\Gunny\Gunny12.5\RUN-SERVICES.bat        ← khuyến nghị (tự xin UAC, chờ SQL, start đúng thứ tự)
hoặc
C:\Gunny\Gunny12.5\Service\Service\Run.bat  ← bản mới, tự chứa, kiểm tra port
```

> **Vì sao phải chạy Administrator?** `Center.Service` mở WCF tại `http://+:2008/CenterService/`;
> chạy bằng user thường sẽ lỗi `AddressAccessDeniedException` (đã gặp khi test), Center không lên
> → Road báo `Connect 127.0.0.1:1593 failed!` rồi tự stop.

Port cần thấy sau khi start: `1593` (Center), `7539` (Fight), `8527` + `9601` (Road).

Panel GM:
```bat
C:\Gunny\Gunny12.5\RUN-127.0.0.1-90.bat      → http://127.0.0.1:90
```

---

## 4. (Tuỳ chọn) Trỏ IIS thẳng vào Gunny12.5

Hiện IIS đang trỏ `C:\Gunny\BK_Gunny12.5\Website` (junction tạm do lần đổi tên trước).
Muốn sạch (không phụ thuộc junction):

```bat
C:\Gunny\FIX-IIS-Gunny12.5.bat      ← chạy Administrator
```

Script sẽ: đổi physical path các site/app/virtual-dir từ `BK_Gunny12.5` → `Gunny12.5`,
`iisreset /restart`, gỡ junction `BK_Gunny12.5`, rồi test lại 2 URL.

---

## 5. Ghi chú

* `Database\` trong Gunny12.5 chứa 8 bản backup `.bak` (6 FULL + 2 CLEAN) chụp lúc 21:50 — dùng để
  restore nếu cần; game hiện dùng DB trực tiếp trên SQL Server.
* File `Run.bat` cũ (gọi `run-stack.ps1` không tồn tại) đã được thay bằng bản tự chứa.
* Bản `Gunny13.0` là thư mục cũ — sau khi xác nhận Gunny12.5 chạy ổn có thể xoá để giải phóng ~2,8 GB.
* Nếu đổi tên thư mục lần nữa: sửa lại 9 file ở mục 2 + physical path IIS (hoặc chạy lại script tương tự).
---

## 6. Start nhanh tất cả (khuyến nghị)

```bat
C:\Gunny\Gunny12.5\START-ALL.bat      ← chạy Administrator (tự xin UAC)
```

Script làm 3 việc:
1. Bảo đảm junction `C:\Gunny\BK_Gunny12.5` → `Gunny12.5` (IIS đang trỏ vào đường dẫn này);
2. Start Center → Fight → Road (qua `Service\Service\Run.bat --no-pause`);
3. Start panel GM + mở `http://127.0.0.1:90`.

Kiểm tra sau khi chạy: `1593`, `7539`, `8527`, `9601` phải LISTEN; mở `http://127.0.0.1` thấy trang game.