using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Catalog;
using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Farm;
using Game.Core.Inventory;
using Game.Core.Save;
using Game.Core.Simulation;
using Game.Core.Staff;
using Game.Core.State;
using Game.Core.Store;
using Game.Core.Supplier;
using Game.Core.Time;
using NUnit.Framework;

namespace Game.Tests.Core
{
    public class EconomyTests
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

        private void Run(double seconds, double step = 0.5)
        {
            for (double t = 0; t < seconds; t += step) { _clock.AdvanceSeconds(step); GameSimulation.Tick(_ctx); }
        }

        private void NoCustomers() => _ctx.State.Store.NextSpawnAtMs = long.MaxValue / 2;

        // ---------- Fornecedor ----------

        [TestCase("alface", 8)]
        [TestCase("cenoura", 11)]
        [TestCase("batata", 14)]
        [TestCase("tomate", 19)]
        [TestCase("milho", 24)]
        public void Supplier_Price_Is80PercentOfP(string product, long expected)
        {
            Assert.AreEqual(expected, SupplierRules.UnitPrice(_catalog.GetProduct(product), _catalog.Economy));
        }

        [Test]
        public void Supplier_ChargesNow_DeliversIn2Minutes()
        {
            var delivered = new List<SupplierDelivered>();
            _bus.Subscribe<SupplierDelivered>(delivered.Add);

            Assert.IsTrue(_exec.Execute(new BuyFromSupplierCommand("leite", 5)).IsSuccess);
            Assert.AreEqual(100 - 5 * 10, _ctx.State.Wallet.Coins);
            Assert.AreEqual(5, SupplierRules.PendingUnits(_ctx.State, "leite"));
            Assert.AreEqual(50, _ctx.State.Stats.SupplierSpent);

            _clock.AdvanceSeconds(119); GameSimulation.Tick(_ctx);
            Assert.AreEqual(0, _ctx.Warehouse.Count("leite", _clock.UtcNowMs));
            _clock.AdvanceSeconds(1); GameSimulation.Tick(_ctx);
            Assert.AreEqual(5, _ctx.Warehouse.Count("leite", LotSource.Supplier, _clock.UtcNowMs));
            Assert.AreEqual(0, SupplierRules.PendingUnits(_ctx.State, "leite"));
            Assert.AreEqual(1, delivered.Count);
        }

        [Test]
        public void Supplier_Validation()
        {
            Assert.AreEqual(CommandErrors.UnknownProduct, _exec.Execute(new BuyFromSupplierCommand("nada", 1)).Error);
            Assert.AreEqual(CommandErrors.InvalidArgument, _exec.Execute(new BuyFromSupplierCommand("leite", 0)).Error);
            Assert.AreEqual(CommandErrors.InvalidArgument, _exec.Execute(new BuyFromSupplierCommand("leite", 201)).Error);
            Assert.AreEqual(CommandErrors.InsufficientCoins, _exec.Execute(new BuyFromSupplierCommand("leite", 11)).Error);
        }

        [Test]
        public void Supplier_DeliversWhileOffline()
        {
            _exec.Execute(new BuyFromSupplierCommand("arroz", 3));
            _clock.Advance(TimeSpan.FromHours(1));
            GameSimulation.Tick(_ctx);
            Assert.AreEqual(3, _ctx.Warehouse.Count("arroz", _clock.UtcNowMs));
        }

        [Test]
        public void Supplier_OrderForRemovedProduct_IsDropped()
        {
            _ctx.State.Supplier.Orders.Add(new SupplierOrder { ProductId = "removido", Units = 1, DeliverAtMs = _clock.UtcNowMs });
            GameSimulation.Tick(_ctx);
            Assert.IsEmpty(_ctx.State.Supplier.Orders);
        }

        [Test]
        public void FarmIsCheaperThanSupplier_ByDesign()
        {
            foreach (var crop in _catalog.Crops)
            {
                var product = _catalog.GetProduct(crop.ProductId);
                double farmUnit = (double)crop.SeedCost / crop.Yield;
                Assert.Less(farmUnit, SupplierRules.UnitPrice(product, _catalog.Economy) / 2.0, crop.Id);
            }
        }

        // ---------- Salários e caixa ----------

        [Test]
        public void Hire_Validation_AndFire()
        {
            Assert.AreEqual(CommandErrors.InvalidArgument, _exec.Execute(new HireCommand("nada")).Error);
            _ctx.State.Wallet.Coins = 0;
            Assert.AreEqual(CommandErrors.InsufficientCoins, _exec.Execute(new HireCommand("caixa")).Error);
            _ctx.State.Wallet.Coins = 10_000;
            Assert.IsTrue(_exec.Execute(new HireCommand("caixa")).IsSuccess);
            Assert.AreEqual(CommandErrors.MaxReached, _exec.Execute(new HireCommand("caixa")).Error);
            Assert.AreEqual(CommandErrors.InvalidArgument, _exec.Execute(new FireCommand(3)).Error);
            Assert.IsTrue(_exec.Execute(new FireCommand(0)).IsSuccess);
            Assert.IsEmpty(_ctx.State.Staff.Employees);
        }

