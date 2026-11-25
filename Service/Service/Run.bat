@echo off
setlocal EnableExtensions
REM ============================================================================
REM  Gunny12.5 - Run 3 service (Center -> Fight -> Road)
REM  - Tu dong yeu cau quyen Administrator (UAC).
REM  - Tat service cu, start lai dung thu tu:
REM      Center.Service.exe  (1593)
REM      Fighting.Service.exe(7539)
REM      Road.Service.exe    (8527 + 9601)
REM  - Cho SQL Server san sang truoc khi start.
REM  Dung: double-click Run.bat   hoac   Run.bat --no-pause
REM ============================================================================

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Can quyen Administrator - dang yeu cau UAC...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "BASE=%~dp0"
if "%BASE:~-1%"=="\" set "BASE=%BASE:~0,-1%"
set "NO_PAUSE="
if /i "%~1"=="--no-pause" set "NO_PAUSE=1"

title Gunny12.5 - Services
echo ============================================================
echo   Gunny12.5 - START SERVICES
echo   Thu muc: %BASE%
echo ============================================================
echo.

echo [1/5] Dung service cu...
taskkill /F /IM Road.Service.exe /T     >nul 2>&1
taskkill /F /IM Fighting.Service.exe /T >nul 2>&1
taskkill /F /IM Center.Service.exe /T   >nul 2>&1
ping -n 5 127.0.0.1 >nul

echo [2/5] Cho SQL Server san sang...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ok=$false; for($i=0;$i -lt 120;$i++){ try { $c=New-Object System.Data.SqlClient.SqlConnection 'Data Source=DESKTOP-SMLR4VU;Initial Catalog=Db_Tank_All;User ID=sa;Password=123456;Connect Timeout=2'; $c.Open(); $c.Close(); $ok=$true; break } catch { Start-Sleep -Seconds 1 } }; if(-not $ok){ exit 1 }"
if errorlevel 1 (
    echo [LOI] SQL Server chua san sang. Kiem tra SQL service roi chay lai.
    goto :fail
)
echo       SQL OK.

echo [3/5] Start service theo thu tu Center - Fight - Road
call :StartWait Center   Center.Service.exe   1593 0    150 || goto :fail
call :StartWait Fight    Fighting.Service.exe 7539 0    180 || goto :fail
call :StartWait Road     Road.Service.exe     8527 9601 210 || goto :fail

echo.
echo [4/5] Kiem tra port...
for %%P in (1593 7539 8527 9601) do (
    netstat -ano | findstr /R /C:":%%P .*LISTENING" >nul && echo       %%P : LISTEN || echo       %%P : CLOSED
)

echo.
echo [5/5] XONG - Server da chay.
if defined NO_PAUSE exit /b 0
echo Bam phim bat ky de dong cua so...
pause >nul
exit /b 0

:StartWait
set "SVC=%~1"
set "EXE=%~2"
set "P1=%~3"
set "P2=%~4"
set "T=%~5"
if not exist "%BASE%\%SVC%\%EXE%" (
    echo [LOI] Khong thay %BASE%\%SVC%\%EXE%
    exit /b 1
)
echo       Start %SVC% ...
start "" /D "%BASE%\%SVC%" "%BASE%\%SVC%\%EXE%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p1=%P1%; $p2=%P2%; $t=%T%; $ok=$false; for($i=0; $i -lt $t; $i++){ $a=Get-NetTCPConnection -LocalPort $p1 -State Listen -ErrorAction SilentlyContinue; if($p2 -gt 0){ $b=Get-NetTCPConnection -LocalPort $p2 -State Listen -ErrorAction SilentlyContinue } else { $b=$true }; if($a -and $b){ $ok=$true; break }; Start-Sleep -Seconds 1 }; if(-not $ok){ exit 1 }"
if errorlevel 1 (
    echo       [LOI] %SVC% khong listen port %P1% %P2%
    exit /b 1
)
echo       OK - %SVC% ready (port %P1% %P2%)
ping -n 4 127.0.0.1 >nul
exit /b 0

:fail
echo.
echo ============================================================
echo  [LOI] Service khong khoi dong duoc.
echo  Kiem tra thu muc logs cua tung service va file .config.
echo ============================================================
if defined NO_PAUSE exit /b 1
pause >nul
exit /b 1
