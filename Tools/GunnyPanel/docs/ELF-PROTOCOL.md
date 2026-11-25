# ELF — Thần Hộ Mệnh: Client Protocol & UI Spec (Linh Bảo / Kỹ năng / Triệu hồi / Đổi)

> **Mục đích**: tài liệu wire-format + UI-behaviour phía **client** cho hệ Thần Hộ Mệnh (packet **664**) và Linh Bảo (packet **707**), đủ để đội server implement đúng byte-for-byte.
> **Phạm vi**: client decompiled AS3 (Gunny 12.6, FFDec) + đối chiếu server C# hiện có (read-only). **Không** sửa server/DB/WebSite; mọi script phụ nằm ở `data/elf-work/client/`.
> **Ngày**: 2026-11-09. Người viết: `elf-client-analyst` (team task-1).

## 0. Kết luận nhanh (TL;DR)

| # | Kết luận | Bằng chứng |
|---|----------|-----------|
| 1 | **664/18 ELF_EQUIP_OPERATE = (int elfID, int elfEquipRecordID)** — mặc Linh Bảo cho 1 elf. Không có packet cửi riêng; double-click lại ô Linh Bảo đang mặc gửi đúng packet này ⇒ server phải hiểu **toggle** (trùng id đang mặc = tháo). | `scripts/elf/view/equip/ElfEquipView.as:103`, `elf/view/property/ElfPropertyView.as:75`, `elf/view/skill/ElfSkillView.as:70`; không có call-site nào khác |
| 2 | **707/3 GET_ELFEQUIP = (int bagType, int place)** — dùng item CategoryID **91** (Linh Bảo item) trong túi: server biến item thành bản ghi Linh Bảo của user. Client **không** có listener 707/3 ⇒ response phải là **push 707/2** (+707/1 nếu type mới). | `scripts/bagAndInfo/bag/BagView.as:3205-3208`; `_83$56.as:578-585`; không có `PkgEvent(707,3)` trong `_83$56.as:60-76` |
| 3 | **707/4 UPGRADE_ELFEQUIP (C2S) = (int nextTemplateID, int type, int itemTempId1, int itemTempId2)**; **response = (int type, int newLevel)** ⇒ client `elfEquipLvList[type-1]=newLevel`. Level là **level chung theo Type (hệ)**, không phải theo từng món. | `elf/view/equip/ElfEquipInfoView.as:182`, `_83$56.as:123-129`, `elf/model/ElfModel.as:283-286` |
| 4 | **707/1 = `[count] then count×(int type, int level)`**; 707/2 = `[count] then count×(int id, int type, int quality)`. Server hiện **derive sai từ danh sách Elf** (không có bảng `Sys_Users_ElfEquip` trong code) và 707/1 gửi `count = ElfInfo.Count` + luôn level=1 ⇒ sai ngữ nghĩa, và có thể NRE khi `TempID` lạ. | `Game.Server.Elf/ElfPacketsOut.cs:268-298`; grep `Sys_Users_ElfEquip` trong toàn bộ `game.server\*.cs` = **0 hit** |
| 5 | **Routing 707 đang hỏng**: `ElfEquipHander` đẩy 707 vào **cùng** `ElfLogicProcessor` đọc byte đầu là `ElfPackageType` ⇒ 707/3 bị xử lý bởi handler 664/**3** (Exercise), 707/4 bởi 664/**4** (AddGrowth). | `Game.Server.Packets.Client/ElfEquipHander.cs:10-17`; `Game.Server.Elf/ElfLogicProcessor.cs:24-31`; `GamePlayer.cs:7079` |
| 6 | Dữ liệu Linh Bảo đầy đủ đã có sẵn: **TS_LinkPalTemplate = 6 Type × 5 Quality × 50 Level = 1500 rows**; mỗi cặp (Type,Quality) = 1 item template ID (48001..48030, CategoryID=91) đã có trong `Db_Tank_All.Shop_Goods`; material nâng cấp = **12286** (Pha Lê Linh Hồn) + **Linh Nguyên theo hệ** (12289/12290/12288/12287/12293/12294). | DB query + `data/elf-work/client/xml/TS_LinkPalTemplate.decoded.xml`; bảng ở §4.7 |
| 7 | Server **chưa có** handler 707/3, 707/4, 664/18; chưa load/save `Sys_Users_ElfEquip` (bảng có 0 rows); chưa push 707/1–2 sau thay đổi. Đây là các P0 cho tab Linh Bảo. | `ElfHandleMgr` reflect các class `[ElfHandle]` 2..17 (không có 18); `ElfPacketsOut.cs`; `GamePlayer.cs:4400-4409,4558-4568` |
| 8 | Kỹ năng: sách KN là item **CategoryID 86**, server gắn thẳng vào `Sys_Users_SkillBooks` khi add item (`GamePlayer.AddItem/StackItemToAnother`) và push **664/9**; học = **664/5**, dung hợp = **664/12**; atlas là **client-only** (đọc TS_ElfSkillBook.xml, không packet). | `GamePlayer.cs:1553-1579`; `elf/view/skill/ElfSkillAtlasView.as:24-26` |

---

## 1. Nguồn & phương pháp

| Nguồn | Đường dẫn |
|-------|-----------|
| Client AS3 decompiled | `C:\Gunny\ToolGunny\GunnyPanel\data\tmp\root4_scripts\scripts` (gọi tắt `scripts/…` bên dưới) |
| Client request XML (zlib) | `C:\Gunny\Gunny12.6\WebSite\Request\{TS_ElfTemplate,TS_ElfSkillBook,TS_LinkPalTemplate,TS_ElfIntimacy,TS_UpgradeTemplate}.xml` |
| Server C# (read-only) | `C:\Gunny\Gunny12.6\Full Source\game.server` (`Game.Server.Elf`, `Game.Server.Elf.Handle`, …) |
| DB read-only | `DESKTOP-SMLR4VU` / `Db_Tank_All`, `Db_Tank` (sa/123456) |
| Bản dịch tiếng Việt | `data\tmp\client_assets\lang\language.txt` (build client mới hơn, keys `tank.elf.*`) |
| Script giải nén/analytics | `data/elf-work/client/decompress_elf_xml.py`, `analyze_elf_xml.py`, `extra_xml_extract.py`; output `xml_schema.txt`, `xml_analysis.txt`, `extra_analysis.txt` |

Ghi chú: bản client đang serve là build cũ hơn (chuỗi tiếng Trung inline ở vài chỗ, ví dụ `ElfPropertyView.as:162`), nhưng cùng bộ class `elf`/packet như bản mới ⇒ dùng `language.txt` để dịch key.

---

## 2. Tầng transport & kiểu dữ liệu

- Frame: 20-byte header `[short 29099][short len][short checksum][short packetCode][int clientId][int param1][int param2]`, payload bắt đầu offset 20 (`scripts/road7th/comm/PackageIn.as:79-88`, `PackageOut.as:14-26`). Mỗi packet ELF có **byte đầu tiên của payload = subtype** (`_83$56.as:60-76`).
- **writeInt / readInt**: 4 byte big-endian (`_83$56.as` + AS3 ByteArray; server `Game.Base/PacketIn.cs:74-81,280-286`).
- **writeByte**: 1 byte; **writeBoolean**: 1 byte 0/1 (`readBoolean` server = `ReadByte()!=0`).
- **String**: server `WriteString` = `short(utf8Len+1)` + UTF8 bytes + `0x00`; server `ReadString` = `short len` + UTF8 (bỏ NUL) (`Game.Base/PacketIn.cs:129-135,324-338`). Client dùng `writeUTF/readUTF` (AS3 AMF-UTF: 2-byte length + UTF-8). **Mọi string field ELF phải ghi bằng `WriteString`** (không `WriteInt`).
- Client dispatch: `SocketManager.Instance.addEventListener(PkgEvent.format(code, subtype), handler)` (`_83$56.as:60-76`).

---

## 3. Bản đồ packet 664 / 707 (client ⇄ server)

### 3.1 Client listener & parser — `scripts/elf/_83$56.as`

| Cặp | Handler client | Payload client đọc | Dòng |
|-----|---------------|--------------------|------|
| 664/1 | `__onUpdateElf` | `bool hidePet` + `addElf()` (full elf block) | 313-322, 324-395 |
| 664/2 | `__onUpdateCultivate` | int elfID, int exp, int grade, 8×value (hp,atk,huo,shui,tu,feng,guang,an) | 157-172 |
| 664/3 | *(không ở `_83$56`)* — `ElfPropertyExerciseView.__onExercise` | int (unused), int n, n×int delta | `elf/view/property/ElfPropertyExerciseView.as:71-90` |
| 664/4 | `__onAddGrowth` | int elfID, 8×value, 8×growth, int quality | 174-197 |
| 664/5 | `__onUpdateSkill` | int elfID, UTF `skillList` ("id,id,…") | 288-295 |
| 664/6 | `__onFight` | int elfID, bool isFight | 223-229 |
| 664/7 | `__onDiscard` | int elfID, bool (false = xoá) | 231-241 |
| 664/8 | `__onSummonResult` | int c1, int c2, int n, n×(int id, int count, bool isBind) | 131-155 |
| 664/9 | `__onUpdateSkillBook` | int n, n×(int bookTemplateID, int isBind, int count) | 249-286 |
| 664/10 | `__onAddElf` | `bool hidePet` + `addElf()` (giống 664/1) | 307-311 |
| 664/12 | `__onUpdateSkill` (dùng chung 664/5) | int elfID, UTF skillList | 67-68 |
| 664/13 | `__onExerciseSave` | int elfID, 8×value, 8×growth, int quality | 199-221 |
| 664/14 | `__onUpdateCoin` | int coin | 243-247 |
| 664/15 | `__onUpdateGoodFeling` | int elfID, 4×int (goodFeling, dungeon, game, cultivate) | 297-305 |
| 707/1 | `__onUpdateElfEquipLvInfo` | int count, count×(int type, int level) | 78-95 |
| 707/2 | `__onUpdateElfEquipInfo` | int count, count×(int id, int type, int quality) | 97-121 |
| 707/4 | `__onUpgradeElfEquip` | int type, int newLevel | 123-129 |

> `addElf()` (664/1, 664/10) đọc **per elf**: `int elfID; bool ok;` rồi nếu `ok=true`:
> `int tempID, int exp, int grade, int hpBase, attackBase, huoBase, shuiBase, tuBase, fengBase, guangBase, anBase, 8×Value(hp,atk,huo,shui,tu,feng,guang,an), 8×Growth(hp,atk,huo,shui,tu,feng,guang,an), int goodFeling, bool isFight, int quality, UTF skillList, int dungeonGoodFeling, int gameGoodFeling, int cultivateGoodFeling, int elfEquipId` (`_83$56.as:332-394`).
> **Lưu ý chỉ số base**: thứ tự đọc là `hpBase, attackBase, huoBase, shuiBase, tuBase, fengBase, guangBase, anBase` (`_83$56.as:345-352`).

### 3.2 Client → server — các hàm send (`_83$56.as`)

| Subtype | Hàm | Payload | Dòng |
|---------|-----|---------|------|
| 664/2 | `sendElfCultivate(elfId, itemTemplateId, isAll)` | int, int, bool | 439-447 |
| 664/3 | `sendElfExercise(elfId)` | int | 449-455 |
| 664/4 | `sendElfExerciseADD(elfId, arr)` | int elfId, int count, count×int | 465-479 |
| 664/5 | `sendElfSkill(elfId, bookId, isBind, lockArr)` | int, int, int, int count, count×int | 481-497 |
| 664/6 | `sendElfFight(elfId, fight)` | int, bool | 516-523 |
| 664/7 | `sendElfDiscard(elfId)` | int | 525-531 |
| 664/8 | `sendElfSummon(starLevel, count)` | int, int | 533-540 |
| 664/10 | `sendElfActivate(bagType, place)` | int, int | 552-559 |
| 664/11 | `sendElfBuy(itemTemplateId, count, isBind)` | int, int, bool | 542-550 |
| 664/12 | `sendElfBlend(mainElfId, viceElfId, lockArr)` | int, int, int count, count×int | 499-514 |
| 664/13 | `sendElfExerciseSave(elfId)` | int | 457-463 |
| 664/16 | `sendElfHide(bool)` | bool | 561-567 |
| 664/17 | `sendElfReset(elfId, isBind)` | int, bool | 569-576 |
| 664/18 | `sendDressELfEquip(elfId, equipRecordId)` | int, int | 598-605 |
| 707/3 | `sendGetEllfEquip(bagType, place)` | int, int | 578-585 |
| 707/4 | `sendUpgradeElfEquip(nextTemplateID, type, itemTempId1, itemTempId2)` | 4×int | 587-596 |

### 3.3 Server handler hiện có (`Game.Server.Elf.Handle`, reflect qua `[ElfHandle(n)]`)

`ElfHandleMgr` reflect toàn assembly, đăng ký theo `ElfHandleAttribute.Code` (`Game.Server.Elf.Handle/ElfHandleMgr.cs:22-44`).

| ElfPackageType | Handler server | Trạng thái |
|----------------|----------------|-----------|
| 1 UPDATE_ELF | *(không có)* chỉ server→client | ok (push) |
| 2 CULTIVATE | `Cultivate.cs` | có |
| 3 EXERCISE | `Exercise.cs` | có (nhưng **cũng bị 707/3 gọi nhầm**) |
| 4 ADD_GROWTH | `AddGrowth.cs` | có (nhưng **cũng bị 707/4 gọi nhầm**) |
| 5 SKILL | `Skill.cs` | có |
| 6 FIGHT | `Fight.cs` | có |
| 7 DISCARD | `Discard.cs` | có |
| 8 SUMMON | `Summon.cs` | có |
| 9 USE_GOODS | *(không có)* — client chỉ nhận push 664/9 | ok (push) |
| 10 ACTIVATE_ELF | `ActivateElf.cs` | có |
| 11 BUY | `Buy.cs` | có |
| 12 BLEND | `Blend.cs` | có |
| 13 EXERCISE_SAVE | `ExerciseSave.cs` | có |
| 14 UPDATE_COIN | *(chỉ push)* | ok |
| 15 UPDATE_GOODFELING | *(chỉ push)* | ok |
| 16 HIDE_PET | `HidePet.cs` | có |
| 17 ELF_RESET | `ElfReset.cs` (**stub rỗng**, đọc packet không?) | thiếu logic |
| 18 ELF_EQUIP_OPERATE | **KHÔNG CÓ** → `KeyNotFoundException` (log live 2026-11-09) | **P0** |
| 707/* | không có processor riêng; bị route vào `ElfLogicProcessor` | **P0** |

Enum 2 phía khớp nhau: client `scripts/elf/ElfPackageType.as:6-40` vs server `Game.Server.Packets/ElfPackageType.cs:3-23` (1..18 cùng tên/thứ tự).

---

## 4. TAB "LINH BẢO" (elf equipment, packet 707 + 664/18)

### 4.1 Client data model

`ElfModel` (`scripts/elf/model/ElfModel.as`):
- `_equipLvList:Array = [1,1,1,1]` — **level chung theo Type** (index = Type-1), khởi tạo chỉ 4 phần tử (`:38-45`).
  - `getElfEquipLvByType(type) = int(_equipLvList[type-1])` (`:283-286`).
- `_equipTemp:Dictionary[Type][Quality][Level] → ElfEquipTemp` (nạp từ TS_LinkPalTemplate) (`:216-234`, analyzer §4.7).
- `_equipTempById:Dictionary[Id → ElfEquipTemp]` — key = cột `Id` (1..1500) của XML (`:226-234`; `elf/analyzer/ElfEquipAnalyzer.as:42-43`).
- `_elfEquipInfo:DictionaryData[recordId → ElfEquipInfoVo{id,type,quality,equipElfId}]` (`:32`, `elf/model/ElfEquipInfoVo.as:6-12`).
- `elfEquipInfoList` = các bản ghi **chưa bị elf nào mặc** (`equipElfId<=0`), sort theo quality desc (`:251-269`).
- `getElfEquipTemplate(type,quality)` = `_equipTemp[type][quality][levelByType]` (`:271-275`).
- `getNextElfEquipTemplate(temp)` = `_equipTemp[temp.Type][temp.Quality][level+1]` (`:277-281`).

`_83$56.checkElfEquipIsOn()` (`_83$56.as:630-653`): reset `equipElfId=0` cho mọi món, rồi với mỗi ElfInfo có `elfEquipId>0` set `elfEquipInfo[elfEquipId].equipElfId = elfID`. Được gọi sau 707/2 (`:119`) và sau 664/1 (`:317-320`).
⇒ **Hệ quả**: `ElfInfo.elfEquipId` (664/1 trường cuối) phải là **record id trong `elfEquipInfo`** (chính là int thứ 1 của 707/2), nếu không UI sẽ không biết elf đang mặc món nào.

`ElfEquipTemp` field list (`scripts/elf/model/ElfEquipTemp.as:6-64`): `Id, TemplateID, Type, Quality, Level, Name, Defence, Agility, Lucky, Damage, Blood, Consume, SkillID, Hp, Attack, MagicAttack, MagicDefence, Crit, SunderArmor, ViolenceInjury, Speed, TricKill, Armor, Desc, ItemTempId1, Param1, ItemTempId2, Param2, elementDefType`. Properties thực tế được XML điền: `Id, TemplateID, Type, Quality, Level, Blood, Attack, Defence, Agility, Lucky, Damage, SkillID, ItemTempId1, Param1, ItemTempId2, Param2` (analyzer copy theo tên attr, `ElfEquipAnalyzer.as:29-44`). `Name/Hp/Attack/.../Consume` **không có trong XML** ⇒ luôn =0/default; tên hiển thị lấy từ **item template** `TemplateID` (`elf/view/equip/ElfEquipBagCell.as:49-54`, `uigeneral/elf/ElfEquipTip.as:51,57`). `elementDefType` chỉ được set runtime khi xem info người khác (`ElfPropertyLookView.as:237`).

### 4.2 707/1 — `UPDATE_ELFEQUIPLVINFO` (server → client)

```
[byte 1][int count]  then count × ( [int type][int level] )
```
Parser: `_83$56.as:78-95` — `_loc2_[_loc5_-1] = _loc3_` (type 1-based) ⇒ `elfEquipLvList[type-1] = level`; dispatch event `"updateelfequiplv"` → các view refresh (`ElfEquipView.as:61-71`, `ElfEquipInfoView.as:58-68`, `ElfSkillView.as:63,283-286`, `ElfPropertyView.as:68,298-301`).

**Ý nghĩa**: level "tu luyện" chung cho **từng hệ (Type 1..6)**; mọi Linh Bảo cùng Type dùng chung level này (dùng để tra `_equipTemp[type][quality][level]`).
**Map Type**: 1=Lửa, 2=Gíó, 3=Đất, 4=Nước, 5=Sáng, 6=Tối (suy ra từ `getMaxElementDefType` `_83$56.as:655-683` + mô tả item 48001..48030 trong `TemplateAlllist.xml`, xem §4.7).
**Trigger**: login — `GamePlayer.cs:4405-4408` (chỉ khi `GameProperties.VERSION >= 12100`).
**Yêu cầu server**: gửi **đủ 6 type** (hoặc ít nhất các type có item/đang mặc). Nếu thiếu, `int(undefined)=0` ⇒ `_equipTemp[type][q][0]` undefined ⇒ lỗi render (`ElfModel.as:271-286`). `count` = **số cặp (type,level)** chứ không phải số elf.

**Hiện trạng server** `ElfPacketsOut.cs:268-282`: `count = ElfInfo.Count`; mỗi elf ghi `(ElfType, 1)` ⇒ (a) count sai bản chất, (b) level luôn 1, (c) nếu 2 elf cùng type sẽ ghi trùng, (d) NRE nếu `Elf_Template_Info` không có `TempID` tương ứng (`FirstOrDefault(...).ElfType`).

### 4.3 707/2 — `UPDATE_ELFEQUIP` (server → client)

```
[byte 2][int count]  then count × ( [int id][int type][int quality] )
```
Parser: `_83$56.as:97-121` — tạo `ElfEquipInfoVo` và `elfEquipInfo.add(id, vo)`, sau đó `checkElfEquipIsOn()` + dispatch `"updateelfequip"`.
- `id` = **record id** Linh Bảo của user (khóa để mặc/tháo; = `ElfEquipInfoVo.id`, được gửi lại trong 664/18).
- `type` = 1..6 (hệ), dùng tra template `_equipTemp[type][quality][level]`.
- `quality` = 1..5 (phẩm), dùng tra tiếp.

**Trigger**: login (`GamePlayer.cs:4405-4408`); **cần bổ sung** sau 707/3 (nhận item Linh Bảo mới) và bất cứ khi nào inventory Linh Bảo đổi.
**Hiện trạng server** `ElfPacketsOut.cs:284-298`: `count = ElfInfo.Count`; mỗi elf ghi `(ElfID, ElfType, elf.Quality)` ⇒ đang giả lập Linh Bảo bằng chính Elf; không đọc/ghi bảng `Sys_Users_ElfEquip`.

### 4.4 707/3 — `GET_ELFEQUIP` (client → server)

```
[byte 3][int bagType][int place]
```
Call site duy nhất: dùng item **CategoryID == 91** trong túi (`scripts/bagAndInfo/bag/BagView.as:3205-3208` → `_83$56.instance.sendGetEllfEquip(_loc6_.bagType,_loc6_.place)`).
⇒ **Semantics**: "biến item Linh Bảo (480xx) trong túi thành bản ghi Linh Bảo của user". `bagType` là `eBageType` số (0=Equip,1=Prop,…); `place` là ô.
**Client không đăng ký listener 707/3** ⇒ server **không được** trả 707/3; phải:
1. validate item tại (bagType, place) có `CategoryID=91` và `TemplateID ∈ 48001..48030`;
2. tra `TS_LinkPalTemplate` theo `TemplateID` để lấy `Type,Quality` (mỗi cặp có 50 row cùng TemplateID, khác Level — lấy Level=1/hoặc Type+Quality);
3. insert `Sys_Users_ElfEquip (UserID, TemplateID, Type, Quality, Level=1, IsEquip=0)`;
4. remove item;
5. push **707/2** (toàn bộ danh sách mới) + 707/1 nếu type đó chưa có level.

**Hiện trạng server**: `ElfEquipHander` route 707 vào `ElfLogicProcessor` (`ElfEquipHander.cs:10-17`), đọc byte đầu = subtype ⇒ 707/3 rơi vào `[ElfHandle(3)] Exercise.cs` — handler này đọc **1 int** (coi là elfId), `RemoveTemplate(12247,5)`, random growth… ⇒ **sai hoàn toàn** (có thể mất item Thuốc Luyện Thể / lỗi). Cần processor 707 riêng.

### 4.5 707/4 — `UPGRADE_ELFEQUIP` (client → server) + response

**Request** (4 ints) từ `ElfEquipInfoView.__onClickCultivate` (`elf/view/equip/ElfEquipInfoView.as:162-183`):
```
[byte 4][int nextTemplateID][int type][int itemTempId1][int itemTempId2]
```
Trong đó (lấy từ `_nextTemplateInfo = getNextElfEquipTemplate(_curTemplateInfo)`):
1. `nextTemplateID` = `ElfEquipTemp.TemplateID` của **level kế tiếp** (48001..48030) — không phải level;
2. `type` = `ElfEquipTemp.Type` (1..6);
3. `itemTempId1` = `ItemTempId1` của row level kế tiếp (luôn **12286**);
4. `itemTempId2` = `ItemTempId2` (Linh Nguyên theo hệ: 12289/12290/12288/12287/12293/12294).
Client **không** gửi số lượng: trước đó client tự check túi `PropBag`:
`count(ItemTempId1) >= Param1` và `count(ItemTempId2) >= Param2`, nếu thiếu hiện `tank.elf.elfEquip.notLvUp` (`ElfEquipInfoView.as:170-181`).
Client cũng chặn khi không có `_nextTemplateInfo` (đã max level): message `tank.elf.elfEquip.maxLv` (`:170-174`).

**Response** (server → client, **đúng 2 int**):
```
[byte 4][int type][int newLevel]
```
Parser `_83$56.as:123-129`: `elfEquipLvList[type-1] = newLevel;` + dispatch `"updateelfequiplv"` ⇒ UI đổi LV + chỉ số ngay.
⇒ Server nên xác định level mới = `level[type]+1` **theo dữ liệu server**, không tin client; trả `(type, newLevel)`.

**Rule dữ liệu** (từ TS_LinkPalTemplate, §4.7): 50 level/type; level 1..50; cấp kế tiếp cần `Param1` × 12286 và `Param2` × Linh Nguyên hệ; `Param1/Param2` tăng dần theo level (ví dụ Type4-Q1: L1→L2 cần 20×12286; L4→L5 cần 70×12286 + **1**×12287; L9→L10 cần 410 + 2; L49→L50 cần 7900 + 50). Khi `level==50` không còn next ⇒ client tự ẩn nút.
**Validation phía server (bắt buộc)**:
- `type` phải khớp `ElfEquipTemp.Type` của `nextTemplateID`; `itemTempId1/2` phải khớp row tương ứng (chống cheat);
- kiểm tra đủ 2 material trong PropBag/EquipBag (client chỉ check PropBag — `_83$56.model` không check EquipBag);
- level hiện tại <50; trừ nguyên liệu; level[type]++; push 707/4 (+707/1 nếu muốn đồng bộ toàn bộ).

**Hiện trạng server**: 707/4 rơi vào `[ElfHandle(4)] AddGrowth.cs` ⇒ đọc `elfId = 48002…` rồi `num = type` (1..6) rồi đọc thêm `num` int ⇒ **desync** packet (client chỉ gửi 4 int) + có thể `RemoveTemplate(12246, sum)` (mất Thuốc Tư Chất) hoặc exception. P0.

### 4.6 664/18 — `ELF_EQUIP_OPERATE` (mặc/tháo Linh Bảo)

```
[byte 18][int elfID][int elfEquipRecordID]
```
Call sites (tất cả giống nhau: `sendDressELfEquip(_elfInfo.elfID, _loc2_.info.id)`):
- `elf/view/equip/ElfEquipView.as:96-109` — double-click ô Linh Bảo trong list → mặc cho elf đang chọn; nếu chưa chọn elf → message `tank.elf.elfEquip.noElf`;
- `elf/view/equip/ElfEquipView.as:64,70-77` — **ô Linh Bảo đang mặc** cũng có handler double-click (`currElfEquipCell`), gửi **cùng** packet với chính id đang mặc ⇒ **đây là thao tác tháo** (không có packet tháo riêng trong toàn bộ client; xác nhận bằng grep `sendDressELfEquip` chỉ 3 call-site và không có hàm unequip khác);
- `elf/view/property/ElfPropertyView.as:57,71-76` (tab Thuộc tính) và `elf/view/skill/ElfSkillView.as:58,66-71` (tab Kỹ năng) — cùng logic.

**Semantics server cần implement**:
```
if equipRecordID == 0                    -> elf.ElfEquipId = 0 (tháo)
else if elf.ElfEquipId == equipRecordID  -> elf.ElfEquipId = 0 (toggle tháo)
else:
   row = Sys_Users_ElfEquip(ID=equipRecordID, UserID=player)
   validate row exists, chưa bị elf khác mặc (IsEquip…)
   validate elf.type == row.Type  (xem dưới)
   elf.ElfEquipId = equipRecordID
push 664/1 (SendUpdateElf) cho elf vừa đổi  -> client cập nhật ElfInfo + "updateall"
```
**Điều kiện type**: client có hàm chết `checkElfEquipCanEquip(param1,param2)` chỉ so `param1==param2`, show `tank.elf.elfEquip.error` (`_83$56.as:607-617`) — không được gọi ở đâu; nhưng UI `ElfEquipInfoView` gray skill khi `getMaxElementDefType() != ElfEquipTemp.Type` (`ElfEquipInfoView.as:75-78`) ⇒ ý định thiết kế: **Linh Bảo hệ nào chỉ mặc cho elf hệ đó** (elf.type == equip.Type). Server nên enforce; nếu sai gửi message.
**Response**: client **không** có listener 664/18 ⇒ server phải push **664/1** (format §3.1 `addElf`) cho elf vừa thay đổi; `__onUpdateElf` sẽ cập nhật `elfEquipId` cho ElfInfo đã tồn tại (`_83$56.as:340: getElfInfo(id) || new`) rồi `checkElfEquipIsOn()` + dispatch `"updateall"` (`:313-322`) ⇒ list Linh Bảo refresh (`ElfEquipView.as:73-77`).
**Hiện trạng server**: không có handler 18 ⇒ log `KeyNotFoundException at ElfHandleMgr.LoadCommandHandler` (live log 2026-11-09 00:41). P0.

### 4.7 Dữ liệu `TS_LinkPalTemplate.xml` (client) — chính là bảng Linh Bảo

File: `C:\Gunny\Gunny12.6\WebSite\Request\TS_LinkPalTemplate.xml` (zlib, 36267 B → 342061 B; loader `ddt/loader/LoaderCreate.as:1901-1906`, analyzer `elf/analyzer/ElfEquipAnalyzer.as`).
Schema `<Item …/>`: `Id, TemplateID, Type, Quality, Level, Attack, Defence, Agility, Lucky, Damage, Blood, SkillID, ItemTempId1, Param1, ItemTempId2, Param2` (16 attr).

Thống kê (script `analyze_elf_xml.py`, output `xml_analysis.txt`):
- **1500 rows = 6 Type × 5 Quality × 50 Level**; `Id` 1..1500; `TemplateID` ∈ **48001..48030** (30 template = 6×5, mỗi cặp Type/Quality 1 template ID).
- `Level` 1..50; `Quality` 1..5; `Type` 1..6.
- `SkillID`: pet-skill id (vd 1791..1849, 1923..1942) đổi theo mốc level (1/10/20/…).
- `ItemTempId1` = **12286** (Pha Lê Linh Hồn) cho mọi row; `Param1` = số lượng cần (0 ở L1, tăng dần tới 7900 ở L50).
- `ItemTempId2` = Linh Nguyên theo hệ (xem bảng dưới); `Param2` = số lượng (0 hoặc 1/2/5/10/50 tại các mốc 5/10/15/20/50).
- Stats row (Blood/Attack/Defence/Agility/Lucky/Damage) tăng theo Level và Quality.
- Không có `Name`, `Desc`, `Hp`, `MagicAttack`, `Consume`… trong XML (các field này default 0 trong ElfEquipTemp).

Bảng map Type ↔ hệ ↔ material ↔ item Linh Bảo:

| Type | Hệ | `ItemTempId2` (Linh Nguyên) | Item template (Quality 1..5) | Tên item (5 cấp) |
|------|-----|------------------------------|------------------------------|------------------|
| 1 | Lửa | **12289** | 48009..48012, 48019 | Vương Miện Rực Lửa |
| 2 | Gió | **12290** | 48013..48016, 48020 | Áo Khoác Cánh Đen |
| 3 | Đất | **12288** | 48005..48008, 48018 | Khiên Chắn Naga |
| 4 | Nước | **12287** | 48001..48004, 48017 | Châu Báu Hàn Băng |
| 5 | Sáng | **12293** | 48021..48025 | Quyền Trượng Pharaoh |
| 6 | Tối | **12294** | 48026..48030 | Ngọn Đao Đêm Tối |

> Thứ tự `Quality` 1..5 ↔ item template (ví dụ Type4): Q1=48001, Q2=48002, Q3=48003, Q4=48004, **Q5=48017** (Q5 dùng chung dải 48017..48020 cho Type4..Type1, còn Type5/6 Q5 = 48025/48030).

**Item template của Linh Bảo**: tồn tại thật trong `Db_Tank_All.Shop_Goods` (same schema as `TemplateAlllist.xml`), `CategoryID=91`, `MaxCount=1`, `Level=5`, `Quality` **= 2..6** (item quality = ElfEquipTemp.Quality + 1), `Property1` = 10/40/160/640/2560 (giá trị phôi). Client dùng để render icon/tên/tooltip: `ItemManager.fillByID(ElfEquipTemp.TemplateID)` (`elf/view/equip/ElfEquipBagCell.as:49-54`).

**Nguyên liệu** (đã có trong `Shop_Goods`, CategoryID=11):

| TemplateID | Tên | Ghi chú |
|-----------|-----|---------|
| 12286 | Pha Lê Linh Hồn | material chung mọi lần nâng cấp |
| 12287 | Linh Nguyên-Nước | hệ 4 |
| 12288 | Linh Nguyên-Đất | hệ 3 |
| 12289 | Linh Nguyên-Lửa | hệ 1 |
| 12290 | Linh Nguyên-Gió | hệ 2 |
| 12293 | Linh Nguyên-Sáng | hệ 5 |
| 12294 | Linh Nguyên-Tối | hệ 6 |

**Cost tổng hợp**: nâng cấp = `Param1` × 12286 + `Param2` × Linh Nguyên hệ; **không** tiêu Đá Thánh/Xu; không bind flag; không có tỉ lệ thành công (deterministic — client hiển thị next level value ngay). Max **Level 50** / Quality tối đa **5**.

### 4.8 UI tab Linh Bảo — người chơi làm được gì

Cấu trúc: tab thứ 5 trong `ElfMainView` (`menuTab` index **4**, xem §10). View = `ElfEquipView` (`elf/view/equip/ElfEquipView.as`) gồm: danh sách Linh Bảo chưa mặc (grid), ô Linh Bảo đang mặc, tên/level/exp elf đang chọn, quality clip, sao, nút `fightBtn`/`restBtn`/`discardBtn`/`goodFelingBtn`.

| Hành động | Thao tác | Packet | Điều kiện / message |
|-----------|----------|--------|---------------------|
| Mặc Linh Bảo | double-click món trong danh sách (khi đã chọn elf) | 664/18 (elfID, recordID) | nếu chưa chọn elf: `tank.elf.elfEquip.noElf` = *"Chưa có Thần Hộ Mệnh không thể trang bị linh bảo!"* (`ElfEquipView.as:107`) |
| Tháo Linh Bảo | double-click ô Linh Bảo đang mặc | 664/18 (elfID, recordID đang mặc) | toggle (xem §4.6) |
| Xem chi tiết Linh Bảo | click nút `currEquipBtn` trên ô | client-only: mở `ElfEquipInfoView` | `ElfEquipBagCell.as:63-73` |
| Nâng cấp (cultivate) | trong `ElfEquipInfoView`: bấm `cultivateBtn` | 707/4 | branch: max level → `tank.elf.elfEquip.maxLv` (*"Tối đa"*); thiếu material → `tank.elf.elfEquip.notLvUp` (*"Đạo cụ tăng cấp không đủ!"*, key trùng ở `language.txt:9200` bản cũ = *"Pha Lê không đủ"*); bagLocked → mở `baglocked` |
| Xem thông tin elf khác (tab Thuộc tính/Kỹ năng) | chọn elf bên trái | client-only; `ElfPropertyView`/`ElfSkillView` hiển thị elf equip hiện tại | `ElfPropertyView.as:248-260`, `ElfSkillView.as:204-216` |
| Bỏ elf | nút `discardBtn` | 664/7 | nếu `elfEquipId>0`: chặn + `tank.elf.elfEquip.discardTip` — trong build này hardcode tiếng Trung *"请先卸下灵宠身上的灵宝"* (`ElfPropertyView.as:160-163`), key `discardTip` không có trong language.txt |
| Đổi elf chiến đấu | `fightBtn`/`restBtn` | 664/6 (elfID, bool) | |
| Good feeling | `goodFelingBtn` | client-only view | |

**Không có thao tác đổi Quality**: toàn bộ client không có packet/UI đổi phẩm chất Linh Bảo — Quality là thuộc tính cố định của **item template** khi nhận (48001..48030), được server trả trong 707/2 (`quality`). "Cultivate/Nâng cấp" trong UI = tăng **Level** (dùng chung theo Type), không đổi Quality. Bất biến: `quality` chỉ có 1..5; UI dùng quality để tra bảng `_equipTemp[type][quality][level]` và tooltip.

Tên phẩm chất: `tank.elf.elfEquipProfile` = *"Thô, Thường, Ưu Tú, Hoàn Mỹ, Siêu Việt, Truyền Thuyết"*; client index **bằng Quality** (1..5) ⇒ Q1 hiển thị "Thường" … Q5 "Truyền Thuyết" (lệch 1 bậc so với danh sách — `uigeneral/elf/ElfEquipTip.as:59`). `Tối đa`/`maxLv` khi không có next.

Chỉ số hiển thị trong tooltip: `ElfEquipTemp.getProArr()` push các cặp tên/giá trị cho Blood, Attack, Defence, Agility, Lucky, MagicAttack, MagicDefence, Crit, SunderArmor, ViolenceInjury, Speed, TricKill, Damage, Armor nếu >0 (`ElfEquipTemp.as:71-174`); tên lấy từ `tank.newCard.ProName` split ",". Skill của Linh Bảo: `PetSkillManager.getSkillByID(SkillID)`; skill bị **disable (xám)** nếu `player.getMaxElementDefType() != Type` (`ElfEquipInfoView.as:75-78`, `ElfEquipTip.as:63-67`).

### 4.9 Bảng mismatch client vs server (Linh Bảo)

| # | Vấn đề | Client yêu cầu | Server hiện tại | Mức |
|---|--------|----------------|-----------------|-----|
| L1 | 707 routing | 707 là protocol riêng, subtype 1..4 | route vào ElfLogicProcessor (đọc subtype là ElfPackageType) | **P0** |
| L2 | 707/3 handler | đọc (bagType, place), convert item cat 91 | không có; bị Exercise.cs xử lý nhầm | **P0** |
| L3 | 707/4 handler | đọc 4 int, trả (type,newLevel) | không có; bị AddGrowth.cs xử lý nhầm | **P0** |
| L4 | 664/18 handler | (elfID, equipRecordID) toggle, response 664/1 | không có handler ⇒ KeyNotFoundException | **P0** |
| L5 | 707/1 ngữ nghĩa | count = số type, mỗi entry (type, level chung) | count = ElfInfo.Count, mỗi elf (type,1); NRE nếu template lạ | **P1** |
| L6 | 707/2 nguồn dữ liệu | (id=bản ghi equip, type, quality) | giả lập từ ElfInfo: (ElfID, ElfType, elf.Quality) | **P1** |
| L7 | Lưu trữ | cần `Sys_Users_ElfEquip` persist | bảng có 0 rows; **không** file .cs nào tham chiếu `Sys_Users_ElfEquip`; không load/save | **P0** |
| L8 | `elfEquipId` link | 664/1 trường cuối = record id Linh Bảo của elf | ElfMgr tạo Elf với `ElfEquipId=0`; không có đường set | **P0** |
| L9 | show info người khác | packet 71 `elfEquipTempId` = `TS_LinkPalTemplate.Id` (1..1500) | AbstractPacketLib ghi `elfinfo.FirstOrDefault(IsFight)` → **NRE khi không có elf chiến đấu**; nếu có: ghi `EquipTempId` (không xác định mapping), và đoạn sau ghi `WriteInt(0)` vào chỗ client `readUTF` (`lingStoneStr`) ⇒ **desync** | **P0/P1** |

---

## 5. TAB "KỸ NĂNG" (skill)

### 5.1 UI structure

`ElfSkillView` (`elf/view/skill/ElfSkillView.as`):
- `menuTab` sub-tab: **0 = Học (Study)**, **1 = Dung hợp (Blend)** (`:233-249`).
- `skillList` hiển thị skill hiện có của elf (từ `ElfInfo.skillList`); mỗi ô `ElfSkillLockCell` có thể **khoá** bằng cách kéo icon ổ khoá vào (`:55-56,129-146`; `ElfSkillLockCell.as:19-33`).
- Nút `atlasBtn` mở **Atlas** (client-only); `goodFelingBtn` mở view hảo cảm; `currElfEquipCell` hiển thị Linh Bảo đang mặc (double-click → 664/18, `:66-71`).
- Giới hạn khoá: `maxLock = (skillCount<=5 ? skillCount-1 : 5)` (`:87-93`) ⇒ elf 3★ (5 skill) khoá tối đa 4; 4★/5★ (6/8 skill) khoá tối đa 5. Vượt → `tank.elf.maxLock` (*"Dòng được khóa đã đạt tối đa"*).
- Chi phí khoá: `ServerConfigManager.instance.elfLockMoney[lockNum-1]` (`:99-109`), config `ElfLockSkillPayMoney = 200,500,1500,3000,5000,8500,12000` (DB `Server_Config` ID 1077 / `ServerConfig.xml`).
- `lockTip1/2` visible khi `starLevel<4`, `lockTip3` khi `starLevel<5` (`:201-202`).

### 5.2 Nguồn sách kỹ năng & 664/9

- Sách KN = item **CategoryID 86**, tên ví dụ *"Rực Cháy-Sơ"*, item template bắt đầu 45101… (`TemplateAlllist.xml`; `TS_ElfSkillBook.xml` 177 rows).
- Khi player **nhận** item CategoryID 86, server **không** đưa vào túi mà cộng vào `Sys_Users_SkillBooks` rồi push 664/9: `GamePlayer.AddItem` → `ElfMgr.AddSkillBook`; `StackItemToAnother` → `ElfMgr.StackSkillBook` (`GamePlayer.cs:1553-1579`; `ElfMgr.cs:391-415`); push `SendSkillBooks` (`ElfPacketsOut.cs:128-151`).
- Format 664/9: `[byte 9][int n] n×(int bookTemplateID, int isBind(0/1), int count)` — client key = `"templateID-isBind"`; count<=0 ⇒ remove (`_83$56.as:249-286`).
- Client catalog `TS_ElfSkillBook.xml` (`ElfSkillBookAnalyzer`, model `ElfSkillBook`): attrs `TemplateId, TemplateName, ElfType, NeedBookCount, Randoms, SkillTempId, Description`; 177 rows; `ElfType` 0 = dùng cho mọi hệ, filter list theo `elf.type` (`ElfModel.getSkillBookListByType`, `:133-151`). Catalog này **chỉ để hiển thị/tra skill id**; client không dùng `Randoms/NeedBookCount` để validate.
- Double-click item cat 86 trong túi (nếu còn) chỉ mở UI tab Kỹ năng: `_83$56.instance.show(1,0)` (`BagView.as:3201-3204`).

### 5.3 664/5 — Học kỹ năng

**Request**: `sendElfSkill(elfId, bookTemplateID, isBind(int), lockSkillIDs[])` (`_83$56.as:481-497`):
```
[byte 5][int elfId][int bookTemplateID][int isBind(0/1)][int lockCount] lockCount×int skillID
```
- `bookTemplateID` = item template id của sách (khớp `Sys_Users_SkillBooks.TemplateID`).
- `isBind` = 0/1 (giá trị int từ server; client chỉ chuyển tiếp).
- `lockSkillIDs`: **danh sách skillID hiện có đang được khoá** (không phải id ô), lấy từ `lockSKillArray` (`ElfSkillView.as:265-281`).
- Nếu `lockCount>0`, client **trước khi gửi** bắt user xác nhận và trả phí: `tank.elf.studyLockConfirm` = *"Hiện đã khóa {0} dòng kỹ năng, cần tốn {1} Xu, tiếp tục?"*; `CheckMoneyUtils.checkMoney(false, price, …)` rồi mới `sendElfSkill` (`elf/view/skill/ElfSkillStudyView.as:73-104`). **Trong packet không có field tiền** ⇒ server phải tự trừ đúng giá `ElfLockSkillPayMoney[lockCount-1]` (theo số skill khoá) — hiện `Skill.cs` **không trừ tiền** (P1).
- Nếu chưa chọn sách: `tank.elf.skill.studyTips` (*"Hãy chọn sách kỹ năng cần học"*).

**Response**: **664/5** `[byte 5][int elfId][UTF skillList]` — `__onUpdateSkill` set `ElfInfo.skillList = skillList.split(",")` (`_83$56.as:288-295`). Server dùng chung `SendUpdateSkill` (`ElfPacketsOut.cs:239-246`).

**Server logic hiện tại** (`Game.Server.Elf.Handle/Skill.cs`):
- đọc `elfId, skillId, num(isBind), lockCount(≤7), lockSkill[]`;
- elf phải tồn tại, book phải có trong `Sys_Users_SkillBooks`, `Elf_SkillBook` có row;
- nếu skill đã có → message `Elf.Skill.AlreadyLearned`;
- random `0..999` < `Elf_SkillBook.Randoms` (DB cột Randoms 0..1000) mới thành công, nếu fail trừ 1 sách + message `Elf.Skill.TryAgain`;
- nếu `skillCount >= Elf_Template_Info.MaxSkillCount` → **thay thế ngẫu nhiên 1 skill không bị khoá** (bug: nếu tất cả đang khoá → `dictionary.Count==0` → `ElementAt(Random.Next(0))` ném ArgumentOutOfRangeException);
- thành công: push 664/5 + trừ sách (664/9) + message `Elf.Skill.Success`.
- **MaxSkillCount** (DB `Elf_Template_Info`): 3★ = **5**, 4★ = **6**, 5★ = **8** (18 rows = 6 hệ × 3 star; ví dụ `TemplateID 11/12/13` = Thần Lửa 3/4/5★).
- `ElfInitSkillRandoms` / `ElfLearnSkillRandoms` có trong `GameProperties` (`Bussiness/GameProperties.cs:1064-1068`) và DB nhưng **chưa dùng** trong code elf hiện tại.

**Client hiển thị**: số ô skill = `skillList.length` (không biết max từ server). Lưu ý: server gửi `WriteString("")` khi skill rỗng ⇒ client `"".split(",")` = `[""]` ⇒ 1 ô trống (không crash); muốn sạch có thể gửi chuỗi rỗng vẫn chấp nhận được.

### 5.4 664/12 — Dung hợp (Blend)

**Request**: `sendElfBlend(mainElfId, viceElfId, lockSkillIDs[])` (`_83$56.as:499-514`):
```
[byte 12][int mainElfId][int viceElfId][int lockCount] lockCount×int skillID
```
- Main = elf tab đang chọn; Vice chọn qua `ElfListView` (danh sách elf không phải main & không trong trận; `ElfSkillBlendView.as:67-79`, `ElfListView.as:35-52`).
- Confirm flow: `tank.elf.blend` (*"Sau khi dung hợp, Thần Hộ Mệnh phụ {0} sẽ mất, tiếp tục?"*); nếu vice `grade>=2` thêm confirm `tank.elf.studyGradeConfirm` (*"Thần Hộ Mệnh phụ {0} đang ở Lv{1}, dung hợp tiếp？"*); nếu đang khoá skill → confirm + trả phí như 5.3 (`ElfSkillBlendView.as:104-195`).
- `lockSkillIDs` = skill của **main** đang khoá (giữ lại sau dung hợp).

**Response**: client dùng **664/5** (`__onUpdateSkill`), **664/7** (vice bị xoá, `__onDiscard`) và có thể **664/15** (good feeling). Client không có listener 664/12.

**Server logic hiện tại** (`Blend.cs`):
- merge skill lists: bỏ khỏi main các skill bị khoá → union(distinct) với skill của vice → shuffle → lấy `Min(unionSize, MaxSkillCount(main))` → **prepend lockSkill**; gán `elf.SkillList`.
- bug: tổng cuối = `lockSkill.Count + take` ⇒ có thể **> MaxSkillCount** (vượt giới hạn skill).
- nếu `CultivateGoodFeling<20`: tăng `GoodFeling`, `CultivateGoodFeling` → push 664/15.
- remove vice elf → push 664/5 (main) + 664/7 (vice) + message `"Elf.Blend.Success"` (raw key, **chưa translate** — nên `LanguageMgr.GetTranslation`).
- không kiểm tra vice đang chiến đấu / khoá / cùng hệ; không kiểm tra grade.

### 5.5 Atlas

`ElfSkillAtlasView` render **toàn bộ** `model.bookTempList` (177 sách từ `TS_ElfSkillBook.xml`) thành icon skill, phân trang (`elf/view/skill/ElfSkillAtlasView.as:18-27`). **Không gửi/nhận packet nào** — thuần client-side (catalog đã load lúc mở UI: `_83$56.show()` add loader `creatElfSkillBookTemplate` `_83$56.as:397-404`).

---

## 6. TAB "TRIỆU HỒI" (summon)

UI `ElfSummonView` (`elf/view/summon/ElfSummonView.as`):
- 2 loại: nút 1 = **3★** dùng item **12241** `Sách Thần-Sơ`; nút 2 = **4★** dùng item **12242** `Sách Thần-Trung` (`:59-62`, `BagCell(ItemManager.fillNoBindInfoByID(12241/12242))` `:70-71`).
- Checkbox "all": summon tối đa `min(count(12241/12242), 10)` (`:185-201`).
- Nếu không có item: `tank.elf.sommonTips` (*"Không đủ sách"*).
- Local counter text: `count1.text = "x"+itemCount(12241)`, tương tự 12242 (`:211-215`).
- 2 nút "xem thưởng" mở `ElfSummonPreview` với danh sách từ **ServerConfig** `ElfThreeItems` / `ElfFourItems` (`:59-60`; `ServerConfigManager.as:3940-3958`); mỗi entry "templateId,count" (18 entry). Giá trị DB hiện tại:
  `ElfThreeItems = 1123666,1|1123669,1|1122189,1|…|1122253,1`
  `ElfFourItems = 1123667,1|1123670,1|1122190,1|…|1122253,1` (xem `data/elf-work/client/extra_analysis.txt`).
- Bấm "mua" (nút buy) → **664/11** mua sách bằng Xu/DDTMoney (`:114-176`).
- Nhận kết quả summon → **664/8** → mở `ElfSummonRewardView` (list item, `id/count/isBind`) và update counter.

### 6.1 664/8 — `SUMMON` (request + response dùng chung subtype)

**Request**: `sendElfSummon(starLevel, count)` (`_83$56.as:533-540`):
```
[byte 8][int starLevel(3|4)][int count(1..10)]
```
**Response** `SendSummonResult` (`ElfPacketsOut.cs:112-126`):
```
[byte 8][int summonCount1][int summonCount2][int n] n×(int id, int count, bool isBind)
```
Client parser `_83$56.as:131-155`: `model.summonCount1/2` + `dispatchEvent("summon", arr)`; `ElfSummonRewardView.show(arr)` → mỗi `ElfSummonRewardCell` = `ItemManager.fillByID(info.id)`, count, bind.
**Server hiện tại** (`Summon.cs`): map `3→12241`, `4→12242`; `player.RemoveTemplate(item, count)`; mỗi lần `ItemBoxMgr.CreateItemBox(itemTemplate, …, specialValue)`; add item; push 664/8 với `GetSummonCount(1/2)` (`GamePlayer.cs:5328`); không validate count>10 (client gửi ≤10 nhưng server nên chặn).
**Dữ liệu phải có**: 2 item template 12241/12242 (có); `ItemBoxMgr.CreateItemBox` cấu hình box cho 2 item này (cần kiểm tra box config DB — CHƯA CHẮC vì ngoài phạm vi client); config `ElfThreeItems/ElfFourItems` (có).

### 6.2 664/11 — `BUY` (mua sách triệu hồi + nhận Đá Thánh)

**Request**: `sendElfBuy(itemTemplateId, count, isBind)` (`_83$56.as:542-550`):
```
[byte 11][int itemTemplateId(12241|12242)][int count(1|10)][bool isBind]
```
Client flow: nếu chọn "mua 10" count=10, ngược lại 1; `isBind` = dùng tiền khoá hay không (`ElfSummonView.as:114-176`).
**Server** (`Buy.cs`): giá lấy từ `GameProperties.ElfBuyItemMoneys = "180,1600|800,7200"` (DB ID 1075) — cặp 1: 12241 (1x=180, 10x=1600), cặp 2: 12242 (1x=800, 10x=7200); `MoneyType.Money` nếu `isBind=false`, `DDTMoney` nếu true; cộng **Đá Thánh**: 12241 → +1 (1x) / +10 (10x); 12242 → +5 (1x) / +50 (10x) (`num3`); tạo item + push **664/14** coin (`SendElfCoin`, `ElfPacketsOut.cs:260-266`).
**664/14**: `[byte 14][int coin]` — client `__onUpdateCoin` chỉ set `model.coinCount`, dispatch `"updatecoin"` (UI Đổi + summon refresh).

---

## 7. TAB "ĐỔI" (shop)

UI `ElfShopView` (`elf/view/shop/ElfShopView.as`):
- Danh sách = `ShopManager.Instance.getValidGoodsArrayByType(133)` (`:25`) ⇒ **shop type/ShopID = 133**.
- Mỗi `ElfShopItemView` hiển thị item + giá `_info.getItemPrice(1).goodsPrice` + đơn vị `tank.elf.shop.price` (*"Đá Thánh"*) (`:51-69`).
- `ElfShopBuyFrame` (style `elf.view.alertShopBuyFrame`) cho chọn số lượng; **kiểm tra local** `model.coinCount >= số lượng*giá`; nếu thiếu → `tank.elf.shop.notBuy` (*"Đá Thánh không đủ"*); nếu đủ → gửi **packet 345** generic `ShopManager.out.sendNewBuyGoods(goodsID, 1, count)` (`ElfShopBuyFrame.as:27-39`; format packet 345: `int count, int typeIndex(=1), int goodsID, int param2(=1), UTF "", bool false, UTF "", int 0, bool true, int 0` — `ddt/manager/GameSocketOut.as:243-257`).
- Server cần: (a) bán goods thuộc ShopID 133, (b) trừ **Extra.Info.Coin** (Đá Thánh) thay vì tiền thường, (c) push **664/14** sau khi trừ để client refresh (`__onUpdateElf`→`updateCoin`, `ElfShopView.as:29-40`).
- DB hiện có `ShopID = 133` với **24 goods**: 12243/12244/12245 (Quả Hồn Sơ/Trung/Cao), 12246 Thuốc Tăng Tư Chất, 12247 Thuốc Luyện Thể, 1122188..1122199 (trứng/nguyên liệu elf), 1122534..1122538, 1123683/1123684. Giá nằm trong bảng `Shop` (`APrice1/AValue1`…; client lấy qua push `ShopItemList` — cần server đẩy đúng giá trị; cách đọc cột giá cần đối chiếu `ShopItemListout.xml` — **CHƯA CHẮC**, ngoài phạm vi client decompile).
- Client **không** có packet mua riêng cho Đổi ⇒ dùng chung 345; handler 345 server phải phân biệt shop type 133.

---

## 8. Packet 71 (player info) — hiển thị Thần Hộ Mệnh của người khác

Client parse `PlayerManager.as` (packet 71 "user equip"): trong vòng lặp item, sau `emblemSkillArr`:
```
int elfTempId
if elfTempId>0:
    int goodFeling, int grade, int quality,
    8×value (hp,atk,huo,shui,tu,feng,guang,an),
    8×growth (hp,atk,huo,shui,tu,feng,guang,an),
    UTF skillList
…
int swornExp
int elfEquipTempId        <-- TS_LinkPalTemplate.Id của Linh Bảo elf đang mặc
int cultureCount
cultureCount×(int id, int machining, int quenching, UTF quenchingSkill)
int cultureAddDamage
UTF lingStoneStr
int plotDamage
int plotDefence
```
(`PlayerManager.as:1016-1058`; `BasePlayer.elfEquipTempId` getter/setter `BasePlayer.as:1522-1532`; hiển thị `PlayerInfoFrame.showElf` → `ElfPropertyLookView.updateView(elfInfo, elfEquipTempId, elementDefType)` `PlayerInfoFrame.as:286-301`; tra template bằng `model.equipTempById[elfEquipTempId]` ⇒ **key = XML cột `Id` 1..1500**, `ElfPropertyLookView.as:229-242`).

**Server hiện tại** (`AbstractPacketLib.cs`):
- Elf block khớp thứ tự (2253-2285) ✔ (đủ guang/an nếu VERSION>=12100);
- `swornExp` = swornCount (2287-2299) ✔;
- dòng 2300-2303: `Sys_Users_Elf e = elfinfo.FirstOrDefault(IsFight); WriteInt(e.EquipTempId)` ⇒ **NRE khi player không có elf chiến đấu** (đúng log `UserEquipListHandler NullReferenceException … SendUserEquip line 2303`). Sau khi fix còn phải map sang **Id** của row `TS_LinkPalTemplate` (TemplateID+Quality+Level hiện tại), không phải `EquipTempId` tùy tiện.
- dòng 2304-2315: ghi placeholder sai kiểu: `WriteInt(0)` cho `lingStoneStr` (client `readUTF`), và chỉ ghi 4 field cuối trong khi client đọc thêm (cultureAddDamage, lingStoneStr, plotDamage, plotDefence — 4 field, khớp số lượng nhưng sai kiểu string) ⇒ **desync sau khi hết NRE**.
- 3 chỗ `WriteInt(0) // ElfEquip` trong PlayerProp (1482, 1522, 1537) là các field khác (element ElfEquip) — client đọc ở bảng thuộc tính (ngoài phạm vi tài liệu này).

---

## 9. Entry points & 5 tab

### 9.1 Mở UI

| Cách mở | Điều kiện | Dòng |
|---------|-----------|------|
| Nút toolbar Thần Hộ Mệnh | click → `_83$56.instance.show()` | `ddt/view/MainToolBar.as:181,208,351-355,851-855` (tip `tank.view.ToolStripView.elf` = *"Thần Hộ Mệnh (E)"*) |
| Phím tắt **E** | `PlayerManager.Self.Grade >= 30` | `ddt/manager/KeyboardShortcutsManager.as:154-162` |
| Item CategoryID 86 (sách KN) | mở thẳng tab Kỹ năng, sub Học: `show(1,0)` | `bagAndInfo/bag/BagView.as:3201-3204` |
| Item CategoryID 85 (trứng elf) | gửi 664/10 activate (không mở UI) | `BagView.as:3197-3200` |
| Item CategoryID 91 (Linh Bảo) | gửi 707/3 (không mở UI) | `BagView.as:3205-3208` |
| JumpSystem / bible / hall | `show()` | `ddt/manager/JumpSystemManager.as:128`, `bible/_25$96.as:365`, `hall/_129$33.as:964` |
| Xem info người khác | `PlayerInfoFrame.showElf()` (packet 71) | `bagAndInfo/info/PlayerInfoFrame.as:286-301` |

`show(menu1Index=0, menu2Index=0)` → `ElfMainView(menu1,menu2)` (`_83$56.as:397-411`). Không có điều kiện level trong code mở UI (level 30 chỉ là điều kiện phím tắt; nút toolbar có thể bị ẩn bởi hệ thống icon — **CHƯA CHẮC**, xem `MainToolBar.as:181-214,355`).

### 9.2 5 tab (menuTab index) — `ElfMainView.as:102-147`

| index | View | Tên tab (suy luận theo chức năng/key) | Sub-tab |
|-------|------|----------------------------------------|---------|
| 0 | `ElfPropertyView` | **Thuộc tính / Thần Hộ Mệnh** | sub 0=GrowUp, 1=Cultivate, 2=Exercise (`ElfPropertyView.as:189-209`) |
| 1 | `ElfSkillView` | **Kỹ năng** | sub 0=Study, 1=Blend (`ElfSkillView.as:233-249`) |
| 2 | `ElfSummonView` | **Triệu hồi** | — |
| 3 | `ElfShopView` | **Đổi** (shop Đá Thánh) | — |
| 4 | `ElfEquipView` | **Linh Bảo** | — |

Tab 0,1,4 dùng chung `ElfSelectView` (danh sách elf bên trái, chọn elf → `updateElf`); tab 2,3 không cần chọn elf. `menuTab.selectHandler` gọi `updateView(index)` (`ElfMainView.as:96-147`). Help: `asset.elf.help0..4` (`:41-47`).
`ElfPropertyView` menu con: `growUpView/cultivateView/exerciseView` visible theo sub index (`:189-209`).

---

## 10. Client request XML bắt buộc

Tất cả nằm ở `C:\Gunny\Gunny12.6\WebSite\Request`, **zlib-compressed** (giải nén bằng `zlib.decompress`, magic `78 da/9c`). Root thực tế: `<Result value="true" message="Success!">` chứa các row `<Item …/>`; các analyzer client kiểm `_loc4_.@value == "true"`, nếu lỗi hiển thị `@message` (`ElfTemplateAnalyzer.as:23-40`, `ElfSkillBookAnalyzer.as:23-40`, `ElfEquipAnalyzer.as:25-51`).

| File | Loader client | Rows | Dùng cho |
|------|---------------|------|----------|
| `TS_ElfTemplate.xml` | `creatElfTemplate` (`LoaderCreate.as:1887-1892`), load ở `MainResourceLoader.as:601` | 18 | catalog elf: TemplateId, TemplateName, ElfType, StarLevel, MaxSkillCount, randoms/grow ranges (`ElfTemplateAnalyzer`) |
| `TS_ElfSkillBook.xml` | `creatElfSkillBookTemplate` (`:1894-1899`); load khi mở UI (`_83$56.as:401`) | 177 | catalog sách KN (TemplateId, TemplateName, ElfType, NeedBookCount, Randoms, SkillTempId, Description) |
| `TS_LinkPalTemplate.xml` | `creatElfEquipTemplate` (`:1901-1906`); load ở `MainResourceLoader.as:602` | **1500** | toàn bộ dữ liệu Linh Bảo (6×5×50) — §4.7 |
| `TS_ElfIntimacy.xml` | `creatElfGoodFelingTemplate` (`:1908-1913`); `MainResourceLoader.as:603` | 70 (client) / 68 (DB) | bảng hảo cảm (Gp, Grades, Types1-4, Value1-4, Description) — dùng cho `ElfPropertyGoodFelingView`, **không liên quan 4 tab chính** |
| `TS_UpgradeTemplate.xml` | TemplateManager (không thuộc LoaderCreate elf) | 664 rows tổng | EXP cấp elf: `Types` 106/107/108 = elf 3/4/5★; `Grades` 1..70, `Data` = EXP cần (`_246$11.getElfUpgradeData(starLevel, grade)`, dùng ở `ElfEquipView.as:185-196`, `ElfPropertyView.as:232-243`) |

**ServerConfig keys bắt buộc** (client `ServerConfigManager.as:3920-3979`; DB `Db_Tank_All.Server_Config`): `ElfIntimacyDayMax=10,30|10,30|10,20` (ID 1076), `ElfBuyItemMoneys=180,1600|800,7200` (1075), `ElfThreeItems` (1074), `ElfFourItems` (1073), `ElfLockSkillPayMoney=200,500,1500,3000,5000,8500,12000` (1077), `ElfResetPoint=1000` (1231), `ElfInitSkillRandoms=500,300,0,0|0,300,200,0|0,0,200,50` (1532), `ElfLearnSkillRandoms=100,50,30,20,0` (1533).
**DB tables**: `Db_Tank_All`: `Elf_Template_Info` (18), `Elf_SkillBook` (177), `TS_LinkPalTemplate` (1500), `TS_ElfIntimacy`/`ElfIntimacy`/`Elf_Intimacy` (68 mỗi bảng), `TS_UpgradeTemplate` (664 rows; Types 106/107/108 × 70 grades), `Shop_Goods` (item templates: Linh Bảo 48001-48030 cat 91, materials, books cat 11; sách KN item 45101.. cat 86), `Shop` (ShopID 133 = elf shop, 24 goods). `Db_Tank`: `Sys_Users_Elf` (8 rows), **`Sys_Users_ElfEquip` (0 rows; columns ID,UserID,TemplateID,Type,Quality,Level,IsEquip)**.

---

## 11. CHƯA CHẮC (cần xác minh thêm)

1. **Toggle tháo Linh Bảo (664/18)**: suy luận từ việc ô Linh Bảo đang mặc có double-click handler gửi cùng packet + không có packet tháo riêng + message `tank.elf.equipOut.msg` (*"Tháo Linh Bảo trên người Linh Thú"*, `language.txt:8656`) không được dùng trong source. Server có thể chọn quy ước khác (ví dụ gửi equipRecordID=0 để tháo, client hiện không làm vậy). **Cần test live** để xác nhận.
2. **`GetElfEquipLvList` dùng chung level theo Type hay theo (Type,Quality)?** Client code chỉ hỗ trợ **theo Type** (`_equipLvList[type-1]`), nhưng bảng DB `Sys_Users_ElfEquip` có cột `Level` → cần chốt thiết kế: nếu mỗi bản ghi có level riêng thì client hiện tại **không** hiển thị được (luôn dùng level theo type). Khuyến nghị: coi Level là level-theo-type (đồng bộ mọi row cùng Type), hoặc thêm bảng riêng.
3. **`Sys_Users_ElfEquip.TemplateID`**: client không đọc TemplateID từ 707/2 (chỉ id/type/quality); client tra item template = `_equipTemp[type][quality][level].TemplateID`. Server cần map row → TemplateID tương ứng để tính `EquipTempId` cho packet 71; cách lưu (TemplateID + Quality) cần thống nhất.
4. **Giá shop 133 / đơn vị tiền**: cột giá `Shop` (`AUnit/APrice1/AValue1`…) chưa decode; xác nhận `getItemPrice(1)` ánh xạ cột nào và server có special-case `Extra.Info.Coin` cho ShopID 133. Client chỉ check local bằng `model.coinCount` nên nếu server trừ tiền thường client sẽ hiển thị sai.
5. **`GameProperties.VERSION`**: default 12100 (`Bussiness/GameProperties.cs:17`), DB `Server_Config.ClientVersion` có 2 row mâu thuẫn: ID1554=**10200** và ID1677=**12100** — cần xác định loader lấy row nào. Nếu VERSION < 12100, các branch guang/an + ElfEquipId trong 664/1, 664/4, 664/2, 664/13, packet 71 sẽ **không được ghi** ⇒ client đọc thiếu field (desync toàn bộ ELF). Phải đảm bảo VERSION >= 12100.
6. **Nguồn chuỗi `SkillList`**: server `WriteString` ghi thêm NUL (`len+1`); client `readUTF` + `split(",")` — nếu NUL nằm trong phần tử cuối, `int()` có thể trả 0/NaN. Cần test thực tế 1 skill list có ≥1 skill trên client (rủi ro thấp nhưng nên normalize: gửi `"id1,id2"` và verify UI).
7. **Item box config cho 12241/12242** (tỉ lệ ra sách/nguyên liệu): phụ thuộc `ItemBoxMgr.CreateItemBox` + bảng box — ngoài phạm vi decompile client; audit server cần xác nhận.
8. **Điều kiện hiển thị nút toolbar Thần Hộ Mệnh** (level? quest?): chưa thấy gate trong `MainToolBar.as`; có thể do `TankIconManager`/config icon. Phím tắt chắc chắn yêu cầu Level>=30.
9. **`EquipTempId` server ghi ở packet 71**: chưa rõ nguồn (cột `Sys_Users_Elf.EquipTempId` có trong DB nhưng không có logic set trong source); client cần **Id 1..1500** của TS_LinkPalTemplate. Đây là điểm audit server phải chốt.
10. **`664/17 ELF_RESET`**: server stub rỗng; client gửi `(elfId, isBind)` + phí `ElfResetPoint=1000` và confirm `tank.elf.resetTips`; client **không** có listener 664/17 ⇒ sau reset server phải push **664/2 hoặc 664/1** để cập nhật exp/grade về 1. Chưa rõ client mong đợi push nào (cả 2 đều parse được; 664/1 an toàn nhất).

---

## 12. Phụ lục: công thức / ví dụ payload

**707/1** (6 type, level 1–3):
```
01 00000006 | 00000001 00000003 | 00000002 00000001 | 00000003 00000001 |
             00000004 00000002 | 00000005 00000001 | 00000006 00000001
```
**707/2** (2 món):
```
02 00000002 | 00000001 00000004 00000001 | 00000002 00000001 00000003
```
**707/4 request** (nâng Type4 Q1 L1→L2):
```
04 0000BB81(48001) 00000004 00002FFE(12286) 00002FFF(12287)
```
**707/4 response**: `04 00000004 00000002` (= type 4, new level 2).
**664/18 mặc**: `12 00000000 00000001` (elfID=0, recordID=1); tháo (toggle) gửi lại y nguyên.
**664/5 học sách 45101, không khoá**: `05 00000000 0000B02D 00000001 00000000`.
**664/12 blend, khoá 1 skill**: `0C main vice 00000001 000000C8`.

---

## 13. Artifact phụ (không phải deliverable)

| File | Nội dung |
|------|----------|
| `data/elf-work/client/decompress_elf_xml.py` | giải nén 4 XML elf → `data/elf-work/client/xml/*.decoded.xml` |
| `data/elf-work/client/analyze_elf_xml.py` | thống kê TS_LinkPalTemplate/ElfTemplate/SkillBook/Intimacy → `xml_analysis.txt` |
| `data/elf-work/client/extra_xml_extract.py` | item templates (480xx, 122xx), item categories, ServerConfig Elf* → `extra_analysis.txt` |
| `data/elf-work/client/xml_schema.txt` | schema từng XML + toàn bộ 1500 row Linh Bảo |

*Hết.*
