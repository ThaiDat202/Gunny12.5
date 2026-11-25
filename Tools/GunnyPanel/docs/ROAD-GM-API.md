# Road GM API — áp thay đổi GM live cho người chơi đang online

> Trạng thái: **hoàn thành (v0.26)** — API HTTP JSON nội bộ chạy **trong process Road.Service**,
> cho phép panel bấm 1 nút là user đang online nhận thay đổi ngay (in-memory + packet),
> **không cần đăng nhập lại**. Kèm 2 fix gốc: **Vinh Dự cộng HP** và **Ma Thạch đủ 6 chỉ số**.

## 1. Kiến trúc

```
GunnyPanel (ASP.NET, .NET 10)                    Road.Service (net472, win-x64)
┌─────────────────────────┐   HTTP JSON          ┌──────────────────────────────────┐
│ RoadGmApiClient.cs      │ ──────────────────▶  │ GmApiServer.cs  (HttpListener)   │
│ 127.0.0.1:9601, token   │   X-GM-Token         │  ↓                              │
│ /Users/Details (server) │                      │ GmApiActions.cs (in-process GM)  │
└─────────────────────────┘                      │  WorldMgr.GetPlayerById(userId)  │
                                                 │  → sửa object in-memory          │
                                                 │  → EquipBag.UpdatePlayerProperties()
                                                 │  → packet 67 / rank / avatar /   │
                                                 │    card / drill                  │
                                                 └──────────────────────────────────┘
```

- **Chỉ bind `127.0.0.1`**, mọi request phải có header `X-GM-Token` (so khớp constant-time).
- Token nằm trong `Road.Service.exe.config` (`GMApi.Token`) + `appsettings.json` panel
  (`Panel:RoadGmApiToken`) — **không lộ ra browser**.
- Panel gọi API ở **server-side** (PageModel), không phải JS.

## 2. Cấu hình

`C:\Gunny\Gunny12.6\Service\Service\Road\Road.Service.exe.config`:

```xml
<!--Road GM API (GunnyPanel -> Road.Service, chỉ bind 127.0.0.1)-->
<add key="GMApi.Enabled" value="true" />
<add key="GMApi.Prefix" value="http://127.0.0.1:9601/" />
<add key="GMApi.Token" value="<token 32+ ký tự>" />
```

Panel `appsettings.json`:

```json
"Panel": {
  "RoadGmApiUrl": "http://127.0.0.1:9601/",
  "RoadGmApiToken": "<trùng GMApi.Token>",
  "RoadGmApiTimeoutSeconds": 5
}
```

Nếu API tắt/sai token: panel tự fallback ghi DB như cũ (badge trên web báo “Road GM API: không kết nối”).
Token thực tế của máy test: xem `data/road-gm-api-token.txt` (chỉ local, không commit).

## 3. Hợp đồng API

| Method | Path | Body / Query | Kết quả |
|---|---|---|---|
| GET | `/health` | — | `{ok, api, version, zone, onlinePlayers, time}` |
| GET | `/features` | — | `{ok, features:[...]}` |
| GET | `/player?userId=N` | — | `{ok, online, userId, player:{...snapshot...}}` |
| POST | `/gm/apply` | `{"userId":1,"feature":"honor"}` | `{ok, online, userId, feature, message, player:{...}}` |

HTTP status: `200` ok; `400` feature lỗi/không hỗ trợ; `401` sai token; `404` user offline với
feature yêu cầu online; `500` exception.

### Feature

