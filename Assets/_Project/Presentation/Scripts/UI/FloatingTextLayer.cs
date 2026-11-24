using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    /// <summary>Textos que sobem e somem sobre o mundo ("+4 Alface", "+38"). Pool de rótulos.</summary>
    public sealed class FloatingTextLayer : MonoBehaviour
    {
        private sealed class Item
        {
            public RectTransform Rect;
            public Text Label;
            public Outline Outline;
            public Vector3 World;
            public float Start;
            public bool Active;
        }

        private const float Duration = 1.3f;
        private static FloatingTextLayer _instance;
        private readonly List<Item> _items = new List<Item>();
        private Camera _camera;
        private RectTransform _rect;

        private void Awake()
        {
            _instance = this;
            _rect = (RectTransform)transform;
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        public static void Show(Vector3 worldPosition, string text, Color color)
        {
            if (_instance != null) _instance.ShowInternal(worldPosition, text, color);
        }

        private void ShowInternal(Vector3 world, string text, Color color)
        {
            Item item = null;
            foreach (var i in _items) if (!i.Active) { item = i; break; }
            if (item == null)
            {
                var label = UiFactory.Label("Float", transform, "", 46);
                label.fontStyle = FontStyle.Bold;
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0, 0, 0, 0.7f);
                outline.effectDistance = new Vector2(2.5f, -2.5f);
                var rt = (RectTransform)label.transform;
                rt.sizeDelta = new Vector2(600, 80);
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                item = new Item { Rect = rt, Label = label, Outline = outline };
                _items.Add(item);
            }
            item.Label.text = text;
            item.Label.color = color;
            item.World = world;
            item.Start = Time.unscaledTime;
            item.Active = true;
            item.Rect.gameObject.SetActive(true);
            Position(item, 0f);
        }

        private void LateUpdate()
        {
            foreach (var item in _items)
            {
                if (!item.Active) continue;
                float t = (Time.unscaledTime - item.Start) / Duration;
                if (t >= 1f)
                {
                    item.Active = false;
                    item.Rect.gameObject.SetActive(false);
                    continue;
                }
                Position(item, t);
                var c = item.Label.color;
                c.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                item.Label.color = c;
                float scale = t < 0.15f ? Mathf.Lerp(0.6f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 1f, Mathf.Min(1f, (t - 0.15f) / 0.2f));
                item.Rect.localScale = Vector3.one * scale;
            }
        }

        private void Position(Item item, float t)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            Vector3 screen = _camera.WorldToScreenPoint(item.World + Vector3.up * (1.2f + t * 1.4f));
            if (screen.z < 0) { item.Rect.gameObject.SetActive(false); return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screen, null, out var local);
            item.Rect.anchoredPosition = local + _rect.rect.size * 0.5f;
        }
    }
}
