using Game.Core.Farm;
using Game.Core.Store;
using Game.Core.Supplier;
using UnityEngine;

namespace Game.Presentation.Effects
{
    /// <summary>Sons de interface e de jogo (catálogo visual). Ouve os eventos do domínio; a UI chama <see cref="Play"/>.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SoundPlayer : MonoBehaviour
    {
        private static SoundPlayer _instance;
        private AudioSource _source;
        private float _lastScan;

        private static bool? _muted;

        /// <summary>Som desligado (salvo no aparelho).</summary>
        public static bool Muted
        {
            get => _muted ?? (bool)(_muted = PlayerPrefs.GetInt("sound_muted", 0) == 1);
            set { _muted = value; PlayerPrefs.SetInt("sound_muted", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        private void Awake()
        {
            _instance = this;
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
        }

        private void Start()
        {
            var events = GameRoot.Instance.Events;
            events.Subscribe<CropHarvested>(_ => Play("harvest"));
            events.Subscribe<SupplierOrderPlaced>(_ => Play("buy"));
            events.Subscribe<ItemScanned>(_ => { if (Time.unscaledTime - _lastScan > 0.08f) { _lastScan = Time.unscaledTime; Play("scan"); } });
            events.Subscribe<CustomerLeft>(e => { if (e.Paid > 0) Play("sale"); });
            GameRoot.Instance.CommandFailed += _ => Play("error");
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        public static void Play(string id)
        {
            if (_instance == null || Muted) return;
            var visuals = GameRoot.Instance != null ? GameRoot.Instance.Visuals : null;
            var sound = visuals != null ? visuals.Sound(id) : null;
            if (sound == null || sound.clip == null) return;
            _instance._source.pitch = Random.Range(0.95f, 1.05f);
            _instance._source.PlayOneShot(sound.clip, sound.volume);
        }
    }
}
