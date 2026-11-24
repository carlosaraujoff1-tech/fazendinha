using System;
using System.Collections.Generic;
using Game.Core.Economy;
using Game.Core.Events;
using Game.Core.State;
using NUnit.Framework;

namespace Game.Tests.Core
{
    public class WalletTests
    {
        private WalletState _state;
        private StatsState _stats;
        private List<CurrencyChanged> _events;
        private Wallet _wallet;

        [SetUp]
        public void SetUp()
        {
            _state = new WalletState { Coins = 100, Gems = 5 };
            _stats = new StatsState();
            _events = new List<CurrencyChanged>();
            var bus = new EventBus();
            bus.Subscribe<CurrencyChanged>(_events.Add);
            _wallet = new Wallet(_state, _stats, bus);
        }

        [Test]
        public void AddAndSpendCoins_UpdateBalanceStatsAndEvents()
        {
            _wallet.AddCoins(50, "venda");
            Assert.IsTrue(_wallet.TrySpendCoins(30, "semente"));

            Assert.AreEqual(120, _wallet.Coins);
            Assert.AreEqual(50, _stats.CoinsEarned);
            Assert.AreEqual(30, _stats.CoinsSpent);
            Assert.AreEqual(2, _events.Count);
            Assert.AreEqual(Currency.Coins, _events[1].Currency);
            Assert.AreEqual(-30, _events[1].Delta);
            Assert.AreEqual(120, _events[1].Balance);
            Assert.AreEqual("semente", _events[1].Reason);
        }

        [Test]
        public void SpendCoins_Insufficient_ChangesNothing()
        {
            Assert.IsFalse(_wallet.TrySpendCoins(101, "caro"));
            Assert.AreEqual(100, _wallet.Coins);
            Assert.IsEmpty(_events);
            Assert.IsTrue(_wallet.TrySpendCoins(100, "tudo"));
            Assert.AreEqual(0, _wallet.Coins);
        }

        [Test]
        public void Gems_AddAndSpend()
        {
            _wallet.AddGems(10, "meta");
            Assert.IsFalse(_wallet.TrySpendGems(16, "x"));
            Assert.IsTrue(_wallet.TrySpendGems(15, "x"));
            Assert.AreEqual(0, _wallet.Gems);
            Assert.AreEqual(Currency.Gems, _events[0].Currency);
        }

        [Test]
        public void CanAfford_Checks()
        {
            Assert.IsTrue(_wallet.CanAffordCoins(100));
            Assert.IsFalse(_wallet.CanAffordCoins(101));
            Assert.IsFalse(_wallet.CanAffordCoins(-1));
            Assert.IsTrue(_wallet.CanAffordGems(5));
            Assert.IsFalse(_wallet.CanAffordGems(6));
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void NonPositiveAmounts_Throw(long amount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _wallet.AddCoins(amount, ""));
            Assert.Throws<ArgumentOutOfRangeException>(() => _wallet.TrySpendCoins(amount, ""));
            Assert.Throws<ArgumentOutOfRangeException>(() => _wallet.AddGems(amount, ""));
            Assert.Throws<ArgumentOutOfRangeException>(() => _wallet.TrySpendGems(amount, ""));
        }

        [Test]
        public void Overflow_Throws_AndStatsSaturate()
        {
            _state.Coins = long.MaxValue - 1;
            Assert.Throws<OverflowException>(() => _wallet.AddCoins(5, ""));
            _stats.CoinsEarned = long.MaxValue - 1;
            _state.Coins = 0;
            _wallet.AddCoins(5, "");
            Assert.AreEqual(long.MaxValue, _stats.CoinsEarned);
        }

        [Test]
        public void Constructor_RequiresState_ButToleratesMissingStatsAndBus()
        {
            Assert.Throws<ArgumentNullException>(() => new Wallet(null, null, null));
            var w = new Wallet(new WalletState(), null, null);
            w.AddCoins(1, "");
            Assert.AreEqual(1, w.Coins);
        }
    }
}
