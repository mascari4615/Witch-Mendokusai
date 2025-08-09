using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

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
	}
}