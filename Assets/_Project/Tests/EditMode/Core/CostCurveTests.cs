using System;
using Game.Core.Economy;
using NUnit.Framework;

namespace Game.Tests.Core
{
    // Testes de C# puro: rodam no Unity (EditMode) e fora dele (Tools/CoreTests via dotnet).
    // Não usar UnityEngine nesta pasta.
    public class CostCurveTests
    {
        [TestCase(100, 0, 100)]
        [TestCase(100, 1, 115)]
        [TestCase(100, 2, 132)]   // 132,25
        [TestCase(100, 10, 405)]  // 404,56
        [TestCase(0, 5, 0)]
        public void RepeatableCost_FollowsFormula(long baseCost, int n, long expected)
        {
            Assert.AreEqual(expected, CostCurve.RepeatableCost(baseCost, n));
        }

        [Test]
        public void RepeatableCost_IsMonotonic()
        {
            long previous = 0;
            for (int n = 0; n < 60; n++)
            {
                long cost = CostCurve.RepeatableCost(50, n);
                Assert.GreaterOrEqual(cost, previous, $"n={n}");
                previous = cost;
            }
        }

        [Test]
        public void RepeatableCost_SaturatesInsteadOfOverflowing()
        {
            Assert.AreEqual(long.MaxValue, CostCurve.RepeatableCost(1_000_000, 10_000));
        }

        [Test]
        public void RepeatableCost_RejectsInvalidInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CostCurve.RepeatableCost(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CostCurve.RepeatableCost(10, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CostCurve.RepeatableCost(10, 1, 0.9));
        }
    }
}
