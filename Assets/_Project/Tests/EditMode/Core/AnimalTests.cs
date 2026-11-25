using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Animals;
using Game.Core.Catalog;
using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Save;
using Game.Core.Simulation;
using Game.Core.State;
using Game.Core.Time;
using NUnit.Framework;

namespace Game.Tests.Core
{
    public class AnimalTests
    {
        private ManualClock _clock;
        private GameCatalog _catalog;
        private GameContext _ctx;
        private CommandExecutor _exec;
        private EventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock();
            _catalog = GameContentTests.LoadRealCatalog();
            _bus = new EventBus();
            _ctx = new GameContext(NewGame.Create(_catalog, _clock), _catalog, _clock, _bus);
            _exec = GameCommands.CreateExecutor(_ctx);
            _ctx.State.Store.NextSpawnAtMs = long.MaxValue / 2; // sem clientes
        }

        private void Add(string product, int units) =>
            _ctx.Warehouse.Add(LotInventory.CreateLot(_catalog.GetProduct(product), units, _clock.UtcNowMs, LotSource.Farm));

        private void Tick(double seconds) { _clock.AdvanceSeconds(seconds); GameSimulation.Tick(_ctx); }

        [Test]
        public void NewGame_StartsWithAChicken_AndStartingSeeds()
        {
            Assert.AreEqual(1, _ctx.State.Farm.Animals.Count);
            Assert.AreEqual("galinha", _ctx.State.Farm.Animals[0].TypeId);
            Assert.AreEqual(AnimalPhase.Hungry, AnimalRules.GetPhase(_ctx.State.Farm.Animals[0], _clock.UtcNowMs));
            Assert.AreEqual(4, _ctx.State.Farm.Seeds["alface"]);
            Assert.AreEqual(4, _ctx.State.Farm.Seeds["trigo"]);
        }

        [Test]
        public void Chicken_EatsWheat_LaysEggs_FromOurFarm()
        {
            var collected = new List<AnimalProductCollected>();
            _bus.Subscribe<AnimalProductCollected>(collected.Add);
            Add("trigo", 5);

            Assert.IsTrue(_exec.Execute(new FeedAnimalCommand(0)).IsSuccess);
            Assert.AreEqual(3, _ctx.Warehouse.Count("trigo", _clock.UtcNowMs), "comeu 2 trigos");
            Assert.AreEqual(AnimalPhase.Producing, AnimalRules.GetPhase(_ctx.State.Farm.Animals[0], _clock.UtcNowMs));
            Assert.AreEqual(CommandErrors.AlreadyFed, _exec.Execute(new FeedAnimalCommand(0)).Error);

            Tick(60);
            Assert.AreEqual(0.5f, AnimalRules.Progress(_ctx.State.Farm.Animals[0], _clock.UtcNowMs), 0.01f);
            Assert.AreEqual(60_000, AnimalRules.RemainingMs(_ctx.State.Farm.Animals[0], _clock.UtcNowMs));
            Assert.AreEqual(CommandErrors.NotReady, _exec.Execute(new CollectAnimalCommand(0)).Error);

            Tick(60);
            Assert.IsTrue(_exec.Execute(new CollectAnimalCommand(0)).IsSuccess);
            Assert.AreEqual(3, _ctx.Warehouse.Count("ovo", LotSource.Farm, _clock.UtcNowMs));
            Assert.AreEqual(1, collected.Count);
            Assert.AreEqual(AnimalPhase.Hungry, AnimalRules.GetPhase(_ctx.State.Farm.Animals[0], _clock.UtcNowMs), "volta a ter fome");
        }

        [Test]
        public void Hungry_AnimalDoesNotProduce_AndDoesNotDie()
        {
            Tick(3600);
            Assert.AreEqual(CommandErrors.NoFeed, _exec.Execute(new CollectAnimalCommand(0)).Error);
            Assert.AreEqual(CommandErrors.NoFeed, _exec.Execute(new FeedAnimalCommand(0)).Error);
            Assert.AreEqual(1, _ctx.State.Farm.Animals.Count);
            Assert.AreEqual(0f, AnimalRules.Progress(_ctx.State.Farm.Animals[0], _clock.UtcNowMs));
            Assert.AreEqual(0, AnimalRules.RemainingMs(_ctx.State.Farm.Animals[0], _clock.UtcNowMs));
        }

