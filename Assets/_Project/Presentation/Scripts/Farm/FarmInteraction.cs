using Game.Core.Display;
using Game.Core.Farm;
using Game.Presentation.Controls;
using Game.Presentation.Effects;
using Game.Presentation.UI;
using UnityEngine;

namespace Game.Presentation.Farm
{
    /// <summary>
    /// Toque numa parcela: vazia abre o menu de plantio, crescendo e sem rega rega, pronta colhe.
    /// Toda mudança passa por comandos no <see cref="GameRoot"/>.
    /// </summary>
    public sealed class FarmInteraction : MonoBehaviour
    {
        [SerializeField] private TouchCameraController cameraController;
        [SerializeField] private FarmView farmView;

        private PlantMenu _plantMenu;

        private void Start()
        {
            if (cameraController == null) cameraController = FindFirstObjectByType<TouchCameraController>();
            if (farmView == null) farmView = FindFirstObjectByType<FarmView>();
            _plantMenu = FindFirstObjectByType<PlantMenu>();
            cameraController.Tapped += OnTapped;
            farmView.LayoutChanged += FitCamera;
            FitCamera();
        }

        private void OnDestroy()
        {
            if (cameraController != null) cameraController.Tapped -= OnTapped;
            if (farmView != null) farmView.LayoutChanged -= FitCamera;
        }

        private void FitCamera()
        {
            var b = farmView.Bounds();
            var area = FindFirstObjectByType<AreaSwitcher>();
            var rect = Rect.MinMaxRect(b.xMin, b.yMin - 2f, b.xMax, b.yMax + 2f);
            if (area != null) area.SetBounds(GameArea.Farm, rect);
            else cameraController.SetBounds(rect.min, rect.max);
        }

        private void OnTapped(Vector2 screen)
        {
            var ray = cameraController.Camera.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out var hit, 200f)) return;
            var plotView = hit.collider.GetComponentInParent<PlotView>();
            if (plotView == null) return;
            HandlePlot(plotView.Index);
        }

        public void HandlePlot(int index)
        {
            var root = GameRoot.Instance;
            var plot = root.State.Farm.Plots[index];
            switch (FarmRules.GetPhase(plot, root.NowMs))
            {
                case PlotPhase.Empty:
                    SoundPlayer.Play("click");
                    if (_plantMenu != null) _plantMenu.Open(index);
                    break;
                case PlotPhase.Growing:
                    if (!plot.Watered)
                    {
                        if (root.Execute(new WaterCommand(index)).IsSuccess)
                        {
                            var pos = farmView.PlotWorldPosition(index);
                            FxPlayer.Play("water", pos);
                            SoundPlayer.Play("water");
                            FloatingTextLayer.Show(pos, "Regado! -25% tempo", new Color(0.6f, 0.85f, 1f));
                        }
                    }
                    else
                    {
                        FloatingTextLayer.Show(farmView.PlotWorldPosition(index),
                            "Pronto em " + DisplayFormat.Duration(FarmRules.RemainingMs(plot, root.NowMs)), Color.white);
                    }
                    break;
                case PlotPhase.Ready:
                    string cropName = root.Catalog.TryGetCrop(plot.CropId, out var crop) ? crop.Name : plot.CropId;
                    int qty = crop != null ? crop.Yield : 0;
                    if (root.Execute(new HarvestCommand(index)).IsSuccess)
                    {
                        var pos = farmView.PlotWorldPosition(index);
                        FxPlayer.Play("harvest", pos + Vector3.up * 0.3f);
                        FloatingTextLayer.Show(pos, $"+{qty} {cropName}", new Color(0.65f, 1f, 0.45f));
                    }
                    break;
            }
        }
    }
}
