using System;
using UnityEngine;

namespace WitchMendokusai
{
	public enum FSMType
	{
		Slime = 0,
		Wisp = 1,
		NPC = 2,
		SlimeKing = 3
	}

	[RequireComponent(typeof(UnitObject))]
	public class StateMachine : MonoBehaviour
	{
		[SerializeField] private FSMType fsmType = FSMType.Slime;
		private IFSM fsm;

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
				FSMType.SlimeKing => new FSMSlimeKing(unitObject),
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