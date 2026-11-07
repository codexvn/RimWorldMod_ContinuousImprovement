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
        }
    }

    public class ContinuousImprovementMod : Mod
    {
        private static readonly int[] TableLayers = { 1, 5, 10, 15, 20, 30 };
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

            var def = CyclicResearch.Def;
            if (def != null)
            {
                listing.Label("ContinuousImprovement.StatusRow".Translate(
                    CyclicResearch.Layers,
                    CyclicResearch.Multiplier.ToString("0.##"),
                    def.baseCost.ToString("F0")));
            }
            listing.Gap(10f);

            DrawLayerTable(listing);

            listing.End();
            Widgets.EndScrollView();
        }

        private static float CalcContentHeight()
        {
            // 切换行 3 + 状态行 1 + 表头 1 + 数据 6 + 当前行 1，逐行 30f/24f + 余量
            return 30f * 4 + 32f + 22f + 24f * 7 + 40f;
        }

        /// <summary>层数 → 两种算法加成与下一层成本的对照表；末行高亮当前状态。</summary>
        private static void DrawLayerTable(Listing_Standard listing)
        {
            var headerRect = listing.GetRect(22f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.45f, 0.5f, 0.6f, 1f);
            DrawRow(headerRect,
                "ContinuousImprovement.ColLayers".Translate(),
                "ContinuousImprovement.ColAdditive".Translate(),
                "ContinuousImprovement.ColMultiplicative".Translate(),
                "ContinuousImprovement.ColCost".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Widgets.DrawBoxSolid(new Rect(headerRect.x, headerRect.yMax - 1f, headerRect.width, 1f), new Color(0.7f, 0.7f, 0.7f, 0.5f));

            foreach (var n in TableLayers)
            {
                DrawRow(listing.GetRect(24f),
                    n.ToString(),
                    "×" + CyclicResearch.AdditiveAt(n).ToString("0.0"),
                    "×" + CyclicResearch.MultiplicativeAt(n).ToString("0.00"),
                    ((long)CyclicResearch.CostAt(n)).ToString("N0"));
            }

            var def = CyclicResearch.Def;
            var curRect = listing.GetRect(24f);
            Widgets.DrawBoxSolid(curRect, new Color(0.22f, 0.4f, 0.6f, 0.22f));
            DrawRow(curRect,
                "ContinuousImprovement.CurrentLayer".Translate(CyclicResearch.Layers),
                "×" + CyclicResearch.AdditiveAt(CyclicResearch.Layers).ToString("0.##"),
                "×" + CyclicResearch.MultiplicativeAt(CyclicResearch.Layers).ToString("0.##"),
                def != null ? ((long)def.baseCost).ToString("N0") : "—");
        }

        private static void DrawRow(Rect r, string c1, string c2, string c3, string c4)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(r.x + 8f, r.y, r.width * 0.16f, r.height), c1);
            Widgets.Label(new Rect(r.x + r.width * 0.24f, r.y, r.width * 0.24f, r.height), c2);
            Widgets.Label(new Rect(r.x + r.width * 0.50f, r.y, r.width * 0.22f, r.height), c3);
            Widgets.Label(new Rect(r.x + r.width * 0.74f, r.y, r.width * 0.26f, r.height), c4);
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