| Nhóm | Feature | Cơ chế live |
|---|---|---|
| Live (cần online) | `king`, `guard`, `honor`, `sworn`, `explorer`, `titles`, `amulet_level`, `amulet_wash`, `potential`, `phuma`, `duchon`, `avatar`, `album` | sửa object in-memory → `EquipBag.UpdatePlayerProperties()` (tính lại HP/công/thủ/nhanh/may) → `UpdateProperties()` + `SendUpdatePublicPlayer()` (packet 67) + packet riêng (rank 1006, avatar all-info, card 648, explorer) |
| API-managed (online + offline) | `beads`, `magicstone` | `beads`: mở **6 drill** (`Sys_User_Drills` HoleLv=6) qua `PlayerBussiness` + `BeadBag.SendPlayerDrill()`; gửi 9 mail châu báu. `magicstone`: nâng/ gửi 8 Ma Thạch Tứ Tượng **đủ 6 chỉ số** qua `PlayerBussiness.SendMailAndItemByNickName` (đã có `ApplyMagicStoneMailStats`); online thì `MagicStoneBag.UpdateItem` + refresh |

`/player` snapshot gồm: Grade, GP, Money, Gold, ddtKingGrade, guardCoreGrade, HonorLevel,
SwornId, explorer manual level, **Attack/Defence/Agility/Luck/HP/MagicAttack/MagicDefence/FightPower**,
RankCount, AvatarCount, CardCount, EquipCount, **DrillHoles** (`"6,6,6,6,6,6"`), MagicStoneCount.

## 4. Fix gốc đi kèm

### 4.1 Vinh Dự không cộng chỉ số — sửa `PlayerEquipInventory.UpdatePlayerProperties`

- Bảng DB đã có đủ: `Db_Tank_All.dbo.TS_HonorSystem_Template` (**Level 1–100**, cột `Blood` =
  HP cộng thêm; level 100 = **10.235 HP**) và `HonorTempInfo` (bản sao).
- Code game tính: `hp += ... + num58` (num58 = Blood theo HonorLevel) **nhưng dòng cuối lại bỏ `hp`**:
  ```csharp
  // trước (bug): mất toàn bộ HP tích luỹ (Vinh Dự, trang bị, avatar, mark...)
  enhanceHp = (int)(((double)(enhanceHp + m_player.LevelPlusBlood + def / 10) + m_player.GetGoldBlood()) * m_player.GetBaseBlood());
  // sau (fix):
  enhanceHp = (int)(((double)(enhanceHp + hp + m_player.LevelPlusBlood + def / 10) + m_player.GetGoldBlood()) * m_player.GetBaseBlood());
  ```
