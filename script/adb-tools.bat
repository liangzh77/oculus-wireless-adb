@echo off
setlocal enabledelayedexpansion

:menu
cls
echo ========================================
echo        Quest Wireless ADB Tools
echo ========================================
echo.
echo  1. Scan devices
echo  2. Connect all devices
echo  3. Disconnect all devices
echo  4. Connect one device
echo  5. Disconnect one device
echo  6. Grant WRITE_SECURE_SETTINGS
echo  7. Revoke WRITE_SECURE_SETTINGS
echo  8. List all devices
echo  0. Exit
echo.
echo ========================================
set /p choice=Select (0-8):

if "%choice%"=="1" goto scan
if "%choice%"=="2" goto connect_all
if "%choice%"=="3" goto disconnect_all
if "%choice%"=="4" goto connect_one
if "%choice%"=="5" goto disconnect_one
if "%choice%"=="6" goto grant_permission
if "%choice%"=="7" goto revoke_permission
if "%choice%"=="8" goto list_devices
if "%choice%"=="0" goto end
goto menu

:scan
echo.
python "%~dp0discover-and-connect.py" scan
echo.
pause
goto menu

:connect_all
echo.
python "%~dp0discover-and-connect.py" connect
echo.
pause
goto menu

:disconnect_all
echo.
echo Disconnecting all wireless devices...
adb disconnect
echo Done.
echo.
pause
goto menu

:list_devices
echo.
python "%~dp0discover-and-connect.py" list
echo.
pause
goto menu

:connect_one
echo.
echo Saved devices (from scan):
echo ----------------------------------------
python "%~dp0discover-and-connect.py" list
echo.
set /p dev_input=Enter device number or address (0=cancel, a=all):
if "%dev_input%"=="0" goto menu
if "%dev_input%"=="" goto menu
if /i "%dev_input%"=="a" (
    python "%~dp0discover-and-connect.py" connect
    echo.
    pause
    goto menu
)
echo.
python "%~dp0discover-and-connect.py" connect %dev_input%
echo.
pause
goto menu

:disconnect_one
call :load_adb_devices
if %device_count%==0 (
    echo No devices connected.
    echo.
    pause
    goto menu
)
echo.
set /p dev_num=Enter device number to disconnect (0=cancel, a=all):
if "%dev_num%"=="0" goto menu
if "%dev_num%"=="" goto menu
if /i "%dev_num%"=="a" (
    echo Disconnecting all wireless devices...
    adb disconnect
    echo Done.
    echo.
    pause
    goto menu
)
set "target_device=!device_%dev_num%!"
if "%target_device%"=="" (
    echo Invalid number.
    pause
    goto menu
)
echo Disconnecting %target_device%...
adb disconnect %target_device%
echo.
pause
goto menu

:grant_permission
call :load_adb_devices
if %device_count%==0 (
    echo No devices connected.
    echo.
    pause
    goto menu
)
echo.
set /p dev_num=Enter device number to grant (0=cancel, a=all):
if "%dev_num%"=="0" goto menu
if "%dev_num%"=="" goto menu
if /i "%dev_num%"=="a" (
    echo Granting permission to all devices...
    for /L %%i in (1,1,%device_count%) do (
        set "d=!device_%%i!"
        echo Granting to: !d!
        adb -s !d! shell pm grant com.ChuJiao.quest3_wireless_adb android.permission.WRITE_SECURE_SETTINGS
    )
    echo Done.
    echo.
    pause
    goto menu
)
set "target_device=!device_%dev_num%!"
if "%target_device%"=="" (
    echo Invalid number.
    pause
    goto menu
)
echo Granting permission to %target_device%...
adb -s %target_device% shell pm grant com.ChuJiao.quest3_wireless_adb android.permission.WRITE_SECURE_SETTINGS
echo Done.
echo.
pause
goto menu

:revoke_permission
call :load_adb_devices
if %device_count%==0 (
    echo No devices connected.
    echo.
    pause
    goto menu
)
echo.
set /p dev_num=Enter device number to revoke (0=cancel, a=all):
if "%dev_num%"=="0" goto menu
if "%dev_num%"=="" goto menu
if /i "%dev_num%"=="a" (
    echo Revoking permission from all devices...
    for /L %%i in (1,1,%device_count%) do (
        set "d=!device_%%i!"
        echo Revoking from: !d!
        adb -s !d! shell pm revoke com.ChuJiao.quest3_wireless_adb android.permission.WRITE_SECURE_SETTINGS
    )
    echo Done.
    echo.
    pause
    goto menu
)
set "target_device=!device_%dev_num%!"
if "%target_device%"=="" (
    echo Invalid number.
    pause
    goto menu
)
echo Revoking permission from %target_device%...
adb -s %target_device% shell pm revoke com.ChuJiao.quest3_wireless_adb android.permission.WRITE_SECURE_SETTINGS
echo Done.
echo.
pause
goto menu

:load_adb_devices
echo.
echo ADB connected devices:
echo ----------------------------------------
set device_count=0
for /f "skip=1 tokens=1" %%d in ('adb devices') do (
    if not "%%d"=="" (
        set /a device_count+=1
        set "device_!device_count!=%%d"
        echo  !device_count!. %%d
    )
)
echo ----------------------------------------
exit /b

:end
endlocal
exit /b 0
