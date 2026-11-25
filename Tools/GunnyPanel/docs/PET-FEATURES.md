# PET 12.6 â€” Má»Ÿ Ã´ pet (68/10) + Thá»©c tá»‰nh trang bá»‹ pet (68/64)

> Tráº¡ng thÃ¡i: **ÄÃƒ CODE + BUILD + DEPLOY DLL + MIGRATE DB + RESTART Road.Service** (2026-11-08 16:35).
> Chá» test trong game báº±ng tÃ i khoáº£n `test1`.

## 1. Hiá»‡n tráº¡ng trÆ°á»›c khi sá»­a

- Client Gunny 12.6 cÃ³ sáºµn 2 cá»­a sá»•: **Há»‡ thá»‘ng pet** (Ã´ pet bá»‹ khoÃ¡) vÃ  **ÄÃ o Táº¡o Pet**
  (Thá»©c Tá»‰nh / Thá»©c Tá»‰nh SÃ¢u / Dung Há»£p).
- Server `Game.Server.dll`:
  - `PetPackageType` khÃ´ng cÃ³ handler code **10** (má»Ÿ Ã´ pet) â†’ client báº¥m Ã´ khoÃ¡ khÃ´ng pháº£n há»“i.
  - KhÃ´ng push packet 68/10 lÃºc login â†’ client máº·c Ä‘á»‹nh `unlockedCellNum=0` â†’ **má»i Ã´ ngoÃ i Ä‘á»u khoÃ¡**.
  - CÃ³ handler cÅ© `PetAwaken` code **37** theo protocol cÅ©, nhÆ°ng UI má»›i cá»§a 12.6 gá»­i code **64** â†’
    `LoadCommandHandler code024: 64` vÃ  khÃ´ng cÃ³ gÃ¬ xáº£y ra (tÃ­nh nÄƒng thá»©c tá»‰nh cháº¿t).
  - Báº£ng giÃ¡ má»Ÿ Ã´ cÃ³ sáºµn trong `WebSite\Request\TS_UpgradeTemplate.xml` (`Types="103"`),
    nhÆ°ng server khÃ´ng Ä‘á»c.
  - Sá»‘ Ã´ pet: client hiá»ƒn thá»‹ 50 Ã´ (10 trang Ã— 5); server `PetInventory` capability 20.

## 2. Thiáº¿t káº¿ chá»‘t

### 2.1 Sá»‘ Ã´ pet â€” dÃ¹ng `Sys_Users_Detail.SlotPet`

Server 12.6 Ä‘Ã£ cÃ³ sáºµn cá»™t `SlotPet` (`PlayerInfo.SlotPet`, load trong `PlayerBussiness`,
save trong `UpdatePlayer`). ÄÃ¢y chÃ­nh lÃ  chá»— chuáº©n Ä‘á»ƒ lÆ°u sá»‘ Ã´ pet Ä‘Ã£ má»Ÿ â€” **khÃ´ng thÃªm báº£ng má»›i**.

- `PetInventory.UnlockedCellCount` = `PlayerCharacter.SlotPet`, káº¹p trong
  `[PetExtConfigMgr.FreeCellCount, PetExtConfigMgr.MaxPetCellCount]`.
- Login: `PetInventory.NormalizeSlotPet()` Ä‘áº£m báº£o `SlotPet >= FreeCellCount`.
- Má»Ÿ Ã´: `PetInventory.UnlockPetCells(times)` tÄƒng `SlotPet`, gá»i `GamePlayer.UpdatePlayer()` Ä‘á»ƒ ghi DB ngay.
- DB migration `data/pet-work/lead/pet_slotpet_migrate.sql`:
  - `UPDATE Sys_Users_Detail SET SlotPet = 8` cho 5 user test (pet cÅ© Ä‘á»u á»Ÿ place <= 5, khÃ´ng máº¥t pet).
  - Default cho user má»›i = 8.
  - Drop báº£ng/SP `Sys_Users_PetCellUnlock` cá»§a phÆ°Æ¡ng Ã¡n cÅ© (khÃ´ng cÃ²n dÃ¹ng).

### 2.2 GiÃ¡ má»Ÿ Ã´ â€” Ä‘á»c chung file client

Client tÃ­nh giÃ¡ báº±ng `TemplateManager._petUnlockCell` â€” náº¡p tá»«
`WebSite\Request\TS_UpgradeTemplate.xml`, cÃ¡c dÃ²ng `Types="103"`:

| Grades (priceIndex) | Data (Xu) |
|---|---|
| 1..15 | 600, 900, 1200, 1500, 1800, 2100, 2400, 2700, 3000, 3300, 3600, 3900, 4200, 4500, 4800 |
| 16..56 | 4800 (má»—i Ã´) |

CÃ´ng thá»©c client (`PetBagModel.getMoneyNeeded`):
`priceIndex báº¯t Ä‘áº§u = unlockedCellNum - MaxFreePetCount + 1`, cá»™ng dá»“n `times` Ã´.
Server `PetExtConfigMgr.GetUnlockCost(unlockedCount, times)` dÃ¹ng **Ä‘Ãºng cÃ´ng thá»©c** nÃ y
nÃªn sá»‘ Xu hiá»ƒn thá»‹ trÃªn dialog = sá»‘ Xu server trá»«.

