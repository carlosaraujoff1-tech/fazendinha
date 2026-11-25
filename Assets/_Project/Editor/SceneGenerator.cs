using System.IO;
using Game.Content;
using Game.Editor.Art;
using Game.Presentation;
using Game.Presentation.Controls;
using Game.Presentation.Dev;
using Game.Presentation.Effects;
using Game.Presentation.Farm;
using Game.Presentation.Store;
using Game.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Gera a cena principal por código (nunca editar o .unity à mão): mundo, luz, céu, pós-processamento,
    /// cenário, loja, UI. Rode "Fazenda/Gerar cena Main" (ou setup-project) depois de mudar este script.
    /// Layout do mundo: fazenda em x≈0 (parcelas de z=0 para trás), loja em x≈24, câmera olhando para +z.
    /// </summary>
    public static class SceneGenerator
    {
        public const string MainScenePath = "Assets/_Project/Presentation/Scenes/Main.unity";
        public const string CatalogPath = "Assets/_Project/Content/Data/catalog.json";
        public const string SettingsDir = "Assets/_Project/Presentation/Settings";
        public static readonly Vector3 StoreOrigin = new Vector3(24, 0, 0);

        [MenuItem("Fazenda/Gerar cena Main")]
        public static void GenerateMain()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MainScenePath));
            var visuals = AssetDatabase.LoadAssetAtPath<VisualCatalog>(VisualCatalogGenerator.CatalogPath);
            if (visuals == null) visuals = VisualCatalogGenerator.Generate();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // IMPORTANTE: recarregar DEPOIS de NewScene. Criar a cena descarrega assets não usados, e uma referência
            // carregada antes vira "nula" ao ser salva na cena (era o motivo do rosa: o jogo ficava sem catálogo).
            visuals = AssetDatabase.LoadAssetAtPath<VisualCatalog>(VisualCatalogGenerator.CatalogPath);

            // ---------- Câmera ----------
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 38f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 220f;
            var camData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cameraGo.AddComponent<AudioListener>();

            var rig = new GameObject("CameraRig");
            rig.transform.position = new Vector3(0, 0, -3f);
            var cameraController = rig.AddComponent<TouchCameraController>();
            SetRef(cameraController, "targetCamera", camera);
            var areaSwitcher = rig.AddComponent<AreaSwitcher>();
            SetRef(areaSwitcher, "cameraController", cameraController);

            // ---------- Luz, céu, neblina, pós ----------
            SetupLighting();
            SetupPostProcessing();

            // ---------- Chão ----------
            var grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>(VisualCatalogGenerator.TexturesDir + "/grass.png");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(8, 0, -4);
            ground.transform.localScale = new Vector3(14, 1, 10);
            ground.GetComponent<Renderer>().sharedMaterial =
                VisualCatalogGenerator.LitMaterial("Ground", Color.white, grassTex, new Vector2(35, 25));
            ground.isStatic = true;

            var dirtTex = AssetDatabase.LoadAssetAtPath<Texture2D>(VisualCatalogGenerator.TexturesDir + "/dirt_path.png");
            var pathMat = VisualCatalogGenerator.LitMaterial("DirtPath", Color.white, dirtTex, new Vector2(6, 1));
            Flat("Path_FarmStore", new Vector3(12.5f, 0.012f, -3.5f), new Vector3(13f, 1f, 2.2f), pathMat, null);
            Flat("Path_Barn", new Vector3(-1.5f, 0.011f, 3.4f), new Vector3(2.2f, 1f, 3.2f), pathMat, null);

            // ---------- Raiz do jogo ----------
            var rootGo = new GameObject("GameRoot");
            var gameRoot = rootGo.AddComponent<GameRoot>();
            SetRef(gameRoot, "catalogJson", AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogPath));
            SetRef(gameRoot, "visuals", AssetDatabase.LoadAssetAtPath<VisualCatalog>(VisualCatalogGenerator.CatalogPath));
            if (new SerializedObject(gameRoot).FindProperty("visuals").objectReferenceValue == null)
                Debug.LogError("[SceneGenerator] Referência ao catálogo visual ficou NULA na cena.");
            rootGo.AddComponent<Bootstrap>();
            rootGo.AddComponent<FxPlayer>();
            rootGo.AddComponent<SoundPlayer>();
            rootGo.AddComponent<ScreenshotDirector>();

            // ---------- Fazenda ----------
            var farmGo = new GameObject("Farm");
            var farmView = farmGo.AddComponent<FarmView>();
            var interaction = farmGo.AddComponent<FarmInteraction>();
            SetRef(interaction, "cameraController", cameraController);
            SetRef(interaction, "farmView", farmView);

            // ---------- Mercadinho ----------
            var storeGo = new GameObject("Store");
            storeGo.transform.position = StoreOrigin;
            storeGo.AddComponent<StoreView>();
            var storeInteraction = storeGo.AddComponent<StoreInteraction>();
            SetRef(storeInteraction, "cameraController", cameraController);
            BuildStore(visuals, storeGo.transform);

            // ---------- Cenário ----------
            PlaceDecor(visuals);

            // ---------- UI ----------
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // paisagem: escala pela altura
            canvasGo.AddComponent<GraphicRaycaster>();

            var safe = FullRect("SafeArea", canvasGo.transform);
            safe.gameObject.AddComponent<SafeArea>();
            // Ordem dos irmãos = ordem de desenho.
            FullRect("FloatingText", safe).gameObject.AddComponent<FloatingTextLayer>();
            FullRect("Hud", safe).gameObject.AddComponent<HudController>();
            FullRect("PlantMenu", safe).gameObject.AddComponent<PlantMenu>();
            FullRect("StoreMenus", safe).gameObject.AddComponent<StoreMenus>();
            FullRect("WelcomeBack", safe).gameObject.AddComponent<WelcomeBackPanel>();
            FullRect("Toast", safe).gameObject.AddComponent<Toast>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            Debug.Log("[SceneGenerator] Cena gerada: " + MainScenePath);
        }

        // ------------------------------------------------------------------ luz e pós

        private static void SetupLighting()
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.82f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            sunGo.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
            RenderSettings.sun = sun;

            Directory.CreateDirectory(SettingsDir);
            string skyPath = SettingsDir + "/Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_AtmosphereThickness", 0.75f);
            sky.SetColor("_SkyTint", new Color(0.45f, 0.62f, 0.95f));
            sky.SetColor("_GroundColor", new Color(0.55f, 0.62f, 0.5f));
            sky.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.80f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.66f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.33f, 0.27f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.85f, 0.95f);
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 150f;
        }

        private static void SetupPostProcessing()
        {
            string path = SettingsDir + "/PostProcess.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }

            var tone = Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.Neutral);
            var bloom = Add<Bloom>();
            bloom.intensity.Override(0.45f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.65f);
            var color = Add<ColorAdjustments>();
            color.postExposure.Override(0.2f);
            color.contrast.Override(12f);
            color.saturation.Override(22f);
            var wb = Add<WhiteBalance>();
            wb.temperature.Override(7f);
            var vignette = Add<Vignette>();
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var volumeGo = new GameObject("PostProcess");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // ------------------------------------------------------------------ loja

        private static void BuildStore(VisualCatalog v, Transform store)
        {
            // Piso de ladrilho (textura própria) — cobre até 12 móveis (6 fileiras)
            var floorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(VisualCatalogGenerator.TexturesDir + "/floor_tiles.png");
            var floorMat = VisualCatalogGenerator.LitMaterial("StoreFloor", Color.white, floorTex, new Vector2(5, 9));
            Flat("StoreFloor", store.position + new Vector3(0, 0.02f, -5f), new Vector3(10.4f, 1f, 18.4f), floorMat, store);

            // Paredes Kenney (medidas no prefab para encaixar)
            float wallWidth = MeasureWidth(v.Decor("storeWall"), out bool wallAlongX);
            if (wallWidth > 0.1f)
            {
                float yawX = wallAlongX ? 0f : 90f;   // parede correndo em X
                float yawZ = wallAlongX ? 90f : 0f;   // parede correndo em Z
                const float back = 4f, front = -14f, left = -5.2f, right = 5.2f;
                // Fundo com porta no meio
                int nBack = Mathf.Max(3, Mathf.RoundToInt((right - left) / wallWidth));
                for (int i = 0; i < nBack; i++)
                {
                    float x = left + wallWidth * (i + 0.5f);
                    bool door = i == nBack / 2;
                    Place(v, door ? "storeDoor" : (i % 2 == 0 ? "storeWall" : "storeWindow"), store.position + new Vector3(x, 0, back), yawX + 180f, store);
                }
                // Laterais
                for (float z = back - wallWidth * 0.5f; z > front; z -= wallWidth)
                {
                    bool window = Mathf.RoundToInt(z) % 3 == 0;
                    Place(v, window ? "storeWindow" : "storeWall", store.position + new Vector3(left, 0, z), yawZ, store);
                    Place(v, window ? "storeWindow" : "storeWall", store.position + new Vector3(right, 0, z), yawZ + 180f, store);
                }
            }

            // Decoração interna (prateleiras cheias de fundo, geladeiras, carrinho)
            Place(v, "shelfBoxes", store.position + new Vector3(-3.6f, 0, 3.3f), 180f, store);
            Place(v, "shelfBags", store.position + new Vector3(3.6f, 0, 3.3f), 180f, store);
            Place(v, "fridgeTall", store.position + new Vector3(4.5f, 0, -8f), -90f, store);
            Place(v, "fridgeTall", store.position + new Vector3(4.5f, 0, -10.5f), -90f, store);
            Place(v, "bread", store.position + new Vector3(-4.4f, 0, -9f), 90f, store);
            Place(v, "cart", store.position + new Vector3(1.6f, 0, 5.2f), 200f, store);
            Place(v, "cart", store.position + new Vector3(2.3f, 0, 5.4f), 190f, store);
            Place(v, "awning", store.position + new Vector3(0, 2.4f, 4.35f), 180f, store);
        }

        private static float MeasureWidth(GameObject prefab, out bool alongX)
        {
            alongX = true;
            if (prefab == null) return 0f;
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var b = new Bounds(probe.transform.position, Vector3.zero);
            foreach (var r in probe.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            Object.DestroyImmediate(probe);
            alongX = b.size.x >= b.size.z;
            return Mathf.Max(b.size.x, b.size.z);
        }

        // ------------------------------------------------------------------ cenário

        private static GameObject Place(VisualCatalog visuals, string id, Vector3 position, float yaw, Transform parent, float scale = 1f)
        {
            var prefab = visuals.Decor(id);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale *= scale;
            go.isStatic = go.GetComponentInChildren<Animator>() == null;
            return go;
        }

        /// <summary>Cenário fixo e determinístico. Fazenda x -6..6, z -9..2; loja x 18..30, z -15..5; caminho entre elas.</summary>
        private static void PlaceDecor(VisualCatalog v)
        {
            var root = new GameObject("Decor").transform;

            Place(v, "barn", new Vector3(-1.5f, 0, 7f), 180, root);
            Place(v, "silo", new Vector3(4.5f, 0, 7.5f), 0, root);
            Place(v, "windmill", new Vector3(-11f, 0, 7f), 160, root);
            Place(v, "well", new Vector3(8f, 0, 1.5f), 30, root);
            Place(v, "openbarn", new Vector3(12.5f, 0, 5.5f), 200, root);
            Place(v, "tractor", new Vector3(6.2f, 0, 4.2f), 215, root);
            Place(v, "truck", new Vector3(32.5f, 0, -1.5f), 180, root);

            // Cerca em volta das parcelas (comprimento real medido no prefab), com portão na frente.
            float length = MeasureWidth(v.Decor("fence"), out bool alongX);
            if (length > 0.1f)
            {
                const float xMin = -6.2f, xMax = 6.2f, zMin = -9.2f, zMax = 2.2f;
                float yawX = alongX ? 0 : 90, yawZ = alongX ? 90 : 0;
                for (float x = xMin + length * 0.5f; x <= xMax; x += length)
                {
                    Place(v, "fence", new Vector3(x, 0, zMax), yawX, root);
                    if (Mathf.Abs(x) > 1.4f) Place(v, "fence", new Vector3(x, 0, zMin), yawX, root);
                }
                for (float z = zMin + length * 0.5f; z <= zMax; z += length)
                {
                    Place(v, "fence", new Vector3(xMin, 0, z), yawZ, root);
                    if (z < -5.5f || z > -1.5f) Place(v, "fence", new Vector3(xMax, 0, z), yawZ, root); // saída para a loja
                }
            }

            // Pasto
            Place(v, "cow", new Vector3(-11f, 0, -2f), 60, root);
            Place(v, "cow", new Vector3(-13.5f, 0, -4.5f), 200, root);
            Place(v, "pig", new Vector3(-10f, 0, -6f), 120, root);
            Place(v, "sheep", new Vector3(-14f, 0, -1f), 300, root);
            Place(v, "sheep", new Vector3(-12f, 0, -7.5f), 20, root);

            // Árvores em volta (moldura), arbustos, pedras, flores, grama — semente fixa.
            var rng = new System.Random(42);
            string[] trees = { "tree1", "tree2", "tree3", "tree4", "tree5", "tree6" };
            string[] small = { "bush1", "bush2", "bush3", "flowerR", "flowerY", "flowerP", "grass", "grass", "rockS1", "rockS2", "stump", "log", "mushrooms" };

            bool Blocked(float x, float z, float margin)
            {
                bool farm = x > -7 - margin && x < 9 + margin && z > -10 - margin && z < 10 + margin;
                bool store = x > 17.5f - margin && x < 31 + margin && z > -16 - margin && z < 7 + margin;
                bool path = x > 5 && x < 19 && z > -5 - margin && z < -2 + margin;
                bool pasture = x > -15.5f && x < -8.5f && z > -9 && z < 0.5f;
                bool truck = x > 30 && x < 35 && z > -4 && z < 1;
                return farm || store || path || pasture || truck;
            }

            int placedTrees = 0;
            for (int tries = 0; placedTrees < 70 && tries < 4000; tries++)
            {
                float x = (float)(rng.NextDouble() * 80 - 32);
                float z = (float)(rng.NextDouble() * 50 - 30);
                if (Blocked(x, z, 2.5f)) continue;
                float s = 0.8f + (float)rng.NextDouble() * 0.5f;
                Place(v, trees[rng.Next(trees.Length)], new Vector3(x, 0, z), (float)(rng.NextDouble() * 360), root, s);
                placedTrees++;
            }

            int placedSmall = 0;
            for (int tries = 0; placedSmall < 160 && tries < 6000; tries++)
            {
                float x = (float)(rng.NextDouble() * 70 - 26);
                float z = (float)(rng.NextDouble() * 42 - 26);
                if (Blocked(x, z, 0.3f)) continue;
                Place(v, small[rng.Next(small.Length)], new Vector3(x, 0, z), (float)(rng.NextDouble() * 360), root,
                    0.8f + (float)rng.NextDouble() * 0.6f);
                placedSmall++;
            }

            // Flores ao longo da cerca da fazenda (moldura colorida)
            string[] flowers = { "flowerR", "flowerY", "flowerP" };
            for (float x = -6.8f; x <= 6.8f; x += 1.1f)
                Place(v, flowers[rng.Next(3)], new Vector3(x + (float)rng.NextDouble() * 0.3f, 0, 2.9f), (float)(rng.NextDouble() * 360), root);
            Place(v, "rockL", new Vector3(-8.5f, 0, 3.5f), 40, root);
            Place(v, "rockL", new Vector3(15.5f, 0, -7.5f), 160, root, 0.8f);
        }

        // ------------------------------------------------------------------ utilidades

        private static void Flat(string name, Vector3 position, Vector3 size, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size.x, size.z, 1f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
        }

        private static RectTransform FullRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[SceneGenerator] Campo '{field}' não encontrado em {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
