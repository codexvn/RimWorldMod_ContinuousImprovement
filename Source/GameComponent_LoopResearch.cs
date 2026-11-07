using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ContinuousImprovement
{
    /// <summary>
    /// 全部循环科技层数的存档持久化（按 defName 的字典）。引擎在 Game 初始化时
    /// 按类型自动扫描实例化，无需注册。兼容旧版单科技存档的 int 层数字段。
    /// </summary>
    public class GameComponent_LoopResearch : GameComponent
    {
        private Dictionary<string, int> _scribeLayers;
        private int _legacyLayers;

        public GameComponent_LoopResearch(Game game) { }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            LoopResearch.ResetAllForNewGame();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            // Def 的 baseCost 是跨存档全局对象，进档后按本档层数重算
            LoopResearch.ApplyAll();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                _scribeLayers = LoopResearch.LayersSnapshot();
            }
            Scribe_Collections.Look(ref _scribeLayers, "loopLayers", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (_scribeLayers == null)
                {
                    _scribeLayers = new Dictionary<string, int>();
                }
                // 旧版（单科技）存档迁移：cyclicImprovementLayers int → 持续改进协议条目
                Scribe_Values.Look(ref _legacyLayers, "cyclicImprovementLayers", 0);
                if (_legacyLayers > 0 && !_scribeLayers.ContainsKey("ContinuousImprovementProtocol"))
                {
                    _scribeLayers["ContinuousImprovementProtocol"] = _legacyLayers;
                }
                _legacyLayers = 0;
                LoopResearch.SetLayers(_scribeLayers);
            }
        }
    }
}
