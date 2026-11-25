# Phó bản / Ải Viên Chinh — tái phát triển tính năng (v0.43)

> Trạng thái: **đã build + deploy + restart server lúc 2026-11-09 ~08:29** (Road.Service, Fighting.Service).
> Phạm vi: tính năng **Phó bản (ải viên chinh)** trong game Gunny 12.6 — tạo phòng phó bản, chọn
> phó bản/độ khó, đánh quái + boss, nhận thưởng (lật bài), gồm **phó bản thường**, **phó bản
> Nguyên Tố** (chỉ sát thương nguyên tố) và **phó bản Sự kiện** (sát thương theo Vỏ Vũ Khí).
>
> Tài liệu recon đầy đủ (bằng chứng client/server, file:line):
> `data/tmp/dgn-work/client-flow/REPORT.md`, `data/tmp/dgn-work/data-db/REPORT.md`,
> `data/tmp/dgn-work/proto-diff/REPORT.md`, `data/tmp/dgn-work/ref-search/REPORT.md`.


## 16. v0.51 — Fix "副本尚未开启或者副本已经关闭！" (IndexOutOfRange khi tạo game)

Log Road 11:11:17/11:11:19: `Create game error: IndexOutOfRangeException` tại
`PVEGame.SetupMissions` (line 338) từ `AntCaveNormalGame.OnCreated`. Chuỗi tiếng Trung
**"副本尚未开启或者副本已经关闭！"** là thông báo của `StartGameAction.StartGame` khi
`GameMgr.StartPVEGame` trả null (tạo game lỗi), không phải phó bản đóng. Nguyên nhân: sau khi hạ room 123 →
Dungeon (2D), `SetupMissions` rơi vào nhánh `(Dungeon||Element_PVE) && SessionId != 0` và lấy
`array[SessionId]` — mission list chỉ có 1 phần tử (`2102`) nên IndexOutOfRange (SessionId=currentFloor-1).

Đã sửa: clamp mission index về 0 nếu ngoài mảng. Build `Game.Logic.dll` `F4A20BECED259F3E`,
`Game.Server.dll` `811422999787D43A`; deploy 3 folder + restart bằng **Run.bat** lúc **11:14:09** →
`DEPLOY-AND-RESTART-DUNGEON-OK`, Error.log = 0.

(Lưu ý: log cùng lúc còn lỗi của phó bản khác — `RRCNM7404.CreateBossEffect` NRE và
`DropInventory: Copy dropId 202351`; nếu gặp lại, gửi tên phó bản để xử lý.)

## 17. v0.52 — Kho Báu Người Cá đúng cơ chế (Poseidon + Cua Rương) và luật Vỏ Vũ Khí cho phó bản sự kiện

### 17.1 Log Road trước khi sửa (11:52–11:53)

```
ERROR SimpleNpc - Can't create abrain :GameServerScript.AI.NPC.EighthKraken
ERROR SimpleNpc - SimpleNpc StartAttacking error: InvalidCastException
  at ...AutoAINPC.KillAttack(Player player)  (lặp nhiều lần)
```

Nguyên nhân: mission tạm dùng NPC placeholder (11103 Sứa Biển → script `EighthKraken` không có; 71122 Cá Lâu
La → script `AutoAINPC` giả định body là SimpleBoss). Đã thay toàn bộ bằng **NPC family gốc** trong `NPC_Info`
(script gốc thiếu class sẽ fallback `SimpleBrain`, không còn cast error).

### 17.2 Kho Báu Người Cá — đúng 4 ải theo hướng dẫn

NPC family: `2002x0y` với x=ải (0..3), y=slot; map ải 1..4 = `1504,1505,1506,1507`.

| Ải | Cơ chế đã code |
|
## 18. v0.53 — Sửa cấu trúc 4 ải × 4 độ khó (Kho Báu Người Cá) + log Road

### 18.1 Log Road (12:18–12:22)

```
ERROR SimpleNpc - Can't create abrain :GameServerScript.AI.NPC.SimpleNpcFor2002013  (4 Cua Rương)
ERROR SimpleBoss - Can't create abrain :GameServerScript.AI.NPC.SimpleBossFor2002012 (Poseidon)
```

Các class AI gốc của NPC family phó bản sự kiện không có trong source repack → server fallback
`SimpleBrain` (vẫn chạy) nhưng log lỗi. Đã **xoá Script của 240 NPC family 2001000–2005999** (dùng
SimpleBrain) ⇒ log sạch; đồng thời bỏ NPC placeholder từng gây `InvalidCastException (AutoAINPC)`.

### 18.2 Sửa cấu trúc ải (đúng bài hướng dẫn VNG)

Trước đây hiểu sai: độ khó được dùng làm ải. Đã sửa theo đúng thiết kế:

- **4 độ khó (Thường/Khó/Anh Hùng/Legend) × 4 ải**; độ khó chỉ **nhân chỉ số** quái/boss
  (×1.0 / ×1.25 / ×1.5 / ×1.75 máu), **không đổi nội dung ải**.
- Ải lấy theo `BaseRoom.currentFloor` → `PVEGame.SetupMissions` chọn đúng mission trong 4 mission của
  phó bản (`Mission_Info.Param1`=phó bản, `Param2`=ải, script `EventDungeonStageMission`).
- NPC family Kho Báu: `2002x0y` với **x = ải−1** (2002001/2002101/2002201/2002301…), map ải 1–4 =
  **1504/1505/1506/1507**.
- Nội dung từng ải (đúng hướng dẫn):
  - **Ải 1**: boss **Quái Bạch Tuộc**; hạ boss để thắng (bong bóng bảo vệ chưa mô phỏng).
  - **Ải 2**: boss **Cá Mập Biển Sâu**, Kền Kên; **bắn vỡ Cột Buồm = thua ngay**.
  - **Ải 3**: hạ 4 lính Naga (2 Chiến Sĩ + 2 Thầy Tế).
  - **Ải 4**: phá 4 **Cua Rương** → gỡ phong ấn, **Poseidon** xuất hiện; hạ Poseidon để thắng.
- **Tiến trình**: phải vượt Ải trước mới vào Ải sau (lưu theo map+ải trong `Sys_Users_Extra`);
  **3 lượt/ngày** (chỉ trừ lượt khi hoàn thành Ải 4), reset 0h.
- **Vỏ Vật Tổ (10073) không dùng được** trong phó bản sự kiện (theo đúng bài viết) — dùng vỏ khác.

### 18.3 Về hiệu ứng Vỏ Vũ Khí

- Luật sát thương Vỏ Vũ Khí đã áp (không vỏ/Vỏ Vật Tổ ⇒ sát thương = 1; có vỏ ⇒ +100% và bonus theo
  `AttackCompose`; bị đánh khi không vỏ ⇒ ×2).
- Hiệu ứng **hình ảnh** vỏ do client quyết định theo roomType sự kiện gốc (123 — chế độ 3D). Bản client
  local này không render 3D (bị màn hình đen), nên khi hạ xuống phòng 2D client dùng vũ khí thường
  (hiệu ứng VIP phi tiêu) dù server đã gửi template Vỏ Vũ Khí trong gói game start.
  → Muốn hiệu ứng vỏ đúng cần xử lý phía client (room 123 3D / tài nguyên 3D) — tách thành việc riêng.

### 18.4 Deploy

`Game.Server.dll` `B0582D1D3623114F`, `Game.Logic.dll` `2D0CA61F7B6FD7D1`; deploy Road/Fight/Center +
restart bằng **Run.bat** lúc 12:38:17 → `DEPLOY-AND-RESTART-DUNGEON-OK`, Error.log = 0.

## 19. v0.54 — Vỏ mặc định AK-7, kỹ năng Chiến Kỹ cho phó bản sự kiện

### 19.1 Log Road (12:44–12:46)

```
ERROR SimpleBoss - Can't create abrain :
ERROR SimpleNpc - Can't create abrain :
```

Do đã xoá `Script` của NPC family sự kiện (rỗng) nhưng `SimpleNpc`/`SimpleBoss` vẫn gọi
`ScriptMgr.CreateInstance("")` → log lỗi. Đã sửa: chỉ tạo AI khi `Script` không rỗng, fallback
`SimpleBrain` im lặng ⇒ log sạch.

### 19.2 Vỏ Vũ Khí mặc định AK-7

- Trong phó bản sự kiện (Pve Type 24):
  - **Không trang bị vỏ** → server coi như đang dùng **vỏ AK-7 (template 10076, pic `goldak`)**:
    hiển thị trong gói game start và cộng luật vỏ (+`WeaponShellNormalAdd`), **không còn bị ×2**.
  - **Vỏ Vật Tổ (10073)** vẫn bị cấm: sát thương gây ra = 1.
- Vỏ khác (Sấm Sét Aduka, Cung Ảo Ảnh, Đạn Bi Grub…) vẫn dùng bình thường; hiệu ứng hình ảnh phụ
  thuộc client (category 64) — server đã gửi template/pic tương ứng.

### 19.3 Kỹ năng Chiến Kỹ cho phó bản sự kiện

- Box phó bản sự kiện đặt `room.GameType = 120` ⇒ client dùng **BattleSkillManager** cho thanh kỹ năng
  trong trận (ô kỹ năng chủ động 1–3, bị động 4–6).
- GameStart push `132/8` (danh sách Chiến Kỹ đã gắn) cho phó bản Type 24; `Player.InitHourseSkillEffect`
  gộp Chiến Kỹ vào danh sách kỹ năng dùng khi đánh (PetUseKill type 2) cho cả phó bản Type 24.
- Nâng cấp Chiến Kỹ vẫn dùng **Mảnh Chiến Kỹ (12548)** qua `BattleGroundHandler` (132/6, 132/7).
- **Đảo Hải Tặc (Pve 70001, Type 21)** giữ nguyên kỹ năng cũ (không nằm trong Type 24).

### 19.4 Deploy

