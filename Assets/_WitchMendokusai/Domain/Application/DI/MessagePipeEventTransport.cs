using System;
using MessagePipe;

namespace WitchMendokusai
{
	/// <summary>
	/// Unity 호스트의 이벤트 배달 통로. VContainer 가 세운 <c>GlobalMessagePipe</c> 그대로
	/// RootLifetimeScope 가 provider 를 심은 직후 <see cref="EventBusBridge.UseTransport"/> 로 연결
	/// 판정 층(DomainSDK) 은 이 클래스를 모름. 엔진 밖 기본값은 InMemoryEventTransport
	/// </summary>
	public sealed class MessagePipeEventTransport : IEventTransport
	{
		public void Publish<T>(T evt)
		{
			GlobalMessagePipe.GetPublisher<T>().Publish(evt);
		}

		public IDisposable Subscribe<T>(Action<T> handler)
		{
			ISubscriber<T> subscriber = GlobalMessagePipe.GetSubscriber<T>();
			return subscriber.Subscribe(handler);
		}
	}
}
