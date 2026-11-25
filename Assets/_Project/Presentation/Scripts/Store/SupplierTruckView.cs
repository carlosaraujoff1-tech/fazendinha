using Game.Presentation.Player;
using UnityEngine;

namespace Game.Presentation.Store
{
    /// <summary>Caminhão do fornecedor estacionado no mercadinho: abre o menu do fornecedor.</summary>
    public sealed class SupplierTruckView : InteractableBehaviour
    {
        public override float InteractionRadius => 4f;
        public override string Prompt(PlayerContext player) => "Fornecedor (pedir mercadoria)";
        public override void Interact(PlayerContext player) =>
            FindFirstObjectByType<Game.Presentation.UI.StoreMenus>()?.OpenSupplier();
    }
}
