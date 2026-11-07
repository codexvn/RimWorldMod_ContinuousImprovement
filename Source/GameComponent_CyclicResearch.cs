using RimWorld;
using Verse;

namespace ContinuousImprovement
{
    /// <summary>
    /// 持续改进协议层数的存档持久化。引擎在 Game 初始化时按类型自动扫描实例化
    /// （Game.InitNewGame 遍历 GameComponent 子类），无需注册。
    /// </summary>
    public class GameComponent_CyclicResearch : GameComponent
    {
        public GameComponent_CyclicResearch(Game game) { }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            CyclicResearch.ResetForNewGame();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            CyclicResearch.OnLoadedGame();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref CyclicResearch._layers, "cyclicImprovementLayers", 0);
        }
    }
}
