using Game.Presentation.Graybox;
using UnityEngine;

namespace Game.Presentation.Player
{
    /// <summary>
    /// Jogadora em terceira pessoa: anda relativo à câmera (joystick), corre com o joystick no fim do curso,
    /// vira para a direção do movimento. Modelo vem do catálogo visual (Mixamo quando houver).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 1.9f;
        [SerializeField] private float runSpeed = 4.6f;
        [SerializeField] private float turnSpeed = 12f;
        [SerializeField] private float gravity = 20f;

        private static readonly int SpeedParam = Animator.StringToHash("speed");
        private CharacterController _cc;
        private Animator _animator;
        private Transform _camera;
        private float _vertical;
        private float _animSpeed;

        public static PlayerController Instance { get; private set; }
        public PlayerContext Context { get; private set; }
        public float CurrentSpeed { get; private set; }

        private void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
            _cc.height = 1.7f;
            _cc.radius = 0.3f;
            _cc.center = new Vector3(0, 0.88f, 0);
            _cc.stepOffset = 0.35f;
            _cc.slopeLimit = 50f;
            Context = new PlayerContext { Transform = transform, Controller = this };
        }

        private void Start()
        {
            var visuals = GameRoot.Instance != null ? GameRoot.Instance.Visuals : null;
            var prefab = visuals != null ? visuals.player : null;
            if (prefab != null)
            {
                var body = Instantiate(prefab, transform);
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.identity;
                _animator = body.GetComponentInChildren<Animator>();
            }
            else
            {
                var body = GrayboxMaterials.Primitive(PrimitiveType.Capsule, "Body", transform, new Color(0.85f, 0.4f, 0.5f));
                body.transform.localPosition = new Vector3(0, 0.88f, 0);
                body.transform.localScale = new Vector3(0.6f, 0.85f, 0.6f);
            }
            if (Camera.main != null) _camera = Camera.main.transform;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Teleporta (viagem rápida fazenda ⇄ mercadinho).</summary>
        public void TeleportTo(Vector3 position, float yaw)
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            _cc.enabled = true;
        }

        public void PlayAction(string trigger)
        {
            if (_animator != null) _animator.SetTrigger(trigger);
        }

        private void Update()
        {
            var input = VirtualJoystick.Read();
            float mag = Mathf.Clamp01(input.magnitude);
            Vector3 move = Vector3.zero;
            if (mag > 0.08f && _camera != null)
            {
                var fwd = _camera.forward; fwd.y = 0; fwd.Normalize();
                var right = _camera.right; right.y = 0; right.Normalize();
                move = (fwd * input.y + right * input.x).normalized;
                float speed = mag > 0.85f ? runSpeed : Mathf.Lerp(0.6f, walkSpeed, mag / 0.85f);
                move *= speed;
                var look = Quaternion.LookRotation(new Vector3(move.x, 0, move.z), Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
            }

            _vertical = _cc.isGrounded ? -1f : _vertical - gravity * Time.deltaTime;
            _cc.Move((move + Vector3.up * _vertical) * Time.deltaTime);

            var flat = _cc.velocity; flat.y = 0;
            CurrentSpeed = flat.magnitude;
            // speed do Animator: 0 parado, ~0.5 andando, 1 correndo
            float target = CurrentSpeed < 0.1f ? 0f : CurrentSpeed < walkSpeed + 0.3f ? 0.5f : 1f;
            _animSpeed = Mathf.MoveTowards(_animSpeed, target, Time.deltaTime * 4f);
            if (_animator != null) _animator.SetFloat(SpeedParam, _animSpeed);
        }
    }
}
