namespace Game.Core.Store
{
    /// <summary>Tipo de cliente. Aceita até P×(1+a) sem hesitar; a chance cai a zero em P×(1+a+b).</summary>
    public sealed class CustomerTypeDef
    {
        public CustomerTypeDef(string id, string name, double toleranceA, double rangeB, double weight, int patienceSeconds)
        {
            Id = id; Name = string.IsNullOrEmpty(name) ? id : name;
            ToleranceA = toleranceA; RangeB = rangeB; Weight = weight; PatienceSeconds = patienceSeconds;
        }

        public string Id { get; }
        public string Name { get; }
        public double ToleranceA { get; }
        public double RangeB { get; }
        public double Weight { get; }
        public int PatienceSeconds { get; }
    }

    /// <summary>Móvel da loja que expõe produtos de uma categoria.</summary>
    public sealed class FurnitureDef
    {
        public FurnitureDef(string id, string name, string categoryId, int capacity, long baseCost)
        {
            Id = id; Name = string.IsNullOrEmpty(name) ? id : name;
            CategoryId = categoryId; Capacity = capacity; BaseCost = baseCost;
        }

        public string Id { get; }
        public string Name { get; }
        public string CategoryId { get; }
        public int Capacity { get; }
        public long BaseCost { get; }
    }

    /// <summary>Parâmetros da loja (balanceáveis por dados).</summary>
    public sealed class StoreConfig
    {
        public string[] InitialFurniture { get; set; } = new string[0];
        public int MaxFurniture { get; set; } = 12;
        public double SpawnIntervalSeconds { get; set; } = 6;
        public int MaxCustomersInStore { get; set; } = 8;
        public double ShoppingSecondsPerItem { get; set; } = 2.5;
        public int MinItemsPerList { get; set; } = 1;
        public int MaxItemsPerList { get; set; } = 4;
        public int MinUnitsPerItem { get; set; } = 1;
        public int MaxUnitsPerItem { get; set; } = 3;
        public double FarmSealBonus { get; set; } = 0.05;
        /// <summary>Chance de aceitar um substituto da mesma categoria quando falta o item desejado.</summary>
        public double SubstituteAcceptance { get; set; } = 0.6;
        public double DiscountQuantityFactor { get; set; } = 0.5;
        /// <summary>Paciência na fila cai (1 + isto × clientes à frente) por segundo.</summary>
        public double QueueDrainPerCustomerAhead { get; set; } = 0.25;
        /// <summary>Lacuna de tempo acima disso = loja estava fechada (app em segundo plano/fechado).</summary>
        public double MaxCatchUpSeconds { get; set; } = 10;
        public double MaxPriceFactor { get; set; } = 10;
    }
}
