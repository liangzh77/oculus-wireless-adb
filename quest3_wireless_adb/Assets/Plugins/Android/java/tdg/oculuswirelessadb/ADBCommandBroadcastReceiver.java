package tdg.oculuswirelessadb;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.util.Log;

import com.unity3d.player.UnityPlayer;

/**
 * ADB 命令广播接收器
 * 接收来自 ADB 的广播命令并转发给 Unity
 */
public class ADBCommandBroadcastReceiver extends BroadcastReceiver {

    private static final String TAG = "ADBCommandReceiver";

    private static final String ACTION_PLAY_SOUND = "com.ChuJiao.quest3_wireless_adb.PLAY_SOUND";
    private static final String ACTION_VIBRATE = "com.ChuJiao.quest3_wireless_adb.VIBRATE";
    private static final String ACTION_FIND_DEVICE = "com.ChuJiao.quest3_wireless_adb.FIND_DEVICE";

    @Override
    public void onReceive(Context context, Intent intent) {
        String action = intent.getAction();
        Log.d(TAG, "Received broadcast: " + action);

        String command = null;

        if (ACTION_PLAY_SOUND.equals(action)) {
            command = "PLAY_SOUND";
        } else if (ACTION_VIBRATE.equals(action)) {
            command = "VIBRATE";
        } else if (ACTION_FIND_DEVICE.equals(action)) {
            command = "FIND_DEVICE";
        }

        if (command != null) {
            // 发送消息给 Unity（使用 ADBBroadcastListener，它始终激活）
            Log.d(TAG, "Sending command to Unity: " + command);
            UnityPlayer.UnitySendMessage("ADBBroadcastListener", "OnCommandReceived", command);
        }
    }
}
