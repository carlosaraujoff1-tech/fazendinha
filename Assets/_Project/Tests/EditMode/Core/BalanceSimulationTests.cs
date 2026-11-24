using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core.Balance;
using NUnit.Framework;

namespace Game.Tests.Core
{
    /// <summary>
    /// Etapa 4: simulador headless. Joga 1 h, 3 h e 8 h com as 3 estratégias.
    /// Com FAZENDA_WRITE_BALANCE=1 grava os CSV e o resumo em Docs/balance/ (Tools/run-balance.sh).
    /// </summary>
    [Category("Balance")]
    public class BalanceSimulationTests
    {
        private static readonly int[] Durations = { 1, 3, 8 };
        private static Dictionary<(Strategy, int), BalanceResult> _results;

        private static Dictionary<(Strategy, int), BalanceResult> Results()
        {
            if (_results != null) return _results;
            var catalog = GameContentTests.LoadRealCatalog();
            _results = new Dictionary<(Strategy, int), BalanceResult>();
            foreach (Strategy s in Enum.GetValues(typeof(Strategy)))
                foreach (int h in Durations)
                    _results[(s, h)] = BalanceSimulator.Run(catalog, s, h);
            if (Environment.GetEnvironmentVariable("FAZENDA_WRITE_BALANCE") == "1") Write(_results);
            return _results;
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(8)]
        public void Mixed_BeatsBothExtremes(int hours)
        {
            var r = Results();
            long mixed = r[(Strategy.Mixed, hours)].Final.NetWorth;
            long farm = r[(Strategy.FarmOnly, hours)].Final.NetWorth;
            long supplier = r[(Strategy.SupplierOnly, hours)].Final.NetWorth;
            TestContext.WriteLine($"{hours} h: misto {mixed}, só fazenda {farm}, só fornecedor {supplier}");
            Assert.Greater(mixed, farm, "misto deve superar só fazenda");
            Assert.Greater(mixed, supplier, "misto deve superar só fornecedor");
        }

        [Test]
        public void NoStrategy_GetsStuck()
        {
            foreach (var kv in Results())
                Assert.AreEqual(-1, kv.Value.StuckAtMinute, $"{kv.Key.Item1} {kv.Key.Item2} h travou no minuto {kv.Value.StuckAtMinute}");
        }

        [Test]
        public void EveryStrategy_GrowsOverTime()
        {
            foreach (Strategy s in Enum.GetValues(typeof(Strategy)))
            {
                var r = Results()[(s, 8)];
                long start = r.Samples[0].NetWorth, end = r.Final.NetWorth;
                Assert.Greater(end, start * 3, $"{s}: patrimônio deve crescer bem em 8 h ({start} -> {end})");
            }
        }

        private static void Write(Dictionary<(Strategy, int), BalanceResult> results)
        {
            string dir = Path.Combine(ProjectRoot(), "Docs", "balance");
            Directory.CreateDirectory(dir);
            var md = new StringBuilder();
            md.AppendLine("# Simulação de balanceamento (gerado)");
            md.AppendLine();
            md.AppendLine("Gerado por `Tools/run-balance.sh` (BalanceSimulationTests). Robô age 1×/s, caixa manual ~1 item/s.");
            md.AppendLine("Patrimônio = moedas + investido (parcelas/móveis) + estoque a preço de fornecedor.");
            md.AppendLine();
            md.AppendLine("| Estratégia | Horas | Patrimônio | Moedas | Receita | Gasto fornecedor | Vencidos | Atendidos | Bravos | Itens em falta | Recusa por preço | Parcelas | Móveis |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var kv in results.OrderBy(k => k.Key.Item2).ThenBy(k => k.Key.Item1))
            {
                string name = $"{kv.Key.Item1.ToString().ToLowerInvariant()}_{kv.Key.Item2}h";
                File.WriteAllText(Path.Combine(dir, name + ".csv"), kv.Value.ToCsv());
                var f = kv.Value.Final;
                md.AppendLine($"| {kv.Key.Item1} | {kv.Key.Item2} | {f.NetWorth} | {f.Coins} | {f.Revenue} | {f.SupplierSpent} | {f.ExpiredUnits} | {f.Served} | {f.Angry} | {f.Missing} | {f.RejectedByPrice} | {f.Plots} | {f.Shelves} |");
            }
            File.WriteAllText(Path.Combine(dir, "resumo.md"), md.ToString());
        }

        private static string ProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project"))) dir = dir.Parent;
            if (dir == null) dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            return dir.FullName;
        }
    }
}
