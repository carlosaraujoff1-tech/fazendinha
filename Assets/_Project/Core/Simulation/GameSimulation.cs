using Game.Core.Events;
using Game.Core.Store;

namespace Game.Core.Simulation
{
    /// <summary>
    /// Avança o mundo até o instante atual do relógio. Idempotente: chamar duas vezes no mesmo
    /// instante não muda nada. Relógio voltando no tempo é ignorado (proteção básica; reforço na Etapa 7).
    /// Sistemas novos (crescimento, clientes, salários) entram aqui nas próximas etapas.
    /// </summary>
    public static class GameSimulation
    {
        public static void Tick(GameContext ctx)
        {
            long now = ctx.NowMs;
            if (now < ctx.State.LastTickMs) return;

            var expired = ctx.Warehouse.RemoveExpired(now);
            if (expired.Count > 0)
            {
                long units = 0;
                foreach (var lot in expired) units += lot.Quantity;
                ctx.State.Stats.UnitsExpired += units;
                ctx.Events.Publish(new LotsExpired(expired));
            }

            StoreSimulation.Advance(ctx);

            ctx.State.LastTickMs = now;
        }
    }
}
