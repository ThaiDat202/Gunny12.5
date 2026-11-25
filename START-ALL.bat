@echo off
setlocal EnableExtensions
title Gunny12.5 - START ALL (IIS + 3 service + panel)

rem ============================================================================
rem  Start toan bo server Gunny12.5:
rem    1. Bao dam junction C:\Gunny\BK_Gunny12.5 -> Gunny12.5 (cho IIS)
rem    2. Start Center -> Fight -> Road
rem    3. Start panel GM + mo http://127.0.0.1:90
rem  CAN QUYEN ADMINISTRATOR (tu xin UAC).
rem ============================================================================

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Can quyen Administrator - dang yeu cau UAC...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

echo ============================================================
echo   Gunny12.5 - START ALL
echo   %ROOT%
echo ============================================================
echo.

echo [1/3] Kiem tra junction cho IIS...
if not exist "C:\Gunny\BK_Gunny12.5\Website\Index.html" (
    if exist "C:\Gunny\BK_Gunny12.5" rmdir "C:\Gunny\BK_Gunny12.5" >nul 2>&1
    cmd /c mklink /J "C:\Gunny\BK_Gunny12.5" "%ROOT%" >nul
    if exist "C:\Gunny\BK_Gunny12.5\Website\Index.html" (
        echo       da tao junction BK_Gunny12.5 -^> Gunny12.5
    ) else (
        echo       [CANH BAO] Khong tao duoc junction. Chay FIX-IIS-Gunny12.5.bat de tro IIS thang vao Gunny12.5.
    )
) else (
    echo       OK
)

echo.
echo [2/3] Start 3 service (Center - Fight - Road)...
call "%ROOT%\Service\Service\Run.bat" --no-pause
if errorlevel 1 (
    echo.
    echo [LOI] Service khong start duoc. Xem logs trong Service\Service\*\logs
    pause
    exit /b 1
)

echo.
echo [3/3] Start panel GM...
if exist "%ROOT%\Tools\GunnyPanel\app\GunnyPanel.exe" (
    taskkill /F /IM GunnyPanel.exe /T >nul 2>&1
    ping -n 3 127.0.0.1 >nul
    start "" /D "%ROOT%\Tools\GunnyPanel\app" "%ROOT%\Tools\GunnyPanel\app\GunnyPanel.exe"
    ping -n 6 127.0.0.1 >nul
    start "" "http://127.0.0.1:90"
    echo       panel: http://127.0.0.1:90
) else (
    echo       [CANH BAO] Khong thay GunnyPanel.exe
)

echo.
echo ============================================================
echo  KIEM TRA:
echo    Website  : http://127.0.0.1
echo    Request  : http://127.0.0.1:88
echo    Panel GM : http://127.0.0.1:90
echo    Port     : 1593 (Center) / 7539 (Fight) / 8527+9601 (Road)
echo ============================================================
echo Bam phim bat ky de dong cua so...
pause >nul
exit /b 0
