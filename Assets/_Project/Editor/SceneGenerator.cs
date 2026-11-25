using System.Collections.Generic;
using System.IO;
using Game.Content;
using Game.Editor.Art;
using Game.Presentation;
using Game.Presentation.Dev;
using Game.Presentation.Effects;
using Game.Presentation.Farm;
using Game.Presentation.Player;
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
    /// Gera a cena principal por código (nunca editar o .unity à mão). Mundo em escala real, terceira pessoa:
    /// fazenda cercada em volta da origem (canteiros de x -9..9, z 0..-13.5), galinheiro e pasto a oeste,
    /// vendinha de sementes a leste, estrada de cascalho (z≈-11) até o mercadinho em <see cref="StoreOrigin"/>
    /// (entrada na frente, voltada para a estrada). Rode "Fazenda/Gerar cena Main" depois de mudar este script.
    /// </summary>
    public static class SceneGenerator
    {
        public const string MainScenePath = "Assets/_Project/Presentation/Scenes/Main.unity";
        public const string CatalogPath = "Assets/_Project/Content/Data/catalog.json";
        public const string SettingsDir = "Assets/_Project/Presentation/Settings";
        public static readonly Vector3 StoreOrigin = new Vector3(40, 0, 10);
        private const float RoadZ = -11f, RoadWidth = 4.6f;
        private const float WallHeight = 3.4f;

        // Áreas onde o cenário aleatório não entra (x0, z0, x1, z1).
        private static readonly List<Rect> Reserved = new List<Rect>();

        private static Material _wood, _plaster, _plasterTint, _gravel, _tiles, _glass, _metal, _lightPanel, _green, _dirt, _leaves, _bark;

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
            CreateMaterials();
            Reserved.Clear();

            // ---------- Luz, céu, neblina, pós ----------
            SetupLighting();
            SetupPostProcessing();

            // ---------- Chão ----------
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(15, 0, -5);
            ground.transform.localScale = new Vector3(26, 1, 26); // 260 m
            ground.GetComponent<Renderer>().sharedMaterial =
                VisualCatalogGenerator.PbrMaterial("Ground_Grass", "grass004", new Vector2(90, 90), new Color(0.92f, 0.96f, 0.88f), 0.05f);
            ground.isStatic = true;

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
            rootGo.AddComponent<GraphicsQuality>();
            rootGo.AddComponent<ScreenshotDirector>();

            // ---------- Fazenda ----------
            var farmGo = new GameObject("Farm");
            var farmView = farmGo.AddComponent<FarmView>();
            var interaction = farmGo.AddComponent<FarmInteraction>();
            SetRef(interaction, "farmView", farmView);
            new GameObject("Animals").AddComponent<AnimalsView>();
            BuildFarmYard();

            // ---------- Mercadinho ----------
            var storeGo = new GameObject("Store");
            storeGo.transform.position = StoreOrigin;
            storeGo.AddComponent<StoreView>();
            BuildStore(visuals, storeGo.transform);

            // ---------- Estrada e cenário ----------
            BuildRoads();
            PlaceDecor(visuals);

            // ---------- Pisos duros (som de passo) ----------
            var surfaces = new GameObject("FootstepSurfaces").AddComponent<FootstepSurfaces>();
            var sso = new SerializedObject(surfaces);
            var areas = sso.FindProperty("hardAreas");
            var hard = new[]
            {
                Rect.MinMaxRect(11f, RoadZ - RoadWidth * 0.5f, 56f, RoadZ + RoadWidth * 0.5f),
                Rect.MinMaxRect(StoreOrigin.x - StoreView.HalfWidth - 1.5f, StoreOrigin.z + StoreView.FrontZ - 6.4f,
                                StoreOrigin.x + StoreView.HalfWidth + 1.5f, StoreOrigin.z + StoreView.BackZ),
                Rect.MinMaxRect(17.5f, -9f, 20.5f, -1.7f),
            };
            areas.arraySize = hard.Length;
            for (int i = 0; i < hard.Length; i++) areas.GetArrayElementAtIndex(i).rectValue = hard[i];
            sso.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Jogadora e câmera ----------
            var playerGo = new GameObject("Player");
            playerGo.transform.SetPositionAndRotation(HudController.FarmSpawn, Quaternion.Euler(0, HudController.FarmSpawnYaw, 0));
            playerGo.AddComponent<CharacterController>();
            playerGo.AddComponent<PlayerController>();
            playerGo.AddComponent<InteractionSystem>();

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.15f;
            camera.farClipPlane = 320f;
            var camData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None; // MSAA do URP já suaviza
            cameraGo.AddComponent<AudioListener>();
            var tpc = cameraGo.AddComponent<ThirdPersonCamera>();
            SetRef(tpc, "target", playerGo.transform);
            var so = new SerializedObject(tpc);
            so.FindProperty("yaw").floatValue = HudController.FarmSpawnYaw;
            so.ApplyModifiedPropertiesWithoutUndo();

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

        // ------------------------------------------------------------------ materiais

        private static void CreateMaterials()
        {
            _wood = VisualCatalogGenerator.PbrMaterial("WoodPlanks", "brown_planks_05", new Vector2(1, 1), new Color(0.78f, 0.56f, 0.38f), 0.12f);
            _leaves = VisualCatalogGenerator.LitMaterial("TreeLeaves", new Color(0.22f, 0.42f, 0.16f), null, Vector2.one, null, 0.05f, false);
            _bark = VisualCatalogGenerator.LitMaterial("TreeBark", new Color(0.36f, 0.26f, 0.18f), null, Vector2.one, null, 0.05f, false);
            _plaster = VisualCatalogGenerator.PbrMaterial("Plaster", "plaster001", new Vector2(3, 1), new Color(0.96f, 0.94f, 0.9f), 0.08f);
            _plasterTint = VisualCatalogGenerator.PbrMaterial("PlasterBarn", "plaster001", new Vector2(2, 1), new Color(0.72f, 0.28f, 0.22f), 0.08f);
            _gravel = VisualCatalogGenerator.PbrMaterial("Gravel", "gravel_ground_01", new Vector2(2, 20), new Color(0.95f, 0.92f, 0.86f), 0.05f);
            _dirt = VisualCatalogGenerator.PbrMaterial("FarmYard", "gravel_ground_01", new Vector2(10, 9), new Color(0.78f, 0.64f, 0.5f), 0.04f);
            _tiles = VisualCatalogGenerator.PbrMaterial("StoreTiles", "tiles062", new Vector2(6, 10), Color.white, 0.55f);
            _metal = VisualCatalogGenerator.LitMaterial("DarkMetal", new Color(0.18f, 0.19f, 0.2f), null, Vector2.one, null, 0.6f, false);
            _green = VisualCatalogGenerator.LitMaterial("BrandGreen", new Color(0.16f, 0.5f, 0.25f), null, Vector2.one, null, 0.35f, false);
            _lightPanel = VisualCatalogGenerator.UnlitMaterial("CeilingLight", new Color(1.6f, 1.55f, 1.4f));
            _glass = GlassMaterial();
        }

        private static Material GlassMaterial()
        {
            string path = VisualCatalogGenerator.MaterialsDir + "/Glass.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", new Color(0.7f, 0.85f, 0.95f, 0.22f));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_Smoothness", 0.95f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ luz e pós

        private static void SetupLighting()
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;
            sunGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            RenderSettings.sun = sun;

            // Céu HDRI (Poly Haven, CC0) em panorâmica; se faltar, céu procedural.
            Directory.CreateDirectory(SettingsDir);
            var hdri = AssetDatabase.LoadAssetAtPath<Texture>("Assets/_Project/Presentation/Textures/Sky/sky_partly_cloudy_2k.hdr");
            string skyPath = SettingsDir + "/Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            var wanted = Shader.Find(hdri != null ? "Skybox/Panoramic" : "Skybox/Procedural");
            if (sky == null || sky.shader != wanted)
            {
                if (sky != null) AssetDatabase.DeleteAsset(skyPath);
                sky = new Material(wanted);
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            if (hdri != null)
            {
                sky.SetTexture("_MainTex", hdri);
                sky.SetFloat("_Mapping", 1f);   // latitude-longitude
                sky.SetFloat("_ImageType", 0f); // 360°
                sky.SetFloat("_Exposure", 1.05f);
                sky.SetFloat("_Rotation", 40f);
                sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
            }
            else
            {
                sky.SetFloat("_SunSize", 0.035f);
                sky.SetFloat("_AtmosphereThickness", 0.75f);
                sky.SetColor("_SkyTint", new Color(0.45f, 0.62f, 0.95f));
                sky.SetFloat("_Exposure", 1.25f);
            }
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            // Luz ambiente em três tons (estável sem bake), puxada para as cores do céu.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.76f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.62f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.3f, 0.25f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.76f, 0.83f, 0.9f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 230f;
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
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = Add<Bloom>();
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.0f);
            bloom.scatter.Override(0.6f);
            var color = Add<ColorAdjustments>();
            color.postExposure.Override(0.35f);
            color.contrast.Override(14f);
            color.saturation.Override(6f);
            var wb = Add<WhiteBalance>();
            wb.temperature.Override(5f);
            var vignette = Add<Vignette>();
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var volumeGo = new GameObject("PostProcess");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // ------------------------------------------------------------------ fazenda

        /// <summary>Chão de terra batida sob os canteiros e cerca de madeira com porteiras (leste e oeste).</summary>
        private static void BuildFarmYard()
        {
            var root = new GameObject("FarmYard").transform;
            const float xMin = -11.8f, xMax = 11.8f, zMin = -16.6f, zMax = 3f;
            Flat("Yard", new Vector3(0, 0.01f, (zMin + zMax) * 0.5f), new Vector3(xMax - xMin, 1, zMax - zMin), _dirt, root);
            Reserve(xMin - 1, zMin - 1, xMax + 1, zMax + 1);

            Fence(new Vector3(xMin, 0, zMax), new Vector3(xMax, 0, zMax), root);
            Fence(new Vector3(xMin, 0, zMin), new Vector3(xMax, 0, zMin), root);
            // Leste: porteira na altura da estrada (z -12.6..-9.4)
            Fence(new Vector3(xMax, 0, zMin), new Vector3(xMax, 0, -12.6f), root);
            Fence(new Vector3(xMax, 0, -9.4f), new Vector3(xMax, 0, zMax), root);
            // Oeste: porteira para o pasto (z -8..-5)
            Fence(new Vector3(xMin, 0, zMin), new Vector3(xMin, 0, -8f), root);
            Fence(new Vector3(xMin, 0, -5f), new Vector3(xMin, 0, zMax), root);

            // Pasto (vacas e ovelhas) e terreiro das galinhas, a oeste
            const float pxMin = -28f, pxMax = -15.5f, pzMin = -13f, pzMax = -1.2f;
            Fence(new Vector3(pxMin, 0, pzMax), new Vector3(pxMax, 0, pzMax), root);
            Fence(new Vector3(pxMin, 0, pzMin), new Vector3(pxMax, 0, pzMin), root);
            Fence(new Vector3(pxMin, 0, pzMin), new Vector3(pxMin, 0, pzMax), root);
            Fence(new Vector3(pxMax, 0, pzMin), new Vector3(pxMax, 0, -8f), root);
            Fence(new Vector3(pxMax, 0, -5f), new Vector3(pxMax, 0, pzMax), root);
            Reserve(pxMin - 1, pzMin - 1, pxMax + 1, pzMax + 1);
            Reserve(-24f, 0f, -14f, 10f); // galinheiro
            // Trilha entre a porteira oeste e o pasto
            Flat("PathPasture", new Vector3(-13.6f, 0.012f, -6.5f), new Vector3(4.2f, 1, 3f), _dirt, root);
        }

        /// <summary>Cerca de madeira de verdade: mourões a cada ~2,2 m e duas travessas, com colisão.</summary>
        private static void Fence(Vector3 a, Vector3 b, Transform parent)
        {
            var dir = b - a;
            float len = dir.magnitude;
            if (len < 0.3f) return;
            var rot = Quaternion.LookRotation(dir / len, Vector3.up);
            var seg = new GameObject("Fence").transform;
            seg.SetParent(parent, false);
            seg.SetPositionAndRotation(a, rot);
            int posts = Mathf.Max(1, Mathf.CeilToInt(len / 2.2f));
            for (int i = 0; i <= posts; i++)
                Box("Post", seg, new Vector3(0, 0.6f, len * i / posts), new Vector3(0.14f, 1.2f, 0.14f), _wood, false);
            Box("RailTop", seg, new Vector3(0, 0.95f, len * 0.5f), new Vector3(0.06f, 0.14f, len), _wood, false);
            Box("RailLow", seg, new Vector3(0, 0.5f, len * 0.5f), new Vector3(0.06f, 0.14f, len), _wood, false);
            var col = seg.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.7f, len * 0.5f);
            col.size = new Vector3(0.25f, 1.4f, len);
            seg.gameObject.isStatic = true;
        }

        // ------------------------------------------------------------------ estrada

        private static void BuildRoads()
        {
            var root = new GameObject("Roads").transform;
            // Estrada principal: da porteira da fazenda até depois do mercadinho
            const float x0 = 11f, x1 = 56f;
            var road = Flat("Road", new Vector3((x0 + x1) * 0.5f, 0.015f, RoadZ), new Vector3(x1 - x0, 1, RoadWidth), _gravel, root);
            road.GetComponent<Renderer>().sharedMaterial = VisualCatalogGenerator.PbrMaterial("Road", "gravel_ground_01",
                new Vector2((x1 - x0) / 4f, 1.2f), new Color(0.88f, 0.85f, 0.8f), 0.05f);
            Reserve(x0, RoadZ - RoadWidth * 0.5f - 1.5f, x1, RoadZ + RoadWidth * 0.5f + 1.5f);
            // Pátio na frente do mercadinho (calçada)
            var s = StoreOrigin;
            Flat("StorePlaza", new Vector3(s.x + 1, 0.018f, s.z + StoreView.FrontZ - 3.2f), new Vector3(15f, 1, 6.4f),
                VisualCatalogGenerator.PbrMaterial("Plaza", "tiles062", new Vector2(8, 3.5f), new Color(0.8f, 0.8f, 0.78f), 0.2f), root);
            // Acesso à vendinha de sementes
            Flat("PathSeedShop", new Vector3(19f, 0.013f, -5.4f), new Vector3(3f, 1, 7.4f),
                VisualCatalogGenerator.PbrMaterial("PathSeeds", "gravel_ground_01", new Vector2(1, 2.5f), new Color(0.88f, 0.85f, 0.8f), 0.05f), root);
        }

        // ------------------------------------------------------------------ loja

        /// <summary>
        /// Mercadinho de alvenaria: paredes rebocadas, piso de cerâmica, vitrines de vidro na frente, porta larga,
        /// teto com luminárias, letreiro verde e toldo. Medidas em <see cref="StoreView"/>.
        /// </summary>
        private static void BuildStore(VisualCatalog v, Transform store)
        {
            float back = StoreView.BackZ, front = StoreView.FrontZ, hw = StoreView.HalfWidth, h = WallHeight;
            float doorL = StoreView.DoorX - StoreView.DoorWidth * 0.5f, doorR = StoreView.DoorX + StoreView.DoorWidth * 0.5f;
            float depth = back - front, midZ = (back + front) * 0.5f;
            var p = store.position;
            Reserve(p.x - hw - 1.5f, p.z + front - 1f, p.x + hw + 4.5f, p.z + back + 1.5f);

            Flat("Floor", p + new Vector3(0, 0.02f, midZ), new Vector3(hw * 2, 1, depth), _tiles, store);

            // Paredes
            Box("WallBack", store, new Vector3(0, h * 0.5f, back), new Vector3(hw * 2 + 0.2f, h, 0.2f), _plaster, true, local: true);
            Box("WallLeft", store, new Vector3(-hw, h * 0.5f, midZ), new Vector3(0.2f, h, depth), _plaster, true, local: true);
            Box("WallRight", store, new Vector3(hw, h * 0.5f, midZ), new Vector3(0.2f, h, depth), _plaster, true, local: true);
            // Frente: mureta + vitrine + viga, com a porta aberta no meio
            FrontSegment(store, -hw, doorL, front, h);
            FrontSegment(store, doorR, hw, front, h);
            Box("DoorHeader", store, new Vector3((doorL + doorR) * 0.5f, (2.7f + h) * 0.5f, front), new Vector3(doorR - doorL, h - 2.7f, 0.22f), _plaster, true, local: true);
            Box("DoorFrameL", store, new Vector3(doorL, 1.35f, front), new Vector3(0.1f, 2.7f, 0.26f), _metal, false, local: true);
            Box("DoorFrameR", store, new Vector3(doorR, 1.35f, front), new Vector3(0.1f, 2.7f, 0.26f), _metal, false, local: true);
            // Porta de vidro aberta (de correr, recolhida na lateral)
            Box("DoorGlass", store, new Vector3(doorR + 0.75f, 1.35f, front - 0.16f), new Vector3(1.5f, 2.6f, 0.03f), _glass, false, local: true);

            // Teto (não projeta sombra, para o sol "entrar" como iluminação interna) e luminárias
            var ceiling = Box("Ceiling", store, new Vector3(0, h + 0.08f, midZ), new Vector3(hw * 2 + 0.4f, 0.16f, depth + 0.4f), _plaster, true, local: true);
            ceiling.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            for (float z = back - 2.5f; z > front + 1f; z -= 3.2f)
            for (int c = -1; c <= 1; c += 2)
            {
                var lamp = Box("CeilingLight", store, new Vector3(c * 2.6f, h - 0.02f, z), new Vector3(1.4f, 0.04f, 0.35f), _lightPanel, false, local: true);
                lamp.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            // Platibanda + letreiro
            Box("Parapet", store, new Vector3(0, h + 0.5f, front - 0.05f), new Vector3(hw * 2 + 0.4f, 0.8f, 0.3f), _plaster, false, local: true);
            Box("SignBoard", store, new Vector3(StoreView.DoorX, h + 0.55f, front - 0.25f), new Vector3(7.5f, 0.75f, 0.1f), _green, false, local: true);
            SignText(store, "MERCADINHO DA FAZENDA", new Vector3(StoreView.DoorX, h + 0.55f, front - 0.31f), v);
            Place(v, "awning", p + new Vector3(StoreView.DoorX, 2.75f, front - 0.25f), 0, store);

            // Decoração interna: prateleiras de fundo cheias, geladeiras, pães, carrinhos, caixas
            Place(v, "shelfBoxes", p + new Vector3(-3.8f, 0, back - 0.6f), 180f, store);
            Place(v, "shelfBags", p + new Vector3(0f, 0, back - 0.6f), 180f, store);
            Place(v, "shelfBoxes", p + new Vector3(3.8f, 0, back - 0.6f), 180f, store);
            Place(v, "fridgeTall", p + new Vector3(hw - 0.6f, 0, -3.5f), -90f, store);
            Place(v, "fridgeTall", p + new Vector3(hw - 0.6f, 0, -6.2f), -90f, store);
            Place(v, "bread", p + new Vector3(hw - 0.7f, 0, -10.5f), -90f, store);
            Place(v, "cart", p + new Vector3(doorR + 0.6f, 0, front + 1.0f), 200f, store);
            Place(v, "cart", p + new Vector3(doorR + 1.2f, 0, front + 1.1f), 190f, store);
            Place(v, "cardboard", p + new Vector3(-hw + 0.6f, 0, back - 2.2f), 15f, store);
            Place(v, "plasticCrate", p + new Vector3(-hw + 0.7f, 0, back - 3.2f), 70f, store);

            // Do lado de fora: bancos, floreiras, caixotes e o caminhão do fornecedor
            Place(v, "bench", p + new Vector3(-4.2f, 0, front - 1.4f), 180f, store);
            Place(v, "planter", p + new Vector3(doorL - 0.9f, 0, front - 0.6f), 0f, store);
            Place(v, "planter", p + new Vector3(doorR + 2.6f, 0, front - 0.6f), 0f, store);
            Place(v, "crate", p + new Vector3(hw + 1.2f, 0, front + 2f), 20f, store);
            Place(v, "crate", p + new Vector3(hw + 1.3f, 0.0f, front + 2.9f), 65f, store);
            var truck = Place(v, "truck", p + new Vector3(hw + 3.2f, 0, front + 6f), 180f, store);
            if (truck != null)
            {
                truck.isStatic = false;
                Solid(truck, 0.95f);
                truck.AddComponent<SupplierTruckView>();
            }
        }

        private static void FrontSegment(Transform store, float x0, float x1, float z, float h)
        {
            float w = x1 - x0, cx = (x0 + x1) * 0.5f;
            if (w < 0.2f) return;
            Box("Sill", store, new Vector3(cx, 0.45f, z), new Vector3(w, 0.9f, 0.22f), _plaster, true, local: true);
            Box("Header", store, new Vector3(cx, (2.7f + h) * 0.5f, z), new Vector3(w, h - 2.7f, 0.22f), _plaster, true, local: true);
            Box("Glass", store, new Vector3(cx, 1.8f, z), new Vector3(w, 1.8f, 0.04f), _glass, false, local: true);
            Box("SillTop", store, new Vector3(cx, 0.92f, z), new Vector3(w, 0.05f, 0.3f), _metal, false, local: true);
            int mullions = Mathf.Max(1, Mathf.RoundToInt(w / 1.8f));
            for (int i = 0; i <= mullions; i++)
                Box("Mullion", store, new Vector3(x0 + w * i / mullions, 1.8f, z), new Vector3(0.07f, 1.8f, 0.08f), _metal, false, local: true);
        }

        private static void SignText(Transform parent, string text, Vector3 localPos, VisualCatalog v)
        {
            var go = new GameObject("SignText");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            var tm = go.AddComponent<TextMesh>();
            var font = v != null && v.font != null ? v.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = 0.055f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.97f, 0.88f);
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            // O TextMesh é legível olhando na direção +z, ou seja, de quem está na estrada olhando a fachada.
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
            if (id.StartsWith("tree")) RecolorTree(go);
            return go;
        }

        /// <summary>Árvores Kenney vêm verde-água: folhas viram verde-escuro natural e o tronco marrom.</summary>
        private static void RecolorTree(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var c = mats[i].HasProperty("_BaseColor") ? mats[i].GetColor("_BaseColor") : Color.white;
                    mats[i] = c.g > c.r * 1.15f ? _leaves : _bark;
                }
                r.sharedMaterials = mats;
            }
        }

        /// <summary>Colisor de caixa do tamanho do modelo (prédios, veículos, poço…).</summary>
        private static void Solid(GameObject go, float shrink = 1f)
        {
            if (go == null || go.GetComponentInChildren<Collider>() != null) return;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            var col = go.AddComponent<BoxCollider>();
            var t = go.transform;
            col.center = t.InverseTransformPoint(b.center);
            var size = t.InverseTransformVector(b.size);
            col.size = new Vector3(Mathf.Abs(size.x) * shrink, Mathf.Abs(size.y), Mathf.Abs(size.z) * shrink);
        }

        private static void NoShadow(GameObject go)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Tronco: cápsula fina no pé da árvore.</summary>
        private static void Trunk(GameObject go, float radius)
        {
            if (go == null) return;
            var col = go.AddComponent<CapsuleCollider>();
            float s = Mathf.Max(0.01f, go.transform.lossyScale.x);
            col.radius = radius / s;
            col.height = 3f / s;
            col.center = new Vector3(0, 1.5f / s, 0);
        }

        private static void PlaceDecor(VisualCatalog v)
        {
            var root = new GameObject("Decor").transform;
            // Capim e arbustos ficam num grupo próprio: a qualidade gráfica liga/desliga parte deles.
            var clutter = new GameObject("Clutter").transform;

            // Construções da fazenda (com colisão)
            Solid(Place(v, "barn", new Vector3(-2f, 0, 10f), 180, root), 0.9f);
            Solid(Place(v, "silo", new Vector3(5.5f, 0, 11f), 0, root), 0.8f);
            Solid(Place(v, "windmill", new Vector3(-12f, 0, 13f), 160, root), 0.5f);
            Solid(Place(v, "well", new Vector3(14.5f, 0, 2.5f), 30, root), 0.9f);
            Solid(Place(v, "tractor", new Vector3(9.5f, 0, 6.5f), 215, root), 0.9f);
            Reserve(-9, 5, 11, 17);
            Reserve(12, 0, 17, 5);

            // Vendinha de sementes (galpão aberto) com sacaria e caixotes na frente
            var seedShop = Place(v, "openbarn", new Vector3(19f, 0, 0.5f), 180, root);
            if (seedShop != null)
            {
                seedShop.isStatic = false;
                seedShop.AddComponent<SeedShopView>();
                Place(v, "crate", new Vector3(17.6f, 0, -1.8f), 15, root);
                Place(v, "crate", new Vector3(17.9f, 0.0f, -2.6f), 50, root);
                Place(v, "cardboard", new Vector3(20.4f, 0, -1.9f), -20, root);
                Place(v, "plasticCrate", new Vector3(21f, 0, -2.6f), 80, root);
                PlaceProductProp(v, "trigo", new Vector3(18.6f, 0, -2.2f), root);
                PlaceProductProp(v, "milho", new Vector3(19.4f, 0, -2.4f), root);
                PlaceProductProp(v, "alface", new Vector3(20.0f, 0, -2.9f), root);
            }
            Reserve(15, -3.5f, 23.5f, 5);

            // Galinheiro (as galinhas em volta vêm do AnimalsView); porco só de enfeite no pasto
            Solid(Place(v, "coop", new Vector3(-19f, 0, 7.2f), 170, root), 0.9f);
            Place(v, "pig", new Vector3(-26f, 0, -11.5f), 120, root);

            // Postes ao longo da estrada e bancos/floreiras
            for (float x = 14f; x <= 54f; x += 10f)
                Solid(Place(v, "streetLamp", new Vector3(x, 0, RoadZ + RoadWidth * 0.5f + 0.7f), 180, root), 0.3f);
            Place(v, "bench", new Vector3(16f, 0, RoadZ + RoadWidth * 0.5f + 1.2f), 180, root);
            Place(v, "planter", new Vector3(12.8f, 0, -8.5f), 0, root);
            Place(v, "planter", new Vector3(12.8f, 0, -13.6f), 0, root);

            var rng = new System.Random(42);
            bool Free(float x, float z, float margin)
            {
                foreach (var r in Reserved)
                    if (x > r.xMin - margin && x < r.xMax + margin && z > r.yMin - margin && z < r.yMax + margin) return false;
                return true;
            }

            // Árvores: moldura distante (estilizadas, a neblina suaviza) — com tronco sólido
            string[] trees = { "tree1", "tree2", "tree3", "tree4", "tree5" };
            int placedTrees = 0;
            for (int tries = 0; placedTrees < 90 && tries < 5000; tries++)
            {
                float x = (float)(rng.NextDouble() * 140 - 55);
                float z = (float)(rng.NextDouble() * 100 - 55);
                if (!Free(x, z, 4f)) continue;
                // Mais densas longe do centro; perto, só algumas
                float d = new Vector2(x - 15, z + 3).magnitude;
                if (d < 22 && rng.NextDouble() < 0.75) continue;
                float s = 1.1f + (float)rng.NextDouble() * 0.6f;
                Trunk(Place(v, trees[rng.Next(trees.Length)], new Vector3(x, 0, z), (float)(rng.NextDouble() * 360), root, s), 0.35f);
                placedTrees++;
            }

            // Arbustos realistas (Poly Haven), tocos e pedras
            string[] shrubs = { "shrubA", "shrubB", "shrubC" };
            for (int n = 0, tries = 0; n < 55 && tries < 4000; tries++)
            {
                float x = (float)(rng.NextDouble() * 100 - 40);
                float z = (float)(rng.NextDouble() * 70 - 40);
                if (!Free(x, z, 1f)) continue;
                NoShadow(Place(v, shrubs[rng.Next(shrubs.Length)], new Vector3(x, 0, z), (float)(rng.NextDouble() * 360), clutter, 0.8f + (float)rng.NextDouble() * 0.6f));
                n++;
            }
            for (int n = 0, tries = 0; n < 10 && tries < 2000; tries++)
            {
                float x = (float)(rng.NextDouble() * 90 - 35);
                float z = (float)(rng.NextDouble() * 60 - 35);
                if (!Free(x, z, 1f)) continue;
                Place(v, n % 2 == 0 ? "stumpPH" : "rockS1", new Vector3(x, 0, z), (float)(rng.NextDouble() * 360), root, n % 2 == 0 ? 1f : 2.2f);
                n++;
            }

            // Tufos de capim (recorte por alfa, instanciados) — dão volume ao gramado
            for (int n = 0, tries = 0; n < 220 && tries < 8000; tries++)
            {
                float x = (float)(rng.NextDouble() * 90 - 35);
                float z = (float)(rng.NextDouble() * 60 - 35);
                if (!Free(x, z, 0.2f)) continue;
                NoShadow(Place(v, "grassClump", new Vector3(x, 0, z), (float)(rng.NextDouble() * 360), clutter, 0.7f + (float)rng.NextDouble() * 0.6f));
                n++;
            }
            // Capim ao longo das cercas da fazenda
            for (float x = -11.5f; x <= 11.5f; x += 1.3f)
            {
                NoShadow(Place(v, "grassClump", new Vector3(x + (float)rng.NextDouble() * 0.4f, 0, 3.4f), (float)(rng.NextDouble() * 360), clutter));
                NoShadow(Place(v, "grassClump", new Vector3(x + (float)rng.NextDouble() * 0.4f, 0, -17f), (float)(rng.NextDouble() * 360), clutter));
            }
        }

        private static void PlaceProductProp(VisualCatalog v, string productId, Vector3 pos, Transform parent)
        {
            var prefab = v.Product(productId);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.localScale *= 2.2f;
            go.isStatic = true;
        }

        // ------------------------------------------------------------------ utilidades

        private static void Reserve(float x0, float z0, float x1, float z1) => Reserved.Add(Rect.MinMaxRect(x0, z0, x1, z1));

        private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool collider, bool local = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        private static GameObject Flat(string name, Vector3 position, Vector3 size, Material mat, Transform parent)
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
            return go;
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
