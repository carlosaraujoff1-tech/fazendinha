using Game.Core.Events;
using Game.Core.Farm;
using Game.Core.Store;
using Game.Core.Supplier;

namespace Game.Core.Simulation
{
    /// <summary>Resumo para a tela "Bem-vindo de volta".</summary>
    public readonly struct OfflineReturned : IGameEvent
    {
        public OfflineReturned(long awayMs, long creditedMs, int plotsReady, int deliveries, long unitsExpired, int animalsReady = 0)
        {
            AwayMs = awayMs; CreditedMs = creditedMs; PlotsReady = plotsReady; Deliveries = deliveries; UnitsExpired = unitsExpired;
            AnimalsReady = animalsReady;
        }
        public int AnimalsReady { get; }
        public long AwayMs { get; }
        /// <summary>Quanto do tempo fora contou (no máximo o limite offline, 4 h).</summary>
        public long CreditedMs { get; }
        public int PlotsReady { get; }
        public int Deliveries { get; }
        public long UnitsExpired { get; }
    }

    /// <summary>
    /// Avança o mundo até o instante atual do relógio. Idempotente no mesmo instante. Relógio voltando
    /// no tempo é ignorado. Tempo fora acima do limite offline (4 h) não conta: o mundo "dorme"
    /// (todos os relógios do estado são empurrados para frente), então a fazenda produz no máximo 4 h
    /// e o estoque não vence enquanto o jogador dorme.
    /// </summary>
    public static class GameSimulation
    {
        /// <summary>Ausência a partir da qual se mostra o "Bem-vindo de volta".</summary>
        public const long WelcomeBackThresholdMs = 60_000;

        public static void Tick(GameContext ctx)
        {
            long now = ctx.NowMs;
            var state = ctx.State;
            if (now < state.LastTickMs) return;

            long gap = now - state.LastTickMs;
            long cap = ctx.Catalog.Economy.OfflineFarmCapSeconds * 1000L;
            long credited = gap;
            if (gap > cap)
            {
                long excess = gap - cap;
                TimeShift.Apply(state, excess);
                state.Stats.OfflineMsIgnored += excess;
                credited = cap;
            }

            bool welcome = gap >= WelcomeBackThresholdMs;
            int ordersBefore = state.Supplier.Orders.Count;
            long expiredBefore = state.Stats.UnitsExpired;

            SupplierRules.Deliver(ctx);

            var expired = ctx.Warehouse.RemoveExpired(now);
            if (expired.Count > 0)
            {
                long units = 0;
                foreach (var lot in expired) units += lot.Quantity;
                state.Stats.UnitsExpired += units;
                ctx.Events.Publish(new LotsExpired(expired));
            }

            StoreSimulation.Advance(ctx);
            state.LastTickMs = now;

            if (welcome)
            {
                int ready = 0;
                foreach (var plot in state.Farm.Plots) if (FarmRules.GetPhase(plot, now) == PlotPhase.Ready) ready++;
                int animalsReady = 0;
                foreach (var a in state.Farm.Animals) if (Animals.AnimalRules.GetPhase(a, now) == Animals.AnimalPhase.Ready) animalsReady++;
                ctx.Events.Publish(new OfflineReturned(gap, credited, ready, ordersBefore - state.Supplier.Orders.Count,
                    state.Stats.UnitsExpired - expiredBefore, animalsReady));
            }
        }
    }
}
