using System;
using System.IO;
using Game.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Configura o projeto de forma reprodutível (idempotente): Player Settings Android,
    /// URP mobile e a cena Boot. Rodar pelo menu "Fazenda/Configurar projeto" ou em batchmode:
    /// Unity -batchmode -quit -buildTarget Android -executeMethod Game.Editor.ProjectSetup.ApplyFromCommandLine
    /// </summary>
    public static class ProjectSetup
    {
        public const string CompanyName = "FazendaMercadinho";
        public const string ProductName = "Fazenda & Mercadinho";
        public const string AndroidPackageId = "com.fazendamercadinho.tycoon";
        public const string Version = "0.0.1";
        public const int MinAndroidApi = 26;    // Android 8.0
        public const int TargetAndroidApi = 36; // exigência do Google Play desde 31/08/2026

        public const string SettingsDir = "Assets/_Project/Presentation/Settings";
        public const string UrpAssetPath = SettingsDir + "/URP-Mobile.asset";
        public const string UrpRendererPath = SettingsDir + "/URP-Mobile_Renderer.asset";
        public const string BootScenePath = "Assets/_Project/Presentation/Scenes/Boot.unity";

        [MenuItem("Fazenda/Configurar projeto (Etapa 0)")]
        public static void Apply()
        {
            ConfigurePlayer();
            var urp = ConfigureUrp();
            ConfigureQuality(urp);
            GenerateBootScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Projeto configurado.");
        }

        /// <summary>Entrada para -executeMethod; sai com código 1 em caso de erro.</summary>
        public static void ApplyFromCommandLine()
        {
            try
            {
                Apply();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigurePlayer()
        {
            var android = NamedBuildTarget.Android;

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(android, AndroidPackageId);

            // Retrato fixo
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // IL2CPP + ARM64
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // API mínima / alvo
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinAndroidApi;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetAndroidApi;
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;

            // Gráficos: Vulkan com fallback GLES3, espaço de cor linear
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            // Input System novo (0 = antigo, 1 = novo, 2 = ambos). Sem API pública: SerializedObject.
            var playerSettingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (playerSettingsAsset.Length > 0)
            {
                var so = new SerializedObject(playerSettingsAsset[0]);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null && prop.intValue != 1)
                {
                    prop.intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[ProjectSetup] Input System novo ativado (requer reiniciar o editor).");
                }
            }
        }

        private static UniversalRenderPipelineAsset ConfigureUrp()
        {
            Directory.CreateDirectory(SettingsDir);

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UrpRendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, UrpRendererPath);
            }

            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, UrpAssetPath);
            }

            // Perfil mobile. Usa SerializedObject para não depender de setters que mudam entre versões do URP.
            var so = new SerializedObject(urp);
            Set(so, "m_SupportsHDR", false);
            Set(so, "m_MSAA", 2);
            Set(so, "m_RenderScale", 1f);
            Set(so, "m_UseSRPBatcher", true);
            Set(so, "m_SupportsCameraDepthTexture", false);
            Set(so, "m_SupportsCameraOpaqueTexture", false);
            Set(so, "m_MainLightShadowsSupported", true);
            Set(so, "m_MainLightShadowmapResolution", 1024);
            Set(so, "m_ShadowDistance", 30f);
            Set(so, "m_ShadowCascadeCount", 1);
            Set(so, "m_SoftShadowsSupported", false);
            Set(so, "m_AdditionalLightsRenderingMode", 0); // desligadas
            Set(so, "m_AdditionalLightShadowsSupported", false);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);

            GraphicsSettings.defaultRenderPipeline = urp;
            return urp;
        }

        private static void ConfigureQuality(RenderPipelineAsset urp)
        {
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
                QualitySettings.vSyncCount = 0;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        private static void GenerateBootScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BootScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.78f, 0.45f);
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraGo.transform.SetPositionAndRotation(new Vector3(0, 10, -10), Quaternion.Euler(45, 0, 0));

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var labelGo = new GameObject("Status", typeof(RectTransform));
            labelGo.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)labelGo.transform;
            rect.anchorMin = new Vector2(0.05f, 0.3f);
            rect.anchorMax = new Vector2(0.95f, 0.7f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = labelGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 56;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "Carregando...";

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();

            var bootGo = new GameObject("Bootstrap");
            var bootstrap = bootGo.AddComponent<Bootstrap>();
            var bootSo = new SerializedObject(bootstrap);
            bootSo.FindProperty("statusLabel").objectReferenceValue = text;
            bootSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, BootScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };
        }

        private static void Set(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Warn(name); return; }
            p.boolValue = value;
        }

        private static void Set(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Warn(name); return; }
            p.intValue = value; // para enums, intValue grava o valor numérico (não o índice)
        }

        private static void Set(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Warn(name); return; }
            p.floatValue = value;
        }

        private static void Warn(string name) =>
            Debug.LogWarning($"[ProjectSetup] Propriedade URP '{name}' não encontrada nesta versão; ajuste manualmente.");
    }
}
