using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// VR手柄UI交互 - 使用物理射线检测与World Space Canvas交互
/// </summary>
public class VRUIInput : MonoBehaviour
{
    [Header("射线设置")]
    public float rayLength = 20f;  // 增加射线长度

    [Header("视觉反馈")]
    public LineRenderer lineRenderer;
    public Color normalColor = Color.white;
    public Color hoverColor = Color.green;

    private InputDevice rightController;
    private InputDevice leftController;
    private bool wasPressed = false;
    private Button lastHoveredButton;
    private Toggle lastHoveredToggle;

    // 射线拖动滚动相关
    private ScrollRect currentScrollRect;
    private bool isDraggingScroll = false;
    private Vector3 lastDragPoint;
    private float dragScrollSpeed = 0.003f;  // 拖动滚动灵敏度

    void Awake()
    {
        Debug.Log("[VRUIInput] Awake called on " + gameObject.name);
    }

    void Start()
    {
        Debug.Log("[VRUIInput] Start called on " + gameObject.name);
        Debug.Log("[VRUIInput] Transform position: " + transform.position);

        // 创建射线可视化
        if (lineRenderer == null)
        {
            Debug.Log("[VRUIInput] Creating LineRenderer");
            GameObject lineObj = new GameObject("ControllerRay");
            lineObj.transform.SetParent(transform);
            lineRenderer = lineObj.AddComponent<LineRenderer>();
            lineRenderer.startWidth = 0.005f;
            lineRenderer.endWidth = 0.002f;

            // 使用 URP 兼容的 shader
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            if (shader != null)
            {
                lineRenderer.material = new Material(shader);
                Debug.Log("[VRUIInput] LineRenderer shader: " + shader.name);
            }
            else
            {
                Debug.LogWarning("[VRUIInput] Could not find suitable shader for LineRenderer");
            }

            lineRenderer.startColor = normalColor;
            lineRenderer.endColor = normalColor;
            lineRenderer.positionCount = 2;
            Debug.Log("[VRUIInput] LineRenderer created");
        }

        // 确保所有UI按钮有碰撞体
        SetupUIColliders();

        InitializeControllers();
    }

    void SetupUIColliders()
    {
        Debug.Log("[VRUIInput] SetupUIColliders called");

        // 为所有Button添加碰撞体
        Button[] buttons = FindObjectsOfType<Button>(true);
        Debug.Log($"[VRUIInput] Found {buttons.Length} buttons");

        foreach (var button in buttons)
        {
            if (button.GetComponent<Collider>() == null)
            {
                BoxCollider col = button.gameObject.AddComponent<BoxCollider>();
                RectTransform rt = button.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // 使用本地尺寸，加上厚度让射线更容易命中
                    col.size = new Vector3(rt.rect.width, rt.rect.height, 50f);
                    col.center = Vector3.zero;

                    Debug.Log($"[VRUIInput] Added collider to button: {button.name}, size={col.size}, world pos={button.transform.position}");
                }
            }
        }

        // 为所有Toggle添加碰撞体
        Toggle[] toggles = FindObjectsOfType<Toggle>(true);
        Debug.Log($"[VRUIInput] Found {toggles.Length} toggles");

        foreach (var toggle in toggles)
        {
            if (toggle.GetComponent<Collider>() == null)
            {
                BoxCollider col = toggle.gameObject.AddComponent<BoxCollider>();
                RectTransform rt = toggle.GetComponent<RectTransform>();
                if (rt != null)
                {
                    col.size = new Vector3(rt.rect.width, rt.rect.height, 50f);
                    col.center = Vector3.zero;

                    Debug.Log($"[VRUIInput] Added collider to toggle: {toggle.name}, size={col.size}, world pos={toggle.transform.position}");
                }
            }
        }

        // 为所有 ScrollRect 添加碰撞体
        ScrollRect[] scrollRects = FindObjectsOfType<ScrollRect>(true);
        Debug.Log($"[VRUIInput] Found {scrollRects.Length} ScrollRects");

        foreach (var scrollRect in scrollRects)
        {
            if (scrollRect.GetComponent<Collider>() == null)
            {
                BoxCollider col = scrollRect.gameObject.AddComponent<BoxCollider>();
                RectTransform rt = scrollRect.GetComponent<RectTransform>();
                if (rt != null)
                {
                    col.size = new Vector3(rt.rect.width, rt.rect.height, 50f);
                    col.center = Vector3.zero;

                    Debug.Log($"[VRUIInput] Added collider to ScrollRect: {scrollRect.name}, size={col.size}, world pos={scrollRect.transform.position}");
                }
            }
        }

