using Game.Core.Animals;
using Game.Core.State;
using Game.Presentation.Effects;
using Game.Presentation.Graybox;
using UnityEngine;

namespace Game.Presentation.Farm
{
    /// <summary>Um animal: modelo animado que passeia devagar perto de casa + indicador de estado.</summary>
    public sealed class AnimalView : MonoBehaviour
    {
        private Vector3 _home;
        private Vector3 _target;
        private float _nextWander;
        private GameObject _body;
        private GameObject _icon;
        private GameObject _readyFx;
        private AnimalPhase _shownPhase = (AnimalPhase)(-1);
        private float _iconScale = 1f;
        private string _typeId;

        public int Index { get; private set; }

        public void Build(int index, string typeId, Vector3 home)
        {
            Index = index;
            _typeId = typeId;
            _home = home;
            transform.position = home;
            transform.rotation = Quaternion.Euler(0, (index * 83) % 360, 0);
            _target = home;

            var visuals = GameRoot.Instance.Visuals;
            var prefab = visuals != null ? visuals.Animal(typeId) : null;
            if (prefab != null) _body = Instantiate(prefab, transform);
            else
            {
                _body = GrayboxMaterials.Primitive(PrimitiveType.Capsule, "Body", transform, GrayboxMaterials.ColorFor(typeId), keepCollider: true);
                _body.transform.localPosition = new Vector3(0, 0.5f, 0);
                _body.transform.localScale = new Vector3(0.6f, 0.5f, 0.9f);
            }
            if (GetComponentInChildren<Collider>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 0.5f, 0);
                box.size = new Vector3(1f, 1f, 1.4f);
            }
        }

        public void Render(AnimalState state, long nowMs)
        {
            var phase = AnimalRules.GetPhase(state, nowMs);
            if (phase == _shownPhase) return;
            _shownPhase = phase;
            if (_icon != null) { Destroy(_icon); _icon = null; }
            if (_readyFx != null) { Destroy(_readyFx); _readyFx = null; }

            var root = GameRoot.Instance;
            if (!root.Catalog.TryGetAnimal(_typeId, out var def)) return;
            var visuals = root.Visuals;
            string iconProduct = phase == AnimalPhase.Hungry ? def.FeedProductIds[0] : phase == AnimalPhase.Ready ? def.ProductId : null;
            if (iconProduct == null) return;

            var prefab = visuals != null ? visuals.Product(iconProduct) : null;
            _icon = prefab != null ? Instantiate(prefab, transform)
                : GrayboxMaterials.Primitive(PrimitiveType.Sphere, "Icon", transform, phase == AnimalPhase.Ready ? new Color(1f, 0.85f, 0.2f) : Color.white);
            _iconScale = prefab != null ? 1.6f : 0.3f;
            _icon.transform.localScale = Vector3.one * _iconScale;
            if (phase == AnimalPhase.Ready) _readyFx = FxPlayer.Spawn("ready", transform, Vector3.zero);
        }

        private void Update()
        {
            float height = _typeId == "galinha" ? 1.25f : 2.0f;
            if (_icon != null)
            {
                bool hungry = _shownPhase == AnimalPhase.Hungry;
                float pulse = hungry ? 1f + Mathf.Sin(Time.time * 5f) * 0.12f : 1f;
                _icon.transform.localScale = Vector3.one * _iconScale * pulse;
                _icon.transform.position = transform.position + Vector3.up * (height + Mathf.Sin(Time.time * 3f + Index) * 0.1f);
                _icon.transform.Rotate(0, 90f * Time.deltaTime, 0, Space.World);
            }

            // Passeio lento perto de casa (só quando não está com fome: com fome fica parado esperando)
            if (Time.time >= _nextWander)
            {
                _nextWander = Time.time + UnityEngine.Random.Range(3f, 7f);
                _target = _shownPhase == AnimalPhase.Hungry ? transform.position
                    : _home + new Vector3(UnityEngine.Random.Range(-0.9f, 0.9f), 0, UnityEngine.Random.Range(-0.9f, 0.9f));
            }
            var to = _target - transform.position;
            to.y = 0;
            if (to.sqrMagnitude > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, _target, 0.35f * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), 3f * Time.deltaTime);
            }
        }
    }
}
