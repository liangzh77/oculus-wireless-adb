# Quest 3 无线 ADB 集成指南

## ✅ 已完成的集成步骤

本项目已经完成了所有必要的文件集成，可以直接使用。

---

## 📁 已集成的文件

### C# 脚本（Assets/Scripts/）
- ✅ `ADBManager.cs` - ADB 核心管理器（单例模式）
- ✅ `ADBUIController.cs` - VR UI 控制器
- ✅ `ADBUISetup.cs` - UI 自动设置脚本（新增）

### Java 桥接类（Assets/Plugins/Android/java/tdg/oculuswirelessadb/）
- ✅ `UnityADBBridge.java` - Unity 与 Android 系统的桥梁
- ✅ `JmDNSAdbDiscoveryJava.java` - mDNS 服务发现

### 依赖库（Assets/Plugins/Android/libs/）
- ✅ `jmdns-3.5.8.jar` - mDNS 库
- ✅ `arm64-v8a/libadb.so` - ADB 二进制文件

### 配置文件
- ✅ `Assets/Plugins/Android/AndroidManifest.xml` - Android 权限配置

---

## 🚀 快速开始

### 方法一：使用自动 UI 设置脚本（推荐）

1. **在场景中创建 UI Setup GameObject**：
   - 打开场景 `Assets/Scenes/wireless_adb.unity`
   - 右键 Hierarchy → Create Empty
   - 命名为 "ADB UI Setup"
   - 附加脚本 `ADBUISetup.cs`

2. **自动创建 UI**：
   - 在 Inspector 中右键点击 `ADBUISetup` 组件
   - 选择 "Setup ADB UI"
   - 等待几秒，UI 会自动创建完成

3. **完成！**
   - 所有 UI 组件会自动创建并关联
   - ADBManager 也会自动创建

### 方法二：手动创建 UI

如果你想自定义 UI 布局，请参考下面的手动创建步骤。

#### 1. 创建 World Space Canvas

1. Hierarchy → 右键 → UI → Canvas
2. 命名为 "ADB Control Canvas"
3. 设置 Canvas 组件：
   - Render Mode: **World Space**
   - Position: (0, 1.5, 2)
   - Rotation: (0, 0, 0)
   - Scale: (0.001, 0.001, 0.001)
   - Width: 1920, Height: 1080

#### 2. 创建 UI 组件

在 Canvas 下创建以下 UI 元素：

**背景面板：**
- GameObject → UI → Image
- 命名为 "Panel"
- 颜色：深灰色半透明

**标题文本：**
- GameObject → UI → Text - TextMeshPro
- 命名为 "Title"
- Text: "无线 ADB 控制面板"
- Font Size: 72

**状态指示器：**
- GameObject → UI → Image
- 命名为 "StatusIndicator"
- 大小：50x50
- 颜色：灰色（初始状态）

**IP 地址显示：**
- GameObject → UI → Text - TextMeshPro
- 命名为 "IPAddressText"
- Text: "IP: ---"
- Font Size: 48

**端口显示：**
- GameObject → UI → Text - TextMeshPro
- 命名为 "PortText"
- Text: "端口: ----"
- Font Size: 48

**状态消息：**
- GameObject → UI → Text - TextMeshPro
- 命名为 "StatusText"
- Text: "未初始化"
- Font Size: 36

**启用按钮：**
- GameObject → UI → Button - TextMeshPro
- 命名为 "EnableButton"
- Text: "启用 ADB"
- 颜色：绿色

**禁用按钮：**
- GameObject → UI → Button - TextMeshPro
- 命名为 "DisableButton"
- Text: "禁用 ADB"
- 颜色：红色

**刷新按钮：**
- GameObject → UI → Button - TextMeshPro
- 命名为 "RefreshButton"
- Text: "刷新状态"

**tcpip 模式开关：**
- GameObject → UI → Toggle
- 命名为 "TcpipModeToggle"
- Label: "tcpip 5555 模式"

#### 3. 附加脚本并关联组件

1. **创建 ADBManager**：
   - Hierarchy → 右键 → Create Empty
   - 命名为 "ADBManager"
   - 附加脚本 `ADBManager.cs`

