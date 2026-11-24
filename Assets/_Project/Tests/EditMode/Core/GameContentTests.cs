using System;
using System.IO;
using System.Linq;
using Game.Core.Catalog;
using Game.Core.Economy;
using NUnit.Framework;

namespace Game.Tests.Core
{
    /// <summary>Valida os DADOS REAIS do jogo (Assets/_Project/Content/Data/catalog.json).</summary>
    public class GameContentTests
    {
        private static string CatalogPath()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string p = Path.Combine(dir.FullName, "Assets", "_Project", "Content", "Data", "catalog.json");
                    if (File.Exists(p)) return p;
                    dir = dir.Parent;
                }
            }
            Assert.Fail("catalog.json não encontrado.");
            return null;
        }

        public static GameCatalog LoadRealCatalog() => CatalogJson.Parse(File.ReadAllText(CatalogPath()));

        [Test]
        public void RealCatalog_IsValid_AndMatchesTheDesignTable()
        {
            var c = LoadRealCatalog();
            // Tabela "Economia inicial" do PROMPT_MESTRE: cultura, tempo(s), semente, rendimento, P, fornecedor.
            var table = new[]
            {
                ("alface", 60, 10, 4, 10, 8),
                ("cenoura", 180, 18, 5, 14, 11),
                ("batata", 480, 27, 6, 18, 14),
                ("tomate", 900, 36, 6, 24, 19),
                ("milho", 1800, 60, 8, 30, 24),
            };
            foreach (var (id, time, seed, yield, price, supplier) in table)
            {
                var crop = c.GetCrop(id);
                var product = c.GetProduct(crop.ProductId);
                Assert.AreEqual(time, crop.GrowSeconds, id);
                Assert.AreEqual(seed, crop.SeedCost, id);
                Assert.AreEqual(yield, crop.Yield, id);
                Assert.AreEqual(price, product.ReferencePrice, id);
                Assert.AreEqual(supplier, (long)Math.Round(price * c.Economy.SupplierPriceFactor, MidpointRounding.AwayFromZero), id);
                // "Produzir na fazenda custa ~25% de P": semente / rendimento.
                Assert.That((double)seed / yield / price, Is.InRange(0.2, 0.3), id);
            }
            Assert.AreEqual(5, c.GetCrop("milho").UnlockLevel);
            Assert.AreEqual("Alface", c.GetCrop("alface").Name);
            Assert.AreEqual("Tomate", c.GetProduct("tomate").Name);
            Assert.AreEqual(14400, c.Economy.OfflineFarmCapSeconds);
            Assert.AreEqual(1.15, c.Economy.RepeatableCostGrowth);
        }

        [Test]
        public void RealCatalog_EveryCropIsPlantableWithStartingCoins_AtItsUnlockLevel()
        {
            var c = LoadRealCatalog();
            Assert.IsTrue(c.Crops.Where(x => x.UnlockLevel == 1).All(x => x.SeedCost <= c.Economy.StartingCoins));
        }

        [Test]
        public void Parse_UsesDefaultsForMissingFields()
        {
            var c = CatalogJson.Parse("{\"products\":[{\"id\":\"a\",\"category\":\"x\",\"referencePrice\":5}],\"crops\":[{\"id\":\"a\",\"product\":\"a\",\"growSeconds\":1,\"seedCost\":1,\"yield\":1}]}");
            Assert.AreEqual(1, c.GetCrop("a").UnlockLevel);
            Assert.AreEqual("a", c.GetCrop("a").Name, "sem nome, usa o ID");
            Assert.IsFalse(c.GetProduct("a").Perishable);
            Assert.AreEqual(new EconomyConfig().StartingCoins, c.Economy.StartingCoins);
        }

        [TestCase("")]
        [TestCase("null")]
        [TestCase("{ quebrado")]
        [TestCase("{\"produtos\":[]}")]                                   // campo desconhecido (erro de digitação)
        [TestCase("{\"products\":[null],\"crops\":[]}")]
        [TestCase("{\"products\":[],\"crops\":[{\"id\":\"a\",\"product\":\"nada\",\"growSeconds\":1,\"seedCost\":1,\"yield\":1}]}")]
        public void Parse_RejectsBadContent(string json)
        {
            Assert.Throws<CatalogException>(() => CatalogJson.Parse(json));
        }

        [Test]
        public void Parse_EmptyObject_IsAnEmptyCatalog()
        {
            Assert.IsEmpty(CatalogJson.Parse("{}").Crops);
        }

        [Test]
        public void CostCurve_UsesCatalogGrowth()
        {
            var c = LoadRealCatalog();
            Assert.AreEqual(66, CostCurve.RepeatableCost(c.Economy.PlotBaseCost, 2, c.Economy.RepeatableCostGrowth)); // 66,125
        }
    }
}
