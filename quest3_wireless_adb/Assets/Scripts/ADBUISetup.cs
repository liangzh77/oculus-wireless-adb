using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ADB UI 自动设置脚本 - 用于在编辑器中快速创建 ADB 控制面板
///
/// 使用方法：
/// 1. 在场景中创建一个空的GameObject，命名为 "ADB UI Setup"
/// 2. 附加这个脚本
/// 3. 在Inspector中点击右键菜单 "Setup ADB UI" 或者运行场景时自动创建
/// </summary>
[ExecuteInEditMode]
public class ADBUISetup : MonoBehaviour
{
    [Header("UI 配置")]
    [Tooltip("Canvas的位置")]
    public Vector3 canvasPosition = new Vector3(0, 1.5f, 2);

    [Tooltip("Canvas的旋转")]
    public Vector3 canvasRotation = new Vector3(0, 0, 0);

    [Tooltip("Canvas的缩放")]
    public float canvasScale = 0.001f;

    [Header("颜色配置")]
    public Color enabledColor = Color.green;
    public Color disabledColor = Color.red;
    public Color neutralColor = Color.gray;

    [Header("状态")]
    public bool isSetupComplete = false;

    /// <summary>
    /// 自动设置 ADB UI（编辑器模式）
    /// </summary>
    [ContextMenu("Setup ADB UI")]
    public void SetupADBUI()
    {
        if (isSetupComplete)
        {
            Debug.LogWarning("[ADBUISetup] UI已经设置完成，如需重新设置请先删除现有UI");
            return;
        }

        Debug.Log("[ADBUISetup] 开始创建 ADB UI...");

        // 1. 创建 Canvas
        GameObject canvasObj = new GameObject("ADB Control Canvas");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = canvasPosition;
        canvasObj.transform.localRotation = Quaternion.Euler(canvasRotation);
        canvasObj.transform.localScale = new Vector3(canvasScale, canvasScale, canvasScale);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;

        GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1920, 1080);

        // 2. 创建背景面板
        GameObject panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(canvasObj.transform, false);

        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // 3. 创建标题
        CreateTextTMP(panelObj, "Title", "无线 ADB 控制面板",
            new Vector2(0, 400), new Vector2(800, 100), 72, TextAlignmentOptions.Center);

        // 4. 创建状态指示器
        GameObject indicatorObj = new GameObject("StatusIndicator");
        indicatorObj.transform.SetParent(panelObj.transform, false);

        Image indicatorImage = indicatorObj.AddComponent<Image>();
        indicatorImage.color = neutralColor;

        RectTransform indicatorRect = indicatorObj.GetComponent<RectTransform>();
        indicatorRect.anchoredPosition = new Vector2(0, 300);
        indicatorRect.sizeDelta = new Vector2(50, 50);

        // 5. 创建 IP 地址显示
        TextMeshProUGUI ipText = CreateTextTMP(panelObj, "IPAddressText", "IP: ---",
            new Vector2(0, 200), new Vector2(600, 60), 48, TextAlignmentOptions.Center);

        // 6. 创建端口显示
        TextMeshProUGUI portText = CreateTextTMP(panelObj, "PortText", "端口: ----",
            new Vector2(0, 130), new Vector2(600, 60), 48, TextAlignmentOptions.Center);

        // 7. 创建状态消息
        TextMeshProUGUI statusText = CreateTextTMP(panelObj, "StatusText", "未初始化",
            new Vector2(0, 40), new Vector2(800, 80), 36, TextAlignmentOptions.Center);

        // 8. 创建启用按钮
        Button enableBtn = CreateButton(panelObj, "EnableButton", "启用 ADB",
            new Vector2(-250, -100), new Vector2(200, 80), Color.green);

        // 9. 创建禁用按钮
        Button disableBtn = CreateButton(panelObj, "DisableButton", "禁用 ADB",
            new Vector2(0, -100), new Vector2(200, 80), Color.red);

        // 10. 创建刷新按钮
        Button refreshBtn = CreateButton(panelObj, "RefreshButton", "刷新状态",
            new Vector2(250, -100), new Vector2(200, 80), new Color(0.3f, 0.5f, 0.8f));

        // 11. 创建 Panel2 (应用列表面板)
        GameObject panel2Obj = new GameObject("Panel2");
        panel2Obj.transform.SetParent(canvasObj.transform, false);

        Image panel2Image = panel2Obj.AddComponent<Image>();
        panel2Image.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        RectTransform panel2Rect = panel2Obj.GetComponent<RectTransform>();
        panel2Rect.anchorMin = new Vector2(0, 0);
        panel2Rect.anchorMax = new Vector2(1, 1);
        panel2Rect.offsetMin = Vector2.zero;
        panel2Rect.offsetMax = Vector2.zero;

        // Panel2 标题
        CreateTextTMP(panel2Obj, "Panel2Title", "已安装应用列表",
            new Vector2(0, 450), new Vector2(800, 80), 56, TextAlignmentOptions.Center);

        // 刷新应用列表按钮
        Button refreshAppsBtn = CreateButton(panel2Obj, "RefreshAppsButton", "刷新应用列表",
            new Vector2(-300, 360), new Vector2(250, 70), new Color(0.2f, 0.6f, 0.3f));

        // 应用计数文本
        TextMeshProUGUI appCountText = CreateTextTMP(panel2Obj, "AppCountText", "应用数量: 0",
            new Vector2(200, 360), new Vector2(400, 60), 36, TextAlignmentOptions.Center);

        // 创建 app_list 滚动视图
        GameObject appListObj = CreateScrollableAppList(panel2Obj);

