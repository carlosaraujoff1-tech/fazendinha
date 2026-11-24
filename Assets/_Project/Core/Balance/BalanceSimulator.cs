using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Game.Core.Catalog;
using Game.Core.Events;
using Game.Core.Farm;
using Game.Core.Simulation;
using Game.Core.Store;
using Game.Core.Supplier;
using Game.Core.Time;

namespace Game.Core.Balance
{
    public sealed class BalanceSample
    {
        public int Minute;
        public long Coins, NetWorth, WarehouseUnits, ShelfUnits, PendingUnits, ExpiredUnits;
        public long Revenue, SupplierSpent, Invested, Served, Angry, LeftEmpty, Missing, RejectedByPrice;
        public int Plots, Shelves;
    }

    public sealed class BalanceResult
    {
        public Strategy Strategy;
        public int Hours;
        public readonly List<BalanceSample> Samples = new List<BalanceSample>();
        /// <summary>Minuto em que ficou sem saída (sem dinheiro, estoque, plantio ou pedido), ou −1.</summary>
        public int StuckAtMinute = -1;

        public BalanceSample Final => Samples[Samples.Count - 1];

        public string ToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("minute,coins,net_worth,warehouse_units,shelf_units,pending_units,expired_units,revenue,supplier_spent,invested,served,angry,left_empty,missing,rejected_by_price,plots,shelves");
            foreach (var s in Samples)
                sb.AppendLine(string.Join(",", new object[] { s.Minute, s.Coins, s.NetWorth, s.WarehouseUnits, s.ShelfUnits, s.PendingUnits,
                    s.ExpiredUnits, s.Revenue, s.SupplierSpent, s.Invested, s.Served, s.Angry, s.LeftEmpty, s.Missing, s.RejectedByPrice, s.Plots, s.Shelves }
                    .Select(o => Convert.ToString(o, CultureInfo.InvariantCulture))));
            return sb.ToString();
        }
    }

    /// <summary>Roda um jogo inteiro sem cena, com relógio falso e um <see cref="BalanceBot"/>.</summary>
    public static class BalanceSimulator
    {
        public static BalanceResult Run(GameCatalog catalog, Strategy strategy, int hours, ulong seed = 12345)
        {
            var clock = new ManualClock();
            var state = NewGame.Create(catalog, clock);
            state.Store.Rng.Seed = seed;
            var ctx = new GameContext(state, catalog, clock, NullEventBus.Instance);
            var exec = GameCommands.CreateExecutor(ctx);
            var bot = new BalanceBot(ctx, exec, strategy);
            var result = new BalanceResult { Strategy = strategy, Hours = hours };

            int totalSeconds = hours * 3600;
            for (int s = 1; s <= totalSeconds; s++)
            {
                clock.AdvanceSeconds(1);
                GameSimulation.Tick(ctx);
                bot.Act();
                if (s % 60 != 0) continue;
                var sample = Sample(ctx, s / 60);
                result.Samples.Add(sample);
                if (result.StuckAtMinute < 0 && IsStuck(ctx, strategy)) result.StuckAtMinute = sample.Minute;
            }
            return result;
        }

        private static BalanceSample Sample(GameContext ctx, int minute)
        {
            var st = ctx.State;
            long now = ctx.NowMs;
            long warehouse = 0, shelf = 0, pending = 0, stockValue = 0;
            foreach (var p in ctx.Catalog.Products)
            {
                long unitValue = SupplierRules.UnitPrice(p, ctx.Catalog.Economy);
                int w = ctx.Warehouse.Count(p.Id, now);
                int pend = SupplierRules.PendingUnits(st, p.Id);
                warehouse += w;
                pending += pend;
                stockValue += (w + pend) * unitValue;
            }
            foreach (var s in st.Store.Shelves)
            {
                int u = StoreRules.Units(s, now);
                shelf += u;
                if (s.ProductId != null && ctx.Catalog.TryGetProduct(s.ProductId, out var p))
                    stockValue += u * SupplierRules.UnitPrice(p, ctx.Catalog.Economy);
            }
            return new BalanceSample
            {
                Minute = minute,
                Coins = st.Wallet.Coins,
                // Patrimônio: dinheiro + o que foi investido em parcelas/móveis + estoque a preço de fornecedor.
                NetWorth = st.Wallet.Coins + st.Stats.CoinsInvested + stockValue,
                WarehouseUnits = warehouse, ShelfUnits = shelf, PendingUnits = pending,
                ExpiredUnits = st.Stats.UnitsExpired, Revenue = st.Stats.SalesRevenue, SupplierSpent = st.Stats.SupplierSpent,
                Invested = st.Stats.CoinsInvested, Served = st.Stats.CustomersServed, Angry = st.Stats.CustomersLeftAngry,
                LeftEmpty = st.Stats.CustomersLeftEmpty, Missing = st.Stats.ItemsMissing, RejectedByPrice = st.Stats.ItemsRejectedByPrice,
                Plots = st.Farm.Plots.Count, Shelves = st.Store.Shelves.Count,
            };
        }

        /// <summary>Sem saída: não tem dinheiro para a ação mais barata disponível e nada que gere dinheiro.</summary>
        public static bool IsStuck(GameContext ctx, Strategy strategy)
        {
            var st = ctx.State;
            long now = ctx.NowMs;
            foreach (var plot in st.Farm.Plots) if (FarmRules.GetPhase(plot, now) != PlotPhase.Empty) return false;
            if (st.Supplier.Orders.Count > 0 || st.Warehouse.Count > 0 || st.Store.Customers.Count > 0) return false;
            foreach (var s in st.Store.Shelves) if (StoreRules.Units(s, now) > 0) return false;

            long cheapest = long.MaxValue;
            if (strategy != Strategy.SupplierOnly)
                foreach (var c in ctx.Catalog.Crops)
                    if (FarmRules.IsCropUnlocked(c, st)) cheapest = Math.Min(cheapest, c.SeedCost);
            if (strategy != Strategy.FarmOnly)
                foreach (var p in ctx.Catalog.Products) cheapest = Math.Min(cheapest, SupplierRules.UnitPrice(p, ctx.Catalog.Economy));
            return st.Wallet.Coins < cheapest;
        }
    }
}
