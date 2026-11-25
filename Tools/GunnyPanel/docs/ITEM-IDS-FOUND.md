# Tra cứu TemplateID từ source client/server (07/11/2026)

## Nguồn đã đọc

| Vai trò | Đường dẫn |
|---|---|
| Client Flash hiện tại | `D:\Edit tào lao\Source Flash` |
| Client Flash (bản khác) | `D:\Game\Gunny\flash src bản hiện tại`, `D:\Game\Gunny\DDTank41-main\Source Flash` |
| Server source | `C:\Gunny\Gunny12.6\Full Source` |
| Server source (bản khác) | `D:\Gunny VNG Remake\Full Source`, `D:\Game\Gunny\DDTank41-main` |

## ID tìm được

### Châu báu (client `EquipType.as`)

| Loại | TemplateID |
|---|---|
| Châu báu tấn công (BEAD_ATTACK) | `311500` |
| Châu báu phòng ngự (BEAD_DEFENSE) | `312500` |
| Châu báu thuộc tính (BEAD_ATTRIBUTE) | `313500` |
| Tấn công Lv1–Lv4 | `311199`, `311299`, `311399`, `311499` |
| Phòng ngự Lv1–Lv4 | `312199`, `312299`, `312399`, `312499` |
| Thuộc tính Lv1–Lv4 | `313199`, `313299`, `313399`, `313499` |
| Châu báu chúc phúc công kích/phòng ngự/thuộc tính | `11560`, `11561`, `11562` |
| **Châu Báu Nhanh-ST Lv21** (Shop_Goods) | **`1122540`** |

### Ma thạch (CategoryID 61)

| Loại | TemplateID |
|---|---|
| Ma Thạch Tấn Công | `100101`–`100106` (Lv1–6) |
| Ma Thạch Phòng Thủ | `100201`–`100206` |
| Ma Thạch Nhanh Nhẹn | `100301`–`100306` |
| Ma Thạch May Mắn | `100401`–`100406` |
| Ma Thạch Ma Công/Ma Kháng | `100501`–`100506`, `100601`–`100606` |
| **Ma Thạch Tứ Tượng** | `100901`–`100906` (Lv1–6) |
| **Ma Thạch Tứ Tượng - Truyền Thuyết** (item) | **`122318`** |

### Sổ tay thám hiểm

- Item `Sổ Tay Thám Hiểm`: **`1122910`**
- Các bản khác: `1122904` (Sổ Rách Nát-Đồng Hành), `1122905` (Thi Đấu), `1122906/1122907` (Nguyên Vẹn), `1122908/1122909` (Rực Rỡ).

### Ghi chú khác

- Client `EquipType.HoleMaxLevel = 4`, nhưng server DB `Sys_User_Drills` có 6 lỗ (BeadPlace 13–18, DrillPlace 0–5);
  có thể set `HoleLv = 6` để chứa châu báu cấp cao (JudgeLevel trả true với drill 6).
- Item templates server load từ `SP_Items_All` (bảng `Shop_Goods`), không phải file XML rời.

## Chưa tìm thấy ID chính xác

Các tên sau **không có trong source client/server đang đọc và không có trong `Shop_Goods`**:

- Châu báu **Siêu Giáp Lv21**
- Châu báu **Siêu ST Lv21**
- Châu báu **ST Giáp**
- Châu báu **Thần Báo Lv21**
- Châu báu **Chống Đào Lv21**
- Châu báu **Hấp Thụ Lv21**

→ Có thể đây là **custom item** trong một DB/nguồn khác chưa nằm trong các source trên. Cần:
1. Tên chính xác trong game hoặc TemplateID; hoặc
2. File produce/SQL thêm các item đó; hoặc
3. Xác nhận chúng nằm ở bảng/DB nào (ngoài `Db_Tank_All.Shop_Goods`).

## Đề xuất bước tiếp theo

- Có thể bắt đầu ngay với phần **chắc chắn**:
  - Mở full 6 mũi khoan (`HoleLv = 6`).
  - Gửi **15× `1122540`** (Châu Báu Nhanh-ST Lv21).
  - Gửi **8× `122318`** (Ma Thạch Tứ Tượng - Truyền Thuyết).
  - Gửi **`1122910`** (Sổ Tay Thám Hiểm).
- Chờ ID 6 loại châu báu còn lại để hoàn thiện đúng yêu cầu.
