using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Presentation.Player
{
    /// <summary>
    /// Metade direita da tela: arrastar um dedo gira a câmera; dois dedos (pinça) aproximam/afastam.
    /// No editor, roda do mouse dá zoom.
    /// </summary>
    public sealed class LookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private float degreesPerPixel = 0.22f;

        private readonly Dictionary<int, Vector2> _pointers = new Dictionary<int, Vector2>();
        private float _lastPinch;

        private static Vector2 LookDelta;   // graus (yaw, pitch) acumulados desde a última leitura
        private static float ZoomDelta;     // metros acumulados

        /// <summary>Lê e zera o giro acumulado (chamado pela câmera uma vez por quadro).</summary>
        public static Vector2 ConsumeLook() { var v = LookDelta; LookDelta = Vector2.zero; return v; }

        /// <summary>Lê e zera o zoom acumulado (pinça + roda do mouse).</summary>
        public static float ConsumeZoom()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                float s = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(s) > 0.01f) ZoomDelta -= Mathf.Sign(s) * 0.6f;
            }
            float z = ZoomDelta;
            ZoomDelta = 0f;
            return z;
        }

        private void Awake()
        {
            var img = gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
        }

        public void OnPointerDown(PointerEventData e)
        {
            _pointers[e.pointerId] = e.position;
            _lastPinch = 0f;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_pointers.ContainsKey(e.pointerId)) return;
            _pointers[e.pointerId] = e.position;
            if (_pointers.Count == 1)
            {
                LookDelta += new Vector2(e.delta.x, -e.delta.y) * degreesPerPixel;
                return;
            }
            // Pinça com os dois primeiros dedos
            var it = _pointers.Values.GetEnumerator();
            it.MoveNext(); var a = it.Current; it.MoveNext(); var b = it.Current;
            float d = Vector2.Distance(a, b);
            if (_lastPinch > 0f) ZoomDelta += (_lastPinch - d) * 0.012f;
            _lastPinch = d;
        }

        public void OnPointerUp(PointerEventData e)
        {
            _pointers.Remove(e.pointerId);
            _lastPinch = 0f;
        }
    }
}
