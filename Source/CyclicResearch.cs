using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ContinuousImprovement
{
    /// <summary>
    /// 持续改进协议（Continuous Improvement Protocol）：可无限循环研究的科技。
    /// 层数账本、全局工作速度倍率计算、完成处理与成本缩放均在此收口。
    /// 层数由 <see cref="GameComponent_CyclicResearch"/> 随存档持久化。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class CyclicResearch
    {
        public const string DefName = "ContinuousImprovementProtocol";
        public const float BaseCost = 3000f;
        public const float CostGrowthPerLayer = 1.25f;
        public const float BonusPerLayer = 0.10f;

        internal static int _layers;

        // 本地化基准名称缓存：首次调用时 def.label 已由 DefInjected 注入当前语言，缓存后追加层数后缀
        private static string _baseLabelCache;

        public static int Layers => _layers;

        public static ResearchProjectDef Def => DefDatabase<ResearchProjectDef>.GetNamed(DefName, false);

        /// <summary>
        /// 当前全局工作速度倍率。公式跟随 Mod 设置（全局配置）实时读取：
        /// 切换叠加方式后无需重启，下一次层数完成与下一次属性求值即用新公式。
        /// </summary>
        public static float Multiplier
        {
            get
            {
                var mode = ContinuousImprovementMod.Instance?.Settings?.StackMode ?? CyclicStackMode.Additive;
                return mode == CyclicStackMode.Multiplicative ? MultiplicativeAt(_layers) : AdditiveAt(_layers);
            }
        }

        public static float AdditiveAt(int layers) => 1f + BonusPerLayer * layers;

        public static float MultiplicativeAt(int layers) => (float)Math.Pow(1f + BonusPerLayer, layers);

        public static float CostAt(int layers) => (float)Math.Round(BaseCost * Math.Pow(CostGrowthPerLayer, layers));

        static CyclicResearch()
        {
            // 挂载点：StatPart 在 StatWorker.FinalizeValue 统一应用，自动传导到全部以
            // WorkSpeedGlobal 为 statFactor 的工作速度（研究、制作、建造、采矿、种植等）
            var parts = StatDefOf.WorkSpeedGlobal.parts;
            if (parts != null && !parts.OfType<StatPart_CyclicResearch>().Any())
            {
                parts.Add(new StatPart_CyclicResearch());
            }
        }

        /// <summary>一层研究完成：计层、缩放下一层成本、清零进度、通知并自动续研。</summary>
        public static void OnLayerCompleted(bool notify, bool reselect)
        {
            var def = Def;
            if (def == null)
            {
                return;
            }
            _layers++;
            ApplyCostScaling();
            ResetProgress(def);
            UpdateLabel();
            if (notify)
            {
                Messages.Message(
                    "ContinuousImprovement.LayerCompleted".Translate(_layers, Multiplier.ToStringPercent()),
                    MessageTypeDefOf.PositiveEvent);
            }
            // FinishProject 会清空当前项目；原项目即本协议时自动续研，玩家/AI 只需设定一次
            if (reselect && Find.ResearchManager != null && Find.ResearchManager.GetProject() == null)
            {
                Find.ResearchManager.SetCurrentProject(def);
            }
            Log.Message("[ContinuousImprovement] Layer " + _layers + " complete, global work speed ×" + Multiplier.ToStringPercent());
        }

        /// <summary>第 N 层成本 = BaseCost × 1.25^N，写入 Def 供研究树与进度条直接显示。</summary>
        public static void ApplyCostScaling()
        {
            Def?.baseCost = CostAt(_layers);
        }

        /// <summary>科技名称实时携带层数（如「持续改进协议（3 层）」），清除 LabelCap 缓存后全界面即时刷新。</summary>
        public static void UpdateLabel()
        {
            var def = Def;
            if (def == null)
            {
                return;
            }
            if (_baseLabelCache == null)
            {
                _baseLabelCache = def.label;
            }
            def.label = _layers > 0 ? _baseLabelCache + "ContinuousImprovement.LayerSuffix".Translate(_layers) : _baseLabelCache;
            def.ClearCachedData();
        }

        public static void ResetForNewGame()
        {
            _layers = 0;
            ApplyCostScaling();
            UpdateLabel();
        }

        public static void OnLoadedGame()
        {
            // Def 的 baseCost 是跨存档全局对象，进档后按本档层数重算
            ApplyCostScaling();
            UpdateLabel();
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
