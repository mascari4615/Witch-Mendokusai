using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	public abstract class DataBufferSO<T> : DataSO, ISerializationCallbackReceiver
	{
		[field: SerializeField] public List<T> InitItems { get; private set; }
		[field: NonSerialized] public List<T> Data { get; protected set; } = new();

		[field: NonSerialized] public List<UIBase> UIs { get; private set; } = new();

		public virtual void Add(T t)
		{
			Data.Add(t);
			UpdateUI();
		}

		public virtual bool Remove(T t)
		{
			bool removeResult = Data.Remove(t);
			UpdateUI();
			return removeResult;
		}

		public virtual void Clear()
		{
			Data.Clear();
			UpdateUI();
		}

		public void RegisterUI(UIBase ui) => UIs.Add(ui);

		public void UpdateUI() => UIs.ForEach(ui => ui.UpdateUI());

		public virtual void OnAfterDeserialize()
		{
			if (InitItems != null && InitItems.Count > 0)
				Data = InitItems.ToList();
			else
				Data = new();
		}
		public virtual void OnBeforeSerialize() { }
	}
}