using System;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.WMHelper;

namespace WitchMendokusai
{
	public class GameEventManager : MonoBehaviour, IGameEventBridge
	{
		public static GameEventManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out GameEventManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		public Dictionary<GameEventType, Action> Callback { get; } = new();

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			GameEventBridge.Register(this);
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
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
