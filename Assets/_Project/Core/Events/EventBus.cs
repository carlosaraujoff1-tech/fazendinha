using System;
using System.Collections.Generic;

namespace Game.Core.Events
{
    /// <summary>Marcador de eventos do domínio. A apresentação escuta; o domínio nunca depende de quem escuta.</summary>
    public interface IGameEvent { }

    public interface IEventBus
    {
        void Publish<T>(T gameEvent) where T : IGameEvent;
        IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent;
    }

    /// <summary>Barramento síncrono simples. Assinantes podem se desinscrever durante a publicação.</summary>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        public void Publish<T>(T gameEvent) where T : IGameEvent
        {
            if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0) return;
            // Cópia para tolerar inscrição/desinscrição durante o loop.
            var snapshot = list.ToArray();
            foreach (var d in snapshot) ((Action<T>)d)(gameEvent);
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _handlers[typeof(T)] = list;
            }
            list.Add(handler);
            return new Subscription(() => list.Remove(handler));
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;
            public Subscription(Action dispose) { _dispose = dispose; }
            public void Dispose() { _dispose?.Invoke(); _dispose = null; }
        }
    }

    /// <summary>Barramento que descarta tudo (simulador headless, quando ninguém escuta).</summary>
    public sealed class NullEventBus : IEventBus
    {
        public static readonly NullEventBus Instance = new NullEventBus();
        public void Publish<T>(T gameEvent) where T : IGameEvent { }
        public IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }
}