        // 输出 Canvas 信息用于调试
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        foreach (var canvas in canvases)
        {
            Debug.Log($"[VRUIInput] Canvas: {canvas.name}, renderMode={canvas.renderMode}, scale={canvas.transform.localScale}, pos={canvas.transform.position}");
        }
    }

    void InitializeControllers()
    {
        var rightHandDevices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightHandDevices);
        if (rightHandDevices.Count > 0)
        {
            rightController = rightHandDevices[0];
            Debug.Log("[VRUIInput] Right controller found: " + rightController.name);
        }

        var leftHandDevices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftHandDevices);
        if (leftHandDevices.Count > 0)
        {
            leftController = leftHandDevices[0];
            Debug.Log("[VRUIInput] Left controller found: " + leftController.name);
        }
    }

    private int frameCount = 0;

    void Update()
    {
        frameCount++;
        // 每300帧（约5秒）输出一次调试信息
        if (frameCount % 300 == 0)
        {
            Debug.Log($"[VRUIInput] Update running, frame {frameCount}, rightController.isValid={rightController.isValid}, leftController.isValid={leftController.isValid}");
        }

        // 重新检测控制器
        if (!rightController.isValid && !leftController.isValid)
        {
            InitializeControllers();
        }

        // 获取相机
        Camera vrCamera = Camera.main;
        if (vrCamera == null)
        {
            if (frameCount % 300 == 0)
            {
                Debug.LogWarning("[VRUIInput] Camera.main is null!");
            }
            return;
        }

        Transform xrOrigin = vrCamera.transform.parent;

        // 获取射线起点和方向 - 默认使用头部
        Vector3 rayOrigin = vrCamera.transform.position;
        Vector3 rayDirection = vrCamera.transform.forward;

        // 从右手控制器获取位置和方向
        if (rightController.isValid)
        {
            if (rightController.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
            {
                rayOrigin = xrOrigin != null ? xrOrigin.TransformPoint(pos) : pos;
            }
            if (rightController.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
            {
                Quaternion worldRot = xrOrigin != null ? xrOrigin.rotation * rot : rot;
                // Quest手柄握持时需要调整角度让射线指向前方
                rayDirection = worldRot * Quaternion.Euler(70f, 0f, 0f) * Vector3.forward;
            }
        }

        // 更新射线可视化
        Vector3 rayEnd = rayOrigin + rayDirection * rayLength;
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, rayOrigin);
            lineRenderer.SetPosition(1, rayEnd);
        }

        // 检测扳机按下 - 同时检测trigger值
        bool isPressed = false;
        float triggerValue = 0f;

        if (rightController.isValid)
        {
            rightController.TryGetFeatureValue(CommonUsages.triggerButton, out isPressed);
            rightController.TryGetFeatureValue(CommonUsages.trigger, out triggerValue);
            if (triggerValue > 0.5f) isPressed = true;
        }
        if (!isPressed && leftController.isValid)
        {
            leftController.TryGetFeatureValue(CommonUsages.triggerButton, out isPressed);
            leftController.TryGetFeatureValue(CommonUsages.trigger, out triggerValue);
            if (triggerValue > 0.5f) isPressed = true;
        }

        // 当扳机状态变化时输出调试信息
        if (isPressed != wasPressed)
        {
            Debug.Log($"[VRUIInput] Trigger state changed: isPressed={isPressed}, triggerValue={triggerValue}");
            // 当按下扳机时输出射线信息
            if (isPressed)
            {
                Debug.Log($"[VRUIInput] Ray origin={rayOrigin}, direction={rayDirection}");
            }
        }

        // 每300帧输出射线信息
        if (frameCount % 300 == 0)
        {
            Debug.Log($"[VRUIInput] Ray: origin={rayOrigin}, direction={rayDirection}, length={rayLength}");
        }

        // 物理射线检测 - 使用 RaycastAll 获取所有命中的碰撞体
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, rayDirection, rayLength);
        Button hoveredButton = null;
        Toggle hoveredToggle = null;
        ScrollRect hoveredScrollRect = null;
        RaycastHit closestHit = default;
        float closestDistance = float.MaxValue;

        // 遍历所有命中，优先查找 Button 和 Toggle
        foreach (var hit in hits)
        {
            // 先找 Button
            Button btn = hit.collider.GetComponent<Button>();
            if (btn == null)
                btn = hit.collider.GetComponentInParent<Button>();

            if (btn != null && btn.interactable)
            {
                hoveredButton = btn;
                closestHit = hit;
                break; // Button 优先级最高
            }

            // 再找 Toggle
            Toggle tog = hit.collider.GetComponent<Toggle>();
            if (tog == null)
                tog = hit.collider.GetComponentInParent<Toggle>();

            if (tog != null && tog.interactable)
            {
                hoveredToggle = tog;
                closestHit = hit;
                break; // Toggle 次优先
            }

            // 记录最近的 ScrollRect
            ScrollRect sr = hit.collider.GetComponent<ScrollRect>();
            if (sr == null)
                sr = hit.collider.GetComponentInParent<ScrollRect>();

            if (sr != null && hit.distance < closestDistance)
            {
                hoveredScrollRect = sr;
                closestHit = hit;
                closestDistance = hit.distance;
            }
        }

        // 每300帧输出一次命中信息
        if (frameCount % 300 == 0 && hits.Length > 0)
        {
            Debug.Log($"[VRUIInput] RaycastAll hit {hits.Length} objects, first: {hits[0].collider.gameObject.name}");
            if (hoveredButton != null)
                Debug.Log($"[VRUIInput] Found button: {hoveredButton.name}");
        }

        if (hits.Length > 0)
        {
            if (hoveredButton != null || hoveredToggle != null)
            {
                // 更新射线颜色和终点
                if (lineRenderer != null)
                {
                    lineRenderer.startColor = hoverColor;
                    lineRenderer.endColor = hoverColor;
                    lineRenderer.SetPosition(1, closestHit.point);
                }

                // 每300帧输出一次悬停信息
                if (frameCount % 300 == 0)
                {
                    if (hoveredButton != null)
                        Debug.Log($"[VRUIInput] Hovering over button: {hoveredButton.name}, interactable={hoveredButton.interactable}");
                    if (hoveredToggle != null)
                        Debug.Log($"[VRUIInput] Hovering over toggle: {hoveredToggle.name}, interactable={hoveredToggle.interactable}");
                }

                // 扳机按下时点击
                if (isPressed && !wasPressed)
                {
                    Debug.Log($"[VRUIInput] Trigger pressed while hovering!");
                    if (hoveredButton != null && hoveredButton.interactable)
                    {
                        Debug.Log($"[VRUIInput] Button clicked: {hoveredButton.name}");
                        hoveredButton.onClick.Invoke();
                    }
                    else if (hoveredToggle != null && hoveredToggle.interactable)
                    {
                        Debug.Log($"[VRUIInput] Toggle clicked: {hoveredToggle.name}");
                        hoveredToggle.isOn = !hoveredToggle.isOn;
                    }
                }
            }
            else if (hoveredScrollRect != null)
            {
                // 悬停在 ScrollRect 上
                if (lineRenderer != null)
                {
                    lineRenderer.startColor = hoverColor;
                    lineRenderer.endColor = hoverColor;
                    lineRenderer.SetPosition(1, closestHit.point);
                }

                // 处理拖动滚动
                if (isPressed)
                {
                    if (!isDraggingScroll)
                    {
                        // 开始拖动
                        isDraggingScroll = true;
                        currentScrollRect = hoveredScrollRect;
                        lastDragPoint = closestHit.point;
                        Debug.Log($"[VRUIInput] Started dragging ScrollRect: {hoveredScrollRect.name}");
                    }
                    else if (currentScrollRect == hoveredScrollRect)
                    {
                        // 继续拖动 - 计算拖动距离
                        Vector3 dragDelta = closestHit.point - lastDragPoint;

                        // 使用垂直方向的移动来滚动（在世界空间中是 Y 轴）
                        // 但需要考虑 Canvas 的朝向，所以用本地空间的 Y
                        float scrollDelta = Vector3.Dot(dragDelta, hoveredScrollRect.transform.up) * dragScrollSpeed;

                        if (Mathf.Abs(scrollDelta) > 0.0001f)
                        {
                            float newPos = currentScrollRect.verticalNormalizedPosition + scrollDelta;
                            currentScrollRect.verticalNormalizedPosition = Mathf.Clamp01(newPos);
                        }

                        lastDragPoint = closestHit.point;
                    }
                }
            }
            else
            {
                // 命中其他物体
                if (lineRenderer != null)
                {
                    lineRenderer.startColor = normalColor;
                    lineRenderer.endColor = normalColor;
                    lineRenderer.SetPosition(1, closestHit.point);
                }
            }
        }
        else
        {
            // 没命中任何东西
            if (lineRenderer != null)
            {
                lineRenderer.startColor = normalColor;
                lineRenderer.endColor = normalColor;
            }
        }

        // 松开扳机时停止拖动
        if (!isPressed && isDraggingScroll)
        {
            Debug.Log("[VRUIInput] Stopped dragging ScrollRect");
            isDraggingScroll = false;
            currentScrollRect = null;
        }

        wasPressed = isPressed;
        lastHoveredButton = hoveredButton;
        lastHoveredToggle = hoveredToggle;
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
        Debug.Log($"[VRUIInput] Device connected: {device.name}");
        InitializeControllers();
    }
}
