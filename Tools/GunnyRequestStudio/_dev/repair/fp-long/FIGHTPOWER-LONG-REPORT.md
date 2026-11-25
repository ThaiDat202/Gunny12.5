# BÃ¡o cÃ¡o: chuyá»ƒn Lá»±c chiáº¿n cÃ¡ nhÃ¢n (FightPower) tá»« int â†’ long (64-bit)

Thá»i Ä‘iá»ƒm: 2026-11-09 22:40 â†’ 22:55 (Asia/Bangkok)
Pháº¡m vi: **Db_Tank** (dá»¯ liá»‡u ngÆ°á»i chÆ¡i) + server **Road.Service** (game.server/Bussiness/SqlDataProvider/Game.Logic).

## 1. VÃ¬ sao pháº£i Ä‘á»•i

- Sá»‘ liá»‡u hiá»‡n táº¡i: `Sys_Users_Detail.FightPower` cá»§a test1 = **242.619.769**;
  client hiá»ƒn thá»‹ `Lá»±c chiáº¿n = 922.814.814`.
- Server tÃ¡ch 2 pháº§n gá»­i client (Ä‘Ã£ cÃ³ sáºµn cÆ¡ cháº¿):
  - `WriteInt(PlayerCharacter.FightPower)` â€” pháº§n int (legacy), client Ä‘á»c `readInt()`.
  - `WriteLong(PlayerCharacter.FightPower_Show)` â€” pháº§n cá»™ng thÃªm (long), client Ä‘á»c `readLong()`.
  - Client hiá»ƒn thá»‹ **tá»•ng = FightPower (int) + FightPower_Show (long)** â†’ 242.619.769 + 680.195.045 = **922.814.814** (khá»›p Ä‘Ãºng áº£nh chá»¥p).
- Giá»›i háº¡n cÅ©: `FightPower` lÃ  `int` (DB `int`, `GamePlayer.FightPower int`, `PlayerInfo.FightPower int`), khi vÆ°á»£t
  **2.147.483.647** sáº½ trÃ n/bá»‹ cháº·n `int.MaxValue` â†’ lá»±c chiáº¿n bá»‹ Ä‘á»©ng/Ã¢m.
- Má»¥c tiÃªu: lÆ°u & tÃ­nh **Ä‘áº§y Ä‘á»§ 64-bit**, váº«n hiá»ƒn thá»‹ Ä‘Ãºng trÃªn client hiá»‡n táº¡i.

## 2. ÄÃ£ thay Ä‘á»•i

### 2.1. Database (Db_Tank)

| Äá»‘i tÆ°á»£ng | TrÆ°á»›c | Sau |
|---|---|---|
| `dbo.Sys_Users_Detail.FightPower` | `int NOT NULL` | **`bigint NOT NULL`** (giá»¯ default `0`) |
| `dbo.SP_Users_Update` | `@FightPower int` | **`@FightPower bigint`** |
| View `V_Sys_Users_Detail`, `V_Sys_Users_Friends`, `V_Sys_Marry_Info`, `V_Consortia`, `V_Consortia_Users`, `V_Consortia_Apply_Users` | metadata int | Ä‘Ã£ `sp_refreshview` |

- Backup: `dbo.Sys_Users_Detail_bak_fpbigint` (5 user hiá»‡n cÃ³).
- Script tÃ¡i sá»­ dá»¥ng: `GunnyRequestStudio\_dev\repair\fp-long\migrate-db-fightpower-bigint.sql`.

### 2.2. Server code (Ä‘Ã£ build & deploy)

