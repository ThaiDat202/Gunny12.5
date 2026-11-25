# AVATAR COLLECTION — "Bộ Sưu Tập" trang phục/vũ khí (Phụ Kiện)

Trang **`/Users/Details?id=<user>` → khung GM Tool → nhóm **Phụ Kiện** → nút **Bộ Sưu Tập**
mở **toàn bộ bộ sưu tập trang phục/vũ khí** cho user (server-side, vĩnh viễn) và áp **live** nếu user đang online.
Đây là nút đã bấm khi user xem **Users → Chi tiết → GM Tool**.

## 1. Dữ liệu lấy từ đâu

```
Publisher (quest.gn.zing.vn)
  clothgrouptemplateinfo.xml     -> Cloth_Group_Template    (ID, TemplateID, Sex, ...)   Db_Tank_All
  clothpropertytemplateinfo.xml  -> Cloth_Property_Template (ID, Sex, Name, chỉ số, Cost) Db_Tank_All
        │  (GunnyRequestStudio: Rip Request + Promote → Import DB)
        ▼
Db_Tank_All.dbo.Cloth_Group_Template / Cloth_Property_Template
        │
        ├── Road.Service: Bussiness.Managers.AvatarColectionMgr.Init()  (load 1 lần khi start server)
        ├── Client cache (WebSite\Request): ClothGroupTemplateInfo.ashx / ClothPropertyTemplateInfo.ashx
        │      build XML zlib từ DB → clothgrouptemplateinfo.xml, clothpropertytemplateinfo.xml (client tải qua :88)
        └── GM Tool "Bộ Sưu Tập": đọc trực tiếp 2 bảng DB → ghi User_Avatar_Colection cho user
```

## 2. Số liệu sau lần rip data mới (2026-11-09)

| Hạng mục | Trước (old local) | Sau (publisher mới) |
|---|---|---|
| Số bộ (Cloth_Property_Template) | 111 | **147** (ID 1..148) |
| Số dòng Cloth_Group_Template | 3.564 | **1.654** (1.650 TemplateID distinct) |
| Item dùng cho bộ có trong `Shop_Goods` | — | **1.654/1.654** (không thiếu item) |
| Trang bị tối đa 1 bộ | tới 251 ký tự ActiveDress | tối đa **86** ký tự |
| `TemplateAlllist.xml` cho client | 574.402 B | **731.379 B (18.774 item)** — có item mới |

## 3. Nút "Bộ Sưu Tập" làm gì (`GmToolService.AvatarAsync`)

1. `DELETE FROM User_Avatar_Colection WHERE UserID = @uid;`
2. Dựng lại **toàn bộ** bộ từ DB:
   - `Cloth_Property_Template` (147 bộ, Sex 1/2/3)
   - join `Cloth_Group_Template` theo `(ID, Sex)` → `ActiveCount` = số trang bị, `ActiveDress` = danh sách TemplateID.
3. `INSERT` tất cả vào `User_Avatar_Colection` với `endTime = '2099-12-31'` (**vĩnh viễn**).
4. Đọc lại và trả message kiểu: `Đã mở full Bộ Sưu Tập: 147 bộ / 1.654 trang bị (Nam 76, Nữ 52, Khác 19) — vĩnh viễn.`
5. Nếu user **online**: Road GM API áp **live** (reload in-memory + gửi packet), không cần đăng nhập lại.

> Lưu ý: cột `User_Avatar_Colection.ActiveDress` là `nvarchar(max)`; `endTime` phải tương lai (GM set 2099).

## 4. Áp dụng lần 2026-11-10 (đã chạy)

- **Backup**: bảng `Db_Tank.dbo.User_Avatar_Colection_bak_20261110_091740` (738 dòng);
  JSON: `GunnyPanel/data/sql/user-avatar-colection-before-20261110_091740.json`.
- Chạy nút **Bộ Sưu Tập** cho các user 2, 3, 4, 5 (offline) và **7 = Đạt** (đang online → live).
  `test1` (user 1) đã full từ trước (147 bộ, endTime 2099).
- **Kết quả kiểm chứng** (query `Db_Tank.dbo.User_Avatar_Colection`):

| UserID | Nick | Bộ | Trang bị | Nam/Nữ/Khác | ActiveDress rỗng | endTime |
|---|---|---|---|---|---|---|
| 1 | test1 | 147 | 1.654 | 76/52/19 | 0 | 2099-12-31 |
| 2 | test2 | 147 | 1.654 | 76/52/19 | 0 | 2099-12-31 |
| 3 | hú hú | 147 | 1.654 | 76/52/19 | 0 | 2099-12-31 |
| 4 | hihi | 147 | 1.654 | 76/52/19 | 0 | 2099-12-31 |
| 5 | hahaa | 147 | 1.654 | 76/52/19 | 0 | 2099-12-31 |
| 7 | Đạt | 147 | 1.654 | 76/52/19 | 0 | 2099-12-31 |

- **Live check**: `GET http://127.0.0.1:9601/player?userId=7` (Road GM API) trả `"AvatarCount":147` cho user đang online.
- **Client cache**: gọi `http://127.0.0.1:88/ClothGroupTemplateInfo.ashx` + `.../ClothPropertyTemplateInfo.ashx`
  → rebuild `WebSite\Request\clothgrouptemplateinfo.xml` (8.463 B zlib / 1654 item / 147 bộ) và
  `clothpropertytemplateinfo.xml` (2.449 B zlib / 147 bộ); `TemplateAlllist.xml` đã có item mới.
- **Audit log**: `data/logs/audit-20261110.log` có 5 dòng `user.gm.avatar` (id=2,3,4,5,7; id=7 `live=True`).

