using System;
using Game.Core.Randomness;
using Game.Core.Store;
using NUnit.Framework;

namespace Game.Tests.Core
{
    public class StoreFormulaTests
    {
        // Tabela do design: Econômico a=0 b=0,2 · Família a=0,1 b=0,3 · Gourmet a=0,25 b=0,5. P = 100.
        [TestCase(0.00, 0.2, 100, 1.0)]
        [TestCase(0.00, 0.2, 80, 1.0)]
        [TestCase(0.00, 0.2, 110, 0.5)]
        [TestCase(0.00, 0.2, 120, 0.0)]
        [TestCase(0.00, 0.2, 150, 0.0)]
        [TestCase(0.10, 0.3, 110, 1.0)]
        [TestCase(0.10, 0.3, 125, 0.5)]
        [TestCase(0.10, 0.3, 140, 0.0)]
        [TestCase(0.25, 0.5, 125, 1.0)]
        [TestCase(0.25, 0.5, 150, 0.5)]
        [TestCase(0.25, 0.5, 175, 0.0)]
        [TestCase(0.05, 0.2, 105, 1.0)]  // Econômico + selo "Da nossa fazenda"
        [TestCase(0.00, 0.2, 105, 0.75)] // mesmo preço sem selo
        public void BuyChance_MatchesDesign(double a, double b, long price, double expected)
        {
            Assert.AreEqual(expected, StoreRules.BuyChance(price, 100, a, b), 1e-9);
        }

        [Test]
        public void BuyChance_ZeroRange_IsAStep()
        {
            Assert.AreEqual(1, StoreRules.BuyChance(110, 100, 0.1, 0));
            Assert.AreEqual(0, StoreRules.BuyChance(111, 100, 0.1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => StoreRules.BuyChance(1, 0, 0, 0));
        }

        [TestCase(80, 1.10)]  // 20% de desconto → +10% de unidades
        [TestCase(50, 1.25)]
        [TestCase(100, 1.0)]
        [TestCase(150, 1.0)]
        public void QuantityMultiplier_HalfOfDiscount(long price, double expected)
        {
            Assert.AreEqual(expected, StoreRules.QuantityMultiplier(price, 100, 0.5), 1e-9);
            Assert.Throws<ArgumentOutOfRangeException>(() => StoreRules.QuantityMultiplier(1, 0, 0.5));
        }

        [TestCase(80, PriceTag.Cheap)]
        [TestCase(94, PriceTag.Cheap)]
        [TestCase(95, PriceTag.Fair)]
        [TestCase(100, PriceTag.Fair)]
        [TestCase(110, PriceTag.Fair)]
        [TestCase(111, PriceTag.Expensive)]
        public void Tag(long price, PriceTag expected) => Assert.AreEqual(expected, StoreRules.Tag(price, 100));

        [Test]
        public void Random_IsDeterministicAndInRange()
        {
            var a = new GameRandom(new RngState { Seed = 42 });
            var b = new GameRandom(new RngState { Seed = 42 });
            for (int i = 0; i < 1000; i++)
            {
                int x = a.Range(3, 7);
                Assert.AreEqual(x, b.Range(3, 7));
                Assert.That(x, Is.InRange(3, 6));
                double d = a.NextDouble(); b.NextDouble();
                Assert.That(d, Is.GreaterThanOrEqualTo(0).And.LessThan(1));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => a.Range(5, 5));
            Assert.Throws<ArgumentNullException>(() => new GameRandom(null));
        }

        [Test]
        public void Random_ChanceAndStochasticRound_PreserveTheMean()
        {
            var r = new GameRandom(new RngState { Seed = 7 });
            Assert.IsTrue(r.Chance(1)); Assert.IsFalse(r.Chance(0)); Assert.IsFalse(r.Chance(-1));
            Assert.AreEqual(0, r.StochasticRound(-2)); Assert.AreEqual(3, r.StochasticRound(3.0));

            int hits = 0; long sum = 0; const int n = 20000;
            for (int i = 0; i < n; i++)
            {
                if (r.Chance(0.3)) hits++;
                sum += r.StochasticRound(2.25);
            }
            Assert.AreEqual(0.3, hits / (double)n, 0.02);
            Assert.AreEqual(2.25, sum / (double)n, 0.03);
        }
    }
}
