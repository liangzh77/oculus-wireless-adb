package tdg.oculuswirelessadb;

import android.content.Context;
import android.content.pm.PackageManager;
import android.provider.Settings;
import android.util.Log;
import android.util.Pair;
import android.Manifest;

/**
 * Unity ADB Bridge - 为Unity提供ADB无线调试功能的桥接类
 *
 * 这个类封装了Android无线ADB调试的核心功能，可以被Unity通过AndroidJavaObject调用
 */
public class UnityADBBridge {

    private static final String TAG = "UnityADBBridge";

    private Context context;
    private JmDNSAdbDiscoveryJava adbDiscovery;

    // 状态信息
    private String currentIP = "";
    private int currentPort = 0;
    private String statusMessage = "未初始化";
    private boolean isEnabled = false;

    /**
     * 构造函数 - Unity会调用这个构造函数
     * @param context Android应用上下文
     */
    public UnityADBBridge(Context context) {
        this.context = context;
        this.adbDiscovery = new JmDNSAdbDiscoveryJava(context);

        Log.d(TAG, "UnityADBBridge initialized");

        // 初始化时检查当前状态
        updateStatus();
    }

    /**
     * 启用无线ADB
     * @return 是否成功启动
     */
    public boolean enableWirelessADB() {
        Log.d(TAG, "Attempting to enable wireless ADB");

        try {
            // 启用ADB WiFi
            Settings.Global.putInt(
                context.getContentResolver(),
                "adb_wifi_enabled",
                1
            );

            isEnabled = true;
            statusMessage = "正在启动...";

            // 异步发现ADB服务
            new Thread(new Runnable() {
                @Override
                public void run() {
                    try {
                        Thread.sleep(1000); // 等待ADB服务启动

                        Pair<String, Integer> result = adbDiscovery.discoverLocalAdbService();

                        if (result != null) {
                            currentIP = result.first;
                            currentPort = result.second;
                            statusMessage = "ADB已启动: " + currentIP + ":" + currentPort;

                            Log.d(TAG, "ADB discovered at " + currentIP + ":" + currentPort);
                        } else {
                            statusMessage = "无法发现ADB服务";
                            Log.w(TAG, "Failed to discover ADB service");
                        }
                    } catch (Exception e) {
                        statusMessage = "启动失败: " + e.getMessage();
                        Log.e(TAG, "Error enabling wireless ADB", e);
                    }
                }
            }).start();

            return true;

        } catch (Exception e) {
            Log.e(TAG, "Failed to enable wireless ADB", e);
            statusMessage = "启动失败: " + e.getMessage();
            isEnabled = false;
            return false;
        }
    }

    /**
     * 禁用无线ADB
     * @return 是否成功禁用
     */
    public boolean disableWirelessADB() {
        Log.d(TAG, "Disabling wireless ADB");

        try {
            Settings.Global.putInt(
                context.getContentResolver(),
                "adb_wifi_enabled",
                0
            );

            isEnabled = false;
            currentIP = "";
            currentPort = 0;
            statusMessage = "ADB已禁用";

            return true;

        } catch (Exception e) {
            Log.e(TAG, "Failed to disable wireless ADB", e);
            statusMessage = "禁用失败: " + e.getMessage();
            return false;
        }
    }

    /**
     * 更新当前状态
     */
    public void updateStatus() {
        try {
            int adbWifiEnabled = Settings.Global.getInt(
                context.getContentResolver(),
                "adb_wifi_enabled"
            );

            if (adbWifiEnabled == 1) {
                isEnabled = true;

                // 异步更新IP和端口
                new Thread(new Runnable() {
                    @Override
                    public void run() {
                        Pair<String, Integer> result = adbDiscovery.discoverLocalAdbService();
                        if (result != null) {
                            currentIP = result.first;
                            currentPort = result.second;
                            statusMessage = "ADB已启动: " + currentIP + ":" + currentPort;
                        }
                    }
                }).start();
            } else {
                isEnabled = false;
                currentIP = "";
                currentPort = 0;
                statusMessage = "ADB已禁用";
            }

        } catch (Exception e) {
            Log.e(TAG, "Error updating status", e);
            statusMessage = "状态更新失败";
        }
    }

    // ========== Unity调用的接口方法 ==========

    /**
     * 获取当前ADB是否启用（直接从系统设置读取）
     */
    public boolean isADBEnabled() {
        try {
            int adbWifiEnabled = Settings.Global.getInt(
                context.getContentResolver(),
                "adb_wifi_enabled",
                0
            );
            isEnabled = (adbWifiEnabled == 1);
        } catch (Exception e) {
            Log.e(TAG, "Error reading adb_wifi_enabled: " + e.getMessage());
        }
        return isEnabled;
    }

    /**
     * 获取当前IP地址
     */
    public String getIPAddress() {
        return currentIP;
    }

    /**
     * 获取当前ADB端口
     */
    public int getADBPort() {
        return currentPort;
    }

    /**
     * 获取状态消息
     */
    public String getStatusMessage() {
        return statusMessage;
    }

    /**
     * 检查是否有所需权限
     * 通过尝试读取设置来验证权限，比checkSelfPermission更可靠
     */
    public boolean hasRequiredPermissions() {
        try {
            // 尝试读取当前值
            int currentValue = Settings.Global.getInt(
                context.getContentResolver(),
                "adb_wifi_enabled",
                0
            );

            // 尝试写入相同的值（不改变实际状态）
            Settings.Global.putInt(
                context.getContentResolver(),
                "adb_wifi_enabled",
                currentValue
            );

            return true;
        } catch (SecurityException e) {
            Log.d(TAG, "No WRITE_SECURE_SETTINGS permission: " + e.getMessage());
            return false;
        } catch (Exception e) {
            Log.e(TAG, "Error checking permissions: " + e.getMessage());
            return false;
        }
    }

    /**
     * 获取授权命令（用于提示用户）
     */
    public String getPermissionCommand() {
        return "adb shell pm grant " + context.getPackageName() + " android.permission.WRITE_SECURE_SETTINGS";
    }
}
