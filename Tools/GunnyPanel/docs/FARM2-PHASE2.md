# Nông trại 2.0 — Phase 2.1 (trồng trọt / kinh tế / nâng cấp khu)

Ngày: 2026-11-08 · Trạng thái: **code xong, build OK, deploy 12:5x** (sau khi Road shutdown theo lịch Center 1001).

## 1. Đã có gì trong phase 2.1

### Server
- **Lưu trữ DB thật** (`Db_Tank`, bảng mới — xem `docs/DB-CHANGES.md`):
  - `Sys_User_Manor` (Exp, Money, Value, ExpValue, BuyFoodCount, BuffRate/BuffDate)
  - `Sys_User_ManorArea` (6 khu: 1 farm, 2 fish, 3 mine, 4 ranch, 5 đất, 6 cuốc)
  - `Sys_User_ManorGoods` (túi nông sản: TemplateId, Property=bitmask túi, Count)
  - `Sys_User_ManorWorker` (để dành cho phase worker)
- **Tầng dữ liệu**: `Bussiness.ManorBussiness` (static, upsert, an toàn lỗi);
  entity `SqlDataProvider.Data.UserManor*`.
- **PlayerManor** (`Game.Server.GameUtils.PlayerManor`): state trong RAM + ghi DB ngay khi thay đổi;
  load ở `GamePlayer.Login()` (sau Farm.LoadFarmLand), save ở `GamePlayer.SaveToDatabase()`.
- **Cấu hình nông trại** (`Game.Server.Farm.ManorConfigMgr`) tự đọc XML nén zlib trong
  `C:\Gunny\Gunny12.6\WebSite\Request`: `templatemanorlist.xml` (82 item), `ts_manorplant.xml`,
  `ts_upgradetemplate.xml` (Types 109–113), `ts_manortask.xml`, `petexpitemprice.xml`,
  + shop 135 nạp từ DB (`ProduceBussiness.GetALllShop()`).
- **Handler mới (packet 81)**:
  | Subtype | Handler | Chức năng |
  |---|---|---|
  | 24 | RequestFarmInfo | farm info thật từ DB + 81/26 + 81/34 |
  | 11 | FarmPlantBegin | gieo hạt (khu 1), thả con giống (2/4), đặt cuốc khai thác (3); `templateId=0` = dừng |
  | 28 | FarmPlantSpeed | đặt/huỷ thức ăn tự động cho cá (túi 6) / gia súc (túi 3) |
  | 14 | FarmUpPlant | nâng cấp khu 1–6 theo TS_ManorPlant (kiểm tra cấp + tiêu hao vật liệu) |
  | 31 | FarmSellGoods | bán nông sản → tiền nông trại (giá = ReclaimValue) |
  | 15 | FarmBuyGoods | mua vật phẩm shop 135 (moneyType 1=tiền nông trại, 2=DDTMoney, 3=Money) |
  | 27 | FarmUpdatePlantData | refresh: xử lý lớn/thu hoạch + gửi 81/17 toàn bộ khu + 81/26 |
  | 33/34 | FeedFarmPoultry / FarmPetFoodQuery | slacker list + số lượt mua thức ăn pet |
  | 68/19 | BuyPetExpItem (Pet) | mua thức ăn pet theo bảng PetExpItemPrice, cộng item 334102 |
- **Tăng trưởng (lazy, kể cả offline)**:
  - Farm (khu 1): chín theo thời gian `Property3` phút × tốc độ nâng cấp khu (`PlantSpeedValue`/1000);
    chín → tự thu hoạch `Property4 × Property5`, cộng exp `Property6`.
  - Mine (khu 3): mỗi `Property3` phút tự sinh 1 quặng (MINE_STORE) + exp.
  - Fish/Ranch (khu 2/4): mỗi 30 phút tự ăn 1 phần thức ăn đã chọn (foodTempId), +`Property4` điểm;
    đủ `Property3` điểm → thu hoạch con trưởng thành + exp.
  - Được chạy khi mở nông trại (24), refresh 60s (27), và mọi thao tác.
