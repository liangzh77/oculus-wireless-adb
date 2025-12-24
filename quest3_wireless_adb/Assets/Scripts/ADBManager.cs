using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Android;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 远程应用信息结构（从API获取）
/// </summary>
[Serializable]
public class RemoteAppInfo
{
    public string app_id;
    public string app_code;
    public string app_name;
    public string latest_version;
    public string apk_url;
    public int file_size;
    public string updated_at;
}

/// <summary>
/// 远程应用信息列表包装类（用于JSON反序列化）
/// </summary>
[Serializable]
public class RemoteAppInfoList
{
    public List<RemoteAppInfo> items;
}

/// <summary>
/// 应用信息结构（包含本机和远程版本）
/// </summary>
[Serializable]
public class AppInfo
{
    public string packageName;
    public string appName;
    public string versionName;      // 本机版本
    public int versionCode;
    public string remoteVersion;    // 远程最新版本
    public string remoteAppId;      // 远程应用ID
    public string apkUrl;           // APK下载地址
    public bool isMatched;          // 是否匹配远程应用

    public AppInfo(string packageName, string appName, string versionName, int versionCode)
    {
        this.packageName = packageName;
        this.appName = appName;
        this.versionName = versionName;
        this.versionCode = versionCode;
        this.remoteVersion = "";
        this.remoteAppId = "";
        this.apkUrl = "";
        this.isMatched = false;
    }
}

/// <summary>
/// ADB管理器 - Unity与Android ADB桥接的核心管理类
///
/// 使用方法：
/// 1. 将此脚本附加到场景中的GameObject上
/// 2. 通过单例访问：ADBManager.Instance
/// 3. 调用EnableADB()和DisableADB()控制无线ADB
/// </summary>
public class ADBManager : MonoBehaviour
{
    // 单例实例
    private static ADBManager instance;
    public static ADBManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("ADBManager");
                instance = go.AddComponent<ADBManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    // Android Java对象
    private AndroidJavaObject unityADBBridge;
    private AndroidJavaObject unityActivity;

    // 状态变量
    private bool isInitialized = false;
    private bool isADBEnabled = false;
    private string currentIP = "";
    private int currentPort = 0;
    private string statusMessage = "未初始化";

    // 应用列表
    private List<AppInfo> installedApps = new List<AppInfo>();
    private List<RemoteAppInfo> remoteApps = new List<RemoteAppInfo>();
    private bool isLoadingApps = false;

    // 远程API配置
    private const string REMOTE_API_URL = "https://mrgun.chu-jiao.com/api/v1/admins/applications/versions/all";

    // 状态更新事件
    public event Action<bool> OnADBStatusChanged;
    public event Action<string, int> OnConnectionInfoUpdated;
    public event Action<string> OnStatusMessageUpdated;
    public event Action<List<AppInfo>> OnAppListUpdated;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeADBBridge();
        InitializeCommandReceiver();
    }

    /// <summary>
    /// 初始化 ADB 命令接收器
    /// </summary>
    private void InitializeCommandReceiver()
    {
        // 创建 ADBBroadcastListener GameObject（始终激活，处理广播）
        GameObject listenerObj = GameObject.Find("ADBBroadcastListener");
        if (listenerObj == null)
        {
            listenerObj = new GameObject("ADBBroadcastListener");
            listenerObj.AddComponent<ADBBroadcastListener>();
            DontDestroyOnLoad(listenerObj);
            Debug.Log("[ADBManager] ADBBroadcastListener created");
        }

        // 创建 ADBCommandReceiver GameObject（处理声音和震动逻辑）
        GameObject receiverObj = GameObject.Find("ADBCommandReceiver");
        if (receiverObj == null)
        {
            receiverObj = new GameObject("ADBCommandReceiver");
            receiverObj.AddComponent<ADBCommandReceiver>();
            DontDestroyOnLoad(receiverObj);
            Debug.Log("[ADBManager] ADBCommandReceiver created");
        }
    }

    /// <summary>
    /// 初始化ADB桥接
    /// </summary>
    private void InitializeADBBridge()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // 获取Unity Activity
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            // 创建UnityADBBridge实例
            unityADBBridge = new AndroidJavaObject("tdg.oculuswirelessadb.UnityADBBridge", unityActivity);

            isInitialized = true;
            Debug.Log("[ADBManager] UnityADBBridge initialized successfully");

            // 更新初始状态
            UpdateStatus();
        }
        catch (Exception e)
        {
            Debug.LogError("[ADBManager] Failed to initialize UnityADBBridge: " + e.Message);
            statusMessage = "初始化失败: " + e.Message;
            isInitialized = false;
        }
