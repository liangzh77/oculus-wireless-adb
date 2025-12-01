@echo off
echo ===== Quest 3 Wireless ADB Debug Info =====
echo.

echo 1. Checking installed packages...
adb shell pm list packages | grep -i quest3
echo.

echo 2. Getting actual package name...
adb shell pm list packages | grep -i wireless
echo.

echo 3. Checking main activity...
adb shell pm dump com.ChuJiao.quest3_wireless_adb | grep -A 5 "Activity"
echo.

echo 4. Attempting to start app...
adb shell am start -n com.ChuJiao.quest3_wireless_adb/com.unity3d.player.UnityPlayerActivity
echo.

echo 5. Checking for crashes (last 100 lines)...
adb logcat -d -s Unity AndroidRuntime -t 100
echo.

echo ===== Debug Info Complete =====
pause