Vá»›i `MaxFreePetCount=8`: Ã´ Ä‘áº§u tiÃªn pháº£i tráº£ (Ã´ thá»© 9) = 600 Xu; má»Ÿ tá»›i Ã´ 50 háº¿t tá»•ng 170.100 Xu.

`PetCellUnlockPrice.xml` (loader `createPetCellUnlockPriceLoader` trong client) lÃ  **dead code** â€”
khÃ´ng Ä‘Æ°á»£c gá»i trong toÃ n bá»™ decompile, nÃªn **khÃ´ng cáº§n táº¡o file nÃ y**.

### 2.3 Packet 68/10 â€” má»Ÿ Ã´ pet

```
Client -> Server:  [68][byte 10][bool isBind][int times]
Server -> Client:  [68][byte 10][int unlockedCellNum]
```
- `isBind=true` â†’ tráº£ báº±ng **Xu khÃ³a** (`PlayerCharacter.DDTMoney`), `false` â†’ **Xu** (`Money`).
  Server kiá»ƒm tra Ä‘á»§ tiá»n trÆ°á»›c khi gá»i `MoneyDirect` (trÃ¡nh bug cÅ© cá»§a `MoneyDirect(DDTMoney)`).
- `times` client tá»± tÃ­nh `place - unlockedCellNum + 1` (báº¥m Ã´ nÃ o thÃ¬ má»Ÿ tá»›i Ã´ Ä‘Ã³).
- Handler `PetCellUnlock` clamp `times` theo 50 Ã´, tÃ­nh giÃ¡, trá»« tiá»n, tÄƒng `SlotPet`, gá»­i 68/10.

### 2.4 Packet 68/64 â€” thá»©c tá»‰nh

```
Client -> Server: [68][byte 64][int type]
  type 1: [int equipBagType][int equipPlace][int stoneBagType][int stonePlace]
  type 2: [int equipBagType][int equipPlace]
  type 3: [int equipBagType][int equipPlace][int equipBagType2][int equipPlace2]
Server -> Client: [68][byte 37][bool ok] (+ náº¿u ok: [int itemId][int skillId1][int skillId2][int petKindId])
```
BagType: `0 = EquipBag (tÃºi chÃ­nh)`, `1 = PropBag (hÃ nh trang)`.

| Type | NguyÃªn liá»‡u | Tá»‰ lá»‡ | Káº¿t quáº£ |
|---|---|---|---|
| 1 Thá»©c Tá»‰nh | equip pet Q5 (cat 50/51/52) + 1 Ä‘Ã¡ `Property1=132` (12570) | `Property4/1000` (Q5=200 â†’ 20%) | 1 trang bá»‹ Q6 ngáº«u nhiÃªn trong 3 cat (301xx/311xx/321xx), 2 skill thá»©c tá»‰nh, record `Pet_Equip_Awake_Data` |
| 2 Thá»©c Tá»‰nh SÃ¢u | equip Q6/Q7 cÃ³ trong `ts_petequipawakensuper.xml` + `NeedCount` Ä‘Ã¡ 12570 | 100% | `NewEquipID` theo mapping (30103â†’140103â†’150103â†’160103) |
| 3 Dung Há»£p | 2 equip Q8 **cÃ¹ng TemplateID** cÃ³ mapping + `NeedCount` Ä‘Ã¡ 12269 | 100% | `NewEquipID` (150103+150103 â†’160103) |

- Tháº¥t báº¡i type 1: **váº«n máº¥t** trang bá»‹ + 1 Ä‘Ã¡ (client cÅ©ng clear Ã´ chá»n + giáº£m count Ä‘Ã¡).
- Trang bá»‹ káº¿t quáº£: vÃ o tÃºi chÃ­nh, `IsBinds=true`, háº¡n `random(5..15)` ngÃ y (giá»‘ng `PetAwaken` cÅ©),
  thÃªm DB ngay (`SaveNewItemToDatabase`) Ä‘á»ƒ cÃ³ `ItemID` tháº­t, rá»“i ghi record
  `PetBag.AddAwakenEquipRecord` (insert `Pet_Equip_Awake_Data` qua `SP_User_Add_eqAwakenPet`).
- KindID (pet thá»©c tá»‰nh) suy tá»« chá»¯ sá»‘ cuá»‘i TemplateID: `1â†’5, 2â†’9, 3â†’6, 4â†’7, 5â†’11, 6â†’10, 7â†’12, 8â†’15, 9â†’27`
  (khá»›p code `PetAwaken.cs` cÅ© + `Pet_Special_Skills.KindID` cÃ³ `IsAwaken=1`);
  type 2/3 Æ°u tiÃªn KindID tá»« record cÅ© cá»§a trang bá»‹ nguyÃªn liá»‡u.
- Response gá»­i **cáº£ khi lá»—i validate** (`ok=false`) Ä‘á»ƒ client reset UI.

## 3. File thay Ä‘á»•i

### Server (`C:\Gunny\Gunny12.6\Full Source\game.server`)

