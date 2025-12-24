#!/usr/bin/env python3
"""
测试 discover-and-connect.py 脚本的基本功能
"""

import sys
import time
from pathlib import Path

# 添加当前目录到 Python 路径
sys.path.insert(0, str(Path(__file__).parent))

def test_imports():
    """测试所有必要的模块是否可以导入"""
    try:
        import shutil
        from os import _exit
        from pathlib import Path
        from subprocess import Popen, PIPE
        from zeroconf import ServiceBrowser, ServiceListener, Zeroconf
        print("✓ 所有模块导入成功")
        return True
    except ImportError as e:
        print(f"✗ 模块导入失败：{e}")
        return False

def test_adb_detection():
    """测试 ADB 检测功能"""
    try:
        import shutil
        from pathlib import Path
        
        # 测试 PATH 中的 ADB
        adb_path = shutil.which('adb')
        if adb_path:
            print(f"✓ 在 PATH 中找到 ADB：{adb_path}")
            return True
        
        # 测试本地 ADB
        local_adb = Path("platform-tools/adb.exe")
        if local_adb.exists():
            print(f"✓ 找到本地 ADB：{local_adb.absolute()}")
            return True
        
        print("✗ 未找到 ADB")
        return False
    except Exception as e:
        print(f"✗ ADB 检测出错：{e}")
        return False

def test_zeroconf():
    """测试 Zeroconf 功能"""
    try:
        from zeroconf import Zeroconf, ServiceBrowser, ServiceListener
        
        class TestListener(ServiceListener):
            def add_service(self, zc, type_, name):
                pass
            def update_service(self, zc, type_, name):
                pass
            def remove_service(self, zc, type_, name):
                pass
        
        zeroconf = Zeroconf()
        listener = TestListener()
        browser = ServiceBrowser(zeroconf, "_adb-tls-connect._tcp.local.", listener)
        
        print("✓ Zeroconf 初始化成功")
        
        # 清理
        zeroconf.close()
        return True
    except Exception as e:
        print(f"✗ Zeroconf 测试失败：{e}")
        return False

if __name__ == "__main__":
    print("脚本功能测试")
    print("=" * 30)
    
    tests = [
        ("模块导入", test_imports),
        ("ADB 检测", test_adb_detection),
        ("Zeroconf 功能", test_zeroconf),
    ]
    
    passed = 0
    total = len(tests)
    
    for test_name, test_func in tests:
        print(f"\n测试 {test_name}...")
        if test_func():
            passed += 1
        else:
            print(f"✗ {test_name} 测试失败")
    
    print(f"\n测试结果：{passed}/{total} 通过")
    
    if passed == total:
        print("✓ 所有测试通过！脚本应该可以正常工作")
        sys.exit(0)
    else:
        print("✗ 部分测试失败，请检查问题")
        sys.exit(1)
