# Project Context: Gunny (DDTank) Private Server & Client
- **Backend / Server:** C# (.NET Framework 4.0/4.5, Visual Studio solution: Game.Server, Game.Logic, Fighting.Server, Center.Server, Bussiness).
- **Frontend / Client:** ActionScript 3.0 (AS3 - Flash/AIR, sử dụng thư viện Pickgliss UI, Road.Flash).
- **Database:** Microsoft SQL Server (Db_Tank, Db_Membership, Db_Tank_All,...).
- **Quy tắc quan trọng:**
  1. Mọi thay đổi về cấu trúc gói tin (`ePackageType`) bắt buộc phải kiểm tra và đồng bộ thứ tự đọc/ghi byte giữa file Handler C# và file Socket/Manager bên AS3.
  2. Không dùng cú pháp C# 8.0+ (như pattern matching hiện đại, record, nullable reference types) để tránh lỗi biên dịch trên .NET Framework cũ.
  3. Code AS3 phải khai báo đầy đủ kiểu dữ liệu (`:int`, `:String`, `:Boolean`, `:void`) và đúng cấu trúc `package`.
