using System;
using System.Collections.Generic;
using Game.Core.Animals;
using Game.Core.State;
using Game.Presentation.Effects;
using Game.Presentation.Graybox;
using UnityEngine;

namespace Game.Presentation.Farm
{
    /// <summary>
    /// Animais do jogador: galinhas perto do galinheiro, vacas e ovelhas no pasto. Cada um mostra o estado:
    /// com fome (ícone da comida pulsando), produzindo (nada) ou pronto (produto girando + brilho).
    /// </summary>
    public sealed class AnimalsView : MonoBehaviour
    {
        [SerializeField] private Vector3 coopArea = new Vector3(-19f, 0f, 4.5f);
        [SerializeField] private Vector3 pastureArea = new Vector3(-21.5f, 0f, -7f);
        [SerializeField] private float refreshSeconds = 0.25f;

        private readonly List<AnimalView> _views = new List<AnimalView>();
        private readonly List<IDisposable> _subs = new List<IDisposable>();
        private float _nextRefresh;

        private void Start()
        {
            var events = GameRoot.Instance.Events;
            _subs.Add(events.Subscribe<AnimalBought>(_ => Rebuild()));
            _subs.Add(events.Subscribe<AnimalChanged>(e => Render(e.AnimalIndex)));
            Rebuild();
        }

        private void OnDestroy()
        {
            foreach (var s in _subs) s.Dispose();
        }

        public Vector3 AnimalPosition(int index) => index >= 0 && index < _views.Count ? _views[index].transform.position : transform.position;

        /// <summary>Posição de "casa" do animal: galinhas em volta do galinheiro, os outros no pasto.</summary>
        private Vector3 HomeOf(int index, string typeId)
        {
            int sameType = 0;
            var animals = GameRoot.Instance.State.Farm.Animals;
            for (int i = 0; i < index; i++) if (animals[i].TypeId == typeId) sameType++;
            if (typeId == "galinha")
            {
                float a = sameType * 1.1f;
                return coopArea + new Vector3(Mathf.Cos(a) * (1.2f + 0.25f * sameType), 0, -Mathf.Abs(Mathf.Sin(a)) * 1.3f - 0.6f);
            }
            int otherIndex = 0;
            for (int i = 0; i < index; i++) if (animals[i].TypeId != "galinha") otherIndex++;
            return pastureArea + new Vector3((otherIndex % 3) * 2.2f - 2.2f, 0, -(otherIndex / 3) * 2.4f + 1.2f);
        }

        private void Rebuild()
        {
            var animals = GameRoot.Instance.State.Farm.Animals;
            while (_views.Count < animals.Count)
            {
                int i = _views.Count;
                var go = new GameObject("Animal_" + i);
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<AnimalView>();
                view.Build(i, animals[i].TypeId, HomeOf(i, animals[i].TypeId));
                _views.Add(view);
            }
            for (int i = 0; i < _views.Count; i++) Render(i);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshSeconds;
            for (int i = 0; i < _views.Count; i++) Render(i);
        }

        private void Render(int index)
        {
            var root = GameRoot.Instance;
            var animals = root.State.Farm.Animals;
            if (index < 0 || index >= _views.Count || index >= animals.Count) return;
            _views[index].Render(animals[index], root.NowMs);
        }
    }
}
