# Sửa website Request (ASP.NET) sau khi đổi FightPower int → bigint

Hiện tượng (2026-11-09 22:56):

- Trong game báo **"注册形象失败1"** (đăng ký hình tượng thất bại) khi tạo nhân vật.
- Mở trực tiếp `http://127.0.0.1:88/fightnovicerecord.ashx?...` thấy **Parser Error: Could not create type 'Tank.Request.FightNoviceRecord'**.

## Nguyên nhân

1. **"注册形象失败1"**: sau khi đổi `Db_Tank.dbo.Sys_Users_Detail.FightPower` từ `int` → `bigint`,
   website Request vẫn dùng bản `WebSite\Request\Bin\Bussiness.dll` cũ (compile cho `PlayerInfo.FightPower` là `int`).
   Các hàm `PlayerBussiness.InitPlayerInfo`, `ActivePlayer`, `GetUserSingleByNickName` đọc cột bigint bằng
   `(int)reader["FightPower"]` → ném `InvalidCastException: Specified cast is not valid`.
   → `VisualizeRegister.ashx` bắt exception và trả `value="false"`; client hiện "注册形象失败1".
   Log: `WebSite\Request\logs\Error.log` có các dòng `InvalidCastException ... InitPlayerInfo/ActivePlayer`.

2. **Parser Error FightNoviceRecord**: file `FightNoviceRecord.ashx` (có sẵn trong package) trỏ tới class
   `Tank.Request.FightNoviceRecord` nhưng **class này không có trong `Tank.Request.dll`** của package
   (đã kiểm tra bằng reflection: DLL có `VisualizeRegister`, `novicenodedata`... nhưng không có `FightNoviceRecord`).
   Client (SWF `1 - Copy.swf` / `RegisterLuncher.sendTrainLog`) gọi handler này sau khi tạo nhân vật
   (params `userId`, `state`, `target`, `rnd`) và **không đọc nội dung trả về** — nhưng vẫn cần HTTP 200 + XML hợp lệ.

## Đã sửa

### 1. Rebuild `WebSite\Request\Bin\Bussiness.dll` (tương thích bigint)

- Decompile bản `Bussiness.dll` cũ (2022, 685.056 bytes) → build lại (net472) với duy nhất thay đổi:
  đọc `FightPower` bằng helper `FightPowerCompat.FromDb(object)` (nhận `bigint/Int64`, clamp về int.MaxValue).
- Các vị trí đã vá: `InitPlayerInfo`, `GetUserSingleBy*`, `GetUserLoginList` (qua InitPlayerInfo), friend list, marry info,
  consortia users/apply (cùng helper).
- File build: `Bussiness.dll` (652.288 bytes) — **đã deploy** vào `WebSite\Request\Bin\Bussiness.dll`
  (backup bản cũ: `WebSite\Request\Bin\_backup-fpweb-YYYYMMDD-HHMMSS\Bussiness.dll`, và `Bussiness.dll.orig` trong thư mục này).
- DLL này biên dịch đúng với các dependency cũ trong `Request\Bin` (EntityFramework 6, Newtonsoft 6, log4net...),
  không cần đổi các DLL khác, không ảnh hưởng code Road mới.

### 2. Thêm handler `FightNoviceRecord`

- Tạo assembly `Tank.Request.Extra.dll` (chuỗi namespace `Tank.Request`) chứa class
  `Tank.Request.FightNoviceRecord : IHttpHandler`:
  - Nhận `userId`, `state`, `target`, `rnd`.
  - Ghi log nhẹ vào `WebSite\Request\logs\fightnovicerecord.log` (nếu ghi được).
  - Trả về XML: `<Result value="true" message="Success!" />`.
- Đã deploy vào `WebSite\Request\Bin\Tank.Request.Extra.dll` (ASP.NET tự tìm class trong các assembly ở Bin).

### 3. Recycle app domain

- Thay DLL trong Bin + cập nhật LastWriteTime `Web.config` → ASP.NET tự recycle. Không cần iisreset.

## Kiểm chứng

| Test | Kết quả |
|---|---|
| `GET /fightnovicerecord.ashx?userId=test1&rnd=0.123&state=14&target=01` | 200, `<Result value="true" message="Success!" />`; log `logs\fightnovicerecord.log` |
| `GET /NickNameCheck.ashx?NickName=test01` | 200, `value="true" message="chúc mừng! tên nhân vật có thể sử dụng."` (trước đó ném InvalidCast) |
| `GET /GetSID.ashx` | 200, XML RSA key |
| `GET /ServerList.ashx` | 200, danh sách server |
| `GET /UserRankDate.ashx?userId=1` | 200, XML rank |
| `GET /VisualizeRegister.ashx` (account đã có nhân vật) | 200, `value="false" message="Register Player Fail!"` (đúng business, không exception) |
| Account mới (IsFirst=0) + `VisualizeRegister.ashx` | 200, `value="true"`; tạo đủ nhân vật + 7 item khởi đầu; sau test đã xoá user test |

- `WebSite\Request\logs\Error.log` không còn `InvalidCastException`.
- Test user tạo ra đã được **xoá sạch** khỏi Db_Tank (126 bảng có cột UserID được quét).

## Rollback

1. Copy `Bussiness.dll.orig` → `WebSite\Request\Bin\Bussiness.dll`.
2. Xoá `WebSite\Request\Bin\Tank.Request.Extra.dll` (hoặc giữ, không ảnh hưởng).
3. Chạm `Web.config` / restart app pool.
Lưu ý: nếu DB vẫn để `FightPower bigint`, rollback này sẽ khiến lỗi InvalidCast quay lại.

## File trong thư mục này

- `Bussiness.dll` — bản build đã vá bigint-compat (deploy vào `WebSite\Request\Bin`).
- `Bussiness.dll.orig` — bản website gốc 2022 (để rollback).
- `Tank.Request.Extra.dll` + `Tank.Request.Extra.csproj` + `FightNoviceRecord.cs` — handler bổ sung.
- `FightPowerCompat.cs` — helper clamp bigint→int dùng trong Bussiness.
- Cách rebuild Bussiness: decompile `Bussiness.dll.orig` bằng ILSpy (`ilspycmd -p -o src Bussiness.dll.orig`),
  thêm `FightPowerCompat.cs`, thay các cast `(int)reader["FightPower"]` → `FightPowerCompat.FromDb(...)`,
  build project net472 tham chiếu các DLL cũ trong `Request\Bin` (loại trừ chính `Bussiness.dll`).
