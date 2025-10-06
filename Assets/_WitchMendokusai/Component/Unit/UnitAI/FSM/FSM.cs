using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum FSMStateCommon
	{
		Idle,
		Attack
	}

	public enum StateEvent
	{
		Enter,
		Update,
		Exit
	}

	public interface IFSM : IDisposable
	{

	}

	public abstract class FSM<T> : IFSM where T : Enum
	{
		protected T currentState;
		protected abstract T DefaultState { get; }

		private readonly Dictionary<(T, StateEvent), Action> stateEventDic = new();
		private Coroutine coroutine;

		protected UnitObject UnitObject { get; private set; }

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
			UnitObject.StopCoroutine(coroutine);
			stateEventDic.Clear();
			currentState = DefaultState;
			UnitObject = null;
		}

		public bool IsCurState(T state) => currentState.Equals(state);

		/// <summary>
		/// 상태머신을 쓰기 전에 초기화를 해주는 함수.
		/// OnEnable에서 호출됩니다.
		/// </summary>
		protected abstract void Init();

		public void SetStateEvent(T state, StateEvent stateEvent, Action action)
		{
			stateEventDic[(state, stateEvent)] = action;
		}

		public void ChangeState(T newState)
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
			const float TICK = 0.3f;
			WaitForSeconds waitForTick = new(TICK);

			while (true)
			{
				if (stateEventDic.ContainsKey((currentState, StateEvent.Update)))
					stateEventDic[(currentState, StateEvent.Update)]?.Invoke();
				yield return waitForTick;
			}
		}
	}
}