        [Test]
        public void Cashier_ScansQueueAutomatically()
        {
            _ctx.State.Wallet.Coins = 10_000;
            _exec.Execute(new HireCommand("caixa"));
            _ctx.Warehouse.Add(LotInventory.CreateLot(_catalog.GetProduct("alface"), 24, _clock.UtcNowMs, LotSource.Farm));
            _ctx.Warehouse.Add(LotInventory.CreateLot(_catalog.GetProduct("arroz"), 30, _clock.UtcNowMs, LotSource.Supplier));
            _exec.Execute(new RestockCommand(0, "alface", 24));
            _exec.Execute(new RestockCommand(3, "arroz", 30));
            Run(120);
            Assert.Greater(_ctx.State.Stats.CustomersServed, 5);
            Assert.AreEqual(0, _ctx.State.Stats.CustomersLeftAngry);
        }

        [Test]
        public void Salaries_ChargedPerOnlineInterval_StopWhenBroke_ResumeWhenPaid()
        {
            NoCustomers();
            var def = _catalog.GetStaff("caixa");
            _ctx.State.Wallet.Coins = def.HireCost + def.Salary; // paga contratação + 1 salário
            _exec.Execute(new HireCommand("caixa"));
            var paid = new List<SalaryPaid>();
            _bus.Subscribe<SalaryPaid>(paid.Add);

            Run(600);
            Assert.AreEqual(1, paid.Count);
            Assert.AreEqual(0, _ctx.State.Wallet.Coins);
            Assert.IsTrue(_ctx.State.Staff.Employees[0].Active);

            Run(600);
            Assert.AreEqual(1, paid[1].Unpaid);
            Assert.IsFalse(_ctx.State.Staff.Employees[0].Active, "sem saldo: para (sem game over)");
            Assert.AreEqual(0, _ctx.State.Wallet.Coins);

            _ctx.State.Wallet.Coins = def.Salary;
            Run(600);
            Assert.IsTrue(_ctx.State.Staff.Employees[0].Active);
            Assert.AreEqual(2 * def.Salary, _ctx.State.Stats.SalariesPaid);
        }

        [Test]
        public void Salaries_NotChargedOffline()
        {
            NoCustomers();
            _ctx.State.Wallet.Coins = 10_000;
            _exec.Execute(new HireCommand("caixa"));
            long coins = _ctx.State.Wallet.Coins;
            _clock.Advance(TimeSpan.FromHours(3));
            GameSimulation.Tick(_ctx);
            Assert.AreEqual(coins, _ctx.State.Wallet.Coins);
        }

        [Test]
        public void Salaries_UnknownStaffType_StopsWorking()
        {
            NoCustomers();
            _ctx.State.Staff.Employees.Add(new EmployeeState { TypeId = "removido" });
            Run(601);
            Assert.IsFalse(_ctx.State.Staff.Employees[0].Active);
        }

        // ---------- Offline ----------

        [Test]
        public void Offline_FarmProducesAtMost4Hours_AndStockSleeps()
        {
            _ctx.State.Progress.Level = 5;
            _ctx.State.Farm.Seeds["milho"] = 1;
            _exec.Execute(new PlantCommand(0, "milho")); // 30 min
            _ctx.State.Wallet.Coins = 1000;
            _ctx.Warehouse.Add(LotInventory.CreateLot(_catalog.GetProduct("alface"), 4, _clock.UtcNowMs, LotSource.Farm)); // vence em 6 h
            var welcome = new List<OfflineReturned>();
            _bus.Subscribe<OfflineReturned>(welcome.Add);

            _clock.Advance(TimeSpan.FromHours(10));
            GameSimulation.Tick(_ctx);

            Assert.AreEqual(1, welcome.Count);
            Assert.AreEqual(10 * TimeMs.Hour, welcome[0].AwayMs);
            Assert.AreEqual(4 * TimeMs.Hour, welcome[0].CreditedMs);
            Assert.AreEqual(1, welcome[0].PlotsReady);
            Assert.AreEqual(4, _ctx.Warehouse.Count("alface", _clock.UtcNowMs), "só 4 h contaram: alface (6 h) ainda boa");
            Assert.AreEqual(6 * TimeMs.Hour, _ctx.State.Stats.OfflineMsIgnored);
            Assert.AreEqual(_clock.UtcNowMs, _ctx.State.LastTickMs);
        }

