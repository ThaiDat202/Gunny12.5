# Server Live — điều khiển server qua CenterService có sẵn (Phase A)

> Trạng thái: **hoàn thành (v0.25)**. Không sửa `Full Source` / `Service` — chỉ gọi WCF
> **CenterService** đã host sẵn của Gunny.

## Vị trí

- Trang mới: `http://127.0.0.1:91/ServerLive` (menu **Server Live**).
- Trang `/Users/Details?id=` có thêm nút **Kick** + **Kiểm tra live** (trạng thái online thật).

## Kiến trúc

`Road.Service` và `Center.Service` (chạy sẵn trên máy) đã host WCF contract
`Center.Server.ICenterService`. `Center.Service.exe.config` khai báo 2 endpoint:

| Endpoint | Binding | Dùng bởi |
|---|---|---|
| `net.tcp://127.0.0.1:2009/` | netTcpBinding (None) | Road.Service / Admincp cũ |
| `http://127.0.0.1:4109/CenterServicePhP/` | basicHttpBinding | **GunnyPanel (mới)** |

Panel dùng `HttpClient` gửi **SOAP 1.1 raw** tới endpoint basicHttp — không cần package
`System.ServiceModel` trên .NET 10, không cần sửa/built lại bất kỳ service nào.

> ⚠️ Bẫy đã gặp: element tham số SOAP phải nằm trong namespace `http://tempuri.org/`
> (tạo bằng `XElement(TempuriNs + "playerID", ...)`), nếu tạo `new XElement("playerID", ...)`
> thì .NET sẽ serialize kèm `xmlns=""` và WCF bind **giá trị mặc định** (0/null) —
> mọi lệnh có tham số sẽ âm thầm sai (notice/reload trả `false`, GetPlayerState trả 0).

## Chức năng (trang `/ServerLive`)

| Chức năng | Operation CenterService | Ghi chú |
|---|---|---|
| Gửi thông báo toàn server | `SystemNotice(string msg)` | `msg` = `nội_dung\|type\|md5(nội_dung+LoginKey)`. type: `3` system notice (mặc định), `0` GM notice, `4` auto notice |
| Kick người chơi online | `KitoffUser(playerID, zoneId, msg)` | Không cần chữ ký; trả `false` nếu player không online ở zone |
| Kiểm tra trạng thái online | `GetPlayerState(playerID, zoneId)` | >0 = online, `-1` = offline, `0` = zone login chưa kết nối, `-2` = lỗi |
| Reload manager/game data | `Reload(type)` | `type` = `tên_eReloadType\|md5(tên+LoginKey)`; gửi tới **tất cả Road** đang kết nối |
| Reload danh sách server | `ReLoadServerList()` | Không cần chữ ký |
| Chat gần đây | `GetAllChat()` | Trả base64 (4 byte length + gzip JSON), panel giải mã rồi hiển thị |
| Shutdown (ẩn mặc định) | `Shutdown(zoneId)` | `zoneId\|md5(zoneId+LoginKey)`; chỉ hiện khi `Panel:EnableShutdown=true` + role **Develop** + gõ chữ `SHUTDOWN` |

**Danh sách reload chọn được trên UI** (đúng tên `eReloadType` trong
`Full Source\Bussiness\Bussiness.Protocol\eReloadType.cs`): `notice`, `clearchat`, `center`,
`server`, `serverconfig`, `rate`, `ball`, `ballconfig`, `map`, `npc`, `pve`, `prop`, `item`,
`quest`, `fusion`, `fight`, `consortia`, `shop`, `shopbox`, `drop`, `mission`, `pet`, `mount`,
`rune`, `level`, `strengthen`, `active`, `language`, `card`, `newtitle`, `achievement`, `totem`,
`suit`, `magicstone`.

## Cấu hình (`appsettings.json`)

```json
"Panel": {
  "ZoneId": 1001,
  "CenterServiceUrl": "http://127.0.0.1:4109/CenterServicePhP/",
  "CenterServiceTimeoutSeconds": 10,
  "LoginKey": "",
  "EnableShutdown": false
},
"Paths": {
  "CenterServiceConfig": "C:\\Gunny\\Gunny12.6\\Service\\Service\\Center\\Center.Service.exe.config"
}
```

- `Panel:LoginKey` để trống ⇒ panel **đọc key `LoginKey` từ file config Center** (chỉ đọc file)
  theo thứ tự: `Paths:CenterServiceConfig` → `GameRoot\Service\Service\Center\Center.Service.exe.config`
  → `Paths:RoadServiceConfig`.
- Nếu thiếu key: chức năng notice/reload/shutdown báo lỗi rõ ràng, các chức năng không cần
  chữ ký (kick/state/server list/chat) vẫn chạy.
