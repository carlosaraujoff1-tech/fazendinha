using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Content
{
    /// <summary>
    /// Ponte entre IDs do jogo (dados) e os prefabs visuais. Gerado pelo Editor (menu Fazenda/Arte)
    /// a partir de Content/Data/visuals.json; o jogo referencia só os prefabs próprios, nunca o modelo do pack.
    /// </summary>
    [CreateAssetMenu(menuName = "Fazenda/Visual Catalog")]
    public sealed class VisualCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class CropVisual
        {
            public string cropId;
            public string productId;
            [Tooltip("Fases 1..4 de crescimento.")] public GameObject[] stages = new GameObject[0];
            [Tooltip("Produto colhido (prateleira/armazém).")] public GameObject product;
        }

        [Serializable]
        public sealed class DecorVisual
        {
            public string id;
            public GameObject prefab;
        }

        public List<CropVisual> crops = new List<CropVisual>();
        public List<DecorVisual> decor = new List<DecorVisual>();

        private Dictionary<string, CropVisual> _byCrop;
        private Dictionary<string, CropVisual> _byProduct;

        private void Index()
        {
            if (_byCrop != null) return;
            _byCrop = new Dictionary<string, CropVisual>();
            _byProduct = new Dictionary<string, CropVisual>();
            foreach (var c in crops)
            {
                if (c == null) continue;
                if (!string.IsNullOrEmpty(c.cropId)) _byCrop[c.cropId] = c;
                if (!string.IsNullOrEmpty(c.productId)) _byProduct[c.productId] = c;
            }
        }

        /// <summary>Prefab da fase (1-based) da cultura; null se não houver (cai no gray box).</summary>
        public GameObject CropStage(string cropId, int stage)
        {
            Index();
            if (cropId == null || !_byCrop.TryGetValue(cropId, out var c) || c.stages == null || c.stages.Length == 0) return null;
            int i = Mathf.Clamp(stage - 1, 0, c.stages.Length - 1);
            return c.stages[i];
        }

        public GameObject Product(string productId)
        {
            Index();
            return productId != null && _byProduct.TryGetValue(productId, out var c) ? c.product : null;
        }

        public GameObject Decor(string id)
        {
            foreach (var d in decor) if (d != null && d.id == id) return d.prefab;
            return null;
        }

        private void OnValidate() { _byCrop = null; _byProduct = null; }
    }
}
