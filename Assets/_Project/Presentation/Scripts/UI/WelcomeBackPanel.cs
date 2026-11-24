using System;
using Game.Core.Display;
using Game.Core.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    /// <summary>Tela "Bem-vindo de volta": quanto tempo ficou fora, quanto contou (até 4 h) e o que aconteceu.</summary>
    public sealed class WelcomeBackPanel : MonoBehaviour
    {
        private GameObject _panel;
        private Text _body;
        private IDisposable _sub;

        private void Start()
        {
            var panel = UiFactory.Panel("WelcomeBack", transform, new Color(0.08f, 0.12f, 0.08f, 0.95f));
            ((RectTransform)panel.transform).Anchor(new Vector2(0.28f, 0.2f), new Vector2(0.72f, 0.82f));
            _panel = panel.gameObject;
            var title = UiFactory.Label("Title", panel.transform, "Bem-vindo de volta!", 54);
            ((RectTransform)title.transform).Anchor(new Vector2(0, 0.82f), new Vector2(1, 0.98f));
            _body = UiFactory.Label("Body", panel.transform, "", 36);
            ((RectTransform)_body.transform).Anchor(new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.8f));
            var ok = UiFactory.MakeButton("Ok", panel.transform, "Coletar", () => _panel.SetActive(false));
            ((RectTransform)ok.transform).Anchor(new Vector2(0.3f, 0.04f), new Vector2(0.7f, 0.18f));
            _panel.SetActive(false);

            var root = GameRoot.Instance;
            if (root.PendingWelcome.HasValue)
            {
                Show(root.PendingWelcome.Value);
                root.PendingWelcome = null;
            }
            _sub = root.Events.Subscribe<OfflineReturned>(Show);
        }

        private void OnDestroy() => _sub?.Dispose();

        private void Show(OfflineReturned e)
        {
            string credited = e.CreditedMs < e.AwayMs
                ? $"A fazenda trabalhou {DisplayFormat.Duration(e.CreditedMs)} (máximo offline)."
                : "A fazenda trabalhou o tempo todo.";
            _body.text =
                $"Você ficou fora {DisplayFormat.Duration(e.AwayMs)}.\n{credited}\n\n" +
                $"Parcelas prontas para colher: {e.PlotsReady}\n" +
                $"Entregas do fornecedor: {e.Deliveries}\n" +
                (e.UnitsExpired > 0 ? $"Produtos vencidos: {e.UnitsExpired}\n" : "") +
                "\nA loja não vende com o jogo fechado.";
            _panel.SetActive(true);
        }
    }
}
