using UnityEngine;

namespace Game.Presentation.Player
{
    /// <summary>
    /// Câmera atrás da jogadora (estilo GTA): orbita com o arrasto da área direita, zoom por pinça,
    /// não atravessa paredes (spherecast) e segue suave.
    /// </summary>
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float height = 1.55f;
        [SerializeField] private float distance = 4.2f;
        [SerializeField] private float minDistance = 1.6f;
        [SerializeField] private float maxDistance = 9f;
        [SerializeField] private float minPitch = -10f;
        [SerializeField] private float maxPitch = 65f;
        [SerializeField] private float yaw;
        [SerializeField] private float pitch = 18f;
        [SerializeField] private float followSharpness = 14f;

        private Vector3 _focus;
        private float _currentDistance;

        public float Yaw { get => yaw; set => yaw = value; }
        public float Pitch { get => pitch; set => pitch = Mathf.Clamp(value, minPitch, maxPitch); }
        public float Distance { get => distance; set => distance = Mathf.Clamp(value, minDistance, maxDistance); }

        public void SetTarget(Transform t) { target = t; Snap(); }

        public void Snap()
        {
            if (target == null) return;
            _focus = target.position + Vector3.up * height;
            _currentDistance = distance;
            Apply();
        }

        private void Start()
        {
            if (target == null && PlayerController.Instance != null) target = PlayerController.Instance.transform;
            Snap();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            var look = LookArea.ConsumeLook();
            yaw += look.x;
            pitch = Mathf.Clamp(pitch + look.y, minPitch, maxPitch);
            distance = Mathf.Clamp(distance + LookArea.ConsumeZoom(), minDistance, maxDistance);

            var desired = target.position + Vector3.up * height;
            _focus = Vector3.Lerp(_focus, desired, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            Apply();
        }

        private void Apply()
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var dir = rot * Vector3.back;
            float wanted = distance;
            // Não atravessar parede/teto: encurta até o primeiro obstáculo (ignora a própria jogadora).
            if (Physics.SphereCast(_focus, 0.25f, dir, out var hit, distance, ~0, QueryTriggerInteraction.Ignore)
                && (target == null || !hit.transform.IsChildOf(target)))
                wanted = Mathf.Max(0.6f, hit.distance - 0.1f);
            _currentDistance = wanted < _currentDistance ? wanted : Mathf.Lerp(_currentDistance, wanted, 1f - Mathf.Exp(-6f * Time.deltaTime));
            transform.SetPositionAndRotation(_focus + dir * _currentDistance, rot);
        }
    }
}
