using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	public class EventBus : Singleton<EventBus>
	{
		private readonly Dictionary<Type, List<Delegate>> handlers = new();

		public void Subscribe<T>(Action<T> handler) where T : struct
		{
			Type type = typeof(T);
			if (handlers.ContainsKey(type) == false)
				handlers[type] = new List<Delegate>();
			handlers[type].Add(handler);
		}

		public void Unsubscribe<T>(Action<T> handler) where T : struct
		{
			Type type = typeof(T);
			if (handlers.ContainsKey(type) == false)
				return;
			handlers[type].Remove(handler);
		}

		public void Publish<T>(T evt) where T : struct
		{
			Type type = typeof(T);
			if (handlers.TryGetValue(type, out List<Delegate> list) == false)
				return;

			for (int i = 0; i < list.Count; i++)
				((Action<T>)list[i]).Invoke(evt);
		}
	}
}
