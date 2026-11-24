using System.Collections.Generic;

namespace Game.Core.State
{
    /// <summary>
    /// Todo o progresso do jogador, em um único objeto serializável. Salvar = serializar isto.
    /// Só dados: as regras ficam nos sistemas/handlers. Tempos em ms Unix UTC.
    /// Ao mudar o formato: incrementar <see cref="CurrentSchemaVersion"/> e criar uma migração.
    /// </summary>
    public sealed class GameState
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public long CreatedAtMs { get; set; }
        public long LastSavedAtMs { get; set; }

        /// <summary>Último instante já processado pela simulação (vencimentos, crescimento...).</summary>
        public long LastTickMs { get; set; }

        public WalletState Wallet { get; set; } = new WalletState();

        /// <summary>Armazém central: estoque em lotes.</summary>
        public List<StockLot> Warehouse { get; set; } = new List<StockLot>();

        public StatsState Stats { get; set; } = new StatsState();
    }

    public sealed class WalletState
    {
        public long Coins { get; set; }
        public long Gems { get; set; }
    }

    public enum LotSource
    {
        Farm = 0,
        Supplier = 1,
    }

    /// <summary>Lote de um produto: mesma origem, mesma data de produção e mesma validade.</summary>
    public sealed class StockLot
    {
        public const long NeverExpires = long.MaxValue;

        public string ProductId { get; set; }
        public int Quantity { get; set; }
        public long ProducedAtMs { get; set; }
        public long ExpiresAtMs { get; set; } = NeverExpires;
        public LotSource Source { get; set; }

        public bool IsExpired(long nowMs) => nowMs >= ExpiresAtMs;

        public StockLot CloneWithQuantity(int quantity) => new StockLot
        {
            ProductId = ProductId,
            Quantity = quantity,
            ProducedAtMs = ProducedAtMs,
            ExpiresAtMs = ExpiresAtMs,
            Source = Source,
        };
    }

    public sealed class StatsState
    {
        public long CoinsEarned { get; set; }
        public long CoinsSpent { get; set; }
        public long UnitsExpired { get; set; }
    }
}
