using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ContinuousImprovement
{
    /// <summary>
    /// 循环科技的事件挂点。启动时通过 StaticConstructorOnStartup 应用补丁。
    /// 所有常规完成路径（殖民者研究 / 任务奖励 AddProgress / 异常知识）都收口在
    /// ResearchManager.FinishProject，postfix 按注册表匹配 defName，任何科技完成一层都不漏计。
    /// 代谢科技挂 Need_Food.FoodFallPerTickAssumingCategory——食物消耗率的唯一计算点；
    /// 其倍率不经过属性系统，另以两处 UI 补丁展示（食物消耗分解行 + 信息卡独立行）。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Hook_LoopResearch
    {
        static Hook_LoopResearch()
        {
            new Harmony("codexvn.ContinuousImprovement").PatchAll(typeof(Hook_LoopResearch).Assembly);
        }

        [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
        public static class Patch_ResearchManager_FinishProject
        {
            // 完成弹窗（Dialog_NodeTree）强制暂停并吸收输入，循环场景逐层打断，仅对循环科技抑制
            public static void Prefix(ResearchProjectDef proj, ref bool doCompletionDialog)
            {
                if (proj != null && LoopResearch.ByDefName(proj.defName) != null)
                {
                    doCompletionDialog = false;
                }
            }

            public static void Postfix(ResearchProjectDef proj)
            {
                var tech = proj == null ? null : LoopResearch.ByDefName(proj.defName);
                if (tech == null)
                {
                    return;
                }
                try
                {
                    LoopResearch.OnLayerCompleted(tech, notify: true, reselect: true);
                }
                catch (Exception ex)
                {
                    Log.Error("[ContinuousImprovement] 层完成处理失败: " + ex);
                }
            }
        }

        [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.DebugSetAllProjectsFinished))]
        public static class Patch_ResearchManager_DebugSetAll
        {
            // dev 工具直接填满进度、不经过 FinishProject，会把循环科技打成已完成且不计层；
            // 逐项校正为「完成一层并回到可研究状态」
            public static void Postfix()
            {
                foreach (var tech in LoopTech.All)
                {
                    var def = DefDatabase<ResearchProjectDef>.GetNamed(tech.DefName, false);
                    if (def == null || !def.IsFinished)
                    {
                        continue;
                    }
                    try
                    {
                        LoopResearch.OnLayerCompleted(tech, notify: false, reselect: false);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[ContinuousImprovement] dev 全完成校正失败: " + ex);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Need_Food), nameof(Need_Food.FoodFallPerTickAssumingCategory))]
        public static class Patch_Need_Food_HungerRate
        {
            // Need.pawn 为 protected 字段（Need.cs:13），经 AccessTools 读取
            private static readonly System.Reflection.FieldInfo PawnField =
                AccessTools.Field(typeof(Need), "pawn");

            // 高效代谢协议：玩家阵营单位的食物消耗按层数倍率缩减（倍率已含 0.25 下限）
            public static void Postfix(Need_Food __instance, ref float __result)
            {
                var pawn = (Pawn)PawnField.GetValue(__instance);
                if (pawn == null || Current.ProgramState != ProgramState.Playing
                    || pawn.Faction != Faction.OfPlayer)
                {
                    return;
                }
                if (LoopResearch.Layers(LoopTech.EfficientMetabolism) <= 0)
                {
                    return;
                }
                __result *= LoopResearch.Multiplier(LoopTech.EfficientMetabolism);
            }
        }

        [HarmonyPatch(typeof(RaceProperties), nameof(RaceProperties.NutritionEatenPerDayExplanation))]
        public static class Patch_RaceProperties_HungerExplanation
        {
            // 高效代谢不经过属性系统，原版「食物消耗」分解里列不出它，各因子与最终值对不上账；
            // 把倍率行插到「最终值」一节之前（商队物品行的悬停用 showCalculations:false，不含分解，不插）
            public static void Postfix(Pawn p, bool showCalculations, ref string __result)
            {
                if (!showCalculations || p == null || p.Faction != Faction.OfPlayer
                    || LoopResearch.Layers(LoopTech.EfficientMetabolism) <= 0)
                {
                    return;
                }
                string line = "Loop.StatLine".Translate(
                    LoopResearch.BaseLabel(LoopTech.EfficientMetabolism),
                    LoopResearch.Layers(LoopTech.EfficientMetabolism),
                    LoopResearch.Multiplier(LoopTech.EfficientMetabolism).ToStringPercent());
                int idx = __result.LastIndexOf("StatsReport_FinalValue".Translate().ToString(), StringComparison.Ordinal);
                __result = idx > 0
                    ? __result.Insert(idx, line + Environment.NewLine)
                    : __result + Environment.NewLine + line;
            }
        }

        [HarmonyPatch(typeof(StatsReportUtility), "StatsToDraw")]
        public static class Patch_StatsReport_StatsToDraw
        {
            // StatsToDraw 有五个同名重载，按参数类型锁定 Thing 版本（殖民者信息卡统计页的入口）
            private static MethodBase TargetMethod() =>
                AccessTools.Method(typeof(StatsReportUtility), "StatsToDraw", new[] { typeof(Thing) });

            // 信息卡「食物」类目下追加一行当前代谢倍率，排在「食物消耗」（显示序 1600）之后
            public static void Postfix(Thing thing, ref IEnumerable<StatDrawEntry> __result)
            {
                if (__result == null || !(thing is Pawn pawn) || pawn.Faction != Faction.OfPlayer
                    || LoopResearch.Layers(LoopTech.EfficientMetabolism) <= 0)
                {
                    return;
                }
                var entry = new StatDrawEntry(
                    StatCategoryDefOf.PawnFood,
                    LoopResearch.BaseLabel(LoopTech.EfficientMetabolism),
                    LoopResearch.Multiplier(LoopTech.EfficientMetabolism).ToStringPercent(),
                    "Loop.StatLine".Translate(
                        LoopResearch.BaseLabel(LoopTech.EfficientMetabolism),
                        LoopResearch.Layers(LoopTech.EfficientMetabolism),
                        LoopResearch.Multiplier(LoopTech.EfficientMetabolism).ToStringPercent()),
                    1601);
                __result = __result.Concat(new[] { entry });
            }
        }
    }
}
