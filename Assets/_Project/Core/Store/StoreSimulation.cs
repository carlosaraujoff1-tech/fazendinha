using System;
using System.Collections.Generic;
using Game.Core.Catalog;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Randomness;
using Game.Core.Simulation;
using Game.Core.Staff;
using Game.Core.State;

namespace Game.Core.Store
{
    /// <summary>
    /// Simulação da loja: chegada de clientes, compras pela fórmula, fila e paciência.
    /// Avança em passos de no máximo 1 s. A loja NÃO vende offline: uma lacuna maior que
    /// <see cref="StoreConfig.MaxCatchUpSeconds"/> fecha a loja (clientes saem, carrinhos voltam ao armazém).
    /// </summary>
    public static class StoreSimulation
    {
        private const long StepMs = 1000;

        public static void Advance(GameContext ctx)
        {
            var store = ctx.State.Store;
            var config = ctx.Catalog.Store;
            long now = ctx.NowMs;

            ExpireShelves(ctx, now);

            if (store.LastStoreTickMs <= 0)
            {
                Reopen(store, config, now);
                return;
            }

            long gap = now - store.LastStoreTickMs;
            if (gap <= 0) return;
            if (gap > config.MaxCatchUpSeconds * 1000)
            {
                CloseStore(ctx);
                Reopen(store, config, now);
                return;
            }

            long t = store.LastStoreTickMs;
            while (t < now)
            {
                long next = Math.Min(now, t + StepMs);
                Step(ctx, next, (next - t) / 1000.0);
                t = next;
            }
            store.LastStoreTickMs = now;
        }

        private static void Reopen(StoreState store, StoreConfig config, long now)
        {
            store.LastStoreTickMs = now;
            store.NextSpawnAtMs = now + (long)(config.SpawnIntervalSeconds * 1000);
        }

        private static void ExpireShelves(GameContext ctx, long now)
        {
            var shelves = ctx.State.Store.Shelves;
            for (int i = 0; i < shelves.Count; i++)
            {
                var expired = new LotInventory(shelves[i].Lots).RemoveExpired(now);
                if (expired.Count == 0) continue;
                foreach (var lot in expired) ctx.State.Stats.UnitsExpired += lot.Quantity;
                ctx.Events.Publish(new LotsExpired(expired));
                ctx.Events.Publish(new ShelfChanged(i));
            }
        }

        private static void Step(GameContext ctx, long t, double dtSeconds)
        {
            var store = ctx.State.Store;
            var config = ctx.Catalog.Store;
            var rng = new GameRandom(store.Rng);

            // 1) Chegadas
            long interval = Math.Max(1, (long)(config.SpawnIntervalSeconds * 1000));
            while (t >= store.NextSpawnAtMs)
            {
                if (store.Customers.Count < config.MaxCustomersInStore) TrySpawn(ctx, rng, store.NextSpawnAtMs);
                store.NextSpawnAtMs += interval;
            }

            // 2) Compras (cópia: a lista muda quando alguém sai)
            foreach (var customer in store.Customers.ToArray())
            {
                if (customer.Phase != CustomerPhase.Shopping || t < customer.NextActionAtMs) continue;
                if (customer.NextItem < customer.List.Count)
                {
                    ShopItem(ctx, rng, customer, customer.List[customer.NextItem], t);
                    customer.NextItem++;
                    customer.NextActionAtMs = t + (long)(config.ShoppingSecondsPerItem * 1000);
                }
                else if (customer.Cart.Count == 0)
                {
                    Leave(ctx, customer, LeaveReason.Empty);
                }
                else
                {
                    customer.Phase = CustomerPhase.Queued;
                    customer.QueueTicket = store.NextQueueTicket++;
                    ctx.Events.Publish(new CustomerQueued(customer.Id));
                }
            }

            // 3) Funcionários (só trabalham e recebem com o jogo aberto)
            long dtMs = (long)Math.Round(dtSeconds * 1000);
            StaffRules.AccrueSalaries(ctx, dtMs);
            foreach (var e in ctx.State.Staff.Employees)
            {
                if (!e.Active || !ctx.Catalog.TryGetStaff(e.TypeId, out var def) || def.Role != StaffRole.Cashier) continue;
                if (StoreRules.FrontOfQueue(store) == null) { e.WorkProgressMs = 0; continue; }
                e.WorkProgressMs += dtMs;
                long per = Math.Max(1, (long)(def.SecondsPerAction * 1000));
                while (e.WorkProgressMs >= per && StoreRules.FrontOfQueue(store) != null)
                {
                    e.WorkProgressMs -= per;
                    ScanFront(ctx);
                }
            }

            // 4) Fila: paciência cai mais rápido quanto mais gente à frente
            foreach (var customer in store.Customers.ToArray())
            {
                int position = StoreRules.QueuePosition(store, customer);
                if (position < 0) continue;
                customer.PatienceSeconds -= dtSeconds * (1 + config.QueueDrainPerCustomerAhead * position);
                if (customer.PatienceSeconds <= 0) Leave(ctx, customer, LeaveReason.OutOfPatience);
            }
        }

