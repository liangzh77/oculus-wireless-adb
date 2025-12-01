# Unity 集成文件

本目录包含将 Oculus Wireless ADB 集成到 Unity Quest 3 项目所需的所有文件。

## 目录结构

```
UnityScripts/
├── README.md                          # 本文件
├── ADBManager.cs                      # Unity ADB 核心管理器（C#）
├── ADBUIController.cs                 # Unity VR UI 控制器（C#）
│
└── Plugins/
    └── Android/
        ├── AndroidManifest.xml        # Android 权限配置
        │
        └── java/
            └── tdg/
                └── oculuswirelessadb/
                    ├── UnityADBBridge.java          # Unity-Android 桥接类
                    └── JmDNSAdbDiscoveryJava.java   # mDNS 服务发现（Java版）
```

## 使用说明

### 快速开始

1. **将整个 `UnityScripts` 目录复制到你的 Unity 项目的 `Assets/` 目录下**

2. **重命名目录**（可选）：
   ```
   Assets/UnityScripts/  →  Assets/ADBPlugin/
   ```

3. **添加依赖库**：
   - 下载 `jmdns-3.5.8.jar`
   - 放到 `Assets/Plugins/Android/libs/jmdns-3.5.8.jar`

4. **添加 ADB 二进制**：
   - 从本项目的 `app/src/main/jniLibs/arm64-v8a/libadb.so` 复制
   - 放到 `Assets/Plugins/Android/libs/arm64-v8a/libadb.so`

5. **创建 VR UI**（参见完整集成指南）

6. **构建并授权权限**：
   ```bash
   adb shell pm grant <你的包名> android.permission.WRITE_SECURE_SETTINGS
   ```

## 文件说明

### C# 脚本

#### `ADBManager.cs`
- **单例模式**的 ADB 管理器
- 负责与 Android Java 层通信
- 提供事件通知机制
- **使用位置**: 附加到场景中的 GameObject

#### `ADBUIController.cs`
- VR UI 界面控制器
- 处理按钮点击和状态显示
- 订阅 ADBManager 的事件
- **使用位置**: 附加到 Canvas 或 UI GameObject

### Java 类

#### `UnityADBBridge.java`
- Unity 和 Android 系统的桥梁
- 控制 Android 的无线 ADB 设置
- 调用 mDNS 发现 ADB 服务
- 执行 ADB 命令（tcpip 模式）

#### `JmDNSAdbDiscoveryJava.java`
- 纯 Java 实现的 mDNS 服务发现
- 不依赖 Kotlin 或协程
- 监听 ADB 服务广播并返回 IP 和端口

### 配置文件

#### `AndroidManifest.xml`
- 包含所需的 Android 权限：
  - `INTERNET`
  - `ACCESS_WIFI_STATE`
  - `CHANGE_WIFI_MULTICAST_STATE`
  - `WRITE_SECURE_SETTINGS`（需要 ADB 授权）

## 完整集成指南

详细的集成步骤、UI 设置、使用方法和常见问题，请查看：

📖 **[UNITY_INTEGRATION_GUIDE.md](../UNITY_INTEGRATION_GUIDE.md)**

## 依赖项

### 必需库

1. **jmdns-3.5.8.jar**
   - 下载地址: https://github.com/jmdns/jmdns/releases
   - 或 Maven: https://repo1.maven.org/maven2/org/jmdns/jmdns/3.5.8/jmdns-3.5.8.jar

2. **libadb.so** (ARM64)
   - 从本项目构建获取：`app/src/main/jniLibs/arm64-v8a/libadb.so`

### Unity 要求

- Unity 2020.3 或更高版本
- Android SDK API Level 29 或更高
- IL2CPP 脚本后端
- ARM64 架构

## 主要功能

✅ 在 VR 中通过按钮启用/禁用无线 ADB
✅ 自动发现并显示 ADB 的 IP 地址和端口
✅ 支持 tcpip 5555 模式
✅ 实时状态更新和事件通知
✅ 权限检查和提示
✅ VR 友好的 UI 界面

## 代码示例

### 启用 ADB

```csharp
// 获取单例
ADBManager manager = ADBManager.Instance;

// 启用 ADB
manager.EnableADB(false);

// 启用 ADB（带 tcpip 模式）
manager.EnableADB(true);
```

### 订阅状态变化

```csharp
ADBManager.Instance.OnADBStatusChanged += (enabled) => {
    Debug.Log($"ADB is {(enabled ? "ON" : "OFF")}");
};

ADBManager.Instance.OnConnectionInfoUpdated += (ip, port) => {
    Debug.Log($"Connect with: adb connect {ip}:{port}");
};
```

### 获取当前状态

```csharp
bool isEnabled = ADBManager.Instance.IsADBEnabled;
string ip = ADBManager.Instance.CurrentIP;
int port = ADBManager.Instance.CurrentPort;
string status = ADBManager.Instance.StatusMessage;
```

## 注意事项

⚠️ **安全提醒**:
- 仅在开发/调试时使用
- 不要在发布版本中包含此功能
- 只在受信任的网络中启用 ADB

⚠️ **权限要求**:
- 必须通过 ADB 授予 `WRITE_SECURE_SETTINGS` 权限
- 应用才能控制无线 ADB 设置

⚠️ **设备要求**:
- Quest 必须连接到 WiFi 网络
- 不支持仅通过热点模式

## 故障排除

### 应用闪退
- 检查所有 Java 文件的包路径是否正确
- 确保 jmdns 和 libadb.so 已正确放置
- 查看 Unity Logcat 日志

### 无法启用 ADB
- 确认已授予 `WRITE_SECURE_SETTINGS` 权限
- 检查 Quest 是否已连接 WiFi
- 查看状态消息和日志

### 无法发现 IP/端口
- 等待 10-15 秒让服务完全启动
- 点击刷新按钮
- 检查防火墙是否阻止 mDNS（UDP 5353）
- 确认 `CHANGE_WIFI_MULTICAST_STATE` 权限已添加

## 更多帮助

详细的故障排除和调试方法，请参考：

📖 **[完整集成指南](../UNITY_INTEGRATION_GUIDE.md)** - 包含详细的步骤、UI 设置、常见问题等

---

**Happy Coding! 🎮**
