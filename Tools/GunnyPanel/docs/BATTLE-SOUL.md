# CHIẾN HỒN (Battle Soul / FigSpirit) — nút GM "Chiến Hồn"

Trang **`/Users/Details?id=<user>` → khung GM Tool → nhóm **Chiến Hồn** → nút **Chiến Hồn**
(`data-gm-feature="battle_soul"`).

## 1. Dữ liệu nằm ở đâu

5 slot Chiến Hồn là 5 dòng trong **`Db_Tank.dbo.Sys_User_Gemstone`** (cùng bảng với Ngọc):

| EquipPlace | Loại hồn | FigSpiritId | Chỉ số |
|---|---|---|---|
| `2` | Tóc (hari) | `100001` | Tấn công |
| `3` | Mặt (face) | `100004` | May mắn |
| `5` | Kính (glass) | `100003` | Nhạy nhẹn |
| `11` | Bộ (suit) | `100002` | Phòng thủ |
| `13` | Trang trí (decoration) | `100005` | Máu |

* `FigSpiritIdValue` = 3 mức `level,exp,place`, tối đa `6,0,0|6,0,1|6,0,2`.
* Client map place → slot theo `GemstoneManager.playerFigSpiritinit` (`equipPlace - 2` → 0/1/3/9/11).
* Bảng template: `Db_Tank_All.dbo.Fight_Spirit_Templatelist` (chỉ có 100001–100005, level 0..6),
  client tải `http://127.0.0.1:88/fightspirittemplatelist.xml`.

> **Quan trọng:** client (`GemstoneCriView.setCurInfo`, `GoodTip.creatGemstone`) **chỉ hiển thị chỉ số
> khi `fightSpiritId ∈ 100001..100005`**. Nếu slot bị dòng Ngọc (47001–47037) chiếm chỗ → tooltip
> **trống chỉ số** dù server đã "nâng full".

## 2. Sự cố 2026-11-21 (datho153 = user 8)

### Hiện tượng

Bấm nút **Chiến Hồn** → panel báo `Đã nâng Chiến Hồn lên tối đa.` (ok=true, live=true) nhưng vào game
rê chuột vào các slot **không thấy chỉ số**; 2 slot (11, 13) trống hẳn.

### Nguyên nhân (3 lớp)

1. **Slot 2/3/5 bị Ngọc chiếm chỗ**: user 8 có `FigSpiritId = 47004 / 47003 / 47028` ở place 2/3/5
   (ID ngọc) thay vì `100001 / 100004 / 100003`, và **không có dòng nào ở place 11 / 13**.
2. **Server chỉ tạo 5 dòng mặc định khi user CHƯA có dòng ngọc nào**:
   `GamePlayer.LoadGemStone()` (`Full Source\game.server\Game.Server.GameObjects\GamePlayer.cs:~4510`)
   kiểm tra `if (m_GemStone.Count == 0)` rồi mới tạo 5 dòng level 0. Nick mới đã nhận 32 Ngọc
   (Quà Tân Thủ / EquipPlace 0) ⇒ **không bao giờ có dòng Chiến Hồn**.
3. **Nút GM cũ chỉ sửa value**: `GmApiActions.BattleSoul` chỉ `gem.FigSpiritIdValue = "6,0,0|..."`
   cho các place **đã tồn tại**, `if (gem == null) continue;` → không set `FigSpiritId`, không tạo slot
   thiếu, vẫn trả về thành công (`any = true` khi có ít nhất 1 dòng).

Log Road.Service **không có lỗi** cho Chiến Hồn (chỉ có NRE `totem_level` của feature khác).

### Đã sửa (2026-11-21)

1. **Panel**: thêm `GmToolService.BattleSoulAsync` — **UPSERT** 5 slot:
   * dòng **không phải hồn** đang chiếm slot → đưa về túi ngọc (`EquipPlace = 0`), **không xoá item**;
   * dòng hồn trùng slot → giữ ID nhỏ nhất;
   * dòng hồn đã có → chuẩn hoá `FigSpiritId` + `FigSpiritIdValue = 6,0,0|6,0,1|6,0,2`;
   * slot thiếu → `INSERT` với đúng loại hồn;
   * đọc lại kiểm tra đủ 5 slot, đúng 100001–100005, đúng value → mới báo thành công.
2. **Định tuyến feature** (`RoadGmApiClient`): bỏ `battle_soul` khỏi `ExtraApiFeatures`
   (để panel ghi DB), thêm vào `LiveFeatures` (sau khi ghi DB vẫn gọi Road GM API để áp live
   — API reload `LoadGemStone` từ DB rồi gửi `SendPlayerFigSpiritUp` cho cả 5 slot).
   `GmToolService.FeatureOrder` + `RunAsync` thêm `battle_soul`.
