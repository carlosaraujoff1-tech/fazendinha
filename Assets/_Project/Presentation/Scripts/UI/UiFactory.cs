using System;
using Game.Content;
using Game.Presentation.Effects;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    public enum ButtonStyle { Green, Yellow, Red, Blue, Grey }

    /// <summary>
    /// Cria UI uGUI por código, com a "pele" do catálogo visual quando existir (botões arredondados com
    /// profundidade e painéis 9-slice do Kenney UI Pack, fonte estilizada, texto com sombra).
    /// Referência de layout: 1920 x 1080 (paisagem).
    /// </summary>
    public static class UiFactory
    {
        private static Font _builtin;

        public static readonly Color PanelColor = new Color(0.10f, 0.16f, 0.10f, 0.82f);
        public static readonly Color MenuColor = new Color(0.12f, 0.16f, 0.12f, 0.96f);
        public static readonly Color ButtonColor = new Color(0.20f, 0.62f, 0.32f, 1f);
        public static readonly Color ButtonDisabled = new Color(0.45f, 0.45f, 0.45f, 1f);

        private static VisualCatalog Skin => GameRoot.Instance != null ? GameRoot.Instance.Visuals : null;

        public static Font Font
        {
            get
            {
                var skin = Skin;
                if (skin != null && skin.font != null) return skin.font;
                return _builtin != null ? _builtin : (_builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            }
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Ancora o retângulo em (anchorMin..anchorMax) com margens em pixels de referência.</summary>
        public static RectTransform Anchor(this RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>Painel arredondado (9-slice) tingido com a cor pedida.</summary>
        public static Image Panel(string name, Transform parent, Color color)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            var skin = Skin;
            if (skin != null && skin.panel != null)
            {
                img.sprite = skin.panel;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 0.45f; // bordas mais grossas em 1080p
            }
            img.color = color;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size = 44, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = Node(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(14, size / 2);
            t.resizeTextMaxSize = size;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        public static Button MakeButton(string name, Transform parent, string text, Action onClick, int fontSize = 42,
            ButtonStyle style = ButtonStyle.Green)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = Color.white;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => { SoundPlayer.Play("click"); onClick(); });
            var label = Label("Text", rt, text, fontSize);
            ((RectTransform)label.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(18, 10), new Vector2(-18, -6));
            SetStyle(button, style);
            return button;
        }

        public static void SetStyle(Button button, ButtonStyle style)
        {
            var img = (Image)button.targetGraphic;
            var skin = Skin;
            Sprite sprite = null;
            if (skin != null)
            {
                switch (style)
                {
                    case ButtonStyle.Green: sprite = skin.buttonGreen; break;
                    case ButtonStyle.Yellow: sprite = skin.buttonYellow; break;
                    case ButtonStyle.Red: sprite = skin.buttonRed; break;
                    case ButtonStyle.Blue: sprite = skin.buttonBlue; break;
                    default: sprite = skin.buttonGrey; break;
                }
            }
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 0.5f;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                switch (style)
                {
                    case ButtonStyle.Green: img.color = ButtonColor; break;
                    case ButtonStyle.Yellow: img.color = new Color(0.95f, 0.75f, 0.2f); break;
                    case ButtonStyle.Red: img.color = new Color(0.75f, 0.25f, 0.22f); break;
                    case ButtonStyle.Blue: img.color = new Color(0.25f, 0.45f, 0.8f); break;
                    default: img.color = ButtonDisabled; break;
                }
            }
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = style == ButtonStyle.Grey ? new Color(0.25f, 0.25f, 0.28f) : Color.white;
        }

        public static void SetButton(Button button, string text, bool interactable, ButtonStyle style = ButtonStyle.Green)
        {
            button.interactable = interactable;
            SetStyle(button, interactable ? style : ButtonStyle.Grey);
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = text;
        }
    }
}
