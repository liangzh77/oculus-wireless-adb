using UnityEngine;
using System;

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

    // 状态更新事件
    public event Action<bool> OnADBStatusChanged;
    public event Action<string, int> OnConnectionInfoUpdated;
    public event Action<string> OnStatusMessageUpdated;

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
    /// <param name="useTcpipMode">是否使用tcpip 5555模式</param>
    public void EnableADB(bool useTcpipMode = false)
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
            statusMessage = "缺少权限，请执行:\n" + GetPermissionCommand();
            OnStatusMessageUpdated?.Invoke(statusMessage);
            return;
        }

        try
        {
            bool success = unityADBBridge.Call<bool>("enableWirelessADB", useTcpipMode);

            if (success)
            {
                Debug.Log($"[ADBManager] Wireless ADB enabled (tcpip mode: {useTcpipMode})");

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

    /// <summary>
    /// 定期更新状态（可选）
    /// </summary>
    void Start()
    {
        // 每5秒自动更新一次状态
        InvokeRepeating("UpdateStatus", 1f, 5f);
    }

    void OnDestroy()
    {
        CancelInvoke();
    }
}
