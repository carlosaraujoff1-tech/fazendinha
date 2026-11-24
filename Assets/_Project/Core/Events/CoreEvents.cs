using System.Collections.Generic;
using Game.Core.State;

namespace Game.Core.Events
{
    public enum Currency { Coins, Gems }

    public readonly struct CurrencyChanged : IGameEvent
    {
        public CurrencyChanged(Currency currency, long delta, long balance, string reason)
        {
            Currency = currency; Delta = delta; Balance = balance; Reason = reason;
        }
        public Currency Currency { get; }
        public long Delta { get; }
        public long Balance { get; }
        public string Reason { get; }
    }

    public readonly struct StockAdded : IGameEvent
    {
        public StockAdded(string productId, int quantity, LotSource source)
        {
            ProductId = productId; Quantity = quantity; Source = source;
        }
        public string ProductId { get; }
        public int Quantity { get; }
        public LotSource Source { get; }
    }

    public readonly struct StockRemoved : IGameEvent
    {
        public StockRemoved(string productId, int quantity)
        {
            ProductId = productId; Quantity = quantity;
        }
        public string ProductId { get; }
        public int Quantity { get; }
    }

    /// <summary>Lotes que venceram (viraram perda).</summary>
    public readonly struct LotsExpired : IGameEvent
    {
        public LotsExpired(IReadOnlyList<StockLot> lots) { Lots = lots; }
        public IReadOnlyList<StockLot> Lots { get; }
    }

    public readonly struct CommandExecuted : IGameEvent
    {
        public CommandExecuted(Commands.ICommand command) { Command = command; }
        public Commands.ICommand Command { get; }
    }

    public readonly struct CommandRejected : IGameEvent
    {
        public CommandRejected(Commands.ICommand command, string error) { Command = command; Error = error; }
        public Commands.ICommand Command { get; }
        public string Error { get; }
    }
}
