# TRẠNG THÁI CLIENT · DATABASE · WEBSHOP — Gunny 12.5

> **Đây là file sống (living document).** Ghi lại toàn bộ kết quả khảo sát của phiên 2026-11-22.
> Các khối nằm giữa cặp marker `AUTO:...:START` / `AUTO:...:END` do script sinh lại từ hệ thống thật.
> **Đừng sửa tay các khối AUTO** — hãy chạy script ở §0.

| | |
|---|---|
| Ngày tạo | 2026-11-22 ~18:35 (Asia/Bangkok) |
| Người tạo | DSH agent "lead" — phiên khảo sát client/DB/webshop |
| Phạm vi | Client (`Website\*`), Database (6 DB online + 5 DB rác), Webshop (web + shop in-game), hạ tầng service |
| Tính chất | Khảo sát **chỉ ĐỌC**. Ngoài ra: **22/11 21:17 sửa WS-01** (port 91 → 90) và **22/11 22:15 sửa CL-03** (patch client `2.png`+`4.png`) |
| Quyền của agent | **KHÔNG phải Administrator** (`DESKTOP-SMLR4VU\hotha`, Elevated = False) → không stop/start service được |
| File script | [`refresh-trang-thai.ps1`](refresh-trang-thai.ps1) |

---

## 0. Cách dùng & cơ chế tự cập nhật

### 0.1 Chạy lại để cập nhật

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Gunny\Gunny12.5\docs\refresh-trang-thai.ps1"
```

Script sẽ:
1. Kiểm tra **14 vấn đề** (WS-*, SEC-*, CL-*, DB-*) bằng probe thật: đọc file, `Test-Path`, `Get-NetTCPConnection`, HTTP GET, `sqlcmd`.
2. Ghi lại §1 (snapshot), §6.1 (bảng trạng thái), §9 (changelog).
3. So trạng thái với lần chạy trước (`.trang-thai-state.json`) — **chỗ nào vừa được sửa sẽ tự động chuyển `ISSUE` → `OK`** và ghi 1 dòng changelog có ngày giờ.

Script **chỉ đọc** hệ thống, ngoại trừ việc ghi 3 file: file `.md` này, `.trang-thai-state.json`, và gọi endpoint `/shopcheapitemlist.ashx` (endpoint này không ghi file XML, chỉ trả response).

> **Vì sao các khối AUTO viết không dấu (ASCII)?** Script `.ps1` phải **thuần ASCII** thì Windows PowerShell 5.1 mới không lỗi encoding (bài học từ chính phiên này — xem §7.4). Nội dung văn xuôi trong file vẫn là tiếng Việt đầy đủ; chỉ phần do máy sinh là không dấu.

### 0.2 Quy tắc bắt buộc cho phiên sau

> **Sau KHI sửa bất kỳ mục nào trong §6 → PHẢI chạy lại script** để tài liệu khớp thực tế.
> Nếu sửa xong mà trạng thái vẫn `ISSUE`, nghĩa là sửa chưa đúng chỗ — script là "test tự động" của tài liệu này.

### 0.3 Thứ tự ưu tiên xử lý (đề xuất)

`SEC-01` + `SEC-02` + `SEC-03` (bảo mật, không cần restart) → `WS-01` + `WS-02` (link chết, không cần restart) → `DB-01` (dọn 13,6 GB, cần Admin) → còn lại.

---

## 1. Snapshot hạ tầng

<!-- AUTO:SNAPSHOT:START -->
| Muc | Gia tri |
|---|---|
| **Kiem tra luc** | **2026-11-24 10:29** (Asia/Bangkok) |
| Service | Road.Service=PID 18592 @ 11-24 08:42 \| Fighting.Service=PID 9040 @ 11-24 08:42 \| Center.Service=PID 19524 @ 11-24 08:42 \| GunnyPanel=PID 35912 @ 11-24 08:43 |
| Port | 80=LISTEN \| 88=LISTEN \| 90=LISTEN \| 91=CLOSED \| 92=CLOSED \| 93=CLOSED \| 1593=LISTEN \| 4109=LISTEN \| 7539=LISTEN \| 8527=LISTEN \| 9601=LISTEN |
| DLL Road Game.Server | Game.Server.dll = 3,120,640 B @ 2026-11-22 15:48:40 |
| DLL Road Bussiness | Bussiness.dll = 843,776 B @ 2026-11-22 15:48:28 |
| DLL Road Game.Logic | Game.Logic.dll = 657,920 B @ 2026-11-22 11:33:38 |
| DLL Fight Fighting.Server | Fighting.Server.dll = 72,704 B @ 2026-11-22 11:36:17 |
| DLL Fight Game.Logic | Game.Logic.dll = 657,920 B @ 2026-11-22 11:33:38 |
| Source .cs | 4684 file / so-file-cs-moi-hon-DLL = 0 |
| O dia C | con trong 46.9 GB / da dung 191.0 GB |
| DB online | Db_Tank=3267.6 MB \| Db_Tank_All=840.7 MB \| Db_Membership=85.6 MB \| Db_Count=11.3 MB \| Db_Logs=6.9 MB \| Db_GM=6.5 MB |
| DB rac (tong) | 13910.9 MB \| recovery_pending = 4 |
| Bang backup | Db_Tank=23 \| Db_Tank_All=8 |
| Shop_Goods (Db_Tank_All) | 18774 |
| Shop_Goods (Db_Tank) | 18780 |
| Shop rows | 7081 |
| Shop_Goods_Show_List | 3132 |
| Suit_Template / Suit_Bonus | 128 / 142 |
| Site_Webshop_Item / webshop_bag | 0 / 0 |
| eStore / Pay_Exchange | 161 / 0 |
| Kich thuoc thu muc Request (XML) | 367 file .xml / 35.8 MB |
| Client 2.png (main / VNG) | 0626ACE0D004 / 3DC743EEDE09 |
| Client 4.png (main / VNG) | 5C8B518F9D60 / 1F9359B28A6A |
<!-- AUTO:SNAPSHOT:END -->

**Đọc snapshot thế nào:**

| Trường | Ý nghĩa |
|---|---|
| `so-file-cs-moi-hon-DLL` | Nếu **> 0** → source đang lệch bản chạy, đã sửa mà chưa build/deploy |
| `Port 91/92/93` | Phải LISTEN nếu muốn nút Webshop hoạt động |
| `Bang backup` | Số bảng rác; giảm khi dọn DB |
| `Kich thuoc thu muc Request (XML)` | 367 file XML tĩnh; mỗi lần gọi `.ashx` sẽ ghi lại 1 file |

---

## 2. CLIENT

### 2.1 File SWF thật

| File | Kích thước | MD5 (12) | Kết luận |
|---|---:|---|---|
| `Website\2.png` | 3.334.642 B | `514726D304E0` | = `Website\Flash\2.png` ✅ |
| `Website\4.png` | 7.213.359 B | `7E65A460AE52` | = `Website\Flash\4.png` ✅ |
| `Website\FlashVNG\2.png` | 3.331.228 B | `3DC743EEDE09` | ❌ **bản cũ 15/11, chưa patch** |
| `Website\FlashVNG\4.png` | 7.209.693 B | `1F9359B28A6A` | ❌ **bản cũ 15/11, chưa patch** |

Khác biệt dung lượng so với bản chính: 2.png nhỏ hơn 3.414 B, 4.png nhỏ hơn 3.666 B → đúng phần code FightPower 64-bit + SUIT tooltip bị thiếu.

`config.xml` (bản chính) trỏ `FLASHSITE = http://127.0.0.1/flash/` → client đang dùng `Website\Flash` (đã patch). **Chỉ khi có ai trỏ sang `FlashVNG` mới gặp lỗi.**

