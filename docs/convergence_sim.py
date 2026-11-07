#!/usr/bin/env python3
"""持续改进协议（循环研究科技）数值模拟：README 数据源（临时分析脚本）。

模型（均已对 RimWorld 1.7 反编译源码与官方 XML 核实）：
  ResearchSpeed(pawn) = WorkSpeedGlobal × (0.08 + 0.115×智力等级) × 能力系数
  每人每日研究点 = ResearchSpeed × 研究台系数 × 0.00825 × 60000 (= ×495)
  第 n 层成本 C(n) = 3000 × 1.25^n
  加法叠加 M(n) = 1 + 0.10*n ；乘法叠加 M(n) = 1.10^n
  每层耗时 t(n) = C(n) / (T0 × M(n))
假设：健康成人（能力=1、WorkSpeedGlobal=1）、室内常温房间、难度系数 1。
"""
C0, K = 3000.0, 1.25
TPD = 0.00825 * 60000  # 495 点/天

def M_add(n): return 1 + 0.10 * n
def M_mul(n): return 1.10 ** n
def cost(n): return C0 * K ** n

PROFILES = [
    ("前期", 1, 8, 0.75),   # 1 人，智力 8，简易研究台
    ("中期", 2, 12, 1.00),  # 2 人，智力 12，高科技研究台
    ("后期", 3, 16, 1.10),  # 3 人，智力 16，高科技台 + 多元分析仪
]

T0s = {}
for name, cnt, lvl, bench in PROFILES:
    T0s[name] = (0.08 + 0.115 * lvl) * bench * TPD * cnt

def layers_within(T0, budget, mode):
    n, days = 0, 0.0
    while days <= budget:
        t = cost(n) / (T0 * (M_mul(n) if mode == "mul" else M_add(n)))
        if days + t > budget:
            break
        days += t
        n += 1
    return n

print("### 阶段产能（技能显式建模）\n")
print("| 阶段 | 研究员 | 智力等级 | 研究台 | 合计产能（点/天） |")
print("|------|------|------|------|------|")
for name, cnt, lvl, bench in PROFILES:
    print(f"| {name} | {cnt} 人 | {lvl}（系数 {0.08 + 0.115 * lvl:.2f}） | ×{bench:.2f} | {T0s[name]:.0f} |")

print("\n### 层数 → 全局工作速度加成与成本\n")
print("| 层数 N | 加法 1+0.1N | 乘法 1.1^N | 第 N+1 层成本 |")
print("|------|------|------|------|")
for n in [1, 2, 3, 5, 8, 10, 12, 15, 20, 25, 30]:
    print(f"| {n} | ×{M_add(n):.1f} | ×{M_mul(n):.2f} | {cost(n):,.0f} |")

print("\n### 每层循环耗时 t(n)（天）\n")
print("| 层数 n | 前期 加法 | 前期 乘法 | 中期 加法 | 中期 乘法 | 后期 加法 | 后期 乘法 |")
print("|------|------|------|------|------|------|------|")
for n in [0, 1, 2, 3, 5, 8, 10, 12, 15, 20]:
    cells = [f"{cost(n) / (T0s[p] * (M_mul(n) if m == 'mul' else M_add(n))):.2f}"
             for p in ["前期", "中期", "后期"] for m in ["add", "mul"]]
    print(f"| {n} | " + " | ".join(cells) + " |")

print("\n### 累计耗时（从第 0 层研到第 N 层，天）\n")
print("| 完成层数 N | 前期 加法 | 前期 乘法 | 中期 加法 | 中期 乘法 | 后期 加法 | 后期 乘法 |")
print("|------|------|------|------|------|------|------|")
for N in [1, 3, 5, 8, 10, 12, 15]:
    cells = []
    for p in ["前期", "中期", "后期"]:
        for m in ["add", "mul"]:
            cum = sum(cost(n) / (T0s[p] * (M_mul(n) if m == "mul" else M_add(n))) for n in range(N))
            cells.append(f"{cum:.1f}")
    print(f"| {N} | " + " | ".join(cells) + " |")

print("\n### 发散对照（恒定成本 + 乘法）：理论无限层总耗时 = 3000/T0 × 11\n")
for name, _, _, _ in PROFILES:
    print(f"- {name}（T0={T0s[name]:.0f}）：{C0 / T0s[name] * 11:.1f} 天达到无限层")

print("\n### 阶段预算内可达层数（HTML 表格用）\n")
BUDGETS = {"前期": 10, "中期": 30, "后期": 90}
for name, _, _, _ in PROFILES:
    a = layers_within(T0s[name], BUDGETS[name], "add")
    m = layers_within(T0s[name], BUDGETS[name], "mul")
    print(f"- {name}（预算 {BUDGETS[name]} 天）：加法 {a} 层 ×{M_add(a):.2f} | 乘法 {m} 层 ×{M_mul(m):.2f}")
