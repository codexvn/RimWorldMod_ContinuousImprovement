#!/usr/bin/env python3
"""把构建产物组装并部署到 publish/ContinuousImprovement（游戏 Mods 目录联接指向此处）。

用法：
  dotnet build && python scripts/deploy_mod.py

要点：
  - 游戏运行中会锁定已加载的 DLL；.NET 运行时加载程序集带 FILE_SHARE_DELETE，
    被锁文件可改名挪走（进程继续用旧句柄），改名后放入新文件，游戏重启即生效。
  - 挪走的旧文件命名为 *.locked-<时间戳>，本脚本每次部署时顺带清理 24 小时前的残留。
"""
import os
import shutil
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BIN_DLL = os.path.join(ROOT, "bin", "ContinuousImprovement.dll")
LIVE = os.path.join(ROOT, "publish", "ContinuousImprovement")
LOCK_MARK = ".locked-"

# 源文件 → 部署相对路径
SOURCES = [(BIN_DLL, os.path.join("1.7", "Assemblies", "ContinuousImprovement.dll"))]


def collect_dir(src_dir, rel_prefix):
    for base, _, files in os.walk(src_dir):
        for f in files:
            full = os.path.join(base, f)
            SOURCES.append((full, os.path.join(rel_prefix, os.path.relpath(full, src_dir))))


collect_dir(os.path.join(ROOT, "About"), "About")
collect_dir(os.path.join(ROOT, "Defs"), "Defs")
collect_dir(os.path.join(ROOT, "Languages"), "Languages")
readme = os.path.join(ROOT, "README.md")
if os.path.exists(readme):
    SOURCES.append((readme, "README.md"))


def deploy_file(src, dst):
    if os.path.exists(dst):
        with open(src, "rb") as a, open(dst, "rb") as b:
            if a.read() == b.read():
                return "skip"
    try:
        shutil.copy2(src, dst)
        return "copy"
    except PermissionError:
        locked = f"{dst}{LOCK_MARK}{int(time.time())}"
        os.rename(dst, locked)  # 游戏进程锁定：改名挪走（FILE_SHARE_DELETE 允许）
        shutil.copy2(src, dst)
        return "swap"


def gc_locked():
    now = time.time()
    cleaned = []
    for base, _, files in os.walk(LIVE):
        for f in files:
            if LOCK_MARK not in f:
                continue
            full = os.path.join(base, f)
            try:
                if now - os.path.getmtime(full) > 86400:
                    os.remove(full)
                    cleaned.append(os.path.relpath(full, LIVE))
            except OSError:
                pass
    return cleaned


def main():
    if not os.path.exists(BIN_DLL):
        print(f"缺少构建产物: {BIN_DLL}\n请先 dotnet build")
        sys.exit(1)
    stats = {"copy": 0, "swap": 0, "skip": 0}
    for src, rel in SOURCES:
        if not os.path.exists(src):
            print(f"跳过缺失文件: {src}")
            continue
        dst = os.path.join(LIVE, rel)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        stats[deploy_file(src, dst)] += 1
    cleaned = gc_locked()
    print(f"部署完成: 更新 {stats['copy']} / 锁定换新 {stats['swap']} / 相同跳过 {stats['skip']}")
    if cleaned:
        print(f"清理过期锁定残留 {len(cleaned)} 个: {', '.join(cleaned)}")
    if stats["swap"]:
        print("游戏运行中更换的 DLL 需重启游戏后生效")


if __name__ == "__main__":
    main()
