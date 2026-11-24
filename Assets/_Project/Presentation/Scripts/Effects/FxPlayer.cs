using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Effects
{
    /// <summary>
    /// Toca efeitos de partícula do catálogo visual por ID (pool por tipo, sem alocar a cada uso).
    /// Os prefabs desligam sozinhos ao terminar (stopAction = Disable) e voltam para o pool.
    /// </summary>
    public sealed class FxPlayer : MonoBehaviour
    {
        private static FxPlayer _instance;
        private readonly Dictionary<string, List<GameObject>> _pool = new Dictionary<string, List<GameObject>>();

        private void Awake() => _instance = this;
        private void OnDestroy() { if (_instance == this) _instance = null; }

        public static void Play(string id, Vector3 position)
        {
            if (_instance != null) _instance.PlayInternal(id, position);
        }

        /// <summary>Instância contínua (ex.: brilho de "pronto"); quem chama destrói/desliga.</summary>
        public static GameObject Spawn(string id, Transform parent, Vector3 localPosition)
        {
            var visuals = GameRoot.Instance != null ? GameRoot.Instance.Visuals : null;
            var prefab = visuals != null ? visuals.Effect(id) : null;
            if (prefab == null) return null;
            var go = Instantiate(prefab, parent);
            go.transform.localPosition = localPosition;
            return go;
        }

        private void PlayInternal(string id, Vector3 position)
        {
            var visuals = GameRoot.Instance != null ? GameRoot.Instance.Visuals : null;
            var prefab = visuals != null ? visuals.Effect(id) : null;
            if (prefab == null) return;

            if (!_pool.TryGetValue(id, out var list))
            {
                list = new List<GameObject>();
                _pool[id] = list;
            }
            GameObject fx = null;
            foreach (var candidate in list)
                if (candidate != null && !candidate.activeSelf) { fx = candidate; break; }
            if (fx == null)
            {
                fx = Instantiate(prefab, transform);
                list.Add(fx);
            }
            fx.transform.position = position + prefab.transform.localPosition;
            fx.SetActive(true);
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear(true);
                ps.Play(true);
            }
        }
    }
}
