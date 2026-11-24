using System.Linq;
using System.Text;
using Game.Core.Display;
using Game.Core.Farm;
using Game.Core.Store;
using Game.Presentation.Controls;
using Game.Presentation.Store;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    /// <summary>HUD gray box: moedas, meta, abas Fazenda/Loja e a barra de ações de cada área.</summary>
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] private float refreshSeconds = 0.25f;

        private Text _coins;
        private Text _goal;
        private Text _warehouse;
        private Button _buyPlot;
        private Button _scan;
        private Button _furniture;
        private Button _tabFarm;
        private Button _tabStore;
        private AreaSwitcher _area;
        private float _nextRefresh;
        private readonly StringBuilder _sb = new StringBuilder(256);

        private void Start()
        {
            var top = UiFactory.Panel("TopBar", transform, UiFactory.PanelColor);
            ((RectTransform)top.transform).Anchor(new Vector2(0, 0.91f), new Vector2(1, 1));
            _coins = UiFactory.Label("Coins", top.transform, "", 52, TextAnchor.MiddleLeft);
            ((RectTransform)_coins.transform).Anchor(new Vector2(0.04f, 0), new Vector2(0.6f, 1));
            _goal = UiFactory.Label("Goal", top.transform, "", 34, TextAnchor.MiddleRight);
            ((RectTransform)_goal.transform).Anchor(new Vector2(0.5f, 0), new Vector2(0.96f, 1));

            // Abas (as 5 da UI final chegam na Etapa 5)
            _area = FindFirstObjectByType<AreaSwitcher>();
            // Paisagem: uma barra inferior com abas à esquerda, informação no meio e ações à direita.
            var bottom = UiFactory.Panel("BottomBar", transform, UiFactory.PanelColor);
            ((RectTransform)bottom.transform).Anchor(new Vector2(0, 0), new Vector2(1, 0.12f));
            _tabFarm = UiFactory.MakeButton("TabFarm", bottom.transform, "Fazenda", () => _area.Show(GameArea.Farm), 36);
            ((RectTransform)_tabFarm.transform).Anchor(new Vector2(0.01f, 0.12f), new Vector2(0.13f, 0.88f));
            _tabStore = UiFactory.MakeButton("TabStore", bottom.transform, "Mercadinho", () => _area.Show(GameArea.Store), 36);
            ((RectTransform)_tabStore.transform).Anchor(new Vector2(0.14f, 0.12f), new Vector2(0.27f, 0.88f));
            _warehouse = UiFactory.Label("Info", bottom.transform, "", 30, TextAnchor.MiddleLeft);
            ((RectTransform)_warehouse.transform).Anchor(new Vector2(0.29f, 0), new Vector2(0.64f, 1));
            _furniture = UiFactory.MakeButton("Furniture", bottom.transform, "+ Móvel", () => FindFirstObjectByType<StoreMenus>()?.OpenFurniture(), 32);
            ((RectTransform)_furniture.transform).Anchor(new Vector2(0.65f, 0.12f), new Vector2(0.79f, 0.88f));
            _buyPlot = UiFactory.MakeButton("BuyPlot", bottom.transform, "", () => GameRoot.Instance.Execute(new BuyPlotCommand()), 32);
            ((RectTransform)_buyPlot.transform).Anchor(new Vector2(0.80f, 0.12f), new Vector2(0.99f, 0.88f));
            _scan = UiFactory.MakeButton("Scan", bottom.transform, "Passar item", StoreInteraction.Scan, 32);
            ((RectTransform)_scan.transform).Anchor(new Vector2(0.80f, 0.12f), new Vector2(0.99f, 0.88f));

            if (_area != null) _area.Changed += _ => Refresh();
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshSeconds;
            Refresh();
        }

        private void Refresh()
        {
            var root = GameRoot.Instance;
            var s = root.State;
            _coins.text = $"Moedas {DisplayFormat.Coins(s.Wallet.Coins)}   Gemas {s.Wallet.Gems}";
            // Metas de verdade (dados) chegam na Etapa 5; por enquanto mostra o progresso de colheitas.
            _goal.text = $"Nível {s.Progress.Level} · Colheitas {s.Stats.CropsHarvested}";

            bool inStore = _area != null && _area.Current == GameArea.Store;
            _buyPlot.gameObject.SetActive(!inStore);
            _scan.gameObject.SetActive(inStore);
            _furniture.gameObject.SetActive(inStore);
            ((Image)_tabFarm.targetGraphic).color = inStore ? UiFactory.ButtonDisabled : UiFactory.ButtonColor;
            ((Image)_tabStore.targetGraphic).color = inStore ? UiFactory.ButtonColor : UiFactory.ButtonDisabled;
            if (inStore)
            {
                RefreshStore(root);
                return;
            }

            _sb.Clear();
            _sb.Append("Armazém: ");
            long now = root.NowMs;
            bool any = false;
            foreach (var product in root.Catalog.Products.OrderBy(p => p.ReferencePrice))
            {
                int n = root.Context.Warehouse.Count(product.Id, now);
                if (n <= 0) continue;
                if (any) _sb.Append("  ");
                _sb.Append(product.Name).Append(' ').Append(n);
                any = true;
            }
            if (!any) _sb.Append("vazio");
            _warehouse.text = _sb.ToString();

            var economy = root.Catalog.Economy;
            bool atMax = s.Farm.Plots.Count >= economy.MaxPlots;
            long cost = FarmRules.NextPlotCost(s, economy);
            UiFactory.SetButton(_buyPlot, atMax ? "Máx. parcelas" : $"+ Parcela ({DisplayFormat.Coins(cost)})",
                !atMax && s.Wallet.Coins >= cost);
        }
    
        private void RefreshStore(GameRoot root)
        {
            var store = root.State.Store;
            int queued = 0, shopping = 0;
            foreach (var c in store.Customers)
            {
                if (c.Phase == CustomerPhase.Queued) queued++;
                else shopping++;
            }
            var front = StoreRules.FrontOfQueue(store);
            string next = front != null ? $"  ·  item {front.ScannedLines + 1}/{front.Cart.Count}" : "";
            _warehouse.text = $"Fila {queued}{next}\nComprando {shopping}  ·  Vendas {DisplayFormat.Coins(root.State.Stats.SalesRevenue)}";
            UiFactory.SetButton(_scan, "Passar item", front != null);
        }
    }
}
