using System.Collections.Generic;
using Game.Core.Randomness;
using Game.Core.State;

namespace Game.Core.Store
{
    public sealed class StoreState
    {
        public List<ShelfState> Shelves { get; set; } = new List<ShelfState>();

        /// <summary>Preço definido pelo jogador por produto. Ausente = preço de referência P.</summary>
        public Dictionary<string, long> Prices { get; set; } = new Dictionary<string, long>();

        /// <summary>Clientes na loja (comprando ou na fila), em ordem de chegada.</summary>
        public List<CustomerState> Customers { get; set; } = new List<CustomerState>();

        /// <summary>Quantos móveis de cada tipo foram COMPRADOS (n da curva de custo).</summary>
        public Dictionary<string, int> FurnitureBought { get; set; } = new Dictionary<string, int>();

        public long LastStoreTickMs { get; set; }
        public long NextSpawnAtMs { get; set; }
        public int NextCustomerId { get; set; } = 1;
        public long NextQueueTicket { get; set; } = 1;
        public RngState Rng { get; set; } = new RngState();
    }

    public sealed class ShelfState
    {
        public string FurnitureId { get; set; }

        /// <summary>Produto exposto; null quando vazia (aceita qualquer produto da categoria).</summary>
        public string ProductId { get; set; }

        public List<StockLot> Lots { get; set; } = new List<StockLot>();
    }

    public enum CustomerPhase
    {
        Shopping = 0,
        Queued = 1,
    }

    public sealed class CustomerState
    {
        public int Id { get; set; }
        public string TypeId { get; set; }
        public CustomerPhase Phase { get; set; }
        public List<ShoppingItem> List { get; set; } = new List<ShoppingItem>();
        public int NextItem { get; set; }
        public long NextActionAtMs { get; set; }
        public List<CartLine> Cart { get; set; } = new List<CartLine>();
        public int ScannedLines { get; set; }
        public long QueueTicket { get; set; }
        public double PatienceSeconds { get; set; }
        public long EnteredAtMs { get; set; }
    }

    public sealed class ShoppingItem
    {
        public string ProductId { get; set; }
        public int Units { get; set; }
    }

    /// <summary>Item no carrinho: lotes retirados da prateleira (com origem/validade) e preço do momento.</summary>
    public sealed class CartLine
    {
        public string ProductId { get; set; }
        public long UnitPrice { get; set; }
        public List<StockLot> Lots { get; set; } = new List<StockLot>();

        public int Units
        {
            get
            {
                int n = 0;
                foreach (var l in Lots) n += l.Quantity;
                return n;
            }
        }

        public long Total => UnitPrice * Units;
    }
}
