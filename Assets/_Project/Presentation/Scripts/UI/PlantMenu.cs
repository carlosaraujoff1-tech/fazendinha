using System.Collections.Generic;
using System.Linq;
using Game.Core.Catalog;
using Game.Core.Display;
using Game.Core.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    /// <summary>Painel "Plantar": lista as culturas do catálogo (dados), com tempo, custo e bloqueio por nível.</summary>
    public sealed class PlantMenu : MonoBehaviour
    {
        private GameObject _panel;
        private RectTransform _list;
        private readonly List<(CropDef crop, Button button)> _buttons = new List<(CropDef, Button)>();
        private int _plotIndex = -1;
        private static PlantMenu _instance;
        public static bool IsOpen => _instance != null && _instance._panel != null && _instance._panel.activeSelf;

        private void Start()
        {
            _instance = this;
            var root = GameRoot.Instance;
            var panel = UiFactory.Panel("PlantMenu", transform, UiFactory.MenuColor);
            ((RectTransform)panel.transform).Anchor(new Vector2(0.2f, 0.14f), new Vector2(0.8f, 0.88f));
            _panel = panel.gameObject;

            var title = UiFactory.Label("Title", panel.transform, "Plantar", 56);
            ((RectTransform)title.transform).Anchor(new Vector2(0, 0.88f), new Vector2(1, 1));

            _list = UiFactory.Node("List", panel.transform).Anchor(new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.86f));
            var layout = _list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            // Ordem por nível de desbloqueio e tempo: dado, não código.
            foreach (var crop in root.Catalog.Crops.OrderBy(c => c.UnlockLevel).ThenBy(c => c.GrowSeconds))
            {
                var c = crop;
                var button = UiFactory.MakeButton("Crop_" + c.Id, _list, c.Name, () => Plant(c), 36);
                var le = button.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 88;
                _buttons.Add((c, button));
            }

            var close = UiFactory.MakeButton("Close", panel.transform, "Fechar", Close);
            ((RectTransform)close.transform).Anchor(new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.12f));
            UiFactory.SetStyle(close, ButtonStyle.Red);

            _panel.SetActive(false);
        }

        public void Open(int plotIndex)
        {
            _plotIndex = plotIndex;
            var root = GameRoot.Instance;
            foreach (var (crop, button) in _buttons)
            {
                bool unlocked = FarmRules.IsCropUnlocked(crop, root.State);
                int seeds = FarmRules.SeedsOf(root.State, crop.Id);
                string time = DisplayFormat.Duration(crop.GrowSeconds * 1000L);
                string text = !unlocked ? $"{crop.Name}  ·  nível {crop.UnlockLevel}"
                    : seeds > 0 ? $"{crop.Name}   {seeds} sementes   {time}   rende {crop.Yield}"
                    : $"{crop.Name}   comprar e plantar ({crop.SeedCost})   {time}";
                bool can = unlocked && (seeds > 0 || root.State.Wallet.Coins >= crop.SeedCost);
                UiFactory.SetButton(button, text, can, seeds > 0 ? ButtonStyle.Green : ButtonStyle.Yellow);
            }
            _panel.SetActive(true);
        }

        public void Close()
        {
            _panel.SetActive(false);
            _plotIndex = -1;
        }

        private void Plant(CropDef crop)
        {
            if (_plotIndex < 0) return;
            int plot = _plotIndex;
            var root = GameRoot.Instance;
            // Sem semente no estoque: compra 1 na vendinha e já planta (atalho).
            if (FarmRules.SeedsOf(root.State, crop.Id) < 1 && !root.Execute(new BuySeedsCommand(crop.Id, 1)).IsSuccess) return;
            if (!root.Execute(new PlantCommand(plot, crop.Id)).IsSuccess) return;
            Close();
            var farm = FindFirstObjectByType<Game.Presentation.Farm.FarmView>();
            if (farm != null)
            {
                var pos = farm.PlotWorldPosition(plot);
                Game.Presentation.Effects.FxPlayer.Play("plant", pos + Vector3.up * 0.2f);
                FloatingTextLayer.Show(pos, $"Plantou {crop.Name}!", new Color(0.75f, 1f, 0.55f));
            }
            Game.Presentation.Effects.SoundPlayer.Play("plant");
        }
    }
}
