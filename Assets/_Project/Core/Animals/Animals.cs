using System;
using System.Collections.Generic;
using Game.Core.Catalog;
using Game.Core.Commands;
using Game.Core.Economy;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Simulation;
using Game.Core.State;

namespace Game.Core.Animals
{
    /// <summary>
    /// Tipo de animal: come <see cref="FeedPerCycle"/> unidades de qualquer produto de <see cref="FeedProductIds"/>
    /// (na ordem de preferência) e, depois de <see cref="CycleSeconds"/>, entrega <see cref="Yield"/> de <see cref="ProductId"/>.
    /// Sem comida, só para de produzir (não morre, não existe game over).
    /// </summary>
    public sealed class AnimalDef
    {
        public AnimalDef(string id, string name, string productId, IReadOnlyList<string> feedProductIds, int feedPerCycle,
            int cycleSeconds, int yield, long baseCost, int maxCount, int unlockLevel = 1)
        {
            Id = id; Name = string.IsNullOrEmpty(name) ? id : name; ProductId = productId;
            FeedProductIds = feedProductIds ?? new string[0]; FeedPerCycle = feedPerCycle; CycleSeconds = cycleSeconds;
            Yield = yield; BaseCost = baseCost; MaxCount = maxCount; UnlockLevel = unlockLevel;
        }

        public string Id { get; }
        public string Name { get; }
        public string ProductId { get; }
        public IReadOnlyList<string> FeedProductIds { get; }
        public int FeedPerCycle { get; }
        public int CycleSeconds { get; }
        public int Yield { get; }
        public long BaseCost { get; }
        public int MaxCount { get; }
        public int UnlockLevel { get; }
    }

    public enum AnimalPhase { Hungry, Producing, Ready }

    public sealed class BuyAnimalCommand : ICommand
    {
        public BuyAnimalCommand(string animalTypeId) { AnimalTypeId = animalTypeId; }
        public string AnimalTypeId { get; }
    }

    public sealed class FeedAnimalCommand : ICommand
    {
        public FeedAnimalCommand(int animalIndex) { AnimalIndex = animalIndex; }
        public int AnimalIndex { get; }
    }

    public sealed class CollectAnimalCommand : ICommand
    {
        public CollectAnimalCommand(int animalIndex) { AnimalIndex = animalIndex; }
        public int AnimalIndex { get; }
    }

    public readonly struct AnimalChanged : IGameEvent
    {
        public AnimalChanged(int index) { AnimalIndex = index; }
        public int AnimalIndex { get; }
    }

    public readonly struct AnimalBought : IGameEvent
    {
        public AnimalBought(int index, string typeId) { AnimalIndex = index; TypeId = typeId; }
        public int AnimalIndex { get; }
        public string TypeId { get; }
    }

    public readonly struct AnimalProductCollected : IGameEvent
    {
        public AnimalProductCollected(int index, string productId, int quantity) { AnimalIndex = index; ProductId = productId; Quantity = quantity; }
        public int AnimalIndex { get; }
        public string ProductId { get; }
        public int Quantity { get; }
    }

    public static class AnimalRules
    {
        public static AnimalPhase GetPhase(AnimalState a, long nowMs)
        {
            if (a == null || a.IsHungry) return AnimalPhase.Hungry;
            return nowMs >= a.ReadyAtMs ? AnimalPhase.Ready : AnimalPhase.Producing;
        }

        public static long RemainingMs(AnimalState a, long nowMs) =>
            a == null || a.IsHungry ? 0 : Math.Max(0, a.ReadyAtMs - nowMs);

        public static float Progress(AnimalState a, long nowMs)
        {
            if (a == null || a.IsHungry) return 0f;
            long total = a.ReadyAtMs - a.FedAtMs;
            if (total <= 0 || nowMs >= a.ReadyAtMs) return 1f;
            return Math.Max(0f, (float)(nowMs - a.FedAtMs) / total);
        }

        public static int CountOf(GameState s, string typeId)
        {
            int n = 0;
            foreach (var a in s.Farm.Animals) if (a.TypeId == typeId) n++;
            return n;
        }

        /// <summary>Preço do próximo animal deste tipo (curva 1,15^n sobre os que já tem).</summary>
        public static long NextCost(GameState s, AnimalDef def, EconomyConfig economy) =>
            CostCurve.RepeatableCost(def.BaseCost, CountOf(s, def.Id), economy.RepeatableCostGrowth);

        /// <summary>Quanta comida aceita há no armazém para este animal.</summary>
        public static int AvailableFeed(GameContext ctx, AnimalDef def)
        {
            int n = 0;
            foreach (var p in def.FeedProductIds) n += ctx.Warehouse.Count(p, ctx.NowMs);
            return n;
        }

        internal static bool TryGet(GameContext ctx, int index, out AnimalState animal, out AnimalDef def)
        {
            var list = ctx.State.Farm.Animals;
            animal = index >= 0 && index < list.Count ? list[index] : null;
            def = null;
            return animal != null && ctx.Catalog.TryGetAnimal(animal.TypeId, out def);
        }
    }

