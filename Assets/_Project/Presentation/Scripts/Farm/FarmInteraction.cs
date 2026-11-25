using Game.Core.Animals;
using Game.Core.Display;
using Game.Core.Farm;
using Game.Presentation.Effects;
using Game.Presentation.UI;
using UnityEngine;

namespace Game.Presentation.Farm
{
    /// <summary>
    /// Ação numa parcela (botão de ação perto dela): vazia abre o menu de plantio, crescendo e sem rega rega,
    /// pronta colhe. Animais: alimentar / coletar. Toda mudança passa por comandos no <see cref="GameRoot"/>.
    /// </summary>
    public sealed class FarmInteraction : MonoBehaviour
    {
        [SerializeField] private FarmView farmView;

        private PlantMenu _plantMenu;

        private void Start()
        {
            _plantMenu = FindFirstObjectByType<PlantMenu>();
            if (farmView == null) farmView = FindFirstObjectByType<FarmView>();
        }

        public void HandleAnimal(int index, Vector3 pos)
        {
            var root = GameRoot.Instance;
            var state = root.State.Farm.Animals[index];
            if (!root.Catalog.TryGetAnimal(state.TypeId, out var def)) return;
            switch (AnimalRules.GetPhase(state, root.NowMs))
            {
                case AnimalPhase.Hungry:
                    if (root.Execute(new FeedAnimalCommand(index)).IsSuccess)
                    {
                        FxPlayer.Play("plant", pos + Vector3.up * 0.3f);
                        SoundPlayer.Play("water");
                        string food = root.Catalog.TryGetProduct(def.FeedProductIds[0], out var f) ? f.Name : "comida";
                        FloatingTextLayer.Show(pos, $"-{def.FeedPerCycle} {food}  ·  {def.Name} comendo!", new Color(1f, 0.9f, 0.55f));
                    }
                    break;
                case AnimalPhase.Producing:
                    FloatingTextLayer.Show(pos, $"{def.Name}: pronto em {DisplayFormat.Duration(AnimalRules.RemainingMs(state, root.NowMs))}", Color.white);
                    break;
                case AnimalPhase.Ready:
                    if (root.Execute(new CollectAnimalCommand(index)).IsSuccess)
                    {
                        FxPlayer.Play("harvest", pos + Vector3.up * 0.4f);
                        SoundPlayer.Play("harvest");
                        string product = root.Catalog.TryGetProduct(def.ProductId, out var pdef) ? pdef.Name : def.ProductId;
                        FloatingTextLayer.Show(pos, $"+{def.Yield} {product}", new Color(0.65f, 1f, 0.45f));
                    }
                    break;
            }
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
