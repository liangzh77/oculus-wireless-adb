#!/usr/bin/env python3

import shutil
from os import _exit
from pathlib import Path
from subprocess import Popen, PIPE
from zeroconf import ServiceBrowser, ServiceListener, Zeroconf

class MyListener(ServiceListener):

    def do_stuff(self, zc: Zeroconf, type_: str, name: str) -> None:
        info = zc.get_service_info(type_, name)
        ip_bytes = info.addresses[0]
        ip_str = f"{ip_bytes[0]}.{ip_bytes[1]}.{ip_bytes[2]}.{ip_bytes[3]}"
        print(f"Found: {ip_str}:{info.port}")
        
        # 检查 ADB 是否可用
        adb_path = shutil.which('adb')
        if not adb_path:
            # 尝试使用脚本目录下的 platform-tools
            script_dir = Path(__file__).parent
            local_adb = script_dir / "platform-tools" / ("adb.exe" if Path.cwd().drive else "adb")
            if local_adb.exists():
                adb_path = str(local_adb.absolute())
                print(f"使用本地 ADB：{adb_path}")
            else:
                print("错误：未找到 ADB 工具。请确保已安装 Android SDK Platform Tools 并添加到 PATH 环境变量中。")
                print("或运行 install_adb.py 自动安装")
                print("下载地址：https://developer.android.com/studio/releases/platform-tools")
                zeroconf.close()
                _exit(1)
        
        try:
            pipe = Popen([adb_path, 'connect', f"{ip_str}:{info.port}"], stdout=PIPE, stderr=PIPE)
            output, error = pipe.communicate()
            output_str = output.decode("utf-8")
            error_str = error.decode("utf-8")
            pipe.wait()
            
            if pipe.returncode == 0 and output_str.startswith("connected"):
                print(output_str)
                zeroconf.close()
                _exit(0)
            else:
                print(f"连接失败：{error_str if error_str else output_str}")
        except Exception as e:
            print(f"执行 ADB 命令时出错：{e}")
            zeroconf.close()
            _exit(1)

    def add_service(self, zc: Zeroconf, type_: str, name: str) -> None:
        self.do_stuff(zc, type_, name)
    
    def update_service(self, zc: Zeroconf, type_: str, name: str) -> None:
        self.do_stuff(zc, type_, name)
 
    def remove_service(self, zc: Zeroconf, type_: str, name: str) -> None:
        return


zeroconf = Zeroconf()
listener = MyListener()
ServiceBrowser(zeroconf, "_adb-tls-connect._tcp.local.", listener)
ServiceBrowser(zeroconf, "_adb_secure_connect._tcp.local.", listener)

try:
    input("Waiting for a device, press Enter to abort...\n")
except KeyboardInterrupt:
    pass
finally:
    zeroconf.close()