| File | Loáº¡i | Ná»™i dung |
|---|---|---|
| `Game.Server.Pet\PetExtConfigMgr.cs` | **má»›i** | Load `petconfiginfo.xml` (FreeCellCount=8, MaxPetCellCount=50), `TS_UpgradeTemplate.xml` (Types=103), `TS_PetEquipAwakenSuper.xml`, `ServerConfig.xml` (PetEquipSuperAwakenConfig 12570,12269); map KindID; roll skill |
| `Game.Server.Pet.Handle\PetCellUnlock.cs` | **má»›i** | Handler `[PetHandleAttbute(10)]` má»Ÿ Ã´ pet |
| `Game.Server.Pet.Handle\PetEquipAwaken.cs` | **má»›i** | Handler `[PetHandleAttbute(64)]` 3 tab thá»©c tá»‰nh |
| `Game.Server.GameUtils\PetInventory.cs` | sá»­a | `UnlockedCellCount` (SlotPet), `NormalizeSlotPet`, `UnlockPetCells`, `AddAwakenEquipRecord` |
| `Game.Server.GameObjects\GamePlayer.cs` | sá»­a | `PetInventory` capability 20â†’50; push `68/10` lÃºc login |

Build: `data/pet-work/lead/build-deploy.ps1` (MSBuild VS 18, `BuildProjectReferences=false`) â†’
`Game.Server.dll` ~3.318.784 bytes, copy vÃ o `Service\Service\Road` + backup `.bak-20261108-164436`.

### Web / DB

| File | Thay Ä‘á»•i |
|---|---|
| `WebSite\Request\petconfiginfo.xml` | `MaxFreePetCount` 20 â†’ **8** |
| `Db_Tank_All.Pet_Config` | `MaxFreePetCount` = 8 (Ä‘á»“ng bá»™) |
| `Db_Tank.Sys_Users_Detail.SlotPet` | = 8 cho 5 user test; default 8 |

### Tool

| File | Má»¥c Ä‘Ã­ch |
|---|---|
| `_tools\restart-road-pet.bat` | double-click â†’ UAC â†’ restart Road vá»›i DLL má»›i (cÃ³ backup, health check, Ä‘á»c log PET) |
| `_tools\restart-road-pet-auto.ps1` | báº£n khÃ´ng tÆ°Æ¡ng tÃ¡c (dÃ¹ng cho automation) |
| `_tools\pet-send-test-items.ps1` | gá»­i 7 thÆ° `[PET-TEST]` cho user 1 (váº­t pháº©m test) |
| `data\pet-work\lead\build-deploy.ps1` | build + deploy DLL |
| `data\pet-work\lead\pet_slotpet_migrate.sql` | migration DB chá»‘t |

## 4. Test trong game

1. Äáº£m báº£o Road Ä‘Ã£ restart báº±ng `_tools\restart-road-pet.bat` (log `data\restart-road-pet.log` cÃ³ `RESTART-PET-OK`).
2. ÄÄƒng nháº­p **test1** (user 1) â†’ nháº­n 7 thÆ° `[PET-TEST]` trong Há»™p thÆ° (dá»n bá»›t tÃºi chÃ­nh náº¿u cáº§n;
   tÃºi chÃ­nh cÃ²n ~4 Ã´ trá»‘ng).
3. **Má»Ÿ Ã´ pet**: Há»‡ thá»‘ng pet â†’ báº¥m má»™t Ã´ khoÃ¡ ngoÃ i 8 Ã´ Ä‘áº§u â†’ dialog *"DÃ¹ng 600 Xu má»Ÿ 1 Ã´?"* (hoáº·c N Ã´ náº¿u
   báº¥m Ã´ xa hÆ¡n: giÃ¡ cá»™ng dá»“n) â†’ OK â†’ Ã´ má»Ÿ ngay; relog váº«n giá»¯ (kiá»ƒm tra `Sys_Users_Detail.SlotPet`).
   Tick "Xu khÃ³a" Ä‘á»ƒ test tráº£ báº±ng Xu khÃ³a (user test1 chá»‰ cÃ³ 123 Xu khÃ³a â†’ sáº½ bÃ¡o thiáº¿u, Ä‘Ãºng).
4. **Thá»©c tá»‰nh** (ÄÃ o Táº¡o Pet â†’ tab Thá»©c Tá»‰nh): chá»n trang bá»‹ **30004** (Q5) + Ä‘Ã¡ **12570** â†’ báº¥m Thá»©c Tá»‰nh:
   - 20% ra trang bá»‹ Q6 + 2 skill; 80% bÃ¡o tháº¥t báº¡i; **cáº£ 2 trÆ°á»ng há»£p Ä‘á»u máº¥t trang bá»‹ + 1 Ä‘Ã¡**.
   - Kiá»ƒm tra DB: `SELECT * FROM Db_Tank.dbo.Pet_Equip_Awake_Data WHERE userId = 1`.
5. **Thá»©c Tá»‰nh SÃ¢u** (tab 2): chá»n **30103** (Q6) + Ä‘á»§ 80 Ä‘Ã¡ 12570 â†’ ra **140103**; tiáº¿p **140103** â†’ **150103**.
6. **Dung Há»£p** (tab 3): chá»n **150103** + **150103** (2 mÃ³n) + Ä‘á»§ 8 Ä‘Ã¡ 12269 â†’ ra **160103**.
7. Sau má»—i láº§n: item má»›i náº±m trong tÃºi chÃ­nh, cÃ³ tooltip 2 skill thá»©c tá»‰nh; relog kiá»ƒm tra skill váº«n cÃ²n
   (login push 68/38 danh sÃ¡ch awaken).

