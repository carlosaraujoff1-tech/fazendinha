using Game.Core.Store;
using Game.Presentation.Graybox;
using UnityEngine;

namespace Game.Presentation.Store
{
    /// <summary>Móvel gray box: corpo na cor da categoria e uma "pilha" que mostra o quanto está cheio.</summary>
    public sealed class ShelfView : MonoBehaviour
    {
        private Transform _fill;
        private Renderer _fillRenderer;
        private string _shownProduct;
        private float _shownRatio = -1f;
        private readonly System.Collections.Generic.List<GameObject> _items = new System.Collections.Generic.List<GameObject>();
        private const int MaxItemsShown = 8;

        public int Index { get; private set; }

        public void Build(int index, string categoryId)
        {
            Index = index;
            name = "Shelf_" + index;
            var body = GrayboxMaterials.Primitive(PrimitiveType.Cube, "Body", transform,
                Color.Lerp(GrayboxMaterials.ColorFor(categoryId), Color.white, 0.5f), keepCollider: true);
            body.transform.localScale = new Vector3(2.2f, 1.0f, 0.9f);
            body.transform.localPosition = new Vector3(0, 0.5f, 0);

            var fill = GrayboxMaterials.Primitive(PrimitiveType.Cube, "Stock", transform, Color.gray);
            _fill = fill.transform;
            _fillRenderer = fill.GetComponent<Renderer>();
        }

        public void Render(ShelfState shelf, int capacity, long nowMs)
        {
            int units = StoreRules.Units(shelf, nowMs);
            float ratio = capacity > 0 ? Mathf.Clamp01(units / (float)capacity) : 0f;
            if (shelf.ProductId != _shownProduct && shelf.ProductId != null)
            {
                _fillRenderer.sharedMaterial = GrayboxMaterials.Get(GrayboxMaterials.ColorFor(shelf.ProductId));
                _shownProduct = shelf.ProductId;
                _shownRatio = -1f;
                foreach (var item in _items) Destroy(item);
                _items.Clear();
            }
            if (Mathf.Approximately(ratio, _shownRatio)) return;
            _shownRatio = ratio;

            // Com modelo 3D do produto: mostra até 8 unidades em fila em cima do móvel (proporcional ao estoque).
            var visuals = GameRoot.Instance != null ? GameRoot.Instance.Visuals : null;
            var prefab = visuals != null ? visuals.Product(shelf.ProductId) : null;
            if (prefab != null)
            {
                _fill.gameObject.SetActive(false);
                int show = units <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(ratio * MaxItemsShown), 1, MaxItemsShown);
                while (_items.Count < show)
                {
                    int i = _items.Count;
                    var go = Instantiate(prefab, transform);
                    float x = -0.85f + (i % 4) * 0.57f;
                    float z = i < 4 ? -0.18f : 0.18f;
                    go.transform.localPosition = new Vector3(x, 1.0f, z);
                    go.transform.localRotation = Quaternion.Euler(0, i * 37f, 0);
                    _items.Add(go);
                }
                while (_items.Count > show)
                {
                    Destroy(_items[_items.Count - 1]);
                    _items.RemoveAt(_items.Count - 1);
                }
                return;
            }
            _fill.gameObject.SetActive(units > 0);
            float h = 0.05f + 0.6f * ratio;
            _fill.localScale = new Vector3(2.0f, h, 0.7f);
            _fill.localPosition = new Vector3(0, 1.0f + h * 0.5f, 0);
        }
    }
}
