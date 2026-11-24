using System;
using System.IO;
using System.Linq;
using Game.Content;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Editor.Art
{
    /// <summary>
    /// Gera os prefabs próprios que envolvem os modelos dos packs (escala e pivô normalizados) e o
    /// <see cref="VisualCatalog"/>. Fonte: Content/Data/visuals.json. Idempotente; menu "Fazenda/Arte/Gerar prefabs".
    /// </summary>
    public static class VisualCatalogGenerator
    {
        public const string MappingPath = "Assets/_Project/Content/Data/visuals.json";
        public const string OutDir = "Assets/_Project/Presentation/Prefabs/Generated";
        public const string CatalogPath = OutDir + "/VisualCatalog.asset";
        private const string ThirdParty = "Assets/ThirdParty/";

        // Tamanhos-alvo em metros (1 unidade = 1 m; parcela = 1,6 m).
        private const float CropHeight = 1.1f, CropFootprint = 1.3f;
        private const float ProductHeight = 0.35f, ProductFootprint = 0.45f;

#pragma warning disable 0649 // preenchidos pelo JsonUtility
        [Serializable] private sealed class Mapping { public CropEntry[] crops; public DecorEntry[] decor; }
        [Serializable] private sealed class CropEntry { public string crop; public string stages; public string product; }
        [Serializable] private sealed class DecorEntry { public string id; public string model; public float height; public string animation; }
#pragma warning restore 0649

        [MenuItem("Fazenda/Arte/Gerar prefabs e catálogo visual")]
        public static VisualCatalog Generate()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(MappingPath);
            if (json == null) throw new FileNotFoundException(MappingPath);
            var mapping = JsonUtility.FromJson<Mapping>(json.text);
            Directory.CreateDirectory(OutDir + "/Crops");
            Directory.CreateDirectory(OutDir + "/Products");
            Directory.CreateDirectory(OutDir + "/Decor");
            Directory.CreateDirectory(OutDir + "/Animators");

            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<VisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.crops.Clear();
            catalog.decor.Clear();

            foreach (var entry in mapping.crops ?? new CropEntry[0])
            {
                var models = Enumerable.Range(1, 4).Select(i => LoadModel(string.Format(entry.stages, i))).ToArray();
                if (models.Any(m => m == null)) { Debug.LogWarning($"[Arte] Faltam fases para '{entry.crop}'."); continue; }

                // A escala vem da fase 4 (maior) e vale para todas: a planta "cresce" de verdade.
                float scale = FitScale(models[3], CropHeight, CropFootprint);
                var visual = new VisualCatalog.CropVisual { cropId = entry.crop, productId = entry.crop, stages = new GameObject[4] };
                for (int i = 0; i < 4; i++)
                    visual.stages[i] = SaveWrapper(models[i], scale, $"{OutDir}/Crops/{entry.crop}_{i + 1}.prefab", castShadows: true);

                var productModel = LoadModel(entry.product);
                if (productModel != null)
                    visual.product = SaveWrapper(productModel, FitScale(productModel, ProductHeight, ProductFootprint),
                        $"{OutDir}/Products/{entry.crop}.prefab", castShadows: false);
                catalog.crops.Add(visual);
            }

            foreach (var entry in mapping.decor ?? new DecorEntry[0])
            {
                var model = LoadModel(entry.model);
                if (model == null) { Debug.LogWarning($"[Arte] Modelo não encontrado: {entry.model}"); continue; }
                float scale = FitScale(model, entry.height, float.MaxValue);
                var prefab = SaveWrapper(model, scale, $"{OutDir}/Decor/{entry.id}.prefab", castShadows: entry.height >= 0.8f,
                    animationPath: string.IsNullOrEmpty(entry.animation) ? null : entry.model, animation: entry.animation, id: entry.id);
                catalog.decor.Add(new VisualCatalog.DecorVisual { id = entry.id, prefab = prefab });
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Arte] Catálogo visual: {catalog.crops.Count} culturas, {catalog.decor.Count} decorações.");
            return catalog;
        }

        private static GameObject LoadModel(string relative) =>
            string.IsNullOrEmpty(relative) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(ThirdParty + relative);

        private static Bounds MeasureBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Escala uniforme para caber na altura e na base pedidas.</summary>
        private static float FitScale(GameObject model, float targetHeight, float maxFootprint)
        {
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                var b = MeasureBounds(temp);
                float s = b.size.y > 1e-4f ? targetHeight / b.size.y : 1f;
                float footprint = Mathf.Max(b.size.x, b.size.z) * s;
                if (footprint > maxFootprint) s *= maxFootprint / footprint;
                return s;
            }
            finally { Object.DestroyImmediate(temp); }
        }

        /// <summary>Prefab próprio: raiz vazia com o modelo do pack como filho, pivô no chão e centralizado.</summary>
        private static GameObject SaveWrapper(GameObject model, float scale, string path, bool castShadows,
            string animationPath = null, string animation = null, string id = null)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(root.transform, false);
            instance.transform.localScale *= scale;
            var b = MeasureBounds(instance);
            instance.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);

            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
            }

            if (animationPath != null) SetupAnimator(instance, animationPath, animation, id);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SetupAnimator(GameObject instance, string modelRelative, string animation, string id)
        {
            string modelPath = ThirdParty + modelRelative;
            var clip = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__") && c.name.EndsWith(animation, StringComparison.OrdinalIgnoreCase));
            if (clip == null) { Debug.LogWarning($"[Arte] Animação '{animation}' não encontrada em {modelPath}."); return; }

            string controllerPath = $"{OutDir}/Animators/{id}.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);

            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
        }
    }
}
