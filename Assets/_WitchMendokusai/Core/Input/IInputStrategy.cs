using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace WitchMendokusai
{
	public readonly struct InputRegisterData
	{
		public InputEventType InputEventType { get; }
		public InputEventResponseType InputEventResponseType { get; }
		public Action Callback { get; }
		public Action<InputAction.CallbackContext> CallbackWithContext { get; }
		public Func<bool> Condition { get; }

		public InputRegisterData(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action callback, Func<bool> condition = null)
		{
			InputEventType = inputEventType;
			InputEventResponseType = inputEventResponseType;
			Callback = callback;
			CallbackWithContext = null;
			Condition = condition;
		}

		public InputRegisterData(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action<InputAction.CallbackContext> callbackWithContext, Func<bool> condition = null)
		{
			InputEventType = inputEventType;
			InputEventResponseType = inputEventResponseType;
			Callback = null;
			CallbackWithContext = callbackWithContext;
			Condition = condition;
		}
	}

	public interface IInputStrategy
	{
		List<InputRegisterData> InputRegisterDataList { get; }
	
		// bool TryGetEventReturnConditions(InputEventType eventType, out GameConditionType[] conditions);
		bool TryGetAxisReturnConditions(InputAxisType axisType, out GameConditionType[] conditions);
	}

	public abstract class InputStrategyBase : IInputStrategy
	{
		public abstract List<InputRegisterData> InputRegisterDataList { get; }
	
		protected abstract Dictionary<InputEventType, GameConditionType[]> EventReturnConditions { get; }
		protected abstract Dictionary<InputAxisType, GameConditionType[]> AxisReturnConditions { get; }

		protected bool TryGetEventReturnConditions(InputEventType eventType, out GameConditionType[] conditions)
		{
			return EventReturnConditions.TryGetValue(eventType, out conditions);
		}

		protected bool CanExecute(InputEventType eventType)
		{
			return TryGetEventReturnConditions(eventType, out GameConditionType[] conditions) == false || GameConditionBridge.IsGameConditionAny(conditions) == false;
		}
	
		public bool TryGetAxisReturnConditions(InputAxisType axisType, out GameConditionType[] conditions)
		{
			return AxisReturnConditions.TryGetValue(axisType, out conditions);
		}
	}
}