using Game.Presentation.Graybox;
using UnityEngine;

namespace Game.Presentation.Store
{
    /// <summary>Cliente gray box (cápsula) que anda até um alvo; barra de paciência quando está na fila.</summary>
    public sealed class CustomerView : MonoBehaviour
    {
        private const float Speed = 3.5f;
        private Transform _patienceBar;
        private Renderer _body;

        public int CustomerId { get; private set; }
        public Vector3 Target { get; set; }
        public bool Leaving { get; private set; }

        public void Build()
        {
            var body = GrayboxMaterials.Primitive(PrimitiveType.Capsule, "Body", transform, Color.white);
            body.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            body.transform.localPosition = new Vector3(0, 0.6f, 0);
            _body = body.GetComponent<Renderer>();
            var bar = GrayboxMaterials.Primitive(PrimitiveType.Cube, "Patience", transform, new Color(0.9f, 0.3f, 0.2f));
            _patienceBar = bar.transform;
        }

        public void Spawn(int customerId, string typeId, Vector3 door)
        {
            CustomerId = customerId;
            Leaving = false;
            name = "Customer_" + customerId;
            _body.sharedMaterial = GrayboxMaterials.Get(GrayboxMaterials.ColorFor(typeId));
            transform.position = door;
            Target = door;
            SetPatience(-1);
            gameObject.SetActive(true);
        }

        public void Leave(Vector3 door)
        {
            Leaving = true;
            Target = door;
            SetPatience(-1);
        }

        /// <summary>0..1 mostra a barra; negativo esconde.</summary>
        public void SetPatience(float ratio)
        {
            bool show = ratio >= 0;
            _patienceBar.gameObject.SetActive(show);
            if (!show) return;
            ratio = Mathf.Clamp01(ratio);
            _patienceBar.localScale = new Vector3(0.8f * ratio, 0.08f, 0.08f);
            _patienceBar.localPosition = new Vector3(0, 1.55f, 0);
        }

        /// <summary>Retorna true quando chegou ao alvo.</summary>
        public bool Tick(float dt)
        {
            var pos = transform.position;
            var next = Vector3.MoveTowards(pos, Target, Speed * dt);
            transform.position = next;
            return (next - Target).sqrMagnitude < 0.0004f;
        }
    }
}
