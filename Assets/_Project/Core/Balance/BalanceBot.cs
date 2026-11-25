using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Catalog;
using Game.Core.Commands;
using Game.Core.Farm;
using Game.Core.Simulation;
using Game.Core.Store;
using Game.Core.Supplier;

namespace Game.Core.Balance
{
    public enum Strategy
    {
        /// <summary>Só vende o que a fazenda produz (hortifruti); nunca usa o fornecedor.</summary>
        FarmOnly,
        /// <summary>Não planta; compra tudo do fornecedor.</summary>
        SupplierOnly,
        /// <summary>Fazenda abastece o hortifruti; fornecedor cobre o resto e as faltas.</summary>
        Mixed,
    }

    /// <summary>
    /// Jogador automático para o simulador de balanceamento. Age uma vez por segundo simulado,
    /// sempre por comandos (as mesmas regras do jogador). Heurísticas simples e documentadas.
    /// </summary>
    public sealed class BalanceBot
    {
        private readonly GameContext _ctx;
        private readonly CommandExecutor _exec;
        private readonly Strategy _strategy;

        /// <summary>Unidades de um produto (armazém + prateleira + a caminho) a partir das quais não planta/compra mais.</summary>
        public int StockTarget { get; set; } = 40;

        public BalanceBot(GameContext ctx, CommandExecutor exec, Strategy strategy)
        {
            _ctx = ctx; _exec = exec; _strategy = strategy;
        }

        private bool UsesFarm => _strategy != Strategy.SupplierOnly;
        private bool UsesSupplier => _strategy != Strategy.FarmOnly;

        public void Act()
        {
            _exec.Execute(new ScanItemCommand()); // caixa manual: ~1 item por segundo
            if (UsesFarm) Farm();
            Restock();
            if (UsesSupplier) Supply();
            Invest();
        }

        private long Now => _ctx.NowMs;

        private int TotalStock(string productId)
        {
            int n = _ctx.Warehouse.Count(productId, Now) + SupplierRules.PendingUnits(_ctx.State, productId);
            foreach (var s in _ctx.State.Store.Shelves) if (s.ProductId == productId) n += StoreRules.Units(s, Now);
            return n;
        }

        private void Farm()
        {
            var plots = _ctx.State.Farm.Plots;
            for (int i = 0; i < plots.Count; i++)
            {
                var phase = FarmRules.GetPhase(plots[i], Now);
                if (phase == PlotPhase.Ready) _exec.Execute(new HarvestCommand(i));
                else if (phase == PlotPhase.Growing && !plots[i].Watered) _exec.Execute(new WaterCommand(i));
                if (FarmRules.GetPhase(plots[i], Now) != PlotPhase.Empty) continue;

                // Maior lucro por segundo entre as culturas liberadas, pagáveis e sem excesso de estoque.
                CropDef best = null;
                double bestRate = 0;
                foreach (var crop in _ctx.Catalog.Crops)
                {
                    bool hasSeed = FarmRules.SeedsOf(_ctx.State, crop.Id) > 0;
                    if (!FarmRules.IsCropUnlocked(crop, _ctx.State) || (!hasSeed && !_ctx.Wallet.CanAffordCoins(crop.SeedCost))) continue;
                    var product = _ctx.Catalog.GetProduct(crop.ProductId);
                    if (TotalStock(product.Id) + CountGrowing(crop.Id) * crop.Yield >= StockTarget) continue;
                    double rate = (crop.Yield * (double)product.ReferencePrice - crop.SeedCost) / crop.GrowSeconds;
                    if (rate > bestRate) { bestRate = rate; best = crop; }
                }
                if (best == null) continue;
                if (FarmRules.SeedsOf(_ctx.State, best.Id) < 1) _exec.Execute(new BuySeedsCommand(best.Id, 1)); // vendinha
                _exec.Execute(new PlantCommand(i, best.Id));
            }
        }

        private int CountGrowing(string cropId) =>
            _ctx.State.Farm.Plots.Count(p => p.CropId == cropId && FarmRules.GetPhase(p, Now) == PlotPhase.Growing);

