using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public interface IStickyEvent : IEvent
	{
	}

	public class EventBus : MonoBehaviour, IEventBus
	{
		private readonly Dictionary<Type, List<Delegate>> handlers = new();
		private readonly Dictionary<Type, object> stickyValues = new();

		private void Awake()
		{
			EventBusBridge.Register(this);
		}

		public void Subscribe<T>(Action<T> handler) where T : IEvent
		{
			Type type = typeof(T);
			if (handlers.ContainsKey(type) == false)
				handlers[type] = new List<Delegate>();
			handlers[type].Add(handler);

			if (typeof(IStickyEvent).IsAssignableFrom(type) == false)
				return;

			if (stickyValues.TryGetValue(type, out object stickyValue))
				handler.Invoke((T)stickyValue);
		}

		public void Unsubscribe<T>(Action<T> handler) where T : IEvent
		{
			Type type = typeof(T);
			if (handlers.ContainsKey(type) == false)
				return;
			handlers[type].Remove(handler);
		}

		public void Publish<T>(T evt) where T : IEvent
		{
			Type type = typeof(T);

			if (typeof(IStickyEvent).IsAssignableFrom(type))
				stickyValues[type] = evt;

			if (handlers.TryGetValue(type, out List<Delegate> list) == false)
				return;

			for (int i = 0; i < list.Count; i++)
				((Action<T>)list[i]).Invoke(evt);
		}

		public void ClearSticky<T>() where T : IEvent
		{
			stickyValues.Remove(typeof(T));
		}
	}
}