#else
        Debug.LogWarning("[ADBManager] Running in Editor - ADB features disabled");
        statusMessage = "编辑器模式 - ADB功能禁用";
#endif
    }

    /// <summary>
    /// 启用无线ADB
    /// </summary>
    public void EnableADB()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized)
        {
            Debug.LogError("[ADBManager] Bridge not initialized!");
            return;
        }

        if (!HasRequiredPermissions())
        {
            Debug.LogWarning("[ADBManager] Missing WRITE_SECURE_SETTINGS permission");
            statusMessage = "缺少权限！请在电脑端执行:\nadb shell pm grant\ncom.ChuJiao.quest3_wireless_adb\nandroid.permission.WRITE_SECURE_SETTINGS";
            OnStatusMessageUpdated?.Invoke(statusMessage);
            OnADBStatusChanged?.Invoke(false);
            return;
        }

        try
        {
            bool success = unityADBBridge.Call<bool>("enableWirelessADB");

            if (success)
            {
                Debug.Log("[ADBManager] Wireless ADB enabled");

                // 延迟更新状态，等待ADB服务完全启动
                Invoke("UpdateStatus", 2f);
            }
            else
            {
                Debug.LogError("[ADBManager] Failed to enable wireless ADB");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[ADBManager] Error enabling ADB: " + e.Message);
            statusMessage = "启动失败: " + e.Message;
            OnStatusMessageUpdated?.Invoke(statusMessage);
        }
#else
        Debug.Log("[ADBManager] EnableADB called in Editor mode");
#endif
    }

    /// <summary>
    /// 禁用无线ADB
    /// </summary>
    public void DisableADB()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized)
        {
            Debug.LogError("[ADBManager] Bridge not initialized!");
            return;
        }

        try
        {
            bool success = unityADBBridge.Call<bool>("disableWirelessADB");

            if (success)
            {
                Debug.Log("[ADBManager] Wireless ADB disabled");
                UpdateStatus();
            }
            else
            {
                Debug.LogError("[ADBManager] Failed to disable wireless ADB");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[ADBManager] Error disabling ADB: " + e.Message);
            statusMessage = "禁用失败: " + e.Message;
            OnStatusMessageUpdated?.Invoke(statusMessage);
        }
#else
        Debug.Log("[ADBManager] DisableADB called in Editor mode");
#endif
    }

    /// <summary>
    /// 更新状态信息
    /// </summary>
    public void UpdateStatus()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized) return;

        try
        {
            // 调用Java端更新状态
            unityADBBridge.Call("updateStatus");

            // 延迟读取状态，给Java端时间更新
            Invoke("ReadStatus", 0.5f);
        }
        catch (Exception e)
        {
            Debug.LogError("[ADBManager] Error updating status: " + e.Message);
        }
#endif
    }

    /// <summary>
    /// 读取状态信息
    /// </summary>
    private void ReadStatus()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized) return;

        try
        {
            bool wasEnabled = isADBEnabled;
            string oldIP = currentIP;
            int oldPort = currentPort;

            isADBEnabled = unityADBBridge.Call<bool>("isADBEnabled");
            currentIP = unityADBBridge.Call<string>("getIPAddress");
            currentPort = unityADBBridge.Call<int>("getADBPort");
            statusMessage = unityADBBridge.Call<string>("getStatusMessage");

            // 触发状态变化事件
            if (wasEnabled != isADBEnabled)
            {
                OnADBStatusChanged?.Invoke(isADBEnabled);
            }

            if (oldIP != currentIP || oldPort != currentPort)
            {
                OnConnectionInfoUpdated?.Invoke(currentIP, currentPort);
            }

            OnStatusMessageUpdated?.Invoke(statusMessage);

            Debug.Log($"[ADBManager] Status updated - Enabled: {isADBEnabled}, IP: {currentIP}, Port: {currentPort}");
        }
        catch (Exception e)
        {
            Debug.LogError("[ADBManager] Error reading status: " + e.Message);
        }