        [Test]
        public void Feed_UsesPreferenceOrder_AndMixesFeeds()
        {
            Add("trigo", 1);
            Add("milho", 5);
            Assert.AreEqual(6, AnimalRules.AvailableFeed(_ctx, _catalog.GetAnimal("galinha")));
            Assert.IsTrue(_exec.Execute(new FeedAnimalCommand(0)).IsSuccess);
            Assert.AreEqual(0, _ctx.Warehouse.Count("trigo", _clock.UtcNowMs), "trigo primeiro");
            Assert.AreEqual(4, _ctx.Warehouse.Count("milho", _clock.UtcNowMs), "completa com milho");
        }

        [Test]
        public void Sheep_OnlyEatsWheat()
        {
            _ctx.State.Wallet.Coins = 10_000;
            Assert.IsTrue(_exec.Execute(new BuyAnimalCommand("ovelha")).IsSuccess);
            Add("milho", 10);
            Assert.AreEqual(CommandErrors.NoFeed, _exec.Execute(new FeedAnimalCommand(1)).Error);
            Add("trigo", 3);
            Assert.IsTrue(_exec.Execute(new FeedAnimalCommand(1)).IsSuccess);
            Tick(480);
            Assert.IsTrue(_exec.Execute(new CollectAnimalCommand(1)).IsSuccess);
            Assert.AreEqual(2, _ctx.Warehouse.Count("la", _clock.UtcNowMs));
        }

        [Test]
        public void BuyAnimal_CostGrows_AndRespectsMax()
        {
            _ctx.State.Wallet.Coins = 100_000;
            var def = _catalog.GetAnimal("galinha");
            Assert.AreEqual(92, AnimalRules.NextCost(_ctx.State, def, _catalog.Economy), "já tem 1: 80 × 1,15");
            while (_exec.Execute(new BuyAnimalCommand("galinha")).IsSuccess) { }
            Assert.AreEqual(def.MaxCount, AnimalRules.CountOf(_ctx.State, "galinha"));
            Assert.AreEqual(CommandErrors.MaxReached, _exec.Execute(new BuyAnimalCommand("galinha")).Error);
            Assert.AreEqual(CommandErrors.UnknownAnimal, _exec.Execute(new BuyAnimalCommand("dragao")).Error);

            _ctx.State.Wallet.Coins = 0;
            Assert.AreEqual(CommandErrors.InsufficientCoins, _exec.Execute(new BuyAnimalCommand("vaca")).Error);
            Assert.Greater(_ctx.State.Stats.CoinsInvested, 0);
        }

        [Test]
        public void BuyAnimal_LockedByLevel()
        {
            var catalog = GameCatalog.Build(_catalog.Products, _catalog.Crops, new EconomyConfig(), null, null, null, null,
                new[] { new AnimalDef("dragao", "Dragão", "ovo", new[] { "milho" }, 1, 10, 1, 0, 1, unlockLevel: 9) });
            var ctx = new GameContext(NewGame.Create(catalog, _clock), catalog, _clock, null);
            Assert.AreEqual(CommandErrors.Locked, GameCommands.CreateExecutor(ctx).Execute(new BuyAnimalCommand("dragao")).Error);
            ctx.State.Progress.Level = 9;
            Assert.IsTrue(GameCommands.CreateExecutor(ctx).Execute(new BuyAnimalCommand("dragao")).IsSuccess, "custo 0 = grátis");
        }

        [Test]
        public void InvalidIndex_AndRemovedType_AreRejected()
        {
            Assert.AreEqual(CommandErrors.UnknownAnimal, _exec.Execute(new FeedAnimalCommand(9)).Error);
            Assert.AreEqual(CommandErrors.UnknownAnimal, _exec.Execute(new CollectAnimalCommand(-1)).Error);
            _ctx.State.Farm.Animals.Add(new AnimalState { TypeId = "removido" });
            Assert.AreEqual(CommandErrors.UnknownAnimal, _exec.Execute(new FeedAnimalCommand(1)).Error);
            Assert.AreEqual(AnimalPhase.Hungry, AnimalRules.GetPhase(null, 0));
        }