### 2.2 Đường nạp client

```
PlayGame.aspx
  └─ Loading.swf?<Content>&config=<Config>   (FlashVars = <AutoParam>)
       └─ 2.png / 4.png  (SWF thật, đổi đuôi để né bộ lọc)
```

Asset phụ: `DDT_Loading.swf` 329.297 B · `Loading.swf` 298.726 B · `ui\vietnam\language.png` 207.881 B.

### 2.3 config.xml — 5 bản

| File | MD5 | Kích thước | SUIT |
|---|---|---:|---|
| `Website\config.xml` | `537DE265` | 4.579 B | ✅ true |
| `Website\Flash\config.xml` | `1F1CE06A` | 4.611 B | ✅ true |
| `Website\FlashVNG\config.xml` | `7893D35E` | 4.406 B | ✅ true |
| `Website\Request\config.xml` | `AE8B5ACB` | 7.442 B | ✅ true |
| `Website\Request\config (4).xml` | `AE8B5ACB` | 7.442 B | ✅ true |

Cờ khác: `<LANGUAGE value="vietnam" />` · `ALLOW_MULTI=true` · `STATISTIC=true` · `SUCIDE_TIME=120` · `GAME_BOXPIC=1` · `REQUEST_PATH=http://127.0.0.1:88/`.

**Version client = 101** (chuỗi update 97→98→99→100→101 trong `config.xml`).

### 2.4 Link chết trong config

| Cờ | Trỏ tới | Thực tế |
|---|---|---|
| `FILL_PATH` (nạp thẻ) | `http://127.0.0.1/ddtLogin/Charge.aspx` | ❌ thư mục `ddtLogin` **không tồn tại** |
| `LOGIN_PATH` | `http://127.0.0.1/Default.aspx` | ❌ `Default.aspx` **không tồn tại** (chỉ có `Index.html`) |
| `SITE` | `http://gunny.vcdn.vn/` | CDN ngoài — dùng cho ảnh item, không có internet thì ảnh hỏng |

### 2.5 Source AS3 đã decompile

| Bộ | File `.as` | Dung lượng |
|---|---:|---:|
| `ToolGunny\_analysis\4png_scripts\scripts` | 7.439 | 36,5 MB |
| `ToolGunny\_analysis\client4_scripts\scripts` | 7.347 | 36,3 MB |
| `ToolGunny\_analysis\preloader1_scripts\scripts` | 371 | 2,1 MB |

Module shop trong client: `store` **198 file** (`fineStore` 25, `godRefining` 14, `forge` 13, `view` 68, `data` 10, `morn` 10) · `auctionHouse` 42 · `shop` **31** · `gypsyShop` 22 · `storefineseal` 20 · `firstRecharge` 19 · `vip` 18 · `continueRecharge` 10 · `HappyRecharge` 10 · `vipIntegralShop` 6 · `oldplayerintegralshop` 4 · `vipCoupons` 4 · `RechargeRank` 4 · `conRecharge` 4 · `rechargeBack` 6 · `yyvip` 9 · `roleRecharge` 5.

Điểm client gọi XML shop:

| File AS3 | Dòng |
|---|---|
| `ddt\loader\LoaderCreate.as` | 628 |
| `ddt\manager\ShopManager.as` | 1047 |
| `shop\view\ShopRightView.as` | 142 |
| `email\_84$73.as` | 316 |

### 2.6 Cache client khi test

Xoá 3 nơi:
```
%LOCALAPPDATA%\37abc\User Data\Default\Cache\*
%APPDATA%\Macromedia\Flash Player\#SharedObjects\*
%LOCALAPPDATA%\Pepper Data\Shockwave Flash\WritableRoot\#SharedObjects\*
```

---

## 3. DATABASE

### 3.1 DB đang ONLINE (6)

| DB | Size (MB) | Tables | Vai trò |
|---|---:|---:|---|
| `Db_Tank` | 3.267,6 | 214 | Nhân vật, item user, active, quà |
| `Db_Tank_All` | 840,7 | 256 | Template, shop, suit, server config |
| `Db_Membership` | 85,6 | 22 | Tài khoản web + **eStore/Pay_***/Mem_*** |
| `Db_Count` | 11,3 | 69 | Log, đếm |
| `Db_Logs` | 6,9 | 13 | Log |
| `Db_GM` | 6,5 | 14 | Panel (Category 48, Table_Games 225) |

### 3.2 DB rác — 13.910,9 MB (~13,6 GB)

| DB | Size (MB) | state_desc | Truy cập | Ghi chú |
|---|---:|---|---|---|
| `BKorigT` | 3.267,6 | RECOVERY_PENDING | ❌ | Bản sao Db_Tank |
| `BKpet` | 3.267,6 | RECOVERY_PENDING | ❌ | |
| `BKpetOld` | 3.267,6 | RECOVERY_PENDING | ❌ | |
| `BKcmpA` | 840,7 | RECOVERY_PENDING | ❌ | Bản sao Db_Tank_All |
| `Db_TankSchemaTemp` | 3.267,6 | **ONLINE** | ✅ 193 bảng | Bản sao schema — vẫn query được |

Ổ C: còn **46,3 GB trống** (đã dùng 191,5 GB).

> 4 DB `RECOVERY_PENDING` là file mồ côi — không attach được, chỉ DROP được bằng `ALTER DATABASE ... SET OFFLINE` + drop, hoặc detach rồi xoá file (cần Admin).
> `Db_TankSchemaTemp` ONLINE nên `DROP DATABASE` bình thường.

