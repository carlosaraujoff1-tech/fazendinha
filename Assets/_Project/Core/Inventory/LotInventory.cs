using System;
using System.Collections.Generic;
using Game.Core.Catalog;
using Game.Core.State;

namespace Game.Core.Inventory
{
    /// <summary>
    /// Estoque em lotes (armazém, depois prateleiras). Opera sobre a lista do estado para manter
    /// o <see cref="GameState"/> como única fonte da verdade. Saída sempre FEFO:
    /// o que vence primeiro sai primeiro. Lotes vencidos não contam e não saem; são removidos por
    /// <see cref="RemoveExpired"/>.
    /// </summary>
    public sealed class LotInventory
    {
        private readonly List<StockLot> _lots;

        public LotInventory(List<StockLot> lots)
        {
            _lots = lots ?? throw new ArgumentNullException(nameof(lots));
        }

        public IReadOnlyList<StockLot> Lots => _lots;

        /// <summary>Cria um lote novo com a validade do produto a partir de <paramref name="producedAtMs"/>.</summary>
        public static StockLot CreateLot(ProductDef product, int quantity, long producedAtMs, LotSource source)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            long expires = product.Perishable
                ? producedAtMs + product.ShelfLifeSeconds * 1000L
                : StockLot.NeverExpires;
            return new StockLot
            {
                ProductId = product.Id,
                Quantity = quantity,
                ProducedAtMs = producedAtMs,
                ExpiresAtMs = expires,
                Source = source,
            };
        }

        /// <summary>Adiciona um lote, juntando com um lote idêntico (mesmo produto, origem, produção e validade).</summary>
        public void Add(StockLot lot)
        {
            if (lot == null) throw new ArgumentNullException(nameof(lot));
            if (lot.Quantity <= 0) throw new ArgumentOutOfRangeException(nameof(lot), "Quantidade deve ser > 0.");
            if (string.IsNullOrEmpty(lot.ProductId)) throw new ArgumentException("Lote sem produto.", nameof(lot));

            foreach (var existing in _lots)
            {
                if (existing.ProductId == lot.ProductId && existing.Source == lot.Source &&
                    existing.ProducedAtMs == lot.ProducedAtMs && existing.ExpiresAtMs == lot.ExpiresAtMs)
                {
                    existing.Quantity = checked(existing.Quantity + lot.Quantity);
                    return;
                }
            }
            _lots.Add(lot.CloneWithQuantity(lot.Quantity));
        }

        /// <summary>Unidades válidas (não vencidas) de um produto.</summary>
        public int Count(string productId, long nowMs)
        {
            int total = 0;
            foreach (var lot in _lots)
                if (lot.ProductId == productId && !lot.IsExpired(nowMs)) total += lot.Quantity;
            return total;
        }

        /// <summary>Unidades válidas de um produto vindas de uma origem específica.</summary>
        public int Count(string productId, LotSource source, long nowMs)
        {
            int total = 0;
            foreach (var lot in _lots)
                if (lot.ProductId == productId && lot.Source == source && !lot.IsExpired(nowMs)) total += lot.Quantity;
            return total;
        }

        /// <summary>
        /// Retira <paramref name="quantity"/> unidades (FEFO). Tudo ou nada: se não houver o suficiente,
        /// nada muda e retorna false. <paramref name="taken"/> traz os pedaços retirados (com origem e validade).
        /// </summary>
        public bool TryRemove(string productId, int quantity, long nowMs, out List<StockLot> taken)
        {
            taken = new List<StockLot>();
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (Count(productId, nowMs) < quantity) return false;

            var candidates = new List<StockLot>();
            foreach (var lot in _lots)
                if (lot.ProductId == productId && !lot.IsExpired(nowMs)) candidates.Add(lot);
            candidates.Sort(CompareFefo);

            int remaining = quantity;
            foreach (var lot in candidates)
            {
                if (remaining == 0) break;
                int take = Math.Min(remaining, lot.Quantity);
                taken.Add(lot.CloneWithQuantity(take));
                lot.Quantity -= take;
                remaining -= take;
            }
            _lots.RemoveAll(l => l.Quantity <= 0);
            return true;
        }

        /// <summary>Remove e devolve todos os lotes vencidos em <paramref name="nowMs"/>.</summary>
        public List<StockLot> RemoveExpired(long nowMs)
        {
            var expired = new List<StockLot>();
            for (int i = _lots.Count - 1; i >= 0; i--)
            {
                if (_lots[i].IsExpired(nowMs))
                {
                    expired.Add(_lots[i]);
                    _lots.RemoveAt(i);
                }
            }
            expired.Reverse();
            return expired;
        }

        /// <summary>Próximo instante em que algum lote vence (ou <see cref="StockLot.NeverExpires"/>).</summary>
        public long NextExpirationMs()
        {
            long next = StockLot.NeverExpires;
            foreach (var lot in _lots) if (lot.ExpiresAtMs < next) next = lot.ExpiresAtMs;
            return next;
        }

        private static int CompareFefo(StockLot a, StockLot b)
        {
            int c = a.ExpiresAtMs.CompareTo(b.ExpiresAtMs);
            return c != 0 ? c : a.ProducedAtMs.CompareTo(b.ProducedAtMs);
        }
    }
}
