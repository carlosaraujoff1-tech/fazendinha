using System;

namespace Game.Core.Randomness
{
    /// <summary>Estado serializável do gerador (fica no GameState: simulação reproduzível e salva).</summary>
    public sealed class RngState
    {
        public ulong Seed { get; set; }
    }

    /// <summary>SplitMix64 determinístico sobre um <see cref="RngState"/>. Mesma semente = mesma sequência.</summary>
    public sealed class GameRandom
    {
        private readonly RngState _state;

        public GameRandom(RngState state) { _state = state ?? throw new ArgumentNullException(nameof(state)); }

        public ulong NextULong()
        {
            unchecked
            {
                _state.Seed += 0x9E3779B97F4A7C15UL;
                ulong z = _state.Seed;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Número em [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        /// <summary>Inteiro em [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return minInclusive + (int)(NextULong() % (ulong)(maxExclusive - minInclusive));
        }

        public bool Chance(double probability) => probability >= 1 || (probability > 0 && NextDouble() < probability);

        /// <summary>Arredonda para baixo ou para cima com probabilidade igual à parte fracionária (média preservada).</summary>
        public int StochasticRound(double value)
        {
            if (value <= 0) return 0;
            double floor = Math.Floor(value);
            return (int)floor + (Chance(value - floor) ? 1 : 0);
        }
    }
}
