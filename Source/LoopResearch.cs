using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ContinuousImprovement
{
    /// <summary>
    /// 循环科技的核心账本：按 defName 记录各项科技的层数，统一处理计层、成本缩放、
    /// 名称层数后缀与自动续研。层数由 <see cref="GameComponent_LoopResearch"/> 随存档持久化。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class LoopResearch
    {
        public const float CostGrowthPerLayer = 1.25f;

        private static readonly Dictionary<string, int> _layers = new Dictionary<string, int>();

        static LoopResearch()
        {
            // StatPart 在 StatWorker.FinalizeValue 统一应用：为每项循环科技的每个目标属性注入一份
            foreach (var tech in LoopTech.All)
            {
                foreach (var stat in tech.Stats)
                {
                    stat.parts ??= new List<StatPart>();
                    if (!stat.parts.OfType<StatPart_LoopBonus>().Any(p => p.Tech == tech))
                    {
                        stat.parts.Add(new StatPart_LoopBonus(tech));
                    }
                }
            }
        }

        public static CyclicStackMode Mode =>
            ContinuousImprovementMod.Instance?.Settings?.StackMode ?? CyclicStackMode.Additive;

        public static LoopTech ByDefName(string defName) =>
            LoopTech.All.FirstOrDefault(t => t.DefName == defName);

        public static int Layers(LoopTech t) => _layers.TryGetValue(t.DefName, out var n) ? n : 0;

        public static float Multiplier(LoopTech t) => t.Multiplier(Layers(t), Mode);

        public static float CostAt(LoopTech t, int layers) =>
            (float)Math.Round(t.BaseCost * Math.Pow(CostGrowthPerLayer, layers));

        public static float NextCost(LoopTech t) => CostAt(t, Layers(t));

        /// <summary>科技的本地铁基准名（不带层数后缀）；首次访问时缓存 DefInjected 注入后的名称。</summary>
        public static string BaseLabel(LoopTech t)
        {
            var def = DefDatabase<ResearchProjectDef>.GetNamed(t.DefName, false);
            if (def == null)
            {
                return t.DefName;
            }
            return t.CachedBaseLabel ??= def.label;
        }

        public static void SetLayers(Dictionary<string, int> layers)
        {
            _layers.Clear();
            foreach (var kv in layers)
            {
                _layers[kv.Key] = kv.Value;
            }
        }

        public static Dictionary<string, int> LayersSnapshot() => new Dictionary<string, int>(_layers);

        /// <summary>一项循环科技完成一层：计层、缩放成本、清零进度、通知并自动续研。</summary>
        public static void OnLayerCompleted(LoopTech t, bool notify, bool reselect)
        {
            var def = DefDatabase<ResearchProjectDef>.GetNamed(t.DefName, false);
            if (def == null)
            {
                return;
            }
            _layers[t.DefName] = Layers(t) + 1;
            def.baseCost = CostAt(t, Layers(t));
            ResetProgress(def);
            UpdateLabel(t);
            if (notify)
            {
                Messages.Message(
                    "Loop.LayerCompleted".Translate(
                        BaseLabel(t),
                        Layers(t),
                        ("Loop.Effect." + t.DefName).Translate(),
                        Multiplier(t).ToStringPercent()),
                    MessageTypeDefOf.PositiveEvent);
            }
            // FinishProject 会清空当前项目；原项目即本科技时自动续研，玩家/AI 只需设定一次
            if (reselect && Find.ResearchManager != null && Find.ResearchManager.GetProject() == null)
            {
                Find.ResearchManager.SetCurrentProject(def);
            }
            Log.Message("[ContinuousImprovement] " + t.DefName + " layer " + Layers(t)
                + " complete, multiplier ×" + Multiplier(t).ToStringPercent());
        }

        /// <summary>科技名称实时携带层数；清除 LabelCap 缓存后全界面即时刷新。</summary>
        public static void UpdateLabel(LoopTech t)
        {
            var def = DefDatabase<ResearchProjectDef>.GetNamed(t.DefName, false);
            if (def == null)
            {
                return;
            }
            BaseLabel(t);
            def.label = Layers(t) > 0
                ? t.CachedBaseLabel + "Loop.LayerSuffix".Translate(Layers(t))
                : t.CachedBaseLabel;
            def.ClearCachedData();
        }

        /// <summary>按当前层数刷新全部循环科技的成本与名称（读档/切档/新档后调用）。</summary>
        public static void ApplyAll()
        {
            foreach (var t in LoopTech.All)
            {
                var def = DefDatabase<ResearchProjectDef>.GetNamed(t.DefName, false);
                if (def == null)
                {
                    continue;
                }
                def.baseCost = CostAt(t, Layers(t));
                UpdateLabel(t);
            }
        }

        public static void ResetAllForNewGame()
        {
            _layers.Clear();
            ApplyAll();
        }

        private static void ResetProgress(ResearchProjectDef def)
        {
            var rm = Find.ResearchManager;
            if (rm == null)
            {
                return;
            }
            var progress = (Dictionary<ResearchProjectDef, float>)AccessTools
                .Field(typeof(ResearchManager), "progress").GetValue(rm);
            progress?.SetOrAdd(def, 0f);
        }
    }
}
