using System;
using RimWorld;
using Verse;

namespace ContinuousImprovement
{
    /// <summary>
    /// 一项循环科技的定义：目标 Def、参数与挂点属性。全部循环科技注册于 <see cref="All"/>，
    /// 新增一项只需在此追加条目并补对应 ResearchProjectDef 与翻译键。
    /// </summary>
    public sealed class LoopTech
    {
        public string DefName;
        public float BaseCost = 3000f;

        /// <summary>每层幅度；加法模式为 1 + Bonus×N，乘法模式为 (1+Bonus)^N。</summary>
        public float BonusPerLayer = 0.10f;

        /// <summary>反向属性（值越小越好，如瞄准时长、食物消耗）：倍率取倒数。</summary>
        public bool Inverted;

        /// <summary>倍率下限（如代谢 ×0.25，防止食物消耗被压到零）。</summary>
        public float? MinMultiplier;

        /// <summary>倍率上限（概率型属性防免疫：闪避/命中/护甲 ×2，防止高层数近战或伤害免疫）。</summary>
        public float? MaxMultiplier;

        /// <summary>挂点属性；为空表示走专用 Harmony 挂点（代谢 → Need_Food.FoodFallPerTickAssumingCategory）。</summary>
        public StatDef[] Stats = System.Array.Empty<StatDef>();

        /// <summary>属性作用于玩家阵营的「物」而非殖民者（耐久 → 玩家建筑与穿戴装备的 MaxHitPoints）。</summary>
        public bool ThingScope;

        /// <summary>运行期缓存的本地铁基准名（首次使用时 Def 已由 DefInjected 注入当前语言）。</summary>
        public string CachedBaseLabel;

        public static readonly LoopTech ContinuousImprovement = new LoopTech
        {
            DefName = "ContinuousImprovementProtocol",
            Stats = new[] { StatDefOf.WorkSpeedGlobal },
        };

        public static readonly LoopTech NimbleEvasion = new LoopTech
        {
            DefName = "NimbleEvasionProtocol",
            BonusPerLayer = 0.05f,
            MaxMultiplier = 2f,
            Stats = new[] { StatDefOf.MeleeDodgeChance },
        };

        public static readonly LoopTech Precision = new LoopTech
        {
            DefName = "PrecisionProtocol",
            BonusPerLayer = 0.05f,
            MaxMultiplier = 2f,
            Stats = new[] { StatDefOf.MeleeHitChance, StatDefOf.ShootingAccuracyPawn },
        };

        public static readonly LoopTech RapidAiming = new LoopTech
        {
            DefName = "RapidAimingProtocol",
            BonusPerLayer = 0.05f,
            Inverted = true,
            Stats = new[] { StatDefOf.AimingDelayFactor },
        };

        public static readonly LoopTech LoadBearing = new LoopTech
        {
            DefName = "LoadBearingProtocol",
            BonusPerLayer = 0.05f,
            Stats = new[] { StatDefOf.CarryingCapacity },
        };

        public static readonly LoopTech SwiftMovement = new LoopTech
        {
            DefName = "SwiftMovementProtocol",
            BonusPerLayer = 0.05f,
            Stats = new[] { StatDefOf.MoveSpeed },
        };

        public static readonly LoopTech EfficientMetabolism = new LoopTech
        {
            DefName = "EfficientMetabolismProtocol",
            BonusPerLayer = 0.05f,
            Inverted = true,
            MinMultiplier = 0.25f,
        };

        public static readonly LoopTech ArmorRating = new LoopTech
        {
            DefName = "ArmorRatingProtocol",
            BonusPerLayer = 0.05f,
            MaxMultiplier = 2f,
            Stats = new[] { StatDefOf.ArmorRating_Sharp, StatDefOf.ArmorRating_Blunt, StatDefOf.ArmorRating_Heat },
        };

        public static readonly LoopTech Durability = new LoopTech
        {
            DefName = "DurabilityProtocol",
            ThingScope = true,
            Stats = new[] { StatDefOf.MaxHitPoints },
        };

        public static readonly LoopTech[] All =
        {
            ContinuousImprovement,
            NimbleEvasion,
            Precision,
            RapidAiming,
            LoadBearing,
            SwiftMovement,
            EfficientMetabolism,
            ArmorRating,
            Durability,
        };

        public float Multiplier(int layers, CyclicStackMode mode)
        {
            float m = mode == CyclicStackMode.Multiplicative
                ? (float)Math.Pow(1f + BonusPerLayer, layers)
                : 1f + BonusPerLayer * layers;
            if (Inverted)
            {
                m = 1f / m;
            }
            if (MinMultiplier.HasValue)
            {
                m = Math.Max(m, MinMultiplier.Value);
            }
            if (MaxMultiplier.HasValue)
            {
                m = Math.Min(m, MaxMultiplier.Value);
            }
            return m;
        }
    }
}