## 5. Rá»§i ro / lÆ°u Ã½

- **Item thá»©c tá»‰nh lÃ  temporary** (5â€“14 ngÃ y) â€” giá»‘ng code `PetAwaken.cs` cÅ©, khÃ´ng pháº£i bug má»›i.
- Type 1 chá»n **ngáº«u nhiÃªn 1 trong 3 category** (vÅ© khÃ­/nÃ³n/giÃ¡p) theo server cÅ© + tip client
  "nháº­n Ä‘Æ°á»£c 1 mÃ³n trang bá»‹ thá»©c tá»‰nh ngáº«u nhiÃªn" â†’ Ä‘Ã¢y lÃ  gamble, khÃ´ng chá»n Ä‘Æ°á»£c category.
- Type 1 hiá»‡n **bá» qua Ã½ nghÄ©a riÃªng cá»§a Ä‘Ã¡ thá»©c tá»‰nh chuyÃªn biá»‡t** (12594..12601 cÃ³
  `Property2 = KindID`): má»i Ä‘Ã¡ `Property1=132` Ä‘á»u cho káº¿t quáº£ ngáº«u nhiÃªn nhÆ° 12570.
  Legacy `PetAwaken.cs` cÃ³ dÃ¹ng `Property2/Property4` nhÆ°ng dá»¯ liá»‡u `Property4=0` cho má»i Ä‘Ã¡
  nÃªn báº£n legacy cÅ©ng khÃ´ng chá»n Ä‘Ãºng KindID â€” cháº¥p nháº­n hÃ nh vi random thá»‘ng nháº¥t.
- Type 1 loáº¡i cÃ¡c TemplateID táº­n cÃ¹ng `0` (30110/30120/30130â€¦) khá»i danh sÃ¡ch káº¿t quáº£ vÃ¬
  khÃ´ng suy ra Ä‘Æ°á»£c KindID â†’ chá»‰ 27/30 design má»—i category cÃ³ thá»ƒ xuáº¥t hiá»‡n (an toÃ n, trÃ¡nh petId=0).
- Type 2/3 vá»›i trang bá»‹ táº­n cÃ¹ng `0` mÃ  **khÃ´ng cÃ³ record cÅ©** (nháº­n tá»« nguá»“n ngoÃ i type 1) sáº½
  fallback KindID ngáº«u nhiÃªn â†’ tooltip cÃ³ thá»ƒ hiá»‡n sai tÃªn pet; chá»‰ xáº£y ra vá»›i item khÃ´ng qua type 1.
- Record awaken cá»§a trang bá»‹ bá»‹ tiÃªu hao (type 2/3) **khÃ´ng bá»‹ xoÃ¡** khá»i `Pet_Equip_Awake_Data`
  (khÃ´ng cÃ³ SP delete; itemId auto-increment khÃ´ng tÃ¡i dÃ¹ng â†’ vÃ´ háº¡i; DB sáº½ tÃ­ch luá»¹ cháº­m).
- `AddPet`/`AdoptPet` **Ä‘Ã£ giá»›i háº¡n** nháº­n pet vÃ o cÃ¡c Ã´ Ä‘Ã£ má»Ÿ (`FindFirstEmptySlot(0, SlotPet)`) â€”
  háº¿t Ã´ Ä‘Ã£ má»Ÿ sáº½ bÃ¡o "hÃ£y má»Ÿ thÃªm Ã´ pet" thay vÃ¬ Ä‘áº·t pet vÃ o Ã´ khoÃ¡.
- Migration production: `pet_slotpet_migrate.sql` dÃ¹ng cÃ´ng thá»©c an toÃ n
  `SlotPet >= max(pet.Place)+1`, tá»‘i thiá»ƒu 8, tá»‘i Ä‘a 50 â€” khÃ´ng lÃ m khoÃ¡ pet cÅ© khi deploy.
- Náº¿u client Ä‘ang cache `petconfiginfo.xml` cÅ© (`MaxFreePetCount=20`) thÃ¬ giÃ¡ hiá»ƒn thá»‹ sáº½ lá»‡ch â€”
  cáº§n Ä‘Ã³ng/má»Ÿ game (client load láº¡i XML má»—i láº§n khá»Ÿi Ä‘á»™ng).
- Muá»‘n Ä‘á»•i sá»‘ Ã´ má»Ÿ sáºµn / giÃ¡: sá»­a `petconfiginfo.xml` (MaxFreePetCount) vÃ  `TS_UpgradeTemplate.xml`
  (dÃ²ng Types=103) â€” server tá»± Ä‘á»c láº¡i sau khi restart (khÃ´ng cáº§n rebuild).
- Rollback: copy `Game.Server.dll.bak-<stamp>` Ä‘Ã¨ láº¡i trong `Service\Service\Road` rá»“i restart;
  DB rollback: `UPDATE Sys_Users_Detail SET SlotPet = 20` (náº¿u muá»‘n vá» nhÆ° cÅ©).


## 6. Bản sửa lỗi sau test thực tế (turn 2) — kỹ năng thức tỉnh + chặn trang bị khác loại

Người chơi test xong phản ánh: (a) thức tỉnh tạo được trang bị nhưng chưa thấy kỹ năng thức tỉnh
cộng vào "Kỹ năng thú cưng"; (b) trang bị khác loại pet vẫn trang bị được; (c) nghi ngờ thiếu viền vàng
cho skill thức tỉnh khi trang bị đồ. Đã kiểm tra SWF + source và sửa:

