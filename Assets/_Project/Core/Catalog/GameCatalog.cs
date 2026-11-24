using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Core.Staff;
using Game.Core.Store;

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
        private readonly Dictionary<string, CustomerTypeDef> _customers;
        private readonly Dictionary<string, FurnitureDef> _furniture;
        private readonly List<ProductDef> _productList;
        private readonly List<CustomerTypeDef> _customerList;
        private readonly Dictionary<string, StaffDef> _staff = new Dictionary<string, StaffDef>();

        private GameCatalog(Dictionary<string, ProductDef> products, Dictionary<string, CropDef> crops,
            Dictionary<string, CustomerTypeDef> customers, Dictionary<string, FurnitureDef> furniture,
            List<ProductDef> productList, List<CustomerTypeDef> customerList, EconomyConfig economy, StoreConfig store)
        {
            _products = products;
            _crops = crops;
            _customers = customers;
            _furniture = furniture;
            _productList = productList;
            _customerList = customerList;
            Economy = economy;
            Store = store;
        }

        public EconomyConfig Economy { get; }
        public StoreConfig Store { get; }

        /// <summary>Produtos na ordem em que aparecem nos dados (determinística).</summary>
        public IReadOnlyList<ProductDef> Products => _productList;
        public IEnumerable<CropDef> Crops => _crops.Values;

        /// <summary>Tipos de cliente na ordem dos dados (determinística para o sorteio).</summary>
        public IReadOnlyList<CustomerTypeDef> CustomerTypes => _customerList;
        public IEnumerable<FurnitureDef> Furniture => _furniture.Values;

        public IEnumerable<StaffDef> Staff => _staff.Values;
        public bool TryGetStaff(string id, out StaffDef def) => _staff.TryGetValue(id ?? "", out def);
        public StaffDef GetStaff(string id) =>
            TryGetStaff(id, out var s) ? s : throw new KeyNotFoundException($"Funcionário '{id}' não existe no catálogo.");

        public bool TryGetCustomerType(string id, out CustomerTypeDef def) => _customers.TryGetValue(id ?? "", out def);
        public bool TryGetFurniture(string id, out FurnitureDef def) => _furniture.TryGetValue(id ?? "", out def);

        public FurnitureDef GetFurniture(string id) =>
            TryGetFurniture(id, out var f) ? f : throw new KeyNotFoundException($"Móvel '{id}' não existe no catálogo.");

        public CustomerTypeDef GetCustomerType(string id) =>
            TryGetCustomerType(id, out var c) ? c : throw new KeyNotFoundException($"Cliente '{id}' não existe no catálogo.");

        public bool TryGetProduct(string id, out ProductDef product) => _products.TryGetValue(id ?? "", out product);
        public bool TryGetCrop(string id, out CropDef crop) => _crops.TryGetValue(id ?? "", out crop);

        public ProductDef GetProduct(string id) =>
            TryGetProduct(id, out var p) ? p : throw new KeyNotFoundException($"Produto '{id}' não existe no catálogo.");

        public CropDef GetCrop(string id) =>
            TryGetCrop(id, out var c) ? c : throw new KeyNotFoundException($"Cultura '{id}' não existe no catálogo.");

        /// <summary>Valida e monta o catálogo. Lança <see cref="CatalogException"/> com TODOS os erros encontrados.</summary>
        public static GameCatalog Build(IEnumerable<ProductDef> products, IEnumerable<CropDef> crops, EconomyConfig economy = null,
            IEnumerable<CustomerTypeDef> customerTypes = null, IEnumerable<FurnitureDef> furniture = null, StoreConfig store = null,
            IEnumerable<StaffDef> staff = null)
        {
            var errors = new List<string>();
            var productMap = new Dictionary<string, ProductDef>();
            var productList = new List<ProductDef>();
            var cropMap = new Dictionary<string, CropDef>();
            var customerMap = new Dictionary<string, CustomerTypeDef>();
            var customerList = new List<CustomerTypeDef>();
            var furnitureMap = new Dictionary<string, FurnitureDef>();
            economy = economy ?? new EconomyConfig();
            store = store ?? new StoreConfig();

            foreach (var p in products ?? Enumerable.Empty<ProductDef>())
            {
                if (p == null) { errors.Add("Produto nulo."); continue; }
                if (!CheckId(p.Id, "Produto", errors)) continue;
                if (productMap.ContainsKey(p.Id)) { errors.Add($"Produto '{p.Id}' duplicado."); continue; }
                if (string.IsNullOrWhiteSpace(p.CategoryId)) errors.Add($"Produto '{p.Id}' sem categoria.");
                if (p.ReferencePrice <= 0) errors.Add($"Produto '{p.Id}' com preço de referência <= 0.");
                if (p.ShelfLifeSeconds < 0) errors.Add($"Produto '{p.Id}' com validade negativa.");
                productMap[p.Id] = p;
                productList.Add(p);
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

            foreach (var c in customerTypes ?? Enumerable.Empty<CustomerTypeDef>())
            {
                if (c == null) { errors.Add("Tipo de cliente nulo."); continue; }
                if (!CheckId(c.Id, "Cliente", errors)) continue;
                if (customerMap.ContainsKey(c.Id)) { errors.Add($"Cliente '{c.Id}' duplicado."); continue; }
                if (c.ToleranceA < 0) errors.Add($"Cliente '{c.Id}' com 'a' negativo.");
                if (c.RangeB < 0) errors.Add($"Cliente '{c.Id}' com 'b' negativo.");
                if (c.Weight < 0) errors.Add($"Cliente '{c.Id}' com peso negativo.");
                if (c.PatienceSeconds <= 0) errors.Add($"Cliente '{c.Id}' com paciência <= 0.");
                customerMap[c.Id] = c;
                customerList.Add(c);
            }
            if (customerList.Count > 0 && customerList.Sum(c => Math.Max(0, c.Weight)) <= 0)
                errors.Add("Soma dos pesos dos clientes deve ser > 0.");

            var categories = new HashSet<string>(productList.Select(p => p.CategoryId));
            foreach (var f in furniture ?? Enumerable.Empty<FurnitureDef>())
            {
                if (f == null) { errors.Add("Móvel nulo."); continue; }
                if (!CheckId(f.Id, "Móvel", errors)) continue;
                if (furnitureMap.ContainsKey(f.Id)) { errors.Add($"Móvel '{f.Id}' duplicado."); continue; }
                if (!categories.Contains(f.CategoryId ?? "")) errors.Add($"Móvel '{f.Id}' com categoria sem produtos '{f.CategoryId}'.");
                if (f.Capacity <= 0) errors.Add($"Móvel '{f.Id}' com capacidade <= 0.");
                if (f.BaseCost < 0) errors.Add($"Móvel '{f.Id}' com custo negativo.");
                furnitureMap[f.Id] = f;
            }

            foreach (var id in store.InitialFurniture ?? new string[0])
                if (!furnitureMap.ContainsKey(id ?? "")) errors.Add($"Móvel inicial inexistente '{id}'.");
            int initialCount = store.InitialFurniture?.Length ?? 0;
            if (store.MaxFurniture < initialCount) errors.Add("Máximo de móveis menor que os iniciais.");
            if (store.SpawnIntervalSeconds <= 0) errors.Add("Intervalo de clientes <= 0.");
            if (store.MaxCustomersInStore < 1) errors.Add("Loja precisa aceitar ao menos 1 cliente.");
            if (store.ShoppingSecondsPerItem < 0) errors.Add("Tempo por item negativo.");
            if (store.MinItemsPerList < 1 || store.MaxItemsPerList < store.MinItemsPerList) errors.Add("Faixa de itens por lista inválida.");
            if (store.MinUnitsPerItem < 1 || store.MaxUnitsPerItem < store.MinUnitsPerItem) errors.Add("Faixa de unidades por item inválida.");
            if (store.MaxCatchUpSeconds <= 0) errors.Add("MaxCatchUpSeconds <= 0.");
            if (store.MaxPriceFactor < 1) errors.Add("MaxPriceFactor < 1.");

            var staffMap = new Dictionary<string, StaffDef>();
            foreach (var s in staff ?? Enumerable.Empty<StaffDef>())
            {
                if (s == null) { errors.Add("Funcionário nulo."); continue; }
                if (!CheckId(s.Id, "Funcionário", errors)) continue;
                if (staffMap.ContainsKey(s.Id)) { errors.Add($"Funcionário '{s.Id}' duplicado."); continue; }
                if (s.Salary < 0 || s.HireCost < 0) errors.Add($"Funcionário '{s.Id}' com custo negativo.");
                if (s.SecondsPerAction <= 0) errors.Add($"Funcionário '{s.Id}' com tempo por ação <= 0.");
                if (s.MaxHired < 1) errors.Add($"Funcionário '{s.Id}' com máximo < 1.");
                staffMap[s.Id] = s;
            }
            if (economy.MaxSupplierOrderUnits < 1) errors.Add("Pedido máximo ao fornecedor < 1.");
            if (economy.SalaryIntervalSeconds < 1) errors.Add("Intervalo de salário < 1 s.");

            if (errors.Count > 0) throw new CatalogException(errors);
            var catalog = new GameCatalog(productMap, cropMap, customerMap, furnitureMap, productList, customerList, economy, store);
            foreach (var kv in staffMap) catalog._staff[kv.Key] = kv.Value;
            return catalog;
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