### 3.3 Bảng lớn nhất

**Db_Tank_All**

| Bảng | Rows |
|---|---:|
| `Shop_Goods_Box` | 44.614 |
| `Shop_Goods` | 18.774 |
| `Shop` | 7.081 |
| `Pet_Skill_Element_Info` | 4.959 |
| `Quest_Goods` | 4.128 |
| `Pet_Skill_Info` | 3.763 |
| `Shop_Goods_Show_List` | 3.132 |
| `NPC_Info` | 3.068 |
| `Item_Record` | 2.427 |
| `Quest_Condiction` | 2.289 |
| `Quest` | 2.063 |
| `Server_Config` | 2.029 |

**Db_Tank**

| Bảng | Rows |
|---|---:|
| `Shop_Goods` | 18.780 ⚠️ |
| `Sys_User_Rank` | 3.625 |
| `Sys_Users_Goods` | 3.363 |
| `Sys_Users_ExplorerManualInfoDebris` | 3.150 |
| `QuestData` | 2.270 |
| `User_Avatar_Colection` | 441 |
| `Sys_Users_Mark` | 300 |
| `Active_Award` | 289 |
| `Sys_NewbieGiftItems` | 289 |
| `User_Messages` | 260 |

### 3.4 Bộ trang bị (Suit) — còn nguyên ✅

| Bảng | Rows |
|---|---:|
| `Suit_Template` | 128 |
| `Suit_Bonus` | 142 |
| `Suit_Part_Equip` | 274 |
| `Shop_Goods_Categorys` | 46 |

Khớp đúng tài liệu `SUIT-BONUS-CAP-NHAN-VONG-TAY.md`.

### 3.5 Phát hiện phụ (không phải lỗi, nhưng đừng hiểu nhầm)

1. **`Db_Tank.Shop_Goods` (18.780) ≠ `Db_Tank_All.Shop_Goods` (18.774)** — Db_Tank dư 6 dòng: `52000502` LEGEND-Thánh Kiếm Theia · `52000503/504/505/506` Nón truyền thống · `52000507` LEGEND-Thánh Kiếm Theia. Server đọc `Db_Tank_All` nên 6 dòng này vô hại, nhưng gây lệch số liệu khi tra cứu.
2. **`Shop_Goods.Valid = 0` cho toàn bộ 18.774 dòng.** Cột này **không được xuất ra XML client** (`shopitemlist_out.xml` không có attribute `Valid`) → **không phải lỗi**, là cột chết.
3. `Max_Level_Template` **không phải** bảng backup — nó khớp nhầm filter `%temp%` (vì "Template" chứa "temp"). Khi đếm backup phải loại nó ra.

### 3.6 Bảng backup/rác — 31 bảng (23 + 8)

**`Db_Tank` (23):**

`bak_active_20261120_2140` · `bak_active_award_20261120` · `bak_active_award_20261120_now` (284) · `bak_dt153_Gemstone` · `bak_dt153_Goods` · `bak_dt153_Goods2_20261120` · `bak_dt153_ListBox` · `bak_dt153_mail2_20261120` · `bak_dt153_Messages` · `bak_gem_user8_20261120` · `bak_giftitems_20261120` · `bak_giftitems_preGem_20261120` · `bak_goods_latent_20261119` · `bak_mark_20261120` · `bak_maxcount_20261120_1128` · `bak_newbie_items_20261120` · `bak_shopgoods_maxcount_20261120` · `bak_sp_Active_PullDown_20261120` · `Sys_NewbieGiftItems_bak_20261119` · `Sys_User_Gemstone_CHIENHON_bak_20261121_0900` · `Sys_User_Gemstone_CHIENHON_bak_20261121_1836` · `User_Avatar_Colection_bak_20261121_0832` (**1.176 dòng**) · `TempTable` (0 dòng)

**`Db_Tank_All` (8):**

`Active_Award_bak2_20261119` · `Active_bak_dsh_20261119` · `Active_bak2_20261119` · `bak_active_20261120_2140` · `bak_activeaward_20261120` · `bak_gmactivity_20261120_2145` · `GM_Activity_bak_dsh_20261119` · `Suit_Template_bak_20261122` ⚠️ **giữ lại** (là backup có chủ đích, ghi trong `NEW-SESSION-CONTEXT.md` §2)

### 3.7 Bảo mật `Website\Web.config`

| Dòng | Nội dung |
|---:|---|
| 18 | `<add key="conString" ... Initial Catalog=Db_Tank; User ID=sa; password=...` — **plaintext** |
| 19 | `crosszoneString` → `Db_Tank_All`, sa plaintext |
| 20 | `membershipDb` → `Db_Membership`, sa plaintext |
| 23 | `Db_TankConnectionString` → `Db_Tank_All`, sa plaintext |
| 33 | `<compilation debug="true" targetFramework="4.0">` |
| 41 | `<customErrors mode="Off" defaultRedirect="Error.htm">` |
| 49–50 | Đăng ký handler PHP 7.0.33 và 5.6.31 (`C:\v7.0\php-cgi.exe`, `C:\v5.6\php-cgi.exe`) |

---

## 4. WEBSHOP

> **Kết luận: webshop vẫn KHÔNG hoạt động** — 3 lớp độc lập, **lớp 1 đã sửa 22/11 21:17**, lớp 2 và 3 còn hỏng.

### 4.1 Lớp 1 — Nút "Webshop" trong game ✅ ĐÃ SỬA (22/11 21:17)

`Website\PlayGame.aspx` (dòng 151) — trước đây trỏ port 91 (đóng), **đã đổi sang port 90**:

```html
<li class="menu_list highligh" ><a href="http://127.0.0.1:90/" target="_blank" class="menu btnPay">
  <em class="icon16 gold_16_orange"></em><span class="name">Webshop</span></a></li>
```

- Port 90 = **GunnyPanel** (LISTEN) → bấm "Webshop" giờ mở được panel.
- Đã kiểm: **0 chỗ nào còn `127.0.0.1:91`** trong toàn bộ `Website`.
- **File backup:** `Website\PlayGame.aspx.bak-20261122-2116`
- **Sửa ở mức byte** (1 byte, offset 7290: `1`→`0`) để **giữ nguyên UTF-8 BOM** — file có tiếng Việt (`Người Chơi`, `[Đăng Xuất]`). Nếu xoá BOM, ASP.NET có thể đọc sai encoding.
- Không cần restart IIS / build lại `Tank.Flash.dll`: chỉ đổi markup, ASP.NET tự recompile trang.
- **Lưu ý còn lại:** panel yêu cầu đăng nhập, và trang đích là `/` (login của panel), **không phải** `/Shop`. Muốn mở thẳng trang tra cứu thì đổi thành `http://127.0.0.1:90/Shop`.

