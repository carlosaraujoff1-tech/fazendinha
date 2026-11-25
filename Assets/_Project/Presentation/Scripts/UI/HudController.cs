using System.Linq;
using System.Text;
using Game.Core.Display;
using Game.Core.Farm;
using Game.Core.Store;
using Game.Presentation.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    /// <summary>
    /// HUD em terceira pessoa (paisagem): barra de cima com moedas, nível e armazém; botões de menu no topo à
    /// direita; joystick na metade esquerda; área de giro da câmera na direita; botão de ação grande embaixo à
    /// direita (aparece só perto de algo interagível); botão de viagem rápida fazenda ⇄ mercadinho.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] private float refreshSeconds = 0.2f;

        public static Vector3 FarmSpawn = new Vector3(14f, 0f, -11f);
        public static float FarmSpawnYaw = -90f;
        public static Vector3 StoreSpawn = new Vector3(41f, 0f, -8.5f);
        public static float StoreSpawnYaw = 0f;

        private Text _coins, _info;
        private Button _action;
        private Text _actionLabel;
        private Button _travel;
        private Text _viewLabel;
        private float _nextRefresh;
        private bool _atStore;
        private readonly StringBuilder _sb = new StringBuilder(256);

        private void Start()
        {
            // Áreas de toque (atrás de tudo): joystick à esquerda, câmera à direita.
            var joy = UiFactory.Node("Joystick", transform).Anchor(new Vector2(0, 0), new Vector2(0.42f, 0.86f));
            joy.gameObject.AddComponent<VirtualJoystick>();
            var look = UiFactory.Node("Look", transform).Anchor(new Vector2(0.42f, 0), new Vector2(1, 0.86f));
            look.gameObject.AddComponent<LookArea>();

            // Barra de cima
            var top = UiFactory.Panel("TopBar", transform, UiFactory.PanelColor);
            ((RectTransform)top.transform).Anchor(new Vector2(0, 0.88f), new Vector2(1, 1));
            _coins = UiFactory.Label("Coins", top.transform, "", 40, TextAnchor.MiddleLeft);
            ((RectTransform)_coins.transform).Anchor(new Vector2(0.02f, 0.45f), new Vector2(0.4f, 1));
            _info = UiFactory.Label("Info", top.transform, "", 24, TextAnchor.MiddleLeft);
            ((RectTransform)_info.transform).Anchor(new Vector2(0.02f, 0), new Vector2(0.45f, 0.5f));

            var menus = FindFirstObjectByType<StoreMenus>();
            string[] labels = { "Sementes", "Animais", "Fornecedor", "Melhorias" };
            System.Action[] actions =
            {
                () => menus?.OpenSeedShop(), () => menus?.OpenAnimals(), () => menus?.OpenSupplier(), () => menus?.OpenFurniture(),
            };
            for (int i = 0; i < labels.Length; i++)
            {
                var b = UiFactory.MakeButton(labels[i], top.transform, labels[i], actions[i], 26, ButtonStyle.Blue);
                float x0 = 0.46f + i * 0.105f;
                ((RectTransform)b.transform).Anchor(new Vector2(x0, 0.12f), new Vector2(x0 + 0.1f, 0.88f));
            }
            _travel = UiFactory.MakeButton("Travel", top.transform, "Ir ao mercadinho", Travel, 24, ButtonStyle.Yellow);
            ((RectTransform)_travel.transform).Anchor(new Vector2(0.885f, 0.12f), new Vector2(0.995f, 0.88f));

            // Botão de ação
            _action = UiFactory.MakeButton("Action", transform, "", InteractionSystem.InteractNow, 34, ButtonStyle.Green);
            ((RectTransform)_action.transform).Anchor(new Vector2(0.66f, 0.05f), new Vector2(0.97f, 0.2f));
            _actionLabel = _action.GetComponentInChildren<Text>();
            _action.gameObject.SetActive(false);

            // Alternar 1ª / 3ª pessoa
            var view = UiFactory.MakeButton("View", transform, "1ª pessoa", ToggleView, 24, ButtonStyle.Grey);
            ((RectTransform)view.transform).Anchor(new Vector2(0.86f, 0.23f), new Vector2(0.97f, 0.31f));
            _viewLabel = view.GetComponentInChildren<Text>();

            Refresh();
        }

        private void Travel()
        {
            var p = PlayerController.Instance;
            if (p == null) return;
            _atStore = !_atStore;
            if (_atStore) p.TeleportTo(StoreSpawn, StoreSpawnYaw);
            else p.TeleportTo(FarmSpawn, FarmSpawnYaw);
            var cam = FindFirstObjectByType<ThirdPersonCamera>();
            if (cam != null) { cam.Yaw = p.transform.eulerAngles.y; cam.Snap(); }
        }

        private void ToggleView()
        {
            var cam = FindFirstObjectByType<ThirdPersonCamera>();
            if (cam == null) return;
            cam.FirstPerson = !cam.FirstPerson;
            _viewLabel.text = cam.FirstPerson ? "3ª pessoa" : "1ª pessoa";
        }

        private void Update()
        {
            // Botão de ação acompanha a interação mais próxima (a cada quadro, barato).
            string prompt = InteractionSystem.CurrentPrompt;
            bool show = !string.IsNullOrEmpty(prompt) && !MenuOpen();
            if (_action.gameObject.activeSelf != show) _action.gameObject.SetActive(show);
            if (show && _actionLabel.text != prompt) _actionLabel.text = prompt;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame && show) InteractionSystem.InteractNow();

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshSeconds;
            Refresh();
        }

        private static bool MenuOpen()
        {
            var plant = PlantMenu.IsOpen;
            return plant || StoreMenus.IsOpen;
        }

        private void Refresh()
        {
            var root = GameRoot.Instance;
            var s = root.State;
            _coins.text = $"Moedas {DisplayFormat.Coins(s.Wallet.Coins)}   ·   Nível {s.Progress.Level}";

            var p = PlayerController.Instance;
            if (p != null) _atStore = p.transform.position.x > 26f;
            UiFactory.SetButton(_travel, _atStore ? "Ir à fazenda" : "Ir ao mercadinho", true, ButtonStyle.Yellow);

            _sb.Clear();
            if (_atStore)
            {
                int queued = 0, shopping = 0;
                foreach (var c in s.Store.Customers) { if (c.Phase == CustomerPhase.Queued) queued++; else shopping++; }
                _sb.Append("Fila ").Append(queued).Append("  ·  Comprando ").Append(shopping)
                   .Append("  ·  Vendas ").Append(DisplayFormat.Coins(s.Stats.SalesRevenue));
            }
            else
            {
                _sb.Append("Armazém: ");
                long now = root.NowMs;
                int shown = 0, more = 0;
                foreach (var product in root.Catalog.Products.OrderByDescending(pr => root.Context.Warehouse.Count(pr.Id, now)))
                {
                    int n = root.Context.Warehouse.Count(product.Id, now);
                    if (n <= 0) continue;
                    if (shown >= 4) { more++; continue; }
                    if (shown > 0) _sb.Append("  ");
                    _sb.Append(product.Name).Append(' ').Append(n);
                    shown++;
                }
                if (shown == 0) _sb.Append("vazio");
                if (more > 0) _sb.Append("  +").Append(more);
                int seeds = 0;
                foreach (var kv in s.Farm.Seeds) seeds += kv.Value;
                _sb.Append("   ·   Sementes ").Append(seeds);
            }
            _info.text = _sb.ToString();
        }
    }
}
