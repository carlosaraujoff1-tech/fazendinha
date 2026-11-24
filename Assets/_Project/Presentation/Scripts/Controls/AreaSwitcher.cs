using System;
using System.Collections;
using UnityEngine;

namespace Game.Presentation.Controls
{
    public enum GameArea { Farm, Store }

    /// <summary>Troca a câmera entre as áreas do mundo (fazenda e loja ficam lado a lado).</summary>
    public sealed class AreaSwitcher : MonoBehaviour
    {
        [SerializeField] private TouchCameraController cameraController;

        private readonly Rect[] _bounds = new Rect[2];

        public GameArea Current { get; private set; } = GameArea.Farm;

        public event Action<GameArea> Changed;

        private void Awake()
        {
            if (cameraController == null) cameraController = FindFirstObjectByType<TouchCameraController>();
        }

        private IEnumerator Start()
        {
            yield return null; // espera as áreas informarem seus limites no Start delas
            Show(Current);
        }

        /// <summary>Cada área informa o retângulo (xz) que a câmera pode percorrer.</summary>
        public void SetBounds(GameArea area, Rect rect)
        {
            _bounds[(int)area] = rect;
            if (area == Current) ApplyBounds();
        }

        public void Show(GameArea area)
        {
            Current = area;
            var r = _bounds[(int)area];
            cameraController.SetBounds(r.min, r.max);
            cameraController.FocusOn(new Vector3(r.center.x, 0, r.center.y));
            Changed?.Invoke(area);
        }

        private void ApplyBounds()
        {
            var r = _bounds[(int)Current];
            cameraController.SetBounds(r.min, r.max);
        }
    }
}