### 4.2 Lớp 2 — Bảng dữ liệu webshop rỗng

| Bảng | Rows | Cột chính |
|---|---:|---|
| `Db_Tank.dbo.Site_Webshop_Item` | **0** | `Id, templateid, price, countBuy, lastBuy, attackCompose, defenceCompose, agilityCompose, luckCompose, avanco, validade, transfer, renew` |
| `Db_Tank.dbo.webshop_bag` | **0** | `UserId, TemplateId, Count, timeadd, isget, ID, timeget, Server, Price` |

→ Dù có dựng site thì cũng **không có item nào để bán**.

### 4.3 Lớp 3 — Hạ tầng nạp tiền (`Db_Membership`)

| Bảng | Rows | Trạng thái |
|---|---:|---|
| `eStore` | 161 | Catalog webshop cũ — `AddDate` **2013-12-27**, `AddUser='SqlStudio'` → dữ liệu 13 năm trước |
| `Pay_Card` | 2.694 | Thẻ nạp |
| `Pay_Info` | 45 | Đơn nạp |
| `Pay_Way` | 4 | Cổng thanh toán |
| `Pay_Exchange` | **0** | Lịch sử đổi xu — **chưa từng có giao dịch** |
| `Pay_History` | **0** | |
| `Mem_ActiveCode` | 20.200 | Giftcode |
| `Mem_Users` / `Mem_UserInfo` | 11 / 11 | Tài khoản |
| `Mem_Module` / `Mem_Right` | 117 / 153 | Phân quyền CMS |
| `Mem_Paths` | **0** | Menu CMS rỗng |

`eStore` schema: `StoreID, TemplateID, PriceValue, StrengthenLevel, AttackCompose, DefendCompose, LuckCompose, AgilityCompose, IsBinds, ValidDate, BuyMany, Type, AddDate, AddUser, PriceValue1`.

Không tìm thấy thư mục website nào đọc `eStore`. Chỉ có **1 `w3wp` (PID 33240, 210 MB)** phục vụ site Gunny + Request.

### 4.4 Cái gì THẬT SỰ chạy được

**a) Shop in-game** — dữ liệu đầy đủ, xem §5.

**b) Pipeline XML** — test live ngày 22/11, tất cả trên port 88:

| Endpoint | Kết quả thực tế | Ghi chú |
|---|---|---|
| `/ShopItemList.ashx` | ✅ `Build: ShopItemList.xml, Success!` | sinh file zlib 72.500 B |
| `/ShopGoodsShowList.ashx` | ✅ `Build: ShopGoodsShowList.xml, Success!` | sinh file zlib 9.795 B |
| `/searchgoodstemp.ashx` | ✅ `Build: searchgoodstemp.xml, Success!` | sinh file 569 B |
| `/shopcheapitemlist.ashx` | ⚠️ `<Result value="true" message="Success!" />` | **rỗng** — không có item nào thoả `IsCheap=1 AND EndDate>now AND Label=4` |
| `/SuitTemplateInfoList.ashx` | ❌ 404 | Đúng: suit là file `.xml` tĩnh, không có ashx |

> **Cơ chế quan trọng:** các `.ashx` này **sinh file XML tĩnh** trong `Website\Request\` (zlib header `78 DA`), rồi client tải file `.xml`. Không stream trực tiếp.
> Tổng **367 file `.xml`** trong `Website\Request`, tất cả đều tháng 11/2026.
> File "dịch ngược" `*_out.xml` (không nén) là do công cụ phân tích tạo, **không phải client đọc**.

**c) Panel — chỉ tra cứu read-only**

`Tools\GunnyPanel\src\GunnyPanel\Pages\Shop.cshtml` — trang **Shop Explorer**, 100% `SELECT`, không mua/bán.
- Panel chạy port **90**; `http://127.0.0.1:90/Shop` trả về trang đăng nhập (1.644 B, title "Đăng nhập - Gunny Panel") nếu chưa login.
- Dòng 5 ghi cứng `"…Shop_Goods (15.060 item)…"` — **số đã lỗi thời** (thực tế 18.774).

---

## 5. SHOP TRONG GAME (`Db_Tank_All`)

| Bảng | Rows | Ý nghĩa |
|---|---:|---|
| `Shop` | 7.081 | Giá A/B/C (`AUnit/APrice1/AValue1`…), `BuyType` 0/1, `Label` (tab), `IsCheap`, `StartDate/EndDate`, `LimitCount`, `LimitGrade` |
| `Shop_Goods` | 18.774 | Template item — 48 cột (`Attack/Defence/Agility/Luck`, `Colors`, `Hole`, `SuitId`, `Reclaim*`, `Can*`…) |
| `Shop_Goods_Box` | 44.614 | Nội dung hộp/túi mở ra (`TemplateId, ItemCount, StrengthenLevel, *Compose, Random`) |
| `Shop_Goods_Show_List` | 3.132 | Map `ShopId → Type` (tab hiển thị) |
| `Shop_Goods_Categorys` | 46 | Tên thể loại tiếng Việt |
| `Shop_GoodsTemp` | 82 | |
| `Shop_GoXu` | 2 | |
| `Shop_Vip` | 22 | |
| `Vip_Store` | 30 | |
| `VipStoreList` | 29 | |
| `Mystery_Shop_Items` | 184 | Shop bí ẩn |
| `MinigameShopTemplate` | 11 | |
| `BattleTeamShopItemList` | 33 | |

**Phân bố `Shop.Label`:** `0` → 4.216 · `1` → 1.522 · `2` → 1.241 · `4` → 16 · `6` → 80 · `100` → 6.

**46 thể loại** (`Shop_Goods_Categorys`): Nón, Kính, Tóc, Mặt, Áo, Mắt, Vũ khí, Vòng tay, Nhẫn, Đạo cụ chiến đấu, Đạo cụ hỗ trợ, Vật phẩm nhiệm vụ, Set quần áo, Dây chuyền, Cánh, Bong bóng, Vũ khí phụ, Hộp thẻ, Hỗ trợ, nước tu luyện, Item kết hôn, Pháo, Sách nhiệm vụ tu luyện, Thẻ danh hiệu, Quà tặng, Thẻ bài, Vũ khí đặc biệt, Vòng tay đặc biệt, Nhẫn tay đặc biệt, Đạo cụ đặc biệt, Vũ khí phụ đặc biệt, Hạt giống, Phân hóa học, Thực phẩm, Trứng thú cưng, Cây trồng, Trang trí vườn nhà, Huân chương, Vũ khí thú cưng, Nón thú cưng, Áo thú cưng, Chiến hồn, Ma thạch, Thẻ gọi pet, Mảnh vũ khí, Item hỗ trợ.

