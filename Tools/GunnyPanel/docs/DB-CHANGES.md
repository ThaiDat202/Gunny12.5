# Database đã cập nhật từ lúc bắt đầu code

## Tóm tắt nhanh

| Database | Trạng thái |
|---|---|
| **Db_Tank** | **Có ghi** — toàn bộ dữ liệu user/item/title/collection |
| **Db_Tank_All** | **Chỉ đọc** — template `Shop_Goods`, `TS_*`, `New_Title`, `Jamps*`, `Cloth*`… |
| **Db_Gm** | **Không ghi** — Panel chỉ đọc cấu hình |
| Db_Logs / Db_Membership / Db_Count / Db_Tank_Orig / Db_Tank_All_Test | Không đụng |

## Các bảng trong `Db_Tank` đã ghi

### 1. `Sys_Users_Detail`
- Ban/unban + `ForbidDate`, `ForbidReason`, `State` (đã test).
- Sửa level/money/gold/gift token (test).
- `guardCoreGrade = 20` (Bảo Vệ).
- Hiện user 1: `Grade=70, State=0, GiftToken=123, guardCoreGrade=20`.

### 2. `Sys_Users_Goods`
- Award gửi item qua mail (tạo/xoá test).
- Set chỉ số trang bị đang mặc:
  - `latentEnergyCurStr/latentEnergyNewStr = 80,80,80,80`
  - `MagicLevel = 40`, `MagicExp = 298100`
  - `enchantSpiritLevel = 20`
- Soft-delete item test (nếu có).
- Hiện user 1 có **177** dòng goods.

### 3. `User_Messages`
- Award mail + mail test; đã xoá mail test `GM Panel`/`TEST%`.
- Còn mail hợp lệ khác.

### 4. `Sys_User_Drills` (lưu ý: **User** số ít)
- Tạo đủ **6 mũi khoan** (`BeadPlace 13–18`, `DrillPlace 0–5`), `HoleLv = 6`.
- Hiện **6 dòng**.

### 5. `Sys_Users_Card`
- Album thẻ: 82 thẻ hợp lệ, giữ 5 thẻ trang bị.
- Hiện **87 dòng** = 5 equipped + 82 album.

### 6. `User_Avatar_Colection`
- Full bộ sưu tập trang phục/vũ khí: **111 bộ** (58 nam + 37 nữ + 16 vũ khí).
- Không duplicate.

### 7. Sổ tay thám hiểm
- `Sys_Users_ExplorerManualInfo`: `manualLevel=21`, `progress=1/1`, `havePage=45`.
- `Sys_Users_ExplorerManual`: `ManualLevel=21`, `HavePage=45`.
- `Sys_Users_ExplorerManualInfoPages`: **45 trang**.
- `Sys_Users_ExplorerManualInfoDebris`: **630 mảnh**.

### 8. `Sys_User_Rank`
- Thêm danh hiệu: **548** hợp lệ.
- Hiện **554** dòng (548 mới + 6 dòng cũ game tự thêm; có thể dọn lại nếu cần).

### 9. `Sys_Users_Honor`
- Vinh Dự Gunner: `HonorLevel = 100`, `HonorExp = 0`.

### 10. `Sys_Users_Sworn`
- Nhẫn Cầu Hôn: `swornId=9522`, `swornCount=124860`, `isHasSwornItem=1`.

### 11. `King_Grade_Data`
- Cấp Vua Gà: `ddtKingGrade=10`, 6 nhánh level 10.

## Bảng KHÔNG ghi
- `Db_Tank_All`: chỉ SELECT template (`Shop_Goods`, `New_Title`, `TS_HonorSystem_Template`, `Cloth*`, `Jamps*`…).
- `Db_Gm`: không ghi.
- `Sys_Users_Mark`, `Sys_Users_SoulRing_*`, `Sys_Users_AvatarCollection`: không ghi.
- Không sửa cấu trúc bảng/DB nào.