`Game.Server.dll` `D04B67D130DA4A98`, `Game.Logic.dll` `7F4CFAC63D7FA408`; deploy Road/Fight/Center +
restart bằng **Run.bat** lúc 12:53:57 → `DEPLOY-AND-RESTART-DUNGEON-OK`, Error.log = 0.
---|---|
| 1 (Thường) | Boss **Quái Bạch Tuộc** (slot 4) + Cua Biển/Cua Gai; hạ boss để thắng |
| 2 (Khó) | Boss **Cá Mập Biển Sâu** (slot 6) + 2 Kền Kên (slot 5); nếu phá **Cột Buồm** (slot 7) → **thua ngay** |
| 3 (Anh Hùng) | 4 lính Naga: 2 **Chiến Sĩ Naga** (slot 8) + 2 **Thầy Tế Naga** (slot 10); hạ cả 4 để thắng |
| 4 (Legend) | 4 **Cua Rương** (slot 13) — phá hết để **gỡ phong ấn**, tượng Poseidon biến mất, **Poseidon** (slot 12) xuất hiện; hạ Poseidon để thắng |

Giới hạn: **3 lần/ngày** (reset 0h, đếm bằng trial-count theo map), cần hoạt bát như phó bản thường; thắng
được tính lượt. Thêm mission 310001–310005 vào drop chung nên có thẻ thưởng.

### 17.3 Các phó bản sự kiện khác — dùng NPC gốc

| Phó bản | NPC family | Boss |
|---|---|---|
| Xưởng Hơi Nước (27) | 2001x0y | Robot Sửa Chữa (slot 4) + Gà Robot/Goblin Robot |
| Lỡ Bước Dung Nham (32) | 2003x0y | Thủy Tinh Dung Nham (4) + Rết Dung Nham Chúa (12) |
| Đối Đầu Ác Quỷ (41) | 2004x0y | Bóng Thiên Sứ (31) + Bóng Ác Quỷ (32) |
| Chiến Dịch Trời Sao (42) | 2005x0y | Gargoyle (1) + Thú Thủ Vệ/Mây Đỏ |

### 17.4 Luật Vỏ Vũ Khí (toàn bộ phó bản sự kiện)

- Phó bản Type 24 (map nhóm 1499–40056) = chế độ **Vỏ Vũ Khí**:
  - **Không trang bị vỏ (ô 20)** → sát thương gây ra = **1** (không thể phá quái/boss) — bắt buộc dùng vỏ.
  - **Có vỏ** → cộng `WeaponShellNormalAdd/1000` (config = 1000 ⇒ +100%); nếu `AttackCompose` của vỏ trùng NPC
    ID thì cộng thêm `DefendCompose/1000`.
  - Không có vỏ khi bị quái đánh → sát thương phải chịu **×2**.
- **Hiển thị**: gói game start ghi **template/Pic của Vỏ Vũ Khí** thay cho vũ khí thường khi vào phó bản sự kiện
  → nhân vật/loading hiện đúng vỏ đang trang bị (như cung ảo ảnh, đạn bi grub…).
- **Deploy**: `Game.Server.dll` `0914402C3EC62931`, `Game.Logic.dll` `DF6709A845FA14A4`,
  `Bussiness.dll` `4512875F0F1CFCE2`; deploy Road/Fight/Center + restart bằng **Run.bat** lúc 12:08:15 →
  `DEPLOY-AND-RESTART-DUNGEON-OK`, Error.log = 0.
---

## 1. Bức tranh trước khi sửa (vì sao phó bản không chơi được)

Khảo sát client thật (`WebSite\Flash\4.swf`, 7.345 script AS3) + DB + source C# cho thấy phó bản
hỏng ở cả 3 tầng, không phải chỉ 1 lỗi:

| # | Lỗi | Bằng chứng |
|---|---|---|
| 1 | **Phó bản Nguyên Tố (Pve Type=75, map 33–39)**: mỗi map chỉ cấu hình 1 cột độ khó, các cột khác NULL → `PVEGame` không tạo được game AI, đá toàn bộ người chơi (`"Can't create game ai"`). | `Pve_Info` 33–39 trước sửa; `PVEGame.cs:237-248` |
| 2 | **Map Nguyên Tố yêu cầu vé** `100000010..100000016` nhưng 7 item vé này **không tồn tại** trong `Shop_Goods` → `GameStart` luôn báo “không có vé phó bản”. | `BaseRoom.GetTicketID`; client UI không hề hiển thị vé Nguyên Tố |
| 3 | **Không có Mission_Info cho 33001..39001** → `SetupMissions` nạp `null` → NullReference, trận đấu không bắt đầu. | `PVEGame.SetupMissions`, `PVEGame.cs:741` |
| 4 | 5/7 mission Nguyên Tố (`YS35001..YS39001`) là **class rỗng 5 dòng** → không có quái/boss. | `Game.Server.GameServerScript.A\YS35xxx.cs` |
| 5 | **321 (GetPlayerSpecialProperty)** trả sai format: sub-type 4..11 trả nguyên dict mapId→count thay vì `stage→count`; thiếu case 4,5 → số lần vượt ải trên tooltip luôn sai. | `GetPlayerSpecialPropertyHandle.cs` cũ; client `PlayerManager.as:1781-1867` |
| 6 | **Không có luật “chỉ nguyên tố gây sát thương”** trong phó bản Nguyên Tố. Client không tự tính sát thương; server `Living.MakeDamage` cộng nguyên tố **lên trên** sát thương thường. | `Living.cs:1249-1256`; client `game\**` không có code damage |
| 7 | **Không có luật Vỏ Vũ Khí** cho phó bản Sự kiện. Client chỉ hiển thị tooltip (`+DefendCompose/10%`, thiếu vỏ = +100% sát thương phải chịu) còn server bỏ qua. | `GoodTip.as:1539-1563`; `Living.MakeDamage` |
| 8 | **Boss đặc biệt (roomType 123, map 27/30/32)** luôn bị chặn: `GetDungeonTicketId()` trả 0 nhưng code vẫn đòi `GetItemCount(0) > 0`. Map 30/32 còn thiếu script. | `GameStart.cs:103-124` |
| 9 | **94/18 SingleRoom** sai thứ tự field (`HardLevel` ghi cuối, client đọc thứ 3) → phòng đơn bị lệch dữ liệu. | `AbstractPacketLib.SendSingleRoomCreate` |
| 10 | Thiếu thưởng: đa số mission không có `Drop_Condiction` type 5 (copy drop) / type 4 (boss card) → lật bài rỗng. | `data-db` §6 |

---

## 2. Đã sửa — Server C# (Game.Server.dll / Game.Logic.dll / Bussiness.dll)

### 2.1 Phó bản Nguyên Tố — luật “chỉ dùng nguyên tố”
- `Game.Logic\Game.Logic.Phy.Object\Living.cs` — `MakeDamage`:
  - Khi `RoomType == Element_PVE` **và người tấn công là Player**: trả về **duy nhất** sát thương
    nguyên tố (6 hệ: Hoả/Thuỷ/Phong/Thổ/Quang/Ám):
    `Σ(elementAtk * 0.6 - target.elementDef * 0.3) * CurrentDamagePlus * CurrentShootMinus`, tối thiểu 1.
  - Sát thương thường (BaseDamage/Attack) **bị bỏ hoàn toàn** → muốn hạ quái/boss phải có Công nguyên tố
    (từ Phù Hiệu/Elf). Quái đánh người chơi vẫn dùng công thức thường (người chơi vẫn chết được).
- `Game.Logic\Game.Logic.Phy.Object\Player.cs:819-830` (có sẵn): vào phó bản Nguyên Tố, chỉ số người chơi
  được chuẩn hoá (HP 30.000, Atk/Def/Agi/Luck 4.000, BaseDamage 1.000, Guard 800) → chênh lệch duy nhất
  giữa người chơi là **nguyên tố**.
- `game.server\Game.Server.Rooms\BaseRoom.cs` — `GetTicketID`: map 33–39 **bỏ yêu cầu vé** (trả 0).
- `game.server\Game.Server.GameRoom.Handle\GameStart.cs` — thêm `CheckElementDungeonGate`:
  - **Ngày mở**: đọc `ElementCopyTimeLimited` (dạng `33-1,5|34-2,6|…`, thứ 1=Mon…7=Sun; map không có
    trong cấu hình = mở cả tuần). Đóng → báo “Phó bản Nguyên Tố này hôm nay không mở cửa”.
  - **Giới hạn/ngày/ải**: `ElementCopyPerDayMaxCount` (DB=1) so với số lần đã vượt
    `GetTrialDungeonCount(mapId, stage)`; hết → báo hết lượt.
  - **Hoạt bát**: cần `ElementDungeonCostEnergy` (=20) — dùng `MissionEnergyEmpty`.
  - Thắng trận trừ hoạt bát (đã có sẵn ở `PVEGame`), thua/thoát không trừ.
- `game.server\Game.Server.GameUtils\PlayerExtra.cs` — lưu **số lần vượt ải theo (mapId, ải/độ khó)**
  trong cột JSON sẵn có `Sys_Users_Extra.dungeonCount` bằng khoá âm `-(mapId*10+stage)`; tự **reset theo
  ngày** (khoá `-1` giữ ngày); mapId thật gửi client lọc khóa dương. Không cần đổi schema DB.
- `game.server\Game.Server.Packets.Client\GetPlayerSpecialPropertyHandle.cs` — viết lại đúng contract
  client (byte-exact theo `PlayerManager.as`):
  - sub-type 3: `int count {mapId, count}…` (chỉ khóa dương);
  - sub-type 4..11: `int count {stage, count}…` cho map lần lượt **27, 30, 32, 33, 34, 35, 36, 37**;
  - sub-type 40: trả rỗng (client không map).
- `Game.Logic\Game.Logic\PVEGame.cs`:
  - `GetScript`: nếu cột script của độ khó đang chọn rỗng thì **fallback** sang cột khác (sửa lỗi
    “chỉ 1 cột độ khó” cho mọi phó bản cũ) — phòng thủ kể cả khi DB chưa chuẩn.
  - Khi thắng: tăng số lần vượt ải (`UpdateTrialDungeonCount`) cho `Element_PVE` và `SpecialDungeon1/2/3`
    (boss đặc biệt 27/30/32) theo ải = `hardLevel` (1..4).
  - `SetupMissions`: bỏ qua mission id không tồn tại + log lỗi, không add `null` (hết NullReference).
