using System;
using System.Collections.Generic;
using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Simulation;
using Game.Core.State;
using Game.Core.Time;
using NUnit.Framework;

namespace Game.Tests.Core
{
    public class CommandExecutorTests
    {
        // Comandos de teste: os do jogo (Plantar, Colher...) entram na Etapa 2.
        private sealed class BuyItem : ICommand { public long Price; }

        private sealed class BuyItemHandler : ICommandHandler<BuyItem>
        {
            public CommandResult Validate(GameContext ctx, BuyItem c)
            {
                if (c.Price <= 0) return CommandResult.Fail(CommandErrors.InvalidArgument);
                return ctx.Wallet.CanAffordCoins(c.Price) ? CommandResult.Ok() : CommandResult.Fail(CommandErrors.InsufficientCoins);
            }

            public void Apply(GameContext ctx, BuyItem c) => ctx.Wallet.TrySpendCoins(c.Price, "item");
        }

        private sealed class SellFromWarehouse : ICommand { public string ProductId; public int Quantity; }

        private sealed class SellHandler : ICommandHandler<SellFromWarehouse>
        {
            public CommandResult Validate(GameContext ctx, SellFromWarehouse c)
            {
                if (!ctx.Catalog.TryGetProduct(c.ProductId, out _)) return CommandResult.Fail(CommandErrors.UnknownProduct);
                if (c.Quantity <= 0) return CommandResult.Fail(CommandErrors.InvalidArgument);
                return ctx.Warehouse.Count(c.ProductId, ctx.NowMs) >= c.Quantity
                    ? CommandResult.Ok()
                    : CommandResult.Fail(CommandErrors.InsufficientStock);
            }

            public void Apply(GameContext ctx, SellFromWarehouse c)
            {
                ctx.Warehouse.TryRemove(c.ProductId, c.Quantity, ctx.NowMs, out _);
                ctx.Wallet.AddCoins(ctx.Catalog.GetProduct(c.ProductId).ReferencePrice * c.Quantity, "venda");
            }
        }

        private sealed class Unregistered : ICommand { }

        private ManualClock _clock;
        private GameContext _ctx;
        private CommandExecutor _executor;
        private List<CommandExecuted> _executed;
        private List<CommandRejected> _rejected;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock();
            var catalog = TestCatalogs.Mvp();
            var bus = new EventBus();
            _executed = new List<CommandExecuted>();
            _rejected = new List<CommandRejected>();
            bus.Subscribe<CommandExecuted>(_executed.Add);
            bus.Subscribe<CommandRejected>(_rejected.Add);
            _ctx = new GameContext(NewGame.Create(catalog, _clock), catalog, _clock, bus);
            _executor = new CommandExecutor(_ctx)
                .Register(new BuyItemHandler())
                .Register(new SellHandler());
        }

        [Test]
        public void ValidCommand_IsApplied_AndPublished()
        {
            var cmd = new BuyItem { Price = 40 };
            var result = _executor.Execute(cmd);

            Assert.IsTrue(result.IsSuccess, result.ToString());
            Assert.IsNull(result.Error);
            Assert.AreEqual(60, _ctx.State.Wallet.Coins);
            Assert.AreSame(cmd, _executed[0].Command);
            Assert.IsEmpty(_rejected);
        }

        [Test]
        public void InvalidCommand_ChangesNothing_AndReportsError()
        {
            var result = _executor.Execute(new BuyItem { Price = 101 });

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(CommandErrors.InsufficientCoins, result.Error);
            Assert.AreEqual("Fail(insufficient_coins)", result.ToString());
            Assert.AreEqual(100, _ctx.State.Wallet.Coins);
            Assert.AreEqual(CommandErrors.InsufficientCoins, _rejected[0].Error);
            Assert.IsEmpty(_executed);
        }

        [TestCase("nada", 1, CommandErrors.UnknownProduct)]
        [TestCase("alface", 0, CommandErrors.InvalidArgument)]
        [TestCase("alface", 5, CommandErrors.InsufficientStock)]
        public void Sell_Validation(string product, int qty, string expectedError)
        {
            _ctx.Warehouse.Add(LotInventory.CreateLot(_ctx.Catalog.GetProduct("alface"), 4, _clock.UtcNowMs, LotSource.Farm));
            Assert.AreEqual(expectedError, _executor.Execute(new SellFromWarehouse { ProductId = product, Quantity = qty }).Error);
            Assert.AreEqual(4, _ctx.Warehouse.Count("alface", _clock.UtcNowMs));
        }

        [Test]
        public void Execute_AdvancesSimulationFirst_SoExpiredStockCannotBeUsed()
        {
            _ctx.Warehouse.Add(LotInventory.CreateLot(_ctx.Catalog.GetProduct("alface"), 4, _clock.UtcNowMs, LotSource.Farm));
            Assert.IsTrue(_executor.Execute(new SellFromWarehouse { ProductId = "alface", Quantity = 1 }).IsSuccess);
            Assert.AreEqual(110, _ctx.State.Wallet.Coins);

            _clock.Advance(TimeSpan.FromHours(7));
            var result = _executor.Execute(new SellFromWarehouse { ProductId = "alface", Quantity = 1 });

            Assert.AreEqual(CommandErrors.InsufficientStock, result.Error);
            Assert.AreEqual(3, _ctx.State.Stats.UnitsExpired);
            Assert.IsEmpty(_ctx.State.Warehouse);
        }

        [Test]
        public void UnknownCommand_IsRejected()
        {
            Assert.AreEqual(CommandErrors.NoHandler, _executor.Execute(new Unregistered()).Error);
            Assert.IsFalse(_executor.CanHandle(typeof(Unregistered)));
            Assert.IsTrue(_executor.CanHandle(typeof(BuyItem)));
        }

        [Test]
        public void Registration_Guards()
        {
            Assert.Throws<InvalidOperationException>(() => _executor.Register(new BuyItemHandler()));
            Assert.Throws<ArgumentNullException>(() => _executor.Register<BuyItem>(null));
            Assert.Throws<ArgumentNullException>(() => _executor.Execute(null));
            Assert.Throws<ArgumentNullException>(() => new CommandExecutor(null));
        }

        [Test]
        public void Fail_WithNullError_BecomesUnknown()
        {
            Assert.AreEqual(CommandErrors.Unknown, CommandResult.Fail(null).Error);
            Assert.AreEqual("Ok", CommandResult.Ok().ToString());
        }
    }
}
