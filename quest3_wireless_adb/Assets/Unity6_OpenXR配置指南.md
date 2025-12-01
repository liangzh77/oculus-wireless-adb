# Unity 6 OpenXR 配置指南 for Quest 3

## ✅ OpenXR 配置步骤

### 步骤 1：启用 OpenXR

1. **Edit → Project Settings → XR Plug-in Management**

2. **切换到 Android 标签页**

3. **勾选 OpenXR**
   - ✅ OpenXR

4. 如果弹出警告或建议，点击 **Fix** 或 **Apply**

---

### 步骤 2：配置 OpenXR 功能

1. 在 **XR Plug-in Management** 下，找到并点击 **OpenXR**

2. 在 **Interaction Profiles** 部分，添加：
   - ✅ **Meta Quest Touch Pro Controller Profile**
   - ✅ **Oculus Touch Controller Profile**

3. 在 **Features** 部分，确认启用：
   - ✅ **Meta Quest Support**
   - ✅ **Hand Tracking Subsystem** (可选)
   - ✅ **Meta Quest Feature** (应该会自动添加)

---

### 步骤 3：设置渲染

1. **Edit → Project Settings → Player**

2. **Android 标签 → Other Settings**

3. **Graphics APIs**：
   - 移除 Vulkan（如果有的话）
   - 保留或添加：**OpenGLES3**
   - 顺序应该是：OpenGLES3（或 Vulkan 在前也可以）

4. **Color Space**：
   - 设置为 **Linear** (推荐)

---

### 步骤 4：配置 Stereo Rendering Mode

1. 在 **Project Settings → XR Plug-in Management → OpenXR → Android**

2. **Render Mode**：
   - 选择 **Multi Pass** 或 **Single Pass Instanced** (推荐 Single Pass)

---

### 步骤 5：检查场景设置

#### 方法 A：使用 XR Origin (推荐)

1. **删除默认的 Main Camera**

2. **GameObject → XR → XR Origin (Mobile AR/VR)**
   - 这会自动创建正确的 XR 摄像机设置

3. 确认 XR Origin 包含：
   - XR Origin (GameObject)
   - └─ Camera Offset
   -    └─ Main Camera (带 TrackedPoseDriver)

#### 方法 B：手动配置现有 Camera

如果场景中已有 Main Camera：

1. 选中 Main Camera

2. **Add Component → Tracked Pose Driver**
   - Tracking Type: **Rotation and Position**
   - Update Type: **Update and Before Render**

---

### 步骤 6：Build Settings 检查

1. **File → Build Settings**

2. **Texture Compression**:
   - 选择 **ASTC**

3. **Platform**:
   - 确认是 **Android**

---

## 🎯 完整配置清单

- [ ] XR Plug-in Management → Android → 勾选 OpenXR
- [ ] OpenXR → Interaction Profiles → 添加 Meta Quest 控制器
- [ ] OpenXR → Features → 启用 Meta Quest Support
- [ ] Player Settings → Graphics APIs → OpenGLES3
- [ ] 场景中有 XR Origin 或带 TrackedPoseDriver 的 Camera
- [ ] Build Settings → Texture Compression → ASTC

---

## 📝 测试配置是否正确

构建并运行后，在电脑上执行：

```bash
adb logcat -s Unity | grep XR
```

应该看到类似输出：
```
XRSettings: Enabled VR Devices: OpenXR
OpenXR: Successfully initialized
```

---

## 🐛 常见问题

### Q1: 没有 "XR Origin" 菜单选项

**解决方法：**
- 确认已安装 **XR Interaction Toolkit** 包
- Window → Package Manager → Unity Registry → 搜索 "XR Interaction Toolkit" → Install

### Q2: 应用启动黑屏

**可能原因：**
- Graphics API 不兼容
- 尝试改为 OpenGLES3

### Q3: 应用在 Quest 上仍然无法启动

**检查：**
- Project Settings → Player → Android → Other Settings
- **Minimum API Level**: 至少 Android 10.0 (API 29)
- **Target API Level**: Android 12.0 (API 31) 或更高

---

## 🚀 配置完成后的下一步

1. **保存场景和项目**
2. **File → Build Settings → Build And Run**
3. **等待构建完成**
4. **授予权限**：
   ```bash
   adb shell pm grant com.ChuJiao.quest3_wireless_adb android.permission.WRITE_SECURE_SETTINGS
   ```
5. **测试启动**

---

## 💡 Unity 6 与旧版本的区别

| 项目 | Unity 2021/2022 | Unity 6 |
|------|----------------|---------|
| XR 插件 | Oculus XR Plugin | OpenXR |
| 控制器 | OVR Input | XR Input / OpenXR |
| Camera | OVRCameraRig | XR Origin |
| SDK | Oculus Integration | Meta XR SDK |

Unity 6 使用更标准化的 OpenXR，这是正确的方向！