- File: `Full Source\game.server\Game.Server.GameUtils\PlayerEquipInventory.cs`.
- Vì là code trong `Game.Server.dll`, phải **build lại Game.Server.dll** và deploy dạng loose
  vào `Service\Service\Road\` (file này có sẵn ở deploy dir nên được ưu tiên hơn bản Costura nhúng trong exe).
- ⚠️ Ảnh hưởng toàn server: HP của **mọi** nhân vật tăng đúng theo các nguồn HP (Vinh Dự, trang bị,
  avatar, mark…) — đây là hành vi đúng theo thiết kế, nhưng cần lưu ý khi test.

### 4.2 Build `Game.Server.dll` bị lỗi 9.069 CS0111

- `Full Source\game.server` có 2 thư mục decompile **giống hệt nhau** `Game.Server.GameServerScript.A`
  và `Game.Server.GameServerScript.AI` (mỗi thư mục 906 file, trùng tên class) → build lỗi
  “already defines a member”.
- Đã thêm vào `game.server.csproj`:
  ```xml
  <Compile Remove="Game.Server.GameServerScript.AI\**\*.cs" />
  ```
- Build ra `Game.Server.dll` **đúng 3.218.432 bytes** (cùng kích thước bản gốc đang chạy) → an tâm tương đương.
- Lệnh build: `MSBuild.exe game.server\game.server.csproj /p:Configuration=Debug`.

### 4.3 Châu Báu — 6 mũi khoan chưa mở

- Nguyên nhân: bản cũ chỉ ghi `Sys_User_Drills` trong DB; khi user **đang online**, game giữ
  `BeadBag.UserDrills` trong RAM và lúc logout ghi đè DB → drill lại về 0.
- Fix: API mở drill bằng `PlayerBussiness.GetPlayerDrillByID/UpdateUserDrillInfo/AddUserUserDrill`
  rồi cập nhật `player.BeadBag.UserDrills` + `SendPlayerDrill()` (packet drill) → client mở **6 lỗ ngay**.
  Offline vẫn ghi DB (vào game thấy sau khi đăng nhập).

### 4.4 Ma Thạch Tứ Tượng Lv10 thiếu 6 chỉ số khi mở thư

- Nguyên nhân: bản cũ gửi mail bằng SQL `SP_Admin_SendUserItem` → item trong mail **không có**
  `MagicAttack`/`MagicDefence`/`StrengthenExp`/4 compose stat.
- Fix: API gửi mail bằng `PlayerBussiness.SendMailAndItemByNickName(...)` — hàm này có
  `ApplyMagicStoneMailStats(itemInfo, templateID, StrengthenLevel)` (sửa trước đó)
  → item trong mail có đủ **Công/Thủ/Nhanh/May/Ma công/Ma kháng = 154**, `StrengthenLevel=10`,
  `StrengthenExp=1134400` (test thực tế trên DB).
- Nếu user đã có ma thạch: API `UpgradeAllMagicStones` nâng toàn bộ ma thạch hiện có lên max
  (level/exp/6 chỉ số) và `UpdateItem` để client thấy ngay.

## 5. File thay đổi

| File | Thay đổi |
|---|---|
| `Full Source\Road.Service\Game.Service.GMService\GmApiServer.cs` | **mới** — HttpListener + JSON + token + routing |
| `Full Source\Road.Service\Game.Service.GMService\GmApiActions.cs` | **mới** — 15 feature in-process (live/API-managed) + snapshot |
| `Full Source\Road.Service\Game.Service.actions\ConsoleStart.cs` | start GMApi sau khi GameServer.Start, stop khi tắt |
| `Full Source\Road.Service\App.config` | thêm `GMApi.Enabled/Prefix/Token`, redirect Newtonsoft 13 |
| `Full Source\game.server\Game.Server.GameUtils\PlayerEquipInventory.cs` | fix HP formula (Vinh Dự cộng HP) |
| `Full Source\game.server\game.server.csproj` | loại bản script `.AI` trùng lặp để build được |
| `Service\Service\Road\Road.Service.exe` | bản build mới có GM API (backup `.bak-*`) |
| `Service\Service\Road\Game.Server.dll` | bản build mới có fix HP (backup `.bak-*`) |
| `Service\Service\Road\Road.Service.exe.config` | thêm 3 key GMApi |
| `src\GunnyPanel\Services\RoadGmApiClient.cs` | **mới** — client + fallback |
| `src\GunnyPanel\Pages\UserDetail.cshtml(.cs)` | gọi API live/API-managed + badge + handler `LiveStatus` |
| `src\GunnyPanel\appsettings.json` / `.example.json` | `RoadGmApiUrl/Token/TimeoutSeconds` |
| `src\GunnyPanel\Program.cs` | DI singleton `RoadGmApiClient` |
| `_tools\deploy-road-gm-api.ps1` | script deploy/rollback (cần Admin để stop service đang elevated) |
| `_tools\test-road-gm-api.ps1` | script test nhanh API + snapshot + 3 feature |

## 6. Build & deploy

### Build Road (không rebuild dependencies)

```powershell
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
& $msbuild 'C:\Gunny\Gunny12.6\Full Source\Road.Service\Road.Service.csproj' `
    /p:Configuration=Debug /p:BuildProjectReferences=false
& $msbuild 'C:\Gunny\Gunny12.6\Full Source\game.server\game.server.csproj' /p:Configuration=Debug
```

### Deploy an toàn (script)