        private static void TrySpawn(GameContext ctx, GameRandom rng, long t)
        {
            var store = ctx.State.Store;
            var config = ctx.Catalog.Store;
            var type = PickType(ctx.Catalog, rng);
            if (type == null) return;

            // Clientes querem produtos das categorias que a loja tem móvel (estocado ou não).
            var categories = new HashSet<string>();
            foreach (var shelf in store.Shelves)
                if (ctx.Catalog.TryGetFurniture(shelf.FurnitureId, out var f)) categories.Add(f.CategoryId);
            var candidates = new List<ProductDef>();
            foreach (var p in ctx.Catalog.Products)
                if (categories.Contains(p.CategoryId)) candidates.Add(p);
            if (candidates.Count == 0) return;

            int items = Math.Min(candidates.Count, rng.Range(config.MinItemsPerList, config.MaxItemsPerList + 1));
            for (int i = 0; i < items; i++) // embaralha só o começo (Fisher-Yates parcial)
            {
                int j = rng.Range(i, candidates.Count);
                var tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp;
            }

            var customer = new CustomerState
            {
                Id = store.NextCustomerId++,
                TypeId = type.Id,
                Phase = CustomerPhase.Shopping,
                PatienceSeconds = type.PatienceSeconds,
                EnteredAtMs = t,
                NextActionAtMs = t + (long)(config.ShoppingSecondsPerItem * 1000),
            };
            for (int i = 0; i < items; i++)
                customer.List.Add(new ShoppingItem
                {
                    ProductId = candidates[i].Id,
                    Units = rng.Range(config.MinUnitsPerItem, config.MaxUnitsPerItem + 1),
                });

            store.Customers.Add(customer);
            ctx.Events.Publish(new CustomerEntered(customer.Id, type.Id));
        }

        private static CustomerTypeDef PickType(GameCatalog catalog, GameRandom rng)
        {
            double total = 0;
            foreach (var c in catalog.CustomerTypes) total += Math.Max(0, c.Weight);
            if (total <= 0) return null;
            double roll = rng.NextDouble() * total;
            CustomerTypeDef last = null;
            foreach (var c in catalog.CustomerTypes)
            {
                if (c.Weight <= 0) continue;
                last = c;
                roll -= c.Weight;
                if (roll < 0) return c;
            }
            return last;
        }