- **Starter kit** (1 lần, cho nông trại mới/trống): 1.000 tiền nông trại + 5 Hạt Giống Cỏ Tươi (332139).
- **Farm level**: exp → cấp theo Type 109 (`GetGradeByExp`), dùng để mở/giới hạn khu & nâng cấp.

### Client
- Không sửa client. Mọi gói tin đúng protocol decompile từ `decompiled_4` (farm/_96$41.as + UI views).

## 2. Hướng dẫn test nhanh

1. Đăng nhập test1 → mở **Nông trại** (lần đầu sẽ tạo dữ liệu manor + starter kit).
2. Bấm vào khu **Nông trại** (cánh đồng) → chọn túi hạt → gieo 1 hạt Cỏ Tươi (30 phút chín).
3. Kiểm tra: cây hiện trên ruộng; nút "làm mới" (refresh 60s) hoặc vào lại nông trại sau 30 phút → nhận
   Cỏ Tươi (333001) + EXP; xem **Quản lý → Cửa hàng** để mua thêm hạt hoặc bán nông sản ở tab "Kho".
4. Nâng cấp khu: giao diện nâng cấp (tab trong menu khu) → gửi 81/14; cần đủ vật liệu (Táo Vàng/Tùng Nhung…).
5. Test nhanh thu hoạch offline: gieo cây, thoát game, chờ đủ 30 phút, vào lại.

## 3. Ghi chú / giới hạn (phase 2.2)

- Worker/thợ (thuê, cho ăn, làm việc, slacker) — DB đã sẵn, handler chưa làm.
- Nhiệm vụ bảng tin (18/19), log (29), xếp hạng (40), an ủi/buff (3,4,8).
- Shop 139 (personal limit shop — vật liệu nâng cấp) và mở khoá khu (điều kiện mở khu theo farm level).
- Farm fight/boss (36,37,39,41,42,43,44,45,46).
- Giá mua/bán, tốc độ sinh trưởng đang dùng đúng dữ liệu XML; nếu muốn test nhanh có thể giảm
  `Property3` trong `templatemanorlist.xml` (zlib) hoặc tăng tốc bằng nâng cấp khu.

## 4. Deploy / rollback

