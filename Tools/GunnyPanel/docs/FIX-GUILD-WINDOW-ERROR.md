# Fix lỗi mở cửa sổ Guild sau khi tạo Guild (2026-11-08)

> **Triệu chứng (user báo 10:45):** vừa tạo Guild xong, mở cửa sổ Guild thì hiện hộp
> **"tips"** với nội dung `http://127.0.0.1:88/ConsortiaRichRankList.ashx` và nút **ok**.
> Danh sách Guild / thông tin Guild không hiển thị đúng.

Ca này gồm **2 lỗi độc lập** xảy ra cùng lúc:

| # | Lỗi | Hậu quả |
|---|---|---|
| A | DLL web bị **lệch phiên bản** sau bước deploy 10:1x sáng 08/11 | `ConsortiaList.ashx` ném `MissingMethodException` → trả `value="false"` → cửa sổ Guild không có dữ liệu |
| B | **Thiếu handler** `ConsortiaRichRankList.ashx` (file 52 byte rác, không có `<%@ WebHandler %>`) | HTTP 500 → client coi là lỗi tải → hiện `tips` = URL request |

---

## A. Lỗi DLL web lệch phiên bản (MissingMethodException)

### Bằng chứng
`C:\Gunny\Gunny12.6\WebSite\Request\logs\Error.log` (10:44:46 và 10:46:06):

```
System.MissingMethodException: Method not found: 'Void SqlDataProvider.Data.ConsortiaInfo.set_FightPower(Int64)'.
   at Bussiness.ConsortiaBussiness.InitConsortiaInfo(DataRow dr)
   at Bussiness.ConsortiaBussiness.GetConsortiaPage(Int32 page, Int32 size, Int32& total, Int32 order, ...)
```

`ConsortiaList.ashx` bắt exception → trả zlib `<Result total="0" value="false" message="Fail!" />`.

### Nguyên nhân
- Sáng 08/11, khi deploy bản sửa `SqlDataProvider` (`isWeddingRing`, v0.31.1/`docs/NEWBIE-GIFT.md` §16–17),
  DLL build từ **Full Source** (`Full Source\SqlDataProvider\bin\Debug\net472\SqlDataProvider.dll`,
  `ConsortiaInfo.FightPower = int`, SHA `2233DF...`) bị copy **nhầm vào 2 thư mục bin của web**:
  - `C:\Gunny\Gunny12.6\WebSite\Request\Bin\SqlDataProvider.dll`
  - `C:\Gunny\Gunny12.6\WebSite\bin\SqlDataProvider.dll`
- Hai web app dùng `Bussiness.dll` **bản release** biên dịch với `FightPower = long` (Int64).
  Thay provider bằng bản `int` ⇒ `InitConsortiaInfo` không tìm thấy `set_FightPower(Int64)` ⇒ chết toàn bộ
  các handler consortia (Guild list, guild info, member list...).
- Trước khi sửa: `Bussiness.dll` (release) gọi **49 member** không tồn tại trong provider mới
  (đo bằng `probe2.cs` — so khớp toàn bộ 4357 MemberRef).

### Cách sửa
Khôi phục đúng cặp DLL release cho từng web app (đã backup bản bị thay):

| File | Nguồn khôi phục | Kích thước | SHA256 (16 ký tự) |
|---|---|---|---|
| `WebSite\Request\Bin\SqlDataProvider.dll` | giải nén từ `WebSite\Request\Bin\Bin.rar` (file gốc 2022-03-06) | 542 720 | `CBB7E584D01EC970` |
| `WebSite\bin\SqlDataProvider.dll` | `WebSite\Request\obj\Release\Package\PackageTmp\bin\SqlDataProvider.dll` (bản publish release) | 314 368 | `5721FEEF73638C7A` |
| `WebSite\Request\Bin\Bussiness.dll` | (giữ nguyên, không bị thay) | 685 056 | `CF43014FFAB675CE` |
| `WebSite\bin\Bussiness.dll` | (giữ nguyên, không bị thay) | 303 104 | `E5DBF350E79C249E` |

Sau khi thay: sửa `LastWriteTime` của `WebSite\Request\Web.config` / `WebSite\Web.config`
để IIS recycle app pool, rồi GET lại handler.

**Đã verify bằng metadata (không chỉ test tay):** so khớp *toàn bộ* MemberRef
`Bussiness.dll → SqlDataProvider` với định nghĩa trong provider:
- `Request\Bin\Bussiness.dll` ↔ `Request\Bin\SqlDataProvider.dll`: **4357 refs, missing = 0**
- `WebSite\bin\Bussiness.dll` ↔ `WebSite\bin\SqlDataProvider.dll`: **1986 refs, missing = 0**
- (bản lỗi: `Request\Bin\Bussiness.dll` ↔ provider `2233DF`: missing = 49)

