using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// ADB UI控制器 - VR界面的ADB控制面板
///
/// 使用方法：
/// 1. 创建World Space Canvas
/// 2. 添加按钮和文本组件
/// 3. 将此脚本附加到Canvas或单独的GameObject上
/// 4. 在Inspector中关联UI组件
/// </summary>
public class ADBUIController : MonoBehaviour
{
    [Header("UI组件引用")]
    [Tooltip("启用ADB按钮")]
    public Button enableButton;

    [Tooltip("禁用ADB按钮")]
    public Button disableButton;

    [Tooltip("刷新状态按钮")]
    public Button refreshButton;

    [Tooltip("显示IP地址的文本")]
    public TextMeshProUGUI ipAddressText;

    [Tooltip("显示端口号的文本")]
    public TextMeshProUGUI portText;

    [Tooltip("显示状态消息的文本")]
    public TextMeshProUGUI statusText;

    [Tooltip("显示权限提示的文本（不会被状态消息覆盖）")]
    public TextMeshProUGUI permissionText;

    [Tooltip("显示ADB启用状态的指示器")]
    public Image statusIndicator;

    [Header("UI颜色设置")]
    public Color enabledColor = Color.green;
    public Color disabledColor = Color.red;
    public Color neutralColor = Color.gray;

    [Header("面板切换")]
    [Tooltip("控制面板 (Panel1)")]
    public GameObject panel1;

    [Tooltip("应用列表面板 (Panel2)")]
    public GameObject panel2;

    [Tooltip("显示应用列表按钮")]
    public Button showAppsButton;

    [Tooltip("返回控制面板按钮")]
    public Button backToPanel1Button;

    [Header("应用列表")]
    [Tooltip("刷新应用列表按钮")]
    public Button refreshAppsButton;

    [Tooltip("应用数量文本")]
    public TextMeshProUGUI appCountText;

    [Tooltip("应用列表内容容器")]
    public GameObject appListContent;

    [Tooltip("应用列表滚动视图")]
    public ScrollRect appListScrollRect;

    [Tooltip("提示信息文本")]
    public TextMeshProUGUI promptText;

    [Header("滚动设置")]
    [Tooltip("连续滚动速度（条/秒）")]
    public float scrollItemsPerSecond = 8f;

    [Tooltip("摇杆触发阈值")]
    public float scrollThreshold = 0.3f;

    private ADBManager adbManager;
    private List<GameObject> appItemObjects = new List<GameObject>();
    private float lastScrollTime = 0f;
    private float scrollInterval => 1f / scrollItemsPerSecond;  // 每条的时间间隔

    // 存储应用信息和对应的UI元素，用于更新下载进度
    private Dictionary<string, AppInfo> appInfoMap = new Dictionary<string, AppInfo>();
    private Dictionary<string, GameObject> appItemMap = new Dictionary<string, GameObject>();

    void Start()
    {
        // 获取ADBManager实例
        adbManager = ADBManager.Instance;

        // 订阅ADB状态变化事件
        adbManager.OnADBStatusChanged += OnADBStatusChanged;
        adbManager.OnConnectionInfoUpdated += OnConnectionInfoUpdated;
        adbManager.OnStatusMessageUpdated += OnStatusMessageUpdated;
        adbManager.OnAppListUpdated += OnAppListUpdated;
        adbManager.OnDownloadProgress += OnDownloadProgress;
        adbManager.OnInstallComplete += OnInstallComplete;

        // 绑定按钮事件
        if (enableButton != null)
            enableButton.onClick.AddListener(OnEnableButtonClick);

        if (disableButton != null)
            disableButton.onClick.AddListener(OnDisableButtonClick);

        if (refreshButton != null)
            refreshButton.onClick.AddListener(OnRefreshButtonClick);

        // 绑定面板切换按钮
        if (showAppsButton != null)
            showAppsButton.onClick.AddListener(OnShowAppsButtonClick);

        if (backToPanel1Button != null)
            backToPanel1Button.onClick.AddListener(OnBackToPanel1ButtonClick);

        if (refreshAppsButton != null)
            refreshAppsButton.onClick.AddListener(OnRefreshAppsButtonClick);

        // 初始化UI
        UpdateUI();
    }