---

## 6. Danh sách vấn đề

### 6.1 Trạng thái (tự kiểm) — KHỐI AUTO

<!-- AUTO:ISSUES:START -->
**Tom tat:** 1 OK / 13 ISSUE / 0 ERR - kiem tra luc 2026-11-24 10:29

| ID | Trang thai | Bang chung (tu kiem) |
|---|---|---|
| WS-01 | **OK** | href=http://127.0.0.1:90/ port=90 LISTEN |
| WS-02 | ISSUE | FILL_PATH=http://127.0.0.1/ddtLogin/Charge.aspx -> target missing |
| WS-03 | ISSUE | Site_Webshop_Item=0 rows |
| WS-04 | ISSUE | webshop_bag=0 rows |
| WS-05 | ISSUE | endpoint empty (42 B) |
| SEC-01 | ISSUE | Website\Web.config debug="true" |
| SEC-02 | ISSUE | Website\Web.config customErrors mode="Off" |
| SEC-03 | ISSUE | 4 connection string(s) with plaintext password |
| CL-01 | ISSUE | main 2=0626ACE0D004 4=5C8B518F9D60 / VNG 2=3DC743EEDE09 4=1F9359B28A6A |
| CL-02 | ISSUE | LOGIN_PATH=http://127.0.0.1/Default.aspx -> target missing |
| DB-01 | ISSUE | recovery_pending=4 schemaTemp=1 total=13910.9 MB |
| DB-02 | ISSUE | Db_Tank=23 Db_Tank_All=8 total=31 |
| DB-03 | ISSUE | 6 extra TemplateID in Db_Tank |
| DB-04 | ISSUE | Shop.cshtml says 15.060, actual=18774 |
<!-- AUTO:ISSUES:END -->

### 6.2 Chi tiết & bằng chứng

| ID | Mức | Vấn đề | Bằng chứng |
|---|---|---|---|
| **WS-01** | ✅ Đã sửa | ~~Nút Webshop trỏ port 91~~ → **đã đổi sang port 90** (22/11 21:17) | `Website\PlayGame.aspx:151` → `http://127.0.0.1:90/`; port 90 LISTEN; backup `.bak-20261122-2116` |
| **WS-02** | 🔴 Cao | Nạp thẻ (`FILL_PATH`) trỏ thư mục không tồn tại | `Website\config.xml` → `/ddtLogin/Charge.aspx`; `Website\ddtLogin` không có |
| **WS-03** | 🟠 TB | `Site_Webshop_Item` rỗng | 0 dòng |
| **WS-04** | 🟠 TB | `webshop_bag` rỗng | 0 dòng |
| **WS-05** | 🟡 Thấp | Shop khuyến mãi rỗng | `/shopcheapitemlist.ashx` → không có `<Item>` |
| **SEC-01** | 🔴 Cao | `debug="true"` | `Website\Web.config:33` |
| **SEC-02** | 🔴 Cao | `customErrors mode="Off"` | `Website\Web.config:41` |
| **SEC-03** | 🔴 Cao | Connection string `sa` plaintext ×4 | `Website\Web.config:18,19,20,23` |
| **CL-01** | 🟠 TB | `FlashVNG` chưa patch | MD5 `3DC743EEDE09` / `1F9359B28A6A` ≠ bản chính |
| **CL-02** | 🟡 Thấp | `LOGIN_PATH` → `Default.aspx` không tồn tại | `Website\config.xml`; file không có |
| **CL-03** | ✅ **Đã sửa** | Phụ kiện "vật để ngồi" (210/214/231/251) che mất nhân vật — 2 lỗi: z-order (head dưới mount) + thiếu tư thế NGỒI | `starling\scene\hall\player\HallPlayerDraw.as` + `horse\_139$25.as` — **sửa xong 22/11 22:15**, quy trình đầy đủ ở §11 |
| **DB-01** | 🟠 TB | 5 DB rác chiếm 13.910,9 MB | 4 `RECOVERY_PENDING` + `Db_TankSchemaTemp` |
| **DB-02** | 🟡 Thấp | 31 bảng backup/rác (23 + 8) | xem §3.6 |
| **DB-03** | 🟡 Thấp | `Db_Tank.Shop_Goods` dư 6 dòng | `52000502`–`52000507` |
| **DB-04** | 🟡 Thấp | Panel Shop ghi cứng "15.060 item" | `Pages\Shop.cshtml:5`; thực tế 18.774 |

### 6.3 Kế hoạch sửa từng mục

**WS-01 — Sửa nút Webshop** ✅ **ĐÃ LÀM 22/11 21:17** *(không cần Admin, không cần restart service)*

```diff
- <a href="http://127.0.0.1:91/" target="_blank" class="menu btnPay">
+ <a href="http://127.0.0.1:90/"  target="_blank" class="menu btnPay">
```

Đã verify: `refresh-trang-thai.ps1` → `WS-01 OK  href=http://127.0.0.1:90/ port=90 LISTEN`; không còn `127.0.0.1:91` trong `Website`; trang vẫn trả 200.

**Rollback:**
```powershell
Copy-Item 'C:\Gunny\Gunny12.5\Website\PlayGame.aspx.bak-20261122-2116' `
          'C:\Gunny\Gunny12.5\Website\PlayGame.aspx' -Force
```

Lưu ý: `PlayGame.aspx` có `CodeBehind="playgame.aspx.cs"`, nhưng link nằm ở HTML tĩnh → IIS đọc lại ngay, **không cần build lại `Tank.Flash.dll`**.
Muốn mở thẳng trang tra cứu thì đổi tiếp thành `http://127.0.0.1:90/Shop` (hiện đang là `/` → trang đăng nhập panel).

**WS-02 — Sửa nạp thẻ**: hoặc tạo thư mục `Website\ddtLogin\` + trang `Charge.aspx`, hoặc đổi `FILL_PATH` sang trang nạp thẻ đang có (`Website\Request\ChargeMoney.aspx`). File có trong `Request`, không có trong `Website` → nếu dùng phải trỏ `http://127.0.0.1:88/ChargeMoney.aspx`.