- Timeout mỗi lệnh: mặc định 10s, cấu hình `CenterServiceTimeoutSeconds` (2–60).

## An toàn & audit

- Gọi POST chỉ cho role **Admin/Develop** (`CanWrite`), kiểm tra ở server handler, không chỉ ẩn nút.
- Trang là **GET** cho dữ liệu read-only; CSRF token tự động của Razor Pages cho mọi POST.
- Audit: `road.notice` (type + nội dung), `road.kick` (userId + lý do), `road.reload` (loại),
  `road.serverlist`, `road.shutdown` (zone).
- Kick / notice áp dụng tức thì cho client đang online (không cần relogin).
- Sửa tiền/level vẫn là ghi DB trực tiếp ⇒ sau khi kick cần chờ vài giây cho server lưu.

## Test đã chạy (local, 2026-11-07)

Điều kiện: `Center.Service` PID 20316, `Road.Service` PID 23680, không có player online.

| Test | Kết quả |
|---|---|
| SOAP `AASGetState` từ PowerShell | `0` (Center phản hồi) |
| SOAP signed `SystemNotice` (PowerShell) | `true` — thông báo vào bộ đệm chat Center |
| SOAP signed `Reload("notice")` | `true` |
| SOAP `ReLoadServerList` | `true` |
| SOAP `GetPlayerState(1, 1001)` | `-1` (offline) |
| SOAP `KitoffUser(1, ...)` khi offline | `false` (đúng) |
| Panel `POST /ServerLive?handler=Notice` | `200 {"ok":true,...}` |
| Panel `POST /ServerLive?handler=Reload` (`notice`) | `200 {"ok":true,...}` |
| Panel `POST /ServerLive?handler=ReloadServerList` | `200 {"ok":true,...}` |
| Panel `GET /ServerLive?handler=PlayerState&userId=1` | `200`, `centerState=-1`, nhãn `test1` |
| Panel `POST /ServerLive?handler=Kick` (user offline) | `400` + thông báo “người chơi không online ở zone 1001” (đúng) |
| Panel `POST /ServerLive?handler=Reload` (`not-a-real-type`) | `400` “Loại reload không hợp lệ.” (validate OK) |
| `GET /ServerLive` (admin) | `200`, hiện trạng thái CenterService OK, chat 18–19 dòng, không lỗi |
| `POST /Users/Details?handler=Kick` (user offline) | `400` + thông báo đúng |
| `GET /Users/Details?handler=PlayerState&id=1` | `200`, `centerState=-1` |
| Audit log | có `road.notice`, `road.reload`, `road.serverlist`, `road.kick` |

Ghi chú: nhánh **kick thành công** chưa test trực tiếp được vì server test **không có người chơi online**;
panel xử lý đúng trường hợp `false` và hiển thị lý do. Khi có player online, `KitoffUser` trả `true`
theo đúng source `CenterService.KitoffUser`.

## File thay đổi

| File | Thay đổi |
|---|---|
| `src/GunnyPanel/Services/RoadLiveService.cs` | **mới** — SOAP client + chữ ký + parse kết quả |
| `src/GunnyPanel/Pages/ServerLive.cshtml(.cs)` | **mới** — trang Server Live |
| `src/GunnyPanel/Pages/UserDetail.cshtml(.cs)` | thêm nút Kick / Kiểm tra live + handler |
| `src/GunnyPanel/Program.cs` | đăng ký `RoadLiveService` |
| `src/GunnyPanel/appsettings.json` / `.example.json` | thêm `CenterService*`, `LoginKey`, `EnableShutdown`, `Paths:CenterServiceConfig` |
| `src/GunnyPanel/Pages/Shared/_Layout.cshtml` | menu **Server Live** (+ Shop/Hộp thư), v0.25 |
| `src/GunnyPanel/wwwroot/css/site.css` | style chat list / danger card |
| `docs/WEB-SERVER-LIVE.md` | tài liệu này |

## Hạn chế / đề xuất

- Panel phụ thuộc endpoint **basicHttp 4109** của Center. Nếu môi trường khác chỉ mở net.tcp,
  cần thêm helper PowerShell (`_tools/center-service.ps1`) tương tự `notify-mail.ps1` làm phương án dự phòng.
- `GetAllChat` chỉ giữ ~100 dòng gần nhất và tự xoá khi vượt ngưỡng (hành vi của CenterService).
- Reload `clearchat` sẽ **xoá bộ đệm chat** — đã ghi chú trên UI, cân nhắc trước khi bấm.
- Shutdown mặc định tắt (`Panel:EnableShutdown=false`) — bật khi thật cần bảo trì.