| File/project | Thay Ä‘á»•i |
|---|---|
| `SqlDataProvider.Data.PlayerInfo` | `_fightPower`/`FightPower`: `int` â†’ **`long`**; thÃªm helper `FightPower_PacketInt` (clamp int) vÃ  `FightPower_PacketShow` (bÃ¹ pháº§n vÆ°á»£t sang long) |
| `SqlDataProvider.Data.FriendInfo`, `MarryInfo`, `ConsortiaInfo`, `ConsortiaUserInfo`, `ConsortiaApplyUserInfo` | `FightPower`: `int` â†’ `long` |
| `game.server.GamePlayer` | `FightPower`, `WashFightPower`: `long`; cÃ´ng thá»©c `UpdateFightPower` tÃ­nh báº±ng `long`/`double`, bá» cháº·n `int.MaxValue`, chá»‘ng Ã¢m báº±ng `long.MaxValue` |
| `Game.Logic.BaseGame`, `PVEGame` vÃ  cÃ¡c packet handler (`AbstractPacketLib`, room handlers, consortia, ringstation...) | Gá»­i client dáº¡ng **split**: `WriteInt(FightPower_PacketInt)` + `WriteLong(FightPower_PacketShow)` cho packet há»“ sÆ¡; cÃ¡c packet chá»‰ cÃ³ 1 trÆ°á»ng int thÃ¬ clamp `FightPowerPacketUtil.ToInt(...)` |
| `Game.Base.Packets.FightPowerPacketUtil` | Helper má»›i: clamp `long` â†’ `int` an toÃ n |
| `AcademyMgr` | NgÆ°á»¡ng/so sÃ¡nh (`Academy_FightPower`, `CheckCanMaster/Apprentice`, `SendAwards`, `UpdateAwardApp`): `long` |
| `Game.Server.GameRoom.Handle.Create` | `GetTimeDelay(long fightPower)` |
| `Road.Service GM API` (`GmApiActions`) | snapshot `FightPower`: `long` (panel hiá»ƒn thá»‹ Ä‘áº§y Ä‘á»§) |
| `Road.Service.csproj` | thÃªm `System.Resources.Extensions` + `GenerateResourceUsePreserializedResources` Ä‘á»ƒ build láº¡i exe (costura) |

- Build láº¡i: `SqlDataProvider`, `Bussiness`, `Game.Base`, `Game.Logic`, `Game.Server`, `Road.Service`.
- Refresh láº¡i resource costura embedded trong `Road.Service.exe` theo DLL má»›i (`_dev\repair\fp-long\refresh-costura-resources.ps1`).
- Deploy vÃ o `Service\Service\Road` (backup `_backup-fp-long-*`), restart Center â†’ Fight â†’ Road.

## 3. Kiá»ƒm chá»©ng

- Harness `fptest` (dÃ¹ng chÃ­nh DLL má»›i + DB tháº­t):
  - `PlayerBussiness.GetUserSingleByUserID(1).FightPower` kiá»ƒu **Int64**, giÃ¡ trá»‹ 242.619.769.
  - Ghi 3.000.000.000 vÃ o DB â†’ Ä‘á»c láº¡i qua Bussiness = **3.000.000.000** (khÃ´ng trÃ n).
  - `FightPower=3.000.000.000`, `FightPower_Show=680.195.045`:
    - `PacketInt = 2.147.483.647` (clamp)
    - `PacketShow = 1.532.711.398`
    - tá»•ng client = **3.680.195.045** (Ä‘Ãºng = 3.000.000.000 + 680.195.045).
- Restart thÃ nh cÃ´ng: Center 1593 / Fight 7539 / Road 8527 + GM API 9601; **Error.log cáº£ 3 service = 0 byte**; health `ok=true`.
- DB sau deploy: `COL_LENGTH(FightPower)=8` (bigint), user1=242.619.769.

## 4. Giá»›i háº¡n cÃ²n láº¡i (quan trá»ng)

- **Hiá»ƒn thá»‹ Lá»±c chiáº¿n cÃ¡ nhÃ¢n**: client hiá»‡n táº¡i Ä‘Ã£ cá»™ng `int + long` (AS3 Number) â†’ sau khi Ä‘á»•i, khi FightPower tháº­t > 2.147.483.647,
  server gá»­i `int = 2.147.483.647` vÃ  dá»“n pháº§n dÆ° sang `FightPower_Show` â†’ client váº«n hiá»ƒn thá»‹ Ä‘Ãºng tá»•ng (tá»›i 2^53).
  KhÃ´ng cáº§n sá»­a client cho Ã´ Lá»±c chiáº¿n chÃ­nh.
- CÃ¡c packet **chá»‰ cÃ³ 1 trÆ°á»ng int** (danh sÃ¡ch thÃ nh viÃªn bang há»™i, má»™t sá»‘ danh sÃ¡ch phÃ²ng/Ä‘áº¥u trÆ°á»ng, proxy room bÃªn Fighting,
  center rank) váº«n bá»‹ clamp á»Ÿ `2.147.483.647` vÃ¬ client chá»‰ Ä‘á»c `readInt()` cho cÃ¡c mÃ n Ä‘Ã³. Muá»‘n cÃ¡c mÃ n nÃ y hiá»‡n > 2.1 tá»· cáº§n
  nÃ¢ng protocol + patch client (SWF) â€” giai Ä‘oáº¡n sau.
- Center/Fight chÆ°a rebuild (váº«n dÃ¹ng báº£n cÅ©); Road Ä‘Ã£ clamp trÆ°á»›c khi gá»­i sang nÃªn 2 service nÃ y cháº¡y an toÃ n, khÃ´ng trÃ n.

