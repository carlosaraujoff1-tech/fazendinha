using System;
using System.Collections.Generic;
using Game.Core.Catalog;
using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Farm;
using Game.Core.Save;
using Game.Core.Simulation;
using Game.Core.State;
using Game.Core.Time;
using NUnit.Framework;

namespace Game.Tests.Core
{
    public class FarmTests
    {
        private ManualClock _clock;
        private GameContext _ctx;
        private CommandExecutor _exec;
        private EventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock();
            var catalog = TestCatalogs.Mvp();
            _bus = new EventBus();
            _ctx = new GameContext(NewGame.Create(catalog, _clock), catalog, _clock, _bus);
            _exec = GameCommands.CreateExecutor(_ctx);
            foreach (var crop in catalog.Crops) _ctx.State.Farm.Seeds[crop.Id] = 10; // estoque de sementes para os testes
        }

        private PlotState Plot(int i) => _ctx.State.Farm.Plots[i];

        [Test]
        public void NewGame_StartsWithInitialEmptyPlots()
        {
            Assert.AreEqual(4, _ctx.State.Farm.Plots.Count);
            Assert.AreEqual(PlotPhase.Empty, FarmRules.GetPhase(Plot(0), _clock.UtcNowMs));
        }

        [Test]
        public void FullCycle_PlantWaterHarvest()
        {
            var harvested = new List<CropHarvested>();
            _bus.Subscribe<CropHarvested>(harvested.Add);

            Assert.IsTrue(_exec.Execute(new PlantCommand(0, "cenoura")).IsSuccess);   // 180 s, semente 18
            Assert.AreEqual(100, _ctx.State.Wallet.Coins, "plantar usa semente do estoque, não moedas");
            Assert.AreEqual(9, FarmRules.SeedsOf(_ctx.State, "cenoura"));
            Assert.AreEqual(PlotPhase.Growing, FarmRules.GetPhase(Plot(0), _clock.UtcNowMs));

            _clock.AdvanceSeconds(60);                                                // restam 120 s
            Assert.IsTrue(_exec.Execute(new WaterCommand(0)).IsSuccess);              // -25% = -30 s
            Assert.AreEqual(_clock.UtcNowMs + 90_000, Plot(0).ReadyAtMs);
            Assert.AreEqual(CommandErrors.AlreadyWatered, _exec.Execute(new WaterCommand(0)).Error);

            _clock.AdvanceSeconds(89);
            Assert.AreEqual(CommandErrors.NotReady, _exec.Execute(new HarvestCommand(0)).Error);
            _clock.AdvanceSeconds(1);
            Assert.IsTrue(_exec.Execute(new HarvestCommand(0)).IsSuccess);

            Assert.AreEqual(5, _ctx.Warehouse.Count("cenoura", LotSource.Farm, _clock.UtcNowMs));
            Assert.AreEqual(PlotPhase.Empty, FarmRules.GetPhase(Plot(0), _clock.UtcNowMs));
            Assert.AreEqual(1, harvested.Count);
            Assert.AreEqual(5, harvested[0].Quantity);
            Assert.AreEqual(1, _ctx.State.Stats.CropsHarvested);
        }

        [Test]
        public void Harvest_LotValidityStartsAtHarvestTime()
        {
            _exec.Execute(new PlantCommand(0, "alface"));
            _clock.Advance(TimeSpan.FromHours(3));      // ficou 3 h madura no pé
            _exec.Execute(new HarvestCommand(0));
            Assert.AreEqual(_clock.UtcNowMs + 6 * TimeMs.Hour, _ctx.State.Warehouse[0].ExpiresAtMs);
        }