        private static void ShopItem(GameContext ctx, GameRandom rng, CustomerState customer, ShoppingItem item, long t)
        {
            var catalog = ctx.Catalog;
            var store = ctx.State.Store;
            var config = catalog.Store;
            if (!catalog.TryGetProduct(item.ProductId, out var wanted)) return;

            int shelfIndex = FindShelf(store, s => s.ProductId == wanted.Id, t);
            bool substitute = false;
            if (shelfIndex < 0)
            {
                // Substituto: outro produto da mesma categoria que esteja na loja.
                shelfIndex = FindShelf(store, s => s.ProductId != null && s.ProductId != wanted.Id &&
                    catalog.TryGetProduct(s.ProductId, out var p) && p.CategoryId == wanted.CategoryId, t);
                substitute = shelfIndex >= 0;
                if (substitute && !rng.Chance(config.SubstituteAcceptance)) shelfIndex = -1;
            }
            if (shelfIndex < 0)
            {
                ctx.State.Stats.ItemsMissing++;
                ctx.Events.Publish(new CustomerItemFailed(customer.Id, wanted.Id, ItemFailReason.Missing));
                return;
            }

            var shelf = store.Shelves[shelfIndex];
            var product = catalog.GetProduct(shelf.ProductId);
            var type = catalog.GetCustomerType(customer.TypeId);
            long price = StoreRules.PriceOf(ctx.State, product);
            double a = type.ToleranceA + (StoreRules.FrontIsFromFarm(shelf, t) ? config.FarmSealBonus : 0);

            if (!rng.Chance(StoreRules.BuyChance(price, product.ReferencePrice, a, type.RangeB)))
            {
                ctx.State.Stats.ItemsRejectedByPrice++;
                ctx.Events.Publish(new CustomerItemFailed(customer.Id, product.Id, ItemFailReason.Price));
                return;
            }

            var shelfInv = new LotInventory(shelf.Lots);
            int available = shelfInv.Count(product.Id, t);
            double wantedUnits = item.Units * StoreRules.QuantityMultiplier(price, product.ReferencePrice, config.DiscountQuantityFactor);
            int units = Math.Min(available, Math.Max(1, rng.StochasticRound(wantedUnits)));
            shelfInv.TryRemove(product.Id, units, t, out var taken);

            customer.Cart.Add(new CartLine { ProductId = product.Id, UnitPrice = price, Lots = taken });
            ctx.Events.Publish(new CustomerPickedItem(customer.Id, shelfIndex, product.Id, units, substitute));
            ctx.Events.Publish(new ShelfChanged(shelfIndex));
        }

        private static int FindShelf(StoreState store, Func<ShelfState, bool> match, long t)
        {
            for (int i = 0; i < store.Shelves.Count; i++)
            {
                var s = store.Shelves[i];
                if (match(s) && StoreRules.Units(s, t) > 0) return i;
            }
            return -1;
        }

        /// <summary>Passa o próximo item do cliente na frente da fila; se acabou, ele paga e sai.</summary>
        public static bool ScanFront(GameContext ctx)
        {
            var customer = StoreRules.FrontOfQueue(ctx.State.Store);
            if (customer == null) return false;
            var line = customer.Cart[customer.ScannedLines];
            customer.ScannedLines++;
            ctx.Events.Publish(new ItemScanned(customer.Id, line.ProductId, line.Units, line.Total));
            if (customer.ScannedLines >= customer.Cart.Count) Leave(ctx, customer, LeaveReason.Paid);
            return true;
        }

        /// <summary>Cliente sai: paga as linhas já passadas no caixa; o resto volta ao armazém.</summary>
        public static void Leave(GameContext ctx, CustomerState customer, LeaveReason reason)
        {
            long paid = 0;
            for (int i = 0; i < customer.Cart.Count; i++)
            {
                var line = customer.Cart[i];
                if (i < customer.ScannedLines)
                {
                    paid += line.Total;
                    ctx.State.Stats.UnitsSold += line.Units;
                }
                else
                {
                    foreach (var lot in line.Lots) if (lot.Quantity > 0) ctx.Warehouse.Add(lot);
                }
            }
            if (paid > 0)
            {
                ctx.Wallet.AddCoins(paid, "venda");
                ctx.State.Stats.SalesRevenue += paid;
            }

            var stats = ctx.State.Stats;
            switch (reason)
            {
                case LeaveReason.Paid: stats.CustomersServed++; break;
                case LeaveReason.Empty: stats.CustomersLeftEmpty++; break;
                case LeaveReason.OutOfPatience: stats.CustomersLeftAngry++; break;
            }

            ctx.State.Store.Customers.Remove(customer);
            ctx.Events.Publish(new CustomerLeft(customer.Id, reason, paid));
        }

        /// <summary>Fecha a loja: todos saem (pagando o que já passou), carrinhos voltam ao armazém.</summary>
        public static void CloseStore(GameContext ctx)
        {
            foreach (var c in ctx.State.Store.Customers.ToArray()) Leave(ctx, c, LeaveReason.StoreClosed);
        }
    }
}
