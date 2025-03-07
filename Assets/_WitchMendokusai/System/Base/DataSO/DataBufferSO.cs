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
		[field: NonSerialized] public List<T> Datas { get; protected set; } = new();

		[field: NonSerialized] public List<IUI> UIs { get; private set; } = new();

		public virtual void Add(T t)
		{
			Datas.Add(t);
			UpdateUI();
		}

		public virtual bool Remove(T t)
		{
			bool removeResult = Datas.Remove(t);
			UpdateUI();
			return removeResult;
		}

		public virtual void Clear()
		{
			Datas.Clear();
			UpdateUI();
		}

		public void RegisterUI(IUI ui) => UIs.Add(ui);

		public void UpdateUI() => UIs.ForEach(ui => ui.UpdateUI());

		public virtual void OnAfterDeserialize()
		{
			if (InitItems != null && InitItems.Count > 0)
				Datas = InitItems.ToList();
			else
				Datas = new();
		}
		public virtual void OnBeforeSerialize() { }
	}
}