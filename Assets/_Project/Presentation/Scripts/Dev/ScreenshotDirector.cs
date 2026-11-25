using System;
using System.Collections;
using System.IO;
using Game.Core.Farm;
using Game.Core.Inventory;
using Game.Core.State;
using Game.Core.Store;
using Game.Presentation.Player;
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
            var cam = FindFirstObjectByType<ThirdPersonCamera>();
            var player = PlayerController.Instance;

            // ---------- Cenário ----------
            state.Wallet.Coins = 50_000;
            state.Progress.Level = 5;
            for (int i = 0; i < 6; i++) root.Execute(new BuyPlotCommand());
            foreach (var c in root.Catalog.Crops) state.Farm.Seeds[c.Id] = 20;
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
            // Animais: compra vaca e ovelha, alimenta todos; a galinha já fica pronta, uma ovelha com fome.
            root.Execute(new Game.Core.Animals.BuyAnimalCommand("vaca"));
            root.Execute(new Game.Core.Animals.BuyAnimalCommand("vaca"));
            root.Execute(new Game.Core.Animals.BuyAnimalCommand("ovelha"));
            root.Execute(new Game.Core.Animals.BuyAnimalCommand("galinha"));
            for (int i = 0; i < state.Farm.Animals.Count - 1; i++) root.Execute(new Game.Core.Animals.FeedAnimalCommand(i));
            state.Farm.Animals[0].ReadyAtMs = ctx.NowMs;

            root.Catalog.Store.SpawnIntervalSeconds = 1.2;
            state.Store.NextSpawnAtMs = ctx.NowMs;

            // Pose: jogadora em pos virada para yaw; câmera atrás dela (camYaw relativo), com pitch e distância.
            void Pose(Vector3 pos, float yaw, float pitch, float dist, float camYawOffset = 0f)
            {
                player.TeleportTo(pos, yaw);
                cam.Yaw = yaw + camYawOffset;
                cam.Pitch = pitch;
                cam.Distance = dist;
                cam.Snap();
            }

            // ---------- Fazenda ----------
            Pose(HudController.FarmSpawn, HudController.FarmSpawnYaw, 16f, 6.5f);
            yield return new WaitForSeconds(2f);
            yield return Shot("01_fazenda_portao");
            Pose(new Vector3(2.25f, 0, -2.25f), 0f, 6f, 3.2f, 35f);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("02_plantacao_perto");
            Pose(new Vector3(-2.25f, 0, -2.25f), 20f, 12f, 3f);
            cam.FirstPerson = true;
            yield return new WaitForSeconds(0.5f);
            yield return Shot("02b_primeira_pessoa");
            cam.FirstPerson = false;
            Pose(new Vector3(0f, 0, -6.75f), 180f, 48f, 9f);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("03_fazenda_alto");

            // Perto de uma parcela vazia: botão de ação "Plantar"
            Pose(new Vector3(4.5f, 0, -2.5f), 0f, 18f, 4.2f, -30f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("04a_botao_acao");
            FindFirstObjectByType<PlantMenu>()?.Open(3);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("04_menu_plantar");
            FindFirstObjectByType<PlantMenu>()?.Close();

            // Colheita com efeito
            var farmInteraction = FindFirstObjectByType<Game.Presentation.Farm.FarmInteraction>();
            Pose(new Vector3(9f, 0, -2.3f), 0f, 14f, 3.8f, 25f);
            yield return new WaitForSeconds(0.6f);
            for (int i = 0; i < state.Farm.Plots.Count; i++)
                if (FarmRules.GetPhase(state.Farm.Plots[i], ctx.NowMs) == PlotPhase.Ready) { farmInteraction.HandlePlot(i); break; }
            yield return new WaitForSeconds(0.25f);
            yield return Shot("05_colheita_efeito");

            // ---------- Animais e vendinha ----------
            Pose(new Vector3(-14.5f, 0, -3f), -110f, 14f, 6f);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("10_animais");
            var menusFarm = FindFirstObjectByType<StoreMenus>();
            menusFarm?.OpenSeedShop();
            yield return new WaitForSeconds(0.5f);
            yield return Shot("11_vendinha_sementes");
            menusFarm?.OpenAnimals();
            yield return new WaitForSeconds(0.5f);
            yield return Shot("12_menu_animais");
            menusFarm?.Close();
            Pose(new Vector3(18.5f, 0, -4.5f), 0f, 12f, 5.5f, -20f);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("13_vendinha_predio");

            // ---------- Estrada ----------
            Pose(new Vector3(24f, 0, -11f), 90f, 10f, 5f);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("14_estrada");

            // ---------- Loja ----------
            Pose(HudController.StoreSpawn, HudController.StoreSpawnYaw, 12f, 7.5f, 20f);
            yield return new WaitForSeconds(14f); // clientes entram e fazem fila
            yield return Shot("06_mercadinho_fachada");
            Pose(new Vector3(41f, 0, -2f), 0f, 14f, 3.6f);
            yield return new WaitForSeconds(1f);
            yield return Shot("07_mercadinho_dentro");
            Pose(new Vector3(36.1f, 0, -3.1f), 0f, 22f, 3.2f, 30f);
            yield return new WaitForSeconds(1f);
            yield return Shot("07b_caixa");

            var menus = FindFirstObjectByType<StoreMenus>();
            menus?.OpenShelf(0);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("08_menu_prateleira");
            menus?.OpenSupplier();
            yield return new WaitForSeconds(0.5f);
            yield return Shot("09_menu_fornecedor");
            menus?.OpenFurniture();
            yield return new WaitForSeconds(0.5f);
            yield return Shot("09b_menu_melhorias");
            menus?.Close();

            Debug.Log("[Screenshot] Fim.");
            yield return new WaitForSeconds(0.5f);
            Application.Quit(0);
        }
    }
}
