using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public abstract class Stat<T> where T : Enum
	{
		protected readonly Dictionary<T, int> stats = new();
		protected readonly Dictionary<T, Action> events = new();
		protected readonly Dictionary<T, Action<int>> valueReturnEvents = new();

		public void InitAllZero()
		{
			foreach (T statType in Enum.GetValues(typeof(T)))
				stats[statType] = 0;
		}

		public virtual void Init(Stat<T> newStats)
		{
			stats.Clear();
			foreach (var (stat, value) in newStats.stats)
				stats[stat] = value;
		}

		public void Add(Stat<T> addStats)
		{
			foreach (var (stat, value) in addStats.stats)
				this[stat] += value;
		}

		public int this[T statType]
		{
			get
			{
				if (stats.ContainsKey(statType) == false)
					stats[statType] = 0;

				return stats[statType];
			}
			set
			{
				stats[statType] = value;

				if (valueReturnEvents.ContainsKey(statType))
					valueReturnEvents[statType]?.Invoke(stats[statType]);

				if (events.ContainsKey(statType))
					events[statType]?.Invoke();
			}
		}

		public void AddListener(T statType, Action<int> action)
		{
			if (valueReturnEvents.ContainsKey(statType) == false)
				valueReturnEvents[statType] = action;
			else
				valueReturnEvents[statType] += action;
		}

		public void AddListener(T statType, Action action)
		{
			if (events.ContainsKey(statType) == false)
				events[statType] = action;
			else
				events[statType] += action;
		}

		public void RemoveListener(T statType, Action<int> action)
		{
			if (valueReturnEvents.ContainsKey(statType) == false)
				return;

			valueReturnEvents[statType] -= action;
		}
		
		public void RemoveListener(T statType, Action action)
		{
			if (events.ContainsKey(statType) == false)
				return;

			events[statType] -= action;
		}
	}
}