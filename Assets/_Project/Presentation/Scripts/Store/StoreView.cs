using System;
using System.Collections.Generic;
using Game.Core.Store;
using Game.Presentation.Controls;
using Game.Presentation.Graybox;
using UnityEngine;

namespace Game.Presentation.Store
{
    /// <summary>
    /// Loja gray box: móveis em grade, caixa e porta; clientes andam até as prateleiras e fazem fila.
    /// Só lê o estado/eventos do Core; não decide nada de regra.
    /// </summary>
    public sealed class StoreView : MonoBehaviour
    {
        [SerializeField] private int columns = 2;
        [SerializeField] private float spacingX = 3.4f;
        [SerializeField] private float spacingZ = 2.6f;
        [SerializeField] private float refreshSeconds = 0.2f;

        private readonly List<ShelfView> _shelves = new List<ShelfView>();
        private readonly Dictionary<int, CustomerView> _customers = new Dictionary<int, CustomerView>();
        private readonly List<CustomerView> _leaving = new List<CustomerView>();
        private readonly Stack<CustomerView> _pool = new Stack<CustomerView>();
        private readonly Dictionary<int, int> _lastShelfOfCustomer = new Dictionary<int, int>();
        private readonly List<IDisposable> _subs = new List<IDisposable>();
        private float _nextRefresh;

        private Vector3 Door => transform.position + new Vector3(0, 0, 4.5f);
        private Vector3 CounterPos => transform.position + new Vector3(-3.2f, 0, 2.5f);

        private void Start()
        {
            var counter = GrayboxMaterials.Primitive(PrimitiveType.Cube, "Counter", transform, new Color(0.55f, 0.4f, 0.25f), keepCollider: true);
            counter.transform.position = CounterPos;
            counter.transform.localScale = new Vector3(1.6f, 1f, 1f);
            counter.transform.position += Vector3.up * 0.5f;
            counter.AddComponent<CounterView>();

            var floor = GrayboxMaterials.Primitive(PrimitiveType.Cube, "Floor", transform, new Color(0.85f, 0.85f, 0.8f));
            floor.transform.localScale = new Vector3(10f, 0.05f, 12f);
            floor.transform.localPosition = new Vector3(0, 0.025f, -2f);

            var events = GameRoot.Instance.Events;
            _subs.Add(events.Subscribe<ShelfChanged>(_ => RebuildShelves()));
            _subs.Add(events.Subscribe<CustomerPickedItem>(e => _lastShelfOfCustomer[e.CustomerId] = e.ShelfIndex));
            _subs.Add(events.Subscribe<CustomerLeft>(OnCustomerLeft));
            RebuildShelves();

            var area = FindFirstObjectByType<AreaSwitcher>();
            if (area != null)
            {
                var c = transform.position;
                area.SetBounds(GameArea.Store, Rect.MinMaxRect(c.x - 3, c.z - 6, c.x + 3, c.z + 2));
            }
        }

        private void OnDestroy()
        {
            foreach (var s in _subs) s.Dispose();
        }

        public Vector3 ShelfPosition(int index)
        {
            int col = index % columns, row = index / columns;
            float width = (columns - 1) * spacingX;
            return transform.position + new Vector3(col * spacingX - width * 0.5f, 0, -row * spacingZ);
        }

        private void RebuildShelves()
        {
            var root = GameRoot.Instance;
            var shelves = root.State.Store.Shelves;
            while (_shelves.Count < shelves.Count)
            {
                int i = _shelves.Count;
                var go = new GameObject();
                go.transform.SetParent(transform, false);
                go.transform.position = ShelfPosition(i);
                var view = go.AddComponent<ShelfView>();
                string category = root.Catalog.TryGetFurniture(shelves[i].FurnitureId, out var f) ? f.CategoryId : "?";
                view.Build(i, category);
                _shelves.Add(view);
            }
            RenderShelves();
        }

        private void RenderShelves()
        {
            var root = GameRoot.Instance;
            var shelves = root.State.Store.Shelves;
            for (int i = 0; i < _shelves.Count && i < shelves.Count; i++)
            {
                int capacity = root.Catalog.TryGetFurniture(shelves[i].FurnitureId, out var f) ? f.Capacity : 0;
                _shelves[i].Render(shelves[i], capacity, root.NowMs);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + refreshSeconds;
                RenderShelves();
                SyncCustomers();
            }
            foreach (var view in _customers.Values) view.Tick(dt);
            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                if (!_leaving[i].Tick(dt)) continue;
                _leaving[i].gameObject.SetActive(false);
                _pool.Push(_leaving[i]);
                _leaving.RemoveAt(i);
            }
        }

        private void SyncCustomers()
        {
            var root = GameRoot.Instance;
            var store = root.State.Store;
            foreach (var c in store.Customers)
            {
                if (!_customers.TryGetValue(c.Id, out var view))
                {
                    view = _pool.Count > 0 ? _pool.Pop() : CreateView();
                    view.Spawn(c.Id, c.TypeId, Door);
                    _customers[c.Id] = view;
                }

                int queuePos = StoreRules.QueuePosition(store, c);
                if (queuePos >= 0)
                {
                    view.Target = CounterPos + new Vector3(0, 0, -1.1f - queuePos * 0.9f) + Vector3.right * 1.2f;
                    float max = root.Catalog.TryGetCustomerType(c.TypeId, out var t) ? t.PatienceSeconds : 60f;
                    view.SetPatience((float)(c.PatienceSeconds / max));
                }
                else
                {
                    // Comprando: vai até a última prateleira de onde pegou algo, ou passeia por uma prateleira.
                    int shelf = _lastShelfOfCustomer.TryGetValue(c.Id, out int s) ? s : (c.Id * 7 + c.NextItem) % Math.Max(1, _shelves.Count);
                    view.Target = ShelfPosition(shelf) + new Vector3(((c.Id % 3) - 1) * 0.6f, 0, 1.0f);
                    view.SetPatience(-1);
                }
            }
        }

        private void OnCustomerLeft(CustomerLeft e)
        {
            _lastShelfOfCustomer.Remove(e.CustomerId);
            if (!_customers.TryGetValue(e.CustomerId, out var view)) return;
            _customers.Remove(e.CustomerId);
            view.Leave(Door);
            _leaving.Add(view);
        }

        private CustomerView CreateView()
        {
            var go = new GameObject("Customer");
            go.transform.SetParent(transform, false);
            var view = go.AddComponent<CustomerView>();
            view.Build();
            return view;
        }
    }
}
