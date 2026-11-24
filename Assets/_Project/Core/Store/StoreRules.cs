using System;
using Game.Core.Catalog;
using Game.Core.Economy;
using Game.Core.Inventory;
using Game.Core.State;

namespace Game.Core.Store
{
    public enum PriceTag { Cheap, Fair, Expensive }

    /// <summary>Regras puras da loja e dos clientes (fórmulas do design).</summary>
    public static class StoreRules
    {
        /// <summary>chance(p) = clamp(1 − (p/P − (1 + a)) / b, 0, 1). Com b = 0 vira degrau em P×(1+a).</summary>
        public static double BuyChance(long price, long referencePrice, double a, double b)
        {
            if (referencePrice <= 0) throw new ArgumentOutOfRangeException(nameof(referencePrice));
            double ratio = (double)price / referencePrice;
            if (b <= 0) return ratio <= 1 + a + 1e-9 ? 1 : 0;
            double c = 1 - (ratio - (1 + a)) / b;
            return c < 0 ? 0 : c > 1 ? 1 : c;
        }

        /// <summary>Abaixo de P a quantidade cresce em <paramref name="factor"/> × desconto relativo (0,5: 20% → +10%).</summary>
        public static double QuantityMultiplier(long price, long referencePrice, double factor)
        {
            if (referencePrice <= 0) throw new ArgumentOutOfRangeException(nameof(referencePrice));
            double discount = 1 - (double)price / referencePrice;
            return 1 + factor * Math.Max(0, discount);
        }

        public static PriceTag Tag(long price, long referencePrice)
        {
            double ratio = (double)price / referencePrice;
            if (ratio < 0.95) return PriceTag.Cheap;
            return ratio <= 1.10 + 1e-9 ? PriceTag.Fair : PriceTag.Expensive;
        }

        public static long PriceOf(GameState state, ProductDef product) =>
            state.Store.Prices.TryGetValue(product.Id, out long p) ? p : product.ReferencePrice;

        public static long MaxPrice(ProductDef product, StoreConfig config) =>
            (long)Math.Floor(product.ReferencePrice * config.MaxPriceFactor);

        public static int Units(ShelfState shelf, long nowMs) =>
            shelf.ProductId == null ? 0 : new LotInventory(shelf.Lots).Count(shelf.ProductId, nowMs);

        public static int FreeSpace(ShelfState shelf, FurnitureDef furniture, long nowMs) =>
            Math.Max(0, furniture.Capacity - Units(shelf, nowMs));

        /// <summary>O próximo item a sair (FEFO) veio da fazenda? Dá o selo "Da nossa fazenda".</summary>
        public static bool FrontIsFromFarm(ShelfState shelf, long nowMs)
        {
            StockLot front = null;
            foreach (var lot in shelf.Lots)
            {
                if (lot.IsExpired(nowMs) || lot.ProductId != shelf.ProductId) continue;
                if (front == null || lot.ExpiresAtMs < front.ExpiresAtMs ||
                    (lot.ExpiresAtMs == front.ExpiresAtMs && lot.ProducedAtMs < front.ProducedAtMs)) front = lot;
            }
            return front != null && front.Source == LotSource.Farm;
        }

        public static long FurnitureCost(GameState state, FurnitureDef furniture, EconomyConfig economy)
        {
            state.Store.FurnitureBought.TryGetValue(furniture.Id, out int bought);
            return CostCurve.RepeatableCost(furniture.BaseCost, bought, economy.RepeatableCostGrowth);
        }

        /// <summary>Posição na fila (0 = sendo atendido) ou −1 se não está na fila.</summary>
        public static int QueuePosition(StoreState store, CustomerState customer)
        {
            if (customer.Phase != CustomerPhase.Queued) return -1;
            int ahead = 0;
            foreach (var c in store.Customers)
                if (c.Phase == CustomerPhase.Queued && c.QueueTicket < customer.QueueTicket) ahead++;
            return ahead;
        }

        /// <summary>Cliente na frente da fila do caixa (ou null).</summary>
        public static CustomerState FrontOfQueue(StoreState store)
        {
            CustomerState front = null;
            foreach (var c in store.Customers)
                if (c.Phase == CustomerPhase.Queued && (front == null || c.QueueTicket < front.QueueTicket)) front = c;
            return front;
        }
    }
}
