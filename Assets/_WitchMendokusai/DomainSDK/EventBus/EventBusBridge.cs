using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임 전체가 쓰는 이벤트 창구. 호출부 API 는 예전 그대로다 —
	/// 바뀐 것은 <b>배달을 직접 하지 않고</b> <see cref="IEventTransport"/> 에 맡긴다는 점뿐 (TASK-WM-214).
	///
	/// Unity 안 기본값 = MessagePipe(기존과 동일 경로). 엔진 밖 기본값 = <see cref="InMemoryEventTransport"/>.
	/// 서버·테스트는 <see cref="UseTransport"/> 로 자기 통로를 꽂는다.
	/// </summary>
	public static class EventBusBridge
	{
		private static readonly Dictionary<(Type, Delegate), IDisposable> subscriptions = new();
		private static IEventTransport transport = CreateDefaultTransport();

		/// <summary>
		/// 배달 통로를 갈아끼운다. 이미 걸린 구독은 <b>옛 통로에 남는다</b> —
		/// 호스트 부팅 시점(구독 0)에 한 번만 부르는 것이 규약.
		/// </summary>
		public static void UseTransport(IEventTransport newTransport)
		{
			transport = newTransport;
		}

		public static void Subscribe<T>(Action<T> handler)
		{
			IDisposable sub = transport.Subscribe(handler);
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
			transport.Publish(evt);
		}

		private static IEventTransport CreateDefaultTransport()
		{
			return new InMemoryEventTransport();
		}

	}
}