```powershell
# Chạy bằng quyền Administrator (vì Road.Service đang chạy elevated):
powershell -ExecutionPolicy Bypass -File C:\Gunny\ToolGunny\GunnyPanel\_tools\deploy-road-gm-api.ps1

# Rollback bản gần nhất:
powershell -ExecutionPolicy Bypass -File ...\deploy-road-gm-api.ps1 -Rollback
```

Script: kiểm tra không có player online → backup exe/pdb/config → stop Road → copy file → start →
chờ port 8527/9601 → gọi `/health` → ghi log `data\deploy-road-gm-api.log` và kết thúc `DEPLOY-OK`.

### Deploy thủ công (khi Road đang chạy non-elevated)

1. `Stop-Process -Name Road.Service -Force` (hoặc kick player trước để lưu dữ liệu).
2. Copy `Road.Service.exe`, `Road.Service.pdb` từ `Full Source\Road.Service\bin\Debug\net472`.
3. Copy `Game.Server.dll` (+pdb) từ `Full Source\game.server\bin\Debug\net472` vào deploy dir.
4. Start `Road.Service.exe` tại deploy dir (nếu chạy non-elevated: `set __COMPAT_LAYER=RunAsInvoker`).
5. Kiểm tra: port `8527` (game), `9601` (API), `GET /health`, log `logs\Error.log` rỗng.

## 7. Test đã chạy (2026-11-07, test1 online)

| Test | Kết quả |
|---|---|
| Build Game.Server.dll (sau khi loại script `.AI`) | OK, DLL 3.218.432 bytes |
| Road API `/health` | `{"ok":true,"api":"RoadGMApi","zone":"1001","onlinePlayers":1}` |
| Auth sai token | HTTP 401 |
| 13 feature live (bấm từng nút web) | `ok=true live=true`; audit `user.gm.<feature> live=True` |
| Snapshot trước/sau SET FULL | Honor 1→100, Sworn 1→9522, Explorer 1→21, Rank 1→548 |
| **Vinh Dự sau fix HP** | Honor 1→100; **HP 542.970 → 562.835 (+19.865)** không relogin |
| **Châu Báu** | drill `0,0,0,0,0,0 → 6,6,6,6,6,6` (packet drill), gửi 9 mail châu báu |
| **Ma Thạch** | item trong DB: `StrengthenLevel=10`, `StrengthenExp=1134400`, 6 chỉ số = `154` |
| Fallback khi API tắt | panel ghi DB như cũ, message cảnh báo “không kết nối Road GM API” |
| Phụ Ma/Đúc Hồn | báo “Hãy trang bị nhẫn và vòng tay” khi user không đeo — giống logic cũ |

## 8. Hạn chế / lưu ý

- **Deploy cần restart Road.Service** → ngắt người chơi online (nên kick trước để lưu dữ liệu).
- Fix HP ảnh hưởng **toàn bộ nhân vật** (tăng HP đúng theo các nguồn) — cần theo dõi sau deploy.
- API chỉ dùng nội bộ `127.0.0.1` + token; không có HTTPS.
- `beads`/`magicstone` gửi mail: user vẫn phải **mở hộp thư** để nhận item (riêng drill/ma thạch
  đang có thì cập nhật live ngay).
- `phuma`/`duchon` yêu cầu nhân vật **đang trang bị nhẫn/vòng tay** (giống Form1/DB path cũ).
- Khi build `Road.Service.exe` mới, bản Costura embedded `Game.Server.dll` vẫn là bản cũ trong
  `costura.game.server.dll.compressed`; **bản loose `Game.Server.dll` trong deploy dir được ưu tiên**
  nên fix vẫn có hiệu lực. Nếu sau này deploy sang máy khác, nhớ copy kèm loose `Game.Server.dll`.
- Token test lưu ở `data/road-gm-api-token.txt` (gitignored); khi đổi token phải sửa cả 2 config.

---

# v0.29 — Port 18 tính năng Form1 + fix luật Phụ Ma/Đúc Hồn/Bùa/Tiềm Năng/Ma Thạch

