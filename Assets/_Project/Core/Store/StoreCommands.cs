using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Simulation;

namespace Game.Core.Store
{
    /// <summary>Leva unidades do armazém para uma prateleira (FEFO, mantendo origem e validade).</summary>
    public sealed class RestockCommand : ICommand
    {
        public RestockCommand(int shelfIndex, string productId, int units) { ShelfIndex = shelfIndex; ProductId = productId; Units = units; }
        public int ShelfIndex { get; }
        public string ProductId { get; }
        public int Units { get; }
    }

    public sealed class SetPriceCommand : ICommand
    {
        public SetPriceCommand(string productId, long price) { ProductId = productId; Price = price; }
        public string ProductId { get; }
        public long Price { get; }
    }

    /// <summary>Caixa manual: passa o próximo item do cliente na frente da fila.</summary>
    public sealed class ScanItemCommand : ICommand { }

    public sealed class BuyFurnitureCommand : ICommand
    {
        public BuyFurnitureCommand(string furnitureId) { FurnitureId = furnitureId; }
        public string FurnitureId { get; }
    }

    public readonly struct PriceChanged : IGameEvent
    {
        public PriceChanged(string productId, long price) { ProductId = productId; Price = price; }
        public string ProductId { get; }
        public long Price { get; }
    }

    public sealed class RestockHandler : ICommandHandler<RestockCommand>
    {
        public CommandResult Validate(GameContext ctx, RestockCommand c)
        {
            var shelves = ctx.State.Store.Shelves;
            if (c.ShelfIndex < 0 || c.ShelfIndex >= shelves.Count) return CommandResult.Fail(CommandErrors.UnknownShelf);
            var shelf = shelves[c.ShelfIndex];
            if (!ctx.Catalog.TryGetFurniture(shelf.FurnitureId, out var furniture)) return CommandResult.Fail(CommandErrors.UnknownFurniture);
            if (!ctx.Catalog.TryGetProduct(c.ProductId, out var product)) return CommandResult.Fail(CommandErrors.UnknownProduct);
            if (c.Units <= 0) return CommandResult.Fail(CommandErrors.InvalidArgument);
            if (product.CategoryId != furniture.CategoryId) return CommandResult.Fail(CommandErrors.WrongCategory);

            long now = ctx.NowMs;
            int onShelf = StoreRules.Units(shelf, now);
            if (onShelf > 0 && shelf.ProductId != product.Id) return CommandResult.Fail(CommandErrors.ShelfHasOtherProduct);
            if (c.Units > furniture.Capacity - onShelf) return CommandResult.Fail(CommandErrors.ShelfFull);
            if (ctx.Warehouse.Count(product.Id, now) < c.Units) return CommandResult.Fail(CommandErrors.InsufficientStock);
            return CommandResult.Ok();
        }

        public void Apply(GameContext ctx, RestockCommand c)
        {
            var shelf = ctx.State.Store.Shelves[c.ShelfIndex];
            if (shelf.ProductId != c.ProductId)
            {
                shelf.ProductId = c.ProductId;
                shelf.Lots.Clear(); // só sobravam lotes vencidos/zerados de outro produto
            }
            ctx.Warehouse.TryRemove(c.ProductId, c.Units, ctx.NowMs, out var taken);
            var shelfInv = new LotInventory(shelf.Lots);
            foreach (var lot in taken) shelfInv.Add(lot);
            ctx.Events.Publish(new StockRemoved(c.ProductId, c.Units));
            ctx.Events.Publish(new ShelfChanged(c.ShelfIndex));
        }
    }

    public sealed class SetPriceHandler : ICommandHandler<SetPriceCommand>
    {
        public CommandResult Validate(GameContext ctx, SetPriceCommand c)
        {
            if (!ctx.Catalog.TryGetProduct(c.ProductId, out var product)) return CommandResult.Fail(CommandErrors.UnknownProduct);
            if (c.Price < 1 || c.Price > StoreRules.MaxPrice(product, ctx.Catalog.Store)) return CommandResult.Fail(CommandErrors.InvalidArgument);
            return CommandResult.Ok();
        }

        public void Apply(GameContext ctx, SetPriceCommand c)
        {
            ctx.State.Store.Prices[c.ProductId] = c.Price;
            ctx.Events.Publish(new PriceChanged(c.ProductId, c.Price));
        }
    }

    public sealed class ScanItemHandler : ICommandHandler<ScanItemCommand>
    {
        public CommandResult Validate(GameContext ctx, ScanItemCommand c) =>
            StoreRules.FrontOfQueue(ctx.State.Store) != null ? CommandResult.Ok() : CommandResult.Fail(CommandErrors.NoCustomer);

        public void Apply(GameContext ctx, ScanItemCommand c)
        {
            var customer = StoreRules.FrontOfQueue(ctx.State.Store);
            var line = customer.Cart[customer.ScannedLines];
            customer.ScannedLines++;
            ctx.Events.Publish(new ItemScanned(customer.Id, line.ProductId, line.Units, line.Total));
            if (customer.ScannedLines >= customer.Cart.Count) StoreSimulation.Leave(ctx, customer, LeaveReason.Paid);
        }
    }

    public sealed class BuyFurnitureHandler : ICommandHandler<BuyFurnitureCommand>
    {
        public CommandResult Validate(GameContext ctx, BuyFurnitureCommand c)
        {
            if (!ctx.Catalog.TryGetFurniture(c.FurnitureId, out var furniture)) return CommandResult.Fail(CommandErrors.UnknownFurniture);
            if (ctx.State.Store.Shelves.Count >= ctx.Catalog.Store.MaxFurniture) return CommandResult.Fail(CommandErrors.MaxReached);
            long cost = StoreRules.FurnitureCost(ctx.State, furniture, ctx.Catalog.Economy);
            return cost == 0 || ctx.Wallet.CanAffordCoins(cost) ? CommandResult.Ok() : CommandResult.Fail(CommandErrors.InsufficientCoins);
        }

        public void Apply(GameContext ctx, BuyFurnitureCommand c)
        {
            var furniture = ctx.Catalog.GetFurniture(c.FurnitureId);
            long cost = StoreRules.FurnitureCost(ctx.State, furniture, ctx.Catalog.Economy);
            if (cost > 0) ctx.Wallet.TrySpendCoins(cost, "movel:" + furniture.Id);
            var bought = ctx.State.Store.FurnitureBought;
            bought[furniture.Id] = bought.TryGetValue(furniture.Id, out int n) ? n + 1 : 1;
            ctx.State.Store.Shelves.Add(new ShelfState { FurnitureId = furniture.Id });
            ctx.Events.Publish(new ShelfChanged(ctx.State.Store.Shelves.Count - 1));
        }
    }
}
