using RimWorld;
using Verse;

namespace ContinuousImprovement
{
    /// <summary>
    /// 把某项循环科技的层数倍率注入目标属性（仅玩家阵营 Pawn）。
    /// 反向科技（瞄准时长、食物消耗）倍率已取倒数，直接相乘即可。
    /// </summary>
    public sealed class StatPart_LoopBonus : StatPart
    {
        public LoopTech Tech { get; }

        public StatPart_LoopBonus(LoopTech tech)
        {
            Tech = tech;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (IsActiveFor(req))
            {
                val *= LoopResearch.Multiplier(Tech);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!IsActiveFor(req) || LoopResearch.Layers(Tech) <= 0)
            {
                return null;
            }
            return "Loop.StatLine".Translate(
                LoopResearch.BaseLabel(Tech),
                LoopResearch.Layers(Tech),
                LoopResearch.Multiplier(Tech).ToStringPercent());
        }

        private bool IsActiveFor(StatRequest req)
        {
            if (!req.HasThing || req.Thing == null || Current.ProgramState != ProgramState.Playing)
            {
                return false;
            }
            if (Tech.ThingScope)
            {
                // 耐久：玩家阵营建筑，以及殖民者正穿戴的装备（装备磨损为固定每日伤害，上限越高寿命越长）
                if (req.Thing is Building building)
                {
                    return building.Faction == Faction.OfPlayer;
                }
                if (req.Thing is Apparel apparel && apparel.Wearer != null)
                {
                    return apparel.Wearer.Faction == Faction.OfPlayer;
                }
                return false;
            }
            return req.Thing is Pawn pawn && pawn.Faction == Faction.OfPlayer;
        }
    }
}
