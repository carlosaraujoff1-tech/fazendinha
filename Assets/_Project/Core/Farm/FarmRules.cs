using System;
using Game.Core.Catalog;
using Game.Core.Economy;
using Game.Core.Simulation;
using Game.Core.State;

namespace Game.Core.Farm
{
    public enum PlotPhase { Empty, Growing, Ready }

    /// <summary>Consultas puras sobre parcelas (usadas pelos handlers e pela apresentação).</summary>
    public static class FarmRules
    {
        public static PlotPhase GetPhase(PlotState plot, long nowMs)
        {
            if (plot == null || plot.IsEmpty) return PlotPhase.Empty;
            return nowMs >= plot.ReadyAtMs ? PlotPhase.Ready : PlotPhase.Growing;
        }

        /// <summary>Progresso visual de 0 a 1 (considera a rega, que adianta o fim).</summary>
        public static float Progress(PlotState plot, long nowMs)
        {
            if (plot == null || plot.IsEmpty) return 0f;
            long total = plot.ReadyAtMs - plot.PlantedAtMs;
            if (total <= 0 || nowMs >= plot.ReadyAtMs) return 1f;
            if (nowMs <= plot.PlantedAtMs) return 0f;
            return (float)(nowMs - plot.PlantedAtMs) / total;
        }

        public static long RemainingMs(PlotState plot, long nowMs)
        {
            if (plot == null || plot.IsEmpty) return 0;
            return Math.Max(0, plot.ReadyAtMs - nowMs);
        }

        /// <summary>Fase visual 1..<paramref name="stages"/> (modelos _1 a _4 do pack de culturas).</summary>
        public static int VisualStage(PlotState plot, long nowMs, int stages = 4)
        {
            if (stages < 1) throw new ArgumentOutOfRangeException(nameof(stages));
            if (plot == null || plot.IsEmpty) return 0;
            float p = Progress(plot, nowMs);
            if (p >= 1f) return stages;
            return Math.Min(stages - 1, 1 + (int)(p * (stages - 1)));
        }

        /// <summary>Quanto a rega reduz agora: <c>WaterTimeReduction</c> do tempo restante.</summary>
        public static long WaterReductionMs(PlotState plot, long nowMs, EconomyConfig economy) =>
            (long)Math.Round(RemainingMs(plot, nowMs) * economy.WaterTimeReduction);

        public static long NextPlotCost(GameState state, EconomyConfig economy) =>
            CostCurve.RepeatableCost(economy.PlotBaseCost, state.Farm.PlotsBought, economy.RepeatableCostGrowth);

        public static int SeedsOf(GameState state, string cropId) =>
            cropId != null && state.Farm.Seeds.TryGetValue(cropId, out int n) ? n : 0;

        public static bool IsCropUnlocked(CropDef crop, GameState state) => state.Progress.Level >= crop.UnlockLevel;

        internal static bool TryGetPlot(GameContext ctx, int index, out PlotState plot)
        {
            var plots = ctx.State.Farm.Plots;
            plot = index >= 0 && index < plots.Count ? plots[index] : null;
            return plot != null;
        }
    }
}
