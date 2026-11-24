using Game.Presentation.Graybox;
using UnityEngine;

namespace Game.Presentation.Store
{
    /// <summary>
    /// Cliente: personagem animado (Kenney) que anda até o alvo virando para a direção do movimento.
    /// Animator com "speed" (parado/andando) e gatilhos "pick", "yes", "no". Barra de paciência na fila.
    /// Sem personagem no catálogo, usa uma cápsula.
    /// </summary>
    public sealed class CustomerView : MonoBehaviour
    {
        private const float Speed = 2.2f;
        private static readonly int SpeedParam = Animator.StringToHash("speed");

        private Transform _patienceBar;
        private Transform _patienceBack;
        private Renderer _patienceRenderer;
        private GameObject _body;
        private Animator _animator;
        private float _currentSpeed;

        public int CustomerId { get; private set; }
        public Vector3 Target { get; set; }
        public bool Leaving { get; private set; }

        public void Build() { }

        public void Spawn(int customerId, string typeId, Vector3 door)
        {
            CustomerId = customerId;
            Leaving = false;
            name = "Customer_" + customerId;
            if (_body != null) Destroy(_body);

            var visuals = GameRoot.Instance != null ? GameRoot.Instance.Visuals : null;
            var prefab = visuals != null ? visuals.Customer(customerId) : null;
            if (prefab != null)
            {
                _body = Instantiate(prefab, transform);
                _animator = _body.GetComponentInChildren<Animator>();
            }
            else
            {
                _body = GrayboxMaterials.Primitive(PrimitiveType.Capsule, "Body", transform, GrayboxMaterials.ColorFor(typeId));
                _body.transform.localScale = Vector3.one * 0.6f;
                _body.transform.localPosition = new Vector3(0, 0.6f, 0);
                _animator = null;
            }

            if (_patienceBar == null)
            {
                _patienceBack = GrayboxMaterials.Primitive(PrimitiveType.Cube, "PatienceBack", transform, new Color(0.15f, 0.15f, 0.15f)).transform;
                _patienceBar = GrayboxMaterials.Primitive(PrimitiveType.Cube, "Patience", transform, new Color(0.3f, 0.9f, 0.35f)).transform;
                _patienceRenderer = _patienceBar.GetComponent<Renderer>();
            }

            transform.position = door;
            Target = door;
            SetPatience(-1);
            gameObject.SetActive(true);
        }

        public void Trigger(string trigger)
        {
            if (_animator != null) _animator.SetTrigger(trigger);
        }

        public void Leave(Vector3 door, bool happy)
        {
            Leaving = true;
            Target = door;
            SetPatience(-1);
            Trigger(happy ? "yes" : "no");
        }

        /// <summary>0..1 mostra a barra (verde → vermelho); negativo esconde.</summary>
        public void SetPatience(float ratio)
        {
            if (_patienceBar == null) return;
            bool show = ratio >= 0;
            _patienceBar.gameObject.SetActive(show);
            _patienceBack.gameObject.SetActive(show);
            if (!show) return;
            ratio = Mathf.Clamp01(ratio);
            _patienceBack.localScale = new Vector3(0.84f, 0.1f, 0.06f);
            _patienceBack.localPosition = new Vector3(0, 1.65f, 0);
            _patienceBar.localScale = new Vector3(0.8f * ratio, 0.07f, 0.08f);
            _patienceBar.localPosition = new Vector3(-0.4f * (1f - ratio), 1.65f, 0);
            _patienceRenderer.sharedMaterial = GrayboxMaterials.Get(Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.3f, 0.9f, 0.35f), ratio));
        }

        /// <summary>Retorna true quando chegou ao alvo.</summary>
        public bool Tick(float dt)
        {
            var pos = transform.position;
            var to = Target - pos;
            to.y = 0;
            float dist = to.magnitude;
            bool moving = dist > 0.05f;
            if (moving)
            {
                transform.position = Vector3.MoveTowards(pos, Target, Speed * dt);
                var look = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 10f * dt);
            }
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, moving ? 1f : 0f, dt * 6f);
            if (_animator != null) _animator.SetFloat(SpeedParam, _currentSpeed);
            if (_patienceBar != null && _patienceBar.gameObject.activeSelf && Camera.main != null)
            {
                // barra sempre virada para a câmera
                var camFwd = Camera.main.transform.forward;
                _patienceBar.rotation = _patienceBack.rotation = Quaternion.LookRotation(camFwd, Vector3.up);
            }
            return !moving;
        }
    }
}