        [Test]
        public void Plant_Validation()
        {
            Assert.AreEqual(CommandErrors.UnknownPlot, _exec.Execute(new PlantCommand(99, "alface")).Error);
            Assert.AreEqual(CommandErrors.UnknownPlot, _exec.Execute(new PlantCommand(-1, "alface")).Error);
            Assert.AreEqual(CommandErrors.UnknownCrop, _exec.Execute(new PlantCommand(0, "banana")).Error);
            Assert.AreEqual(CommandErrors.Locked, _exec.Execute(new PlantCommand(0, "milho")).Error);
            _exec.Execute(new PlantCommand(0, "alface"));
            Assert.AreEqual(CommandErrors.PlotOccupied, _exec.Execute(new PlantCommand(0, "alface")).Error);

            _ctx.State.Farm.Seeds["alface"] = 0;
            Assert.AreEqual(CommandErrors.NoSeeds, _exec.Execute(new PlantCommand(1, "alface")).Error);
        }

        [Test]
        public void BuySeeds_AddsToStock_AndCostsCoins()
        {
            _ctx.State.Farm.Seeds.Clear();
            Assert.IsTrue(_exec.Execute(new BuySeedsCommand("cenoura", 3)).IsSuccess);
            Assert.AreEqual(100 - 3 * 18, _ctx.State.Wallet.Coins);
            Assert.AreEqual(3, FarmRules.SeedsOf(_ctx.State, "cenoura"));
            Assert.IsTrue(_exec.Execute(new PlantCommand(0, "cenoura")).IsSuccess);
            Assert.AreEqual(2, FarmRules.SeedsOf(_ctx.State, "cenoura"));

            Assert.AreEqual(CommandErrors.UnknownCrop, _exec.Execute(new BuySeedsCommand("banana", 1)).Error);
            Assert.AreEqual(CommandErrors.InvalidArgument, _exec.Execute(new BuySeedsCommand("alface", 0)).Error);
            Assert.AreEqual(CommandErrors.Locked, _exec.Execute(new BuySeedsCommand("milho", 1)).Error);
            Assert.AreEqual(CommandErrors.InsufficientCoins, _exec.Execute(new BuySeedsCommand("cenoura", 3)).Error);
            Assert.AreEqual(0, FarmRules.SeedsOf(_ctx.State, null));
        }

        [Test]
        public void Plant_LockedCrop_UnlocksWithLevel()
        {
            _ctx.State.Progress.Level = 5;
            Assert.IsTrue(_exec.Execute(new PlantCommand(0, "milho")).IsSuccess);
        }

        [Test]
        public void Water_And_Harvest_Validation()
        {
            Assert.AreEqual(CommandErrors.PlotEmpty, _exec.Execute(new WaterCommand(0)).Error);
            Assert.AreEqual(CommandErrors.PlotEmpty, _exec.Execute(new HarvestCommand(0)).Error);
            Assert.AreEqual(CommandErrors.UnknownPlot, _exec.Execute(new WaterCommand(7)).Error);
            Assert.AreEqual(CommandErrors.UnknownPlot, _exec.Execute(new HarvestCommand(7)).Error);

            _exec.Execute(new PlantCommand(0, "alface"));
            _clock.AdvanceSeconds(61);
            Assert.AreEqual(CommandErrors.AlreadyReady, _exec.Execute(new WaterCommand(0)).Error);
        }

        [Test]
        public void Harvest_CropRemovedFromCatalog_IsRejected()
        {
            Plot(0).CropId = "removida";
            Plot(0).ReadyAtMs = _clock.UtcNowMs;
            Assert.AreEqual(CommandErrors.UnknownCrop, _exec.Execute(new HarvestCommand(0)).Error);
        }

        [Test]
        public void BuyPlot_CostGrowsAndStopsAtMax()
        {
            _ctx.State.Wallet.Coins = 1_000_000;
            Assert.AreEqual(50, FarmRules.NextPlotCost(_ctx.State, _ctx.Catalog.Economy));
            Assert.IsTrue(_exec.Execute(new BuyPlotCommand()).IsSuccess);
            Assert.AreEqual(1_000_000 - 50, _ctx.State.Wallet.Coins);
            Assert.AreEqual(58, FarmRules.NextPlotCost(_ctx.State, _ctx.Catalog.Economy)); // 57,5
            Assert.AreEqual(5, _ctx.State.Farm.Plots.Count);

            while (_exec.Execute(new BuyPlotCommand()).IsSuccess) { }
            Assert.AreEqual(20, _ctx.State.Farm.Plots.Count);
            Assert.AreEqual(CommandErrors.MaxReached, _exec.Execute(new BuyPlotCommand()).Error);
        }

