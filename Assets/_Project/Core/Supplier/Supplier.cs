using System;
using System.Collections.Generic;
using Game.Core.Catalog;
using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Simulation;
using Game.Core.State;

namespace Game.Core.Supplier
{
    public sealed class SupplierState
    {
        public List<SupplierOrder> Orders { get; set; } = new List<SupplierOrder>();
    }

    public sealed class SupplierOrder
    {
        public string ProductId { get; set; }
        public int Units { get; set; }
        public long DeliverAtMs { get; set; }
    }

    /// <summary>Compra do fornecedor: qualquer produto, sem limite de estoque, a 0,8 × P, entrega em ~2 min.</summary>
    public sealed class BuyFromSupplierCommand : ICommand
    {
        public BuyFromSupplierCommand(string productId, int units) { ProductId = productId; Units = units; }
        public string ProductId { get; }
        public int Units { get; }
    }

    public readonly struct SupplierOrderPlaced : IGameEvent
    {
        public SupplierOrderPlaced(string productId, int units, long cost, long deliverAtMs)
        {
            ProductId = productId; Units = units; Cost = cost; DeliverAtMs = deliverAtMs;
        }
        public string ProductId { get; }
        public int Units { get; }
        public long Cost { get; }
        public long DeliverAtMs { get; }
    }

    public readonly struct SupplierDelivered : IGameEvent
    {
        public SupplierDelivered(string productId, int units) { ProductId = productId; Units = units; }
        public string ProductId { get; }
        public int Units { get; }
    }

    public static class SupplierRules
    {
        public static long UnitPrice(ProductDef product, EconomyConfig economy) =>
            Math.Max(1, (long)Math.Round(product.ReferencePrice * economy.SupplierPriceFactor, MidpointRounding.AwayFromZero));

        public static int PendingUnits(GameState state, string productId)
        {
            int n = 0;
            foreach (var o in state.Supplier.Orders) if (o.ProductId == productId) n += o.Units;
            return n;
        }

        /// <summary>Entrega os pedidos vencidos (também offline: o caminhão chega mesmo com o app fechado).</summary>
        public static void Deliver(GameContext ctx)
        {
            var orders = ctx.State.Supplier.Orders;
            if (orders.Count == 0) return;
            long now = ctx.NowMs;
            for (int i = 0; i < orders.Count; i++)
            {
                var o = orders[i];
                if (o.DeliverAtMs > now) continue;
                if (ctx.Catalog.TryGetProduct(o.ProductId, out var product))
                {
                    ctx.Warehouse.Add(LotInventory.CreateLot(product, o.Units, o.DeliverAtMs, LotSource.Supplier));
                    ctx.Events.Publish(new StockAdded(product.Id, o.Units, LotSource.Supplier));
                    ctx.Events.Publish(new SupplierDelivered(product.Id, o.Units));
                }
                orders.RemoveAt(i);
                i--;
            }
        }
    }

    public sealed class BuyFromSupplierHandler : ICommandHandler<BuyFromSupplierCommand>
    {
        public CommandResult Validate(GameContext ctx, BuyFromSupplierCommand c)
        {
            if (!ctx.Catalog.TryGetProduct(c.ProductId, out var product)) return CommandResult.Fail(CommandErrors.UnknownProduct);
            if (c.Units <= 0 || c.Units > ctx.Catalog.Economy.MaxSupplierOrderUnits) return CommandResult.Fail(CommandErrors.InvalidArgument);
            long cost = SupplierRules.UnitPrice(product, ctx.Catalog.Economy) * c.Units;
            return ctx.Wallet.CanAffordCoins(cost) ? CommandResult.Ok() : CommandResult.Fail(CommandErrors.InsufficientCoins);
        }

        public void Apply(GameContext ctx, BuyFromSupplierCommand c)
        {
            var product = ctx.Catalog.GetProduct(c.ProductId);
            long cost = SupplierRules.UnitPrice(product, ctx.Catalog.Economy) * c.Units;
            ctx.Wallet.TrySpendCoins(cost, "fornecedor:" + product.Id);
            ctx.State.Stats.SupplierSpent += cost;
            long deliverAt = ctx.NowMs + ctx.Catalog.Economy.SupplierDeliverySeconds * 1000L;
            ctx.State.Supplier.Orders.Add(new SupplierOrder { ProductId = product.Id, Units = c.Units, DeliverAtMs = deliverAt });
            ctx.Events.Publish(new SupplierOrderPlaced(product.Id, c.Units, cost, deliverAt));
        }
    }
}
