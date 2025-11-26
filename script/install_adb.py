#!/usr/bin/env python3
"""
ADB 安装助手脚本
自动下载并安装 Android SDK Platform Tools
"""

import os
import sys
import zipfile
import urllib.request
from pathlib import Path

def download_file(url, filename):
    """下载文件并显示进度"""
    print(f"正在下载 {filename}...")
    try:
        urllib.request.urlretrieve(url, filename)
        print(f"下载完成：{filename}")
        return True
    except Exception as e:
        print(f"下载失败：{e}")
        return False

def install_platform_tools():
    """安装 Android SDK Platform Tools"""
    
    # 下载链接（请根据最新版本更新）
    platform_tools_url = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip"
    zip_filename = "platform-tools-latest-windows.zip"
    extract_dir = "platform-tools"
    
    print("Android SDK Platform Tools 安装程序")
    print("=" * 50)
    
    # 检查是否已经存在
    if os.path.exists(extract_dir) and os.path.exists(os.path.join(extract_dir, "adb.exe")):
        print(f"检测到已安装的 Platform Tools：{os.path.abspath(extract_dir)}")
        print("请确保该目录已添加到 PATH 环境变量中")
        return True
    
    # 下载文件
    if not download_file(platform_tools_url, zip_filename):
        return False
    
    # 解压文件
    print("正在解压文件...")
    try:
        with zipfile.ZipFile(zip_filename, 'r') as zip_ref:
            zip_ref.extractall()
        print("解压完成")
    except Exception as e:
        print(f"解压失败：{e}")
        return False
    
    # 清理下载的zip文件
    try:
        os.remove(zip_filename)
        print("清理临时文件完成")
    except:
        pass
    
    # 检查安装结果
    adb_path = os.path.join(extract_dir, "adb.exe")
    if os.path.exists(adb_path):
        print(f"\n安装成功！")
        print(f"ADB 位置：{os.path.abspath(adb_path)}")
        print(f"\n请将以下路径添加到系统 PATH 环境变量中：")
        print(f"{os.path.abspath(extract_dir)}")
        print(f"\n或者将 adb.exe 复制到系统目录中")
        return True
    else:
        print("安装失败：未找到 adb.exe")
        return False

def check_adb_installation():
    """检查 ADB 是否已正确安装"""
    import shutil
    adb_path = shutil.which('adb')
    if adb_path:
        print(f"ADB 已安装并可用：{adb_path}")
        return True
    else:
        print("ADB 未安装或未添加到 PATH")
        return False

if __name__ == "__main__":
    print("ADB 安装检查工具")
    print("=" * 30)
    
    # 首先检查是否已经安装
    if check_adb_installation():
        print("ADB 已正确安装，无需重新安装")
        sys.exit(0)
    
    print("\nADB 未安装，开始安装过程...")
    
    if install_platform_tools():
        print("\n安装完成！请重启命令提示符或 IDE 后再次运行脚本")
    else:
        print("\n安装失败，请手动安装 Android SDK Platform Tools")
        print("下载地址：https://developer.android.com/studio/releases/platform-tools")
