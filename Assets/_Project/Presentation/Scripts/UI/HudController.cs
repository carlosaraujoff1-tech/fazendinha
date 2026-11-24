using System.Linq;
using System.Text;
using Game.Core.Display;
using Game.Core.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    /// <summary>HUD gray box: moedas, gemas, meta, resumo do armazém e compra de parcela.</summary>
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] private float refreshSeconds = 0.25f;

        private Text _coins;
        private Text _goal;
        private Text _warehouse;
        private Button _buyPlot;
        private float _nextRefresh;
        private readonly StringBuilder _sb = new StringBuilder(256);

        private void Start()
        {
            var top = UiFactory.Panel("TopBar", transform, UiFactory.PanelColor);
            ((RectTransform)top.transform).Anchor(new Vector2(0, 0.93f), new Vector2(1, 1));
            _coins = UiFactory.Label("Coins", top.transform, "", 52, TextAnchor.MiddleLeft);
            ((RectTransform)_coins.transform).Anchor(new Vector2(0.04f, 0), new Vector2(0.6f, 1));
            _goal = UiFactory.Label("Goal", top.transform, "", 34, TextAnchor.MiddleRight);
            ((RectTransform)_goal.transform).Anchor(new Vector2(0.5f, 0), new Vector2(0.96f, 1));

            var bottom = UiFactory.Panel("BottomBar", transform, UiFactory.PanelColor);
            ((RectTransform)bottom.transform).Anchor(new Vector2(0, 0), new Vector2(1, 0.11f));
            _warehouse = UiFactory.Label("Warehouse", bottom.transform, "", 34, TextAnchor.MiddleLeft);
            ((RectTransform)_warehouse.transform).Anchor(new Vector2(0.03f, 0), new Vector2(0.62f, 1));
            _buyPlot = UiFactory.MakeButton("BuyPlot", bottom.transform, "", () => GameRoot.Instance.Execute(new BuyPlotCommand()), 36);
            ((RectTransform)_buyPlot.transform).Anchor(new Vector2(0.64f, 0.15f), new Vector2(0.97f, 0.85f));

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
    }
}
