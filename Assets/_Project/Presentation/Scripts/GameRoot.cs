using System;
using System.IO;
using Game.Content;
using Game.Core.Catalog;
using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Save;
using Game.Core.Simulation;
using Game.Core.State;
using Game.Core.Time;
using Game.Services;
using Game.Services.FileSystem;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Raiz do jogo na cena: monta catálogo, relógio, eventos, save e executor de comandos.
    /// A UI só lê o estado e envia comandos por <see cref="Execute"/>.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] private TextAsset catalogJson;
        [SerializeField] private float tickIntervalSeconds = 0.25f;
        [SerializeField] private float autosaveIntervalSeconds = 30f;

        private SaveSystem _saveSystem;
        private float _nextTick;
        private float _nextAutosave;

        public static GameRoot Instance { get; private set; }

        public GameContext Context { get; private set; }
        public CommandExecutor Commands { get; private set; }
        public IEventBus Events => Context.Events;
        public GameCatalog Catalog => Context.Catalog;
        public GameState State => Context.State;
        public long NowMs => Context.NowMs;

        /// <summary>Disparado a cada comando rejeitado (para a UI mostrar o motivo).</summary>
        public event Action<string> CommandFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            var catalog = ContentLoader.LoadCatalog(catalogJson);
            var clock = new SystemClock();
            var store = new AtomicFileSaveStore(UnitySavePaths.SaveDirectory);
            _saveSystem = new SaveSystem(store, new SaveSerializer(), clock);

            GameState state = LoadOrCreate(store, catalog, clock);
            Context = new GameContext(state, catalog, clock, new EventBus());
            Commands = GameCommands.CreateExecutor(Context);
            GameSimulation.Tick(Context);
            _nextAutosave = Time.unscaledTime + autosaveIntervalSeconds;
        }

        private GameState LoadOrCreate(AtomicFileSaveStore store, GameCatalog catalog, IClock clock)
        {
            try
            {
                var result = _saveSystem.Load();
                if (result.Source == SaveSource.Backup)
                    Debug.LogWarning("[Save] Principal inválido, carregado o backup: " + result.PrimaryError);
                if (result.State != null) return result.State;
            }
            catch (SaveFormatException e)
            {
                // Nunca apaga: guarda os arquivos ilegíveis ao lado para análise e começa de novo.
                Debug.LogError("[Save] Saves ilegíveis, começando jogo novo. " + e.Message);
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                TryMove(store.PrimaryPath, store.PrimaryPath + ".corrupt-" + stamp);
                TryMove(store.BackupPath, store.BackupPath + ".corrupt-" + stamp);
            }
            return NewGame.Create(catalog, clock);
        }

        private static void TryMove(string from, string to)
        {
            try { if (File.Exists(from)) File.Move(from, to); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public CommandResult Execute(ICommand command)
        {
            var result = Commands.Execute(command);
            if (!result.IsSuccess) CommandFailed?.Invoke(result.Error);
            return result;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now >= _nextTick)
            {
                _nextTick = now + tickIntervalSeconds;
                GameSimulation.Tick(Context);
            }
            if (now >= _nextAutosave)
            {
                _nextAutosave = now + autosaveIntervalSeconds;
                SaveNow();
            }
        }

        public void SaveNow()
        {
            if (Context == null) return;
            try { _saveSystem.Save(Context.State); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow();
        }

        private void OnApplicationQuit() => SaveNow();

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
