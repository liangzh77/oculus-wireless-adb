# Oculus 无线 ADB

一个可以在 Meta Quest VR 头显内启用无线 ADB 的应用。

通过 Android 全局设置提供程序实现（需要手动授予 `WRITE_SECURE_SETTINGS` 权限）。

由于 ADB TLS 端口每次都是随机的，应用内使用 mDNS 发现机制来检测端口。

之后，你可以使用该端口（如果你的 ADB 客户端已被授权），或者启用旧的 TCP 模式（端口 5555，首次设置需要电脑）。

### 安装命令

```bash
adb install wireless_adb.apk
adb shell pm grant com.ChuJiao.quest3_wireless_adb android.permission.WRITE_SECURE_SETTINGS
```

- 启用无线 ADB 后，可以在已授权的电脑上使用 [Python 脚本](script/) 自动发现并连接设备（无需 tcpip 模式）。

- `tcpip` 模式允许未授权的连接通过设备上的提示进行授权，首次需要电脑设置，以便嵌入式 ADB 客户端能够自行启用该模式。

  * 通过 USB 连接 Quest 到电脑后运行命令 `adb tcpip 5555`，然后使用本应用勾选 tcpip 模式选项激活 ADB。

  * 一旦嵌入式客户端被授权，后续激活无需电脑即可完成。

**注意：** 在 Quest 上使用 Termux 等应用作为 ADB 客户端时，请注意 ADB 守护进程（服务器）仅在*尚未运行*时启动，这可能会导致与 Oculus Wireless ADB 的嵌入式客户端（如果激活了 tcpip 模式）和 Termux 客户端之间产生冲突，因为授权密钥**仅**由启动 ADB 守护进程的客户端加载，这可能导致连接问题。

可以使用 `adb kill-server` 命令终止 ADB 守护进程，以便使用正确的 ADB 客户端/密钥重新启动。

## 从源码构建

### 前置要求

1. **Android SDK** - 构建 APK 所需
   - 下载：https://developer.android.com/studio/releases/platform-tools

2. **Java 开发工具包 (JDK)** - 版本 8 或更高
   - 下载：https://www.oracle.com/java/technologies/downloads/

3. **签名密钥库** - 用于签名 APK

### 步骤 1：创建密钥库（仅首次需要）

生成用于签名 APK 的密钥库文件：

**Windows：**
```bash
generate-keystore.bat
```

**Linux/Mac：**
```bash
keytool -genkeypair -v -keystore my-release-key.jks -keyalg RSA -keysize 2048 -validity 10000 -alias my-key-alias -storepass android -keypass android -dname "CN=Test, OU=Test, O=Test, L=Test, ST=Test, C=US"
```

这将创建 `my-release-key.jks` 文件，默认凭据为：
- **密钥库密码：** `android`
- **密钥别名：** `my-key-alias`
- **密钥密码：** `android`

**⚠️ 重要提示：**
- 妥善保管密钥库文件
- **不要提交到 Git**（已在 `.gitignore` 中）
- 如果丢失，将无法更新之前签名的 APK
- 正式发布时请使用强密码

### 步骤 2：配置 Android SDK 路径

在项目根目录创建 `local.properties` 文件：

```properties
sdk.dir=C\:\\Users\\YourUsername\\AppData\\Local\\Android\\Sdk
```

替换为你实际的 Android SDK 路径。

### 步骤 3：构建 APK

```bash
# 清理并构建
gradlew.bat clean assembleRelease

# 未签名的 APK 位于：
# app/build/outputs/apk/release/app-release-unsigned.apk
```

### 步骤 4：签名 APK

**首先对齐 APK：**
```bash
zipalign -v -p 4 app/build/outputs/apk/release/app-release-unsigned.apk app-aligned-unsigned.apk
```

**使用 apksigner 签名：**
```bash
apksigner sign --ks my-release-key.jks --ks-pass pass:android --key-pass pass:android --out app-release-signed.apk app-aligned-unsigned.apk
```

注意：`zipalign` 和 `apksigner` 位于 `Android SDK/build-tools/[版本]/` 目录

### 步骤 5：安装到设备

```bash
adb install -r app-release-signed.apk
adb shell pm grant tdg.oculuswirelessadb android.permission.WRITE_SECURE_SETTINGS
```

## 本分支的改进

- **Android 11+ 兼容性** - 修复了 Android 11+（API 30+）设备上的 APK 安装问题
- **增强的 ADB 检测** - 自动检测 PATH 或本地安装的 ADB
- **ADB 自动安装器** - 自动下载和安装 Android Platform Tools 的脚本
- **改进的错误处理** - 更好的错误消息和调试信息

## Python 脚本使用

`script/` 目录包含用于自动设备发现的辅助脚本：

### 自动安装 ADB（可选）
```bash
cd script
python install_adb.py
```

### 发现并连接设备
```bash
cd script
python discover-and-connect.py
```

这将自动在网络中查找并连接到你的 Oculus 设备。
