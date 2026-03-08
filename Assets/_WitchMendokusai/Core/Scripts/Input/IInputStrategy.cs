using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	public readonly struct InputRegisterData
	{
		public InputEventType InputEventType { get; }
		public InputEventResponseType InputEventResponseType { get; }
		public Action Callback { get; }
		public Func<bool> Condition { get; }

		public InputRegisterData(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action callback, Func<bool> condition)
		{
			InputEventType = inputEventType;
			InputEventResponseType = inputEventResponseType;
			Callback = callback;
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
			return TryGetEventReturnConditions(eventType, out var conditions) == false || GameManager.Instance.Conditions.IsGameConditionAny(conditions) == false;
		}
	
		public bool TryGetAxisReturnConditions(InputAxisType axisType, out GameConditionType[] conditions)
		{
			return AxisReturnConditions.TryGetValue(axisType, out conditions);
		}
	}
}