# FARM2-PROTOCOL — Đặc tả protocol Nông trại 2.0 (client Gunny 12.6, packet 81)

Ngày: 2026-11-08 · Người thực hiện: teammate `farm-proto` · Task: `task-1`
Trạng thái: **ĐÃ XONG PHÂN TÍCH CLIENT** — mọi field dưới đây lấy trực tiếp từ client decompile.
File deliverable duy nhất: `docs/FARM2-PROTOCOL.md`. Không sửa source/WebSite/DB.

Quy ước độ tin cậy dùng trong tài liệu:

| Ký hiệu | Nghĩa |
|---|---|
| ✅ | Chắc chắn — đọc trực tiếp từ code client (có file:line) |
| 🟡 | Suy luận logic từ code + data XML, rất có thể đúng nhưng chưa có server gốc đối chiếu |
| ❓ | **CHƯA CHẮC** — client không tiết lộ / có nhiều khả năng; cuối tài liệu có mục liệt kê |

---

## 0. Nguồn dữ liệu & quy ước chung

### 0.1 Nguồn

| Nguồn | Đường dẫn |
|---|---|
| Client decompile chính (theo task) | `data\tmp\swf\decompiled_4\scripts\` (7347 class, FFDec 26.4.0, từ `WebSite\4.swf`) |
| Client decompile tên class **không bị obfuscate** (cùng build, dùng để tra cứu tên dễ đọc) | `data\tmp\swf\flash4_scripts\scripts\` — `farm/FarmManager.as` ≡ `farm/_96$41.as` (diff chỉ khác tên class obfuscate; 882 dòng như nhau) |
| Client decompile build khác (đối chiếu chéo) | `data\_activity_debug\swf44_as\scripts\` — handler 81 giống hệt decompiled_4 (20 listener, cùng thứ tự read) |
| Request XML server (zlib) | `C:\Gunny\Gunny12.6\WebSite\Request\` |
| Server source (đọc tham khảo) | `C:\Gunny\Gunny12.6\Full Source\game.server\...` |
| UI XML đã giải nén | `data\tmp\client_assets\ui\farm.xml` (+ 45 definition trong `farm.ui`), `data\tmp\client_assets\lang\language.txt` |

Class bí danh hay gặp (decompiled_4 ↔ flash4_scripts):

| decompiled_4 | Tên thật |
|---|---|
| `farm\_96$41.as` | `farm.FarmManager` |
| `templateData\_246$11.as` | `templateData.TemplateManager` |
| `quest\_198$16.as` | `quest.TaskManager` |
| `hallIcon\_132$94.as` | `hallIcon.HallIconManager` |
| `trainer\controller\_254$40.as` | `trainer.controller.SystemOpenPromptManager` |

### 0.2 Khung packet 81 (✅)

- Packet code = **81 (0x51)**. Body do server gửi (`GSPacketIn(81, playerId)`) bắt đầu bằng **1 byte subtype** (client đọc `readUnsignedByte()`):
  `SocketManager.as:670-671`: `_loc2_ = int(_loc4_.readUnsignedByte()); dispatchEvent(new PkgEvent(PkgEvent.format(_loc7_,_loc2_),_loc4_));`
- Sau khi đọc subtype, client **giữ nguyên vị trí con trỏ** → mọi field tiếp theo đọc tiếp từ đầu payload sau subtype.
- **Không có listener nào cho subtype 81 mà client không đăng ký** → packet lạ bị bỏ qua im lặng (không crash).
- Danh sách listener 81 trong toàn bộ client chỉ có **17 registration** (20 dòng add/remove — đã grep toàn bộ 7347 class):

| File:line | Subtype |
|---|---|
| `farm\_96$41.as:76-91` | 2, 18, 24, 17, 26, 32, 33, 34, 46, 36, 44, 37, 39, 43 |
| `farm\view\manage\FarmManageView.as:32` | 1 |
| `farm\view\manage\FarmManageHireView.as:34` | 30 |
| `farm\view\fight\FarmFightRankView.as:36` | 40 |

### 0.3 Kiểu dữ liệu trên wire (✅ — `road7th\comm\PackageIn.as`, `PackageOut.as`, server `Game.Base\PacketIn.cs`)

| Kiểu | Byte | Ghi chú |
|---|---|---|
| `byte` | 1 | số nguyên 0..255 |
| `bool` | 1 | 0 = false, 1 = true (`WriteBoolean`) |
| `int` | 4 | big-endian có dấu (`WriteInt`) |
| `UTF` (string) | 2 + N | `[short length][N byte UTF-8]`, length = **số byte**, KHÔNG có null terminator. Client dùng `ByteArray.readUTF()` (AMF). Server: **không dùng `WriteString` legacy** (nó +1 và thêm `\0`) — dùng helper `WriteShort(len)+Write(bytes)` như `RequestFarmInfo.cs` hiện tại hoặc `Road.Service\DataOutput.WriteUTF`. |
| `Date` | 8 | `[short year][byte month 1..12][byte day][byte hour][byte minute][byte second]` (`PackageIn.readDate()`; server `PacketIn.WriteDateTime`). Khi không có giá trị: gửi `DateTime.Now` (client không phân biệt). |
| Header | 20 | gồm code 81; client không dùng `clientId` của packet 81 (trừ chat kênh normal). |

### 0.4 Ghi chú cực quan trọng về “va chạm” protocol cũ/mới

Server source cũ map packet 81 theo **nông trại v1** (`Game.Server.Packets\FarmPackageType.cs`). Client 12.6 dùng **cùng code 81 nhưng nghĩa hoàn toàn khác** cho từng subtype ⇒ bắt buộc phải **xoá/thay toàn bộ handler cũ** (xem bảng mapping mục 7.3). Nếu giữ lại, client sẽ nhận dữ liệu sai/parse lệch hoặc server throw (ví dụ 81/2 = “GrowField” cũ nay là “thuê thợ”; 81/9 = “HelperSwitchField” cũ nay là “cho thợ ăn”; 81/17 = “FarmLandInfo” cũ nay là “area info”; 81/32 = “FarmWater” cũ nay là “update worker”; 81/33 = “PoultryFeed” cũ nay là “slacker query”).

---

## 1. BẢNG TỔNG HỢP 40 SUBTYPE CỦA PACKET 81

Chú thích hướng: **C→S** client gửi; **S→C** server gửi; **C↔S** cùng subtype dùng cho cả hai (request và push).

| Sub | Tên (`farm\data\FarmPackageType.as`) | Hướng | Payload C→S (sau subtype) | Payload S→C (sau subtype) | Client ghi (GameSocketOut) / Đọc (parser) | Trigger UI |
|---:|---|---|---|---|---|---|
| 1 | REQ_INFO | C↔S | *(rỗng)* | `[UTF title][bool isEmploy][Date employProtectDate][int x][bool isProtect][Date protectDate][int x][int ransomMoney][int protectMoney][int skillTotalGrade][int farmExp][int fishExp][int mineExp][int ranchExp][int bossID]{boss...}[int logCount]{[int x][UTF text]}` | GS `9231-9236` / `FarmManageView.as:32,37-89` | Mở **Quản lý nông trại** (listener chỉ sống khi view mở) |
| 2 | EMP_LOYD | C↔S | `[int workerUserID]` | `[bool success][int workerUserID]` + nếu success: `[UTF name][UTF title][int feeling][int energy][int farmExp][int fishExp][int mineExp][int ranchExp][bool isWorking][int workType][Date workBeginDate][Date employProtectDate][int protectPrice][bool sex][UTF colors][UTF style]`; nếu fail: client xoá worker khỏi list | GS `9307-9313` / `_96$41.as:500-533` | Nút **Thuê** / **Mua đứt** ở tab “Tuyển thợ” (`FarmManageHireItemView.as:43-91`); push fail dùng cho **sa thải** (5) |
| 3 | ADD_TIME | C→S | `[int workerUserID][int days]` | → server push **81/32** (worker cập nhật) | GS `9272-9279` / `FarmEmployeeProtectBuyView.as:73-82` | Icon khoá ở dòng thợ → mua bảo vệ hợp đồng N ngày (Xu) |
| 4 | SET_TITLE | C→S | `[int workerUserID][UTF title][bool isBind]` | → **81/32** | GS `9262-9270` / `FarmChangeTitleView.as:71-75` | Nút đổi danh hiệu thợ (Xu, giá `ManorOtherNameMoney`) |
| 5 | FIRE_EMP | C→S | `[int workerUserID]` | → **81/2** success=false (xoá khỏi UI) ± **81/32** | GS `9315-9321` / `FarmManageInfoServantItem.as:80-100` | Nút **Sa thải** ở tab “Thông tin” |
| 6 | FREEDOM | C→S | *(rỗng)* | → **81/1** (hoặc 81/2 fail) ± **81/26** | GS `9323-9328` / `FarmManageInfoView.as:88-119` | Nút **Tự chuộc thân** (Cỏ Vàng = `ransomMoney`) |
| 7 | *(không có)* | — | — | — | — | Không dùng trong Farm 2.0 |
| 8 | ADD_LOCK_TIME | C→S | `[int days]` | → **81/1** (cập nhật protectDate/protectMoney) ± **81/26** | GS `9291-9297` / `FarmProtectBuyView.as:72-82` | Nút **Bảo vệ bản thân** N ngày (Xu = `protectMoney`/ngày) |
| 9 | EAT_FOOD | C→S | `[int workerUserID][int foodTemplateID][int count]` | → **81/32** (energy) + **81/26** (goods giảm) | GS `9281-9289` / `FarmFeedView.as:73-78` | Tab “Thông tin” → icon “nhắc thể lực” → chọn thức ăn → cho ăn |
| 10 | *(không có)* | — | — | — | — | — |
| 11 | PLANT_BEGIN | C→S | `[int areaType][int plantTemplateID]` — `plantTemplateID==0` ⇒ **ngưng trồng/ngưng nuôi/ngưng đào** | → **81/17** (area) + **81/26** (goods/tiền/exp) | GS `9406-9413` / `FarmMenuGoodsItem.as:112-182`, `FarmMenuHoeItem.as:66-110` | Nút **Trồng/Nuôi/Dùng cuốc** hoặc **Đang trồng/Đang nuôi → ngưng** |
| 12 | WORK_BEGIN | C→S | `[int workType][int workerUserID]` (workType = areaType 1..4) | → **81/32** | GS `9245-9252` / `FarmEmployeeListItemView.as:29-49` | Tab thợ → chọn 1 slot trống → chọn thợ → **Làm việc** |
| 13 | WORK_END | C→S | `[int workType][int workerUserID]` | → **81/32** | GS `9380-9387` / `FarmMenuHeadItem.as:60-72` | Tab thợ → nút **Cho nghỉ việc** trên đầu thợ đang làm |
| 14 | UP_PLANT | C→S | `[int upgradeType 1..6]` — 1..4 = nâng cấp khu (FARM/FISH/MINE/RANCH), 5 = nâng **Đất Trồng** (levelTwo của area 1), 6 = mở **Cuốc Chim** (levelTwo của area 3) | → **81/17** (+**81/14** không tồn tại; +**81/26** nếu tiêu item) | GS `9446-9452` / `FarmMenuUpgradeItem.as:48-71`, `FarmMenuHoeItem.as:112-135` | Tab “Nông Trại/Ao Cá/Hầm Mỏ/Bãi Cỏ/Đất Trồng/Cuốc Chim” → nút **Tăng cấp/Mở** |
| 15 | BUY_GOODS | C→S | `[int goodsID (ShopItemInfo.ID của shop 135)][int count][int moneyType]` — moneyType: 1 = Cỏ Vàng (`selfMoney`), 2 = Huy Chương (`DDTMoney`), 3 = Xu (`Money`) | → **81/26** (goods mới + tiền) | GS `9330-9338` / `FarmBuyView.as:129-134` | Popup mua nhanh vật phẩm nông trại (từ menu/kho) |
| 16 | *(không có)* | — | — | — | — | — |
| 17 | REQUEST_FARM_AREA_INFO | **S→C** | — (client **không có** sender 17) | `[int areaType][int level][int levelTwo][bool isPlant][int plantTempId][Date plantDate][int plantRipeDate][int foodTempId][Date foodDate]` | — / `_96$41.as:423-446` | Server push sau 11/14/28/27; client chỉ cập nhật nếu là farm của mình |
| 18 | TASK_BEGIN | C↔S | *(rỗng)* | `[int flag][int count]` + mỗi task `[int id][int templateID][int condition1][int condition2][int condition3][bool isComplete][bool isReward]` | GS `9238-9243` / `_96$41.as:535-564` | Mở **Bảng tin → tab Uỷ thác**, bấm “Mở” task; cũng là packet push khi task thay đổi |
| 19 | TASK_FINISH | C→S | `[int taskInstanceId]` | → **81/18** (list mới, isReward=true) | GS `9299-9305` / `FarmBillboardItemView.as:39-49` | Nút **Hoàn thành/Nộp** trên dòng task |
| 20 | BEAT | C→S | `[int workerUserID]` | → **81/32** | GS `9389-9395` / `FarmWorkerMenu.as:52-62` | Menu chuột phải thợ → **Trói/Đánh** (giảm feeling) |
| 21 | LOVE | C→S | `[int workerUserID]` | → **81/32** | GS `9363-9369` / `FarmWorkerMenu.as:64-74` | Menu chuột phải thợ → **Yêu thương** (tăng feeling; CD `ManorBeatLoveCD`) |
| 22 | GITF | C→S | `[int workerUserID][bool isBind]` | → **81/32** | GS `9397-9404` / `FarmSendGiftView.as:45-49` | Menu chuột phải thợ → **Tặng quà** (Xu `ManorGiftMoney`, +10 feeling) |
| 23 | REQUEST_PLAYER_INFO | *(chết)* | — | — | Không có sender/listener trong build này | ❓ Không dùng |
| 24 | REQUEST_FARM_INFO | C↔S | `[int farmUserID]` | Khối dữ liệu nông trại đầy đủ (mục 2.1) | GS `2533-2539; 9171` (`sendRequestFarmInfo`) / `_96$41.as:333-421` | Bấm **Nông trại** ở sảnh / đổi farm / vào farm bạn / `backFarm()` |
| 25 | SLACKER | C→S | *(rỗng)* | → **81/18** (task) + (khuyến nghị) **81/33** | GS `9439-9444` / `FarmSceneWorker.as:201-217` | Ở **farm bạn**, click thợ đang lười (hammer) để đập |
| 26 | UPDATE_GOODS | **S→C** | — | `[int selfMoney][int selfValue][int selfExp][int goodsCount]` + mỗi goods `[int templateID][int property][int count]` | — / `_96$41.as:477-498` | Push sau **mọi** thay đổi tiền/exp/goods (11/14/15/28/31/9/nhiệm vụ…) |
| 27 | UPDATE_PLANT | C→S | *(rỗng)* | → server tự chọn (khuyến nghị 81/17 ×4 area + 81/26 nếu đổi) | GS `9356-9361` / timer `FarmMainView.as:90-97` (60 giây) | Timer client tự gửi mỗi 60s khi đang ở farm |
| 28 | SPEED_PLANT | C→S | `[int areaType][int foodTemplateID]` — `0` = ngưng cho ăn | → **81/17** (foodTempId/foodDate) + **81/26** (goods giảm) | GS `9371-9378` / `FarmMenuGoodsItem.as:87-170` | Tab “Thức Ăn” của Ao Cá/Bãi Cỏ → **Cho Ăn / Ngưng** |
| 29 | LOG | *(chết)* | — | — | Không có sender/listener | ❓ Log thực tế nằm trong **81/1** response; 29 không dùng |
| 30 | WORKER_LIST | C↔S | `[UTF keyword]` (rỗng = tất cả) | `[int count]` + mỗi ứng viên `[int userID][UTF name][int value][int farmExp][int fishExp][int mineExp][int ranchExp][int bossID][UTF bossName][bool sex][UTF colors][UTF style][int price][Date protectDate][Date employProtectDate]` | GS `9214-9220` / `FarmManageHireView.as:198-229` | Mở tab **Tuyển thợ** / bấm **Tìm** (có/không từ khoá) |
| 31 | SELL_GOODS | C→S | `[int templateID][int count]` | → **81/26** (goods giảm + selfMoney tăng `ReclaimValue*count`) | GS `9222-9229` / `FarmSellGoodsView.as:72-82` | Tab “Kho” → nút **Bán** → nhập số lượng (giá = `ItemTemplateInfo.ReclaimValue`) |
| 32 | UPDATE_WORKER | **S→C** | — | `[int farmUserID][int workerUserID][UTF name][UTF title][int feeling][int energy][int farmExp][int fishExp][int mineExp][int ranchExp][bool isWorking][int workType][Date workBeginDate][Date employProtectDate][int protectPrice]` **KHÔNG có sex/colors/style** | — / `_96$41.as:448-475` | Push sau 3/4/9/12/13/20/21/22 (và bất kỳ thay đổi worker) |
| 33 | SLACKER_QUERY | C↔S | `[int count][int userID...]` (danh sách bạn bè) | `[int count][int userID...]` (những userID đang có thợ lười) | GS `9171-9184` / `_96$41.as:300-324` | `gotoFarm()` → `initFrined()` (`_96$41.as:609-631`) — tự gửi mỗi lần vào/đổi farm |
| 34 | PET_FOOD_QUERY | C↔S | *(rỗng)* | `[int buyExpRemainNum]` | GS `9186-9191` / `_96$41.as:746-750` | `FarmView` khởi tạo (`FarmView.as:49` — mỗi lần vào farm) |
| 36 | FIGHT_SCENE_INFO | C↔S | *(rỗng)* | scene payload (mục 2.11) | GS `9415-9420` / `_96$41.as:205-257` | Vào vùng chiến đấu (`FarmSceneMap.as:733-736`) — **phase sau** |
| 37 | MOVE | C↔S | `[int x][int y]` | `[int userID][int x][int y]` | GS `9340-9347` / `_96$41.as:115-139` | Click di chuyển khi farm đang fight — **phase sau** |
| 38 | CHAT | C→S | `[byte channel=38][UTF msg]` | *(server relay bằng packet 19 chat thường, channel 38 — 🟡)* | GS `9422-9429` / `ChatManager.as:457-464` | Gõ chat trong state farm — **phase sau** |
| 39 | EXIT_FIGHT_SCENE | C↔S | *(rỗng)* | `[int farmUserID][int userID]` | GS `9349-9354` / `_96$41.as:104-113` | Rời state farm khi đang fight (`FarmStateView.as:45-56`) |
| 40 | UPDATE_RANK_LIST | C↔S | *(rỗng)* | `[int farmUserID][int myScore][int count]` + mỗi dòng `[UTF name][int score]` | GS `9193-9198` / `FarmFightRankView.as:78-111` | Mở/refresh bảng xếp hạng hoạt động |
| 41 | CHECK_STATE | *(chết)* | *(rỗng)* | — | GS `9207-9212` — **không chỗ nào gọi** | ❓ Không dùng |
| 42 | START_FIGHT | C→S | `[int masterID]` | → thường kèm **81/43** (xoá quái) + **81/40** (rank) | GS `9431-9437` / `FarmSceneMap.as:216-225` | Click vào quái (NPC) trong farm đang fight |
| 43 | REMOVE_MONSTER | **S→C** | — | `[int farmUserID][int masterID]` | — / `_96$41.as:93-102` | Khi quái bị hạ / biến mất |
| 44 | UPDATE_SCENE | **S→C** | — | scene payload (mục 2.12) | — / `_96$41.as:141-203` | Push khi có người/quái vào-rời/đổi state |
| 45 | FIGHT_OPEN | C→S | *(rỗng)* | → **81/46** (hoặc 81/36) | GS `9200-9205` / `FarmBillboardActivityView.as:26-38` | Bảng tin → tab **Hoạt động** → “Mở hoạt động Sâu Bọ Tấn Công” |
| 46 | FIGHT_ACTIVITY_OPEN | **S→C** | — | `[bool open][int count]` + mỗi farm `[int farmUserID][Date fightStartTime][Date fightEndTime][bool isFight]` | — / `_96$41.as:259-298` | Server push khi hoạt động mở/đóng; nếu farm bạn đang fight → hiện popup mời + cho vào |

---

## 2. CHI TIẾT TỪNG RESPONSE (thứ tự field CHÍNH XÁC)

> Nguyên tắc: đọc sai 1 field ⇒ lệch toàn bộ phần sau / crash. Các mục dưới đây ghi đúng thứ tự `pkg.read*()` trong client, kèm code minh hoạ.

### 2.1 `81/24` — REQUEST_FARM_INFO (response) ✅

Parser: `farm\_96$41.as:333-421` (`__onRequestFarmInfo`).

```as3
var _loc13_:int = param1.pkg.readInt();                    // farmUserID
var _loc11_:FarmInfo = this._model.getFarmInfoById(_loc13_) || new FarmInfo();
var _loc16_:String = param1.pkg.readUTF();                 // userName  (rỗng -> dùng tên lưu từ gotoFarm)
_loc11_.userName = _loc16_ != "" ? _loc16_ : enterFarmUserName;
_loc11_.userID = _loc13_;
_loc11_.exp = param1.pkg.readInt();                         // farm exp
var _loc3_:int = param1.pkg.readInt();                      // landLevel  -> levelTwo của object type 1
var _loc6_:int = param1.pkg.readInt();                      // mineLevel  -> levelTwo của object type 3
_loc11_.buffRate = param1.pkg.readInt();
_loc11_.buffDate = param1.pkg.readDate();
var _loc7_:int = param1.pkg.readInt();                      // objectCount
while (_loc12_ < _loc7_) {
   _loc8_ = pkg.readInt();                                  //   object type (1..6)
   _loc5_.level = pkg.readInt();
   // (type==1 -> levelTwo=landLevel; type==3 -> levelTwo=mineLevel)
   _loc5_.isPlant = pkg.readBoolean();
   _loc5_.plantTempId = pkg.readInt();
   _loc5_.plantDate = pkg.readDate();
   _loc5_.plantRipeDate = pkg.readInt();
   _loc5_.foodTempId = pkg.readInt();
   _loc5_.foodDate = pkg.readDate();
}
_loc11_.clearWorkers();
var _loc2_:int = pkg.readInt();                             // workerCount
while (...) {                                               // mỗi worker:
   _loc10_ = pkg.readInt();                                  //   userID
   _loc17_.name = pkg.readUTF();
   _loc17_.title = pkg.readUTF();
   _loc17_.feeling = pkg.readInt();
   _loc17_.energy = pkg.readInt();
   _loc17_.farmExp = pkg.readInt();
   _loc17_.fishExp = pkg.readInt();
   _loc17_.mineExp = pkg.readInt();
   _loc17_.ranchExp = pkg.readInt();
   _loc17_.isWorking = pkg.readBoolean();
   _loc17_.workType = pkg.readInt();
   _loc17_.workBeginDate = pkg.readDate();
   _loc17_.employProtectDate = pkg.readDate();
   _loc17_.protectPrice = pkg.readInt();
   _loc17_.sex = pkg.readBoolean();
   _loc17_.colors = pkg.readUTF();
   _loc17_.style = pkg.readUTF();
}
var _loc15_:int = pkg.readInt();                            // goodsCount
while (...) {                                               // mỗi goods:
   _loc18_ = pkg.readInt();                                  //   templateID
   _loc4_.property = pkg.readInt();                          //   property (= bitmask túi, xem 3.2)
   _loc4_.count = pkg.readInt();                             //   count
}
this._model.addFarmInfo(_loc13_, _loc11_);
this.dispatchEvent(new FarmEvent("updatefarm", _loc11_));
StateManager.setState("farm");                              // <<< CHỈ khi nhận packet này client mới vào state farm
```

**S→C payload (đúng thứ tự):**

| # | Kiểu | Tên/ý nghĩa | Ghi chú |
|---:|---|---|---|
| 1 | int | farmUserID (chủ farm đang xem) | phải khớp request |
| 2 | UTF | userName | rỗng ⇒ client dùng tên đã lưu (từ friend list/param gotoFarm) |
| 3 | int | exp (exp nông trại **của chủ farm**) | dùng để tính “Trang Trại LvN” |
| 4 | int | landLevel (**levelTwo** của area type 1) = cấp Đất Trồng | TS_ManorPlant UpgradeType 5 |
| 5 | int | mineLevel (**levelTwo** của area type 3) = cấp Cuốc/Hầm mỏ | TS_ManorPlant UpgradeType 6 |
| 6 | int | buffRate (%) | 0 nếu không buff |
| 7 | Date | buffDate (thời điểm hết buff) | client: `buffDate.time > now` thì hiện tooltip “Tốc độ sản xuất tăng” |
| 8 | int | objectCount | nên = **4** (type 1..4) hoặc 6 (1..6) — client chỉ dùng 1..4 |
| 9 | × objectCount | — | mỗi object: `int type`, `int level`, `bool isPlant`, `int plantTempId`, `Date plantDate`, `int plantRipeDate`, `int foodTempId`, `Date foodDate` — **object KHÔNG có levelTwo** (levelTwo lấy từ #4/#5) |
| 10 | int | workerCount | số thợ của farm |
| 11 | × workerCount | — | `int userID`, `UTF name`, `UTF title`, `int feeling`, `int energy`, `int farmExp`, `int fishExp`, `int mineExp`, `int ranchExp`, `bool isWorking`, `int workType`, `Date workBeginDate`, `Date employProtectDate`, `int protectPrice`, `bool sex`, `UTF colors`, `UTF style` |
| 12 | int | goodsCount | số loại goods có trong kho farm |
| 13 | × goodsCount | — | `int templateID`, `int property`, `int count` |

Ghi chú:
- Khi `farmUserID != self`, client vẫn **ghi goods vào model chung của mình** (dòng 411-415). Vì vậy khi xem farm bạn nên gửi `goodsCount = 0` để tránh ghi đè kho của người xem. 🟡
- `FarmInfo` model chỉ giữ 1 entry/`farmUserID`; mở lại farm cùng ID sẽ update tại chỗ.
- `plantRipeDate` là **số nguyên (giây)**, không phải timestamp (xem 3.10).
- `StateManager.setState("farm")` chỉ xảy ra ở đây ⇒ thiếu 81/24 = “không mở được nông trại” (đúng triệu chứng lỗi phase 1).

Hex mẫu tối thiểu (farm self userID=1, tên `test1`, exp 0, land=1, mine=1, buffRate=0, buffDate 2026-11-08 12:00:00, 4 area Lv1 trống, 0 thợ, 0 goods):

```
18                                   ; subtype 24
00000001                             ; farmUserID
0005 7465737431                      ; UTF "test1"
00000000 00000001 00000001 00000000  ; exp, landLevel, mineLevel, buffRate
07EA 0B 08 0C 00 00                  ; buffDate (2026-11-08 12:00:00)
00000004                             ; objectCount
 00000001 00000001 00 00000000 07EA0B080C0000 00000000 00000000 07EA0B080C0000
 00000002 00000001 00 00000000 07EA0B080C0000 00000000 00000000 07EA0B080C0000
 00000003 00000001 00 00000000 07EA0B080C0000 00000000 00000000 07EA0B080C0000
 00000004 00000001 00 00000000 07EA0B080C0000 00000000 00000000 07EA0B080C0000
