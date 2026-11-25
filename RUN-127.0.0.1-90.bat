@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
set "APPDIR=%ROOT%Tools\GunnyPanel\app"
title BK Gunny12.5 - GunnyPanel 127.0.0.1:90
taskkill /F /IM GunnyPanel.exe /T >nul 2>&1
timeout /t 1 /nobreak >nul
if not exist "%APPDIR%\GunnyPanel.exe" (
  echo [LOI] Khong thay %APPDIR%\GunnyPanel.exe
  pause
  exit /b 1
)
echo Starting GunnyPanel at http://127.0.0.1:90 ...
start "" /D "%APPDIR%" "%APPDIR%\GunnyPanel.exe"
timeout /t 5 /nobreak >nul
start "" "http://127.0.0.1:90"
echo.
echo GunnyPanel da khoi dong. Bam phim bat ky de dong cua so.
pause >nul
exit /b 0