    public sealed class BuyAnimalHandler : ICommandHandler<BuyAnimalCommand>
    {
        public CommandResult Validate(GameContext ctx, BuyAnimalCommand c)
        {
            if (!ctx.Catalog.TryGetAnimal(c.AnimalTypeId, out var def)) return CommandResult.Fail(CommandErrors.UnknownAnimal);
            if (ctx.State.Progress.Level < def.UnlockLevel) return CommandResult.Fail(CommandErrors.Locked);
            if (AnimalRules.CountOf(ctx.State, def.Id) >= def.MaxCount) return CommandResult.Fail(CommandErrors.MaxReached);
            long cost = AnimalRules.NextCost(ctx.State, def, ctx.Catalog.Economy);
            return cost == 0 || ctx.Wallet.CanAffordCoins(cost) ? CommandResult.Ok() : CommandResult.Fail(CommandErrors.InsufficientCoins);
        }

        public void Apply(GameContext ctx, BuyAnimalCommand c)
        {
            var def = ctx.Catalog.GetAnimal(c.AnimalTypeId);
            long cost = AnimalRules.NextCost(ctx.State, def, ctx.Catalog.Economy);
            if (cost > 0) ctx.Wallet.TrySpendCoins(cost, "animal:" + def.Id);
            ctx.State.Stats.CoinsInvested += cost;
            ctx.State.Farm.Animals.Add(new AnimalState { TypeId = def.Id });
            int index = ctx.State.Farm.Animals.Count - 1;
            ctx.Events.Publish(new AnimalBought(index, def.Id));
            ctx.Events.Publish(new AnimalChanged(index));
        }
    }

    public sealed class FeedAnimalHandler : ICommandHandler<FeedAnimalCommand>
    {
        public CommandResult Validate(GameContext ctx, FeedAnimalCommand c)
        {
            if (!AnimalRules.TryGet(ctx, c.AnimalIndex, out var animal, out var def)) return CommandResult.Fail(CommandErrors.UnknownAnimal);
            if (!animal.IsHungry) return CommandResult.Fail(CommandErrors.AlreadyFed);
            return AnimalRules.AvailableFeed(ctx, def) >= def.FeedPerCycle ? CommandResult.Ok() : CommandResult.Fail(CommandErrors.NoFeed);
        }

        public void Apply(GameContext ctx, FeedAnimalCommand c)
        {
            AnimalRules.TryGet(ctx, c.AnimalIndex, out var animal, out var def);
            int remaining = def.FeedPerCycle;
            foreach (var productId in def.FeedProductIds) // ordem de preferência dos dados
            {
                if (remaining == 0) break;
                int take = Math.Min(remaining, ctx.Warehouse.Count(productId, ctx.NowMs));
                if (take <= 0) continue;
                ctx.Warehouse.TryRemove(productId, take, ctx.NowMs, out _);
                ctx.Events.Publish(new StockRemoved(productId, take));
                remaining -= take;
            }
            animal.FedAtMs = ctx.NowMs;
            animal.ReadyAtMs = ctx.NowMs + def.CycleSeconds * 1000L;
            ctx.Events.Publish(new AnimalChanged(c.AnimalIndex));
        }
    }

    public sealed class CollectAnimalHandler : ICommandHandler<CollectAnimalCommand>
    {
        public CommandResult Validate(GameContext ctx, CollectAnimalCommand c)
        {
            if (!AnimalRules.TryGet(ctx, c.AnimalIndex, out var animal, out var def)) return CommandResult.Fail(CommandErrors.UnknownAnimal);
            switch (AnimalRules.GetPhase(animal, ctx.NowMs))
            {
                case AnimalPhase.Hungry: return CommandResult.Fail(CommandErrors.NoFeed);
                case AnimalPhase.Producing: return CommandResult.Fail(CommandErrors.NotReady);
            }
            return ctx.Catalog.TryGetProduct(def.ProductId, out _) ? CommandResult.Ok() : CommandResult.Fail(CommandErrors.UnknownProduct);
        }

        public void Apply(GameContext ctx, CollectAnimalCommand c)
        {
            AnimalRules.TryGet(ctx, c.AnimalIndex, out var animal, out var def);
            var product = ctx.Catalog.GetProduct(def.ProductId);
            ctx.Warehouse.Add(LotInventory.CreateLot(product, def.Yield, ctx.NowMs, LotSource.Farm));
            animal.ReadyAtMs = 0;
            animal.FedAtMs = 0;
            ctx.Events.Publish(new StockAdded(product.Id, def.Yield, LotSource.Farm));
            ctx.Events.Publish(new AnimalProductCollected(c.AnimalIndex, product.Id, def.Yield));
            ctx.Events.Publish(new AnimalChanged(c.AnimalIndex));
        }
    }
}
