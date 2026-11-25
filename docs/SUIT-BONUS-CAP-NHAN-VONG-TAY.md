# Thưởng bộ "Cặp Nhẫn / Cặp Vòng Tay" (Suit Bonus) — 2026-11-22

> Trước đây: dữ liệu `Suit_Template` + `Suit_Part_Equip` có sẵn, **client chỉ hiển thị**,
> **server không cộng chỉ số**. Nay đã implement server-side.

---

## 1. Dữ liệu (Db_Tank_All)

### Cột mới trong `Suit_Template`

| Cột | Ý nghĩa |
|---|---|
| `BonusAttack`, `BonusDefence`, `BonusAgility`, `BonusLuck` | cộng 4 chỉ số cơ bản |
| `BonusHP` | cộng HP |
| `BonusDamage`, `BonusArmor` | cộng Sát thương / Hộ giáp |
| `BonusMagicAttack`, `BonusMagicDefence` | cộng Ma công / Ma kháng |
| `BonusReducePercent` | % giảm sát thương phải chịu |
| `MinStrengthen` | cường hóa tối thiểu của **từng món** để được tính (0 = không yêu cầu) |

Điều kiện kích hoạt: số món đang mặc thuộc bộ **≥ `EqipCount2`** (tất cả bộ "Cặp" đều = 2).

### Giá trị đã seed

| SuitId | Tên bộ | Điều kiện | Thưởng |
|---|---|---|---|
| 13–18 | Cặp đôi Athena | 2 món | Công/Thủ/Nhanh/May +50 |
| 33, 34 | 10 Tuổi | 2 món ≥ +5 | Công/Thủ/Nhanh/May +25 |
| 43, 44 | 11 Tuổi | 2 món ≥ +5 | 4 chỉ số +25, HP +250 |
| 51, 52 | 12 Tuổi | 2 món ≥ +5 | 4 chỉ số +28, HP +500 |
| 57, 58 | 13 Tuổi | 2 món ≥ +5 | 4 chỉ số +30, HP +500, giảm ST 1% |
| 68, 69 | 14 Tuổi | 2 món ≥ +5 | Thủ/Nhanh/M.Công/M.Kháng +35, giảm ST 2% |
| 82, 83 | 15 Tuổi | 2 món ≥ +5 | ST & HG +5, M.Công/M.Kháng +40, giảm ST 3% |
| 100, 101 | 16 Tuổi | 2 món | ST & HG +10, M.Công/M.Kháng +50, giảm ST 4% |
| **123, 124** | **17 Tuổi (Cặp Vòng Tay / Cặp Nhẫn)** | 2 món | **ST & HG +12, M.Công/M.Kháng +60, giảm ST 5%** |
| 122 | 17 Tuổi (Cánh + Bong Bóng) | 2 món | ST & HG +17, giảm ST 7% |

Backup bảng cũ: `dbo.Suit_Template_bak_20261122`.

---

## 2. Code server đã sửa

| File | Thay đổi |
|---|---|
| `SqlDataProvider\SqlDataProvider.Data\SuitTemplateInfo.cs` | thêm 11 property `Bonus*` / `MinStrengthen` |
| `Bussiness\Bussiness\ProduceBussiness.cs` | `InitSuitTemplateInfo` đọc cột mới qua `ReadIntColumn` (thiếu cột → 0, không crash) |
| `game.server\Game.Server.GameObjects\GamePlayer.cs` | thêm `GetSuitBonus(...)` + `GetSuitDamage/GetSuitArmor/GetSuitReducePercent`; `GetBaseAttack` → `num2 = GetSuitDamage()`; `GetBaseDefence` → `num = GetSuitArmor()`; `UpdateReduceDame` cộng % giảm ST của bộ |
| `game.server\Game.Server.GameUtils\PlayerEquipInventory.cs` | cộng 4 chỉ số + HP + Ma công/Ma kháng vào tổng; `CreateProp("Suit", …)`; `MagicAttack/MagicDefence → Suit` |

Luồng: `UpdatePlayerProperties` → `GetSuitBonus` (đếm món theo `Suit_Part_Equip.ContainEquip`, lọc `MinStrengthen`) → cộng vào tổng → `UpdateBaseProperties` → `UpdateReduceDame` (cộng % giảm ST) → `UpdateFightPower` (Damage/Armor đã gồm thưởng bộ).

