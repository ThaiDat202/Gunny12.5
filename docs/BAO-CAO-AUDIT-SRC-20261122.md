# BÁO CÁO AUDIT SOURCE CODE — BK Gunny 12.5

> **Ngày:** 2026-11-22 · **Người thực hiện:** AI audit (tự động + review thủ công có mục tiêu)
> **Trạng thái:** ⚠️ **CHỈ BÁO CÁO — CHƯA SỬA BẤT KỲ LỖI NÀO TRONG TÀI LIỆU NÀY.**
> **Phạm vi:** toàn bộ source server (`C:\Gunny\Gunny12.5\Full Source`) + client AS3 (decompile).

---

## 1. Tổng quan quy mô code

| Thành phần | File | Dòng code | Ghi chú |
|---|---:|---:|---|
| `game.server` | 3.366 | 356.908 | Road.Service (game logic chính) |
| `Game.Logic` | 594 | 56.359 | Battle logic (dùng bởi Fight) |
| `Bussiness` | 143 | 42.722 | Truy cập DB / managers |
| `SqlDataProvider` | 374 | 27.431 | Model + data provider |
| `Road.Service` | 32 | 13.426 | Host + GM API |
| `center.server` | 25 | 6.792 | Center (login/zone) |
| `fighting.server` | 23 | 4.301 | Fight server |
| `game.base` | 43 | 4.147 | Packet/network/config |
| `EntityDatabase` | 66 | 2.062 | EF models |
| `Fighting.Service`, `Center.Service`, `ddtankhelp`, `filedialogsthreadappar` | 18 | 1.208 | Host/build |
| **TỔNG SERVER** | **4.684** | **515.356** | 13 project |
| **CLIENT AS3 (4.png)** | **7.439** | **1.168.458** | `_analysis\4png_scripts\scripts` |
| CLIENT AS3 (bản 2.png) | 7.347 | 1.161.949 | `_analysis\client4_scripts\scripts` |
| Preloader | 371 | 53.151 | `preloader1_scripts` |

> Hai bản decompile client gần như trùng nhau — khi sửa client phải xác định đúng bản SWF đang chạy (`2.png` hay `4.png`).

---

## 2. Phương pháp & giới hạn

**Đã làm:**
1. Quét tĩnh toàn bộ 4.684 file C# theo ~30 mẫu lỗi/anti-pattern (empty catch, NRE, SQL concat, MD5, hardcode credential, dead code…).
2. Build lại toàn bộ 2 solution (`Road.Service.sln`, `Fighting.Service.sln`) với `-t:Rebuild` để thu **622 warning** của compiler.
3. Đối chiếu **569 stored procedure** được code tham chiếu với thực tế trong **11 database**.
4. Kiểm tra toàn vẹn dữ liệu DB (item, túi đồ, gemstone, account…).
5. Quét 7.439 file AS3 client (empty catch, listener leak, hardcode host, setInterval…).
6. Đọc thủ công các module rủi ro cao: GM API, CenterService (login), Marshal/serialization, config bảo mật.
7. Đọc log runtime 3 service để đối chiếu lỗi thực tế.

**Giới hạn (quan trọng):**
- KHÔNG thể review từng dòng 1,68 triệu dòng code trong một phiên → báo cáo dựa trên **quét tĩnh + review mẫu**, các mục ghi "cần xác minh" là chưa đủ bằng chứng kết luận.
- Website (`Tank.Request.dll`, `Tank.Flash.dll`) **không có source C#** trong checkout → không audit được lỗi tầng web (SQL injection, auth…) ở mức source; chỉ audit phần config.
- Lỗi đã sửa trong các phiên trước (FightPower long, Suit bonus, Chiến Hồn, Bộ Sưu Tập, Quà Tân Thủ, RingStation packet, crit reduce) **không** nằm trong báo cáo này.
- Mọi kết luận mang tính "phát hiện tĩnh"; lỗi runtime thật cần test trong game.

---

## 3. Tổng hợp mức độ

| Mức | Số nhóm lỗi | Ví dụ tiêu biểu |
|---|---|---|
| 🔴 **Cao** | 4 | 32 stored procedure thiếu; 123 empty catch nuốt lỗi; 6 điểm NRE chắc chắn; MD5 + so sánh plaintext ở login |
| 🟠 **Trung bình** | 7 | SQL nối chuỗi; dead code (CS0162/1717/1522); field không bao giờ gán; `NotImplementedException`; debug=true/customErrors Off; Center 4109 mở all-interface; DB account NULL fields |
| 🟡 **Thấp / Info** | 6 | binary formatter dead code; `GC.Collect`; `Thread.Abort`; hardcode sa password; client listener imbalance; host legacy trong client |

