# GM "Set Full" v0.57.1 — Thẻ Bài mới, Ma Ngọc, Nhẫn Cầu Hôn (Tình Ý)

> ⚠️ **Đọc mục 6 (Hotfix v0.57.1) trước**: mục 2 (Ma Ngọc) và mục 3 (Tình Ý) bên dưới mô tả bản
> triển khai đầu tiên 11:58; bản đang chạy hiện tại (14:40) đã được sửa: Ma Ngọc **không gửi mail** nữa mà
> đặt trực tiếp 9 item vào Place 21..29 + Hole1=1, server ghi `magicJadeStr`; Tình Ý có thêm **feature live
> `sworn_love`** để không bị server ghi đè `RingExp` khi user online.

Ngày áp dụng: **2026-11-10 11:57–11:58**, hotfix **14:39–14:40** (Asia/Bangkok). Panel **v0.57.1**,
game stack restart lúc 11:57 và 14:39 với DLL patch. Đã áp cho **tất cả tài khoản**: user 1–5 (`test1`…`hahaa`)
và user 7 (`Đạt`).

Ba nút mới nằm trong **Users → Chi tiết → GM Tool** (giao diện hiện đại, có nút **Set Full** chạy lần lượt):

| Nhóm | Nút | Feature | Ghi chú |
|---|---|---|---|
| Thẻ Bài | **Full Album** (đã có, nâng cấp) | `album` | + 4 album mới, + 10 thẻ mới |
| Ma Thạch | **Ma Ngọc** (mới) | `magicjade` | 9 viên Ma Ngọc Tứ Tượng full |
| Nhẫn Cầu Hôn | **Tình Ý** (mới) | `sworn_love` | max điểm tình ý Lv60 |

Ảnh giao diện: [`_tools/screenshots/gm-set-full-v057.png`](../_tools/screenshots/gm-set-full-v057.png).

---

## 1. Thẻ Bài — Full Album (`album`)

**Kết quả:** mỗi user có **102 thẻ album** (Place 5..106 liên tục, cộng 5 ô thẻ đang trang bị Place 0–4),
**30 bộ thẻ** trong `Sys_Users_CardActive` (đúng **1 bộ active**), `Level=45`, `CardGP=865000`, `Type=1`
(lấy max từ `Db_Tank_All.dbo.Card_Groove_Update`).

Vì sao trước đây "mới thêm vài album thẻ mới" mà user không thấy đủ:

1. Data NPH mới có **103 thẻ** trong `TS_CardBooklet` nhưng tool cũ chỉ insert **thẻ** (CardType=4, 92 thẻ) và
   **không kích hoạt bộ thẻ** nào (`Sys_Users_CardActive` rỗng) → 4 bộ mới #27–#30 không dùng được.
