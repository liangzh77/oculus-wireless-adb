using UnityEngine;

/// <summary>
/// 应用初始化器 - 在应用启动时立即初始化所有必要组件
/// 使用 BeforeSceneLoad 确保在场景加载前就开始初始化
/// </summary>
public class AppInitializer : MonoBehaviour
{
    private static bool isInitialized = false;

    // 使用 BeforeSceneLoad 比 AfterSceneLoad 更早执行
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnBeforeSceneLoad()
    {
        Debug.Log("[AppInitializer] BeforeSceneLoad - Starting early initialization");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnAfterSceneLoad()
    {
        if (isInitialized) return;
        isInitialized = true;

        Debug.Log("[AppInitializer] AfterSceneLoad - Initializing app components...");

        // 创建一个持久的初始化器 GameObject 来管理初始化流程
        GameObject initializerObj = new GameObject("AppInitializerRunner");
        initializerObj.AddComponent<AppInitializer>();
        Object.DontDestroyOnLoad(initializerObj);
    }

    void Awake()
    {
        Debug.Log("[AppInitializer] Awake - Creating persistent components");

        // 1. 先创建 ADBBroadcastListener（优先级最高，需要接收广播）
        CreateADBBroadcastListener();

        // 2. 创建 ADBCommandReceiver
        CreateADBCommandReceiver();

        // 3. 确保 ADBManager 被创建
        var adbManager = ADBManager.Instance;
        Debug.Log("[AppInitializer] ADBManager initialized");

        Debug.Log("[AppInitializer] All components initialized");
    }

    private void CreateADBBroadcastListener()
    {
        GameObject listenerObj = GameObject.Find("ADBBroadcastListener");
        if (listenerObj == null)
        {
            listenerObj = new GameObject("ADBBroadcastListener");
            var listener = listenerObj.AddComponent<ADBBroadcastListener>();
            Object.DontDestroyOnLoad(listenerObj);
            Debug.Log("[AppInitializer] ADBBroadcastListener created and should register receiver in its Awake");
        }
        else
        {
            Debug.Log("[AppInitializer] ADBBroadcastListener already exists");
        }
    }

    private void CreateADBCommandReceiver()
    {
        GameObject receiverObj = GameObject.Find("ADBCommandReceiver");
        if (receiverObj == null)
        {
            receiverObj = new GameObject("ADBCommandReceiver");
            receiverObj.AddComponent<ADBCommandReceiver>();
            Object.DontDestroyOnLoad(receiverObj);
            Debug.Log("[AppInitializer] ADBCommandReceiver created");
        }
        else
        {
            Debug.Log("[AppInitializer] ADBCommandReceiver already exists");
        }
    }
}
