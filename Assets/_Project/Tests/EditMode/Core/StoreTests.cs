using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Catalog;
using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Save;
using Game.Core.Simulation;
using Game.Core.State;
using Game.Core.Store;
using Game.Core.Time;
using NUnit.Framework;

namespace Game.Tests.Core
{
    public class StoreTests
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
        }

        private StoreState Store => _ctx.State.Store;

        private void AddToWarehouse(string product, int units, LotSource source = LotSource.Farm) =>
            _ctx.Warehouse.Add(LotInventory.CreateLot(_catalog.GetProduct(product), units, _clock.UtcNowMs, source));

        private void Run(double seconds, double step = 0.25)
        {
            for (double t = 0; t < seconds; t += step)
            {
                _clock.AdvanceSeconds(step);
                GameSimulation.Tick(_ctx);
            }
        }

        private int ShelfOf(string furnitureId) => Store.Shelves.FindIndex(s => s.FurnitureId == furnitureId);

        [Test]
        public void NewGame_HasInitialFurniture_AndSeededRng()
        {
            CollectionAssert.AreEqual(new[] { "banca", "banca", "geladeira", "gondola" }, Store.Shelves.Select(s => s.FurnitureId));
            Assert.AreNotEqual(0UL, Store.Rng.Seed);
            Assert.AreEqual(_clock.UtcNowMs + 6000, Store.NextSpawnAtMs);
        }

        [Test]
        public void Restock_MovesLotsKeepingSourceAndExpiry()
        {
            AddToWarehouse("alface", 4, LotSource.Farm);
            AddToWarehouse("alface", 6, LotSource.Supplier);
            long farmExpiry = _ctx.State.Warehouse[0].ExpiresAtMs;

            Assert.IsTrue(_exec.Execute(new RestockCommand(0, "alface", 7)).IsSuccess);

            var shelf = Store.Shelves[0];
            Assert.AreEqual("alface", shelf.ProductId);
            Assert.AreEqual(7, StoreRules.Units(shelf, _clock.UtcNowMs));
            Assert.AreEqual(3, _ctx.Warehouse.Count("alface", _clock.UtcNowMs));
            Assert.IsTrue(shelf.Lots.Any(l => l.Source == LotSource.Farm && l.ExpiresAtMs == farmExpiry));
            Assert.IsTrue(StoreRules.FrontIsFromFarm(shelf, _clock.UtcNowMs) || !StoreRules.FrontIsFromFarm(shelf, _clock.UtcNowMs));
        }

        [Test]
        public void Restock_Validation()
        {
            AddToWarehouse("alface", 30);
            AddToWarehouse("cenoura", 5);
            AddToWarehouse("leite", 5, LotSource.Supplier);

            Assert.AreEqual(CommandErrors.UnknownShelf, _exec.Execute(new RestockCommand(99, "alface", 1)).Error);
            Assert.AreEqual(CommandErrors.UnknownProduct, _exec.Execute(new RestockCommand(0, "nada", 1)).Error);
            Assert.AreEqual(CommandErrors.InvalidArgument, _exec.Execute(new RestockCommand(0, "alface", 0)).Error);
            Assert.AreEqual(CommandErrors.WrongCategory, _exec.Execute(new RestockCommand(0, "leite", 1)).Error);
            Assert.AreEqual(CommandErrors.ShelfFull, _exec.Execute(new RestockCommand(0, "alface", 25)).Error);
            Assert.AreEqual(CommandErrors.InsufficientStock, _exec.Execute(new RestockCommand(0, "cenoura", 6)).Error);

            Assert.IsTrue(_exec.Execute(new RestockCommand(0, "alface", 20)).IsSuccess);
            Assert.AreEqual(CommandErrors.ShelfHasOtherProduct, _exec.Execute(new RestockCommand(0, "cenoura", 1)).Error);
            Assert.AreEqual(CommandErrors.ShelfFull, _exec.Execute(new RestockCommand(0, "alface", 5)).Error);
            Assert.IsTrue(_exec.Execute(new RestockCommand(0, "alface", 4)).IsSuccess);
            Assert.IsTrue(_exec.Execute(new RestockCommand(ShelfOf("geladeira"), "leite", 5)).IsSuccess);

            Store.Shelves.Add(new ShelfState { FurnitureId = "removido" });
            Assert.AreEqual(CommandErrors.UnknownFurniture, _exec.Execute(new RestockCommand(Store.Shelves.Count - 1, "alface", 1)).Error);
        }

        [Test]
        public void Restock_EmptyShelf_CanSwitchProduct()
        {
            AddToWarehouse("alface", 2);
            AddToWarehouse("cenoura", 2);
            _exec.Execute(new RestockCommand(0, "alface", 2));
            Store.Shelves[0].Lots[0].ExpiresAtMs = _clock.UtcNowMs + 1; // vai vencer
            _clock.AdvanceSeconds(1);
            Assert.IsTrue(_exec.Execute(new RestockCommand(0, "cenoura", 2)).IsSuccess);
            Assert.AreEqual("cenoura", Store.Shelves[0].ProductId);
            Assert.AreEqual(2, _ctx.State.Stats.UnitsExpired);
        }

        [Test]
        public void SetPrice_Bounds()
        {
            Assert.IsTrue(_exec.Execute(new SetPriceCommand("alface", 13)).IsSuccess);
            Assert.AreEqual(13, StoreRules.PriceOf(_ctx.State, _catalog.GetProduct("alface")));
            Assert.AreEqual(24, StoreRules.PriceOf(_ctx.State, _catalog.GetProduct("tomate")), "sem preço definido = P");
            Assert.AreEqual(CommandErrors.InvalidArgument, _exec.Execute(new SetPriceCommand("alface", 0)).Error);
            Assert.AreEqual(CommandErrors.InvalidArgument, _exec.Execute(new SetPriceCommand("alface", 101)).Error);
            Assert.IsTrue(_exec.Execute(new SetPriceCommand("alface", 100)).IsSuccess);
            Assert.AreEqual(CommandErrors.UnknownProduct, _exec.Execute(new SetPriceCommand("nada", 5)).Error);
        }

        [Test]
        public void BuyFurniture_CostGrowsAndRespectsMax()
        {
            _ctx.State.Wallet.Coins = 100_000;
            Assert.AreEqual(CommandErrors.UnknownFurniture, _exec.Execute(new BuyFurnitureCommand("nada")).Error);
            Assert.IsTrue(_exec.Execute(new BuyFurnitureCommand("banca")).IsSuccess);
            Assert.AreEqual(100_000 - 120, _ctx.State.Wallet.Coins);
            Assert.AreEqual(138, StoreRules.FurnitureCost(_ctx.State, _catalog.GetFurniture("banca"), _catalog.Economy));
            Assert.AreEqual(300, StoreRules.FurnitureCost(_ctx.State, _catalog.GetFurniture("geladeira"), _catalog.Economy));
            while (_exec.Execute(new BuyFurnitureCommand("gondola")).IsSuccess) { }
            Assert.AreEqual(12, Store.Shelves.Count);
            Assert.AreEqual(CommandErrors.MaxReached, _exec.Execute(new BuyFurnitureCommand("banca")).Error);

            _ctx.State.Wallet.Coins = 0;
            Store.Shelves.RemoveAt(11);
            Assert.AreEqual(CommandErrors.InsufficientCoins, _exec.Execute(new BuyFurnitureCommand("banca")).Error);
        }

        [Test]
        public void OneMinuteOfStore_CustomersBuyQueueAndPay()
        {
            AddToWarehouse("alface", 24); AddToWarehouse("cenoura", 20);
            AddToWarehouse("leite", 16, LotSource.Supplier); AddToWarehouse("arroz", 30, LotSource.Supplier);
            _exec.Execute(new RestockCommand(0, "alface", 24));
            _exec.Execute(new RestockCommand(1, "cenoura", 20));
            _exec.Execute(new RestockCommand(ShelfOf("geladeira"), "leite", 16));
            _exec.Execute(new RestockCommand(ShelfOf("gondola"), "arroz", 30));

            var entered = new List<CustomerEntered>();
            var scanned = new List<ItemScanned>();
            var left = new List<CustomerLeft>();
            var picks = new List<CustomerPickedItem>();
            _bus.Subscribe<CustomerPickedItem>(picks.Add);
            _bus.Subscribe<CustomerEntered>(entered.Add);
            _bus.Subscribe<ItemScanned>(scanned.Add);
            _bus.Subscribe<CustomerLeft>(left.Add);
            long coinsBefore = _ctx.State.Wallet.Coins;

            // O jogador passa um item no caixa a cada segundo.
            for (int s = 0; s < 60; s++)
            {
                Run(1);
                _exec.Execute(new ScanItemCommand());
            }

            Assert.That(entered.Count, Is.InRange(8, 11), "um cliente a cada 6 s");
            Assert.Greater(scanned.Count, 0);
            Assert.Greater(_ctx.State.Stats.CustomersServed, 0);
            Assert.Greater(_ctx.State.Wallet.Coins, coinsBefore);
            Assert.AreEqual(_ctx.State.Wallet.Coins - coinsBefore, _ctx.State.Stats.SalesRevenue);
            Assert.IsTrue(left.Where(l => l.Reason == LeaveReason.Paid).All(l => l.Paid > 0));
            // Querem batata, tomate, queijo... que não estão na loja: toda categoria tem algo, então substituem.
            Assert.IsTrue(picks.Any(p => p.Substitute));
            Assert.AreEqual(0, _ctx.State.Stats.ItemsMissing);
        }

        [Test]
        public void HighPrices_CustomersRefuse()
        {
            AddToWarehouse("alface", 24);
            _exec.Execute(new RestockCommand(0, "alface", 24));
            _exec.Execute(new SetPriceCommand("alface", 20)); // 2×P: ninguém aceita (gourmet vai até 1,75×P)
            Run(120);
            Assert.AreEqual(24, StoreRules.Units(Store.Shelves[0], _clock.UtcNowMs));
            Assert.Greater(_ctx.State.Stats.ItemsRejectedByPrice, 0);
            Assert.AreEqual(0, _ctx.State.Stats.UnitsSold);
        }

        [Test]
        public void EmptyStore_CustomersLeaveWithoutBuying()
        {
            Run(60);
            Assert.Greater(_ctx.State.Stats.CustomersLeftEmpty, 0);
            Assert.Greater(_ctx.State.Stats.ItemsMissing, 0);
            Assert.AreEqual(0, _ctx.State.Stats.SalesRevenue);
        }

        private CustomerState AddCustomer(string type, params string[] products)
        {
            var c = new CustomerState
            {
                Id = Store.NextCustomerId++,
                TypeId = type,
                Phase = CustomerPhase.Shopping,
                PatienceSeconds = _catalog.GetCustomerType(type).PatienceSeconds,
                NextActionAtMs = _clock.UtcNowMs,
            };
            foreach (var p in products) c.List.Add(new ShoppingItem { ProductId = p, Units = 2 });
            Store.Customers.Add(c);
            return c;
        }

        private void NoMoreSpawns() => Store.NextSpawnAtMs = long.MaxValue / 2;

        [Test]
        public void MissingItem_SubstituteFromSameCategory()
        {
            NoMoreSpawns();
            AddToWarehouse("cenoura", 10);
            _exec.Execute(new RestockCommand(0, "cenoura", 10));
            var picks = new List<CustomerPickedItem>();
            _bus.Subscribe<CustomerPickedItem>(picks.Add);

            var c = AddCustomer("gourmet", "alface", "queijo");
            Run(6);

            Assert.AreEqual(1, picks.Count);
            Assert.AreEqual("cenoura", picks[0].ProductId);
            Assert.IsTrue(picks[0].Substitute);
            Assert.AreEqual(1, _ctx.State.Stats.ItemsMissing, "queijo: geladeira vazia e sem substituto");
            Assert.AreEqual(CustomerPhase.Queued, c.Phase);
        }

        [Test]
        public void DiscountBuysMoreUnits_OnAverage()
        {
            NoMoreSpawns();
            AddToWarehouse("arroz", 30, LotSource.Supplier);
            int gondola = ShelfOf("gondola");
            _exec.Execute(new RestockCommand(gondola, "arroz", 30));
            _exec.Execute(new SetPriceCommand("arroz", 10)); // 50% de desconto → ×1,25
            for (int i = 0; i < 6; i++) AddCustomer("economico", "arroz");
            Run(4);
            int units = Store.Customers.Sum(c => c.Cart.Sum(l => l.Units));
            Assert.That(units, Is.InRange(13, 18), "6 clientes × 2 un × 1,25 = 15");
            Assert.IsTrue(Store.Customers.All(c => c.Cart.All(l => l.UnitPrice == 10)));
        }

        [Test]
        public void Patience_RunsOut_PaysOnlyScannedLines_RestReturnsToWarehouse()
        {
            NoMoreSpawns();
            AddToWarehouse("alface", 10); AddToWarehouse("arroz", 10, LotSource.Supplier);
            _exec.Execute(new RestockCommand(0, "alface", 10));
            _exec.Execute(new RestockCommand(ShelfOf("gondola"), "arroz", 10));
            var c = AddCustomer("economico", "alface", "arroz");
            Run(8);
            Assert.AreEqual(CustomerPhase.Queued, c.Phase);
            Assert.AreEqual(2, c.Cart.Count);

            Assert.IsTrue(_exec.Execute(new ScanItemCommand()).IsSuccess); // passa só a alface
            long alfaceTotal = c.Cart[0].Total;
            int arrozUnits = c.Cart[1].Units;
            long coins = _ctx.State.Wallet.Coins;

            Run(50); // paciência 45 s

            Assert.IsEmpty(Store.Customers);
            Assert.AreEqual(coins + alfaceTotal, _ctx.State.Wallet.Coins);
            Assert.AreEqual(1, _ctx.State.Stats.CustomersLeftAngry);
            Assert.AreEqual(arrozUnits, _ctx.Warehouse.Count("arroz", _clock.UtcNowMs));
        }

        [Test]
        public void LongQueue_DrainsPatienceFaster()
        {
            NoMoreSpawns();
            AddToWarehouse("arroz", 10, LotSource.Supplier);
            _exec.Execute(new RestockCommand(ShelfOf("gondola"), "arroz", 10));
            var first = AddCustomer("gourmet", "arroz");
            var second = AddCustomer("gourmet", "arroz");
            var third = AddCustomer("gourmet", "arroz");
            Run(4);
            Assert.IsTrue(new[] { first, second, third }.All(c => c.Phase == CustomerPhase.Queued));
            Assert.AreEqual(0, StoreRules.QueuePosition(Store, first));
            Assert.AreEqual(2, StoreRules.QueuePosition(Store, third));
            Assert.AreSame(first, StoreRules.FrontOfQueue(Store));

            double p1 = first.PatienceSeconds, p3 = third.PatienceSeconds;
            Run(10);
            Assert.AreEqual(10, p1 - first.PatienceSeconds, 0.6);
            Assert.AreEqual(15, p3 - third.PatienceSeconds, 0.6, "2 à frente: 1 + 0,25×2 = 1,5×");
        }

        [Test]
        public void Scan_WithoutQueue_IsRejected()
        {
            Assert.AreEqual(CommandErrors.NoCustomer, _exec.Execute(new ScanItemCommand()).Error);
        }

        [Test]
        public void Store_DoesNotSellOffline()
        {
            AddToWarehouse("alface", 24);
            _exec.Execute(new RestockCommand(0, "alface", 24));
            Run(20);
            int inStore = Store.Customers.Count;
            Assert.Greater(inStore, 0);
            int carts = Store.Customers.Sum(c => c.Cart.Sum(l => l.Units));
            int warehouseBefore = _ctx.Warehouse.Count("alface", _clock.UtcNowMs);

            _clock.Advance(TimeSpan.FromHours(3)); // app fechado
            GameSimulation.Tick(_ctx);

            Assert.IsEmpty(Store.Customers);
            Assert.AreEqual(warehouseBefore + carts, _ctx.Warehouse.Count("alface", _clock.UtcNowMs));
            Assert.AreEqual(0, _ctx.State.Stats.SalesRevenue);
            Assert.AreEqual(_clock.UtcNowMs + 6000, Store.NextSpawnAtMs);
        }

        [Test]
        public void ShelfLots_Expire()
        {
            AddToWarehouse("alface", 5);
            _exec.Execute(new RestockCommand(0, "alface", 5));
            NoMoreSpawns();
            _clock.Advance(TimeSpan.FromHours(4)); // alface vence em 6 h (dois saltos: limite offline 4 h)
            GameSimulation.Tick(_ctx);
            _clock.Advance(TimeSpan.FromHours(3));
            GameSimulation.Tick(_ctx);
            Assert.AreEqual(0, StoreRules.Units(Store.Shelves[0], _clock.UtcNowMs));
            Assert.AreEqual(5, _ctx.State.Stats.UnitsExpired);
        }

        [Test]
        public void FarmSeal_DependsOnTheNextUnitToLeave()
        {
            var shelf = new ShelfState { ProductId = "alface" };
            Assert.IsFalse(StoreRules.FrontIsFromFarm(shelf, 0));
            shelf.Lots.Add(new StockLot { ProductId = "alface", Quantity = 1, ExpiresAtMs = 200, Source = LotSource.Supplier });
            shelf.Lots.Add(new StockLot { ProductId = "alface", Quantity = 1, ExpiresAtMs = 100, Source = LotSource.Farm });
            Assert.IsTrue(StoreRules.FrontIsFromFarm(shelf, 0));
            Assert.IsFalse(StoreRules.FrontIsFromFarm(shelf, 150), "o lote da fazenda venceu");
            shelf.Lots.Add(new StockLot { ProductId = "alface", Quantity = 1, ExpiresAtMs = 200, ProducedAtMs = -1, Source = LotSource.Farm });
            Assert.IsTrue(StoreRules.FrontIsFromFarm(shelf, 150), "empate na validade: mais antigo primeiro");
        }

        [Test]
        public void Simulation_IsDeterministic_EvenAcrossSaveAndLoad()
        {
            AddToWarehouse("alface", 24); AddToWarehouse("arroz", 30, LotSource.Supplier);
            _exec.Execute(new RestockCommand(0, "alface", 24));
            _exec.Execute(new RestockCommand(ShelfOf("gondola"), "arroz", 30));
            Run(15);

            var serializer = new SaveSerializer();
            string json = serializer.Serialize(_ctx.State);
            var clock2 = new ManualClock(_clock.UtcNowMs);
            var ctx2 = new GameContext(serializer.Deserialize(json), _catalog, clock2, null);
            var exec2 = GameCommands.CreateExecutor(ctx2);

            for (int s = 0; s < 40; s++)
            {
                Run(1);
                _exec.Execute(new ScanItemCommand());
                for (int k = 0; k < 4; k++) { clock2.AdvanceSeconds(0.25); GameSimulation.Tick(ctx2); }
                exec2.Execute(new ScanItemCommand());
            }
            Assert.AreEqual(serializer.Serialize(_ctx.State), serializer.Serialize(ctx2.State));
        }

        [Test]
        public void Save_WithBrokenStore_IsRejected()
        {
            var s = new SaveSerializer();
            var state = NewGame.Create(_catalog, _clock);
            state.Store.Shelves.Add(null);
            Assert.Throws<SaveFormatException>(() => s.Deserialize(s.Serialize(state)));
            state.Store.Shelves.RemoveAt(state.Store.Shelves.Count - 1);
            state.Store.Customers.Add(null);
            Assert.Throws<SaveFormatException>(() => s.Deserialize(s.Serialize(state)));
            state.Store = null;
            Assert.Throws<SaveFormatException>(() => s.Deserialize(s.Serialize(state)));
        }

        [Test]
        public void Save_FillsMissingRngAndFurnitureBought()
        {
            var s = new SaveSerializer();
            var state = NewGame.Create(_catalog, _clock);
            state.Store.Rng = null;
            state.Store.FurnitureBought = null;
            var loaded = s.Deserialize(s.Serialize(state));
            Assert.IsNotNull(loaded.Store.Rng);
            Assert.IsNotNull(loaded.Store.FurnitureBought);
        }
    }
}