    void Update()
    {
        // 处理手柄滚轴滚动应用列表
        HandleScrollInput();
    }

    /// <summary>
    /// 处理手柄滚轴输入来滚动应用列表
    /// </summary>
    private void HandleScrollInput()
    {
        // 只在 Panel2 激活时处理滚动
        if (panel2 == null || !panel2.activeSelf || appListScrollRect == null)
            return;

        float scrollInput = 0f;

        // 使用 Unity XR InputDevice 获取手柄摇杆输入
        var rightHandDevices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightHandDevices);

        var leftHandDevices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftHandDevices);

        // 尝试从右手柄获取摇杆输入
        if (rightHandDevices.Count > 0)
        {
            if (rightHandDevices[0].TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 rightStick))
            {
                scrollInput = rightStick.y;
            }
        }

        // 如果右手柄没有输入，尝试左手柄
        if (Mathf.Abs(scrollInput) < scrollThreshold && leftHandDevices.Count > 0)
        {
            if (leftHandDevices[0].TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 leftStick))
            {
                scrollInput = leftStick.y;
            }
        }

#if UNITY_EDITOR
        // 编辑器模式下使用键盘或鼠标滚轮
        if (Mathf.Abs(scrollInput) < scrollThreshold)
        {
            scrollInput = Input.GetAxis("Vertical");
            if (scrollInput == 0)
            {
                scrollInput = Input.mouseScrollDelta.y;
            }
        }
