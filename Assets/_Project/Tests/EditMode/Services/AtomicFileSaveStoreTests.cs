using System;
using System.IO;
using Game.Core.Save;
using Game.Core.Simulation;
using Game.Core.Time;
using Game.Services.FileSystem;
using Game.Tests.Core;
using NUnit.Framework;

namespace Game.Tests.Services
{
    public class AtomicFileSaveStoreTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "fazenda-save-test-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void FirstWrite_CreatesPrimaryOnly()
        {
            var store = new AtomicFileSaveStore(_dir);
            Assert.IsNull(store.ReadPrimary());
            Assert.IsNull(store.ReadBackup());

            store.WriteAtomic("{\"a\":1}");

            Assert.AreEqual("{\"a\":1}", store.ReadPrimary());
            Assert.IsNull(store.ReadBackup());
            Assert.IsFalse(File.Exists(store.TempPath));
        }

        [Test]
        public void NextWrites_KeepPreviousAsBackup()
        {
            var store = new AtomicFileSaveStore(_dir);
            store.WriteAtomic("v1");
            store.WriteAtomic("v2");
            store.WriteAtomic("v3 çãé");

            Assert.AreEqual("v3 çãé", store.ReadPrimary());
            Assert.AreEqual("v2", store.ReadBackup());
            Assert.IsFalse(File.Exists(store.TempPath));
        }

        [Test]
        public void OrphanTempFile_FromCrash_DoesNotAffectReads()
        {
            var store = new AtomicFileSaveStore(_dir);
            store.WriteAtomic("bom");
            File.WriteAllText(store.TempPath, "meio-escrito");

            Assert.AreEqual("bom", store.ReadPrimary());
            store.WriteAtomic("novo");
            Assert.AreEqual("novo", store.ReadPrimary());
            Assert.AreEqual("bom", store.ReadBackup());
        }

        [Test]
        public void Guards()
        {
            Assert.Throws<ArgumentException>(() => new AtomicFileSaveStore(""));
            Assert.Throws<ArgumentNullException>(() => new AtomicFileSaveStore(_dir).WriteAtomic(null));
        }

        [Test]
        public void WithSaveSystem_RecoversFromCorruptFileOnDisk()
        {
            var clock = new ManualClock();
            var catalog = TestCatalogs.Mvp();
            var store = new AtomicFileSaveStore(_dir, "jogo.json");
            var system = new SaveSystem(store, new SaveSerializer(), clock);
            var state = NewGame.Create(catalog, clock);

            state.Wallet.Coins = 10;
            system.Save(state);
            state.Wallet.Coins = 20;
            system.Save(state);
            File.WriteAllText(store.PrimaryPath, "{\"SchemaVersion\":1,\"Wal");

            var result = system.Load();
            Assert.AreEqual(SaveSource.Backup, result.Source);
            Assert.AreEqual(10, result.State.Wallet.Coins);
            StringAssert.EndsWith("jogo.json", store.PrimaryPath);
        }
    }
}
