using System;
using Verse;
using RimWorld;
using UnityEngine;

namespace ContinuousImprovement
{
    public enum CyclicStackMode { Additive, Multiplicative }

    public class ContinuousImprovementSettings : ModSettings
    {
        /// <summary>叠加公式（全局配置，不随存档）：下一次层数完成与属性求值时按新公式生效。</summary>
        public CyclicStackMode StackMode = CyclicStackMode.Additive;

        /// <summary>是否需要从帝国购买科技蓝图才能开始研究（全局配置）：关闭时蓝图判定直接通过。</summary>
        public bool RequireTechprint = true;

        public static string[] ModeLabels => new string[]
        {
            "ContinuousImprovement.ModeAdd".Translate(),
            "ContinuousImprovement.ModeMul".Translate()
        };

        public override void ExposeData()
        {
            base.ExposeData();
            var mode = (int)StackMode;
            Scribe_Values.Look(ref mode, "stackMode", (int)CyclicStackMode.Additive);
            StackMode = (CyclicStackMode)mode;
            Scribe_Values.Look(ref RequireTechprint, "requireTechprint", true);
        }
    }

    public class ContinuousImprovementMod : Mod
    {
        private Vector2 _scrollPos;

        public static ContinuousImprovementMod Instance { get; private set; }
        public ContinuousImprovementSettings Settings { get; private set; }

        public ContinuousImprovementMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<ContinuousImprovementSettings>();
        }

        public override string SettingsCategory()
        {
            return "ContinuousImprovement.SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            float h = CalcContentHeight();
            var viewRect = new Rect(0f, 0f, inRect.width - 16f, h);
            Widgets.BeginScrollView(inRect, ref _scrollPos, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.Label("ContinuousImprovement.StackModeRow".Translate(
                ContinuousImprovementSettings.ModeLabels[(int)Settings.StackMode]));
            if (listing.ButtonText("ContinuousImprovement.SwitchButton".Translate()))
            {
                var next = (int)Settings.StackMode + 1;
                if (next >= ContinuousImprovementSettings.ModeLabels.Length) next = 0;
                Settings.StackMode = (CyclicStackMode)next;
            }
            listing.Label("ContinuousImprovement.ModeDescLine1".Translate());
            listing.Label("ContinuousImprovement.ModeDescLine2".Translate());
            listing.Gap(6f);

            var requireTechprint = Settings.RequireTechprint;
            listing.CheckboxLabeled("ContinuousImprovement.TechprintToggle".Translate(), ref requireTechprint,
                "ContinuousImprovement.TechprintToggleTooltip".Translate());
            if (requireTechprint != Settings.RequireTechprint)
            {
                Settings.RequireTechprint = requireTechprint;
                LoopResearch.ApplyTechprintRequirement();
            }
            listing.Gap(6f);

            DrawTechTable(listing);

            listing.End();
            Widgets.EndScrollView();
        }

        private static float CalcContentHeight()
        {
            // 切换行 3 + 蓝图开关 1 + 表头 1 + 数据 9 + 余量
            return 30f * 5 + 32f + 22f + 24f * LoopTech.All.Length + 40f;
        }

        /// <summary>全部循环科技的当前状态表：层数、当前倍率与下一层成本。</summary>
        private static void DrawTechTable(Listing_Standard listing)
        {
            var headerRect = listing.GetRect(22f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.45f, 0.5f, 0.6f, 1f);
            DrawRow(headerRect,
                "ContinuousImprovement.ColTech".Translate(),
                "ContinuousImprovement.ColLayerCount".Translate(),
                "ContinuousImprovement.ColBonus".Translate(),
                "ContinuousImprovement.ColCost".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Widgets.DrawBoxSolid(new Rect(headerRect.x, headerRect.yMax - 1f, headerRect.width, 1f), new Color(0.7f, 0.7f, 0.7f, 0.5f));

            foreach (var tech in LoopTech.All)
            {
                var def = DefDatabase<ResearchProjectDef>.GetNamed(tech.DefName, false);
                if (def == null)
                {
                    continue;
                }
                var rowRect = listing.GetRect(24f);
                if (LoopResearch.Layers(tech) > 0)
                {
                    Widgets.DrawBoxSolid(rowRect, new Color(0.22f, 0.4f, 0.6f, 0.18f));
                }
                DrawRow(rowRect,
                    LoopResearch.BaseLabel(tech),
                    LoopResearch.Layers(tech).ToString(),
                    "×" + LoopResearch.Multiplier(tech).ToString("0.##"),
                    ((long)def.baseCost).ToString("N0"));
            }
        }

        private static void DrawRow(Rect r, string c1, string c2, string c3, string c4)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(r.x + 8f, r.y, r.width * 0.34f, r.height), c1);
            Widgets.Label(new Rect(r.x + r.width * 0.42f, r.y, r.width * 0.14f, r.height), c2);
            Widgets.Label(new Rect(r.x + r.width * 0.58f, r.y, r.width * 0.18f, r.height), c3);
            Widgets.Label(new Rect(r.x + r.width * 0.78f, r.y, r.width * 0.22f, r.height), c4);
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
