<#
    refresh-trang-thai.ps1
    ---------------------------------------------------------------
    Auto-refresh the AUTO blocks in TRANG-THAI-CLIENT-DB-WEBSHOP.md

    - Probes the real system: files, ports, HTTP, SQL Server.
    - Rewrites:  SNAPSHOT block, ISSUES block, LOG block.
    - Compares status with .trang-thai-state.json and appends a
      changelog line when something flips (ISSUE -> OK).
    - Read-only probes. Does NOT restart services, does NOT write DB.

    ASCII only on purpose: Windows PowerShell 5.1 + Vietnamese in .ps1
    needs UTF-8 with BOM. Keep this file pure ASCII so it just works.

    Usage:
      powershell -ExecutionPolicy Bypass -File refresh-trang-thai.ps1
      powershell -ExecutionPolicy Bypass -File refresh-trang-thai.ps1 -Quiet
#>
param([switch]$Quiet)

$ErrorActionPreference = 'Continue'

$Here      = $PSScriptRoot
if (-not $Here) { $Here = Split-Path -Parent $MyInvocation.MyCommand.Path }
$MdPath    = Join-Path $Here 'TRANG-THAI-CLIENT-DB-WEBSHOP.md'
$StatePath = Join-Path $Here '.trang-thai-state.json'

$Root  = 'C:\Gunny\Gunny12.5'
$Web   = Join-Path $Root 'Website'
$Svc   = Join-Path $Root 'Service\Service'
$Src   = Join-Path $Root 'Full Source'
$Panel = Join-Path $Root 'Tools\GunnyPanel\src\GunnyPanel'

$SqlServer = 'DESKTOP-SMLR4VU'
$SqlUser   = 'sa'
$SqlPass   = '123456'

$Stamp = (Get-Date).ToString('yyyy-MM-dd HH:mm')

# ---------------------------------------------------------------- helpers

function Read-Utf8([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    $t = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    if ($t.Length -gt 0 -and $t[0] -eq [char]0xFEFF) { $t = $t.Substring(1) }
    return $t
}

function Write-Utf8([string]$Path, [string]$Text) {
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $enc)
}

function Sql1([string]$Db, [string]$Query) {
    $out = & sqlcmd -S $SqlServer -U $SqlUser -P $SqlPass -d $Db -C -h -1 -W -Q ("SET NOCOUNT ON; " + $Query) 2>&1
    if ($LASTEXITCODE -ne 0) { return 'ERR' }
    $lines = @()
    foreach ($l in $out) {
        $s = $l.ToString().Trim()
        if ($s -ne '') { $lines += $s }
    }
    if ($lines.Count -eq 0) { return 'ERR' }
    return ($lines -join ' ')
}

function PortState([int]$Port) {
    $c = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if ($c) { return 'LISTEN' }
    return 'CLOSED'
}

function FileLine([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path)) { return ("{0} = MISSING" -f $Label) }
    $i = Get-Item -LiteralPath $Path
    return ("{0} = {1:N0} B @ {2}" -f $Label, $i.Length, $i.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))
}

function Replace-Block([string]$Text, [string]$StartMarker, [string]$EndMarker, [string]$Body) {
    $i1 = $Text.IndexOf($StartMarker)
    if ($i1 -lt 0) { return $Text }
    $i2 = $Text.IndexOf($EndMarker, $i1)
    if ($i2 -lt 0) { return $Text }
    $head = $Text.Substring(0, $i1 + $StartMarker.Length)
    $tail = $Text.Substring($i2)
    return ($head + "`r`n" + $Body.TrimEnd() + "`r`n" + $tail)
}

function Get-BlockBody([string]$Text, [string]$StartMarker, [string]$EndMarker) {
    $i1 = $Text.IndexOf($StartMarker)
    if ($i1 -lt 0) { return '' }
    $i2 = $Text.IndexOf($EndMarker, $i1)
    if ($i2 -lt 0) { return '' }
    $from = $i1 + $StartMarker.Length
    return $Text.Substring($from, $i2 - $from)
}

# ---------------------------------------------------------------- issues

$Issues = New-Object System.Collections.ArrayList

