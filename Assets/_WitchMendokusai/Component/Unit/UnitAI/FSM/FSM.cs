using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public abstract class FSM : IDisposable
	{
		private const float TICK = 0.3f;

		protected FSMState currentState;
		private readonly Dictionary<(FSMState, StateEvent), Action> stateEventDic = new();
		private Coroutine coroutine;

		protected abstract FSMState DefaultState { get; }
		protected UnitObject UnitObject { get; private set; }
		protected Transform Transform => UnitObject.transform;

		public FSM(UnitObject unitObject)
		{
			Debug.Assert(unitObject != null, "unitObject cannot be null");

			UnitObject = unitObject;
			stateEventDic.Clear();

			Init();

			ChangeState(DefaultState);
		}

		public void Dispose()
		{
			if (coroutine != null)
				UnitObject.StopCoroutine(coroutine);
			stateEventDic.Clear();
			currentState = DefaultState;
			UnitObject = null;
		}

		// ~FSM()
		// {
		// 	UnitObject.StopCoroutine(coroutine);
		// }

		public bool IsCurState(FSMState state) => currentState.Equals(state);

		/// <summary>
		/// 상태머신을 쓰기 전에 초기화를 해주는 함수.
		/// OnEnable에서 호출됩니다.
		/// </summary>
		protected abstract void Init();

		public void SetStateEvent(FSMState state, StateEvent stateEvent, Action action)
		{
			Debug.Assert(action != null, "action cannot be null");
			stateEventDic[(state, stateEvent)] = action;
		}

		public void ChangeState(FSMState newState)
		{
			if (stateEventDic.ContainsKey((currentState, StateEvent.Exit)))
				stateEventDic[(currentState, StateEvent.Exit)]?.Invoke();
			currentState = newState;
			if (stateEventDic.ContainsKey((currentState, StateEvent.Enter)))
				stateEventDic[(currentState, StateEvent.Enter)]?.Invoke();

			if (coroutine != null)
				UnitObject.StopCoroutine(coroutine);
			coroutine = UnitObject.StartCoroutine(StateLoop());
		}

		private IEnumerator StateLoop()
		{
			WaitForSeconds waitForTick = new(TICK);
			while (true)
			{
				if (stateEventDic.ContainsKey((currentState, StateEvent.Update)))
					stateEventDic[(currentState, StateEvent.Update)]?.Invoke();
				yield return waitForTick;
				// yield return null;
			}
		}
	}
}