**WS-03/04 — Webshop**: cần quyết định *làm thật* hay *bỏ nút*. Nếu làm thật: nhập `Site_Webshop_Item` (templateid + price, tham chiếu `Db_Tank_All.Shop_Goods`) và dựng site đọc 2 bảng này.

**WS-05**: chỉ là thiếu dữ liệu — thêm dòng `Shop` với `IsCheap=1, Label=4, EndDate > now`.

**SEC-01/02/03** *(không cần Admin)*: `debug="false"`, `customErrors mode="RemoteOnly"`, mã hoá `connectionStrings` bằng `aspnet_regiis -pef`.

**CL-01**: nếu muốn đồng bộ, copy `Website\2.png`,`4.png` → `Website\FlashVNG\` (backup trước). **Chỉ nên làm khi xác nhận không site nào dùng FlashVNG.**

**CL-02**: sửa `LOGIN_PATH` → `http://127.0.0.1/Index.html` hoặc tạo `Default.aspx`.

**DB-01** *(cần Admin)*: script mẫu
```sql
-- chạy bằng sqlcmd / SSMS với quyền sysadmin
ALTER DATABASE BKorigT  SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE BKorigT;
ALTER DATABASE BKpet    SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE BKpet;
ALTER DATABASE BKpetOld SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE BKpetOld;
ALTER DATABASE BKcmpA   SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE BKcmpA;
DROP DATABASE Db_TankSchemaTemp;   -- ONLINE, drop bình thường
```
Với 4 DB `RECOVERY_PENDING` nếu `DROP` báo lỗi: detach/xoá file `.mdf/.ldf` thủ công trong `C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\`. **Backup trước.**

**DB-02**: DROP từng bảng `bak_*`/`TempTable`. **Giữ `Suit_Template_bak_20261122`.**

**DB-03**:
```sql
DELETE FROM Db_Tank.dbo.Shop_Goods WHERE TemplateID BETWEEN 52000502 AND 52000507;
```

**DB-04**: sửa chữ trong `Shop.cshtml:5` — bỏ số cứng, hoặc đổi thành 18.774.

---

## 7. Nhật ký phiên khảo sát (2026-11-22)

### 7.1 Đã đọc / kiểm tra

1. `docs\NEW-SESSION-CONTEXT.md` (bản đầu phiên, 277 dòng) — đối chiếu toàn bộ với máy.
2. Xác nhận: **0 file `.cs` mới hơn `Road\Game.Server.dll`** → source đồng bộ bản chạy.
3. Xác nhận: 4 process sống, 8 port LISTEN, DLL khớp tài liệu từng byte.
4. Xác nhận: **user không có quyền Administrator**.
5. Khảo sát client (SWF, 5 config.xml, 15.157 file `.as` decompile, module shop).
6. Khảo sát DB (6 DB online, 5 DB rác, 15 bảng lớn nhất mỗi DB, 31 bảng backup).
7. Khảo sát webshop (3 lớp), shop in-game (13 bảng), pipeline XML (4 endpoint test live).
8. Đọc decompile `Tank.Request`: `ShopItemList.cs`, `ShopGoodsShowList.cs`, `searchgoodstemp.cs`, `shopcheapitemlist.cs`.
9. Đọc `PlayGame.aspx`, `Website\config.xml`, `Website\Web.config`, `Pages\Shop.cshtml`.

### 7.2 Tác động phụ (phải ghi nhớ)

> Khi test live 3 endpoint generate XML, **3 file đã bị ghi lại** lúc `2026-11-22 18:09:53–54`:
>
> | File | Trước | Sau |
> |---|---:|---:|
> | `Website\Request\ShopItemList.xml` | 72.500 B | 72.500 B |
> | `Website\Request\shopgoodsshowlist.xml` | 9.795 B | 9.795 B |
> | `Website\Request\searchgoodstemp.xml` | 569 B | 569 B |
>
> Kích thước **y nguyên** → nội dung không đổi, chỉ đổi mtime. Không ảnh hưởng client.
> Ngoài ra **không thay đổi gì**: không sửa source, không sửa DB, không restart service.

### 7.3 Chưa đụng tới

- 32 stored procedure thiếu, 123 `catch {}` rỗng, 6 điểm NRE, MD5 auth… (theo `BAO-CAO-AUDIT-SRC-20261122.md`, cần duyệt riêng).
- Không chạy `DBCC CHECKDB` (nặng trên DB 3,2 GB).

### 7.4 Bài học gặp phải khi viết script

Ba lỗi thật đã gặp và đã sửa — ghi lại để phiên sau không lặp:

| Lỗi | Triệu chứng | Cách sửa |
|---|---|---|
| `.ps1` có tiếng Việt nhưng lưu UTF-8 **không BOM** | PS 5.1 đọc theo ANSI → mojibake → `Unexpected token` | Viết **thuần ASCII**, hoặc lưu **UTF-8 có BOM** |
| Dùng `-Include *.cs` với `-LiteralPath` | Đếm ra **4.798** thay vì **4.684** (lẫn file sinh trong `obj\`) | Dùng `-Filter *.cs` rồi lọc `\obj\`, `\bin\`, `_build_*` |
| Dùng chung biến `$webCfgPath` cho cả `config.xml` và `Web.config` | `SEC-01/02/03` báo **OK** sai, dù Web.config rõ ràng có `debug="true"` | Tách biến: `$webCfgPath` (config.xml) và `$webConfigPath` (Web.config) |
| Sửa `.aspx` có **UTF-8 BOM** bằng tool ghi lại file | File **mất BOM** (`PlayGame.aspx` 10.294 → 10.291 B), tiếng Việt có nguy cơ mojibake | Sửa ở mức byte (`ReadAllText` + `Replace` + `UTF8Encoding($true)`), rồi `Compare-Object` byte để chắc chỉ đổi đúng ký tự cần đổi |

> Chính vì lỗi thứ 3 mà **lần chạy đầu** cho ra 3 OK / 11 ISSUE. Baseline đúng là **0 OK / 14 ISSUE**.

---

## 8. Lệnh kiểm tra nhanh

```powershell
# 1) Chạy lại toàn bộ tài liệu này
powershell -ExecutionPolicy Bypass -File "C:\Gunny\Gunny12.5\docs\refresh-trang-thai.ps1"

# 2) Service + port
Get-Process Road.Service,Center.Service,Fighting.Service,GunnyPanel -EA SilentlyContinue | Select ProcessName,Id,StartTime
foreach($p in 80,88,90,91,1593,4109,7539,8527,9601){ '{0}: {1}' -f $p, $(if(Get-NetTCPConnection -LocalPort $p -State Listen -EA SilentlyContinue){'LISTEN'}else{'CLOSED'}) }

