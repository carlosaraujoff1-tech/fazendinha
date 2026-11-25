using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Animals;
using Game.Core.Staff;
using Game.Core.Store;
using Newtonsoft.Json;

namespace Game.Core.Catalog
{
    /// <summary>
    /// Lê o catálogo a partir de JSON (Assets/_Project/Content/Data/catalog.json, depois remote config).
    /// Campos ausentes usam os padrões de <see cref="EconomyConfig"/>; campos desconhecidos são erro,
    /// para pegar erro de digitação no balanceamento.
    /// </summary>
    public static class CatalogJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            TypeNameHandling = TypeNameHandling.None,
        };

        public static GameCatalog Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new CatalogException(new[] { "JSON do catálogo vazio." });

            CatalogDto dto;
            try { dto = JsonConvert.DeserializeObject<CatalogDto>(json, Settings); }
            catch (JsonException e) { throw new CatalogException(new[] { "JSON do catálogo inválido: " + e.Message }); }
            if (dto == null) throw new CatalogException(new[] { "JSON do catálogo vazio." });

            var products = (dto.Products ?? new List<ProductDto>())
                .Select(p => p == null ? null : new ProductDef(p.Id, p.Category, p.ReferencePrice, p.ShelfLifeSeconds, p.Name));
            var crops = (dto.Crops ?? new List<CropDto>())
                .Select(c => c == null ? null : new CropDef(c.Id, c.Product, c.GrowSeconds, c.SeedCost, c.Yield, c.UnlockLevel, c.Name));
            var customers = (dto.CustomerTypes ?? new List<CustomerDto>())
                .Select(c => c == null ? null : new CustomerTypeDef(c.Id, c.Name, c.A, c.B, c.Weight, c.PatienceSeconds));
            var furniture = (dto.Furniture ?? new List<FurnitureDto>())
                .Select(f => f == null ? null : new FurnitureDef(f.Id, f.Name, f.Category, f.Capacity, f.BaseCost));
            var staff = (dto.Staff ?? new List<StaffDto>())
                .Select(s => s == null ? null : new StaffDef(s.Id, s.Name, s.Role, s.Salary, s.HireCost, s.SecondsPerAction, s.MaxHired));
            var animals = (dto.Animals ?? new List<AnimalDto>())
                .Select(a => a == null ? null : new AnimalDef(a.Id, a.Name, a.Product, a.Feed ?? new List<string>(), a.FeedPerCycle,
                    a.CycleSeconds, a.Yield, a.Cost, a.MaxCount, a.UnlockLevel));
            return GameCatalog.Build(products, crops, dto.Economy ?? new EconomyConfig(), customers, furniture, dto.Store ?? new StoreConfig(), staff, animals);
        }

        private sealed class CatalogDto
        {
            [JsonProperty("economy")] public EconomyConfig Economy { get; set; }
            [JsonProperty("products")] public List<ProductDto> Products { get; set; }
            [JsonProperty("crops")] public List<CropDto> Crops { get; set; }
            [JsonProperty("customerTypes")] public List<CustomerDto> CustomerTypes { get; set; }
            [JsonProperty("furniture")] public List<FurnitureDto> Furniture { get; set; }
            [JsonProperty("store")] public StoreConfig Store { get; set; }
            [JsonProperty("staff")] public List<StaffDto> Staff { get; set; }
            [JsonProperty("animals")] public List<AnimalDto> Animals { get; set; }
        }

        private sealed class AnimalDto
        {
            [JsonProperty("id")] public string Id { get; set; }
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("product")] public string Product { get; set; }
            [JsonProperty("feed")] public List<string> Feed { get; set; }
            [JsonProperty("feedPerCycle")] public int FeedPerCycle { get; set; } = 1;
            [JsonProperty("cycleSeconds")] public int CycleSeconds { get; set; }
            [JsonProperty("yield")] public int Yield { get; set; }
            [JsonProperty("cost")] public long Cost { get; set; }
            [JsonProperty("maxCount")] public int MaxCount { get; set; } = 1;
            [JsonProperty("unlockLevel")] public int UnlockLevel { get; set; } = 1;
        }

        private sealed class StaffDto
        {
            [JsonProperty("id")] public string Id { get; set; }
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("role")] [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))] public StaffRole Role { get; set; }
            [JsonProperty("salary")] public long Salary { get; set; }
            [JsonProperty("hireCost")] public long HireCost { get; set; }
            [JsonProperty("secondsPerAction")] public double SecondsPerAction { get; set; }
            [JsonProperty("maxHired")] public int MaxHired { get; set; } = 1;
        }

        private sealed class CustomerDto
        {
            [JsonProperty("id")] public string Id { get; set; }
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("a")] public double A { get; set; }
            [JsonProperty("b")] public double B { get; set; }
            [JsonProperty("weight")] public double Weight { get; set; }
            [JsonProperty("patienceSeconds")] public int PatienceSeconds { get; set; }
        }

        private sealed class FurnitureDto
        {
            [JsonProperty("id")] public string Id { get; set; }
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("category")] public string Category { get; set; }
            [JsonProperty("capacity")] public int Capacity { get; set; }
            [JsonProperty("baseCost")] public long BaseCost { get; set; }
        }

        private sealed class ProductDto
        {
            [JsonProperty("id")] public string Id { get; set; }
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("category")] public string Category { get; set; }
            [JsonProperty("referencePrice")] public long ReferencePrice { get; set; }
            [JsonProperty("shelfLifeSeconds")] public int ShelfLifeSeconds { get; set; }
        }

        private sealed class CropDto
        {
            [JsonProperty("id")] public string Id { get; set; }
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("product")] public string Product { get; set; }
            [JsonProperty("growSeconds")] public int GrowSeconds { get; set; }
            [JsonProperty("seedCost")] public long SeedCost { get; set; }
            [JsonProperty("yield")] public int Yield { get; set; }
            [JsonProperty("unlockLevel")] public int UnlockLevel { get; set; } = 1;
        }
    }
}
