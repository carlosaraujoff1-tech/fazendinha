using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    /// <summary>Mensagem curta no meio da tela (feedback de ação e erros de comando).</summary>
    public sealed class Toast : MonoBehaviour
    {
        [SerializeField] private float seconds = 1.8f;

        private static Toast _instance;
        private Image _panel;
        private Text _label;
        private float _hideAt;

        public static void Show(string message)
        {
            if (_instance != null) _instance.ShowInternal(message);
            else Debug.Log("[Toast] " + message);
        }

        private void Awake()
        {
            _instance = this;
            _panel = UiFactory.Panel("Toast", transform, new Color(0, 0, 0, 0.75f));
            ((RectTransform)_panel.transform).Anchor(new Vector2(0.3f, 0.72f), new Vector2(0.7f, 0.82f));
            _panel.raycastTarget = false;
            _label = UiFactory.Label("Text", _panel.transform, "", 44);
            ((RectTransform)_label.transform).Anchor(Vector2.zero, Vector2.one);
            _panel.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (GameRoot.Instance != null) GameRoot.Instance.CommandFailed += OnCommandFailed;
        }

        private void OnDestroy()
        {
            if (GameRoot.Instance != null) GameRoot.Instance.CommandFailed -= OnCommandFailed;
            if (_instance == this) _instance = null;
        }

        private void OnCommandFailed(string code) => ShowInternal(Game.Core.Display.DisplayFormat.Error(code));

        private void ShowInternal(string message)
        {
            _label.text = message;
            _panel.gameObject.SetActive(true);
            _hideAt = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            if (_panel.gameObject.activeSelf && Time.unscaledTime >= _hideAt) _panel.gameObject.SetActive(false);
        }
    }
}
