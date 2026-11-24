using System.IO;
using Game.Presentation;
using Game.Presentation.Controls;
using Game.Presentation.Farm;
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
            rootGo.AddComponent<Bootstrap>();

            // Fazenda
            var farmGo = new GameObject("Farm");
            var farmView = farmGo.AddComponent<FarmView>();
            var interaction = farmGo.AddComponent<FarmInteraction>();
            SetRef(interaction, "cameraController", cameraController);
            SetRef(interaction, "farmView", farmView);

            // UI
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var safe = FullRect("SafeArea", canvasGo.transform);
            safe.gameObject.AddComponent<SafeArea>();
            // Ordem dos irmãos = ordem de desenho: HUD, menus por cima, toast por último.
            FullRect("Hud", safe).gameObject.AddComponent<HudController>();
            FullRect("PlantMenu", safe).gameObject.AddComponent<PlantMenu>();
            FullRect("Toast", safe).gameObject.AddComponent<Toast>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            Debug.Log("[SceneGenerator] Cena gerada: " + MainScenePath);
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
