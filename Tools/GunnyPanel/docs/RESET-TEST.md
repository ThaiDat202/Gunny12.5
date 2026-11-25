# Reset test + thay đổi ngày 07/11 (12:12)

## 1. Châu báu
- Nút **Châu Báu → Mở Mũi Khoan** giờ gửi:
  - **15× mỗi loại**: `313615` (Nhanh-ST), `313623` (Siêu Giáp), `313622` (Siêu ST), `313621` (ST-Giáp)
  - **1× mỗi loại**: `311545`, `312519`, `312521`, `311541`, `311540`
- **ID Nhanh-ST cũ là `1122540`** → đã đổi sang **`313615`** theo yêu cầu.

## 2. Bảo Vệ — level 50 prerequisite
- Nút **Bảo Vệ → Bảo Vệ**:
  - Nếu `Grade < 50`: nâng lên **level 50** (`GP = 63832646`, mốc LevelInfo).
  - Nếu `Grade >= 50`: không hạ level.
  - Sau đó set `guardCoreGrade = 20` (max).

## 3. Phụ Ma / Đúc Hồn — chỉ Nhẫn + Vòng Tay
- Online: chỉ xử lý item **đang trang bị** có `CategoryID = 8 (Vòng tay)` hoặc `9 (Nhẫn)`.
- Offline SQL giới hạn `TemplateID IN Shop_Goods CategoryID 8/9`.
- Nhẫn Vật Định Tình `9522` là category 9 → áp dụng đúng.

## 4. Reset database để test lại

| Hạng mục | Trạng thái hiện tại |
|---|---|
| Châu báu trong túi | **0** |
| Ma thạch (BagType 41) | **0** |
| Mũi khoan (`Sys_User_Drills`) | **0** |
| Bộ sưu tập (`User_Avatar_Colection`) | **0** |
| Album thẻ (slot 5+) | **0** (còn 5 thẻ trang bị) |
| Danh hiệu (`Sys_User_Rank`) | **0** |
| Sổ tay | level 1, progress 0, havePage 0, 0 page/debris |
| Vinh Dự | level 1, exp 0 |
| Cấp Vua Gà | 0 |
| Bảo Vệ | level 1 |
| Nhẫn Cầu Hôn | swornId 1, count 0, isHasSwornItem 0 |
| Bùa Hộ Thân | level 1, exp 0, holes -1 |
| Tiềm năng / Magic / Đúc hồn trên trang bị | reset 0 |
| Mail test/GM | 0 |
| Vũ khí/quần áo/nón/trang bị khác | **giữ nguyên** (33 item) |

## 7. Phụ Ma / Đúc Hồn chỉ áp dụng khi đang trang bị

- Nguyên nhân cũ:
  - `EquipBag.GetItems()` trả về cả item trong túi (Place >= 31).
  - Hàm kiểm tra còn yêu cầu `IsEnchant()` → nhẫn/vòng tay thật bị coi là không hợp lệ, dù đang mặc.
- Đã sửa:
  - Chỉ lấy item **Place 0–30** (ô trang bị).
  - Nhận **CategoryID 8, 9, 28, 29** (Vòng tay/Nhẫn).
  - Bỏ điều kiện `IsEnchant()`.
  - Vẫn loại Vật Định Tình `9022/9122/9222/9322/9422/9522` và Ngọc Kim Lan `9900`.
  - **Đúc Hồn** không còn bắt buộc `MagicLevel > 0`.
- Nếu không có item hợp lệ đang mặc → hiện:
  **“Hãy trang bị nhẫn và vòng tay.”**
- User 1 hiện có **4 item hợp lệ đang trang bị** → bấm Phụ Ma/Đúc Hồn sẽ nâng trực tiếp, không hiện cảnh báo.

## 9. Tự test trước khi giao lại

Đã chạy test SQL trong transaction và rollback:

| Test | Kết quả |
|---|---|
| Item hợp lệ đang trang bị | **4** |
| Phụ Ma update | **4 dòng** |
| Đúc Hồn update | **4 dòng** |
| Vật Định Tình 9522 | **không đổi** (`MagicLevel=0`, `enchant=0`) |
| Chuyển item ra túi | eligible = **0** |
| Phụ Ma khi item trong túi | **0 dòng** |
| Rollback | OK |

### Dữ liệu sau test (để bạn test lại)
- 2 vòng tay `85484` (Place 7,8): `MagicLevel=0`, `MagicExp=0`, `enchant=0`
- 2 nhẫn `95484` (Place 9,10): `MagicLevel=0`, `MagicExp=0`, `enchant=0`
- Vật Định Tình `9522` (Place 16): `MagicLevel=0`, `enchant=0`
- Tổng item hợp lệ đang trang bị: **4**
- Không còn dữ liệu test nào khác.

### Deploy
- Process: **PID 12688**
- Port: `127.0.0.1:8527`
- Đã copy đủ 27 file build; backup `_backup-dlls-20261107-123852`.
- `Error.log` rỗng.


---

# Reset lần 2 — v0.27 (07/11/2026 18:00)

Mục đích: xoá toàn bộ dữ liệu do 15 nút **Công cụ GM — Set Full** tạo ra cho **test1 (UserID=1)**
để test lại từ đầu.

## Script

```powershell
# Xem kế hoạch, không ghi DB
powershell -ExecutionPolicy Bypass -File _tools\reset-test-user1.ps1 -DryRun

# Reset thật (tự kick nếu đang online)
powershell -ExecutionPolicy Bypass -File _tools\reset-test-user1.ps1 -Kick

# Reset + xoá luôn Money/Gold/GiftToken
powershell -ExecutionPolicy Bypass -File _tools\reset-test-user1.ps1 -Kick -ResetCurrency
```

