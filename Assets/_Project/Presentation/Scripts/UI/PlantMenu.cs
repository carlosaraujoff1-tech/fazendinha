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

        private void Start()
        {
            var root = GameRoot.Instance;
            var panel = UiFactory.Panel("PlantMenu", transform, new Color(0.1f, 0.1f, 0.1f, 0.92f));
            ((RectTransform)panel.transform).Anchor(new Vector2(0.25f, 0.14f), new Vector2(0.75f, 0.88f));
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
                var button = UiFactory.MakeButton("Crop_" + c.Id, _list, c.Name, () => Plant(c));
                var le = button.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 88;
                _buttons.Add((c, button));
            }

            var close = UiFactory.MakeButton("Close", panel.transform, "Fechar", Close);
            ((RectTransform)close.transform).Anchor(new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.12f));
            ((Image)close.targetGraphic).color = new Color(0.5f, 0.2f, 0.2f);

            _panel.SetActive(false);
        }

        public void Open(int plotIndex)
        {
            _plotIndex = plotIndex;
            var root = GameRoot.Instance;
            foreach (var (crop, button) in _buttons)
            {
                bool unlocked = FarmRules.IsCropUnlocked(crop, root.State);
                string text = unlocked
                    ? $"{crop.Name}  ·  {DisplayFormat.Duration(crop.GrowSeconds * 1000L)}  ·  {crop.SeedCost} moedas  ·  rende {crop.Yield}"
                    : $"{crop.Name}  ·  nível {crop.UnlockLevel}";
                UiFactory.SetButton(button, text, unlocked && root.State.Wallet.Coins >= crop.SeedCost);
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
            if (GameRoot.Instance.Execute(new PlantCommand(_plotIndex, crop.Id)).IsSuccess) Close();
        }
    }
}