---

## 4. 🔴 LỖI MỨC CAO (server)

### 4.1 — 32 stored procedure được code gọi nhưng KHÔNG tồn tại trong bất kỳ DB nào

Code tham chiếu **569 SP**; đối chiếu 11 database (`Db_Tank`, `Db_Tank_All`, `Db_Membership`, `Db_Count`, `Db_GM`, `Db_Logs`, …) → **32 SP thiếu hoàn toàn**.

Hệ quả: khi method tương ứng được gọi, `Sql_DbObject.GetReader` ném exception; do đa số method bọc `try/catch` rỗng → **tính năng im lặng không hoạt động**, không có log.

| STT | Stored procedure thiếu | File tham chiếu |
|---:|---|---|
| 1 | `SP_AASInfo_All` | `Bussiness\Bussiness\ProduceBussiness.cs` |
| 2 | `SP_Active_Award_Add` | `Bussiness\Bussiness\PlayerBussiness.cs` |
| 3 | `SP_Active_Number_Add` | `Bussiness\Bussiness\ActiveBussiness.cs` |
| 4 | `SP_Charge_Order` | `Bussiness\Bussiness\OrderBussiness.cs` |
| 5 | `SP_Charge_Order_Single` | `Bussiness\Bussiness\OrderBussiness.cs` |
| 6 | `SP_Cookie_Info_Insert` | `Bussiness\Bussiness\CookieInfoBussiness.cs` |
| 7 | `SP_Cookie_Info_QueryByUser` | `Bussiness\Bussiness\CookieInfoBussiness.cs` |
| 8 | `SP_DailyRecordInfo_Delete` | `Bussiness\Bussiness\PlayerBussiness.cs` |
| 9 | `SP_DailyRecordInfo_Single` | `Bussiness\Bussiness\PlayerBussiness.cs` |
| 10 | `SP_Get_AreaBigBugle_Record` | `Bussiness\Bussiness\ProduceBussiness.cs` |
| 11 | `SP_Get_DailyLeagueAwardItems` | `Bussiness\Bussiness\ProduceBussiness.cs` |
| 12 | `SP_Get_DailyLeagueAwardList` | `Bussiness\Bussiness\ProduceBussiness.cs` |
| 13 | `SP_Get_UserPet_By_IsExit` | `Bussiness\Bussiness\PlayerBussiness.cs` |
| 14 | `SP_GetSingleDiceData` | `Bussiness\Bussiness\PlayerBussiness.cs` |
| 15 | `SP_GetSingleFields` | `Bussiness\Bussiness\PlayerBussiness.cs` |
| 16 | `SP_GetSingleUserGemStones` | `Bussiness\Bussiness\PlayerBussiness.cs` |
| 17 | `SP_GetSingleUserLabyrinth` | `Bussiness\Bussiness\PlayerBussiness.cs` |
| 18 | `SP_GetSingleUserRankInfo` | `Bussiness\Bussiness\PlayerBussiness.cs` |
| 19 | `SP_Prop_All` | `Bussiness\Bussiness\ProduceBussiness.cs` |
| 20 | `SP_Server_Edition` | `Bussiness\Bussiness\ServiceBussiness.cs` |
| 21 | `SP_Server_Record` | `Bussiness\Bussiness\ServiceBussiness.cs` |
| 22 | `SP_Service_ListByIP` | `Bussiness\Bussiness\ServiceBussiness.cs` |
| 23 | `SP_Service_UpdateRSA` | `Bussiness\Bussiness\ServiceBussiness.cs` |
| 24 | `SP_Team_Add` | `Bussiness\Bussiness\TeamBussiness.cs` |
| 25 | `SP_TeamUsers_Add` | `Bussiness\Bussiness\TeamBussiness.cs` |
| 26 | `SP_Test1` | `Bussiness\Bussiness\PlayerBussiness.cs` |
| 27 | `SP_User_Bank` | `Bussiness\Bussiness\PlayerBussiness.cs` |
| 28 | `SP_User_Info_Insert` | `Bussiness\Bussiness\UserInfoBussiness.cs` |
| 29 | `SP_User_Info_QueryByUid` | `Bussiness\Bussiness\UserInfoBussiness.cs` |
| 30 | `SP_User_Mark` | `Bussiness\Bussiness\PlayerBussiness.cs` |
| 31 | `SP_User_Update_History` | `Bussiness\Bussiness\ProduceBussiness.cs` |
| 32 | `SP_Users_UpdatePassword` | `Bussiness\Bussiness\PlayerBussiness.cs` |