- Deploy 2026-11-08 ~12:5x: backup `C:\Gunny\Gunny12.6\Service\Service\Road\Game.Server.dll.bak-<stamp>`,
  copy `Game.Server.dll` + `.pdb` từ `Full Source\game.server\bin\Debug\net472\`.
- Log: `GunnyPanel\data\deploy-farm2.log`.
- Rollback: copy `Game.Server.dll.bak-*` cũ trở lại + restart Road.Service.
- Bảng DB mới chỉ thêm, không sửa bảng cũ; có thể bỏ bằng `DROP TABLE Sys_User_Manor*` nếu rollback.

## 5. Đính chính protocol (theo spec client docs/FARM2-PROTOCOL.md)

- **Thu hoạch không có request riêng** — server tự thu hoạch theo chu kỳ rồi push 81/17 + 81/26
  (đã làm: lazy check khi mở nông trại/refresh 60s/mọi thao tác, kể cả offline).
- **Điều kiện cấp khi gieo**: dùng Property5 (cấp yêu cầu), KHÔNG phải Property2.
- **Số lượng sản phẩm thu hoạch**: dùng Property2 (số lượng), KHÔNG phải Property5.
- **81/15 mua shop**: field đầu client gửi là ShopItemInfo.GoodsID = cột ID bảng Shop
  (vd 332139135), không phải TemplateID → server tra theo ID rồi lấy TemplateID.
- **BuyFoodCount** mặc định 30 (client tính giá Count = 31 - remain).
- **Fish/Ranch**: Property3 = điểm trưởng thành cần; server tích điểm qua tự cho ăn mỗi 30 phút
  (Property4 điểm/phần), hiện chưa gửi tiến độ điểm cho client (client chỉ thấy trạng thái isPlant).
- **Đã xoá 13 handler nông trại cũ** (EnterFarm, GrowFields, GainFields, PayField, KillCropField,
  HelperSwitchField, ExitFarm, FarmGropFastforward, FarmPoultryLevel, InitFarmTree, CallFarmPoultry,
  FarmCondenser, FarmWater) vì trùng mã 81 nhưng khác payload với client 12.6 — tránh gửi packet sai
  làm client lỗi. Thay bằng handler Farm 2.0 (11/14/15/24/27/28/31/33/34 + manage info 81/1).

## 6. Trạng thái deploy (quan trọng)

- Bản build phase 2.1 (12:5x/13:04) đã copy sẵn vào Service\Service\Road (backup DLL phase 1 đã có).
- Road.Service chạy quyền **Administrator** nên agent (chạy quyền user thường) **không thể stop/start**
  trực tiếp. Đã dừng server an toàn bằng Center Shutdown(zone 1001); process cũ có thể còn treo console.
- **Cần user restart 1 lần** bằng: double-click GunnyPanel\_tools\restart-road-farm2.bat (bấm Yes UAC),
  hoặc mở PowerShell Admin chạy _tools\restart-road-farm2.ps1. Script tự stop process cũ, backup,
  copy DLL mới, start, đợi port 8527/9601 + health check.
- Nếu không muốn script: đóng cửa sổ Road.Service cũ (hoặc End Task) rồi chạy
  C:\Gunny\Gunny12.6\Service\Service\Road\Road.Service.exe bằng quyền Admin.
- Log deploy: GunnyPanel\data\deploy-farm2.log; console Road (nếu start bằng script) nằm trong
  GunnyPanel\data\tmp\road-console-*.log (khi script redirect) hoặc cửa sổ console riêng.

## 7. Sau khi restart — checklist test

1. Login test1 → vào **Nông trại**.
2. Xem tiền nông trại (Quản lý → Shop): kỳ vọng 1.000 (starter kit), tab Kho có 5 Hạt Giống Cỏ Tươi.
3. Vào khu Nông trại → gieo 1 hạt (menu hạt giống) → cây hiện; đợi 30 phút (hoặc thoát game chờ đủ rồi vào lại)
   → tự thu hoạch Cỏ Tươi (333001) + EXP; xem Kho/Quản lý.
4. Bán nông sản ở tab Kho (giá = ReclaimValue), mua thêm hạt ở Shop bằng tiền nông trại.
5. Nâng cấp khu (menu nâng cấp) khi đủ điều kiện + vật liệu.
6. Mua thức ăn pet ở nút "Mua EXP" (68/19) — kiểm tra số lượt giảm + nhận item 334102.
7. Bật/tắt tự cho ăn ở khu Ao Cá / Bãi Cỏ (menu thức ăn) sau khi thả con giống.
8. Nếu có lỗi: gửi Road\logs\Error.log + ảnh màn hình; các dòng "LoadCommandHandler code001: X"
   là request của phase 2.2 (worker/task) chưa implement — bỏ qua.

## 8. Sự cố 14:17 — login lỗi TypeLoadException (đã fix 14:21)

**Log gốc (Road\logs\Error.log, 14:17:35):**

```
ERROR - Game.Server.GameServer - Đang nhập vào server thất bại, hãy thử vào server khác...
System.TypeLoadException: Could not load type 'SqlDataProvider.Data.UserManorAreaInfo'
  from assembly 'SqlDataProvider, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'.
   at Game.Server.GameUtils.PlayerManor..ctor(GamePlayer player)
   at Game.Server.GameObjects.GamePlayer..ctor(...)
   at Game.Server.Packets.Client.UserLoginHandler.HandlePacket(...)
