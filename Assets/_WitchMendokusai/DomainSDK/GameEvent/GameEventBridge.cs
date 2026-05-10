using System;

namespace WitchMendokusai
{
	public static class GameEventBridge
	{
		private static IGameEventBridge instance;

		public static void Register(IGameEventBridge bridge)
		{
			instance = bridge;
		}

		public static void Raise(GameEventType gameEventType)
		{
			instance.Raise(gameEventType);
		}

		public static void RegisterCallback(GameEventType gameEventType, Action action)
		{
			instance.RegisterCallback(gameEventType, action);
		}

		public static void UnregisterCallback(GameEventType gameEventType, Action action)
		{
			instance.UnregisterCallback(gameEventType, action);
		}
	}
}