---

## 3. Kiểm tra sau khi deploy

Ví dụ **Hồ Thái Đạt** (2× Vòng Tay 17 Tuổi + 2× Nhẫn 17 Tuổi + Cánh/Bong Bóng 17 Tuổi):

| Nguồn | Damage | Armor | Ma Công | Ma Kháng | Giảm ST |
|---|---|---|---|---|---|
| Bộ 123 (2 vòng tay) | +12 | +12 | +60 | +60 | 5% |
| Bộ 124 (2 nhẫn) | +12 | +12 | +60 | +60 | 5% |
| Bộ 122 (cánh + bong bóng) | +17 | +17 | – | – | 7% |
| **Tổng** | **+41** | **+41** | **+120** | **+120** | **17%** |

Kỳ vọng: chỉ số trong panel tăng đúng như trên, lực chiến tăng nhẹ; tháo 1 món → mất thưởng của bộ đó.
Các nick khác đang mặc 2 món cùng bộ (test1, test2, Hạt Đồ…) cũng được cộng.

---

## 4. Deploy

```bat
C:\Gunny\Gunny12.5\DEPLOY-SUIT-BONUS.bat      ← chuột phải → Run as administrator
```

Script: stop Road → backup `*.bak-suit-<stamp>` → copy 6 DLL mới → start Road → chờ port 8527/9601 → in log cuối.
(Chỉ restart Road; Center/Fight không cần đụng.)

---

## 5. Rollback

```powershell
# 1) DLL
Copy-Item "C:\Gunny\Gunny12.5\Service\Service\Road\Game.Server.dll.bak-suit-<stamp>" "...\Game.Server.dll" -Force
# (tương tự các DLL khác, rồi restart Road)

# 2) Dữ liệu
DROP TABLE Db_Tank_All.dbo.Suit_Template;
SELECT * INTO Db_Tank_All.dbo.Suit_Template FROM Db_Tank_All.dbo.Suit_Template_bak_20261122;
-- (hoặc chỉ cần UPDATE các cột Bonus* = 0 để tắt thưởng)
```

---

## 6. Hạn chế / ghi chú

* "Giảm ST bị bạo kích" (bộ 122) **chưa** mô hình hóa — hiện chỉ áp dụng giảm sát thương thường.
* Client không cần sửa: tooltip bộ (`LaterEquipmentGoodView`) đã hiển thị đúng; client **không** kiểm tra điều kiện +5 (server kiểm tra).
* Bộ áo giáp (SuitId 19/20/21/22…) vẫn chưa có thưởng vì `Bonus*` = 0 — muốn thêm thì cập nhật cột tương ứng, không cần build lại (server đọc DB khi khởi động / reload SuitMgr).
* Sau khi sửa `Suit_Template`, có thể reload không cần restart: dùng chức năng reload `suit` của Center/Road (nếu có), hoặc restart Road.
---

## 7. Phía client (bổ sung 2026-11-22)

Client **không tự tính** thưởng bộ; nó chỉ hiển thị theo dữ liệu XML + cộng chỉ số theo
`propertyAddition[...]["Suit"]` do server gửi. Hai thứ phải bật/đúng:

### a) Cờ `SUIT_ENABLE` trong config XML

`ConfigParaser`: `SUIT_ENABLE = <SUIT enable="..."> != "false"`.
Cờ này điều khiển **cả** tooltip bộ (`GoodTip.showSuitTip` → `LaterEquipmentGoodView`)
**và** phần cộng chỉ số trong bảng Thuộc tính (`StaticFormula.getSuitAddition`).

Đã bật ở tất cả config client:

| File | Trước | Sau |
|---|---|---|
| `Website\config.xml` | (không có thẻ) | `<SUIT enable="true" />` |
| `Website\Flash\config.xml` | (không có thẻ) | `<SUIT enable="true" />` |
| `Website\FlashVNG\config.xml` | (không có thẻ) | `<SUIT enable="true" />` |
| `Website\Request\config.xml` | `<SUIT enable="false" />` | `<SUIT enable="true" />` |
| `Website\Request\config (4).xml` | `<SUIT enable="false" />` | `<SUIT enable="true" />` |

### b) Dữ liệu XML cho client

Client tải (nén zlib, loader type 5 `COMPRESS_TEXT_LOADER`):