```

**Nguyên nhân:** script deploy đợt đầu chỉ copy Game.Server.dll (+pdb). Phase 2.1 còn dùng
SqlDataProvider.Data.UserManor* (mới) và Bussiness.ManorBussiness (mới) nên Road cần **cả**
SqlDataProvider.dll và Bussiness.dll bản build mới; thiếu 2 DLL này → TypeLoadException
ngay khi tạo PlayerManor lúc login.

**Đã fix (14:21):**
- Copy Bussiness.dll, Bussiness.pdb, SqlDataProvider.dll, SqlDataProvider.pdb bản mới vào
  Service\Service\Road (file cũ đổi tên *.20261108-142122.bak).
- Kiểm tra reflection: các type UserManorInfo/AreaInfo/GoodsInfo/WorkerInfo, ManorBussiness,
  PlayerManor, toàn bộ handler Farm 2.0 **resolve OK** trong thư mục Road.
- Cập nhật _tools/restart-road-farm2.ps1: copy đủ bộ 3 DLL (Game.Server + Bussiness + SqlDataProvider)
  và pdb; nếu file đang bị process giữ thì tự rename rồi copy.
- **Cần restart Road lần nữa** để nạp DLL mới (process hiện tại vẫn giữ DLL cũ trong RAM):
  double-click _tools\restart-road-farm2.bat (UAC Yes).

**Quy tắc từ nay:** mỗi lần deploy phase nông trại phải copy **Game.Server.dll + Bussiness.dll +
SqlDataProvider.dll** (và pdb) — không chỉ Game.Server.dll.

## 9. Phase 2.2 — Thợ / nhiệm vụ / shop 139 (2026-11-08 ~14:35)

### 9.1 Hệ thống thợ (worker)
- Bảng Sys_User_ManorWorker + ManorBussiness (GetWorkers/GetWorker/SaveWorker/DeleteWorker/GetWorkerByWorkerUserID/GetManorCandidates/GetUserNickName).
- PlayerManor xử lý: thuê/mua đứt thợ (HireWorker — giá thuê Cỏ Vàng theo ManorEmploryMoneys, mua đứt theo ManorFreeMoneys Xu), sa thải, tự chuộc thân, gia hạn hợp đồng (ManorAddTimeMoneys Xu/ngày), đổi danh hiệu (ManorOtherNameMoney), cho ăn (ManorGiftMoney/bag 10 → energy), làm việc/nghỉ, đánh/yêu/tặng quà (ManorBeatLoveCD, ManorWorkDecPhysic/Feeling), chu kỳ làm việc 20 phút tự giảm thể lực/tâm trạng + exp nghề, tự nghỉ khi kiệt sức.
- Handler mới (packet 81): 2 thuê, 3 gia hạn, 4 danh hiệu, 5 sa thải, 6 tự chuộc, 8 lock (stub), 9 cho ăn, 12/13 làm/nghỉ, 20/21/22 đánh/yêu/quà, 25 đập thợ lười bạn bè, 30 danh sách ứng viên, 32 push update worker. 81/1 giờ trả info bản thân (title/isEmploy/ransomMoney/skill grade/bossID+boss info).
- 81/33 slacker query trả friendID có thợ đang làm với feeling<=20 (thay vì rỗng).

### 9.2 Nhiệm vụ bảng tin
- Bảng mới Sys_User_ManorTask + entity UserManorTaskInfo; mỗi ngày tạo 6 nhiệm vụ (3 loại 1 = đập thợ lười, 3 loại 2 = nộp vật phẩm) từ TS_ManorTask.xml.
- 81/18 trả danh sách (flag=1), 81/19 nộp/thưởng: loại 1 kiểm tra tiến độ; loại 2 kiểm tra cấp Trang Trại (Condition3) + số goods (Condition2) rồi trừ goods; thưởng add vào túi thường (rơi vào mail nếu túi đầy). Tiến độ loại 2 tự cập nhật theo số goods khi mở bảng tin/refresh.
- Push 81/18 kèm 81/24 (vào nông trại mình), 81/27 (refresh 60s), sau 81/19, sau 81/25.

### 9.3 Shop nông trại 139 (packet 528 + 345)
- ShopPersonalLimitHandler (528) đọc shopID từ packet (bỏ hard-code 120) → hỗ trợ 139.
- NewBuyGoodsHandler (345) special-case ShopID 139: trừ **Cỏ Vàng** (AValue1), item CategoryID 87 → kho farm goods (Property1 từ ManorConfigMgr), còn lại → túi thường; push 81/26 + message + packet 44.

### 9.4 Cấu hình ServerConfig dùng cho nông trại
ManorConfigMgr đọc WebSite\Request\ServerConfig.xml (zlib): ManorEmploryMoneys, ManorFreeMoneys, ManorAddTimeMoneys, ManorOtherNameMoney, ManorGiftMoney, ManorBeatLoveCD, ManorWorkEventMinute/Random, ManorWorkDecPhysic/Feeling, FarmHavestMinute, ManorTaskBoxId.

## 10. Deploy phase 2.2 (2026-11-08 14:39)

- Build final: game.server.csproj Debug — PASS 0 error (log data/tmp/build-phase22-final.log),
  Game.Server.dll 3.266.048 bytes lúc 14:39:17.
- Đã copy vào Service\Service\Road: **Game.Server.dll/.pdb + Bussiness.dll/.pdb + SqlDataProvider.dll/.pdb**
  (file cũ đổi tên *.20261108-143923.old); hash khớp bản build; reflection xác nhận đủ type
  UserManorTaskInfo, ManorCandidateInfo, toàn bộ handler worker/task, PlayerManor methods.
- Bảng Sys_User_ManorTask đã tạo trong Db_Tank (0 dòng — sẽ sinh khi user mở bảng tin/lần đầu vào farm).
- **Cần user restart Road** (Road.Service chạy quyền Admin, agent không stop/start được):
  double-click _tools\restart-road-farm2.bat → Yes UAC. Script tự stop process cũ, backup, copy đủ 3 DLL,
  start, đợi port 8527/9601 + health check.
- Sau restart test: Quản lý → tab Tuyển thợ (81/30 danh sách ứng viên) → thuê test2; tab Thông tin
  (cho ăn/đánh/yêu/quà/đổi danh hiệu/sa thải); chọn khu cho thợ làm; bảng tin (icon giấy) → 6 nhiệm vụ,
  thử nộp; Shop → mua Táo Vàng 333006 giá 888 Cỏ Vàng.

## 11. Phase 2.3 — BXH + chat + khung hoạt động (2026-11-08 ~15:00)

### 11.1 BXH nông trại (FarmRankJob)
- `Game.Server.Farm.FarmRankJob` khởi động cùng `GameServer.Start()` (timer 5 phút, lần đầu sau 3s).
- Sinh 2 file zlib vào `Request` dir (config `Request` = `C:\Gunny\Gunny12.6\WebSite\Request`):
  - `ManorWealth.xml` — BXH Tài Phú: `Order, NickName, UserId, TotalGP (exp farm), Money, Wealth,
    Colors, Style, Sex, AreaId (config AreaID), AreaName (config AreaName)`.
  - `ManorAbility.xml` — BXH Lao Động: `Order, NickName, UserId, TotalAbilityGrades (tổng cấp 4 nghề từ
    exp thợ), BossNickName, Colors, Style, Sex, AreaId, AreaName`.
- Dữ liệu: `ManorBussiness.GetManorWealthRank(limit)` / `GetManorAbilityRank(limit)` (entity `UserManorRankInfo`).
- Ghi file an toàn (tmp → move), lỗi được log `FarmRankJob.Refresh error`.
- Backup dữ liệu gốc: `data/tmp/backup-rank/ManorWealth.original.xml` + `ManorAbility.original.xml`.

### 11.2 Chat nông trại + scene tracker
- `FarmSceneTracker`: map playerId → farmUserId (cập nhật khi nhận 81/24), lọc người online qua
  `WorldMgr.GetPlayerById`, tự dọn entry offline.
- 81/38 → packet 19 `[int zoneId][byte channel=38][bool false][UTF sender][UTF msg]` (kèm người gửi).
- 81/37 → broadcast `[playerId][x][y]`; 81/39 → broadcast `[farmUserID][playerID]` + xoá tracker.

### 11.3 Khung hoạt động Sâu Bọ
- Handler 36/37/39/40/41/42/45 đã có; 45 (mở hoạt động) trả message + 81/46 `isFight=false`
  để client không treo.
- **Chưa làm quái**: asset `resourceSite + image/farm/fightliving/farmliving{npcType}.swf` trả 404
  trên CDN hiện tại (đã thử id 1..30, 100-102, 1001-1010, 9001-9005, 11001-11005, 20001-20003...).
  Khi có asset: bật activity state, spawn master (npcType hợp lệ), xử lý 42 → trừ máu/nhận điểm,
  push 40/43/44/46, thưởng theo `ManorActivityScoreRange/PersonRange/AwardRange`.

### 11.4 Deploy phase 2.3
- Build: SqlDataProvider + Bussiness + game.server **PASS** (log `data/tmp/build-phase23.log`).
- Đã copy 3 DLL/.pdb vào Road folder; reflection xác nhận `FarmRankJob`, `FarmSceneTracker`,
  `UserManorRankInfo`, các handler 36/37/38/39/40/41/42/45.
- **Cần restart Road** bằng `_tools\restart-road-farm2.bat` (UAC) để nạp bản mới.
- Sau restart kiểm tra: `WebSite\Request\ManorWealth.xml` được ghi lại (mtime mới, > 2 KB zlib),
  Quản lý → BXH hiển thị; chat trong nông trại (test2 cùng farm) nhận được; bấm "Mở hoạt động" hiện message.

## 12. Phase 2.4 — Nhật Ký / hạn mức shop 139 / tự bảo vệ / reset pet food (2026-11-08 ~15:30)

### 12.1 DB mới (`data/sql/manor_phase24.sql`, đã chạy)
- `Sys_User_Manor` + cột `ProtectDate datetime`, `BuyFoodDate datetime`.
- Bảng `Sys_User_ManorLog` (ID, UserID, LogType, Content, CreateDate) + index UserID/CreateDate.
- Bảng `Sys_User_ManorShopLimit` (ID, UserID, GoodsID, MonthKey, BuyCount, UpdateDate) + unique
  (UserID, GoodsID, MonthKey).

### 12.2 Nhật Ký (81/1)
- `ManorBussiness.AddManorLog/GetManorLogs/TrimManorLogs`; `PlayerManor.AddLog(...)` gọi từ các sự kiện:
  gieo trồng, thu hoạch farm/mỏ/cá+bãi cỏ, bán nông sản, mua pet food, thuê/mua đứt/sa thải/tự chuộc thợ,
  nâng cấp khu, hoàn thành nhiệm vụ, tự bảo vệ, mua shop 139.
- 81/1 gửi `logCount` + từng dòng `[int 0][UTF content]` (mới nhất trước, tối đa 20 dòng hiển thị).
- Tự dọn còn 50 dòng/user mỗi 10 lần ghi.

### 12.3 Reset lượt thức ăn pet
- `EnsureDailyPetFoodReset`: nếu `BuyFoodDate` cũ hơn hôm nay → `BuyFoodCount = 30`, cập nhật ngày.
- Gọi khi load nông trại và trước mỗi lần mua (68/19).

### 12.4 Hạn mức shop 139
- `EnsureShopMonth` nạp hạn mức tháng (`MonthKey = yyyyMM`), tự xoá dữ liệu tháng cũ.
- `GetShop139Bought(goodsID)` / `AddShop139Purchase(goodsID, count)`.
- `ShopPersonalLimitHandler` (528) trả đúng boughtCount cho shop 139; `NewBuyGoodsHandler` (345) chặn khi
  `bought + count > LimitPersonalCount` và ghi log sau khi mua thành công.

### 12.5 Tự bảo vệ (81/8)
- `BuySelfProtect(days)`: giá `ManorAddTimeMoneys(grade) * days` Xu; cộng dồn `ProtectDate`; log + push.
- 81/1 trả `isProtect`, `protectDate`, `protectMoney` thật.
- 81/30 trả `protectDate` của ứng viên; `HireWorker` chặn thuê/mua đứt khi ứng viên còn bảo vệ.

### 12.6 Build/deploy
- Build: SqlDataProvider + Bussiness + game.server **PASS** (log `data/tmp/build-phase24.log`).
- 3 DLL/.pdb đã copy vào Road; reflection xác nhận `UserManorLogInfo`, `UserManorShopLimitInfo`,
  `PlayerManor.AddLog/EnsureShopMonth/GetShop139Bought/BuySelfProtect/EnsureDailyPetFoodReset`.
- **Cần restart Road** bằng `_tools\restart-road-farm2.bat` (UAC).

## 13. Phase 2.5 — Nhiệm vụ đập thợ lười đúng chuẩn (2026-11-08 ~15:45)

### 13.1 Friend list bị rỗng (đã sửa phía website)
- Client tải friend list qua `REQUEST_PATH + IMListLoad.ashx` (loader type 7 = CompressRequestLoader) với
  tham số `id`, nhưng handler gốc `Tank.Request.IMListLoad` chỉ đọc `selfid` → luôn trả danh sách rỗng.
- Đã thay `WebSite\Request\IMListLoad.ashx` bằng handler inline (dynamic compile) đọc `id` hoặc `selfid`,
  query `Sys_Users_Friends` + `Sys_Users_Detail`, trả XML nén zlib đúng analyzer `FriendListAnalyzer`.
  Backup gốc: `data/tmp/backup-web/IMListLoad.ashx.original`.
- Dữ liệu test: thêm quan hệ bạn bè test1 ↔ test2/3/4/5.

### 13.2 Cơ chế thợ lười thật
- 81/33 (slacker query): trả friendID có farm đang có thợ "lười" = thợ `IsWorking=1` và `Feeling <= 20`.
- 81/25 (client bấm búa ở farm bạn, không payload):
  1. Lấy farm đang xem từ `FarmSceneTracker.GetFarm(playerId)`.
  2. Nếu là farm mình → thông báo cần đến farm bạn.
  3. Chọn ngẫu nhiên 1 thợ đang làm của farm đó, `Feeling -= 10`, lưu DB.
  4. Push 81/32 cho chủ farm nếu online.
  5. `AddTaskProgress(1, 1)` + push 81/18 + thông báo.
- `FinishTask` loại 1 yêu cầu tiến độ thật (`Condition1 >= target`), đã bỏ test-mode fake progress.
- Test data: farm test2 có 2 thợ đang làm việc với tâm trạng 10 (hihi, hahaa) → vào farm test2 sẽ thấy búa.

### 13.3 Build/deploy
- Build game.server PASS (log `data/tmp/build-group2.log`); đã copy Game.Server.dll/.pdb (+ bộ DLL) vào Road.
- **Cần restart Road** bằng `_tools\restart-road-farm2.bat` (UAC) rồi đăng nhập lại test1 để client tải
  friend list mới.

## 14. Phase 2.6 — Hoạt động Sâu Bọ (2026-11-08 ~16:00)

### 14.1 Asset quái (đã tìm ra)
- CDN trả 200 cho đúng 2 file trong dải 1..2600 + 700..760: `farmliving713.swf`, `farmliving714.swf`
  (giải nén thấy class `farm.living.Living713/714`, có nhãn `born` đúng như client gọi).
- Vì vậy `npcType` dùng 713/714 là hiển thị được quái thật.

### 14.2 Cấu hình (ServerConfig.xml — đã có sẵn)
- `ManorActivityConfig = "6,0|10:00-18:00|5|15"` → thứ cho phép (T7, CN) | khung giờ | số quái tối đa | giây hồi quái.
- `ManorActivityScoreRange = "300,800,1500,99999"` → mốc điểm thưởng.
- `ManorActivityAwardRange` → danh sách item thưởng theo mốc (4 mốc).

### 14.3 Luồng hoạt động (FarmActivityMgr)
1. Chủ farm bấm "Mở hoạt động" trong bảng tin → **81/45** → server kiểm tra khung giờ, tạo vòng 30 phút
   (tối đa đến 18:00), spawn 5 quái (713/714) → broadcast **81/46** cho toàn zone.
2. Người xem farm đó nhận 81/46 (popup mời nếu đang ở sảnh, hoặc tự tải lại farm nếu đang xem).
3. Vào farm → client gửi **81/36** → server trả scene người chơi + quái; người mới được broadcast **81/44**.
4. Di chuyển **81/37** được broadcast; rời scene **81/39**.
5. Click quái → **81/42** masterID → server diệt quái, +100 điểm, push **81/43** (xoá quái) và **81/40**
   (BXH điểm realtime cho người trong farm); quái hồi sau 15s cho đến tối đa 5 con.
6. Hết 30 phút: server push 81/46 isFight=false, gửi thư thưởng theo mốc điểm (300/800/1500),
   ghi Nhật Ký nông trại. Quái/điểm lưu in-memory (restart Road giữa vòng sẽ mất vòng đó).

### 14.4 Deploy
- Build game.server PASS (log `data/tmp/build-group3.log`); đã copy bộ DLL vào Road.
- **Cần restart Road** bằng `_tools\restart-road-farm2.bat` (UAC). Hoạt động chỉ mở trong khung 10:00-18:00
  Thứ 7/CN (hôm nay là Chủ Nhật nên test được ngay trước 18:00).

## 15. Phase 2.7 — Thưởng BXH tuần + buff + sự kiện thợ (2026-11-08 ~16:00)

### 15.1 DB mới (`data/sql/manor_phase27.sql`, đã chạy)
- `Sys_User_ManorRankSettle` (WeekKey PK, SettleDate): đánh dấu tuần đã chốt.
- `Sys_User_ManorRankReward` (ID, WeekKey, RankType, UserID, [Rank], ItemID, CreateDate; unique theo
  WeekKey+RankType+UserID) — chống phát trùng.

### 15.2 FarmRankSettler
- Chạy trong tick của `FarmRankJob` (3s sau start rồi mỗi 5 phút). Tuần = `year*100 + dayOfYear/7+1`.
- Khi tuần chưa chốt: lấy top 100 mỗi BXH (`GetManorWealthRank` / `GetManorAbilityRank`), parse
  `ManorWealthsOrderReward` / `ManorAbilityOrderReward` (dạng `min-max,itemId|...`), gửi thư thưởng
  qua `WorldEventMgr.SendItemsToMail` (hỗ trợ offline), ghi bảng reward, đánh dấu tuần, thông báo hệ thống.
- Lưu ý: hiện chốt theo **số liệu tích luỹ** (chưa trừ baseline đầu tuần) — có thể nâng cấp sau.

### 15.3 Buff tốc độ + sự kiện thợ
- `GetAreaSpeed` nhân thêm hệ số buff: nếu `BuffRate>0` và `BuffDate>now` → speed = speed*100/(100+BuffRate).
  Buff hiển thị ở 81/24 (buffRate/buffDate) và tooltip khu.
- Hoạt động Sâu Bọ: điểm >= 1500 → tặng BuffRate=10 trong 2 giờ (lưu DB + thông báo).
- Chu kỳ thợ: mỗi lần tick roll `ManorWorkEventRandom`% → giảm/tăng tâm trạng hoặc hồi thể lực;
  kiệt sức tự nghỉ (đã có).
- Mua đứt thợ: chủ cũ + người lao động (nếu online) nhận thông báo.

### 15.4 Deploy
- Build PASS (`data/tmp/build-group4.log`), đã copy 3 DLL vào Road.
- **Cần restart Road**; sau khi start ~3 giây, tuần hiện tại sẽ được chốt thưởng 1 lần (BXH tích luỹ hiện có).
  Muốn test lại chốt tuần: xoá dòng trong `Sys_User_ManorRankSettle` (và có thể xoá `Sys_User_ManorRankReward`)
  rồi restart.
