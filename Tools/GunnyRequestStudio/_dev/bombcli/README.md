# bombcli - harness dev dong bo bomb/crater asset (GunnyRequestStudio)

Project net48 console, compile chung source `..\..\src\Core\**\*.cs` (khong dung ProjectReference)
=> chay duoc ca khi App/solution dang duoc build boi agent khac.

## Lenh

```bat
cd C:\Gunny\ToolGunny\GunnyRequestStudio\_dev\bombcli
dotnet run -c Release -- check   :: kiem tra .bomb + roundtrip PNG + so sanh crater.png voi mask that
dotnet run -c Release -- scan    :: quet id tham chieu, ghi reports\bomb-report.json
dotnet run -c Release -- rip     :: mirror asset thieu tu CDN ve WebSite\image\bomb
dotnet run -c Release -- mask    :: sinh .bomb tu crater.png va deploy vao cac folder Bomb
dotnet run -c Release -- site    :: doc SITE trong config.xml, bao thieu asset gi cho id moi
dotnet run -c Release -- all     :: check -> scan -> rip -> mask -> scan lai (final)
```

Tuy chon: `--overwrite` (ghi de file da co, mask cu duoc backup vao `<folder>\_bak_<yyyyMMdd_HHmmss>\`).

## File sinh ra

| File | Noi dung |
|---|---|
| `reports\bomb-report.json` | report day du: per-id status, so mask, asset tai/404, unresolved, ket qua check, client-site |
| `cache\probe-cache.json` | cache HEAD probe theo duong dan (rerun nhanh, khong cache loi mang) |
| `cache\last-new-ids.json` | id thieu mask truoc khi deploy (dung cho lenh `site`) |
| `logs\bombcli-YYYYMMDD.log` | log chi tiet |

## Dinh dang .bomb (da kiem chung)

```
offset 0 : int32 LE width
offset 4 : int32 LE height
offset 8 : anh 1bpp, stride = (width >> 3) + 1 byte/hang (luon +1 ke ca width chia het 8),
           MSB = pixel trai nhat, bit = 1 => dat bi pha.
```

## Quy tac sinh mask (quan trong)

Server (`Game.Logic.BallMgr.LoadFromFiles`) nap mask theo **BALL ID**:
`bomb\{ballInfo.ID}.bomb`, nhung noi dung mask cua ball = `crater.png` cua
crater duoc tro boi thuoc tinh `Crater` cua ball do (nhieu ball dung chung 1 crater).
Vi vay khi sinh mask: tra `Crater` cua ball -> tai `image/bomb/crater/<craterId>/crater.png`
-> chuyen 1bpp -> ghi `<ballId>.bomb`. Ball co `Crater` rong/0 thi khong can mask.

- PNG -> mask: bit = (alpha >= 128), kich thuoc lay theo PNG (khop 99.77% pixel voi mask publisher).
- Mask -> PNG preview: trang duc = bi pha, trong suot = con nguyen.
