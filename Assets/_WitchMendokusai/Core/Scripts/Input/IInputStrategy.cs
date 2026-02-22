using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	public readonly struct InputRegisterData
	{
		public InputEventType InputEventType { get; }
		public InputEventResponseType InputEventResponseType { get; }
		public Action Callback { get; }

		public InputRegisterData(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action callback)
		{
			InputEventType = inputEventType;
			InputEventResponseType = inputEventResponseType;
			Callback = callback;
		}
	}

	public interface IInputStrategy
	{
		List<InputRegisterData> InputRegisterDataList { get; }
	
		bool TryGetEventReturnConditions(InputEventType eventType, out GameConditionType[] conditions);
		bool TryGetAxisReturnConditions(InputAxisType axisType, out GameConditionType[] conditions);
	}

	public abstract class InputStrategyBase : IInputStrategy
	{
		public abstract List<InputRegisterData> InputRegisterDataList { get; }
	
		protected abstract Dictionary<InputEventType, GameConditionType[]> EventReturnConditions { get; }
		protected abstract Dictionary<InputAxisType, GameConditionType[]> AxisReturnConditions { get; }

		public bool TryGetEventReturnConditions(InputEventType eventType, out GameConditionType[] conditions)
		{
			return EventReturnConditions.TryGetValue(eventType, out conditions);
		}
	
		public bool TryGetAxisReturnConditions(InputAxisType axisType, out GameConditionType[] conditions)
		{
			return AxisReturnConditions.TryGetValue(axisType, out conditions);
		}
	}
}