**Nhóm tính năng bị ảnh hưởng (dự kiến):** đổi mật khẩu, bank, mark (dấu ấn), team, đơn nạp (`Charge_Order`), cookie tracking, daily record, rank info, gemstone load, dice, labyrinth, pet load, server registry (service list/edition/RSA), active award/number.

> Đây là dạng lỗi nguy hiểm nhất vì **không crash** — chỉ lặng lẽ trả dữ liệu rỗng.

### 4.2 — 123 khối `catch { }` rỗng (nuốt toàn bộ exception)

- **123 khối** empty catch trong **59 file** (kiểu `catch { }`).
- Thêm **1.204 chỗ** `catch (Exception …)` (296 file) — phần lớn có log, nhưng nhiều chỗ chỉ `return null/false`.

Top file có empty catch:

| Số | File |
|---:|---|
| 13 | `center.server\Center.Server\CenterService.cs` |
| 11 | `Road.Service\Game.Service.GMService\GmApiActions.cs` |
| 5 | `game.server\Game.Server.Packets.Client\GemstoneHandler.cs` |
| 5 | `Road.Service\Game.Service.GMService\GmApiServer.cs` |
| 4 | `game.server\Game.Server.GameObjects\GamePlayer.cs` |
| 4 | `game.server\Game.Server.GameObjects\JewelCultureMgr.cs` |
| 4 | `Bussiness\Bussiness.Managers\GoldEquipMgr.cs` |
| 4 | `Bussiness\Bussiness\PlayerBussiness.cs` |

Rủi ro: lỗi DB, lỗi dữ liệu, lỗi packet đều bị bỏ qua → khó truy vết, tính năng sai âm thầm.

### 4.3 — 6 điểm NullReferenceException gần như chắc chắn

`.FirstOrDefault(...)` rồi truy cập trực tiếp **cùng dòng** (không null-check):

| File:Line | Code |
|---|---|
| `game.server\Game.Server.DevilTurn\DevilTurnMgr.cs:52` | `...FirstOrDefault().Need` |
| `game.server\Game.Server.DevilTurn.Handle\BeadConversion.cs:38` | `...FirstOrDefault().Count++` |
| `game.server\Game.Server.Team\TeamMgr.cs:160` | `...FirstOrDefault().SegmentID` |
| `game.server\Game.Server.Team\TeamMgr.cs:379` | `...FirstOrDefault().` (so sánh) |
| `game.server\Game.Server.Team\TeamMgr.cs:458` | `serverData.BattleTeamSeasonList.FirstOrDefault().RankGift.Split(',')` |
| `game.server\Game.Server.GameObjects\GamePlayer.cs:5288` | gọi `FirstOrDefault()` 2 lần (kiểm tra null nhưng không lưu biến — race/không nhất quán) |

Ngoài ra toàn source có **216** chỗ `.FirstOrDefault(` — phần lớn đã null-check, nhưng 6 chỗ trên là rủi ro trực tiếp khi dữ liệu thiếu (bảng rỗng/mùa giải chưa có).

### 4.4 — Xác thực bằng MD5 + so sánh mật khẩu dạng plaintext

1. **MD5 dùng cho chữ ký xác thực** (`25` chỗ):
   - `center.server\Center.Server\CenterService.cs:110,128,141,154` — so sánh `md5(id + login_key)` để cấp quyền reload/shutdown/login device.
   - `Bussiness\Bussiness.Interface\BaseInterface.cs:227,267,309` — chữ ký API.
   - So sánh chuỗi thường (`!=`) → không constant-time (timing attack ở mức lý thuyết), và MD5 đã lỗi thời.
2. **So sánh mật khẩu plaintext**: `CenterService.cs:383`
   ```csharp
   if (player.Name == name && player.Password == password)
   ```
   → Nếu luồng này được dùng thật, mật khẩu lưu/so sánh dạng thô.
   *Hiện `Sys_Users_Detail.Password` rỗng với **8/8** account* → có thể login đi qua hệ thống khác (Membership/passport). **Cần xác minh luồng đăng nhập thực tế trước khi kết luận.**

---

## 5. 🟠 LỖI MỨC TRUNG BÌNH

