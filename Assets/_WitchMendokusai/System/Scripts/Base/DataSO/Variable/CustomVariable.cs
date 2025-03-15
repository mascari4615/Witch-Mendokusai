using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	public abstract class CustomVariable<T> : DataSO, ISerializationCallbackReceiver
	{
		[field: Header("_" + nameof(CustomVariable<T>))]
		[field: SerializeField] public T InitialValue { get; private set; }
		public T RuntimeValue
		{
			get => _runtimeValue;
			set
			{
				_runtimeValue = value;
				// GameEvent?.Raise();
				OnValueChanged?.Invoke();
			}
		}
		[NonSerialized] private T _runtimeValue;
		// [field: SerializeField] public GameEvent GameEvent { get; private set; }
		public Action OnValueChanged { get; set; }

		public void OnAfterDeserialize() { RuntimeValue = InitialValue; }
		public void OnBeforeSerialize() { }
	}
}