> ⚠️ **Quy tắc deploy từ nay:** `SqlDataProvider.dll` build từ `Full Source` **chỉ** copy vào
> `Service\Service\Road` (game server) và các service cần nó. **Tuyệt đối không** copy vào
> `WebSite\bin` / `WebSite\Request\Bin` — web dùng bộ DLL release riêng.
> Kiểm tra sau mỗi lần deploy: `powershell -ExecutionPolicy Bypass -File _tools\verify-web-dlls.ps1`.

---

## B. Handler `ConsortiaRichRankList.ashx` (client hiện tips URL)

### Client hoạt động thế nào
Decompile `WebSite\Flash\4.swf` (JPEXS FFDec 26.4.0, Java 20 — báo cáo đầy đủ ở
`data\tmp\swf\swf-analysis.md`):

- `ddt.loader.LoaderCreate.creatConsortiaRichRankLoader()`:
  ```as3
  var vars:URLVariables = RequestVairableCreater.creatWidthKey(true); // selfid, key=MD5(GlobalData.key), rnd
  vars["consortiaID"] = PlayerManager.Instance.Self.ConsortiaID;
  var l:BaseLoader = LoadResourceManager.Instance.createLoader(
      PathManager.solveRequestPath("ConsortiaRichRankList.ashx"), 6, vars); // 6 = RequestLoader (GET, text thường)
  l.analyzer = new ConsortiaRichRankAnalyze(...analyzeRichRank);
  ```
- Được gọi trong `consortion.view.selfConsortia.PlacardAndEvent.initView()` (constructor của panel
  Guild) → **mỗi lần mở cửa sổ Guild**, và được load lại khi `consortionStateChange`
  (tạo Guild / vào Guild thành công) ⇒ giải thích đúng triệu chứng "vừa tạo guild là lỗi".
- Analyzer `consortion.analyze.ConsortiaRichRankAnalyze`:
  ```as3
  var xml:XML = new XML(data);
  if (xml.@value == "true") {
     var list:XMLList = xml.Item;                    // phần tử con tên Item
     // ObjectUtils.copyPorpertiesByXML: copy ATTRIBUTE trùng tên property
     // VO consortion.data.ConsortiaWeekRewardPlayerVo { Rank:int, UserID:int, ConsortiaID:int, Rich:int, Name:String }
     dataList.sortOn("Rank", 16);                    // 16 = Array.NUMERIC tăng dần
  } else { message = xml.@message; onAnalyzeError(); }
  ```
- Response **phải là XML text thường (không zlib)** vì loader type 6 = `RequestLoader extends TextLoader`.
- Nếu lỗi tải: `BaseLoader.loadErrorMessage` trả `_loadErrorMessage || _url` ⇒ hộp **tips hiện đúng URL**
  (đây chính là ảnh user gửi). Handler cũ chỉ là file 52 byte:
  `<Result total="0" value="true" message="Success!" />` **không có dòng `<%@ WebHandler %>`**
  ⇒ ASP.NET trả **HTTP 500** ⇒ client hiện tips.

### Cách sửa
Thay `C:\Gunny\Gunny12.6\WebSite\Request\ConsortiaRichRankList.ashx` bằng handler thật
(single-file handler, tự compile trong ASP.NET, không cần build DLL):

- Nhận `consortiaID` (GET), lấy thành viên Guild qua
  `Bussiness.ConsortiaBussiness.GetConsortiaUsersPage(1, 1000, ref total, 0, consortiaID, -1, -1)`
  (view `V_Consortia_Users`, gồm cả online + offline).
- `Rich = RichesOffer + RichesRob` (đúng bằng `SqlDataProvider.Data.PlayerInfo.Riches` mà game dùng),
  sort giảm dần, `Rank` từ 1.
- Trả plain text:
  ```xml
  <Result value="true" message="Success!">
    <Item Rank="1" UserID="5" ConsortiaID="1" Rich="0" Name="hahaa" />
  </Result>
  ```
- **Chịu lỗi:** thiếu/không hợp lệ `consortiaID` → trả list rỗng `value="true"` (không bao giờ 500/Fail
  ⇒ không còn tips). Mọi request được ghi 1 dòng vào `WebSite\Request\logs\richrank-requests.log`
  (thời gian, method, URL, User-Agent) để đối chiếu với client thật.
- Client `ConsortionWeekRewardView` (tab **"Thưởng tuần"**) hiển thị top 10; dòng nào `Rich < 1000`
  hiện `-` (placeholder) — với Guild mới tạo 1 thành viên sẽ thấy toàn `-`, **không phải lỗi**.

---

## Kết quả kiểm tra (sau khi sửa)

