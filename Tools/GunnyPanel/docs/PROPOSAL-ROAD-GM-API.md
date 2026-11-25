# ĐỀ XUẤT — Road GM API (cần duyệt sửa `Service`/`Full Source`)

> Trạng thái: **✅ ĐÃ TRIỂN KHAI ở v0.26** — phương án **ít xâm lấn hơn**: thêm
> **HTTP JSON API** (`HttpListener`, 127.0.0.1:9601, token) host ngay trong `Road.Service`
> (không cần WCF GMService). Xem chi tiết: [`ROAD-GM-API.md`](ROAD-GM-API.md).
>
> Kèm 2 fix gốc đã deploy (build lại `Game.Server.dll`):
> - **Vinh Dự cộng HP** (sửa `PlayerEquipInventory.UpdatePlayerProperties` bỏ sót biến `hp`).
> - **Châu Báu 6 mũi khoan** mở live + **Ma Thạch** gửi mail đủ 6 chỉ số.
>
> Tài liệu gốc bên dưới giữ lại để tham khảo/rollback.

## 1. Hiện trạng (đã đọc source, không sửa)

- `Road.Service` là **WinForms/service** nhúng cả game server. GM hiện có 2 cách thao tác:
  1. **Form1.cs** (WinForms GM tool, 349 KB) — sửa item/user, đọc DB trực tiếp, chạy trong process.
  2. **Console commands** (`ConsoleStart.cs`): `cp` (xem clients/players/rooms/memory), `exit`,
     `shutdown`, `ball&reload`, `map&reload`, `prop&reload`, `item&reload`, `quest&reload`,
     `rate&reload`, `consortia&reload`, `fight&reload`…
- Có sẵn `Game.Service.GMService\IService.cs` (WCF) nhưng **chỉ có 1 hàm `ShowMessage(type,msg)`**
  và **chưa được host** (config không có service endpoint). Chưa có API HTTP để panel gọi.
- Vì vậy: **ban/unban** có thể làm thuần DB (xem proposal khác), nhưng **kick player, gửi thông báo,
  reload manager, xem trạng thái realtime** cần bổ sung API trong Road.Service.

## 2. Đề xuất kỹ thuật

### 2.1 Thêm WCF GM API vào Road.Service (giữ nguyên kiến trúc hiện tại)

- Mở rộng `Game.Service.GMService\IService.cs`:
  ```csharp
  [ServiceContract]
  public interface IService
  {
      [OperationContract] void ShowMessage(int type, string msg);          // có sẵn
      [OperationContract] string GetStatus();                              // JSON: clients/players/rooms/memory
      [OperationContract] bool KickPlayer(string token, string nickName);  // kick 1 player
      [OperationContract] bool Reload(string token, string cmd);           // ball/map/prop/item/quest/rate/consortia/fight
      [OperationContract] bool SendNotice(string token, int type, string msg);
  }
  ```
- `Service.cs`: implement bằng API in-process:
  - `GameServer.Instance.GetAllClients()` (đã dùng trong ShowMessage) → tìm client theo `Player.PlayerCharacter.NickName`
    (kiểm tra lại property trong `GamePlayer`/`PlayerInfo`) rồi `client.Disconnect()`.
  - `Reload(cmd)`: gọi đúng các `XxxMgr.ReLoad()` như trong `ConsoleStart.cs`.
  - `GetStatus`: trả JSON `{clients, players, rooms, playingRooms, memoryMb, time}`.
- Thêm `Game.Service.GMService\GMServiceHost.cs`:
  - `ServiceHost` với `basicHttpBinding` **bind 127.0.0.1:9601** (`http://127.0.0.1:9601/GMService`).
  - Chỉ start khi `GMService.Enabled = true` trong `Road.Service.exe.config`.
- `ConsoleStart.cs`: sau khi `GameServer.Instance.Start()` gọi `GMServiceHost.Start()`; dừng khi shutdown.
- `Road.Service.exe.config` thêm:
  ```xml
  <add key="GMService.Enabled" value="true" />
  <add key="GMService.Url" value="http://127.0.0.1:9601/GMService" />
  <add key="GMService.Token" value="<chuỗi random 32+ ký tự>" />
  ```

### 2.2 Bảo mật

- **Chỉ bind 127.0.0.1** (không mở firewall/public).
- Mọi operation (trừ `GetStatus`) yêu cầu `token` khớp config; sai → log + false.
- Panel gửi token qua header/param (token lưu trong appsettings panel, không hiện UI).
- Rate-limit đơn giản (1 request/giây/action) trong Service.
- Audit phía Panel cho mọi lần gọi; log phía Road.Service bằng `log4net` sẵn có.

### 2.3 Phía GunnyPanel (sau khi API có)

- Trang **Server** thêm nút: `Gửi thông báo`, `Reload manager`, `Kick player`, `Xem status realtime`.
- Trang **Users/Details** thêm nút `Kick` (gọi API) bên cạnh `Ban/Unban` (DB).
- Tạo `RoadGmClient` (HttpClient) + `road-action` job, có xác nhận + audit.

## 3. File dự kiến thay đổi (trong `Full Source\Road.Service`)

| File | Thay đổi |
|---|---|
| `Game.Service.GMService\IService.cs` | thêm operation contracts |
| `Game.Service.GMService\Service.cs` | implement Kick/Reload/Status/Notice |
| `Game.Service.GMService\GMServiceHost.cs` | **file mới** host WCF |
| `Game.Service.actions\ConsoleStart.cs` | start/stop host |
| `Road.Service.exe.config` / app.config | thêm 3 key GMService.* |
| (project file nếu cần) | thêm `GMServiceHost.cs` vào compile |

> Nếu bạn muốn ít xâm lấn hơn: có thể làm **HTTP listener riêng** (`HttpListener` trong ConsoleStart,
> port 9601) thay vì WCF — ít file hơn nhưng vẫn phải sửa `ConsoleStart.cs` + config.

## 4. Build / triển khai / rollback

1. Backup `Gunny12.6\Service\Service\Road` (đặc biệt `Road.Service.exe`, `.config`, `*.dll` sửa).
2. Build `Full Source\Road.Service` (net472, Visual Studio/MSBuild) → lấy `Road.Service.exe` + DLL mới.
3. Test ở chế độ console (`Road.Service.exe --start`) trên máy, gọi thử API bằng PowerShell/WCF test client.
4. Dừng service `ROAD` → copy file → start service → test từ Panel (chỉ 127.0.0.1).
5. Rollback: copy lại file backup, start service.

## 5. Rủi ro & lưu ý

- **Phải restart Road.Service** khi deploy → player đang online bị ngắt (hẹn giờ bảo trì).
- API kick/reload chạy trong process game server → cần test kỹ để không deadlock; ưu tiên gọi
  trên thread của game server (QueueAction) nếu API yêu cầu.
- Nếu chỉ cần ban/unban, **chưa cần** thay đổi Service — có thể bật trước theo proposal user-actions.

## 6. Câu hỏi cần bạn duyệt

1. Đồng ý **thêm WCF GM API vào Road.Service** (sửa `Full Source` + build lại + thay file trong `Service`)?
2. Chọn cách **WCF** (đề xuất) hay **HTTP listener** đơn giản hơn?
3. Port nội bộ: dùng **9601** được không (chỉ 127.0.0.1)?
4. Có cần thêm action `Shutdown` / `SendNotice` ngay đợt này không?
5. Bạn muốn mình soạn **patch chi tiết từng file** trước để bạn xem, hay làm rồi test luôn sau khi duyệt?