        [Test]
        public void BuyPlot_WithoutMoney_IsRejected()
        {
            _ctx.State.Wallet.Coins = 49;
            Assert.AreEqual(CommandErrors.InsufficientCoins, _exec.Execute(new BuyPlotCommand()).Error);
        }

        [Test]
        public void BuyPlot_FreeWhenBaseCostIsZero()
        {
            var catalog = TestCatalogs.Mvp(new EconomyConfig { PlotBaseCost = 0, StartingCoins = 0 });
            var ctx = new GameContext(NewGame.Create(catalog, _clock), catalog, _clock, null);
            Assert.IsTrue(GameCommands.CreateExecutor(ctx).Execute(new BuyPlotCommand()).IsSuccess);
        }

        [Test]
        public void Queries_ProgressStageRemaining()
        {
            var empty = new PlotState();
            Assert.AreEqual(0f, FarmRules.Progress(empty, 0));
            Assert.AreEqual(0, FarmRules.VisualStage(empty, 0));
            Assert.AreEqual(0, FarmRules.RemainingMs(null, 0));
            Assert.AreEqual(PlotPhase.Empty, FarmRules.GetPhase(null, 0));

            var plot = new PlotState { CropId = "x", PlantedAtMs = 1000, ReadyAtMs = 5000 };
            Assert.AreEqual(0f, FarmRules.Progress(plot, 500));
            Assert.AreEqual(1, FarmRules.VisualStage(plot, 1000));
            Assert.AreEqual(0.5f, FarmRules.Progress(plot, 3000));
            Assert.AreEqual(2, FarmRules.VisualStage(plot, 3000));
            Assert.AreEqual(3, FarmRules.VisualStage(plot, 4999));
            Assert.AreEqual(4, FarmRules.VisualStage(plot, 5000));
            Assert.AreEqual(1f, FarmRules.Progress(plot, 9000));
            Assert.AreEqual(2000, FarmRules.RemainingMs(plot, 3000));
            Assert.AreEqual(0, FarmRules.RemainingMs(plot, 9000));
            Assert.AreEqual(1f, FarmRules.Progress(new PlotState { CropId = "x", PlantedAtMs = 5, ReadyAtMs = 5 }, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => FarmRules.VisualStage(plot, 0, 0));
        }

        [Test]
        public void FarmSurvivesSaveRoundTrip()
        {
            _exec.Execute(new PlantCommand(2, "tomate"));
            _exec.Execute(new WaterCommand(2));
            var serializer = new SaveSerializer();
            var loaded = serializer.Deserialize(serializer.Serialize(_ctx.State));

            Assert.AreEqual("tomate", loaded.Farm.Plots[2].CropId);
            Assert.IsTrue(loaded.Farm.Plots[2].Watered);
            Assert.AreEqual(Plot(2).ReadyAtMs, loaded.Farm.Plots[2].ReadyAtMs);
            Assert.IsTrue(loaded.Farm.Plots[0].IsEmpty);
            Assert.AreEqual(1, loaded.Progress.Level);
        }

        [TestCase("{\"SchemaVersion\":1,\"Wallet\":{},\"Warehouse\":[],\"Farm\":null}")]
        [TestCase("{\"SchemaVersion\":1,\"Wallet\":{},\"Warehouse\":[],\"Farm\":{\"Plots\":[null]}}")]
        public void Save_WithBrokenFarm_IsRejected(string json)
        {
            Assert.Throws<SaveFormatException>(() => new SaveSerializer().Deserialize(json));
        }
    }
}