### 5.1 — SQL nối chuỗi (nguy cơ injection / lỗi cú pháp)

**22 chỗ** nối chuỗi SQL:
- `Bussiness\Bussiness\ManorBussiness.cs` — **18 chỗ**
- `Bussiness\Bussiness\PlayerBussiness.cs` — 3
- `Bussiness\Bussiness\MagicStonePlanBussiness.cs` — 1

Cần rà từng chỗ xem có dùng tham số hoá (`SqlParameter`) hay ghép trực tiếp giá trị người chơi.

### 5.2 — Dead code / logic chết (compiler cảnh báo)

| Mã | Số | Ý nghĩa | Ví dụ |
|---|---:|---|---|
| CS0162 | 18 | Unreachable code | `RateMgr.cs:97`, `ActivityPackageHandler.cs:161,167,170`, `ItemRefineryHandler.cs:106`, `MarkHandler.cs:137`, `ConverScore.cs:99`, `BuyTransnationalGoodsHandler.cs:111`, `DragonBoatExchange.cs:121` |
| CS1717 | 10 | Gán biến cho chính nó (nghiệp vụ sai) | `PVPGame.cs:635`, `Blind_Seal.cs:31`, `EatPet.cs:33` |
| CS1522 | 2 | `switch` rỗng | `SendAwardAction.cs:50` |
| CS0649 | 12 | Field không bao giờ gán | `AbstractGame.m_isSingleBatle`, `AwardMgr._searchGoodsTemp`, `AwardMgr.m_lock`, `PlayerInfo._newDay` |
| CS0108 | 4 | Ẩn thành viên lớp cha | `BattleSkillBussiness.log` |

`AwardMgr.m_lock` không bao giờ gán = **`lock` sẽ luôn ném NullReference/Debate** nếu được dùng (cần kiểm tra).

### 5.3 — `NotImplementedException` (228 chỗ)

- **212** nằm trong `game.server\Game.Server.Packets.Server\ConsolePacketLib.cs` (stub cho console commands).
- Còn lại: `GamePlayer.cs` (3), `SwornHandler.cs` (3), `RobotGamePlayer.cs` (2), `AbstractPacketLib.cs` (2), `SearchGoodsHandler.cs`, `PVEGame.cs`, `VipStoreMgr.cs`.
→ Tính năng tương ứng (console, kết nghĩa, robot, VIP store…) **chưa hoàn thiện**.

### 5.4 — `Website\Web.config`: debug + lộ stack trace

```xml
<compilation debug="true" targetFramework="4.0">   <!-- L33 -->
<customErrors mode="Off" defaultRedirect="Error.htm"> <!-- L41 -->
```
- `debug="true"` → giảm hiệu năng + lộ chi tiết lỗi.
- `customErrors mode="Off"` → **trả stack trace/đường dẫn file cho client** khi lỗi.
- `Website\Request\Web.config` cũng `debug="true"` (customErrors `RemoteOnly`).

### 5.5 — Center SOAP (4109) mở trên mọi interface

| Port | Bind | Đánh giá |
|---|---|---|
| 8527 (Road) | `127.0.0.1` | ✅ an toàn |
| 7539 (Fight) | `127.0.0.1` | ✅ |
| 9601 (GM API) | `127.0.0.1` | ✅ |
| 90 (Panel) | `127.0.0.1` | ✅ |
| 80 / 88 (IIS) | `::` | ⚠️ mở all-interface (bình thường với web) |
| **4109 (Center SOAP)** | **`::`** | ⚠️ **mở all-interface** — nếu firewall cho phép, có thể gọi trực tiếp SOAP nội bộ |

### 5.6 — Dữ liệu account Membership thiếu cờ trạng thái

`Db_Membership.dbo.Mem_UserInfo`:
- 11 user, `PasswordFormat = 1` (Hashed) ✔
- **`IsApproved`, `IsLockedOut` đều NULL** (100%) — nếu code kiểm tra `IsApproved == true` thì có thể từ chối login / bỏ qua lockout.
- `PasswordSalt` chỉ dài **3 ký tự** (salt quá ngắn).

### 5.7 — `GC.Collect`, `Thread.Abort`, `while(true)`

- `GC.Collect` 6 chỗ (`CenterServer`, `FightServer` ×2, `GameServer` ×2, `ScriptMgr`) — gây spike hiệu năng.
- `Thread.Abort` 2 chỗ (`GameMgr.cs`, `Form1.cs`).
- `while(true)` 2 chỗ (`DictionaryHelper.cs`, `PlayerAvataInventory.cs`) — cần đảm bảo luôn có `return/break`.

