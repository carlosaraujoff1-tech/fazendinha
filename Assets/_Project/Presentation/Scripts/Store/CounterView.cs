using Game.Core.Store;
using Game.Presentation.Player;
using UnityEngine;

namespace Game.Presentation.Store
{
    /// <summary>Caixa registradora: perto dela, o botão de ação passa o próximo item do cliente da frente.</summary>
    public sealed class CounterView : InteractableBehaviour
    {
        public override float InteractionRadius => 2.4f;

        public override string Prompt(PlayerContext player)
        {
            var store = GameRoot.Instance.State.Store;
            var front = StoreRules.FrontOfQueue(store);
            if (front == null) return null;
            int queued = 0;
            foreach (var c in store.Customers) if (c.Phase == CustomerPhase.Queued) queued++;
            return $"Passar item {front.ScannedLines + 1}/{front.Cart.Count}  (fila {queued})";
        }

        public override void Interact(PlayerContext player) => StoreInteraction.Scan();
    }
}
