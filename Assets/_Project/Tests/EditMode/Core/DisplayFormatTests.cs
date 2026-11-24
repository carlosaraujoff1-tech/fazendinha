using Game.Core.Commands;
using Game.Core.Display;
using NUnit.Framework;

namespace Game.Tests.Core
{
    public class DisplayFormatTests
    {
        [TestCase(0, "0 s")]
        [TestCase(-5, "0 s")]
        [TestCase(1, "1 s")]
        [TestCase(45_000, "45 s")]
        [TestCase(60_000, "1 min")]
        [TestCase(90_000, "1 min 30 s")]
        [TestCase(3_600_000, "1 h")]
        [TestCase(7_500_000, "2 h 5 min")]
        public void Duration(long ms, string expected) => Assert.AreEqual(expected, DisplayFormat.Duration(ms));

        [Test]
        public void Errors_HaveFriendlyText_AndFallback()
        {
            Assert.AreEqual("Moedas insuficientes", DisplayFormat.Error(CommandErrors.InsufficientCoins));
            Assert.AreEqual("Não foi possível", DisplayFormat.Error("xyz"));
            Assert.AreEqual("Não foi possível", DisplayFormat.Error(null));
        }

        [TestCase(0, "0")]
        [TestCase(999, "999")]
        [TestCase(1000, "1.000")]
        [TestCase(12345, "12.345")]
        [TestCase(1234567, "1.234.567")]
        [TestCase(-1500, "-1.500")]
        [TestCase(long.MinValue, "-9.223.372.036.854.775.808")]
        public void Coins_UsesThousandsSeparator(long v, string expected) => Assert.AreEqual(expected, DisplayFormat.Coins(v));

        [Test]
        public void ColorFor_IsStableAndInRange()
        {
            Assert.AreEqual(DisplayFormat.ColorFor("alface"), DisplayFormat.ColorFor("alface"));
            Assert.AreNotEqual(DisplayFormat.ColorFor("alface"), DisplayFormat.ColorFor("tomate"));
            for (int i = 0; i < 360; i += 7)
            {
                var (r, g, b) = DisplayFormat.HsvToRgb(i / 360f, 0.65f, 0.85f);
                Assert.That(r, Is.InRange(0f, 1f)); Assert.That(g, Is.InRange(0f, 1f)); Assert.That(b, Is.InRange(0f, 1f));
            }
            Assert.DoesNotThrow(() => DisplayFormat.ColorFor(null));
        }
    }
}