00000000                             ; workerCount
00000000                             ; goodsCount
```

### 2.2 `81/17` — REQUEST_FARM_AREA_INFO (S→C push) ✅

Parser: `farm\_96$41.as:423-446` (`__onRequestFarmAreaInfo`) — **client không bao giờ gửi subtype 17**; đây là push của server.

```as3
var _loc4_:FarmInfo = this._model.selfFarmInfo;
if(_loc4_) {
   _loc3_ = pkg.readInt();                 // areaType
   _loc2_ = _loc4_.getObjectInfo(_loc3_) || new FarmObjectUpgradeInfo();
   _loc2_.type = _loc3_;
   _loc2_.level = pkg.readInt();
   _loc2_.levelTwo = pkg.readInt();
   _loc2_.isPlant = pkg.readBoolean();
   _loc2_.plantTempId = pkg.readInt();
   _loc2_.plantDate = pkg.readDate();
   _loc2_.plantRipeDate = pkg.readInt();
   _loc2_.foodTempId = pkg.readInt();
   _loc2_.foodDate = pkg.readDate();
   if(_loc4_.isSelf) dispatchEvent(new FarmEvent("updatefarmarea", _loc2_));
}
```

Payload: `[int areaType][int level][int levelTwo][bool isPlant][int plantTempId][Date plantDate][int plantRipeDate][int foodTempId][Date foodDate]`

- Chỉ áp dụng khi farm hiện tại là **farm của chính mình** (`selfFarmInfo`); farm bạn bỏ qua.
- `levelTwo` chỉ có nghĩa với areaType 1 (Đất Trồng) và 3 (Cuốc/Hầm mỏ); các type khác gửi 0.
- Đây là packet chính để cập nhật trạng thái trồng/cho ăn sau 11/14/28.

Hex mẫu: trồng cỏ (332139 = 0x0005116B) ở area 1, level 1, levelTwo 1, ripe 1800s:

```
11 00000001 00000001 00000001 01 0005116B 07EA0B080C0000 00000708 00000000 07EA0B080C0000
```

### 2.3 `81/26` — UPDATE_GOODS (S→C push) ✅

Parser: `farm\_96$41.as:477-498` (`__onUpdateGoods`).

```as3
_model.selfMoney = pkg.readInt();     // Cỏ Vàng (tiền farm)
_model.selfValue = pkg.readInt();     // giá trị farm — client chỉ lưu, KHÔNG hiển thị ở đâu
_model.selfExp   = pkg.readInt();     // exp farm (level Trang Trại)
var _loc3_:int = pkg.readInt();       // goodsCount
while (...) {
   _loc5_ = pkg.readInt();            //   templateID
   _loc4_.property = pkg.readInt();   //   property
   _loc4_.count = pkg.readInt();      //   count
}
this.dispatchEvent(new FarmEvent("updateselfinfo"));
```

- Client **không xoá** goods không có trong packet; entry cũ giữ count cũ. Vì vậy sau khi bán/tiêu hết, server **phải gửi lại item đó với count mới (0)** — hoặc đơn giản nhất: **luôn gửi toàn bộ danh sách goods hiện có** (khuyến nghị). 🟡
- `property` phải là **bitmask túi** (giống `Property1` trong TemplateManorList) để client phân loại tab Kho (xem 3.2).
- `selfMoney` = “Cỏ Vàng”, dùng cho: bán hàng, thuê thợ, tự chuộc thân, mua shop farm (moneyType 1), mua shop 139.
- `selfExp` quyết định **level Trang Trại** qua bảng exp (Types 109) — xem 3.10.
- `selfValue` ❓ chưa rõ nghĩa (không nơi nào dùng); khuyến nghị gửi 0 hoặc = selfMoney.

Hex mẫu: money 1000, value 0, exp 0, 1 goods (332139, property 1, count 5):

```
1A 000003E8 00000000 00000000 00000001 0005116B 00000001 00000005
```

### 2.4 `81/2` — EMP_LOYD (thuê thợ / xoá thợ) ✅

Parser: `farm\_96$41.as:500-533` (`__onEmpLoyd`).

```as3
var _loc3_:Boolean = pkg.readBoolean();          // success
var _loc5_:FarmInfo = this._model.selfFarmInfo;
var _loc4_:int = pkg.readInt();                 // workerUserID
if(_loc3_) {
   // đọc đủ worker (GIỐNG 81/24): name UTF, title UTF, feeling, energy,
   // farmExp, fishExp, mineExp, ranchExp, isWorking bool, workType int,
   // workBeginDate Date, employProtectDate Date, protectPrice int,
   // sex bool, colors UTF, style UTF
   _loc5_.addWorkerInfo(_loc2_);
} else {
   _loc5_.removeWorkerInfo(_loc4_);
}
dispatchEvent(new FarmEvent("updateemplory", _loc4_));
```

- Dùng cho **request 81/2** (thuê) → response success=true; và làm **response cho 81/5** (sa thải) / 81/6 / mua đứt → success=false để client xoá dòng thợ.
- `_loc5_ = selfFarmInfo` — nếu null sẽ NPE, nên server chỉ gửi sau khi client đã có 81/24.

### 2.5 `81/18` — TASK_BEGIN (list nhiệm vụ) ✅

Parser: `farm\_96$41.as:535-564` (`__onTaskBegin`).

```as3
var _loc5_:int = pkg.readInt();          // flag: != 0 -> dispatch "updatequest" (UI refresh); 0 -> update model im lặng
var _loc2_:int = pkg.readInt();          // count; <=0 -> clearQuestList()
while (...) {
   _loc3_.id = pkg.readInt();            // instance id (dùng cho 81/19)
   _loc3_.templateID = pkg.readInt();    // TS_ManorTask.TemplateId (1..23)
   _loc3_.condition1 = pkg.readInt();
   _loc3_.condition2 = pkg.readInt();
   _loc3_.condition3 = pkg.readInt();
   _loc3_.isComplete = pkg.readBoolean();
   _loc3_.isReward   = pkg.readBoolean();
}
if(_loc5_) dispatchEvent(new FarmEvent("updatequest"));
```

- **Phải gửi flag = 1**, nếu không UI bảng tin không refresh (dù model đã đổi).
- `isReward=true` = task đã nhận thưởng hôm nay (client đếm `questListFightCount` để hiện “Hôm nay đã xong uỷ thác: x/y” và ẩn nút).
- `count = 0` ⇒ client xoá sạch danh sách task.
- Server gửi packet này để trả lời **81/18** (request) và push sau **81/19**, **81/25**, harvest… 🟡

Hex mẫu: 1 task id=1, template=1, cond1=3, cond2=0, cond3=5, chưa xong:

```
12 00000001 00000001 00000001 00000001 00000003 00000000 00000005 00 00
```

### 2.6 `81/32` — UPDATE_WORKER (S→C push) ✅

Parser: `farm\_96$41.as:448-475` (`__onUpdateWorker`).

```as3
var _loc2_:int = pkg.readInt();          // farmUserID (thường = self)
var _loc5_:FarmInfo = model.getFarmInfoById(_loc2_) || new FarmInfo();
var _loc4_:int = pkg.readInt();          // workerUserID
// name UTF, title UTF, feeling, energy, farmExp, fishExp, mineExp, ranchExp,
// isWorking bool, workType int, workBeginDate Date, employProtectDate Date, protectPrice int
dispatchEvent(new FarmEvent("updateworker", _loc4_));
```

- **KHÔNG có sex/colors/style** trong packet này (khác 81/24 và 81/2). Nếu cần cập nhật hiển thị đầu thợ, dùng 81/24 (reload) hoặc gửi 81/2 success.
- Push sau: 3 (bảo vệ thợ), 4 (đổi danh hiệu), 9 (cho ăn), 12/13 (làm/nghỉ), 20/21/22 (đánh/yêu/quà), hoặc bất kỳ thay đổi feeling/energy/exp.

### 2.7 `81/33` — SLACKER_QUERY (response) ✅

Parser: `farm\_96$41.as:300-324` (`__onSlackerQuery`).

```as3
var _loc4_:int = pkg.readInt();          // count
while (...) { _loc2_ = pkg.readInt(); /* userID có thợ lười */ ... _loc3_.slackerCount = 1; }
dispatchEvent(new FarmEvent("updatefriend"));
```

- Payload: `[int count][int userID...]`.
- Client đánh dấu `slackerCount = 1` cho bạn bè có trong danh sách. Ở farm bạn, `FarmMainView.getRandomList()` chọn **1 thợ ngẫu nhiên** trong số thợ đang làm việc để hiện “lười” (hiện búa) nếu `slackerCount > 0` (giá trị client chỉ tăng lên 1, không đếm số).
- Đề xuất: trả danh sách bạn bè (trong danh sách client gửi) đang có ≥1 thợ “slacker” (server tự định nghĩa, ví dụ feeling thấp / random theo `ManorWorkEventRandom`). 🟡

### 2.8 `81/34` — PET_FOOD_QUERY (response) + `68/19` ✅

Parser: `farm\_96$41.as:746-750` — cùng function đăng ký cho **cả 81/34 và 68/19**:

```as3
buyExpRemainNum = param1.pkg.readInt();
dispatchEvent(new FarmEvent("updatecount", buyExpRemainNum));
```

- Payload chỉ 1 int: `[int buyExpRemainNum]`.
- Client dùng:
  - `FarmView`: `buyCountLabel.text = remain` (FarmView.as:100-103).
  - `FarmBuyExpFrame.getCurrentMoney()`: `index = 31 - remain`; tìm `PetExpItemPrice.Count == index` → giá Xu (`Money`); nếu remain ≤ 0 thì chặn mua.
- Mua thực tế: client gửi **68/19** `[bool isBind]`; server (đã có handler `BuyPetExpItem.cs`) trừ tiền, cộng item **334102** (Thức Ăn Trung) x `ItemCount`, `remain--`, rồi trả **68/19** `[int remain]` (client nghe cả 2 subtype).
- ⚠️ **Mâu thuẫn cần chốt**: client 12.6 tính `Count = 31 - remain`, còn handler cũ `BuyPetExpItem.cs:68-93` dùng `RealMoney(remain)` (≈ `Count = 21 - remain` với remain tối đa 20 — theo `PlayerFarmInventory` set 20). Để khớp client: **giá phải là `31 - remain`**, tức remain khởi tạo nên là **30** (mua lần 1 giá Count=1=80 Xu) giảm dần; khi còn 11..30 thì bảng giá 1..20 phủ hết 20 lượt mua. Nếu giữ remain=20 như phase 1 thì giá hiển thị sẽ là Count=11 (480 Xu). ❓ (xem 6.7)

### 2.9 `81/1` — REQ_INFO (response) ✅

Parser: `farm\view\manage\FarmManageView.as:37-89` (`__onReqInfo`) — listener chỉ sống khi bảng **Quản lý** đang mở.

```as3
var _loc3_:FarmPlayerInfo = new FarmPlayerInfo();
_loc3_.title = pkg.readUTF();                     // danh hiệu của mình (khi làm thợ / do chủ đặt)
_loc3_.isEmploy = pkg.readBoolean();              // mình đang bị người khác thuê?
_loc3_.employProtectDate = pkg.readDate();        // hết hạn bảo vệ hợp đồng lao động của mình
pkg.readInt();                                    // BỎ QUA — ý nghĩa ❓ (gợi ý: employerID / level)
_loc3_.isProtect = pkg.readBoolean();             // mình đang tự bảo vệ (chống bị thuê)?
_loc3_.protectDate = pkg.readDate();              // hết hạn tự bảo vệ
pkg.readInt();                                    // BỎ QUA — ý nghĩa ❓ (gợi ý: protectorID / days)
_loc3_.ransomMoney = pkg.readInt();               // giá tự chuộc thân (Cỏ Vàng)
_loc3_.protectMoney = pkg.readInt();              // giá bảo vệ bản thân / ngày (Xu)
_loc3_.skillTotalGrade = pkg.readInt();           // tổng cấp kỹ năng thợ (110..113) — hiện ở BXH “Lao động”
_loc3_.farmExp = pkg.readInt();                   // exp nghề nông của mình (khi làm thợ)
_loc3_.fishExp = pkg.readInt();
_loc3_.mineExp = pkg.readInt();
_loc3_.ranchExp = pkg.readInt();
_loc3_.bossID = pkg.readInt();                    // ID chủ (nếu isEmploy)
if (_loc3_.bossID != 0) {
   bossName UTF, bossTitle UTF, bossExp int, bossValue int, bossMoney int,
   bossSex bool, bossColor UTF, bossStyle UTF
}
var _loc4_:int = pkg.readInt();                   // logCount
while (...) { pkg.readInt(); /* bỏ qua */ var s:String = pkg.readUTF(); /* nối bằng "\n------------\n" */ }
infoView.updateLog(...); infoView.updateSelfInfo(...); rankView.laborView.updateSelfInfo(...);
```

- Trả lời **81/1 request** (client gửi body rỗng khi mở Quản lý) và có thể dùng làm bản cập nhật trạng thái lao động của mình.
- 2 int bị bỏ qua: gửi `0` là an toàn (client không dùng).
- `boss*` = thông tin **chủ thuê mình** (hiển thị trong tab Thông tin + BXH Lao động).
- `log*`: mảng log hoạt động; mỗi dòng `[int ?][UTF text]`. `int` bị bỏ qua (có thể là loại/thời gian) — **gửi 0**. Nội dung text hiển thị nối bằng `\n------------\n` (client tự nối).

Hex mẫu tối thiểu (không có gì đặc biệt):

```
01 0000 00 07EA0B080C0000 00000000 00 07EA0B080C0000 00000000
00000000 00000000 00000000 00000000 00000000 00000000 00000000 00000000 00000000 00000000
; title="" isEmploy=false date x=0 isProtect=false date x=0 ransom=0 protectMoney=0
; skillTotalGrade=0 farmExp=0 fishExp=0 mineExp=0 ranchExp=0 bossID=0 logCount=0
```

### 2.10 `81/30` — WORKER_LIST (response tuyển thợ) ✅

Parser: `farm\view\manage\FarmManageHireView.as:198-229` (`__onUpdateWorkerList`).

```as3
var _loc2_:int = pkg.readInt();               // count
while (...) {
   _loc3_.userid = pkg.readInt();
   _loc3_.name   = pkg.readUTF();
   _loc3_.value  = pkg.readInt();              // điểm/giá trị (client không dùng)
   _loc3_.farmExp = pkg.readInt();
   _loc3_.fishExp = pkg.readInt();
   _loc3_.mineExp = pkg.readInt();
   _loc3_.ranchExp = pkg.readInt();
   _loc3_.bossID = pkg.readInt();              // 0 = chưa có chủ; !=0 = đang làm cho ai (phải mua đứt)
   _loc3_.bossName = pkg.readUTF();
   _loc3_.sex = pkg.readBoolean();
   _loc3_.colors = pkg.readUTF();
   _loc3_.style = pkg.readUTF();
   _loc3_.price = pkg.readInt();               // giá thuê (Cỏ Vàng) hoặc giá mua đứt (Xu) — xem 4.13
   _loc3_.protectDate = pkg.readDate();        // thời gian tự bảo vệ của ứng viên (chặn bị thuê)
   _loc3_.employProtectDate = pkg.readDate();  // thời gian bảo vệ hợp đồng của ứng viên (chặn mua đứt)
}
```

- Request 81/30: `[UTF keyword]` (chuỗi rỗng = tất cả).
- `price` dùng cho **cả 2 nút**: nếu ứng viên `bossID==0` → “Thuê” trả `price` **Cỏ Vàng**; nếu `bossID!=0` → “Mua đứt” trả `price` **Xu** cho `bossName` (client kiểm tra bằng `CheckMoneyUtils`). Gợi ý server: tính theo `ManorEmploryMoneys` (config) cho thuê; mua đứt theo cấu hình riêng. ❓

### 2.11 `81/36` — FIGHT_SCENE_INFO (response) ✅ (phase sau)

Parser: `farm\_96$41.as:205-257` (`__onFightSceneInfo`).

```as3
var _loc3_:int = pkg.readInt();                 // farmUserID
var _loc12_:FarmInfo = model.getFarmInfoById(_loc3_) || new FarmInfo();
_loc12_.getPlayers().clear();
var _loc9_:int = pkg.readInt();                 // playerCount
while (...) {
   id int, nick UTF, grade int, x int, y int, state int,
   sex bool, style UTF, colors UTF, isVip bool, vipLevel int
}
_loc12_.getMasters().clear();
var _loc7_:int = pkg.readInt();                 // masterCount
while (...) { id int, npcType int, x int, y int, state int }
dispatchEvent(new FarmEvent("initFight"));
```

- `state` của player: **2 = đang đánh** (hiện hiệu ứng fight), khác 2 = bình thường.
- `state` của master (quái): 1 = click được, 2 = đang trong trận (không click).
- Request 81/36 body rỗng (client gửi khi vào farm đang fight: `FarmSceneMap.as:733-736`).

### 2.12 `81/44` — UPDATE_SCENE (S→C push) ✅ (phase sau)

Parser: `farm\_96$41.as:141-203` (`__onUpdateScene`). Payload **cùng layout** với 81/36: `[int farmUserID][int playerCount]{id,nick,grade,x,y,state,sex,style,colors,isVip,vipLevel}[int masterCount]{id,npcType,x,y,state}`.

Khác 36: 44 dùng để **update** (chỉ thêm player/master mới nếu chưa có, không clear), và nếu `id == Self.ID` thì set `fightPoint` + dispatch “updateSelfPos”.

### 2.13 Các response/notification phase sau còn lại ✅ (ngắn)

| Sub | Payload S→C | Ghi chú |
|---:|---|---|
| 37 | `[int userID][int x][int y]` | broadcast vị trí khi fight (`_96$41.as:115-139`) |
| 39 | `[int farmUserID][int userID]` | xoá player khỏi scene (`_96$41.as:104-113`) |
| 40 | `[int farmUserID][int myScore][int count]{[UTF name][int score]}` | chỉ áp dụng nếu `farmUserID == currentFarmInfo.userID`; client sort score giảm (`FarmFightRankView.as:78-111`) |
| 43 | `[int farmUserID][int masterID]` | xoá quái (`_96$41.as:93-102`) |
| 46 | `[bool open][int count]{[int farmUserID][Date fightStartTime][Date fightEndTime][bool isFight]}` | `open` client **không dùng**; với mỗi farm: nếu `isFight && farmUserID != self` → hiện popup mời (khi ở sảnh) hoặc auto `backFarm()` nếu đang xem farm đó (`_96$41.as:259-298`) |

---

## 3. NGỮ NGHĨA DỮ LIỆU / ECONOMY (các XML server)

### 3.1 Client load XML khi vào state farm (✅)

`ddt\states\StateCreater.as:525-531` (case `"farm"`):

```as3
AssetModuleLoader.addRequestLoader(LoaderCreate.Instance.creatFarmQuest());            // TS_ManorTask.xml
AssetModuleLoader.addRequestLoader(LoaderCreate.Instance.creatFarmTempLoader());      // TemplateManorList.xml
AssetModuleLoader.addRequestLoader(LoaderCreate.Instance.creatFarmObject());          // TS_ManorPlant.xml
AssetModuleLoader.addRequestLoader(LoaderCreate.Instance.creatSuperPetFoodPriceList()); // PetExpItemPrice.xml
AssetModuleLoader.addRequestLoader(LoaderCreate.Instance.creatFarmManorWealthTempLoader(), true); // ManorWealth.xml
AssetModuleLoader.addRequestLoader(LoaderCreate.Instance.creatFarmManorAbilityTempLoader(), true); // ManorAbility.xml
```

`ddt\loader\LoaderCreate.as:2011-2051` — loader type **5** = XML nén (zlib) từ `WebSite\Request\`; `ManorWealth.xml`/`ManorAbility.xml` được load “optional” (`true`) nên thiếu file không chặn vào farm.

`TS_UpgradeTemplate.xml` load riêng bởi `LoaderCreate.createTemplateUpGradeTemplateLoader()` (`LoaderCreate.as:874-879`) → analyzer `TemplateUpGradeAnalyz` → `templateData\_246$11.ins` (TemplateManager), dùng cho **Types 109-113**.

Định dạng chung của cả 7 file (✅ — kiểm chứng bằng giải nén zlib):

```xml
<Result value="true" message="Success!">
  <Item Attr="..." ... />
  ...
