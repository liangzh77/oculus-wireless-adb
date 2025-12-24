#!/usr/bin/env python3
"""
测试 ADB 是否正常工作
"""

import os
import subprocess
import sys
from pathlib import Path

def test_adb():
    """测试 ADB 功能"""
    print("测试 ADB 安装...")
    
    # 方法1：使用 shutil.which
    import shutil
    adb_path = shutil.which('adb')
    if adb_path:
        print(f"✓ 通过 PATH 找到 ADB：{adb_path}")
    else:
        print("✗ 未在 PATH 中找到 ADB")
        
        # 方法2：检查本地安装的 ADB
        local_adb = Path("platform-tools/adb.exe")
        if local_adb.exists():
            print(f"✓ 找到本地 ADB：{local_adb.absolute()}")
            adb_path = str(local_adb.absolute())
        else:
            print("✗ 未找到本地 ADB")
            return False
    
    # 测试 ADB 命令
    try:
        print(f"\n测试 ADB 命令：{adb_path}")
        result = subprocess.run([adb_path, 'version'], 
                              capture_output=True, 
                              text=True, 
                              timeout=10)
        
        if result.returncode == 0:
            print("✓ ADB 命令执行成功")
            print(f"版本信息：\n{result.stdout}")
            return True
        else:
            print(f"✗ ADB 命令执行失败：{result.stderr}")
            return False
            
    except subprocess.TimeoutExpired:
        print("✗ ADB 命令超时")
        return False
    except Exception as e:
        print(f"✗ 执行 ADB 命令时出错：{e}")
        return False

if __name__ == "__main__":
    print("ADB 测试工具")
    print("=" * 20)
    
    if test_adb():
        print("\n✓ ADB 测试通过！")
        sys.exit(0)
    else:
        print("\n✗ ADB 测试失败！")
        sys.exit(1)
