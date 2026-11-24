namespace Game.Core.Catalog
{
    /// <summary>Produto vendável (hortifruti, laticínios, mercearia...). Imutável depois de criado.</summary>
    public sealed class ProductDef
    {
        public ProductDef(string id, string categoryId, long referencePrice, int shelfLifeSeconds)
        {
            Id = id;
            CategoryId = categoryId;
            ReferencePrice = referencePrice;
            ShelfLifeSeconds = shelfLifeSeconds;
        }

        /// <summary>ID de texto estável (ex.: "alface"). Nunca muda depois de publicado.</summary>
        public string Id { get; }

        /// <summary>Categoria da prateleira (ex.: "hortifruti").</summary>
        public string CategoryId { get; }

        /// <summary>Preço de referência de mercado (P).</summary>
        public long ReferencePrice { get; }

        /// <summary>Validade a partir da produção, em segundos. 0 = não vence.</summary>
        public int ShelfLifeSeconds { get; }

        public bool Perishable => ShelfLifeSeconds > 0;
    }

    /// <summary>Cultura plantável. Colher gera <see cref="Yield"/> unidades de <see cref="ProductId"/>.</summary>
    public sealed class CropDef
    {
        public CropDef(string id, string productId, int growSeconds, long seedCost, int yield, int unlockLevel = 1)
        {
            Id = id;
            ProductId = productId;
            GrowSeconds = growSeconds;
            SeedCost = seedCost;
            Yield = yield;
            UnlockLevel = unlockLevel;
        }

        public string Id { get; }
        public string ProductId { get; }
        public int GrowSeconds { get; }
        public long SeedCost { get; }
        public int Yield { get; }
        public int UnlockLevel { get; }
    }

    /// <summary>Parâmetros globais de economia (todos balanceáveis por dados).</summary>
    public sealed class EconomyConfig
    {
        public long StartingCoins { get; set; } = 100;
        public long StartingGems { get; set; } = 0;
        public double RepeatableCostGrowth { get; set; } = 1.15;
        public double SupplierPriceFactor { get; set; } = 0.8;
        public int SupplierDeliverySeconds { get; set; } = 120;
        public double WaterTimeReduction { get; set; } = 0.25;
        public int OfflineFarmCapSeconds { get; set; } = 4 * 3600;
        public int SalaryIntervalSeconds { get; set; } = 600;
    }
}
