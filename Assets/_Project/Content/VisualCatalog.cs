using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Content
{
    /// <summary>
    /// Ponte entre IDs do jogo (dados) e tudo que é visual/sonoro: prefabs próprios (que envolvem os modelos dos
    /// packs), materiais, efeitos, sons e a "pele" da UI. Gerado pelo Editor (menu Fazenda/Arte) a partir de
    /// Content/Data/visuals.json. O jogo nunca referencia o arquivo do pack direto. Tudo é opcional: o que faltar
    /// cai no gray box.
    /// </summary>
    [CreateAssetMenu(menuName = "Fazenda/Visual Catalog")]
    public sealed class VisualCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class CropVisual
        {
            public string cropId;
            [Tooltip("Fases 1..4 de crescimento.")] public GameObject[] stages = new GameObject[0];
        }

        [Serializable]
        public sealed class IdPrefab
        {
            public string id;
            public GameObject prefab;
        }

        [Serializable]
        public sealed class FurnitureVisual
        {
            public string furnitureId;
            public GameObject prefab;
            [Tooltip("Altura (m) da superfície onde os produtos ficam expostos.")] public float surfaceHeight = 1f;
            [Tooltip("Tamanho (m) em X e Z da superfície útil.")] public Vector2 surfaceSize = new Vector2(1.8f, 0.6f);
        }

        [Serializable]
        public sealed class IdSound
        {
            public string id;
            public AudioClip clip;
            [Range(0, 1)] public float volume = 0.8f;
        }

        [Header("Fazenda")]
        public List<CropVisual> crops = new List<CropVisual>();
        public Material soil;
        public Material soilWet;

        [Header("Produtos e loja")]
        public List<IdPrefab> products = new List<IdPrefab>();
        public List<FurnitureVisual> furniture = new List<FurnitureVisual>();
        public GameObject cashRegister;
        [Tooltip("Personagens animados (Animator com speed/pick/yes/no).")] public List<GameObject> customers = new List<GameObject>();
        public GameObject employee;

        [Header("Cenário e animais")]
        public List<IdPrefab> decor = new List<IdPrefab>();
        [Tooltip("Modelo por tipo de animal do jogo (galinha, vaca, ovelha).")] public List<IdPrefab> animals = new List<IdPrefab>();

        [Header("Efeitos e som")]
        public List<IdPrefab> effects = new List<IdPrefab>();
        public List<IdSound> sounds = new List<IdSound>();

        [Header("Materiais base (garantem os shaders no build)")]
        public Material baseLit;
        public Material baseUnlit;

        [Header("UI")]
        public Font font;
        public Sprite buttonGreen, buttonYellow, buttonRed, buttonBlue, buttonGrey;
        public Sprite panel;

        private Dictionary<string, CropVisual> _crops;
        private Dictionary<string, GameObject> _products, _decor, _effects, _animals;
        private Dictionary<string, FurnitureVisual> _furniture;
        private Dictionary<string, IdSound> _sounds;

        private void Index()
        {
            if (_crops != null) return;
            _crops = new Dictionary<string, CropVisual>();
            foreach (var c in crops) if (c != null && !string.IsNullOrEmpty(c.cropId)) _crops[c.cropId] = c;
            _products = ToMap(products);
            _decor = ToMap(decor);
            _effects = ToMap(effects);
            _animals = ToMap(animals);
            _furniture = new Dictionary<string, FurnitureVisual>();
            foreach (var f in furniture) if (f != null && !string.IsNullOrEmpty(f.furnitureId)) _furniture[f.furnitureId] = f;
            _sounds = new Dictionary<string, IdSound>();
            foreach (var s in sounds) if (s != null && !string.IsNullOrEmpty(s.id)) _sounds[s.id] = s;
        }

        private static Dictionary<string, GameObject> ToMap(List<IdPrefab> list)
        {
            var map = new Dictionary<string, GameObject>();
            foreach (var e in list) if (e != null && !string.IsNullOrEmpty(e.id) && e.prefab != null) map[e.id] = e.prefab;
            return map;
        }

        /// <summary>Prefab da fase (1-based) da cultura; null se não houver.</summary>
        public GameObject CropStage(string cropId, int stage)
        {
            Index();
            if (cropId == null || !_crops.TryGetValue(cropId, out var c) || c.stages == null || c.stages.Length == 0) return null;
            return c.stages[Mathf.Clamp(stage - 1, 0, c.stages.Length - 1)];
        }

        public GameObject Product(string productId) { Index(); return Get(_products, productId); }
        public GameObject Decor(string id) { Index(); return Get(_decor, id); }
        public GameObject Effect(string id) { Index(); return Get(_effects, id); }
        public GameObject Animal(string animalTypeId) { Index(); return Get(_animals, animalTypeId); }

        public FurnitureVisual Furniture(string furnitureId)
        {
            Index();
            return furnitureId != null && _furniture.TryGetValue(furnitureId, out var f) ? f : null;
        }

        public IdSound Sound(string id)
        {
            Index();
            return id != null && _sounds.TryGetValue(id, out var s) ? s : null;
        }

        public GameObject Customer(int customerId) =>
            customers.Count == 0 ? null : customers[Mathf.Abs(customerId * 7919) % customers.Count];

        private static GameObject Get(Dictionary<string, GameObject> map, string id) =>
            id != null && map.TryGetValue(id, out var go) ? go : null;

        private void OnValidate() { _crops = null; }
    }
}
