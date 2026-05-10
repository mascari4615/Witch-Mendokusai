using System;

namespace WitchMendokusai
{
	public interface IGameEventBridge
	{
		void Raise(GameEventType gameEventType);
		void RegisterCallback(GameEventType gameEventType, Action action);
		void UnregisterCallback(GameEventType gameEventType, Action action);
	}
}