</Result>
```
- `value="true"` (lowercase) mới được client chấp nhận (analyzer check `_loc2_.@value == "true"`), nếu không → `message` hiện lỗi.
- Các attribute đọc bằng `ObjectUtils.copyPorpertiesByXML` = **copy attribute trùng tên property của VO**, thứ tự không quan trọng, attribute lạ bị bỏ qua. Riêng `FarmGoodsTempAnalyzer` chỉ đọc `TemplateID` + `Property1`.
- File trên đĩa `WebSite\Request\*.xml` là **zlib** (đã xác nhận: `templatemanorlist.xml` 4.850B → 68.644B; `ts_manorplant.xml` 824B → 10.787B; `TS_UpgradeTemplate.xml` 8.379B → 154.303B; `manorability.xml` 65B → 61B rỗng).
- Nếu server muốn sinh XML động: phải trả đúng format + nén zlib như hiện tại (loader type 5). 🟡

### 3.2 `TemplateManorList.xml` — danh mục vật phẩm nông trại (✅)

- 82 `Item`, đều `CategoryID="87"` (category nông sản), có đủ trường item template chuẩn (`TemplateID, Name, Description, Pic, Quality, MaxCount, ReclaimType, ReclaimValue, ...`).
- Analyzer: `farm\analyzer\FarmGoodsTempAnalyzer.as` — chỉ lấy **`TemplateID`** và **`Property1`**:

```as3
_list.push({ id: int(@TemplateID), property: int(@Property1) });   // FarmGoodsTempAnalyzer.as:29-32
```

⇒ `Property1` = **bitmask loại túi** (khớp `FarmAreaType`), client lọc tab Kho/menu bằng `(property >> bagIndex) & 1` (`FarmAreaType.belongToBag`, `farm\data\FarmAreaType.as:47-50`):

| bit | bagIndex | Hằng số | Ý nghĩa |
|---:|---:|---|---|
| 1<<0 | 0 | FARM_SEEDS | Hạt giống cây trồng (tab “Cây Trồng”) |
| 1<<1 | 1 | FARM_STORE | Kho nông sản farm + vật liệu nâng cấp farm |
| 1<<2 | 2 | RANCH_SEEDS | Thú con (tab “Thú Con”) |
| 1<<3 | 3 | RANCH_FODDER | Thức ăn bãi cỏ (lúa, đậu…) |
| 1<<4 | 4 | RANCH_STORE | Kho bãi cỏ (thú lớn + vật liệu) |
| 1<<5 | 5 | FISH_SEEDS | Cá con |
| 1<<6 | 6 | FISH_FODDER | Thức ăn cá |
| 1<<7 | 7 | FISH_STORE | Kho cá (cá lớn + vật liệu) |
| 1<<8 | 8 | MINE_STORE | Kho mỏ (quặng + vật liệu) |
| 1<<9 | 9 | MINE_HOE | Cuốc/đạo cụ đào mỏ |
| 1<<10 | 10 | FEED | Thức ăn cho **thợ** (Cỏ Dại, Chocolate…) |

- `Property2..Property8` của TemplateManorList mang nghĩa **riêng theo nhóm** (server dùng để tính kinh tế; client 12.6 **không đọc** các field này trong package farm, chỉ dùng `Name/Pic/Description/ReclaimValue` + một vài property của item template cho UI tooltip). 🟡 Bảng nghĩa suy ra (đối chiếu `Description` của item trong `TemplateAlllist.xml`):

| Nhóm item (theo P1) | P2 | P3 | P4 | P5 | P6 | P7 | P8 |
|---|---|---|---|---|---|---|---|
| Hạt giống **cây trồng** farm (P1 bit 0) | **số lượng sản phẩm** mỗi lần thu | **thời gian chín (phút)** | **TemplateID sản phẩm** | **cấp Nông Trại tối thiểu** | **exp farm** mỗi lần thu | **exp thợ** mỗi lần thu | **item đánh dấu cấp** (334009… = “Nông Trại LvN”) |
| Giống **thú/cá con** (P1 bit 2,5) | **số lượng sản phẩm** khi trưởng thành | **số điểm trưởng thành cần** (không phải phút!) | **TemplateID con trưởng thành** | **cấp Bãi Cỏ/Ao Cá tối thiểu** | exp farm | exp thợ | item đánh dấu cấp (334036.. = “Bãi Cỏ LvN”, 334018.. = “Ao Cá LvN”) |
| Cuốc mỏ (P1 bit 9) | 0 | **thời gian khai thác (phút)** | (trùng TemplateID — output suy ra theo cấp cuốc ❓) | **cấp Hầm Mỏ tối thiểu** | exp farm | exp thợ | item đánh dấu cấp Hầm Mỏ (334027..334031) |
| Sản phẩm farm/ranch/fish (Cỏ Tươi, Táo, Kiến Lửa lớn…) (P1 bit 1/3/4/7/…) | 0 | 0 | **điểm trưởng thành/no** cộng thêm (20..100) | cấp khu tối thiểu (1..5) | 0 | 0 | 0 |
| Quặng mỏ (P1 bit 8) | 0 | 1 | 0 | 0 | 0 | 0 | 0 |
| Thức ăn thợ (P1 bit 10) | 0 | 0 | **điểm thể lực hồi** (10..50) | 0 | 0 | 0 | 0 |
| Vật liệu nâng cấp khu (P1 bit 1/4/5/8: 333006-8, 333406-8, 333706-8, 333806-8) | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Hạt sự kiện (334045-334059) | số lượng sản phẩm (10/3/5) | phút | sản phẩm | 1 | exp farm | exp thợ | 334009 |

⚠️ **Hai field dễ nhầm nhất**: `P2` = **số lượng** sản phẩm; `P5` = **cấp khu tối thiểu**. Ví dụ `332140` (Hạt Giống Táo): P2=1 (1 Táo/lần), P5=2 (cần Nông Trại Lv2).


Ví dụ đã đối chiếu description (✅ dữ liệu, 🟡 cách diễn giải):
- `332139` (Hạt Giống Cỏ Tươi): P1=1, P2=1, P3=30, P4=333001 (Cỏ Tươi), P5=1, P6=5, P7=1, P8=334009; Description: “Nhận được 1 Cỏ Tươi, thời gian chín 30 phút… trang trại nhận 5 EXP, nhân viên 1 EXP”.
- `332140` (Táo): P5=2, P8=334010 (“Nông Trại Lv2”); Description có “(Nông Trại Lv2 được mua)”.
- `333201` (Kiến Lửa nhỏ): P2=1, P3=100, P4=333401 (Kiến Lửa lớn), P6=25, P7=6, P8=334036 (“Bãi Cỏ Lv1”).
- `333001` (Cỏ Tươi — sản phẩm): P1=10 (bit1+bit3), P4=20 (điểm trưởng thành), P5=1, ReclaimValue=50 (giá bán Cỏ Vàng).

### 3.3 `TS_ManorPlant.xml` — bảng nâng cấp 6 khu (✅ dữ liệu)

50 `Item`, attributes: `UpgradeType, Grade, NeedGrade1, NeedGrade2, NeedItem1, NeedItemCount1, NeedItem2, NeedItemCount2, PlantSpeedValue, Style, WorkMax, DropId, UnlockItem`. Analyzer `FarmObjectUpgradeAnalyzer` → `Dictionary[UpgradeType][Grade]` (client dùng `getObjectTempByType(type)[grade]`).

| UpgradeType | Nghĩa | Grade | NeedGrade1 | NeedGrade2 | NeedItem (số lượng) | PlantSpeedValue | Style | WorkMax | DropId |
|---:|---|---|---|---|---|---|---|---|---|
| 1 | Cấp **Nông Trại** (area 1 `level`) | 1..9 | cấp exp Trang Trại (TS 109) | — | 333006 x5/20/50/80/100/150/200/300/600 | 1000,980,960,940,920,900,870,840,800 | 1,1,1,2,2,2,3,3,3 | 0,0,1,2,2,2,3,3,3 | 0 |
| 2 | Cấp **Ao Cá** (area 2) | 1..9 | như trên (10,12,16,20,23,28,33,38,45) | — | 333706 x20/80/100/150/200/300/400/500/600 | 1000→800 | 1..3 | 1,1,1,2,2,2,3,3,3 | 0 |
| 3 | Cấp **Hầm Mỏ** (area 3) | 1..9 | 15,17,21,24,29,34,39,44,50 | — | 333806 x30/50/100/200/300/400/500/600 | 1000→800 | 1..3 | 1,1,1,2,2,2,3,3,3 | 0 |
| 4 | Cấp **Bãi Cỏ** (area 4) | 1..9 | 5,7,11,14,18,22,27,32,40 | — | 333406 x10/50/80/100/150/200/300/400 | 1000→800 | 1..3 | 1,1,1,2,2,2,3,3,3 | 0 |
| 5 | Cấp **Đất Trồng** (area 1 `levelTwo`) | 1..9 | 1 | **cấp Nông Trại yêu cầu** (1,2,3,4,5,6,7,8,9) | 333006 x4/15/40/60/80/120/150/250 (+333007 x2/4/8, 333008 x15/40/60) | 0,10,20,40,60,80,100,120,150 | 1,1,1,2,2,2,3,3,3 | 0 | 0 |
| 6 | **Cuốc Chim / Hầm mỏ mở rộng** (area 3 `levelTwo`) | 1..5 | 1 | **cấp Hầm Mỏ yêu cầu** (1,2,3,4,5) | 333806 x25/150/250/400 (+333807 x40/80, 333808 x80/150) | 0,30,60,100,150 | 0 | 0 | 0; **UnlockItem** = 333901,333902,333903,333904,333905 |

- `UnlockItem` chỉ dùng cho UpgradeType 6 = **cuốc mỏ** tương ứng cấp; client tab “Cuốc Chim” hiển thị item `UnlockItem`, cho phép “dùng” (gửi 11 area 3 + templateID cuốc) khi `levelTwo >= Grade`, và gửi 14/6 để nâng cấp.
- `DropId` = 0 ở **toàn bộ** dữ liệu hiện tại ⇒ ❓ không dùng (khả năng bảng drop quái/hoạt động khác; hoặc bản này bỏ).
- `NeedGrade1` với type 1..4 = **cấp exp Trang Trại (Types 109)** chứ không phải cấp khu. ⚠️ Tên dễ nhầm.
- `PlantSpeedValue`:
  - Type 1..4: hệ số **per-mille thời gian** (1000 = bình thường, 800 = nhanh 20%). Client hiển thị `(1000 - PlantSpeedValue)/10` **%** (`FarmAreaTips.as:65-70` + text `tank.farm.menu.areaTips1..4`).
  - Type 5,6: là **bonus trực tiếp ×10** (`PlantSpeedValue/10` % — `tank.farm.menu.areaTips5/6`).
- `WorkMax` = **số thợ tối đa** của khu ở cấp tương ứng (client chặn thêm thợ khi vượt — `FarmMenuHeadItem.as:93-107`).
- `Style` = chỉ số skin hình ảnh khu (client dùng để load asset qua `PathManager.getFarmSceneBackPath(...)`, `getFarmAreaIcon`).

### 3.4 `TS_UpgradeTemplate.xml` — Types 109..113 (✅)

664 `Item` (toàn bộ game); riêng nông trại: **Types 109, 110, 111, 112, 113**. Attributes: `Types, TemplateId, Grades, Data, TemplateName, ItemTempId1..4, Param1..4, ActiveObj`.

Mapping client (`templateData\_246$11.as:214-228` + `getFarmUpgradeDic`, dòng 262-279):

| Types | Dictionary | Dùng cho | Số row | Ý nghĩa |
|---:|---|---|---:|---|
| 109 | `_farmDic` | Cấp **Trang Trại** (exp farm) | 50 (Grades 1..50) | `Data` = **exp tích luỹ** cần để đạt grade; `TemplateName`=“Lv.N”, `ActiveObj`=“Trang Trại LvN” |
| 110 | `_farmSkillDic` | Kỹ năng **Nông Dân** (worker workType 1) | 9 (1..9) | `Data` = exp tích luỹ; `Param1` = % tốc độ (client hiện `Param1/10` %); `Param4` = 1/2/3 (hình thể worker → asset); `ActiveObj` = tên cấp |
| 111 | `_fishSkillDic` | **Dân Chài** (workType 2) | 9 | như trên |
| 112 | `_mineSkillDic` | **Thợ Mỏ** (workType 3) | 9 | như trên |
| 113 | `_ranchSkillDic` | **Chăn Nuôi** (workType 4) | 9 | như trên |

Bảng exp Trang Trại (109, `Data` tích luỹ): 0, 190, 465, 2050, 3305, 6985, 14360, 24825, 32490, 56720, 78995, 112660, 152985, 217890, 296860, 366735, 460750, 529330, 677780, 867060, 1067395, 1324305, 1616590, 1926715, 2440100, 2841335, 3119975, 3823875, 4419625, 5308485, 6007645, 6502800, 7655995, 8913160, 9978750, 11929090, 12777185, 13661185, 15544160, 17562545, 20379500, 21653035, 22974040, 24343255, 27142020, 30949295, 32674480, 34457270, 36298515, 38198770.

Bảng kỹ năng (110-113, `Data`): 0, 150, 1550, 6230, 21190, 62500, 147840, 402620, 898290; `Param1`: 30,35,40,45,50,55,60,65,70; `Param4`: 1,1,1,2,2,2,3,3,3.

Công thức client (`_96$41.getUpgradeDataByExp`): duyệt grade 1..N, trả grade lớn nhất có `Data <= exp` (grade 1 Data=0).

### 3.5 `TS_ManorTask.xml` — nhiệm vụ bảng tin (✅ dữ liệu)

23 `Item`: `TemplateId, TaskType, TaskName, TaskDescp, Condition1..3, RewardItemId1/2, RewardCount1/2`.

| TaskType | Nghĩa | Condition1 | Condition2 | Condition3 | Ghi chú |
|---:|---|---|---|---|---|
| 1 | “Nhắc nhở nhân viên bạn bè” (đập thợ lười ở farm bạn) | **số thợ lười cần đập** (3, 5) | rỗng | 5 (❓ chưa rõ; có thể số bạn bè cần ghé) | Client hiện tiến độ `condition1/Condition1`; nút “Hoàn thành” bật khi `condition1 == Condition1` (`FarmBillboardItemView.as:73-87`) |
| 2 | “Thu hoạch lớn” — nộp vật phẩm | **TemplateID vật phẩm cần nộp** | **số lượng cần** | **cấp Trang Trại tối thiểu** (1,3,6,9,13…) | Client hiện “Cần: {tên} x{count}”; nút “Nộp” → gửi 19 (`FarmBillboardItemView.as:89-94`) |

Ví dụ: `TemplateId=1` TaskType1 Condition1=3 → thưởng 40009 x10; `TemplateId=20` TaskType2 Condition1=333002, Condition2=3, Condition3=6 → thưởng 12572 x8.
`RewardItemId2/RewardCount2` = rỗng trong toàn bộ data ⇒ thưởng chỉ 1 loại. ❓ Thưởng đưa vào đâu (túi thường hay kho farm) — client không quy định; server nên AddTemplate vào túi thường (các item 40009/12248… là item thường) 🟡.

### 3.6 `PetExpItemPrice.xml` — bảng giá “Thức Ăn Trung” (✅ dữ liệu)

20 `Item`: `Count` (1..20), `Money` (giá Xu), `ItemCount` (số item mỗi lần mua = 1).

```
Count:  1   2   3   4   5   6   7   8   9   10  11  12  13  14  15  16  17  18  19  20
Money: 80 100 120 140 160 180 200 220 240 440 480 520 560 600 640 680 720 760 800 840
ItemCount: 1 (tất cả)
```

- Client chọn giá theo `31 - buyExpRemainNum` (`_96$41.as:752-782`).
- Item nhận: **334102** “Thức Ăn (Trung)” — mỗi cái tăng tối đa 10000 exp pet (`ItemManager.getTemplateById(334102).Property3 = 10000`, dùng trong `FarmBuyExpFrame.as:62`).
- Giá hiển thị là **Xu** (text `farm.viewx.farmBuyExpFrame.explain`: “Hao {0} xu…”), thanh toán qua `CheckMoneyUtils` (bind/unbind Xu) → gửi 68/19.

### 3.7 `ManorWealth.xml` / `ManorAbility.xml` — BXH tuần (✅)

- `ManorWealth.xml`: root `<Result value="true" message="Success!" date="MM-DD HH:mm">`, 100 `Item` attrs: `Order, NickName, UserId, Money, TotalGP, Wealth, AreaId, AreaName, Colors, Style, Sex`. Analyzer `FarmManorWealthAnalyzer` → `model.manorWealthList` + `wealthDate`; hiển thị tab **BXH Tài phú** (xếp theo `Order`, hiện cấp Trang Trại từ `TotalGP`, money từ `Money`).
- `ManorAbility.xml`: cùng format nhưng hiện **rỗng** (`<Result ... date="01-21 09:58" />`), 0 Item; dùng cho tab **BXH Lao động**, field `TotalAbilityGrades`, `BossNickName` (xem `FarmManageRankLaborView.as`).
- Hai file này là **snapshot tĩnh** do website/website-job sinh định kỳ (client chỉ GET); không phải packet 81. Server cần job ghi 2 file này (hoặc giữ tĩnh) — ngoài phạm vi game-server. 🟡

### 3.8 Shop liên quan nông trại (✅ dữ liệu + client)

| ShopID | Tên client (`ddt\data\ShopType.as:226,228`) | Dùng ở đâu | Thanh toán | Đặc điểm |
|---:|---|---|---|---|
| 135 | `FARM_QUICK_SHOP` | `FarmBuyView` (mua nhanh khi thiếu hạt/vật phẩm) | AValue1 = **Cỏ Vàng** (moneyType 1), AValue2 = **Huy Chương** (2), AValue3 = **Xu** (3) | 55 item: hạt giống, thú/cá, thức ăn, cuốc, vật liệu, hạt sự kiện…; mua qua **81/15** |
| 139 | `FARM_SHOP` | Tab **Cửa hàng** trong bảng Quản lý | AValue1 = **Cỏ Vàng** | 18 item: vật liệu nâng cấp khu (333006-8, 333406-8, 333706-8, 333806-8), thức ăn pet 334101/2/3, Quả Hồn 12243-45; có **giới hạn mua cá nhân** (`LimitPersonalCount`: 99/20/10/5 tuỳ item, reset theo tháng — xem `FarmManageShopView.getNextTime`); mua qua **packet 345** (shop thường) và xem hạn mức qua **packet 528** |

- Client gửi `sendPersonalLimitShop(139)` = **packet 528** body `[int shopID]` (`GameSocketOut.as:7073-7078`). Server trả (packet 528, không subtype): `[int shopID][int count]` + mỗi item `[int goodsID][int boughtCount]`; client tính `personalBuyCnt = LimitPersonalCount - boughtCount` (`ShopManager.as:60-81`). **Handler server hiện tại `ShopPersonalLimitHandler.cs` hard-code shop 120** → cần sửa cho shop 139 (và trả đúng goodsID + số đã mua). ✅ đọc từ code server.
- Mua shop 139: client gửi **packet 345** `NewBuyGoods` (`FarmShopBuyView.as:62-80`), nhưng UI hiển thị/so sánh giá **Cỏ Vàng** (`selfMoney`) ⇒ server phải special-case shop 139: trừ Cỏ Vàng (farm money) thay vì Xu/Huy Chương, và cộng item vào **kho farm** (goods) hay túi thường? ❓ (client không kiểm tra; khuyến nghị cộng goods farm cho item CategoryID 87, còn lại túi thường). Response: **81/26** (goods/tiền) + (nếu cần) 528 cập nhật hạn mức.

### 3.9 ServerConfig keys cho nông trại (✅ dữ liệu `WebSite\Request\ServerConfig.xml`)

| Key | Value hiện tại | Ý nghĩa suy ra |
|---|---|---|
| `ManorEmploryMoneys` | `0-5,100\|6-10,200\|11-15,300\|16-20,400\|21-25,500\|26-30,650\|31-35,800\|36-40,1000\|41-45,1200\|46-50,1500` | Giá **thuê thợ** (Cỏ Vàng) theo khoảng cấp (cấp nhân vật hoặc cấp nghề của thợ — ❓) |
| `ManorFreeMoneys` | `0-5,100\|6-10,200\|11-20,300\|21-100,300` (và bản ghi trước `0-5,350\|6-10,700\|11-100,1750` trong cùng file) | Giá **tự chuộc thân** (Cỏ Vàng) theo khoảng cấp — file có 2 dòng trùng key, dòng sau thắng khi client parse dictionary 🟡 |
| `ManorAddTimeMoneys` | `0-5,35\|6-10,70\|11-100,100` | Giá **bảo vệ hợp đồng thợ** (Xu/ngày) hoặc giá/1 lần mua ❓ |
| `ManorOtherNameMoney` | `200` (client default 300) | Giá **đổi danh hiệu thợ** (Xu) |
| `ManorGiftMoney` | `35` (client default 100) | Giá **tặng quà thợ** (Xu) |
| `ManorBeatLoveCD` | `100` | Cooldown (giây) giữa đánh/yêu thương thợ |
| `ManorWorkEventMinute` / `ManorWorkEventRandom` | `20` / `25` | Chu kỳ sự kiện thợ (phút) / % random (lười, cảm xúc…) |
| `ManorWorkDecPhysic` / `ManorWorkDecFeeling` | `3` / `3` | Giảm thể lực / tâm trạng mỗi chu kỳ làm việc |
| `ManorSlackId` | `11019` | Asset/npc id của thợ lười? ❓ |
| `ManorAbilityOrderReward` | `1-1,1122641\|2-2,...` | Thưởng BXH Lao động theo hạng (item template) |
| `ManorWealthsOrderReward` | `1-1,1122635\|...` | Thưởng BXH Tài phú theo hạng |
| `ManorTaskBoxId` | `1122728` | Item hộp thưởng nhiệm vụ? ❓ |
| `ManorActivityConfig` | `6,0\|10:00-18:00\|5\|15` | Cấu hình hoạt động Sâu Bọ (ngày/khung giờ/số quái?) ❓ |
| `ManorActivityScoreRange` | `300,800,1500,99999` | Mốc điểm hoạt động; client lấy phần tử thứ 3 = **1500** làm “điều kiện thắng” (`farmFightWinScore`) |
| `ManorActivityPersonRange` / `ManorActivityAwardRange` | theo 4 mốc | Ngưỡng số người / item thưởng theo mốc điểm |
| `ManorActivitySpeedBuffTeamRange` | `0,0,0,10` | Buff tốc độ sản xuất (%) theo mốc? (liên quan `buffRate` trong 81/24) ❓ |
| `FarmHavestMinute` | `30` | Chu kỳ thu hoạch cơ sở (phút) — ❓ có thể là “mỗi 30 phút 1 phần thức ăn” |
| `FarmActivity*`, `IsOpenFarmActivity` | — | Thuộc nông trại v1/cũ, không dùng cho 2.0 |

### 3.10 Công thức & ngữ nghĩa field (tổng hợp)

| Khái niệm | Công thức / cách dùng (suy ra + code client) | Tin cậy |
|---|---|---|
| Level Trang Trại | grade lớn nhất trong Types 109 có `Data <= selfExp` (`getUpgradeDataByExp(1, selfExp)`) | ✅ |
| Level khu (1..4) | `objectInfo(type).level` (chính là Grade trong TS_ManorPlant UpgradeType=type) | ✅ |
| Đất Trồng / Cuốc Chim | `objectInfo(1).levelTwo` / `objectInfo(3).levelTwo` ↔ UpgradeType 5/6 Grade | ✅ |
| Mở khoá khu | `isOpenArea`: `getObjectTempByType(type)[1].NeedGrade1 <= currentFarmLevel` (lấy từ TS_ManorPlant type) | ✅ |
| Thời gian chín | `plantRipeDate` = **số giây của 1 chu kỳ**; client tính `còn lại = plantRipeDate - ((now - plantDate)/1000) % plantRipeDate` (`FarmFarmInfoTips.as:79-85`) | ✅ cách tính; 🟡 server nên set: farm/mỏ = `P3(phút)*60*speed/1000`; thú/cá = ước lượng (P3 là điểm, xem 6.21) |
| `plantDate` | Date bắt đầu chu kỳ hiện tại (thời điểm trồng / mốc tính) | ✅ |
| `isPlant` | area đang có cây/thú/cuốc hoạt động | ✅ |
| `plantTempId` | area 1..2,4: TemplateID hạt/thú/cá đang nuôi; area 3: TemplateID cuốc đang dùng (UnlockItem của type 6) | ✅ |
| `foodTempId` / `foodDate` | thức ăn đang dùng (area 2/4) + thời điểm cho ăn; 0 = chưa cho ăn (client hiện “Không” + nhắc đỏ) | ✅ |
| Tốc độ khu | bonus% = `(1000 - PlantSpeedValue)/10` với type 1..4; `PlantSpeedValue/10` với type 5/6 | ✅ (client text) |
| Tốc độ thợ | `Param1/10` % theo kỹ năng (Types 110-113) | ✅ (client text) |
| Tổng tốc | 🟡 cộng dồn bonus khu + đất + thợ + buffRate (server tự chốt; client chỉ hiển thị từng phần) | 🟡 |
| Sản lượng/lần thu | seed: P2 × P4 (TemplateID), + exp farm P6, + exp thợ P7 (cho thợ đang làm khu đó) | 🟡 |
| Giá bán goods | `ReclaimValue` (Cỏ Vàng/1 item) — `FarmSellGoodsView.as:84-87` + text “Bán xong nhận được {0} Cỏ Vàng” | ✅ |
| Giá mua shop 135 | AValue1/2/3 (Cỏ Vàng/Huy Chương/Xu) — 81/15 moneyType tương ứng | ✅ |
| Tiền farm (`selfMoney`) | “Cỏ Vàng”: nguồn = bán goods; dùng = thuê thợ, mua shop 135 (type1), tự chuộc thân, shop 139 | ✅ |
| `selfValue` | client chỉ lưu; không hiển thị | ❓ gửi 0 |
| `selfExp` | exp Trang Trại; tăng khi thu hoạch (P6) — server chốt | 🟡 |
| `good.property` | = `Property1` của item (bitmask túi) | ✅ |
| `plantRipeDate` đơn vị | giây (client `/1000`) | ✅ |
| DropId | hiện = 0 toàn bộ ⇒ bỏ qua; quặng mỏ nên suy ra theo cấp cuốc ❓ | ❓ |

### 3.11 Map field ↔ DB entity đã có trong server source (tham khảo)

Trong `Full Source` đã có sẵn (mới thêm, cùng ngày — **không phải source gốc**): `SqlDataProvider.Data.UserManorInfo`, `UserManorAreaInfo`, `UserManorGoodsInfo`, `UserManorWorkerInfo`, `Bussiness.ManorBussiness`, `game.server.Game.Server.Farm.ManourConfigMgr`. Đối chiếu nhanh với spec này:

| Entity field | Có trong protocol? | Ghi chú |
|---|---|---|
| `UserManorInfo.Exp/Money/Value/ExpValue/BuffRate/BuffDate/BuyFoodCount` | ✅ | `BuyFoodCount` = buyExpRemainNum (81/34) |
| `UserManorAreaInfo.AreaType/Level/LevelTwo/IsPlant/PlantTempId/PlantDate/RipeDate/FoodTempId/FoodDate/ProduceCount` | ✅ | `RipeDate` nên lưu **số giây** (không phải timestamp) — comment hiện tại đang ghi “hoặc unix time” ❓ |
| `UserManorGoodsInfo.TemplateId/Property/Count` | ✅ | property = bitmask túi (Property1) |
| `UserManorWorkerInfo.OwnerID/WorkerUserID/Name/Title/Feeling/Energy/FarmExp/FishExp/MineExp/RanchExp/IsWorking/WorkType/WorkBeginDate/EmployProtectDate/ProtectPrice/Sex/Colors/Style/HiredDate` | ✅ | Khớp 100% field 81/24 + 81/2; 81/32 thiếu Sex/Colors/Style (giữ nguyên DB) |

---

## 4. FLOW TỪNG TÍNH NĂNG + PACKET SERVER PHẢI GỬI

### 4.1 Vào nông trại / vào farm bạn

1. Người chơi bấm nút **Nông trại** ở sảnh → `farm._96$41.instance.gotoFarm(userID)` (`_96$41.as:583-597`; gọi từ hall `hall\_129$33.as:144`, spouse `ChatOutputField.as:746`, farm list…).
2. `gotoFarm` → `LayerManager.clearnGameDynamic()`; nếu vào farm mình hoặc chưa có friend list → `initFrined()` → gửi **81/33** `[count][friendIDs...]` (`_96$41.as:609-631`).
3. Gửi **81/24** `[farmUserID]` (`GameSocketOut.sendRequestFarmInfo`, `2533-2539`).
4. Server **bắt buộc** trả **81/24** full snapshot → client `StateManager.setState("farm")` mở scene. (Nếu không có → màn hình không chuyển, đúng lỗi phase 1.)
5. Server trả **81/33** (danh sách bạn bè có thợ lười) — có thể trả trước/sau 24. Client dùng cho list bạn bè + hammer icon.
6. Nếu farm đang trong hoạt động Sâu Bọ: server push **81/46** (ai có farm đang fight) và/hoặc **81/36** khi client gửi 36; server push **81/44** khi có người vào.
7. Vào **chính farm mình** lần đầu: server nên gửi kèm **81/26** (tiền/exp/goods) + **81/18** (tasks) + **81/34** (pet food) — client sẽ tự gửi 34 khi FarmView init, nhưng 26/18 phải push (không có request nào khác). 🟡 (khuyến nghị)

### 4.2 Trồng / ngưng trồng (81/11)

Trigger: menu khu → tab “Cây Trồng/Thú Con/Cá Con” hoặc “Cuốc Chim”.
- Gửi **81/11** `[int areaType][int templateID]`; `templateID = 0` = ngưng (huỷ cây/thú hiện tại).
- Client hiện popup xác nhận (`FarmMenuGoodsItem.sendFarmPlantBegin`) rồi mới gửi.
- Server cần:
  1. Kiểm tra khu mở khoá (level Trang Trại ≥ seed P5/P8 level marker).
  2. Kiểm tra có item trong kho farm (goods): templateID với count ≥ 1; **trừ 1**.
  3. Nếu area đang có cây → thay thế (mất cây cũ, client đã cảnh báo).
  4. Set area: `isPlant=true, plantTempId=templateID, plantDate=now, plantRipeDate=<chu kỳ giây>`, `foodTempId=0`, `foodDate=now`. Với area 3 (mỏ): `plantTempId` = cuốc (UnlockItem type6) tương ứng.
  5. Push **81/17** (area vừa đổi) + **81/26** (goods bị trừ) (+ **81/18** nếu task liên quan).
- Ngưng (templateID=0): set `isPlant=false, plantTempId=0, plantRipeDate=0, foodTempId=0` → push **81/17**.
- ❓ Cây đang lớn bị nhổ thì có mất hạt không? Client text “Xác nhận ngưng trồng” không nói — server tự chốt (khuyến nghị: mất cây, không hoàn hạt).

### 4.3 Cho ăn / tăng tốc (81/28) cho Ao Cá / Bãi Cỏ

Trigger: tab “Thức Ăn” của area 2/4 → chọn item → xác nhận (hoặc ngưng).
- Gửi **81/28** `[int areaType][int foodTemplateID]`; `0` = ngưng cho ăn.
- Server: kiểm tra item food thuộc bag 6 (fish fodder) hoặc 3 (ranch fodder), count ≥ 1; trừ 1; set `foodTempId`, `foodDate=now`; (tác dụng gameplay: tăng tốc trưởng thành/điểm no — lưu vào state).
- Push **81/17** + **81/26**.
- ⚠️ Client không gửi số lượng/không hỏi thời gian; server tự tính chu kỳ tiêu thụ (gợi ý `FarmHavestMinute`=30 phút → mỗi 30 phút tiêu 1 phần như description item).

### 4.4 Cho thợ ăn (81/9)

Trigger: Quản lý → tab Thông tin → icon “nhắc thể lực” của thợ → chọn thức ăn (bag 10) → **Cho ăn**.
- Gửi **81/9** `[int workerUserID][int foodTemplateID][int count]`.
- Server: kiểm tra item bag 10, count; trừ; `worker.energy += item.P4 * count` (clamp 100); push **81/32** + **81/26**.
- Client hiển thị “Cho ăn thành công sẽ tăng {P4*count} điểm no” (`FarmFeedView.as:80-84`).

### 4.5 Nâng cấp khu / đất / cuốc (81/14)

Trigger: tab “Nông Trại/Ao Cá/Hầm Mỏ/Bãi Cỏ” (type 1..4) hoặc “Đất Trồng” (type 5) hoặc nút mở cuốc ở tab “Cuốc Chim” (type 6).
- Gửi **81/14** `[int upgradeType]`.
- Server: dùng TS_ManorPlant row (type, Grade = level+1):
  - `NeedGrade1` ≤ level Trang Trại (Types 109 grade).
  - Type 5 cần `NeedGrade2` ≤ area 1 level; type 6 cần `NeedGrade2` ≤ area 3 level.
  - Có đủ `NeedItem1/2` trong kho farm → trừ.
  - `level++` (hoặc `levelTwo++` với type 5/6); nếu type 5 → area 1 `levelTwo`; type 6 → area 3 `levelTwo`.
- Push **81/17** (type tương ứng; với type 5 push type 1, type 6 push type 3) + **81/26** (goods bị trừ) + (nếu thay đổi level Trang Trại thì **81/24** hoặc 26 đã có selfExp; level Trang Trại không đổi khi nâng khu nên không cần).
- Client tự so sánh `level+1 == temp.Grade` để bật nút; không kiểm tra giá server-side.

### 4.6 Thu hoạch — **KHÔNG có request riêng** 🟡

- Grep toàn bộ client: **không có** hàm gửi request “harvest/gain”; 81/14 chỉ là nâng cấp; click vào cây/thú trong scene không có handler (các layer object `mouseEnabled=false`).
- Client tính “còn lại bao lâu” bằng `plantRipeDate` (mục 2.2/3.10). Khi chín, **server tự thu hoạch theo chu kỳ** (tính cả offline) và:
  - Cộng sản phẩm vào kho farm (`goods`), cộng exp farm (P6), exp thợ (P7) cho thợ đang làm khu.
  - Push **81/26** (goods + selfExp) + **(mobile)** **81/17** nếu chu kỳ reset `plantDate` (hoặc giữ nguyên vì công thức modulo của client tự cuộn chu kỳ).
  - Với task type 2 hoặc sự kiện: push **81/18** nếu tiến độ thay đổi.
- ❓ Có thể game gốc dùng chung 81/14 cho cả “thu hoạch” trong bản khác; build này chỉ dùng 14 cho upgrade (đã kiểm tra tất cả call site: `FarmMenuUpgradeItem.as:68`, `FarmMenuHoeItem.as:132`). Nếu server muốn cho phép “thu hoạch chủ động”, không có đường client → **không thể**; phải auto.

### 4.7 Bán nông sản (81/31)

Trigger: tab “Kho” → nút **Bán** → popup nhập số lượng.
- Gửi **81/31** `[int templateID][int count]`.
- Server: kiểm tra goods count; trừ; `selfMoney += ReclaimValue*count`; push **81/26** (goods list + selfMoney mới).
- Giá bán = `ItemTemplateInfo.ReclaimValue` (client hiển thị trước). Không phụ thuộc shop.

### 4.8 Mua hàng

a) **Mua nhanh (81/15)** — shop 135:
- Trigger: menu/kho thiếu item → icon mua → popup `FarmBuyView` chọn số lượng + loại tiền.
- Gửi **81/15** `[int goodsID][int count][int moneyType]`; `goodsID = ShopItemInfo.GoodsID` (của shop 135, không phải TemplateID).
- Server: tra shop 135; giá `AValue{moneyType}`; trừ tiền tương ứng (1: selfMoney/Cỏ Vàng; 2: Player.DDTMoney/Huy Chương; 3: Player.Money/Xu); cộng goods vào kho farm (TemplateID x count); push **81/26**.
- Không có response riêng; nếu lỗi server có thể gửi 81/26 (không đổi) + message thường (SendMessage) — ❓ client không có UI lỗi riêng cho packet này.

b) **Shop farm 139** — tab Cửa hàng trong Quản lý:
- Mở view: client gửi **528** `[int 139]` (và đã có sẵn data shop từ `ShopItemList.xml`).
- Mua: gửi **packet 345** (NewBuyGoods) như shop thường; server phải special-case shop 139 → trừ **Cỏ Vàng** (selfMoney), cộng goods; push **81/26**; cập nhật hạn mức mua (528 có thể push lại).
- ⚠️ Handler 345 hiện tại của server (`NewBuyGoodsHandler.cs`) không biết Cỏ Vàng → cần thêm nhánh.
c) **Mua Thức Ăn pet (68/19 → 81/34)**: xem 2.8/4.12.

### 4.9 Refresh định kỳ (81/27)

- `FarmMainView` timer 60s gửi **81/27** (body rỗng) (`FarmMainView.as:90-97`).
- Không có listener 27 ⇒ server **phải trả bằng packet push khác**. Khuyến nghị: gửi **81/17** cho 4 area của farm hiện tại + **81/26** nếu tiền/goods/exp thay đổi + **81/18** nếu task đổi. ❓ (cách gốc không xác định được từ client)
- Cũng nên gửi **81/46** định kỳ khi hoạt động Sâu Bọ đang mở (client chỉ nhận 46, không request).

### 4.10 Nhiệm vụ bảng tin (81/18/19)

1. Mở bảng tin → tab **Uỷ thác**: client gửi **81/18** (rỗng). Server trả **81/18** `[flag=1][count]{...}`.
2. Bấm **Nộp/Hoàn thành**: gửi **81/19** `[taskInstanceId]`.
3. Server: kiểm tra điều kiện:
   - TaskType 1: `condition1 >= Condition1` (số thợ lười đã đập) — tiến độ này server tự tăng khi nhận **81/25**.
   - TaskType 2: có đủ `Condition2` item `Condition1` trong kho farm (trừ đi) và level Trang Trại ≥ Condition3.
   - Đánh dấu `isReward=true`, cấp `RewardItemId1 x RewardCount1`.
4. Push **81/18** (list mới) + **81/26** nếu có trừ/grant goods.
- Danh sách task hôm nay: ❓ server tự chọn/random bao nhiêu task (client không quy định); reset 00:00 (theo text “Hôm nay đã xong uỷ thác”). Gợi ý: mỗi ngày chọn N template (N=5 theo `ManorActivityConfig`? ❓), tạo instance id riêng.

### 4.11 Log hoạt động (81/1) & 81/29

- Log hiển thị trong Quản lý → tab Thông tin nằm trong **response 81/1** (chuỗi UTF bất kỳ; client nối bằng `\n------------\n`).
- Subtype **29 LOG không được client dùng** (không sender, không listener) → server không cần implement.
- Nội dung log do server tự sinh (bán hàng, thuê thợ, bị trộm…) — ❓ định dạng text tự do.

### 4.12 Thức ăn pet (81/34 + 68/19)

1. `FarmView` init → gửi **81/34** (rỗng) → server trả **81/34** `[int remain]`.
2. Bấm nút mua (frame “Mua thức ăn”) → chọn bind/unbind → gửi **68/19** `[bool isBind]`.
3. Server (`BuyPetExpItem.cs` đã có): kiểm tra remain ≥ 1; giá theo bảng (client kỳ vọng `Count = 31 - remain`); trừ tiền tương ứng; cộng item **334102** x `ItemCount`; `remain--`; trả **68/19** `[int remain]` (client cũng nghe 81/34).
4. Nếu remain = 0 → client báo “Số lần mua hôm nay đã hết”. Reset hàng ngày — cơ chế reset ❓ (theo ngày? theo tháng? `PlayerFarmInventory` cũ reset khi >20; client v1 dùng `ManorFreeMoneys`…).

### 4.13 Luồng thợ (worker) — tóm tắt request → push

| Bước | Packet C→S | Server push |
|---|---|---|
| Mở tab Tuyển thợ | **81/30** `[UTF keyword]` | **81/30** list ứng viên |
| Thuê | **81/2** `[workerUserID]` | **81/2** success=true (full worker) + **81/26** (trừ Cỏ Vàng) |
| Mua đứt (thợ đang thuộc chủ khác) | **81/2** `[workerUserID]` | như trên (trừ Xu cho chủ cũ) |
| Sa thải | **81/5** `[workerUserID]` | **81/2** success=false (xoá) |
| Tự chuộc thân | **81/6** (rỗng) | **81/1** hoặc **81/2** fail + **81/26** |
| Bảo vệ hợp đồng thợ N ngày | **81/3** `[workerUserID][N]` | **81/32** (+**81/26** nếu trừ tiền ngay) |
| Đổi danh hiệu | **81/4** `[workerUserID][UTF title][bool isBind]` | **81/32** |
| Bảo vệ bản thân N ngày | **81/8** `[N]` | **81/1** (protectDate) + **81/26** |
| Cho thợ làm việc | **81/12** `[workType][workerUserID]` | **81/32** (isWorking, workType, workBeginDate) |
| Cho thợ nghỉ | **81/13** `[workType][workerUserID]` | **81/32** |
| Đánh thợ (beat) | **81/20** `[workerUserID]` | **81/32** (feeling giảm) |
| Yêu thương | **81/21** `[workerUserID]` | **81/32** (feeling tăng) |
| Tặng quà (+10 feeling) | **81/22** `[workerUserID][bool isBind]` | **81/32** (+**81/26**) |
| Cho thợ ăn | **81/9** `[workerUserID][foodID][count]` | **81/32** + **81/26** |
| Đập thợ lười ở farm bạn | **81/25** (rỗng) | **81/18** (task tiến độ) + (khuyến nghị) **81/33** |
| Đồng bộ feeling/energy khi làm việc | — | **81/32** định kỳ (mỗi chu kỳ `ManorWorkEventMinute`) |

Quy ước dữ liệu:
- `feeling`, `energy`: 0..100 (client thanh tiến trình /100).
- `workType` = areaType: 1 Nông Dân, 2 Dân Chài, 3 Thợ Mỏ, 4 Chăn Nuôi (client map sang tên `farmer/fisher/miner/herdsman`).
- Số thợ tối đa 1 khu = `WorkMax` của TS_ManorPlant theo level khu hiện tại.
- Thợ có thể là **chính người chơi khác** (mỗi user chỉ bị 1 chủ thuê tại 1 thời điểm; `bossID` trong 81/1/81/30 thể hiện điều này). 🟡
- Giá thuê/mua đứt/bảo vệ: xem 3.9 (config) — client không tự tính, chỉ hiển thị số server gửi.

### 4.14 Hoạt động chiến đấu / xếp hạng (phase sau — chưa cần)

Tóm tắt luồng suy ra:
1. Server push **81/46** khi cửa sổ hoạt động Sâu Bọ mở/đóng: mỗi farm có `fightStartTime/fightEndTime/isFight`. Client ở sảnh hiện popup mời nếu farm của bạn/người khác đang fight; nếu đang xem farm đó thì tự reload.
2. Client bấm “Mở hoạt động” (bảng tin activity) → **81/45**; server bật `isFight` cho farm và push **81/46**.
3. Vào farm đang fight → client gửi **81/36** → server trả **81/36** full scene (players + monsters) → client init fight.
4. Di chuyển: **37** (C→S `[x][y]`; S→C `[userID][x][y]` broadcast).
5. Click quái → đi tới → gửi **42** `[masterID]`; server xử lý trận (ngoài phạm vi protocol này — có thể dùng hệ thống fight thường), khi quái chết push **43** `[farmUserID][masterID]`.
6. Điểm/rank: server push **40** `[farmUserID][myScore][count]{name,score}`; client refresh mỗi 60s (đang fight) hoặc khi mở FarmView.
7. Rời state farm khi fight → **39** (client C→S rỗng; server broadcast **39** `[farmUserID][userID]` cho người khác); server push **44** khi có người vào/đổi state.
8. Chat kênh farm: **38** C→S `[byte 38][UTF msg]`; server broadcast bằng **packet 19** (scene chat) `[int zoneID][byte channel=38][bool isGuild][UTF sender][UTF msg]` (🟡 — theo parser `ChatManager.__sceneChat`).
9. **41 CHECK_STATE không được dùng** trong build này.
10. Bảng xếp hạng tĩnh: `ManorWealth.xml` / `ManorAbility.xml` (website job).

### 4.15 Chat (81/38)

- Client: gõ chat trong state farm (ChatManager.state=27) → channel 38 → `sendFarmChat(38, msg)`.
- Server: relay cho người trong farm bằng packet 19 với `channel=38` (client hiển thị như chat scene; 🟡 vì parser chung không phân biệt).
- Không có response 81/38.

---

## 5. BẢNG TÓM TẮT NHANH CHO DEV SERVER

### 5.1 Bảng “1 dòng/subtype” (copy để code)

| Sub | Dir | Request sau subtype | Response/push server phải gửi |
|---:|---|---|---|
| 1 | C↔S | *(rỗng)* | **81/1**: `UTF title, bool isEmploy, Date employProtectDate, int 0, bool isProtect, Date protectDate, int 0, int ransomMoney, int protectMoney, int skillTotalGrade, int farmExp, int fishExp, int mineExp, int ranchExp, int bossID, [boss fields nếu !=0], int logCount, [int 0, UTF log]...` |
| 2 | C↔S | `int workerUserID` | **81/2**: `bool true, int userID, UTF name, UTF title, int feeling, int energy, int farmExp, int fishExp, int mineExp, int ranchExp, bool isWorking, int workType, Date workBeginDate, Date employProtectDate, int protectPrice, bool sex, UTF colors, UTF style` / fail: `bool false, int userID` |
| 3 | C→S | `int workerUserID, int days` | **81/32** |
| 4 | C→S | `int workerUserID, UTF title, bool isBind` | **81/32** |
| 5 | C→S | `int workerUserID` | **81/2** false |
| 6 | C→S | *(rỗng)* | **81/1** (hoặc 81/2 false) + **81/26** |
| 8 | C→S | `int days` | **81/1** + **81/26** |
| 9 | C→S | `int workerUserID, int foodTemplateID, int count` | **81/32** + **81/26** |
| 11 | C→S | `int areaType, int plantTemplateID (0=ngưng)` | **81/17** + **81/26** |
| 12 | C→S | `int workType, int workerUserID` | **81/32** |
| 13 | C→S | `int workType, int workerUserID` | **81/32** |
| 14 | C→S | `int upgradeType 1..6` | **81/17** (+**81/26** nếu tiêu item) |
| 15 | C→S | `int goodsID, int count, int moneyType 1/2/3` | **81/26** |
| 17 | S→C | — | `int areaType, int level, int levelTwo, bool isPlant, int plantTempId, Date plantDate, int plantRipeDate, int foodTempId, Date foodDate` |
| 18 | C↔S | *(rỗng)* | **81/18**: `int flag(=1), int count, [int id, int templateID, int c1, int c2, int c3, bool isComplete, bool isReward]*` |
| 19 | C→S | `int taskInstanceId` | **81/18** |
| 20 | C→S | `int workerUserID` | **81/32** |
| 21 | C→S | `int workerUserID` | **81/32** |
| 22 | C→S | `int workerUserID, bool isBind` | **81/32** (+**81/26**) |
| 24 | C↔S | `int farmUserID` | **81/24** full (mục 2.1) |
| 25 | C→S | *(rỗng)* | **81/18** (+**81/33**) |
| 26 | S→C | — | `int selfMoney, int selfValue, int selfExp, int goodsCount, [int templateID, int property, int count]*` |
| 27 | C→S | *(rỗng)* | **81/17** ×4 (+**81/26**, **81/18**) |
| 28 | C→S | `int areaType, int foodTemplateID (0=ngưng)` | **81/17** + **81/26** |
| 30 | C↔S | `UTF keyword` | **81/30** list (mục 2.10) |
| 31 | C→S | `int templateID, int count` | **81/26** |
| 32 | S→C | — | `int farmUserID, int workerUserID, UTF name, UTF title, int feeling, int energy, int farmExp, int fishExp, int mineExp, int ranchExp, bool isWorking, int workType, Date workBeginDate, Date employProtectDate, int protectPrice` |
| 33 | C↔S | `int count, [int userID]*` | **81/33**: `int count, [int userID]*` |
| 34 | C↔S | *(rỗng)* | **81/34**: `int buyExpRemainNum` (hoặc **68/19** cùng payload int) |
| 36 | C↔S | *(rỗng)* | **81/36** scene (mục 2.11) — phase sau |
| 37 | C↔S | `int x, int y` | **81/37**: `int userID, int x, int y` — phase sau |
| 38 | C→S | `byte channel(38), UTF msg` | Packet 19 chat channel 38 — phase sau |
| 39 | C↔S | *(rỗng)* | **81/39**: `int farmUserID, int userID` — phase sau |
| 40 | C↔S | *(rỗng)* | **81/40**: `int farmUserID, int myScore, int count, [UTF name, int score]*` — phase sau |
| 42 | C→S | `int masterID` | phase sau |
| 43 | S→C | — | `int farmUserID, int masterID` — phase sau |
| 44 | S→C | — | scene payload — phase sau |
| 45 | C→S | *(rỗng)* | **81/46** (bật hoạt động) — phase sau |
| 46 | S→C | — | `bool open, int count, [int farmUserID, Date start, Date end, bool isFight]*` — phase sau |

### 5.2 Thứ tự implement đề xuất (server)

**Phase A — mở & xem nông trại (đã một phần):**
1. `81/24` full snapshot đúng spec 2.1 (thay `RequestFarmInfo.cs` phase 1) — thêm header landLevel/mineLevel thật, workers, goods.
2. `81/33` trả danh sách (có thể rỗng) — đã có.
3. `81/34` trả `buyExpRemainNum` thật (mặc định 30 — xem 6.7), `81/18` task rỗng `[1][0]`, `81/26` full (tiền/exp/goods), `81/1` info tối thiểu.
4. Client sẽ gửi **81/27** mỗi 60s → handler ack + push 17/26 (hoặc tạm no-op như phase 1 vẫn không crash).

**Phase B — trồng trọt/cơ bản:**
5. `81/11` plant/ngưng + `81/17` + `81/26`.
6. `81/14` nâng cấp khu/đất/cuốc + `81/17`.
7. `81/28` cho ăn + `81/17`; `81/31` bán + `81/26`.
8. `81/15` mua nhanh shop 135 + `81/26`.
9. Timer server tự thu hoạch (hoặc tính offline lúc login) → `81/26` (+`81/17`).

**Phase C — thợ:**
10. `81/30` list ứng viên; `81/2` thuê; `81/5` sa thải; `81/12/13` làm/nghỉ; `81/32` push.
11. `81/3/4/8/20/21/22/9` + `81/32`; `81/6` tự chuộc; `81/1` info đầy đủ + log.

**Phase D — nhiệm vụ/shop/bảng tin:**
12. `81/18/19` task + `81/25`; reset ngày.
13. Shop 139: sửa `528` (shop 139 + bought counts) + special-case `345` trừ Cỏ Vàng.
14. `81/1` log + ranking tĩnh (ManorWealth/ManorAbility XML).

**Phase E — fight/rank (sau):**
15. `81/45/46/36/44/37/42/43/39/40`, chat 38→packet 19, config `ManorActivity*`.

**Bắt buộc trước Phase B:** xoá/rename toàn bộ handler nông trại cũ trong `Game.Server.Farm.Handle` (xung đột subtype — xem 7.3).

### 5.3 Payload hex mẫu (tham khảo nhanh)

Chú thích: sau `code=81`, byte đầu = subtype. Date = `[year short][month][day][hour][min][sec]`.

```
// 81/33 rỗng:  21 00000000
// 81/34 remain=30: 22 0000001E
// 81/26 money=1000: 1A 000003E8 00000000 00000000 00000000
// 81/2 fail (sa thải worker 5): 02 00 00000005
// 81/17 trồng cỏ area1 Lv1: 11 00000001 00000001 00000001 01 0005116B 07EA0B080C0000 00000708 00000000 07EA0B080C0000
// 81/46 mở, 1 farm đang fight: 2E 01 00000001 00000001 07EA0B08100000 07EA0B08110000 01
```

---

## 6. CÁC ĐIỂM “CHƯA CHẮC” (cần chốt khi code server)

1. **`plantRipeDate` ngữ nghĩa/đơn vị** — client chắc chắn dùng theo công thức modulo: `còn lại = plantRipeDate - ((now-plantDate)/1000) % plantRipeDate` ⇒ hiểu là **chu kỳ (giây)**. Chưa chắc server gốc lưu “thời gian còn lại” hay “timestamp”; nếu server lưu timestamp thì client hiển thị sai. **Khuyến nghị: lưu & gửi chu kỳ giây** (vd 30 phút = 1800).
2. **Thu hoạch tự động** — client không có request harvest; suy ra server auto thu hoạch theo chu kỳ. Chưa biết chính xác thời điểm reset `plantDate` và cách cộng dồn offline; cũng chưa chắc cây/thú biến mất sau 1 lần thu (với hạt thường) hay thu lặp nhiều lần (mô tả item sự kiện “được thu hoạch 10 lần”, cuốc mỏ “mỗi 30 phút 1 phần”).
3. **Ý nghĩa 2 int bị bỏ qua trong 81/1** — client đọc rồi vứt (sau `employProtectDate` và sau `protectDate`). Gửi 0 an toàn; nếu server gốc dùng để chứa employerID/protectorID thì client cũng không quan tâm.
4. **`selfValue` trong 81/26** — client lưu nhưng không hiển thị/không dùng. Gửi 0 hoặc bằng selfMoney.
5. **`DropId` trong TS_ManorPlant** — toàn bộ = 0; chưa rõ dùng cho quặng mỏ ngẫu nhiên hay bảng drop khác. Cần tự định nghĩa cách chọn quặng (gợi ý: theo cấp cuốc 333901→333801, 333902→333802, …).
6. **Nghĩa `Property2..8` của TemplateManorList** — client 12.6 không đọc; bảng nghĩa ở 3.2 là **suy ra từ Description**; chưa có server gốc đối chiếu. Riêng P2 (số lượng), P3 (phút), P4 (product), P6/P7 (exp) rất chắc; P5/P8 (level điều kiện) chắc vừa.
7. **`buyExpRemainNum` & bảng giá PetExpItemPrice** — client dùng `Count = 31 - remain`; server cũ dùng `RealMoney(remain) ≈ 21 - remain` (remain max 20). Cần chốt: nếu theo client 12.6 thì **remain khởi tạo 30** (giá 80..840 cho 20 lượt); nếu theo code cũ (remain=20) thì client hiển thị giá Count=11 (480) cho lần đầu — lệch thiết kế. **Khuyến nghị: dùng công thức client (`31-remain`), remain mặc định 30, giảm 1/lần, reset hằng ngày**.
8. **Response sau các request 3,4,5,6,8,9,11,12,13,14,15,19,20,21,22,25,28,31** — client không có listener riêng; server **phải** dùng các push 17/26/32/2/18/1 (đã đề xuất ở mục 4). Cách gốc (server Gunny chính thức) không thể suy ra từ client; đây là **thiết kế tự chọn** nhưng phải đúng bộ packet ở trên.
9. **Response 81/27 (refresh 60s)** — không có listener 27; gợi ý gửi 17×4 + 26 (+18), nhưng không chắc bản gốc làm vậy.
10. **Task hằng ngày**: số lượng task/ngày, chọn template nào, reset giờ nào (00:00?), thưởng vào túi thường hay kho farm — client không quy định. `Condition2/Condition3` của TaskType 1 (rỗng / =5) chưa rõ nghĩa.
11. **Giá thuê/mua đứt/bảo vệ thợ** — config `ManorEmploryMoneys`, `ManorFreeMoneys`, `ManorAddTimeMoneys` là “khoảng cấp → giá”; chưa rõ khoảng cấp dựa trên cấp nhân vật, cấp nghề thợ hay cấp Trang Trại của chủ; cũng chưa rõ `price` trong 81/30 là giá thuê Cỏ Vàng và giá mua đứt Xu được tính thế nào (client dùng **cùng 1 field** cho 2 mục đích với 2 loại tiền khác nhau).
12. **Worker stats: per-user hay per-owner** — entity server hiện tại lưu theo (OwnerID, WorkerUserID); client 81/1/81/30 cho thấy mỗi user chỉ có 1 “boss” tại 1 thời điểm ⇒ 1 dòng/user là đủ. Chưa chắc feeling/energy reset khi đổi chủ.
13. **Quy tắc “slacker”** — client chỉ nhận danh sách userID từ 81/33 và tự random thợ lười ở farm bạn (`FarmMainView.getRandomList`); server chỉ cần quyết định *farm nào* có slacker (gợi ý dùng `ManorWorkEventRandom`/`ManorSlackId`), chưa rõ logic gốc.
14. **Shop 139 thanh toán & nơi nhận item** — client hiển thị giá Cỏ Vàng + gửi packet 345 thường; server chưa có nhánh; chưa chắc item nhận vào kho farm hay túi thường.
15. **`81/17` chỉ áp dụng cho self farm** — khi xem farm bạn, server có thể muốn cập nhật trạng thái trồng của bạn; client bỏ qua (dòng 441-445). Không có cách khác ngoài gửi lại 81/24.
16. **`buffRate`/`buffDate`** — chưa rõ nguồn buff (hoạt động Sâu Bọ? item? admin?) và công thức cộng tốc độ với bonus khu/thợ.
17. **Chat 81/38 → hiển thị**: gần chắc server phải dùng packet 19 (scene chat) với channel 38, nhưng chưa kiểm chứng end-to-end.
18. **`FarmGoodsQuickBuyFrame`** là class chết (không được instantiate ở đâu) — đừng dùng nó để suy luận luồng.
19. **`81/41`, `81/23`, `81/29`** không có call site/listener trong client 12.6 này ⇒ **dead**; nếu log server cũ có xuất hiện thì có thể client khác/kênh khác gửi — không ảnh hưởng.
20. **ManorWealth.xml/ManorAbility.xml**: hiện là file tĩnh; chưa rõ job sinh định kỳ (website) — nếu không cập nhật, BXH sẽ là dữ liệu cũ/ rỗng.
21. **Đếm ngược cho thú/cá (area 2/4)**: client dùng chung công thức `R - (elapsed_giây % R)` cho mọi area (`FarmAreaInfoTips`), nhưng dữ liệu thú/cá tăng trưởng theo **điểm** (mỗi 30 phút ăn 1 phần thức ăn = P4 điểm no, cần P3 điểm để lớn) chứ không phải theo thời gian tuyến tính. Chưa rõ server gốc đặt `plantDate`/`plantRipeDate` thế nào cho area 2/4 để countdown có nghĩa. Khuyến nghị: `plantDate` = mốc cho ăn gần nhất (hoặc mốc trồng), `plantRipeDate` = ước lượng tổng giây còn lại**, và gửi lại **81/17** mỗi lần cho ăn để client vẽ lại; chấp nhận countdown chỉ là “tương đối”.
22. **Hạt sự kiện “thu hoạch nhiều lần”** (mô tả 334045-334051: “được thu hoạch 10 lần”; cuốc mỏ: “mỗi 30 phút 1 phần”): chưa rõ server gốc cho thu 1 lần rồi mất cây hay giữ cây và lặp chu kỳ. Protocol không thể hiện số lần còn lại (client không có field) ⇒ server tự quyết; nếu lặp chu kỳ thì giữ `isPlant=true`, cập nhật `plantDate` và push 17.

---

## 7. PHỤ LỤC

### 7.1 File client tham chiếu (decompiled_4)

| File | Dòng quan trọng | Nội dung |
|---|---|---|
| `farm\data\FarmPackageType.as` | 6-88 | Tên 40 subtype (class không được dùng — sender hard-code số) |
| `farm\data\FarmAreaType.as` | 6-50 | Bag index + belongToBag |
| `farm\_96$41.as` | 74-91 (setup listener), 93-298 (fight handlers), 300-324 (33), 333-421 (24), 423-446 (17), 448-475 (32), 477-498 (26), 500-533 (2), 535-564 (18), 583-597 (gotoFarm), 609-631 (initFrined), 653-707 (level/exp helpers), 746-750 (34/68-19) | Toàn bộ parser S→C + logic farm |
| `ddt\manager\GameSocketOut.as` | 2533-2539 (24), 9171-9452 (toàn bộ farm senders) | Request builders |
| `farm\view\manage\FarmManageView.as` | 32-35 (gửi 81/1 + 528), 37-89 (nhận 81/1) | Quản lý nông trại |
| `farm\view\manage\FarmManageHireView.as` | 34, 164-184 (gửi 30), 198-229 (nhận 30) | Tab tuyển thợ |
| `farm\view\manage\FarmManageHireItemView.as` | 43-91 | Thuê/mua đứt → 81/2 |
| `farm\view\manage\FarmManageInfoView.as` | 76-119 | Bảo vệ bản thân (8) / tự chuộc (6) |
| `farm\view\manage\FarmManageInfoServantItem.as` | 80-157 | Sa thải (5), đổi tên (4), bảo vệ thợ (3), cho ăn (9) |
| `farm\view\manage\FarmManageShopView.as` | 26-29, 80-83 | Shop 139 + hiển thị Cỏ Vàng |
| `farm\view\manage\FarmManageShopItem.as` | 40-50 | Mở popup mua (345) |
| `farm\view\manage\FarmManageRank*.as` | — | BXH từ ManorWealth/ManorAbility XML |
| `farm\view\menu\FarmMenuView.as` | 120-139 (red tip), 303-364 (initData theo khu/tab), 406-447 (lọc goods theo bag) | Menu khu |
| `farm\view\menu\FarmMenuGoodsItem.as` | 69-182 (11/28), 256-288 | Trồng/cho ăn/ngưng |
| `farm\view\menu\FarmMenuHoeItem.as` | 66-135 (11/14 cho mỏ) | Cuốc Chim |
| `farm\view\menu\FarmMenuUpgradeItem.as` | 48-71 (14) | Nâng cấp khu |
| `farm\view\menu\FarmMenuHeadItem.as` | 60-72 (13), 93-107 | Slot thợ |
| `farm\view\menu\FarmMenuStoreItem.as` | 38-52 | Bán goods |
| `farm\view\alertFrame\FarmBuyView.as` | 31-35, 95-134 | Mua nhanh (81/15) |
| `farm\view\alertFrame\FarmSellGoodsView.as` | 72-87 | Bán (81/31) |
| `farm\view\alertFrame\FarmBuyExpFrame.as` | 50-116 | Pet food (34/68-19) |
| `farm\view\alertFrame\FarmShopBuyView.as` | 28-129 | Mua shop 139 (345) |
| `farm\view\alertFrame\FarmFeedView.as` | 25-84 | Cho thợ ăn (81/9) |
| `farm\view\alertFrame\FarmFeedSelectView.as` | 80-95 | Chọn thức ăn bag 10 |
| `farm\view\alertFrame\FarmWorkerMenu.as` | 52-87 | 20/21/22 |
| `farm\view\alertFrame\FarmSendGiftView.as` | 21-49 | Quà (22, giá Xu `ManorGiftMoney`) |
| `farm\view\alertFrame\FarmChangeTitleView.as` | 50-75 | Đổi tên (4, giá Xu `ManorOtherNameMoney`) |
| `farm\view\alertFrame\FarmEmployeeProtectBuyView.as` | 66-83 | Bảo vệ thợ (3) |
| `farm\view\alertFrame\FarmProtectBuyView.as` | 65-82 | Bảo vệ bản thân (8) |
| `farm\view\alertFrame\FarmEmployeeListItemView.as` | 29-49 | Làm việc (12) |
| `farm\view\alertFrame\FarmEmployeeListView.as` | 19-51 | Danh sách thợ |
| `farm\view\alertFrame\FarmInfoView.as` | 47-97 | Thông tin cấp các khu |
| `farm\view\billboard\FarmBillboardQuestView.as` | 59-63 | Task begin (18) |
| `farm\view\billboard\FarmBillboardItemView.as` | 39-94 | Nộp task (19) + hiển thị tiến độ |
| `farm\view\billboard\FarmBillboardActivityView.as` | 26-38 | Mở hoạt động (45) |
| `farm\view\fight\FarmFightRankView.as` | 78-111 | BXH điểm (40) |
| `farm\view\map\FarmSceneMap.as` | 216-225 (42), 733-736 (36), 783-813 (37) | Scene fight + di chuyển |
| `farm\view\map\layerobject\FarmLayer*.as`, `farm\view\tips\Farm*Tips.as` | — | Hiển thị cây/thú/cá + đếm ngược |
| `farm\view\player\FarmSceneWorker.as` | 189-217 | Đập thợ lười (25), hammer |
| `farm\view\player\FarmScenePlayer.as` | 500-524 | state=2 (đang đánh) |
| `farm\view\player\FarmHeadLoader.as`, `FarmHeadSprite.as` | 25-54 / 39-54 | Format `style/colors` 11 phần (index 2 hair, 3 eff, 5 face) |
| `farm\view\FarmView.as` | 37-50 (gửi 34), 95-263 (red tip, buff, buy exp, rank refresh) | UI chính |
| `farm\view\FarmMainView.as` | 51-97 (timer 27/40), 99-151 (worker hiển thị) | Scene chính |
| `farm\view\FarmStateView.as` | 22-56 | Vào/rời state farm (gửi 39 khi fight) |
| `farm\view\manage\FarmManageView.as` | 32-35 | 81/1 + 528 |
| `ddt\manager\ChatManager.as` | 457-464 | Chat farm (38) |
| `ddt\manager\SocketManager.as` | 568, 670-671 | Dispatch packet 81 (subtype = 1 byte) |
| `ddt\loader\LoaderCreate.as` | 2011-2051, 874-879 | Load 6 XML farm + TS_UpgradeTemplate |
| `ddt\states\StateCreater.as` | 525-531 | Load XML khi vào state farm |
| `templateData\_246$11.as` | 214-228, 262-279, 25-33 | Types 109-113 mapping |
| `templateData\TemplateUpGradDataVo.as` | — | Field của bảng exp/skill |
| `farm\analyzer\*.as` | — | Analyzer XML |
| `farm\data\Farm*.as`, `farm\vo\*.as` | — | Cấu trúc dữ liệu client |
| `road7th\comm\PackageIn.as` | 141-150 (readDate), 79-88 (header), 136-139 (readDateString) | Kiểu dữ liệu |
| `road7th\comm\PackageOut.as` | 56-64 (writeDate), 14-26 (ctor) | Kiểu dữ liệu |

### 7.2 Vị trí XML / config server cần dùng

| File | Trạng thái | Ghi chú |
|---|---|---|
| `WebSite\Request\templatemanorlist.xml` | có, zlib, 82 item | danh mục goods + Property1 bitmask |
| `WebSite\Request\ts_manorplant.xml` | có, zlib, 50 item | bảng nâng cấp 6 khu |
| `WebSite\Request\ts_manortask.xml` | có, zlib, 23 item | task bảng tin |
| `WebSite\Request\petexpitemprice.xml` | có, zlib, 20 item | giá thức ăn pet |
| `WebSite\Request\manorwealth.xml` | có, zlib, 100 item + date | BXH tài phú (tĩnh) |
| `WebSite\Request\manorability.xml` | có, zlib, rỗng | BXH lao động (tĩnh) |
| `WebSite\Request\TS_UpgradeTemplate.xml` | có, zlib, 664 item | Types 109-113 |
| `WebSite\Request\TemplateAlllist.xml` | có, zlib | item template đầy đủ (Name/Pic/Description/ReclaimValue/PropertyX của từng item — client dùng cho tooltip & giá bán) |
| `WebSite\Request\ShopItemList.xml` | có, zlib | shop 135 (55 item) + 139 (18 item) |
| `WebSite\Request\ServerConfig.xml` | có, zlib | các key `Manor*` (mục 3.9) |

### 7.3 Mapping handler nông trại CŨ (server) → nghĩa MỚI (client 12.6)

Bắt buộc thay thế toàn bộ (cùng code 81, payload khác hoàn toàn):

| Sub | Handler cũ trong `Game.Server.Farm.Handle` (theo `[FarmHandleAttbute(n)]`) | Nghĩa cũ (`FarmPackageType`) | Nghĩa mới (client 12.6) |
|---:|---|---|---|
| 1 | `EnterFarm.cs` | ENTER_FARM (đọc int) | REQ_INFO (rỗng) → trả info+log |
| 2 | `GrowFields.cs` | GROW_FIELD | EMP_LOYD (thuê thợ / xoá thợ) |
| 3 | *(không có handler)* | ACCELERATE_FIELD | ADD_TIME (bảo vệ thợ) |
| 4 | `GainFields.cs` | GAIN_FIELD | SET_TITLE |
| 5 | *(không có handler)* | COMPOSE_FOOD | FIRE_EMP |
| 6 | `PayField.cs` | PAY_FIELD | FREEDOM |
| 7 | `KillCropField.cs` | KILLCROP_FIELD | *(không dùng)* |
| 8 | *(không có handler)* | HELPER_PAY_FIELD | ADD_LOCK_TIME |
| 9 | `HelperSwitchField.cs` | HELPER_SWITCH_FIELD | EAT_FOOD |
| 11 | `FarmPlantBegin.cs` (mới, đã đúng hướng) | — | PLANT_BEGIN |
| 12 | *(chưa có)* | — | WORK_BEGIN |
| 13 | *(chưa có)* | — | WORK_END |
| 14 | `FarmUpPlant.cs` (mới) | — | UP_PLANT |
| 15 | `FarmBuyGoods.cs` (mới — **sai field: xem 7.5**) | — | BUY_GOODS |
| 16 | `ExitFarm.cs` | EXIT_FARM | *(không dùng)* |
| 17 | *(không có handler cũ)* | FARM_LAND_INFO | REQUEST_FARM_AREA_INFO (S→C only) |
| 18 | `FarmGropFastforward.cs` | FRAM_GROP_FASTFORWARD | TASK_BEGIN |
| 19 | *(chưa có; handler cũ ở `Game.Server.Pet.Handle` cho packet 68)* | BUY_PET_EXP_ITEM | TASK_FINISH |
| 20 | *(không có handler)* | FARM_GIFTPACKS | BEAT |
| 21 | `FarmPoultryLevel.cs` | FARM_POULTYRLEVEL | LOVE |
| 22 | `InitFarmTree.cs` | FARM_UPGRADEINITTREE | GITF |
| 23 | `CallFarmPoultry.cs` | FARM_CALLPOULTYR | REQUEST_PLAYER_INFO *(dead)* |
| 24 | `RequestFarmInfo.cs` (đã thay) | FARM_POULTYWAKE | REQUEST_FARM_INFO |
| 25 | `FarmCondenser.cs` | FARM_CONDENSER | SLACKER |
| 27 | `FarmUpdatePlantData.cs` (mới) | — | UPDATE_PLANT (refresh) |
| 28 | `FarmPlantSpeed.cs` (mới) | — | SPEED_PLANT |
| 30 | *(chưa có)* | — | WORKER_LIST |
| 31 | `FarmSellGoods.cs` (mới) | — | SELL_GOODS |
| 32 | `FarmWater.cs` | FARM_WATER | UPDATE_WORKER (S→C push) |
| 33 | `FeedFarmPoultry.cs` (đã thay) | FARM_POULTYRFEED | SLACKER_QUERY |
| 34 | `FarmPetFoodQuery.cs` (đã thay) | — | PET_FOOD_QUERY |
| 36-46 | — | — | fight/rank (phase sau) |

> Kết luận: giữ lại bất kỳ handler cũ nào ngoài danh sách phase A ở trên sẽ làm client parse lệch hoặc server throw mỗi khi người chơi thao tác (test bằng cách bấm từng nút ở farm và xem `Road\logs\Error.log`). Đây cũng là lý do phase 1 mới chỉ “mở được farm”.

### 7.4 Ghi chú server hiện đang code dở (đối chiếu, cập nhật 2026-11-08 ~12:55)

Theo `docs\FARM2-PHASE2.md` (teammate server), phase 2.1 **đã code xong + build OK + deploy ~12:5x** (`data\deploy-farm2.log`): các handler 24/11/28/14/31/15/27/33/34 (+68/19) đã chạy trên Road.Service. Tài liệu này là nguồn truth từ client; các điểm cần sửa trong bản deploy đó nằm ở 7.5.

Trong `Full Source` đã có (do teammate/server implement song song cùng ngày — **không phải source gốc**; tài liệu này là nguồn truth từ client):
- `game.server\Game.Server.Farm\ManorConfigMgr.cs` — loader XML + shop 135 + pet food price. ✅ hướng đúng; dùng `Game.Base.Marshal.Uncompress` để giải nén — OK.
- `SqlDataProvider.Data\UserManorInfo/AreaInfo/GoodsInfo/WorkerInfo` + `Bussiness\ManorBussiness.cs` + `game.server\Game.Server.GameUtils\PlayerManor.cs` — schema/hướng đúng; xem 3.11 + 7.5 để biết điểm cần sửa.
- `Game.Server.Farm.Handle\RequestFarmInfo.cs` — đã nâng cấp (gọi `PlayerManor.SendManorInfo` + 26 + 34). ✅ khớp spec 2.1; lưu ý với farm bạn goods list phải rỗng (đang đúng) và `name` rỗng nếu offline (client fallback tên friend list — OK).
- `FarmPlantBegin/FarmPlantSpeed/FarmUpPlant/FarmSellGoods/FarmBuyGoods/FarmUpdatePlantData/FarmPetFoodQuery.cs` — mới thêm 12:49, cần đối chiếu 7.5.
- `ShopPersonalLimitHandler.cs` — hard-code shop 120 → cần sửa thành đọc shopID từ packet (139 cho nông trại).
- `BuyPetExpItem.cs` — đã chuyển sang `PlayerManor.BuyPetFood` (12:5x); cần đảm bảo giá theo `31-remain` (xem 6.7).
- Worker handlers (2,3,4,5,6,8,9,12,13,20,21,22,25,30,32) **chưa có** — phase C.

### 7.5 Đối chiếu nhanh với `PlayerManor.cs` hiện có (các điểm cần sửa theo client)

Đọc `game.server\Game.Server.GameUtils\PlayerManor.cs` (bản 12:5x) — các điểm **lệch với client/data**, cần fix trước khi test in-game:

| # | Vị trí code server | Vấn đề | Theo client/data phải là |
|---:|---|---|---|
| 1 | `BuyGoods()` + `ManorConfigMgr.GetFarmShopItem(int templateId)` (tra theo `TemplateID`) | Request **81/15** field đầu = `ShopItemInfo.GoodsID` = cột **ID** trong `ShopItemList.xml`/DB Shop (vd `332139135`), **không phải TemplateID**. Tra theo TemplateID sẽ luôn fail (client bấm mua không có gì xảy ra). | Tra shop 135 theo **ID** (server `ShopItemInfo.ID`), sau đó lấy `TemplateID` từ row để cộng goods. |
| 2 | `PlantBegin()`: `if (item.Property2 > 0 && GetFarmLevelGrade() < item.Property2)` | Dùng nhầm P2 (số lượng sản phẩm) làm điều kiện cấp | Điều kiện cấp = **P5** (hạt cây: cấp Nông Trại; thú/cá: cấp Bãi Cỏ/Ao Cá; cuốc mỏ: cấp Hầm Mỏ). Với farm/fish/ranch dùng `GetFarmLevelGrade()`, với mỏ dùng `GetMineLevel()`. |
| 3 | `ProcessFarmGrowth()`: `AddGoods(product, seed.Property5)` | Dùng nhầm P5 (cấp) làm số lượng sản phẩm | Số lượng = **P2**. (Ví dụ Hạt Táo 332140: P2=1 → 1 Táo; code hiện tại cho 2.) |
| 4 | `PlantBegin()`: `area.RipeDate` cho fish/ranch = `item.Property3` | Với thú/cá, P3 = **điểm trưởng thành cần** (100/200/…), không phải giây. Client sẽ hiển thị đếm ngược sai (100 giây) ở tooltip khu. | Có thể: `plantRipeDate = 0` khi chưa đủ điểm, hoặc ước lượng `số phần còn lại * 1800` giây (mỗi 30 phút 1 phần) — xem 6.2/6.21. |
| 5 | `ProcessFarmGrowth()` reset area sau 1 lần thu | Với hạt sự kiện mô tả “thu hoạch 10 lần” (334045-51) và cuốc mỏ “mỗi 30 phút 1 phần”, cơ chế 1 lần có thể không khớp thiết kế | Chưa chắc (6.2); nếu muốn đúng mô tả item → cho phép nhiều chu kỳ (giữ `isPlant`, reset `plantDate`) — nhưng client cũng chấp nhận 1 lần. |
| 6 | `ProcessMineGrowth()` chọn quặng random toàn bộ bag 8 | Không sai protocol (client chỉ cần goods), nhưng mô tả “theo cấp cuốc” gợi ý map cấp cuốc → quặng | Tùy thiết kế (6.5). |
| 7 | `SendUpdateArea()`: `plantRipeDate = area.RipeDate` và `plantDate` = `PlantDate` | Công thức client: `còn lại = R - ((now - plantDate)/1000) % R` ⇒ nếu server gửi R là "tổng thời gian" thì OK; nếu gửi R là "còn lại" thì client tính sai | Giữ `RipeDate` = **tổng chu kỳ (giây)** như code đang làm với farm/mine. ✅ |
| 8 | `SendManorInfo()` gửi `areas.Count` (6 rows gồm 5,6) | Không sai (client bỏ qua type 5/6) | Có thể giữ; hoặc chỉ gửi 4. ✅ |
| 9 | `FarmUpdatePlantData` (81/27) push 17×6 + 26 | Khớp khuyến nghị spec 4.9. ✅ (client bỏ qua 17 cho type 5/6 vì `selfFarmInfo.getObjectInfo(5/6)` chỉ lưu, không hiển thị — vô hại) | — |
| 10 | `ProcessGrowth()` chỉ push khi có thay đổi | Client không cần push định kỳ nếu không đổi. ✅ | — |
| 11 | Starter kit: 1000 tiền + 5 hạt cỏ (debug) | Chỉ là hack test — nhớ bỏ/gate trước production | — |
| 12 | `SendPetFoodCount()`: `remain = m_info == null ? 20 : BuyFoodCount`; `LoadFromDatabase` set `BuyFoodCount=30` nếu ≤0 | Nếu DB đang có 20 từ phase trước → client hiển thị giá Count=11 (480) thay vì 80 | Đảm bảo BuyFoodCount khởi tạo **30** (xem 6.7) và không reset về 20. |
| 13 | Shop 139 mua qua packet 345 (client `FarmShopBuyView`) — server `NewBuyGoodsHandler` xử lý giá theo `ShopMgr.SetItemType/AValue1..3`; dữ liệu `ShopItemList.xml` shop 139 có `APrice1="-4100"` (loại tiền nông trại đặc biệt, không nằm trong enum `Price` của client) | ❓ Chưa rõ server map -4100 thành loại tiền gì; nếu map sai → có thể **mua free** hoặc trừ nhầm Xu | Cần special-case shop 139: trừ **Cỏ Vàng** (`UserManorInfo.Money`), cộng item, cập nhật hạn mức personal-limit; thêm nhánh shop 139 trong handler 345 + trả 528 `[shopID=139][count]{[goodsID][bought]}` (sửa `ShopPersonalLimitHandler` hard-code shop 120). |
| 14 | `Farm2-PHASE2.md` ghi “chín → tự thu hoạch `Property4 × Property5`” | Số lượng phải là **P2** (xem #3); P5 là cấp khu ⇒ cần sửa doc + code | P2 × 1 (sản phẩm TemplateID = P4). |

> Lưu ý: các file server trên đang được teammate khác sửa **song song** trong lúc tài liệu này viết; bảng 7.5 là ảnh chụp tại 2026-11-08 ~12:55 — khi Lead review nên `git diff`/đọc lại trước khi kết luận.

