using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    /// <summary>
    /// Cria UI uGUI por código (gray box). Sem prefabs nem YAML: a UI final da Etapa 5 substitui isto.
    /// Referência de layout: 1920 x 1080 (paisagem).
    /// </summary>
    public static class UiFactory
    {
        private static Font _font;

        public static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color ButtonColor = new Color(0.20f, 0.55f, 0.25f, 1f);
        public static readonly Color ButtonDisabled = new Color(0.4f, 0.4f, 0.4f, 1f);

        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

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

        public static Image Panel(string name, Transform parent, Color color)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
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
            return t;
        }

        public static Button MakeButton(string name, Transform parent, string text, Action onClick, int fontSize = 42)
        {
            var img = Panel(name, parent, ButtonColor);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            var label = Label("Text", img.transform, text, fontSize);
            ((RectTransform)label.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(16, 8), new Vector2(-16, -8));
            return button;
        }

        public static void SetButton(Button button, string text, bool interactable)
        {
            button.interactable = interactable;
            ((Image)button.targetGraphic).color = interactable ? ButtonColor : ButtonDisabled;
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = text;
        }
    }
}