## API mới (33 feature)

- **15 feature cũ**: king..album (live) + beads, magicstone (API-managed).
- **18 feature port từ Form1** (đều API-managed):
  `training_basic`, `training_second`, `training_element`, `card_level`, `card_break`,
  `totem_level`, `totem_rank`, `temple_bless`, `temple_exalt`,
  `gold_weapon`, `gold_hat`, `gold_cloth`, `battle_soul`, `craft`,
  `star_weapon`, `star_hat`, `star_cloth`, `subweapon_evolve`.

## Luật đã sửa

- **Phụ Ma/Đúc Hồn**: chỉ nhẫn/vòng tay **đang trang bị** (Place 0–30), gồm Nhẫn Cầu Hôn (9022–9522)
  và Ngọc Kim Lan; trong túi/không có → `Bạn chưa trang bị nhẫn và vòng tay.`
- **Hỗ Trợ**: bắt buộc Bùa Hộ Thân **ô 18**; trong túi → `Bạn chưa trang bị bùa hộ thân.`
- **Tiềm Năng**: chỉ Kính(2)/Tóc(3)/Mặt(4)/Mắt(6)/Set quần áo(13)/Set cánh(15) đang trang bị.
- **Bộ Sưu Tập**: online 1 click full nhờ clear cache + `LoadFromDatabase` + `UpdateInfo()`.
- **Ma Thạch**: patch item mail theo `User_Messages.Annex1` + `Db_Tank_All.dbo.Magicstone_Template`
  → **6 chỉ số 154**, `StrengthenExp=1134400`, Lv10.

## Bài học kỹ thuật (quan trọng khi sửa tiếp)

1. `SP_Admin_SendUserItem` tạo item mail: `UserID=0, Place=-1, BagType=0`, **không có**
   `MagicAttack/MagicDefence/StrengthenExp` → phải UPDATE theo `Annex1`.
2. T-SQL **không** dùng alias trong SET: `UPDATE g SET g.col=... FROM ...` trả **0 dòng**;
   phải viết `SET col = ...`.
3. Bảng ma thạch chuẩn là `Db_Tank_All.dbo.Magicstone_Template` (không phải `Magic_Item_Template`).
4. Build legacy: phải **touch file .cs** trước khi MSBuild nếu sửa nhanh trong cùng giây (MSBuild
   incremental có thể bỏ qua); kiểm tra exe bằng tìm chuỗi UTF-16 (`[Text.Encoding]::Unicode`).
5. Road.Service do agent start là **non-elevated** (kill được); nếu user chạy `Run.bat` (elevated)
   thì cần user tắt console/Task Manager trước khi deploy.

## Test đã chạy

- API `/features` = 33; 18 feature offline trên user2: tất cả `ok=true` (trừ subweapon_evolve cần online).
- Panel v0.29: 33 nút, `training_basic`/`temple_bless`/`star_weapon`/`battle_soul`/`craft` ok;
  `phuma` → thất bại đúng message; `potential` → áp 2 trang bị; `amulet_level` ok.
- Ma Thạch mail user2: 24 item, cả 6 chỉ số = **154**, `StrengthenExp=1134400`.
- test1 đã reset lại baseline (Grade 10, GP 0, Vinh Dự 1, drill 0, mail 0, BagType 21/41/1 = 0).

## v0.29 — Test live

- Script tự động test live khi nhân vật online:
  `_tools\test-live-gm-v029.ps1` (chờ tối đa 45 phút; gọi 30 feature qua Road GM API;
  ghi log + JSON `data/live-test-v029-*.log|json`; kiểm tra DB: Texp, CardInfo, totem, temple,
  gemstone, gold, Phụ Ma/Đúc Hồn, Bùa, Tiềm Năng, Avatar).