## 5. Rollback (náº¿u cáº§n)

1. Stop Center/Fight/Road.
2. Restore `Service\Service\Road\_backup-fp-long-*` (exe + DLL cÅ©) trá»Ÿ láº¡i `Service\Service\Road`.
3. SQL: khÃ´i phá»¥c cá»™t vá» `int`:
   - `SELECT * INTO Sys_Users_Detail_rollback FROM Sys_Users_Detail` (náº¿u cáº§n)
   - Drop default constraint â†’ `ALTER TABLE Sys_Users_Detail ALTER COLUMN FightPower int NOT NULL` â†’ add default `0`.
   - Cháº¡y láº¡i SP_Users_Update báº£n cÅ© (lÆ°u trong `_analysis\sp-fightpower\SP_Users_Update.sql` báº£n gá»‘c trÆ°á»›c khi sá»­a).
4. Start stack.
LÆ°u Ã½: náº¿u Ä‘Ã£ cÃ³ giÃ¡ trá»‹ > 2.1 tá»· thÃ¬ rollback vá» int sáº½ máº¥t/khÃ´ng ghi Ä‘Æ°á»£c.

## 6. File liÃªn quan

- `_dev\repair\fp-long\migrate-db-fightpower-bigint.sql` â€” migration DB.
- `_dev\repair\fp-long\deploy-fp-long.ps1` â€” deploy + restart.
- `_dev\repair\fp-long\refresh-costura-resources.ps1` â€” refresh embedded DLL costura.
- `_dev\repair\fp-long\fptest.*` â€” harness kiá»ƒm chá»©ng Ä‘á»c/ghi long + split packet.
- `Service\Service\Road\_backup-fp-long-*` â€” backup DLL/exe trÆ°á»›c khi deploy.
- Backup DB: `Sys_Users_Detail_bak_fpbigint`; SP gá»‘c: `_analysis\sp-fightpower\SP_Users_Update.sql`.
## 7. Bổ sung: kiểm chứng tầng lưu (SP_Users_Update)

- `PlayerBussiness.UpdatePlayer(PlayerInfo)` với `FightPower = 3.221.225.472` (> int32) trả về `True`;
  DB `Sys_Users_Detail.FightPower` đọc lại = **3.221.225.472**; sau đó restore về 242.619.769 OK.
- Như vậy toàn bộ chuỗi ghi: `PlayerInfo.FightPower (long)` → `SqlParameter("bigint")` → `SP_Users_Update @FightPower bigint`
  → `Sys_Users_Detail.FightPower bigint` hoạt động đúng, không tràn.

## 8. Website Request (ASP.NET) tương thích bigint + handler FightNoviceRecord

Sau khi đổi DB FightPower int->bigint, website `WebSite\Request` (Bussiness.dll cu 2022) bi loi
`InvalidCastException` trong `InitPlayerInfo`/`ActivePlayer` => `VisualizeRegister.ashx` tra `value=false`
=> client hien **"注册形象失败1"**.

Da sua:
- Rebuild `WebSite\Request\Bin\Bussiness.dll` tu source decompile cua ban cu, chi thay cac cho doc FightPower
  bang `FightPowerCompat.FromDb(object)` (nhan Int64/bigint, clamp int.MaxValue). Build net472, giu nguyen dependency cu.
  Backup: `WebSite\Request\Bin\_backup-fpweb-*\Bussiness.dll`; ban goc: `_dev\repair\fp-long\web\Bussiness.dll.orig`.
- File `FightNoviceRecord.ashx` tro class `Tank.Request.FightNoviceRecord` khong co trong `Tank.Request.dll`
  (package thieu) => Parser Error. Da them assembly `Tank.Request.Extra.dll` (class handler, tra XML
  `<Result value="true" message="Success!"/>`, ghi log `logs\fightnovicerecord.log`).
- Recycle app domain bang cach thay DLL + touch Web.config (khong can iisreset).

Ket qua test:
- `/fightnovicerecord.ashx?...` = 200 + XML success.
- `/NickNameCheck.ashx?...` = 200 XML (truoc do InvalidCast).
- `/ServerList.ashx`, `/UserRankDate.ashx`, `/GetSID.ashx` = 200.
- Account IsFirst=0 + `/VisualizeRegister.ashx` = `value="true"` + tao du nhan vat/7 item; user test da xoa khoi DB.
- `WebSite\Request\logs\Error.log` khong con InvalidCast.

Chi tiet: `_dev\repair\fp-long\web\README-web-compat.md`.