- `Game.Logic\Game.Logic\IGamePlayer.cs` + `GamePlayer.cs` + `fighting.server\ProxyPlayer.cs`:
  thêm `GetTrialDungeonCount/UpdateTrialDungeonCount` và `ArmShell` (item CategoryID 64 ở ô 20).
- `game.server\Game.Server.GameObjects\GamePlayer.cs`: **fix bug WaterDefence** — trước đây cộng nhầm
  `waterAtt` vào WaterDefence (dòng 4846) → giờ đúng `waterDef` (ảnh hưởng Phù Hiệu nguyên tố thuỷ).

### 2.2 Phó bản Sự kiện — luật Vỏ Vũ Khí
- `Living.MakeDamage` — khi phòng là **ActivityDungeon (21)** hoặc **SpecialActivityDungeon (23)**:
  - **Người chơi tấn công quái**: có Vỏ Vũ Khí ở ô 20 → `+WeaponShellNormalAdd/1000` sát thương;
    nếu `Vỏ.AttackCompose == NPC ID` cộng thêm `Vỏ.DefendCompose/1000`.
  - **Quái tấn công người chơi**: người chơi **không có vỏ** → sát thương phải chịu **×2 (+100%)**.
- `Bussiness\Bussiness\GameProperties.cs`: thêm config
  `WeaponShellNormalAdd` (mặc định 1000 = +100% theo tooltip client `/10`),
  `ElementCopyPerDayMaxCount`, `ElementCopyTimeLimited`.
- `Server_Config` DB: `WeaponShellNormalAdd` 0 → **1000**; đã rebuild `WebSite\Request\ServerConfig.xml`
  để client hiển thị đúng tooltip.

### 2.3 Sửa lỗi khởi động phòng khác (đường đi của phó bản)
- `GameStart.cs`: boss đặc biệt (room 123 / map 27/30/32): chỉ kiểm tra vé khi `GetDungeonTicketId > 0`
  → hết bị chặn vĩnh viễn.
- `AbstractPacketLib.SendSingleRoomCreate` (94/18): ghi đúng thứ tự field client đọc:
  `ID, type, hardLevel, isPlaying, gameMode, mapId, isCrossZone, zoneId, isShowLoading`.

---

## 3. Đã sửa — Nội dung game (mission script + DB)

### 3.1 Mission script mới (C#)
`game.server\Game.Server.GameServerScript.A\` — thay 5 class rỗng bằng mission hoàn chỉnh
(mẫu theo `YS33001`/`YS34001`, chọn nhóm NPC theo độ khó `HandLevel`, boss chết = thắng, giới hạn turn):

| Mission | Map | Boss (Normal/Hard/Terror/Epic) | Quái phụ |
|---|---|---|---|
| `YS35001` | 70035 | 35004 / 35104 / 35204 / 35304 (Bolo Bala) | 35001-02 / 35101-02 / 35201-02 / 35301-02 |
| `YS36001` | 70036 | 36011 / 36111 / 36211 / 36311 (Sulan) | 36001-02 / 36101-02 / 36201-02 / 36301-02 |
| `YS37001` | 70041 | 2 rồng: 37010/37011 → 37110/37111 → 37210/37211 → 37310/37311 | 37004 + tier |
| `YS38001` | 70037 | 38018 / 38118 / 38218 / 38318 (Vòng Xoáy Hắc Ám) | 38001/38008 + tier |
| `YS39001` | 40025 (Đấu Trường Gà_Ải 1) | 34012 / 34112 / 34212 / 34312 (Gà Mái Xấu Xí) | 34005-06 + tier |

> Map 39 không có NPC 39xxx trong DB → dùng nhóm NPC Gà 34xxx (đúng chủ đề “Đấu Trường Gà”).

### 3.2 DB (`Db_Tank_All`) — file migration đầy đủ trong `data/tmp/dgn-work/impl-core/`
- `Pve_Info` 33–39: điền **đủ 6 cột script** = `YuanSuMaYi/XiaoJi/BoGu/XieShen/Long/HeiAn/YunDongHui`,
  `FightPower` số (`3000|5000|…`), `Ordering` 1..7.
- `Mission_Info`: thêm **33001, 34001, 35001, 36001, 37001, 38001, 39001** (script YS*), **4000001**
  (Vùng Đất Băng Giá, class `BFXY4000001` có sẵn), **16102/16202/16302/16402** (Kẽ Hở Không Gian 20,
  dùng `LoopholeSpaceHardBoss`/`TerrorBoss`).
- Ẩn map không thể bắt đầu (đặt `Name=''`, client tự bỏ qua): Type 4 (31, 10115, 10116), Type 6
  (MasappTry1–4 — repack không có game control gốc), Type 14 (15, 28, 50000), Type 21 (22, 29, 70001).
  Type 47/48 (15001/16001) ẩn từ trước.
- Sửa `Ordering` trùng: Type 4 = 1..11; Type 75 = 1..7.
- **Drop/thưởng**:
  - `Drop_Condiction`: 202281/202282 (Nguyên Tố 33/34) mở rộng `Para2=',1,2,3,4,'` để lật bài +
    preview đều có item; thêm 202283–202287 cho mission 35001..39001; 202290 type 4 (boss card)
    cho 33001..39001.
  - Thêm dải **202298** (type 4 boss card chung cho 43 mission có `IsBossWar`) và **202900–202903**
    (type 5 copy drop chung cho 137 mission phó bản chưa có thưởng) → **mọi trận phó bản đều có thẻ
    thưởng**, không còn màn lật bài rỗng.
- Bảng backup tự tạo: `Pve_Info_bak_20261109*`, `Mission_Info_bak_20261109*`,
  `Drop_Condiction_bak_20261109*`, `Drop_Item_bak_20261109*`, `Server_Config_bak_20261109`.

---

## 4. Cách test (user)

### Chuẩn bị
- **Thoát game và đăng nhập lại** (client tải `LoadPVEItems.xml`/`ServerConfig.xml` khi vào hall).
- Hộp thư nhân vật **test1** có sẵn thư `[DGN-TEST]` (do `_tools\dgn-send-test-items.ps1`):
  - 5 **Vỏ Vũ Khí** (10076 AK-7, 10075, 10077, 10080, 10081) — dùng cho phó bản Sự kiện.
  - 2 **Quyển Phù Hiệu Vũ Khí Siêu Việt** + 420 nguyên liệu (12231/12225/12226/12227) — dùng tạo
    Phù Hiệu tăng Công nguyên tố cho phó bản Nguyên Tố.
- Vào **Ải viên chinh → Phó bản → Tạo phòng** → chọn phó bản.

### Test 1 — Phó bản thường (hồi quy)
1. Tạo phòng **Phó bản thường** → chọn **Huyệt Ma Kiến** (hoặc Cung Điện Gà, Đại chiến rồng…).
2. Chọn độ khó (Thường/Khó/Anh Hùng) → **Bắt đầu**.
3. Đánh hết quái + boss → màn **thắng** hiện bảng lật bài: thẻ phải có item (trước đây rỗng).
4. Thoát phòng, vào lại phòng mới → kiểm tra không lỗi.

### Test 2 — Phó bản Nguyên Tố (chỉ nguyên tố gây sát thương)
1. Tạo phòng Phó bản → tab **Nguyên Tố** → chọn **Tổ Kiến Lửa** (map 33) → độ khó **Thường** → Bắt đầu.
2. **Kỳ vọng số liệu** (test1 hiện có `Sys_Users_ElementPro`: WaterAttack=4954, các hệ khác 0;
   NPC Nguyên Tố elementDef = 0):
   - Mỗi phát bắn/đạn nổ lên quái: sát thương ≈ **4954 × 0.6 ≈ 2972** (cộng/trừ buff, chí mạng riêng),
     **không** cộng sát thương thường dù Atk nhân vật cao.
   - Dùng thêm Phù Hiệu để tăng Công nguyên tố (mở **Phù Hiệu** → tạo từ Quyển + nguyên liệu) →
     sát thương tăng theo.
3. Vào phòng: panel phải hiển thị **số lần vượt ải còn lại** theo từng ải (trước đây sai/0).
4. **Giới hạn**: `ElementCopyPerDayMaxCount=1` → vượt ải xong, vào lại cùng map+ải trong ngày sẽ bị
   chặn với thông báo hết lượt; map đóng theo `ElementCopyTimeLimited` sẽ báo “hôm nay không mở cửa”.
5. Hoạt bát: cần ≥20 để vào; thắng trận trừ 20 hoạt bát.
6. Làm tương tự cho các map còn lại: **Giải Cứu Gà Con (34), Phế Tích Thành Gà (35), Bộ Lạc Tà Thần (36),
   Đại Chiến Rồng (37), Pháo Đài Hắc Ám (38), Đấu Trường Gà (39)** — tất cả phải vào trận được và có boss.

### Test 3 — Phó bản Sự kiện + Vỏ Vũ Khí
1. Nhận 5 Vỏ Vũ Khí trong hộp thư → mở **Túi → Vỏ Vũ Khí** (ô 20) → mặc 1 vỏ.
2. Tạo phòng Phó bản → tab **Sự kiện** → chọn **Lâu Đài Phép Thuật** / **Thành Tuyết** / **Rừng Ma Pháp** /
   **Ảo Ảnh Đồi Cát** / **Kẽ Hở Không Gian** (map 20) / **Xưởng Hơi Nước**… → Bắt đầu.
3. **Kỳ vọng Vỏ Vũ Khí**:
   - Có vỏ: sát thương lên quái **+100%** (`WeaponShellNormalAdd=1000`); nếu đúng chủng quái
     (`AttackCompose` của vỏ trùng NPC ID) cộng thêm `DefendCompose/1000`.
   - **Không mặc vỏ**: sát thương quái đánh lên người chơi **×2** (dễ chết hơn rõ rệt).
4. So sánh 2 trận (có/không vỏ) để thấy khác biệt; thưởng lật bài có item.

### Kiểm tra log khi test
- `C:\Gunny\Gunny12.6\Service\Service\Road\logs\GameServer.log`:
  - trận phó bản bắt đầu: `StartPVE type ...` (màu magenta).
  - lỗi cần báo lại: `Can't create game ai`, `SetupMissions`, `NullReferenceException`.
