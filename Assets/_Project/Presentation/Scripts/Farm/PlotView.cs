using Game.Core.Farm;
using Game.Core.State;
using Game.Presentation.Effects;
using Game.Core.Display;
using Game.Presentation.Graybox;
using Game.Presentation.Player;
using UnityEngine;

namespace Game.Presentation.Farm
{
    /// <summary>
    /// Visual de uma parcela: canteiro de terra arada com borda de madeira, planta 3D da fase atual
    /// (com "pulo" ao mudar de fase) e, quando pronta, brilho dourado + o produto flutuando.
    /// </summary>
    public sealed class PlotView : InteractableBehaviour
    {
        private static readonly Color BorderColor = new Color(0.47f, 0.32f, 0.2f);

        private Renderer _soil;
        private Material _soilDry, _soilWet;
        private Transform _plantRoot;
        private GameObject _model;
        private float _size = 3.2f;
        private float _iconHeight = 1.8f;
        private GameObject _readyFx;
        private GameObject _readyIcon;
        private string _shownCrop;
        private int _shownStage = -1;
        private bool _shownWet;
        private bool _shownReady;
        private float _popStart = -10f;

        public int Index { get; private set; }

        // ---------- Interação (botão de ação) ----------
        public override string Prompt(PlayerContext player)
        {
            var root = GameRoot.Instance;
            var plots = root.State.Farm.Plots;
            if (Index >= plots.Count) return null;
            var plot = plots[Index];
            switch (FarmRules.GetPhase(plot, root.NowMs))
            {
                case PlotPhase.Empty: return "Plantar";
                case PlotPhase.Growing:
                    return plot.Watered ? $"Crescendo · {DisplayFormat.Duration(FarmRules.RemainingMs(plot, root.NowMs))}" : "Regar";
                default:
                    string name = root.Catalog.TryGetCrop(plot.CropId, out var crop) ? crop.Name : plot.CropId;
                    return $"Colher {name}";
            }
        }

        public override void Interact(PlayerContext player) =>
            FindFirstObjectByType<FarmInteraction>()?.HandlePlot(Index);

        public void Build(int index, float size)
        {
            Index = index;
            _size = size;
            SetRadius(size * 0.5f + 1.4f);
            name = "Plot_" + index;
            var visuals = GameRoot.Instance != null ? GameRoot.Instance.Visuals : null;
            _soilDry = visuals != null && visuals.soil != null ? visuals.soil : GrayboxMaterials.Get(new Color(0.45f, 0.3f, 0.18f));
            _soilWet = visuals != null && visuals.soilWet != null ? visuals.soilWet : GrayboxMaterials.Get(new Color(0.3f, 0.2f, 0.12f));

            // Borda de madeira (4 tábuas) + terra levemente rebaixada
            float t = 0.12f;
            var border = visuals != null && visuals.wood != null ? visuals.wood : GrayboxMaterials.Get(BorderColor);
            Board(new Vector3(0, 0.09f, size * 0.5f), new Vector3(size + t, 0.18f, t), border);
            Board(new Vector3(0, 0.09f, -size * 0.5f), new Vector3(size + t, 0.18f, t), border);
            Board(new Vector3(size * 0.5f, 0.09f, 0), new Vector3(t, 0.18f, size), border);
            Board(new Vector3(-size * 0.5f, 0.09f, 0), new Vector3(t, 0.18f, size), border);

            var soil = GrayboxMaterials.Primitive(PrimitiveType.Cube, "Soil", transform, _soilDry, keepCollider: true);
            soil.transform.localScale = new Vector3(size, 0.14f, size);
            soil.transform.localPosition = new Vector3(0, 0.07f, 0);
            _soil = soil.GetComponent<Renderer>();

            _plantRoot = new GameObject("Plant").transform;
            _plantRoot.SetParent(transform, false);
            _plantRoot.localPosition = new Vector3(0, 0.14f, 0);
        }

        private void Board(Vector3 pos, Vector3 scale, Material mat)
        {
            var b = GrayboxMaterials.Primitive(PrimitiveType.Cube, "Board", transform, mat);
            b.transform.localPosition = pos;
            b.transform.localScale = scale;
        }

        public void Render(PlotState plot, long nowMs)
        {
            var phase = FarmRules.GetPhase(plot, nowMs);

            bool wet = phase == PlotPhase.Growing && plot.Watered;
            if (wet != _shownWet)
            {
                _soil.sharedMaterial = wet ? _soilWet : _soilDry;
                _shownWet = wet;
            }

            if (phase == PlotPhase.Empty)
            {
                if (_shownStage != 0)
                {
                    ClearModel();
                    SetReady(false, null);
                }
                _shownStage = 0;
                _shownCrop = null;
                return;
            }

            if (_shownCrop != plot.CropId)
            {
                _shownCrop = plot.CropId;
                _shownStage = -1;
            }

            int stage = FarmRules.VisualStage(plot, nowMs);
            if (stage != _shownStage)
            {
                bool grew = _shownStage > 0;
                _shownStage = stage;
                ShowStage(plot.CropId, stage);
                _popStart = Time.time;
                if (grew) FxPlayer.Play("puff", transform.position + Vector3.up * 0.3f);
            }

            SetReady(phase == PlotPhase.Ready, plot.CropId);
        }