#endif
    }

    /// <summary>
    /// 检查是否有所需权限
    /// </summary>
    public bool HasRequiredPermissions()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized) return false;

        try
        {
            return unityADBBridge.Call<bool>("hasRequiredPermissions");
        }
        catch (Exception e)
        {
            Debug.LogError("[ADBManager] Error checking permissions: " + e.Message);
            return false;
        }
#else
        return true; // Editor模式下假装有权限
#endif
    }

    /// <summary>
    /// 获取授权命令
    /// </summary>
    public string GetPermissionCommand()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized) return "";

        try
        {
            return unityADBBridge.Call<string>("getPermissionCommand");
        }
        catch (Exception e)
        {
            Debug.LogError("[ADBManager] Error getting permission command: " + e.Message);
            return "";
        }
#else
        return "adb shell pm grant <package_name> android.permission.WRITE_SECURE_SETTINGS";
#endif
    }

    // ========== 公共属性访问器 ==========

    public bool IsInitialized => isInitialized;
    public bool IsADBEnabled => isADBEnabled;
    public string CurrentIP => currentIP;
    public int CurrentPort => currentPort;
    public string StatusMessage => statusMessage;
    public List<AppInfo> InstalledApps => installedApps;
    public bool IsLoadingApps => isLoadingApps;

    /// <summary>
    /// 定期更新状态（可选）
    /// </summary>
    void Start()
    {
        // 每5秒自动更新一次状态
        InvokeRepeating("UpdateStatus", 1f, 5f);

        // 应用启动时自动启用ADB
        Invoke("AutoEnableADB", 2f);
    }

    /// <summary>
    /// 自动启用ADB（延迟调用，确保初始化完成）
    /// </summary>
    private void AutoEnableADB()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized || !HasRequiredPermissions())
        {
            Debug.Log("[ADBManager] Cannot auto-enable: not initialized or no permission");
            return;
        }

        // 直接从系统读取真实状态，不依赖缓存的 isADBEnabled
        bool isCurrentlyEnabled = false;
        try
        {
            isCurrentlyEnabled = unityADBBridge.Call<bool>("isADBEnabled");
            // 同时触发 Java 端更新状态
            unityADBBridge.Call("updateStatus");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ADBManager] Error checking ADB status: " + e.Message);
        }

        if (!isCurrentlyEnabled)
        {
            Debug.Log("[ADBManager] Auto-enabling ADB");
            EnableADB();
        }
        else
        {
            Debug.Log("[ADBManager] ADB already enabled");
            // 更新本地状态
            Invoke("ReadStatus", 0.5f);
        }
