using System;
using System.Collections.Generic;
using System.Linq;
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
            return GameCatalog.Build(products, crops, dto.Economy ?? new EconomyConfig());
        }

        private sealed class CatalogDto
        {
            [JsonProperty("economy")] public EconomyConfig Economy { get; set; }
            [JsonProperty("products")] public List<ProductDto> Products { get; set; }
            [JsonProperty("crops")] public List<CropDto> Crops { get; set; }
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