        // 返回按钮（切换到Panel1）
        Button backToPanel1Btn = CreateButton(panel2Obj, "BackToPanel1Button", "返回控制面板",
            new Vector2(0, -450), new Vector2(250, 70), new Color(0.5f, 0.5f, 0.5f));

        // 在 Panel 中添加切换按钮
        Button showAppsBtn = CreateButton(panelObj, "ShowAppsButton", "查看应用列表",
            new Vector2(0, -250), new Vector2(250, 70), new Color(0.4f, 0.4f, 0.7f));

        // 默认隐藏 Panel2
        panel2Obj.SetActive(false);

        // 12. 创建 ADB UI Controller 并关联组件
        GameObject controllerObj = new GameObject("ADB UI Controller");
        controllerObj.transform.SetParent(canvasObj.transform, false);

        ADBUIController controller = controllerObj.AddComponent<ADBUIController>();
        controller.enableButton = enableBtn;
        controller.disableButton = disableBtn;
        controller.refreshButton = refreshBtn;
        controller.ipAddressText = ipText;
        controller.portText = portText;
        controller.statusText = statusText;
        controller.statusIndicator = indicatorImage;
        controller.enabledColor = enabledColor;
        controller.disabledColor = disabledColor;
        controller.neutralColor = neutralColor;

        // 设置应用列表相关组件
        controller.panel1 = panelObj;
        controller.panel2 = panel2Obj;
        controller.showAppsButton = showAppsBtn;
        controller.backToPanel1Button = backToPanel1Btn;
        controller.refreshAppsButton = refreshAppsBtn;
        controller.appCountText = appCountText;
        controller.appListContent = appListObj.transform.Find("Viewport/Content")?.gameObject;
        controller.appListScrollRect = appListObj.GetComponent<ScrollRect>();

        // 13. 创建 ADB Manager
        GameObject managerObj = GameObject.Find("ADBManager");
        if (managerObj == null)
        {
            managerObj = new GameObject("ADBManager");
            managerObj.AddComponent<ADBManager>();
            Debug.Log("[ADBUISetup] 创建了 ADBManager GameObject");
        }

        isSetupComplete = true;
        Debug.Log("[ADBUISetup] ✅ ADB UI 创建完成！");
    }

    /// <summary>
    /// 创建 TextMeshPro 文本
    /// </summary>
    private TextMeshProUGUI CreateTextTMP(GameObject parent, string name, string text,
        Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent.transform, false);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        return tmp;
    }

    /// <summary>
    /// 创建按钮
    /// </summary>
    private Button CreateButton(GameObject parent, string name, string text,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent.transform, false);

        Image image = buttonObj.AddComponent<Image>();
        image.color = color;

        Button button = buttonObj.AddComponent<Button>();

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        // 创建按钮文本
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 32;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    /// <summary>
    /// 创建可滚动的应用列表
    /// </summary>
    private GameObject CreateScrollableAppList(GameObject parent)
    {
        // 创建 ScrollView 容器
        GameObject scrollViewObj = new GameObject("app_list");
        scrollViewObj.transform.SetParent(parent.transform, false);

        Image scrollBg = scrollViewObj.AddComponent<Image>();
        scrollBg.color = new Color(0.08f, 0.08f, 0.08f, 1f);

        RectTransform scrollRect = scrollViewObj.GetComponent<RectTransform>();
        scrollRect.anchoredPosition = new Vector2(0, -50);
        scrollRect.sizeDelta = new Vector2(1600, 700);

        ScrollRect scroll = scrollViewObj.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.1f;
        scroll.scrollSensitivity = 30f;

        // 添加 Mask
        Mask mask = scrollViewObj.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        // 创建 Viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);

        Image viewportImage = viewportObj.AddComponent<Image>();
        viewportImage.color = new Color(1, 1, 1, 0);

        RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        // 创建 Content 容器
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);

        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        // 添加 VerticalLayoutGroup
        UnityEngine.UI.VerticalLayoutGroup layoutGroup = contentObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.spacing = 10;
        layoutGroup.padding = new RectOffset(20, 20, 20, 20);

        // 添加 ContentSizeFitter
        UnityEngine.UI.ContentSizeFitter sizeFitter = contentObj.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        sizeFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        // 关联 ScrollRect
        scroll.viewport = viewportRect;
        scroll.content = contentRect;

        // 创建垂直滚动条
        GameObject scrollbarObj = new GameObject("Scrollbar");
        scrollbarObj.transform.SetParent(scrollViewObj.transform, false);

        Image scrollbarImage = scrollbarObj.AddComponent<Image>();
        scrollbarImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);

        Scrollbar scrollbar = scrollbarObj.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        RectTransform scrollbarRect = scrollbarObj.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1, 0);
        scrollbarRect.anchorMax = new Vector2(1, 1);
        scrollbarRect.pivot = new Vector2(1, 0.5f);
        scrollbarRect.anchoredPosition = new Vector2(0, 0);
        scrollbarRect.sizeDelta = new Vector2(20, 0);

        // 创建滚动条滑块
        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(scrollbarObj.transform, false);

        Image handleImage = handleObj.AddComponent<Image>();
        handleImage.color = new Color(0.6f, 0.6f, 0.6f, 1f);

        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = new Vector2(4, 4);
        handleRect.offsetMax = new Vector2(-4, -4);

        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        return scrollViewObj;
    }

    /// <summary>
    /// 清理已创建的 UI（用于重新设置）
    /// </summary>
    [ContextMenu("Clear ADB UI")]
    public void ClearADBUI()
    {
        Transform canvas = transform.Find("ADB Control Canvas");
        if (canvas != null)
        {
            DestroyImmediate(canvas.gameObject);
            isSetupComplete = false;
            Debug.Log("[ADBUISetup] UI 已清理");
        }
    }
}
