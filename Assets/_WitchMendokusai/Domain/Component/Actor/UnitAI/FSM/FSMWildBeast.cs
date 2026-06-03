using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 마계 야수(약한 사냥감)의 자율 brain (TASK-WM-182, 생태 트랙③).
	/// <see cref="FSMWisp"/> 의 거리 전환을 뒤집은 형태 — 플레이어가 <see cref="fleeRange"/> 안으로
	/// 들어오면 도주(<see cref="BT_Flee"/>), 멀어지면 배회(<see cref="BT_Idle"/>).
	/// 추격하지 않으므로 cozy 마을 동물과 달리 *사냥 대상*이지만 능동 위협은 아니다.
	/// </summary>
	public class FSMWildBeast : FSM<FSMStateCommon>
	{
		[SerializeField] private float fleeRange = 12f;
		[SerializeField] private bool isSpriteLookLeft = false;

		private PlayerProvider playerProvider;
		private BT_Idle idle;
		private BT_Flee flee;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		protected override FSMStateCommon DefaultState => FSMStateCommon.Idle;

		protected override void InitFSMEvent()
		{
			idle = new(UnitObject, isSpriteLookLeft: isSpriteLookLeft);
			flee = new(UnitObject, playerProvider, isSpriteLookLeft);

			SetStateEvent(FSMStateCommon.Idle, StateEvent.Update, () =>
			{
				UpdateFleeTransition();
				idle.UpdateBT();
			});

			SetStateEvent(FSMStateCommon.Flee, StateEvent.Update, () =>
			{
				UpdateFleeTransition();
				flee.UpdateBT();
			});
		}

		/// <summary> 플레이어가 fleeRange 안이면 도주, 밖이면 배회. (FSMWisp.CanSeePlayer 의 역) </summary>
		private void UpdateFleeTransition()
		{
			float distance = Vector3.Distance(UnitObject.transform.position, playerProvider.Current.transform.position);
			if (distance < fleeRange)
			{
				if (IsCurState(FSMStateCommon.Flee) == false)
					ChangeState(FSMStateCommon.Flee);
			}
			else
			{
				if (IsCurState(FSMStateCommon.Idle) == false)
					ChangeState(FSMStateCommon.Idle);
			}
		}
	}
}
