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
		Wait,

		// 마계 야수 도주 상태 (TASK-WM-182). append-only — 기존 FSM(Slime/Wisp/SlimeKing) 무영향.
		Flee
	}

	public enum StateEvent
	{
		Enter,
		Update,
		Exit
	}

	/// <summary>
	/// 유닛 자율 brain 의 비제네릭 마커 베이스. FSM&lt;T&gt; 가 상속.
	/// 아레나 등 외부 드라이버(TacticDriver)가 이동/행동을 권위적으로 구동하는 컨텍스트에서
	/// 구체 brain 타입을 enumerate 하지 않고 일괄 비활성(`GetComponents&lt;UnitBrain&gt;()` → enabled=false)하기 위한 seam.
	/// 새 brain 타입도 자동 격리(회귀 안전).
	/// </summary>
	public abstract class UnitBrain : MonoBehaviour { }

	[RequireComponent(typeof(UnitObject))]
	public abstract class FSM<T> : UnitBrain where T : Enum
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
				Debug.LogError($"[FSM] ({name}) UnitObject component is missing.");
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
				// Debug.LogWarning($"[FSM] ({name}) Already in state: {newState}");
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
				// Debug.Log($"[FSM] ({name}) Current State: {currentState}");
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