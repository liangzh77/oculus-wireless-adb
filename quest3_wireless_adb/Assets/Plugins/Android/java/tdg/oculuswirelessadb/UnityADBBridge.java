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
    private String adbPath;

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
        this.adbPath = context.getApplicationInfo().nativeLibraryDir + "/libadb.so";
        this.adbDiscovery = new JmDNSAdbDiscoveryJava(context);

        Log.d(TAG, "UnityADBBridge initialized with ADB path: " + adbPath);

        // 初始化时检查当前状态
        updateStatus();
    }

    /**
     * 启用无线ADB
     * @param useTcpipMode 是否使用tcpip 5555模式
     * @return 是否成功启动
     */
    public boolean enableWirelessADB(final boolean useTcpipMode) {
        Log.d(TAG, "Attempting to enable wireless ADB, tcpip mode: " + useTcpipMode);

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

                            // 如果需要tcpip模式
                            if (useTcpipMode) {
                                enableTcpipMode();
                            }
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
     * 启用tcpip 5555模式（需要先连接到主ADB端口）
     */
    private void enableTcpipMode() {
        try {
            // 检查是否已经有5555连接
            if (checkAdbConnection("127.0.0.1", 5555)) {
                statusMessage = currentIP + ":" + currentPort + "\n127.0.0.1:5555";
                Log.d(TAG, "tcpip mode already active");
                return;
            }

            // 检查主ADB连接
            if (!checkAdbConnection(currentIP, currentPort)) {
                statusMessage = "无法连接到ADB，请确保已授权";
                Log.w(TAG, "Cannot connect to main ADB port");
                return;
            }

            // 执行tcpip 5555命令
            String output = runCommand(adbPath + " -s " + currentIP + ":" + currentPort + " tcpip 5555");

            if (output != null && output.contains("restarting")) {
                // 等待ADB服务重启
                Thread.sleep(2000);

                // 检查tcpip模式是否成功
                if (checkAdbConnection("127.0.0.1", 5555)) {
                    statusMessage = currentIP + ":" + currentPort + "\n127.0.0.1:5555";
                    Log.d(TAG, "tcpip mode enabled successfully");
                } else {
                    statusMessage = "tcpip模式启动失败";
                    Log.w(TAG, "Failed to verify tcpip mode");
                }
            } else {
                statusMessage = "tcpip命令执行失败";
                Log.w(TAG, "Unexpected output from tcpip command: " + output);
            }

        } catch (Exception e) {
            Log.e(TAG, "Error enabling tcpip mode", e);
        }
    }

    /**
     * 检查ADB连接
     */
    private boolean checkAdbConnection(String host, int port) {
        String output = runCommand(adbPath + " connect " + host + ":" + port);
        return output != null && output.contains("connected");
    }

    /**
     * 执行命令
     */
    private String runCommand(String command) {
        Log.d(TAG, "Running command: " + command);
        try {
            String[] parts = command.split("\\s+");
            ProcessBuilder procBuilder = new ProcessBuilder(parts)
                .directory(context.getFilesDir())
                .redirectErrorStream(true);

            procBuilder.environment().put("HOME", context.getFilesDir().getPath());
            procBuilder.environment().put("TMPDIR", context.getCacheDir().getPath());

            Process process = procBuilder.start();

            // 读取输出
            java.io.BufferedReader reader = new java.io.BufferedReader(
                new java.io.InputStreamReader(process.getInputStream())
            );

            StringBuilder output = new StringBuilder();
            String line;
            while ((line = reader.readLine()) != null) {
                output.append(line).append("\n");
            }

            reader.close();
            process.waitFor();

            String result = output.toString();
            Log.d(TAG, "Command output: " + result);
            return result;

        } catch (Exception e) {
            Log.e(TAG, "Error running command", e);
            return null;
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
     * 获取当前ADB是否启用
     */
    public boolean isADBEnabled() {
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
     */
    public boolean hasRequiredPermissions() {
        return context.checkSelfPermission(Manifest.permission.WRITE_SECURE_SETTINGS)
            == PackageManager.PERMISSION_GRANTED;
    }

    /**
     * 获取授权命令（用于提示用户）
     */
    public String getPermissionCommand() {
        return "adb shell pm grant " + context.getPackageName() + " android.permission.WRITE_SECURE_SETTINGS";
    }
}