function Add-Issue([string]$Id, [string]$Title, [string]$Status, [string]$Evidence) {
    [void]$Issues.Add([pscustomobject]@{ Id = $Id; Title = $Title; Status = $Status; Evidence = $Evidence })
}

# ---- WS-01: Webshop button port
$wsHref = ''
$pgPath = Join-Path $Web 'PlayGame.aspx'
if (Test-Path -LiteralPath $pgPath) {
    $pg = Read-Utf8 $pgPath
    $m = [regex]::Match($pg, 'href="([^"]+)"[^>]*class="menu btnPay"')
    if (-not $m.Success) { $m = [regex]::Match($pg, 'class="menu btnPay"[^>]*href="([^"]+)"') }
    if ($m.Success) { $wsHref = $m.Groups[1].Value }
}
$wsPort = 0
$wsState = 'n/a'
if ($wsHref -match '127\.0\.0\.1:(\d+)') {
    $wsPort = [int]$Matches[1]
    $wsState = PortState $wsPort
}
if ($wsPort -gt 0 -and $wsState -eq 'LISTEN') {
    Add-Issue 'WS-01' 'Webshop button target' 'OK' ("href={0} port={1} {2}" -f $wsHref, $wsPort, $wsState)
}
else {
    Add-Issue 'WS-01' 'Webshop button target' 'ISSUE' ("href={0} port={1} {2}" -f $wsHref, $wsPort, $wsState)
}

