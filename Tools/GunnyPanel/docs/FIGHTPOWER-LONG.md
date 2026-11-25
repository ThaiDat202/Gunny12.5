# Lá»°C CHIáº¾N (FightPower) â€” hiá»ƒn thá»‹ dáº¡ng LONG (64-bit)

> Sá»± cá»‘: lá»±c chiáº¿n > 2.147.483.647 bá»‹ káº¹p á»Ÿ `2147483647` (int32) trÃªn HUD, cá»­a sá»•
> ThÃ´ng tin cÃ¡ nhÃ¢n vÃ  danh sÃ¡ch thÃ nh viÃªn Guild. ÄÃ£ sá»­a 2026-11-21.

## 1. NguyÃªn nhÃ¢n (3 lá»›p)

| Lá»›p | Váº¥n Ä‘á» |
|---|---|
| **DB** | `Sys_Users_Detail.FightPower` = **bigint** âœ” (Ä‘Ã£ Ä‘Ãºng tá»« trÆ°á»›c). `Sys_Users_Rank_Date` (4 cá»™t `*FightPower`) váº«n **int** âœ— |
| **Server** | `PlayerInfo.FightPower` = long âœ”; `FightPower_PacketInt` = clamp `int.MaxValue`, `FightPower_PacketShow` = pháº§n bÃ¹. CÃ¡c packet `SendLoginSuccess`, `SendUpdatePublicPlayer`, `SendUserEquip` Ä‘Ã£ gá»­i cáº·p int+long âœ”. Má»™t sá»‘ packet khÃ¡c cÃ²n int-only âœ— |
| **Client** | `BasePlayer._fightPower:int`, `_fightPower_show:uint`, `get FightPower_Show():uint` â†’ **tráº§n uint 4.294.967.295** âœ—. Nhiá»u view cÃ²n dÃ¹ng `FightPower` (int) âœ— |
| **Website XML** | `ConsortiaUsersList.ashx` ghi `FightPower` int (clamp) vÃ  **khÃ´ng cÃ³ `ExFightPower`** â†’ client cá»™ng 0 â†’ káº¹p int âœ— |

## 2. ÄÃ£ sá»­a (2026-11-21)

### 2.1 Client SWF (`Website\2.png`, `Website\4.png`, `Website\Flash\...`)

DÃ¹ng FFDec export/import cÃ¡c class:

* `ddt.data.player.BasePlayer`
  * `_fightPower:int â†’ Number`, `_fightPower_show:uint â†’ Number`
  * `get FightPower():Number`, `get FightPower_Show():Number`, `get ExFightPower_Show():Number`
  * `set FightPower(Number)`, `set FightPower_Show(Number)`
* `bagAndInfo.info.PlayerInfoView`: má»i nhÃ¡nh hiá»ƒn thá»‹ dÃ¹ng `_info.FightPower_Show`
* `academy.view.AcademyMemberItem`, `academy.view.AcademyPlayerPanel`: dÃ¹ng `FightPower_Show`

Build:

```powershell
# export class tá»« SWF -> sá»­a .as -> import láº¡i
java -jar C:\Gunny\ToolGunny\_analysis\ffdec\ffdec.jar -selectclass <Class> -export script <outDir> <in.swf>
java -jar C:\Gunny\ToolGunny\_analysis\ffdec\ffdec.jar -onerror abort -importScript <in.swf> <out.swf> <outDir>\scripts
```

