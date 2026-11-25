using UnityEngine;

namespace Game.Presentation.Player
{
    /// <summary>
    /// Procura o interagível mais próximo (priorizando o que está à frente) e expõe a ação para o botão.
    /// </summary>
    public sealed class InteractionSystem : MonoBehaviour
    {
        [SerializeField] private float refreshSeconds = 0.12f;

        private float _next;
        public static IInteractable Current { get; private set; }
        public static string CurrentPrompt { get; private set; }

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + refreshSeconds;
            var player = PlayerController.Instance;
            if (player == null) { Current = null; CurrentPrompt = null; return; }

            Vector3 pos = player.transform.position;
            Vector3 fwd = player.transform.forward;
            IInteractable best = null;
            string bestPrompt = null;
            float bestScore = float.MaxValue;
            foreach (var i in Interactables.Active)
            {
                var p = i.InteractionPoint;
                var to = p - pos; to.y = 0;
                float d = to.magnitude;
                if (d > i.InteractionRadius) continue;
                string prompt = i.Prompt(player.Context);
                if (string.IsNullOrEmpty(prompt)) continue;
                float facing = d > 0.01f ? Vector3.Dot(fwd, to / d) : 1f;   // -1..1
                float score = d - facing * 0.8f;                             // perto e de frente ganham
                if (score < bestScore) { bestScore = score; best = i; bestPrompt = prompt; }
            }
            Current = best;
            CurrentPrompt = bestPrompt;
        }

        /// <summary>Executa a ação atual (botão de ação ou tecla E).</summary>
        public static void InteractNow()
        {
            var player = PlayerController.Instance;
            if (Current == null || player == null) return;
            var target = Current.InteractionPoint - player.transform.position; target.y = 0;
            if (target.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(target.normalized);
            player.PlayAction("pick");
            Current.Interact(player.Context);
        }
    }
}
