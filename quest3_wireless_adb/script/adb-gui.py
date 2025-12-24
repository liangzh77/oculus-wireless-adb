#!/usr/bin/env python3

import tkinter as tk
from tkinter import ttk, scrolledtext, messagebox
import threading
import time
import json
import shutil
import re
import urllib.request
import urllib.error
from pathlib import Path
from subprocess import Popen, PIPE
from zeroconf import ServiceBrowser, ServiceListener, Zeroconf

# 设备列表文件
DEVICES_FILE = Path(__file__).parent / "devices.json"

# 远程APK列表API
REMOTE_API_URL = "https://mrgun.chu-jiao.com/api/v1/admins/applications/versions/all"


class DeviceListener(ServiceListener):
    """设备发现监听器"""

    def __init__(self, callback=None):
        self.discovered_devices = {}
        self.callback = callback

    def do_stuff(self, zc: Zeroconf, type_: str, name: str) -> None:
        info = zc.get_service_info(type_, name)
        if not info or not info.addresses:
            return

        ip_bytes = info.addresses[0]
        ip_str = f"{ip_bytes[0]}.{ip_bytes[1]}.{ip_bytes[2]}.{ip_bytes[3]}"
        device_addr = f"{ip_str}:{info.port}"

        if device_addr in self.discovered_devices:
            return

        self.discovered_devices[device_addr] = {
            "ip": ip_str,
            "port": info.port,
            "name": name,
            "address": device_addr
        }

        if self.callback:
            self.callback(device_addr)

    def add_service(self, zc: Zeroconf, type_: str, name: str) -> None:
        self.do_stuff(zc, type_, name)

    def update_service(self, zc: Zeroconf, type_: str, name: str) -> None:
        self.do_stuff(zc, type_, name)

    def remove_service(self, zc: Zeroconf, type_: str, name: str) -> None:
        pass