# 3) Shop XML endpoints
'http://127.0.0.1:88/ShopItemList.ashx','http://127.0.0.1:88/ShopGoodsShowList.ashx','http://127.0.0.1:88/shopcheapitemlist.ashx' |
  ForEach-Object { $r=Invoke-WebRequest $_ -UseBasicParsing; '{0} -> {1} ({2} B)' -f $_, $r.StatusCode, $r.RawContentLength }

# 4) Webshop tables
sqlcmd -S DESKTOP-SMLR4VU -U sa -P 123456 -d Db_Tank -C -W -Q "SELECT (SELECT COUNT(*) FROM Site_Webshop_Item) AS SiteWebshopItem, (SELECT COUNT(*) FROM webshop_bag) AS webshop_bag"

# 5) DB rac
sqlcmd -S DESKTOP-SMLR4VU -U sa -P 123456 -C -W -Q "SELECT name, state_desc FROM sys.databases ORDER BY database_id"

# 6) SWF client
'C:\Gunny\Gunny12.5\Website\4.png','C:\Gunny\Gunny12.5\Website\FlashVNG\4.png' | ForEach-Object { '{0} {1}' -f (Get-FileHash $_ -Algorithm MD5).Hash.Substring(0,12), $_ }
```

---

## 9. Changelog tự động

<!-- AUTO:LOG:START -->
- 2026-11-22 21:17 | WS-01 | ISSUE -> OK | href=http://127.0.0.1:90/ port=90 LISTEN
<!-- AUTO:LOG:END -->

---

## 10. Liên kết

| File | Nội dung |
|---|---|
| `docs\NEW-SESSION-CONTEXT.md` | Bàn giao phiên — môi trường, deploy, việc còn tồn |
| `docs\FIX-FULL-TITLES-20261124.md` | Fix lỗi Full danh hiệu hết hạn sau 1 ngày (Validate=0) |
| `docs\BAO-CAO-AUDIT-SRC-20261122.md` | Audit source đầy đủ (32 SP thiếu, 123 catch rỗng…) |
| `docs\SUIT-BONUS-CAP-NHAN-VONG-TAY.md` | Bộ trang bị: cơ chế, deploy, rollback |
| `docs\CHAY-GAME-GUNNY12.5.md` | Cách chạy server |
| `Tools\GunnyPanel\docs\WEB-SHOP-EXPLORER.md` | Trang `/Shop` của panel (read-only) |
| `Tools\GunnyPanel\docs\ROAD-GM-API.md` | GM API port 9601 |
| `docs\refresh-trang-thai.ps1` | Script sinh các khối AUTO |

---

## 11. CL-03 — Sửa lỗi phụ kiện thú cưỡi che mất nhân vật ✅ ĐÃ SỬA

**Trạng thái: HOÀN TẤT — user xác nhận 2026-11-22 22:15.**
Mục này ghi lại **cách sửa đầy đủ** để tái sử dụng cho các mount lỗi tương tự sau này.

### 11.1 Triệu chứng

4 phụ kiện dạng "vật để ngồi" làm **nhân vật bị che mất** (đầu + thân chìm sau vật):

| Mount | TemplateID | MountType |
|---|---|---:|
| Xe Hoa Tiên | 12973 | 210 |
| Giỏ Trứng Thần Kỳ | 12985 | 214 |
| Sách Ma Thuật | 12993 | 231 |
| Ghế Nhạc Thiên Hoa | 14914 | 251 |

Đối chứng: các mount **con vật** (Rồng Vương 193, Hồ Ly 223) hiển thị đúng → lỗi chỉ xảy ra với art có bề mặt cao (sách/ghế/giỏ/xe).

### 11.2 Nguyên nhân — 2 lỗi độc lập

**(a) Thứ tự lớp:** `HallPlayerDraw.initialize()` thêm layer theo thứ tự head → bodyBack → mount → mountSaddle → body ⇒ lúc vẽ, **đầu nằm dưới lớp mount**. Mount con vật thấp nên không ai thấy; mount "vật để ngồi" cao nên che mất đầu.

**(b) Tư thế:** client chọn NGỒI hay CƯỠI qua `_139$25.MountSitList`. 4 type trên **không có trong danh sách** → dùng tư thế cưỡi (đứng/dang chân) trên vật để ngồi → thân chìm trong vật.

### 11.3 Kiến trúc client — 3 điều BẮT BUỘC biết trước khi patch

| # | Sự thật | Hệ quả |
|---|---|---|
| 1 | Hall được vẽ bởi **`starling.scene.hall.player.*`** (`starling\scene\hall\HallPlayerView.as:327` → `new HallPlayer(...)`). `hall\player\HallPlayerView.as` chỉ có `touchArea` → **`hall.player.*` là dead code** | Đừng patch `hall.player.HallPlayerBase` — vô nghĩa |
| 2 | Client nạp **cả `2.png` (3.675 script) và `4.png` (7.439 script)**, `2.png` tải trước; **code hall trong 2 file giống hệt nhau** (cùng md5 từng file .as) | Phải patch **và deploy cả 2 file**, ở cả `Website\` và `Website\Flash\` |
| 3 | Flash **chỉ nạp SWF một lần lúc khởi động** | Xoá cache mà không thoát hẳn client ⇒ thay đổi "không có tác dụng" |

### 11.4 Cách sửa — 7 bước (đã dùng thành công)

#### Bước 1 — Backup (làm trước mọi thứ)

```powershell
$w='C:\Gunny\Gunny12.5\Website'
$stamp=Get-Date -Format 'yyyyMMdd-HHmm'
foreach($f in "$w\2.png","$w\Flash\2.png","$w\4.png","$w\Flash\4.png"){
  Copy-Item $f "$f.bak-$stamp" -Force
}
```

#### Bước 2 — Export AS3 từ SWF **đang chạy**

```powershell
$ffdec='C:\Gunny\ToolGunny\_analysis\ffdec\ffdec.jar'
$java ='C:\Program Files\Common Files\Oracle\Java\javapath\java.exe'
$w='C:\Gunny\Gunny12.5\Website'

