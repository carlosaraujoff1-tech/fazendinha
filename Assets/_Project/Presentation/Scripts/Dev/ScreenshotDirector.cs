using System;
using System.Collections;
using System.IO;
using Game.Core.Farm;
using Game.Core.Inventory;
using Game.Core.State;
using Game.Core.Store;
using Game.Presentation.Controls;
using Game.Presentation.UI;
using UnityEngine;

namespace Game.Presentation.Dev
{
    /// <summary>
    /// Modo de capturas automáticas (usado pelo CI para o Claude "ver" o jogo): com o argumento
    /// <c>-fazendaScreenshots &lt;pasta&gt;</c>, monta um cenário (plantas em todas as fases, prateleiras cheias,
    /// clientes), tira prints das telas principais e fecha o jogo. Sem o argumento, não faz nada.
    /// </summary>
    public sealed class ScreenshotDirector : MonoBehaviour
    {
        private string _outDir;

        private void Start()
        {
            _outDir = Arg("-fazendaScreenshots");
            if (string.IsNullOrEmpty(_outDir)) { enabled = false; return; }
            Directory.CreateDirectory(_outDir);
            StartCoroutine(Run());
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            yield return null;
            string path = Path.Combine(_outDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[Screenshot] " + path);
            yield return new WaitForSeconds(0.6f);
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(1.5f);
            var root = GameRoot.Instance;
            var ctx = root.Context;
            var state = ctx.State;
            var area = FindFirstObjectByType<AreaSwitcher>();
            var cam = FindFirstObjectByType<TouchCameraController>();

            // ---------- Cenário ----------
            state.Wallet.Coins = 50_000;
            state.Progress.Level = 5;
            for (int i = 0; i < 6; i++) root.Execute(new BuyPlotCommand());
            var crops = new System.Collections.Generic.List<Game.Core.Catalog.CropDef>(root.Catalog.Crops);
            long now = ctx.NowMs;
            for (int i = 0; i < state.Farm.Plots.Count; i++)
            {
                if (i == 3) continue; // uma vazia, para ver a terra
                var crop = crops[i % crops.Count];
                root.Execute(new PlantCommand(i, crop.Id));
                var plot = state.Farm.Plots[i];
                float progress = (i % 5) / 4f;                      // 0, 25, 50, 75, 100%
                long total = crop.GrowSeconds * 1000L;
                plot.PlantedAtMs = now - (long)(total * progress);
                plot.ReadyAtMs = plot.PlantedAtMs + total;
                if (i % 2 == 1 && progress < 1f) plot.Watered = true;
            }
            foreach (var p in root.Catalog.Products)
                ctx.Warehouse.Add(LotInventory.CreateLot(p, 60, now, LotSource.Farm));
            for (int s = 0; s < state.Store.Shelves.Count; s++)
            {
                var shelf = state.Store.Shelves[s];
                if (!root.Catalog.TryGetFurniture(shelf.FurnitureId, out var f)) continue;
                foreach (var p in root.Catalog.Products)
                {
                    if (p.CategoryId != f.CategoryId) continue;
                    if (s == 1 && p.Id == "alface") continue; // segunda banca com outro produto
                    int units = s == 1 ? f.Capacity / 2 : f.Capacity;
                    if (root.Execute(new RestockCommand(s, p.Id, units)).IsSuccess) break;
                }
            }
            root.Catalog.Store.SpawnIntervalSeconds = 1.2;
            state.Store.NextSpawnAtMs = ctx.NowMs;

            // ---------- Fazenda ----------
            area.Show(GameArea.Farm);
            cam.SetZoom(17f);
            yield return new WaitForSeconds(2f);
            yield return Shot("01_fazenda");
            cam.SetZoom(cam.MinDistance);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("02_fazenda_perto");
            cam.SetZoom(cam.MaxDistance);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("03_fazenda_longe");

            cam.SetZoom(17f);
            FindFirstObjectByType<PlantMenu>()?.Open(3);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("04_menu_plantar");
            FindFirstObjectByType<PlantMenu>()?.Close();

            // Colheita com efeito
            var farmInteraction = FindFirstObjectByType<Game.Presentation.Farm.FarmInteraction>();
            for (int i = 0; i < state.Farm.Plots.Count; i++)
                if (FarmRules.GetPhase(state.Farm.Plots[i], ctx.NowMs) == PlotPhase.Ready) { farmInteraction.HandlePlot(i); break; }
            yield return new WaitForSeconds(0.25f);
            yield return Shot("05_colheita_efeito");

            // ---------- Loja ----------
            area.Show(GameArea.Store);
            cam.SetZoom(17f);
            yield return new WaitForSeconds(14f); // clientes entram e fazem fila
            yield return Shot("06_mercadinho");
            cam.SetZoom(cam.MinDistance);
            yield return new WaitForSeconds(1f);
            yield return Shot("07_mercadinho_perto");
            cam.SetZoom(17f);

            var menus = FindFirstObjectByType<StoreMenus>();
            menus?.OpenShelf(0);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("08_menu_prateleira");
            menus?.OpenSupplier();
            yield return new WaitForSeconds(0.5f);
            yield return Shot("09_menu_fornecedor");
            menus?.Close();

            Debug.Log("[Screenshot] Fim.");
            yield return new WaitForSeconds(0.5f);
            Application.Quit(0);
        }
    }
}