| Kiểm tra | Kết quả |
|---|---|
| `GET :88/ConsortiaList.ashx?...` | `200` + `<Result total="1" value="true" message="Success!">` + Guild `family` (ID 1, chủ `hahaa`, FightPower 370671) |
| `GET :88/ConsortiaRichRankList.ashx?...&consortiaID=1` | `200`, `<Result value="true"><Item Rank="1" UserID="5" ConsortiaID="1" Rich="0" Name="hahaa" /></Result>` |
| `GET :88/ConsortiaRichRankList.ashx` (không tham số) | `200`, list rỗng `value="true"` (trước đây: 500) |
| Đường dẫn chữ thường `consortiarichranklist.ashx` (client lowercases URL) | `200` OK |
| `Error.log` sau các request trên | không có `MissingMethodException` mới |
| `_tools\verify-web-dlls.ps1` | **PASS 9/9** |
| Script kiểm tra DLL: `Request\Bin` 4357 refs / `WebSite\bin` 1986 refs | **missing = 0** |

Ghi chú: 3 dòng lỗi còn trong `Error.log` (11:02, 11:10, 11:14) là do **lúc test gọi handler thiếu
tham số** (`ConsortiaList`, `ConsortiaRichRankList`, `ConsortiaNameCheck`) — không phải lỗi của game,
và handler RichRank mới đã chịu được trường hợp này.

## Xác nhận từ client thật (user test 11:26 + 11:58, báo OK 12:00)

`WebSite\Request\logs\richrank-requests.log` đã ghi được request thật của client Flash
(UA `37abc/2.0.6.16 Chrome/60...`) với đúng các tham số dự đoán từ decompile:

```
2026-11-08 11:26:38  GET /consortiarichranklist.ashx?rnd=0%2E53...&selfid=5&key=1c1b...&consortiaID=1  -> 200 OK
2026-11-08 11:58:15  GET /consortiarichranklist.ashx?consortiaID=2&selfid=1&key=c410...&rnd=0%2E38...  -> 200 OK
```

- `Error.log` bị app-pool recycle lúc 11:57:09 và **hiện có 0 byte** (không còn exception nào).
- Client gọi đúng `consortiaID` (Guild 1 lúc 11:26, Guild 2 `nhà khách` của `test1` ngay khi tạo lúc 11:58)
  và cả 2 request đều được handler trả `value="true"` — user xác nhận hộp tips đã hết.


## Cách test lại (game)

1. Trong game: đóng/mở lại **cửa sổ Guild** (hoặc đăng nhập lại nếu cần).
2. Không còn hộp "tips" URL ⇒ OK. Mở tab **"Thưởng tuần"** (nếu có) sẽ thấy bảng (Guild mới → toàn `-`).
3. Nếu còn lỗi: gửi lại ảnh; kiểm tra:
   - `C:\Gunny\Gunny12.6\WebSite\Request\logs\richrank-requests.log` — URL + User-Agent thật của client.
   - `C:\Gunny\Gunny12.6\WebSite\Request\logs\Error.log` — nếu có exception mới.

## File thay đổi / artifact

- `C:\Gunny\Gunny12.6\WebSite\Request\ConsortiaRichRankList.ashx` — handler mới (backup bản rác:
  `data\tmp\dllbackup-20261108-111014\ConsortiaRichRankList.ashx.orig-stub`).
- `C:\Gunny\Gunny12.6\WebSite\Request\Bin\SqlDataProvider.dll` — khôi phục bản gốc (backup bản lỗi:
  `data\tmp\dllbackup-20261108-110155\RequestBin-SqlDataProvider.dll-2233DF`).
- `C:\Gunny\Gunny12.6\WebSite\bin\SqlDataProvider.dll` — khôi phục bản release tương thích
  (backup: `data\tmp\dllbackup-20261108-111610\WebSiteBin-SqlDataProvider.dll.pre-restore`).
- `_tools\verify-web-dlls.ps1` — script kiểm tra định kỳ (9 checks, read-only).
- Phân tích client: `data\tmp\swf\swf-analysis.md` (+ AS3 export `data\tmp\swf\decompiled_4\`,
  `data\tmp\swf\flash4_scripts\`), công cụ probe DLL: `data\tmp\probe\probe2.cs`.

## Việc chưa xử lý / quan sát thêm

- **Dữ liệu "Thưởng tuần" thật:** server repack hiện **không** lưu riches theo tuần cho từng member;
  handler dùng Riches tích luỹ (`RichesOffer + RichesRob`). Nếu muốn đúng nghĩa "tuần", cần thêm cột/bảng
  reset tuần ở game server — nằm ngoài phạm vi fix lỗi này.
- **Header "Cấp Guild 10":** ảnh chụp hiện số 10 nhưng DB `Consortia.Level = 1`; client 12.6 vẽ level bằng
  ảnh `asset.consortion.level` (`ScaleFrameImage.setFrame(Level)`). Có thể là quy ước hiển thị của client
  (level ×10) — **không liên quan lỗi tips**; nếu user thấy sai thì kiểm tra riêng phần UI guild level.
- Các trang login legacy của site `:80` (`LoginGame.aspx`, `createLogin.ashx`, `checkuser.ashx`) trả 500
  **cả trước và sau khi sửa DLL** (đã A/B test với cả 2 bản provider) — lỗi có sẵn của repack, không liên
  quan; login thực tế của client đi qua `Tank.Request.Login` ở `:88`.
