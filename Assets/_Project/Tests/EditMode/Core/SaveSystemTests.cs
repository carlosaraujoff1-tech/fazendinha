using System;
using Game.Core.Catalog;
using Game.Core.Inventory;
using Game.Core.Save;
using Game.Core.Simulation;
using Game.Core.State;
using Game.Core.Time;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.Core
{
    public class SaveSystemTests
    {
        private ManualClock _clock;
        private GameCatalog _catalog;
        private SaveSerializer _serializer;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock();
            _catalog = TestCatalogs.Mvp();
            _serializer = new SaveSerializer();
        }

        private GameState SampleState()
        {
            var state = NewGame.Create(_catalog, _clock);
            state.Wallet.Coins = 1234;
            state.Wallet.Gems = 7;
            state.Stats.UnitsExpired = 3;
            var inv = new LotInventory(state.Warehouse);
            inv.Add(LotInventory.CreateLot(_catalog.GetProduct("alface"), 4, _clock.UtcNowMs, LotSource.Farm));
            inv.Add(LotInventory.CreateLot(_catalog.GetProduct("arroz"), 9, _clock.UtcNowMs, LotSource.Supplier));
            return state;
        }

        [Test]
        public void RoundTrip_PreservesEverything()
        {
            var original = SampleState();
            string json = _serializer.Serialize(original);
            var loaded = _serializer.Deserialize(json);

            Assert.AreEqual(json, _serializer.Serialize(loaded));
            Assert.AreEqual(1234, loaded.Wallet.Coins);
            Assert.AreEqual(2, loaded.Warehouse.Count);
            Assert.AreEqual(LotSource.Supplier, loaded.Warehouse[1].Source);
            Assert.AreEqual(StockLot.NeverExpires, loaded.Warehouse[1].ExpiresAtMs);
            Assert.AreEqual(original.Warehouse[0].ExpiresAtMs, loaded.Warehouse[0].ExpiresAtMs);
            StringAssert.Contains("\"SchemaVersion\":1", json);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{ isso não é json")]
        [TestCase("{\"Wallet\":{}}")]
        [TestCase("{\"SchemaVersion\":\"1\"}")]
        [TestCase("{\"SchemaVersion\":0}")]
        [TestCase("{\"SchemaVersion\":99}")]
        [TestCase("{\"SchemaVersion\":1,\"Wallet\":null,\"Warehouse\":[]}")]
        [TestCase("{\"SchemaVersion\":1,\"Wallet\":{},\"Warehouse\":null}")]
        [TestCase("{\"SchemaVersion\":1,\"Wallet\":{\"Coins\":-1},\"Warehouse\":[]}")]
        [TestCase("{\"SchemaVersion\":1,\"Wallet\":{},\"Warehouse\":[{\"ProductId\":\"alface\",\"Quantity\":0}]}")]
        [TestCase("{\"SchemaVersion\":1,\"Wallet\":{},\"Warehouse\":[null]}")]
        [TestCase("{\"SchemaVersion\":1,\"Wallet\":{\"Coins\":\"muito\"},\"Warehouse\":[]}")]
        public void Deserialize_RejectsBadSaves(string json)
        {
            Assert.Throws<SaveFormatException>(() => _serializer.Deserialize(json));
        }

        [Test]
        public void Deserialize_FillsMissingStats()
        {
            var state = _serializer.Deserialize("{\"SchemaVersion\":1,\"Wallet\":{\"Coins\":5},\"Warehouse\":[],\"Stats\":null}");
            Assert.IsNotNull(state.Stats);
            Assert.AreEqual(5, state.Wallet.Coins);
        }

        [Test]
        public void Serialize_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _serializer.Serialize(null));
        }

        // Simula uma versão futura (2) que renomeou "Wallet.Coins" de um campo antigo "Money".
        private sealed class MoneyToWallet : ISaveMigration
        {
            public int FromVersion => 1;
            public void Migrate(JObject root)
            {
                long money = root.Value<long?>("Money") ?? 0;
                root.Remove("Money");
                root["Wallet"] = new JObject { ["Coins"] = money, ["Gems"] = 0 };
            }
        }

        [Test]
        public void Migrations_RunInOrderUpToCurrentVersion()
        {
            var serializer = new SaveSerializer(new ISaveMigration[] { new MoneyToWallet() }, currentVersion: 2);
            var state = serializer.Deserialize("{\"SchemaVersion\":1,\"Money\":77,\"Warehouse\":[]}");
            Assert.AreEqual(77, state.Wallet.Coins);
            Assert.AreEqual(2, state.SchemaVersion);
            StringAssert.Contains("\"SchemaVersion\":2", serializer.Serialize(state));
        }

        [Test]
        public void Migrations_MissingStep_Throws()
        {
            var serializer = new SaveSerializer(null, currentVersion: 3);
            var ex = Assert.Throws<SaveFormatException>(() => serializer.Deserialize("{\"SchemaVersion\":1,\"Wallet\":{},\"Warehouse\":[]}"));
            StringAssert.Contains("Falta migração", ex.Message);
        }

        [Test]
        public void Migrations_DuplicateStep_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SaveSerializer(new ISaveMigration[] { new MoneyToWallet(), new MoneyToWallet() }, 2));
        }

        [Test]
        public void SaveSystem_SavesAndLoads_WithTimestamp()
        {
            var store = new InMemorySaveStore();
            var system = new SaveSystem(store, _serializer, _clock);
            var state = SampleState();
            _clock.AdvanceSeconds(30);

            system.Save(state);
            var result = system.Load();

            Assert.AreEqual(SaveSource.Primary, result.Source);
            Assert.AreEqual(_clock.UtcNowMs, result.State.LastSavedAtMs);
            Assert.AreEqual(1234, result.State.Wallet.Coins);
            Assert.IsNull(result.PrimaryError);
        }

        [Test]
        public void SaveSystem_NoSave_MeansNewGame()
        {
            var result = new SaveSystem(new InMemorySaveStore(), _serializer, _clock).Load();
            Assert.AreEqual(SaveSource.None, result.Source);
            Assert.IsNull(result.State);
        }

        [Test]
        public void SaveSystem_CorruptPrimary_FallsBackToPreviousSave()
        {
            var store = new InMemorySaveStore();
            var system = new SaveSystem(store, _serializer, _clock);
            var state = SampleState();
            system.Save(state);              // vira backup
            state.Wallet.Coins = 9999;
            system.Save(state);              // principal
            store.Primary = store.Primary.Substring(0, store.Primary.Length / 2); // corrompido (app morreu?)

            var result = system.Load();

            Assert.AreEqual(SaveSource.Backup, result.Source);
            Assert.AreEqual(1234, result.State.Wallet.Coins);
            Assert.IsNotNull(result.PrimaryError);
            Assert.AreEqual(2, store.Writes);
        }

        [Test]
        public void SaveSystem_BothCorrupt_Throws_InsteadOfStartingOver()
        {
            var store = new InMemorySaveStore { Primary = "lixo", Backup = "lixo" };
            Assert.Throws<SaveFormatException>(() => new SaveSystem(store, _serializer, _clock).Load());
        }

        [Test]
        public void SaveSystem_CorruptPrimaryWithoutBackup_Throws()
        {
            var store = new InMemorySaveStore { Primary = "lixo" };
            Assert.Throws<SaveFormatException>(() => new SaveSystem(store, _serializer, _clock).Load());
        }

        [Test]
        public void SaveSystem_OnlyBackup_IsUsed()
        {
            var store = new InMemorySaveStore { Backup = _serializer.Serialize(SampleState()) };
            var result = new SaveSystem(store, _serializer, _clock).Load();
            Assert.AreEqual(SaveSource.Backup, result.Source);
            Assert.IsNull(result.PrimaryError);
        }

        [Test]
        public void SaveSystem_Guards()
        {
            Assert.Throws<ArgumentNullException>(() => new SaveSystem(null, _serializer, _clock));
            Assert.Throws<ArgumentNullException>(() => new SaveSystem(new InMemorySaveStore(), null, _clock));
            Assert.Throws<ArgumentNullException>(() => new SaveSystem(new InMemorySaveStore(), _serializer, null));
            Assert.Throws<ArgumentNullException>(() => new SaveSystem(new InMemorySaveStore(), _serializer, _clock).Save(null));
        }
    }
}