#endif
    }

    void OnDestroy()
    {
        CancelInvoke();
    }

    /// <summary>
    /// 应用从后台恢复时调用
    /// </summary>
    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            // 从后台恢复，延迟检查并启用 ADB
            Debug.Log("[ADBManager] App resumed from pause");
            Invoke("AutoEnableADB", 2f);
        }
    }

    /// <summary>
    /// 应用获得焦点时调用
    /// </summary>
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            // 获得焦点，延迟检查并启用 ADB
            Debug.Log("[ADBManager] App gained focus");
            Invoke("AutoEnableADB", 2f);
        }
    }

    // ========== 应用列表功能 ==========

    /// <summary>
    /// 获取已安装的第三方应用列表（异步）
    /// 先从远程API获取应用列表，再与本地应用匹配
    /// </summary>
    public void RefreshInstalledApps()
    {
        if (isLoadingApps)
        {
            Debug.Log("[ADBManager] Already loading apps...");
            return;
        }

        isLoadingApps = true;
        Debug.Log("[ADBManager] Starting to load apps (remote + local)...");

        // 启动协程获取远程应用列表
        StartCoroutine(FetchRemoteAppsAndMatch());
    }

    /// <summary>
    /// 从远程API获取应用列表并与本地匹配
    /// </summary>
    private IEnumerator FetchRemoteAppsAndMatch()
    {
        Debug.Log("[ADBManager] Fetching remote app list from API...");

        using (UnityWebRequest request = UnityWebRequest.Get(REMOTE_API_URL))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                Debug.Log($"[ADBManager] Remote API response: {json}");

                try
                {
                    // 解析JSON数组
                    remoteApps = ParseRemoteAppList(json);
                    Debug.Log($"[ADBManager] Parsed {remoteApps.Count} remote apps");

                    foreach (var app in remoteApps)
                    {
                        Debug.Log($"[ADBManager] Remote app: {app.app_name} v{app.latest_version}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ADBManager] Error parsing remote app list: {e.Message}");
                    remoteApps.Clear();
                }
            }
            else
            {
                Debug.LogError($"[ADBManager] Failed to fetch remote apps: {request.error}");
                remoteApps.Clear();
            }
        }

        // 获取本地应用列表并与远程匹配
        LoadAndMatchLocalApps();
    }

    /// <summary>
    /// 解析远程应用列表JSON
    /// </summary>
    private List<RemoteAppInfo> ParseRemoteAppList(string json)
    {
        var result = new List<RemoteAppInfo>();

        // JSON是数组格式，需要包装一下
        string wrappedJson = "{\"items\":" + json + "}";
        var wrapper = JsonUtility.FromJson<RemoteAppInfoList>(wrappedJson);

        if (wrapper != null && wrapper.items != null)
        {
            result = wrapper.items;
        }

        return result;
    }

    /// <summary>
    /// 加载本地应用并与远程列表匹配
    /// </summary>
    private void LoadAndMatchLocalApps()
    {
        installedApps.Clear();

        // 创建远程应用名称到信息的映射
        var remoteAppMap = new Dictionary<string, RemoteAppInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var remoteApp in remoteApps)
        {
            if (!string.IsNullOrEmpty(remoteApp.app_name))
            {
                remoteAppMap[remoteApp.app_name] = remoteApp;
            }
        }

        Debug.Log($"[ADBManager] Remote app map created with {remoteAppMap.Count} entries");

        // 分别存储匹配和不匹配的应用
        var matchedApps = new List<AppInfo>();
        var unmatchedApps = new List<AppInfo>();

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
            {
                // 获取所有已安装的包
                AndroidJavaObject packages = packageManager.Call<AndroidJavaObject>("getInstalledPackages", 0);
                int count = packages.Call<int>("size");

                Debug.Log($"[ADBManager] Found {count} total packages");

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        AndroidJavaObject packageInfo = packages.Call<AndroidJavaObject>("get", i);
                        string packageName = packageInfo.Get<string>("packageName");

                        // 排除核心系统应用
                        if (IsSystemPackage(packageName))
                        {
                            packageInfo.Dispose();
                            continue;
                        }

                        AndroidJavaObject appInfoObj = packageInfo.Get<AndroidJavaObject>("applicationInfo");

                        string versionName = "N/A";
                        try
                        {
                            versionName = packageInfo.Get<string>("versionName") ?? "N/A";
                        }
                        catch
                        {
                            Debug.LogWarning($"[ADBManager] Failed to get versionName for {packageName}");
                        }

                        int versionCode = 0;
                        try
                        {
                            versionCode = packageInfo.Get<int>("versionCode");
                        }
                        catch
                        {
                            Debug.LogWarning($"[ADBManager] Failed to get versionCode for {packageName}");
                        }

                        // 获取应用名称
                        string appName = packageName;
                        try
                        {
                            AndroidJavaObject labelObj = packageManager.Call<AndroidJavaObject>("getApplicationLabel", appInfoObj);
                            if (labelObj != null)
                            {
                                appName = labelObj.Call<string>("toString");
                            }
                        }
                        catch
                        {
                            Debug.LogWarning($"[ADBManager] Failed to get app label for {packageName}");
                        }

                        var localApp = new AppInfo(packageName, appName, versionName, versionCode);

                        // 检查是否匹配远程应用
                        if (remoteAppMap.TryGetValue(appName, out RemoteAppInfo remoteInfo))
                        {
                            localApp.remoteVersion = remoteInfo.latest_version;
                            localApp.remoteAppId = remoteInfo.app_id;
                            localApp.apkUrl = remoteInfo.apk_url;
                            localApp.isMatched = true;
                            matchedApps.Add(localApp);
                            Debug.Log($"[ADBManager] Matched app: {appName} - Local: {versionName}, Remote: {remoteInfo.latest_version}");
                        }
                        else
                        {
                            localApp.isMatched = false;
                            unmatchedApps.Add(localApp);
                            Debug.Log($"[ADBManager] Unmatched app: {appName} v{versionName}");
                        }

                        packageInfo.Dispose();
                        appInfoObj.Dispose();
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ADBManager] Error processing package at index {i}: {e.Message}");
                    }
                }

                packages.Dispose();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[ADBManager] Error loading installed apps: " + e.Message);
        }
