using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private ADBManager adbManager;

    void Start()
    {
        // 获取ADBManager实例
        adbManager = ADBManager.Instance;

        // 订阅ADB状态变化事件
        adbManager.OnADBStatusChanged += OnADBStatusChanged;
        adbManager.OnConnectionInfoUpdated += OnConnectionInfoUpdated;
        adbManager.OnStatusMessageUpdated += OnStatusMessageUpdated;

        // 绑定按钮事件
        if (enableButton != null)
            enableButton.onClick.AddListener(OnEnableButtonClick);

        if (disableButton != null)
            disableButton.onClick.AddListener(OnDisableButtonClick);

        if (refreshButton != null)
            refreshButton.onClick.AddListener(OnRefreshButtonClick);

        // 初始化UI
        UpdateUI();
    }

    void OnDestroy()
    {
        // 取消订阅事件
        if (adbManager != null)
        {
            adbManager.OnADBStatusChanged -= OnADBStatusChanged;
            adbManager.OnConnectionInfoUpdated -= OnConnectionInfoUpdated;
            adbManager.OnStatusMessageUpdated -= OnStatusMessageUpdated;
        }

        // 移除按钮事件
        if (enableButton != null)
            enableButton.onClick.RemoveListener(OnEnableButtonClick);

        if (disableButton != null)
            disableButton.onClick.RemoveListener(OnDisableButtonClick);

        if (refreshButton != null)
            refreshButton.onClick.RemoveListener(OnRefreshButtonClick);
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
}