        [Test]
        public void Offline_ProductionSleepsAfter4Hours_AndWelcomeCountsReadyAnimals()
        {
            var welcome = new List<OfflineReturned>();
            _bus.Subscribe<OfflineReturned>(welcome.Add);
            Add("trigo", 2);
            _exec.Execute(new FeedAnimalCommand(0));
            long readyAt = _ctx.State.Farm.Animals[0].ReadyAtMs;
            _clock.Advance(TimeSpan.FromHours(10));
            GameSimulation.Tick(_ctx);
            Assert.AreEqual(readyAt + 6 * TimeMs.Hour, _ctx.State.Farm.Animals[0].ReadyAtMs, "relógio empurrado pelo tempo dormido");
            Assert.AreEqual(1, welcome[0].AnimalsReady);
        }

        [Test]
        public void Save_RoundTrip_KeepsSeedsAndAnimals_AndFillsMissing()
        {
            Add("trigo", 2);
            _exec.Execute(new FeedAnimalCommand(0));
            var s = new SaveSerializer();
            var loaded = s.Deserialize(s.Serialize(_ctx.State));
            Assert.AreEqual("galinha", loaded.Farm.Animals[0].TypeId);
            Assert.AreEqual(_ctx.State.Farm.Animals[0].ReadyAtMs, loaded.Farm.Animals[0].ReadyAtMs);
            Assert.AreEqual(4, loaded.Farm.Seeds["alface"]);

            _ctx.State.Farm.Seeds = null; _ctx.State.Farm.Animals = null;
            var fixedUp = s.Deserialize(s.Serialize(_ctx.State));
            Assert.IsNotNull(fixedUp.Farm.Seeds);
            Assert.IsNotNull(fixedUp.Farm.Animals);

            _ctx.State.Farm.Animals = new List<AnimalState> { null };
            Assert.Throws<SaveFormatException>(() => s.Deserialize(s.Serialize(_ctx.State)));
        }

        [Test]
        public void Catalog_ValidatesAnimalsAndStartingData()
        {
            var products = new[] { new ProductDef("ovo", "x", 10, 0) };
            var ex = Assert.Throws<CatalogException>(() => GameCatalog.Build(products, null,
                new EconomyConfig
                {
                    StartingSeeds = new Dictionary<string, int> { { "nada", -1 } },   // 2 erros
                    StartingAnimals = new[] { "fantasma" },                          // 1
                }, null, null, null, null,
                new[]
                {
                    new AnimalDef("a", "", "nada", new string[0], 0, 0, 0, -1, 0),     // 7 erros
                    new AnimalDef("a", "", "ovo", new[] { "ovo" }, 1, 1, 1, 1, 1),     // duplicado
                    new AnimalDef("A", "", "ovo", new[] { "x" }, 1, 1, 1, 1, 1),       // ID inválido
                    new AnimalDef("b", "", "ovo", new[] { "nada" }, 1, 1, 1, 1, 1),    // come inexistente
                    null,
                }));
            Assert.AreEqual(14, ex.Errors.Count, string.Join("\n", ex.Errors));
            Assert.Throws<KeyNotFoundException>(() => _catalog.GetAnimal("x"));
            Assert.AreEqual(3, _catalog.Animals.Count);
            Assert.AreEqual("Galinha", _catalog.GetAnimal("galinha").Name);
            Assert.AreEqual("b", new AnimalDef("b", null, "ovo", null, 1, 1, 1, 1, 1).Name);
        }

        [Test]
        public void Economy_FeedingAnimalsIsWorthMoreThanSellingTheFeed()
        {
            foreach (var a in _catalog.Animals)
            {
                long feedValue = a.FeedPerCycle * _catalog.GetProduct(a.FeedProductIds[0]).ReferencePrice;
                long productValue = a.Yield * _catalog.GetProduct(a.ProductId).ReferencePrice;
                Assert.Greater(productValue, feedValue * 2, a.Id);
            }
        }
    }
}