## Dữ liệu test đã dọn
- Xoá mail test `GM Panel`/`TEST%` (0 còn lại).
- Xoá 6 dòng `Sys_User_Rank` cũ không có trong `New_Title`.
- Xoá card/album lỗi, không còn `TemplateID=0/CardType=0`.
- Các item test Award đã được xoá trước đó.

---

## Nông trại 2.0 (Manor) — 2026-11-08 (task-2, farm-db)

Schema **mới** cho Nông trại 2.0 (client Gunny 12.6, packet 81). **Không đụng** hệ
nông trại cũ `Sys_User_Farm` / `Sys_User_Field` (giữ nguyên, không sửa/xoá).

### Bảng đã tạo trong `Db_Tank` (script `data\sql\manor_tables.sql`)

Script idempotent (IF NOT EXISTS — chạy lại vô hại), chỉ CREATE TABLE/INDEX, không DROP/ALTER.
Chạy:

```powershell
$sqlcmd='C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE'
& $sqlcmd -S DESKTOP-SMLR4VU -U sa -P 123456 -C -d Db_Tank -i "C:\Gunny\ToolGunny\GunnyPanel\data\sql\manor_tables.sql"
```

| Bảng | Khoá | Cột chính |
|---|---|---|
| `Sys_User_Manor` | `UserID` PK | `Exp, Money, [Value], ExpValue, BuyFoodCount, BuffRate, BuffDate, LastUpdate` |
| `Sys_User_ManorArea` | `ID` identity PK, UNIQUE `(UserID, AreaType)` | `UserID, AreaType (1..6), [Level], LevelTwo, IsPlant, PlantTempId, PlantDate, RipeDate, FoodTempId, FoodDate, ProduceCount, LastUpdate` |
| `Sys_User_ManorGoods` | `ID` identity PK, UNIQUE `(UserID, TemplateId, [Property])` | `UserID, TemplateId, [Property], [Count]` |
| `Sys_User_ManorWorker` | `ID` identity PK, UNIQUE `(OwnerID, WorkerUserID)` | `OwnerID, WorkerUserID, [Name], [Title], Feeling, Energy, FarmExp, FishExp, MineExp, RanchExp, IsWorking, WorkType, WorkBeginDate, EmployProtectDate, ProtectPrice, Sex, Colors, Style, HiredDate` |

Index bổ sung: `IX_Sys_User_ManorWorker_WorkerUserID (WorkerUserID)`,
`IX_Sys_User_ManorGoods_UserID_TemplateId (UserID, TemplateId)`.
AreaType: 1=FARM, 2=FISH, 3=MINE, 4=RANCH, 5=FARM_LAND, 6=HOE_UPGRADE.
`LevelTwo` = landLevel (type 1) / mineLevel (type 3) — khớp header protocol 81/24.

Đã test thật trên `Db_Tank`: INSERT/SELECT/UPDATE/DELETE 1 dòng mỗi bảng (UserID test
`999999`), test UNIQUE constraint chặn duplicate → **OK**, sau đó **đã xoá sạch** dữ liệu
test (0 dòng còn lại). Script chạy 2 lần liên tiếp: lần 2 “already exists - skipped”, exit 0.

### File code mới (server source)

| File | Nội dung |
|---|---|
| `SqlDataProvider\SqlDataProvider.Data\UserManorInfo.cs` | entity `UserManorInfo` |
| `SqlDataProvider\SqlDataProvider.Data\UserManorAreaInfo.cs` | entity `UserManorAreaInfo` |
| `SqlDataProvider\SqlDataProvider.Data\UserManorGoodsInfo.cs` | entity `UserManorGoodsInfo` |
| `SqlDataProvider\SqlDataProvider.Data\UserManorWorkerInfo.cs` | entity `UserManorWorkerInfo` |
| `Bussiness\Bussiness\ManorBussiness.cs` | data layer (static API, pattern BaseBussiness/db + log) |

