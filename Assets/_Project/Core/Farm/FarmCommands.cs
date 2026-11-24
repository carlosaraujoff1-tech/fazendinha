using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Simulation;
using Game.Core.State;

namespace Game.Core.Farm
{
    public sealed class PlantCommand : ICommand
    {
        public PlantCommand(int plotIndex, string cropId) { PlotIndex = plotIndex; CropId = cropId; }
        public int PlotIndex { get; }
        public string CropId { get; }
    }

    public sealed class WaterCommand : ICommand
    {
        public WaterCommand(int plotIndex) { PlotIndex = plotIndex; }
        public int PlotIndex { get; }
    }

    public sealed class HarvestCommand : ICommand
    {
        public HarvestCommand(int plotIndex) { PlotIndex = plotIndex; }
        public int PlotIndex { get; }
    }

    public sealed class BuyPlotCommand : ICommand { }

    public readonly struct PlotChanged : IGameEvent
    {
        public PlotChanged(int plotIndex) { PlotIndex = plotIndex; }
        public int PlotIndex { get; }
    }

    public readonly struct CropHarvested : IGameEvent
    {
        public CropHarvested(int plotIndex, string cropId, string productId, int quantity)
        {
            PlotIndex = plotIndex; CropId = cropId; ProductId = productId; Quantity = quantity;
        }
        public int PlotIndex { get; }
        public string CropId { get; }
        public string ProductId { get; }
        public int Quantity { get; }
    }

    public readonly struct PlotBought : IGameEvent
    {
        public PlotBought(int plotIndex, long cost) { PlotIndex = plotIndex; Cost = cost; }
        public int PlotIndex { get; }
        public long Cost { get; }
    }

    public sealed class PlantHandler : ICommandHandler<PlantCommand>
    {
        public CommandResult Validate(GameContext ctx, PlantCommand c)
        {
            if (!FarmRules.TryGetPlot(ctx, c.PlotIndex, out var plot)) return CommandResult.Fail(CommandErrors.UnknownPlot);
            if (!plot.IsEmpty) return CommandResult.Fail(CommandErrors.PlotOccupied);
            if (!ctx.Catalog.TryGetCrop(c.CropId, out var crop)) return CommandResult.Fail(CommandErrors.UnknownCrop);
            if (!FarmRules.IsCropUnlocked(crop, ctx.State)) return CommandResult.Fail(CommandErrors.Locked);
            if (crop.SeedCost > 0 && !ctx.Wallet.CanAffordCoins(crop.SeedCost)) return CommandResult.Fail(CommandErrors.InsufficientCoins);
            return CommandResult.Ok();
        }

        public void Apply(GameContext ctx, PlantCommand c)
        {
            var crop = ctx.Catalog.GetCrop(c.CropId);
            if (crop.SeedCost > 0) ctx.Wallet.TrySpendCoins(crop.SeedCost, "semente:" + crop.Id);
            var plot = ctx.State.Farm.Plots[c.PlotIndex];
            plot.CropId = crop.Id;
            plot.PlantedAtMs = ctx.NowMs;
            plot.ReadyAtMs = ctx.NowMs + crop.GrowSeconds * 1000L;
            plot.Watered = false;
            ctx.Events.Publish(new PlotChanged(c.PlotIndex));
        }
    }

    public sealed class WaterHandler : ICommandHandler<WaterCommand>
    {
        public CommandResult Validate(GameContext ctx, WaterCommand c)
        {
            if (!FarmRules.TryGetPlot(ctx, c.PlotIndex, out var plot)) return CommandResult.Fail(CommandErrors.UnknownPlot);
            switch (FarmRules.GetPhase(plot, ctx.NowMs))
            {
                case PlotPhase.Empty: return CommandResult.Fail(CommandErrors.PlotEmpty);
                case PlotPhase.Ready: return CommandResult.Fail(CommandErrors.AlreadyReady);
            }
            return plot.Watered ? CommandResult.Fail(CommandErrors.AlreadyWatered) : CommandResult.Ok();
        }

        public void Apply(GameContext ctx, WaterCommand c)
        {
            var plot = ctx.State.Farm.Plots[c.PlotIndex];
            plot.ReadyAtMs -= FarmRules.WaterReductionMs(plot, ctx.NowMs, ctx.Catalog.Economy);
            plot.Watered = true;
            ctx.Events.Publish(new PlotChanged(c.PlotIndex));
        }
    }

    public sealed class HarvestHandler : ICommandHandler<HarvestCommand>
    {
        public CommandResult Validate(GameContext ctx, HarvestCommand c)
        {
            if (!FarmRules.TryGetPlot(ctx, c.PlotIndex, out var plot)) return CommandResult.Fail(CommandErrors.UnknownPlot);
            switch (FarmRules.GetPhase(plot, ctx.NowMs))
            {
                case PlotPhase.Empty: return CommandResult.Fail(CommandErrors.PlotEmpty);
                case PlotPhase.Growing: return CommandResult.Fail(CommandErrors.NotReady);
            }
            return ctx.Catalog.TryGetCrop(plot.CropId, out _)
                ? CommandResult.Ok()
                : CommandResult.Fail(CommandErrors.UnknownCrop); // cultura removida do catálogo
        }

        public void Apply(GameContext ctx, HarvestCommand c)
        {
            var plot = ctx.State.Farm.Plots[c.PlotIndex];
            var crop = ctx.Catalog.GetCrop(plot.CropId);
            var product = ctx.Catalog.GetProduct(crop.ProductId);

            // Validade conta a partir da colheita (planta madura no pé não estraga: jogo casual).
            ctx.Warehouse.Add(LotInventory.CreateLot(product, crop.Yield, ctx.NowMs, LotSource.Farm));
            ctx.State.Stats.CropsHarvested++;
            plot.Clear();

            ctx.Events.Publish(new StockAdded(product.Id, crop.Yield, LotSource.Farm));
            ctx.Events.Publish(new CropHarvested(c.PlotIndex, crop.Id, product.Id, crop.Yield));
            ctx.Events.Publish(new PlotChanged(c.PlotIndex));
        }
    }

    public sealed class BuyPlotHandler : ICommandHandler<BuyPlotCommand>
    {
        public CommandResult Validate(GameContext ctx, BuyPlotCommand c)
        {
            if (ctx.State.Farm.Plots.Count >= ctx.Catalog.Economy.MaxPlots) return CommandResult.Fail(CommandErrors.MaxReached);
            long cost = FarmRules.NextPlotCost(ctx.State, ctx.Catalog.Economy);
            return cost == 0 || ctx.Wallet.CanAffordCoins(cost) ? CommandResult.Ok() : CommandResult.Fail(CommandErrors.InsufficientCoins);
        }

        public void Apply(GameContext ctx, BuyPlotCommand c)
        {
            long cost = FarmRules.NextPlotCost(ctx.State, ctx.Catalog.Economy);
            if (cost > 0) ctx.Wallet.TrySpendCoins(cost, "parcela");
            ctx.State.Stats.CoinsInvested += cost;
            ctx.State.Farm.PlotsBought++;
            ctx.State.Farm.Plots.Add(new PlotState());
            int index = ctx.State.Farm.Plots.Count - 1;
            ctx.Events.Publish(new PlotBought(index, cost));
            ctx.Events.Publish(new PlotChanged(index));
        }
    }
}