- Backup trước khi reset: `data/reset-user1-before-<timestamp>.json`.
- Toàn bộ UPDATE/DELETE chạy trong **1 transaction**; lỗi → rollback toàn bộ.
- Script tự kiểm tra online (DB State + Road GM API), nếu online mà không có `-Kick` thì dừng.

## Dữ liệu đã reset

| Hạng mục | Sau reset |
|---|---|
| Nhân vật | Grade=10, GP=0, guardCoreGrade=0 |
| Cấp Vua Gà (`King_Grade_Data`) | ddtKingGrade=0, usableNum=0, các level=0 |
| Vinh Dự (`Sys_Users_Honor`) | HonorLevel=1, HonorExp=0 |
| Nhẫn Cầu Hôn (`Sys_Users_Sworn`) | swornId=1, swornCount=0, isHasSwornItem=0, skillId=0 |
| Danh hiệu (`Sys_User_Rank`) | 0 dòng |
| Bộ sưu tập (`User_Avatar_Colection`) | 0 dòng |
| Album thẻ (`Sys_Users_Card` Place>=5) | 0 dòng (giữ 5 thẻ trang bị) |
| Sổ tay (`Sys_Users_ExplorerManualInfo` + pages/debris) | level 1, progress 0, havePage 0, 0 page/debris |
| Mũi khoan (`Sys_User_Drills`) | 6 drill HoleLv=0 |
| Mail (`User_Messages`) | 0 |
| Châu báu (BagType 21) / Ma Thạch (BagType 41) / item mail (BagType 1) | 0 |
| Chỉ số trên trang bị | cường hoá/khảm/hole/tiềm năng/phụ ma/đúc hồn/đúc vàng = 0 |
| Mũi khoan / item test khác | đã xoá |

**Giữ nguyên**: 28 trang bị đang mặc (BagType 0) — trong đó có **2 vòng tay 85484 (Place 7,8)**,
**2 nhẫn 95484 (Place 9,10)** và **Bùa Hộ Thân 390101 (Place 18)** để test ngay
Phụ Ma / Đúc Hồn / Hỗ Trợ Lên Cấp / Tẩy Luyện mà không cần Award thêm.

## Sau reset — checklist test GM

1. Đăng nhập lại **test1**.
2. Vào `http://127.0.0.1:91/Users/Details?id=1` → badge **Road GM API OK**.
3. Bấm lần lượt từng nút; user online phải thấy đổi ngay (không relogin):
   - Cấp Vua Gà: level 10→70, Cấp Vua Gà 0→10.
   - Bảo Vệ: level lên 50, Bảo Vệ 0→20.
   - Vinh Dự: 1→100, **HP tăng**.
   - Nhẫn Cầu Hôn: cấp 100.
   - Sổ Tay: level 1→21.
   - Danh Hiệu: 0→~548.
   - Hỗ Trợ Lên Cấp/Tẩy Luyện: Bùa Hộ Thân 0→50 / 10 sao.
   - Tiềm Năng: 80/80/80/80; Phụ Ma: level 40; Đúc Hồn: level 20.
   - Châu Báu: drill 0→6 + 9 mail; Ma Thạch: 8 mail có đủ 6 chỉ số (154).
   - Bộ Sưu Tập: 0→111; Full Album: card list đầy.
4. Xem phụ lục: `_tools/ROAD-GM-API.md` phần test.

---

# Bổ sung v0.29 — checklist test GM đầy đủ (33 nút)

1. **Ma Thạch**: bấm → 8 mail; mở hộp thư, item phải có **đủ 6 chỉ số** (Công/Thủ/Nhanh/May/Ma công/Ma kháng = 154), Lv10.
2. **Phụ Ma / Đúc Hồn**: cần ít nhất 1 nhẫn/vòng tay **đang trang bị** (Nhẫn Cầu Hôn 9522 cũng tính);
   item chỉ nằm trong túi → báo “Bạn chưa trang bị nhẫn và vòng tay.”
3. **Hỗ Trợ**: Bùa Hộ Thân phải **đang trang bị ô 18**; trong túi → báo “Bạn chưa trang bị bùa hộ thân.”
4. **Tiềm Năng**: chỉ Kính/Mắt/Mặt/Tóc/Set quần áo/Set cánh **đang trang bị**; tháo hết → báo lỗi.
5. **Bộ Sưu Tập**: online bấm 1 phát phải full ngay (không cần relogin).
6. **Nút mới**: Tu luyện (Cơ Bản/TT Cấp 2/Nguyên Tố), Thẻ bài (Lên Cấp/Đột Phá),
   Vật tổ (Lên Cấp/Tăng Bậc), Miếu thần (Chúc Phúc/Thăng Hoa), Dát vàng (VK/Nón/Quần Áo),
   Tăng sao (VK/Nón/Quần Áo), Chiến Hồn, Chế Tạo, Tiến Hoá VK Phụ.
   - Chiến Hồn / Tăng Sao / Tiến Hoá VK Phụ **cần online** (Tiến Hoá cần đã trang bị VK phụ).
7. Test tự động: `_tools\test-live-gm-v029.ps1` (chờ user online rồi chạy + kiểm tra DB),
   hoặc watcher `_tools\watch-online-and-test.ps1` (tự chạy khi test1 login).