        private void Update()
        {
            // "Pulo" ao trocar de fase (escala com overshoot)
            float t = (Time.time - _popStart) / 0.45f;
            if (_plantRoot != null)
            {
                float s = t >= 1f ? 1f : t < 0.5f ? Mathf.Lerp(0.4f, 1.15f, t / 0.5f) : Mathf.Lerp(1.15f, 1f, (t - 0.5f) / 0.5f);
                _plantRoot.localScale = Vector3.one * s;
            }
            if (_readyIcon != null)
            {
                _readyIcon.transform.localPosition = new Vector3(0, _iconHeight + Mathf.Sin(Time.time * 3f + Index) * 0.12f, 0);
                _readyIcon.transform.Rotate(0, 90f * Time.deltaTime, 0, Space.World);
            }
        }

        private void SetReady(bool ready, string cropId)
        {
            if (ready == _shownReady) return;
            _shownReady = ready;
            if (_readyFx != null) { Destroy(_readyFx); _readyFx = null; }
            if (_readyIcon != null) { Destroy(_readyIcon); _readyIcon = null; }
            if (!ready) return;

            _readyFx = FxPlayer.Spawn("ready", transform, Vector3.zero);
            if (_readyFx != null) _readyFx.transform.localScale = Vector3.one * (_size / 1.6f);
            _iconHeight = 1.0f;
            if (_model != null)
                foreach (var r in _model.GetComponentsInChildren<Renderer>()) _iconHeight = Mathf.Max(_iconHeight, r.bounds.max.y - transform.position.y + 0.5f);
            var visuals = GameRoot.Instance != null ? GameRoot.Instance.Visuals : null;
            var productId = cropId != null && GameRoot.Instance.Catalog.TryGetCrop(cropId, out var crop) ? crop.ProductId : null;
            var icon = visuals != null ? visuals.Product(productId) : null;
            if (icon != null)
            {
                _readyIcon = Instantiate(icon, transform);
                _readyIcon.transform.localScale = Vector3.one * 1.8f;
            }
            else
            {
                _readyIcon = GrayboxMaterials.Primitive(PrimitiveType.Sphere, "Ready", transform, new Color(1f, 0.85f, 0.2f));
                _readyIcon.transform.localScale = Vector3.one * 0.3f;
            }
        }

        /// <summary>Modelo 3D da fase (prefab gerado); sem catálogo visual, usa um cilindro.</summary>
        private void ShowStage(string cropId, int stage)
        {
            ClearModel();
            var visuals = GameRoot.Instance != null ? GameRoot.Instance.Visuals : null;
            var prefab = visuals != null ? visuals.CropStage(cropId, stage) : null;
            if (prefab != null)
            {
                // Plantação: grade density × density com leve variação (posição, giro, tamanho) — parece de verdade.
                _model = new GameObject("Plants");
                _model.transform.SetParent(_plantRoot, false);
                int n = visuals.CropDensity(cropId);
                float step = (_size - 0.5f) / n;
                var rng = new System.Random(Index * 7919 + stage);
                for (int ix = 0; ix < n; ix++)
                for (int iz = 0; iz < n; iz++)
                {
                    var plant = Instantiate(prefab, _model.transform);
                    float jx = (float)(rng.NextDouble() - 0.5) * step * 0.25f;
                    float jz = (float)(rng.NextDouble() - 0.5) * step * 0.25f;
                    plant.transform.localPosition = new Vector3(-(_size - 0.5f) * 0.5f + step * (ix + 0.5f) + jx, 0, -(_size - 0.5f) * 0.5f + step * (iz + 0.5f) + jz);
                    plant.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    plant.transform.localScale = Vector3.one * (0.88f + (float)rng.NextDouble() * 0.24f);
                }
                return;
            }
            _model = GrayboxMaterials.Primitive(PrimitiveType.Cylinder, "Plant", _plantRoot, GrayboxMaterials.ColorFor(cropId));
            float h = 0.15f + 0.2f * stage, w = 0.2f + 0.1f * stage;
            _model.transform.localScale = new Vector3(w, h * 0.5f, w);
            _model.transform.localPosition = new Vector3(0, h * 0.5f, 0);
        }

        private void ClearModel()
        {
            if (_model == null) return;
            Destroy(_model);
            _model = null;
        }
    }
}
