using System;
using Game.Core.Time;
using NUnit.Framework;

namespace Game.Tests.Core
{
    public class ClockTests
    {
        [Test]
        public void ManualClock_AdvancesAndSets()
        {
            var clock = new ManualClock(1000);
            clock.AdvanceSeconds(1.5);
            Assert.AreEqual(2500, clock.UtcNowMs);
            clock.Advance(TimeSpan.FromMinutes(1));
            Assert.AreEqual(62500, clock.UtcNowMs);
            clock.Set(10);
            Assert.AreEqual(10, clock.UtcNowMs);
        }

        [Test]
        public void ManualClock_RejectsNegativeAdvance()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ManualClock().Advance(TimeSpan.FromSeconds(-1)));
        }

        [Test]
        public void SystemClock_IsUtcNow()
        {
            long expected = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(new SystemClock().UtcNowMs, Is.InRange(expected - 5000, expected + 5000));
        }

        [Test]
        public void TimeMs_Conversions()
        {
            Assert.AreEqual(1500, TimeMs.FromSeconds(1.5));
            Assert.AreEqual(3_600_000, TimeMs.Hour);
            Assert.AreEqual(60_000, TimeMs.Minute);
        }
    }
}