Entities: `namespace SqlDataProvider.Data`, kế thừa `DataObject`, auto-property.
Không sửa `PlayerBussiness.cs` hay file có sẵn nào khác.

### API `ManorBussiness` (namespace `Bussiness`) — tất cả **static**

Mỗi call tự tạo `ManorBussiness` (mở `Sql_DbObject` theo `AppConfig/conString`) rồi dispose
→ an toàn đa luồng, handler **không cần** `new`/`using`. Lỗi SQL/connection được try/catch
+ ghi `BaseBussiness.log`, trả `null` / `false` / list rỗng (không ném exception).

```csharp
// Manor
UserManorInfo GetManor(int userId)                    // null nếu chưa có
bool AddManor(UserManorInfo info)                    // INSERT (false nếu đã tồn tại)
bool UpdateManor(UserManorInfo info)                 // UPDATE theo UserID
bool SaveManor(UserManorInfo info)                   // upsert
// Area (6 khu)
List<UserManorAreaInfo> GetAreas(int userId)
UserManorAreaInfo GetArea(int userId, int areaType)  // 1..6
bool SaveArea(UserManorAreaInfo info)                // ID<=0: insert/update theo (UserID,AreaType); có ID: update
// Goods
List<UserManorGoodsInfo> GetGoods(int userId)
bool SaveGoods(UserManorGoodsInfo info)              // ID<=0: insert/update theo (UserID,TemplateId,Property)
bool DeleteGoods(int id)
bool DeleteGoods(int userId, int templateId, int property)
// Worker
List<UserManorWorkerInfo> GetWorkers(int ownerId)
bool SaveWorker(UserManorWorkerInfo info)            // ID<=0: insert/update theo (OwnerID,WorkerUserID)
bool DeleteWorker(int id)
bool DeleteWorker(int ownerId, int workerUserID)
// Khởi tạo
bool CreateDefaultManor(int userId)                  // idempotent; alias: Manor_CreateDefault(int)
```

`CreateDefaultManor(userId)` tạo (nếu thiếu): dòng manor `Exp=Money=Value=ExpValue=BuffRate=0`,
`BuffDate=1900-01-01`; 6 area type 1..6 `Level=1`, `LevelTwo=1` cho type 1 & 3, còn lại 0,
`IsPlant=false`, `PlantTempId=FoodTempId=0`, `ProduceCount=0`; goods/workers để trống.

### Ví dụ gọi từ handler phase 2

```csharp
using Bussiness;
using SqlDataProvider.Data;

// Login / lần đầu vào nông trại: đảm bảo có dữ liệu (gọi lại vô hại)
ManorBussiness.CreateDefaultManor(Player.PlayerId);

// 81/24 REQUEST_FARM_INFO
UserManorInfo manor = ManorBussiness.GetManor(farmUserID);
List<UserManorAreaInfo> areas = ManorBussiness.GetAreas(farmUserID);
int landLevel = 1, mineLevel = 1;
foreach (UserManorAreaInfo a in areas)
{
    if (a.AreaType == 1) landLevel = a.LevelTwo;
    else if (a.AreaType == 3) mineLevel = a.LevelTwo;
}
int workerCount = ManorBussiness.GetWorkers(farmUserID).Count;
int goodsCount = ManorBussiness.GetGoods(farmUserID).Count;

// Gieo hạt (81/11): sửa entity rồi SaveArea — SaveArea/SaveGoods/SaveWorker là UPDATE TOÀN BỘ dòng
UserManorAreaInfo area = ManorBussiness.GetArea(userId, 1);
area.IsPlant = true;
area.PlantTempId = tempId;
area.PlantDate = DateTime.Now;
area.RipeDate = seconds;          // theo spec protocol
ManorBussiness.SaveArea(area);

// Thu hoạch → cộng kho nông sản (upsert theo UserID+TemplateId+Property)
UserManorGoodsInfo goods = new UserManorGoodsInfo
{
    UserID = userId, TemplateId = templateId, Property = 0, Count = addCount
};
ManorBussiness.SaveGoods(goods);  // ID<=0 → tự tìm dòng cũ để UPDATE, không tạo trùng
```

