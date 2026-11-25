@echo off
setlocal EnableExtensions
title Deploy GIAM ST BAO KICH - Road + Fight

rem ============================================================================
rem  Deploy build co "% giam sat thuong bi bao kich" (thuong bo trang bi)
rem  cho CA Road.Service VA Fighting.Service (packet player info thay doi,
rem  phai cap nhat dong thoi 2 service). CAN QUYEN ADMINISTRATOR.
rem ============================================================================

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Can quyen Administrator - dang yeu cau UAC...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
set "ROAD=%ROOT%\Service\Service\Road"
set "FIGHT=%ROOT%\Service\Service\Fight"
set "SRC=%ROOT%\Full Source"
for /f %%I in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set "STAMP=%%I"

echo ============================================================
echo  Deploy CRIT REDUCE  ^|  backup = .bak-crit-%STAMP%
echo ============================================================
echo.

echo [1/6] Stop Road + Fighting...
taskkill /F /IM Road.Service.exe /T >nul 2>&1
taskkill /F /IM Fighting.Service.exe /T >nul 2>&1
for /L %%i in (1,1,30) do (
    netstat -ano | findstr /R /C:":8527 .*LISTENING" >nul || goto :stopped
    ping -n 2 127.0.0.1 >nul
)
:stopped
echo       done.

echo.
echo [2/6] Copy DLL -> Road...
call :cp "%SRC%\game.server\bin\Release\net472\Game.Server.dll"        "%ROAD%\Game.Server.dll"
call :cp "%SRC%\Bussiness\bin\Release\net472\Bussiness.dll"            "%ROAD%\Bussiness.dll"
call :cp "%SRC%\SqlDataProvider\bin\Release\net472\SqlDataProvider.dll" "%ROAD%\SqlDataProvider.dll"
call :cp "%SRC%\Game.Logic\bin\Release\net472\Game.Logic.dll"          "%ROAD%\Game.Logic.dll"
call :cp "%SRC%\game.base\bin\Release\net472\Game.Base.dll"            "%ROAD%\Game.Base.dll"
call :cp "%SRC%\EntityDatabase\bin\Release\net472\EntityDatabase.dll"  "%ROAD%\EntityDatabase.dll"

echo.
echo [3/6] Copy DLL -> Fight...
call :cp "%SRC%\fighting.server\bin\Release\net472\Fighting.Server.dll" "%FIGHT%\Fighting.Server.dll"
call :cp "%SRC%\Game.Logic\bin\Release\net472\Game.Logic.dll"           "%FIGHT%\Game.Logic.dll"
call :cp "%SRC%\SqlDataProvider\bin\Release\net472\SqlDataProvider.dll" "%FIGHT%\SqlDataProvider.dll"
call :cp "%SRC%\Bussiness\bin\Release\net472\Bussiness.dll"             "%FIGHT%\Bussiness.dll"
call :cp "%SRC%\game.base\bin\Release\net472\Game.Base.dll"             "%FIGHT%\Game.Base.dll"
call :cp "%SRC%\EntityDatabase\bin\Release\net472\EntityDatabase.dll"   "%FIGHT%\EntityDatabase.dll"

echo.
echo [4/6] Start Fighting...
if not exist "%FIGHT%\Fighting.Service.exe" ( echo [LOI] Khong thay Fighting.Service.exe & goto :fail )
start "" /D "%FIGHT%" "%FIGHT%\Fighting.Service.exe"
call :waitport 7539 180 || goto :fail
echo       OK - Fight LISTEN 7539

echo.
echo [5/6] Start Road...
start "" /D "%ROAD%" "%ROAD%\Road.Service.exe"
call :waitport 8527 210 || goto :fail
call :waitport 9601 60 || goto :fail
echo       OK - Road LISTEN 8527 + 9601

echo.
echo [6/6] XONG.
echo ============================================================
echo  Backup DLL cu: %ROAD%\*.bak-crit-%STAMP%  va  %FIGHT%\*.bak-crit-%STAMP%
echo  Dang nhap lai game de kiem tra giam ST bao kich.
echo ============================================================
pause
exit /b 0

:cp
if not exist "%~1" ( echo       MISSING build: %~1 & exit /b 1 )
if exist "%~2" copy /Y "%~2" "%~2.bak-crit-%STAMP%" >nul
copy /Y "%~1" "%~2" >nul
if errorlevel 1 ( echo       FAIL  %~nx2 & exit /b 1 )
echo       OK    %~nx2
exit /b 0

:waitport
set "PORT=%~1"
set "TRIES=%~2"
for /L %%i in (1,1,%TRIES%) do (
    netstat -ano | findstr /R /C:":%PORT% .*LISTENING" >nul && exit /b 0
    ping -n 2 127.0.0.1 >nul
)
exit /b 1

:fail
echo.
echo [LOI] Service khong khoi dong duoc. Xem logs\ cua service tuong ung.
pause
exit /b 1
