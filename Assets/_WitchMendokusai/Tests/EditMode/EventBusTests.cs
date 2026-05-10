using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	[TestFixture]
	public class EventBusTests
	{
		private TestEventBus bus;

		[SetUp]
		public void SetUp()
		{
			bus = new TestEventBus();
		}

		[Test]
		public void Subscribe_Publish_HandlerInvoked()
		{
			bool called = false;
			bus.Subscribe<TestEvent>(_ => called = true);
			bus.Publish(new TestEvent());
			Assert.IsTrue(called);
		}

		[Test]
		public void Unsubscribe_Publish_HandlerNotInvoked()
		{
			bool called = false;
			Action<TestEvent> handler = _ => called = true;
			bus.Subscribe<TestEvent>(handler);
			bus.Unsubscribe<TestEvent>(handler);
			bus.Publish(new TestEvent());
			Assert.IsFalse(called);
		}

		[Test]
		public void Publish_MultipleSubscribers_AllInvoked()
		{
			int count = 0;
			bus.Subscribe<TestEvent>(_ => count++);
			bus.Subscribe<TestEvent>(_ => count++);
			bus.Publish(new TestEvent());
			Assert.AreEqual(2, count);
		}

		[Test]
		public void Publish_NoSubscribers_DoesNotThrow()
		{
			Assert.DoesNotThrow(() => bus.Publish(new TestEvent()));
		}

		[Test]
		public void Publish_EventDataPassedToHandler()
		{
			TestEvent received = null;
			TestEvent sent = new TestEvent(42);
			bus.Subscribe<TestEvent>(evt => received = evt);
			bus.Publish(sent);
			Assert.AreEqual(42, received.Value);
		}

		[Test]
		public void Unsubscribe_NonRegisteredHandler_DoesNotThrow()
		{
			Action<TestEvent> handler = _ => { };
			Assert.DoesNotThrow(() => bus.Unsubscribe<TestEvent>(handler));
		}

		private class TestEvent : IEvent
		{
			public int Value { get; }
			public TestEvent(int value = 0) => Value = value;
		}

		private class TestEventBus : IEventBus
		{
			private readonly Dictionary<Type, List<Delegate>> handlers = new Dictionary<Type, List<Delegate>>();

			public void Subscribe<T>(Action<T> handler) where T : IEvent
			{
				Type type = typeof(T);
				if (handlers.ContainsKey(type) == false)
					handlers[type] = new List<Delegate>();
				handlers[type].Add(handler);
			}

			public void Unsubscribe<T>(Action<T> handler) where T : IEvent
			{
				Type type = typeof(T);
				if (handlers.ContainsKey(type) == false)
					return;
				handlers[type].Remove(handler);
			}

			public void Publish<T>(T evt) where T : IEvent
			{
				Type type = typeof(T);
				if (handlers.TryGetValue(type, out List<Delegate> list) == false)
					return;
				for (int i = 0; i < list.Count; i++)
					((Action<T>)list[i]).Invoke(evt);
			}

			public void ClearSticky<T>() where T : IEvent { }
		}
	}
}