- `...\Road\logs\Error.log` phải rỗng (hiện đang rỗng sau restart).

---

## 5. Rollback

1. **DB**: restore từ các bảng backup `*_bak_20261109*` (hoặc backup full trước khi migrate —
   xem `data/tmp/dgn-work/impl-core/`), rồi chạy lại:
   - `UPDATE Pve_Info SET Name=... FROM Pve_Info_bak_20261109 ...`
   - Khôi phục `Mission_Info`, `Drop_Condiction`, `Drop_Item`, `Server_Config`.
2. **DLL**: trong `Service\Service\Road` và `...\Fight` có bản backup `*.dll.bak-<stamp>`
   (do `_tools\deploy-dungeon.ps1` tạo, stamp 20261109-082442 / 20261109-082924).
   Copy đè lại rồi restart service (chạy `_tools\deploy-dungeon.ps1` bản cũ hoặc script restart
   có UAC như trước).
3. **Client data**: `WebSite\Request\LoadPVEItems.xml` + `ServerConfig.xml` là file sinh tự động từ DB —
   chạy lại `http://127.0.0.1:88/LoadPVEItems.ashx` và `http://127.0.0.1:88/ServerConfig.ashx` sau khi
   rollback DB.

---

## 6. Hạn chế còn lại (đã ghi nhận, chưa làm trong bản này)

| Hạng mục | Trạng thái |
|---|---|
| **Cứu Hộ / HelpGame event dungeon** (client module 685, room type 29, dùng Vỏ Vũ Khí theo từng ải `NewHelpGameWeaponLimit`) | **Chưa có** server-side trong repack (không có source tham chiếu công khai). Đây là tính năng event riêng, ngoài “Phó bản” room. |
| **Ải Viễn Chinh (PlotDungeon)** — module 705, room type 92, config `TS_StoryCopy*` đã có đủ trong DB | **Chưa có** handler server. Cần implement riêng (module 705 + roomType 92 + bảng `Sys_Users_PlotDgn_Info` đang là bảng mồ côi). |
| Tab **Phó bản đặc biệt** (Type 6 — MasappTry1–4) và vài map **Phó bản Đơn** | Script gốc (`MasappTry*`) **không có** trong source repack → đã tạm ẩn để khỏi hiện map không vào được. Cần bổ sung nội dung gốc nếu muốn mở lại. |
| Nút **Làm lại** trong trận (client gửi `91/119`) | Server chưa có command handler 119 → bấm không có phản hồi. Cần làm riêng. |
| 94 handler còn thiếu: `6,12,19,21,23,24,25,32` (đổi đội, mời nhanh, reconnect, đổi chủ phòng, robot…) | Chưa làm; không chặn luồng phó bản cơ bản. |
| Nâng cấp Vỏ Vũ Khí (Vỏ/Totem/Đá) có UI client nhưng server xử lý một phần (chỉ trang bị ô 20) | Cần làm thêm nếu muốn đập/nâng vỏ. |
| `ServerConfig.xml` còn `WeaponShellMinAdd/MaxAdd = 0` | Chỉ dùng cho UI nâng cấp vỏ; chưa ảnh hưởng luật sát thương hiện tại. |

---

## 7. File/thay đổi kỹ thuật (index)

**Source C# đã sửa**
- `Full Source\Game.Logic\Game.Logic.Phy.Object\Living.cs` — luật nguyên tố + vỏ vũ khí.
- `Full Source\Game.Logic\Game.Logic\PVEGame.cs` — fallback script, đếm ải, mission null-safe.
- `Full Source\Game.Logic\Game.Logic\IGamePlayer.cs` — API mới (trial count, ArmShell).
- `Full Source\game.server\Game.Server.GameObjects\GamePlayer.cs` — implement API, fix WaterDefence.
- `Full Source\game.server\Game.Server.GameUtils\PlayerExtra.cs` — lưu/đếm số lần vượt ải theo ngày.
- `Full Source\game.server\Game.Server.Packets.Client\GetPlayerSpecialPropertyHandle.cs` — 321 đúng contract.
- `Full Source\game.server\Game.Server.Rooms\BaseRoom.cs` — bỏ vé Nguyên Tố.
- `Full Source\game.server\Game.Server.GameRoom.Handle\GameStart.cs` — gate Nguyên Tố, fix boss special.
- `Full Source\game.server\Game.Base.Packets\AbstractPacketLib.cs` — fix 94/18.
- `Full Source\Bussiness\Bussiness\GameProperties.cs` — config mới.
- `Full Source\game.server\Game.Server.GameServerScript.A\YS35001..YS39001.cs` — mission mới.
- `Full Source\fighting.server\Fighting.Server.GameObjects\ProxyPlayer.cs` — implement interface.

**DB / migration / tool**
- `data/tmp/dgn-work/impl-core/migration_dungeon.sql` — Pve_Info/Mission_Info/drop Nguyên Tố chính.
- `data/tmp/dgn-work/impl-core/fix_unicode.sql` — sửa encoding tiếng Việt.
- `data/tmp/dgn-work/impl-core/migration_generic_drops*.sql` — thưởng chung cho 137 + boss 43 mission.
- `data/tmp/dgn-work/impl-core/migration_visibility.sql` — ẩn map lỗi + mission Kẽ Hở Không Gian.
- `_tools\deploy-dungeon.ps1` — build→deploy→restart có UAC (đã dùng).
- `_tools\dgn-send-test-items.ps1` — gửi Vỏ Vũ Khí/Quyển Phù Hiệu test cho test1.

**Build/Deploy**
- `Game.Server.dll` sha256 `934584D886E45E47…`, `Game.Logic.dll`, `Bussiness.dll`, `Fighting.Server.dll`
  đã copy vào `Service\Service\Road` và `Service\Service\Fight` (backup `.bak-20261109-*`).
- Restart lúc `2026-11-09 08:29`: port 8527/9601/7539 OK, `Error.log` rỗng.

---

## 8. v0.44 — Import dữ liệu MỚI từ Gunny VNG (rip XML) + mở rộng phó bản

> Trạng thái: **đã import + merge + rebuild/deploy/restart lúc 2026-11-09 ~08:47**.
> Nguồn: các máy chủ request của NPH VNG vẫn còn sống — `https://quest.gn.zing.vn/` (và `quest2.gn.zing.vn`)
> trả XML mới hơn bản client/repack local.

### 8.1 Quy trình rip + import vào DB mới

- **Tool rip**: dùng đúng pipeline của `ToolRip`/panel (`GunTools` + job `request-sql` — tải XML từ
  `Vng:RequestSource`, giải nén zlib, map `Db_Gm.dbo.Table_Games.XmlName → table`, sinh INSERT SQL).
  Script tái sử dụng: [`data/dgn-vng/rip_xml.py`](../data/dgn-vng/rip_xml.py) (tải 16 XML) và
  [`data/dgn-vng/import_to_newdb.py`](../data/dgn-vng/import_to_newdb.py) (import vào DB mới).
- **DB mới**: `Db_Tank_All_VNG` được tạo bằng restore `C:\Gunny\DB Backup\Db_Tank_All.bak` rồi import
  dữ liệu VNG vừa rip vào các bảng:

  | XML | Bảng | Số dòng import |
  |---|---|---|
  | loadpveitems | Pve_Info | **246** |
  | loadmapsitems | Game_Map | **584** |
  | npcinfolist | NPC_Info | **3.068** |
  | serverconfig | Server_Config | **1.905** |
  | missionenergyprice | MissionEnergyPrice | 30 |
  | mapserverlist | Map_Server | 21 |
  | helpgamereward | Help_Game_Reward | 72 |
  | loaditemscategory | Shop_Goods_Categorys | 46 |
  | newtitleinfo | New_Title | 766 |

  Bản gốc trước import: `Pve_Info 226 / Game_Map 532 / NPC_Info 2.295 / Server_Config 1.726`.

### 8.2 Data MỚI liên quan ải viên chinh (phó bản) tìm thấy từ VNG

1. **5 phó bản mới hoàn toàn** (chưa có trong repack):
   - `43` **Gọi Tinh Linh Mùa** (Type 4 – Phó bản thường, 4 độ khó, template thưởng `1125745‑1125782`).
   - `41` **Đối Đầu Ác Quỷ**, `42` **Chiến Dịch Trời Sao** (Type 24 – Phó bản sự kiện, 6 độ khó).
   - `2016` **Ma Pháp Long Tộc**, `70301` **Khu Vực Bí Ẩn** (Type 21 – Phó bản sự kiện).
2. **Mô tả/template/drop-preview mới cho toàn bộ phó bản cũ** (Type 4/6/14/21/24/47/48/75): VNG có
   `Description`, `Pic`, `Simple/Normal/Hard/Terror/Epic/NightmareTemplateIds`, `BossFightNeedMoney`,
   `AdviceTips`, `Missions` đầy đủ — trước đây repack để trống. (FightPower của Type 75 vẫn là
   placeholder “Phù Hiệu” do NPH để vậy; bản test giữ số riêng để hiển thị đẹp.)
3. **Config phó bản mới** trong `Server_Config`: `DungeonCrawlFamCost`, `DungeonCrawlFamGiveItems`,
   `NewWeekCopyMaxCount`, `RelicItemWarCopyPerDayMaxCount`, `MagicLongChapterWeekCopyPassAward1/2`
   (các hệ phó bản/tuần mới của bản VNG mới hơn); cập nhật
   `ElementCopyTimeLimited` = `33-1,5|34-2,6|35-3,7|36-4,7|37-5,6|38-1,4|39-2,3`
   (map 39 mở thứ 3/4) và `ElementCopyAddEmblemValue` (9 tham số như VNG).
