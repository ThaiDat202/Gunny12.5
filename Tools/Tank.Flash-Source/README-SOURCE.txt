TANK.FLASH - SOURCE ĐÃ DECOMPILE (GUNNY 13.0)
=================================================

Vị trí: C:\Gunny\Gunny12.5\Tools\Tank.Flash-Source

Đây là source decompile từ:
  C:\Gunny\Gunny12.5\Website\bin\Tank.Flash.dll
(assembly xử lý checkuser / register / các trang đăng nhập-đăng ký của website)

CÁCH SỬA
- Mở các file .cs và sửa chuỗi tiếng Trung / tiếng Việt tùy ý.
- Các file quan trọng:
  * Tank.Flash\checkuser.cs       -> lỗi tài khoản/mật khẩu
  * Tank.Flash.auth\register.cs   -> đăng ký thành công / thất bại / mã xác nhận
  * Tank.Flash\playgame.cs, logingame.cs, LoadingManager.cs, ...
- Không cần sửa file .csproj nếu chỉ đổi chuỗi.

BUILD LẠI
  dotnet build "C:\Gunny\Gunny12.5\Tools\Tank.Flash-Source\Tank.Flash.csproj" -c Release
  Output: ...\Tank.Flash-Source\bin\Release\net472\Tank.Flash.dll

Sau khi sửa xong: báo Lead, Lead sẽ build và copy DLL vào:
  - C:\Gunny\Gunny12.5\Website\bin\Tank.Flash.dll
  - C:\Gunny\Gunny12.9\Website\bin\Tank.Flash.dll
(Đã có backup DLL gốc tại C:\Gunny\ToolGunny\GunnyPanel\_tools\tankflash-backup.)

GHI CHÚ
- Source Tank.Flash hiện KHÔNG còn chuỗi tiếng Trung nào (đã dịch các thông báo
  login/register chính). Nếu giao diện còn một câu tiếng Trung khác, câu đó
  thường nằm ở một trong các file sau:
    * C:\Gunny\Gunny12.5\Website\bin\Languages\Language-vn.txt
    * C:\Gunny\Gunny12.5\Website\ui\vietnam\xml\*.xml
    * C:\Gunny\Gunny12.5\Website\movingnotification.txt
    * C:\Gunny\Gunny12.5\Website\Register.html
- Gửi ảnh/nội dung câu tiếng Trung còn sót, Lead sẽ chỉ đúng file + key để sửa.