2. **创建 ADB UI Controller**：
   - 在 Canvas 下创建空 GameObject
   - 命名为 "ADB UI Controller"
   - 附加脚本 `ADBUIController.cs`
   - 在 Inspector 中拖拽所有 UI 组件到对应字段：
     - Enable Button → `enableButton`
     - Disable Button → `disableButton`
     - Refresh Button → `refreshButton`
     - IP Address Text → `ipAddressText`
     - Port Text → `portText`
     - Status Text → `statusText`
     - Status Indicator (Image) → `statusIndicator`
     - Tcpip Mode Toggle → `tcpipModeToggle`

---

## 🔨 构建和部署

### 1. Unity Build Settings

1. **File → Build Settings**
2. 设置：
   - Platform: **Android**
   - Texture Compression: **ASTC**
   - Run Device: 选择你的 Quest 3

### 2. Player Settings

1. **Edit → Project Settings → Player**
2. **Other Settings**：
   - Package Name: `com.tdg.quest3wirelessadb`（必须与 AndroidManifest 一致）
   - Minimum API Level: **Android 10.0 (API level 29)** 或更高
   - Scripting Backend: **IL2CPP**
   - Target Architectures: **ARM64** （只勾选这个）

3. **Publishing Settings**：
   - 创建或选择 Keystore
   - 设置 Keystore 密码

### 3. 构建 APK

1. **File → Build Settings → Build**
2. 选择保存位置
3. 等待构建完成

### 4. 安装到 Quest 3

可以通过以下方式安装：

**方法 1: Unity 直接安装**
- Build Settings → Build And Run

**方法 2: SideQuest**
- 使用 SideQuest 安装生成的 APK

**方法 3: ADB 命令行**
```bash
adb install quest3_wireless_adb.apk
```

---

## 🔐 授予权限（重要！）

安装完成后，**必须**通过 ADB 授予 `WRITE_SECURE_SETTINGS` 权限：

```bash
# 1. 连接 Quest 3 到电脑
adb devices

# 2. 授予权限
adb shell pm grant com.tdg.quest3wirelessadb android.permission.WRITE_SECURE_SETTINGS

# 3. 验证权限
adb shell dumpsys package com.tdg.quest3wirelessadb | grep WRITE_SECURE_SETTINGS
```

**如果不授予此权限，应用无法控制无线 ADB！**

---

## 📖 使用说明

### 在 VR 中使用

1. **启动应用**：
   - 在 Quest 3 上打开应用
   - 你会看到 ADB 控制面板浮现在眼前

2. **启用无线 ADB**：
   - 用控制器指向 "启用 ADB" 按钮
   - 按下扳机键点击按钮
   - 等待 5-10 秒，IP 地址和端口会自动显示
   - 状态指示器变为绿色

3. **连接到电脑**：
   - 记下显示的 IP 和端口（例如 `192.168.1.100:37893`）
   - 在电脑终端执行：
     ```bash
     adb connect 192.168.1.100:37893
     ```

4. **禁用 ADB**：
   - 点击 "禁用 ADB" 按钮
   - 状态指示器变为红色

5. **tcpip 5555 模式**（可选）：
   - 勾选 "tcpip 5555 模式"
   - 点击 "启用 ADB"
   - 成功后会同时显示主端口和 5555 端口

### 代码调用示例

如果你想在其他脚本中控制 ADB：

```csharp
// 启用 ADB
ADBManager.Instance.EnableADB(false);

// 启用 ADB（带 tcpip 模式）
ADBManager.Instance.EnableADB(true);

// 禁用 ADB
ADBManager.Instance.DisableADB();

// 刷新状态
ADBManager.Instance.UpdateStatus();

// 获取状态
bool isEnabled = ADBManager.Instance.IsADBEnabled;
string ip = ADBManager.Instance.CurrentIP;
int port = ADBManager.Instance.CurrentPort;

// 订阅事件
ADBManager.Instance.OnADBStatusChanged += (enabled) => {
    Debug.Log($"ADB {(enabled ? "已启用" : "已禁用")}");
};

ADBManager.Instance.OnConnectionInfoUpdated += (ip, port) => {
    Debug.Log($"ADB 地址: {ip}:{port}");
};
```

---

## 🐛 常见问题

### Q1: 应用闪退或无法启动

**检查项：**
1. 确认 `libadb.so` 在 `Assets/Plugins/Android/libs/arm64-v8a/` 目录
2. 确认 `jmdns-3.5.8.jar` 在 `Assets/Plugins/Android/libs/` 目录
3. 确认所有 Java 文件在正确的包路径：`tdg.oculuswirelessadb`
4. 查看 Unity Logcat 日志：`adb logcat | grep Unity`

