using System;
using Game.Core.Events;
using Game.Core.State;

namespace Game.Core.Economy
{
    /// <summary>Regras da carteira (moedas e gemas) sobre o <see cref="WalletState"/>. Nunca fica negativa.</summary>
    public sealed class Wallet
    {
        private readonly WalletState _state;
        private readonly StatsState _stats;
        private readonly IEventBus _events;

        public Wallet(WalletState state, StatsState stats, IEventBus events)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _stats = stats ?? new StatsState();
            _events = events ?? NullEventBus.Instance;
        }

        public long Coins => _state.Coins;
        public long Gems => _state.Gems;

        public bool CanAffordCoins(long amount) => amount >= 0 && _state.Coins >= amount;
        public bool CanAffordGems(long amount) => amount >= 0 && _state.Gems >= amount;

        public void AddCoins(long amount, string reason)
        {
            RequirePositive(amount);
            _state.Coins = checked(_state.Coins + amount);
            _stats.CoinsEarned = SaturatingAdd(_stats.CoinsEarned, amount);
            _events.Publish(new CurrencyChanged(Currency.Coins, amount, _state.Coins, reason));
        }

        public bool TrySpendCoins(long amount, string reason)
        {
            RequirePositive(amount);
            if (_state.Coins < amount) return false;
            _state.Coins -= amount;
            _stats.CoinsSpent = SaturatingAdd(_stats.CoinsSpent, amount);
            _events.Publish(new CurrencyChanged(Currency.Coins, -amount, _state.Coins, reason));
            return true;
        }

        public void AddGems(long amount, string reason)
        {
            RequirePositive(amount);
            _state.Gems = checked(_state.Gems + amount);
            _events.Publish(new CurrencyChanged(Currency.Gems, amount, _state.Gems, reason));
        }

        public bool TrySpendGems(long amount, string reason)
        {
            RequirePositive(amount);
            if (_state.Gems < amount) return false;
            _state.Gems -= amount;
            _events.Publish(new CurrencyChanged(Currency.Gems, -amount, _state.Gems, reason));
            return true;
        }

        private static void RequirePositive(long amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Valor deve ser > 0.");
        }

        private static long SaturatingAdd(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
    }
}