#else
        // Editor 模式下添加一些测试数据
        if (remoteAppMap.ContainsKey("枪王之王"))
        {
            var testApp = new AppInfo("com.example.mrgun", "枪王之王", "0.7.5", 75);
            testApp.remoteVersion = remoteAppMap["枪王之王"].latest_version;
            testApp.remoteAppId = remoteAppMap["枪王之王"].app_id;
            testApp.apkUrl = remoteAppMap["枪王之王"].apk_url;
            testApp.isMatched = true;
            matchedApps.Add(testApp);
        }
        // 添加一些不匹配的测试应用
        var unmatchedTest = new AppInfo("com.example.other", "其他应用", "1.0.0", 1);
        unmatchedTest.isMatched = false;
        unmatchedApps.Add(unmatchedTest);
        Debug.Log("[ADBManager] Editor mode - added test apps");
#endif

        // 分别排序
        matchedApps.Sort((a, b) => string.Compare(a.appName, b.appName, StringComparison.OrdinalIgnoreCase));
        unmatchedApps.Sort((a, b) => string.Compare(a.appName, b.appName, StringComparison.OrdinalIgnoreCase));

        // 合并：匹配的在前，不匹配的在后
        installedApps.AddRange(matchedApps);
        installedApps.AddRange(unmatchedApps);

        Debug.Log($"[ADBManager] Loaded {matchedApps.Count} matched apps, {unmatchedApps.Count} unmatched apps");

        isLoadingApps = false;
        Debug.Log($"[ADBManager] Invoking OnAppListUpdated with {installedApps.Count} apps, listeners: {OnAppListUpdated?.GetInvocationList()?.Length ?? 0}");
        OnAppListUpdated?.Invoke(installedApps);
    }

    /// <summary>
    /// 获取已安装应用的数量
    /// </summary>
    public int GetInstalledAppCount()
    {
        return installedApps.Count;
    }

    /// <summary>
    /// 判断是否为系统核心应用（通过包名前缀）
    /// </summary>
    private bool IsSystemPackage(string packageName)
    {
        // 核心 Android 系统包名前缀
        string[] systemPrefixes = new string[]
        {
            "android",
            "com.android.",
            "com.google.",
            "com.qualcomm.",
            // Meta/Oculus/Facebook 系统组件
            "com.oculus.",
            "oculus.",
            "com.meta.",
            "com.facebook.",
            "horizonos.",
            "horizon."
        };

        foreach (var prefix in systemPrefixes)
        {
            if (packageName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // 特殊处理：packageName 完全等于 "android"
        if (packageName == "android")
        {
            return true;
        }

        return false;
    }

    // ========== APK下载和安装功能 ==========

    // 下载状态
    private bool isDownloading = false;
    private string currentDownloadingApp = "";
    private float downloadProgress = 0f;

    // 下载和安装事件
    public event Action<string, float> OnDownloadProgress;      // appName, progress (0-1)
    public event Action<string, bool, string> OnInstallComplete; // appName, success, message

    public bool IsDownloading => isDownloading;
    public string CurrentDownloadingApp => currentDownloadingApp;
    public float DownloadProgress => downloadProgress;

    /// <summary>
    /// 检查并请求存储权限
    /// </summary>
    private bool CheckAndRequestStoragePermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // 检查是否有外部存储写入权限
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
        {
            Debug.Log("[ADBManager] Requesting external storage write permission...");
            Permission.RequestUserPermission(Permission.ExternalStorageWrite);
            return false;
        }
        return true;
#else
        return true;
#endif
    }

    /// <summary>
    /// 验证文件是否为有效的APK（ZIP格式）
    /// </summary>
    private bool ValidateApkFile(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath))
            {
                Debug.LogError($"[ADBManager] APK file does not exist: {filePath}");
                return false;
            }

            // APK是ZIP格式，文件头应该是 "PK" (0x50, 0x4B)
            using (var fs = System.IO.File.OpenRead(filePath))
            {
                if (fs.Length < 4)
                {
                    Debug.LogError($"[ADBManager] File too small: {fs.Length} bytes");
                    return false;
                }

                byte[] header = new byte[4];
                fs.Read(header, 0, 4);

                // 检查ZIP魔术字节: PK\x03\x04
                if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
                {
                    Debug.Log("[ADBManager] Valid APK file (ZIP format verified)");
                    return true;
                }

                Debug.LogError($"[ADBManager] Invalid file header: {header[0]:X2} {header[1]:X2} {header[2]:X2} {header[3]:X2}");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ADBManager] Error validating APK: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 刷新媒体库，让文件在文件管理器中可见
    /// </summary>
    private void ScanMediaFile(string filePath)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass mediaScanner = new AndroidJavaClass("android.media.MediaScannerConnection"))
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                string[] paths = new string[] { filePath };
                string[] mimeTypes = new string[] { "application/vnd.android.package-archive" };
                mediaScanner.CallStatic("scanFile", activity, paths, mimeTypes, null);
                Debug.Log($"[ADBManager] Media scan triggered for: {filePath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ADBManager] Media scan failed: {e.Message}");
        }
