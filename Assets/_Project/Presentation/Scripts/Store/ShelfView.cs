using System.Collections.Generic;
using Game.Core.Store;
using Game.Presentation.Graybox;
using UnityEngine;

namespace Game.Presentation.Store
{
    /// <summary>
    /// Móvel da loja: modelo 3D do catálogo visual (banca, freezer, gôndola) com os produtos expostos em cima,
    /// em quantidade proporcional ao estoque. Sem modelo, cai no bloco colorido do gray box.
    /// </summary>
    public sealed class ShelfView : MonoBehaviour
    {
        private const int Columns = 5, Rows = 2;
        private const int MaxItemsShown = Columns * Rows;

        private readonly List<GameObject> _items = new List<GameObject>();
        private string _shownProduct;
        private int _shownCount = -1;
        private float _surfaceHeight = 1f;
        private Vector2 _surfaceSize = new Vector2(1.8f, 0.5f);
        private Transform _fill; // fallback gray box

        public int Index { get; private set; }

        public void Build(int index, string furnitureId, string categoryId)
        {
            Index = index;
            name = "Shelf_" + index;
            var visuals = GameRoot.Instance != null ? GameRoot.Instance.Visuals : null;
            var fv = visuals != null ? visuals.Furniture(furnitureId) : null;
            if (fv != null && fv.prefab != null)
            {
                var model = Instantiate(fv.prefab, transform);
                model.transform.localRotation = Quaternion.identity;
                _surfaceHeight = fv.surfaceHeight;
                _surfaceSize = fv.surfaceSize;
                if (model.GetComponent<Collider>() == null)
                {
                    var box = gameObject.AddComponent<BoxCollider>();
                    box.center = new Vector3(0, _surfaceHeight * 0.5f, 0);
                    box.size = new Vector3(2.4f, _surfaceHeight, 1f);
                }
                return;
            }

            var body = GrayboxMaterials.Primitive(PrimitiveType.Cube, "Body", transform,
                Color.Lerp(GrayboxMaterials.ColorFor(categoryId), Color.white, 0.5f), keepCollider: true);
            body.transform.localScale = new Vector3(2.2f, 1.0f, 0.9f);
            body.transform.localPosition = new Vector3(0, 0.5f, 0);
            _fill = GrayboxMaterials.Primitive(PrimitiveType.Cube, "Stock", transform, Color.gray).transform;
        }

        public void Render(ShelfState shelf, int capacity, long nowMs)
        {
            int units = StoreRules.Units(shelf, nowMs);
            float ratio = capacity > 0 ? Mathf.Clamp01(units / (float)capacity) : 0f;
            int show = units <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(ratio * MaxItemsShown), 1, MaxItemsShown);

            if (shelf.ProductId != _shownProduct)
            {
                foreach (var item in _items) Destroy(item);
                _items.Clear();
                _shownProduct = shelf.ProductId;
                _shownCount = -1;
            }
            if (show == _shownCount) return;
            _shownCount = show;

            var visuals = GameRoot.Instance != null ? GameRoot.Instance.Visuals : null;
            var prefab = visuals != null ? visuals.Product(shelf.ProductId) : null;
            if (prefab == null)
            {
                if (_fill == null) return;
                _fill.gameObject.SetActive(units > 0);
                if (shelf.ProductId != null)
                    _fill.GetComponent<Renderer>().sharedMaterial = GrayboxMaterials.Get(GrayboxMaterials.ColorFor(shelf.ProductId));
                float h = 0.05f + 0.6f * ratio;
                _fill.localScale = new Vector3(2.0f, h, 0.7f);
                _fill.localPosition = new Vector3(0, 1.0f + h * 0.5f, 0);
                return;
            }

            while (_items.Count < show)
            {
                int i = _items.Count;
                int col = i % Columns, row = i / Columns;
                float x = Mathf.Lerp(-_surfaceSize.x * 0.5f, _surfaceSize.x * 0.5f, (col + 0.5f) / Columns);
                float z = Mathf.Lerp(-_surfaceSize.y * 0.5f, _surfaceSize.y * 0.5f, (row + 0.5f) / Rows);
                var go = Instantiate(prefab, transform);
                go.transform.localPosition = new Vector3(x, _surfaceHeight - 0.02f, z);
                go.transform.localRotation = Quaternion.Euler(0, (i * 53) % 360, 0);
                _items.Add(go);
            }
            while (_items.Count > show)
            {
                Destroy(_items[_items.Count - 1]);
                _items.RemoveAt(_items.Count - 1);
            }
        }
    }
}
