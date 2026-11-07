using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ContinuousImprovement
{
    /// <summary>
    /// 持续改进协议的事件挂点。启动时通过 StaticConstructorOnStartup 应用补丁；
    /// 所有常规完成路径（殖民者研究 / 任务奖励 AddProgress / 异常知识）都收口在
    /// ResearchManager.FinishProject，postfix 挂在此处保证任何路径完成一层都不漏计。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Hook_CyclicResearch
    {
        static Hook_CyclicResearch()
        {
            new Harmony("codexvn.ContinuousImprovement").PatchAll(typeof(Hook_CyclicResearch).Assembly);
        }

        [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
        public static class Patch_ResearchManager_FinishProject
        {
            // 完成弹窗（Dialog_NodeTree）强制暂停并吸收输入，循环场景逐层打断，仅对本协议抑制
            public static void Prefix(ResearchProjectDef proj, ref bool doCompletionDialog)
            {
                if (proj != null && proj.defName == CyclicResearch.DefName)
                {
                    doCompletionDialog = false;
                }
            }

            public static void Postfix(ResearchProjectDef proj)
            {
                if (proj == null || proj.defName != CyclicResearch.DefName)
                {
                    return;
                }
                try
                {
                    CyclicResearch.OnLayerCompleted(notify: true, reselect: true);
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
            // dev 工具直接填满进度、不经过 FinishProject，会把本协议打成已完成且不计层，
            // 导致循环卡死；此处校正为「完成一层并回到可研究状态」
            public static void Postfix()
            {
                var def = CyclicResearch.Def;
                if (def == null || !def.IsFinished)
                {
                    return;
                }
                try
                {
                    CyclicResearch.OnLayerCompleted(notify: false, reselect: false);
                }
                catch (Exception ex)
                {
                    Log.Error("[ContinuousImprovement] dev 全完成校正失败: " + ex);
                }
            }
        }
    }
}
