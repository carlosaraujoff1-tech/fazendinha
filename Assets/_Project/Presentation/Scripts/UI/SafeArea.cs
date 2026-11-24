using UnityEngine;

namespace Game.Presentation.UI
{
    /// <summary>Ajusta o RectTransform à área segura da tela (recorte de câmera, barra de gestos).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        private Rect _applied;
        private Vector2Int _screen;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != _applied || _screen.x != Screen.width || _screen.y != Screen.height) Apply();
        }

        private void Apply()
        {
            var rt = (RectTransform)transform;
            Rect safe = Screen.safeArea;
            _applied = safe;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
