using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Catalog;
using Game.Core.Display;
using Game.Core.Animals;
using Game.Core.Farm;
using Game.Core.Staff;
using Game.Core.Store;
using Game.Core.Supplier;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    /// <summary>
    /// Menus da loja (gray box): prateleira (repor/preço), preço de um produto e compra de móveis.
    /// Tudo é montado a partir do catálogo; os botões só enviam comandos.
    /// </summary>
    public sealed class StoreMenus : MonoBehaviour
    {
        private GameObject _panel;
        private Text _title;
        private RectTransform _list;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private Action _refresh;
        private float _nextRefresh;
        private static StoreMenus _instance;
        public static bool IsOpen => _instance != null && _instance._panel != null && _instance._panel.activeSelf;

        private void Start()
        {
            _instance = this;
            var panel = UiFactory.Panel("StoreMenu", transform, UiFactory.MenuColor);
            ((RectTransform)panel.transform).Anchor(new Vector2(0.18f, 0.14f), new Vector2(0.82f, 0.88f));
            _panel = panel.gameObject;

            _title = UiFactory.Label("Title", panel.transform, "", 50);
            ((RectTransform)_title.transform).Anchor(new Vector2(0, 0.88f), new Vector2(1, 1));

            _list = UiFactory.Node("List", panel.transform).Anchor(new Vector2(0.04f, 0.15f), new Vector2(0.96f, 0.87f));
            var layout = _list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            var close = UiFactory.MakeButton("Close", panel.transform, "Fechar", Close);
            ((RectTransform)close.transform).Anchor(new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.12f));
            UiFactory.SetStyle(close, ButtonStyle.Red);
            _panel.SetActive(false);
        }

        private void Update()
        {
            if (!_panel.activeSelf || _refresh == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.6f;
            _refresh();
        }

        public void Close()
        {
            _panel.SetActive(false);
            _refresh = null;
        }

        private void Show(string title, Action build)
        {
            _title.text = title;
            _refresh = () => { Clear(); build(); };
            _refresh();
            _panel.SetActive(true);
        }

        private void Clear()
        {
            foreach (var row in _rows) Destroy(row);
            _rows.Clear();
        }

        private Button Row(string text, Action onClick, bool interactable = true)
        {
            var b = UiFactory.MakeButton("Row", _list, text, onClick, 36);
            b.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
            UiFactory.SetButton(b, text, interactable);
            _rows.Add(b.gameObject);
            return b;
        }

        /// <summary>Linha com dois botões lado a lado (paisagem: cabe repor e preço na mesma linha).</summary>
        private void PairRow(string left, Action onLeft, bool leftEnabled, string right, Action onRight)
        {
            var row = UiFactory.Node("Pair", _list);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            var a = UiFactory.MakeButton("Left", row, left, onLeft, 32);
            UiFactory.SetButton(a, left, leftEnabled);
            var b = UiFactory.MakeButton("Right", row, right, onRight, 32);
            UiFactory.SetStyle(b, ButtonStyle.Blue);
            _rows.Add(row.gameObject);
        }

        private void Info(string text)
        {
            var label = UiFactory.Label("Info", _list, text, 36);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
            _rows.Add(label.gameObject);
        }

        // ---------- Prateleira ----------

        public void OpenShelf(int shelfIndex)
        {
            var root = GameRoot.Instance;
            var shelf = root.State.Store.Shelves[shelfIndex];
            if (!root.Catalog.TryGetFurniture(shelf.FurnitureId, out var furniture)) return;
            Show(furniture.Name, () => BuildShelf(shelfIndex, furniture));
        }

        private void BuildShelf(int shelfIndex, FurnitureDef furniture)
        {
            var root = GameRoot.Instance;
            var shelf = root.State.Store.Shelves[shelfIndex];
            long now = root.NowMs;
            int onShelf = StoreRules.Units(shelf, now);
            int free = furniture.Capacity - onShelf;

            string current = onShelf > 0 && root.Catalog.TryGetProduct(shelf.ProductId, out var cur)
                ? $"{cur.Name}: {onShelf}/{furniture.Capacity}" + (StoreRules.FrontIsFromFarm(shelf, now) ? "  · Da nossa fazenda" : "")
                : $"Vazia (0/{furniture.Capacity})";
            Info(current);

            foreach (var product in root.Catalog.Products)
            {
                if (product.CategoryId != furniture.CategoryId) continue;
                var p = product;
                int stock = root.Context.Warehouse.Count(p.Id, now);
                bool sameOrEmpty = onShelf == 0 || shelf.ProductId == p.Id;
                int units = Math.Min(free, stock);
                string label = $"Repor {p.Name} (armazém {stock})" + (units > 0 && sameOrEmpty ? $" +{units}" : "");
                long price = StoreRules.PriceOf(root.State, p);
                PairRow(label, () => root.Execute(new RestockCommand(shelfIndex, p.Id, units)), sameOrEmpty && units > 0,
                    $"Preço {price} (P {p.ReferencePrice}) · {TagText(StoreRules.Tag(price, p.ReferencePrice))}",
                    () => OpenPrice(p));
            }
        }

        // ---------- Preço ----------

        public void OpenPrice(ProductDef product) => Show("Preço: " + product.Name, () => BuildPrice(product));

        private void BuildPrice(ProductDef product)
        {
            var root = GameRoot.Instance;
            long price = StoreRules.PriceOf(root.State, product);
            long max = StoreRules.MaxPrice(product, root.Catalog.Store);
            Info($"Preço atual {price}  ·  Referência P = {product.ReferencePrice}  ·  {TagText(StoreRules.Tag(price, product.ReferencePrice))}");
            Row("+1", () => root.Execute(new SetPriceCommand(product.Id, Math.Min(max, price + 1))), price < max);
            Row("-1", () => root.Execute(new SetPriceCommand(product.Id, Math.Max(1, price - 1))), price > 1);
            Row($"Igual a P ({product.ReferencePrice})", () => root.Execute(new SetPriceCommand(product.Id, product.ReferencePrice)));
            long promo = Math.Max(1, (long)Math.Round(product.ReferencePrice * 0.8));
            Row($"Promoção -20% ({promo})", () => root.Execute(new SetPriceCommand(product.Id, promo)));
        }

        // ---------- Móveis ----------

        public void OpenFurniture() => Show("Melhorias", BuildFurniture);

        // ---------- Vendinha de sementes ----------

        public void OpenSeedShop() => Show("Vendinha de sementes", BuildSeedShop);

        private void BuildSeedShop()
        {
            var root = GameRoot.Instance;
            foreach (var crop in root.Catalog.Crops.OrderBy(c => c.UnlockLevel).ThenBy(c => c.GrowSeconds))
            {
                var c = crop;
                int have = FarmRules.SeedsOf(root.State, c.Id);
                bool unlocked = FarmRules.IsCropUnlocked(c, root.State);
                long coins = root.State.Wallet.Coins;
                if (!unlocked) { Info($"{c.Name}: libera no nível {c.UnlockLevel}"); continue; }
                PairRow($"{c.Name} x5 · {DisplayFormat.Coins(c.SeedCost * 5)}   (tem {have})",
                    () => root.Execute(new BuySeedsCommand(c.Id, 5)), coins >= c.SeedCost * 5,
                    $"x1 · {c.SeedCost}  ·  {DisplayFormat.Duration(c.GrowSeconds * 1000L)}",
                    () => root.Execute(new BuySeedsCommand(c.Id, 1)));
            }
        }

        // ---------- Animais ----------

        public void OpenAnimals() => Show("Animais", BuildAnimals);

        private void BuildAnimals()
        {
            var root = GameRoot.Instance;
            long now = root.NowMs;
            foreach (var animal in root.Catalog.Animals)
            {
                var a = animal;
                int count = AnimalRules.CountOf(root.State, a.Id);
                long cost = AnimalRules.NextCost(root.State, a, root.Catalog.Economy);
                string eats = string.Join("/", a.FeedProductIds.Select(id => root.Catalog.TryGetProduct(id, out var p) ? p.Name : id));
                string gives = root.Catalog.TryGetProduct(a.ProductId, out var prod) ? prod.Name : a.ProductId;
                int available = AnimalRules.AvailableFeed(root.Context, a);
                Info($"{a.Name} ({count}/{a.MaxCount}): come {a.FeedPerCycle} {eats} (tem {available}) → {a.Yield} {gives} a cada {DisplayFormat.Duration(a.CycleSeconds * 1000L)}");
                bool can = count < a.MaxCount && root.State.Wallet.Coins >= cost && root.State.Progress.Level >= a.UnlockLevel;
                Row(count >= a.MaxCount ? $"{a.Name}: máximo" : $"Comprar {a.Name} · {DisplayFormat.Coins(cost)}",
                    () => root.Execute(new BuyAnimalCommand(a.Id)), can);
            }
        }

        // ---------- Fornecedor ----------

        public void OpenSupplier() => Show("Fornecedor (entrega em ~2 min)", BuildSupplier);

        private void BuildSupplier()
        {
            var root = GameRoot.Instance;
            var economy = root.Catalog.Economy;
            long now = root.NowMs;
            foreach (var order in root.State.Supplier.Orders)
            {
                string name = root.Catalog.TryGetProduct(order.ProductId, out var op) ? op.Name : order.ProductId;
                Info($"A caminho: {order.Units} {name} · chega em {DisplayFormat.Duration(order.DeliverAtMs - now)}");
            }
            foreach (var product in root.Catalog.Products)
            {
                var p = product;
                long unit = SupplierRules.UnitPrice(p, economy);
                int stock = root.Context.Warehouse.Count(p.Id, now);
                PairRow($"{p.Name} x10 · {DisplayFormat.Coins(unit * 10)}  (armazém {stock})",
                    () => root.Execute(new BuyFromSupplierCommand(p.Id, 10)), root.State.Wallet.Coins >= unit * 10,
                    $"x1 · {unit}  (P {p.ReferencePrice})",
                    () => root.Execute(new BuyFromSupplierCommand(p.Id, 1)));
            }
        }

        private void BuildFurniture()
        {
            var root = GameRoot.Instance;
            var economy = root.Catalog.Economy;
            bool plotsMax = root.State.Farm.Plots.Count >= economy.MaxPlots;
            long plotCost = FarmRules.NextPlotCost(root.State, economy);
            Row(plotsMax ? "Fazenda: máximo de parcelas" : $"Fazenda: + 1 parcela · {DisplayFormat.Coins(plotCost)} ({root.State.Farm.Plots.Count}/{economy.MaxPlots})",
                () => root.Execute(new BuyPlotCommand()), !plotsMax && root.State.Wallet.Coins >= plotCost);
            foreach (var staff in root.Catalog.Staff)
            {
                var s = staff;
                int hired = StaffRules.CountOf(root.State.Staff, s.Id);
                bool active = root.State.Staff.Employees.Exists(e => e.TypeId == s.Id && e.Active);
                string status = hired == 0 ? "" : active ? " · trabalhando" : " · PARADO (sem salário)";
                Row($"Contratar {s.Name} · {DisplayFormat.Coins(s.HireCost)} + {s.Salary}/10 min ({hired}/{s.MaxHired}){status}",
                    () => root.Execute(new HireCommand(s.Id)),
                    hired < s.MaxHired && root.State.Wallet.Coins >= s.HireCost);
            }

            bool atMax = root.State.Store.Shelves.Count >= root.Catalog.Store.MaxFurniture;
            if (atMax) Info("Loja cheia");
            foreach (var furniture in root.Catalog.Furniture)
            {
                var f = furniture;
                long cost = StoreRules.FurnitureCost(root.State, f, root.Catalog.Economy);
                Row($"{f.Name} (cabe {f.Capacity}) · {DisplayFormat.Coins(cost)}",
                    () => root.Execute(new BuyFurnitureCommand(f.Id)),
                    !atMax && root.State.Wallet.Coins >= cost);
            }

            // Opções do aparelho
            var g = Game.Presentation.Effects.GraphicsQuality.Level;
            string Mark(Game.Presentation.Effects.GraphicsLevel l) =>
                (l == g ? "[ATUAL] " : "") + Game.Presentation.Effects.GraphicsQuality.Name(l);
            Info("Gráficos (mais baixo = mais fluido)");
            PairRow(Mark(Game.Presentation.Effects.GraphicsLevel.Baixo), () => Game.Presentation.Effects.GraphicsQuality.Set(Game.Presentation.Effects.GraphicsLevel.Baixo), true,
                Mark(Game.Presentation.Effects.GraphicsLevel.Medio), () => Game.Presentation.Effects.GraphicsQuality.Set(Game.Presentation.Effects.GraphicsLevel.Medio));
            Row(Mark(Game.Presentation.Effects.GraphicsLevel.Alto), () => Game.Presentation.Effects.GraphicsQuality.Set(Game.Presentation.Effects.GraphicsLevel.Alto));
            Row(Game.Presentation.Effects.SoundPlayer.Muted ? "Som: desligado (tocar para ligar)" : "Som: ligado (tocar para desligar)",
                () => Game.Presentation.Effects.SoundPlayer.Muted = !Game.Presentation.Effects.SoundPlayer.Muted);
        }

        public static string TagText(PriceTag tag)
        {
            switch (tag)
            {
                case PriceTag.Cheap: return "barato";
                case PriceTag.Fair: return "justo";
                default: return "caro";
            }
        }
    }
}