4. **Dữ liệu ải viễn chinh (StoryCopy – tab Ải Viễn Chinh/plot-dungeon) mới**:
   - `TS_StoryCopyChapterTemplate`: **4 chương** (Thám Hiểm Rừng Sâu, Bí Mật Nông Trại, Bóng Ma Kinh Đô,
     Bộ Lạc Gây Hấn) — bản cũ chỉ 1 chương.
   - `TS_StoryCopySectionTemplate`: **20 ải** (mission 601001‑604005) — bản cũ 5 ải.
   - `TS_StoryCopyQuest`: **224** quest (cũ 56); `TS_StoryCopyLevelUp`: **128** (cũ 32);
     `TS_FirstCopy`: 14 dòng.
5. **Dữ liệu bổ trợ khác**: +52 map (`Game_Map 532→584`), +773 NPC (`NPC_Info 2.295→3.068`),
   +chức danh mới (`New_Title 548→766`), `Help_Game_Reward` (thưởng Cứu Hộ) 72 dòng.

### 8.3 Đã áp vào DB test `Db_Tank_All` (để chơi thử ngay)

File: [`data/dgn-vng/merge_vng_into_test.sql`](../data/dgn-vng/merge_vng_into_test.sql) — backup trước
khi merge: `Pve_Info_bak_vng`, `Server_Config_bak_vng`, `Game_Map_bak_vng`, `NPC_Info_bak_vng`.

- `Pve_Info`: cập nhật mô tả/template/level/advice/boss-money cho toàn bộ phó bản cũ (giữ
  `GameScript`/`FightPower`/`Ordering` riêng của bản test); **thêm 5 phó bản mới**
  `43/41/42/2016/70301`; **mở lại các tab trước đây bị ẩn** bằng script dự phòng có sẵn:
  - Phó bản thường: **31 Thành Phố Trên Không, 10115 Lực Vô Tận, 10116 Người Bảo Vệ Cuối Cùng**
    (dùng `AntCaveNormalGame`, mission 2102).
  - **Phó bản đặc biệt** (Type 6): 8 Rừng rậm Gamazon, 9 Hoang mạc Xatara, 10 Lâu đài Daucula,
    11 Vịnh Cariga (fallback `AntCaveNormalGame` — nội dung tạm, chờ script MasappTry gốc).
  - **Phó bản Đơn** (Type 14): 15/28/50000 (fallback `DSLT3Normal`).
  - **Phó bản sự kiện** (Type 21): 22 Không Gian Thần Bí, 29 Trang Viên Đỏ, 70001 Đảo Hải Tặc
    (fallback `LoopholeSpaceNormalGame`).
  - Phó bản mới: 43 → `AntCaveNormalGame`; 41/42 → `SteamFactoryNormal` (room boss 123);
    2016/70301 → `LoopholeSpaceNormalGame` (phòng thường).
  - `Game_Map` +52 map, `NPC_Info` +773 NPC (chỉ thêm dòng thiếu, không sửa dòng cũ).
- `Server_Config`: thêm 6 key mới + cập nhật `ElementCopyTimeLimited`/`ElementCopyAddEmblemValue`.
  **Giữ `WeaponShellNormalAdd=1000`** (NPH để 0) để test cơ chế Vỏ Vũ Khí đã làm ở v0.43 — có thể đổi
  về 0 bất cứ lúc nào.
- **Client request files**: thay `WebSite\Request\TS_StoryCopy*.xml` + `TS_FirstCopy.xml` bằng bản VNG
  mới (backup `.bak-<stamp>`), regenerate `LoadPVEItems.xml`/`LoadMapsItems.xml`/`ServerConfig.xml`
  từ DB qua các handler `*.ashx`.
