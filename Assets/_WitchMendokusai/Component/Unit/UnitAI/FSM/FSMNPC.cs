using System;
using UnityEngine;

namespace WitchMendokusai
{
	public class FSMNPC : FSM
	{
		public FSMNPC(UnitObject unitObject) : base(unitObject) {}
		protected override FSMState DefaultState => FSMState.Idle;

		protected override void Init() {}
	}
}