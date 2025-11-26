# oculus-wireless-adb

An app that enables wireless ADB from within a Meta Quest VR headset.

This is done through the Android global settings provider (requires manually granting `WRITE_SECURE_SETTINGS`).

Since the ADB TLS port is random each time, mDNS discovery is used in order to detect it within the app.

From there, you can either use that port (if your ADB client was already authorized), or you can enable the old TCP mode on port 5555 (requires a computer for first set up).

### Installation commands

```
adb install app-debug.apk
adb shell pm grant tdg.oculuswirelessadb android.permission.WRITE_SECURE_SETTINGS
```

- After ADB wireless is enabled, [a python script](script/) can be used on an authorized computer to automatically discover and connect to the device (without the need for tcpip mode).

- The `tcpip` mode, which allows unauthorized connections to come through with a on-device prompt, needs a computer to set up for the first time, so that the embedded ADB client can be allowed to enable the mode by itself in the future.
  
  * This can be achieved by running the command `adb tcpip 5555` from a computer with the Quest plugged in via USB, then using this app to activate ADB with the tcpip mode option checked.
    
  * Once the embedded client is authorized, subsequent activations should work without requiring a computer.

**Note:** When using an app like Termux on the Quest for the ADB client, beware that the ADB daemon (server) only starts if it's not *already running*, so this can cause conflicts with the embedded client on Oculus Wireless ADB (if tcpip mode is activated), and the Termux one, since the authorized keys will **only** be loaded by the client that starts the ADB deamon, which can cause connection issues.

The ADB daemon can be killed using `adb kill-server`, in order to spawn it again with the right ADB client/keys.

## Building from Source

### Prerequisites

1. **Android SDK** - Required for building the APK
   - Download: https://developer.android.com/studio/releases/platform-tools

2. **Java Development Kit (JDK)** - Version 8 or higher
   - Download: https://www.oracle.com/java/technologies/downloads/

3. **Keystore for Signing** - Required to sign the APK

### Step 1: Create Keystore (First Time Only)

Generate a keystore file to sign your APK:

**Windows:**
```bash
generate-keystore.bat
```

**Linux/Mac:**
```bash
keytool -genkeypair -v -keystore my-release-key.jks -keyalg RSA -keysize 2048 -validity 10000 -alias my-key-alias -storepass android -keypass android -dname "CN=Test, OU=Test, O=Test, L=Test, ST=Test, C=US"
```

This creates `my-release-key.jks` with default credentials:
- **Keystore password:** `android`
- **Key alias:** `my-key-alias`
- **Key password:** `android`

**⚠️ IMPORTANT:**
- Keep your keystore file safe
- **DO NOT commit it to Git** (it's in `.gitignore`)
- If lost, you cannot update previously signed APKs
- For production, use strong passwords

### Step 2: Configure Android SDK Path

Create `local.properties` in the project root:

```properties
sdk.dir=C\:\\Users\\YourUsername\\AppData\\Local\\Android\\Sdk
```

Replace with your actual Android SDK path.

### Step 3: Build the APK

```bash
# Clean and build
gradlew.bat clean assembleRelease

# The unsigned APK will be at:
# app/build/outputs/apk/release/app-release-unsigned.apk
```

### Step 4: Sign the APK

**Align the APK first:**
```bash
zipalign -v -p 4 app/build/outputs/apk/release/app-release-unsigned.apk app-aligned-unsigned.apk
```

**Sign with apksigner:**
```bash
apksigner sign --ks my-release-key.jks --ks-pass pass:android --key-pass pass:android --out app-release-signed.apk app-aligned-unsigned.apk
```

Note: `zipalign` and `apksigner` are in `Android SDK/build-tools/[version]/`

### Step 5: Install to Device

```bash
adb install -r app-release-signed.apk
adb shell pm grant tdg.oculuswirelessadb android.permission.WRITE_SECURE_SETTINGS
```

## Improvements in This Fork

- **Android 11+ Compatibility** - Fixed APK installation on devices running Android 11+ (API 30+)
- **Enhanced ADB Detection** - Automatic detection of ADB in PATH or local installation
- **ADB Auto-Installer** - Script to automatically download and install Android Platform Tools
- **Improved Error Handling** - Better error messages and debugging information

## Python Script Usage

The `script/` directory contains helper scripts for automatic device discovery:

### Auto-install ADB (Optional)
```bash
cd script
python install_adb.py
```

### Discover and Connect
```bash
cd script
python discover-and-connect.py
```

This will automatically find and connect to your Oculus device on the network.
