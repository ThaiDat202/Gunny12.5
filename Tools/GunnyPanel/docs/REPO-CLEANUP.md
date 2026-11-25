# Vinh Dự + Dọn code/DB (07/11/2026)

## 1. Vinh Dự Gunner — full level 100

- Hệ `Sys_Users_Honor` + `TS_HonorSystem_Template` (max level **100**).
- Nút **Vinh Dự Gunner → Lên Cấp** (`button41`):
  - `HonorLevel = 100`
  - `HonorExp = 0`
- Online cập nhật `PlayerCharacter.HonorInfo`, properties; offline SQL.
- User 1 hiện: `HonorLevel=100, HonorExp=0`.

## 2. Dọn code

- `Form1.cs`: xoá 2 method chết không còn dùng:
  - `CreateMaxMagicStone`
  - `GrantMaxMagicStones`
- Giữ các helper đang dùng: `ApplyMaxMagicStoneStats`, `GetMaxMagicStone`, `SendMailItem`, `OpenAllSixDrills`, `ApplyAmuletWash`.
- Build Road.Service: **0 lỗi**.

## 3. Dọn repo GunnyPanel

- `.gitignore` bổ sung:
  - `_tools/chrome-profile/`
  - `src/GunnyPanel/appsettings.json` (tránh commit mật khẩu DB)
  - `*.log`
- Tạo `src/GunnyPanel/appsettings.example.json`:
  - thay `DESKTOP-SMLR4VU` → `YOUR_SERVER`
  - thay `Password=123456` → `Password=YOUR_PASSWORD`
- Đã xoá:
  - `_tools/chrome-profile`
  - `data/jobs.json`, `data/sp2.txt`, `data/server*.log`, `data/logs/*`, `data/out/*`
- Giữ `_tools/screenshots`, `_tools/generator`, `capture.js`, `notify-mail.ps1`.

### Trước khi đẩy GitHub
```powershell
copy src\GunnyPanel\appsettings.example.json src\GunnyPanel\appsettings.json
# sửa YOUR_SERVER / YOUR_PASSWORD trong appsettings.json (file đã bị .gitignore)
git init
git add .
git status
```
- **Không commit** `appsettings.json` thật (đã ignore).
- Máy GitHub chưa cài `git`; cần cài Git for Windows trước khi push.

## 4. Dọn database

- Xoá 6 dòng `Sys_User_Rank` cũ không có trong `New_Title`.
- Xoá 4 mail test `GM Panel`/`TEST%` của user 1.
- Còn lại:
  - `Sys_User_Rank`: **548** danh hiệu hợp lệ.
  - `User_Avatar_Colection`: 111 bộ sạch, không duplicate.
  - `Sys_Users_Card`: 82 thẻ, 0 bad row.
  - Test mail = 0.

## 7. Cấp Vua Gà — thêm điều kiện level 70

- Nút **Cấp Vua Gà → Đột Phá** giờ:
  1. Nâng user lên **level 70** (điều kiện mở Cấp Vua Gà):
     - `Grade = 70`
     - `GP = 857997577` (mốc level 70 trong `LevelInfo`)
  2. Sau đó mới set Cấp Vua Gà:
     - `ddtKingGrade = 10`
     - 6 nhánh level 10.
- Online cập nhật `PlayerCharacter.Grade/GP`, `KingGrade.Data`; offline SQL.
- User 1: `Grade=70`, `GP=857997577`, `ddtKingGrade=10`.

## 8. Deploy hiện tại

- Process: **PID 32184**
- Port: `127.0.0.1:8527`
- Hash: `12279D13E15C489531B52B97F1C8AD59E465D910AF89C9412BD46C3CA3EF6550`
- Backup: `Road.Service.exe.bak-20261107-111340`
- Form1 mở; `Error.log` rỗng.


## 6. Fix lỗi bộ sưu tập phụ kiện

- Lỗi: `Invalid object name 'Cloth_Group_Template'`.
- Nguyên nhân: câu SQL con trong `button34_Click` chưa qualify database.
- Đã sửa thành:
  `Db_Tank_All.dbo.Cloth_Group_Template`.
- Test lại SQL: **111 bộ sưu tập** (58 nam + 37 nữ + 16 vũ khí), 0 bộ rỗng.

