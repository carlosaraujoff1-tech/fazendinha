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
        [SerializeField] private float refreshSeconds = 0.5f;

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
        private bool? _shownAtStore;
        private long _shownCoins = -1;
        private int _shownLevel = -1;
        private readonly StringBuilder _sb = new StringBuilder(256);
        private readonly int[] _top = new int[4], _topN = new int[4];

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
            if (s.Wallet.Coins != _shownCoins || s.Progress.Level != _shownLevel)
            {
                _shownCoins = s.Wallet.Coins;
                _shownLevel = s.Progress.Level;
                _coins.text = $"Moedas {DisplayFormat.Coins(_shownCoins)}   ·   Nível {_shownLevel}";
            }

            var p = PlayerController.Instance;
            if (p != null) _atStore = p.transform.position.x > 26f;
            if (_atStore != _shownAtStore)
            {
                _shownAtStore = _atStore;
                UiFactory.SetButton(_travel, _atStore ? "Ir à fazenda" : "Ir ao mercadinho", true, ButtonStyle.Yellow);
            }

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
                // Os 4 produtos com mais estoque, sem LINQ (evita lixo de memória a cada atualização).
                var products = root.Catalog.Products;
                int count = products.Count, shown = 0, more = 0;
                for (int k = 0; k < _top.Length; k++) { _top[k] = -1; _topN[k] = 0; }
                for (int i = 0; i < count; i++)
                {
                    int n = root.Context.Warehouse.Count(products[i].Id, now);
                    if (n <= 0) continue;
                    more++;
                    for (int k = 0; k < _top.Length; k++)
                    {
                        if (n <= _topN[k]) continue;
                        for (int m = _top.Length - 1; m > k; m--) { _top[m] = _top[m - 1]; _topN[m] = _topN[m - 1]; }
                        _top[k] = i; _topN[k] = n;
                        break;
                    }
                }
                for (int k = 0; k < _top.Length; k++)
                {
                    if (_top[k] < 0) continue;
                    if (shown > 0) _sb.Append("  ");
                    _sb.Append(products[_top[k]].Name).Append(' ').Append(_topN[k]);
                    shown++;
                }
                more -= shown;
                if (shown == 0) _sb.Append("vazio");
                if (more > 0) _sb.Append("  +").Append(more);
                int seeds = 0;
                foreach (var kv in s.Farm.Seeds) seeds += kv.Value;
                _sb.Append("   ·   Sementes ").Append(seeds);
            }
            if (!StringBuilderEquals(_sb, _info.text)) _info.text = _sb.ToString();
        }

        private static bool StringBuilderEquals(StringBuilder sb, string text)
        {
            if (text == null || sb.Length != text.Length) return false;
            for (int i = 0; i < text.Length; i++) if (sb[i] != text[i]) return false;
            return true;
        }
    }
}
