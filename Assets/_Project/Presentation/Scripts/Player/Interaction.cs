using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Player
{
    /// <summary>
    /// Contexto de quem interage. Hoje só a jogadora e o que ela segura (sempre vazio na versão com botão de ação);
    /// a atualização "física" (carregar caixa até a prateleira) vai usar <see cref="HeldItemId"/>.
    /// </summary>
    public sealed class PlayerContext
    {
        public Transform Transform;
        public PlayerController Controller;
        /// <summary>Item nas mãos (futuro: caixa de produto). Null = mãos livres.</summary>
        public string HeldItemId;
        public int HeldUnits;
    }

    /// <summary>Algo no mundo com que a jogadora pode interagir chegando perto (botão de ação).</summary>
    public interface IInteractable
    {
        /// <summary>Ponto de referência para a distância.</summary>
        Vector3 InteractionPoint { get; }
        /// <summary>Raio (m) dentro do qual a ação aparece.</summary>
        float InteractionRadius { get; }
        /// <summary>Texto do botão ("Plantar", "Colher 4 Alface"...). Null/vazio = nada a fazer agora.</summary>
        string Prompt(PlayerContext player);
        void Interact(PlayerContext player);
    }

    /// <summary>Registro global de interagíveis ativos (evita FindObjectsOfType a cada quadro).</summary>
    public static class Interactables
    {
        private static readonly List<IInteractable> All = new List<IInteractable>();
        public static IReadOnlyList<IInteractable> Active => All;
        public static void Register(IInteractable i) { if (!All.Contains(i)) All.Add(i); }
        public static void Unregister(IInteractable i) => All.Remove(i);
    }

    /// <summary>Base MonoBehaviour que se registra sozinha.</summary>
    public abstract class InteractableBehaviour : MonoBehaviour, IInteractable
    {
        [SerializeField] private float radius = 2.2f;
        public virtual Vector3 InteractionPoint => transform.position;
        public virtual float InteractionRadius => radius;
        public void SetRadius(float r) => radius = r;
        public abstract string Prompt(PlayerContext player);
        public abstract void Interact(PlayerContext player);
        protected virtual void OnEnable() => Interactables.Register(this);
        protected virtual void OnDisable() => Interactables.Unregister(this);
    }
}