* `http://127.0.0.1:88/SuitTemplateInfoList.xml` ← bảng `Suit_Template`
* `http://127.0.0.1:88/SuitPartEquipInfoList.xml` ← bảng `Suit_Part_Equip`

Đã sinh lại từ DB (128 bộ / 274 part, gồm 123/124), backup `*.bak-suit-xml-<stamp>`.

> Nếu sửa `Suit_Template` / `Suit_Part_Equip`, phải **sinh lại 2 file XML** (và restart client)
> thì tooltip mới cập nhật. Định dạng: `<Result value="true" message="Success!"><item .../></Result>`,
> nén zlib level 9.

### c) Cache client

Client đọc config + XML **lúc khởi động** → sau khi đổi phải thoát hẳn client và xoá cache:

```powershell
Remove-Item "$env:LOCALAPPDATA\37abc\User Data\Default\Cache\*" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$env:APPDATA\Macromedia\Flash Player\#SharedObjects\*" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$env:LOCALAPPDATA\Pepper Data\Shockwave Flash\WritableRoot\#SharedObjects\*" -Recurse -Force -ErrorAction SilentlyContinue
```

Kết quả kiểm chứng (2026-11-22): tooltip hiển thị panel "Cặp Nhẫn 17 Tuổi (2/2)" +
dòng thưởng xanh; server log/API xác nhận `Suit=24` (Damage/Armor) cho nick mặc 2 bộ.
---

## 8. Bộ giáp (armor set) — 2026-11-22

Trước đây chỉ bộ "Cặp Nhẫn/Cặp Vòng Tay" có thưởng; các bộ giáp (SuitId 1–128) dù tooltip hiện mô tả
nhưng server **không cộng gì**. Nay đã mở rộng cho **tất cả 128 bộ**.

### Bảng mới `Suit_Bonus` (Db_Tank_All)

| Cột | Ý nghĩa |
|---|---|
| `SuitId`, `NeedCount` | bộ + số món cần (mốc 2 / 3 / 4) |
| `MinStrengthen` | cường hóa tối thiểu từng món (bộ "Cặp" 10–15 Tuổi = 5) |
| `BonusAttack/Defence/Agility/Luck/HP` | chỉ số cơ bản |
| `BonusDamage/Armor` | Sát thương / Hộ giáp |
| `BonusMagicAttack/MagicDefence` | Ma công / Ma kháng |
| `BonusReducePercent` | % giảm sát thương nhận |
| `BonusCritReducePercent` | % giảm ST bị bạo kích (**lưu nhưng chưa áp dụng** — server chưa có cơ chế) |

* 142 dòng / 128 bộ, seed bằng cách map chính xác từ `SkillDescribe2/3/4` (50 mô tả tier2 + 10 tier3 + 1 tier4).
* SP đọc dữ liệu: `SP_Suit_Bonus_All`.
* Quy tắc: chọn **mốc cao nhất** mà số món đang mặc đạt được (2 → 3 → 4), cộng đúng mốc đó
  (mô tả mốc sau đã bao gồm mốc trước).

### Code

| File | Thay đổi |
|---|---|
| `SqlDataProvider.Data\SuitBonusInfo.cs` | model mới |
| `Bussiness\ProduceBussiness.cs` | `GetAllSuitBonus()` + `InitSuitBonusInfo()` (đọc `SP_Suit_Bonus_All`) |
| `Bussiness.Managers\SuitMgr.cs` | load `m_SuitBonuses` (sort giảm dần theo `NeedCount`), `FindSuitBonuses(int)` |
| `game.server\GamePlayer.cs` | `GetSuitBonus` dùng bảng mốc; thêm `ItemMatchesSuitParts`; fallback về `Suit_Template.Bonus*` nếu bảng trống |

### Ví dụ (test2 — Hiệp Sĩ Rồng)

| Bộ | Số món | Mốc áp dụng | Thưởng |
|---|---|---|---|
| 19 (Nón/Tóc/Áo/Set) | 4 | tier 4 | Công-Thủ-Nhanh-May +15, HP +200, giảm ST 2% |
| 20 (Kính/Mặt/Mắt) | 3 | tier 3 | Công-Thủ-Nhanh-May +10, HP +200, giảm ST 2% |
| **Tổng** | | | **+25 mỗi chỉ số, +400 HP, giảm ST 4%** |

