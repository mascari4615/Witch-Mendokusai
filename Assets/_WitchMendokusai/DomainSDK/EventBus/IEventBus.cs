using System;

namespace WitchMendokusai
{
	public interface IEventBus
	{
		void Subscribe<T>(Action<T> handler) where T : IEvent;
		void Unsubscribe<T>(Action<T> handler) where T : IEvent;
		void Publish<T>(T evt) where T : IEvent;
		void ClearSticky<T>() where T : IEvent;
	}
}
