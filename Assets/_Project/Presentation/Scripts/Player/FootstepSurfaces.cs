using UnityEngine;

namespace Game.Presentation.Player
{
    /// <summary>
    /// Áreas de piso duro (estrada, calçada, loja) para o som de passo; fora delas é grama/terra.
    /// Preenchido pelo gerador de cena.
    /// </summary>
    public sealed class FootstepSurfaces : MonoBehaviour
    {
        [SerializeField] private Rect[] hardAreas = new Rect[0];

        private static FootstepSurfaces _instance;

        private void Awake() => _instance = this;
        private void OnDestroy() { if (_instance == this) _instance = null; }

        public static bool IsHard(Vector3 position)
        {
            if (_instance == null) return false;
            var p = new Vector2(position.x, position.z);
            foreach (var r in _instance.hardAreas) if (r.Contains(p)) return true;
            return false;
        }
    }
}
