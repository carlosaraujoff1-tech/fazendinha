using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Presentation.Player
{
    /// <summary>
    /// Joystick virtual: área invisível na metade esquerda da tela; onde o dedo encosta aparece a base e o
    /// botão. <see cref="Value"/> vai de (-1,-1) a (1,1). Teclado WASD/setas também funciona (editor).
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private float radius = 110f;

        private RectTransform _area, _base, _knob;
        private int _pointerId = int.MinValue;
        private Vector2 _origin;

        public static Vector2 Value { get; private set; }

        private void Awake()
        {
            _area = (RectTransform)transform;
            var img = gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0); // pega o toque, invisível
            _base = Circle("Base", 2 * radius, new Color(1, 1, 1, 0.18f));
            _knob = Circle("Knob", radius * 0.9f, new Color(1, 1, 1, 0.55f));
            _base.gameObject.SetActive(false);
            _knob.gameObject.SetActive(false);
        }

        private RectTransform Circle(string name, float size, Color color)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiCircle.Sprite;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointerId != int.MinValue) return;
            _pointerId = e.pointerId;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, e.pressEventCamera, out _origin);
            _origin -= _area.rect.min;
            _base.anchoredPosition = _knob.anchoredPosition = _origin;
            _base.gameObject.SetActive(true);
            _knob.gameObject.SetActive(true);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, e.pressEventCamera, out var p);
            p -= _area.rect.min;
            var delta = Vector2.ClampMagnitude(p - _origin, radius);
            _knob.anchoredPosition = _origin + delta;
            Value = delta / radius;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            _pointerId = int.MinValue;
            Value = Vector2.zero;
            _base.gameObject.SetActive(false);
            _knob.gameObject.SetActive(false);
        }

        private void OnDisable() { Value = Vector2.zero; _pointerId = int.MinValue; }

        /// <summary>Entrada combinada: joystick + teclado (para testar no editor/PC).</summary>
        public static Vector2 Read()
        {
            var v = Value;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
                float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
                if (x != 0 || y != 0) v = Vector2.ClampMagnitude(new Vector2(x, y), 1f) * (kb.leftShiftKey.isPressed ? 1f : 0.6f);
            }
            return v;
        }
    }

    /// <summary>Sprite de círculo gerado em runtime (sem asset).</summary>
    public static class UiCircle
    {
        private static Sprite _sprite;
        public static Sprite Sprite
        {
            get
            {
                if (_sprite != null) return _sprite;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                float r = n * 0.5f;
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px);
                tex.Apply();
                _sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return _sprite;
            }
        }
    }
}
