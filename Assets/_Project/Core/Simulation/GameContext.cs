using System;
using Game.Core.Catalog;
using Game.Core.Economy;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.State;
using Game.Core.Store;
using Game.Core.Time;

namespace Game.Core.Simulation
{
    /// <summary>Tudo que as regras precisam: estado, catálogo, relógio e eventos, mais as fachadas de regra.</summary>
    public sealed class GameContext
    {
        public GameContext(GameState state, GameCatalog catalog, IClock clock, IEventBus events)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Events = events ?? NullEventBus.Instance;
            Wallet = new Wallet(state.Wallet, state.Stats, Events);
            Warehouse = new LotInventory(state.Warehouse);
        }

        public GameState State { get; }
        public GameCatalog Catalog { get; }
        public IClock Clock { get; }
        public IEventBus Events { get; }
        public Wallet Wallet { get; }
        public LotInventory Warehouse { get; }

        public long NowMs => Clock.UtcNowMs;
    }

    public static class NewGame
    {
        /// <summary>Estado inicial de um jogador novo, a partir da economia do catálogo.</summary>
        public static GameState Create(GameCatalog catalog, IClock clock)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            long now = clock.UtcNowMs;
            var state = new GameState
            {
                CreatedAtMs = now,
                LastTickMs = now,
                Wallet = new WalletState
                {
                    Coins = catalog.Economy.StartingCoins,
                    Gems = catalog.Economy.StartingGems,
                },
            };
            for (int i = 0; i < catalog.Economy.InitialPlots; i++) state.Farm.Plots.Add(new PlotState());
            foreach (var furnitureId in catalog.Store.InitialFurniture ?? new string[0])
                state.Store.Shelves.Add(new ShelfState { FurnitureId = furnitureId });
            state.Store.Rng.Seed = (ulong)now;
            state.Store.LastStoreTickMs = now;
            state.Store.NextSpawnAtMs = now + (long)(catalog.Store.SpawnIntervalSeconds * 1000);
            return state;
        }
    }
}