#endif

        // 摇杆超过阈值才滚动
        if (Mathf.Abs(scrollInput) > scrollThreshold)
        {
            float currentTime = Time.time;

            // 检查是否到了下一次滚动的时间
            if (currentTime - lastScrollTime >= scrollInterval)
            {
                lastScrollTime = currentTime;

                // 计算每条的滚动量
                // Content的总高度 / 条目数 = 平均每条高度
                // 滚动量 = 平均每条高度 / Content总高度
                float itemCount = appItemObjects.Count;
                if (itemCount > 0)
                {
                    RectTransform contentRect = appListScrollRect.content;
                    RectTransform viewportRect = appListScrollRect.viewport;

                    if (contentRect != null && viewportRect != null)
                    {
                        float contentHeight = contentRect.rect.height;
                        float viewportHeight = viewportRect.rect.height;
                        float scrollableHeight = contentHeight - viewportHeight;

                        if (scrollableHeight > 0)
                        {
                            // 计算平均每条高度对应的normalized值
                            float averageItemHeight = contentHeight / itemCount;
                            float scrollAmount = averageItemHeight / scrollableHeight;

                            // 根据摇杆方向滚动
                            float direction = scrollInput > 0 ? 1f : -1f;
                            float newPosition = appListScrollRect.verticalNormalizedPosition + direction * scrollAmount;
                            appListScrollRect.verticalNormalizedPosition = Mathf.Clamp01(newPosition);
                        }
                    }
                }
            }
        }
        else
        {
            // 摇杆回中时重置，下次推动立即响应
            lastScrollTime = 0f;
        }
    }

    void OnDestroy()
    {
        // 取消订阅事件
        if (adbManager != null)
        {
            adbManager.OnADBStatusChanged -= OnADBStatusChanged;
            adbManager.OnConnectionInfoUpdated -= OnConnectionInfoUpdated;
            adbManager.OnStatusMessageUpdated -= OnStatusMessageUpdated;
            adbManager.OnAppListUpdated -= OnAppListUpdated;
            adbManager.OnDownloadProgress -= OnDownloadProgress;
            adbManager.OnInstallComplete -= OnInstallComplete;
        }

        // 移除按钮事件
        if (enableButton != null)
            enableButton.onClick.RemoveListener(OnEnableButtonClick);

        if (disableButton != null)
            disableButton.onClick.RemoveListener(OnDisableButtonClick);

        if (refreshButton != null)
            refreshButton.onClick.RemoveListener(OnRefreshButtonClick);

        // 移除面板切换按钮事件
        if (showAppsButton != null)
            showAppsButton.onClick.RemoveListener(OnShowAppsButtonClick);

        if (backToPanel1Button != null)
            backToPanel1Button.onClick.RemoveListener(OnBackToPanel1ButtonClick);

        if (refreshAppsButton != null)
            refreshAppsButton.onClick.RemoveListener(OnRefreshAppsButtonClick);
    }

    // ========== 按钮回调 ==========

    private void OnEnableButtonClick()
    {
        Debug.Log("[ADBUIController] Enable button clicked");

        // 每次点击都重新检查权限状态
        UpdatePermissionStatus();

        // 如果没有权限，不继续执行
        if (!adbManager.HasRequiredPermissions())
        {
            return;
        }

        adbManager.EnableADB();

        // 更新UI反馈
        if (statusText != null)
            statusText.text = "正在启动ADB...";
    }

    private void OnDisableButtonClick()
    {
        Debug.Log("[ADBUIController] Disable button clicked");

        // 每次点击都重新检查权限状态
        UpdatePermissionStatus();

        // 如果没有权限，不继续执行
        if (!adbManager.HasRequiredPermissions())
        {
            return;
        }

        adbManager.DisableADB();

        // 更新UI反馈
        if (statusText != null)
            statusText.text = "正在禁用ADB...";
    }

    private void OnRefreshButtonClick()
    {
        Debug.Log("[ADBUIController] Refresh button clicked");

        // 每次点击都重新检查权限状态
        UpdatePermissionStatus();

        adbManager.UpdateStatus();

        // 更新UI反馈
        if (statusText != null)
            statusText.text = "正在刷新状态...";
    }

    // ========== 事件处理 ==========

    private void OnADBStatusChanged(bool isEnabled)
    {
        Debug.Log($"[ADBUIController] ADB status changed: {isEnabled}");

        // 更新状态指示器颜色
        if (statusIndicator != null)
        {
            statusIndicator.color = isEnabled ? enabledColor : disabledColor;
        }

        // 更新按钮可用性
        if (enableButton != null)
            enableButton.interactable = !isEnabled;

        if (disableButton != null)
            disableButton.interactable = isEnabled;
    }

    private void OnConnectionInfoUpdated(string ip, int port)
    {
        Debug.Log($"[ADBUIController] Connection info updated: {ip}:{port}");

        if (ipAddressText != null)
        {
            ipAddressText.text = string.IsNullOrEmpty(ip) ? "未连接" : ip;
        }

        if (portText != null)
        {
            portText.text = port > 0 ? port.ToString() : "----";
        }
    }

    private void OnStatusMessageUpdated(string message)
    {
        Debug.Log($"[ADBUIController] Status message updated: {message}");

        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    // ========== UI更新 ==========

    private void UpdateUI()
    {
        if (adbManager == null || !adbManager.IsInitialized)
        {
            // 未初始化状态
            if (statusText != null)
                statusText.text = "ADB管理器未初始化";

            if (statusIndicator != null)
                statusIndicator.color = neutralColor;

            if (ipAddressText != null)
                ipAddressText.text = "---";

            if (portText != null)
                portText.text = "----";

            if (enableButton != null)
                enableButton.interactable = false;

            if (disableButton != null)
                disableButton.interactable = false;

            return;
        }

        // 更新所有UI元素
        OnADBStatusChanged(adbManager.IsADBEnabled);
        OnConnectionInfoUpdated(adbManager.CurrentIP, adbManager.CurrentPort);
        OnStatusMessageUpdated(adbManager.StatusMessage);

        // 检查权限并更新权限提示
        UpdatePermissionStatus();
    }

    /// <summary>
    /// 更新权限状态提示
    /// </summary>
    private void UpdatePermissionStatus()
    {
        if (permissionText == null) return;

        if (adbManager != null && !adbManager.HasRequiredPermissions())
        {
            permissionText.gameObject.SetActive(true);
            permissionText.text = "缺少权限！请在电脑端执行:\nadb shell pm grant com.ChuJiao.quest3_wireless_adb android.permission.WRITE_SECURE_SETTINGS";
        }
        else
        {
            permissionText.gameObject.SetActive(false);
        }
    }

    // ========== 辅助方法 ==========

    /// <summary>
    /// 手动触发UI更新（用于调试）
    /// </summary>
    [ContextMenu("Force Update UI")]
    public void ForceUpdateUI()
    {
        UpdateUI();
    }

    /// <summary>
    /// 复制ADB连接命令到剪贴板（可选功能）
    /// </summary>
    public void CopyConnectCommand()
    {
        if (adbManager == null || !adbManager.IsADBEnabled) return;

        string command = $"adb connect {adbManager.CurrentIP}:{adbManager.CurrentPort}";
        GUIUtility.systemCopyBuffer = command;

        Debug.Log($"[ADBUIController] Copied to clipboard: {command}");

        if (statusText != null)
        {
            string currentStatus = statusText.text;
            statusText.text = "已复制连接命令！";
            Invoke("RestoreStatusText", 2f);
        }
    }

    private string savedStatusText = "";
    private void RestoreStatusText()
    {
        if (statusText != null)
        {
            statusText.text = adbManager.StatusMessage;
        }
    }

    // ========== 应用列表功能 ==========

    /// <summary>
    /// 显示应用列表按钮点击
    /// </summary>
    private void OnShowAppsButtonClick()
    {
        Debug.Log("[ADBUIController] Show apps button clicked");
        Debug.Log($"[ADBUIController] panel1={panel1}, panel2={panel2}");
        ShowPanel2();

        // 如果应用列表为空，自动刷新
        if (adbManager.InstalledApps.Count == 0)
        {
            Debug.Log("[ADBUIController] App list is empty, refreshing...");
            OnRefreshAppsButtonClick();
        }
    }

    /// <summary>
    /// 返回控制面板按钮点击
    /// </summary>
    private void OnBackToPanel1ButtonClick()
    {
        Debug.Log("[ADBUIController] Back to panel1 button clicked");
        ShowPanel1();
    }

    /// <summary>
    /// 刷新应用列表按钮点击
    /// </summary>
    private void OnRefreshAppsButtonClick()
    {
        Debug.Log("[ADBUIController] Refresh apps button clicked");

        if (appCountText != null)
        {
            appCountText.text = "正在加载...";
        }

        adbManager.RefreshInstalledApps();
    }

    /// <summary>
    /// 显示 Panel1（控制面板）
    /// </summary>
    private void ShowPanel1()
    {
        if (panel1 != null)
            panel1.SetActive(true);

        if (panel2 != null)
            panel2.SetActive(false);
    }

    /// <summary>
    /// 显示 Panel2（应用列表面板）
    /// </summary>
    private void ShowPanel2()
    {
        if (panel1 != null)
            panel1.SetActive(false);

        if (panel2 != null)
            panel2.SetActive(true);
    }

    /// <summary>
    /// 应用列表更新事件处理
    /// </summary>
    private void OnAppListUpdated(List<AppInfo> apps)
    {
        Debug.Log($"[ADBUIController] App list updated: {apps.Count} apps");

        // 更新应用计数
        if (appCountText != null)
        {
            appCountText.text = $"应用数量: {apps.Count}";
        }

        // 更新应用列表显示
        UpdateAppListUI(apps);
    }

    /// <summary>
    /// 更新应用列表UI
    /// </summary>
    private void UpdateAppListUI(List<AppInfo> apps)
    {
        Debug.Log($"[ADBUIController] UpdateAppListUI called with {apps.Count} apps");
        Debug.Log($"[ADBUIController] appListContent={appListContent}");

        if (appListContent == null)
        {
            Debug.LogError("[ADBUIController] appListContent is null! Cannot display apps.");
            return;
        }

        // 清除现有的应用项
        foreach (var item in appItemObjects)
        {
            if (item != null)
                Destroy(item);
        }
        appItemObjects.Clear();
        appInfoMap.Clear();
        appItemMap.Clear();

        // 创建应用项
        foreach (var app in apps)
        {
            Debug.Log($"[ADBUIController] Creating item for: {app.appName} ({app.packageName}) v{app.versionName}");
            GameObject itemObj = CreateAppListItem(app);
            appItemObjects.Add(itemObj);

            // 存储映射关系
            appInfoMap[app.appName] = app;
            appItemMap[app.appName] = itemObj;
        }

        Debug.Log($"[ADBUIController] Created {appItemObjects.Count} app items");
    }

    /// <summary>
    /// 创建应用列表项
    /// </summary>
    private GameObject CreateAppListItem(AppInfo app)
    {
        GameObject itemObj = new GameObject($"AppItem_{app.packageName}");
        itemObj.transform.SetParent(appListContent.transform, false);

        // 根据是否匹配远程应用设置不同的样式
        bool isMatched = app.isMatched;

        // 添加背景 - 不匹配的用更暗的背景
        Image bg = itemObj.AddComponent<Image>();
        bg.color = isMatched ? new Color(0.2f, 0.2f, 0.25f, 1f) : new Color(0.15f, 0.15f, 0.15f, 0.8f);

        // 设置尺寸 - 不匹配的项目更小
        float itemHeight = isMatched ? 170 : 100;
        RectTransform rect = itemObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, itemHeight);

        // 添加 LayoutElement
        LayoutElement layoutElement = itemObj.AddComponent<LayoutElement>();
        layoutElement.minHeight = itemHeight;
        layoutElement.preferredHeight = itemHeight;

        if (isMatched)
        {
            // ========== 匹配的应用 - 完整显示 ==========
            bool needsUpdate = !string.IsNullOrEmpty(app.remoteVersion) && app.versionName != app.remoteVersion;

            // 创建应用名称文本
            GameObject nameObj = new GameObject("AppName");
            nameObj.transform.SetParent(itemObj.transform, false);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = app.appName;
            nameText.fontSize = 54;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.color = Color.white;
            nameText.fontStyle = FontStyles.Bold;

            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.6f);
            nameRect.anchorMax = new Vector2(0.5f, 1);
            nameRect.offsetMin = new Vector2(20, 5);
            nameRect.offsetMax = new Vector2(-10, -5);

            // 创建本机版本文本
            GameObject localVersionObj = new GameObject("LocalVersion");
            localVersionObj.transform.SetParent(itemObj.transform, false);

            TextMeshProUGUI localVersionText = localVersionObj.AddComponent<TextMeshProUGUI>();
            localVersionText.text = $"Local: {app.versionName}";
            localVersionText.fontSize = 36;
            localVersionText.alignment = TextAlignmentOptions.Left;
            localVersionText.color = new Color(0.7f, 0.7f, 0.7f);

            RectTransform localVersionRect = localVersionObj.GetComponent<RectTransform>();
            localVersionRect.anchorMin = new Vector2(0, 0.3f);
            localVersionRect.anchorMax = new Vector2(0.4f, 0.6f);
            localVersionRect.offsetMin = new Vector2(20, 0);
            localVersionRect.offsetMax = new Vector2(-10, 0);

            // 创建远程版本文本
            GameObject remoteVersionObj = new GameObject("RemoteVersion");
            remoteVersionObj.transform.SetParent(itemObj.transform, false);

            TextMeshProUGUI remoteVersionText = remoteVersionObj.AddComponent<TextMeshProUGUI>();
            remoteVersionText.text = $"Latest: {(string.IsNullOrEmpty(app.remoteVersion) ? "N/A" : app.remoteVersion)}";
            remoteVersionText.fontSize = 36;
            remoteVersionText.alignment = TextAlignmentOptions.Left;
            remoteVersionText.color = needsUpdate ? new Color(1f, 0.8f, 0.2f) : new Color(0.5f, 0.8f, 0.5f);

            RectTransform remoteVersionRect = remoteVersionObj.GetComponent<RectTransform>();
            remoteVersionRect.anchorMin = new Vector2(0.4f, 0.3f);
            remoteVersionRect.anchorMax = new Vector2(0.75f, 0.6f);
            remoteVersionRect.offsetMin = new Vector2(10, 0);
            remoteVersionRect.offsetMax = new Vector2(-10, 0);

            // 如果需要更新，添加更新按钮
            if (needsUpdate && !string.IsNullOrEmpty(app.apkUrl))
            {
                GameObject updateBtnObj = new GameObject("UpdateButton");
                updateBtnObj.transform.SetParent(itemObj.transform, false);

                Image btnImage = updateBtnObj.AddComponent<Image>();
                btnImage.color = new Color(0.2f, 0.6f, 0.3f);

                Button updateBtn = updateBtnObj.AddComponent<Button>();

                RectTransform btnRect = updateBtnObj.GetComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.75f, 0.3f);
                btnRect.anchorMax = new Vector2(0.98f, 0.95f);
                btnRect.offsetMin = new Vector2(10, 5);
                btnRect.offsetMax = new Vector2(-10, -5);

                // 按钮文本
                GameObject btnTextObj = new GameObject("Text");
                btnTextObj.transform.SetParent(updateBtnObj.transform, false);

                TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
                btnText.text = "Update";
                btnText.fontSize = 32;
                btnText.alignment = TextAlignmentOptions.Center;
                btnText.color = Color.white;

                RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.offsetMin = Vector2.zero;
                btnTextRect.offsetMax = Vector2.zero;

                // 添加碰撞体以便VR射线交互
                // 注意：RectTransform在刚创建时rect尺寸可能为0，使用固定尺寸
                BoxCollider btnCollider = updateBtnObj.AddComponent<BoxCollider>();
                btnCollider.size = new Vector3(300f, 100f, 50f);
                btnCollider.center = Vector3.zero;

                // 绑定点击事件
                AppInfo appCopy = app; // 避免闭包问题
                updateBtn.onClick.AddListener(() => OnUpdateButtonClick(appCopy));
            }

            // 创建进度条（默认隐藏）
            GameObject progressObj = new GameObject("ProgressBar");
            progressObj.transform.SetParent(itemObj.transform, false);
            progressObj.SetActive(false);

            Image progressBg = progressObj.AddComponent<Image>();
            progressBg.color = new Color(0.3f, 0.3f, 0.3f);

            RectTransform progressRect = progressObj.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0.75f, 0.3f);
            progressRect.anchorMax = new Vector2(0.98f, 0.6f);
            progressRect.offsetMin = new Vector2(10, 5);
            progressRect.offsetMax = new Vector2(-10, -5);

            // 进度填充
            GameObject progressFillObj = new GameObject("Fill");
            progressFillObj.transform.SetParent(progressObj.transform, false);

            Image progressFill = progressFillObj.AddComponent<Image>();
            progressFill.color = new Color(0.3f, 0.7f, 0.4f);

            RectTransform fillRect = progressFillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0, 1);  // 初始宽度为0
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // 进度文本
            GameObject progressTextObj = new GameObject("ProgressText");
            progressTextObj.transform.SetParent(progressObj.transform, false);

            TextMeshProUGUI progressText = progressTextObj.AddComponent<TextMeshProUGUI>();
            progressText.text = "0%";
            progressText.fontSize = 28;
            progressText.alignment = TextAlignmentOptions.Center;
            progressText.color = Color.white;

            RectTransform progressTextRect = progressTextObj.GetComponent<RectTransform>();
            progressTextRect.anchorMin = Vector2.zero;
            progressTextRect.anchorMax = Vector2.one;
            progressTextRect.offsetMin = Vector2.zero;
            progressTextRect.offsetMax = Vector2.zero;

            // 创建包名文本
            GameObject packageObj = new GameObject("PackageName");
            packageObj.transform.SetParent(itemObj.transform, false);

            TextMeshProUGUI packageText = packageObj.AddComponent<TextMeshProUGUI>();
            packageText.text = app.packageName;
            packageText.fontSize = 30;
            packageText.alignment = TextAlignmentOptions.Left;
            packageText.color = new Color(0.5f, 0.5f, 0.5f);

            RectTransform packageRect = packageObj.GetComponent<RectTransform>();
            packageRect.anchorMin = new Vector2(0, 0);
            packageRect.anchorMax = new Vector2(0.75f, 0.3f);
            packageRect.offsetMin = new Vector2(20, 5);
            packageRect.offsetMax = new Vector2(-10, -5);
        }
        else
        {
            // ========== 不匹配的应用 - 灰色简化显示 ==========
            Color grayColor = new Color(0.4f, 0.4f, 0.4f);

            // 创建应用名称文本
            GameObject nameObj = new GameObject("AppName");
            nameObj.transform.SetParent(itemObj.transform, false);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = app.appName;
            nameText.fontSize = 36;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.color = grayColor;

            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.5f);
            nameRect.anchorMax = new Vector2(0.6f, 1);
            nameRect.offsetMin = new Vector2(20, 5);
            nameRect.offsetMax = new Vector2(-10, -5);

            // 创建版本文本
            GameObject versionObj = new GameObject("Version");
            versionObj.transform.SetParent(itemObj.transform, false);

            TextMeshProUGUI versionText = versionObj.AddComponent<TextMeshProUGUI>();
            versionText.text = $"v{app.versionName}";
            versionText.fontSize = 30;
            versionText.alignment = TextAlignmentOptions.Right;
            versionText.color = grayColor;

            RectTransform versionRect = versionObj.GetComponent<RectTransform>();
            versionRect.anchorMin = new Vector2(0.6f, 0.5f);
            versionRect.anchorMax = new Vector2(1, 1);
            versionRect.offsetMin = new Vector2(10, 5);
            versionRect.offsetMax = new Vector2(-20, -5);

            // 创建包名文本
            GameObject packageObj = new GameObject("PackageName");
            packageObj.transform.SetParent(itemObj.transform, false);

            TextMeshProUGUI packageText = packageObj.AddComponent<TextMeshProUGUI>();
            packageText.text = app.packageName;
            packageText.fontSize = 24;
            packageText.alignment = TextAlignmentOptions.Left;
            packageText.color = new Color(0.35f, 0.35f, 0.35f);

            RectTransform packageRect = packageObj.GetComponent<RectTransform>();
            packageRect.anchorMin = new Vector2(0, 0);
            packageRect.anchorMax = new Vector2(1, 0.5f);
            packageRect.offsetMin = new Vector2(20, 5);
            packageRect.offsetMax = new Vector2(-20, -5);
        }

        return itemObj;
    }

    // ========== 下载和安装功能 ==========

    /// <summary>
    /// 更新按钮点击事件
    /// </summary>
    private void OnUpdateButtonClick(AppInfo app)
    {
        Debug.Log($"[ADBUIController] ========== UPDATE BUTTON CLICKED ==========");
        Debug.Log($"[ADBUIController] App: {app?.appName ?? "NULL"}");
        Debug.Log($"[ADBUIController] APK URL: {app?.apkUrl ?? "NULL"}");
        Debug.Log($"[ADBUIController] isDownloading: {adbManager?.IsDownloading}");

        if (app == null)
        {
            Debug.LogError("[ADBUIController] App is null!");
            return;
        }

        if (adbManager == null)
        {
            Debug.LogError("[ADBUIController] adbManager is null!");
            return;
        }

        if (adbManager.IsDownloading)
        {
            Debug.LogWarning("[ADBUIController] Already downloading another app");
            return;
        }

        // 隐藏更新按钮，显示进度条
        if (appItemMap.TryGetValue(app.appName, out GameObject itemObj))
        {
            Transform updateBtn = itemObj.transform.Find("UpdateButton");
            Transform progressBar = itemObj.transform.Find("ProgressBar");

            Debug.Log($"[ADBUIController] Found itemObj, updateBtn={updateBtn != null}, progressBar={progressBar != null}");

            if (updateBtn != null) updateBtn.gameObject.SetActive(false);
            if (progressBar != null) progressBar.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"[ADBUIController] Could not find itemObj for {app.appName}");
        }

        Debug.Log($"[ADBUIController] Calling DownloadAndInstallAPK...");
        // 开始下载安装
        adbManager.DownloadAndInstallAPK(app);
        Debug.Log($"[ADBUIController] DownloadAndInstallAPK called");
    }

    /// <summary>
    /// 下载进度更新事件
    /// </summary>
    private void OnDownloadProgress(string appName, float progress)
    {
        if (appItemMap.TryGetValue(appName, out GameObject itemObj))
        {
            Transform progressBar = itemObj.transform.Find("ProgressBar");
            if (progressBar != null)
            {
                // 更新进度填充
                Transform fill = progressBar.Find("Fill");
                if (fill != null)
                {
                    RectTransform fillRect = fill.GetComponent<RectTransform>();
                    fillRect.anchorMax = new Vector2(progress, 1);
                }

                // 更新进度文本
                Transform textObj = progressBar.Find("ProgressText");
                if (textObj != null)
                {
                    TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
                    if (text != null)
                    {
                        int percent = Mathf.RoundToInt(progress * 100);
                        text.text = progress < 1f ? $"{percent}%" : "Installing...";
                    }
                }
            }
        }
    }

    /// <summary>
    /// 安装完成事件
    /// </summary>
    private void OnInstallComplete(string appName, bool success, string message)
    {
        Debug.Log($"[ADBUIController] Install complete for {appName}: success={success}, message={message}");

        // 更新提示文本
        if (promptText != null)
        {
            promptText.text = message;
            promptText.color = success ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.7f, 0.7f);
        }

        if (appItemMap.TryGetValue(appName, out GameObject itemObj))
        {
            Transform progressBar = itemObj.transform.Find("ProgressBar");
            if (progressBar != null)
            {
                // 更新进度文本显示结果
                Transform textObj = progressBar.Find("ProgressText");
                if (textObj != null)
                {
                    TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
                    if (text != null)
                    {
                        text.text = success ? "Done!" : "Failed";
                        text.color = success ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f);
                    }
                }

                // 更新背景颜色
                Image bg = progressBar.GetComponent<Image>();
                if (bg != null)
                {
                    bg.color = success ? new Color(0.2f, 0.5f, 0.3f) : new Color(0.5f, 0.2f, 0.2f);
                }
            }
        }

        // 如果安装成功，延迟刷新应用列表
        if (success)
        {
            Invoke("DelayedRefreshApps", 2f);
        }
    }

    /// <summary>
    /// 延迟刷新应用列表
    /// </summary>
    private void DelayedRefreshApps()
    {
        Debug.Log("[ADBUIController] Refreshing app list after install...");
        adbManager.RefreshInstalledApps();
    }
}