class ADBDeviceGUI:
    """ADB设备管理GUI"""

    # 优先显示的包名前缀
    PRIORITY_PREFIXES = ('com.chujiao', 'com.ChuJiao', 'com.trev3d')
    # 排在后面的包名前缀
    LOW_PRIORITY_PREFIXES = ('com.meta', 'com.oculus', 'com.whatsapp', 'com.facebook')

    def __init__(self, root):
        self.root = root
        self.root.title("Quest 无线 ADB 管理器")
        self.root.geometry("1200x800")

        # APK 目录
        self.apks_dir = Path(__file__).parent / "apks"

        self.scanning = False
        self.zeroconf = None
        self.adb_path = self.get_adb_path()

        self.create_widgets()
        self.load_and_display_devices()
        self.load_apk_list()

    def get_adb_path(self):
        """获取ADB路径"""
        adb_path = shutil.which('adb')
        if not adb_path:
            script_dir = Path(__file__).parent
            local_adb = script_dir / "platform-tools" / "adb.exe"
            if local_adb.exists():
                adb_path = str(local_adb.absolute())
        return adb_path

    def create_widgets(self):
        """创建界面组件"""
        # 顶部工具栏
        toolbar = ttk.Frame(self.root, padding="5")
        toolbar.pack(fill=tk.X, side=tk.TOP)

        self.scan_btn = ttk.Button(toolbar, text="扫描设备", command=self.start_scan)
        self.scan_btn.pack(side=tk.LEFT, padx=5)

        self.stop_scan_btn = ttk.Button(toolbar, text="停止扫描", command=self.stop_scan, state=tk.DISABLED)
        self.stop_scan_btn.pack(side=tk.LEFT, padx=5)

        self.refresh_btn = ttk.Button(toolbar, text="刷新列表", command=self.load_and_display_devices)
        self.refresh_btn.pack(side=tk.LEFT, padx=5)

        # 扫描进度标签
        self.scan_label = ttk.Label(toolbar, text="")
        self.scan_label.pack(side=tk.LEFT, padx=10)

        # 中间区域：设备列表和操作按钮
        middle_frame = ttk.Frame(self.root)
        middle_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)

        # 左侧：设备列表
        list_frame = ttk.Frame(middle_frame)
        list_frame.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        ttk.Label(list_frame, text="设备列表:", font=('Microsoft YaHei UI', 10, 'bold')).pack(anchor=tk.W)

        # 创建树形视图
        columns = ('Address', 'Status')
        self.tree = ttk.Treeview(list_frame, columns=columns, show='tree headings', height=10)
        self.tree.heading('#0', text='序号')
        self.tree.heading('Address', text='设备地址')
        self.tree.heading('Status', text='状态')

        self.tree.column('#0', width=50, stretch=False)
        self.tree.column('Address', width=250)
        self.tree.column('Status', width=120)

        # 滚动条
        scrollbar = ttk.Scrollbar(list_frame, orient=tk.VERTICAL, command=self.tree.yview)
        self.tree.configure(yscroll=scrollbar.set)

        self.tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)

        # 右侧：操作按钮
        button_frame = ttk.Frame(middle_frame, padding="10")
        button_frame.pack(side=tk.RIGHT, fill=tk.Y)

        ttk.Label(button_frame, text="操作:", font=('Microsoft YaHei UI', 10, 'bold')).pack(pady=(0, 10))

        ttk.Button(button_frame, text="连接", command=self.connect_device, width=15).pack(pady=5)
        ttk.Button(button_frame, text="断开", command=self.disconnect_device, width=15).pack(pady=5)
        ttk.Button(button_frame, text="连接全部", command=self.connect_all, width=15).pack(pady=5)
        ttk.Button(button_frame, text="断开全部", command=self.disconnect_all, width=15).pack(pady=5)

        ttk.Separator(button_frame, orient=tk.HORIZONTAL).pack(fill=tk.X, pady=10)

        ttk.Button(button_frame, text="USB 授权", command=self.usb_grant_permission, width=15).pack(pady=5)
        ttk.Button(button_frame, text="查看应用", command=self.view_app_versions, width=15).pack(pady=5)

        # 中间区域：应用列表和APK安装列表并排
        lists_frame = ttk.Frame(self.root)
        lists_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)

        # 左侧：应用列表区域
        app_frame = ttk.LabelFrame(lists_frame, text="已安装应用", padding="5")
        app_frame.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=(0, 2))

        # 应用列表树形视图
        app_columns = ('PackageName', 'AppName', 'Version')
        self.app_tree = ttk.Treeview(app_frame, columns=app_columns, show='headings', height=8)
        self.app_tree.heading('PackageName', text='包名')
        self.app_tree.heading('AppName', text='应用名')
        self.app_tree.heading('Version', text='版本号')

        self.app_tree.column('PackageName', width=300)
        self.app_tree.column('AppName', width=150)
        self.app_tree.column('Version', width=100)

        # 应用列表滚动条
        app_scrollbar = ttk.Scrollbar(app_frame, orient=tk.VERTICAL, command=self.app_tree.yview)
        self.app_tree.configure(yscroll=app_scrollbar.set)

        self.app_tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        app_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)

        # 右侧：APK安装列表区域
        apk_frame = ttk.LabelFrame(lists_frame, text="安装程序 (script/apks)", padding="5")
        apk_frame.pack(side=tk.RIGHT, fill=tk.BOTH, expand=True, padx=(2, 0))

        # APK列表树形视图
        apk_columns = ('FileName', 'Size')
        self.apk_tree = ttk.Treeview(apk_frame, columns=apk_columns, show='headings', height=8)
        self.apk_tree.heading('FileName', text='文件名')
        self.apk_tree.heading('Size', text='大小')

        self.apk_tree.column('FileName', width=300)
        self.apk_tree.column('Size', width=80)

        # APK列表滚动条
        apk_scrollbar = ttk.Scrollbar(apk_frame, orient=tk.VERTICAL, command=self.apk_tree.yview)
        self.apk_tree.configure(yscroll=apk_scrollbar.set)

        self.apk_tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        apk_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)

        # APK操作按钮
        apk_btn_frame = ttk.Frame(apk_frame)
        apk_btn_frame.pack(fill=tk.X, pady=(5, 0))

        ttk.Button(apk_btn_frame, text="刷新APK列表", command=self.load_apk_list).pack(pady=2)
        ttk.Button(apk_btn_frame, text="安装", command=self.install_apk).pack(pady=2)

        # 底部：日志区域
        log_frame = ttk.LabelFrame(self.root, text="日志", padding="5")
        log_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)

        self.log_text = scrolledtext.ScrolledText(log_frame, height=6, state=tk.DISABLED, wrap=tk.WORD)
        self.log_text.pack(fill=tk.BOTH, expand=True)

        # 状态栏
        self.status_bar = ttk.Label(self.root, text="就绪", relief=tk.SUNKEN, anchor=tk.W)
        self.status_bar.pack(side=tk.BOTTOM, fill=tk.X)

    def log(self, message):
        """添加日志"""
        self.log_text.config(state=tk.NORMAL)
        self.log_text.insert(tk.END, f"{message}\n")
        self.log_text.see(tk.END)
        self.log_text.config(state=tk.DISABLED)

    def log_cmd(self, cmd):
        """记录执行的命令"""
        cmd_str = ' '.join(cmd)
        self.log(f"$ {cmd_str}")

    def set_status(self, message):
        """设置状态栏"""
        self.status_bar.config(text=message)

    def load_devices(self):
        """加载已保存的设备"""
        if DEVICES_FILE.exists():
            try:
                with open(DEVICES_FILE, 'r') as f:
                    return json.load(f)
            except:
                pass
        return {}

    def save_devices(self, devices):
        """保存设备列表"""
        with open(DEVICES_FILE, 'w') as f:
            json.dump(devices, f, indent=2)

    def get_connected_devices(self):
        """获取已连接的设备"""
        if not self.adb_path:
            return set()

        try:
            cmd = [self.adb_path, 'devices']
            self.log_cmd(cmd)
            pipe = Popen(cmd, stdout=PIPE, stderr=PIPE)
            output, _ = pipe.communicate()
            output_str = output.decode("utf-8").strip()

            connected = set()
            for line in output_str.split('\n')[1:]:
                if line.strip():
                    parts = line.split()
                    if len(parts) >= 2:
                        device_addr = parts[0]
                        status = parts[1]
                        if status == 'device':
                            connected.add(device_addr)
            return connected
        except Exception as e:
            self.log(f"获取已连接设备出错: {e}")
            return set()

    def load_and_display_devices(self):
        """加载并显示设备列表"""
        # 清空列表
        for item in self.tree.get_children():
            self.tree.delete(item)

        devices = self.load_devices()
        connected = self.get_connected_devices()

        if not devices:
            self.log("设备列表为空，请先扫描设备")
            self.set_status("无设备")
            return

        # 添加设备到列表
        for i, (addr, info) in enumerate(devices.items(), 1):
            status = "已连接" if addr in connected else "未连接"
            self.tree.insert('', tk.END, text=str(i), values=(addr, status))

        self.set_status(f"已加载 {len(devices)} 个设备")
        self.log(f"从 devices.json 加载了 {len(devices)} 个设备")

    def start_scan(self):
        """开始扫描"""
        if self.scanning:
            return

        self.scanning = True
        self.scan_btn.config(state=tk.DISABLED)
        self.stop_scan_btn.config(state=tk.NORMAL)
        self.log("开始扫描设备...")
        self.set_status("扫描中...")

        # 在新线程中执行扫描
        thread = threading.Thread(target=self.scan_devices, daemon=True)
        thread.start()

    def stop_scan(self):
        """停止扫描"""
        if not self.scanning:
            return

        self.scanning = False
        self.scan_btn.config(state=tk.NORMAL)
        self.stop_scan_btn.config(state=tk.DISABLED)
        self.scan_label.config(text="")
        self.log("用户停止扫描")
        self.set_status("扫描已停止")

    def on_device_found(self, device_addr):
        """设备发现回调"""
        self.root.after(0, lambda: self.log(f"发现: {device_addr}"))

    def scan_devices(self, duration=10):
        """扫描设备"""
        try:
            self.zeroconf = Zeroconf()
            listener = DeviceListener(callback=self.on_device_found)
            ServiceBrowser(self.zeroconf, "_adb-tls-connect._tcp.local.", listener)
            ServiceBrowser(self.zeroconf, "_adb_secure_connect._tcp.local.", listener)

            start_time = time.time()
            while self.scanning:
                elapsed = time.time() - start_time
                remaining = max(0, duration - elapsed)

                if remaining > 0:
                    self.root.after(0, lambda r=remaining: self.scan_label.config(
                        text=f"扫描中... 剩余 {r:.1f}秒"
                    ))
                    time.sleep(0.1)
                else:
                    break

            self.zeroconf.close()

            # 保存设备（无论是否发现设备都要保存，未发现时保存空列表）
            self.save_devices(listener.discovered_devices)
            count = len(listener.discovered_devices)

            if count > 0:
                self.root.after(0, lambda: self.log(f"扫描完成，发现 {count} 个设备"))
                self.root.after(0, lambda: self.set_status(f"发现 {count} 个设备"))
            else:
                self.root.after(0, lambda: self.log("扫描完成，未发现设备，已清空设备列表"))
                self.root.after(0, lambda: self.set_status("未发现设备"))

            self.root.after(0, self.load_and_display_devices)

        except Exception as e:
            self.root.after(0, lambda: self.log(f"扫描出错: {e}"))
            self.root.after(0, lambda: self.set_status("扫描出错"))
        finally:
            self.scanning = False
            self.root.after(0, lambda: self.scan_btn.config(state=tk.NORMAL))
            self.root.after(0, lambda: self.stop_scan_btn.config(state=tk.DISABLED))
            self.root.after(0, lambda: self.scan_label.config(text=""))

    def get_selected_device(self):
        """获取选中的设备"""
        selection = self.tree.selection()
        if not selection:
            messagebox.showwarning("未选择设备", "请先选择一个设备")
            return None

        item = self.tree.item(selection[0])
        return item['values'][0]  # 返回设备地址

    def connect_device(self):
        """连接设备"""
        device = self.get_selected_device()
        if not device:
            return

        if not self.adb_path:
            messagebox.showerror("错误", "未找到 ADB")
            return

        self.log(f"正在连接 {device}...")
        self.set_status(f"正在连接 {device}...")

        def connect():
            try:
                cmd = [self.adb_path, 'connect', device]
                self.root.after(0, lambda: self.log_cmd(cmd))
                pipe = Popen(cmd, stdout=PIPE, stderr=PIPE)
                output, error = pipe.communicate()
                output_str = output.decode("utf-8").strip()
                error_str = error.decode("utf-8").strip()

                if pipe.returncode == 0 and ("connected" in output_str or "already connected" in output_str):
                    self.root.after(0, lambda: self.log(f"成功: {device}"))
                    self.root.after(0, lambda: self.set_status(f"已连接 {device}"))
                    self.root.after(0, self.load_and_display_devices)
                else:
                    error_msg = error_str if error_str else output_str
                    self.root.after(0, lambda: self.log(f"失败: {device} - {error_msg}"))
                    self.root.after(0, lambda: self.set_status("连接失败"))
            except Exception as e:
                self.root.after(0, lambda: self.log(f"错误: {e}"))
                self.root.after(0, lambda: self.set_status("连接出错"))

        thread = threading.Thread(target=connect, daemon=True)
        thread.start()

    def disconnect_device(self):
        """断开设备"""
        device = self.get_selected_device()
        if not device:
            return

        if not self.adb_path:
            messagebox.showerror("错误", "未找到 ADB")
            return

        self.log(f"正在断开 {device}...")
        self.set_status(f"正在断开 {device}...")

        def disconnect():
            try:
                cmd = [self.adb_path, 'disconnect', device]
                self.root.after(0, lambda: self.log_cmd(cmd))
                pipe = Popen(cmd, stdout=PIPE, stderr=PIPE)
                output, _ = pipe.communicate()
                output_str = output.decode("utf-8").strip()

                self.root.after(0, lambda: self.log(f"已断开: {device}"))
                self.root.after(0, lambda: self.set_status(f"已断开 {device}"))
                self.root.after(0, self.load_and_display_devices)
            except Exception as e:
                self.root.after(0, lambda: self.log(f"错误: {e}"))
                self.root.after(0, lambda: self.set_status("断开出错"))

        thread = threading.Thread(target=disconnect, daemon=True)
        thread.start()

    def connect_all(self):
        """连接所有设备"""
        devices = self.load_devices()
        if not devices:
            messagebox.showinfo("提示", "设备列表为空")
            return

        if not self.adb_path:
            messagebox.showerror("错误", "未找到 ADB")
            return

        self.log(f"正在连接 {len(devices)} 个设备...")
        self.set_status("正在连接所有设备...")

        def connect_all():
            success = 0
            for addr in devices:
                try:
                    cmd = [self.adb_path, 'connect', addr]
                    self.root.after(0, lambda c=cmd: self.log_cmd(c))
                    pipe = Popen(cmd, stdout=PIPE, stderr=PIPE)
                    output, _ = pipe.communicate()
                    output_str = output.decode("utf-8").strip()

                    if pipe.returncode == 0 and ("connected" in output_str or "already connected" in output_str):
                        success += 1
                        self.root.after(0, lambda a=addr: self.log(f"成功: {a}"))
                    else:
                        self.root.after(0, lambda a=addr: self.log(f"失败: {a}"))
                except Exception as e:
                    self.root.after(0, lambda a=addr, err=e: self.log(f"错误 {a}: {err}"))

            self.root.after(0, lambda: self.log(f"已连接 {success}/{len(devices)} 个设备"))
            self.root.after(0, lambda: self.set_status(f"已连接 {success}/{len(devices)} 个设备"))
            self.root.after(0, self.load_and_display_devices)

        thread = threading.Thread(target=connect_all, daemon=True)
        thread.start()

    def disconnect_all(self):
        """断开所有设备"""
        if not self.adb_path:
            messagebox.showerror("错误", "未找到 ADB")
            return

        self.log("正在断开所有设备...")
        self.set_status("正在断开所有设备...")

        def disconnect_all():
            try:
                cmd = [self.adb_path, 'disconnect']
                self.root.after(0, lambda: self.log_cmd(cmd))
                pipe = Popen(cmd, stdout=PIPE, stderr=PIPE)
                output, _ = pipe.communicate()

                self.root.after(0, lambda: self.log("所有设备已断开"))
                self.root.after(0, lambda: self.set_status("所有设备已断开"))
                self.root.after(0, self.load_and_display_devices)
            except Exception as e:
                self.root.after(0, lambda: self.log(f"错误: {e}"))
                self.root.after(0, lambda: self.set_status("断开出错"))

        thread = threading.Thread(target=disconnect_all, daemon=True)
        thread.start()

    def usb_grant_permission(self):
        """通过 USB 授予权限"""
        if not self.adb_path:
            messagebox.showerror("错误", "未找到 ADB")
            return

        self.log("正在通过 USB 授予权限...")
        self.set_status("正在授予权限...")

        def grant():
            try:
                # 先获取 USB 连接的设备
                cmd = [self.adb_path, 'devices']
                self.root.after(0, lambda: self.log_cmd(cmd))
                pipe = Popen(cmd, stdout=PIPE, stderr=PIPE)
                output, _ = pipe.communicate()
                output_str = output.decode("utf-8").strip()

                usb_devices = []
                for line in output_str.split('\n')[1:]:
                    if line.strip():
                        parts = line.split()
                        if len(parts) >= 2:
                            device_addr = parts[0]
                            status = parts[1]
                            # USB 设备通常没有冒号（不是 IP:Port 格式）
                            if ':' not in device_addr and status == 'device':
                                usb_devices.append(device_addr)

                if not usb_devices:
                    self.root.after(0, lambda: self.log("未找到 USB 连接的设备"))
                    self.root.after(0, lambda: messagebox.showwarning("提示", "未找到 USB 连接的设备，请用 USB 线连接 Quest"))
                    self.root.after(0, lambda: self.set_status("未找到 USB 设备"))
                    return

                # 对每个 USB 设备授予权限
                for device in usb_devices:
                    cmd = [self.adb_path, '-s', device, 'shell', 'pm', 'grant',
                           'com.ChuJiao.quest3_wireless_adb', 'android.permission.WRITE_SECURE_SETTINGS']
                    self.root.after(0, lambda c=cmd: self.log_cmd(c))
                    pipe = Popen(cmd, stdout=PIPE, stderr=PIPE)
                    output, error = pipe.communicate()

                    if pipe.returncode == 0:
                        self.root.after(0, lambda d=device: self.log(f"权限已授予 {d}"))
                    else:
                        error_str = error.decode("utf-8").strip()
                        self.root.after(0, lambda d=device, e=error_str: self.log(f"授予权限失败 {d}: {e}"))

                self.root.after(0, lambda: self.set_status("权限授予完成"))

            except Exception as e:
                self.root.after(0, lambda: self.log(f"错误: {e}"))
                self.root.after(0, lambda: self.set_status("授予权限出错"))

        thread = threading.Thread(target=grant, daemon=True)
        thread.start()

    def view_app_versions(self):
        """查看选中设备上的应用版本"""
        device = self.get_selected_device()
        if not device:
            return

        if not self.adb_path:
            messagebox.showerror("错误", "未找到 ADB")
            return

        # 检查设备是否已连接
        connected = self.get_connected_devices()
        if device not in connected:
            messagebox.showwarning("设备未连接", f"设备 {device} 未连接，请先连接")
            return

        self.log(f"正在获取 {device} 上的应用列表...")
        self.set_status(f"正在获取应用列表...")

        # 清空应用列表
        for item in self.app_tree.get_children():
            self.app_tree.delete(item)

        def get_apps():
            try:
                # 获取第三方应用列表
                cmd = [self.adb_path, '-s', device, 'shell', 'pm', 'list', 'packages', '-3']
                self.root.after(0, lambda: self.log_cmd(cmd))
                pipe = Popen(cmd, stdout=PIPE, stderr=PIPE)
                output, error = pipe.communicate()

                if pipe.returncode != 0:
                    error_str = error.decode("utf-8").strip()
                    self.root.after(0, lambda: self.log(f"获取应用列表失败: {error_str}"))
                    self.root.after(0, lambda: self.set_status("获取应用列表失败"))
                    return

                output_str = output.decode("utf-8").strip()
                packages = []
                for line in output_str.split('\n'):
                    if line.startswith('package:'):
                        package_name = line[8:].strip()
                        packages.append(package_name)

                self.root.after(0, lambda: self.log(f"找到 {len(packages)} 个第三方应用"))

                # 获取每个应用的版本信息
                apps_info = []
                for package in packages:
                    try:
                        # 获取版本号
                        cmd = [self.adb_path, '-s', device, 'shell', 'dumpsys', 'package', package]
                        pipe = Popen(cmd, stdout=PIPE, stderr=PIPE)
                        output, _ = pipe.communicate(timeout=5)
                        output_str = output.decode("utf-8", errors='ignore')

                        version = "N/A"
                        app_name = package

                        # 解析版本号
                        for line in output_str.split('\n'):
                            line = line.strip()
                            if line.startswith('versionName='):
                                version = line.split('=')[1].strip()
                                break

                        apps_info.append((package, app_name, version))

                    except Exception as e:
                        apps_info.append((package, package, "获取失败"))

                # 排序应用列表
                def get_sort_key(item):
                    package = item[0].lower()
                    # 优先显示的应用
                    for prefix in ADBDeviceGUI.PRIORITY_PREFIXES:
                        if package.startswith(prefix.lower()):
                            return (0, package)
                    # 排在后面的应用
                    for prefix in ADBDeviceGUI.LOW_PRIORITY_PREFIXES:
                        if package.startswith(prefix.lower()):
                            return (2, package)
                    # 其他应用
                    return (1, package)

                apps_info.sort(key=get_sort_key)

                # 更新 UI
                def update_ui():
                    for package, app_name, version in apps_info:
                        self.app_tree.insert('', tk.END, values=(package, app_name, version))
                    self.set_status(f"已加载 {len(apps_info)} 个应用")

                self.root.after(0, update_ui)

            except Exception as e:
                self.root.after(0, lambda: self.log(f"错误: {e}"))
                self.root.after(0, lambda: self.set_status("获取应用列表出错"))

        thread = threading.Thread(target=get_apps, daemon=True)
        thread.start()

    def load_apk_list(self):
        """加载 APK 列表（先显示本地，再从云端同步）"""
        # 清空列表
        for item in self.apk_tree.get_children():
            self.apk_tree.delete(item)

        # 确保目录存在
        if not self.apks_dir.exists():
            self.apks_dir.mkdir(parents=True)
            self.log(f"创建 APK 目录: {self.apks_dir}")

        # 先显示本地 APK
        self.display_local_apks()

        # 在后台从云端同步
        self.log("正在从云端检查更新...")
        thread = threading.Thread(target=self.sync_remote_apks, daemon=True)
        thread.start()

    def display_local_apks(self):
        """显示本地 APK 文件"""
        for item in self.apk_tree.get_children():
            self.apk_tree.delete(item)

        apk_files = list(self.apks_dir.glob("*.apk"))
        for apk_file in sorted(apk_files, key=lambda x: x.name.lower()):
            size = apk_file.stat().st_size
            size_str = self.format_size(size)
            self.apk_tree.insert('', tk.END, values=(apk_file.name, size_str))

        if apk_files:
            self.log(f"本地有 {len(apk_files)} 个 APK 文件")

    def sync_remote_apks(self):
        """从远程API同步APK列表，下载本地没有的版本"""
        try:
            # 获取远程APK列表
            self.root.after(0, lambda: self.log(f"$ GET {REMOTE_API_URL}"))
            req = urllib.request.Request(REMOTE_API_URL, headers={'User-Agent': 'Mozilla/5.0'})
            with urllib.request.urlopen(req, timeout=30) as response:
                data = json.loads(response.read().decode('utf-8'))

            self.root.after(0, lambda: self.log(f"云端有 {len(data)} 个应用"))

            # 获取本地已有的APK文件名
            local_apks = {f.name.lower() for f in self.apks_dir.glob("*.apk")}

            # 检查每个远程应用
            downloads_needed = []
            for app in data:
                app_name = app.get('app_name', '')
                version = app.get('latest_version', '')
                apk_url = app.get('apk_url', '')

                if not app_name or not version or not apk_url:
                    continue

                # 生成本地文件名（与Unity端一致）
                safe_name = app_name.replace(' ', '_').replace('.', '_')
                expected_filename = f"{safe_name}_{version}.apk"

                if expected_filename.lower() not in local_apks:
                    downloads_needed.append({
                        'app_name': app_name,
                        'version': version,
                        'url': apk_url,
                        'filename': expected_filename
                    })
                    self.root.after(0, lambda n=app_name, v=version: self.log(f"需要下载: {n} v{v}"))

            if not downloads_needed:
                self.root.after(0, lambda: self.log("所有APK已是最新"))
                return

            # 下载缺失的APK
            for item in downloads_needed:
                self.download_apk(item)

        except urllib.error.URLError as e:
            self.root.after(0, lambda: self.log(f"网络错误: {e}"))
        except json.JSONDecodeError as e:
            self.root.after(0, lambda: self.log(f"JSON解析错误: {e}"))
        except Exception as e:
            self.root.after(0, lambda: self.log(f"同步错误: {e}"))

    def download_apk(self, item):
        """下载单个APK文件"""
        app_name = item['app_name']
        version = item['version']
        url = item['url']
        filename = item['filename']
        filepath = self.apks_dir / filename

        try:
            self.root.after(0, lambda: self.log(f"正在下载: {app_name} v{version}..."))
            self.root.after(0, lambda: self.set_status(f"正在下载 {filename}..."))

            # 删除该应用的旧版本
            safe_name = app_name.replace(' ', '_').replace('.', '_')
            for old_file in self.apks_dir.glob(f"{safe_name}_*.apk"):
                if old_file.name != filename:
                    old_file.unlink()
                    self.root.after(0, lambda f=old_file.name: self.log(f"删除旧版本: {f}"))

            # 下载文件
            req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
            with urllib.request.urlopen(req, timeout=300) as response:
                total_size = int(response.headers.get('Content-Length', 0))
                downloaded = 0
                chunk_size = 8192

                with open(filepath, 'wb') as f:
                    while True:
                        chunk = response.read(chunk_size)
                        if not chunk:
                            break
                        f.write(chunk)
                        downloaded += len(chunk)

                        if total_size > 0:
                            progress = downloaded / total_size * 100
                            self.root.after(0, lambda p=progress, fn=filename:
                                self.set_status(f"下载 {fn}: {p:.1f}%"))

            # 验证文件
            if filepath.exists() and filepath.stat().st_size > 1000:
                size_str = self.format_size(filepath.stat().st_size)
                self.root.after(0, lambda: self.log(f"下载完成: {filename} ({size_str})"))
                self.root.after(0, self.display_local_apks)
            else:
                self.root.after(0, lambda: self.log(f"下载失败: {filename} 文件太小"))
                if filepath.exists():
                    filepath.unlink()

        except Exception as e:
            self.root.after(0, lambda: self.log(f"下载失败 {filename}: {e}"))
            if filepath.exists():
                filepath.unlink()

        self.root.after(0, lambda: self.set_status("就绪"))

    def format_size(self, size_bytes):
        """格式化文件大小"""
        if size_bytes < 1024:
            return f"{size_bytes} B"
        elif size_bytes < 1024 * 1024:
            return f"{size_bytes / 1024:.1f} KB"
        elif size_bytes < 1024 * 1024 * 1024:
            return f"{size_bytes / (1024 * 1024):.1f} MB"
        else:
            return f"{size_bytes / (1024 * 1024 * 1024):.1f} GB"

    def install_apk(self):
        """安装选中的 APK 到选中的设备"""
        # 获取选中的设备
        device = self.get_selected_device()
        if not device:
            return

        # 获取选中的 APK
        apk_selection = self.apk_tree.selection()
        if not apk_selection:
            messagebox.showwarning("未选择APK", "请先选择一个 APK 文件")
            return

        apk_item = self.apk_tree.item(apk_selection[0])
        apk_name = apk_item['values'][0]
        apk_path = self.apks_dir / apk_name

        if not apk_path.exists():
            messagebox.showerror("错误", f"APK 文件不存在: {apk_path}")
            return

        if not self.adb_path:
            messagebox.showerror("错误", "未找到 ADB")
            return

        # 检查设备是否已连接
        connected = self.get_connected_devices()
        if device not in connected:
            messagebox.showwarning("设备未连接", f"设备 {device} 未连接，请先连接")
            return

        self.log(f"正在安装 {apk_name} 到 {device}...")
        self.set_status(f"正在安装 {apk_name}...")

        def install():
            try:
                cmd = [self.adb_path, '-s', device, 'install', '-r', str(apk_path)]
                self.root.after(0, lambda: self.log_cmd(cmd))
                pipe = Popen(cmd, stdout=PIPE, stderr=PIPE)
                output, error = pipe.communicate(timeout=1800)  # 30分钟超时

                output_str = output.decode("utf-8", errors='ignore').strip()
                error_str = error.decode("utf-8", errors='ignore').strip()

                if pipe.returncode == 0 and "Success" in output_str:
                    self.root.after(0, lambda: self.log(f"安装成功: {apk_name}"))
                    self.root.after(0, lambda: self.set_status(f"安装成功: {apk_name}"))
                    self.root.after(0, lambda: messagebox.showinfo("成功", f"{apk_name} 安装成功"))
                    self.root.after(0, self.view_app_versions)
                else:
                    error_msg = error_str if error_str else output_str
                    self.root.after(0, lambda: self.log(f"安装失败: {apk_name} - {error_msg}"))
                    self.root.after(0, lambda: self.set_status("安装失败"))
                    self.root.after(0, lambda: messagebox.showerror("安装失败", f"{apk_name}\n{error_msg}"))

            except Exception as e:
                self.root.after(0, lambda: self.log(f"安装错误: {e}"))
                self.root.after(0, lambda: self.set_status("安装出错"))
                self.root.after(0, lambda: messagebox.showerror("错误", f"安装出错: {e}"))

        thread = threading.Thread(target=install, daemon=True)
        thread.start()


def main():
    root = tk.Tk()
    app = ADBDeviceGUI(root)
    root.mainloop()


if __name__ == "__main__":
    main()
