using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum FSMType
	{
		Slime = 0,
		Wisp = 1,
		NPC = 2,
	}

	public enum FSMState
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

	[RequireComponent(typeof(UnitObject))]
	public class StateMachine : MonoBehaviour
	{
		[SerializeField] private FSMType fsmType = FSMType.Slime;
		private FSM fsm;

		private void OnEnable()
		{
			UnitObject unitObject = GetComponent<UnitObject>();
			if (unitObject == null)
			{
				Debug.LogError("UnitObject component is missing.");
				return;
			}

			fsm = fsmType switch
			{
				FSMType.Slime => new FSMSlime(unitObject),
				FSMType.Wisp => new FSMWisp(unitObject),
				FSMType.NPC => new FSMNPC(unitObject),
				_ => throw new ArgumentOutOfRangeException(),
			};
		}

		private void OnDisable()
		{
			fsm?.Dispose();
			fsm = null;
		}
	}
}