Deploy: chạy lại `DEPLOY-SUIT-BONUS.bat` (Admin) để copy DLL mới + restart Road.
---

## 9. Giảm sát thương bị bạo kích (crit reduce) — 2026-11-22

### Cơ chế

| Bước | Chi tiết |
|---|---|
| Dữ liệu | `Suit_Bonus.BonusCritReducePercent` (đã seed: bộ 9/10 = 5%, 122 = 7%, 54/56/59/61… = 3%, 24/36/38/40/42 = 2%…) |
| Tính | `GamePlayer.GetSuitBonus(... out critReducePercent)` cộng theo mốc bộ cao nhất; `PlayerEquipInventory` gán `PlayerCharacter.CritReducePercent` |
| Áp dụng | `Living.TakeDamage`: `criticalAmount -= criticalAmount * critReduce / 100` (chặn tối đa 100%) trước khi cộng vào sát thương |
| Truyền sang Fight | Packet player info (Road → Fight) thêm 3 int: `ReduceDamePlus`, `GoldenReduceDamage`, `CritReducePercent` |

> Trước đây `ReduceDamePlus`/`GoldenReduceDamage` **không được truyền** sang Fighting.Service,
> nên giảm sát thương theo badge/bộ chỉ có tác dụng ở phía Road. Nay đã truyền đủ 3 giá trị.

### Code

| File | Thay đổi |
|---|---|
| `SqlDataProvider.Data\PlayerInfo.cs` | thêm `public int CritReducePercent { get; set; }` |
| `game.server\GamePlayer.cs` | `GetSuitBonus` thêm out `critReducePercent`; helper `GetSuitCritReducePercent()` |
| `game.server\PlayerEquipInventory.cs` | gán `PlayerCharacter.CritReducePercent` |
| `Game.Logic\Living.cs` | `TakeDamage` giảm `criticalAmount` theo % (trước khi cộng damage) |
| `game.server\FightServerConnector.cs` | ghi thêm 3 int sau `MagicDefence` |
| `fighting.server\ServerClient.cs` | đọc thêm 3 int tương ứng |

### Deploy — BẮT BUỘC cập nhật cả 2 service

```bat
C:\Gunny\Gunny12.5\DEPLOY-SUIT-CRIT.bat      (Admin)
```
Script: stop Road + Fighting → backup `*.bak-crit-<stamp>` → copy DLL cho **cả hai** →
start Fighting (7539) → start Road (8527 + 9601).

> Packet đã đổi định dạng: **không được chạy Road mới với Fight cũ** (hoặc ngược lại) —
> hai service phải cùng bản build.

### Kiểm tra

* Mặc bộ có crit reduce (vd. bộ 9 "Dã lang" hoặc 10 "Thiên ưng": 5%; bộ 122: 7%) → khi bị đánh chí mạng,
  phần sát thương cộng thêm do bạo kích giảm đúng %.
* Bộ 54/56/59/61…: 3%; bộ 24/36/38/40/42: 2%.

### Kiểm chứng 2026-11-22 15:51 (sau khi sửa `SuitMgr.ReLoad`)

`SuitMgr.ReLoad()` trước đó **không nạp** `LoadSuitBonuses()` → `m_SuitBonuses` null → server rơi vào nhánh
legacy (chỉ đọc cột `Suit_Template.Bonus*`) nên bộ giáp & crit reduce không có tác dụng.
Đã bổ sung `LoadSuitBonuses()` (có try/catch) + `Interlocked.Exchange(ref m_SuitBonuses, ...)`.

Log `SendAddRoom` sau khi deploy (Road khởi động 15:49:30):

| Nick | Trang bị | reduceDame | critReduce |
|---|---|---|---|
| test1 | Bong Bóng 17 Tuổi (1/2) | 22 | 0 |
| test2 | Cánh + Bong Bóng 17 Tuổi (2/2) | 29 | **7** |

* test2 = badge 9 + bộ 19(2) + 20(1) + 123(5) + 124(5) + 122(7) = 29; crit 7%
* test1 = badge 9 + 19(2) + 20(1) + 123(5) + 124(5) = 22; crit 0%

FightPower tăng +80 Σ4 chỉ số và +~1.800 HP hiển thị (thưởng bộ 19+20).
`Fight\Game.Logic.dll` có `PlayerInfo.get_CritReducePercent()` trong `Living.TakeDamage` → crit bị giảm đúng %.