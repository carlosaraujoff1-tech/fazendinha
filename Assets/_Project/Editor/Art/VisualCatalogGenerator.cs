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
    /// Gera, a partir de Content/Data/visuals.json: prefabs próprios que envolvem os modelos dos packs (escala e
    /// pivô normalizados), materiais (terra, base, partículas), animator dos personagens, prefabs de efeitos e o
    /// <see cref="VisualCatalog"/>. Idempotente. Menu "Fazenda/Arte/Gerar prefabs e catálogo visual".
    /// </summary>
    public static class VisualCatalogGenerator
    {
        public const string MappingPath = "Assets/_Project/Content/Data/visuals.json";
        public const string OutDir = "Assets/_Project/Presentation/Prefabs/Generated";
        public const string MaterialsDir = "Assets/_Project/Presentation/Materials";
        public const string TexturesDir = "Assets/_Project/Presentation/Textures";
        // Em Resources: sempre vai no build e pode ser carregado mesmo se a referência da cena falhar.
        public const string CatalogPath = "Assets/_Project/Presentation/Resources/VisualCatalog.asset";
        private const string ThirdParty = "Assets/ThirdParty/";

        // Tamanhos-alvo em metros (1 unidade = 1 m; parcela = 1,6 m).
        // Fração da altura real da cultura em cada fase; o canteiro (3,2 m) recebe density × density plantas.
        private static readonly float[] StageFractions = { 0.22f, 0.45f, 0.72f, 1f };
        public const float PlotSize = 3.2f;

#pragma warning disable 0649 // preenchidos pelo JsonUtility
        [Serializable] private sealed class Mapping
        {
            public CropEntry[] crops; public ModelEntry[] products; public ModelEntry[] furniture; public ModelEntry cashRegister;
            public string[] customers; public float characterHeight = 1.25f; public string employee; public ModelEntry player;
            public ModelEntry[] decor; public ModelEntry[] animals; public Particles particles; public SoundEntry[] sounds; public UiEntry ui;
        }
        [Serializable] private sealed class CropEntry { public string crop; public string stages; public float height = 1.2f; public int density = 3; }
        [Serializable] private sealed class ModelEntry
        {
            public string id; public string model; public float height; public float width; public string animation;
            public string tex; public string normal; public bool cutout; public float scale;
        }
        [Serializable] private sealed class Particles { public string star, circle, dirt, smoke, spark, light; }
        [Serializable] private sealed class SoundEntry { public string id; public string clip; public float volume = 0.8f; }
        [Serializable] private sealed class UiEntry { public string font, buttonGreen, buttonYellow, buttonRed, buttonBlue, buttonGrey, panel; }
#pragma warning restore 0649

        [MenuItem("Fazenda/Arte/Gerar prefabs e catálogo visual")]
        public static VisualCatalog Generate()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(MappingPath);
            if (json == null) throw new FileNotFoundException(MappingPath);
            var map = JsonUtility.FromJson<Mapping>(json.text);
            foreach (var d in new[] { "Crops", "Products", "Furniture", "Characters", "Decor", "Effects", "Animators" })
                Directory.CreateDirectory($"{OutDir}/{d}");
            Directory.CreateDirectory(MaterialsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));

            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<VisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.crops.Clear(); catalog.products.Clear(); catalog.furniture.Clear(); catalog.customers.Clear();
            catalog.decor.Clear(); catalog.effects.Clear(); catalog.sounds.Clear(); catalog.animals.Clear();

            // ---------- Materiais base (referenciados = shaders garantidos no build) ----------
            catalog.baseLit = LitMaterial("BaseLit", Color.white, null, Vector2.one);
            catalog.baseUnlit = UnlitMaterial("BaseUnlit", Color.white);
            // Terra arada real (sulcos) e madeira de tábua (Poly Haven, CC0)
            catalog.soil = PbrMaterial("Soil", "farm_furrows", Vector2.one, Color.white, 0.05f);
            catalog.soilWet = PbrMaterial("SoilWet", "farm_furrows", Vector2.one, new Color(0.55f, 0.5f, 0.5f), 0.35f);
            catalog.wood = PbrMaterial("WoodPlanks", "brown_planks_05", new Vector2(1f, 1f), new Color(0.85f, 0.7f, 0.55f), 0.1f);

            // ---------- Culturas (4 fases, escala da fase 4) ----------
            foreach (var entry in map.crops ?? new CropEntry[0])
            {
                var models = Enumerable.Range(1, 4).Select(i => LoadModel(string.Format(entry.stages, i))).ToArray();
                if (models.Any(m => m == null)) { Debug.LogWarning($"[Arte] Faltam fases para '{entry.crop}'."); continue; }
                // Cada fase com altura própria (os modelos das fases 1-3 do pack são minúsculos): broto legível no celular.
                var visual = new VisualCatalog.CropVisual { cropId = entry.crop, stages = new GameObject[4], density = Mathf.Max(1, entry.density) };
                float footprint = PlotSize / visual.density * 1.15f;
                for (int i = 0; i < 4; i++)
                    visual.stages[i] = SaveWrapper(models[i], FitByHeight(models[i], entry.height * StageFractions[i], footprint),
                        $"{OutDir}/Crops/{entry.crop}_{i + 1}.prefab", true);
                catalog.crops.Add(visual);
            }

            // ---------- Produtos ----------
            foreach (var e in map.products ?? new ModelEntry[0])
            {
                if (e.model == "builtin:yarn")
                {
                    catalog.products.Add(new VisualCatalog.IdPrefab { id = e.id, prefab = BuildYarn(e.id, e.height) });
                    continue;
                }
                var model = LoadModel(e.model);
                if (model == null) { Warn(e.model); continue; }
                var prefab = SaveWrapper(model, FitByHeight(model, e.height, e.height * 1.8f), $"{OutDir}/Products/{e.id}.prefab", false);
                catalog.products.Add(new VisualCatalog.IdPrefab { id = e.id, prefab = prefab });
            }

            // ---------- Móveis da loja (escala pela largura; mede a superfície) ----------
            foreach (var e in map.furniture ?? new ModelEntry[0])
            {
                if (e.model != null && e.model.StartsWith("builtin:"))
                {
                    var built = BuildStall(e.id, e.model.Substring("builtin:".Length), e.width);
                    catalog.furniture.Add(built);
                    continue;
                }
                var model = LoadModel(e.model);
                if (model == null) { Warn(e.model); continue; }
                float s = FitByWidth(model, e.width);
                var prefab = SaveWrapper(model, s, $"{OutDir}/Furniture/{e.id}.prefab", true, addCollider: true);
                var b = PrefabBounds(prefab);
                catalog.furniture.Add(new VisualCatalog.FurnitureVisual
                {
                    furnitureId = e.id, prefab = prefab, surfaceHeight = b.max.y,
                    surfaceSize = new Vector2(b.size.x * 0.8f, b.size.z * 0.6f),
                });
            }
            if (map.cashRegister != null && map.cashRegister.model == "builtin:counter")
                catalog.cashRegister = BuildCounter(map.cashRegister.width > 0 ? map.cashRegister.width : 2f);
            else if (map.cashRegister != null && LoadModel(map.cashRegister.model) is GameObject reg)
                catalog.cashRegister = SaveWrapper(reg, FitByWidth(reg, map.cashRegister.width), $"{OutDir}/Furniture/cash_register.prefab", true, addCollider: true);

            // ---------- Personagens (um animator para todos: mesmo esqueleto Kenney) ----------
            AnimatorController characterController = null;
            foreach (var path in map.customers ?? new string[0])
            {
                var model = LoadModel(path);
                if (model == null) { Warn(path); continue; }
                if (characterController == null) characterController = BuildCharacterController(ThirdParty + path);
                var prefab = SaveWrapper(model, FitByHeight(model, map.characterHeight, 2f),
                    $"{OutDir}/Characters/{Path.GetFileNameWithoutExtension(path)}.prefab", true, controller: characterController);
                catalog.customers.Add(prefab);
            }
            if (!string.IsNullOrEmpty(map.employee) && LoadModel(map.employee) is GameObject emp)
                catalog.employee = SaveWrapper(emp, FitByHeight(emp, map.characterHeight, 2f), $"{OutDir}/Characters/employee.prefab", true,
                    controller: characterController ?? BuildCharacterController(ThirdParty + map.employee));

            // ---------- Jogadora ----------
            if (map.player != null && LoadModel(map.player.model) is GameObject playerModel)
                catalog.player = SaveWrapper(playerModel, FitByHeight(playerModel, map.player.height > 0 ? map.player.height : 1.7f, 2f),
                    $"{OutDir}/Characters/player.prefab", true,
                    controller: characterController ?? BuildCharacterController(ThirdParty + map.player.model));

            // ---------- Cenário ----------
            foreach (var e in map.decor ?? new ModelEntry[0])
            {
                var model = LoadModel(e.model);
                if (model == null) { Warn(e.model); continue; }
                float s = e.height > 0 ? FitByHeight(model, e.height, float.MaxValue) : (e.scale > 0 ? e.scale : 1f);
                AnimatorController ctrl = null;
                if (!string.IsNullOrEmpty(e.animation)) ctrl = BuildLoopController(ThirdParty + e.model, e.animation, e.id);
                Material overrideMat = null;
                if (!string.IsNullOrEmpty(e.tex))
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ThirdParty + e.tex);
                    var nor = string.IsNullOrEmpty(e.normal) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(ThirdParty + e.normal);
                    overrideMat = LitMaterial("Decor_" + e.id, Color.white, tex, Vector2.one, nor, 0.05f, e.cutout);
                }
                var prefab = SaveWrapper(model, s, $"{OutDir}/Decor/{e.id}.prefab", castShadows: e.height >= 0.6f || e.height <= 0,
                    controller: ctrl, overrideMaterial: overrideMat);
                catalog.decor.Add(new VisualCatalog.IdPrefab { id = e.id, prefab = prefab });
            }

            // ---------- Animais do jogo (um modelo por tipo) ----------
            foreach (var e in map.animals ?? new ModelEntry[0])
            {
                var model = LoadModel(e.model);
                if (model == null) { Warn(e.model); continue; }
                AnimatorController ctrl = string.IsNullOrEmpty(e.animation) ? null : BuildLoopController(ThirdParty + e.model, e.animation, "animal_" + e.id);
                var prefab = SaveWrapper(model, FitByHeight(model, e.height, e.height * 2.5f), $"{OutDir}/Decor/animal_{e.id}.prefab", true,
                    addCollider: true, controller: ctrl);
                catalog.animals.Add(new VisualCatalog.IdPrefab { id = e.id, prefab = prefab });
            }

            // ---------- Efeitos ----------
            if (map.particles != null) BuildEffects(catalog, map.particles);

            // ---------- Sons ----------
            foreach (var s in map.sounds ?? new SoundEntry[0])
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ThirdParty + s.clip);
                if (clip == null) { Warn(s.clip); continue; }
                catalog.sounds.Add(new VisualCatalog.IdSound { id = s.id, clip = clip, volume = s.volume });
            }

            // ---------- UI ----------
            if (map.ui != null)
            {
                catalog.font = AssetDatabase.LoadAssetAtPath<Font>(ThirdParty + map.ui.font);
                catalog.buttonGreen = LoadSprite(map.ui.buttonGreen);
                catalog.buttonYellow = LoadSprite(map.ui.buttonYellow);
                catalog.buttonRed = LoadSprite(map.ui.buttonRed);
                catalog.buttonBlue = LoadSprite(map.ui.buttonBlue);
                catalog.buttonGrey = LoadSprite(map.ui.buttonGrey);
                catalog.panel = LoadSprite(map.ui.panel);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Arte] Catálogo visual: {catalog.crops.Count} culturas, {catalog.products.Count} produtos, " +
                      $"{catalog.furniture.Count} móveis, {catalog.customers.Count} clientes, {catalog.decor.Count} decorações, " +
                      $"{catalog.effects.Count} efeitos, {catalog.sounds.Count} sons, fonte={(catalog.font != null)}.");
            return catalog;
        }

        private static void Warn(string what) => Debug.LogWarning($"[Arte] Não encontrado: {what}");

        private static Sprite LoadSprite(string relative) =>
            string.IsNullOrEmpty(relative) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(ThirdParty + relative);

        private static GameObject LoadModel(string relative) =>
            string.IsNullOrEmpty(relative) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(ThirdParty + relative);

        // ---------------------------------------------------------------- materiais

        public static Material LitMaterial(string name, Color color, Texture texture, Vector2 tiling) =>
            LitMaterial(name, color, texture, tiling, null, 0.08f, false);

        /// <summary>Material URP Lit (asset) com textura, normal map, suavidade e recorte por alfa opcionais.</summary>
        public static Material LitMaterial(string name, Color color, Texture texture, Vector2 tiling, Texture normal, float smoothness, bool cutout)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetTexture("_BaseMap", texture);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetFloat("_Smoothness", smoothness);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                mat.SetTexture("_BumpMap", null);
                mat.DisableKeyword("_NORMALMAP");
            }
            if (cutout)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.45f);
                mat.SetFloat("_Cull", 0f); // folhas: duas faces
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Material com o par de texturas PBR baixado (Textures/PBR/&lt;id&gt;_diffuse.jpg + _nor_gl.jpg).</summary>
        public static Material PbrMaterial(string name, string textureId, Vector2 tiling, Color tint, float smoothness = 0.1f)
        {
            var diff = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/PBR/{textureId}_diffuse.jpg");
            var nor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/PBR/{textureId}_nor_gl.jpg");
            if (diff == null) Debug.LogWarning($"[Arte] Textura PBR não encontrada: {textureId}");
            return LitMaterial(name, tint, diff, tiling, nor, smoothness, false);
        }

        public static Material UnlitMaterial(string name, Color color)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material ParticleMaterial(string name, Texture texture, bool additive)
        {
            string path = $"{MaterialsDir}/FX_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- prefabs

        private static Bounds MeasureBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static Bounds PrefabBounds(GameObject prefab)
        {
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try { return MeasureBounds(temp); }
            finally { Object.DestroyImmediate(temp); }
        }

        private static float FitByHeight(GameObject model, float targetHeight, float maxFootprint)
        {
            var b = PrefabBounds(model);
            float s = b.size.y > 1e-4f ? targetHeight / b.size.y : 1f;
            float footprint = Mathf.Max(b.size.x, b.size.z) * s;
            if (footprint > maxFootprint) s *= maxFootprint / footprint;
            return s;
        }

        private static float FitByWidth(GameObject model, float targetWidth)
        {
            var b = PrefabBounds(model);
            float w = Mathf.Max(b.size.x, b.size.z);
            return w > 1e-4f ? targetWidth / w : 1f;
        }

        /// <summary>Prefab próprio: raiz vazia com o modelo do pack como filho, pivô no chão e centralizado.</summary>
        private static GameObject SaveWrapper(GameObject model, float scale, string path, bool castShadows,
            bool addCollider = false, RuntimeAnimatorController controller = null, Material overrideMaterial = null)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(root.transform, false);
            instance.transform.localScale *= scale;
            var b = MeasureBounds(instance);
            instance.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);

            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                if (overrideMaterial != null)
                {
                    var mats = r.sharedMaterials;
                    for (int m = 0; m < mats.Length; m++) mats[m] = overrideMaterial;
                    r.sharedMaterials = mats;
                }
                r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }

            if (addCollider)
            {
                var nb = MeasureBounds(root);
                var box = root.AddComponent<BoxCollider>();
                box.center = nb.center - root.transform.position;
                box.size = nb.size;
            }

            if (controller != null)
            {
                var animator = instance.GetComponentInChildren<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                if (animator.avatar == null)
                    animator.avatar = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(model)).OfType<Avatar>().FirstOrDefault();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Balcão do caixa: móvel de madeira com tampo claro e a caixa registradora realista (Poly Haven) em cima.</summary>
        private static GameObject BuildCounter(float width)
        {
            var wood = PbrMaterial("CounterWood", "brown_planks_05", new Vector2(2f, 1f), new Color(0.75f, 0.58f, 0.42f));
            var top = LitMaterial("CounterTop", new Color(0.92f, 0.92f, 0.9f), null, Vector2.one, null, 0.6f, false);
            var root = new GameObject("cash_register");
            void Box(string n, Vector3 pos, Vector3 size, Material m)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = n;
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = pos;
                go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = m;
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            Box("Body", new Vector3(0, 0.45f, 0), new Vector3(width, 0.9f, 0.75f), wood);
            Box("Top", new Vector3(0, 0.93f, 0), new Vector3(width + 0.06f, 0.06f, 0.82f), top);
            var regModel = LoadModel("PolyHaven/CashRegister_01/CashRegister_01_1k.fbx");
            if (regModel != null)
            {
                var reg = (GameObject)PrefabUtility.InstantiatePrefab(regModel);
                reg.transform.SetParent(root.transform, false);
                var b = MeasureBounds(reg);
                reg.transform.localPosition = new Vector3(-width * 0.25f, 0.96f - b.min.y, 0);
                reg.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.6f, 0);
            col.size = new Vector3(width, 1.2f, 0.8f);
            string path = $"{OutDir}/Furniture/cash_register.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Novelo de lã (não há modelo nos packs): esfera creme com faixas.</summary>
        private static GameObject BuildYarn(string id, float height)
        {
            var wool = LitMaterial("Wool", new Color(0.97f, 0.93f, 0.84f), null, Vector2.one);
            var band = LitMaterial("WoolBand", new Color(0.86f, 0.78f, 0.66f), null, Vector2.one);
            var root = new GameObject(id);
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.SetParent(root.transform, false);
            ball.transform.localScale = Vector3.one * height;
            ball.transform.localPosition = new Vector3(0, height * 0.5f, 0);
            ball.GetComponent<Renderer>().sharedMaterial = wool;
            Object.DestroyImmediate(ball.GetComponent<Collider>());
            for (int i = 0; i < 3; i++)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ring.transform.SetParent(root.transform, false);
                ring.transform.localScale = new Vector3(height * 1.03f, height * 0.12f, height * 1.03f);
                ring.transform.localPosition = new Vector3(0, height * 0.5f, 0);
                ring.transform.localRotation = Quaternion.Euler(30f + i * 60f, i * 50f, 0);
                ring.GetComponent<Renderer>().sharedMaterial = band;
                Object.DestroyImmediate(ring.GetComponent<Collider>());
            }
            string path = $"{OutDir}/Products/{id}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Móvel próprio (sem produtos "pintados" no modelo): tampo, prateleira de baixo, pés e placa colorida.</summary>
        private static VisualCatalog.FurnitureVisual BuildStall(string id, string style, float width)
        {
            bool wood = style == "wood";
            var body = wood ? PbrMaterial("StallWood", "brown_planks_05", new Vector2(2f, 1f), new Color(0.9f, 0.72f, 0.52f))
                            : LitMaterial("StallMetal", new Color(0.82f, 0.84f, 0.88f), null, Vector2.one, null, 0.55f, false);
            var dark = wood ? PbrMaterial("StallWoodDark", "brown_planks_05", new Vector2(1f, 1f), new Color(0.55f, 0.4f, 0.28f))
                            : LitMaterial("StallMetalDark", new Color(0.45f, 0.48f, 0.52f), null, Vector2.one, null, 0.6f, false);
            var sign = LitMaterial(wood ? "StallSignGreen" : "StallSignBlue", wood ? new Color(0.25f, 0.62f, 0.32f) : new Color(0.25f, 0.45f, 0.8f), null, Vector2.one);
            const float depth = 1.0f, top = 0.9f;
            var root = new GameObject(id);
            void Box(string n, Vector3 pos, Vector3 size, Material m)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = n;
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = pos;
                go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = m;
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            Box("Top", new Vector3(0, top - 0.06f, 0), new Vector3(width, 0.12f, depth), body);
            Box("Lip", new Vector3(0, top + 0.03f, depth * 0.5f), new Vector3(width, 0.1f, 0.06f), dark);
            Box("Shelf", new Vector3(0, 0.3f, 0), new Vector3(width - 0.1f, 0.08f, depth - 0.1f), dark);
            foreach (var x in new[] { -width * 0.5f + 0.08f, width * 0.5f - 0.08f })
            foreach (var z in new[] { -depth * 0.5f + 0.08f, depth * 0.5f - 0.08f })
                Box("Leg", new Vector3(x, (top - 0.12f) * 0.5f, z), new Vector3(0.12f, top - 0.12f, 0.12f), dark);
            Box("Sign", new Vector3(0, top - 0.22f, -depth * 0.5f - 0.02f), new Vector3(width * 0.9f, 0.22f, 0.04f), sign);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0, top * 0.5f, 0);
            box.size = new Vector3(width, top, depth);

            string path = $"{OutDir}/Furniture/{id}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return new VisualCatalog.FurnitureVisual
            {
                furnitureId = id, prefab = prefab, surfaceHeight = top,
                surfaceSize = new Vector2(width * 0.86f, depth * 0.7f),
            };
        }

        // ---------------------------------------------------------------- animação

        private static AnimationClip Clip(string modelPath, string name) =>
            AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__") &&
                                     (c.name.Equals(name, StringComparison.OrdinalIgnoreCase) || c.name.EndsWith("|" + name, StringComparison.OrdinalIgnoreCase)));

        private static AnimatorController BuildLoopController(string modelPath, string animation, string id)
        {
            var clip = Clip(modelPath, animation);
            if (clip == null) { Debug.LogWarning($"[Arte] Animação '{animation}' não encontrada em {modelPath}."); return null; }
            string path = $"{OutDir}/Animators/{id}.controller";
            AssetDatabase.DeleteAsset(path);
            return AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
        }

        /// <summary>
        /// Cliente/funcionário: idle ⇄ walk pelo parâmetro "speed"; gatilhos "pick", "yes", "no" tocam uma vez e voltam.
        /// </summary>
        private static AnimatorController BuildCharacterController(string modelPath)
        {
            string path = $"{OutDir}/Animators/character.controller";
            AssetDatabase.DeleteAsset(path);
            var c = AnimatorController.CreateAnimatorControllerAtPath(path);
            c.AddParameter("speed", AnimatorControllerParameterType.Float);
            c.AddParameter("pick", AnimatorControllerParameterType.Trigger);
            c.AddParameter("yes", AnimatorControllerParameterType.Trigger);
            c.AddParameter("no", AnimatorControllerParameterType.Trigger);
            var sm = c.layers[0].stateMachine;

            var idle = sm.AddState("idle");
            idle.motion = Clip(modelPath, "idle");
            sm.defaultState = idle;
            var walk = sm.AddState("walk");
            walk.motion = Clip(modelPath, "walk");

            var toWalk = idle.AddTransition(walk);
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "speed");
            toWalk.hasExitTime = false; toWalk.duration = 0.12f;
            var toIdle = walk.AddTransition(idle);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "speed");
            toIdle.hasExitTime = false; toIdle.duration = 0.15f;

            foreach (var (trigger, clipName) in new[] { ("pick", "pick-up"), ("yes", "emote-yes"), ("no", "emote-no") })
            {
                var state = sm.AddState(trigger);
                state.motion = Clip(modelPath, clipName);
                var enter = sm.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0, trigger);
                enter.hasExitTime = false; enter.duration = 0.1f; enter.canTransitionToSelf = false;
                var back = state.AddTransition(idle);
                back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.15f;
            }
            EditorUtility.SetDirty(c);
            return c;
        }

        // ---------------------------------------------------------------- efeitos

        private static void BuildEffects(VisualCatalog catalog, Particles p)
        {
            Texture2D T(string rel) => string.IsNullOrEmpty(rel) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(ThirdParty + rel);
            var star = ParticleMaterial("star", T(p.star), additive: true);
            var dirt = ParticleMaterial("dirt", T(p.dirt), additive: false);
            var circle = ParticleMaterial("circle", T(p.circle), additive: false);
            var light = ParticleMaterial("light", T(p.light), additive: true);
            var smoke = ParticleMaterial("smoke", T(p.smoke), additive: false);

            var brownA = new Color(0.45f, 0.30f, 0.18f); var brownB = new Color(0.62f, 0.44f, 0.26f);
            var gold = new Color(1f, 0.85f, 0.25f); var goldB = new Color(1f, 0.97f, 0.6f);
            var water = new Color(0.45f, 0.75f, 1f, 0.9f); var waterB = new Color(0.8f, 0.95f, 1f, 0.9f);
            var leafA = new Color(0.45f, 0.85f, 0.3f); var leafB = new Color(0.8f, 1f, 0.45f);

            Add(catalog, "plant", Burst("plant", dirt, 14, brownA, brownB, 0.18f, 0.32f, 1.2f, 2.4f, 0.5f, 0.8f, 1.5f, 0.35f));
            Add(catalog, "water", Burst("water", circle, 26, water, waterB, 0.06f, 0.12f, 0.5f, 1.2f, 0.5f, 0.8f, 2.2f, 0.45f, fromAbove: true));
            Add(catalog, "harvest", Burst("harvest", star, 22, leafA, leafB, 0.18f, 0.35f, 2.5f, 4.5f, 0.6f, 1.0f, 1.2f, 0.4f));
            Add(catalog, "coins", Burst("coins", star, 18, gold, goldB, 0.2f, 0.38f, 2.5f, 4f, 0.6f, 1.0f, 1.0f, 0.25f));
            Add(catalog, "puff", Burst("puff", smoke, 8, new Color(1, 1, 1, 0.6f), new Color(0.9f, 0.9f, 0.9f, 0.4f), 0.5f, 0.9f, 0.4f, 0.9f, 0.7f, 1.1f, -0.05f, 0.3f));
            Add(catalog, "ready", Loop("ready", light, star, gold, goldB));
        }

        private static void Add(VisualCatalog catalog, string id, GameObject prefab) =>
            catalog.effects.Add(new VisualCatalog.IdPrefab { id = id, prefab = prefab });

        private static GameObject Burst(string id, Material mat, int count, Color c1, Color c2, float sizeMin, float sizeMax,
            float speedMin, float speedMax, float lifeMin, float lifeMax, float gravity, float radius, bool fromAbove = false)
        {
            var go = new GameObject("FX_" + id);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count * 2;
            main.stopAction = ParticleSystemStopAction.Disable; // volta para o pool

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = fromAbove ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Hemisphere;
            shape.radius = radius;
            if (fromAbove)
            {
                shape.angle = 12f;
                shape.rotation = new Vector3(90f, 0f, 0f); // cone apontando para baixo (regador)
                go.transform.localPosition = Vector3.up * 1.4f;
            }
            else
            {
                shape.rotation = new Vector3(-90f, 0f, 0f); // hemisfério para cima
            }

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.4f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            string path = $"{OutDir}/Effects/{id}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>Brilho contínuo (parcela pronta para colher): halo + estrelinhas subindo.</summary>
        private static GameObject Loop(string id, Material glow, Material sparkle, Color c1, Color c2)
        {
            var root = new GameObject("FX_" + id);

            var halo = new GameObject("Glow");
            halo.transform.SetParent(root.transform, false);
            var ps = halo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.duration = 1f;
            main.startLifetime = 1.2f; main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 1.8f);
            main.startColor = new Color(1f, 0.9f, 0.4f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.rateOverTime = 2f;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = halo.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = glow; r.shadowCastingMode = ShadowCastingMode.Off;
            halo.transform.localPosition = Vector3.up * 0.6f;

            var sparks = new GameObject("Sparkles");
            sparks.transform.SetParent(root.transform, false);
            var ps2 = sparks.AddComponent<ParticleSystem>();
            ps2.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m2 = ps2.main;
            m2.loop = true; m2.playOnAwake = true; m2.duration = 1f;
            m2.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            m2.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            m2.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            m2.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
            m2.simulationSpace = ParticleSystemSimulationSpace.World;
            var em2 = ps2.emission; em2.rateOverTime = 7f;
            var sh2 = ps2.shape; sh2.shapeType = ParticleSystemShapeType.Cone; sh2.angle = 8f; sh2.radius = 0.55f; sh2.rotation = new Vector3(-90f, 0, 0);
            var col2 = ps2.colorOverLifetime; col2.enabled = true; col2.color = g;
            var r2 = sparks.GetComponent<ParticleSystemRenderer>();
            r2.sharedMaterial = sparkle; r2.shadowCastingMode = ShadowCastingMode.Off;

            string path = $"{OutDir}/Effects/{id}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