- **Code fix kèm**: `StartGameAction.IsPVE` thiếu `SpecialDungeon1/2/3` (47/48/**123**) → phòng boss
  đặc biệt/mới (41,42,27,30,32) trước đây không bao giờ bắt đầu; đã thêm.
- Rebuild/deploy `Game.Server.dll` (sha256 `325409CEC54DCD10…`) và restart Road/Fighting lúc ~08:47.

### 8.4 Test nhanh data mới (sau khi đăng nhập lại)

1. **Phó bản thường**: chọn thêm **Gọi Tinh Linh Mùa** (mới), **Thành Phố Trên Không**,
   **Lực Vô Tận**, **Người Bảo Vệ Cuối Cùng** — vào trận được (nội dung tạm dùng map hang kiến).
2. **Phó bản đặc biệt / Đơn / Sự kiện**: các tab giờ đã có map trở lại (8,9,10,11 / 15,28,50000 /
   22,29,70001) — vào trận được, nội dung tạm.
3. **Phó bản sự kiện mới**: **Đối Đầu Ác Quỷ (41)**, **Chiến Dịch Trời Sao (42)** — phòng boss đặc
   biệt, đã fix room type 123; **Ma Pháp Long Tộc (2016)**, **Khu Vực Bí Ẩn (70301)**.
4. **Tooltip/khung chọn phó bản**: mô tả phó bản, ảnh, LC đề cử, hạn cấp, quà boss giờ có dữ liệu VNG
   đầy đủ; tab Nguyên Tố vẫn như v0.43 (FightPower số riêng).
5. **Ải Viễn Chinh (plot-dungeon)**: dữ liệu client đã là 4 chương/20 ải mới — **nhưng battle
   PlotDungeon (module 705) vẫn chưa có server handler** (xem mục 6), nên chỉ hiển thị UI, chưa đánh được.
6. Lưu ý: 5 phó bản mới + các map mở lại đang dùng **mission/script có sẵn** (fallback) → đánh được
   quái/boss + nhận thưởng chung, nhưng nội dung/map có thể chưa khớp 100% với tên phó bản (chờ script
   gốc của NPH). Các item preview mới (vd. `1125745…`) chưa có trong `Shop_Goods` → icon quà có thể trống,
   không ảnh hưởng vào trận.

### 8.5 Rollback v0.44

- DB test: restore 4 bảng backup `*_bak_vng` (hoặc chạy lại các câu đảo ngược trong
  `merge_vng_into_test.sql`; các bảng `*_bak_vng` giữ nguyên bản trước merge).
  Xoá 5 dòng Pve mới: `DELETE FROM Pve_Info WHERE ID IN (43,41,42,2016,70301);`
- Client request files: copy lại `*.bak-<stamp>` đè lên `TS_StoryCopy*`/`TS_FirstCopy`, rồi gọi lại
  3 handler `*.ashx` để regenerate.
- Code: bản `Game.Server.dll` trước đó nằm trong `Service\Service\Road\Game.Server.dll.bak-20261109-*`;
  copy đè + restart (dùng `_tools\deploy-dungeon.ps1` hoặc script restart có UAC).

---

## 9. Quy trình test chuẩn (BẮT BUỘC mỗi lần test tính năng)

> Yêu cầu của chủ dự án: **trước khi test tính năng phải TẮT hết service, sau đó bật lại đúng thứ tự
> `Center → Fight → Road` và kiểm tra log xem có lỗi gì không.**

### 9.1 Vì sao đúng thứ tự này
- **Center.Service** (port 1593, WCF 2009) là trung tâm: quản lý server list, zone, world, notice,
  nhiều dịch vụ khác kết nối vào.
- **Fighting.Service** (port 7539) khởi động sau Center để đăng ký fight server; Road cần Fight
  để đẩy phòng PVP; Fight cần Center để báo trạng thái.
- **Road.Service** (port 8527 + GM API 9601) khởi động **cuối cùng** vì nó kết nối tới cả Center và
  Fight; nếu bật Road trước, Road sẽ log lỗi kết nối/không thấy fight server.

### 9.2 Script chuẩn
`_tools\restart-stack-dungeon-test.ps1` (chạy bằng quyền **Administrator** — UAC):

```powershell
Start-Process powershell.exe -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass',
  '-File','C:\Gunny\ToolGunny\GunnyPanel\_tools\restart-stack-dungeon-test.ps1'
```

Script làm đúng thứ tự:
1. Stop **Road → Fight → Center**, chờ đóng đủ port 8527/9601/7539/1593.
2. Start **Center** → chờ 1593 → đọc log Center + đếm lỗi.
3. Start **Fight** → chờ 7539 → đọc log Fight + đếm lỗi.
4. Start **Road** → chờ 8527 + 9601 → health `/health` → đọc log Road + `Error.log` + `FlashError.log`.
5. Ghi kết luận `RESTART-STACK-DUNGEON-TEST-OK` / `...-FAILED` vào
   `data\restart-stack-dungeon-test.log` (kèm PID, port, hash DLL, log tail từng service).

### 9.3 Kết quả lần chạy chuẩn (2026-11-09 08:50)

| Bước | Kết quả |
|---|---|
| Stop all | OK, đủ 4 port đóng (8527/9601/7539/1593) |
| Center | OK — PID 11748, port 1593, `Error.log` 0 lỗi, log `base.Start(): True` |
| Fight | OK — PID 21928, port 7539, `Error.log` 0 lỗi, tất cả Manager Init True, `SpellMgr: Loaded 26 spell handlers` |
| Road | OK — PID 22616, port 8527, GM API 9601, `Error.log` size=0, `FlashError` 0, health `ok=True players=0` |
| Soi log | **Không có dòng ERROR/Exception/FATAL nào** trong `GameServer.log` của cả 3 service sau mốc restart |
| DLL | `Game.Server` `325409CEC54DCD10`, `Game.Logic` `8F5B1B27B26CD253`, `Fighting.Server` `FBB7481358DF44A8` |
| **Kết luận** | **OK** |

### 9.4 Mỗi lần test sau này
1. Chạy `_tools\restart-stack-dungeon-test.ps1` (UAC) → đợi log `...-OK`.
2. Nếu `...-FAILED`: đọc ngay `data\restart-stack-dungeon-test.log` (có log tail từng service) +
   `Road\logs\Error.log`, `Fight\logs\Error.log`, `Center\logs\Error.log`.
3. Chỉ khi stack OK mới đăng nhập game test tính năng; lỗi phát sinh trong lúc chơi xem ở
   `Road\logs\GameServer.log` (tìm `ERROR|Exception|FATAL`).
4. Sau khi sửa code/data: build → deploy (`_tools\deploy-and-restart-dungeon.ps1`) → **script tự chạy lại
   bước 1** (stop all → copy DLL → start Center → Fight → Road → health/log check).

### 9.5 Sau mỗi lần dev phải cập nhật DLL cho ĐỦ 3 folder (Road + Fight + **Center**)

> Yêu cầu của chủ dự án: sau khi dev xong phải cập nhật lại **cả** `Service\Service\Center` và
> `Service\Service\Fight`, không chỉ `Road`.

| Folder | DLL cần cập nhật | Lý do |
|---|---|---|
| `Road` | `Game.Server.dll`, `Game.Logic.dll`, `Bussiness.dll` (+pdb) | server game/PVE chính |
| `Fight` | `Fighting.Server.dll`, `Game.Logic.dll`, `Bussiness.dll` (+pdb) | battle PVP + dùng Game.Logic/Bussiness |
| **`Center`** | **`Bussiness.dll`** (+pdb) | Center load `Bussiness.GameProperties` (Server_Config) và dùng chung Game.Logic; trước đây Center vẫn chạy `Bussiness.dll` cũ 802.816 B trong khi Road/Fight đã 862.720 B → lệch version/config |

Script chuẩn: `_tools\deploy-and-restart-dungeon.ps1` (UAC) — stop **tất cả** instance service, copy DLL
vào **Road + Fight + Center** (backup `.bak-<stamp>`), bật lại **Center → Fight → Road**, kiểm tra
port/health/`Error.log`/`GameServer.log`, ghi `data\deploy-and-restart-dungeon.log` + marker
`DEPLOY-AND-RESTART-DUNGEON-OK`.

Lần chạy chuẩn 2026-11-09 08:58: `Bussiness.dll` sha256 `AD836AE470294B13` có mặt ở **cả 3 folder**,
3 service chạy đúng 1 instance, `Error.log` = 0, `GameServer.log ERROR` = 0, health `ok=True` →
`DEPLOY-AND-RESTART-DUNGEON-OK`.

### 9.6 Lỗi thường gặp: double-click Center/Fight khi service cũ vẫn đang chạy

Hiện tượng (đã gặp lúc 08:54): có **2 process Center.Service** và **2 process Fighting.Service**;
2 cửa sổ mới (double-click từ Explorer) báo lỗi ngay khi khởi động vì **port 1593/7539 đã bị instance
cũ giữ** (`Only one usage of each socket address…`), còn Road thì không chạy (cửa sổ Road bị đóng lúc
08:54:04). Cách xử lý:

1. **Không double-click** `Center.Service.exe`/`Fighting.Service.exe` khi service đang chạy.
2. Dùng `_tools\deploy-and-restart-dungeon.ps1` hoặc `_tools\restart-stack-dungeon-test.ps1` — script
   kill **mọi** instance trước khi bật lại.
3. Nếu cần kiểm tra thủ công: `Get-Process Center.Service,Fighting.Service,Road.Service` phải trả đúng
   3 process; `Get-NetTCPConnection -State Listen | ? LocalPort -in 1593,7539,8527,9601` phải đủ 4 port,
   mỗi port do đúng 1 PID giữ.
4. Xem lỗi bind/khởi động: `Center\logs\Error.log`, `Fight\logs\Error.log` và cửa sổ console của service.

---

## 10. v0.45 — Chuẩn hoá `Run.bat` (Center→Fight→Road) + deploy đủ 3 folder + fix lỗi save nhân vật

> Trạng thái: **đã deploy lúc 2026-11-09 09:14**, cả 3 service chạy đúng 1 instance, `Error.log` sạch.

### 10.1 `Run.bat` chính thức đã được nâng cấp (nguyên nhân lỗi Center/Fight)

Chủ dự án chạy `C:\Gunny\Gunny12.6\Service\Service\Run.bat` mỗi lần bật server. Bản **cũ** chỉ
`start` Center → Fight → Road liên tiếp, **không tắt instance cũ, không chờ port** → nếu chạy lần 2 khi
server đang chạy sẽ tạo **2 Center + 2 Fight**, 2 cửa sổ mới báo lỗi bind port 1593/7539
(`Only one usage of each socket address…`), Road có thể chết/không lên — đúng hiện tượng "lỗi ở Center
và Fight" lúc 08:54.

Bản **mới** (`Run.bat` + `run-stack.ps1` cùng folder):
1. Tự xin quyền Administrator (UAC).
2. **Stop TẤT CẢ** instance `Road.Service`/`Fighting.Service`/`Center.Service` (kể cả bản double-click trùng),
   chờ đóng đủ port 8527/9601/7539/1593.
3. Start **Center** (chờ 1593) → **Fight** (chờ 7539) → **Road** (chờ 8527 + 9601), mỗi bước có timeout.
4. Kiểm tra **đúng 3 process**, đủ **4 port**, `Error.log` 3 service sạch, health API `ok=True`.
5. Ghi log `Service\Service\run-stack.log` + in `RUN-STACK-OK` (lỗi in `RUN-STACK-FAILED` kèm tail log).
6. Hỗ trợ `Run.bat --no-pause` để script khác gọi (deploy script dùng tham số này).

Đã test: 09:03, 09:12, 09:14 — mỗi lần ~40–43s, stop sạch instance cũ, start đúng thứ tự, OK.

### 10.2 Deploy sau mỗi lần dev: cập nhật DLL cho ĐỦ 3 folder Road + Fight + Center

Yêu cầu chủ dự án: **sau khi dev xong phải cập nhật cả `Service\Service\Center` và
`Service\Service\Fight`, không chỉ Road**. Script chuẩn:
[`_tools/deploy-and-restart-dungeon.ps1`](../_tools/deploy-and-restart-dungeon.ps1) (UAC) —
stop mọi instance → copy DLL (backup `.bak-<stamp>`) → **gọi `Run.bat --no-pause`** → verify
(3 process, 4 port, health, `Error.log`, chỉ đếm ERROR trong `GameServer.log` **sau mốc restart**;
lỗi cũ không làm fail) → marker `DEPLOY-AND-RESTART-DUNGEON-OK` trong
`data\deploy-and-restart-dungeon.log`.

| Folder | DLL (lần 09:14) | sha256 |
|---|---|---|
| **Road** | Game.Server.dll / Game.Logic.dll / Bussiness.dll | `28033D374929B187` / `2732AC3537B50E94` / `1B9E0D69056AC7D1` |
| **Fight** | Fighting.Server.dll / Game.Logic.dll / Bussiness.dll | `FBB7481358DF44A8` / `2732AC3537B50E94` / `1B9E0D69056AC7D1` |
| **Center** | Bussiness.dll | `1B9E0D69056AC7D1` |

> Fight/Center dùng chung `Game.Logic.dll`/`Bussiness.dll` copy từ build `game.server` để **cả 3 folder
> cùng hash** (tránh lệch version như trước: Center từng chạy `Bussiness.dll` 802.816 B trong khi
> Road/Fight đã 862.720 B).

### 10.3 Fix lỗi (có sẵn từ repack) làm hỏng save nhân vật

Khi player online lúc deploy, log có:
`ERROR - GamePlayer - Error saving all datas of playerID: 1` + NRE tại
`GamePlayer.SavePlayerInfoToDatabase` (loop `PlayerCharacter.ActiveCards`) và 3 NRE save phụ
(`updateExplorerManualInfo/Pages/Debris`, `SP_Insert_Update_HappyRechargeData`).

Nguyên nhân: repack thiếu dữ liệu/SP của các hệ mới (ExplorerManual…) và một số collection của
`PlayerCharacter` bị `null` (load một phần) → `SaveIntoDatabase` ném NRE, **nhân vật không lưu được**.
Đã fix an toàn:
- `GamePlayer.SavePlayerInfoToDatabase`: chuẩn hoá `ActiveCards/HonorRecords/ElfInfo/SkillBooks/ElfEquip/
  ListBoxs/EmblemInfos/ScrollInfos` về list rỗng nếu null trước khi save.
- `Bussiness.PlayerBussiness`: null-guard cho `updateExplorerManualInfo`/`...InfoPages`/`...InfoDebris`
  và `UpdateHappyRechargeData`.
- `explorerManualInfo` sẽ không còn NRE nếu chưa có dữ liệu; (hệ Explorer Manual vẫn chưa có bảng/SP
  trong repack — xem mục hạn chế, nhưng không còn làm hỏng save của nhân vật).

Sau deploy 09:14: `Error.log` cả 3 service = 0, không còn `Error saving all datas`.

### 10.4 Lưu ý khi test (quan trọng)

- **Đừng bật `Run.bat` lần 2 khi server đang chạy** — bản mới tự tắt instance cũ nên an toàn, nhưng nếu
  chạy bằng cách double-click trực tiếp `Center.Service.exe`/`Fighting.Service.exe` thì lại bị trùng port.
  Cứ dùng `Run.bat` (hoặc deploy script) là chuẩn.
- Trước khi mình deploy/restart, nên **thoát game** để không bị ngắt phiên giữa lúc test (các lần restart
  09:03/09:05/09:12/09:14 đã ngắt phiên test1 đang online).
- Server hiện tại (09:14) đã load dữ liệu VNG + fix save; đăng nhập lại để test phó bản.

---

## 11. v0.46 — Fix lỗi boss AI (`Can't create abrain`) phát hiện khi check log Road

### 11.1 Log Road lúc test phó bản (09:14 → 09:38)

`Road\logs\Error.log`:

```
2026-11-09 09:17:36,475 - ERROR - Game.Logic.Phy.Object.SimpleBoss
  - Can't create abrain :GameServerScript.AI.NPC.NightmareQueenAntAi
```

- Đây là **boss Kiến Chúa độ khó "Ác Mộng"** (map Huyệt Ma Kiến) — server không tạo được AI
  `NightmareQueenAntAi`, rơi về AI đơn giản (`SimpleBrain`) → boss **không dùng kỹ năng/không di chuyển
  thông minh**; trận vẫn chạy nhưng sai hành vi.
- Lỗi này **có sẵn từ repack**, không phải do dữ liệu VNG: log cũ 2022‑2023 cũng có
  `CryptBoss4`, `QXBoss70001`, `InsectOne`, `WorldCupBossFor14202`, `FiveNormalFourBoss`…
- Nguyên nhân: `NPC_Info.Script` lưu **tên thiếu prefix namespace** (`GameServerScript.AI.NPC.X`),
  trong khi class thật là `Game.Server.GameServerScript.AI.NPC.X`; `ScriptMgr.CreateInstance` tra đúng
  tên tuyệt đối nên không thấy → fallback AI đơn giản. `Mission_Info.Script` thì có prefix đầy đủ nên
  nhiệm vụ vẫn chạy bình thường.

Các log khác trong Road (09:14→09:38): WARN load cấu hình Farm/Elf/Pet, `FightPower_Show` (thông tin),
`StartGameAction 128ms` (chậm nhẹ, không lỗi), **không còn lỗi save nhân vật** (fix v0.45 hiệu quả).
`FlashError.log` có 3 dòng client log (`test1` … `150239`, `262020`) — là log phía client, cần biết lúc đó
bạn thao tác gì; nếu lặp lại khi mở phó bản cụ thể sẽ điều tra tiếp.

### 11.2 Đã fix: `ScriptMgr` tự thêm prefix namespace

- File `Full Source\game.base\Game.Server.Managers\ScriptMgr.cs`:
  thêm `ResolveType(assembly, name)` — thử lần lượt `name`, `"Game.Server." + name`, `"Game.Logic." + name`;
  dùng cho `GetType`, `CreateInstance(name)`, `CreateInstance(name, baseType)`.
- Kết quả: các boss có Script thiếu prefix (`NightmareQueenAntAi`, `SimpleQueenAntAi`,
  `CryptBoss4`, `QXBoss70001`, `InsectOne`, …) sẽ tìm thấy class AI → boss dùng kỹ năng/brith point đúng
  như thiết kế.
- Vẫn fallback AI đơn giản cho các tên **không tồn tại thật** trong source repack
  (`NullAi`, `QXBoss70099`, `WorldCupBossFor14202`…) — hạn chế nội dung, không phải lỗi code.
- Build `Game.Base.dll` (sha256 `572D1B7977F6ABDA`) và deploy **cả 3 folder Road/Fight/Center**
  (vì Center/Fight cũng reference `Game.Base.dll`); restart stack lúc **09:43:29** →
  `RUN-STACK-OK` + `DEPLOY-AND-RESTART-DUNGEON-OK`, 3 process, đủ port, `Error.log` = 0, health OK.

### 11.3 Test lại

1. Đăng nhập lại (lần restart 09:43 đã ngắt phiên).
2. Vào **Phó bản thường → Huyệt Ma Kiến → độ khó Ác Mộng** (hoặc dungeon có boss Kiến Chúa/Nightmare).
3. Kiểm tra boss có di chuyển/dùng kỹ năng (không còn đứng yên như AI đơn giản) và `Road\logs\Error.log`
   **không còn** dòng `Can't create abrain`.
4. Test tiếp các phó bản mới; nếu thấy dòng `Can't create abrain :…` với tên class khác, gửi lại log để
   mình kiểm tra xem class đó có trong source không.

---

## 12. v0.47 — Chiến Kỹ (Battle Skill) trong phó bản sự kiện (roomType 123)

> Trạng thái: **đã code + build + deploy lúc 2026-11-09 10:10** (Center→Fight→Road OK, `Error.log` sạch).

### 12.1 Client đã có sẵn gì (không cần sửa client)

- Cửa sổ **Chiến kỹ** (`battleSkill` package): tab KN chủ động / KN bị động + 3 ô chủ động (Z/X/C)
  và 3 ô bị động; nút “Mở kỹ năng” / “Thăng cấp kỹ năng”; hiển thị nguyên liệu **Mảnh Chiến Kỹ**.
- Gói tin client ↔ server (opcode **132**):
  - `132/8` client hỏi thông tin → server trả `int count` + từng cặp `(skillId, slot)` (slot 0 = chưa gắn).
  - `132/6` client gửi `int skillId` = **mở khóa nếu chưa có / nâng cấp nếu đã có** → server trả
    `int newSkillId` (skill mới sau khi lên cấp).
  - `132/7` client gửi `(skillId, slot)` để gắn/bỏ kỹ năng → server trả lại đúng cặp đó.
- Dữ liệu mẫu có sẵn trong `WebSite\Request`: `fairbattleskillgettemplate.xml` (cây kỹ năng
  ID/Level/NextID/SkillID/Type — 38 dòng kỹ năng, 13 dòng có 10 cấp) và
  `fairbattleskillmaterialtemplate.xml` (mỗi skill → `SkillDetail TemplateID=12548 Count=50 OrderID`).
- Trong battle: ô kỹ năng Z/X/C dùng `BattleSkillManager.curUseSkillList` (slot 1..3) khi roomType
  **123** (phó bản sự kiện boss 27/30/32/41/42) — client gửi dùng kỹ năng qua opcode **91/144**
  (`sendPetSkill(skillId, 2)`), đây là **horse skill** phía server.

### 12.2 Phần server đã bổ sung

| Thành phần | Mô tả |
|---|---|
| DB | `Db_Tank.Sys_Users_BattleSkill(UserID, SkillID, Slot)` + 3 SP: `SP_Users_BattleSkill_Get`, `_Save`, `_Upgrade` |
| Bussiness | `BattleskillBussiness` (đọc/lưu/nâng cấp) |
| `Game.Server.BattleSkill` | `BattleSkillConfigMgr` (đọc 2 XML request, zlib), `BattleSkillOut` (gửi 132/8,132/6,132/7 + xử lý mở/nâng cấp/trang bị) |
| `GamePlayer` | load chiến kỹ khi login; `BattleSkills` (skillId→slot), `BattleSkillEquip` (slot 1..6); cache cập nhật khi mở/nâng/gắn |
| `BattleGroundHandler` | thay stub bằng xử lý thật cho `132/6` (mở/nâng cấp, trừ **Mảnh Chiến Kỹ** 12548), `132/7` (gắn slot 1..3 chủ động / 4..6 bị động), `132/8` (danh sách) |
| `GameStart` | khi vào trận **roomType 123** gửi `132/8` cho từng người → thanh kỹ năng Z/X/C có dữ liệu |
| `BaseGame` | gói game start ghi đúng `battleSkillEquipList` (trước đây luôn ghi 0) |
| `Player` (Game.Logic) | với roomType 123 dùng **battle skill** thay horse skill để nạp `_horseSkillCd` → `PetUseKill(type=2)` dùng đúng kỹ năng, đúng hồi chiêu/số lần/năng lượng |
| `IGamePlayer`/`ProxyPlayer` | thêm `BattleSkillEquip` (Proxy trả rỗng cho PVP) |

Quy tắc server áp dụng (khớp client):
- Room 123 chỉ cho phép kỹ năng có `MountSkillTemplate.GameType == 0` (client cũng disable các skill khác).
- Kỹ năng chủ động (`Probability == -1`) gắn slot 1..3; bị động (`Probability == 10000`) gắn slot 4..6.
- Mở khóa/nâng cấp tiêu hao theo `fairbattleskillmaterialtemplate.xml` (hiện là **50 Mảnh Chiến Kỹ/lần**);
  cây cấp: `...30400(Lv1) → 30401(Lv2) → ... → 30409(Lv10)`, hết `NextID = 0` là cấp tối đa.

### 12.3 Deploy

- Build lại: `Bussiness.dll` `32B7A4A34CA95F9B`, `Game.Logic.dll` `8BF7CE1BBCD9C02D`,
  `Game.Server.dll` `295C89561A29D399`, `Fighting.Server.dll` `E7599167211B8E32`.
- Deploy **đủ 3 folder** (Road/Fight/Center) + `Run.bat` restart lúc **10:10:13** →
  `DEPLOY-AND-RESTART-DUNGEON-OK`, 3 process, đủ port 1593/7539/8527/9601, `Error.log` = 0, health OK.
- Đã gửi mail test `[DGN-TEST] Manh Chien Ky 12548` (2000 mảnh) cho **user 1** bằng
  [`_tools/dgn-send-battleskill-items.ps1`](../_tools/dgn-send-battleskill-items.ps1).

### 12.4 Test

1. Đăng nhập **test1** → nhận mail `[DGN-TEST] Manh Chien Ky 12548` (2000 mảnh).
2. Vào phòng **phó bản sự kiện** (roomType 123: boss 27/30/32 hoặc 41/42) → bấm nút **Chiến kỹ**:
   - Danh sách 38 kỹ năng, nhóm chủ động/bị động hiển thị đúng; kỹ năng `GameType != 0` bị mờ
     (“Cấm dùng”) đúng như client.
   - Bấm **Mở kỹ năng** → mất 50 Mảnh Chiến Kỹ, skill sáng lên (Lv1), nhận thông báo “Đã mở khóa kỹ năng!”.
   - Bấm **Thăng cấp** → Lv tăng 1 (tối đa Lv10 với các dòng 10 cấp), mất 50 mảnh/lần.
   - Gắn kỹ năng chủ động vào ô Z/X/C (slot 1-3), bị động vào ô 4-6; gắn trùng ô sẽ tự gỡ ô cũ.
3. Vào trận: thanh 3 ô Z/X/C hiển thị skill đã gắn; bấm dùng (đủ năng lượng) → server chạy đúng hiệu ứng
   horse-skill (hồi máu/đổi bóng/buff…), có hồi chiêu & giới hạn số lần theo template.
4. Log kiểm tra: `Road\logs\GameServer.log` có dòng
   `BattleSkillConfigMgr: loaded 155 skill nodes, 155 material lists` (lần đầu mở panel/vào trận room 123);
   `Error.log` không có lỗi.

### 12.5 Hạn chế / lưu ý

- Mới gắn vào phó bản sự kiện **roomType 123** (đúng phạm vi bạn mô tả). Các mode khác dùng battle skill
  (room 120/82/151, gameMode 120 — đấu trường công bằng) vẫn như cũ, chưa mở rộng.
- Kỹ năng **bị động** đã lưu/gắn được và client hiển thị; phần cộng chỉ số bị động phụ thuộc
  `MountSkillTemplate.ElementIDs` do server horse-skill đã có — cần test thực tế từng skill.
- Nếu client bấm mở/nâng cấp nhưng báo lỗi, xem log Road (mục 12.4) và gửi lại để kiểm tra.
---

## 13. v0.48 — Fix "Phó bản bạn chọn chưa cập nhật." (không vào được phó bản sự kiện)

### 13.1 Nguyên nhân (từ log Road lúc bạn test)

`Road\logs\Error.log`:

```
2026-11-09 10:17:08 ERROR - Game.Logic.PVEGame - Can't create game mission ai :Game.Server.GameServerScript.AI.Messions.SF200101
2026-11-09 10:18:08 ... (lặp lại)
```

- Phó bản **sự kiện roomType 123** (Kho Báu Người Cá map 30, Xưởng Hơi Nước 27, Hang Dung Nham 32,
  Đối Đầu Ác Quỷ 41, Chiến Dịch Trời Sao 42) trước đây trỏ tới game control `SteamFactoryNormal`.
- `SteamFactoryNormal` gọi `SetupMissions("200101")`; `Mission_Info` 200101 trỏ tới class
  `Game.Server.GameServerScript.AI.Messions.**SF200101**` — **class này không có trong source repack**
  nên `PrepareNewSession` không tạo được mission AI → server gọi `ResetRoom(false,"CreateInstanceFail")`
  → client hiện đúng dòng **“Phó bản bạn chọn chưa cập nhật.”**.

Đồng thời rà toàn bộ phó bản (49 dòng Pve đang hiển thị) phát hiện thêm các nhóm lỗi tương tự:
- Type 24 (27/30/32/41/42): thiếu `SF200101` (như trên).
- Type 21 **Rừng Ma Pháp (23)**: thiếu mission class `RungMaPhapS/N/H71135..71139`.
- Type 14 **Dũng Sĩ Luyện Tập 2/3/5/6 (16,17,25,26)**: thiếu `DSLT2*/DSLT3Hard/DSLT5*/DSLT6*`.

### 13.2 Đã sửa (DB-only, không cần build)

File migration: `data/tmp/dgn-work/impl-core/fix_dungeon_scripts.sql` (backup `Pve_Info_bak_dgn_scripts`):

| Phó bản | Script mới (cả 6 cột độ khó) | Mission thực tế |
|---|---|---|
| 27,30,32,41,42 (Type 24 – sự kiện) | `LoopholeSpaceNormalGame` | 16202 → `LoopholeSpaceHardBoss` (map 1469, 2 boss) |
| 23 (Type 21 – Rừng Ma Pháp) | `LoopholeSpaceNormalGame` | 16202 |
| 16,17,25,26 (Type 14 – Dũng Sĩ Luyện Tập) | `DSLT3Normal` | 71048 |

→ Mọi phó bản đang hiển thị đều trỏ tới **class + Mission_Info có thật** (đã kiểm tra tự động 49 dòng Pve:
42 lỗi cũ → 0). Nội dung/map của các phó bản bị thiếu script gốc sẽ dùng tạm map/mission của
LoopholeSpace/DSLT (đánh được quái + boss + nhận thưởng), chờ bổ sung script gốc của NPH.

### 13.3 Deploy

- Chỉ đổi DB → restart stack để `PveInfoMgr` nạp lại: **10:22:22** (`RESTART-STACK-DUNGEON-TEST-OK`,
  Center→Fight→Road, 3 process, `Error.log` = 0, health OK). DLL giữ nguyên bản v0.47.
- Đăng nhập lại, vào **Phó bản sự kiện → Kho Báu Người Cá** (hoặc 27/32/41/42) → Bắt đầu: phải vào trận
  bình thường (map Kẽ Hở Không Gian, 2 boss), không còn dòng “chưa cập nhật”.
- Nếu còn lỗi: gửi `Road\logs\Error.log` — tìm `Can't create game mission ai` / `Can't create game ai`.
---

## 14. v0.49 — Đọc log Road sau test + sửa 2 lỗi (Chiến Kỹ & boss Loophole) + restart bằng Run.bat

### 14.1 Log Road lúc bạn test (10:22 → 10:29)

`Road\logs\Error.log`:

```
10:25:28 ERROR - Bussiness.BattleSkillBussiness - GetBattleSkills
  System.InvalidCastException  at GetSkills(Int32 userId) line 29
10:28:56 ERROR - Game.Logic.PVEGame - game ai script Playing error:NullReferenceException
  at Game.Server.GameServerScript.AI.Messions.LoopholeSpaceHardBoss.OnStartGame() line 69
```

- **Lỗi 1 (Chiến Kỹ)**: cột `Sys_Users_BattleSkill.Slot` là `tinyint` nhưng code ép `(int)reader["Slot"]`
  → `InvalidCastException` → không load được danh sách chiến kỹ. Đã sửa `Convert.ToInt32(...)` cho cả
  `SkillID`/`Slot` (Bussiness).
- **Lỗi 2 (vào trận)**: lớp mission `LoopholeSpaceHardBoss` (đang dùng tạm cho nhóm phó bản Type 24) có
  **lỗi sẵn trong repack**: `m_moive`/`m_front` không bao giờ được khởi tạo nhưng `OnStartGame` vẫn gọi
  `PlayMovie` → NullReferenceException ngay khi bắt đầu trận (đây là lý do “Phó bản bạn chọn chưa cập nhật”
  vẫn còn dù đã thay script). Đã thêm null-guard cho `m_moive`/`m_front` (Hard + Terror boss) và guard
  `m_secondboss != null` trong `OnGameOver` (tránh NRE khi hết turn trước khi boss 2 xuất hiện).
- Log khác: `BattleSkillConfigMgr: loaded 155 skill nodes, 155 material lists` (OK) và
  `StartGameAction 191ms` (chậm nhẹ, không lỗi).

### 14.2 Restart LUÔN bằng `Run.bat` (yêu cầu của chủ dự án)

- `_tools\restart-stack-dungeon-test.ps1` đã được viết lại: kill mọi instance → **gọi `Run.bat --no-pause`**
  (Run.bat tự stop/start Center→Fight→Road + check) → xác nhận lại port/health/Error.log → ghi
  `data\restart-stack-dungeon-test.log`.
- `_tools\deploy-and-restart-dungeon.ps1` (deploy DLL 3 folder) cũng **gọi `Run.bat --no-pause`** để bật lại.
- Từ nay mọi lần restart/deploy đều đi qua `Run.bat` — không start trực tiếp `*.Service.exe`.

### 14.3 Deploy

- Build lại `Bussiness.dll` `4512875F0F1CFCE2`, `Game.Server.dll` `4C2B9797D62FF1F3`,
  `Game.Logic.dll` `FF3C63127046C21E`; deploy Road/Fight/Center + `Run.bat` restart lúc **10:31:56** →
  `DEPLOY-AND-RESTART-DUNGEON-OK`, 3 process, `Error.log` = 0, health OK.
- Đăng nhập lại → test lại **Kho Báu Người Cá / Xưởng Hơi Nước / Hang Dung Nham / Đối Đầu Ác Quỷ /
  Chiến Dịch Trời Sao** (vào trận được, boss 2 giai đoạn) và panel **Chiến kỹ** (load danh sách OK).
- Nếu còn lỗi: gửi `Road\logs\Error.log` (tìm `Can't create`, `NullReference`, `BattleSkill`).
---

## 15. v0.50 — Fix màn hình đen khi vào phó bản sự kiện (Kho Báu Người Cá)

### 15.1 Dấu vết

- Server log sạch (`Road\logs\Error.log` = 0) — trận **đã bắt đầu** bình thường; lỗi nằm ở **client loading**:
  `FlashError.log` ghi `Client log: …test1…352202` đúng lúc vào phó bản.
- Client tải tài nguyên từ CDN (`gunny.vcdn.vn`). Kiểm tra CDN:
  - Map 1469: `image/map/1469/back.jpg` **200**, `dead.png` **200** → map OK.
  - Nhưng các file load của mission cũ `LoopholeSpaceHardBoss`:
    `image/bomb/blastOut/blastOut51.swf` → **404**, `image/game/thing/BossBornBgAsset.swf` → **404**.
  → Loading screen phải chờ các file này nên **treo ở màn hình đen**, không vào được trận.
- Ngoài ra trước đó còn 2 lỗi server đã fix ở v0.49: `BattleSkillBussiness.GetSkills` cast `tinyint`
  và `LoopholeSpaceHardBoss.OnStartGame` NRE (`m_moive/m_front`).

### 15.2 Đã sửa

1. **Không dùng fallback Loophole cho phó bản sự kiện nữa**: các dòng 23,27,30,32,41,42 chuyển sang
   `AntCaveNormalGame` (mission 2102 → `DCNM2102`, map **1121 Sà huyệt 1**) — đây là mission/map **đang
   dùng cho Huyệt Ma Kiến** nên tài nguyên client chắc chắn có.
2. **Client không phải tải tài nguyên chiến kỹ khi vào trận**: bỏ việc thay `HorseSkillEquip` bằng battle-skill;
   giữ nguyên danh sách horse-skill cho client preload (an toàn). Server vẫn **merge chiến kỹ vào
   `_horseSkillCd`** khi roomType 123 → dùng Z/X/C trong trận vẫn hoạt động.
3. Build lại `Game.Logic.dll` `D6DCBD4CC7A23ECC`, `Game.Server.dll` `4033780C24970AE8`; deploy Road/Fight/Center
   + restart **bằng `Run.bat`** lúc **10:44:09** → `DEPLOY-AND-RESTART-DUNGEON-OK`, `Error.log` = 0.

### 15.3 Test

- Đăng nhập lại → Phó bản sự kiện → **Kho Báu Người Cá** (và 27/32/41/42, Rừng Ma Pháp) → Bắt đầu:
  phải vào map Sà huyệt 1 (giống Huyệt Ma Kiến), có boss, không còn màn hình đen.
- Nếu vẫn đen: mở DevTools → tab **Console** chụp các dòng đỏ (lỗi ActionScript) + tab **Network** lọc
  `404` gửi mình; đồng thời gửi `Road\logs\FlashError.log`.
