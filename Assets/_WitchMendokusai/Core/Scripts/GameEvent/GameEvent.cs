using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "GameEvent", menuName = "WM/GameSystem/GameEvent")]
	public class GameEvent : ScriptableObject
	{
		[NonSerialized] private readonly List<GameEventListener> _listeners = new();
		private event Action Callback;

		public void Raise()
		{
			for (int i = _listeners.Count - 1; i >= 0; i--)
				_listeners[i].OnEventRaised();
			Callback?.Invoke();
		}

		public void RegisterListener(GameEventListener listener) { _listeners.Add(listener); }
		public void UnregisterListener(GameEventListener listener) { _listeners.Remove(listener); }
		public void AddCallback(Action a) { Callback += a; }
		public void RemoveCallback(Action a) { Callback -= a; }
	}
}