---

## 6. 🟡 LỖI MỨC THẤP / INFO

| # | Phát hiện | Chi tiết |
|---|---|---|
| 1 | `BinaryFormatter` trong `game.base\Game.Base\Marshal.cs` | `ObjectToByteArray`/`ByteArrayToObject` (L192/L200) — **hiện không nơi nào gọi** (dead code). Nếu sau này dùng với dữ liệu mạng → rủi ro deserialization RCE. |
| 2 | Hardcode `sa` credential | 7 chỗ trong `Bussiness` (`BaseBussiness`, `ConsortiaBussiness`, `PlayerBussiness` ×2, `ProduceBussiness`, `JewelCultureMgr`, `GemstoneHandler`) |
| 3 | Hardcode `127.0.0.1` | 8 chỗ (`center.server`, `fighting.server`, `game.server` config, `FarmRankJob`, `GmApiServer` ×3) |
| 4 | `Text file` license | NuGet `log4net 2.0.14` có **NU1902** (lỗ hổng đã biết) — 36 warning khi build |
| 5 | `File .png` client attributes | Client dùng `2.png`/`4.png` giả dạng SWF — khi cập nhật client phải nhớ đúng cặp file `Website` + `Website\Flash` |
| 6 | Panel secret | `appsettings.json` lưu `PasswordHash` SHA256(salt+password) + `Salt` + connection string `sa/123456` dạng plaintext |

---

## 7. Client AS3 — phát hiện

| Chỉ số | Giá trị | Nhận xét |
|---|---:|---|
| File `.as` | 7.439 | Bản `4.png` |
| Empty catch | **68** | Lỗi UI/âm thanh/asset bị bỏ qua |
| `TODO/FIXME` | 1 | |
| `addEventListener` | 11.518 | |
| `removeEventListener` | 10.426 | Chênh **1.092** — nhiều class không gỡ listener |
| `setInterval` / `clearInterval` | 3 / 7 | Tạm ổn |
| Host hardcode legacy | 15 domain | `ddthead.7road.com`, `www.kaixin001.com`, `mygame.17173.com`, `youxi.baidu.com`, `account.changyou.com`, `fcm.duowan.com`, `public.api.360game.vn`, `hotro.zing.vn`… → code chết / có thể gọi nhầm dịch vụ ngoài |

**Top class nghi rò rỉ listener** (add ≫ remove):

| File | add | remove |
|---|---:|---:|
| `ddt\manager\PlayerManager.as` | 44 | 2 |
| `gameCommon\_342$28.as` | 39 | 0 |
| `yzhkof\debug\DebugDisplayObjectViewer.as` | 37 | 0 |
| `fashion\FashionMgr.as` | 25 | 0 |
| `ddt\view\tips\PlayerTip.as` | 26 | 1 |
| `consortion\_47$66.as` | 29 | 5 |
| `room\_212$32.as` | 27 | 5 |
| `ddt\manager\ChatManager.as` | 19 | 0 |
| `kingcar\_147$76.as` | 17 | 0 |
| `church\_38$28.as` | 27 | 10 |

> Singleton manager thường add 1 lần nên không hẳn là leak — nhưng các class view (`PlayerTip`, `church`, `smallMap`) cần kiểm tra khi mở/đóng nhiều lần.

**Cấu hình client:** cả 5 file `config*.xml` đã được bật `<SUIT enable="true" />` (sửa trong phiên trước) ✔.

---

## 8. Database — kiểm tra toàn vẹn

| Kiểm tra | Kết quả | Đánh giá |
|---|---|---|
| Item có `TemplateID` không tồn tại trong `Shop_Goods` | **0** | ✅ |
| Item trùng slot thật (UserID>0, BagType≥0, Place≥0) | **0** | ✅ |
| `BagType` sai (< -1 hoặc > 110) | **0** | ✅ |
| `Place` ngoài khoảng (BagType≥0, Place<0 hoặc >200) | 1.832 | 🟡 cần xác nhận (khả năng cao là pet/extra bag hợp lệ) |
| Item `Count <= 0` | 0 | ✅ |
| Nick trùng tên | 0 | ✅ |
| Gemstone slot sai `EquipPlace`/`FigSpiritId` | 0 | ✅ |
| Item `SuitId` không nằm trong `Suit_Part_Equip` của bộ | **0** (387 item) | ✅ |
| Bộ có bonus nhưng không có item / ngược lại | 0 / 0 | ✅ |
| `Sys_Users_Detail.Password` rỗng | **8/8** | 🟠 login đi qua đường khác? cần xác minh |
| `Mem_UserInfo.IsApproved/IsLockedOut` NULL | 11/11 | 🟠 xem 5.6 |