### 6.1 Nguyên nhân

- Handler AddPetEquip cũ cộng skill thức tỉnh vào **player.Pet** (pet đang xuất trận) thay vì
  **pet nhận trang bị tại place** → trang bị cho pet khác (hoặc pet không xuất trận) không được cộng skill.
- Chuỗi UsersPetInfo.Skill do PetMgr.UpdateSkillPet/UpdateAwakenSkill tạo có dạng "skillId,index"
  (index 0,1,2…). Client đọc phần thứ 2 là **exclusiveID** và chỉ vẽ viền vàng
  (sset.petsBag.awaken.awakenSkillBorder — SkillItem.setExclusiveSkillImg, McType=1) / hiện
  sset.game.petSkill.AwakenMC in-battle (PetSkillCell) khi exclusiveID > 0.
  Vì server gửi index nên client không phân biệt được skill thức tỉnh → mất viền vàng.
- Chưa kiểm tra loại pet khi equip → đồ thức tỉnh của pet này gắn được cho pet khác
  (skill không kích hoạt vì record.petId khác KindID pet).

### 6.2 Đã sửa (chỉ Game.Server.dll)

| File | Thay đổi |
|---|---|
| PetInventory.cs | Thêm GetAwakenRecord(itemId), RebuildPetSkill(pet), RebuildAllPetSkills(), NormalizeSkillExclusiveIds(); login rebuild skill cho **tất cả pet** trước khi gửi 68/1 |
| AddPetEquip.cs | Dùng **pet nhận trang bị** (GetPetAt(place)) thay player.Pet; **chặn trang bị thức tỉnh khác loại** (record.petId ≠ Pet KindID → báo lỗi, không equip); rebuild skill + lưu DB ngay; sửa message tiếng Việt |
| DelPetEquip.cs | Rebuild skill cho đúng pet khi tháo trang bị (loại skill thức tỉnh của món vừa tháo) |
| PetExtConfigMgr.cs | Thêm IsAwakenSkillId() (tra Pet_Special_Skills.IsAwaken) + GetPetEquipKindId() (dự phòng mapping chữ số cuối cho đồ chưa có record) |

RebuildPetSkill logic: Skill = PetMgr.UpdateSkillPet(...) (kỹ năng gốc theo KindID/Level) →
với từng trang bị đang equip: nếu có record thức tỉnh và ecord.petId == Pet.KindID thì
PetMgr.UpdateAwakenSkill(...) (thay skill gốc bị DeleteSkillIDs bằng 2 skill thức tỉnh) →
chuẩn hoá exclusiveID = skillId cho skill thức tỉnh, 0 cho skill thường → lưu pet.Skill
+ pet.SkillEquip (ActiveEquipSkill) và gửi 68/1 để client refresh panel kỹ năng.
Skill thức tỉnh xuất hiện trong pool "Kỹ năng thú cưng" (viền vàng); người chơi kéo vào ô
"Kỹ năng chiến đấu" như skill thường để dùng trong trận (battle chỉ nhận SkillEquip).

### 6.3 Giới hạn

- Việc chặn loại pet dựa trên **record thức tỉnh** (Pet_Equip_Awake_Data.petId) — chính xác cho mọi
  trang bị tạo ra từ tính năng thức tỉnh. Trang bị Q6+ **chưa từng thức tỉnh** (nhận từ GM/mail, không có
  record) chưa có nguồn dữ liệu đáng tin để suy ra pet KindID nên vẫn cho trang bị (fallback
  GetPetEquipKindId chỉ áp dụng dòng 01..09). Khi cần chặn tuyệt đối, phải bổ sung bảng mapping
  TemplateID→KindID chính thức.
- Skill thức tỉnh không tự vào ô chiến đấu: người chơi phải kéo vào ô (đúng thiết kế client).

### 6.4 Test lại

1. Restart Road bằng _tools\restart-road-pet.bat (bản build 17:10, sha256 180F28DF…).
2. Đăng nhập test1 → mở pet Tôn Ngộ Không (ô 4/5, TemplateID 210103/210101) → nhận trang bị
   thức tỉnh kind 11 trong túi (vd 150105/160105) → trang bị: panel "Kỹ năng thú cưng" phải có
   2 skill mới **viền vàng**; relog vẫn còn.
3. Thử trang bị món kind 11 lên pet Hỏa Khuyển (kind 15) → server báo
   "Trang bị thức tỉnh này chỉ có thể trang bị cho thú cưng cùng loại.", không equip.
4. Kéo 2 skill viền vàng vào ô "Kỹ năng chiến đấu" → vào trận thấy biểu tượng AwakenMC + dùng được.

### 6.5 Bổ sung sau báo cáo phân tích client/server (final)

- PickQuality6Template (tab Thức Tỉnh) **chỉ chọn dòng 01..09** (TemplateID % 1000 = 101..109):
  đây là nhóm duy nhất có quy ước chữ số cuối -> KindID đã xác nhận (khớp bảng đá thức tỉnh
  12594..12601 có Property2 = KindID). Các dòng 10..30 không theo quy ước này — nếu dùng digit
  mapping sẽ tạo record petId SAI loại ngay khi thức tỉnh (vd 30113 "Gà Con" bị gán petId 6),
  khiến việc chặn trang bị khác loại về sau bị vô lý.