& $java -jar $ffdec -export script 'C:\Temp\p\4'       "$w\4.png"
& $java -jar $ffdec -export script 'C:\Temp\p\2'       "$w\2.png"
```

Mỗi lần ~1–2 phút. **Luôn export từ file đang chạy**, không dùng thư mục decompile cũ (để chắc chắn đúng phiên bản).

#### Bước 3 — Sửa đúng 2 file

**(3a) `starling\scene\hall\player\HallPlayerDraw.as`** — đưa đầu lên trên mount:

```diff
       override protected function initialize() : void
       {
          if(!_headImage) { _headImage = StarlingMain.instance.createImage(); }
-         addChild(_headImage);
          if(!_bodyBackImage) { ... }  addChild(_bodyBackImage);
          if(!_mountImage)    { ... }  addChild(_mountImage);
          if(!_mountSImage)   { ... }  addChild(_mountSImage);
+         addChild(_headImage);
          if(!_bodyImage)     { ... }  addChild(_bodyImage);
```

Kết quả: `bodyBack → mount → mountSaddle → head → body`.

**(3b) `horse\_139$25.as`** — thêm type vào danh sách NGỒI:

```diff
- public static const MountSitList:Array = [6,7,8,9,118,119,130,137,140,151,152,163];
+ public static const MountSitList:Array = [6,7,8,9,118,119,130,137,140,151,152,163,210,214,231,251];
```

#### Bước 4 — Import **chỉ file đã sửa**

> ⚠️ **Không import cả thư mục export** — FFDec báo lỗi
> `SEVERE: Property fromPool has package internal access in starling.events.Event`
> và **không tạo được file**. Chỉ đưa các file cần sửa vào thư mục riêng, giữ đúng cấu trúc package.

```powershell
$pat='C:\Temp\p\patch\scripts'
New-Item -ItemType Directory -Path "$pat\starling\scene\hall\player","$pat\horse" -Force | Out-Null
Copy-Item 'C:\Temp\p\4\scripts\starling\scene\hall\player\HallPlayerDraw.as' "$pat\starling\scene\hall\player\" -Force
Copy-Item 'C:\Temp\p\4\scripts\horse\_139$25.as'                              "$pat\horse\" -Force

# build cho cả 2 SWF, TỪ FILE GỐC (backup), không chồng lên bản đã patch
& $java -jar $ffdec -importScript "$w\4.png.bak-<stamp>" 'C:\Temp\p\4_new.swf' $pat
& $java -jar $ffdec -importScript "$w\2.png.bak-<stamp>" 'C:\Temp\p\2_new.swf' $pat
```

#### Bước 5 — Verify bằng export **ngược** (bắt buộc)

```powershell
& $java -jar $ffdec -export script 'C:\Temp\p\verify' 'C:\Temp\p\4_new.swf'
# kiểm tra 3 thứ:
#  - số file .as = 7.439 (không mất script nào)
#  - thứ tự addChild: bodyBack > mount > mountS > head > body
#  - MountSitList có đuôi "...,163,210,214,231,251];"
```

Kiểm tra thêm tính hợp lệ SWF (header phải khớp độ dài thực sau khi giải nén):

```python
import zlib, struct
d = open(r"C:\Temp\p\4_new.swf","rb").read()
print(struct.unpack("<I", d[4:8])[0] == len(zlib.decompress(d[8:])) + 8)   # True
```

#### Bước 6 — Deploy cả 2 file, 2 nơi

```powershell
Copy-Item 'C:\Temp\p\4_new.swf' "$w\4.png"       -Force
Copy-Item 'C:\Temp\p\4_new.swf' "$w\Flash\4.png" -Force
Copy-Item 'C:\Temp\p\2_new.swf' "$w\2.png"       -Force
Copy-Item 'C:\Temp\p\2_new.swf' "$w\Flash\2.png" -Force
```

Không cần restart IIS, không cần build DLL, không đụng DB.

#### Bước 7 — Test

1. **Thoát hẳn client** (và launcher) — bắt buộc
2. Xoá cache: `%LOCALAPPDATA%\37abc\User Data\Default\Cache\*`, `%APPDATA%\Macromedia\Flash Player\#SharedObjects\*`
3. Mở lại, kiểm 4 món + hồi quy Rồng Vương (193) / Hồ Ly (223)

**Cách biết client đã nạp bản mới:** xem file cache — phải có file đúng bằng dung lượng SWF mới.

```powershell
Get-ChildItem "$env:LOCALAPPDATA\37abc\User Data\Default\Cache" -File |
  Where-Object { $_.Length -gt 3000000 } |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 5 Length, LastWriteTime, Name
```

### 11.5 Kết quả (bản đang chạy)

| File | Kích thước | md5 |
|---|---|---|
| `Website\2.png` + `Flash\2.png` | 3.335.984 B | `0626ACE0D004` |
| `Website\4.png` + `Flash\4.png` | 7.214.962 B | `5C8B518F9D60` |

Backup bản gốc: `*.bak-20261122-2148` (4.png), `*.bak-20261122-2210` (2.png).
Artifact: `docs\client-patches\` (SWF + `.as` gốc/đã sửa).

#### Rollback

```powershell
$w='C:\Gunny\Gunny12.5\Website'
Copy-Item "$w\2.png.bak-20261122-2210" "$w\2.png" -Force
Copy-Item "$w\Flash\2.png.bak-20261122-2210" "$w\Flash\2.png" -Force
Copy-Item "$w\4.png.bak-20261122-2148" "$w\4.png" -Force
Copy-Item "$w\Flash\4.png.bak-20261122-2148" "$w\Flash\4.png" -Force
# rồi xoá cache + khởi động lại client
```

### 11.6 Bài học (đã trả giá 4 vòng)

| # | Bài học |
|---|---|
| 1 | **Xác định đúng lớp trước khi sửa.** Patch 2 vòng vào `hall.player.*` — hoá ra là dead code. Cách kiểm nhanh: grep xem lớp đó có `new Xxx(` ở đâu không, hay chỉ có constructor rỗng |
| 2 | **Client nạp nhiều SWF.** Phải xem cache để biết nạp file nào; hall nằm ở cả `2.png` và `4.png` |
| 3 | **Phải thoát hẳn client.** 2 lần test đầu "không đổi gì" rất có thể vì SWF cũ vẫn trong RAM |
| 4 | **FFDec không import được toàn bộ script** (lỗi `fromPool`) → chỉ import file thay đổi, luôn build từ **file gốc** |
| 5 | **`sortOrder` tăng dần = lớp dưới** (`SceneCharacterSet.dataSet` sort ascending): head 1 < body 3/4 < mount 5 < saddle 7/8 < body-ngồi 9/10 |
| 6 | **Luôn verify bằng export ngược** SWF mới — đừng tin "import không báo lỗi" |

---
*File này do agent tạo từ khảo sát thực tế ngày 2026-11-22. Sửa tay phần văn xuôi thì được, nhưng các khối AUTO sẽ bị ghi đè ở lần chạy script tiếp theo.*