2. 10 thẻ mới **314189–314198** (bộ #29 Đối Đầu Ác Quỷ, #30 Khu Vực Bí Ẩn) chỉ có trong `TS_CardBooklet`,
   **thiếu `Card_Template_Info`** → nếu insert sẽ bị server reset `TemplateID=0` và có thể crash `GetSuitValue`
   khi login (`TS_CardBooklet` tra theo `Profile = 5 − CardType`).
3. Vài row cũ nằm ở Place **103–107**, vượt capability 100 của `CardInventory` → không load / không hiển thị.

**Đã sửa:**

- **Data-fix** `Db_Tank_All.dbo.Card_Template_Info`: insert thêm **30 row** (10 CardID 314189–314198 × CardType 2,3,4;
  identity 2685–2714), bảng 461 → **491 row**. Giá trị suy từ pattern bộ #27/#28 + `TS_CardBooklet`
  (probability = booklet Weight, AddDamage/AddGuard = Damage/Armor, AddAttack/Def/Agility/Luck theo tỉ lệ profile),
  **không** insert CardType 5 (profile 0, stats 0 → `AddPercents[-1]`, crash album #29/#30).
  Rollback: `DELETE FROM Card_Template_Info WHERE CardID BETWEEN 314189 AND 314198`.
- **Code panel** `src/GunnyPanel/Services/GmToolService.Cards.cs` (partial `AlbumAsync`): xoá album `Place >= 5`
  của đúng user, cấp lại **102 thẻ, Place 5..106 liên tục**, ưu tiên bộ #27–#30/thẻ mới; giữ 5 row Place 0–4;
  upsert `Sys_Users_CardActive` đủ 30 bộ (1 active hợp lệ); Level/CardGP/Type lấy max từ `Card_Groove_Update`.
- **Patch server** (tăng sức chứa thẻ 100 → 150): `game.server/Game.Server.GameObjects/GamePlayer.cs`
  (`new CardInventory(this, saveTodb: true, 150, 0)`) + `game.server/Game.Server.CardNewSystem/CardNewSystemMgr.cs`
  (`GetCards(5, 150)`); build lại **`Game.Server.dll`**, deploy Road/Fight/Center.
- Tool cũ hardcode `Level=30, CardGP=0` → giờ lấy max thật.

**Hiệu lực:** user online được áp **live** (Road API feature `album`); user offline thấy khi đăng nhập lại.
Kiểm chứng user 7: 102 thẻ album + đủ 10 thẻ mới + 20 thẻ của 4 bộ mới, 30 album/1 active.

Rollback DB: backup `Db_Tank.dbo.Sys_Users_Card_gmfull_20261110_111909`.
Rollback code: hoàn file `GmToolService.Cards.cs` + `GamePlayer.cs`/`CardNewSystemMgr.cs` + build/deploy DLL cũ
(`Game.Server.dll.bak-gm057-*` trong các thư mục service).

---

## 2. Ma Ngọc (`magicjade`)

**Kết quả:** mỗi user được gửi **9 viên Ma Ngọc Tứ Tượng 110906** qua hộp thư, mỗi viên:
`BagType=41` (túi Ma Thạch), `StrengthenLevel=10`, `StrengthenExp=1134400`,
6 chỉ số **Attack/Defence/Agility/Luck/MagicAttack/MagicDefence = 92** (đọc động từ
`Db_Tank_All.dbo.Magicstone_Template` dòng Lv10 — mỗi viên 552 điểm, tổng 4.968 điểm). Idempotent:
bấm lại **không gửi trùng**, chỉ nâng cấp các viên đã có.

Vì sao phải dev thêm server: client mới có UI **Ma Ngọc** (`gemstone.GemstoneFrame`, túi 9 ô place 21–29)
nhưng server 12.6 chỉ coi **CategoryID 61** là ma thạch; Ma Ngọc là **CategoryID 90** nên trước patch
không vào `MagicStoneBag`, không có 6 chỉ số, tooltip sai. **Patch 3 file — chỉ 1 DLL `SqlDataProvider.dll`:**

1. `Equip.isMagicStone(int CategoryID)`: nhận `61 || 90`.
2. `ItemInfo.IsMagicStone()`: `CategoryID == 61 || == 90`.
3. `ItemTemplateInfo.BagType`: `case 90: return eBageType.MagicStoneBag;`

→ `ItemMgr`, `PlayerBussiness.ApplyMagicStoneMailStats`, `AbstractInventory`,
`AbstractPacketLib.SendItemInfo`, `PlayerMagicStoneInventory`, `MagicStone.Handle` tự ăn patch;
**không cần build lại** `Bussiness.dll` / `Game.Server.dll` / `Game.Base.dll` cho phần này.

- Build: `MSBuild "Full Source\SqlDataProvider\SqlDataProvider.csproj" /p:Configuration=Debug`
  → `bin\Debug\net472\SqlDataProvider.dll` (SHA256 `75FAB2CA…2E5CF`, khác bản đang chạy `18C9FAA0…`).
- Deploy: chép DLL vào `Service\Service\{Road,Fight,Center}` (backup `.bak-gm057-*`), restart stack.
- Hiệu lực: user mở **Hộp thư** trong game nhận 9 viên → vào túi Ma Thạch, đủ 6 chỉ số.

Hạn chế đã biết (P2, không chặn dùng): `AbstractPacketLib` vẫn ghi `magicJadeStr=""` (comment `//Cần sửa`) nên
phần tooltip "Pro Tip"/kỹ năng/icon Ma Ngọc chưa đẹp; pool "Thám hiểm Ma Thạch" có thể rơi Ma Ngọc cấp 1.
Muốn hoàn thiện UI cần patch thêm `AbstractPacketLib` + `Bussiness` + `Game.Base.dll` (xem
`data/gmdev/magicjade.md` mục 8).

Rollback: `Db_Tank.dbo.Sys_Users_Goods_gmfull_20261110_111909` + `User_Messages_gmfull_20261110_111909`
+ `SqlDataProvider.dll.bak-gm057-*`.

---

## 3. Nhẫn Cầu Hôn — Tình Ý (`sworn_love`)

**Kết quả:** `Db_Tank.dbo.Sys_Users_Detail.RingExp = 99690` = **cấp tình ý 60 (MAX)** cho tất cả user.
Giữ nguyên **cấp nhẫn** (`Sys_Users_Sworn.swornId=9522`), **Ngọc Kim Lan/nghĩa khí**
(`swornCount=124860`, `skillId=1072`, `isHasSwornItem=1`), kỹ năng và trạng thái kết hôn.
Bấm lại: "đã ở MAX từ trước", không ghi DB (idempotent).

Cơ chế: "Điểm tình ý" = `RingExp` của **Hệ thống Nhẫn Cưới** (client `bagAndInfo.bag.ring.RingSystemView`,
tiêu đề "Tình ý"); server cộng % chỉ số nhẫn cưới 9022–9522 qua `LoveLevelMgr.GetLevel(RingExp)`
(`PlayerEquipInventory.cs`), client nhận qua packet 67/74. Bảng mốc: `Db_Tank_All.dbo.Love_level`
**60 cấp**, cấp cuối `Level=60 / Exp=99690` (khớp 1-1 với `WebSite\Request\lovelevelist.xml`) → không có cap cao hơn.

- Không cần server patch; chỉ cần nhân vật **đăng nhập lại** để server/client nạp `RingExp` mới.
- **Không đụng** `Sys_Users_Sworn` (đó là điểm nghĩa khí / Ngọc Kim Lan, không phải tình ý).

Áp dụng lại / cho user khác: **Users → Chi tiết → GM Tool → Nhẫn Cầu Hôn → Tình Ý** (một user/lần),
hoặc chạy `_tools\gm-set-full-v057.ps1 -Users 7 -Features sworn_love`.

---

## 4. Áp dụng & kiểm chứng (2026-11-10 11:57–11:58)

- Deploy server: `_tools\deploy-gm057-elevated.ps1` (Admin): stop stack → backup + chép
  `SqlDataProvider.dll` (patch Ma Ngọc) & `Game.Server.dll` (capacity 150) vào Road/Fight/Center →
  `run-stack.ps1` (Center→Fight→Road) → `HEALTH ok=True`, `Error.log` cả 3 service **0 byte**, service PIDs mới.
- Build panel v0.57 (`dotnet build`) + `_tools\start-panel.ps1`.
- Áp feature: `data/gmdev/apply-20261110-115823.json` — **18/18 lần gọi OK** cho user 1,2,3,4,5,7 ×
  (album, magicjade, sworn_love). User 7 đang online → `album` áp **live**; Ma Ngọc/Tình Ý cần relog.
- Kiểm chứng DB: `RingExp=99690` cả 6 user; cards = **107 row/user (5 equip + 102 album)**, Place 5..106,
  CardActive **30 bộ/1 active**, 10 thẻ mới có mặt; Ma Ngọc **9 mail/user**, stats `Lv10 / 6×92`;
  `Sys_Users_Sworn` user 1,2,3,7 giữ nguyên `9522/124860/1072`.
- Idempotent: bấm lại cả 3 feature không đổi số lượng (Ma Ngọc "đã đủ 9", Tình Ý "đã MAX", Album vẫn 102).

Báo cáo kỹ thuật chi tiết: `data/gmdev/cards.md`, `data/gmdev/magicjade.md`, `data/gmdev/sworn.md`.

## 5. File/tool liên quan

- `src/GunnyPanel/Services/GmToolService.Cards.cs` / `.MagicJade.cs` / `.SwornLove.cs` (partial, feature mới).
- `src/GunnyPanel/Pages/UserDetail.cshtml` — nút Ma Ngọc, Tình Ý + danh sách Set Full.
- `src/GunnyPanel/Pages/Shared/_Layout.cshtml` — v0.57.
- `_tools/gm-set-full-v057.ps1` — chạy lại feature qua HTTP (có log JSON).
- `_tools/deploy-gm057-elevated.ps1` — restart stack + deploy 2 DLL (cần Admin/UAC).
- `data/gmdev/*` — báo cáo + kết quả apply.

---

## 6. Hotfix v0.57.1 (2026-11-10 14:39–14:40) — Ma Ngọc hết "undefined", Tình Ý không bị reset

Sau lần áp đầu (11:58), user báo 2 lỗi trong game: (a) Ma Ngọc hiện **"(undefined)"** / UI Ma Ngọc trống,
(b) Nhẫn Cầu Hôn vẫn **0% tình ý**, chưa đầy cấp. Đã tìm ra nguyên nhân từ **client AS decompiled**
(`data/tmp/dgn-work/client-flow/decompiled_active/scripts/...`) và sửa:

### 6.1 Ma Ngọc — nguyên nhân & fix

| Nguyên nhân | Bằng chứng client | Fix |
|---|---|---|
| `magicJadeStr` rỗng | `PlayerManager.as` đọc `magicJadeStr = readUTF()` cuối mỗi item; `GoodTip.as` / `MagicJadeView.as` split(",") lấy **property type id** (1=Attack,2=Defence,3=Agility,4=Luck,5=MagicAttack,6=MagicDefence,7..14 nguyên tố) để tra `Magicstone_Template` | Server `game.server/Game.Base.Packets/AbstractPacketLib.cs` (packet 64) ghi **`"1,2,3,4"`** khi `Template.CategoryID == 90` |
| **"(undefined)"** khi hover | `GoodTip.as:1196-1200`: tên item nối `tank.magicStone.txt4.split(",")[Hole1-1]`; language pack có 6 tên `Nước,Lửa,Gió,Đất,Sáng,Tối`; `Hole1=-1` → index âm → "undefined" | Nút GM đặt item với **`Hole1=1`** (kỹ năng nhóm 1) |
| UI Ma Ngọc trống | `MagicJadeView.as` chỉ đọc **9 ô Place 21..29** của túi Ma Thạch `BagType=41` | Nút GM đặt **trực tiếp 9 row** vào `Sys_Users_Goods` BagType=41, Place 21..29 (không gửi mail nữa), Lv10/Exp 1134400/6 chỉ số 92, `IsBinds=1, IsExist=1`; dọn mail "Ma Ngọc GM" + 9 item cũ (34..42 / mail) |
| Config skill | `ServerConfig.xml` phục vụ client **đã có** MagicJadeSkill1..4, JadePropertys, MagicJadeLockPoints… | Không cần sửa config |

- Build: `game.server.csproj` (MSBuild VS18) → `Game.Server.dll` **3.367.424 B, 12:44:48,
  SHA256 `039544032FE2B712AAD29A12953EEDD4EF3610B87976CA9F78757CA8B1797C18`**; deploy Road/Fight/Center.
- Panel `GmToolService.MagicJade.cs` viết lại: 1 transaction, `SP_Users_Items_Add` ×9, verify đọc lại
  9/9 item đúng chuẩn, idempotent (bấm lại vẫn đúng 9, không tạo trùng). Message tiếng Việt nêu 9/9,
  Lv10, 6 chỉ số 92, Hole1=1.
- Test SQL user 5 PASS rồi restore baseline; user 7 được apply sau restart 14:39 (offline) → hiện
  **9 item Place 21..29, Hole1=1**. Hết "(undefined)" và UI Ma Ngọc hiển thị 9 viên + 4 dòng chỉ số + skill.

### 6.2 Tình Ý — nguyên nhân & fix

- Triệu chứng: DB đã set `RingExp=99690` nhưng user vào game vẫn 0% (Lv1). Nguyên nhân: user **đang online**
  giữ `PlayerCharacter.RingExp=0` trong bộ nhớ Road; khi logout `GamePlayer.Quit() → SaveIntoDatabase()`
  ghi in-memory (0) đè DB (`SP_Users_Update @RingExp`), nên lần login sau client nhận 0.
- Fix: thêm feature **live `sworn_love`** vào Road GM API (`GmApiActions.cs`):
  `LiveFeatures` + case Apply + method `SwornLove(player)` đọc max `(Level,Exp)` từ
  `Db_Tank_All.dbo.Love_level` (fallback Lv60/99.690), set `player.PlayerCharacter.RingExp = 99690`,
  `RefreshProperties(player)` (cộng % chỉ số nhẫn cưới + gửi packet 67/38), UPDATE DB ngay (an toàn kép),
  idempotent, message tiếng Việt.
- Panel: `RoadGmApiClient.LiveFeatures` thêm `sworn_love` → bấm nút khi user online vừa ghi DB vừa áp live,
  không còn bị reset về 0 khi logout.
- Build: `Road.Service.csproj` → `Road.Service.exe` **5.599.744 B, 12:41:12,
  SHA256 `C60532A329621FB9A5C0D32BB2482FB14CED4FBB6815760EC1036E65E3E9704B`**; deploy Road.
- Đã test live user 7 (14:41): Road API trả `ok=true, online=true`, message "đã ở MAX từ trước (Lv60 —
  99.690 tình ý); đã gửi lại dữ liệu cho client (live)."; DB `RingExp=99690`; snapshot player tăng chỉ số
  (FightPower 123,7M) chứng tỏ % nhẫn cưới đã áp. **Client không tự repaint khung đang mở → đóng/mở lại
  khung Nhẫn Cầu Hôn để thấy Lv60.**

### 6.3 Trạng thái sau hotfix (14:40)

- Deploy: `_tools/deploy-gm057c-elevated.ps1` — stop stack → backup + chép **Game.Server.dll**
  (Road/Fight/Center) + **Road.Service.exe** (Road) → `run-stack.ps1`; `HEALTH ok=True`, `Error.log`
  cả 3 service **0 byte**, service PIDs mới (Road 37596).
- Áp lại feature: `data/gmdev/apply-fix2-20261110-144008.json` — **12/12 OK** (6 user × magicjade + sworn_love).
- DB verify: mỗi user **9 Ma Ngọc** BagType 41 Place **21..29**, `Hole1=1`, `StrengthenLevel=10`,
  `StrengthenExp=1134400`, 6 chỉ số **92**, `IsBinds=IsExist=1`; **0 mail "Ma Ngọc GM"**, 0 item mồ côi
  UserID=0; **RingExp=99690** cả 6 user; jade cũ 34..42 đã dọn.
- Road API `/features` có `sworn_love`; live apply user 7 thành công (đã test trực tiếp).
- Giới hạn còn lại (không ảnh hưởng hiển thị): server 12.6 **chưa handle packet 258 plan info**
  (`MagicStoneType.PLAN_INFO=22`, `BUYPLAN=19`, `PlanChange=20`, `planNameReset=21`, `MAGICJADE_PROSET=18`)
  nên các nút mua/đổi tên bộ ngọc trong UI Ma Ngọc chưa hoạt động; muốn full cần port thêm handler
  (report `data/gmdev/magicjade-ui.md` mục cuối). Ngoài ra `TRANSFORM_MAGICSTONE`/reset pro chưa có server.

### 6.4 File/tool mới của hotfix

- `_tools/deploy-gm057c-elevated.ps1` — deploy 2 file DLL/exe + restart stack (cần Admin/UAC).
- `data/gmdev/magicjade-ui.md`, `data/gmdev/ringexp-live.md` — phân tích + patch + build + test.
- `data/gmdev/ringexp-build.log`, `data/gmdev/deploy-fix2.log`, `data/gmdev/apply-fix2-20261110-144008.json`.
---

## 7. v0.57.2 (2026-11-10 15:12) — Hoàn thiện Ma Ngọc: Chuyển đổi, Xilian (Reset Pro), Bộ ngọc (plan)

### 7.1 Vì sao cần

Client mới (`magicstone.swf`, decompiled AS) đã có 3 tab Ma Ngọc và gửi packet **258** subtype **10** (transform),
**18** (reset pro/xilian), **19/20/21** (plan: mua/chọn/đổi tên) — nhưng server 12.6 chỉ có handler 1,2,3,4,5,9,16
và **không có** 10/18/19/20/21/22. Kết quả: bấm nút không phản hồi và log server báo "LoadCommandHandler not found".

### 7.2 Đã phát triển (deploy 15:12)

| Thành phần | Chi tiết |
|---|---|
| Handler `MagicStoneTransform` (258/10) | Item ở `MagicStoneBag` place 31, CategoryID 61, `Template.Property8` = template Ma Ngọc đích (100901→110901 … 100906→110906). Tiêu `Đá Ma Ngọc` (12268) = `Property7`; đổi template giữ level/exp; 6 chỉ số theo `Magicstone_Template`; random `Hole1` (1..N nhóm skill), `Hole2..5` = 4 property type 1..14 (trọng số `MagicJadeAttrRandoms`); cập nhật túi + gửi item mới + message VN; rollback nguyên liệu nếu add item thất bại. |
| Handler `MagicJadeResetPro` (258/18) | Item place 31 phải CategoryID 90; đọc `lockCount + lockList(1..4) + lockSkill`; random lại các slot không khóa (`Hole2..5`) và `Hole1` nếu không khóa skill; tiêu `Property7` Đá Ma Ngọc + phí khóa theo `MagicJadeLockPoints` (1000/2000/5000/10000); cập nhật túi + message. |
| Handler plan (258/19,20,21) | Bảng mới `Db_Tank.dbo.Sys_Users_MagicStonePlan` (tự tạo idempotent: PlanID identity, UserID, PlanName nvarchar(50), IsActive, CreatedAt); mặc định "Bộ 1" active; mua bộ mới phí `PlanBuyPoint` (1000), tối đa `MagicJadePlanMaxCount` (6); chọn/đổi tên lưu DB; sau mỗi thao tác push lại `PLAN_INFO`. |
| Push `PLAN_INFO` (258/22) | Gói: int count; mỗi bộ: int PlanID, UTF PlanName, int state (1=active). Push 6 giây sau login (`SendLoginSuccess` → `MagicJadeHelper.SchedulePlanInfoPush`) để client kịp đăng ký listener; fallback `EnsurePlanInfoPushed` khi nhận request 258 (transform/reset). |
| Serializer `magicJadeStr` | `AbstractPacketLib` (packet 64) đọc **Hole2..Hole5** nếu >0, fallback "1,2,3,4"; Hole1 = nhóm kỹ năng. |
| Panel GM Ma Ngọc | Đặt 9 viên Place 21..29 với Hole1=1, **Hole2..5 = 1,2,3,4**; cấp **999.999 Đá Ma Ngọc (12268)** vào PropBag (BagType=1) để dùng 2 chức năng trên; idempotent, dọn mail/stack cũ. |
| Bussiness | `MagicStonePlanBussiness` đọc `Server_Config` (Db_Tank_All) cho MagicJade*/Plan* key, truy cập bảng plan qua `Sql_DbObject("AppConfig","conString")` giống `BaseBussiness`; tự tạo bảng nếu thiếu. |

### 7.3 Build & deploy

- `Bussiness.dll` (15:11) + `Game.Server.dll` (15:11) build bằng MSBuild VS18, 0 error;
  deploy Road/Fight/Center qua `_tools/deploy-gm057d-elevated.ps1` (Admin/UAC), restart stack 15:12.
- DLL đã deploy: `Game.Server.dll` SHA256 `FF331C6E79369393CED40DD37F961A7CBA04C3AFC931A31C1387BB4C42ADA835`;
  `Bussiness.dll` SHA256 `9DDF2A13E002E6CE41635ECABB95D7A0A0E687F4E1E364E2188C04347F51A93B`;
  `SqlDataProvider.dll` giữ `75FAB2CA…` (patch category 90 từ trước).
- `HEALTH ok=True`, `Error.log` Center/Fight/Road = **0 byte**; log: `data/gmdev/deploy-fix3.log`.

### 7.4 Áp dữ liệu & kiểm chứng

- Áp lại feature Ma Ngọc cho user 1–5, 7 (6/6 OK) — log `data/gmdev/apply-fix3b-20261110-151350.json`.
- DB verify: mỗi user 9 item 110906 `BagType=41 Place=21..29`, `Hole1=1`, `Hole2..5=1,2,3,4`, Lv10/Exp 1134400,
  6 chỉ số 92; **999.999 Đá Ma Ngọc (12268) BagType=1**; bảng `Sys_Users_MagicStonePlan` có "Bộ 1" active
  cho cả 6 user.
- In-game test: user relog → tab Ma Ngọc hiện 9 viên; tab Chuyển đổi: kéo Ma Thạch vào ô 31 → Chuyển đổi;
  Ma Ngọc vào ô 31 → Xilian (chọn khóa) ; combo plan → Thêm/đổi tên/chọn bộ.

### 7.5 Giới hạn / việc còn lại

- Chọn bộ ngọc hiện chỉ đổi tên/active state; **chưa có loadout riêng từng bộ** (swap 9 viên) vì server 12.6
  không có bảng/item layout cho plan — muốn đầy đủ cần thiết kế thêm (ghi trong báo cáo).
- Hệ thống chưa xử lý `UPDATE_BAG`/`MAGIC_STONE_SCORE` mới của client cho plan/score; không ảnh hưởng 3 chức
  năng vừa làm.
- Báo cáo kỹ thuật đầy đủ: `data/gmdev/magicjade-system.md` (protocol, handler logic, DDL, build, risks).
### 7.6 Hotfix 15:22 — sửa lỗi KHÔNG LOGIN ĐƯỢC (đọc log Road)

Log `Service\Service\Road\logs\GameServer.log` lúc **15:20:04** có:
```
ERROR Game.Server.GameServer - Đang nhập vào server thất bại...
System.ArgumentException: An item with the same key has already been added.
   at MagicStoneHandleMgr.RegisterCommandHandler(Int32 code, ...)
   at MagicStoneHandleMgr..ctor()
   at GamePlayer..ctor(...)
   at UserLoginHandler.HandlePacket(...)
```
Nguyên nhân: 2 class cùng `[MagicStoneHandleAttbute(6)]` — `Clock` (có sẵn) và `MagicStoneLockAck` (mới thêm) →
`Dictionary.Add` ném exception trong **constructor của GamePlayer** → mọi login fail. Trước đó (14:42) cũng có
`LoadCommandHandler code020: 10` do handler transform chưa được deploy — nay đã có handler.

Fix:
- Xoá class trùng `MagicStoneLockAck` (giữ `Clock` xử lý 258/6).
- `MagicStoneHandleMgr.RegisterCommandHandler` chống trùng: nếu code đã có thì **log warn + bỏ qua** thay vì ném
  exception (để 1 handler trùng không bao giờ làm hỏng login nữa).
- Rebuild `Game.Server.dll` (3.381.760 B, SHA256 `B58B9905…`) và deploy lại Road/Fight/Center lúc **15:22:17**
  (`_tools/deploy-gm057e-elevated.ps1`, log `data/gmdev/deploy-fix4.log`); `HEALTH ok=True`,
  `Error.log` 3 service = 0 byte; kiểm tra lại danh sách attribute: **không còn code trùng**.

→ User đăng nhập lại bình thường; nếu còn lỗi, xem tiếp `Road\logs\GameServer.log` (log INFO/WARN/ERROR) và
`Road\logs\Error.log`.
### 7.7 Hotfix 15:36 — sửa lỗi text tiếng Việt + phát triển loadout 9 viên theo bộ

**Lỗi text (screenshot "Bá»™ 2"):** các file C# mới được lưu UTF-8 **không có BOM**, MSBuild/csc đọc theo ANSI → chuỗi
tiếng Việt trong `Player.SendMessage(...)` và tên bộ ("Bộ N") bị mojibake khi vào game. Fix:
- Sửa nội dung về đúng tiếng Việt, chuyển toàn bộ chuỗi ký tự tiếng Việt trong literal thành **escape `\uXXXX`**
  (file còn comment tiếng Việt + BOM UTF-8) để không phụ thuộc encoding khi build lại.
- Sửa lại tên bộ đã bị lỗi trong `Db_Tank.dbo.Sys_Users_MagicStonePlan` (Bộ 2/3/4 của user 7).
- Verify DLL: byte UTF-16 của "Đã"/"Bộ" có trong `Game.Server.dll`, không còn pattern mojibake "ÄÃ".

**Loadout 9 viên theo bộ (swap thật):** thêm bảng `Db_Tank.dbo.Sys_Users_MagicStonePlanItem`
(PlanID, Slot 1..9, ItemID, TemplateID) + `MagicStonePlanBussiness.GetPlanLoadout/SavePlanLoadout`:
- Khi **đổi bộ**: lưu 9 ô ngọc hiện tại (Place 21..29) vào bộ đang active → chuyển active → nạp 9 ItemID của bộ đích
  vào đúng ô (hoán đổi vị trí item; ô trống đưa item hiện tại ra túi 32+), gửi bag update + PLAN_INFO.
- Khi **mua bộ mới**: seed loadout = 9 ô hiện tại (để chuyển qua lại không mất trạng thái).
- Bộ chưa có loadout (các bộ tạo trước bản này): lần chọn đầu tiên sẽ seed = layout hiện tại.
- Giới hạn: loadout chỉ lưu **ItemID**, không lưu chỉ số/skill snapshot (chỉ số lấy theo item hiện tại); nếu item bị bán/ghép
  thì ô đó giữ nguyên item hiện tại và bỏ qua.

**Deploy:** `Game.Server.dll` (SHA256 `9F4DAB53…`) + `Bussiness.dll` (`8B22F9F1…`) vào Road/Fight/Center lúc 15:36,
`HEALTH ok=True`, `Error.log` 3 service = 0. Bảng loadout đã tạo trên DB, plan names đã sửa.
### 7.8 Fix 15:44 — lỗi battle Fight: `MissingMethodException set_FightPower(Int32)`

Log `Service\Service\Fight\logs\GameServer.log`/`Error.log` lúc 15:41:
```
System.MissingMethodException: Method not found: 'Void SqlDataProvider.Data.PlayerInfo.set_FightPower(Int32)'
   at Fighting.Server.ServerClient.HandleGameRoomCreate(GSPacketIn pkg)
```
Nguyên nhân: các lần deploy trước đã copy **`SqlDataProvider.dll` (bản build mới, FightPower = Int64)** và
**`Bussiness.dll` mới** vào `Fight`/`Center`, nhưng `Fighting.Service.exe`/`Center.Service.exe` ở 2 thư mục này là
bản cũ (11/6) biên dịch với `PlayerInfo.FightPower = Int32` → khi tạo phòng đấu ném MissingMethod.
- Fix: **restore bộ DLL gốc (Int32)** cho Fight/Center từ backup `SqlDataProvider.dll.bak-gm057-20261110-115616`
  + `Bussiness.dll.bak-fix3-20261110-151207`; **giữ nguyên bộ mới (Int64 + Ma Ngọc) cho Road** (nơi xử lý login/GM).
  Script: `_tools/restore-fight-center-dlls.ps1`; log `data/gmdev/restore-fight-dlls.log`; restart stack 15:44;
  `HEALTH ok=True`, `Error.log` 3 service = 0; đã kiểm tra Fight/Center `FightPower = System.Int32`, Road = `Int64`.
- **Quy tắc deploy từ nay**: không copy `SqlDataProvider.dll`/`Bussiness.dll`/`Game.Server.dll` bản mới vào
  **Fight/Center** trừ khi build lại chính `Fighting.Service.exe`/`Center.Service.exe`; các feature mới (Ma Ngọc,
  plan, GM API) chỉ chạy ở Road.
### 7.9 Fix 16:10 — sửa lại đúng cơ chế "bộ Ma Ngọc" (loadout độc lập, bộ mới trống)

Theo yêu cầu: mỗi bộ là **một bộ 9 ô ngọc riêng**; bộ đang có giữ nguyên, bộ mới **không copy** mà để trống
cho phép gắn các Ma Ngọc khác; chọn bộ nào thì chỉ số theo bộ đó.
- `MagicJadeBuyPlan` (258/19): bộ mới tạo với **IsActive=0**, **không seed copy** layout hiện tại; message "bộ trống".
- `MagicJadeChangePlan` (258/20):
  - Nếu bấm đúng bộ đang active: chỉ đảm bảo bộ có loadout (nếu trống thì nhận 9 ô hiện tại) — **không tháo ngọc**.
  - Nếu đổi sang bộ khác: lưu 9 ô hiện tại vào bộ cũ → active bộ mới → nạp loadout bộ mới; bộ mới **trống** thì
    tháo 9 ô hiện tại ra túi 32+ để người chơi gắn Ma Ngọc khác.
- `MagicJadeHelper.SendPlanInfo`: khi login, nếu bộ active chưa có loadout thì tự "nhận" 9 ô ngọc đang cắm
  (tránh mất bộ đang dùng của các plan tạo trước tính năng).
- Triển khai: build `Game.Server.dll` 16:08, deploy **Road only** lúc 16:10, restart stack; `HEALTH ok=True`,
  `Error.log` 3 service = 0. Fight/Center giữ DLL gốc Int32 (không bị ảnh hưởng).
- Cách dùng: mua bộ mới (1.000) → chọn bộ mới trong combo (9 ô trống, ngọc cũ về túi) → kéo Ma Ngọc khác vào →
  chọn lại bộ cũ để khôi phục bộ cũ; chỉ số thay đổi theo bộ đang chọn.
### 7.10 Fix 16:18 — Ma Ngọc "gắn chặt với bộ" (snapshot 9 viên, không thả ra túi)

Đúng ý người dùng: ngọc đã gắn vào bộ nào thì **thuộc bộ đó**, chuyển bộ không tháo ngọc ra túi.
- Bảng `Sys_Users_MagicStonePlanItem` được mở rộng thành **snapshot full trạng thái**: TemplateID, Lv, Exp,
  6 chỉ số, Hole1..6, IsBinds (ALTER tự động nếu thiếu).
- `MagicJadeHelper.SnapshotPlanJades` chụp 9 ô; `RemoveEquippedJades` tháo 9 viên khỏi túi;
  `MaterializePlanJades` tạo lại 9 viên từ snapshot khi kích hoạt bộ.
- Handler 258/20: lưu snapshot 9 ô hiện tại vào bộ cũ → active bộ mới → tháo 9 viên cũ khỏi túi → tạo 9 viên của bộ mới
  (bộ trống thì 9 ô trống). Bấm đúng bộ đang dùng: chỉ đảm bảo snapshot, không tháo.
- Handler 258/19: mua bộ mới = bộ trống, không copy, không active; chọn trong combo để dùng.
- `SendPlanInfo`: khi login, nếu bộ active chưa có snapshot thì tự nhận 9 ô đang cắm.
- Build `Bussiness.dll` (SHA256 `9E57BE15…`)+`Game.Server.dll` (`E2CDEA93…`), deploy **Road only** 16:18
  (Fight/Center giữ DLL gốc Int32); `HEALTH ok=True`, `Error.log` 3 service = 0; đã xoá loadout cũ (định dạng cũ)
  và reset để login tự nhận set hiện tại.
- Hệ quả UX: chọn bộ khác → 9 viên của bộ cũ **biến khỏi túi** (nằm trong bộ cũ), chỉ số đổi theo bộ mới;
  chọn lại bộ cũ → 9 viên quay lại. Muốn có viên cho bộ mới: Chuyển đổi thêm Ma Thạch → Ma Ngọc rồi gắn.