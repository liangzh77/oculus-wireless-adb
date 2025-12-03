#!/usr/bin/env python3

import tkinter as tk
from tkinter import ttk, scrolledtext, messagebox
import threading
import time
import json
import shutil
from pathlib import Path
from subprocess import Popen, PIPE
from zeroconf import ServiceBrowser, ServiceListener, Zeroconf

# 设备列表文件
DEVICES_FILE = Path(__file__).parent / "devices.json"


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

    def __init__(self, root):
        self.root = root
        self.root.title("Quest 无线 ADB 管理器")
        self.root.geometry("800x600")

        self.scanning = False
        self.zeroconf = None
        self.adb_path = self.get_adb_path()

        self.create_widgets()
        self.load_and_display_devices()

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
        self.tree = ttk.Treeview(list_frame, columns=columns, show='tree headings', height=15)
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

        ttk.Button(button_frame, text="授予权限", command=self.grant_permission, width=15).pack(pady=5)
        ttk.Button(button_frame, text="撤销权限", command=self.revoke_permission, width=15).pack(pady=5)
        ttk.Button(button_frame, text="授予全部", command=self.grant_permission_all, width=15).pack(pady=5)
        ttk.Button(button_frame, text="撤销全部", command=self.revoke_permission_all, width=15).pack(pady=5)

        # 底部：日志区域
        log_frame = ttk.LabelFrame(self.root, text="日志", padding="5")
        log_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)

        self.log_text = scrolledtext.ScrolledText(log_frame, height=8, state=tk.DISABLED, wrap=tk.WORD)
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
            pipe = Popen([self.adb_path, 'devices'], stdout=PIPE, stderr=PIPE)
            output, _ = pipe.communicate()
            output_str = output.decode("utf-8").strip()

            connected = set()
            for line in output_str.split('\n')[1:]:
                if line.strip():
                    parts = line.split()
                    if len(parts) >= 2:
                        device_addr = parts[0]
                        if ':' in device_addr:
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
                pipe = Popen([self.adb_path, 'connect', device], stdout=PIPE, stderr=PIPE)
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
                pipe = Popen([self.adb_path, 'disconnect', device], stdout=PIPE, stderr=PIPE)
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
                    pipe = Popen([self.adb_path, 'connect', addr], stdout=PIPE, stderr=PIPE)
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
                pipe = Popen([self.adb_path, 'disconnect'], stdout=PIPE, stderr=PIPE)
                output, _ = pipe.communicate()

                self.root.after(0, lambda: self.log("所有设备已断开"))
                self.root.after(0, lambda: self.set_status("所有设备已断开"))
                self.root.after(0, self.load_and_display_devices)
            except Exception as e:
                self.root.after(0, lambda: self.log(f"错误: {e}"))
                self.root.after(0, lambda: self.set_status("断开出错"))

        thread = threading.Thread(target=disconnect_all, daemon=True)
        thread.start()

    def grant_permission(self):
        """授予权限"""
        device = self.get_selected_device()
        if not device:
            return

        if not self.adb_path:
            messagebox.showerror("错误", "未找到 ADB")
            return

        self.log(f"正在授予 {device} 权限...")
        self.set_status(f"正在授予 {device} 权限...")

        def grant():
            try:
                cmd = [self.adb_path, '-s', device, 'shell', 'pm', 'grant',
                       'com.ChuJiao.quest3_wireless_adb', 'android.permission.WRITE_SECURE_SETTINGS']
                pipe = Popen(cmd, stdout=PIPE, stderr=PIPE)
                output, error = pipe.communicate()

                if pipe.returncode == 0:
                    self.root.after(0, lambda: self.log(f"权限已授予 {device}"))
                    self.root.after(0, lambda: self.set_status(f"权限已授予"))
                else:
                    error_str = error.decode("utf-8").strip()
                    self.root.after(0, lambda: self.log(f"授予权限失败: {error_str}"))
                    self.root.after(0, lambda: self.set_status("授予权限失败"))
            except Exception as e:
                self.root.after(0, lambda: self.log(f"错误: {e}"))
                self.root.after(0, lambda: self.set_status("授予权限出错"))

        thread = threading.Thread(target=grant, daemon=True)
        thread.start()

    def revoke_permission(self):
        """撤销权限"""
        device = self.get_selected_device()
        if not device:
            return

        if not self.adb_path:
            messagebox.showerror("错误", "未找到 ADB")
            return

        self.log(f"正在撤销 {device} 权限...")
        self.set_status(f"正在撤销 {device} 权限...")

        def revoke():
            try:
                cmd = [self.adb_path, '-s', device, 'shell', 'pm', 'revoke',
                       'com.ChuJiao.quest3_wireless_adb', 'android.permission.WRITE_SECURE_SETTINGS']
                pipe = Popen(cmd, stdout=PIPE, stderr=PIPE)
                output, error = pipe.communicate()

                if pipe.returncode == 0:
                    self.root.after(0, lambda: self.log(f"权限已撤销 {device}"))
                    self.root.after(0, lambda: self.set_status(f"权限已撤销"))
                else:
                    error_str = error.decode("utf-8").strip()
                    self.root.after(0, lambda: self.log(f"撤销权限失败: {error_str}"))
                    self.root.after(0, lambda: self.set_status("撤销权限失败"))
            except Exception as e:
                self.root.after(0, lambda: self.log(f"错误: {e}"))
                self.root.after(0, lambda: self.set_status("撤销权限出错"))

        thread = threading.Thread(target=revoke, daemon=True)
        thread.start()

    def grant_permission_all(self):
        """授予所有已连接设备的权限"""
        connected = self.get_connected_devices()
        if not connected:
            messagebox.showinfo("提示", "无已连接设备")
            return

        if not self.adb_path:
            messagebox.showerror("错误", "未找到 ADB")
            return

        self.log(f"正在授予 {len(connected)} 个设备权限...")
        self.set_status("正在授予所有设备权限...")

        def grant_all():
            success = 0
            for device in connected:
                try:
                    cmd = [self.adb_path, '-s', device, 'shell', 'pm', 'grant',
                           'com.ChuJiao.quest3_wireless_adb', 'android.permission.WRITE_SECURE_SETTINGS']
                    pipe = Popen(cmd, stdout=PIPE, stderr=PIPE)
                    output, error = pipe.communicate()

                    if pipe.returncode == 0:
                        success += 1
                        self.root.after(0, lambda d=device: self.log(f"权限已授予 {d}"))
                    else:
                        error_str = error.decode("utf-8").strip()
                        self.root.after(0, lambda d=device, e=error_str: self.log(f"授予失败 {d}: {e}"))
                except Exception as e:
                    self.root.after(0, lambda d=device, err=e: self.log(f"错误 {d}: {err}"))

            self.root.after(0, lambda: self.log(f"已授予 {success}/{len(connected)} 个设备权限"))
            self.root.after(0, lambda: self.set_status(f"已授予 {success}/{len(connected)} 个设备权限"))

        thread = threading.Thread(target=grant_all, daemon=True)
        thread.start()

    def revoke_permission_all(self):
        """撤销所有已连接设备的权限"""
        connected = self.get_connected_devices()
        if not connected:
            messagebox.showinfo("提示", "无已连接设备")
            return

        if not self.adb_path:
            messagebox.showerror("错误", "未找到 ADB")
            return

        self.log(f"正在撤销 {len(connected)} 个设备权限...")
        self.set_status("正在撤销所有设备权限...")

        def revoke_all():
            success = 0
            for device in connected:
                try:
                    cmd = [self.adb_path, '-s', device, 'shell', 'pm', 'revoke',
                           'com.ChuJiao.quest3_wireless_adb', 'android.permission.WRITE_SECURE_SETTINGS']
                    pipe = Popen(cmd, stdout=PIPE, stderr=PIPE)
                    output, error = pipe.communicate()

                    if pipe.returncode == 0:
                        success += 1
                        self.root.after(0, lambda d=device: self.log(f"权限已撤销 {d}"))
                    else:
                        error_str = error.decode("utf-8").strip()
                        self.root.after(0, lambda d=device, e=error_str: self.log(f"撤销失败 {d}: {e}"))
                except Exception as e:
                    self.root.after(0, lambda d=device, err=e: self.log(f"错误 {d}: {err}"))

            self.root.after(0, lambda: self.log(f"已撤销 {success}/{len(connected)} 个设备权限"))
            self.root.after(0, lambda: self.set_status(f"已撤销 {success}/{len(connected)} 个设备权限"))

        thread = threading.Thread(target=revoke_all, daemon=True)
        thread.start()


def main():
    root = tk.Tk()
    app = ADBDeviceGUI(root)
    root.mainloop()


if __name__ == "__main__":
    main()