        [Test]
        public void Offline_ShortAbsence_IsFullyCredited_AndNoWelcomeUnderAMinute()
        {
            var welcome = new List<OfflineReturned>();
            _bus.Subscribe<OfflineReturned>(welcome.Add);
            _ctx.State.Farm.Seeds["tomate"] = 1;
            _exec.Execute(new PlantCommand(0, "tomate")); // 15 min
            _clock.AdvanceSeconds(30); GameSimulation.Tick(_ctx);
            Assert.IsEmpty(welcome);
            _clock.Advance(TimeSpan.FromMinutes(20)); GameSimulation.Tick(_ctx);
            Assert.AreEqual(1, welcome.Count);
            Assert.AreEqual(welcome[0].AwayMs, welcome[0].CreditedMs);
            Assert.AreEqual(PlotPhase.Ready, FarmRules.GetPhase(_ctx.State.Farm.Plots[0], _clock.UtcNowMs));
        }

        [Test]
        public void TimeShift_MovesEveryAbsoluteTimestamp()
        {
            var s = NewGame.Create(_catalog, _clock);
            s.Warehouse.Add(new StockLot { ProductId = "arroz", Quantity = 1, ProducedAtMs = 10, ExpiresAtMs = StockLot.NeverExpires });
            s.Farm.Plots[0].CropId = "alface"; s.Farm.Plots[0].PlantedAtMs = 5; s.Farm.Plots[0].ReadyAtMs = 50;
            s.Supplier.Orders.Add(new SupplierOrder { ProductId = "leite", Units = 1, DeliverAtMs = 70 });
            s.Store.Shelves[0].Lots.Add(new StockLot { ProductId = "alface", Quantity = 1, ProducedAtMs = 1, ExpiresAtMs = 9 });
            var c = new CustomerState { EnteredAtMs = 3, NextActionAtMs = 4 };
            c.Cart.Add(new CartLine { Lots = { new StockLot { ProducedAtMs = 2, ExpiresAtMs = 8 } } });
            s.Store.Customers.Add(c);
            long lastTick = s.LastTickMs, spawn = s.Store.NextSpawnAtMs;

            TimeShift.Apply(s, 100);
            TimeShift.Apply(s, 0);

            Assert.AreEqual(StockLot.NeverExpires, s.Warehouse[0].ExpiresAtMs);
            Assert.AreEqual(110, s.Warehouse[0].ProducedAtMs);
            Assert.AreEqual(105, s.Farm.Plots[0].PlantedAtMs); Assert.AreEqual(150, s.Farm.Plots[0].ReadyAtMs);
            Assert.AreEqual(0, s.Farm.Plots[1].ReadyAtMs, "parcela vazia não muda");
            Assert.AreEqual(170, s.Supplier.Orders[0].DeliverAtMs);
            Assert.AreEqual(109, s.Store.Shelves[0].Lots[0].ExpiresAtMs);
            Assert.AreEqual(103, c.EnteredAtMs); Assert.AreEqual(104, c.NextActionAtMs);
            Assert.AreEqual(108, c.Cart[0].Lots[0].ExpiresAtMs);
            Assert.AreEqual(lastTick + 100, s.LastTickMs); Assert.AreEqual(spawn + 100, s.Store.NextSpawnAtMs);
        }

        [Test]
        public void Save_RoundTrip_KeepsSupplierAndStaff_AndFillsMissing()
        {
            _ctx.State.Wallet.Coins = 10_000;
            _exec.Execute(new HireCommand("caixa"));
            _exec.Execute(new BuyFromSupplierCommand("queijo", 2));
            var serializer = new SaveSerializer();
            var loaded = serializer.Deserialize(serializer.Serialize(_ctx.State));
            Assert.AreEqual("queijo", loaded.Supplier.Orders[0].ProductId);
            Assert.AreEqual("caixa", loaded.Staff.Employees[0].TypeId);

            _ctx.State.Supplier = null; _ctx.State.Staff = null;
            var fixedUp = serializer.Deserialize(serializer.Serialize(_ctx.State));
            Assert.IsNotNull(fixedUp.Supplier.Orders);
            Assert.IsNotNull(fixedUp.Staff.Employees);
        }

        [Test]
        public void Catalog_ValidatesStaff()
        {
            var ex = Assert.Throws<CatalogException>(() => GameCatalog.Build(null, null,
                new EconomyConfig { MaxSupplierOrderUnits = 0, SalaryIntervalSeconds = 0 }, null, null, null,
                new[]
                {
                    new StaffDef("s", "", StaffRole.Cashier, -1, 0, 0, 0), // 3 erros
                    new StaffDef("s", "", StaffRole.Cashier, 1, 1, 1, 1),  // duplicado
                    new StaffDef("S", "", StaffRole.Cashier, 1, 1, 1, 1),  // ID inválido
                    null,
                }));
            Assert.AreEqual(8, ex.Errors.Count, string.Join("\n", ex.Errors));
            Assert.Throws<KeyNotFoundException>(() => _catalog.GetStaff("x"));
            Assert.AreEqual("Caixa", _catalog.GetStaff("caixa").Name);
            Assert.AreEqual(1, _catalog.Staff.Count());
        }
    }
}