3. **Dọn dữ liệu user 8**: 5 slot đúng `100001/100004/100003/100002/100005`, value level 6;
   3 viên ngọc 47003/47004/47028 được đưa về túi (EquipPlace 0), không mất item.
4. Build + deploy `GunnyPanel.dll` (backup `app\GunnyPanel.dll.bak-20261121-0855`), restart panel.
   Không cần rebuild/restart Road.Service.

### Hệ quả cho nick mới về sau

* Nick mới có Ngọc trong túi nhưng **không có dòng Chiến Hồn** → bấm nút là panel tự tạo đủ 5 slot
  + đẩy live (nếu online), không còn báo "chưa có dữ liệu Chiến Hồn".
* Nick có data lệch (slot bị Ngọc chiếm, thiếu slot, sai loại hồn) → bấm nút là chuẩn hoá.
* Nếu muốn **nick mới tự có 5 slot level 0 ngay khi tạo nhân vật** (không cần bấm nút) thì phải sửa
  `GamePlayer.LoadGemStone()` (bỏ điều kiện `Count == 0`, luôn `EnsureFightSpiritSlots()`) và build lại
  `Road.Service` — **chưa làm**.

## 3. Kiểm tra nhanh

```sql
SELECT EquipPlace, FigSpiritId, FigSpiritIdValue
FROM Db_Tank.dbo.Sys_User_Gemstone
WHERE UserID = <uid> AND EquipPlace IN (2,3,5,11,13)
ORDER BY EquipPlace;
-- Đúng: 2=100001, 3=100004, 5=100003, 11=100002, 13=100005
--       value = 6,0,0|6,0,1|6,0,2 ; không có 2 dòng cùng place
```

Kiểm tra live API:

```powershell
$tok = (Get-Content 'C:\Gunny\Gunny12.5\Tools\GunnyPanel\app\data\road-gm-api-token.txt' -Raw).Trim()
Invoke-RestMethod -Uri 'http://127.0.0.1:9601/gm/apply' -Method POST `
  -Headers @{ 'X-GM-Token' = $tok } -ContentType 'application/json' `
  -Body '{"userId":8,"feature":"battle_soul"}'
# → ok:true, message "Đã nâng Chiến Hồn lên tối đa."
```

## 4. Reset để test nút "Chiến Hồn"

```sql
-- backup
SELECT * INTO Db_Tank.dbo.Sys_User_Gemstone_CHIENHON_bak_YYYYMMDD
FROM Db_Tank.dbo.Sys_User_Gemstone
WHERE EquipPlace IN (2,3,5,11,13) AND FigSpiritId BETWEEN 100001 AND 100005;

-- xoá dữ liệu Chiến Hồn (không đụng Ngọc trong túi)
DELETE FROM Db_Tank.dbo.Sys_User_Gemstone
WHERE EquipPlace IN (2,3,5,11,13) AND FigSpiritId BETWEEN 100001 AND 100005;
```

Sau đó bấm nút **Chiến Hồn** trong panel → `BattleSoulAsync` tạo lại đủ 5 slot + áp live.
Khôi phục: `INSERT INTO Db_Tank.dbo.Sys_User_Gemstone (UserID, FigSpiritId, FigSpiritIdValue, EquipPlace)
SELECT UserID, FigSpiritId, FigSpiritIdValue, EquipPlace FROM ..._bak_...;`

> Đã reset ngày 2026-11-21 09:00 (40 dòng → backup `Sys_User_Gemstone_CHIENHON_bak_20261121_0900`).
> Lưu ý: đừng mở cửa sổ Chiến Hồn trong game **trước** khi bấm nút — client có thể lỗi khi danh sách rỗng;
> bấm nút GM trước rồi mở panel.

## 5. Ghi chú

* Bảng `Sys_User_Gemstone` dùng chung cho **Ngọc** (place 0–8) và **Chiến Hồn** (place 2/3/5/11/13);
  đừng đưa ngọc vào place 2/3/5 (sẽ chiếm slot Chiến Hồn và làm tooltip trống).
* `AddBaseGemstoneProperty` (`PlayerEquipInventory.cs`) chỉ cộng chỉ số cho `EquipPlace 1..7`
  → 2 slot Chiến Hồn `11` (Phòng thủ) và `13` (Máu) **không được cộng vào nhân vật**.
  Đây là hành vi hiện tại, chưa sửa (cần xác nhận có phải bug).
