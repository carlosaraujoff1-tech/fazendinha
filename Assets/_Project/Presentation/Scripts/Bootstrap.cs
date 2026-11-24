using Game.Core.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>
    /// Ponto de entrada da cena Boot. Na Etapa 0 só configura a taxa de quadros e mostra
    /// um texto de diagnóstico provando que Game.Core está linkado no build.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private Text statusLabel;
        [SerializeField] private int targetFrameRate = 60;

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        private void Start()
        {
            if (statusLabel == null) return;
            long sample = CostCurve.RepeatableCost(100, 1);
            statusLabel.text =
                $"{Application.productName}\n" +
                $"v{Application.version} · Etapa 0\n" +
                $"Core OK (custo(1) = {sample})\n" +
                $"{SystemInfo.deviceModel}\n" +
                $"{SystemInfo.graphicsDeviceType}";
        }
    }
}
