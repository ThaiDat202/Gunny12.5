@echo off
setlocal EnableExtensions
title Deploy SUIT BONUS (Cap Nhan / Cap Vong Tay) - Road.Service

rem ============================================================================
rem  Copy DLL build moi (co tinh nang thuong bo trang bi) vao Road.Service
rem  va restart Road. CAN QUYEN ADMINISTRATOR (tu xin UAC).
rem  Chi restart Road - Center/Fight khong can dung toi.
rem ============================================================================

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Can quyen Administrator - dang yeu cau UAC...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
set "SVC=%ROOT%\Service\Service\Road"
set "SRC=%ROOT%\Full Source"
for /f %%I in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set "STAMP=%%I"

echo ============================================================
echo  Deploy SUIT BONUS  ^|  backup = .bak-suit-%STAMP%
echo ============================================================
echo.

echo [1/4] Stop Road.Service...
taskkill /F /IM Road.Service.exe /T >nul 2>&1
for /L %%i in (1,1,30) do (
    netstat -ano | findstr /R /C:":8527 .*LISTENING" >nul || goto :stopped
    ping -n 2 127.0.0.1 >nul
)
:stopped
echo       done.

echo.
echo [2/4] Backup + copy DLL...
call :cp "%SRC%\game.server\bin\Release\net472\Game.Server.dll"        "%SVC%\Game.Server.dll"
call :cp "%SRC%\Bussiness\bin\Release\net472\Bussiness.dll"            "%SVC%\Bussiness.dll"
call :cp "%SRC%\SqlDataProvider\bin\Release\net472\SqlDataProvider.dll" "%SVC%\SqlDataProvider.dll"
call :cp "%SRC%\game.base\bin\Release\net472\Game.Base.dll"            "%SVC%\Game.Base.dll"
call :cp "%SRC%\Game.Logic\bin\Release\net472\Game.Logic.dll"          "%SVC%\Game.Logic.dll"
call :cp "%SRC%\EntityDatabase\bin\Release\net472\EntityDatabase.dll"  "%SVC%\EntityDatabase.dll"

echo.
echo [3/4] Start Road.Service...
start "" /D "%SVC%" "%SVC%\Road.Service.exe"
set "OK="
for /L %%i in (1,1,240) do (
    netstat -ano | findstr /R /C:":8527 .*LISTENING" >nul && (
        netstat -ano | findstr /R /C:":9601 .*LISTENING" >nul && ( set "OK=1" & goto :up )
    )
    ping -n 2 127.0.0.1 >nul
)
:up
if defined OK (
    echo       OK - Road dang LISTEN 8527 + 9601
) else (
    echo       [LOI] Road khong len sau 240s - xem %SVC%\logs
)

echo.
echo [4/4] Kiem tra log moi...
if exist "%SVC%\logs\GameServer.log" (
    powershell -NoProfile -Command "Get-Content '%SVC%\logs\GameServer.log' -Tail 5 | ForEach-Object { $_.Trim() }"
)

echo.
echo ============================================================
echo  XONG. Backup DLL cu: %SVC%\*.bak-suit-%STAMP%
echo  Dang nhap lai game de server tinh lai chi so + thuong bo.
echo ============================================================
pause
exit /b 0

:cp
if not exist "%~1" (
    echo       MISSING build: %~1
    exit /b 1
)
if exist "%~2" copy /Y "%~2" "%~2.bak-suit-%STAMP%" >nul
copy /Y "%~1" "%~2" >nul
if errorlevel 1 (
    echo       FAIL  %~nx2  ^(file dang bi khoa? Stop Road roi chay lai^)
    exit /b 1
)
echo       OK    %~nx2
exit /b 0
