# Fix lỗi vào Nông trại — Farm 2.0 (phase 1)

Ngày: 2026-11-08 · Người thực hiện: agent (Lead) · Trạng thái: **ĐÃ DEPLOY**

## 1. Triệu chứng

Khi người chơi (test1) bấm vào **Nông trại**, Road.Service in ra console/log:

```
2026-11-08 12:00:12,119 - [17] - ERROR - Game.Server.Farm.FarmLogicProcessor - IP:127.0.0.1:50225, OnGameData is Error:
System.NullReferenceException: Object reference not set to an instance of an object.
   at Game.Server.Farm.Handle.FeedFarmPoultry.CommandHandler(GamePlayer Player, GSPacketIn packet)
   at Game.Server.Farm.FarmLogicProcessor.OnGameData(GamePlayer player, GSPacketIn packet)

2026-11-08 12:00:34,678 - [17] - ERROR - ... (lặp lại y hệt)
```

Log gốc: `C:\Gunny\Gunny12.6\Service\Service\Road\logs\Error.log` (dòng 50–55).
Ngoài ra client không vào được nông trại (không chuyển state), vì server không trả dữ liệu nông trại.

## 2. Nguyên nhân gốc — client 12.6 dùng protocol Nông trại 2.0, server chỉ có nông trại cũ

- Client đang chạy (Gunny 12.6, core client + `farm.swf`) dùng hệ **Nông trại 2.0**
  (`farm.*`, class bí danh `farm._96$41`, có FarmView/FarmSceneMap/FarmWorkerInfo...).
- Server source (`Full Source\game.server\Game.Server.Farm*`) chỉ có **nông trại cũ**
  (`EnterFarm`, `FeedFarmPoultry`, `FarmWater`, `TreeLevel`...), protocol khác hoàn toàn.

Chuỗi lỗi cụ thể:

1. Client vào nông trại: `farm._96$41.gotoFarm()` → `initFrined()`
   (client decompile: `farm\_96$41.as` dòng 583–631) →
   `SocketManager.Instance.out.sendFarmSlackerQuery(_friendPlayer)`.
2. `GameSocketOut.sendFarmSlackerQuery()` (dòng 9171–9184) gửi **packet 81, subtype 33**,
   payload = `[int count][int userID...]`. test1 **không có bạn** (`Sys_Users_Friends` rỗng)
   → payload đúng 4 byte `00 00 00 00` (count = 0).
3. Server cũ map subtype 33 = `FARM_POULTRYFEED` (`Game.Server.Packets\FarmPackageType.cs`),
   `FeedFarmPoultry.CommandHandler` đọc int đó như **userID** → `num = 0`.
   `0 != Player.PlayerCharacter.ID (1)` → rơi vào nhánh `else if (Player.Farm.OtherFarm.isFeed())`;
   `OtherFarm == null` (chưa từng vào nông trại bạn) → **NullReferenceException**.
4. Ngay sau đó client gửi **packet 81, subtype 24** `[int farmUserID]` (`sendRequestFarmInfo`,
   dòng 2533–2539) để xin dữ liệu nông trại; server cũ map subtype 24 = `FARM_POULTYWAKE`
   (`WakeFarmPoultry`) nên **không có phản hồi 81/24**.
5. Client chỉ gọi `StateManager.setState("farm")` bên trong `__onRequestFarmInfo`
   (dòng 333–421) khi nhận được 81/24 → vì không có phản hồi, **nông trại không mở được**.

## 3. Đã sửa (phase 1 — mở được nông trại)

