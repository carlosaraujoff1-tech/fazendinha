using Game.Core.Farm;
using Game.Core.State;
using Game.Presentation.Graybox;
using UnityEngine;

namespace Game.Presentation.Farm
{
    /// <summary>Visual gray box de uma parcela: terra, planta (cresce em 4 fases) e marcador de "pronto".</summary>
    public sealed class PlotView : MonoBehaviour
    {
        private static readonly Color SoilDry = new Color(0.45f, 0.30f, 0.18f);
        private static readonly Color SoilWet = new Color(0.30f, 0.20f, 0.12f);
        private static readonly Color ReadyColor = new Color(1f, 0.85f, 0.2f);

        private Renderer _soil;
        private Transform _plant;
        private Renderer _plantRenderer;
        private Transform _readyMarker;
        private GameObject _model;
        private string _shownCrop;
        private int _shownStage = -1;
        private bool _shownWet;

        public int Index { get; private set; }

        public void Build(int index, float size)
        {
            Index = index;
            name = "Plot_" + index;

            var soil = GrayboxMaterials.Primitive(PrimitiveType.Cube, "Soil", transform, SoilDry, keepCollider: true);
            soil.transform.localScale = new Vector3(size, 0.2f, size);
            soil.transform.localPosition = new Vector3(0, 0.1f, 0);
            _soil = soil.GetComponent<Renderer>();

            var plant = GrayboxMaterials.Primitive(PrimitiveType.Cylinder, "Plant", transform, Color.green);
            _plant = plant.transform;
            _plantRenderer = plant.GetComponent<Renderer>();
            plant.SetActive(false);

            var marker = GrayboxMaterials.Primitive(PrimitiveType.Sphere, "Ready", transform, ReadyColor);
            marker.transform.localScale = Vector3.one * 0.3f;
            _readyMarker = marker.transform;
            marker.SetActive(false);
        }

        public void Render(PlotState plot, long nowMs)
        {
            var phase = FarmRules.GetPhase(plot, nowMs);
            bool wet = phase == PlotPhase.Growing && plot.Watered;
            if (wet != _shownWet)
            {
                _soil.sharedMaterial = GrayboxMaterials.Get(wet ? SoilWet : SoilDry);
                _shownWet = wet;
            }

            if (phase == PlotPhase.Empty)
            {
                if (_shownStage != 0) { _plant.gameObject.SetActive(false); _readyMarker.gameObject.SetActive(false); ClearModel(); }
                _shownStage = 0;
                _shownCrop = null;
                return;
            }

            if (_shownCrop != plot.CropId)
            {
                _plantRenderer.sharedMaterial = GrayboxMaterials.Get(GrayboxMaterials.ColorFor(plot.CropId));
                _shownCrop = plot.CropId;
                _shownStage = -1; // força trocar o modelo
            }

            int stage = FarmRules.VisualStage(plot, nowMs);
            if (stage != _shownStage)
            {
                _shownStage = stage;
                ShowStage(plot.CropId, stage);
            }

            bool ready = phase == PlotPhase.Ready;
            _readyMarker.gameObject.SetActive(ready);
            if (ready)
            {
                float bob = Mathf.Sin(Time.time * 4f + Index) * 0.08f;
                _readyMarker.localPosition = new Vector3(0, 1.4f + bob, 0);
            }
        }
    
        /// <summary>Modelo 3D da fase (prefab gerado); sem catálogo visual, usa o cilindro do gray box.</summary>
        private void ShowStage(string cropId, int stage)
        {
            ClearModel();
            var visuals = GameRoot.Instance != null ? GameRoot.Instance.Visuals : null;
            var prefab = visuals != null ? visuals.CropStage(cropId, stage) : null;
            if (prefab != null)
            {
                _plant.gameObject.SetActive(false);
                _model = Instantiate(prefab, transform);
                _model.transform.localPosition = new Vector3(0, 0.2f, 0);
                _model.transform.localRotation = Quaternion.Euler(0, (Index * 47) % 360, 0); // variedade
                return;
            }
            float h = 0.15f + 0.2f * stage;
            float w = 0.2f + 0.1f * stage;
            _plant.localScale = new Vector3(w, h * 0.5f, w);
            _plant.localPosition = new Vector3(0, 0.2f + h * 0.5f, 0);
            _plant.gameObject.SetActive(true);
        }

        private void ClearModel()
        {
            if (_model == null) return;
            Destroy(_model);
            _model = null;
        }
    }
}
