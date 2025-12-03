#!/usr/bin/env python3

import sys
import shutil
import json
import time
import threading
from pathlib import Path
from subprocess import Popen, PIPE
from zeroconf import ServiceBrowser, ServiceListener, Zeroconf

# 尝试导入平台特定的键盘输入模块
try:
    import msvcrt  # Windows
    HAS_MSVCRT = True
except ImportError:
    HAS_MSVCRT = False
    try:
        import select  # Unix/Linux
        HAS_SELECT = True
    except ImportError:
        HAS_SELECT = False

# 设备列表文件
DEVICES_FILE = Path(__file__).parent / "devices.json"


def check_key_pressed():
    """检测是否有按键（非阻塞）"""
    if HAS_MSVCRT:
        # Windows
        return msvcrt.kbhit()
    elif HAS_SELECT:
        # Unix/Linux
        return select.select([sys.stdin], [], [], 0)[0] != []
    else:
        # 不支持非阻塞输入的平台
        return False


def clear_input_buffer():
    """清空输入缓冲区"""
    if HAS_MSVCRT:
        while msvcrt.kbhit():
            msvcrt.getch()
    elif HAS_SELECT:
        while select.select([sys.stdin], [], [], 0)[0]:
            sys.stdin.readline()

class MyListener(ServiceListener):

    def __init__(self):
        self.discovered_devices = {}  # {address: {ip, port, name}}
        self.adb_path = None

    def get_adb_path(self) -> str:
        """获取 ADB 路径"""
        if self.adb_path:
            return self.adb_path

        adb_path = shutil.which('adb')
        if not adb_path:
            # 尝试使用脚本目录下的 platform-tools
            script_dir = Path(__file__).parent
            local_adb = script_dir / "platform-tools" / ("adb.exe" if Path.cwd().drive else "adb")
            if local_adb.exists():
                adb_path = str(local_adb.absolute())
                print(f"Using local ADB: {adb_path}")
            else:
                print("Error: ADB not found. Please install Android SDK Platform Tools.")
                print("Or run install_adb.py to install automatically.")
                return None

        self.adb_path = adb_path
        return adb_path

    def do_stuff(self, zc: Zeroconf, type_: str, name: str) -> None:
        info = zc.get_service_info(type_, name)
        if not info or not info.addresses:
            return

        ip_bytes = info.addresses[0]
        ip_str = f"{ip_bytes[0]}.{ip_bytes[1]}.{ip_bytes[2]}.{ip_bytes[3]}"
        device_addr = f"{ip_str}:{info.port}"

        # 跳过已发现的设备
        if device_addr in self.discovered_devices:
            return

        self.discovered_devices[device_addr] = {
            "ip": ip_str,
            "port": info.port,
            "name": name,
            "address": device_addr
        }
        # 不在这里打印，避免打断倒计时显示

    def add_service(self, zc: Zeroconf, type_: str, name: str) -> None:
        self.do_stuff(zc, type_, name)

    def update_service(self, zc: Zeroconf, type_: str, name: str) -> None:
        self.do_stuff(zc, type_, name)

    def remove_service(self, zc: Zeroconf, type_: str, name: str) -> None:
        return


def load_devices():
    """加载已保存的设备列表"""
    if DEVICES_FILE.exists():
        try:
            with open(DEVICES_FILE, 'r') as f:
                return json.load(f)
        except:
            pass
    return {}


def save_devices(devices):
    """保存设备列表"""
    with open(DEVICES_FILE, 'w') as f:
        json.dump(devices, f, indent=2)


def scan_devices(scan_duration=10):
    """扫描设备"""
    print("Scanning for ADB devices...")
    print("(Press Enter to stop early)")
    print("-" * 40)

    # 清空输入缓冲区，避免之前的输入干扰
    clear_input_buffer()

    zeroconf = Zeroconf()
    listener = MyListener()
    ServiceBrowser(zeroconf, "_adb-tls-connect._tcp.local.", listener)
    ServiceBrowser(zeroconf, "_adb_secure_connect._tcp.local.", listener)

    last_count = 0
    start_time = time.time()
    user_stopped = False

    try:
        while True:
            elapsed = time.time() - start_time
            remaining = max(0, scan_duration - elapsed)

            # 检查是否有按键
            if check_key_pressed():
                clear_input_buffer()
                print(f"\r{' ' * 60}")  # 清空行
                print("Scan stopped by user.")
                user_stopped = True
                break

            # 检查是否有新设备
            current_count = len(listener.discovered_devices)
            if current_count > last_count:
                # 清除倒计时行，显示发现的设备数
                print(f"\rFound {current_count} device(s)...{' ' * 20}")
                last_count = current_count

            # 显示倒计时
            if remaining > 0:
                print(f"\rScanning... {remaining:.1f}s remaining (press Enter to stop)", end='', flush=True)
                time.sleep(0.1)
            else:
                print(f"\rScanning... complete.{' ' * 40}")
                break

    except KeyboardInterrupt:
        print("\nScan interrupted.")
    finally:
        zeroconf.close()

    # 显示结果
    print("-" * 40)
    if listener.discovered_devices:
        print(f"Found {len(listener.discovered_devices)} device(s)")
        for addr in listener.discovered_devices:
            print(f"  - {addr}")

        # 询问是否继续扫描
        print("-" * 40)
        try:
            response = input("Continue scanning? (y/N): ").strip().lower()
            if response == 'y':
                # 合并之前的发现
                previous_devices = listener.discovered_devices.copy()
                additional = scan_devices(scan_duration)
                # 合并设备
                previous_devices.update(additional)
                listener.discovered_devices = previous_devices
        except EOFError:
            # 非交互式环境，直接继续
            print("N")

        # 保存发现的设备
        save_devices(listener.discovered_devices)
        print(f"Total {len(listener.discovered_devices)} device(s) saved to devices.json")
    else:
        print("No devices found.")
        # 询问是否重试
        try:
            response = input("Retry scanning? (y/N): ").strip().lower()
            if response == 'y':
                return scan_devices(scan_duration)
        except EOFError:
            # 非交互式环境，直接退出
            print("N")

    return listener.discovered_devices


