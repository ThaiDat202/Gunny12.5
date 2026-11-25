@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
set "BASE=%ROOT%Service\Service"
title Gunny12.5 - Services
net session >nul 2>&1
if errorlevel 1 (
  echo Dang yeu cau quyen Administrator...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
echo ============================================================
echo   BK Gunny12.5 - START SERVICES
echo ============================================================
echo [1/4] Dung service cu...
taskkill /F /IM Road.Service.exe /T >nul 2>&1
taskkill /F /IM Fighting.Service.exe /T >nul 2>&1
taskkill /F /IM Center.Service.exe /T >nul 2>&1
timeout /t 4 /nobreak >nul

echo [1/4] Cho SQL Server san sang...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Add-Type -AssemblyName System.Data; $ok=$false; for($i=0;$i -lt 180;$i++){ try { $c=New-Object System.Data.SqlClient.SqlConnection 'Data Source=DESKTOP-SMLR4VU;Initial Catalog=Db_Tank_All;User ID=sa;Password=123456;Connect Timeout=2'; $c.Open(); $c.Close(); $ok=$true; break } catch { Start-Sleep -Seconds 1 } }; if(-not $ok){ exit 1 }"
if errorlevel 1 (
  echo [LOI] SQL Server chua san sang. Kiem tra SQL service roi chay lai.
  pause >nul
  exit /b 1
)
rem --- deploy Road.Service.exe moi (neu co ban staging) ---
if exist "%BASE%\_deploy\Road.Service.exe" (
  if exist "%BASE%\Road\Road.Service.exe" copy /Y "%BASE%\Road\Road.Service.exe" "%BASE%\Road\Road.Service.exe.bak-last" >nul 2>&1
  copy /Y "%BASE%\_deploy\Road.Service.exe" "%BASE%\Road\Road.Service.exe" >nul
  echo [deploy] Da cap nhat Road.Service.exe tu _deploy
)
call :StartWait Center Center.Service.exe 1593 0 150
if errorlevel 1 goto fail
call :StartWait Fight Fighting.Service.exe 7539 0 180
if errorlevel 1 goto fail
call :StartWait Road Road.Service.exe 8527 9601 210
if errorlevel 1 goto fail

echo.
echo [OK] Center  : 1593
echo [OK] Fight   : 7539
echo [OK] Road    : 8527 + 9601
echo.
echo Server da chay. Bam phim bat ky de dong cua so.
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
echo [2/4] Start %SVC% ...
start "" /D "%BASE%\%SVC%" "%BASE%\%SVC%\%EXE%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p1=%P1%; $p2=%P2%; $t=%T%; $ok=$false; for($i=0; $i -lt $t; $i++){ $a=Get-NetTCPConnection -LocalPort $p1 -State Listen -ErrorAction SilentlyContinue; if($p2 -gt 0){ $b=Get-NetTCPConnection -LocalPort $p2 -State Listen -ErrorAction SilentlyContinue } else { $b=$true }; if($a -and $b){ $ok=$true; break }; Start-Sleep -Seconds 1 }; if(-not $ok){ exit 1 }"
if errorlevel 1 exit /b 1
echo       OK - %SVC% ready
timeout /t 3 /nobreak >nul
exit /b 0

:fail
echo.
echo [LOI] Service khong khoi dong duoc. Kiem tra file .config va thu muc logs.
echo Bam phim bat ky de dong.
pause >nul
exit /b 1