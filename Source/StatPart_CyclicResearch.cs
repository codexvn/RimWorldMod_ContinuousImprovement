using RimWorld;
using Verse;

namespace ContinuousImprovement
{
    /// <summary>
    /// 把持续改进协议的层数倍率注入殖民者的全局工作速度（WorkSpeedGlobal）。
    /// 仅作用于玩家阵营 Pawn（殖民者、奴隶、囚犯与玩家机械体）。
    /// </summary>
    public class StatPart_CyclicResearch : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (IsActiveFor(req))
            {
                val *= CyclicResearch.Multiplier;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!IsActiveFor(req) || CyclicResearch.Layers <= 0)
            {
                return null;
            }
            return "ContinuousImprovement.StatLine".Translate(CyclicResearch.Layers, CyclicResearch.Multiplier.ToStringPercent());
        }

        private static bool IsActiveFor(StatRequest req)
        {
            if (!req.HasThing || req.Thing is not Pawn pawn)
            {
                return false;
            }
            if (Current.ProgramState != ProgramState.Playing)
            {
                return false;
            }
            return pawn.Faction == Faction.OfPlayer;
        }
    }
}