### Q2: UI 显示 "缺少权限"

**解决方法：**
```bash
adb shell pm grant com.tdg.quest3wirelessadb android.permission.WRITE_SECURE_SETTINGS
```

### Q3: 点击 "启用 ADB" 后没有反应

**可能原因：**
1. 权限未授予 → 查看状态消息
2. Quest 未连接 WiFi → 确保连接到 WiFi 网络
3. ADB 服务启动慢 → 等待 15 秒后点击"刷新状态"

### Q4: 无法发现 IP 地址（显示为空）

**解决方法：**
1. 确认 Quest 已连接到 WiFi（不是仅热点模式）
2. 检查防火墙是否阻止 mDNS（UDP 端口 5353）
3. 点击 "刷新状态" 按钮重试
4. 重启应用

### Q5: Unity Editor 中无法测试

**说明：**
- ADB 功能**必须在真实 Quest 设备上测试**
- Editor 中会显示 "编辑器模式 - ADB 功能禁用"
- 这是正常的，因为 Android 功能只能在设备上运行

### Q6: 如何查看详细日志

```bash
# 过滤 ADB 相关日志
adb logcat | grep -E "UnityADBBridge|JmDNS|ADBManager"

# 查看 Unity 日志
adb logcat -s Unity

# 查看所有日志
adb logcat
```

---

## 📊 项目结构

```
quest3_wireless_adb/
├── Assets/
│   ├── Scripts/
│   │   ├── ADBManager.cs              ← ADB 核心管理器
│   │   ├── ADBUIController.cs         ← UI 控制器
│   │   └── ADBUISetup.cs              ← UI 自动设置脚本
│   │
│   ├── Plugins/
│   │   └── Android/
│   │       ├── AndroidManifest.xml    ← 权限配置
│   │       ├── libs/
│   │       │   ├── jmdns-3.5.8.jar   ← mDNS 库
│   │       │   └── arm64-v8a/
│   │       │       └── libadb.so      ← ADB 二进制
│   │       │
│   │       └── java/
│   │           └── tdg/
│   │               └── oculuswirelessadb/
│   │                   ├── UnityADBBridge.java         ← Unity 桥接
│   │                   └── JmDNSAdbDiscoveryJava.java  ← mDNS 发现
│   │
│   └── Scenes/
│       └── wireless_adb.unity         ← 主场景
```

---

## ⚠️ 重要提醒

### 安全注意事项

1. **仅用于开发/调试**：
   - 无线 ADB 会降低设备安全性
   - 请勿在发布版本中包含此功能

2. **受信任网络**：
   - 只在受信任的 WiFi 网络中启用 ADB
   - 避免在公共 WiFi 使用

3. **移除生产代码**：
   - 发布前移除 ADB 相关代码
   - 或添加开发者模式开关

### 设备要求

- ✅ Quest 3 或 Quest Pro
- ✅ Android 10 (API 29) 或更高版本
- ✅ 已连接到 WiFi 网络
- ✅ 已授予 `WRITE_SECURE_SETTINGS` 权限

---

## 🎯 功能清单

集成完成后，你的应用支持：

- [x] VR 中通过按钮启用/禁用无线 ADB
- [x] 自动发现并显示 IP 地址和端口
- [x] 支持 tcpip 5555 模式
- [x] 实时状态更新和刷新
- [x] 权限检查和提示
- [x] VR 友好的 UI 界面
- [x] 事件驱动的状态通知
- [x] 代码调用接口
- [x] 自动 UI 设置脚本

---

## 📚 更多资源

- **原项目**: [oculus-wireless-adb](../README.md)
- **完整集成指南**: [UNITY_INTEGRATION_GUIDE.md](../UNITY_INTEGRATION_GUIDE.md)
- **文件清单**: [UNITY_FILES_CHECKLIST.md](../UNITY_FILES_CHECKLIST.md)

---

## 📝 版本历史

### v1.0 (2025-01-27)
- ✅ 初始集成完成
- ✅ 所有必要文件已集成
- ✅ 添加了自动 UI 设置脚本
- ✅ 提供完整的使用文档

---

**祝你开发顺利！🎮**

如有问题，请查看原项目的集成文档或提交 Issue。