## 5. Quy trình khi rip/import data mới lần sau

1. GRS: **Rip Request + Promote** → **Import DB** (Cloth_Group_Template, Cloth_Property_Template, Shop_Goods…).
2. Rebuild client cache (hoặc chỉ cần gọi 2 URL build):
   `http://127.0.0.1:88/ClothGroupTemplateInfo.ashx`, `http://127.0.0.1:88/ClothPropertyTemplateInfo.ashx`
   (nếu item thay đổi: `http://127.0.0.1:88/TemplateAllList.ashx`).
3. **Restart Road.Service** (chạy `Service\Service\Run.bat`) để `AvatarColectionMgr` nạp DB mới.
4. Trong panel: từng user → **GM Tool → Phụ Kiện → Bộ Sưu Tập** (hoặc chạy cho tất cả user cần thiết).
   User online sẽ được áp live ngay; user offline nhận khi đăng nhập.
5. Kiểm tra: `SELECT UserID, COUNT(*) FROM Db_Tank.dbo.User_Avatar_Colection GROUP BY UserID` → phải bằng số bộ trong
   `Db_Tank_All.dbo.Cloth_Property_Template` (hiện **147**), và `ActiveDress` không rỗng.

## 6. Ghi chú

- Nếu **client không hiện bộ mới**: kiểm tra `WebSite\Request\clothgrouptemplateinfo.xml` được build lại (mtime mới,
  decode ra 147 bộ) và client tải `REQUEST_PATH` đúng port 88.
- Nếu **bấm Bộ Sưu Tập báo thiếu dữ liệu**: kiểm tra `Db_Tank_All.dbo.Cloth_Group_Template` /
  `Cloth_Property_Template` (import chưa xong?) — message lỗi sẽ ghi rõ.
- Không cần build lại Road.Service sau khi bấm nút; chỉ cần restart server sau khi **import DB** hoặc **rebuild cache**.

## 7. Sự cố 2026-11-21: nick mới bấm "Bộ Sưu Tập" không ăn

### Hiện tượng

Bấm nút **Bộ Sưu Tập** → panel báo *"Bạn đã thêm full danh hiệu rồi"* (ok=true) nhưng vào game
bộ sưu tập **trống**. Không có lỗi nào trong `Road\logs\Error.log`.

### Nguyên nhân (2 lớp)

1. **Server tạo dòng "placeholder" khi login**: `PlayerAvataInventory.CheckNewAvatarColection()`
   (`Full Source\game.server\Game.Server.GameUtils\PlayerAvataInventory.cs:150`) tạo 1 dòng cho mỗi bộ
   chưa sưu tầm với `endTime = Now - 1 ngày` và `ActiveDress = ''`.
2. **Panel kiểm tra "đã full" chỉ đếm số dòng** (147 bộ / 1.654 trang bị) nên tưởng đã xong → return sớm,
   **không DELETE/INSERT lại**.

Kèm theo: `AbstractPacketLib.SendAvatarColectionAllInfo` (packet 402) **chỉ gửi bộ có `ActiveDress` khác rỗng**
→ client nhận 0 bộ; và `UserAvatarColectionInfo.isValidate` so `endTime` với hôm nay → placeholder hết hạn.

### Đã sửa (2026-11-21)

- `GmToolService.AvatarAsync`:
  - Query "hiện trạng" bổ sung `EmptyDress` + `Expired`; chỉ coi là full khi số bộ/trang bị khớp **và**
    không còn dòng nào `ActiveDress` rỗng **và** không có `endTime` < hôm nay.
  - Sau khi INSERT, đọc lại kiểm tra hợp lệ; nếu còn rỗng/hết hạn → trả lỗi rõ ràng thay vì báo thành công.
  - `COUNT(*)` → `COUNT(g.TemplateID)` trong CTE (tránh đếm khống bộ không có trang bị).
  - Message đổi thành *"Bạn đã thêm full Bộ Sưu Tập rồi."* / *"Đã mở full Bộ Sưu Tập: …"*.
- Build `Release` + copy `GunnyPanel.dll` vào `Tools\GunnyPanel\app`
  (backup: `app\GunnyPanel.dll.bak-20261121-0820`), restart `GunnyPanel.exe`.
- Dọn dữ liệu hỏng cho user **8 (datho153)** và **999002 (admin123)**: 147 bộ, `endTime = 2099-12-31`,
  `ActiveDress` đầy đủ (giống user 1–5, 7).

### Hệ quả cho nick mới về sau

Nick mới login lần đầu vẫn có placeholder (đúng thiết kế — chưa sưu tầm), nhưng khi GM bấm
**Bộ Sưu Tập** thì lần nào cũng rebuild đúng, không còn bị "tưởng full".

Nếu muốn nick mới **tự động full ngay khi tạo nhân vật** (không cần bấm nút) thì phải sửa
`PlayerAvataInventory.CheckNewAvatarColection()` trong `Full Source` (điền `ActiveDress` + `endTime` tương lai)
rồi build lại `Road.Service` — chưa làm, chỉ làm khi có yêu cầu.

### Kiểm tra nhanh sau này

```sql
SELECT UserID, COUNT(*) AS Sets, ISNULL(SUM(ActiveCount),0) AS Items,
       MIN(endTime) AS MinEnd,
       ISNULL(SUM(CASE WHEN LTRIM(RTRIM(ISNULL(ActiveDress,''))) = '' THEN 1 ELSE 0 END),0) AS EmptyDress
FROM Db_Tank.dbo.User_Avatar_Colection GROUP BY UserID;
-- Đúng: 147 bộ / 1654 trang bị / MinEnd = 2099-12-31 / EmptyDress = 0
```
