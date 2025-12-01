using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 测试启动脚本 - 确保应用能启动，并设置VR UI交互
/// </summary>
public class TestStartup : MonoBehaviour
{
    private bool vrInputInitialized = false;

    void Awake()
    {
        Debug.Log("[TestStartup] Awake called");
    }

    void Start()
    {
        Debug.Log("[TestStartup] ========== Application Started ==========");
        Debug.Log($"[TestStartup] Application Identifier: {Application.identifier}");
        Debug.Log($"[TestStartup] Unity Version: {Application.unityVersion}");
        Debug.Log($"[TestStartup] Platform: {Application.platform}");
        Debug.Log($"[TestStartup] Is VR Enabled: {UnityEngine.XR.XRSettings.enabled}");
        Debug.Log($"[TestStartup] VR Device: {UnityEngine.XR.XRSettings.loadedDeviceName}");
        Debug.Log("[TestStartup] =============================================");

        // 显示一个简单的文本确认应用已启动
        GameObject textObj = new GameObject("StartupText");
        textObj.transform.position = new Vector3(0, 0.5f, 3);
        textObj.transform.rotation = Quaternion.identity;

        var textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = "App Started!\nWireless ADB";
        textMesh.fontSize = 24;
        textMesh.characterSize = 0.05f;
        textMesh.color = Color.green;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;

        // 设置VR UI交互
        SetupVRUIInteraction();
    }

    void Update()
    {
        // 在 Update 中持续尝试，直到成功初始化
        if (!vrInputInitialized)
        {
            SetupVRUIInteraction();
        }
    }

    void SetupVRUIInteraction()
    {
        // 首先尝试查找所有相机
        Camera[] allCameras = Camera.allCameras;
        Debug.Log($"[TestStartup] Total cameras found: {allCameras.Length}");

        // 添加VRUIInput到主相机
        Camera mainCam = Camera.main;

        if (mainCam == null)
        {
            Debug.LogWarning("[TestStartup] Camera.main is null, trying to find any camera");
            if (allCameras.Length > 0)
            {
                mainCam = allCameras[0];
                Debug.Log($"[TestStartup] Using first available camera: {mainCam.name}");
            }
        }

        if (mainCam != null)
        {
            Debug.Log($"[TestStartup] Main camera found: {mainCam.name}");
            if (mainCam.GetComponent<VRUIInput>() == null)
            {
                mainCam.gameObject.AddComponent<VRUIInput>();
                Debug.Log("[TestStartup] Added VRUIInput to main camera");
                vrInputInitialized = true;
            }
            else
            {
                Debug.Log("[TestStartup] VRUIInput already exists on camera");
                vrInputInitialized = true;
            }
        }
        else
        {
            Debug.LogError("[TestStartup] No camera found! Creating VRUIInput on separate GameObject");
            // 如果找不到相机，创建一个独立的 VRUIInput
            GameObject vrInputObj = new GameObject("VRUIInputManager");
            vrInputObj.AddComponent<VRUIInput>();
            vrInputInitialized = true;
        }
    }
}
