using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum FSMStateCommon
	{
		Idle,
		Attack,
		Wait
	}

	public enum StateEvent
	{
		Enter,
		Update,
		Exit
	}

	[RequireComponent(typeof(UnitObject))]
	public abstract class FSM<T> : MonoBehaviour where T : Enum
	{
		private readonly Dictionary<(T, StateEvent), Action> stateEvents = new();
		private Coroutine stateUpdateLoop;
		private T currentState;

		protected abstract T DefaultState { get; }
		protected UnitObject UnitObject;

		#region Init
		private void Awake() => Init();
		private void Init()
		{
			if (TryGetComponent(out UnitObject) == false)
			{
				Debug.LogError("UnitObject component is missing.");
				return;
			}
			stateEvents.Clear();
			InitFSMEvent();
		}

		/// <summary> StateEventDict 초기화 (`SetStateEvent(~)` 이용) </summary>
		protected abstract void InitFSMEvent();
		protected void SetStateEvent(T state, StateEvent stateEvent, Action action) =>
			stateEvents[(state, stateEvent)] = action;
		#endregion

		#region Start
		private void OnEnable() => StartFSM();
		private void StartFSM()
		{
			ChangeState(DefaultState);
			StopStateUpdateLoop(); // 중복 방지
			stateUpdateLoop = StartCoroutine(UpdateState());
		}
		#endregion

		#region Update
		protected void ChangeState(T newState)
		{
			if (IsCurState(newState))
			{
				Debug.LogWarning($"[FSM] Already in state: {newState}");
				// 일단 경고만
			}

			ExecuteEventIfAvailable(currentState, StateEvent.Exit);
			currentState = newState;
			ExecuteEventIfAvailable(currentState, StateEvent.Enter);
		}

		private IEnumerator UpdateState()
		{
			WaitForSeconds waitForTick = new(BTRunner.TICK);

			while (true)
			{
				Debug.Log($"[FSM] Current State: {currentState}");
				ExecuteEventIfAvailable(currentState, StateEvent.Update);
				yield return waitForTick;
			}
		}
		#endregion

		#region End
		private void OnDisable() => Dispose();
		private void Dispose()
		{
			StopStateUpdateLoop();

			// stateEvents.Clear();
			currentState = DefaultState;
			// UnitObject = null;
		}
		#endregion

		#region Utils
		protected bool IsCurState(T state) => currentState.Equals(state);

		private void ExecuteEventIfAvailable(T state, StateEvent stateEvent)
		{
			if (stateEvents.ContainsKey((state, stateEvent)))
				stateEvents[(state, stateEvent)]?.Invoke();
		}

		private void StopStateUpdateLoop()
		{
			if (stateUpdateLoop != null)
				StopCoroutine(stateUpdateLoop);
			stateUpdateLoop = null;
		}
		#endregion
	}
}