# ---- WS-02: FILL_PATH (nap the)
$fill = ''
$webCfgPath = Join-Path $Web 'config.xml'
if (Test-Path -LiteralPath $webCfgPath) {
    $wc = Read-Utf8 $webCfgPath
    $m = [regex]::Match($wc, '<FILL_PATH\s+value="([^"]+)"')
    if ($m.Success) { $fill = $m.Groups[1].Value }
}
$fillOk = $false
if ($fill -ne '') {
    $rel = $fill -replace '^https?://[^/]+/', ''
    $loc = Join-Path $Web ($rel -replace '/', '\')
    $fillOk = Test-Path -LiteralPath $loc
}
if ($fillOk) {
    Add-Issue 'WS-02' 'Top-up FILL_PATH' 'OK' ("FILL_PATH={0} exists" -f $fill)
}
else {
    Add-Issue 'WS-02' 'Top-up FILL_PATH' 'ISSUE' ("FILL_PATH={0} -> target missing" -f $fill)
}

# ---- WS-03 / WS-04: webshop tables
$swiRows = Sql1 'Db_Tank' 'SELECT COUNT(*) FROM Site_Webshop_Item'
if ($swiRows -eq 'ERR') {
    Add-Issue 'WS-03' 'Site_Webshop_Item rows' 'ERR' 'query failed'
}
elseif ([int]$swiRows -gt 0) {
    Add-Issue 'WS-03' 'Site_Webshop_Item rows' 'OK' ("Site_Webshop_Item={0} rows" -f $swiRows)
}
else {
    Add-Issue 'WS-03' 'Site_Webshop_Item rows' 'ISSUE' 'Site_Webshop_Item=0 rows'
}

$bagRows = Sql1 'Db_Tank' 'SELECT COUNT(*) FROM webshop_bag'
if ($bagRows -eq 'ERR') {
    Add-Issue 'WS-04' 'webshop_bag rows' 'ERR' 'query failed'
}
elseif ([int]$bagRows -gt 0) {
    Add-Issue 'WS-04' 'webshop_bag rows' 'OK' ("webshop_bag={0} rows" -f $bagRows)
}
else {
    Add-Issue 'WS-04' 'webshop_bag rows' 'ISSUE' 'webshop_bag=0 rows'
}

# ---- WS-05: cheap item list empty
$cheapBody = $null
try {
    $r = Invoke-WebRequest 'http://127.0.0.1:88/shopcheapitemlist.ashx' -UseBasicParsing -TimeoutSec 20
    $cheapBody = $r.Content
}
catch { $cheapBody = $null }
if ($null -eq $cheapBody) {
    Add-Issue 'WS-05' 'Cheap shop list' 'ERR' 'endpoint unreachable'
}
elseif ($cheapBody -match '<Item') {
    Add-Issue 'WS-05' 'Cheap shop list' 'OK' 'endpoint returns Item nodes'
}
else {
    Add-Issue 'WS-05' 'Cheap shop list' 'ISSUE' ("endpoint empty ({0} B)" -f $cheapBody.Length)
}

# ---- SEC-01/02/03: Web.config hardening (NOTE: Web.config, not config.xml)
$webConfigPath = Join-Path $Web 'Web.config'
$webCfgSec = ''
if (Test-Path -LiteralPath $webConfigPath) { $webCfgSec = Read-Utf8 $webConfigPath }

if ($webCfgSec -match '<compilation[^>]*debug="true"') {
    Add-Issue 'SEC-01' 'compilation debug=true' 'ISSUE' 'Website\Web.config debug="true"'
} else {
    Add-Issue 'SEC-01' 'compilation debug=true' 'OK' 'no debug="true" found'
}

if ($webCfgSec -match '<customErrors[^>]*mode="Off"') {
    Add-Issue 'SEC-02' 'customErrors Off' 'ISSUE' 'Website\Web.config customErrors mode="Off"'
} else {
    Add-Issue 'SEC-02' 'customErrors Off' 'OK' 'customErrors not Off'
}

$plainSa = 0
foreach ($k in @('conString', 'crosszoneString', 'membershipDb', 'Db_TankConnectionString')) {
    if ($webCfgSec -match ([regex]::Escape('key="' + $k + '"') + '[^>]*password=') -or
        $webCfgSec -match ([regex]::Escape('name="' + $k + '"') + '[^>]*password=')) { $plainSa++ }
}
if ($plainSa -gt 0) {
    Add-Issue 'SEC-03' 'Plaintext sa connection strings' 'ISSUE' ("{0} connection string(s) with plaintext password" -f $plainSa)
} else {
    Add-Issue 'SEC-03' 'Plaintext sa connection strings' 'OK' 'no plaintext password= in known keys'
}

# ---- CL-01: FlashVNG not patched
$hMain2 = ''
$hVng2 = ''
$hMain4 = ''
$hVng4 = ''
if (Test-Path -LiteralPath (Join-Path $Web '2.png'))  { $hMain2 = (Get-FileHash -LiteralPath (Join-Path $Web '2.png')  -Algorithm MD5).Hash.Substring(0, 12) }
if (Test-Path -LiteralPath (Join-Path $Web '4.png'))  { $hMain4 = (Get-FileHash -LiteralPath (Join-Path $Web '4.png')  -Algorithm MD5).Hash.Substring(0, 12) }
if (Test-Path -LiteralPath (Join-Path $Web 'FlashVNG\2.png')) { $hVng2 = (Get-FileHash -LiteralPath (Join-Path $Web 'FlashVNG\2.png') -Algorithm MD5).Hash.Substring(0, 12) }
if (Test-Path -LiteralPath (Join-Path $Web 'FlashVNG\4.png')) { $hVng4 = (Get-FileHash -LiteralPath (Join-Path $Web 'FlashVNG\4.png') -Algorithm MD5).Hash.Substring(0, 12) }
if (($hMain2 -ne '' -and $hMain2 -eq $hVng2) -and ($hMain4 -ne '' -and $hMain4 -eq $hVng4)) {
    Add-Issue 'CL-01' 'FlashVNG client patched' 'OK' ("2.png={0} 4.png={1} (same as main)" -f $hVng2, $hVng4)
}
else {
    Add-Issue 'CL-01' 'FlashVNG client patched' 'ISSUE' ("main 2={0} 4={1} | VNG 2={2} 4={3}" -f $hMain2, $hMain4, $hVng2, $hVng4)
}

# ---- CL-02: LOGIN_PATH default document (from config.xml)
$loginPath = ''
$cfgXml = ''
if (Test-Path -LiteralPath $webCfgPath) { $cfgXml = Read-Utf8 $webCfgPath }
$m = [regex]::Match($cfgXml, '<LOGIN_PATH\s+value="([^"]+)"')
if ($m.Success) { $loginPath = $m.Groups[1].Value }
$loginOk = $true
if ($loginPath -ne '') {
    $rel = $loginPath -replace '^https?://[^/]+/?', ''
    if ($rel -ne '') { $loginOk = Test-Path -LiteralPath (Join-Path $Web ($rel -replace '/', '\')) }
}
if ($loginOk) {
    Add-Issue 'CL-02' 'LOGIN_PATH target exists' 'OK' ("LOGIN_PATH={0}" -f $loginPath)
} else {
    Add-Issue 'CL-02' 'LOGIN_PATH target exists' 'ISSUE' ("LOGIN_PATH={0} -> target missing" -f $loginPath)
}

# ---- DB-01: stale databases
$rpCount = Sql1 'master' "SELECT COUNT(*) FROM sys.databases WHERE state_desc='RECOVERY_PENDING'"
$staleMb = Sql1 'master' "SELECT CAST(SUM(size)*8.0/1024 AS DECIMAL(10,1)) FROM sys.master_files WHERE database_id IN (SELECT database_id FROM sys.databases WHERE name IN ('Db_TankSchemaTemp','BKorigT','BKpet','BKpetOld','BKcmpA'))"
$tmpDbExists = Sql1 'master' "SELECT COUNT(*) FROM sys.databases WHERE name='Db_TankSchemaTemp'"
if ($rpCount -eq 'ERR' -or $staleMb -eq 'ERR') {
    Add-Issue 'DB-01' 'Stale databases' 'ERR' 'query failed'
}
elseif ([int]$rpCount -eq 0 -and $tmpDbExists -eq '0') {
    Add-Issue 'DB-01' 'Stale databases' 'OK' 'no stale database left'
}
else {
    Add-Issue 'DB-01' 'Stale databases' 'ISSUE' ("recovery_pending={0} schemaTemp={1} total={2} MB" -f $rpCount, $tmpDbExists, $staleMb)
}

# ---- DB-02: backup tables
$bak1 = Sql1 'Db_Tank' "SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'bak[_]%' OR name LIKE '%[_]bak%' OR name = 'TempTable'"
$bak2 = Sql1 'Db_Tank_All' "SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'bak[_]%' OR name LIKE '%[_]bak%'"
if ($bak1 -eq 'ERR' -or $bak2 -eq 'ERR') {
    Add-Issue 'DB-02' 'Backup tables' 'ERR' 'query failed'
}
elseif (([int]$bak1 + [int]$bak2) -eq 0) {
    Add-Issue 'DB-02' 'Backup tables' 'OK' 'no backup table left'
}
else {
    Add-Issue 'DB-02' 'Backup tables' 'ISSUE' ("Db_Tank={0} Db_Tank_All={1} total={2}" -f $bak1, $bak2, ([int]$bak1 + [int]$bak2))
}

# ---- DB-03: extra Shop_Goods rows in Db_Tank
$extra = Sql1 'master' "SELECT COUNT(*) FROM Db_Tank.dbo.Shop_Goods t WHERE NOT EXISTS (SELECT 1 FROM Db_Tank_All.dbo.Shop_Goods a WHERE a.TemplateID = t.TemplateID)"
if ($extra -eq 'ERR') {
    Add-Issue 'DB-03' 'Shop_Goods drift Db_Tank vs Db_Tank_All' 'ERR' 'query failed'
}
elseif ([int]$extra -eq 0) {
    Add-Issue 'DB-03' 'Shop_Goods drift Db_Tank vs Db_Tank_All' 'OK' 'no extra row in Db_Tank'
}
else {
    Add-Issue 'DB-03' 'Shop_Goods drift Db_Tank vs Db_Tank_All' 'ISSUE' ("{0} extra TemplateID in Db_Tank" -f $extra)
}

# ---- DB-04: panel hardcoded item count
$shopGoodsCount = Sql1 'Db_Tank_All' 'SELECT COUNT(*) FROM Shop_Goods'
$shopCfg = Read-Utf8 (Join-Path $Panel 'Pages\Shop.cshtml')
if ($null -eq $shopCfg) {
    Add-Issue 'DB-04' 'Panel Shop hardcoded count' 'ERR' 'Shop.cshtml not found'
}
elseif ($shopCfg -match '15[.,]060') {
    Add-Issue 'DB-04' 'Panel Shop hardcoded count' 'ISSUE' ("Shop.cshtml says 15.060, actual={0}" -f $shopGoodsCount)
}
else {
    Add-Issue 'DB-04' 'Panel Shop hardcoded count' 'OK' ("hardcoded text removed, actual={0}" -f $shopGoodsCount)
}

# ---------------------------------------------------------------- snapshot

$snap = New-Object System.Collections.ArrayList

$svcParts = @()
foreach ($n in @('Road.Service', 'Fighting.Service', 'Center.Service', 'GunnyPanel')) {
    $p = Get-Process -Name $n -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) { $svcParts += ("{0}=PID {1} @ {2}" -f $n, $p.Id, $p.StartTime.ToString('MM-dd HH:mm')) }
    else { $svcParts += ("{0}=DOWN" -f $n) }
}
[void]$snap.Add(@('Service', ($svcParts -join ' \| ')))

$portParts = @()
foreach ($p in @(80, 88, 90, 91, 92, 93, 1593, 4109, 7539, 8527, 9601)) {
    $portParts += ("{0}={1}" -f $p, (PortState $p))
}
[void]$snap.Add(@('Port', ($portParts -join ' \| ')))

[void]$snap.Add(@('DLL Road Game.Server', (FileLine (Join-Path $Svc 'Road\Game.Server.dll') 'Game.Server.dll')))
[void]$snap.Add(@('DLL Road Bussiness', (FileLine (Join-Path $Svc 'Road\Bussiness.dll') 'Bussiness.dll')))
[void]$snap.Add(@('DLL Road Game.Logic', (FileLine (Join-Path $Svc 'Road\Game.Logic.dll') 'Game.Logic.dll')))
[void]$snap.Add(@('DLL Fight Fighting.Server', (FileLine (Join-Path $Svc 'Fight\Fighting.Server.dll') 'Fighting.Server.dll')))
[void]$snap.Add(@('DLL Fight Game.Logic', (FileLine (Join-Path $Svc 'Fight\Game.Logic.dll') 'Game.Logic.dll')))

$dllCut = $null
$roadDll = Join-Path $Svc 'Road\Game.Server.dll'
if (Test-Path -LiteralPath $roadDll) { $dllCut = (Get-Item -LiteralPath $roadDll).LastWriteTime }
$newerCs = 0
$csTotal = 0
if (Test-Path -LiteralPath $Src) {
    $csAll = Get-ChildItem -LiteralPath $Src -Recurse -File -Filter *.cs -ErrorAction SilentlyContinue |
             Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\|_build_release|_build_test' }
    $csTotal = $csAll.Count
    if ($dllCut) { $newerCs = ($csAll | Where-Object { $_.LastWriteTime -gt $dllCut }).Count }
}
[void]$snap.Add(@('Source .cs', ("{0} file / so-file-cs-moi-hon-DLL = {1}" -f $csTotal, $newerCs)))

$drive = Get-PSDrive C -ErrorAction SilentlyContinue
if ($drive) {
    [void]$snap.Add(@('O dia C', ("con trong {0:N1} GB / da dung {1:N1} GB" -f ($drive.Free / 1GB), ($drive.Used / 1GB))))
}

$dbParts = @()
foreach ($d in @('Db_Tank', 'Db_Tank_All', 'Db_Membership', 'Db_Count', 'Db_Logs', 'Db_GM')) {
    $mb = Sql1 'master' ("SELECT CAST(SUM(size)*8.0/1024 AS DECIMAL(10,1)) FROM sys.master_files WHERE database_id = DB_ID('" + $d + "')")
    $dbParts += ("{0}={1} MB" -f $d, $mb)
}
[void]$snap.Add(@('DB online', ($dbParts -join ' \| ')))

[void]$snap.Add(@('DB rac (tong)', ("{0} MB \| recovery_pending = {1}" -f $staleMb, $rpCount)))
[void]$snap.Add(@('Bang backup', ("Db_Tank={0} \| Db_Tank_All={1}" -f $bak1, $bak2)))

[void]$snap.Add(@('Shop_Goods (Db_Tank_All)', $shopGoodsCount))
[void]$snap.Add(@('Shop_Goods (Db_Tank)', (Sql1 'Db_Tank' 'SELECT COUNT(*) FROM Shop_Goods')))
[void]$snap.Add(@('Shop rows', (Sql1 'Db_Tank_All' 'SELECT COUNT(*) FROM Shop')))
[void]$snap.Add(@('Shop_Goods_Show_List', (Sql1 'Db_Tank_All' 'SELECT COUNT(*) FROM Shop_Goods_Show_List')))
[void]$snap.Add(@('Suit_Template / Suit_Bonus', ("{0} / {1}" -f (Sql1 'Db_Tank_All' 'SELECT COUNT(*) FROM Suit_Template'), (Sql1 'Db_Tank_All' 'SELECT COUNT(*) FROM Suit_Bonus'))))
[void]$snap.Add(@('Site_Webshop_Item / webshop_bag', ("{0} / {1}" -f $swiRows, $bagRows)))
[void]$snap.Add(@('eStore / Pay_Exchange', ("{0} / {1}" -f (Sql1 'Db_Membership' 'SELECT COUNT(*) FROM eStore'), (Sql1 'Db_Membership' 'SELECT COUNT(*) FROM Pay_Exchange'))))

$reqDir = Join-Path $Web 'Request'
$xmlCount = 0
$xmlMb = 0
if (Test-Path -LiteralPath $reqDir) {
    $xmls = Get-ChildItem -LiteralPath $reqDir -File -Filter *.xml -ErrorAction SilentlyContinue
    $xmlCount = $xmls.Count
    if ($xmlCount -gt 0) { $xmlMb = [math]::Round((($xmls | Measure-Object -Property Length -Sum).Sum / 1MB), 1) }
}
[void]$snap.Add(@('Kich thuoc thu muc Request (XML)', ("{0} file .xml / {1} MB" -f $xmlCount, $xmlMb)))

[void]$snap.Add(@('Client 2.png (main / VNG)', ("{0} / {1}" -f $hMain2, $hVng2)))
[void]$snap.Add(@('Client 4.png (main / VNG)', ("{0} / {1}" -f $hMain4, $hVng4)))

# ---------------------------------------------------------------- render

$snapBody = New-Object System.Collections.ArrayList
[void]$snapBody.Add('| Muc | Gia tri |')
[void]$snapBody.Add('|---|---|')
[void]$snapBody.Add(("| **Kiem tra luc** | **{0}** (Asia/Bangkok) |" -f $Stamp))
foreach ($row in $snap) {
    [void]$snapBody.Add(("| {0} | {1} |" -f $row[0], $row[1]))
}
$snapBodyText = ($snapBody -join "`r`n")

$okCount = 0
$issueCount = 0
$errCount = 0
foreach ($i in $Issues) {
    if ($i.Status -eq 'OK') { $okCount++ }
    elseif ($i.Status -eq 'ISSUE') { $issueCount++ }
    else { $errCount++ }
}
$issueBody = New-Object System.Collections.ArrayList
[void]$issueBody.Add(("**Tom tat:** {0} OK / {1} ISSUE / {2} ERR - kiem tra luc {3}" -f $okCount, $issueCount, $errCount, $Stamp))
[void]$issueBody.Add('')
[void]$issueBody.Add('| ID | Trang thai | Bang chung (tu kiem) |')
[void]$issueBody.Add('|---|---|---|')
foreach ($i in $Issues) {
    $mark = 'ISSUE'
    if ($i.Status -eq 'OK') { $mark = '**OK**' }
    elseif ($i.Status -eq 'ERR') { $mark = 'ERR' }
    [void]$issueBody.Add(("| {0} | {1} | {2} |" -f $i.Id, $mark, ($i.Evidence -replace '\|', '/')))
}
$issueBodyText = ($issueBody -join "`r`n")

# ---------------------------------------------------------------- changelog

$prevState = @{}
if (Test-Path -LiteralPath $StatePath) {
    try {
        $json = Read-Utf8 $StatePath
        $obj = $json | ConvertFrom-Json
        foreach ($prop in $obj.PSObject.Properties) { $prevState[$prop.Name] = [string]$prop.Value }
    }
    catch { $prevState = @{} }
}

$logNew = New-Object System.Collections.ArrayList
foreach ($i in $Issues) {
    $old = $null
    if ($prevState.ContainsKey($i.Id)) { $old = $prevState[$i.Id] }
    if ($null -eq $old) {
        if ($prevState.Count -gt 0) {
            [void]$logNew.Add(("- {0} | {1} | (moi) -> {2} | {3}" -f $Stamp, $i.Id, $i.Status, $i.Evidence))
        }
    }
    elseif ($old -ne $i.Status) {
        [void]$logNew.Add(("- {0} | {1} | {2} -> {3} | {4}" -f $Stamp, $i.Id, $old, $i.Status, $i.Evidence))
    }
}

# ---------------------------------------------------------------- write md

if (-not (Test-Path -LiteralPath $MdPath)) {
    Write-Host "KHONG TIM THAY: $MdPath" -ForegroundColor Red
    exit 2
}

$md = Read-Utf8 $MdPath
$md = Replace-Block $md '<!-- AUTO:SNAPSHOT:START -->' '<!-- AUTO:SNAPSHOT:END -->' $snapBodyText
$md = Replace-Block $md '<!-- AUTO:ISSUES:START -->' '<!-- AUTO:ISSUES:END -->' $issueBodyText

$logPath = Join-Path $Here '.trang-thai-log.txt'
if ($logNew.Count -gt 0) {
    $oldLog = ''
    if (Test-Path -LiteralPath $logPath) { $oldLog = [System.IO.File]::ReadAllText($logPath, [System.Text.Encoding]::UTF8) }
    $allLog = ($logNew -join "`r`n") + "`r`n" + $oldLog
    [System.IO.File]::WriteAllText($logPath, $allLog, (New-Object System.Text.UTF8Encoding($false)))
}

$logBody = ''
if (Test-Path -LiteralPath $logPath) {
    $logBody = [System.IO.File]::ReadAllText($logPath, [System.Text.Encoding]::UTF8)
    $lines = @($logBody -split "`r?`n" | Where-Object { $_.Trim() -ne '' })
    if ($lines.Count -gt 200) {
        $lines = $lines[0..199]
        $lines += '- ... (chi giu 200 dong gan nhat, xem .trang-thai-log.txt)'
    }
    $logBody = ($lines -join "`r`n")
}
if ($logBody.Trim() -eq '') { $logBody = '(chua co thay doi nao duoc ghi nhan)' }
$md = Replace-Block $md '<!-- AUTO:LOG:START -->' '<!-- AUTO:LOG:END -->' $logBody

Write-Utf8 $MdPath $md

# ---------------------------------------------------------------- save state

$stateObj = [ordered]@{}
foreach ($i in $Issues) { $stateObj[$i.Id] = $i.Status }
$stateObj['_lastRun'] = $Stamp
Write-Utf8 $StatePath ($stateObj | ConvertTo-Json)

# ---------------------------------------------------------------- console

if (-not $Quiet) {
    Write-Host ''
    Write-Host '=========================================================' -ForegroundColor Cyan
    Write-Host (" TRANG THAI  {0}" -f $Stamp) -ForegroundColor Cyan
    Write-Host '=========================================================' -ForegroundColor Cyan
    foreach ($i in $Issues) {
        $color = 'Yellow'
        if ($i.Status -eq 'OK') { $color = 'Green' }
        elseif ($i.Status -eq 'ERR') { $color = 'Magenta' }
        Write-Host ("  {0,-7} {1,-8} {2}" -f $i.Id, $i.Status, $i.Evidence) -ForegroundColor $color
    }
    Write-Host ''
    Write-Host ("  OK={0}  ISSUE={1}  ERR={2}" -f $okCount, $issueCount, $errCount)
    if ($logNew.Count -gt 0) {
        Write-Host ''
        Write-Host '  Thay doi so voi lan chay truoc:' -ForegroundColor Yellow
        foreach ($l in $logNew) { Write-Host ("    " + $l) }
    }
    Write-Host ''
    Write-Host ("  Da cap nhat: {0}" -f $MdPath) -ForegroundColor Green
    Write-Host ''
}

exit 0
