using System.Collections.Generic;
using System.Linq;
using Game.Core.Catalog;
using Game.Core.Store;
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
    
        [Test]
        public void Build_ValidatesStoreData()
        {
            var products = new[] { new ProductDef("a", "horti", 10, 0) };
            var ex = Assert.Throws<CatalogException>(() => GameCatalog.Build(products, null, null,
                new[]
                {
                    new CustomerTypeDef("c", "", -1, -1, -1, 0),     // 4 erros
                    new CustomerTypeDef("c", "", 0, 0, 1, 1),        // duplicado
                    new CustomerTypeDef("X", "", 0, 0, 1, 1),        // ID inválido
                    null,
                },
                new[]
                {
                    new FurnitureDef("f", "", "laticinios", 0, -1),  // 3 erros
                    new FurnitureDef("f", "", "horti", 1, 1),        // duplicado
                    new FurnitureDef("", "", "horti", 1, 1),         // ID inválido
                    null,
                },
                new StoreConfig
                {
                    InitialFurniture = new[] { "nada", "f" },
                    MaxFurniture = 1,
                    SpawnIntervalSeconds = 0,
                    MaxCustomersInStore = 0,
                    ShoppingSecondsPerItem = -1,
                    MinItemsPerList = 0,
                    MinUnitsPerItem = 2,
                    MaxUnitsPerItem = 1,
                    MaxCatchUpSeconds = 0,
                    MaxPriceFactor = 0.5,
                }));
            // clientes: 4+1+1+1, soma de pesos ok (1 > 0); móveis: 3+1+1+1; loja: 1 inicial + 9 regras
            Assert.AreEqual(23, ex.Errors.Count, string.Join("\n", ex.Errors));
        }

        [Test]
        public void Build_CustomerWeightsMustSumAboveZero()
        {
            var ex = Assert.Throws<CatalogException>(() => GameCatalog.Build(null, null, null,
                new[] { new CustomerTypeDef("c", "C", 0, 0.1, 0, 10) }));
            StringAssert.Contains("pesos", ex.Message);
        }

        [Test]
        public void Lookups_ForStoreData()
        {
            var c = GameContentTests.LoadRealCatalog();
            Assert.AreEqual("Gôndola", c.GetFurniture("gondola").Name);
            Assert.AreEqual(3, c.CustomerTypes.Count);
            Assert.AreEqual(3, c.Furniture.Count());
            Assert.Throws<KeyNotFoundException>(() => c.GetFurniture("x"));
            Assert.Throws<KeyNotFoundException>(() => c.GetCustomerType("x"));
            Assert.IsFalse(c.TryGetFurniture(null, out _));
            Assert.AreEqual("c", new CustomerTypeDef("c", null, 0, 0, 1, 1).Name);
            Assert.AreEqual("f", new FurnitureDef("f", null, "x", 1, 1).Name);
        }
    }
}