- EquipSkillPet (68/7) nay **validate skillID phải nằm trong pet.Skill** + slot 0..4:
  không thể gắn skill thức tỉnh khi chưa trang bị đúng trang bị (hoặc packet sửa tay).
- **Không gửi packet 68/20 / 68/21** sau equip/unequip: client 12.6 không cần, đồng bộ hoàn toàn
  qua packet 68/1 (đã kiểm chứng SWF: PetsBagManager.addPetEquipHander chỉ cập nhật danh sách
  trang bị, không refresh skill; refresh skill chỉ từ 68/1). Server vẫn gửi 68/1 cho đúng pet.
- RebuildPetSkill **giữ nguyên 6 ô chiến đấu** người chơi đã gắn (MergeSkillEquip): cập nhật ô
  mở/khóa theo level/VIP, chỉ thay entry nếu skill không còn trong pool (vd vừa tháo trang bị thức tỉnh).
  Trước đó bản 17:10 reset ActiveEquipSkill → mất lựa chọn skill mỗi lần login (đã sửa).
- Login rebuild skill cho toàn bộ pet chạy trước khi gửi 68/1; FeedPet/PetRisingStar (lên cấp/
  tiến hoá) cũng rebuild lại skill thức tỉnh (trước đây bị reset về skill gốc).
- Battle: chỉ SkillEquip được fight server dùng → skill thức tỉnh phải được người chơi kéo vào
  6 ô chiến đấu (client cho phép click skill trong pool; server đã validate). In-battle hiện vòng
  vàng AwakenMC nhờ fallback isAwakenSkill từ danh sách record 68/38/68/37 (đã gửi đủ).
- Ghi chú: bảng mapping chữ số cuối không đáng tin cho dòng 09 trở đi; dòng 09 "Rùa" theo legacy
  map về KindID 27 (Rồng Băng Giá) — nếu muốn chính xác tuyệt đối cần bảng TemplateID->KindID
  chính thức từ server gốc (hiện chỉ dùng 01..08 chắc chắn; 09 giữ theo legacy).

### 6.6 Kết quả kiểm chứng sau fix (2026-11-08 17:3x)

- Build cuối: Game.Server.dll 3.321.856 bytes, sha256  8FEFC5C…C4963, backup .bak-20261108-173614.
- DB sau khi login lại với DLL fix (pet ID=5, Tôn Ngộ Không KindID 11, đang gắn 3 món thức tỉnh
  3613/3614/3615 với record petId=11):
  - Skill = 262,0|267,0|269,0|274,0|309,309|311,311|312,312|315,315|365,0|376,0|377,0
    → 4 skill thức tỉnh (309/311/312/315) nằm trong pool, exclusiveID = skillID (>0) → client vẽ
    viền vàng; các skill thường exclusiveID=0.
  - SkillEquip giữ nguyên lựa chọn người chơi 262,0|267,1|376,2|0,3|269,4; slot 3 thành trống
    vì skill 271 bị skill thức tỉnh thay thế (đúng cơ chế DeleteSkillIDs).
- Kiểm chứng logic bằng 2 harness độc lập (chạy ngoài game, đọc DB thật): 2 harness đều cho cùng
  kết quả skill/merge như trên; khớp với SWF: Skill gửi 2 int/skill (exclusiveID), panel "Kỹ năng
  thú cưng" 8 ô + scroll, 6 ô chiến đấu không có exclusiveID, battle chỉ đọc SkillEquip.
- Chặn trang bị khác loại chưa test trực tiếp trong client (cần người chơi thao tác) nhưng đã
  kiểm tra code path: AddPetEquip so ecord.petId với Pet_Template_Info.KindID trước khi
  AddEqPet; client không optimistic-move nên chỉ nhận message lỗi, không treo UI.

### 7. Trang bị pet: chặn đúng loài cho TOÀN BỘ đồ Q6..Q9 (turn 3)

- Thêm bảng cấu hình WebSite\Request/PetEquipKindMap.xml: DesignRow (TemplateID % 1000,
  101..130) -> KindID của loài pet. Dòng 101..108 là bộ trang bị gốc (khớp quy ước chữ số cuối
  + bảng đá thức tỉnh 12594..12599/12601 có Property2=KindID); dòng 109..130 lấy theo tên vật phẩm
  ("Vũ Khí Rùa Thức Tỉnh" -> Huyền Vũ kind 18, "... Bạch Diệm Thánh Hổ" -> kind 32, ...).
- PetExtConfigMgr.GetPetEquipKindId(templateId) đọc bảng này; AddPetEquip chặn trang bị khác
  loài cho cả đồ **chưa** thức tỉnh (trước đây chỉ chặn đồ đã có record Pet_Equip_Awake_Data).
- Tab Thức Tỉnh (PickQuality6Template) giờ random được đồ của **mọi loài có skill thức tỉnh**
  (KindID 5,6,7,9,10,11,12,15 + 27 Rồng Băng Giá + 32 Bạch Diệm Thánh Hổ), không còn giới hạn
  9 dòng đầu; record petId không còn sai loại.
- Dòng chưa xác định (112 Tiểu Yêu, 115/116 Đấu Sĩ/Đấu Sĩ Hiếm — pet không có trong bản này)
  để trống trong XML (không chặn nhầm). Chỉnh XML rồi restart Road là áp dụng, không cần build.