#endif
    }

    /// <summary>
    /// 下载并安装APK
    /// </summary>
    public void DownloadAndInstallAPK(AppInfo app)
    {
        if (isDownloading)
        {
            Debug.LogWarning($"[ADBManager] Already downloading: {currentDownloadingApp}");
            return;
        }

        if (string.IsNullOrEmpty(app.apkUrl))
        {
            Debug.LogError($"[ADBManager] No APK URL for app: {app.appName}");
            OnInstallComplete?.Invoke(app.appName, false, "没有下载地址");
            return;
        }

        // 尝试请求存储权限（非阻塞，即使没有权限也会使用备选目录）
        CheckAndRequestStoragePermission();

        StartCoroutine(DownloadAndInstallCoroutine(app));
    }

    /// <summary>
    /// 获取下载目录路径
    /// </summary>
    private string GetDownloadDirectory()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // 尝试使用公共 Download 目录
            string publicDownload = "/sdcard/Download";
            if (System.IO.Directory.Exists(publicDownload))
            {
                // 测试是否可写
                string testFile = publicDownload + "/.test_write";
                try
                {
                    System.IO.File.WriteAllText(testFile, "test");
                    System.IO.File.Delete(testFile);
                    Debug.Log($"[ADBManager] Using public Download directory: {publicDownload}");
                    return publicDownload;
                }
                catch
                {
                    Debug.Log("[ADBManager] Public Download directory not writable");
                }
            }

            // 备选：使用应用的外部文件目录
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject externalFilesDir = activity.Call<AndroidJavaObject>("getExternalFilesDir", (string)null))
            {
                if (externalFilesDir != null)
                {
                    string path = externalFilesDir.Call<string>("getAbsolutePath");
                    string downloadPath = path + "/Download";
                    System.IO.Directory.CreateDirectory(downloadPath);
                    Debug.Log($"[ADBManager] Using app external directory: {downloadPath}");
                    return downloadPath;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ADBManager] Error getting download directory: {e.Message}");
        }

        // 最后备选：使用 persistentDataPath
        string fallback = Application.persistentDataPath + "/Download";
        System.IO.Directory.CreateDirectory(fallback);
        Debug.Log($"[ADBManager] Using fallback directory: {fallback}");
        return fallback;
#else
        return Application.persistentDataPath + "/Download";
