using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 프로세스 안에서만 도는 기본 배달 통로 (TASK-WM-214).
	/// 엔진 밖(헤드리스 서버 · 순수 테스트)에서 <see cref="EventBusBridge"/> 의 기본값으로 쓰인다.
	/// Unity 안에서는 MessagePipe 통로가 대신 꽂히므로 이 구현은 타지 않는다.
	///
	/// 발행 중에 구독이 바뀌어도 안전하도록 스냅샷을 떠서 돈다(구독자가 자기 구독을 끊는 흔한 패턴).
	/// </summary>
	public sealed class InMemoryEventTransport : IEventTransport
	{
		private readonly Dictionary<Type, List<Delegate>> handlers = new Dictionary<Type, List<Delegate>>();

		public void Publish<T>(T evt)
		{
			Delegate[] snapshot;
			lock (handlers)
			{
				if (handlers.TryGetValue(typeof(T), out List<Delegate> list) == false)
				{
					return;
				}

				snapshot = list.ToArray();
			}

			for (int i = 0; i < snapshot.Length; i++)
			{
				((Action<T>)snapshot[i]).Invoke(evt);
			}
		}

		public IDisposable Subscribe<T>(Action<T> handler)
		{
			lock (handlers)
			{
				if (handlers.TryGetValue(typeof(T), out List<Delegate> list) == false)
				{
					list = new List<Delegate>();
					handlers[typeof(T)] = list;
				}

				list.Add(handler);
			}

			return new Subscription(this, typeof(T), handler);
		}

		private void Remove(Type eventType, Delegate handler)
		{
			lock (handlers)
			{
				if (handlers.TryGetValue(eventType, out List<Delegate> list) == false)
				{
					return;
				}

				list.Remove(handler);
			}
		}

		private sealed class Subscription : IDisposable
		{
			private readonly InMemoryEventTransport owner;
			private readonly Type eventType;
			private readonly Delegate handler;
			private bool disposed;

			public Subscription(InMemoryEventTransport owner, Type eventType, Delegate handler)
			{
				this.owner = owner;
				this.eventType = eventType;
				this.handler = handler;
			}

			public void Dispose()
			{
				if (disposed)
				{
					return;
				}

				disposed = true;
				owner.Remove(eventType, handler);
			}
		}
	}
}