- Kiểm chứng: harness data/pet-work/lead/kindtest PASS 16/16 mẫu (gồm 30109->18 trước đây bị
  legacy map sai thành 27, 30126->32, 160105/161105/162105->11).
- Nghiên cứu source online (GitHub/RageZone): không có source DDTank 12.6+ miễn phí công khai có
  thức tỉnh; bản public 12.6 (ThaiDat202/Gunny12.6) chính là baseline decompiled thiếu handler gốc.
  Chi tiết + link: data/pet-work/turn3/ddtank-source-research.md.
- Còn lại (chưa làm): 42 element ID hiệu ứng battle của skill thức tỉnh chưa có trong
  Game.Logic InitPetSkillEffect (nhiều nhất ở KindID 32/27; với KindID 11 chỉ thiếu 1516).

### 8. Turn 4 - Dữ liệu chuẩn từ request server VNG + sửa trang bị Lv2/Lv3/Lv4

- Nguồn: tải XML từ https://quest.gn.zing.vn/ (đúng hướng tool GunTools: LinkRequest + giải nén
  zlib + sinh SQL). Dữ liệu hiện hành 11/2026. Script: data/pet-work/turn4/.
- Đã import DB Db_Tank_All (backup *_bak_turn4):
  - Shop_Goods (trang bị pet): 261 -> **351** món (đủ dòng 125,130,131..136 cho Lv1..Lv4);
  - Pet_Template_Info: 250 -> **286** (thêm kind 35..39, 101);
  - Pet_Skill_Info: 2.611 -> **3.763**; Pet_Skill_Element_Info: 3.394 -> **4.959**;
  - Pet_Skill_Template_Info: nạp lại chuẩn **586** dòng;
  - Pet_Star_Exp 108 -> 124; Pet_Form_Data 36 -> 50;
  - Server_Config.PetEquipSuperAwakenConfig = 12570,12269,12320.
- Schema: Pet_Skill_Info.ElementIDs 
varchar(50) -> 
varchar(100) (dữ liệu chuẩn dài hơn).
- Request XML (client): 	s_petequipawakensuper.xml chuẩn **225 entry** (đủ Q6->Lv2->Lv3->Lv4);
  các file pet skill/pet template/TemplateAlllist được **sinh lại từ DB qua handler .ashx**;
  loadpetstarexp.xml, loadpetformdata.xml cập nhật; serverconfig.xml patch stone thứ 3.
- Code: PetExtConfigMgr Quality6 30 -> 36 dòng; PetEquipAwaken **giữ nguyên 2 skill** khi nâng
  cấp món đã thức tỉnh (copy từ record cũ), cho phép record không skill với pet mới; ResolveAwakenKindId
  không gán sai loài nữa; PetEquipKindMap.xml thêm dòng 115/116/131..136.
- Kiểm chứng: harness data/pet-work/turn4/verify -> TURN4-VERIFY-PASS; DLL sha 9F410FAC...,
  restart 21:56:20.
- Báo cáo đầy đủ: data/pet-work/turn4/IMPORT-REPORT.md.

### 9. Turn 5 - 6 ô kỹ năng chiến đấu VIP11 + Chúc phúc thần gà + Road headless

- **6 ô kỹ năng chiến đấu**: server trước đây chỉ lưu/gửi 5 ô nên ô thứ 6 luôn bị client khóa với
  dòng "VIP11 có thể mở". Đã sửa:
  - PetMgr.ActiveEquipSkill + UsersPetInfo.GetSkillEquip trả **6 ô**: ô 0 mở sẵn; ô 1..3 mở
    theo cấp pet 20/30/50; ô 4 (ô đặc biệt) mở cho VIP>=1; **ô 5 mở khi VIP>=11** ("0,5").
  - EquipSkillPet cho phép gắn vào slot 0..5.
  - Login tự merge SkillEquip 5 ô cũ -> 6 ô và lưu DB. Đã xác nhận DB pet user1:
     ,0|0,1|0,2|0,3|0,4|0,5 (pet lvl 60), pet Tôn Ngộ Không có 5 skill gắn + ô 6  ,5 trống.
- **Chúc phúc thần gà (KingBless)**:
  - UsersExtraInfo.KingBlessRenevalDays trước đây gán thẳng field, **không set _isDirty** nên
    gia hạn cùng gói không được lưu; out game vào lại mất hiệu lực. Đã set dirty (+ Deed tương tự).
  - PlayerExtra.UseKingBless khi trừ lượt buff gọi ConvertKingBless() để lưu số lượt đã dùng.
  - KingBlessHandler lưu DB ngay sau khi mua/gia hạn (self + người được tặng).
  - PlayerExtra.LoadFromDatabase tự phục hồi: hết hạn -> tắt; còn hạn nhưng thiếu chuỗi buff ->
    dựng lại theo Index. ResetUsersExtra kiểm tra hạn trước khi dựng buff.
  - Thêm handler opcode **143** (trước đây thiếu): dùng thẻ "Chúc phúc thần gà" (item 11919,
    CategoryID=11/Property1=39) để kích hoạt/gia hạn 30/90/180 ngày.
  - Kiểm chứng: sau khi deploy, user1 gia hạn và DB đã lưu KingBlessEnddate 05/05/2027 -> 01/11/2027,
    login debug thấy server gửi đúng Index=3 + 7 buff.
