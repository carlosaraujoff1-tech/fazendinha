using System;
using System.Collections.Generic;
using Game.Core.Display;
using Game.Core.Staff;
using Game.Core.Store;
using Game.Presentation.Controls;
using Game.Presentation.Effects;
using Game.Presentation.Graybox;
using Game.Presentation.UI;
using UnityEngine;

namespace Game.Presentation.Store
{
    /// <summary>
    /// Loja: móveis em grade, caixa registradora, funcionário do caixa (se contratado) e clientes animados que
    /// entram pela porta do fundo, vão às prateleiras, fazem fila e saem. Só lê estado/eventos do Core.
    /// </summary>
    public sealed class StoreView : MonoBehaviour
    {
        [SerializeField] private int columns = 2;
        [SerializeField] private float spacingX = 3.6f;
        [SerializeField] private float spacingZ = 2.8f;
        [SerializeField] private float refreshSeconds = 0.2f;

        private readonly List<ShelfView> _shelves = new List<ShelfView>();
        private readonly Dictionary<int, CustomerView> _customers = new Dictionary<int, CustomerView>();
        private readonly List<CustomerView> _leaving = new List<CustomerView>();
        private readonly Stack<CustomerView> _pool = new Stack<CustomerView>();
        private readonly Dictionary<int, int> _lastShelfOfCustomer = new Dictionary<int, int>();
        private readonly List<IDisposable> _subs = new List<IDisposable>();
        private GameObject _employee;
        private Animator _employeeAnimator;
        private float _nextRefresh;

        private Vector3 Door => transform.position + new Vector3(0, 0, 4.6f);
        private Vector3 Outside => transform.position + new Vector3(0, 0, 8f);
        private Vector3 CounterPos => transform.position + new Vector3(-3.4f, 0, 1.6f);
        private Vector3 QueueStart => CounterPos + new Vector3(1.5f, 0, -0.6f);

        private void Start()
        {
            var visuals = GameRoot.Instance.Visuals;
            GameObject counter;
            if (visuals != null && visuals.cashRegister != null)
            {
                counter = Instantiate(visuals.cashRegister, transform);
                counter.transform.position = CounterPos;
                counter.transform.rotation = Quaternion.Euler(0, 90f, 0);
                if (counter.GetComponentInChildren<Collider>() == null)
                {
                    var box = counter.AddComponent<BoxCollider>();
                    box.center = new Vector3(0, 0.5f, 0);
                    box.size = new Vector3(1.8f, 1f, 1f);
                }
            }
            else
            {
                counter = GrayboxMaterials.Primitive(PrimitiveType.Cube, "Counter", transform, new Color(0.55f, 0.4f, 0.25f), keepCollider: true);
                counter.transform.position = CounterPos + Vector3.up * 0.5f;
                counter.transform.localScale = new Vector3(1.6f, 1f, 1f);
            }
            counter.AddComponent<CounterView>();

            var events = GameRoot.Instance.Events;
            _subs.Add(events.Subscribe<ShelfChanged>(_ => RebuildShelves()));
            _subs.Add(events.Subscribe<CustomerPickedItem>(OnPicked));
            _subs.Add(events.Subscribe<CustomerItemFailed>(OnItemFailed));
            _subs.Add(events.Subscribe<ItemScanned>(OnScanned));
            _subs.Add(events.Subscribe<CustomerLeft>(OnCustomerLeft));
            RebuildShelves();

            var area = FindFirstObjectByType<AreaSwitcher>();
            if (area != null)
            {
                var c = transform.position;
                area.SetBounds(GameArea.Store, Rect.MinMaxRect(c.x - 3, c.z - 7, c.x + 3, c.z + 1));
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
            return transform.position + new Vector3(col * spacingX - width * 0.5f + 0.6f, 0, -0.6f - row * spacingZ);
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
                view.Build(i, shelves[i].FurnitureId, category);
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
                SyncEmployee();
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
                    view.Spawn(c.Id, c.TypeId, Outside);
                    _customers[c.Id] = view;
                }

                int queuePos = StoreRules.QueuePosition(store, c);
                if (queuePos >= 0)
                {
                    view.Target = QueueStart + new Vector3(0.15f * (queuePos % 2), 0, -0.95f * queuePos);
                    float max = root.Catalog.TryGetCustomerType(c.TypeId, out var t) ? t.PatienceSeconds : 60f;
                    view.SetPatience((float)(c.PatienceSeconds / max));
                }
                else
                {
                    int shelf = _lastShelfOfCustomer.TryGetValue(c.Id, out int s) ? s : (c.Id * 7 + c.NextItem) % Math.Max(1, _shelves.Count);
                    view.Target = ShelfPosition(shelf) + new Vector3(((c.Id % 3) - 1) * 0.7f, 0, -1.1f);
                    view.SetPatience(-1);
                }
            }
        }

        private void SyncEmployee()
        {
            var root = GameRoot.Instance;
            bool hasCashier = false;
            foreach (var e in root.State.Staff.Employees)
                if (e.Active && root.Catalog.TryGetStaff(e.TypeId, out var def) && def.Role == StaffRole.Cashier) hasCashier = true;
            if (hasCashier == (_employee != null)) return;

            if (!hasCashier) { Destroy(_employee); _employee = null; return; }
            var visuals = root.Visuals;
            if (visuals == null || visuals.employee == null) return;
            _employee = Instantiate(visuals.employee, transform);
            _employee.transform.position = CounterPos + new Vector3(-1.0f, 0, 0);
            _employee.transform.rotation = Quaternion.Euler(0, 90f, 0);
            _employeeAnimator = _employee.GetComponentInChildren<Animator>();
        }

        private void OnPicked(CustomerPickedItem e)
        {
            _lastShelfOfCustomer[e.CustomerId] = e.ShelfIndex;
            if (_customers.TryGetValue(e.CustomerId, out var view)) view.Trigger("pick");
        }

        private void OnItemFailed(CustomerItemFailed e)
        {
            if (!_customers.TryGetValue(e.CustomerId, out var view)) return;
            view.Trigger("no");
            if (e.Reason == ItemFailReason.Price)
                FloatingTextLayer.Show(view.transform.position, "Caro demais!", new Color(1f, 0.55f, 0.45f));
            else if (GameRoot.Instance.Catalog.TryGetProduct(e.ProductId, out var p))
                FloatingTextLayer.Show(view.transform.position, "Sem " + p.Name + "?", new Color(1f, 0.85f, 0.5f));
        }

        private void OnScanned(ItemScanned e)
        {
            if (_employeeAnimator != null) _employeeAnimator.SetTrigger("pick");
            FxPlayer.Play("coins", CounterPos + Vector3.up * 1.0f);
        }

        private void OnCustomerLeft(CustomerLeft e)
        {
            _lastShelfOfCustomer.Remove(e.CustomerId);
            if (!_customers.TryGetValue(e.CustomerId, out var view)) return;
            _customers.Remove(e.CustomerId);
            bool happy = e.Reason == LeaveReason.Paid;
            if (e.Paid > 0)
            {
                FloatingTextLayer.Show(CounterPos, "+" + DisplayFormat.Coins(e.Paid), new Color(1f, 0.88f, 0.3f));
                FxPlayer.Play("coins", CounterPos + Vector3.up * 1.2f);
            }
            if (e.Reason == LeaveReason.OutOfPatience)
                FloatingTextLayer.Show(view.transform.position, "Demorou demais!", new Color(1f, 0.5f, 0.45f));
            view.Leave(Outside, happy);
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
