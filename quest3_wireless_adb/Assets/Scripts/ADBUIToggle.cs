using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// ADB UI 显示/隐藏控制器
/// 双击A键显示或隐藏UI面板，并将UI移动到用户面前
/// </summary>
public class ADBUIToggle : MonoBehaviour
{
    [Header("UI引用")]
    [Tooltip("要控制显示/隐藏的Canvas")]
    public GameObject targetCanvas;

    [Header("双击设置")]
    [Tooltip("双击的最大间隔时间（秒）")]
    public float doubleTapInterval = 0.4f;

    [Header("UI位置设置")]
    [Tooltip("UI距离用户的距离")]
    public float distanceFromUser = 2f;

    [Tooltip("UI相对于用户眼睛的高度偏移")]
    public float heightOffset = 0f;

    private InputDevice rightController;
    private InputDevice leftController;
    private float lastTapTime = 0f;
    private bool wasAPressed = false;
    private bool isUIVisible = false;

    void Start()
    {
        // 默认隐藏UI
        if (targetCanvas != null)
        {
            targetCanvas.SetActive(false);
            isUIVisible = false;
        }

        InitializeControllers();
    }

    void InitializeControllers()
    {
        var rightHandDevices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightHandDevices);
        if (rightHandDevices.Count > 0)
        {
            rightController = rightHandDevices[0];
            Debug.Log("[ADBUIToggle] Right controller found: " + rightController.name);
        }

        var leftHandDevices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftHandDevices);
        if (leftHandDevices.Count > 0)
        {
            leftController = leftHandDevices[0];
            Debug.Log("[ADBUIToggle] Left controller found: " + leftController.name);
        }
    }

    void Update()
    {
        // 重新检测控制器
        if (!rightController.isValid && !leftController.isValid)
        {
            InitializeControllers();
        }

        // 检测A键（primaryButton）
        bool isAPressed = false;

        if (rightController.isValid)
        {
            rightController.TryGetFeatureValue(CommonUsages.primaryButton, out isAPressed);
        }

        if (!isAPressed && leftController.isValid)
        {
            leftController.TryGetFeatureValue(CommonUsages.primaryButton, out isAPressed);
        }

        // 检测按下事件（从未按到按下）
        if (isAPressed && !wasAPressed)
        {
            float currentTime = Time.time;
            float timeSinceLastTap = currentTime - lastTapTime;

            if (timeSinceLastTap <= doubleTapInterval)
            {
                // 双击检测成功
                Debug.Log("[ADBUIToggle] Double tap detected!");
                ToggleUI();
                lastTapTime = 0f; // 重置，防止连续触发
            }
            else
            {
                // 记录第一次点击时间
                lastTapTime = currentTime;
            }
        }

        wasAPressed = isAPressed;
    }

    /// <summary>
    /// 切换UI显示状态
    /// </summary>
    private void ToggleUI()
    {
        if (targetCanvas == null)
        {
            Debug.LogWarning("[ADBUIToggle] Target canvas is not assigned!");
            return;
        }

        isUIVisible = !isUIVisible;

        if (isUIVisible)
        {
            // 显示UI并移动到用户面前
            MoveUIToFrontOfUser();
            targetCanvas.SetActive(true);
            Debug.Log("[ADBUIToggle] UI shown");
        }
        else
        {
            // 隐藏UI
            targetCanvas.SetActive(false);
            Debug.Log("[ADBUIToggle] UI hidden");
        }
    }

    /// <summary>
    /// 将UI移动到用户面前
    /// </summary>
    private void MoveUIToFrontOfUser()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[ADBUIToggle] Main camera not found!");
            return;
        }

        // 获取相机位置和前方向
        Vector3 cameraPosition = mainCamera.transform.position;
        Vector3 cameraForward = mainCamera.transform.forward;

        // 计算UI的目标位置（在用户前方，水平方向）
        Vector3 forwardFlat = new Vector3(cameraForward.x, 0, cameraForward.z).normalized;
        if (forwardFlat.magnitude < 0.1f)
        {
            // 如果用户直视上方或下方，使用相机的forward
            forwardFlat = cameraForward;
        }

        Vector3 targetPosition = cameraPosition + forwardFlat * distanceFromUser;
        targetPosition.y = cameraPosition.y + heightOffset;

        // 设置UI位置
        targetCanvas.transform.position = targetPosition;

        // 让UI面向用户
        Vector3 lookDirection = cameraPosition - targetPosition;
        lookDirection.y = 0; // 保持水平
        if (lookDirection.magnitude > 0.1f)
        {
            targetCanvas.transform.rotation = Quaternion.LookRotation(-lookDirection);
        }

        Debug.Log($"[ADBUIToggle] UI moved to {targetPosition}");
    }

    void OnEnable()
    {
        InputDevices.deviceConnected += OnDeviceConnected;
    }

    void OnDisable()
    {
        InputDevices.deviceConnected -= OnDeviceConnected;
    }

    void OnDeviceConnected(InputDevice device)
    {
        Debug.Log($"[ADBUIToggle] Device connected: {device.name}");
        InitializeControllers();
    }

    /// <summary>
    /// 切换UI显示状态（用于调试）
    /// </summary>
    [ContextMenu("Toggle UI (测试)")]
    public void ToggleUIForTest()
    {
        ToggleUI();
    }

    /// <summary>
    /// 手动显示UI（用于调试）
    /// </summary>
    [ContextMenu("Show UI")]
    public void ShowUI()
    {
        if (targetCanvas != null)
        {
            MoveUIToFrontOfUser();
            targetCanvas.SetActive(true);
            isUIVisible = true;
        }
    }

    /// <summary>
    /// 手动隐藏UI（用于调试）
    /// </summary>
    [ContextMenu("Hide UI")]
    public void HideUI()
    {
        if (targetCanvas != null)
        {
            targetCanvas.SetActive(false);
            isUIVisible = false;
        }
    }
}
