using System;
using System.Collections.Generic;
using static WitchMendokusai.WMHelper;

namespace WitchMendokusai
{
	public class GameEventManager : Singleton<GameEventManager>, IGameEventBridge
	{
		public Dictionary<GameEventType, Action> Callback { get; } = new();

		protected override void Awake()
		{
			base.Awake();
			GameEventBridge.Register(this);
		}

		public void Raise(GameEventType gameEventType)
		{
			if (IsPlaying == false)
				return;

			if (Callback.TryGetValue(gameEventType, out var action))
			{
				action?.Invoke();
			}
		}

		public void RegisterCallback(GameEventType gameEventType, Action action)
		{
			if (IsPlaying == false)
				return;

			if (Callback.ContainsKey(gameEventType))
			{
				Callback[gameEventType] += action;
			}
			else
			{
				Callback.Add(gameEventType, action);
			}
		}

		public void UnregisterCallback(GameEventType gameEventType, Action action)
		{
			if (IsPlaying == false)
				return;

			if (Callback.ContainsKey(gameEventType))
			{
				Callback[gameEventType] -= action;
			}
		}
	}
}