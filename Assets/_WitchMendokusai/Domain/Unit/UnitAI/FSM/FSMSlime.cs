using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class FSMSlime : FSM<FSMStateCommon>
	{
#pragma warning disable CS0414 // FSM 공격 범위 미구현 예약 필드
		[SerializeField] private float attackRange = 10f;
#pragma warning restore CS0414
		[SerializeField] private bool isSpriteLookLeft = false;

		private PlayerProvider playerProvider;
		private BT_Idle idle;
		private BT_MoveToPlayer moveToPlayer;
		private MonsterAttack activeAttack;
		private BTRunner attackBehaviour;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		protected override FSMStateCommon DefaultState => FSMStateCommon.Attack;

		protected override void InitFSMEvent()
		{
			idle = new(UnitObject, isSpriteLookLeft: isSpriteLookLeft);
			moveToPlayer = new(UnitObject, playerProvider, isSpriteLookLeft);

			SetStateEvent(FSMStateCommon.Idle, StateEvent.Update, () =>
			{
				idle.UpdateBT();
			});

			SetStateEvent(FSMStateCommon.Attack, StateEvent.Update, () =>
			{
				// 몬스터 자산에 공격 (Monster.Attack) 이 있으면 그 박자, 없으면 추적만. 같은 프리팹을 여러 몬스터 자산이 나눠 써서 매번 확인
				MonsterAttack attack = UnitObject.UnitData is Monster monster ? monster.Attack : null;
				if (attack == null)
				{
					moveToPlayer.UpdateBT();
					return;
				}
				if (attack != activeAttack)
				{
					InterruptAttack();
					activeAttack = attack;
					attackBehaviour = attack.CreateBehaviour(UnitObject, playerProvider, isSpriteLookLeft);
				}
				attackBehaviour.UpdateBT();
			});
		}

		// 풀 반납, 사망, 패턴 이동으로 꺼지면 예고를 치우고 행동을 버림. 다음 사용은 새 행동 트리
		protected override void OnFSMDisabled() => InterruptAttack();

		// 재배치 (MonsterSpawner 반납 정책) 처럼 켜진 채 자리가 바뀔 때도. 다음 틱에 새 행동 트리
		public void InterruptAttack()
		{
			if (activeAttack != null)
				activeAttack.Interrupt(attackBehaviour);
			activeAttack = null;
			attackBehaviour = null;
		}
	}
}