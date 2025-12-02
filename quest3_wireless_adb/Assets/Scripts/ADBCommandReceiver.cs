using UnityEngine;
using System.Collections;

/// <summary>
/// ADB 命令接收器 - 接收来自 ADB 的命令并执行相应操作
///
/// 使用方法（从电脑端执行）：
/// 播放声音: adb shell am broadcast -a com.ChuJiao.quest3_wireless_adb.PLAY_SOUND
/// 震动手柄: adb shell am broadcast -a com.ChuJiao.quest3_wireless_adb.VIBRATE
/// 找到设备: adb shell am broadcast -a com.ChuJiao.quest3_wireless_adb.FIND_DEVICE
/// </summary>
public class ADBCommandReceiver : MonoBehaviour
{
    [Header("音频设置")]
    [Tooltip("要播放的提示音")]
    public AudioClip alertSound;

    [Header("震动设置")]
    [Tooltip("震动持续时间（秒）")]
    public float vibrationDuration = 1.0f;

    [Tooltip("震动强度 (0-1)")]
    [Range(0, 1)]
    public float vibrationAmplitude = 1.0f;

    private AudioSource audioSource;
    private static ADBCommandReceiver instance;

    public static ADBCommandReceiver Instance => instance;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        // 创建 AudioSource
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    void Start()
    {
        Debug.Log("[ADBCommandReceiver] Initialized");

        // 如果没有指定音频，创建一个默认的提示音
        if (alertSound == null)
        {
            CreateDefaultAlertSound();
        }
    }

    /// <summary>
    /// 创建默认提示音（程序生成的蜂鸣声）
    /// </summary>
    private void CreateDefaultAlertSound()
    {
        int sampleRate = 44100;
        float frequency = 880f; // A5 音符
        float duration = 0.5f;
        int sampleCount = (int)(sampleRate * duration);

        alertSound = AudioClip.Create("AlertBeep", sampleCount, 1, sampleRate, false);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            // 生成带衰减的正弦波
            float envelope = 1f - (t / duration);
            samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * envelope * 0.5f;
        }

        alertSound.SetData(samples, 0);
        Debug.Log("[ADBCommandReceiver] Default alert sound created");
    }

    /// <summary>
    /// 播放提示音
    /// </summary>
    public void PlaySound()
    {
        Debug.Log("[ADBCommandReceiver] Playing sound");

        if (audioSource != null && alertSound != null)
        {
            audioSource.PlayOneShot(alertSound);
        }
        else
        {
            Debug.LogWarning("[ADBCommandReceiver] AudioSource or AlertSound is null");
        }
    }

    /// <summary>
    /// 震动手柄
    /// </summary>
    public void VibrateControllers()
    {
        Debug.Log("[ADBCommandReceiver] Vibrating controllers");
        StartCoroutine(VibrateControllersCoroutine());
    }

    private IEnumerator VibrateControllersCoroutine()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // 使用 OVRInput 震动（如果可用）
        try
        {
            // 尝试使用 Unity XR 震动
            UnityEngine.XR.InputDevice rightHand = default;
            UnityEngine.XR.InputDevice leftHand = default;

            var rightHandDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, rightHandDevices);
            if (rightHandDevices.Count > 0) rightHand = rightHandDevices[0];

            var leftHandDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.LeftHand, leftHandDevices);
            if (leftHandDevices.Count > 0) leftHand = leftHandDevices[0];

            // 开始震动
            uint channel = 0;
            if (rightHand.isValid)
            {
                rightHand.SendHapticImpulse(channel, vibrationAmplitude, vibrationDuration);
                Debug.Log("[ADBCommandReceiver] Right controller vibrating");
            }
            if (leftHand.isValid)
            {
                leftHand.SendHapticImpulse(channel, vibrationAmplitude, vibrationDuration);
                Debug.Log("[ADBCommandReceiver] Left controller vibrating");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ADBCommandReceiver] Vibration error: " + e.Message);
        }
#endif
        yield return new WaitForSeconds(vibrationDuration);
    }

    /// <summary>
    /// 找到设备（播放声音 + 震动）
    /// </summary>
    public void FindDevice()
    {
        Debug.Log("[ADBCommandReceiver] Find device triggered");

        // 播放多次声音并震动
        StartCoroutine(FindDeviceCoroutine());
    }

    private IEnumerator FindDeviceCoroutine()
    {
        for (int i = 0; i < 5; i++)
        {
            PlaySound();
            VibrateControllers();
            yield return new WaitForSeconds(1.0f);
        }
    }

    // ========== 命令处理（由 ADBBroadcastListener 调用） ==========

    /// <summary>
    /// 从 ADBBroadcastListener 调用的方法
    /// </summary>
    public void OnCommandReceived(string command)
    {
        Debug.Log($"[ADBCommandReceiver] Command received: {command}");

        switch (command)
        {
            case "PLAY_SOUND":
                PlaySound();
                break;
            case "VIBRATE":
                VibrateControllers();
                break;
            case "FIND_DEVICE":
                FindDevice();
                break;
            default:
                Debug.LogWarning($"[ADBCommandReceiver] Unknown command: {command}");
                break;
        }
    }
}
