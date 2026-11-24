using System.Collections.Generic;
using System.Linq;
using Game.Core.Catalog;
using NUnit.Framework;

namespace Game.Tests.Core
{
    public class CatalogTests
    {
        [Test]
        public void Mvp_BuildsWithAllCrops()
        {
            var catalog = TestCatalogs.Mvp();
            Assert.AreEqual(5, catalog.Crops.Count());
            Assert.AreEqual(6, catalog.Products.Count());
            var milho = catalog.GetCrop("milho");
            Assert.AreEqual(1800, milho.GrowSeconds);
            Assert.AreEqual(5, milho.UnlockLevel);
            Assert.AreEqual(30, catalog.GetProduct(milho.ProductId).ReferencePrice);
            Assert.AreEqual(1.15, catalog.Economy.RepeatableCostGrowth);
        }

        [Test]
        public void Lookup_UnknownIds()
        {
            var catalog = TestCatalogs.Mvp();
            Assert.IsFalse(catalog.TryGetProduct("nada", out _));
            Assert.IsFalse(catalog.TryGetProduct(null, out _));
            Assert.IsFalse(catalog.TryGetCrop(null, out _));
            Assert.Throws<KeyNotFoundException>(() => catalog.GetProduct("nada"));
            Assert.Throws<KeyNotFoundException>(() => catalog.GetCrop("nada"));
        }

        [Test]
        public void Perishable_DependsOnShelfLife()
        {
            var catalog = TestCatalogs.Mvp();
            Assert.IsTrue(catalog.GetProduct("alface").Perishable);
            Assert.IsFalse(catalog.GetProduct("arroz").Perishable);
        }

        [Test]
        public void Build_ReportsAllErrorsAtOnce()
        {
            var ex = Assert.Throws<CatalogException>(() => GameCatalog.Build(
                new[]
                {
                    new ProductDef("ok", "hortifruti", 10, 0),
                    new ProductDef("ok", "hortifruti", 10, 0),       // duplicado
                    new ProductDef("Maiúsculo", "x", 10, 0),        // ID inválido
                    new ProductDef("sem_cat", "", 0, -1),           // 3 erros
                    null,
                },
                new[]
                {
                    new CropDef("c1", "inexistente", 0, -1, 0, 0),  // 5 erros
                    new CropDef("c1", "ok", 1, 1, 1),              // duplicada
                    new CropDef("", "ok", 1, 1, 1),                // ID vazio
                    null,
                },
                new EconomyConfig
                {
                    StartingCoins = -1,
                    RepeatableCostGrowth = 0.5,
                    SupplierPriceFactor = 0,
                    WaterTimeReduction = 1,
                    OfflineFarmCapSeconds = -1,
                }));

            Assert.AreEqual(19, ex.Errors.Count, string.Join("\n", ex.Errors));
            StringAssert.Contains("duplicado", ex.Message);
            StringAssert.Contains("inexistente", ex.Message);
        }

        [Test]
        public void Build_AcceptsNullCollections()
        {
            var catalog = GameCatalog.Build(null, null);
            Assert.IsEmpty(catalog.Products);
            Assert.IsNotNull(catalog.Economy);
        }
    }
}
