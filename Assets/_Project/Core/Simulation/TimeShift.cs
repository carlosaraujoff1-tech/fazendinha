using System.Collections.Generic;
using Game.Core.State;

namespace Game.Core.Simulation
{
    /// <summary>Empurra para frente todos os instantes absolutos do estado (o mundo "dormiu" esse tempo).</summary>
    public static class TimeShift
    {
        public static void Apply(GameState s, long deltaMs)
        {
            if (deltaMs <= 0) return;
            s.LastTickMs += deltaMs;
            ShiftLots(s.Warehouse, deltaMs);
            foreach (var plot in s.Farm.Plots)
            {
                if (plot.IsEmpty) continue;
                plot.PlantedAtMs += deltaMs;
                plot.ReadyAtMs += deltaMs;
            }
            foreach (var order in s.Supplier.Orders) order.DeliverAtMs += deltaMs;
            foreach (var animal in s.Farm.Animals)
            {
                if (animal.IsHungry) continue;
                animal.FedAtMs += deltaMs;
                animal.ReadyAtMs += deltaMs;
            }

            var store = s.Store;
            foreach (var shelf in store.Shelves) ShiftLots(shelf.Lots, deltaMs);
            foreach (var c in store.Customers)
            {
                c.EnteredAtMs += deltaMs;
                c.NextActionAtMs += deltaMs;
                foreach (var line in c.Cart) ShiftLots(line.Lots, deltaMs);
            }
            if (store.LastStoreTickMs > 0) store.LastStoreTickMs += deltaMs;
            store.NextSpawnAtMs += deltaMs;
        }

        private static void ShiftLots(List<StockLot> lots, long deltaMs)
        {
            foreach (var lot in lots)
            {
                lot.ProducedAtMs += deltaMs;
                if (lot.ExpiresAtMs != StockLot.NeverExpires) lot.ExpiresAtMs += deltaMs;
            }
        }
    }
}
