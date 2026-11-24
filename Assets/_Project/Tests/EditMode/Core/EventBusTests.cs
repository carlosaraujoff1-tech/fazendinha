using System;
using System.Collections.Generic;
using Game.Core.Events;
using NUnit.Framework;

namespace Game.Tests.Core
{
    public class EventBusTests
    {
        private struct Ping : IGameEvent { public int Value; }
        private struct Pong : IGameEvent { }

        [Test]
        public void Publish_ReachesOnlySubscribersOfThatType()
        {
            var bus = new EventBus();
            var got = new List<int>();
            int pongs = 0;
            bus.Subscribe<Ping>(p => got.Add(p.Value));
            bus.Subscribe<Pong>(_ => pongs++);

            bus.Publish(new Ping { Value = 7 });

            CollectionAssert.AreEqual(new[] { 7 }, got);
            Assert.AreEqual(0, pongs);
        }

        [Test]
        public void Publish_WithoutSubscribers_DoesNothing()
        {
            Assert.DoesNotThrow(() => new EventBus().Publish(new Ping()));
        }

        [Test]
        public void Dispose_Unsubscribes_AndIsIdempotent()
        {
            var bus = new EventBus();
            int count = 0;
            var sub = bus.Subscribe<Ping>(_ => count++);
            bus.Publish(new Ping());
            sub.Dispose();
            sub.Dispose();
            bus.Publish(new Ping());
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Unsubscribing_DuringPublish_IsSafe()
        {
            var bus = new EventBus();
            int second = 0;
            IDisposable first = null;
            first = bus.Subscribe<Ping>(_ => first.Dispose());
            bus.Subscribe<Ping>(_ => second++);

            bus.Publish(new Ping());
            bus.Publish(new Ping());

            Assert.AreEqual(2, second);
        }

        [Test]
        public void Subscribe_NullHandler_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new EventBus().Subscribe<Ping>(null));
        }

        [Test]
        public void NullEventBus_IgnoresEverything()
        {
            var bus = NullEventBus.Instance;
            int count = 0;
            using (bus.Subscribe<Ping>(_ => count++)) bus.Publish(new Ping());
            Assert.AreEqual(0, count);
        }
    }
}