        /// <summary>Produto que esta prateleira deve vender.</summary>
        private string PlannedProduct(int shelfIndex)
        {
            var shelf = _ctx.State.Store.Shelves[shelfIndex];
            if (!_ctx.Catalog.TryGetFurniture(shelf.FurnitureId, out var furniture)) return null;
            if (shelf.ProductId != null && StoreRules.Units(shelf, Now) > 0) return shelf.ProductId;

            var options = _ctx.Catalog.Products.Where(p => p.CategoryId == furniture.CategoryId).ToList();
            if (options.Count == 0) return null;
            // Prefere o que já tem no armazém; senão, espalha produtos diferentes entre prateleiras da mesma categoria.
            var inWarehouse = options.OrderByDescending(p => _ctx.Warehouse.Count(p.Id, Now)).First();
            if (_ctx.Warehouse.Count(inWarehouse.Id, Now) > 0) return inWarehouse.Id;
            int sameCategoryBefore = 0;
            for (int i = 0; i < shelfIndex; i++)
                if (_ctx.Catalog.TryGetFurniture(_ctx.State.Store.Shelves[i].FurnitureId, out var f) && f.CategoryId == furniture.CategoryId)
                    sameCategoryBefore++;
            return options[sameCategoryBefore % options.Count].Id;
        }

        private void Restock()
        {
            var shelves = _ctx.State.Store.Shelves;
            for (int i = 0; i < shelves.Count; i++)
            {
                string productId = PlannedProduct(i);
                if (productId == null || !_ctx.Catalog.TryGetFurniture(shelves[i].FurnitureId, out var f)) continue;
                int free = StoreRules.FreeSpace(shelves[i], f, Now);
                if (free < f.Capacity / 4) continue;
                int units = Math.Min(free, _ctx.Warehouse.Count(productId, Now));
                if (units > 0) _exec.Execute(new RestockCommand(i, productId, units));
            }
        }

        private void Supply()
        {
            var shelves = _ctx.State.Store.Shelves;
            for (int i = 0; i < shelves.Count; i++)
            {
                if (!_ctx.Catalog.TryGetFurniture(shelves[i].FurnitureId, out var f)) continue;
                bool farmCategory = _ctx.Catalog.Crops.Any(c => _ctx.Catalog.GetProduct(c.ProductId).CategoryId == f.CategoryId);
                string productId = PlannedProduct(i);
                if (productId == null) continue;

                if (_strategy == Strategy.Mixed && farmCategory)
                {
                    // No misto o hortifruti vem da fazenda; o fornecedor só cobre se a categoria inteira secou.
                    int categoryStock = _ctx.Catalog.Products.Where(p => p.CategoryId == f.CategoryId).Sum(p => TotalStock(p.Id));
                    if (categoryStock > 0) continue;
                }

                int missing = f.Capacity - TotalStock(productId);
                if (missing <= f.Capacity / 4) continue;
                long unitPrice = SupplierRules.UnitPrice(_ctx.Catalog.GetProduct(productId), _ctx.Catalog.Economy);
                long reserve = UsesFarm ? 40 : 0; // misto guarda dinheiro para sementes
                long budget = _ctx.Wallet.Coins - reserve;
                int units = (int)Math.Min(missing, Math.Max(0, budget / unitPrice));
                if (units > 0) _exec.Execute(new BuyFromSupplierCommand(productId, units));
            }
        }

        private void Invest()
        {
            var economy = _ctx.Catalog.Economy;

            // Contrata o caixa quando a reserva cobre contratação + algumas folhas de salário (mesma regra para todos).
            foreach (var staff in _ctx.Catalog.Staff)
                if (Staff.StaffRules.CountOf(_ctx.State.Staff, staff.Id) < staff.MaxHired &&
                    _ctx.Wallet.Coins >= staff.HireCost + staff.Salary * 6)
                    _exec.Execute(new Staff.HireCommand(staff.Id));
            if (UsesFarm && _ctx.State.Farm.Plots.Count < economy.MaxPlots)
            {
                long cost = FarmRules.NextPlotCost(_ctx.State, economy);
                if (_ctx.Wallet.Coins >= cost * 3) _exec.Execute(new BuyPlotCommand());
            }

            // Mais um móvel da categoria que mais perde vendas (aproximação: a que tem menos prateleiras).
            var store = _ctx.State.Store;
            if (store.Shelves.Count >= 8) return;
            var options = _ctx.Catalog.Furniture
                .Where(f => UsesSupplier || _ctx.Catalog.Crops.Any(c => _ctx.Catalog.GetProduct(c.ProductId).CategoryId == f.CategoryId))
                .OrderBy(f => store.Shelves.Count(s => s.FurnitureId == f.Id))
                .ToList();
            if (options.Count == 0) return;
            var pick = options[0];
            long furnitureCost = StoreRules.FurnitureCost(_ctx.State, pick, economy);
            if (_ctx.Wallet.Coins >= furnitureCost * 4) _exec.Execute(new BuyFurnitureCommand(pick.Id));
        }
    }
}