- Đã verify nhánh DB fallback (panel) trước khi user online:
  - Phụ Ma/Đúc Hồn chỉ áp **2 món đang trang bị** (Vòng tay place 7, Nhẫn place 9);
    **Nhẫn Cầu Hôn 9522 đang ở trong túi (place 43) KHÔNG bị áp** — đúng luật.
  - Tiềm Năng áp đúng 6 món: Kính(2), Tóc(3), Mặt(4), Mắt(6), Set quần áo(13), Set cánh(15).
  - Hỗ Trợ nâng Bùa ô 18 lên 50; các trang bị khác Strengthen không đổi.
- **Còn chờ**: user đăng nhập test1 để test live (packet) — đặc biệt Bộ Sưu Tập online và các nút mới.

## v0.30 — UI 3 cột giống Form1 + thêm Dây Chuyền

- Layout: 3 cột **Túi / Cá Nhân / Tiệm Rèn**, nhóm + thứ tự nút y như Form1 desktop.
- Nhãn nút ngắn như Form1; toast dùng `data-gm-label` để hiển thị tên đầy đủ
  (vd nút "Đột Phá" ở nhóm Cấp Vua Gà → "Đã nâng cấp tối đa Cấp Vua Gà thành công").
- 2 feature mới: `necklace_train` (Luyện, necklaceExp=630000) và `necklace_cast`
  (Đúc, necklaceCastLevel=40) — online dùng `NecklaceStrengthMgr.UpdateNecklaceInfo/SendNecklaceCastInfo`.
- Road API: 35 feature. Screenshot: `_tools/screenshots/gm-tool-v030.png`.

## v0.30.1 — Live test lần đầu (2026-11-07 19:55, test1 online)

- Kết quả: **25/33 feature live OK** (king, guard, honor, sworn, explorer, titles, amulet_level/wash,
  avatar, album, tu luyện 3, thẻ bài 2, vật tổ 2, miếu thần 2, craft, tăng sao 3, dây chuyền 2).
- Snapshot live: Grade 1→70, GP 857.997.577, Cấp Vua Gà 10, Bảo Vệ 20, Vinh Dự 100, Nhẫn Cầu Hôn 9522,
  Rank 2→3; DB sau đó: Texp 56.655.500, CardInfo 90/209/31/240, totemId 10350, TotemUpgrade 1-5=20,
  temple (6,60,54.940.724,60), Gemstone 5 chip, ghost equip 3 slot level 10.
- **Bug phát hiện + đã sửa**:
  1. `EquipBag.GetItems()` chỉ trả slot >=31 (bag/dress) → Phụ Ma/Đúc Hồn/Tiềm Năng không thấy item
     đang mặc. Sửa dùng `EquipBag.GetItems(0, 31)`.
  2. BattleSoul báo "chưa có dữ liệu Chiến Hồn" khi memory chưa load gemstone → gọi
     `player.LoadGemStone(pb)` trước khi cập nhật.
  3. Script test live bỏ mất body lỗi 400 → đọc `$_.ErrorDetails.Message`.
- Các feature báo lỗi còn lại là **đúng nghiệp vụ** do nhân vật chưa trang bị:
  Dát Vàng (ô 0/4/6 trống), Tiến Hoá VK Phụ (chưa có VK phụ), Phụ Ma/Đúc Hồn/Tiềm Năng trước khi fix.
- Road API đã redeploy bản fix (kick user 1 lần để lưu dữ liệu). Watcher chờ user login lại để test lại.

### Live test #2/#3 (19:58–20:00) — sau khi fix

- Lần 2: 28/33 OK; lần 3 (sau fix Potential): **29/33 OK live**.
- 4 feature còn lại **fail đúng nghiệp vụ** vì nhân vật chưa trang bị:
  `gold_weapon/gold_hat/gold_cloth` → "Không có trang bị ở ô cần dát vàng." (ô 0/4/6 trống);
  `subweapon_evolve` → "Người chơi chưa trang bị VK phụ."
