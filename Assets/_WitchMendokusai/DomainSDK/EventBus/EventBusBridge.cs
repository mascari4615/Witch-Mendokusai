using System;

namespace WitchMendokusai
{
	public static class EventBusBridge
	{
		private static IEventBus instance;

		public static void Register(IEventBus bus)
		{
			instance = bus;
		}

		public static void Subscribe<T>(Action<T> handler) where T : IEvent
		{
			instance?.Subscribe(handler);
		}

		public static void Unsubscribe<T>(Action<T> handler) where T : IEvent
		{
			instance?.Unsubscribe(handler);
		}

		public static void Publish<T>(T evt) where T : IEvent
		{
			instance?.Publish(evt);
		}

		public static void ClearSticky<T>() where T : IEvent
		{
			instance?.ClearSticky<T>();
		}
	}
}
