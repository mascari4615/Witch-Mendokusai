using System;
using System.Collections.Generic;
using MessagePipe;

namespace WitchMendokusai
{
	public static class EventBusBridge
	{
		private static readonly Dictionary<(Type, Delegate), IDisposable> subscriptions = new();

		public static void Subscribe<T>(Action<T> handler)
		{
			ISubscriber<T> subscriber = GlobalMessagePipe.GetSubscriber<T>();
			IDisposable sub = subscriber.Subscribe(handler);
			subscriptions[(typeof(T), handler)] = sub;
		}

		public static void Unsubscribe<T>(Action<T> handler)
		{
			if (subscriptions.TryGetValue((typeof(T), handler), out IDisposable sub))
			{
				sub.Dispose();
				subscriptions.Remove((typeof(T), handler));
			}
		}

		public static void Publish<T>(T evt)
		{
			GlobalMessagePipe.GetPublisher<T>().Publish(evt);
		}
	}
}
