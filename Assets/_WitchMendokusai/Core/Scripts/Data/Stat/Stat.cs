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

		private bool dontInvokeEvents = false;

		public Stat()
		{
			foreach (T statType in Enum.GetValues(typeof(T)))
			{
				stats[statType] = 0;

				events[statType] = () => { };
				valueReturnEvents[statType] = (value) => { };
			}
		
			InitValue();
		}

		public void Init()
		{
			dontInvokeEvents = true;
			InitValue();
			dontInvokeEvents = false;
		}

		protected abstract void InitValue();

		public virtual void Set(Stat<T> newStats)
		{
			Init();
			foreach ((T stat, int value) in newStats.stats)
				stats[stat] = value;
		}

		public void Add(Stat<T> addStats)
		{
			foreach ((T stat, int value) in addStats.stats)
				this[stat] += value;
		}

		public int this[T statType]
		{
			get
			{
				return stats[statType];
			}
			set
			{
				stats[statType] = value;

				if (dontInvokeEvents)
					return;

				valueReturnEvents[statType].Invoke(stats[statType]);
				events[statType].Invoke();
			}
		}

		public void AddListener(T statType, Action<int> action)
		{
			valueReturnEvents[statType] += action;
		}

		public void AddListener(T statType, Action action)
		{
			events[statType] += action;
		}

		public void RemoveListener(T statType, Action<int> action)
		{
			valueReturnEvents[statType] -= action;
		}

		public void RemoveListener(T statType, Action action)
		{
			events[statType] -= action;
		}
	}
}