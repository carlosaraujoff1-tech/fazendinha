using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Simulation;
using Game.Core.State;
using Game.Core.Time;
using NUnit.Framework;

namespace Game.Tests.Core
{
    public class SimulationTests
    {
        [Test]
        public void NewGame_UsesEconomyFromCatalog()
        {
            var clock = new ManualClock(5000);
            var catalog = TestCatalogs.Mvp(new Game.Core.Catalog.EconomyConfig { StartingCoins = 250, StartingGems = 3 });
            var state = NewGame.Create(catalog, clock);
            Assert.AreEqual(250, state.Wallet.Coins);
            Assert.AreEqual(3, state.Wallet.Gems);
            Assert.AreEqual(5000, state.CreatedAtMs);
            Assert.AreEqual(5000, state.LastTickMs);
            Assert.AreEqual(GameState.CurrentSchemaVersion, state.SchemaVersion);
            Assert.Throws<ArgumentNullException>(() => NewGame.Create(null, clock));
            Assert.Throws<ArgumentNullException>(() => NewGame.Create(catalog, null));
        }

        [Test]
        public void Context_RequiresDependencies()
        {
            var catalog = TestCatalogs.Mvp();
            var clock = new ManualClock();
            var state = NewGame.Create(catalog, clock);
            Assert.Throws<ArgumentNullException>(() => new GameContext(null, catalog, clock, null));
            Assert.Throws<ArgumentNullException>(() => new GameContext(state, null, clock, null));
            Assert.Throws<ArgumentNullException>(() => new GameContext(state, catalog, null, null));
            Assert.AreSame(NullEventBus.Instance, new GameContext(state, catalog, clock, null).Events);
        }

        [Test]
        public void Tick_WithFakeClock_ExpiresLotsOverTime()
        {
            var clock = new ManualClock();
            var catalog = TestCatalogs.Mvp();
            var bus = new EventBus();
            var expiredEvents = new List<LotsExpired>();
            bus.Subscribe<LotsExpired>(expiredEvents.Add);
            var ctx = new GameContext(NewGame.Create(catalog, clock), catalog, clock, bus);

            ctx.Warehouse.Add(LotInventory.CreateLot(catalog.GetProduct("alface"), 4, clock.UtcNowMs, LotSource.Farm));
            ctx.Warehouse.Add(LotInventory.CreateLot(catalog.GetProduct("arroz"), 2, clock.UtcNowMs, LotSource.Supplier));

            clock.Advance(TimeSpan.FromHours(5));
            GameSimulation.Tick(ctx);
            Assert.IsEmpty(expiredEvents);
            Assert.AreEqual(4, ctx.Warehouse.Count("alface", ctx.NowMs));

            clock.Advance(TimeSpan.FromHours(2));
            GameSimulation.Tick(ctx);
            GameSimulation.Tick(ctx); // idempotente

            Assert.AreEqual(1, expiredEvents.Count);
            Assert.AreEqual("alface", expiredEvents[0].Lots[0].ProductId);
            Assert.AreEqual(4, ctx.State.Stats.UnitsExpired);
            Assert.AreEqual(1, ctx.State.Warehouse.Count);
            Assert.AreEqual(clock.UtcNowMs, ctx.State.LastTickMs);
        }

        [Test]
        public void Tick_ClockGoingBackwards_IsIgnored()
        {
            var clock = new ManualClock(10_000);
            var catalog = TestCatalogs.Mvp();
            var ctx = new GameContext(NewGame.Create(catalog, clock), catalog, clock, null);
            clock.Set(5_000);
            GameSimulation.Tick(ctx);
            Assert.AreEqual(10_000, ctx.State.LastTickMs);
        }
    }
}
