using UnityEngine;

/// <summary>
/// ADB 广播监听器 - 独立于 UI，始终运行
/// 将此脚本放在不会被 disable 的 GameObject 上
/// </summary>
public class ADBBroadcastListener : MonoBehaviour
{
    private static ADBBroadcastListener instance;
    public static ADBBroadcastListener Instance => instance;

    private bool isReceiverRegistered = false;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject intentReceiver;
    private AndroidJavaObject unityActivity;
#endif

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[ADBBroadcastListener] Awake - Registering receiver immediately");

        // 立即注册 BroadcastReceiver，不等到 Start()
        RegisterIntentReceiver();
    }

    void Start()
    {
        Debug.Log("[ADBBroadcastListener] Start");

        // 双重保险：如果 Awake 中注册失败，在 Start 中再试一次
        if (!isReceiverRegistered)
        {
            Debug.Log("[ADBBroadcastListener] Receiver not registered in Awake, trying again in Start");
            RegisterIntentReceiver();
        }
    }

    void OnDestroy()
    {
        UnregisterIntentReceiver();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            Debug.Log("[ADBBroadcastListener] App resumed, re-registering receiver");
            UnregisterIntentReceiver();
            RegisterIntentReceiver();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            Debug.Log("[ADBBroadcastListener] App focused, re-registering receiver");
            UnregisterIntentReceiver();
            RegisterIntentReceiver();
        }
    }

    private void RegisterIntentReceiver()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (isReceiverRegistered)
        {
            Debug.Log("[ADBBroadcastListener] Receiver already registered");
            return;
        }

        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            // 创建 BroadcastReceiver
            intentReceiver = new AndroidJavaObject("tdg.oculuswirelessadb.ADBCommandBroadcastReceiver");

            // 创建 IntentFilter
            using (AndroidJavaObject intentFilter = new AndroidJavaObject("android.content.IntentFilter"))
            {
                intentFilter.Call("addAction", "com.ChuJiao.quest3_wireless_adb.PLAY_SOUND");
                intentFilter.Call("addAction", "com.ChuJiao.quest3_wireless_adb.VIBRATE");
                intentFilter.Call("addAction", "com.ChuJiao.quest3_wireless_adb.FIND_DEVICE");

                // 注册 receiver
                unityActivity.Call<AndroidJavaObject>("registerReceiver", intentReceiver, intentFilter);
                isReceiverRegistered = true;
                Debug.Log("[ADBBroadcastListener] Intent receiver registered successfully");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ADBBroadcastListener] Failed to register intent receiver: " + e.Message);
        }
#endif
    }

    private void UnregisterIntentReceiver()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isReceiverRegistered)
        {
            return;
        }

        try
        {
            if (unityActivity != null && intentReceiver != null)
            {
                unityActivity.Call("unregisterReceiver", intentReceiver);
                Debug.Log("[ADBBroadcastListener] Intent receiver unregistered");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ADBBroadcastListener] Failed to unregister intent receiver: " + e.Message);
        }
        isReceiverRegistered = false;
#endif
    }

    /// <summary>
    /// 从 Java 端调用的方法（通过 UnitySendMessage）
    /// </summary>
    public void OnCommandReceived(string command)
    {
        Debug.Log($"[ADBBroadcastListener] Command received: {command}");

        // 转发给 ADBCommandReceiver 执行
        ADBCommandReceiver receiver = ADBCommandReceiver.Instance;
        if (receiver != null)
        {
            receiver.OnCommandReceived(command);
        }
        else
        {
            Debug.LogWarning("[ADBBroadcastListener] ADBCommandReceiver not found, executing directly");
            ExecuteCommand(command);
        }
    }

    /// <summary>
    /// 直接执行命令（当 ADBCommandReceiver 不可用时）
    /// </summary>
    private void ExecuteCommand(string command)
    {
        switch (command)
        {
            case "PLAY_SOUND":
                PlayDefaultSound();
                break;
            case "VIBRATE":
                VibrateControllers();
                break;
            case "FIND_DEVICE":
                StartCoroutine(FindDeviceCoroutine());
                break;
        }
    }

    private void PlayDefaultSound()
    {
        // 创建临时 AudioSource 播放声音
        AudioSource tempAudio = gameObject.AddComponent<AudioSource>();

        int sampleRate = 44100;
        float frequency = 880f;
        float duration = 0.5f;
        int sampleCount = (int)(sampleRate * duration);

        AudioClip clip = AudioClip.Create("Beep", sampleCount, 1, sampleRate, false);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (t / duration);
            samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * envelope * 0.5f;
        }

        clip.SetData(samples, 0);
        tempAudio.PlayOneShot(clip);

        // 播放完后删除
        Destroy(tempAudio, duration + 0.1f);
    }

    private void VibrateControllers()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var rightHandDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, rightHandDevices);

            var leftHandDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.LeftHand, leftHandDevices);

            uint channel = 0;
            float amplitude = 1.0f;
            float duration = 1.0f;

            if (rightHandDevices.Count > 0 && rightHandDevices[0].isValid)
            {
                rightHandDevices[0].SendHapticImpulse(channel, amplitude, duration);
                Debug.Log("[ADBBroadcastListener] Right controller vibrating");
            }
            if (leftHandDevices.Count > 0 && leftHandDevices[0].isValid)
            {
                leftHandDevices[0].SendHapticImpulse(channel, amplitude, duration);
                Debug.Log("[ADBBroadcastListener] Left controller vibrating");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ADBBroadcastListener] Vibration error: " + e.Message);
        }
#endif
    }

    private System.Collections.IEnumerator FindDeviceCoroutine()
    {
        for (int i = 0; i < 5; i++)
        {
            PlayDefaultSound();
            VibrateControllers();
            yield return new WaitForSeconds(1.0f);
        }
    }
}
