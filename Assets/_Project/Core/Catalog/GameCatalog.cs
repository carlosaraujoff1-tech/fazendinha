using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Game.Core.Catalog
{
    public sealed class CatalogException : Exception
    {
        public CatalogException(IReadOnlyList<string> errors)
            : base("Catálogo inválido:\n- " + string.Join("\n- ", errors))
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }
    }

    /// <summary>
    /// Todo o conteúdo do jogo, já validado. Criado a partir dos ScriptableObjects (Game.Content)
    /// ou de dados de teste. O código de regra consulta o catálogo por ID, nunca compara IDs fixos.
    /// </summary>
    public sealed class GameCatalog
    {
        private static readonly Regex IdPattern = new Regex("^[a-z0-9][a-z0-9_.-]*$");

        private readonly Dictionary<string, ProductDef> _products;
        private readonly Dictionary<string, CropDef> _crops;

        private GameCatalog(Dictionary<string, ProductDef> products, Dictionary<string, CropDef> crops, EconomyConfig economy)
        {
            _products = products;
            _crops = crops;
            Economy = economy;
        }

        public EconomyConfig Economy { get; }
        public IEnumerable<ProductDef> Products => _products.Values;
        public IEnumerable<CropDef> Crops => _crops.Values;

        public bool TryGetProduct(string id, out ProductDef product) => _products.TryGetValue(id ?? "", out product);
        public bool TryGetCrop(string id, out CropDef crop) => _crops.TryGetValue(id ?? "", out crop);

        public ProductDef GetProduct(string id) =>
            TryGetProduct(id, out var p) ? p : throw new KeyNotFoundException($"Produto '{id}' não existe no catálogo.");

        public CropDef GetCrop(string id) =>
            TryGetCrop(id, out var c) ? c : throw new KeyNotFoundException($"Cultura '{id}' não existe no catálogo.");

        /// <summary>Valida e monta o catálogo. Lança <see cref="CatalogException"/> com TODOS os erros encontrados.</summary>
        public static GameCatalog Build(IEnumerable<ProductDef> products, IEnumerable<CropDef> crops, EconomyConfig economy = null)
        {
            var errors = new List<string>();
            var productMap = new Dictionary<string, ProductDef>();
            var cropMap = new Dictionary<string, CropDef>();
            economy = economy ?? new EconomyConfig();

            foreach (var p in products ?? Enumerable.Empty<ProductDef>())
            {
                if (p == null) { errors.Add("Produto nulo."); continue; }
                if (!CheckId(p.Id, "Produto", errors)) continue;
                if (productMap.ContainsKey(p.Id)) { errors.Add($"Produto '{p.Id}' duplicado."); continue; }
                if (string.IsNullOrWhiteSpace(p.CategoryId)) errors.Add($"Produto '{p.Id}' sem categoria.");
                if (p.ReferencePrice <= 0) errors.Add($"Produto '{p.Id}' com preço de referência <= 0.");
                if (p.ShelfLifeSeconds < 0) errors.Add($"Produto '{p.Id}' com validade negativa.");
                productMap[p.Id] = p;
            }

            foreach (var c in crops ?? Enumerable.Empty<CropDef>())
            {
                if (c == null) { errors.Add("Cultura nula."); continue; }
                if (!CheckId(c.Id, "Cultura", errors)) continue;
                if (cropMap.ContainsKey(c.Id)) { errors.Add($"Cultura '{c.Id}' duplicada."); continue; }
                if (!productMap.ContainsKey(c.ProductId ?? "")) errors.Add($"Cultura '{c.Id}' aponta para produto inexistente '{c.ProductId}'.");
                if (c.GrowSeconds <= 0) errors.Add($"Cultura '{c.Id}' com tempo de crescimento <= 0.");
                if (c.SeedCost < 0) errors.Add($"Cultura '{c.Id}' com semente de custo negativo.");
                if (c.Yield <= 0) errors.Add($"Cultura '{c.Id}' com rendimento <= 0.");
                if (c.UnlockLevel < 1) errors.Add($"Cultura '{c.Id}' com nível de desbloqueio < 1.");
                cropMap[c.Id] = c;
            }

            if (economy.StartingCoins < 0) errors.Add("Moedas iniciais negativas.");
            if (economy.RepeatableCostGrowth < 1) errors.Add("Crescimento de custo repetível < 1.");
            if (economy.SupplierPriceFactor <= 0) errors.Add("Fator de preço do fornecedor <= 0.");
            if (economy.WaterTimeReduction < 0 || economy.WaterTimeReduction >= 1) errors.Add("Redução por rega fora de [0, 1).");
            if (economy.OfflineFarmCapSeconds < 0) errors.Add("Limite offline negativo.");
            if (economy.InitialPlots < 1) errors.Add("Fazenda precisa de pelo menos 1 parcela inicial.");
            if (economy.MaxPlots < economy.InitialPlots) errors.Add("Máximo de parcelas menor que o inicial.");
            if (economy.PlotBaseCost < 0) errors.Add("Custo de parcela negativo.");

            if (errors.Count > 0) throw new CatalogException(errors);
            return new GameCatalog(productMap, cropMap, economy);
        }

        private static bool CheckId(string id, string kind, List<string> errors)
        {
            if (string.IsNullOrEmpty(id) || !IdPattern.IsMatch(id))
            {
                errors.Add($"{kind} com ID inválido '{id}' (use minúsculas, números, '_', '-', '.').");
                return false;
            }
            return true;
        }
    }
}
