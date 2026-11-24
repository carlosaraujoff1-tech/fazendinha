using Game.Core.Store;
using Game.Presentation.Controls;
using Game.Presentation.UI;
using UnityEngine;

namespace Game.Presentation.Store
{
    /// <summary>Toque numa prateleira abre o menu de reposição/preço; toque no caixa passa um item.</summary>
    public sealed class StoreInteraction : MonoBehaviour
    {
        [SerializeField] private TouchCameraController cameraController;
        private StoreMenus _menus;

        private void Start()
        {
            if (cameraController == null) cameraController = FindFirstObjectByType<TouchCameraController>();
            _menus = FindFirstObjectByType<StoreMenus>();
            cameraController.Tapped += OnTapped;
        }

        private void OnDestroy()
        {
            if (cameraController != null) cameraController.Tapped -= OnTapped;
        }

        private void OnTapped(Vector2 screen)
        {
            var ray = cameraController.Camera.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out var hit, 200f)) return;

            var shelf = hit.collider.GetComponentInParent<ShelfView>();
            if (shelf != null)
            {
                if (_menus != null) _menus.OpenShelf(shelf.Index);
                return;
            }
            if (hit.collider.GetComponentInParent<CounterView>() != null) Scan();
        }

        public static void Scan()
        {
            var root = GameRoot.Instance;
            var front = StoreRules.FrontOfQueue(root.State.Store);
            if (front == null)
            {
                Toast.Show("Ninguém na fila");
                return;
            }
            root.Execute(new ScanItemCommand());
        }
    }
}
