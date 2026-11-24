using System;

namespace Game.Core.Time
{
    /// <summary>Fonte de tempo do domínio. Sempre UTC, em milissegundos Unix.</summary>
    public interface IClock
    {
        long UtcNowMs { get; }
    }

    /// <summary>Relógio real do sistema.</summary>
    public sealed class SystemClock : IClock
    {
        public long UtcNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>Relógio controlado manualmente (testes e simulador headless).</summary>
    public sealed class ManualClock : IClock
    {
        public ManualClock(long startMs = 1_700_000_000_000) { UtcNowMs = startMs; }

        public long UtcNowMs { get; private set; }

        public void Advance(TimeSpan delta)
        {
            if (delta < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delta), "Use Set() para voltar no tempo.");
            UtcNowMs += (long)delta.TotalMilliseconds;
        }

        public void AdvanceSeconds(double seconds) => Advance(TimeSpan.FromSeconds(seconds));

        public void Set(long utcMs) { UtcNowMs = utcMs; }
    }

    public static class TimeMs
    {
        public const long Second = 1000;
        public const long Minute = 60 * Second;
        public const long Hour = 60 * Minute;

        public static long FromSeconds(double seconds) => (long)Math.Round(seconds * Second);
    }
}