Lưu ý cho dev handler:
- `Save*` = UPDATE **toàn bộ** cột của dòng → luôn `Get*` entity trước, sửa field cần sửa rồi
  mới `Save*`; nếu tạo entity mới tinh (field chưa set) thì các string sẽ thành rỗng/0.
- `DateTime` mặc định `DateTime.MinValue` được chuẩn hoá về `1900-01-01` khi ghi DB
  (tránh lỗi SqlDateTime overflow).
- `RipeDate` là `int` (giữ đúng protocol); `ProduceCount`, `LastUpdate` là cột dự phòng.
- `UpdateManor/AddManor/SaveManor` tự set `LastUpdate = DateTime.Now`.
- Không restart Road.Service; code mới chỉ nằm trong `SqlDataProvider.dll`/`Bussiness.dll`
  build Debug, phase 2 deploy sau khi handler được viết.

### Mở rộng schema sau này

Script có sẵn block ghi chú mẫu `ALTER TABLE ... ADD` (idempotent bằng `COL_LENGTH`).
Khi proto chốt thêm field (vd ProduceCount/LastUpdate khác nghĩa, lastHarvest...): thêm cột
bằng ALTER + cập nhật entity + INSERT/UPDATE tương ứng trong `ManorBussiness.cs`.

### Build

`MSBuild SqlDataProvider.csproj` + `bussiness.csproj` (`/p:Configuration=Debug`) → **0 error**
(chỉ warning có sẵn của project). Runtime smoke test data layer với DB thật: **56/56 check PASS**
(create default, manor CRUD, area CRUD, goods upsert/delete, worker upsert/delete, unique
constraint, idempotent create), dữ liệu test đã dọn sạch.

---

## Nông trại 2.2 (Manor — worker/task data layer) — 2026-11-08 (task-3, farm-db2)

Mở rộng DB + data layer cho **phase 2.2**: nhiệm vụ bảng tin (task 81/18-19-25) và tra cứu
thợ / ứng viên tuyển thợ (81/2, 81/5, 81/30). **Không sửa bảng cũ**, chỉ thêm 1 bảng mới.

### Bảng mới trong `Db_Tank` (script `data\sql\manor_task_table.sql`)

Script idempotent (**IF NOT EXISTS**, chạy lại vô hại), chỉ CREATE TABLE/INDEX, không DROP/ALTER.
Chạy:

```powershell
$sqlcmd='C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE'
& $sqlcmd -S DESKTOP-SMLR4VU -U sa -P 123456 -C -d Db_Tank -i "C:\Gunny\ToolGunny\GunnyPanel\data\sql\manor_task_table.sql"
```

| Bảng | Khoá | Cột chính |
|---|---|---|
| `Sys_User_ManorTask` | `ID` identity PK, UNIQUE `(UserID, TaskDate, TaskId)`, index `IX_Sys_User_ManorTask_UserID_TaskDate (UserID, TaskDate)` | `UserID, TaskDate (DATE), TaskId, TemplateId, Condition1..3, IsComplete, IsReward, UpdateDate` |

- `TaskDate` là **DATE** (reset theo ngày; mọi query lọc `UserID + ngày`, luôn chuẩn hoá `00:00:00`).
- `TaskId` = **instance id** gửi client trong 81/18 và client gửi lại trong 81/19 ⇒ unique trong
  phạm vi (UserID, TaskDate).
- `TemplateId` = `TS_ManorTask.TemplateId` (1..23); `Condition1..3` = **tiến độ hiện tại**
  (nghĩa theo TaskType: 1 = số thợ lười đã đập, 2 = tiến độ nộp item). Điều kiện gốc/đích nằm
  trong `TS_ManorTask.xml`, không lưu DB.
