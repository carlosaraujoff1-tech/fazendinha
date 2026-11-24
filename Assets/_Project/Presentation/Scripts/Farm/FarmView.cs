using System;
using System.Collections.Generic;
using Game.Core.Farm;
using UnityEngine;

namespace Game.Presentation.Farm
{
    /// <summary>Monta e atualiza a grade de parcelas a partir do estado.</summary>
    public sealed class FarmView : MonoBehaviour
    {
        [SerializeField] private int columns = 5;
        [SerializeField] private float plotSize = 1.6f;
        [SerializeField] private float gap = 0.4f;
        [SerializeField] private float refreshSeconds = 0.2f;

        private readonly List<PlotView> _plots = new List<PlotView>();
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private float _nextRefresh;

        public event Action LayoutChanged;

        public int Columns => columns;
        public float Pitch => plotSize + gap;

        private void Start()
        {
            var root = GameRoot.Instance;
            _subscriptions.Add(root.Events.Subscribe<PlotBought>(_ => Rebuild()));
            _subscriptions.Add(root.Events.Subscribe<PlotChanged>(e => RenderPlot(e.PlotIndex)));
            Rebuild();
        }

        private void OnDestroy()
        {
            foreach (var s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshSeconds;
            for (int i = 0; i < _plots.Count; i++) RenderPlot(i);
        }

        public Vector3 PlotLocalPosition(int index)
        {
            int col = index % columns, row = index / columns;
            float width = (columns - 1) * Pitch;
            return new Vector3(col * Pitch - width * 0.5f, 0f, -row * Pitch);
        }

        public Vector3 PlotWorldPosition(int index) => transform.TransformPoint(PlotLocalPosition(index));

        /// <summary>Retângulo (xz) ocupado pelas parcelas, para limitar a câmera.</summary>
        public Rect Bounds()
        {
            int rows = Mathf.Max(1, Mathf.CeilToInt(_plots.Count / (float)columns));
            float width = (columns - 1) * Pitch;
            Vector3 origin = transform.position;
            return Rect.MinMaxRect(origin.x - width * 0.5f, origin.z - (rows - 1) * Pitch, origin.x + width * 0.5f, origin.z);
        }

        private void Rebuild()
        {
            var plots = GameRoot.Instance.State.Farm.Plots;
            while (_plots.Count < plots.Count)
            {
                int i = _plots.Count;
                var go = new GameObject();
                go.transform.SetParent(transform, false);
                go.transform.localPosition = PlotLocalPosition(i);
                var view = go.AddComponent<PlotView>();
                view.Build(i, plotSize);
                _plots.Add(view);
            }
            for (int i = 0; i < _plots.Count; i++) RenderPlot(i);
            LayoutChanged?.Invoke();
        }

        private void RenderPlot(int index)
        {
            var root = GameRoot.Instance;
            var plots = root.State.Farm.Plots;
            if (index < 0 || index >= _plots.Count || index >= plots.Count) return;
            _plots[index].Render(plots[index], root.NowMs);
        }
    }
}