- **Road.Service chạy headless (tắt Form1)**:
  - Thêm appSettings ShowGMForm (mặc định **false**) và EnableConsoleCommands (mặc định false)
    trong Road.Service.exe.config; ConsoleStart.OnAction chỉ gọi Form1 khi ShowGMForm=true,
    còn không thì chạy message loop ẩn (GM API + web panel vẫn hoạt động).
  - Đã deploy Road.Service.exe mới; xác nhận process có MainWindowHandle=0, health OK, players OK.
  - Bật lại Form1 khi cần: sửa ShowGMForm=true trong config rồi restart Road.

### 10. Turn 6 - Robot/auto-bot giảm sức mạnh còn 0.75 + Road chạy console (không Form1)

- **Robot/auto-bot** (các hàm tạo bot trong RingStationMgr: GetAutoBot/CreateAutoBot/CreateRingStationChallenge/
  CreateBaseAutoBot - dùng khi phòng chỉ có 1 người và hệ thống thêm bot, hoặc đấu trường
  offline): toàn bộ chỉ số (Attack/Defence/Luck/Agility/HP/MagicAttack/MagicDefence/
  Element Attack-Defence/BaseAttack/BaseDefence/BaseAgility/BaseBlood/FightPower) được
  nhân với RobotBattlePowerPercent/100 (appSettings trong Road.Service.exe.config,
  mặc định **75 = 0.75**, tức giảm 25% so với sức mạnh robot hiện tại).
  - GetBotPowerRate() đọc config; sửa value rồi restart Road là áp dụng, không cần build.
  - Khi tạo bot, console Road in dòng [Robot] ... scale=75% atk=... def=... hp=... để kiểm tra.
- **Road.Service**: ShowGMForm=false (không mở Form1 desktop) nhưng **vẫn giữ cửa sổ console**
  để xem log (ColoredConsoleAppender + Console.WriteLine). Road.Service.exe chạy bình thường,
  GM API và web panel hoạt động; khởi động không dùng -WindowStyle Hidden.
  Muốn mở lại Form1: đặt ShowGMForm=true trong Road.Service.exe.config rồi restart.

### 11. Turn 8 - Dọn log Road (bỏ log chẩn đoán + lỗi giả)

- Đã bỏ dòng PETDBG KingBless login ... (log chẩn đoán tạm thời) trong GamePlayer.Login —
  đây là dòng ERROR giả xuất hiện mỗi lần login sau khi sửa KingBless.
- BaseClient.SendTCP: nới ngưỡng cảnh báo packet từ 16KB lên 64KB — client 12.6 có gói hợp lệ
  402 kích thước 16406 byte, trước đây ghi pkg 402 is too high vào Error.log mỗi lần vào trận.
- PacketProcessor: packet client gửi mà server chưa implement (CONSTORTION_ACTIVE_TARGET,
  RECHARGE_RANK, GM_RATE...) trước đây in ra console dạng ___ERROR___; nay chỉ ghi Log.Debug
  (bị ẩn ở mức WARN của log4net) — không còn rác console.
- Road.Service.exe.config: DebugMode=False để không in tên packet handler
  (Game.Server.Packets.Client.GameDataHandler) ra console mỗi packet.
- Đã rebuild Game.Server.dll (sha D16ECA77...) + Game.Base.dll, restart Road 22:59:22.
  Kiểm tra: Error.log trống, console dump không còn dòng ERROR/PETDBG/pkg402/PacketProcessor.
  Các dòng [Robot] ... scale=75% ... vẫn giữ (thông tin, không phải lỗi) để kiểm tra giảm sức mạnh bot.

### 12. Turn 9 - Fix client hiện "Chúc phúc thần gà đã hết hạn" + robot ngang sức người chơi

- **Root cause popup "đã hết hạn, đồng ý tiếp phí?"**: AbstractPacketLib.SendUpdateAllData gửi
  **packet code 142** với dữ liệu giả WriteInt(0) + WriteDateTime(DateTime.Now.AddDays(7)).
  Client KingBlessManager cũng nghe packet 142, nên ngay sau SendKingBlessMain (index=3, hạn
  2027) client lại nhận packet 142 thứ hai (index=0, hạn now+7) và **ghi đè trạng thái**:
  _openType=0, _endTime=now+7 -> checkShowDueAlert() hiện dueTip và icon tắt.
  Fix: bỏ lời gọi Out.SendUpdateAllData(PlayerCharacter) trong GamePlayer.Login; hàm
  SendUpdateAllData không gửi packet giả nữa (trả packet rỗng).
- **Robot chiến đấu ngang bằng người chơi**: RingStationMgr.GetAutoBot/CreateAutoBot copy đúng
  chỉ số hiện tại của player (Attack/Defence/Agility/Luck/HP/Magic/Element/BaseAttack/BaseDefence/
  BaseAgility/BaseBlood), dùng đúng vũ khí + cường hóa + Grade của player; bỏ hệ số 1.5x/0.8x cũ.
  Road.Service.exe.config: RobotBattlePowerPercent=100 (có thể hạ xuống nếu muốn bot yếu hơn).
  Console in [Robot] ... scale=100% ... khi tạo bot để kiểm tra.