- `IsComplete` = đã đạt điều kiện (client bật nút Nộp/Hoàn thành); `IsReward` = đã nhận thưởng
  hôm nay (client ẩn nút + đếm "Hôm nay đã xong uỷ thác").
- Index là composite `(UserID, TaskDate)` — thoả yêu cầu "index UserID" và phục vụ luôn query
  theo ngày (không cần index riêng chỉ trên UserID).

Đã test thật trên `Db_Tank` (UserID test `999999`): INSERT 2 task → SELECT (đúng field) →
UPDATE → test UNIQUE chặn duplicate → DELETE, sau đó **xoá sạch dữ liệu test (0 dòng)**.
Script chạy 2 lần liên tiếp: lần 2 "already exists - skipped", exit 0.

### File code (server source)

| File | Nội dung |
|---|---|
| `SqlDataProvider\SqlDataProvider.Data\UserManorTaskInfo.cs` | entity `UserManorTaskInfo` (**mới**) |
| `SqlDataProvider\SqlDataProvider.Data\ManorCandidateInfo.cs` | entity `ManorCandidateInfo` cho 81/30 (**mới**) |
| `Bussiness\Bussiness\ManorBussiness.cs` | **chỉ sửa file này** — thêm API + SQL hằng số + core methods |

### API `ManorBussiness` bổ sung — tất cả **static**, giữ pattern cũ

Cùng quy ước: mỗi call tự tạo `ManorBussiness` (mở `Sql_DbObject` theo `AppConfig/conString`)
rồi dispose; lỗi SQL/connection được try/catch + ghi `BaseBussiness.log`, trả `null` / `false` /
list rỗng / `0` — **không ném exception** ra handler.

```csharp
// Task bảng tin (81/18 list, 81/19 nộp, 81/25 tiến độ)
List<UserManorTaskInfo> GetManorTasks(int userId, DateTime date)   // TaskDate = date.Date
bool SaveManorTask(UserManorTaskInfo info)                         // ID>0: UPDATE theo ID; ID<=0: upsert theo (UserID,TaskDate,TaskId), INSERT xong gán info.ID
bool DeleteManorTasks(int userId, DateTime date)                   // xoá toàn bộ task của user trong ngày

// Tra cứu thợ (phase 2.2)
UserManorWorkerInfo GetWorkerByWorkerUserID(int workerUserId)      // TOP 1 ORDER BY ID — "user này đang làm thợ cho ai"
List<UserManorWorkerInfo> GetWorkersByWorkerUserID(int workerUserId) // toàn bộ dòng thợ của user (dữ liệu cũ nhiều chủ)
UserManorWorkerInfo GetWorker(int ownerId, int workerUserId)       // theo unique (OwnerID, WorkerUserID)

// 81/30 WORKER_LIST + helper
List<ManorCandidateInfo> GetManorCandidates(int excludeUserId, string keyword, int limit)
string GetUserNickName(int userId)                                 // NickName trong Sys_Users_Detail
int GetManorWorkerCount(int ownerId, int workType)                 // đếm IsWorking=1 (+ WorkType nếu workType>0)
```

`GetManorCandidates` SQL (đúng spec 81/30):

```sql
SELECT TOP {limit} u.UserID, ISNULL(u.NickName,N'') AS NickName, u.Grade, u.Sex, u.Colors, u.Style,
       ISNULL(w.OwnerID,0) AS OwnerID
FROM dbo.Sys_Users_Detail u
LEFT JOIN dbo.Sys_User_ManorWorker w ON w.WorkerUserID = u.UserID
WHERE u.UserID <> {excludeUserId} AND ({kw}='' OR u.NickName LIKE N'%' + {kw} + N'%')
ORDER BY u.Grade DESC, u.UserID;
```

- `limit <= 0` → mặc định **20**, tối đa **100**; `keyword` cắt tối đa 50 ký tự + **escape `'`**
  (nhân đôi) trước khi nhúng literal (vì `FillSqlDataReader` chỉ nhận SQL string, không nhận
  `SqlParameter`).