Backup SWF cÅ©: `2.png.bak-fp-20261121-0915`, `4.png.bak-fp-20261121-0915` (cÃ¹ng thÆ° má»¥c, cáº£ `Flash\`).

### 2.2 Website: thÃªm `ExFightPower` cho XML Guild

* `Tools\GunnyXmlFix\ConsortiaUsersListFix.cs` â†’ biÃªn dá»‹ch `Website\Request\Bin\GunnyFix.Consortia.dll`
  (csc .NET Framework 4.0, tham chiáº¿u `zlib.net.dll`).
* `Website\Request\ConsortiaUsersListRaw.ashx` â†’ trá» class gá»‘c `Tank.Request.ConsortiaUsersList`
  (giá»¯ nguyÃªn 100% XML cÅ©).
* `Website\Request\ConsortiaUsersList.ashx` â†’ trá» `GunnyFix.ConsortiaUsersListFix`
  (backup: `ConsortiaUsersList.ashx.bak-fp-20261121-0920`).

Handler má»›i: gá»i láº¡i Raw (proxy) â†’ giáº£i nÃ©n zlib â†’ thÃªm `ExFightPower = FightPower âˆ’ min(FightPower, int.MaxValue)`
â†’ nÃ©n zlib tráº£ vá». Má»i thuá»™c tÃ­nh khÃ¡c giá»¯ nguyÃªn (Ä‘Ã£ diff 1-1, khÃ´ng lá»‡ch).

Káº¿t quáº£ kiá»ƒm chá»©ng (user 8, guild 4):

```
FightPower="2147483647"   ExFightPower="43707989009"
# client: 2147483647 + 43707989009 = 45855472656 âœ”
```

### 2.3 VÃ¬ sao nick má»›i khÃ´ng cÃ²n bá»‹

* KhÃ´ng cÃ³ dá»¯ liá»‡u riÃªng pháº£i "seed" â€” má»i hiá»ƒn thá»‹ láº¥y trá»±c tiáº¿p `Sys_Users_Detail.FightPower` (bigint).
* Client Ä‘Ã£ lÆ°u/ cá»™ng báº±ng `Number` (double) nÃªn khÃ´ng trÃ n 32-bit/uint ná»¯a.
* Danh sÃ¡ch Guild láº¥y `ExFightPower` Ä‘á»™ng tá»« DB (khÃ´ng phá»¥ thuá»™c dá»¯ liá»‡u user).

## 3. Viá»‡c cáº§n lÃ m sau khi deploy

1. **ÄÃ³ng háº³n client** (37abc) rá»“i má»Ÿ láº¡i â€” SWF Ä‘Æ°á»£c HTTP-cache, Ä‘Ã£ xoÃ¡ cache táº¡i:
   `%LOCALAPPDATA%\37abc\User Data\Default\Cache` vÃ  `...\Pepper Data\Shockwave Flash\WritableRoot\#SharedObjects`.
2. Kiá»ƒm tra: HUD (thanh trÃªn), ThÃ´ng tin cÃ¡ nhÃ¢n, Cáº¥p Guild â†’ danh sÃ¡ch thÃ nh viÃªn.

## 4. CÃ²n láº¡i (chÆ°a sá»­a)

| Chá»— | Ghi chÃº |
|---|---|
| `Sys_Users_Rank_Date` 4 cá»™t `FightPower/PrevFightPower/ConsortiaFightPower/ConsortiaPrevFightPower` | int â†’ cáº§n `bigint` + sá»­a `SP_GetSingleUserRank`, `SP_Sys_Update_Users_Rank_Date`, `PlayerBussiness.cs:3927-3940` |
| Packet int-only | `SendSceneAddPlayer` (2584), `SendAddFriend` (3692), `SendPlayerEnterMarryRoom` (4567), `PVEGame:2476`, `LoginServerConnector:1765`, `FightServerConnector:624`, `EquipAmuletActivate:98` |
| XML khÃ¡c dÃ¹ng `@ExFightPower` | Friend list, tofflist (báº£ng xáº¿p háº¡ng) â€” cáº§n handler proxy tÆ°Æ¡ng tá»± |
| View client cÃ²n dÃ¹ng int | `TakeInMemberItem`, `BraveDoor`, `church`, `AcademyMemberListAnalyze` (sort)â€¦ |

## 5. Rollback

* Client: copy `2.png.bak-fp-20261121-0915` â†’ `2.png` (vÃ  tÆ°Æ¡ng tá»± `4.png`) á»Ÿ `Website` + `Website\Flash`.
* Guild XML: copy `ConsortiaUsersList.ashx.bak-fp-20261121-0920` â†’ `ConsortiaUsersList.ashx`;
  xoÃ¡ `Bin\GunnyFix.Consortia.dll` (khÃ´ng báº¯t buá»™c) â€” handler cÅ© sáº½ cháº¡y láº¡i.

---

## 6. Đồng bộ lực chiến toàn bộ tài khoản (trần chỉ số) — 2026-11-21

### Vấn đề

Công thức (`GamePlayer.UpdateFightPower`) là **giống nhau cho mọi tài khoản**:

```
FightPower = (Σ4 chỉ số + 1000) × (Damage³ + 3.5 × Armor³) / 200.000.000 + HP × 0.95
```

Số liệu thật lấy từ `GameServer.log`:

| Nhân vật | basicSum | BaseAttack | BaseDefence | FightPower |
|---|---|---|---|---|
| test1 (chuẩn) | 125.350 | 11.377 | 10.536 | 3.519.000.000 |
| admin123 | 14.566 | 3.079 | 1.995 | 4.677.024 |
| **Hồ Thái Đạt** | **170.676** | **22.730** | **22.835** | **45.855.472.656** |

→ Cùng công thức, nhưng đồ của Hồ Thái Đạt đã được GM boost **gấp đôi** Damage/Armor và gấp ~1,4 lần 4 chỉ số;
vì công thức **lũy thừa 3** nên chênh ~13 lần. Đây không phải lỗi công thức mà là **chỉ số vượt chuẩn**.

### Cách đồng bộ đã áp dụng: trần chỉ số trong công thức lực chiến

`UpdateFightPower()` clamp 3 đại lượng **chỉ khi tính lực chiến** (không đổi chỉ số chiến đấu thật):

| Key `Server_Config` | Mặc định | Ý nghĩa |
|---|---|---|
| `FightPowerCapBasicSum` | `125350` | trần Σ(Attack+Defence+Agility+Luck) |
| `FightPowerCapBaseAttack` | `11377` | trần Damage (BaseAttack) dùng tính lực chiến |
| `FightPowerCapBaseDefence` | `10536` | trần Armor (BaseDefence) dùng tính lực chiến |

Hệ số chia quay lại `200.000.000` như cũ.

Kết quả:

* **test1, admin123…**: dưới trần → **không đổi** (vẫn "chuẩn").
* **Hồ Thái Đạt**: bị kéo về đúng mức chuẩn → **~3.519.000.000**.
* **Tài khoản mới**: luôn bị giới hạn ở cùng trần → không bao giờ "ảo" như trước.
* Chỉ số chiến đấu (Damage/Armor/4 stats) **giữ nguyên** — chỉ con số lực chiến bị giới hạn.

Muốn nới trần (ví dụ cho phép mạnh hơn test1 20%) thì sửa 3 key trên trong
`Db_Tank_All.dbo.Server_Config`, build lại `ServerConfig.xml`
(`http://127.0.0.1:88/ServerConfig.ashx`) rồi restart Road.Service — **không cần build code**.

### Deploy

```powershell
dotnet build "C:\Gunny\Gunny12.5\Full Source\Road.Service.sln" -c Release   # 0 error
# rồi chạy bằng quyền Administrator:
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Gunny\Gunny12.5\deploy-fightpower-vng.ps1
```

Sau restart: người chơi đăng nhập lại → `FightPower` được tính lại theo trần mới.
Log `GameServer.log` sẽ có thêm `fpBasicSum=… fpAtk=… fpDef=…` để đối chiếu.

> Nick offline: `Sys_Users_Detail.FightPower` chỉ cập nhật khi họ login (danh sách Guild đọc DB
> nên tạm thời vẫn thấy số cũ cho tới lúc đó).