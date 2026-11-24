using System;
using System.Collections.Generic;
using Game.Core.Catalog;
using Game.Core.Inventory;
using Game.Core.State;
using NUnit.Framework;

namespace Game.Tests.Core
{
    public class LotInventoryTests
    {
        private const long T0 = 1_000_000;
        private const long H = 3_600_000;
        private GameCatalog _catalog;
        private List<StockLot> _lots;
        private LotInventory _inv;

        [SetUp]
        public void SetUp()
        {
            _catalog = TestCatalogs.Mvp();
            _lots = new List<StockLot>();
            _inv = new LotInventory(_lots);
        }

        private StockLot Lot(string product, int qty, long producedAt, LotSource source = LotSource.Farm) =>
            LotInventory.CreateLot(_catalog.GetProduct(product), qty, producedAt, source);

        [Test]
        public void CreateLot_SetsExpiryFromShelfLife()
        {
            var alface = Lot("alface", 4, T0);
            Assert.AreEqual(T0 + 6 * H, alface.ExpiresAtMs);
            Assert.AreEqual(StockLot.NeverExpires, Lot("arroz", 1, T0).ExpiresAtMs);
            Assert.IsFalse(alface.IsExpired(T0 + 6 * H - 1));
            Assert.IsTrue(alface.IsExpired(T0 + 6 * H));
        }

        [Test]
        public void CreateLot_InvalidArgs()
        {
            Assert.Throws<ArgumentNullException>(() => LotInventory.CreateLot(null, 1, T0, LotSource.Farm));
            Assert.Throws<ArgumentOutOfRangeException>(() => Lot("alface", 0, T0));
        }

        [Test]
        public void Add_MergesIdenticalLots_ButKeepsDifferentOnesApart()
        {
            _inv.Add(Lot("alface", 4, T0));
            _inv.Add(Lot("alface", 4, T0));
            _inv.Add(Lot("alface", 4, T0, LotSource.Supplier));
            _inv.Add(Lot("alface", 4, T0 + 1));

            Assert.AreEqual(3, _lots.Count);
            Assert.AreEqual(8, _lots[0].Quantity);
            Assert.AreEqual(16, _inv.Count("alface", T0));
            Assert.AreEqual(4, _inv.Count("alface", LotSource.Supplier, T0));
            Assert.AreEqual(12, _inv.Count("alface", LotSource.Farm, T0));
        }

        [Test]
        public void Add_CopiesTheLot_SoCallerChangesDoNotLeak()
        {
            var lot = Lot("cenoura", 5, T0);
            _inv.Add(lot);
            lot.Quantity = 999;
            Assert.AreEqual(5, _inv.Count("cenoura", T0));
        }

        [Test]
        public void Add_InvalidLots_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => _inv.Add(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _inv.Add(new StockLot { ProductId = "x", Quantity = 0 }));
            Assert.Throws<ArgumentException>(() => _inv.Add(new StockLot { ProductId = "", Quantity = 1 }));
            Assert.Throws<ArgumentNullException>(() => new LotInventory(null));
        }

        [Test]
        public void Count_IgnoresExpiredLots()
        {
            _inv.Add(Lot("alface", 4, T0));            // vence em T0+6h
            _inv.Add(Lot("alface", 3, T0 + 2 * H));   // vence em T0+8h
            Assert.AreEqual(7, _inv.Count("alface", T0 + 5 * H));
            Assert.AreEqual(3, _inv.Count("alface", T0 + 7 * H));
            Assert.AreEqual(0, _inv.Count("alface", T0 + 9 * H));
        }

        [Test]
        public void TryRemove_TakesWhatExpiresFirst()
        {
            _inv.Add(Lot("alface", 3, T0 + 2 * H, LotSource.Supplier)); // vence depois
            _inv.Add(Lot("alface", 4, T0));                              // vence antes

            Assert.IsTrue(_inv.TryRemove("alface", 5, T0 + H, out var taken));

            Assert.AreEqual(2, taken.Count);
            Assert.AreEqual(4, taken[0].Quantity);
            Assert.AreEqual(LotSource.Farm, taken[0].Source);
            Assert.AreEqual(1, taken[1].Quantity);
            Assert.AreEqual(LotSource.Supplier, taken[1].Source);
            Assert.AreEqual(2, _inv.Count("alface", T0 + H));
            Assert.AreEqual(1, _lots.Count, "lote zerado deve sair da lista");
        }

        [Test]
        public void TryRemove_TieOnExpiry_UsesOldestProduction()
        {
            var a = Lot("arroz", 1, T0 + 10);
            var b = Lot("arroz", 1, T0);
            _inv.Add(a);
            _inv.Add(b);
            Assert.IsTrue(_inv.TryRemove("arroz", 1, T0 + 20, out var taken));
            Assert.AreEqual(T0, taken[0].ProducedAtMs);
        }

        [Test]
        public void TryRemove_IsAllOrNothing_AndSkipsExpired()
        {
            _inv.Add(Lot("alface", 4, T0));
            _inv.Add(Lot("alface", 2, T0 + 3 * H));

            Assert.IsFalse(_inv.TryRemove("alface", 3, T0 + 7 * H, out var taken)); // só 2 válidas
            Assert.IsEmpty(taken);
            Assert.AreEqual(6, _lots[0].Quantity + _lots[1].Quantity);

            Assert.IsTrue(_inv.TryRemove("alface", 2, T0 + 7 * H, out taken));
            Assert.AreEqual(T0 + 3 * H, taken[0].ProducedAtMs);
        }

        [Test]
        public void TryRemove_InvalidQuantity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _inv.TryRemove("alface", 0, T0, out _));
        }

        [Test]
        public void RemoveExpired_ReturnsLossesInOrder()
        {
            _inv.Add(Lot("alface", 4, T0));          // vence T0+6h
            _inv.Add(Lot("tomate", 6, T0));          // vence T0+8h
            _inv.Add(Lot("arroz", 9, T0));           // nunca
            _inv.Add(Lot("cenoura", 5, T0 - 7 * H)); // vence T0+5h

            Assert.IsEmpty(_inv.RemoveExpired(T0 + H));
            var expired = _inv.RemoveExpired(T0 + 6 * H);

            Assert.AreEqual(2, expired.Count);
            Assert.AreEqual("alface", expired[0].ProductId);
            Assert.AreEqual("cenoura", expired[1].ProductId);
            Assert.AreEqual(2, _lots.Count);
        }

        [Test]
        public void NextExpiration()
        {
            Assert.AreEqual(StockLot.NeverExpires, _inv.NextExpirationMs());
            _inv.Add(Lot("tomate", 1, T0));
            _inv.Add(Lot("alface", 1, T0));
            Assert.AreEqual(T0 + 6 * H, _inv.NextExpirationMs());
        }
    }
}