#endif
    }

    /// <summary>
    /// 下载并安装APK的协程
    /// </summary>
    private IEnumerator DownloadAndInstallCoroutine(AppInfo app)
    {
        isDownloading = true;
        currentDownloadingApp = app.appName;
        downloadProgress = 0f;

        // 生成安全的文件名（移除特殊字符）
        string safeAppName = app.appName.Replace(" ", "_").Replace(".", "_");
        string apkFileName = $"{safeAppName}_{app.remoteVersion}.apk";

        // 获取下载目录（自动选择可用的目录）
        string downloadDir = GetDownloadDirectory();
        string downloadPath = $"{downloadDir}/{apkFileName}";

        // 删除该应用的旧版本 APK
        DeleteOldVersionApks(downloadDir, safeAppName, app.remoteVersion);

        // 检查是否已经下载过
        if (System.IO.File.Exists(downloadPath))
        {
            Debug.Log($"[ADBManager] APK already exists: {downloadPath}");
            downloadProgress = 1f;
            OnDownloadProgress?.Invoke(app.appName, 1f);

            // 刷新媒体库，确保文件在文件管理器中可见
            ScanMediaFile(downloadPath);

            // 显示安装提示
            OnInstallComplete?.Invoke(app.appName, true, GetManualInstallMessage(apkFileName, downloadPath));

            isDownloading = false;
            currentDownloadingApp = "";
            yield break;
        }

        Debug.Log($"[ADBManager] ========== STARTING DOWNLOAD ==========");
        Debug.Log($"[ADBManager] App: {app.appName}");
        Debug.Log($"[ADBManager] URL: {app.apkUrl}");
        Debug.Log($"[ADBManager] Download to: {downloadPath}");

        // 下载APK到公共Download目录
        using (UnityWebRequest request = UnityWebRequest.Get(app.apkUrl))
        {
            request.downloadHandler = new DownloadHandlerFile(downloadPath);
            request.timeout = 60; // 60秒超时

            Debug.Log("[ADBManager] Sending web request...");
            var operation = request.SendWebRequest();

            float startTime = Time.time;
            float lastProgressTime = Time.time;
            float lastProgress = 0f;

            while (!operation.isDone)
            {
                downloadProgress = request.downloadProgress;

                // 检查下载进度是否有变化
                if (downloadProgress != lastProgress)
                {
                    Debug.Log($"[ADBManager] Download progress: {downloadProgress * 100:F1}%");
                    lastProgress = downloadProgress;
                    lastProgressTime = Time.time;
                }

                // 如果30秒内进度没有变化，可能是服务器无响应
                if (Time.time - lastProgressTime > 30f && downloadProgress < 0.01f)
                {
                    Debug.LogError("[ADBManager] Download timeout - no progress for 30 seconds");
                    request.Abort();
                    isDownloading = false;
                    currentDownloadingApp = "";
                    OnInstallComplete?.Invoke(app.appName, false, "下载超时：服务器无响应");
                    yield break;
                }

                OnDownloadProgress?.Invoke(app.appName, downloadProgress);
                yield return null;
            }

            Debug.Log($"[ADBManager] Request completed. Result: {request.result}, Error: {request.error}");
            Debug.Log($"[ADBManager] Response code: {request.responseCode}");
            Debug.Log($"[ADBManager] Downloaded bytes: {request.downloadedBytes}");

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ADBManager] Download failed: {request.error}");
                isDownloading = false;
                currentDownloadingApp = "";
                OnInstallComplete?.Invoke(app.appName, false, $"下载失败: {request.error}");
                yield break;
            }

            // 检查下载的文件大小
            if (request.downloadedBytes < 1000)
            {
                Debug.LogError($"[ADBManager] Downloaded file too small: {request.downloadedBytes} bytes");
                isDownloading = false;
                currentDownloadingApp = "";
                OnInstallComplete?.Invoke(app.appName, false, $"下载失败：文件太小 ({request.downloadedBytes} bytes)");
                yield break;
            }

            // 检查Content-Type
            string contentType = request.GetResponseHeader("Content-Type");
            Debug.Log($"[ADBManager] Content-Type: {contentType}");
        }

        // 验证下载的文件是否为有效的APK（ZIP格式，以PK开头）
        if (!ValidateApkFile(downloadPath))
        {
            Debug.LogError("[ADBManager] Downloaded file is not a valid APK");
            // 读取文件前100字节用于调试
            try
            {
                byte[] header = new byte[100];
                using (var fs = System.IO.File.OpenRead(downloadPath))
                {
                    fs.Read(header, 0, Math.Min(100, (int)fs.Length));
                }
                string headerStr = System.Text.Encoding.UTF8.GetString(header);
                Debug.LogError($"[ADBManager] File header: {headerStr}");
            }
            catch { }

            // 删除无效文件
            try { System.IO.File.Delete(downloadPath); } catch { }

            isDownloading = false;
            currentDownloadingApp = "";
            OnInstallComplete?.Invoke(app.appName, false, "下载的文件不是有效的APK");
            yield break;
        }

        downloadProgress = 1f;
        OnDownloadProgress?.Invoke(app.appName, 1f);
        Debug.Log($"[ADBManager] Download complete and validated: {downloadPath}");

        // 刷新媒体库，让文件在文件管理器中可见
        ScanMediaFile(downloadPath);

        // 尝试自动打开安装界面
        bool installStarted = OpenInstallIntent(downloadPath);

        // 显示提示
        string installMessage;
        if (installStarted)
        {
            installMessage = "下载完成！正在打开安装界面...";
        }
        else
        {
            installMessage = GetManualInstallMessage(apkFileName, downloadPath);
        }
        OnInstallComplete?.Invoke(app.appName, true, installMessage);

        isDownloading = false;
        currentDownloadingApp = "";
    }

    /// <summary>
    /// 打开系统安装界面
    /// </summary>
    private bool OpenInstallIntent(string apkPath)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                // 使用 ACTION_INSTALL_PACKAGE
                using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.INSTALL_PACKAGE"))
                using (AndroidJavaObject file = new AndroidJavaObject("java.io.File", apkPath))
                using (AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri"))
                {
                    AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("fromFile", file);

                    intent.Call<AndroidJavaObject>("setData", uri);
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.NOT_UNKNOWN_SOURCE", true);
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.RETURN_RESULT", true);
                    intent.Call<AndroidJavaObject>("addFlags", 1); // FLAG_GRANT_READ_URI_PERMISSION
                    intent.Call<AndroidJavaObject>("addFlags", 268435456); // FLAG_ACTIVITY_NEW_TASK

                    activity.Call("startActivity", intent);
                    Debug.Log("[ADBManager] Install intent started successfully");
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ADBManager] Failed to open install intent: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }

    /// <summary>
    /// 删除指定应用的旧版本 APK 文件
    /// </summary>
    private void DeleteOldVersionApks(string directory, string safeAppName, string currentVersion)
    {
        try
        {
            if (!System.IO.Directory.Exists(directory))
            {
                Debug.Log($"[ADBManager] Directory does not exist: {directory}");
                return;
            }

            string searchPattern = $"{safeAppName}_*.apk";
            string currentFileName = $"{safeAppName}_{currentVersion}.apk";

            string[] files = System.IO.Directory.GetFiles(directory, searchPattern);
            foreach (string file in files)
            {
                string fileName = System.IO.Path.GetFileName(file);
                if (fileName != currentFileName)
                {
                    try
                    {
                        System.IO.File.Delete(file);
                        Debug.Log($"[ADBManager] Deleted old APK: {fileName}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ADBManager] Failed to delete old APK {fileName}: {e.Message}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ADBManager] Error cleaning old APKs: {e.Message}");
        }
    }

    /// <summary>
    /// 获取手动安装提示信息
    /// </summary>
    private string GetManualInstallMessage(string apkFileName, string downloadPath)
    {
        // 判断是公共目录还是应用目录
        if (downloadPath.Contains("/sdcard/Download"))
        {
            return $"下载完成！请手动安装：\n\n" +
                   $"1. 打开 Quest 设置\n" +
                   $"2. 进入 存储 → 文件\n" +
                   $"3. 打开 Download 文件夹\n" +
                   $"4. 点击 {apkFileName}\n" +
                   $"5. 点击「安装」按钮";
        }
        else
        {
            // 应用目录，提供完整路径
            return $"下载完成！文件位置：\n{downloadPath}\n\n" +
                   $"请使用文件管理器找到该文件并安装";
        }
    }

    /// <summary>
    /// 获取已安装应用的版本号
    /// </summary>
    private string GetInstalledAppVersion(string packageName)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
            {
                AndroidJavaObject packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, 0);
                if (packageInfo != null)
                {
                    string version = packageInfo.Get<string>("versionName");
                    packageInfo.Dispose();
                    return version ?? "";
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log($"[ADBManager] GetInstalledAppVersion failed for {packageName}: {e.Message}");
        }
#endif
        return "";
    }


    /// <summary>
    /// 取消当前下载
    /// </summary>
    public void CancelDownload()
    {
        if (isDownloading)
        {
            StopAllCoroutines();
            isDownloading = false;
            currentDownloadingApp = "";
            downloadProgress = 0f;
            Debug.Log("[ADBManager] Download cancelled");
        }
    }
}
