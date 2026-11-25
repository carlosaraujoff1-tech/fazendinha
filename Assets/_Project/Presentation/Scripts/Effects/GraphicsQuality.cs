using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Presentation.Effects
{
    public enum GraphicsLevel { Baixo = 0, Medio = 1, Alto = 2 }

    /// <summary>
    /// Qualidade gráfica escolhida pelo jogador (salva no aparelho). Ajusta em tempo real: resolução interna,
    /// MSAA, HDR, sombras, pós-processamento, distância de visão e quantidade de capim/arbustos ("Clutter").
    /// Padrão: Médio (fluido na maioria dos celulares).
    /// </summary>
    public sealed class GraphicsQuality : MonoBehaviour
    {
        private const string Key = "graphics_level";
        private static GraphicsQuality _instance;

        public static GraphicsLevel Level { get; private set; } = GraphicsLevel.Medio;

        public static string Name(GraphicsLevel level) =>
            level == GraphicsLevel.Alto ? "Alto" : level == GraphicsLevel.Medio ? "Médio" : "Baixo";

        private void Awake()
        {
            _instance = this;
            int saved = PlayerPrefs.GetInt(Key, (int)GraphicsLevel.Medio);
            Level = (GraphicsLevel)Mathf.Clamp(saved, 0, 2);
        }

        private void Start() => Apply();

        private void OnDestroy() { if (_instance == this) _instance = null; }

        public static void Set(GraphicsLevel level)
        {
            Level = level;
            PlayerPrefs.SetInt(Key, (int)level);
            PlayerPrefs.Save();
            if (_instance != null) _instance.Apply();
        }

        private void Apply()
        {
            var level = Level;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = level == GraphicsLevel.Alto ? 1f : level == GraphicsLevel.Medio ? 0.85f : 0.7f;
                urp.msaaSampleCount = level == GraphicsLevel.Baixo ? 1 : 2;
                urp.supportsHDR = level == GraphicsLevel.Alto;
                urp.shadowDistance = level == GraphicsLevel.Alto ? 45f : level == GraphicsLevel.Medio ? 30f : 18f;
                urp.shadowCascadeCount = level == GraphicsLevel.Alto ? 2 : 1;
            }

            var sun = RenderSettings.sun;
            if (sun != null) sun.shadows = level == GraphicsLevel.Alto ? LightShadows.Soft : LightShadows.Hard;

            var cam = Camera.main;
            if (cam != null)
            {
                cam.farClipPlane = level == GraphicsLevel.Alto ? 320f : level == GraphicsLevel.Medio ? 200f : 130f;
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data != null)
                {
                    data.renderPostProcessing = level != GraphicsLevel.Baixo;
                    data.antialiasing = AntialiasingMode.None; // o MSAA já suaviza; FXAA a mais só custa
                }
            }
            RenderSettings.fogEndDistance = level == GraphicsLevel.Alto ? 230f : level == GraphicsLevel.Medio ? 180f : 120f;
            RenderSettings.fogStartDistance = level == GraphicsLevel.Baixo ? 30f : 45f;

            // Capim/arbustos: todos no Alto, metade no Médio, nenhum no Baixo.
            var clutter = GameObject.Find("Clutter");
            if (clutter != null)
            {
                var t = clutter.transform;
                for (int i = 0; i < t.childCount; i++)
                {
                    bool on = level == GraphicsLevel.Alto || (level == GraphicsLevel.Medio && i % 2 == 0);
                    t.GetChild(i).gameObject.SetActive(on);
                }
            }
        }
    }
}
