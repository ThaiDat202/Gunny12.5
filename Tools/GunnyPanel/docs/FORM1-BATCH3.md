# Form1 — Batch 3: Tiềm Năng / Phụ Ma / Đúc Hồn / Hỗ Trợ (07/11/2026)

## Deploy

- Process: **PID 19872**
- Port: `127.0.0.1:8527`
- Hash: `49D2A570…E260687E`
- Backup: `Road.Service.exe.bak-20261107-104250`
- Form1 đang mở; `Error.log` rỗng.

## 6. Cấp Vua Gà — max level 10

- Nút **Cấp Vua Gà → Đột Phá** (`button33`).
- Cập nhật `King_Grade_Data`:
  - `ddtKingGrade = 10`
  - `AttackLevel = DefenceLevel = AgilityLevel = LuckyLevel = MagicAttackLevel = MagicDefenceLevel = 10`
  - `usableNum = 10`, `currentExp = 0`
- Online cập nhật `KingGrade.Data` + `PlayerCharacter.ddtKingGrade`.
- User 1 hiện: `ddtKingGrade=10`.

## 7. Bảo Vệ — max level 20

- Nút **Bảo Vệ → Bảo Vệ** (`button32`).
- Cập nhật `Sys_Users_Detail.guardCoreGrade = 20` (max `Guard_Core_Level_Template`).
- User 1 hiện: `guardCoreGrade=20` (guardCoreID 20701 giữ nguyên).
- Vật phẩm liên quan: Hồn Bảo Vệ `12540`, vàng/EXP theo `Guard_Core_Level_Template`.

## 8. Danh Hiệu — thêm tất cả

- Nút **Danh Hiệu → Lên Cấp** (`button42`).
- Xoá `Sys_User_Rank` cũ, insert toàn bộ `New_Title`:
  - `NewTitleID`, `Name`, `Att/Def/Luck/Agi`
  - `Validate = 1`, `IsExit = 1`, `BeginDate = now`, `EndDate = 2099-12-31`.
- Kết quả user 1: **548 danh hiệu**.
- User online cần relog để client nạp danh sách danh hiệu.


## 3.5. Nhẫn Cầu Hôn — Vật định tình 9522 + Ngọc Kim Lan 9900

- Hệ **Sworn** (`Sys_Users_Sworn`), cấu hình cấp từ `ts_swornitem.xml`.
- Cấp 100 = `TotalGp 124860`, stats từ config: **Attack 125 / Defen 50 / Agility 50 / Lucky 48**, skill 1072.
- Nút **Nhẫn Cầu Hôn → Lên Cấp** (`button43`):
  - `swornId = 9522` (Vật định tình+5)
  - `swornCount = 124860`
  - `isHasSwornItem = 1`
  - `skillId = 1072`
- Online cập nhật `SwornInfo`; offline SQL `Sys_Users_Sworn`.
- User 1 hiện: `swornId=9522, swornCount=124860, isHasSwornItem=1, skillId=1072`.


## 1. Tiềm Năng (Nguồn sức mạnh 11689)
- Nút **Tiềm Năng → Kích Hoạt** (`button5`) đã có handler.
- Item 11689 có `Property2=Property3=80` → set max latent cho trang bị:
  - `latentEnergyCurStr = "80,80,80,80"`
  - `latentEnergyNewStr = "80,80,80,80"`
  - `latentEnergyEndTime = 2099-12-31`
- Online: cập nhật trực tiếp `EquipBag`, gửi properties.
- Offline: `UPDATE Sys_Users_Goods ... BagType=0`.

## 2. Phụ Ma (Đá Linh Hồn 11166)
- Nút **Phụ Ma → Phụ Ma** (`button44`) đã có handler.
- Max từ `Magic_Item_Template`: `MagicLevel = 40`, `MagicExp = 298100`.
- Online cập nhật `EquipBag`; offline SQL `BagType=0`.

## 3. Đúc Hồn (Đá Đúc Hồn 12214)
- Nút **Phụ Ma → Đúc Hồn** (`button27`) đã có handler.
- Max từ `TS_UpgradeTemplate` Types 104: `enchantSpiritLevel = 20`.
- Item 11166 + 12214 là material của hệ Đúc Hồn.
- Online cập nhật `EquipBag`; offline SQL `BagType=0`.

## 4. Hỗ Trợ / Tẩy Luyện — Bùa Hộ Thân 390101

- Hệ **Amulet** (Bùa Hộ Thân), item `390101`, trang bị ở `EquipBag` slot 18.
- Nút **Hỗ Trợ → Lên Cấp** (`button39`):
  - `StrengthenLevel = 50` (max `Amulet_Info_Item`)
  - `StrengthenTimes = 0`
- Nút **Hỗ Trợ → Tẩy Luyện** (`button36`):
  - `StrengthenExp = 10000` (= 10 sao, max `Amulet_Grade_Item`)
  - 4 chỉ số mạnh nhất: `Hole1..4 = 1,2,3,4`; giá trị `Hole5=Hole6=Hole5Exp=Hole6Exp=10000`.
  - Mapping đúng `EquipAmuletReplace`: 4 cặp (type, value).
- Online cập nhật `EquipBag`; offline SQL `TemplateID=390101 AND BagType=0`.
- User 1 hiện:
  ```
  StrengthenLevel=50, StrengthenExp=10000
  Hole1=1,Hole2=2,Hole3=3,Hole4=4
  Hole5=10000,Hole6=10000,Hole5Exp=10000,Hole6Exp=10000
  ```

## 5. Trạng thái đã test

- Tiềm năng: `latent=80,80,80,80`
- Phụ ma: `MagicLevel=40, MagicExp=298100`
- Đúc hồn: `enchantSpiritLevel=20`
- Hỗ trợ: Bùa Hộ Thân level 50, tẩy luyện 10 sao/4 chỉ số 10000.

