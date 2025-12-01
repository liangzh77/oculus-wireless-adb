package tdg.oculuswirelessadb;

import android.content.Context;
import android.net.wifi.WifiManager;
import android.util.Log;
import android.util.Pair;

import javax.jmdns.JmDNS;
import javax.jmdns.ServiceEvent;
import javax.jmdns.ServiceListener;
import java.io.IOException;
import java.net.InetAddress;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

/**
 * JmDNS ADB Discovery - Java版本
 * 用于在本地网络中发现ADB服务
 */
public class JmDNSAdbDiscoveryJava {

    private static final String TAG = "JmDNSAdbDiscovery";

    private Context context;
    private JmDNS jmdns;
    private ServiceListener serviceListener;
    private WifiManager.MulticastLock multicastLock;

    public JmDNSAdbDiscoveryJava(Context context) {
        this.context = context.getApplicationContext();
    }

    /**
     * 发现本地ADB服务
     * @return Pair<IP地址, 端口> 或 null
     */
    public Pair<String, Integer> discoverLocalAdbService() {
        final Pair<String, Integer>[] result = new Pair[1];
        final CountDownLatch latch = new CountDownLatch(1);

        try {
            // 获取WiFi管理器并创建组播锁
            WifiManager wifiManager = (WifiManager) context.getSystemService(Context.WIFI_SERVICE);
            if (wifiManager == null) {
                Log.e(TAG, "WifiManager is null");
                return null;
            }

            multicastLock = wifiManager.createMulticastLock("jmdns_multicast_lock");
            multicastLock.setReferenceCounted(true);
            multicastLock.acquire();

            // 获取设备IP地址
            InetAddress deviceIp = getDeviceIpAddress(wifiManager);
            if (deviceIp == null) {
                Log.e(TAG, "Could not determine IP address");
                releaseResources();
                return null;
            }

            Log.i(TAG, "Device IP: " + deviceIp.getHostAddress());

            // 创建JmDNS实例
            jmdns = JmDNS.create(deviceIp);

            // 创建服务监听器
            final String localIp = deviceIp.getHostAddress();
            serviceListener = new ServiceListener() {
                @Override
                public void serviceAdded(ServiceEvent event) {
                    Log.d(TAG, "Service added: " + event.getName());
                    jmdns.requestServiceInfo(event.getType(), event.getName(), true, 2000);
                }

                @Override
                public void serviceRemoved(ServiceEvent event) {
                    Log.d(TAG, "Service removed: " + event.getName());
                }

                @Override
                public void serviceResolved(ServiceEvent event) {
                    Log.d(TAG, "Service resolved: " + event.getName());

                    InetAddress[] addresses = event.getInfo().getInetAddresses();
                    if (addresses != null && addresses.length > 0) {
                        String host = addresses[0].getHostAddress();
                        int port = event.getInfo().getPort();

                        Log.i(TAG, "Found ADB service: " + host + ":" + port);

                        // 检查是否是本地设备
                        if (host != null && host.equals(localIp) && port > 0) {
                            Log.i(TAG, "Matched local ADB: " + host + ":" + port);
                            result[0] = new Pair<>(host, port);
                            latch.countDown();
                        }
                    }
                }
            };

            // 添加监听器监听两种ADB服务类型
            jmdns.addServiceListener("_adb-tls-connect._tcp.local.", serviceListener);
            jmdns.addServiceListener("_adb_secure_connect._tcp.local.", serviceListener);

            Log.i(TAG, "Listening for ADB services...");

            // 等待最多10秒
            boolean found = latch.await(10, TimeUnit.SECONDS);

            if (!found) {
                Log.w(TAG, "Discovery timeout - no ADB service found");
            }

        } catch (IOException e) {
            Log.e(TAG, "JmDNS error", e);
        } catch (InterruptedException e) {
            Log.e(TAG, "Discovery interrupted", e);
            Thread.currentThread().interrupt();
        } finally {
            stopDiscovery();
        }

        return result[0];
    }

    /**
     * 停止发现
     */
    public void stopDiscovery() {
        try {
            if (jmdns != null && serviceListener != null) {
                jmdns.removeServiceListener("_adb-tls-connect._tcp.local.", serviceListener);
                jmdns.removeServiceListener("_adb_secure_connect._tcp.local.", serviceListener);
            }
        } catch (Exception e) {
            Log.e(TAG, "Error removing listeners", e);
        }

        try {
            if (jmdns != null) {
                jmdns.close();
                jmdns = null;
            }
        } catch (Exception e) {
            Log.e(TAG, "Error closing JmDNS", e);
        }

        releaseResources();

        Log.i(TAG, "Discovery stopped");
    }

    /**
     * 释放资源
     */
    private void releaseResources() {
        if (multicastLock != null && multicastLock.isHeld()) {
            multicastLock.release();
            multicastLock = null;
        }
    }

    /**
     * 获取设备IP地址
     */
    private InetAddress getDeviceIpAddress(WifiManager wifiManager) {
        try {
            int ipInt = wifiManager.getConnectionInfo().getIpAddress();

            if (ipInt == 0) {
                Log.w(TAG, "IP address is 0 - WiFi might be off");
                return null;
            }

            byte[] ipBytes = new byte[] {
                (byte) (ipInt & 0xff),
                (byte) (ipInt >> 8 & 0xff),
                (byte) (ipInt >> 16 & 0xff),
                (byte) (ipInt >> 24 & 0xff)
            };

            return InetAddress.getByAddress(ipBytes);
        } catch (Exception e) {
            Log.e(TAG, "Error getting device IP", e);
            return null;
        }
    }
}
