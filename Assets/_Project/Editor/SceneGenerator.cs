using System.IO;
using Game.Content;
using Game.Editor.Art;
using Game.Presentation;
using Game.Presentation.Controls;
using Game.Presentation.Farm;
using Game.Presentation.Store;
using Game.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Gera a cena principal (gray box) por código. Nunca editar o .unity à mão: rode o menu
    /// "Fazenda/Gerar cena Main" (ou o setup-project.ps1) depois de mudar este script.
    /// </summary>
    public static class SceneGenerator
    {
        public const string MainScenePath = "Assets/_Project/Presentation/Scenes/Main.unity";
        public const string CatalogPath = "Assets/_Project/Content/Data/catalog.json";
        public const string GrayboxDir = "Assets/_Project/Presentation/Graybox";

        [MenuItem("Fazenda/Gerar cena Main")]
        public static void GenerateMain()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MainScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Câmera
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.82f, 0.95f);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 200f;
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraGo.AddComponent<AudioListener>();

            var rig = new GameObject("CameraRig");
            rig.transform.position = new Vector3(0, 0, -3f);
            var cameraController = rig.AddComponent<TouchCameraController>();
            SetRef(cameraController, "targetCamera", camera);
            var areaSwitcher = rig.AddComponent<AreaSwitcher>();
            SetRef(areaSwitcher, "cameraController", cameraController);

            // Luz
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Hard;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Chão
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(8, 1, 8);
            ground.GetComponent<Renderer>().sharedMaterial = GrayboxMaterialAsset("Ground", new Color(0.45f, 0.70f, 0.35f));

            // Raiz do jogo
            var rootGo = new GameObject("GameRoot");
            var gameRoot = rootGo.AddComponent<GameRoot>();
            SetRef(gameRoot, "catalogJson", AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogPath));
            var visuals = AssetDatabase.LoadAssetAtPath<VisualCatalog>(VisualCatalogGenerator.CatalogPath);
            SetRef(gameRoot, "visuals", visuals);
            rootGo.AddComponent<Bootstrap>();

            // Fazenda
            var farmGo = new GameObject("Farm");
            var farmView = farmGo.AddComponent<FarmView>();
            var interaction = farmGo.AddComponent<FarmInteraction>();
            SetRef(interaction, "cameraController", cameraController);
            SetRef(interaction, "farmView", farmView);

            // Mercadinho (à direita da fazenda)
            var storeGo = new GameObject("Store");
            storeGo.transform.position = new Vector3(24, 0, 0);
            storeGo.AddComponent<StoreView>();
            var storeInteraction = storeGo.AddComponent<StoreInteraction>();
            SetRef(storeInteraction, "cameraController", cameraController);

            // Arte 3D (Etapa 6): cenário em volta da fazenda e paredes da loja
            if (visuals != null) PlaceDecor(visuals);
            BuildStoreWalls(storeGo.transform);

            // UI
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
            // Ordem dos irmãos = ordem de desenho: HUD, menus por cima, toast por último.
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

        private static GameObject Place(VisualCatalog visuals, string id, Vector3 position, float yaw, Transform parent)
        {
            var prefab = visuals.Decor(id);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            go.isStatic = id != "cow" && id != "pig" && id != "sheep";
            return go;
        }

        /// <summary>Cenário fixo (posições determinísticas). A fazenda ocupa x -5..5, z -8..1; a loja fica em x≈24.</summary>
        private static void PlaceDecor(VisualCatalog visuals)
        {
            var root = new GameObject("Decor").transform;

            Place(visuals, "barn", new Vector3(-1.5f, 0, 6.5f), 180, root);     // armazém
            Place(visuals, "silo", new Vector3(4.5f, 0, 7f), 0, root);
            Place(visuals, "windmill", new Vector3(-10f, 0, 6f), 160, root);
            Place(visuals, "well", new Vector3(7.5f, 0, 1.5f), 30, root);
            Place(visuals, "openbarn", new Vector3(13f, 0, 5f), 200, root);

            // Cerca em volta das parcelas (comprimento real medido no prefab).
            var fencePrefab = visuals.Decor("fence");
            if (fencePrefab != null)
            {
                var probe = (GameObject)PrefabUtility.InstantiatePrefab(fencePrefab);
                var b = new Bounds(probe.transform.position, Vector3.zero);
                foreach (var r in probe.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                bool alongX = b.size.x >= b.size.z;
                float length = Mathf.Max(0.5f, Mathf.Max(b.size.x, b.size.z));
                Object.DestroyImmediate(probe);

                const float xMin = -6.2f, xMax = 6.2f, zMin = -9.2f, zMax = 2.2f;
                for (float x = xMin + length * 0.5f; x <= xMax; x += length)
                {
                    Place(visuals, "fence", new Vector3(x, 0, zMax), alongX ? 0 : 90, root);
                    if (Mathf.Abs(x) > 1.6f) Place(visuals, "fence", new Vector3(x, 0, zMin), alongX ? 0 : 90, root); // portão
                }
                for (float z = zMin + length * 0.5f; z <= zMax; z += length)
                {
                    Place(visuals, "fence", new Vector3(xMin, 0, z), alongX ? 90 : 0, root);
                    Place(visuals, "fence", new Vector3(xMax, 0, z), alongX ? 90 : 0, root);
                }
            }

            // Pasto com animais (idle animado)
            Place(visuals, "cow", new Vector3(-11f, 0, -2f), 60, root);
            Place(visuals, "cow", new Vector3(-13.5f, 0, -4.5f), 200, root);
            Place(visuals, "pig", new Vector3(-10f, 0, -6f), 120, root);
            Place(visuals, "sheep", new Vector3(-14f, 0, -1f), 300, root);
            Place(visuals, "sheep", new Vector3(-12f, 0, -7.5f), 20, root);

            // Grama e flores espalhadas, fora da fazenda e da loja (semente fixa = sempre igual).
            var rng = new System.Random(42);
            string[] small = { "grass1", "grass2", "grass1", "flower1", "flower2" };
            int placed = 0, tries = 0;
            while (placed < 90 && tries++ < 2000)
            {
                float x = (float)(rng.NextDouble() * 60 - 22);
                float z = (float)(rng.NextDouble() * 30 - 18);
                bool inFarm = x > -7 && x < 9 && z > -10 && z < 9;
                bool inStore = x > 17 && x < 31 && z > -12 && z < 7;
                bool inPasture = x > -15.5f && x < -8.5f && z > -9 && z < 0.5f;
                if (inFarm || inStore || inPasture) continue;
                Place(visuals, small[rng.Next(small.Length)], new Vector3(x, 0, z), (float)(rng.NextDouble() * 360), root);
                placed++;
            }
        }

        /// <summary>Paredes simples do mercadinho (a arte final da loja depende de um pack com licença).</summary>
        private static void BuildStoreWalls(Transform store)
        {
            var wall = GrayboxMaterialAsset("StoreWall", new Color(0.96f, 0.90f, 0.78f));
            var trim = GrayboxMaterialAsset("StoreTrim", new Color(0.80f, 0.25f, 0.20f));
            void Box(string name, Vector3 localPos, Vector3 size, Material m)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(store, false);
                go.transform.localPosition = localPos;
                go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = m;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.isStatic = true;
            }
            Box("WallLeft", new Vector3(-5.1f, 1.25f, -2f), new Vector3(0.2f, 2.5f, 12f), wall);
            Box("WallRight", new Vector3(5.1f, 1.25f, -2f), new Vector3(0.2f, 2.5f, 12f), wall);
            Box("WallBackL", new Vector3(-3.1f, 1.25f, 4f), new Vector3(4f, 2.5f, 0.2f), wall);
            Box("WallBackR", new Vector3(3.1f, 1.25f, 4f), new Vector3(4f, 2.5f, 0.2f), wall);
            Box("Awning", new Vector3(0, 2.7f, 4.4f), new Vector3(10.4f, 0.4f, 1.0f), trim);
            Box("TrimLeft", new Vector3(-5.1f, 2.6f, -2f), new Vector3(0.3f, 0.2f, 12.1f), trim);
            Box("TrimRight", new Vector3(5.1f, 2.6f, -2f), new Vector3(0.3f, 0.2f, 12.1f), trim);
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

        private static Material GrayboxMaterialAsset(string name, Color color)
        {
            Directory.CreateDirectory(GrayboxDir);
            string path = $"{GrayboxDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
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