- `OwnerID = 0` → ứng viên chưa có chủ (thuê thường, Cỏ Vàng); `OwnerID != 0` → đang làm cho chủ
  khác (client hiện "Mua đứt", trả Xu cho chủ cũ).
- `Sex` trong `ManorCandidateInfo` là **int** (0/1) đúng yêu cầu task (map từ cột `bit` của DB).

### Lưu ý cho dev handler phase 2.2

- `SaveManorTask` = **upsert theo (UserID, TaskDate, TaskId)**: có thể gọi lặp lại mỗi khi thay đổi
  tiến độ mà không tạo trùng. `TaskDate` tự chuẩn hoá `date.Date`; nếu để `DateTime.MinValue`
  (entity mới chưa set) thì tự dùng **hôm nay**. `UpdateDate` tự set `DateTime.Now`.
  Nếu dữ liệu đã có ID > 0 thì handler chỉ cần `GetManorTasks` → sửa field → `SaveManorTask`.
- Khi tạo list task đầu ngày: đặt `TaskId` (instance id) **tăng dần** và unique trong ngày
  (vd 1..N); lúc trả 81/18 gửi đúng `flag = 1` và `count` (xem `docs\FARM2-PROTOCOL.md` mục 2.5).
- `GetManorCandidates` dùng `LEFT JOIN` nên nếu DB có 1 user làm thợ cho **nhiều chủ** (vi phạm quy
  ước "mỗi user chỉ bị 1 chủ thuê") thì ứng viên có thể xuất hiện nhiều dòng — handler nên đảm bảo
  khi thuê/mua đứt thì xoá/ chuyển dòng thợ cũ (`GetWorkersByWorkerUserID` + `DeleteWorker`).
- `GetManorWorkerCount(ownerId, workType)`: `workType > 0` đếm đúng loại (1 Nông Dân, 2 Dân Chài,
  3 Thợ Mỏ, 4 Chăn Nuôi); `workType <= 0` đếm tất cả thợ đang làm.
- Không restart Road.Service trong task này; DLL build Debug nằm ở `bin\Debug\net472`.
  Deploy phase 2.2 phải copy đủ **Game.Server.dll + Bussiness.dll + SqlDataProvider.dll** (như 2.1).

### Build + test

`MSBuild SqlDataProvider.csproj` + `bussiness.csproj` (`/p:Configuration=Debug`) → **0 error**
(chỉ warning có sẵn của project). Runtime smoke test data layer với DB thật (harness tạm, tham
chiếu `Bussiness.dll`/`SqlDataProvider.dll` build Debug + `App.config` trỏ `Db_Tank`):
**46/46 check PASS** — task insert/select/update/upsert/unique (không tạo trùng)/delete/cleanup,
normalize date, validate input, `GetUserNickName`, candidate list (keyword, exclude self, escape
injection, limit cap, `OwnerID` mapping qua LEFT JOIN), worker lookups theo mọi khoá,
`GetManorWorkerCount`. Toàn bộ dữ liệu test (UserID `999999` + 1 dòng thợ tạm) **đã dọn sạch**.

## Nông trại 2.4 (2026-11-08)

- `Sys_User_Manor` thêm cột: `ProtectDate datetime NULL` (hạn tự bảo vệ), `BuyFoodDate datetime NULL`
  (ngày reset lượt mua thức ăn pet).
- Bảng `Sys_User_ManorLog`: ID (identity PK), UserID, LogType, Content nvarchar(200), CreateDate;
  index (UserID, CreateDate DESC).
- Bảng `Sys_User_ManorShopLimit`: ID (identity PK), UserID, GoodsID, MonthKey (yyyyMM), BuyCount,
  UpdateDate; unique (UserID, GoodsID, MonthKey).
- Script: `data/sql/manor_phase24.sql` (idempotent). API: ManorBussiness.AddManorLog/GetManorLogs/
  TrimManorLogs/GetManorShopLimits/AddManorShopLimit/ClearOldManorShopLimits.