Sửa trong `C:\Gunny\Gunny12.6\Full Source\game.server\Game.Server.Farm.Handle\`:

| File | Code | Thay đổi |
|---|---|---|
| `FeedFarmPoultry.cs` | 33 | Đọc đúng SLACKER_QUERY `[count][ids]`, trả `81/33` count=0 (hết NRE; bỏ code feed cũ) |
| `RequestFarmInfo.cs` (mới) | 24 | Trả dữ liệu nông trại: userID, tên, exp, level đất/mỏ, buff, 6 object (type 1–6) Lv1, 0 thợ/0 goods → client vào state `farm` |
| `FarmPetFoodQuery.cs` (mới) | 34 | Trả `buyExpRemainNum = 20` (client xin khi mở FarmView) |
| `FarmUpdatePlantData.cs` (mới) | 27 | No-op (client gửi mỗi 60 giây; trước đây log lỗi "LoadCommandHandler code001: 27") |
| `EnterFarm.cs` | 1 | Guard packet ngắn (client 12.6 gửi 81/1 rỗng khi mở bảng Quản lý) — không còn IndexOutOfRange |
| `WakeFarmPoultry.cs` | 24 | **Đã xoá** (thay bằng `RequestFarmInfo`) |

- Build: `MSBuild game.server.csproj /p:Configuration=Debug` → **0 error**
  (warning CS0414 có sẵn từ trước), `Game.Server.dll` = 3.224.576 bytes (12:16:16).
- Deploy (12:16, không cần admin — Road chạy cùng user):
  - Backup: `Game.Server.dll.bak-20261108-121626`, `Game.Server.pdb.bak-20261108-121626`
  - Stop Road.Service → copy DLL/PDB mới → start lại.
  - Health check: `HEALTH ok=True api=RoadGMApi zone=1001`; port 8527 + 9601 mở.
  - Log deploy: `GunnyPanel\data\deploy-farm-fix.log`.
- Sau deploy: người chơi tự kết nối lại, `Error.log` **không còn** NullReferenceException
  (chỉ còn warning `pkg 402` có sẵn từ trước, không liên quan nông trại).

## 4. Giới hạn phase 1 (cần làm tiếp nếu muốn nông trại đầy đủ)

Dữ liệu nông trại đang là **mặc định Lv1, không lưu DB**; các thao tác chưa có handler:

- Trồng/gieo hạt, tưới, thu hoạch, bán nông sản (81/11, 28, 14, 9, 31, 15...).
- Cho thú ăn / chạm thú / đánh boss (81/2, 5, 20, 21, 22, 25, 42...).
- Thuê/đuổi thợ, danh sách thợ, nhiệm vụ bảng tin, xếp hạng, câu cá/mỏ/đồng cỏ.
- Lưu trữ DB: cần bảng mới (ví dụ `Sys_User_Manor`, `Sys_User_ManorWorker`,
  `Sys_User_ManorGoods`) — schema chưa có trong server source.

Các handler nông trại CŨ còn lại (`2, 4, 6, 7, 9, 16, 18, 21, 22, 23, 25, 32`) sẽ bị client 12.6
gọi nhầm nghĩa khi người chơi thao tác — phase 2 nên thay toàn bộ bằng handler Farm 2.0.

## 5. Rollback

```powershell
$dir='C:\Gunny\Gunny12.6\Service\Service\Road'
Get-Process Road.Service | Stop-Process -Force
Copy-Item "$dir\Game.Server.dll.bak-20261108-121626" "$dir\Game.Server.dll" -Force
Copy-Item "$dir\Game.Server.pdb.bak-20261108-121626" "$dir\Game.Server.pdb" -Force
Start-Process "$dir\Road.Service.exe" -WorkingDirectory $dir
```

## 6. Cách kiểm tra

1. Đăng nhập test1 → bấm **Nông trại**.
2. Kỳ vọng: vào được scene nông trại (đất trống Lv1, 4 khu Farm/Fish/Mine/Ranch + Quản lý).
3. Xem `C:\Gunny\Gunny12.6\Service\Service\Road\logs\Error.log` — không còn dòng
   `FeedFarmPoultry.CommandHandler` / `NullReferenceException`.
4. Nếu client báo lỗi hoặc scene trắng: gửi lại nội dung Error.log + mô tả để xử lý phase 2.

---

## 7. Hậu kiểm sau deploy — màn hình đen (lỗi 2)

**Triệu chứng:** vào được state nông trại nhưng toàn bộ vùng game đen (chỉ còn khung).

**Nguyên nhân:** WebSite local **thiếu file MornUI** ui/vietnam/morn/ui/farm.ui.
State arm nạp module bằng AssetModuleLoader.addModelLoader("farm", 7) — loader type 7 = cần
.ui + xml + swf; arm.ui chứa **45 định nghĩa giao diện** (FarmView.xml, toàn bộ alertFrame,
tips, billboard, manage...). File này không có trong bản WebSite local (cả Flash/ và root).

- Kiểm tra: GET http://127.0.0.1/flash/ui/vietnam/morn/ui/farm.ui → **404**.
- CDN chính thức (client đang dùng esourceSite = http://gunny.vcdn.vn/) vẫn còn file:
  http://gunny.vcdn.vn/flash/ui/vietnam/morn/ui/farm.ui → 200, 11.711 bytes (zlib/AMF).
- Đối chiếu phiên bản: arm.xml local **SHA256 giống hệt** CDN
  (A712C092…C1EC) → cùng build, nên farm.ui của CDN dùng được.

**Đã sửa (08/11 12:2x):**

1. Tải arm.ui (11.711 bytes, sha256 CAC29B96…EE044) từ CDN.
2. Đặt vào 2 đường dẫn WebSite:
   - C:\Gunny\Gunny12.6\WebSite\Flash\ui\vietnam\morn\ui\farm.ui
   - C:\Gunny\Gunny12.6\WebSite\ui\vietnam\morn\ui\farm.ui
3. Kiểm tra GET http://127.0.0.1/flash/ui/vietnam/morn/ui/farm.ui → **200, 11.711 bytes**.
4. Không thay arm.swf local (đã so symbol: đủ sset.farm.map, armTouch, armMovie,
   
oticeTouch… giống CDN) — giữ bản VN local.

**Kiểm tra tiếp:** cần tải lại game (F5/đóng mở trang) để Flash xoá cache module lỗi rồi vào nông trại lại.

**Nếu vẫn đen (fallback):**
- So sánh/thay Flash\ui\vietnam\swf\farm.swf bằng bản CDN (1.219.704 bytes, có backup).
- Bật trace Flash client để lấy AS3 stack (mm.cfg + flashlog).
- Xem Road\logs\Error.log xem client có gửi packet farm khác (subtype lạ) không.
1. **Kiểm tra tương thích farm.ui (CDN, bản mới) với client đang chạy (bản cũ hơn):**
   - arm.ui chứa đủ 45 XML: FarmView, toàn bộ alertFrame/tips/billboard/manage/menu/fight.
   - FarmView.xml có **đủ tất cả** ar mà FarmViewUI (client cũ) cần
     (playerListView, manageBtn, farmBtn, ... activityWaitLabel) + thêm oneKeyWorkBtn (mới).
   - MornUI View.setCompValue bỏ qua an toàn các ar/component lạ
     (hasOwnProperty check) → UI mới không làm crash client cũ.
   - arm.swf local giữ nguyên: cấu trúc sset.farm.map + các child
     (armTouch, armMovie, 
oticeTouch, manageTouch, eggTouch) **giống hệt** CDN;
     chỉ thiếu asset mới sset.farm.oneKeyBtn (nút "một chạm" — client cũ không dùng).