---

## 9. Cảnh báo build (622 warning)

| Mã | Số lượng | Loại |
|---|---:|---|
| CS0414 | 388 | Field gán nhưng không dùng (chủ yếu script NPC) |
| CS0169 | 86 | Field khai báo không dùng |
| CS0219 | 64 | Biến gán không dùng |
| NU1902 | 36 | **log4net 2.0.14 có lỗ hổng đã biết** |
| CS0162 | 18 | **Unreachable code** (xem 5.2) |
| CS0649 | 12 | Field chưa bao giờ gán |
| CS1717 | 10 | **Gán cho chính nó** |
| CS0108 | 4 | Ẩn thành viên |
| MSB3836 | 2 | Binding redirect trùng (System.Resources.Extensions) |
| CS1522 | 2 | Switch rỗng |

---

## 10. Khu vực cần review thủ công (chưa kết luận)

1. **Công thức sát thương battle** (`Game.Logic`) — balance/damage; đã sửa crit reduce nhưng chưa audit toàn bộ.
2. **Pet / Chiến Hồn / Dây chuyền / Ma Thạch** — nhiều manager phức tạp, dễ sai số.
3. **Thanh toán/nạp** (`OrderBussiness`, `Charge_Order` — SP thiếu) — luồng nạp có thể không hoạt động.
4. **Website handlers** (`Tank.Request.dll`) — không có source, không audit được SQL injection/auth.
5. **GM panel** — phân quyền (Develop/Admin/Moderator), audit log, CSRF.
6. **Client UI** — các lớp view có add/remove listener lệch cao (xem bảng mục 7).
7. **Đa ngôn ngữ** — server Road dùng `Languages\Language-zh_cn.txt` (tiếng Trung) trong khi client tiếng Việt; một số message có thể hiện tiếng Trung.

---

## 11. Cách tái lập kiểm tra

```powershell
# 1) Build + thu warning
dotnet build "C:\Gunny\Gunny12.5\Full Source\Road.Service.sln" -c Release -t:Rebuild -v n |
  Select-String 'warning (CS|NU|MSB)'

# 2) Quét empty catch
Get-ChildItem "C:\Gunny\Gunny12.5\Full Source" -Recurse -Filter *.cs |
  Select-String 'catch\s*(\([^)]*\))?\s*\{\s*\}'

# 3) SP tham chiếu nhưng thiếu
#    (script PowerShell đã dùng: extract "SP_*" -> so với sys.procedures 11 DB)

# 4) Kiểm tra DB
sqlcmd -S DESKTOP-SMLR4VU -U sa -P 123456 -d Db_Tank -C -W -Q "
SELECT COUNT(*) FROM Sys_Users_Goods g
WHERE g.TemplateID>0 AND NOT EXISTS (SELECT 1 FROM Db_Tank_All.dbo.Shop_Goods s WHERE s.TemplateID=g.TemplateID)"
```

---

## 12. Kết luận

- Codebase lớn (1,68 triệu dòng cả server+client) nhưng **chất lượng không tệ**: phần lõi (packet, DB access, GM API) có cấu trúc, nhiều chỗ đã có null-check và log.
- Vấn đề nghiêm trọng nhất là **32 stored procedure thiếu** — gây hỏng tính năng âm thầm (đổi mật khẩu, bank, mark, team, nạp tiền…), và **123 empty catch** khiến lỗi không bao giờ được báo.
- Nhóm lỗi tiếp theo: dead code/logic chết (CS0162/1717/1522/0649), NRE tiềm ẩn ở Team/DevilTurn, cấu hình web debug/lộ stack trace, Center SOAP mở all-interface, MD5/plaintext trong auth.
- Client chủ yếu là nợ kỹ thuật (listener chưa gỡ, domain legacy, empty catch) — không có lỗi nghiêm trọng phát hiện bằng quét tĩnh.

> **Một lần nữa: báo cáo này KHÔNG sửa gì.** Mọi thay đổi cần bạn duyệt từng mục.

---

*Hết báo cáo — 2026-11-22 16:50 (Asia/Bangkok).*
