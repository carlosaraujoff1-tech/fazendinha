using Game.Presentation.Player;
using UnityEngine;

namespace Game.Presentation.Farm
{
    /// <summary>Vendinha de sementes: perto dela, o botão de ação abre a loja de sementes.</summary>
    public sealed class SeedShopView : InteractableBehaviour
    {
        public override float InteractionRadius => 4.5f;
        public override string Prompt(PlayerContext player) => "Vendinha de sementes";
        public override void Interact(PlayerContext player) =>
            FindFirstObjectByType<Game.Presentation.UI.StoreMenus>()?.OpenSeedShop();
    }
}
