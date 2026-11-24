using Game.Core.Events;

namespace Game.Core.Store
{
    public enum LeaveReason
    {
        /// <summary>Pagou tudo no caixa.</summary>
        Paid,
        /// <summary>Não achou/não quis nada.</summary>
        Empty,
        /// <summary>Paciência acabou na fila: pagou só o que já passou.</summary>
        OutOfPatience,
        /// <summary>Loja fechou (app saiu do primeiro plano).</summary>
        StoreClosed,
    }

    public enum ItemFailReason { Missing, Price }

    public readonly struct CustomerEntered : IGameEvent
    {
        public CustomerEntered(int id, string typeId) { CustomerId = id; TypeId = typeId; }
        public int CustomerId { get; }
        public string TypeId { get; }
    }

    public readonly struct CustomerQueued : IGameEvent
    {
        public CustomerQueued(int id) { CustomerId = id; }
        public int CustomerId { get; }
    }

    public readonly struct CustomerPickedItem : IGameEvent
    {
        public CustomerPickedItem(int id, int shelfIndex, string productId, int units, bool substitute)
        {
            CustomerId = id; ShelfIndex = shelfIndex; ProductId = productId; Units = units; Substitute = substitute;
        }
        public int CustomerId { get; }
        public int ShelfIndex { get; }
        public string ProductId { get; }
        public int Units { get; }
        public bool Substitute { get; }
    }

    public readonly struct CustomerItemFailed : IGameEvent
    {
        public CustomerItemFailed(int id, string productId, ItemFailReason reason)
        {
            CustomerId = id; ProductId = productId; Reason = reason;
        }
        public int CustomerId { get; }
        public string ProductId { get; }
        public ItemFailReason Reason { get; }
    }

    public readonly struct ItemScanned : IGameEvent
    {
        public ItemScanned(int id, string productId, int units, long amount)
        {
            CustomerId = id; ProductId = productId; Units = units; Amount = amount;
        }
        public int CustomerId { get; }
        public string ProductId { get; }
        public int Units { get; }
        public long Amount { get; }
    }

    public readonly struct CustomerLeft : IGameEvent
    {
        public CustomerLeft(int id, LeaveReason reason, long paid)
        {
            CustomerId = id; Reason = reason; Paid = paid;
        }
        public int CustomerId { get; }
        public LeaveReason Reason { get; }
        public long Paid { get; }
    }

    public readonly struct ShelfChanged : IGameEvent
    {
        public ShelfChanged(int index) { ShelfIndex = index; }
        public int ShelfIndex { get; }
    }
}
