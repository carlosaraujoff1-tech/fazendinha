using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.State;
using Game.Core.Time;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Core.Save
{
    /// <summary>Armazenamento bruto do save. A implementação real (disco) fica em Game.Services.</summary>
    public interface ISaveStore
    {
        /// <summary>Conteúdo do save principal ou null se não existir.</summary>
        string ReadPrimary();

        /// <summary>Conteúdo do backup (último save válido anterior) ou null.</summary>
        string ReadBackup();

        /// <summary>Grava de forma atômica: ou o save novo fica inteiro, ou o anterior continua intacto.
        /// O save anterior vira backup.</summary>
        void WriteAtomic(string content);
    }

    /// <summary>Migra o JSON de <see cref="FromVersion"/> para FromVersion + 1.</summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }
        void Migrate(JObject root);
    }

    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>JSON versionado do <see cref="GameState"/> com cadeia de migrações.</summary>
    public sealed class SaveSerializer
    {
        public const string SchemaVersionField = nameof(GameState.SchemaVersion);

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        private readonly Dictionary<int, ISaveMigration> _migrations;
        private readonly int _currentVersion;

        public SaveSerializer(IEnumerable<ISaveMigration> migrations = null, int currentVersion = GameState.CurrentSchemaVersion)
        {
            _currentVersion = currentVersion;
            _migrations = new Dictionary<int, ISaveMigration>();
            foreach (var m in migrations ?? Enumerable.Empty<ISaveMigration>())
            {
                if (_migrations.ContainsKey(m.FromVersion))
                    throw new ArgumentException($"Duas migrações a partir da versão {m.FromVersion}.");
                _migrations[m.FromVersion] = m;
            }
        }

        public string Serialize(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.SchemaVersion = _currentVersion;
            return JsonConvert.SerializeObject(state, Settings);
        }

        public GameState Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new SaveFormatException("Save vazio.");

            JObject root;
            try { root = JObject.Parse(json); }
            catch (JsonException e) { throw new SaveFormatException("JSON inválido.", e); }

            var versionToken = root[SchemaVersionField];
            if (versionToken == null || versionToken.Type != JTokenType.Integer)
                throw new SaveFormatException("Save sem schemaVersion.");

            int version = versionToken.Value<int>();
            if (version > _currentVersion)
                throw new SaveFormatException($"Save da versão {version} é mais novo que o jogo ({_currentVersion}).");
            if (version < 1) throw new SaveFormatException($"schemaVersion inválido: {version}.");

            while (version < _currentVersion)
            {
                if (!_migrations.TryGetValue(version, out var migration))
                    throw new SaveFormatException($"Falta migração da versão {version}.");
                migration.Migrate(root);
                version++;
                root[SchemaVersionField] = version;
            }

            GameState state;
            try { state = root.ToObject<GameState>(JsonSerializer.Create(Settings)); }
            catch (JsonException e) { throw new SaveFormatException("Estrutura do save inválida.", e); }

            Validate(state);
            return state;
        }

        private static void Validate(GameState s)
        {
            if (s == null) throw new SaveFormatException("Save nulo.");
            if (s.Wallet == null) throw new SaveFormatException("Save sem carteira.");
            if (s.Warehouse == null) throw new SaveFormatException("Save sem armazém.");
            if (s.Stats == null) s.Stats = new StatsState();
            if (s.Wallet.Coins < 0 || s.Wallet.Gems < 0) throw new SaveFormatException("Saldo negativo no save.");
            foreach (var lot in s.Warehouse)
            {
                if (lot == null || string.IsNullOrEmpty(lot.ProductId) || lot.Quantity <= 0)
                    throw new SaveFormatException("Lote inválido no save.");
            }
        }
    }

    public enum SaveSource { None, Primary, Backup }

    public sealed class LoadResult
    {
        public LoadResult(GameState state, SaveSource source, string primaryError)
        {
            State = state; Source = source; PrimaryError = primaryError;
        }

        /// <summary>Estado carregado, ou null se não havia save (jogo novo).</summary>
        public GameState State { get; }
        public SaveSource Source { get; }

        /// <summary>Por que o save principal foi recusado (quando caiu no backup ou falhou).</summary>
        public string PrimaryError { get; }
    }

    /// <summary>Salva e carrega o <see cref="GameState"/>, caindo para o backup se o principal estiver corrompido.</summary>
    public sealed class SaveSystem
    {
        private readonly ISaveStore _store;
        private readonly SaveSerializer _serializer;
        private readonly IClock _clock;

        public SaveSystem(ISaveStore store, SaveSerializer serializer, IClock clock)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public void Save(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.LastSavedAtMs = _clock.UtcNowMs;
            _store.WriteAtomic(_serializer.Serialize(state));
        }

        /// <summary>
        /// Carrega o save. Sem nenhum arquivo: State = null (jogo novo). Principal corrompido: usa o backup.
        /// Ambos corrompidos: lança <see cref="SaveFormatException"/> (nunca sobrescreve um save ilegível em silêncio).
        /// </summary>
        public LoadResult Load()
        {
            string primary = _store.ReadPrimary();
            string primaryError = null;
            if (primary != null)
            {
                try { return new LoadResult(_serializer.Deserialize(primary), SaveSource.Primary, null); }
                catch (SaveFormatException e) { primaryError = e.Message; }
            }

            string backup = _store.ReadBackup();
            if (backup != null)
            {
                try { return new LoadResult(_serializer.Deserialize(backup), SaveSource.Backup, primaryError); }
                catch (SaveFormatException e)
                {
                    throw new SaveFormatException($"Save principal e backup inválidos. Principal: {primaryError ?? "ausente"}; backup: {e.Message}", e);
                }
            }

            if (primaryError != null) throw new SaveFormatException("Save principal inválido e sem backup: " + primaryError);
            return new LoadResult(null, SaveSource.None, null);
        }
    }

    /// <summary>Armazenamento em memória (testes e simulador). Mesma semântica do de disco.</summary>
    public sealed class InMemorySaveStore : ISaveStore
    {
        public string Primary { get; set; }
        public string Backup { get; set; }
        public int Writes { get; private set; }

        public string ReadPrimary() => Primary;
        public string ReadBackup() => Backup;

        public void WriteAtomic(string content)
        {
            if (Primary != null) Backup = Primary;
            Primary = content;
            Writes++;
        }
    }
}