def connect_device(address, adb_path=None):
    """连接单个设备"""
    if not adb_path:
        adb_path = shutil.which('adb')
        if not adb_path:
            script_dir = Path(__file__).parent
            local_adb = script_dir / "platform-tools" / ("adb.exe" if Path.cwd().drive else "adb")
            if local_adb.exists():
                adb_path = str(local_adb.absolute())

    if not adb_path:
        print("Error: ADB not found.")
        return False

    try:
        pipe = Popen([adb_path, 'connect', address], stdout=PIPE, stderr=PIPE)
        output, error = pipe.communicate()
        output_str = output.decode("utf-8").strip()
        error_str = error.decode("utf-8").strip()
        pipe.wait()

        if pipe.returncode == 0 and ("connected" in output_str or "already connected" in output_str):
            print(f"OK: {address}")
            return True
        else:
            print(f"FAIL: {address} - {error_str if error_str else output_str}")
            return False
    except Exception as e:
        print(f"Error: {e}")
        return False


def connect_all():
    """连接所有已保存的设备"""
    devices = load_devices()
    if not devices:
        print("No devices in list. Run scan first.")
        return

    print(f"Connecting to {len(devices)} device(s)...")
    print("-" * 40)

    adb_path = shutil.which('adb')
    if not adb_path:
        script_dir = Path(__file__).parent
        local_adb = script_dir / "platform-tools" / ("adb.exe" if Path.cwd().drive else "adb")
        if local_adb.exists():
            adb_path = str(local_adb.absolute())

    success_count = 0
    for address in devices:
        if connect_device(address, adb_path):
            success_count += 1

    print("-" * 40)
    print(f"Connected {success_count}/{len(devices)} device(s)")


def get_connected_devices():
    """获取当前ADB连接的设备列表"""
    adb_path = shutil.which('adb')
    if not adb_path:
        script_dir = Path(__file__).parent
        local_adb = script_dir / "platform-tools" / ("adb.exe" if Path.cwd().drive else "adb")
        if local_adb.exists():
            adb_path = str(local_adb.absolute())

    if not adb_path:
        return set()

    try:
        pipe = Popen([adb_path, 'devices'], stdout=PIPE, stderr=PIPE)
        output, _ = pipe.communicate()
        output_str = output.decode("utf-8").strip()

        connected = set()
        for line in output_str.split('\n')[1:]:  # Skip header line
            if line.strip():
                parts = line.split()
                if len(parts) >= 2:
                    device_addr = parts[0]
                    # 只保留IP:端口格式的设备(无线设备)
                    if ':' in device_addr:
                        connected.add(device_addr)
        return connected
    except Exception:
        return set()


def list_devices():
    """列出所有设备"""
    devices = load_devices()
    if not devices:
        print("No devices in list. Run scan first.")
        return devices

    connected = get_connected_devices()

    print(f"Saved devices ({len(devices)}):")
    print("-" * 40)
    for i, (addr, info) in enumerate(devices.items(), 1):
        status = "[CONNECTED]" if addr in connected else ""
        print(f"  {i}. {addr} {status}")
    print("-" * 40)
    return devices


def get_device_by_index(index):
    """通过编号获取设备地址"""
    devices = load_devices()
    if not devices:
        return None

    device_list = list(devices.keys())
    if 1 <= index <= len(device_list):
        return device_list[index - 1]
    return None


def main():
    if len(sys.argv) < 2:
        # 默认行为：扫描并连接
        scan_devices()
        return

    command = sys.argv[1].lower()

    if command == "scan":
        scan_devices()
    elif command == "connect":
        if len(sys.argv) > 2:
            arg = sys.argv[2]
            # 检查是编号还是地址
            if arg.isdigit():
                # 通过编号连接
                addr = get_device_by_index(int(arg))
                if addr:
                    connect_device(addr)
                else:
                    print(f"Invalid device number: {arg}")
            else:
                # 直接用地址连接
                connect_device(arg)
        else:
            # 连接所有设备
            connect_all()
    elif command == "list":
        list_devices()
    else:
        print("Usage:")
        print("  python discover-and-connect.py scan     - Scan for devices")
        print("  python discover-and-connect.py connect  - Connect all saved devices")
        print("  python discover-and-connect.py connect <ip:port> - Connect one device")
        print("  python discover-and-connect.py list     - List saved devices")


if __name__ == "__main__":
    main()
