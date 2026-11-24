using System.Collections.Generic;
using Game.Core.Staff;
using Game.Core.Store;
using Game.Core.Supplier;

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

        public FarmState Farm { get; set; } = new FarmState();

        public ProgressState Progress { get; set; } = new ProgressState();

        public StoreState Store { get; set; } = new StoreState();

        public SupplierState Supplier { get; set; } = new SupplierState();

        public StaffState Staff { get; set; } = new StaffState();
    }

    public sealed class FarmState
    {
        /// <summary>Parcelas; o índice na lista é o ID da parcela.</summary>
        public List<PlotState> Plots { get; set; } = new List<PlotState>();

        /// <summary>Quantas parcelas extras já foram compradas (n da curva de custo).</summary>
        public int PlotsBought { get; set; }
    }

    public sealed class PlotState
    {
        /// <summary>Cultura plantada ou null se vazia.</summary>
        public string CropId { get; set; }
        public long PlantedAtMs { get; set; }
        public long ReadyAtMs { get; set; }
        public bool Watered { get; set; }

        public bool IsEmpty => string.IsNullOrEmpty(CropId);

        public void Clear()
        {
            CropId = null;
            PlantedAtMs = 0;
            ReadyAtMs = 0;
            Watered = false;
        }
    }

    public sealed class ProgressState
    {
        public int Level { get; set; } = 1;
        public long Xp { get; set; }
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
        public long CropsHarvested { get; set; }
        public long CustomersServed { get; set; }
        public long CustomersLeftAngry { get; set; }
        public long CustomersLeftEmpty { get; set; }
        public long ItemsMissing { get; set; }
        public long ItemsRejectedByPrice { get; set; }
        public long UnitsSold { get; set; }
        public long SalesRevenue { get; set; }
        public long SupplierSpent { get; set; }
        public long SalariesPaid { get; set; }
        public long OfflineMsIgnored { get; set; }
    }
}