- Xác nhận live khác:
  - Potential OK sau fix `EquipBag.GetItems(0,31)` (bug: GetItems() chỉ trả slot ≥31).
  - Phụ Ma/Đúc Hồn OK → DB `PhuMa=2`, `DucHon=2` (nhẫn+vòng tay đang mặc).
  - Avatar (panel flow DB+live) → `User_Avatar_Colection` 111 dòng **hợp lệ** (ActiveCount>0, ActiveDress đầy đủ, endTime tương lai).
  - Ma Thạch (panel live) → 8 mail mới (ItemID 925–932) **đủ 6 chỉ số =154**, Exp=1.134.400, Lv10.
  - Amulet DB=50; Texp/CardInfo/Totem/Temple/Gemstone đều đúng.
- Bug đã fix thêm: BattleSoul gọi `player.LoadGemStone(pb)` trước khi cập nhật (tránh báo nhầm chưa có dữ liệu).

### Live test #4 (20:00) — sau fix Potential, user test1 online

- **29/33 feature live OK**, gồm toàn bộ 5 lỗi đã sửa:
  - Potential: **OK live** (fix `GetItems(0,31)`) — áp đúng Kính/Tóc/Mặt/Mắt/Set/Ngân Hà.
  - Phụ Ma/Đúc Hồn: OK live (DB `PhuMa=2`, `DucHon=2`).
  - Avatar: OK live; DB 111 group hợp lệ.
  - Ma Thạch: 8 mail (ItemID 925–932) đủ 6 chỉ số 154, Exp 1.134.400.
  - Bùa: DB StrengthenLevel=50.
- 4 feature còn lại trả đúng **yêu cầu nghiệp vụ**: `gold_weapon/hat/cloth` (ô 0/4/6 đang trống), `subweapon_evolve` (chưa trang bị VK phụ).
  → Cần user trang bị Vũ khí/Nón/Áo + VK phụ rồi test lại 4 nút này.
- Snapshot live: Grade 70, GP 857.997.577, Vua Gà 10, Bảo Vệ 20, Vinh Dự 100, Nhẫn Cầu Hôn 9522,
  Danh Hiệu 9, HP 463.883; DB Texp/CardInfo/Totem/Temple/Gemstone/Amulet/PhuMa/DucHon/Avatar đều đúng.

## v0.31 — Căn giữa nút trong nhóm

- Nhóm 1 nút: nút 50% căn giữa; nhóm 2 nút: 2 cột đều; nhóm 3 nút: 3 cột đều (CSS grid,
  class `count-1/count-2/count-3`). Screenshot: `_tools/screenshots/gm-tool-v031c.png`.

## Kết luận v0.30.1/v0.31 — bằng chứng end-to-end

- User test1 mở hộp thư nhận 8 Ma Thạch (mail 925–932) → item chuyển vào `BagType=41` với
  **đủ 6 chỉ số = 154** (Công/Thủ/Nhanh/May/Ma công/Ma kháng), `StrengthenExp=1.134.400`, Lv10
  → xác nhận Ma Thạch đã đúng hoàn toàn cả khi mở thư.
- Phụ Ma/Đúc Hồn lưu DB: 2 món đang mặc (`Place 7` Vòng tay, `Place 9` Nhẫn) `MagicLevel=40`,
  `enchantSpiritLevel=20`; Nhẫn Cầu Hôn 9522 trong túi không bị áp.
- Bộ Sưu Tập: 111 group hợp lệ trong `User_Avatar_Colection`; Hỗ Trợ: Bùa ô 18 = 50.
- Live test: 29/33 OK (4 nút Dát Vàng/Tiến Hoá VK Phụ chỉ fail do nhân vật chưa trang bị
  Vũ khí/Nón/Áo/VK phụ — đúng yêu cầu nghiệp vụ).
- UI v0.31: nút nhóm 1/2/3 được căn giữa & chia đều (CSS grid count-1/2/3).
- Build 0 warning/0 error; panel v0.31 + Road API 35 feature đang chạy.

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
